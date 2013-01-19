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
	public interface ISecurity:IActor
	{
		void PlaceOrder(int orderId, int price, int volume);
		void Check(long sum);
	}


	public class SecurityProxy : ActorProxy<SecurityImpl>, ISecurity
	{

		private Mode _mode;
		public enum Mode
		{
			Zero,
			FullQueue,
			SerializeOnly,
			SerializeInlined
		}

		public SecurityProxy(SecurityImpl target, Mode mode = Mode.FullQueue) : base(target)
		{
			_mode = mode;
#if CHUNKED			
			readCheck = VreadCheck;
			readPlaceOrder = VreadPlaceOrder;
#else
			readCheck = rCheck;
			readPlaceOrder = rPlaceOrder;

#endif
			rd = rPlaceOrder0;
		}
	
/*
		public static Action<IActor, ArgsBuffer> readPlaceOrder = 
			(dst, queue) =>
			{
				var orderId = queue.Read<int>();
				var price = queue.Read<int>();
				var volume = queue.Read<int>();
				var actor = dst as ISecurity;
				actor.PlaceOrder(orderId, price, volume);
			};

		public static Action<IActor, ArgsBuffer> readCheck =
			(dst, queue) =>
			{
				var sum = queue.Read<long>();
				var actor = dst as ISecurity;
				actor.Check(sum);
			};
		*/
#if CHUNKED
		public Action<MessageQueue, int> readPlaceOrder;
#else
		public RefAction<Message> readPlaceOrder;
#endif

		private void VreadPlaceOrder(MessageQueue q, int count)
		{
			var tail = q.Messages.Tail;
			var idx = q.Messages.SeqToIdx(tail);
			var b = q.Messages.Buffer;
			var mask = q.Messages.Mask;
				int WP1=0;

			for (int i = 0; i < count; i++)
			{
#if !ARGSBUF
				_target.PlaceOrder((int)b[idx].WP1, (int)b[idx].WP1, (int)b[idx].WP1);
#else
				//var WP1 = args.Read<int>();
				q.Args.Read<int>(ref WP1);
				//ByteArrayUtils.Read(q.Args.Buffer, q.Args.Tail, ref WP1);
				//q.Args.Tail += ByteArrayUtils.SizeOf<int>();

				_target.PlaceOrder(WP1, WP1, WP1);
#endif
				b[idx].Executor = null;
				idx++;
				idx&=mask;
			}
			q.Messages.Tail = tail + count;
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		private void rPlaceOrder0(ref Message msg)
		{
			
		}
#if !CHUNKED
		//[MethodImpl(MethodImplOptions.NoInlining)]
		private void rPlaceOrder(ref Message msg)
		{
				//int s = 0;
				//var orderId = msg.Read<int>(ref s);
				//var price = msg.Read<int>(ref s);
				//var volume = msg.Read<int>(ref s);
				//var actor = dst as ISecurity;
				//actor.PlaceOrder(orderId, price, volume);
#if FIXED				
			fixed (FixedArgs* wa= &msg.FixedArgs)
				{
					actor.PlaceOrder((int)wa->WordArgs[0], (int)wa->WordArgs[1], (int)wa->WordArgs[2]);
				}
#else
			_target.PlaceOrder((int)msg.WP1, (int)msg.WP1, (int)msg.WP1);

			//_target.PlaceOrder(1, 1, 1);
#endif
		}
#endif

#if CHUNKED
		public Action<MessageQueue,int> readCheck;
#else
		public RefAction<Message> readCheck;
#endif

		private void VreadCheck(MessageQueue q, int count)
		{
			for (int i = 0; i < count; i++)
			{
				_target.Check(q.Args.Read<long>());
//				rCheck(ref q.Buffer[q.SeqToIdx(q.Tail)]);
				q.Messages.FreeTail();
			}
		}

		private void rCheck(ref Message msg)	
		{
				int s = 0;
#if ARGSBUF
			throw new InvalidOperationException();
#else
#if FIXED
				var sum = msg.FixedArgs.Read<long>(ref s);
#else
			var sum = (long)msg.WP1;
#endif
				_target.Check(sum);
#endif
		}
		//[MethodImpl(MethodImplOptions.NoInlining)]
		private void wPlaceOrder(ref Message pmsg, int orderId, int price, int volume)
		{
#if ARGSBUF
			throw new InvalidOperationException();
#else
#if FIXED
			fixed (FixedArgs* wa = &pmsg.FixedArgs)
			{
				wa->WordArgs[0] = (ulong) orderId;
				wa->WordArgs[1] = (ulong)price;
				wa->WordArgs[2] = (ulong)volume;
			}
#elif false
			int p = pmsg.FixedArgs.Write(0, ref orderId);
			pmsg.FixedArgs.Write(p, ref price);
			pmsg.FixedArgs.Write(p, ref volume);
#else
			pmsg.WP1 = orderId;
			//pmsg.WP2= price;
			//pmsg.WP3= volume;
#endif
#endif
			//pmsg.Source = Actor.Current;
			pmsg.Executor = readPlaceOrder;
		}

		Message msg = default(Message);
		public void fqPlaceOrder(int orderId, int price, int volume)
		{
	

			//_queue.TargetMailbox.HaveTasks();
		}

		public RefAction<Message> rd;

		public void soPlaceOrder(int orderId, int price, int volume)
		{
			
		}

	
		public void siPlaceOrder(int orderId, int price, int volume)
		{
#if !ARGSBUF
			msg.WP1 = orderId;

			//msg.WP2 = price;
			//msg.WP3 = volume;
			//msg.Source = Actor.Current;
			msg.Executor = readPlaceOrder;

			_target.PlaceOrder((int)msg.WP1, (int)msg.WP1, (int)msg.WP1);
#else
			throw new InvalidOperationException();
#endif
		}

		private void zPlaceOrder(int orderId,int price, int volume)
		{
			//var curr = Actor.Current;
			_target.PlaceOrder(orderId, price, volume);
		}

		public void PlaceOrder(int orderId, int price, int volume)
		{
#if DIFF
			if (_mode==Mode.FullQueue)
#endif
			{
				var h = _queue.Messages.Head;
				var ready = _queue.Messages.BatchHead - h;
				if (ready == 0)
					_queue.Messages.RealWaitForFreeSlots();

				var m = _queue.Messages.Mask;
				var idx = h & m;
				var b = _queue.Messages.Buffer;

				//wPlaceOrder(ref b[idx], orderId, price, volume);
#if !ARGSBUF				
				b[idx].WP1 = orderId;
#else
				_queue.Args.Write<int>(orderId);
				//_queue.Args.Write<int>(price);
				//_queue.Args.Write<int>(volume);
#endif
				b[idx].Executor = readPlaceOrder;
				h++;
				_queue.Messages.Head = h;
				//_queue.NextHead();

				if (_queue.TargetMailbox.Comandeer == null)
					_queue.TargetMailbox.HaveTasks();

			}
#if DIFF && !ARGSBUF
			else if(_mode==Mode.SerializeOnly)
			{
				wPlaceOrder(ref msg, orderId, price, volume);
				rd(ref msg);
				/*if (_queue.TargetMailbox.Comandeer == null)
					_queue.TargetMailbox.HaveTasks();*/
			}else if(_mode==Mode.SerializeInlined)
			{
				siPlaceOrder(orderId, price, volume);
			}else if(_mode==Mode.Zero)
			{
				zPlaceOrder(orderId, price, volume);
			}
#endif
		}

		private void wCheck(ref Message pmsg, long sum)
		{
#if ARGSBUF
			throw new InvalidOperationException();
#else
#if FIXED
			fixed (FixedArgs* wa = &pmsg.FixedArgs)
			{
				wa->WordArgs[0] = (ulong)sum;
			}
#elif false

			pmsg.FixedArgs.Write(0, ref sum);
#else
			pmsg.WP1= sum;
#endif		
#endif

			//pmsg.Source = Actor.Current;
			pmsg.Executor = readCheck;
		}

		public void Check(long sum)
		{
			//if (_mode==Mode.FullQueue)
			{
				int seq;
				_queue.Messages.WaitForFreeSlots(1,-1);

#if !ARGSBUF				
				wCheck(ref _queue.Buffer[_queue.SeqToIdx(_queue.Head)], sum);
#else
				var idx = _queue.Messages.SeqToIdx(_queue.Messages.Head);
				_queue.Args.Write<long>(ref sum);
				_queue.Messages.Buffer[idx].Executor = readCheck;
#endif
				_queue.Messages.NextHead();
				
				_queue.HaveTasks();

				//_target.Mailbox.HaveTasks();
			}/*else
			{
				Message msg = default(Message);
				wCheck(ref msg, sum);
				rCheck(ref msg);
			}*/
		}

		public void HaveTasks()
		{
			_queue.TargetMailbox.HaveTasks();
		}
		
		/*public void PlaceOrder1(int orderId, int price, int volume)
		{
			if (!_queue.BeginWrite())
				throw new InvalidOperationException();
			var seq = MailboxScheduler.Current.NextSequence();
			_queue.Write(seq);
			_queue.WriteRef(_target);	// dst
			_queue.WriteRef(Actor.Current);	// src 
			//_queue.Write(0xBB);
			_queue.WriteRef(readPlaceOrder);
			//MicroLog.Info("PUT {0} {1,10:X}", seq, _queue.GetHead<IntPtr>(-1));
			//_queue.Write(0xCC);
			_queue.Write(orderId);
			_queue.Write(price);
			_queue.Write(volume);
			_queue.EndWrite();
			_target.Mailbox.HaveTasks();
		}

		public void Check(long sum)
		{
			if (!_queue.BeginWrite())
				throw new InvalidOperationException();
			_queue.Write(MailboxScheduler.Current.NextSequence());
			_queue.WriteRef(_target);	// dst
			_queue.WriteRef(Actor.Current);	// src 
			//_queue.Write(0xBB);
			_queue.WriteRef(readCheck);
			//_queue.Write(0xCC);
			_queue.Write(sum);
			_queue.EndWrite();
			_target.Mailbox.HaveTasks();
		}*/

		public Mailbox Mailbox
		{
			get { throw new NotImplementedException(); }
		}

	
	}

	public class Caller:Actor
	{
		private ISecurity _target;
		private int _job;

		public Caller(Mailbox mb, ISecurity target, int job):base(mb)
		{
			_target = target;
			_job = job;
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void DoCall(int i)
		{
			_target.PlaceOrder(i, i, i);
			for (int k = 0; k < _job * ActorTest.SPIN; k++) ;

		}

		public void Test(int count)
		{
			Actor.Enter(this);
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
			producer.Start(MailboxScheduler.Current.TaskScheduler);
			
			producer.Wait();
			MicroLog.Info("Producer done "+done);
			MailboxScheduler.Current.WaitAll();
			MicroLog.Info("Consumer done ");
			_target.Check(((long)count - 1) * count / 2);

			Actor.Exit(this);
		}
	}

	public class SecurityImpl : Actor, ISecurity
	{
		public static int _price;
		private long _sum;
		private int _job = ActorTest.JOB/2;

		public SecurityImpl(Mailbox mailbox, int job)
			: base(mailbox)
		{
			_job = job;
		}

		public void PlaceOrder(int orderId, int price, int volume)
		{
			_price = price;
			_sum += _price;
			for (int k = 0; k < _job * ActorTest.SPIN; k++) ;
		}
		
		public void Check(long sum)
		{
			MicroLog.Info("We have {0} and need {1}",_sum,sum);
			Console.Out.Flush();
			if (_sum != sum)
				new InvalidOperationException();
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
			ArgsBuffer bu = new ArgsBuffer();
			var a=new SomeObj();
			bu.WriteRef(a);
		}
		public static void Run()
		{
			//a1();
			//a2();
			//return;

			MicroLog.Writers.Add(new StreamWriter("ActorTest.mlog"));
			//MicroLog.Writers.Clear();
			MicroLog.WhenFull = MicroLog.WhenFullAction.NoCaching;

			for (int k = 0; k < 4; k++)
			{
				var implBox = new Mailbox(MailboxScheduler.Current);
				var impl = new SecurityImpl(implBox,JOB/2);

				var callerBox = new Mailbox(MailboxScheduler.Current);
				var callerImpl = new Caller(callerBox, impl, JOB / 2);

				Run("P0C0", callerImpl);

				implBox = new Mailbox(MailboxScheduler.Current);
				impl = new SecurityImpl(implBox, JOB / 2);
				var proxy = new SecurityProxy(impl, SecurityProxy.Mode.SerializeInlined);
				var callerProxy = new Caller(callerBox, proxy,JOB/2);

				/*Run("P0C0.il", callerProxy);

				impl = new SecurityImpl(implBox);
				proxy = new SecurityProxy(impl, SecurityProxy.Mode.Zero);
				callerProxy = new Caller(callerBox, proxy);

				Run("P0C0.z", callerProxy);

				*/
				implBox = new Mailbox(MailboxScheduler.Current);
				impl = new SecurityImpl(implBox, JOB / 2);
				proxy = new SecurityProxy(impl, SecurityProxy.Mode.SerializeOnly);
				callerProxy = new Caller(callerBox, proxy, JOB / 2);

				Run("P0C0.ser", callerProxy);

				implBox = new Mailbox(MailboxScheduler.Current);
				impl = new SecurityImpl(implBox, JOB / 2);
				proxy = new SecurityProxy(impl, SecurityProxy.Mode.FullQueue);
				callerProxy = new Caller(callerBox, proxy, JOB / 2);

				Run("P1C1", callerProxy);
				MicroLog.Info("HaveWorks:{0}", impl.Mailbox.HaveWorks);
				var q = proxy.Queue;
				MicroLog.Info("Backtracks {0} {1:F4}% EnqFulls {2} {3:F4}", q.Messages.Backtrackings,
								  (double)100 * q.Messages.Backtrackings / q.Messages.Tail, q.Messages.EnqueueFulls, (double)100 * q.Messages.EnqueueFulls / q.Messages.Tail);

				Console.ReadKey();
			}
		}
	}
}
