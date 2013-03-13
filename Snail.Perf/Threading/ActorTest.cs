//#define DIFF
#define CHUNKED
//#define FIXED
#define ARGSBUF
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Scheduler;
using Snail.Threading;
using Snail.Util;

namespace Snail.Tests.Threading
{
	public interface ISecurity
	{
		void PlaceOrder(int orderId, int price, int volume);
		void Check(long sum);
	}


	public class ActorId
	{
		public Host Host { get; set; }
	}

	public class SecurityProxyFactory
	{
		public static ISecurity CreateSecurityProxy(Security target)
		{
			Host host = Host.Current;
			var channel = host.GetProducer(target.Mailbox.Host);
			return new SecurityRemoteProxy(channel, target);
		}
	}

	public sealed class SecurityRemoteProxy : ISecurity
	{
		// channel to target
		protected MessageQueue _channel;
		protected ActorId _targetId;
		protected Security _target;
		protected Host _host;

		protected Address _r_PlaceOrder;
		protected Address _r_Check;

		public SecurityRemoteProxy(MessageQueue channel, Security target)
		{
			_target = target;
			_channel = channel;

			_r_PlaceOrder = Address.FromDelegate<MessageQueueReader>(target.r_PlaceOrder);
			_r_Check = Address.FromDelegate<MessageQueueReader>(target.r_Check);
		}
	
		

		public unsafe void PlaceOrder(int orderId, int price, int volume)
		{
			_channel.BeginCallsBatch();

			_channel.BeginPushCall(_r_PlaceOrder);
			_channel.PushArg(_r_PlaceOrder);
			_channel.PushRef(this);
			_channel.PushArg(orderId);
			_channel.EndPushCall(_r_PlaceOrder);
			
		}

		public unsafe void Check(long sum)
		{
			int maxCallsToPush = _channel.BeginCallsBatch();
			_channel.BeginPushCall(_r_Check);
			_channel.PushRef(this);
			_channel.PushArg(sum);
			_channel.EndPushCall(_r_PlaceOrder);
		}

	}
	
	public struct Delay
	{
		private int _count;
		
		public const int CycleFactor = 1000;
		
		public Delay(int count = 0)
		{
			_count = count;
		}
		
		[MethodImpl(MethodImplOptions.NoOptimization)]
		public void Spin()
		{
			for (int k = 0; k < _count * CycleFactor; k++) ;
		}
	}

	public class Caller:IActor
	{
		private Mailbox _mailbox;

		private ISecurity _security;
		
		private Delay _delay;

		public Mailbox Mailbox
		{
			get { return _mailbox; }
		}

		public Caller(Mailbox mailbox, ISecurity security, Delay delay)
		{
			_mailbox = mailbox;
			_security = security;
			_delay = delay;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void DoCall(int i)
		{
			_security.PlaceOrder(i, i, i);
			_delay.Spin();
		}

		public void Test(int count)
		{
			Mailbox.Host.EnterActor(this);
			//if (_target is SecurityProxy)
			//	((SecurityProxy) _target).HaveTasks();
			var done = false;
			var producer = new Task(
				() =>
				{
					MicroLog.Info("TestTask " + Task.CurrentId + " Affinity " + RoundRobinThreadAffinedTaskScheduler.CurrentThreadProcessorIndex);
					for (int i = 0; i < count; i++)
						DoCall(i);
					MicroLog.Info("Producer1 done " + done);
					done = true;
				});
			producer.Start(Scheduler.Current.TaskScheduler);
			
			producer.Wait();
			MicroLog.Info("MsgP done "+done);
			Scheduler.Current.WaitAll(); 
			MicroLog.Info("Consumer done ");
			_security.Check(((long)count - 1) * count / 2);

			Host.Current.ExitActor(this);
		}
	}

	public class SecurityImpl:ISecurity
	{
		public static int _price;
		private long _sum;
		private Delay _delay;

		public SecurityImpl(Delay delay)
		{
			_delay = delay;
		}

		public void PlaceOrder(int orderId, int price, int volume)
		{
			_price = price;
			_sum += _price;
			_delay.Spin();
		}

		public void Check(long sum)
		{
			MicroLog.Info("We have {0} and need {1}", _sum, sum);
			Console.Out.Flush();
			if (_sum != sum)
				new InvalidOperationException();
		}

	}

	public class Security : IActor
	{
		private readonly SecurityImpl _impl;
		
		private Mailbox _mailbox;

		private Delay _delay;
		private static Address _r_Check;
		private static Address _r_PlaceOrder;
		public Mailbox Mailbox
		{
			get { return _mailbox; }
		}

		public ISecurity Impl
		{
			get { return _impl; }
		}

		public Security(Mailbox mailbox, Delay delay)
		{
			_mailbox = mailbox;
			_impl = new SecurityImpl(delay);
		}

		static Security()
		{
			_r_Check = new Address(typeof (Security).GetMethod("r_Check").MethodHandle.GetFunctionPointer().ToInt64());
			_r_PlaceOrder = new Address(typeof(Security).GetMethod("r_PlaceOrder").MethodHandle.GetFunctionPointer().ToInt64());
		}

		public unsafe void r_PlaceOrder(MessageConsumer q, int count)
		{
			object target;
			int done = 0;
			Message msg = default(Message);
			Address nextFun = default(Address);
			Address fun = _r_PlaceOrder;
#if IL
			ldftn instance void Snail.Tests.Threading/Security::r_PlaceOrder(class Snail.Threading.MessageConsumer,valuetype Snail.Threading.Message&,int32)
#endif
			while (true)
			{
				int WP1 = 0;

				q.PopArg(ref WP1);
				q.EndPopCall(ref msg);
				_impl.PlaceOrder(WP1, WP1, WP1);

				done++;
				if (done >= count)
					return;

				// go to next
				q.BeginPopCall(ref nextFun);
				target = q.PopRef();

				if (nextFun.Value!=fun.Value || target!=this)
					break;
			}
			count -= done;
			Address.ExecuteMethod(fun.ToPtr(), target, q, count);
		}

		public void r_Check(MessageConsumer q, int count)
		{
			object target = null;
			int i = 0;
			Message msg = default(Message);
			Address nextFun = default(Address);
			Address fun = _r_Check;

			while (true)
			{
				_impl.Check(q.PopArg<long>());
				q.EndPopCall(ref msg);

				i++;
				if (i >= count)
					return;

				// go to next
				q.BeginPopCall(ref nextFun);
				target = q.PopRef();

				if (nextFun.Value != fun.Value || target != this)
					break;
			}
			count -= i;
			Address.ExecuteMethod(fun.ToPtr(), target, q, count);
		}
	}

	class ActorTest
	{
		public const int JOB = 0;//20;
		public const int SPIN = 60;
		private static int COUNT = 
#if DEBUG
	1024
#else
	160*1024*1024/(JOB+1)
#endif
	;

		private static void Run(String name, Caller caller)
		{
			var rep = new Report(name, "", "");
			rep.Run(COUNT, () => caller.Test(COUNT));
			MicroLog.Info(""+rep);
		}
		class SomeObj
		{
			
		}
		struct SomeStru
		{
			int x;
			public SomeObj r;
		}

		static SomeStru[] arr;
		public static void a1()
		{
			arr=new SomeStru[4];

			SomeStru a = new SomeStru();
			a.r = new SomeObj();
			var b = a;
			arr[1] = a;
		}

		public static void a2()
		{
			//ArgsBuffer bu = new ArgsBuffer();
			//var a=new SomeObj();
			//bu.WriteRef(a);
		}
		public static void Run()
		{
			//a1();
			//a2();
			//return;

			MicroLog.Writers.Add(new StreamWriter("ActorTest.mlog"));
			//MicroLog.Writers.Clear();
			MicroLog.WhenFull = MicroLog.WhenFullAction.NoCaching;
			var abook = new AddressBook();
			
			var net = new Network();
			
			var implHost = new Host(net, Scheduler.Current);

			var callerHost = new Host(net, Scheduler.Current);
			var delay = new Delay(JOB/2);

			for (int k = 0; k < 4; k++)
			{
				var implBox = new Mailbox(implHost);


				var impl = new SecurityImpl(delay);
				var callerBox = new Mailbox(callerHost);
				var callerImpl = new Caller(callerBox, impl, delay);

				Run("P0C0", callerImpl);

				implBox = new Mailbox(implHost);

				var sec = new Security(implBox, delay);
				var proxy = SecurityProxyFactory.CreateSecurityProxy(sec);
				var callerProxy = new Caller(callerBox, proxy, delay);

				Run("P1C1", callerProxy);
				MicroLog.Info("HaveWorks:{0}", sec.Mailbox.HaveWorks);
//				var q = proxy.Queue;
//				MicroLog.Info("Backtracks {0} {1:F4}% EnqFulls {2} {3:F4}", q.Msgs.Backtrackings,
//								  (double)100 * q.Msgs.Backtrackings / q.Msgs.Tail, q.Msgs.EnqueueFulls, (double)100 * q.Msgs.EnqueueFulls / q.Msgs.Tail);

				Console.ReadKey();
			}
		}
	}
}
