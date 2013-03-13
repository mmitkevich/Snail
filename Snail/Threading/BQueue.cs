#define ADAPTIVE
#define POWER2BUFFER
#define STATS
#define INLINEWAIT

using System.Runtime.CompilerServices;
using System.Text;

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
	
	
	public interface IBQueueBuffer
	{
		int Capacity{get;}
		void Init(int capacity);
		IntPtr BufferPtr { get; }
		//IntPtr GetElementPtr(int idx);
	}

	public interface IBQueueBuffer<T>:IBQueueBuffer 
	{
		T[] Buffer { get; }
	}

	public unsafe interface IBQueueBuffer<T,TIndex>:IBQueueBuffer<T> where TIndex:struct
	{
		TIndex Add(TIndex seq, int shift);
		TIndex AddWrap(TIndex seq, int shift);
		TIndex Inc(TIndex seq);
		TIndex Wrap(TIndex seq);
		int Subtract(TIndex left, TIndex right);
		int ToIndex(TIndex seq);
		IntPtr ToPointer(TIndex seq);
		T this[TIndex seq] { get; set; }
		bool IsNull(TIndex seq);
		void SetNull(TIndex seq);
		long ToLong(TIndex seq);
		INullValue<T> NullValue { get; }
	}

	public interface IBArgsBuffer<T,TIndex>:IBQueueBuffer<T,TIndex> where TIndex:struct
	{
		TIndex Write<TValue>(TIndex seq, TValue value) where TValue:struct;
		TIndex Read<TValue>(TIndex seq, ref TValue value) where TValue:struct;
	}

	public interface INullValue<T>
	{
		bool IsNull(ref T value);
		void SetNull(ref T value);
		bool IsNull(IntPtr ptr);
		void SetNull(IntPtr ptr);
	}

	public struct RefsNullValue:INullValue<object>
	{
		public bool IsNull(ref object value)
		{
			return value == null;
		}

		public void SetNull(ref object value)
		{
			value = null;
		}

		public bool IsNull(IntPtr ptr)
		{
			var handle = GCHandle.FromIntPtr(ptr);
			return handle.Target == null;
		}

		public void SetNull(IntPtr ptr)
		{
			var handle = GCHandle.FromIntPtr(ptr);
			handle.Target = null;
		}
	}

	public struct NullValue<T>:INullValue<T>
	{
		public static T Null;

		public bool IsNull(ref T val)
		{
			if (true)
			{
#if IL
				ldarg val
				ldobj !0
				ldsfld !0 valuetype Snail.Threading.NullValue`1<!T>::Null
				ceq
				ret
#endif
			}
			return !object.Equals(val, Null);
		}

		public bool IsNull(IntPtr ptr)
		{
			if (true)
			{
#if IL
				ldarg ptr
				ldobj !0
				ldsfld !0 valuetype Snail.Threading.NullValue`1<!T>::Null
				ceq
				ret
#endif
			}
			return !object.Equals(ptr, Null);
		}

		public void SetNull(ref T val)
		{
			val = Null;
		}

		public void SetNull(IntPtr ptr)
		{
			if (true)
			{
#if IL
				ldarg ptr
				ldsfld !0 valuetype Snail.Threading.NullValue`1<!T>::Null
				stobj !0
#endif
			}
		}
	}

	public struct BQueueBufferImpl<T,TNullValue> where TNullValue:struct,INullValue<T>
	{
		public T[] Buffer;
		public GCHandle GCHandle;
		public IntPtr BufferPtr;
		public TNullValue NullValue;


		public void Init(int capacity)
		{
			Buffer = new T[capacity];
			BufferPtr = IntPtr.Zero;
			for (int i = 0; i < Buffer.Length; i++)
			{
				NullValue.SetNull(ref Buffer[i]);
			}
		}

		public void Pin()
		{
			if (!GCHandle.IsAllocated)
				GCHandle = GCHandle.Alloc(Buffer, GCHandleType.Pinned);
			BufferPtr = GCHandle.AddrOfPinnedObject();
		}

		public void Unpin()
		{
			if (GCHandle.IsAllocated)
				GCHandle.Free();
			BufferPtr = IntPtr.Zero;
		}

		public IntPtr GetElementPtr(int idx)
		{
			return IntPtr.Add(BufferPtr, idx * ByteArrayUtils.SizeOf<T>());
		}

	}
	/// <summary>
	/// Safe ringbuffer implementation with manual index wrapping.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <typeparam name="TNullValue"></typeparam>
	public struct BRingBuffer<T, TNullValue> : IBQueueBuffer<T, int>
		where TNullValue : struct,INullValue<T>
	{
		public BQueueBufferImpl<T, TNullValue> Impl;

		public INullValue<T> NullValue
		{
			get { return Impl.NullValue; }
		}

		public T[] Buffer
		{
			get { return Impl.Buffer; }
		}

		public int Capacity
		{
			get { return Impl.Buffer.Length; }
		}

		public void Init(int capacity)
		{
			Impl.Init(capacity);
		}

		public IntPtr BufferPtr
		{
			get { return Impl.BufferPtr; }
		}

		public IntPtr GetElementPtr(int idx)
		{
			return Impl.GetElementPtr(idx);
		}

		public int Inc(int seq)
		{
			return seq + 1;
		}

		public int Add(int seq, int shift)
		{
			return seq + shift;
		}

		public int AddWrap(int seq, int shift)
		{
			return Wrap(seq+shift);
		}

		public int Subtract(int left, int right)
		{
			int d = left - right;
			if (d < 0)
				d += Capacity;
			return d;
		}

		public int ToIndex(int seq)
		{
			return seq;
		}

		public int Wrap(int seq)
		{
			return seq >= Capacity ? 0 : seq;
		}

		public long ToLong(int seq)
		{
			return seq;
		}

		public T this[int seq]
		{
			get { return Buffer[seq]; }
			set { Buffer[seq] = value; }
		}
		
		public long Read<TValue>(int seq, ref TValue value) where TValue : struct
		{
			ByteArrayUtils.Read(Impl.Buffer, seq, ref value);
			return seq + ByteArrayUtils.SizeOf<TValue>();
		}

		public long Write<TValue>(int seq, ref TValue value) where TValue : struct
		{
			ByteArrayUtils.Read(Impl.Buffer, seq, ref value);
			return seq + ByteArrayUtils.SizeOf<TValue>();
		}


		public bool IsNull(int seq)
		{
			return NullValue.IsNull(ref Impl.Buffer[seq]);
		}

		public void SetNull(int seq)
		{
			NullValue.SetNull(ref Impl.Buffer[seq]);
		}

		public IntPtr ToPointer(int seq)
		{
			return GetElementPtr(seq);
		}
	}

	/// <summary>
	/// Safe ringbuffer with CAPACITY = 2^n implementation using 64bit sequence.
	/// Array index is produced by bitwise AND operation with MASK = 2^n-1.
	/// Should be somewhat faster than <see cref="BRingBuffer{T,TNullValue}"/>.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <typeparam name="TNullValue"></typeparam>
	public struct BRing2Buffer<T, TNullValue> : IBQueueBuffer<T, long>
		where TNullValue : struct,INullValue<T>
	{
		public BQueueBufferImpl<T, TNullValue> Impl;

		public INullValue<T> NullValue
		{
			get { return Impl.NullValue; }
		}

		public T[] Buffer
		{
			get { return Impl.Buffer; }
		}

		public int Capacity
		{
			get { return Impl.Buffer.Length; }
		}

		public void Init(int capacity)
		{
			Impl.Init(capacity);
		}

		public IntPtr BufferPtr
		{
			get { return Impl.BufferPtr; }
		}

		public IntPtr GetElementPtr(int idx)
		{
			return Impl.GetElementPtr(idx);
		}

		public long Inc(long seq)
		{
			return seq + 1;
		}

		public long Add(long seq, int shift)
		{
			return seq + shift;
		}

		public long AddWrap(long seq, int shift)
		{
			return seq + shift;
		}

		public int Subtract(long left, long right)
		{
			return (int)(left - right);
		}

		public int ToIndex(long seq)
		{
			return (int)seq & (Impl.Buffer.Length - 1);
		}

		public long Wrap(long seq)
		{
			return seq;
		}

		public long ToLong(long seq)
		{
			return seq;
		}

		public T this[long seq]
		{
			get{ return Buffer[ToIndex(seq)]; }
			set { Buffer[ToIndex(seq)] = value; }
		}

		public bool IsNull(long seq)
		{
			return Impl.NullValue.IsNull(ref Impl.Buffer[ToIndex(seq)]);
		}

		public void SetNull(long seq)
		{
			Impl.NullValue.SetNull(ref Impl.Buffer[ToIndex(seq)]);
		}

		public IntPtr ToPointer(long seq)
		{
			return GetElementPtr(ToIndex(seq));
		}

		public long Read<TValue>(long seq, ref TValue value) where TValue : struct
		{
			ByteArrayUtils.Read(Impl.Buffer,ToIndex(seq),ref value);
			return seq + ByteArrayUtils.SizeOf<TValue>();
		}

		public long Write<TValue>(long seq, ref TValue value) where TValue : struct
		{
			ByteArrayUtils.Read(Impl.Buffer, ToIndex(seq), ref value);
			return seq + ByteArrayUtils.SizeOf<TValue>();
		}
	}

	/// <summary>
	/// Unsafe pinned ringbuffer implementation using pointers.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <typeparam name="TNullValue"> </typeparam>
	public struct BPinnedRingBuffer<T, TNullValue> : IBArgsBuffer<T, IntPtr>
		where TNullValue : struct,INullValue<T>
	{
		public BQueueBufferImpl<T, TNullValue> Impl;

		public INullValue<T> NullValue
		{
			get { return Impl.NullValue; }
		}

		public T[] Buffer
		{
			get { return Impl.Buffer; }
		}

		public int Capacity
		{
			get { return Impl.Buffer.Length; }
		}

		public void Init(int capacity)
		{
			Impl.Init(capacity);
		}

		public IntPtr BufferPtr
		{
			get { return Impl.BufferPtr; }
		}

		public IntPtr GetElementPtr(int idx)
		{
			return Impl.GetElementPtr(idx);
		}

		public IntPtr Inc(IntPtr seq)
		{
			return seq + 1;
		}

		public IntPtr Add(IntPtr seq, int shift)
		{
			return seq + shift;
		}

		public IntPtr AddWrap(IntPtr seq, int shift)
		{
			return Wrap(seq + shift);
		}

		public int Subtract(IntPtr left, IntPtr right)
		{
			return ByteArrayUtils.Subtract<T>(left, right);
		}

		public int ToIndex(IntPtr seq)
		{
			return Subtract(seq, BufferPtr);
		}

		public IntPtr Wrap(IntPtr seq)
		{
			return ByteArrayUtils.Subtract<T>(seq, BufferPtr) >= Capacity ? BufferPtr : seq;
		}

		public long ToLong(IntPtr seq)
		{
			return seq.ToInt64();
		}

		public T this[IntPtr seq]
		{
			get { return ByteArrayUtils.Read<T>(seq); }
			set { ByteArrayUtils.Write(seq, value); }
		}

		public bool IsNull(IntPtr seq)
		{
			return Impl.NullValue.IsNull(seq);
		}

		public void SetNull(IntPtr seq)
		{
			Impl.NullValue.SetNull(seq);
		}

		public IntPtr ToPointer(IntPtr seq)
		{
			return seq;
		}

		public IntPtr Read<TValue>(IntPtr seq, ref TValue value) where TValue : struct
		{
			ByteArrayUtils.Read(seq, ref value);
			return IntPtr.Add(seq,ByteArrayUtils.SizeOf<TValue>());
		}

		public IntPtr Write<TValue>(IntPtr seq, TValue value) where TValue : struct
		{
			ByteArrayUtils.Write(seq, ref value);
			return IntPtr.Add(seq, ByteArrayUtils.SizeOf<TValue>());
		}
	}

	public static class BQWait
	{
		public const int NoWait = 0;
		public const int BlockWait = -1;
	}

	public interface IBQConsumer<T, TBuffer>
	{
		int WaitData(ref TBuffer buf, int ticksWait = BQWait.BlockWait);
		void MoveNext(ref TBuffer buf);
		T Read(ref TBuffer buf);
		//void Init(int count);
		void UpdateQueueStat(BQueueStat stat);
	}

	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	public unsafe struct BQConsumer<T, TBuffer, TSequence> : IBQConsumer<T, TBuffer>
		where TBuffer : IBQueueBuffer<T, TSequence>
		where TSequence : struct

	{
		private fixed long _pad1[8];
		// consumer only
		public TSequence Tail;
		public TSequence BatchTail;
		public int BatchHistory;

		private int BatchIncrement;
		private int ConsumerBatchSize;
#		if STATS
		public long Backtrackings;
#		endif

		private fixed long _pad2[8];

		public void Init(int capacity)
		{
			ConsumerBatchSize = capacity / 16;
			BatchIncrement = capacity / 32;
			BatchHistory = ConsumerBatchSize;
			Backtrackings = 0;
		}

		public int GetAvailable(ref TBuffer buf)
		{
			return buf.Subtract(BatchTail,Tail); 
		
		}
		
		public void UpdateQueueStat(BQueueStat stat)
		{
			stat.Backtrackings += Backtrackings;
		}


		public int WaitData(ref TBuffer buf, int ticksWait = BQWait.BlockWait)
		{
			var ready = buf.Subtract(BatchTail, Tail);
			if (ready > 0)
				return ready;
			return RealWaitData(ref buf, ticksWait);
		}

		public void MoveNext(ref TBuffer buf)
		{
			//_nullValue.SetNull(ref buf.Buffer[Tail.ToIndex(ref buf)]);
			buf.SetNull(Tail);
			Tail = buf.AddWrap(Tail,1);
		}

		public int RealWaitData(ref TBuffer buf, int ticksWait = BQWait.BlockWait)
		{
			int ready;
			if ((ready = Backtracking(ref buf)) == 0 && ticksWait != BQWait.NoWait)
			{
				long start = DateTime.UtcNow.Ticks;
				do
				{
					default(SpinWait).SpinOnce();
					if (ticksWait > 0 && (int)(DateTime.UtcNow.Ticks - start) > ticksWait)
					{
						return 0;
					}
				} while ((ready = Backtracking(ref buf)) == 0);
			}
			return ready;
		}

		private int Backtracking(ref TBuffer buf)
		{
#			if STATS
			Backtrackings++;
#			endif

			var tail = Tail;
			var batchTail = tail;

			batchTail = buf.Add(batchTail,BatchHistory);
			//tmp_tail += ConsumerBatchSize;

#if ADAPTIVE
			if (BatchHistory < ConsumerBatchSize)
			{
				BatchHistory =
					(ConsumerBatchSize < (BatchHistory + BatchIncrement)) ?
					ConsumerBatchSize : (BatchHistory + BatchIncrement);
			}
#endif

			var batchSize = BatchHistory;

			//while (IsNull(ref buf, batchTail))
			while (buf.IsNull(batchTail))
			{
				if (batchSize == 0)
					return 0;

				//WaitTicks();

				batchSize = batchSize >> 1;
				batchTail = tail;
				batchTail = buf.Add(batchTail,batchSize);
			}
#if ADAPTIVE
			BatchHistory = batchSize;
#endif

			if (buf.Subtract(batchTail,tail) == 0)
				batchTail = buf.AddWrap(batchTail,1);
			BatchTail = batchTail;
			return buf.Subtract(batchTail,tail);
		}


		public bool TryDequeue(ref TBuffer buf, out T value, int ticksWait = BQWait.NoWait)
		{
			if (WaitData(ref buf, ticksWait) == 0)
			{
				value = default(T);
				return false;
			}

			value = Read(ref buf);

			return true; //SUCCESS
		}

		public T Dequeue(ref TBuffer buf)
		{
			var tail = Tail;
#if INLINEWAIT
			if (buf.Subtract(BatchTail,tail)==0)
#endif
				RealWaitData(ref buf);
			T value = buf[tail];
			//MoveNext(ref buf);
			//_nullValue.SetNull(ref buf.Buffer[Tail.ToIndex(ref buf)]);
			buf.SetNull(tail); ;
			Tail = buf.AddWrap(tail,1);
			return value;
		}

		public T Read(ref TBuffer buf)
		{
			T value = buf[Tail];
#if DEBUG
			if (buf.IsNull(Tail) || buf.NullValue.IsNull(ref value))
				throw new InvalidOperationException("Read null at "+Tail);
#endif

			//MoveNext(ref buf);
			buf.SetNull(Tail);
			Tail = buf.AddWrap(Tail,1);
			return value;
		}
	}


	
	public interface IBQProducer<T,TBuffer>
	{
		int BeginBatch(ref TBuffer buf, int batchSize = 1, int ticksWait = BQWait.BlockWait);
		void MoveNext(ref TBuffer buf);
		void Write(ref TBuffer buf, T value);
	}

	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	public unsafe struct BQProducer<T, TBuffer, TSequence> : IBQProducer<T,TBuffer> 
		where TBuffer : IBQueueBuffer<T,TSequence>
		where TSequence:struct

	{
		private fixed long _pad1[8];
		// producer only
		private int ProducerBatchSize;
		public TSequence Head;
		public TSequence BatchHead;
#		if STATS
		public long EnqueueFulls;
#		endif
		private fixed long _pad2[8];


		public void Init(int capacity)
		{
			ProducerBatchSize = capacity / 16;
			EnqueueFulls = 0;
		}

		public int GetSlotsAvailable(ref TBuffer buf)
		{
			return buf.Subtract(BatchHead, Head); 
		}

		public int BeginBatch(ref TBuffer buf, int batchSize = 1, int ticksWait = BQWait.BlockWait)
		{
			var size = buf.Subtract(BatchHead, Head);

			if (size >= batchSize)
			{
#if DEBUG
				if(!S.IsNull(ref buf, Head, NullValue))
					throw new InvalidOperationException();
#endif
				return size;
			}

			return WaitForFreeSlots(ref buf, batchSize, ticksWait);
		}

		public void MoveNext(ref TBuffer buf)
		{
			Head = buf.AddWrap(Head,1);
		}



		public int WaitForFreeSlots(ref TBuffer buf, int batchSize = 1, int ticksWait = BQWait.BlockWait)
		{
			var head = Head;
			var batchHead = BatchHead;

			if (batchSize < ProducerBatchSize)
				batchSize = ProducerBatchSize;

			batchHead = buf.Add(batchHead, batchSize);

			//if (!IsNull(ref buf, batchHead))
			if (!buf.IsNull(batchHead))
			{
#				if STATS
				EnqueueFulls++;
#				endif
				if (ticksWait != BQWait.NoWait)
				{
					long start = DateTime.UtcNow.Ticks;
					do
					{
						default(SpinWait).SpinOnce();
						if (ticksWait > 0 && (int) (DateTime.UtcNow.Ticks - start) > ticksWait)
						{
							return 0;
						}
					} //while (!IsNull(ref buf, batchHead));
					while (!buf.IsNull(batchHead));
				}
			}
			BatchHead = batchHead;
#if DEBUG
			if (!S.IsNull(ref buf,head,NullValue))
				throw new InvalidOperationException();
#endif
			return buf.Subtract(batchHead, head);
		}

		public bool TryEnqueue(ref TBuffer buf, T value, int ticksWait = BQWait.NoWait)
		{
			if (0 == BeginBatch(ref buf, 1, ticksWait))
				return false;

			Write(ref buf, value);
			return true;
		}

		public bool TryEnqueue(ref TBuffer buf, RefAction<long, T> translator, int ticksWait = BQWait.NoWait)
		{
			if (0 == BeginBatch(ref buf, 1, ticksWait))
				return false;

			Enqueue(ref buf, translator);
			return true;
		}

		public void Enqueue(ref TBuffer buf, T value)
		{
			var head = Head;
#if INLINEWAIT
			if (buf.Subtract(BatchHead, head) == 0)
				WaitForFreeSlots(ref buf);
#else
			BeginBatch(ref buf);
#endif
			

#if DEBUG
			if (NullValue.IsNull(ref value))
				throw new InvalidOperationException("Value shoud be non-empty " + value);
#endif
			buf[head]=value;
			Head = buf.AddWrap(head,1);
			
			//var head = Head.ToLong();
			//buf.Buffer[head & (buf.Capacity - 1)] = value;
			//head++;
			//Head.FromLong(head);
		}

		public void Write(ref TBuffer buf, T value)
		{
			var head = Head;
#if DEBUG
			if (NullValue.IsNull(ref value))
				throw new InvalidOperationException("Value should be non-empty " + value);
			if (!S.IsNull(ref buf, head, NullValue))
				throw new InvalidOperationException("Written to occupied location " + head);
#endif
			buf[head]=value;
			Head = buf.AddWrap(head,1);
		}

		public void Enqueue(ref TBuffer buf, RefAction<long, T> translator)
		{
			var head = Head;
#if INLINEWAIT
			if (buf.Subtract(BatchHead, head)==0)
#endif
				WaitForFreeSlots(ref buf);

			var idx = buf.ToIndex(head);
			translator(buf.ToLong(head), ref buf.Buffer[idx]);

			Head = buf.AddWrap(head,1);
		}

	}
		

	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	public unsafe struct BQueueImpl<T, TBuffer, TSequence> 
		where TBuffer:IBQueueBuffer<T,TSequence>
		where TSequence:struct
	{
		// most readonly
		public TBuffer Buffer;

		public BQProducer<T, TBuffer, TSequence> P;
		public BQConsumer<T, TBuffer, TSequence> C;
		
		public IntPtr HeadPtr
		{
			get { return Buffer.ToPointer(P.Head); }
		}

		public IntPtr TailPtr
		{
			get { return Buffer.ToPointer(C.Tail); }
		}

		public void Init(int capacity)
		{
			Buffer.Init(capacity);
			P.Init(capacity);
			C.Init(capacity);
		}

		public int Count
		{
			get
			{
				int count = Buffer.Subtract(P.Head, C.Tail);
				return count;
			}
		}

		public bool TryEnqueue(T value, int ticksWait = BQWait.NoWait)
		{
			return P.TryEnqueue(ref Buffer, value, ticksWait);
		}

		public void Enqueue(T value)
		{
			P.Enqueue(ref Buffer, value);
		}

		public int WaitData(int ticksWait = BQWait.BlockWait)
		{
			return C.WaitData(ref Buffer, ticksWait);
		}

		public int SlotsAvailable
		{
			get { return P.GetSlotsAvailable(ref Buffer); }
		}

		public void Write(T value)
		{
			P.Write(ref Buffer, value);
		}

		public int BeginBatch(int batchSize, int ticksWait = BQWait.BlockWait)
		{
			return P.BeginBatch(ref Buffer, ticksWait);
		}
		

		public bool TryDequeue(out T value, int ticksWait = BQWait.NoWait)
		{
			return C.TryDequeue(ref Buffer, out value, ticksWait);
		}

		public T Dequeue()
		{
			return C.Dequeue(ref Buffer);
		}

		public List<T> ToList()
		{
			var list = new List<T>();
			var tail = C.Tail;
			var head = P.Head;
			while (Buffer.Subtract(tail, head)!=0)
			{
				list.Add(Buffer[tail]);
				tail = Buffer.AddWrap(tail,1);
			}
			return list;
		}
	}

	public unsafe class BQueue<T, TBuffer, TSequence> : IProducerConsumerCollection<T> 
		where TBuffer:IBQueueBuffer<T,TSequence>
		where TSequence:struct
	{
		public const int DefaultCapacity = 64*1024;


		public BQueueImpl<T, TBuffer, TSequence> Impl;// = default(BQueueImpl<TBuffer,TSequence>);

		
		public BQueue() : this(DefaultCapacity)
		{
		}
		
		public BQueue(int capacity)
		{
			Impl.Init(capacity);
		}

		public void Enqueue(T value)
		{
			Impl.Enqueue(value);
		}

		public T Dequeue()
		{
			return Impl.Dequeue();
		}

		public int Count
		{
			get { return Impl.Count; }
		}

		public BQueueStat GetQueueStats()
		{
			return new BQueueStat {Capacity = Impl.Buffer.Capacity, Count = Count, Backtrackings = Impl.C.Backtrackings, EnqueueFulls = Impl.P.EnqueueFulls};
		}

		IEnumerator<T> IEnumerable<T>.GetEnumerator()
		{
			return Impl.ToList().GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return Impl.ToList().GetEnumerator();
		}

		void ICollection.CopyTo(Array array, int index)
		{
			Impl.ToList().CopyTo((T[])array, index);
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
			Impl.ToList().CopyTo(array, index);
		}

		bool IProducerConsumerCollection<T>.TryAdd(T item)
		{
			return Impl.TryEnqueue(item);
		}

		bool IProducerConsumerCollection<T>.TryTake(out T item)
		{
			return Impl.TryDequeue(out item);
		}

		T[] IProducerConsumerCollection<T>.ToArray()
		{
			var cnt = this.Count;
			T[] array = new T[cnt];
			Impl.ToList().CopyTo(array, 0);
			return array;
		}
	}

	public class BQueue<T, TNullValue> : BQueue<T, BRingBuffer<T,TNullValue>, int>
		where TNullValue : struct,INullValue<T>
	{
		public BQueue(int capacity)
			: base(capacity)
		{

		}
	}

	public class BQueue<T> : BQueue<T,NullValue<T>>
	{
		public BQueue(int capacity)
			: base(capacity)
		{

		}
	}

	public class BQueueStat
	{
		public long Count;
		public long Capacity;
		public long Backtrackings;
		public long EnqueueFulls;

		public override string ToString()
		{
			var sb = new StringBuilder();
			sb.AppendFormat("Queue Filled {0}/{1}", Count, Capacity);
			sb.AppendFormat(", Backtracks {0} {1:F4}%", Backtrackings, (double)100 * Backtrackings / Capacity);
			sb.AppendFormat(", EnqFulls {0} {1:F4}", EnqueueFulls, (double)100 * EnqueueFulls / Capacity);
			return sb.ToString();
		}
	}
}
