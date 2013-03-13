using System;
using System.Threading;
using Snail.Threading.A1;

namespace Snail.Tests.Threading.A1
{
	class A1Test
	{
		public static void Run()
		{
			TwoAsync();
		}

		private static Actor a, b;
		private static int BufSize= 1024*1024;
		private static int Count = 100000;
		private static ManualResetEvent mru = new ManualResetEvent(false);
		private static long sum;
		private static void DumpMsg(Message<int> m)
		{
			//var t = m.Target;
			int r = m.Arg;
			sum += r;
			//Console.WriteLine("{3} From {0} To {1} At {2}", m.Source.GetHashCode(), m.Target.GetHashCode(), Thread.CurrentThread.ManagedThreadId,m.Arg);
		}

		public static void TwoAsync()
		{
			a = new Actor(new Mailbox(true, new MPSCBoundedQueue<IMessage>(BufSize,()=>new Message<int>())));
			b = new Actor(new Mailbox(true, new MPSCBoundedQueue<IMessage>(BufSize,()=>new Message<int>())));
		
			Console.WriteLine("a {0} b{1}",a.GetHashCode(),b.GetHashCode());

			Report report = new Report("Agent","Async","C"+Count);
			report.Run(Count, () =>
			                  {
			                  	var m = new Message<int>(a, b, DumpMsg);
			                  	for (int i = 0; i < Count; i++)
			                  		m.Post(i);
								b.Mailbox.WaitWork();
			                  });
			Console.WriteLine("HWT {0} PWT {1}",Mailbox.HWT,Mailbox.PWT);
			Console.WriteLine(report);
			if(sum!=(long)Count*(Count-1)/2)
				throw new InvalidOperationException();
			Console.WriteLine("sum="+sum);
		}
	}
}
