using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Disruptor;
using Disruptor.Dsl;

namespace Disruptor
{
	public class MultiEventHandlerGroup<T> where T : class
	{
		private MultiDisruptor<T> _multiDisruptor;
		private EventHandlerGroup<T> [] _groups;
 
		internal MultiEventHandlerGroup(MultiDisruptor<T> multiDisruptor)
		{
			_multiDisruptor = multiDisruptor;
			_groups = new EventHandlerGroup<T>[_multiDisruptor._disruptors.Length];
		}
		
		private IEventHandler<T>[] CreateHandlers(Func<IEventHandler<T>> []handlersFactory)
		{
			IEventHandler<T>[] handlers = new IEventHandler<T>[handlersFactory.Length];
				for(int i=0;i<handlersFactory.Length;i++)
					handlers[i] = _multiDisruptor._resourceStrategy.WrapHandler(handlersFactory[i]());
			return handlers;
		}

		internal MultiEventHandlerGroup<T> HandleEventsWith(params Func<IEventHandler<T>>[] handlersFactory)
		{
			for(int j=0;j<_multiDisruptor._disruptors.Length;j++)
			{
				_groups[j] = _multiDisruptor._disruptors[j].HandleEventsWith(CreateHandlers(handlersFactory));
			}
			return this;
		}

		public MultiEventHandlerGroup<T> Then(params Func<IEventHandler<T>> []handlersFactory)
		{
			for(int j=0;j<_multiDisruptor._disruptors.Length;j++)
			{
				_groups[j] = _groups[j].Then(CreateHandlers(handlersFactory));
			}
			return this;
		}
	}

	public class MultiDisruptor<T>:IDisruptor<T> where T:class
	{
		internal Disruptor<T>[] _disruptors;
		
		internal ResourceStrategy<T> _resourceStrategy;

		public IEventHandler<T>[] EventHandlers
		{
			get { return _disruptors.SelectMany(d => d.EventHandlers.Select(e=>_resourceStrategy.UnwrapHandler(e))).ToArray(); }
		}


		public MultiDisruptor(int numWorkers, 
			Func<T> eventFactory, 
			ResourceStrategy<T> resourceStrategy,
			Func<IClaimStrategy> claimStrategyFactory,
			Func<IWaitStrategy> waitStrategyFactory, TaskScheduler taskScheduler)
		{
			_resourceStrategy = resourceStrategy;
			_resourceStrategy._getWorkerLoad = this.GetWorkerLoad;
			_resourceStrategy.NumWorkers = numWorkers;
			

			_disruptors = new Disruptor<T>[numWorkers];

			for (int i = 0; i < numWorkers;i++ )
			{
				_disruptors[i] = new Disruptor<T>(eventFactory, claimStrategyFactory(), waitStrategyFactory(), taskScheduler);
			}
		}

		public MultiEventHandlerGroup<T> HandleEventsWith(params Func<IEventHandler<T>>[] handlerFactory)
		{
			return new MultiEventHandlerGroup<T>(this).HandleEventsWith(handlerFactory);
		}

		public void Start()
		{
			foreach (var d in _disruptors)
			{
				d.Start();
			}
		}

		public void Shutdown()
		{
			foreach(var d in _disruptors)
			{
				d.Shutdown();
			}
		}

		public int GetWorkerLoad(int i)
		{
			return (int)_disruptors[i].RingBuffer.GetInWorkCount();
		}

		public void PublishEvent(IEventTranslator<T> eventTranslator, object resource)
		{
			int owner = _resourceStrategy.GetWorker(resource);
			var ringBuffer = _disruptors[owner].RingBuffer;
			var sequence = ringBuffer.Next();
			//MicroLog.Info("Publish to {0} at {1}",owner,sequence);
			try
			{
				eventTranslator.TranslateTo(ringBuffer[sequence], sequence);
			}
			finally
			{
				ringBuffer.Publish(sequence);
			}
		}

		public void PublishEvent(IEventTranslator<T> eventTranslator)
		{
			var owner = _resourceStrategy.GetWorker();
			var ringBuffer = _disruptors[owner].RingBuffer;
			var sequence = ringBuffer.Next();
			//MicroLog.Info("Publish to {0} at {1}",owner,sequence);
			try
			{
				eventTranslator.TranslateTo(ringBuffer[sequence], sequence);
			}
			finally
			{
				ringBuffer.Publish(sequence);
			}
		}
	}
}
