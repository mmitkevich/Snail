#define TOTAL_NOP
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Disruptor;
using Disruptor.Dsl;
using Disruptor.Scheduler;
using Snail.Threading;
using Snail.Util;

namespace Snail.Tests.Disruptor
{
	internal class Security
	{
		public String Name;
		public Volatile.Integer Visitors = new Volatile.Integer(0);
		public int LastOrderId;

		public Resource Resource = new Resource();

		public Security(string name, int index)
		{
			Resource.Index = index;
			Name = name;
		}

		public override string ToString()
		{
			return string.Format("{0}, visited {1}", Name, Visitors);
		}
	}

	internal class Order
	{
		public Security Security;
		public int Id=-1;

		public override string ToString()
		{
			return string.Format("{0,4} {1}", Id, Security);
		}
	}

	internal class OrderHandler:IEventHandler<Order>
	{
		public int Processed;
		public static bool NOP = true;
		public long Sum;

		public void OnNext(Order data, long sequence, bool endOfBatch)
		{
			Sum+=data.Id;
#if !TOTAL_NOP			
			if (NOP)
				return;
			
			Processed++;

			if (data.Security == null)
			{
				MicroLog.Error("data.Security==null, data.Id={0},seq={1},eOfB={2}", data.Id,sequence,endOfBatch);
				return;
			}

	
			
			OnOrder(data);
#endif
		}

		private void OnOrder(Order data)
		{
			int visitors = data.Security.Visitors.AtomicIncrementAndGet();

			if(visitors > 1)
				throw new InvalidOperationException(data.Security+" Visitors="+visitors);

			MicroLog.Info("{4:13}, Sec {0,10}, Order {1,3}, Owner {2,3} Processed {3,3}", data.Security, data.Id, data.Security.Resource.Owner, Processed, "Entry");


			data.Security.LastOrderId = data.Id;


			data.Security.Visitors.AtomicDecrementAndGet();
			MicroLog.Info("{4:13}, Sec {0,10}, Order {1,3}, Owner {2,3} Processed {3,3}", data.Security, data.Id, data.Security.Resource.Owner, Processed, "Exited");
		}
	}

	internal class WorkerPoolTests
	{
		public static Security[] CreateSecurities(int count)
		{
			var list = new List<Security>();

			for (int i = 0; i < count; i++)
				list.Add(new Security("S" + i,i));
			return list.ToArray();
		}



		public const int Size = 1024*1024;
		public const int MILLION = 1000000;
		public const int BIGCOUNT = 5*MILLION;
		public const int COUNT = 50*MILLION;
		public const int NUMSEC = 2;

		private static List<Report> _reports = new List<Report>();
		private static Dictionary<string, Func<IClaimStrategy>> _claimStrategies = new Dictionary<string, Func<IClaimStrategy>>();
		private static Dictionary<string, Func<IWaitStrategy>> _waitStrategies = new Dictionary<string, Func<IWaitStrategy>>();
		private static Dictionary<string, ResourceStrategy<Order>> _resStrategies = new Dictionary<string, ResourceStrategy<Order>>();

		public class Publisher:IDisruptor<Order>
		{
			private IEventHandler<Order> _oh;
			private Order _order;
			private long _seq;

			public Publisher(OrderHandler oh)
			{
				_oh = oh;
				_seq = 0;
				 _order = new Order();
			}
			public void PublishEvent(Func<Order,long,Order> translator)
			{
				translator(_order, _seq);
				_oh.OnNext(_order,_seq,false);
				_seq++;
			}

			public void Start()
			{
				
			}

			public void Shutdown()
			{
				
			}

			public IEventHandler<Order>[] EventHandlers
			{
				get { return new[] {_oh}; }
			}

			public void PublishEvent(IEventTranslator<Order> translator, object resource)
			{
				throw new NotImplementedException();
			}

			public void PublishEvent(IEventTranslator<Order> translator)
			{
				translator.TranslateTo(_order, _seq);
				_oh.OnNext(_order, _seq, false);
				_seq++;
			}
		}

		

		class Translator : IEventTranslator<Order>
		{
			public int Index;
			public Security Security;

			public Order TranslateTo(Order data, long seq)
			{
				data.Id = Index;
				data.Security = Security;
#if !TOTAL_NOP				
				if (!OrderHandler.NOP)
				{
					MicroLog.Info("{4}, {3}, Sec {0,10}, Order {1,3}, Owner {2,3}", data.Security,
					              data.Id, data.Security.Resource.Owner, "Publish", seq);
				}
#endif
				return data;
			}
		}

		private static void RunCycles()
		{
			var order = new Order();
			var securities = CreateSecurities(NUMSEC);
			

			int count = BIGCOUNT;
			long goodValue = (long)count * (count - 1) / 2;

			var crep = new Report("Cycle", "", "");
			long sum = 0;
			crep.Run(count,
					 () =>
					 {
						 for (int i = 0; i < count; i++)
						 {
							 order.Security = securities[i % securities.Length];
							 order.Id = i;

							 sum += (long)order.Id;
						 }
					 });

			

			_reports.Add(crep);
			Console.WriteLine(crep);
			
			if(sum!=goodValue)
				throw new InvalidOperationException(string.Format("{0}!={1}",sum,goodValue));

			
			var oh = new OrderHandler();
			
			var hrep = new Report("Cycle Handler", "", "");
			hrep.Run(count,
					 () =>
					 {

						 for (int i = 0; i < count; i++)
						 {
							 order.Id = i;
							 order.Security = securities[i % securities.Length];
							 oh.OnNext(order,(long)i,true);	 
						 }
					 });

			_reports.Add(hrep);
			Console.WriteLine(hrep);

			if (oh.Sum!= goodValue)
				throw new InvalidOperationException(string.Format("{0}!={1}", sum, goodValue));

			/*Func<Order, long, Order> pub =
					   (o, seq) =>
					   {
						   o.Security = sec;
						   o.Id = (int)seq;
						   return o;
					   };
			*/
			Translator translator = new Translator();

			oh.Sum = 0;

			var phrep = new Report("Cycle DelegateTranslator Handler", "", "");
			phrep.Run(count,
					 () =>
					 {
						 for (int i = 0; i < count; i++)
						 {
						 	translator.Index = i;
							translator.Security = securities[i % securities.Length];
							 oh.OnNext(translator.TranslateTo(order,(long)i), (long)i, true);
						 }
					 });

			_reports.Add(phrep);
			Console.WriteLine(phrep);
			
			if (oh.Sum != goodValue)
				throw new InvalidOperationException(string.Format("{0}!={1}", sum, goodValue));

			var phsrep = new Report("Cycle StaticTranslator Handler", "", "");
			
			oh.Sum = 0;
			phsrep.Run(count,
					 () =>
					 {

						 for (int i = 0; i < count; i++)
						 {
						 	order.Id = i;
							StaticSec = securities[i % securities.Length];
							oh.OnNext(StaticPublicate(order,(long)i), (long)i, true);
						 }
					 });

			_reports.Add(phsrep);
			Console.WriteLine(phsrep);
			if (oh.Sum != goodValue)
				throw new InvalidOperationException(string.Format("{0}!={1}", sum, goodValue));

			var phdrep = new Report("Cycle Publisher Delegate Translator Handler", "", "");
			var publisher = new Publisher(oh);
			oh.Sum = 0;

			//Func<Order, long, Order> del = translator.Translate;

			phdrep.Run(count,
					 () =>
					 {
						 for (int i = 0; i < count; i++)
						 {
						 	translator.Index = i;
						 	translator.Security = securities[i%securities.Length];
							publisher.PublishEvent((o,seq)=>translator.TranslateTo(o,seq));
						 }
					 });

			_reports.Add(phdrep);
			Console.WriteLine(phdrep);

			if (oh.Sum != goodValue)
				 throw new InvalidOperationException(string.Format("{0}!={1}", oh.Sum, goodValue));

			var phirep = new Report("Cycle Publisher Translator Handler", "", "");
			oh.Sum = 0;

			phirep.Run(count,
					 () =>
					 {
						 for (int i = 0; i < count; i++)
						 {
							 translator.Index = i;
							 translator.Security = securities[i % securities.Length];
							 publisher.PublishEvent(translator);
						 }
					 });

			_reports.Add(phirep);
			Console.WriteLine(phirep);

			if (oh.Sum != goodValue)
				throw new InvalidOperationException(string.Format("{0}!={1}", oh.Sum, goodValue));
		}

		private static Security StaticSec = null;
		static Order StaticPublicate(Order o, long seq)
		{
		   o.Security = StaticSec;
		   o.Id = (int)seq;
		   return o;
		}


		public static void Run()
		{
			ConfigureStrategies();
			
			MicroLog.WhenFull = MicroLog.WhenFullAction.Overwrite;

			//RunCycles();

			for (int numProducers = 1; numProducers <= 2; numProducers++)
			{
				int maxWorkers = 2;
				int minWorkers = numProducers <= 1 ? 0 : 1;

				//var wait = "YieldingWaitStrategy";
				var wait = "BusySpinWaitStrategy";
				//var res = "ResourceStrategy";
				foreach (var res in _resStrategies.Keys)
				{
					//foreach(var claim in _claimStrategies.Keys) 
					//var claim = "SingleThreadedClaimStrategy";
					var claim = numProducers > 1
					            	? "MultiThreadedLowContentionClaimStrategy"
					            	: "SingleThreadedClaimStrategy";

					for (int numWorkers = minWorkers; numWorkers <= maxWorkers; numWorkers++)
					{
						Run(numWorkers, numProducers, claim, wait, res);
					}
				}
			}
			MicroLog.Dump4Ever();
		}

		static void ConfigureStrategies()
		{
			_claimStrategies.Add("MultiThreadedClaimStrategy", () => new MultiThreadedClaimStrategy(Size));
			_claimStrategies.Add("MultiThreadedLowContentionClaimStrategy", () => new MultiThreadedLowContentionClaimStrategy(Size));
			_claimStrategies.Add("SingleThreadedClaimStrategy", () => new SingleThreadedClaimStrategy(Size));

			_waitStrategies.Add("BusySpinWaitStrategy", () => new BusySpinWaitStrategy());
			_waitStrategies.Add("YieldingWaitStrategy", () => new YieldingWaitStrategy());
			_waitStrategies.Add("SleepingWaitStrategy", () => new SleepingWaitStrategy());
			_waitStrategies.Add("BlockingWaitStrategy", () => new BlockingWaitStrategy());

			_resStrategies.Add("LeastLoadResourceStrategy", new LeastLoadResourceStrategy<Order>(order => order.Security.Resource));
			_resStrategies.Add("ResourceStrategy", new ResourceStrategy<Order>());
		}

		static IDisruptor<Order> CreatePool(int numThreads, string claim, string wait, string res)
		{
			Func<IClaimStrategy> claimStrategy = numThreads>0?_claimStrategies[claim]:null;
			Func<IWaitStrategy> waitStrategy = numThreads>0?_waitStrategies[wait]:null;
			if (numThreads == 0)
				return new NoDisruptor<Order>(new Order(), new OrderHandler());
			else if(numThreads==1)
			{
				var d = new Disruptor<Order>(
					()=>new Order(),
					claimStrategy(),
					waitStrategy(),
					TaskScheduler.Default);
				d.HandleEventsWith(new OrderHandler());
				return d;
			}

			else
			{
				var d = new MultiDisruptor<Order>(numThreads,
					() => new Order(),
					_resStrategies[res], 
					claimStrategy,
					waitStrategy,
					TaskScheduler.Default// new RoundRobinThreadAffinedTaskScheduler(numThreads)
					);
				d.HandleEventsWith(() => new OrderHandler());
				return d;
			}
		}
		
		

		static int _publishedCount = 0;
		public static void Run(int numThreads, int numProducers, string claim, string wait, string res)
		{

			var securities = CreateSecurities(NUMSEC);

			IDisruptor<Order> pool = CreatePool(numThreads, claim, wait, res);

			pool.Start();

			Report rep = new Report(string.Format("P{0}C{1}.",numProducers,numThreads),"",claim+"."+wait+"."+res);

			rep.Run(COUNT,
			() =>
			{
				_publishedCount = 0;
				ManualResetEvent done = new ManualResetEvent(false);

				Action<Object> sender = obj =>
				             {
				             	int jj = (int) obj;
				             	Translator ot = new Translator();
				             	ot.Security = securities[jj];
				             	int mask = numProducers - 1;
				             	Resource rs = ot.Security.Resource;
				             	if (pool is NoDisruptor<Order>)
				             	{
				             		var nopool = new Publisher(new OrderHandler());
				             		pool = nopool;

									IEventHandler<Order> handler = nopool.EventHandlers[0];
				             		Order e = new Order();
				             		long seq = 0;
				             		
				             		for (int i = jj; i < COUNT; i ++)
				             		{
				             			ot.Index = i;
				             			ot.Security = securities[0];// securities[i % securities.Length];
#if false
				             			nopool.PublishEvent(ot);
#else // inlined publish
										ot.TranslateTo(e, seq++);
										handler.OnNext(e, seq, true);	
#endif
				             		}
				             	}
				             	else if(numThreads==1)
				             	{
				             		var d = pool as Disruptor<Order>;
				             		var rb = d.RingBuffer;

				             		for (int i = jj; i < COUNT; i += numProducers)
				             		{
				             			//ot.Index = i;
										//ot.Security = securities[0];// securities[i % securities.Length];
//				             			pool. PublishEvent(ot);

				             			var seq = rb.Next();
				             			rb[seq].Id = i;
										rb.Publish(seq);
				             		}
				             	}else
				             	{
				             		for (int i = jj; i < COUNT; i += numProducers)
				             		{
				             			ot.Index = i;
										ot.Security = securities[0];// securities[i % securities.Length];
				             			pool.PublishEvent(ot,rs);
				             		}
				             	}
				             	if (Interlocked.Add(ref _publishedCount, 1) == numProducers)
				             		done.Set();
				             };
				
				for(int j=1;j<numProducers;j++)
					Task.Factory.StartNew( sender,j);

				sender(0);


				done.WaitOne();
				pool.Shutdown();
				long sumValue = (long)(-1 + COUNT) * COUNT / 2;
				long actValue = pool.EventHandlers.Sum(ev => ((OrderHandler) ev).Sum);
				if (actValue!= sumValue)
					throw new InvalidOperationException(string.Format("{1}!={0}",sumValue,actValue));
			});
			_reports.Add(rep);
			

			Console.WriteLine(rep.ToString());
		}

		
	}
}
