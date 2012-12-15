using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Disruptor;

namespace Snail.Threading
{

	public interface IMessage
	{
		IActor Source { get; }
		IActor Target { get; }

		/// <summary>
		/// Process message in target actor's thread
		/// </summary>
		void Process();
		
		/// <summary>
		/// Post message to target actor's thread asynchronously.
		/// </summary>
		/// <returns></returns>
		void Post();
		
		/// <summary>
		/// Post message to target actor's thread, or execute in calling thread if possible.
		/// </summary>
		/// <returns></returns>
		bool Send();

		IMessage CopyTo(IMessage dest, long seq, long start);

		Func<IMessage, long, long, IMessage> Serializer { get; }
	}

	public sealed class Message<TData>:IMessage 
	{
		internal IActor _source;

		internal IActor _target;
		internal Action<Message<TData>> _handler;
		internal TData _arg;
		internal Serializers _serializer; 

		internal class Serializers
		{
			public Func<IMessage, long, long, IMessage> Single;
			public Func<IMessage, long, long, IMessage> Multi;

			public IMessage Owner;
			public IMessage[] Batch;

			public Serializers(IMessage owner)
			{
				Owner = owner;
				Single = Owner.CopyTo;
				Multi = CopyNext;
			}
			public IMessage CopyNext(IMessage dest, long seq, long start)
			{
				return Batch[seq - start].CopyTo(dest,seq,start);
			}
		}

		public Message(IActor source, IActor target, Action<Message<TData>> handler)
		{
			_target = target;
			_source = source;
			_handler = handler;
		}

		public Message()
		{
			
		}

		public void Process()
		{
			_handler(this);
		}

		public void Post(TData arg)
		{
			_arg = arg;

			Post();
		}

		public void Post()
		{
			if (_serializer == null)
				_serializer = new Serializers(this);
			_target.Mailbox.Post(_serializer.Single);
		}

		public void Post(IMessage[] batch)
		{
			if (_serializer == null)
				_serializer = new Serializers(this);
			_target.Mailbox.Post(_serializer.Multi);
		}

		public bool Send(TData arg)
		{
			_arg = arg;
			return Send();
		}

		public bool Send()
		{
			return _target.Mailbox.Accept(this);
		}

		public void CopyFrom(Message<TData> src)
		{
			_source = src._source;
			_target = src._target;
			_handler = src._handler;
			_arg = src._arg;
		}

		public Func<IMessage, long, long, IMessage> Serializer
		{
			get { return _serializer.Single; }
		}

		public IMessage CopyTo(IMessage dest, long seq, long start)
		{
			Message<TData> mdest = dest as Message<TData>;

			if (mdest == null)
				mdest = new Message<TData>();

			mdest.CopyFrom(this);
			return mdest;
		}

		public IActor Source
		{
			get { return _source; }
		}

		public IActor Target
		{
			get { return _target; }
		}

		public TData Arg
		{
			get { return _arg; }
		}
	}

	public interface IActor
	{
		Mailbox Mailbox { get; }
	}

	public class Actor:IActor
	{
		private Mailbox _mailbox;

		public Actor(Mailbox mbox)
		{
			_mailbox = mbox;
		}

		public Mailbox Mailbox
		{
			get { return _mailbox; }
		}
	}

	public class Mailbox
	{
		private bool _autonomous = false;
		private Mailbox _controller;
		private bool _hasData;
		private TaskScheduler _taskScheduler = TaskScheduler.Default;
		internal INonBlockingQueue<IMessage> _queue;
		private const int MaxBatch = int.MaxValue;

		public Mailbox(bool autonomous, INonBlockingQueue<IMessage> queue)
		{
			_autonomous = autonomous;
			_queue = queue;
		}

		public bool Accept(IMessage message)
		{
			var source = message.Source;
			if (source != null)
			{
				var sourceMailbox = source.Mailbox;
				if (sourceMailbox == this)
				{
					message.Process();
					return true;
				}
				if (AcquireControl(sourceMailbox.Controller))
				{
					ProcessWork(); 
					message.Process();
					RelinquishControl();
					return true;
				}
			}
			Post(message.Serializer);
			return false;
		}

		public void Post(Func<IMessage, long, long, IMessage> serializer)
		{
			_queue.Enqueue(serializer);

			//_hasData = true;

			if (_controller == null 
				&& Interlocked.CompareExchange(ref _controller, this, null)==null)
				HaveWork();
		}

		public Mailbox Controller
		{
			get { return _controller; }
		}

		public bool AcquireControl(Mailbox controller)
		{
			if (_autonomous)
				return false;

			if (_controller != null)
				return false;

			if(Interlocked.CompareExchange(ref _controller,controller,null)==null)
			{
				//_hasData = false;
				return true;
			}
			return false;
		}

		public void RelinquishControl()
		{
			Thread.MemoryBarrier();
			Mailbox c = _controller;
			
			if (c == this)
				return;

			if(_queue.WorkCount()>0)
			{
				_controller = this;
				HaveWork();
			}else
			{
				_controller = null;
			}
		}

		public  static int HWT = 0;
		private void HaveWork()
		{
			HWT++;
			new Task(ProcessWorkTask).Start(_taskScheduler);
		}

		public static int PWT = 0;
		private void ProcessWorkTask()
		{
			PWT++;
			//MicroLog.Info("PW Enter {0}",PWT);

			if (_controller != this)
				throw new InvalidOperationException();

			//if (_controller.ReadUnfenced() != null ||
			//	!_controller.AtomicCompareExchange(this, null))
			//	return;

			while (true)
			{
				//_hasData = false;
				if (ProcessWork() == 0)
				{
					_controller = null;
					Thread.MemoryBarrier();
					if (_queue.WorkCount()==0)
						break;
					if (!(Interlocked.CompareExchange(ref _controller,this, null)==null))
						break;
				}
			}
			//MicroLog.Info("PW Exits {0}",PWT);
		}

		public int ProcessWork()
		{
			_queue.Dequeue(MaxBatch,(message, sequence, endOfBatch) => message.Process());
			return _queue.WorkCount();
		}

		public void WaitWork()
		{
			while(_queue.WorkCount()>0)
				Thread.Sleep(0);
		}
	}

	public interface INonBlockingQueue<T>
	{
		void Dequeue(int maxBatch, Action<T,long,bool> receiver);
		void Enqueue(int batchSize, Func<T,long,long, T> transformer);
		void Enqueue(Func<T, long,long, T> transformer);
		int WorkCount();
	}


	public class MPSCBoundedQueue<T>:INonBlockingQueue<T> where T:class
	{
		private RingBuffer<T> _ringBuffer;
		private Func<T> _factory;
		private IClaimStrategy _claimSt;
		private IWaitStrategy _waitSt;
		private BatchDescriptor _batchDescriptor;
		private Sequence _sequence = new Sequence(Sequencer.InitialCursorValue);
		private ISequenceBarrier _sequenceBarrier;
		
		public MPSCBoundedQueue(int size, Func<T> factory)
		{
			_factory = factory;
			//_claimSt = new MultiThreadedLowContentionClaimStrategy(size);
			_claimSt = new SingleThreadedClaimStrategy(size);
			_waitSt = new BusySpinWaitStrategy();
			_ringBuffer = new RingBuffer<T>(_factory, _claimSt, _waitSt);
			_batchDescriptor = _ringBuffer.NewBatchDescriptor(size);
			_sequenceBarrier = _ringBuffer.NewBarrier(new Sequence[0]);
			_ringBuffer.SetGatingSequences(_sequence);
		}
		

		public void Dequeue(int maxBatch, Action<T, long, bool> receiver)
		{
			_sequenceBarrier.ClearAlert();

			T evt;
			long nextSequence = _sequence.Value + 1L;
			long maxSequence = nextSequence + maxBatch - 1L;

			SpinWait spinWait = new SpinWait();

			int spinCount = 0;
			int spinMax = 10;
			
			while(nextSequence<=maxSequence)
			{
				long availableSequence = _sequenceBarrier.Cursor;

				if (nextSequence > availableSequence)
				{
					spinWait.SpinOnce();
					if (spinCount++ > spinMax)
						break;
					continue;
				}

				spinCount = 0;

				if (availableSequence > maxSequence)
					availableSequence = maxSequence;		
				try
				{
	

					while (nextSequence <= availableSequence)
					{
						evt = _ringBuffer[nextSequence];
						receiver(evt, nextSequence, nextSequence == availableSequence);
						nextSequence++;
					}
					_sequence.LazySet(nextSequence - 1L);
				}
				catch (Exception ex)
				{
					//_exceptionHandler.HandleEventException(ex, nextSequence, evt);
					_sequence.LazySet(nextSequence);
					nextSequence++;
				}
			}
		}

		public void Enqueue(int batchSize, Func<T, long, long, T> transformer)
		{
			_batchDescriptor.Size = Math.Min(batchSize,_ringBuffer.BufferSize);
			_batchDescriptor = _ringBuffer.Next(_batchDescriptor);
			for (long seq = _batchDescriptor.Start; seq <= _batchDescriptor.End; seq++)
			{
				var oldValue = _ringBuffer[seq];
				var newValue = transformer(oldValue,seq,_batchDescriptor.Start);

				_ringBuffer[seq] = newValue;
					//if (newValue != oldValue)
					//	_ringBuffer[seq] = newValue;
			}
			_ringBuffer.Publish(_batchDescriptor);
		}

		public void Enqueue(Func<T, long, long, T> transformer)
		{
			var seq = _ringBuffer.Next();
			var oldValue = _ringBuffer[seq];
			var newValue = transformer(oldValue, seq, seq);
			_ringBuffer[seq] = newValue;
			_ringBuffer.Publish(seq);
		}

		public int WorkCount()
		{
			long cursor = _ringBuffer.Cursor;
			return (int)(cursor-_sequence.Value);
		}

	}
}
