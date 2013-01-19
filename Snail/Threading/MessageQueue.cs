#define CHUNKED
//#define FIXED
#define INLINE_EMPTY_ELEMENT
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Snail.Threading
{


	[StructLayout(LayoutKind.Sequential,Pack = 1, CharSet = CharSet.Unicode)]
	public unsafe struct Message
	{
		//public object Source;
#if FIXED
		public FixedArgs FixedArgs;
#else
		//public long WP1;
#endif
#if CHUNKED
		[MarshalAs(UnmanagedType.FunctionPtr)]
		public Action<MessageQueue,int> Executor;
#else
		[MarshalAs(UnmanagedType.FunctionPtr)]
		public RefAction<Message> Executor;
#endif

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

	public sealed class MessageQueue 
	{
		public Mailbox TargetMailbox;
		public const int DefaultQueueCapacity = 4*1024*1024;
		public const int MaxArgsPerMessage = 1;
		public const int MaxBytesPerMessage = 8;

		public ArgsBuffer Args;
		public BQueue<Message, MessageElement> Messages;

		public MessageQueue(Mailbox target)
			: this(target, DefaultQueueCapacity, MaxArgsPerMessage, MaxBytesPerMessage)
		{
			
		}

		public MessageQueue(Mailbox target, int count, int argsPerMessage, int bytesPerMessage) 
		{
			Messages = new BQueue<Message, MessageElement>(count);
			TargetMailbox = target;
			Args = new ArgsBuffer(count, argsPerMessage, bytesPerMessage);
		}

		public void Read<T>(ref T obj) where T:struct
		{
			Args.Read(ref obj);
		}
		public void HaveTasks()
		{
			TargetMailbox.HaveTasks();
		}
	}
}
