//#define CHUNKED
//#define FIXED
#define INLINE_EMPTY_ELEMENT
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Snail.Threading
{
	//[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
	public unsafe struct FixedArgs
	{
		public const int MAX_WORD_ARGS = 4
			;
		public fixed ulong WordArgs[MAX_WORD_ARGS];

		public T Read<T>(ref int p)
		{
			T value = default(T);
			p = Read<T>(p, ref value);
			return value;
		}

		public int Read<T>(int p, ref T obj)
		{
			fixed (ulong* p0 = WordArgs)
			{
				ulong* ptr = p0 + p;
#if IL
			ldloc ptr
			ldobj !!T
			stobj !!T
			ldarg p
			sizeof !!T
			ldc.i4 3
			shr
			add
			ret
#endif
				ulong *fake = ptr;
			}
			return p;
		}

		public int Write<T>(int p, ref T obj)
		{
			fixed (ulong* p0 = WordArgs)
			{
				ulong* ptr = p0 + p;
#if IL
			ldloc ptr
			ldarg obj 
			ldobj !!T
			stobj !!T
			ldarg p
			sizeof !!T
			ldc.i4 3
			shr
			add
			ret
#endif
				ulong* fake = ptr;
			}
			return p;
		}
	}

	[StructLayout(LayoutKind.Sequential,Pack = 1, CharSet = CharSet.Unicode)]
	public struct Message
	{

		public const int MAX_REF_ARGS = 16;

		//public object Source;
#if FIXED
		public FixedArgs FixedArgs;
#else
		public long WP1;
		//public int WP2;
		//public int WP3;
		//public long LP;
#endif
#if CHUNKED
		public Action<MessageQueue,int> Executor;
#else
		public RefAction<Message> Executor;
#endif
		/*public object RefArg0;
		public object RefArg1;
		public object RefArg2;
		public object RefArg3;
		*/
		public static Message Empty;

	}

	public struct MessageElement:IBQueueElement<Message>
	{
		public void InitElement(ref Message obj)
		{

		}
		public void SetEmptyElement(ref Message obj)
		{
			obj.Executor = null;
		}

		public bool IsNonEmptyElement(ref Message obj)
		{
			return obj.Executor != null;
		}

	}

	public sealed class MessageQueue : BQueue<Message, MessageElement>
	{
		public Mailbox TargetMailbox;

		public MessageQueue(Mailbox target)
			: this(target, 32 * 1024 * 1024)
		{
			
		}
		public MessageQueue(Mailbox target,int count) 
			: base(count, Message.Empty, false)
		{
			TargetMailbox = target;
		}

		public void HaveTasks()
		{
			TargetMailbox.HaveTasks();
		}
	}
}
