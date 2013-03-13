using System;
using System.Threading;

namespace Disruptor
{
	public class LeastLoadResourceStrategy<T> : ResourceStrategy<T>
	{
		internal SpinWait _spinWait = new SpinWait();


		public LeastLoadResourceStrategy(Func<T, Resource> getResource)
		{
			_getResource = getResource;
		}

		private class ResourceTrackingEventHandler<T> : IEventHandler<T>
		{
			internal IEventHandler<T> _inner;
			private Func<T, Resource> _getResource;

			public ResourceTrackingEventHandler(IEventHandler<T> inner, Func<T, Resource> getResource)
			{
				_inner = inner;
				_getResource = getResource;
			}

			public void OnNext(T data, long sequence, bool endOfBatch)
			{
				_inner.OnNext(data, sequence, endOfBatch);
				Interlocked.Decrement(ref _getResource(data).Counter);
			}
		}

		public override IEventHandler<T> WrapHandler(IEventHandler<T> src)
		{
			return new ResourceTrackingEventHandler<T>(src, _getResource);
		}

		public override IEventHandler<T> UnwrapHandler(IEventHandler<T> src)
		{
			var h = src as ResourceTrackingEventHandler<T>;
			if (h == null)
				throw new InvalidOperationException("Handler is not compatible");
			return h._inner;
		}
		/// <summary>
		/// Called by Publisher.
		/// </summary>
		/// <returns></returns>
		public override int GetWorker(object obj)
		{
			var resource = obj as Resource;
			if (NumWorkers > 1)
				if (Interlocked.CompareExchange(ref resource.Counter, -1, 0) == 0)
				{
					//MicroLog.Info("{0} ce {1} {2}",resource.Index,resource.Counter,Thread.CurrentThread.ManagedThreadId);
					long minInWork = long.MaxValue;
					//int[] workerIndx = new int[NumWorkers];
					int d = 0;
					for (int i = 1; i < NumWorkers; i++)
					{
						long inWork = _getWorkerLoad(i);
						if (inWork < minInWork)
						{
							//workerIndx[0] = i;
							minInWork = inWork;
							//d = 1;
							d = i;
						}
						//else if (inWork == minInWork)
						//{
						//	workerIndx[d++] = i;
						//}
					}
					_sequence++;
					resource.Owner = d;//workerIndx[Sequence % d];
					resource.Counter = 1;
					Thread.MemoryBarrier();
				}
				else
				{
					Thread.MemoryBarrier();
					while (resource.Counter == -1)
					{
						_spinWait.SpinOnce();
						Thread.MemoryBarrier();
					}
					Interlocked.Increment(ref resource.Counter);
				}
			return resource.Owner;
		}

		public override int GetWorker()
		{
			return _sequence++ & _workersMask;
		}
	}
}
