#define PROD_BATCH 
#define CONS_BATCH
#define BACKTRACKING
#define ADAPTIVE
#define ADAPT_ALWAYS
#define POWER2BUFFER

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

using System;
using System.Threading.Tasks;


namespace Snail.Threading
{

	using System.Runtime.InteropServices;
	using System.Threading;

	public class BQueue
	{
		public const int QUEUE_SIZE = 1024*1024;

		private int CONS_BATCH_SIZE;
		private int PROD_BATCH_SIZE;
		private int BATCH_INCREMENT;
		private int _size;
		private int _mask;
		private int[] _data;
		private int _batch_history;


		private Volatile.PaddedInteger _head;
		private Volatile.PaddedInteger _batch_head;

		private Volatile.PaddedInteger _tail;
		private Volatile.PaddedInteger _batch_tail;


		//public int ZERO = default(int);	
		

		
		public BQueue():this(QUEUE_SIZE)
		{
			
		}

		public BQueue(int size)
		{
			_size = size;
#if POWER2BUFFER
			//if(size^(size+1))
			_mask = size - 1;
#endif
			CONS_BATCH_SIZE = PROD_BATCH_SIZE = _size/16;
			BATCH_INCREMENT = _size/32;

			_data = new int[_size];
			
	#if CONS_BATCH
			_batch_history = CONS_BATCH_SIZE;
	#endif
		}
		private SpinWait wt = new SpinWait();
		private void wait_ticks()
		{
			wt.SpinOnce();
		}

	#if PROD_BATCH
		public bool TryEnqueue(int value)
		{
			var  head = _head.ReadUnfenced();
			var  batch_head = _batch_head.ReadUnfenced();
			if( head == batch_head ) 
			{
				batch_head = head + PROD_BATCH_SIZE;
				if ( batch_head >= _size )
					batch_head = 0;
				if ( _data[batch_head]!=0 )
				{
					wait_ticks();
					return false;		// BUFFER_FULL
				}
				_batch_head.WriteUnfenced(batch_head);
			}
			_data[head] = value;
			head++;
			if (head >= _size)
				head = 0;
			_head.WriteUnfenced(head);
			return true;	// SUCCESS
		}
	#else
		bool Enqueue(int value)
		{
			if ( _data[_head]!=Zero )
				return false;	// BUFFER_FULL
			_data[_head] = value;
			_head ++;
			if ( _head >= _size ) {
				_head = 0;
			}
			return true;//SUCCESS;
		}
	#endif
	#if CONS_BATCH
		public int BacktrackCount;
		private bool Backtracking()
		{
			++BacktrackCount;

			var tail = _tail.ReadUnfenced();
			var tmp_tail = tail;
			
#if BACKTRACKING			
			tmp_tail += _batch_history; 
			//tmp_tail += CONS_BATCH_SIZE;
#endif
#if !POWER2BUFFER
			if ( tmp_tail >= _size ) 
			{
				tmp_tail = 0;
			}
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
			while (_data[idx]==0)
#else
			while (_data[tmp_tail] == 0)
#endif
			{
				if (batch_size == 0)
					return false;
				wait_ticks();
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
			if ( _data[tmp_tail]==0 )
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
			return true;
		}

		public bool TryDequeue(ref int value)
		{
			var tail = _tail.ReadUnfenced();
			var batch_tail = _batch_tail.ReadUnfenced();
			if( tail == batch_tail )
			{
				if ( !Backtracking() )
					return false; //BUFFER_EMPTY;
			}
			value = _data[tail];
			_data[tail] = 0;
			tail++;
			if (tail >= _size)
				tail = 0;
			_tail.WriteUnfenced(tail);
			return true; //SUCCESS
		}
	#else 
		bool Dequeue(ref int value)
		{
			if ( !_data[_tail] )
				return false; // BUFFER_EMPTY;
			value = _data[_tail];
			_data[_tail] = ZERO;
			_tail ++;
			if ( _tail >= _size)
				_tail = 0;
			return true;// SUCCESS;
		}
	#endif
		public int EnqueueBatches = 0;
		public void Enqueue(int value)
		{
			var head = _head.ReadUnfenced();
			var batch_head = _batch_head.ReadUnfenced();
			if (head == batch_head)
			{
				EnqueueBatches++;
				batch_head = head + PROD_BATCH_SIZE;
#if POWER2BUFFER
				var idx = batch_head & _mask;
				while (_data[idx] != 0)
					wait_ticks();
#else
				if (batch_head >= _size)
					batch_head = 0;
				while (_data[batch_head] != 0)
					wait_ticks();
#endif				
				//SpinWait.SpinUntil(()=>_data[batch_head]!=0);
					
				_batch_head.WriteUnfenced(batch_head);
			}
#if POWER2BUFFER
			_data[head&_mask] = value;
			head++;
#else
			_data[head] = value;
			head++;
			if (head >= _size)
				head = 0;
#endif
			_head.WriteUnfenced(head);
		}

		public int Dequeue()
		{
			var tail = _tail.ReadUnfenced();
			var batch_tail = _batch_tail.ReadUnfenced();
			if (tail == batch_tail)
			{
				while (!Backtracking())
					wait_ticks();
			}
#if POWER2BUFFER
			var idx = (int) tail & _mask;
			var value = _data[idx];
			_data[idx] = 0;
			tail++;
#else
			var value = _data[tail];
			_data[tail] = 0;
			tail++;
			if (tail >= _size)
				tail = 0;
#endif
			_tail.WriteUnfenced(tail);
			return value;
		}

	}



	
}
