using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Snail.Threading;

namespace Snail.Tests.Threading
{
	public class BQueueTest
	{
		public static void Run()
		{
			BQueue q = new BQueue(128 * 1024);
			int Count = 256 * 1024 * 1024;

			ManualResetEvent done = new ManualResetEvent(false);
			for (int k = 0; k < 3; k++)
			{
				Report rep = new Report("BQUEUE", "", "");
				rep.Run(Count, () =>
				{
					var t = Task.Factory.StartNew(() =>
					{
						int v = 0;
						for (int i = 0; i < Count; i++)
						{
							v = q.Dequeue();
							if (v != i + 1)
								throw new InvalidOperationException();
						}
						Console.WriteLine("DEQ DONE");
					});
					for (int i = 0; i < Count; i++)
						q.Enqueue(i + 1);
					Console.WriteLine("ENQ DONE");
					t.Wait();
				});
				Console.WriteLine(rep);
				Console.WriteLine("Backtracks {0} {1:F4}% EnqBatches {2} {3:F4}", q.BacktrackCount,
								  (double)100 * q.BacktrackCount / Count, q.EnqueueBatches, (double)100 * q.EnqueueBatches / Count);
			}
		}
	}
}
