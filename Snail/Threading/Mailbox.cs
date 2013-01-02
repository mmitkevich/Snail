//#define CHUNKED
//#define CHUNK_DYN
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Scheduler;
using Snail.Util;

namespace Snail.Threading
{
	public interface IActor
	{
		Mailbox Mailbox { get; }
	}

	public class Actor:IActor
	{
		private Mailbox _mailbox;
		private static ThreadLocal<Stack<IActor>> _stack = new ThreadLocal<Stack<IActor>>(()=>new Stack<IActor>());
		
		[ThreadStatic] private static IActor _current;

		public Actor(Mailbox mailbox)
		{
			_mailbox = mailbox;
		}
		public Mailbox Mailbox
		{
			get { return _mailbox; }
		}

		public static IActor Current
		{
			get
			{
				return _current;
				//return _current.Value.Peek();
			}
			set { _current = value; }
		}

		public static void Enter(IActor act)
		{
			_stack.Value.Push(act);
			_current = act;
		}

		public static void Exit(IActor act)
		{
			var stack = _stack.Value;
			stack.Pop();
			_current = stack.Count>0?stack.Peek():null;
		}
	}


	public class MailboxScheduler
	{
		private TaskScheduler _taskScheduler;
		private ConcurrentDictionary<Mailbox,Task> _running;
		private Volatile.PaddedInteger _runningCount = new Volatile.PaddedInteger(0);
		private Volatile.PaddedLong _sequence = new Volatile.PaddedLong(0);

		public static MailboxScheduler Current { get; set; }

		public TaskScheduler TaskScheduler
		{
			get { return _taskScheduler; }
		}

		private bool _shouldContinue = true;

		static MailboxScheduler()
		{
			var ts = TaskScheduler.Default;
			//var ts = new RoundRobinThreadAffinedTaskScheduler(2);
			Current = new MailboxScheduler(ts);
		}

		public MailboxScheduler(TaskScheduler tsched)
		{
			_taskScheduler = tsched;
			_running = new ConcurrentDictionary<Mailbox,Task>();
		}

		public long NextSequence()
		{
			return _sequence.AtomicIncrementAndGet();
		}

		public int Start(Mailbox mailbox)
		{
			var task = new Task(mailbox.Run);
			mailbox.Task = task;
			//_running[mailbox] = task;
			int cnt = _runningCount.AtomicIncrementAndGet();
			MicroLog.Info("StartTask {0} total {1} for mailbox {2}",task.Id,cnt,mailbox.GetHashCode());
			task.Start(_taskScheduler);
			return cnt;
		}

		public bool ShouldContinue(Mailbox mailbox)
		{
			return _shouldContinue;
		}

		public int Exit(Mailbox mailbox)
		{
			//Task task;
			//_running.TryRemove(mailbox, out task);
			mailbox.Task = null;
			return _runningCount.AtomicDecrementAndGet();
		}

		public void WaitAll()
		{
			_shouldContinue = false;
			while (true)
			{
				int r = _runningCount.ReadFullFence();
				if (r == 0)
					break;
				if (r == 1)
					if (Task.CurrentId != null && Actor.Current != null)
						if (Task.CurrentId == Actor.Current.Mailbox.Task.Id)
							break;
				Thread.Sleep(0);
			}
			_shouldContinue = true;
		}
	}

	public interface IMessageExecutor
	{
		void Execute(ref Message msg);
	}

	public abstract class ActorProxy<TImpl> where TImpl:IActor
	{
		protected MessageQueue _queue;
		protected TImpl _target;

		public MessageQueue Queue
		{
			get { return _queue; }
		}

		public ActorProxy(TImpl target)
		{
			_target = target;
			_queue = target.Mailbox.GetChannelFrom(Thread.CurrentThread);
		}
	}

	public sealed class Mailbox
	{
		private ConcurrentDictionary<Thread, MessageQueue> _queues = new ConcurrentDictionary<Thread, MessageQueue>();
		private MessageQueue[] _qcache;
		private Volatile.Reference<Mailbox> _comandeer;
		private int _haveWorks = 0;
		private MailboxScheduler _scheduler;
		

		public Task Task { get; set; }

		public Mailbox Comandeer
		{
			get { return _comandeer.ReadUnfenced(); }
		}

		public Mailbox(MailboxScheduler sched)
		{
			_scheduler = sched;
			_qcache = new MessageQueue[0];
		}

		public long NextSequence()
		{
			return _scheduler.NextSequence();
		}

		public MessageQueue GetChannelFrom(Thread source)
		{
			var q = _queues.GetOrAdd(source, mb => new MessageQueue(this));
			_qcache = _queues.Values.ToArray();
			return q;
		}

		private SpinWait _wt = default(SpinWait);
		private int _nextQueueIdx = 0;

		public void Run()
		{
			MicroLog.Info("Task started " + Task.CurrentId + " ThreadAffinity " + RoundRobinThreadAffinedTaskScheduler.CurrentThreadProcessorIndex);
			int CHUNK = 1000000;
			long timeWait = TimeSpan.FromMilliseconds(100).Ticks;
			while (true)
			{
				int  ntasks = 0;

				var q = WaitNextQueue(timeWait);
				if(q!=null)
				{
					try
					{
						int count = q.Available;
						for (int i = 0; i < count; i++)
						{
							int idx = q.ToIndex(q.Tail);
							var executor = q.Buffer[idx].Executor;
#if CHUNKED
							int chunk = 1;
#if CHUNK_DYN
							while (chunk < count)
							{
								if (q.Buffer[idx].Executor != executor)
									break;
								chunk++;
							}
#else
							chunk = count;
#endif
							executor(q, chunk);
#else
							executor(ref q.Buffer[idx]);
							q.FreeTail();
						}
#endif
					}
					catch (Exception ex)
					{
						Console.WriteLine(ex);
					}
				}else
				{
					if (_scheduler.ShouldContinue(this))
						continue;

					MicroLog.Info("Task scheduled to exit {0}", Task.CurrentId);
		
					// no tasks start to terminate
					_comandeer.WriteFullFence(null);
					
					// still no tasks - quitting
					if (WaitNextQueue(0)==null)
						break;

					// try regain control or abort
					if (_comandeer.ReadFullFence() != null 
						|| !_comandeer.AtomicCompareExchange(this,null))
						break;
				}
			}
			_scheduler.Exit(this);
			MicroLog.Info("Task done" + Task.CurrentId + " ThreadAffinity " + RoundRobinThreadAffinedTaskScheduler.CurrentThreadProcessorIndex);
		}

		public MessageQueue WaitNextQueue(long timeWait)
		{
			int cnt = 0;
			if(_qcache[_nextQueueIdx].Available>0)
				return _qcache[_nextQueueIdx];

			long start = DateTime.UtcNow.Ticks;

			while (_qcache[_nextQueueIdx].WaitForData(0) == 0)
			{
				_nextQueueIdx++;
				if (_nextQueueIdx >= _qcache.Length)
					_nextQueueIdx = 0;
				cnt++;
				if (cnt >= _qcache.Length)
				{
					if (timeWait == 0)
						return null;
					_wt.SpinOnce();
					if (timeWait > 0 && DateTime.UtcNow.Ticks - start > timeWait)
						return null;
					cnt = 0;
				}
			}
			return _qcache[_nextQueueIdx];
		}

		public int HaveWorks
		{
			get { return _haveWorks; }
		}

		public void HaveTasks()
		{
			if (_comandeer.ReadUnfenced() == null)
			{
				_haveWorks++;
				if (_comandeer.AtomicCompareExchange(this, null))
				{
					_scheduler.Start(this);
				}
			}
		}
	}
}
