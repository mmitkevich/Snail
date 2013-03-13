#define CHUNKED
//#define FIXEDBUF

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Snail.Collections;

namespace Snail.Threading
{
	/// <summary>
	/// Smart method address.
	/// For in-process shared memory communications it is raw _fastcall function pointer (IntPtr)
	/// For inter-process communication it is hash (MD4) of fully qualified function signature [assembly]class::method(arg1type,arg2type)
	/// </summary>
	[StructLayout(LayoutKind.Sequential, Pack = 1, Size=8, CharSet = CharSet.Unicode)]
	public unsafe struct Address
	{
		public long Value;
		
		public const long Null = 0;

		public Address(long value)
		{
			Value = value;
		}

		public static Address FromPtr(IntPtr ptr)
		{
			return new Address(ptr.ToInt64());
		}

		public static IntPtr GetFunctionPointer(Delegate d)
		{
			return d.Method.MethodHandle.GetFunctionPointer();
		}

		public static int ExecuteMethod(IntPtr ptr, object obj, MessageConsumer q, int count)
		{
			if (true)
			{
#if IL
			ldarg obj
			ldarg q
			ldarg count
			ldarg ptr
			calli int32 (class [mscorlib]System.Object, class Snail.Threading.MessageConsumer, int32)
			ret
#endif
			}
			return 0;
		}

		public static int ExecuteStaticMethod(IntPtr ptr, MessageConsumer q, int count)
		{
			if (true)
			{
#if IL
			ldarg q
			ldarg count
			ldarg ptr
			calli int32 (class Snail.Threading.MessageConsumer, int32)
			ret
#endif
			}
			return 0;
		}

		public static Address FromDelegate<T>(T v) where T:class
		{
			Delegate d = null;
#if IL
			ldarg v;
			stloc d;
#endif
			return FromPtr(GetFunctionPointer(d)); 
		}

		
		public IntPtr ToPtr()
		{
			return new IntPtr(Value);
		}

		public void SetEmpty()
		{
			Value = Null;
		}

		public bool IsEmpty()
		{
			return Value == Null;
		}

		public bool Equals(Address other)
		{
			return other.Value == Value;
		}

	}

	[StructLayout(LayoutKind.Sequential,Pack = 1, CharSet = CharSet.Unicode)]
	public unsafe struct Message
	{
		public Address Func;
		public Message(Address func)
		{
			Func = func;
		}

		public void SetNull()
		{
			Func.SetEmpty();
		}

		public bool IsNull()
		{
			return Func.IsEmpty();
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	public unsafe struct ArgsConsumer<TArgsIndex, TArgsBuffer, TRefsIndex, TRefsBuffer>
		where TArgsBuffer : IBArgsBuffer<byte, TArgsIndex>
		where TRefsBuffer : IBQueueBuffer<object, TRefsIndex>
		where TArgsIndex:struct
		where TRefsIndex : struct
	{
		private fixed long _pad1[8];
		public TRefsIndex RefsTail;
		public TArgsIndex ArgsTail;
		private fixed long _pad2[8];

		public void Init(int count)
		{
			
		}

		public T Read<T>(ref TArgsBuffer buf) where T : struct
		{
			T val = default(T);
			ArgsTail = buf.Read(ArgsTail, ref val);
			return val;
		}

		public object ReadRef(ref TRefsBuffer buf)
		{
			var tail = RefsTail;
			var ret = buf[RefsTail];
			RefsTail = buf.Inc(tail);
			return ret;
		}

		public void EndRead(ref TArgsBuffer buf, ref TRefsBuffer refbuf)
		{
			ArgsTail = buf.Wrap(ArgsTail);
			RefsTail = refbuf.Wrap(RefsTail);
		}
	}

	[StructLayout(LayoutKind.Sequential,Pack=8)]
	public unsafe struct ArgsProducer<TArgsIndex, TArgsBuffer, TRefsIndex, TRefsBuffer>
		where TArgsBuffer: IBArgsBuffer<byte,TArgsIndex>
		where TRefsBuffer : IBQueueBuffer<object, TRefsIndex>
		where TArgsIndex:struct
		where TRefsIndex : struct
	{
		private fixed long _pad1[8];
		public TRefsIndex RefsHead;
		public TArgsIndex ArgsHead;
		private fixed long _pad2[8];

		public void Init(int count)
		{ }

		public void Write<T>(ref TArgsBuffer buf, T val) where T : struct
		{
			ArgsHead = buf.Write(ArgsHead, val);
		}

		public void WriteRef(ref TRefsBuffer buf, object obj)
		{
			buf[RefsHead] = obj;
			RefsHead = buf.Inc(RefsHead);
		}

		public void EndWrite(ref TArgsBuffer buf, ref TRefsBuffer refbuf)
		{
			ArgsHead = buf.Wrap(ArgsHead);
			RefsHead = refbuf.Wrap(RefsHead);

		}
	}


	public class MessageConsumerImpl<TMessage, TMessageBuffer, TArgsBuffer, TRefsBuffer> 
		: IBQConsumer<TMessage,TMessageBuffer> 
		where TMessageBuffer:IBQueueBuffer<TMessage,IntPtr>
		where TMessage:struct
		where TArgsBuffer:IBArgsBuffer<byte,IntPtr>
		where TRefsBuffer:IBQueueBuffer<object,int>
	{
		public BQConsumer<TMessage, TMessageBuffer, IntPtr> MsgC;

		public ArgsConsumer<IntPtr, TArgsBuffer, int, TRefsBuffer> ArgsC;

		public MessageQueueImpl<TMessage,TMessageBuffer,TArgsBuffer,TRefsBuffer> Queue;

		public MessageConsumerImpl(
			MessageQueueImpl<TMessage, TMessageBuffer, TArgsBuffer, TRefsBuffer>  queue, int consumerIndex)
		{
			Queue = queue;
			MsgC.Init(queue.Msgs.Capacity);
			ArgsC.Init(queue.Args.Capacity);
		}



		int IBQConsumer<TMessage, TMessageBuffer>.WaitData(ref TMessageBuffer buf, int ticksWait)
		{
			return MsgC.WaitData(ref buf, ticksWait);
		}

		void IBQConsumer<TMessage, TMessageBuffer>.MoveNext(ref TMessageBuffer buf)
		{
			MsgC.MoveNext(ref buf);
		}

		TMessage IBQConsumer<TMessage, TMessageBuffer>.Read(ref TMessageBuffer buf)
		{
			return MsgC.Read(ref buf);
		}

		public int Count
		{
			get
			{
				return Queue.Msgs.Subtract(MsgC.Tail,Queue.MsgP.Head); 
			}
		}

		public void UpdateQueueStat(BQueueStat stat)
		{
			MsgC.UpdateQueueStat(stat);
			stat.Count += this.Count;
		}

		public int WaitCalls(int maxCount = -1, int ticksWait = -1)
		{
			var ready = MsgC.WaitData(ref Queue.Msgs, ticksWait);
			if (maxCount > 0 && ready > maxCount)
				ready = maxCount;
			return ready;
		}

		public int Available
		{
			get { return MsgC.GetAvailable(ref Queue.Msgs); }
		}

		public void BeginPopCall(ref Address fun)
		{
			fun = ArgsC.Read<Address>(ref Queue.Args);
		}

		public void EndPopCall(ref TMessage msg)
		{
			ArgsC.EndRead(ref Queue.Args, ref Queue.Refs);
			msg = MsgC.Read(ref Queue.Msgs);
		}

		public void PopArg<T>(ref T arg) where T : struct
		{
			arg = ArgsC.Read<T>(ref Queue.Args);
		}

		public T PopArg<T>() where T : struct
		{
			return ArgsC.Read<T>(ref Queue.Args);
		}

		public object PopRef()
		{
			return ArgsC.ReadRef(ref Queue.Refs);
		}
	}

	public class MessageConsumer:MessageConsumerImpl<
		Message,
		BPinnedRingBuffer<Message,NullValue<Message>>,
		BPinnedRingBuffer<byte,NullValue<byte>>,
		BRingBuffer<object,RefsNullValue>>
	{
		public MessageConsumer(MessageQueue queue, int consumerIndex):base(queue,consumerIndex)
		{
			
		}
	}

	public class MessageQueueImpl<TMessage, TMessageBuffer, TArgsBuffer, TRefsBuffer> 
		where TMessageBuffer:IBQueueBuffer<TMessage,IntPtr>
		where TMessage:struct
		where TArgsBuffer:IBArgsBuffer<byte,IntPtr>
		where TRefsBuffer:IBQueueBuffer<object,int>
		//where TMessageConsumer:IBQConsumer<TMessage,TMessageBuffer>
	{
		public const int DefaultQueueCapacity = 4*1024*1024;
		public const int MaxArgsPerMessage = 1;
		public const int MaxBytesPerMessage = 8;

		public TMessageBuffer Msgs;

		public TArgsBuffer Args;
		public TRefsBuffer Refs;

		public StructArray<IBQConsumer<TMessage, TMessageBuffer>> Consumers;

		public BQProducer<TMessage, TMessageBuffer, IntPtr> MsgP;

		public ArgsProducer<IntPtr, TArgsBuffer, int, TRefsBuffer> ArgsP;

		public MessageQueueImpl()
			: this(DefaultQueueCapacity, MaxArgsPerMessage, MaxBytesPerMessage)
		{
			
		}

		public MessageQueueImpl(int count, int refsPerMessage, int bytesPerMessage) 
		{
			Msgs.Init(count);
			bytesPerMessage += ByteArrayUtils.SizeOf<Address>();
			Args.Init(count * bytesPerMessage);
			Refs.Init(count * refsPerMessage);
			
			MsgP.Init(count);

			Consumers = new StructArray<IBQConsumer<TMessage, TMessageBuffer>>(32);
			AddConsumer();
		}

		public void UpdateQueueStat(BQueueStat stat)
		{
			for(int i=0;i<Consumers.Count;i++)
			{
				Consumers[i].UpdateQueueStat(stat);
			}
			stat.EnqueueFulls += MsgP.EnqueueFulls;
			stat.Capacity += Msgs.Capacity;
		}

		public MessageConsumer AddConsumer()
		{
			var cons = new MessageConsumer(this, Consumers.Count);
			Consumers.Add(cons);
			//this.MsgP._nullValue.Mask.Value = (1 << Consumers.Count) - 1;

			return cons;
		}

		public int Capacity
		{
			get { return Msgs.Capacity; }
		}

		public int BeginCallsBatch(int maxCount = 1, int ticksWait=-1)
		{
			return MsgP.BeginBatch(ref Msgs, maxCount, ticksWait);
		}


		public void PushArg<T>(T arg) where T:struct
		{
			ArgsP.Write(ref Args, arg);
		}

		public void PushRef(object arg)
		{
			ArgsP.WriteRef(ref Refs, arg);
		}
		
		
		public void BeginPushCall(Address fun)
		{
			PushArg(fun);
		}

		public void EndPushCall(Address fun)
		{
			ArgsP.EndWrite(ref Args,ref Refs);
			Message msg = new Message(fun);
			MsgP.Write(ref Msgs, msg);
		}	
	}

	public sealed class MessageQueue:MessageQueueImpl<
		Message, BPinnedRingBuffer<Message,NullValue<Message>>,
		BPinnedRingBuffer<byte,NullValue<byte>>,
		BRingBuffer<object,RefsNullValue>>
	{
	
	}
}
