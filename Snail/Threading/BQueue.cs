#define ADAPTIVE
#define POWER2BUFFER
#define STATS


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
	using System;
	using System.Threading;
	using System.Collections;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Runtime.InteropServices;
	using Snail.Util;
	
	
	public interface IBQueueElement<T>
	{
		void InitElement(ref T obj);
		void SetEmptyElement(ref T obj);
		bool IsNonEmptyElement(ref T obj);
	}

	public struct BQueueElement<T>:IBQueueElement<T>
	{
		private readonly T _emptyElement;

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
			return !object.Equals(val,_emptyElement);
		}

		public void SetEmptyElement(ref T val)
		{
			val = _emptyElement;
		}
	}

	

	public sealed unsafe class BQueue<T, THelper> : IProducerConsumerCollection<T> where THelper:IBQueueElement<T>  
	{
		public const int DefaultCapacity = 64*1024;

		public const int NoWait = 0;
		public const int BlockWait = -1;

		private int _consumerBatchSize;
		private int _producerBatchSize;
		private int _batchIncrement;

		private int _capacity;
		private int _mask;
		private T[] _buffer;

		private int _batch_history;

		private SpinWait _wt = new SpinWait();
		public THelper Helper = default(THelper);

		public void* BufferPtr
		{
			get { return _x.BufferPtr; }
		}

		[StructLayout(LayoutKind.Sequential, Pack=8)]
		private unsafe struct QueueData
		{
			public long Head;
			public long BatchHead;
			public void* BufferPtr;

#if STATS			
			public long EnqueueFulls;
#endif
			private fixed long _pad2 [8];
			public long Tail;
			public long BatchTail;
#if STATS
			public long _backtrackings;
#endif
		}

		private GCHandle _gch;
		private QueueData _x = default(QueueData);

		
		public long EnqueueFulls
		{
			get
			{
	#if STATS
				return _x.EnqueueFulls;
	#endif
			}
		}

		public long Backtrackings
		{
			get
			{
	#if STATS
				return _x._backtrackings;
	#endif
			}
		}

		public int Mask
		{
			get { return _mask; }
		}

		public int Capacity
		{
			get { return _capacity; }
		}

		public T[] Buffer
		{
			get { return _buffer; }
		}

		public BQueue() : this(DefaultCapacity)
		{
		}
		
		public BQueue(int capacity)
		{
			_capacity = capacity;

#if POWER2BUFFER
			//if(capacity^(capacity+1))
			_mask = capacity - 1;
#endif
			_consumerBatchSize = _producerBatchSize = _capacity/16;
			_batchIncrement = _capacity/32;

			_buffer = new T[_capacity];

			for (int i = 0; i < _buffer.Length; i++)
				Helper.InitElement(ref _buffer[i]);

			_batch_history = _consumerBatchSize;
		}

		public void Pin()
		{
			if (!_gch.IsAllocated)
				_gch = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
			_x.BufferPtr = (void*)_gch.AddrOfPinnedObject();
		}

		public void Unpin()
		{
			if(_gch.IsAllocated)
				_gch.Free();
			_x.BufferPtr = null;
		}

		private void WaitTicks()
		{
			//if(!_wt.NextSpinWillYield)
				_wt.SpinOnce();
		}

		public long Head
		{
			get { return _x.Head; }
			set { _x.Head = ToSeq(value); }
		}

		public long BatchHead
		{
			get { return _x.BatchHead; }
		}

		public int SeqToIdx(long seq)
		{
#if POWER2BUFFER
			return (int)(seq & _mask);
#else
			return (int) seq;
#endif
		}
		
		public long NextHead()
		{
			var head = NextSeq(_x.Head);
			_x.Head = head;
			return head;
		}
	
		public long Tail
		{
			get { return _x.Tail; }
			set
			{
				_x.Tail = ToSeq(value);
			}
		}

		public long BatchTail
		{
			get { return _x.BatchTail; }
		}

		private long ToSeq(long arg)
		{
#if !POWER2BUFFER
			var sz = _size;
			while(arg>sz)
				arg-=sz;
#endif
			return arg;
		}

		public int Available
		{
			get { return (int)(_x.BatchTail - _x.Tail); }
		}

		public int Count
		{
			get
			{
				int count = (int)(_x.Head - _x.Tail);
#if !POWER2BUFFER				
				if(count<0)
					count += _size;
#endif
				return count;
			}
		}

		public long NextSeq(long idx)
		{
#if POWER2BUFFER
			return idx + 1;
#else
			return idx<_size-1 ? idx+1:0;
#endif
		}

		public void FreeTail()
		{
			var tail = _x.Tail;
			var idx = SeqToIdx(tail);
			Helper.SetEmptyElement(ref _buffer[idx]);
			tail = NextSeq(tail);
			_x.Tail = tail;
		}

		public int WaitForFreeSlots(int batchSize = 1 , int ticksWait = BlockWait )
		{
			//var head = _x.Head;

			//var batch_head = _x.BatchHead;
			var size = Available;//(int)(batch_head - head);
#if !POWER2BUFFER	
			if (size < 0)
				size += _size;
#endif
			if (size < batchSize)
				RealWaitForFreeSlots(batchSize, ticksWait); 
			
			return size;
		}



		public int RealWaitForFreeSlots(int batchSize = 1, int ticksWait = BlockWait)
		{
			var head = _x.Head;
			var batch_head = _x.BatchHead;

			if (batchSize < _producerBatchSize)
				batchSize = _producerBatchSize;
			batch_head = head + batchSize;
			var idx = SeqToIdx(batch_head);
			if (Helper.IsNonEmptyElement(ref _buffer[idx]))
			{
#if STATS
				_x.EnqueueFulls++;
#endif
				if (ticksWait != NoWait)
				{
					long start = DateTime.UtcNow.Ticks;
					do
					{
						WaitTicks();
						if (ticksWait > 0 && (int)(DateTime.UtcNow.Ticks - start) > ticksWait)
						{
							return 0;
						}
					} while (Helper.IsNonEmptyElement(ref _buffer[idx]));
				}
			}
			_x.BatchHead = batch_head;
			return (int)(batch_head - head);
		}

		public void Enqueue(T value)
		{
			var head = _x.Head;
			var batch_head = _x.BatchHead;
			
			if (head == batch_head)
				RealWaitForFreeSlots();

			_buffer[SeqToIdx(head)] = value;
			
			head = NextSeq(head);
			_x.Head = head;
		}

		public void Enqueue(RefAction<long, T> translator)
		{
			var head = _x.Head;
			var batch_head = _x.BatchHead;

			if (head == batch_head)
				RealWaitForFreeSlots();

			translator(head, ref _buffer[SeqToIdx(head)]);
			head = NextSeq(head);
			_x.Head = head;
		}

		private int Backtracking()
		{
		#if STATS			
			_x._backtrackings++;
		#endif

			var tail = _x.Tail;
			var batch_tail = tail;
			
			batch_tail += _batch_history; 
			//tmp_tail += _consumerBatchSize;
	
		#if ADAPTIVE
			if (_batch_history < _consumerBatchSize)
			{
				_batch_history =
					(_consumerBatchSize < (_batch_history + _batchIncrement)) ?
					_consumerBatchSize : (_batch_history + _batchIncrement);
			}
		#endif

			var batchSize = _batch_history;
			var idx = SeqToIdx(batch_tail);

			while ( !Helper.IsNonEmptyElement(ref _buffer[idx]) ) 
			{
				if (batchSize == 0)
					return 0;
				
				//WaitTicks();

				batchSize = batchSize >> 1;
				batch_tail = tail + batchSize;
				idx = SeqToIdx(batch_tail);
			}
		#if ADAPTIVE
			_batch_history = batchSize;
		#endif
		
			if ( batch_tail == tail )
			{
				batch_tail = NextSeq(batch_tail);
			}
			_x.BatchTail = batch_tail;
			return (int)(batch_tail-tail);
		}
	

		public int WaitForData(int ticksWait = BlockWait)
		{
			var tail = _x.Tail;
			var batch_tail = _x.BatchTail;
			var ready = (int)(batch_tail-tail);
			if (ready == 0)
				ready = RealWaitForData(ticksWait);
			return ready;
		}

		public int RealWaitForData(int ticksWait)
		{
			int ready;
			if ((ready = Backtracking()) == 0 && ticksWait != NoWait)
			{
				long start = DateTime.UtcNow.Ticks;
				do
				{
					WaitTicks();
					if (ticksWait > 0 && (int)(DateTime.UtcNow.Ticks - start) > ticksWait)
					{
						return 0;
					}
				} while ((ready = Backtracking()) == 0);
			}
			return ready;
		}

		public bool TryDequeue(out T value, int ticksWait = NoWait)
		{
			if (WaitForData(ticksWait) == 0)
			{
				value = default(T);
				return false;
			}

			value = Dequeue();

			return true; //SUCCESS
		}
	
		public bool TryEnqueue(T value, int ticksWait = NoWait)
		{
			if(0==WaitForFreeSlots(1, ticksWait))
				return false;
			
			Enqueue(value);
			return true;
		}

		public bool TryEnqueue(RefAction<long, T> translator, int ticksWait = NoWait)
		{
			if (0 == WaitForFreeSlots(1, ticksWait))
				return false;

			Enqueue(translator);
			return true;
		}

		public T Dequeue()
		{
			var tail = _x.Tail;
			var batch_tail = _x.BatchTail;
			
			if(tail==batch_tail)
				WaitForData();

			var idx = SeqToIdx(tail);
			T value = _buffer[idx];

			Helper.SetEmptyElement(ref _buffer[idx]);
			tail = NextSeq(tail);
			_x.Tail = tail;
			return value;
		}

	/*
		public void Enqueue1(T value)
		{
			var head = Head.ReadUnfenced();
			var batch_head = BatchHead.ReadUnfenced();
			if (head == batch_head)
			{
				batch_head = head + _producerBatchSize;
#if POWER2BUFFER
				var idx = batch_head & _mask;

#else
				if (batch_head >= _size)
					batch_head = 0;
				var idx = batch_head;
#endif
#if STATS				
				if (IsNotEmptyElement(ref _buffer[idx]))
					EnqueueFulls.WriteUnfenced(EnqueueFulls.ReadUnfenced()+1);
#endif
				while (IsNotEmptyElement(ref _buffer[idx]))
					WaitTicks();

				//SpinWait.SpinUntil(()=>_buffer[batch_head]!=0);
					
				BatchHead.WriteUnfenced(batch_head);
			}
#if POWER2BUFFER
			_buffer[head&_mask] = value;
			head++;
#else
			_data[head] = value;
			fehead++;
			if (head >= _size)
				head = 0;
#endif
			Head.WriteUnfenced(head);
		}

		public T Dequeue1()
		{
			var tail = Tail.ReadUnfenced();
			var batch_tail = BatchTail.ReadUnfenced();
			if (tail == batch_tail)
			{
				while (0==Backtracking())
					WaitTicks();
			}
#if POWER2BUFFER
			var idx = (int) tail & _mask;
			var value = _buffer[idx];
			SetEmptyElement(ref _buffer[idx]);
			tail++;
#else
			var value = _data[tail];
			SetEmptyElement(out _data[tail]);
			tail++;
			if (tail >= _size)
				tail = 0;
#endif
			Tail.WriteUnfenced(tail);
			return value;
		}
		*/

		private List<T> ToList()
		{
			var list = new List<T>();
			var tail = _x.Tail;
			var head = _x.Head;
			while(tail!=head)
			{
				list.Add(_buffer[SeqToIdx(tail)]);
				tail = NextSeq(tail);
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
