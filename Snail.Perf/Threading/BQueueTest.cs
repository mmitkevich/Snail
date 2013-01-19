#define CHUNKED
#define MESSAGETEST
//#define MYMSG
#define ARGSBUF

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
		public struct TMessage
		{
			public int WP1;
			public int WP2;
			public int WP3;
			public long LP;
			/*
			public int WordArg4;
			public int WordArg5;
			/*public int WordArg6;*/

			/*public int WordArg0a;
			public int WordArg1a;
			public int WordArg2a;
			public int WordArg3a;
			public int WordArg4a;
			public int WordArg5a;
			public int WordArg6a;*/
#if true
			public RefAction<TMessage> Empty;
			public static RefAction<TMessage> _NotEmpty = method;
			public const RefAction<TMessage> _Empty = null;
			private static void method(ref TMessage msg)
			{
				
			}
#else
			public bool Empty;
			public const bool _NotEmpty = true;
			public const bool _Empty = false;
#endif

	
		}

		public struct TMessageElement:IBQueueElement<TMessage>
		{
			public void InitElement(ref TMessage obj)
			{
				//obj = new TMessage();
			}
			public void SetEmptyElement(ref TMessage obj)
			{
				obj.Empty = TMessage._Empty;
			}

			public bool IsNonEmptyElement(ref TMessage obj)
			{
				return obj.Empty != TMessage._Empty;
			}
		}
#if CHUNKED
		private static Action<MessageQueue, int> Fake;
		private static void Fake1(MessageQueue q, int count)
		{
			
		}
#else
		private static RefAction<Message> Fake;
		private static void Fake1(ref Message msg)
		{

		}
#endif
		public static unsafe void Run()
		{
			var queueSize = 8*1024*1024;
			Console.WriteLine("Allocating "+queueSize);
			var q = 
#if MESSAGETEST
#if MYMSG
				new BQueue<TMessage, TMessageElement>(queueSize);
#else
				new MessageQueue(new Mailbox(MailboxScheduler.Current));//, queueSize);
			var qm = q.Messages;
#endif
#else
				new BQueue<long,BQueueElement<long>>(queueSize);
						var qm = q;
#endif
			int Count =
#if DEBUG
				1024;
#else
				32 * 1024 * 1024;
#endif
			qm.Pin();
			Console.WriteLine("Starting " + Count);
			Fake = Fake1;
			ManualResetEvent done = new ManualResetEvent(false);
			for (int k = 0; k <2; k++)
			{
				Report rep = new Report("BQUEUE", "", "");
				rep.Run(Count, () =>
				{
					var t = Task.Factory.StartNew(() =>
					{
						int v = 0;
						int seq;

						void* pmsg = qm.BufferPtr;

						for (int i = 0; i < Count; i++)
						{
#if MESSAGETEST
							qm.WaitForData();
							int idx = qm.SeqToIdx(qm.Tail);
							var b = qm.Buffer;
#if ARGSBUF							
							v=q.Args.Read<int>();
#else
							v = (int) b[idx].WP1;
#endif
							qm.FreeTail();
#else
							v = (int) q.Dequeue();
#endif
							//if (v != i + 1)
							//	throw new InvalidOperationException();
						}
						Console.WriteLine("DEQ DONE");
					});
					int ve = 1;
					int parts = 4;
					Message msg=default(Message);
					for (int r = 0; r < parts;r++ )
					{
						for (int i = 0; i < Count / parts; i++)
						{
							ve++;					
#if MESSAGETEST							
							if(qm.Tail==qm.BatchTail)
								qm.RealWaitForFreeSlots();

							int idx = (int)(qm.Head & qm.Mask);
#if !ARGSBUF							
							q.Buffer[idx].WP1 = ve;
#else
							q.Args.Write(ve);
#endif
#if MYMSG
							q.Buffer[idx].Empty = TMessage._NotEmpty;
#else
							qm.Buffer[idx].Executor = Fake;
#endif
							qm.Head = qm.NextSeq(qm.Head);
#else
							q.Enqueue(ve);
#endif

						}
						Console.WriteLine("QS {0:F2}", (double)q.Messages.Count / q.Messages.Capacity);
					}
					Console.WriteLine("ENQ DONE");
					t.Wait();
				});
				Console.WriteLine(rep);
				Console.WriteLine("Backtracks {0} {1:F4}% EnqFulls {2} {3:F4}", q.Messages.Backtrackings,
								  (double)100 * q.Messages.Backtrackings / Count, q.Messages.EnqueueFulls, (double)100 * q.Messages.EnqueueFulls / Count);
			}
		}
	}
}
