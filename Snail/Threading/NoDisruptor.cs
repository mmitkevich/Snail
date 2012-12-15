using Disruptor;
using Disruptor.Dsl;

namespace SlothDB
{
	public class NoDisruptor<T>:IDisruptor<T>
	{
		private T _event;
		private IEventHandler<T> _singleHandler;
		private long _seq;

		public NoDisruptor(T ev, IEventHandler<T> handler)
		{
			_event = ev;
			_singleHandler = handler;
		}

		public void Start()
		{
			
		}

		public void Shutdown()
		{
			
		}

		public IEventHandler<T>[] EventHandlers
		{
			get { return new[] {_singleHandler}; }
		}

		public void PublishEvent(IEventTranslator<T> eventTranslator, object resource)
		{
			PublishEvent(eventTranslator);
		}

		public void PublishEvent(IEventTranslator<T> eventTranslator)
		{
			long seq = _seq++;
			;
			_singleHandler.OnNext(eventTranslator.TranslateTo(_event, seq), seq, true);			
		}
	}
}