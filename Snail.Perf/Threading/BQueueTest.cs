#define CHUNKED
#define MESSAGETEST
#define ARGSBUF

#define NO_TARGET_REF
#define INLINE_WORKLOAD

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Snail.Threading;
using Snail.Util;

namespace Snail.Tests.Threading
{
	public class BQueueTest
	{


		private class Target
		{
			public int Value;
			public void Func(MessageConsumer q, int count)
			{
				int n =  q.PopArg<int>();
				if(n!=Value+1)
				{
					throw new InvalidOperationException();
				}
				Value = n;
#if DEBUG
				Console.WriteLine("DEQ "+Value);
#endif
				var msg = default(Message);
				q.EndPopCall(ref msg);	// at the end of readiing
			}

			public void FuncInline(MessageConsumer q, int count)
			{
				int n = 0;
				var argsTail = q.ArgsC.ArgsTail;
				ByteArrayUtils.Read(q.Queue.Args.Buffer, (int)argsTail, ref n);
				
				argsTail += ByteArrayUtils.SizeOf<int>();
				q.ArgsC.ArgsTail = q.Queue.Args.Wrap(argsTail);
				
				if (n != Value + 1)
					throw new InvalidOperationException();
				Value = n;
#if DEBUG
				Console.WriteLine("DEQ " + Value);
#endif
				var tail = q.MsgC.Tail;
				q.Queue.Msgs.SetNull(tail);
				q.MsgC.Tail = q.Queue.Msgs.Wrap(q.Queue.Msgs.Inc(tail));
			}

		}


		private static Target _target;

		
		public const int TestRuns = 2;
#if DEBUG
		public const int QueueSize = 32;
		public const int TestCount = 128;
#else
		public const int QueueSize = 32*1024*1024;
		public const int TestCount = 128 * 1024 * 1024;
#endif
		public const int ArgsPerMsg = 1;
		public const int BytesPerMsg = 16;

		public static void Run()
		{
			RunPlainCycle("PlainCycle", TestCount);
			Run("BQueue<int>", new BQueue<int>(QueueSize),
				TestCount, WriteSimple, ReadSimple, DoneSimple);

			Run("BQueue<int>Inline", new BQueue<int>(QueueSize),
				TestCount, WriteInline, ReadInline, DoneSimple);
			
			Run("MessageQueueInline", new MessageQueue(QueueSize, ArgsPerMsg, BytesPerMsg),
				TestCount, WriteQueueInline, ReadQueueInline, DoneQueue);
			
			//Run("MessageQueue", new MessageQueue(QueueSize, ArgsPerMsg, BytesPerMsg),
			//	TestCount, WriteQueue, ReadQueue, DoneQueue);
		}

		private static void RunPlainCycle(string testCase, int TestCount)
		{
			Report rep = new Report(testCase, "", "");
			rep.Run(TestCount,
				()=>
				{
					int ve = 1;
					for(int i=0;i<TestCount;i++)
					{
						if(ve!=i+1)
							throw new InvalidOperationException();
						ve++;
					}
				}
				);
			Console.WriteLine(rep);
		}
		
		private static void ReadSimple(BQueue<int> q, int Count)
		{
			int v;
			int ve = 1;
			int parts = 4;
			for (int r = 0; r < parts; r++)
			{		for (int i = 0; i < Count/parts; i++)
				{
					v = q.Dequeue();
					if(v!=ve)
						throw new InvalidOperationException();
					ve++;
				}
			Console.WriteLine("QueueFilledAt {0:F2}", (double)q.Count / q.Impl.Buffer.Capacity);
			}
		}

		private static void WriteInline(BQueue<int> q, int Count)
		{
			for (int i = 0; i < Count; i++)
			{
				var h = q.Impl.P.Head;
				if (q.Impl.Buffer.Subtract(q.Impl.P.BatchHead, h) == 0)
					q.Impl.P.WaitForFreeSlots(ref q.Impl.Buffer);
				//q.MsgC.Write(i+1);
				q.Impl.Buffer[h]=i+1;
				q.Impl.P.Head = q.Impl.Buffer.AddWrap(q.Impl.P.Head,1);
			}
		}

		private static void ReadInline(BQueue<int> q, int Count)
		{
			int v;
			int ve = 1;
			int parts = 4;
			for (int r = 0; r < parts; r++)
			{
				for (int i = 0; i < Count / parts; i++)
				{
					var t = q.Impl.C.Tail;
					if (q.Impl.Buffer.Subtract(q.Impl.C.BatchTail, t) == 0)
						q.Impl.C.RealWaitData(ref q.Impl.Buffer);
					v = q.Impl.Buffer[t];
					q.Impl.Buffer.SetNull(t);
					q.Impl.C.Tail = q.Impl.Buffer.AddWrap(t,1);

					if (v != ve)
						throw new InvalidOperationException();
					ve++;
				}
				Console.WriteLine("QueueFilledAt {0:F2}", (double)q.Count / q.Impl.Buffer.Capacity);
			}
		}

		private static void WriteSimple(BQueue<int> q, int Count)
		{
			for (int i = 0; i < Count; i++)
			{
				q.Enqueue((i + 1));
			}
		}

		public static void DoneSimple(BQueue<int> q, int Count)
		{
			Console.WriteLine(q.GetQueueStats());
		}

		private static unsafe void ReadQueue(MessageQueue q, int Count)
		{
			for (int i = 0; i < Count; i++)
			{
				var cons = q.Consumers[0];
				cons.WaitCalls();
				var fun = default(Address);
				cons.BeginPopCall(ref fun);
				var obj = cons.PopRef();
				Address.ExecuteMethod(fun.ToPtr(), obj, cons, 1);
				
				if (_target.Value != i + 1)
					throw new InvalidOperationException();
			}
		}

		private static void WriteQueue(MessageQueue q, int Count)
		{
			int parts = 4;
			Address addr = Address.FromDelegate<MessageQueueReader>(_target.Func);
			int v = 1;
			for (int r = 0; r < parts;r++ )
			{
				for (int i = 0; i < Count / parts; i++)
				{
					q.BeginCallsBatch();

					q.BeginPushCall(addr);
					q.PushRef(_target);
					q.PushArg(v);
					q.EndPushCall(addr);
#if DEBUG
					Console.WriteLine("ENQ "+v);
#endif
					v++;
				}
				Console.WriteLine("QueueFilledAt {0:F2}", (double)q.Consumers[0].Count/ q.Capacity);
			}
		}

		private static unsafe void ReadQueueInline(MessageQueue q, int Count)
		{
			var cons = q.Consumers[0];
			if (cons == null)
				throw new InvalidOperationException();

			for (int i = 0; i < Count; i++)
			{
				if (cons.MsgC.Tail == cons.MsgC.BatchTail)
					cons.MsgC.RealWaitData(ref q.Msgs);

#if NO_TARGET_REF
				var obj = _target;
#else
				var refsTail = cons.ArgsC.RefsTail; 
				var obj = q.Refs.Buffer[(int)refsTail];
				refsTail++;
				cons.ArgsC.RefsTail = refsTail >= q.Refs.Buffer.Length ? 0 : refsTail;
#endif

#if INLINE_WORKLOAD
				_target.FuncInline(cons, 1);
#else
				var fun = q.Messages[(int)cons.MsgC.Tail];
				Address.ExecuteMethod(fun.ToPtr(), obj, cons, 1);
#endif

				if (_target.Value != i + 1)
					throw new InvalidOperationException();
			}
		}

		private static void WriteQueueInline(MessageQueue q, int Count)
		{
			int parts = 4;
			Address addr = Address.FromDelegate<MessageQueueReader>(_target.FuncInline);
			int v = 1;
			Message msg = default(Message);
			for (int r = 0; r < parts; r++)
			{
				for (int i = 0; i < Count / parts; i++)
				{
					if (q.MsgP.GetSlotsAvailable(ref q.Msgs) == 0)
						q.MsgP.BeginBatch(ref q.Msgs);

					
#if NO_TARGET_REF
#else
					var refsHead = q.ArgsP.RefsHead;
					q.Refs[(int)refsHead] = _target;
					refsHead++;
					q.ArgsP.RefsHead = refsHead >= q.Refs.Buffer.Length ? 0 : refsHead;
#endif

					//var argsHead = q.ArgsP.ArgsHead;
					//ByteArrayUtils.Write(argsHead, ref v);
					//q.ArgsP.ArgsHead = q.Args.AddWrap(argsHead,sizeof (int));
					q.ArgsP.Write(ref q.Args, v);

					var head = q.MsgP.Head;
					q.Msgs[head] = new Message(addr);
					q.MsgP.MoveNext(ref q.Msgs);
#if DEBUG
					Console.WriteLine("ENQ "+v);
#endif
					v++;
				}
				Console.WriteLine("QueueFilledAt {0:F2}", (double)q.Consumers[0].Count / q.Capacity);
			}
		}

		public static void DoneQueue(MessageQueue q,int Count)
		{
			Console.WriteLine(q.GetQueueStats());
		}
		
		public static unsafe void Run<TQueue>(string testCase, TQueue q, int Count, Action<TQueue,int> Write, Action<TQueue,int> Read, Action<TQueue,int> Done)
		{
			//qm.Pin();
			ManualResetEvent done = new ManualResetEvent(false);

			var abook = new AddressBook();

			for (int k = 0; k < TestRuns; k++)
			{
				_target = new Target();

				Report rep = new Report(testCase, "", "");
				rep.Run(Count, () =>
				{
					var t = Task.Factory.StartNew(() => { Read(q, Count);Console.WriteLine("DEQ DONE"); });
					Write(q, Count);
					Console.WriteLine("ENQ DONE");
					t.Wait();
					Done(q, Count);
				});
				Console.WriteLine(rep);
			}
		}
	}
}
