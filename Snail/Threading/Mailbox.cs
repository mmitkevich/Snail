#define CHUNKED
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
using Snail.Collections;
using Snail.Util;

namespace Snail.Threading
{
	public interface IActor
	{
		Mailbox Mailbox { get; }
	}

	

	public class Scheduler
	{
		private TaskScheduler _taskScheduler;
		private ConcurrentDictionary<Mailbox,Task> _running;
		private Volatile.PaddedInteger _runningCount = new Volatile.PaddedInteger(0);
		private Volatile.PaddedLong _sequence = new Volatile.PaddedLong(0);

		public static Scheduler Current { get; set; }

		public TaskScheduler TaskScheduler
		{
			get { return _taskScheduler; }
		}

		private bool _shouldContinue = true;

		static Scheduler()
		{
			var ts = TaskScheduler.Default;
			//var ts = new RoundRobinThreadAffinedTaskScheduler(2);
			Current = new Scheduler(ts);
		}

		public Scheduler(TaskScheduler tsched)
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

		public int Exited(Mailbox mailbox)
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
					if (Task.CurrentId != null && Host.Current.Caller != null)
						if (Task.CurrentId == Host.Current.Caller.Mailbox.Task.Id)
							break;
				Thread.Sleep(0);
			}
			_shouldContinue = true;
		}
	}

	public delegate void MessageQueueReader(MessageConsumer queue, int count);

	public interface IHashValue<TValue>
	{
		long GetHashValue(ref TValue value);
	}

	public struct HashValue<TValue>:IHashValue<TValue>
	{
		public long GetHashValue(ref TValue value)
		{
			return value.GetHashCode();
		}
	}

	public class HashBuckets<TValue, TEmptyHandler, THashValue> where TEmptyHandler:INullValue<TValue> where THashValue:IHashValue<TValue>
	{
		private int _size = 1024;
		private TValue[] _entries;
		private TEmptyHandler _emptyHandler = default(TEmptyHandler);
		private THashValue _hashValue = default(THashValue);

		public HashBuckets()
		{
			_entries = new TValue[_size];
		}

		public bool Add(long hash, TValue value)
		{
			while (true)
			{
				int idx = (int)(hash & (_size - 1));

				if (_emptyHandler.IsNull(ref _entries[idx]))
				{
					_entries[idx] = value;
					return true;
				}
				
				if(_hashValue.GetHashValue(ref _entries[idx])==hash)
					return false;
				
				lock (_entries)
				{
					Array.Resize(ref _entries, 2*_size);
					_size = 2*_size;
				}
			}
		}

		public void Remove(long hash, TValue value)
		{
			int idx = (int)(hash & (_size - 1));
			if (!_emptyHandler.IsNull(ref _entries[idx]) && !_entries[idx].Equals(value))
				throw new InvalidOperationException(string.Format("Hash {0} does not have entry {1}",hash,value));
			_emptyHandler.SetNull(ref _entries[idx]);
		}

		public bool TryGetValue(long hash, out TValue value)
		{
			value = _entries[(int) (hash & (_size - 1))];
			return !_emptyHandler.IsNull(ref value);
		}

	}

	public sealed class AddressBook
	{
		
		private HashBuckets<IntPtr, NullValue<IntPtr>, HashValue<IntPtr>> _methods = new HashBuckets<IntPtr, NullValue<IntPtr>, HashValue<IntPtr>>();
		private HashBuckets<object, RefsNullValue, HashValue<object>> _objects = new HashBuckets<object, RefsNullValue, HashValue<object>>();

		public AddressBook()
		{
			
		}

		public Address RegisterMethod(Delegate d)
		{
			IntPtr ptr = Address.GetFunctionPointer(d);
			Address addr = Address.FromPtr(ptr);
			_methods.Add(addr.Value, ptr);
			return addr;
		}

		public IntPtr ResolveMethod(Address addr)
		{
			IntPtr ptr = default(IntPtr);
			_methods.TryGetValue(addr.Value, out ptr);
			return ptr;
		}

		public object ResolveObject(Address addr)
		{
			object obj = null;
			_objects.TryGetValue(addr.Value, out obj);
			return obj;
		}

	}

	public class Host
	{
		public Network Network;
		public Scheduler Scheduler;

		private ConcurrentDictionary<Host, MessageConsumer> _inputs = new ConcurrentDictionary<Host, MessageConsumer>();

		[ThreadStatic] public static Host Current;

		public IActor Caller;

		public Host(Network n, Scheduler sched)
		{
			Network = n;
			Scheduler = sched;
			if(Current!=null)
				throw new InvalidOperationException("Thread already assigned a host");
			Current = this;
		}

		public MessageConsumer GetConsumer(Host source)
		{
			var consumer = _inputs.GetOrAdd(source, 
				host =>
				{
					var queue = Network.GetChannel(this, source);
					return queue.AddConsumer();
				});
			return consumer;
		}

		public MessageQueue GetProducer(Host target)
		{
			return Network.GetChannel(target, this);
		}

		public void EnterActor(IActor actor)
		{
			Caller = actor;
		}

		public void ExitActor(IActor actor)
		{
			Caller = null;
		}
	}

	public sealed class Network
	{
		private ConcurrentDictionary<Tuple<Host, Host>, MessageQueue> _channels =
			new ConcurrentDictionary<Tuple<Host, Host>, MessageQueue>();

		public MessageQueue GetChannel(Host target, Host source)
		{
			return _channels.GetOrAdd(new Tuple<Host, Host>(target, source),
			                          destSrc => CreateChannel(destSrc.Item1, destSrc.Item2));
		}

		public MessageQueue CreateChannel(Host target, Host source)
		{
			return new MessageQueue();
		}
	}

	public sealed class Mailbox
	{
		private StructArray<MessageConsumer> _consumers;

		private Volatile.Reference<Mailbox> _comandeer;

		private int _haveWorks = 0;
		private int _nextQueueIdx = 0;

		public Host Host;

		public Task Task { get; set; }

		public Mailbox Comandeer
		{
			get { return _comandeer.ReadUnfenced(); }
		}

		public Mailbox(Host host)
		{
			Host = host;
			_consumers = new StructArray<MessageConsumer>(32);
		}

		public void Run()
		{
			MicroLog.Info("Task started " + Task.CurrentId + " ThreadAffinity " + RoundRobinThreadAffinedTaskScheduler.CurrentThreadProcessorIndex);
			
			long timeWait = TimeSpan.FromMilliseconds(100).Ticks;
			int maxChunk = 100;

			Message msg = default(Message);
			Address fun = default(Address);
			MessageConsumer consumer = null;
			object target = null;
			while (true)
			{
				var avail = WaitCall(ref msg, ref consumer, timeWait);
				int done = 0;
				if (avail>0)
				{
					if (avail > maxChunk)
						avail = maxChunk;
					try
					{
						consumer.BeginPopCall(ref fun);
						target = consumer.PopRef();
						done = Address.ExecuteMethod(fun.ToPtr(), target, consumer, avail);
					}
					catch (Exception ex)
					{
						Console.WriteLine(ex);
					}
				}else
				{
					if (Host.Scheduler.ShouldContinue(this))
						continue;

					MicroLog.Info("Task scheduled to exit {0}", Task.CurrentId);
		
					// no tasks start to terminate
					_comandeer.WriteFullFence(null);
					
					// still no tasks - quitting
					if (WaitCall(ref msg, ref consumer, 0)==null)
						break;

					// try regain control or abort
					if (_comandeer.ReadFullFence() != null 
						|| !_comandeer.AtomicCompareExchange(this,null))
						break;
				}
			}
			Host.Scheduler.Exited(this);
			MicroLog.Info("Task done" + Task.CurrentId + " ThreadAffinity " + RoundRobinThreadAffinedTaskScheduler.CurrentThreadProcessorIndex);
		}

		public int WaitCall(ref Message msg, ref MessageConsumer next, long timeWait=-1)
		{
			next = _consumers[_nextQueueIdx];

			long start = DateTime.UtcNow.Ticks;
			
			int cnt = 0;
			int limit = _consumers.Count;

			int avail;
			while ((avail=next.WaitCalls(-1, 0)) == 0)
			{
				_nextQueueIdx++;

				if (_nextQueueIdx >= _consumers.Count)
					_nextQueueIdx = 0;
				next = _consumers[_nextQueueIdx];
				cnt++;
				if (cnt >= limit)
				{
					if (timeWait == 0)
						return 0;
					default(SpinWait).SpinOnce();
					if (timeWait > 0 && DateTime.UtcNow.Ticks - start > timeWait)
						return 0;
					cnt = 0;
				}
			}
			return avail;
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
					Host.Scheduler.Start(this);
				}
			}
		}
	}
}
