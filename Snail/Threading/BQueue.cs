#define BACKTRACKING
#define ADAPTIVE
#define ADAPT_ALWAYS
#define POWER2BUFFER
#define STATS
#define INLINE_EMPTY_ELEMENT

/*
* B-Queue -- An efficient and practical queueing for fast core-to-core
* communication
*
* Copyright (C) 2011 Junchang Wang <junchang.wang@gmail.com>
*
*
* This program is free software: you can redistribute it and/or modify
* it under the terms of the GNU General Public License as published by
* the Free Software Foundation, either version 3 of the License, or
* (at your option) any later version.
*
* This program is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU General Public License for more details.
*
* You should have received a copy of the GNU General Public License
* along with this program. If not, see <http://www.gnu.org/licenses/>.
*/




namespace Snail.Threading
{
	using System.Threading;
	using System;
	using System.Collections;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using Snail.Util;

	public interface ITranslator<T>
	{
		void Translate(int index, ref T value);
	}

	public delegate void RefAction<T1>(ref T1 t1);
	public delegate void OutAction<T1>(out T1 t1);

	public delegate T2 RefFunc<T1,T2>(ref T1 t1);

	public delegate void RefAction<in T1, T2>(T1 t1, ref T2 t2);
	
	public interface IBQueueElement<T>
	{
		void InitElement(ref T obj);
		void SetEmptyElement(ref T obj);
		bool IsNonEmptyElement(ref T obj);
	}

	public struct BQueueElement<T>:IBQueueElement<T>
	{
		private T _emptyElement;
		public BQueueElement(T emptyElement)
		{
			_emptyElement = emptyElement;
		}

		public void InitElement(ref T val)
		{
			
		}

		public bool IsNonEmptyElement(ref T val)
		{
			if (true)
			{
#if IL
				ldarg val
				ldobj !0
				ldarg.0
				ldfld !0 valuetype Snail.Threading.BQueueElement`1<!T>::_emptyElement
				sub
				ret
#endif
			}
			return true;
		}

		public void SetEmptyElement(ref T val)
		{
			val = _emptyElement;
		}
	}

	public class BQueue<T, THelper> : IProducerConsumerCollection<T> where THelper:IBQueueElement<T>,new()
	{
		public const int QUEUE_SIZE = 64*1024;

		private int CONS_BATCH_SIZE;
		private int PROD_BATCH_SIZE;
		private int BATCH_INCREMENT;

		private int _size;
		private int _mask;

		public int Mask
		{
			get { return _mask; }
		}
		
		private T[] _data;
		private int _batch_history;

	//#if STATS
		private Volatile.PaddedInteger _enqueueFulls = new Volatile.PaddedInteger(0);
		private Volatile.PaddedInteger _backtrackings = new Volatile.PaddedInteger(0); 
		public int EnqueueFulls
		{
			get { return _enqueueFulls.ReadUnfenced(); }
		}

		public int Backtrackings
		{
			get { return _backtrackings.ReadUnfenced(); }
		}
	//#endif

		public const int NO_WAIT = 0;
		public const int BLOCK_WAIT = -1;

		private Volatile.PaddedInteger _head;
		private Volatile.PaddedInteger _batch_head;

		private Volatile.PaddedInteger _tail;
		private Volatile.PaddedInteger _batch_tail;

		protected T _emptyElement = default(T);
		private SpinWait _wt = new SpinWait();

		public THelper Helper = default(THelper);

#if !INLINE_EMPTY_ELEMENT
		public RefFunc<T,bool> IsNotEmptyElement;
		public RefAction<T> SetEmptyElement;
#endif
		public BQueue() : this(QUEUE_SIZE,default(T),false){}
		public BQueue(int size, bool fastCompare=false) : this(size, default(T),fastCompare) { }

		public int Capacity
		{
			get { return _size; }
		}

		public T[] Buffer
		{
			get { return _data; }
		}

		public BQueue(int size, T emptyElement, bool fastCompare)
		{
			_size = size;
			_emptyElement = emptyElement;

#if !INLINE_EMPTY_ELEMENT
			IsNotEmptyElement = IsNotEmptyElementDefault;
			SetEmptyElement = SetEmptyElementDefault;
			if (fastCompare)
			{
				IsNotEmptyElement = IsNotEmptyElementFastImpl;
			}
#endif

#if POWER2BUFFER
			//if(size^(size+1))
			_mask = size - 1;
		#endif
			CONS_BATCH_SIZE = PROD_BATCH_SIZE = _size/16;
			BATCH_INCREMENT = _size/32;

			_data = new T[_size];
			for (int i = 0; i < _data.Length; i++)
				Helper.InitElement(ref _data[i]);

			_batch_history = CONS_BATCH_SIZE;
		}

		private void wait_ticks()
		{
			//if(!_wt.NextSpinWillYield)
				_wt.SpinOnce();
		}

		public int Head
		{
			get { return _head.ReadUnfenced(); }
			set { _head.WriteUnfenced(value);}
		}

		public int ToIndex(int seq)
		{
#if POWER2BUFFER
			return seq & _mask;
#else
			return head;
#endif
		}
		
		public int NextHead()
		{
			int head = _head.ReadUnfenced();
			head++;
#if !POWER2BUFFER
			if(head>=_size)
				head=0;
#endif
			_head.WriteUnfenced(head);
			return head;
		}
	
		public int Tail
		{
			get { return _tail.ReadUnfenced(); }
			set { _tail.WriteUnfenced(value);}
		}

		public int Next(int idx)
		{
#if POWER2BUFFER
			return idx + 1;
#else
			return idx<_size-1 ? idx+1:0;
#endif
		}

		public int FreeTail()
		{
			int tail = _tail.ReadUnfenced();
			int idx;
#if POWER2BUFFER
			idx = tail & _mask;
#else
			idx = tail;
#endif
			Helper.SetEmptyElement(ref _data[idx]);
			tail++;
#if !POWER2BUFFER
			if(tail>=_size)
				tail=0;
#endif
			_tail.WriteUnfenced(tail);
			return tail;
		}

		public void SetTail(int tail)
		{
			_tail.WriteUnfenced(tail);
		}

#if INLINE_EMPTY_ELEMENT
		private bool IsNotEmptyElement(ref T val)
		{
			return Helper.IsNonEmptyElement(ref val);
		}
#endif

		private bool IsNotEmptyElementDefault(ref T val)
		{
			return !object.Equals(val, _emptyElement);
		}
#if INLINE_EMPTY_ELEMENT
		private void SetEmptyElement(ref T val)
		{
			Helper.SetEmptyElement(ref val);
		}
#endif
		private void SetEmptyElementDefault(ref T val)
		{
			val = _emptyElement;
		}

		public int WaitForFreeSlots(int batchSize = 1, int ticksWait = BLOCK_WAIT)
		{
			var head = _head.ReadUnfenced();

			var batch_head = _batch_head.ReadUnfenced();
			var size = batch_head - head;
#if !POWER2BUFFER	
			if (size < 0)
				size += _size;
#endif
			if (size >= batchSize)
				return size;

			return RealWaitForFreeSlots(batchSize, ticksWait);
		}

		public int BatchHead
		{
			get { return _batch_head.ReadUnfenced(); }
		}

		public int RealWaitForFreeSlots(int batchSize = 1, int ticksWait = BLOCK_WAIT)
		{
			var head = _head.ReadUnfenced();
			var batch_head = _batch_head.ReadUnfenced();

			if (batchSize < PROD_BATCH_SIZE)
				batchSize = PROD_BATCH_SIZE;
			batch_head = head + batchSize;
#if POWER2BUFFER
			var idx = batch_head & _mask;
#else
				if (batch_head >= _size)
					batch_head = 0;
				var idx = batch_head;
#endif
			if (IsNotEmptyElement(ref _data[idx]))
			{
#if STATS
				_enqueueFulls.WriteUnfenced(_enqueueFulls.ReadUnfenced()+1);
#endif
				if (ticksWait != NO_WAIT)
				{
					long start = DateTime.UtcNow.Ticks;
					do
					{
						wait_ticks();
						if (ticksWait > 0 && (int)(DateTime.UtcNow.Ticks - start) > ticksWait)
						{
							return 0;
						}
					} while (IsNotEmptyElement(ref _data[idx]));
				}
			}
			_batch_head.WriteUnfenced(batch_head);
			return batch_head - head;
		}

		public void Enqueue(T value)
		{
			var head = _head.ReadUnfenced();
			var batch_head = _batch_head.ReadUnfenced();
			
			if (head == batch_head)
				WaitForFreeSlots();

			_data[ToIndex(head)] = value;
			NextHead();
		}

		public void Enqueue(RefAction<int, T> translator)
		{
			var head = _head.ReadUnfenced();
			var batch_head = _batch_head.ReadUnfenced();

			if (head == batch_head)
				WaitForFreeSlots();

			translator(head, ref _data[ToIndex(head)]);
			NextHead();
		}

		public void Commit(int head)
		{
			_head.WriteUnfenced(head);
		}

		private int Backtracking()
		{
		#if STATS			
			_backtrackings.WriteUnfenced(_backtrackings.ReadUnfenced()+1);
		#endif
			var tail = _tail.ReadUnfenced();
			var tmp_tail = tail;
			
		#if BACKTRACKING			
			tmp_tail += _batch_history; 
			//tmp_tail += CONS_BATCH_SIZE;
		#endif
		#if !POWER2BUFFER
			if ( tmp_tail >= _size ) 
				tmp_tail = 0;
		#endif
		#if ADAPTIVE
			if (_batch_history < CONS_BATCH_SIZE)
			{
				_batch_history =
					(CONS_BATCH_SIZE < (_batch_history + BATCH_INCREMENT)) ?
					CONS_BATCH_SIZE : (_batch_history + BATCH_INCREMENT);
			}
		#endif

		#if BACKTRACKING
			var batch_size = _batch_history;
		  #if POWER2BUFFER
			var idx = ((int)tmp_tail) & _mask;
			while ( !IsNotEmptyElement(ref _data[idx]) ) 
		  #else
			while ( !IsNotEmptyElement(ref _data[tmp_tail]) )
		  #endif
			{
				if (batch_size == 0)
					return 0;
				
				//wait_ticks();

				batch_size = batch_size >> 1;
				tmp_tail = tail + batch_size;
			#if POWER2BUFFER
				idx = ((int)tmp_tail) & _mask;
			#else
				if (tmp_tail >= _size)
					tmp_tail = 0;	
			#endif
			}
			#if ADAPTIVE
			_batch_history = batch_size;
			#endif
		#else
			if ( !IsNotEmptyElementFast(_data[tmp_tail])  )
			{
				return false;
			}
		#endif
			if ( tmp_tail == tail ) 
			{
			#if POWER2BUFFER
				tmp_tail++;
			#else
				tmp_tail = (tmp_tail + 1) >= _size ?
					0 : tmp_tail + 1;
			#endif
			}
			_batch_tail.WriteUnfenced( tmp_tail );
			return tmp_tail-tail;
		}
	

		public int WaitForData(int ticksWait = BLOCK_WAIT)
		{
			var tail = _tail.ReadUnfenced();
			var batch_tail = _batch_tail.ReadUnfenced();
			var ready=batch_tail-tail;
			if (ready == 0)
				ready = RealWaitForData(ticksWait);
			return ready;
		}

		public int Available
		{
			get { return _batch_tail.ReadUnfenced() - _tail.ReadUnfenced(); }
		}

		public int RealWaitForData(int ticksWait)
		{
			var tail = _tail.ReadUnfenced();
			var batch_tail = _batch_tail.ReadUnfenced();
			int ready;
			if ((ready = Backtracking()) == 0 && ticksWait != NO_WAIT)
			{
				long start = DateTime.UtcNow.Ticks;
				do
				{
					wait_ticks();
					if (ticksWait > 0 && (int)(DateTime.UtcNow.Ticks - start) > ticksWait)
					{
						return 0;
					}
				} while ((ready = Backtracking()) == 0);
			}
			return ready;
		}

		public bool TryDequeue(out T value, int ticksWait = NO_WAIT)
		{
			int seq;
			if (WaitForData(ticksWait) == 0)
			{
				value = _emptyElement;
//				SetEmptyElement(ref value);
				return false;
			}

			value = Dequeue();

			return true; //SUCCESS
		}
	
		public bool TryEnqueue(T value, int ticksWait = NO_WAIT)
		{
			if(0==WaitForFreeSlots(1, ticksWait))
				return false;
			
			Enqueue(value);
			return true;
		}

		public bool TryEnqueue(RefAction<int, T> translator, int ticksWait = NO_WAIT)
		{
			if (0 == WaitForFreeSlots(1, ticksWait))
				return false;

			Enqueue(translator);
			return true;
		}

		public T Dequeue()
		{
			T value;
			
			var tail = _tail.ReadUnfenced();
			var batch_tail = _batch_tail.ReadUnfenced();
			
			if(tail==batch_tail)
				WaitForData();

#if POWER2BUFFER
			int idx = tail & _mask;
#else
			int idx = _tail.ReadUnfenced();
#endif
			value = _data[idx];
			SetEmptyElement(ref _data[idx]);
			tail++;
#if !POWER2BUFFER
			if(tail>=_size)
				tail=0;
#endif
			_tail.WriteUnfenced(tail);
			return value;
		}

	
		public void Enqueue1(T value)
		{
			var head = _head.ReadUnfenced();
			var batch_head = _batch_head.ReadUnfenced();
			if (head == batch_head)
			{
				batch_head = head + PROD_BATCH_SIZE;
#if POWER2BUFFER
				var idx = batch_head & _mask;

#else
				if (batch_head >= _size)
					batch_head = 0;
				var idx = batch_head;
#endif
#if STATS				
				if (IsNotEmptyElement(ref _data[idx]))
					_enqueueFulls.WriteUnfenced(_enqueueFulls.ReadUnfenced()+1);
#endif
				while (IsNotEmptyElement(ref _data[idx]))
					wait_ticks();

				//SpinWait.SpinUntil(()=>_data[batch_head]!=0);
					
				_batch_head.WriteUnfenced(batch_head);
			}
#if POWER2BUFFER
			_data[head&_mask] = value;
			head++;
#else
			_data[head] = value;
			fehead++;
			if (head >= _size)
				head = 0;
#endif
			_head.WriteUnfenced(head);
		}

		public T Dequeue1()
		{
			var tail = _tail.ReadUnfenced();
			var batch_tail = _batch_tail.ReadUnfenced();
			if (tail == batch_tail)
			{
				while (0==Backtracking())
					wait_ticks();
			}
#if POWER2BUFFER
			var idx = (int) tail & _mask;
			var value = _data[idx];
			SetEmptyElement(ref _data[idx]);
			tail++;
#else
			var value = _data[tail];
			SetEmptyElement(out _data[tail]);
			tail++;
			if (tail >= _size)
				tail = 0;
#endif
			_tail.WriteUnfenced(tail);
			return value;
		}

		private List<T> ToList()
		{
			var list = new List<T>();
			int tail = _tail.ReadUnfenced();
			int head = _head.ReadUnfenced();
			while(tail!=head)
			{
	#if POWER2BUFFER
				var idx = (int) tail & _mask;
				var value = _data[idx];
				list.Add(value);
				tail++;
	#else
				var value = _data[tail];
				list.Add(value);
				tail++;
				if (tail >= _size)
					tail = 0;
	#endif
			}
			return list;
		}

		IEnumerator<T> IEnumerable<T>.GetEnumerator()
		{
			return ToList().GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return ToList().GetEnumerator();
		}

		void ICollection.CopyTo(Array array, int index)
		{
			ToList().CopyTo((T[])array,index);
		}

		public int Count
		{
			get
			{
				int count = _head.ReadUnfenced() - _tail.ReadUnfenced();
#if !POWER2BUFFER				
				if(count<0)
					count += _size;
#endif
				return count;
			}
		}

		object ICollection.SyncRoot
		{
			get { throw new NotImplementedException(); }
		}

		bool ICollection.IsSynchronized
		{
			get { return false; }
		}

		void IProducerConsumerCollection<T>.CopyTo(T[] array, int index)
		{
			ToList().CopyTo(array,index);
		}

		bool IProducerConsumerCollection<T>.TryAdd(T item)
		{
			return TryEnqueue(item);
		}

		bool IProducerConsumerCollection<T>.TryTake(out T item)
		{
			return TryDequeue(out item);
		}

		T[] IProducerConsumerCollection<T>.ToArray()
		{
			var cnt = this.Count;
			T[] array = new T[cnt];
			ToList().CopyTo(array, 0);
			return array;
		}
	}


	
}
