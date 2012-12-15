using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Disruptor;
using Snail.Util;

namespace Snail.Threading.Disruptor
{
	public class Resource
	{
		public int Counter;
		public int Owner;
		public int Index;
		public int Lock;
	}

	public interface IResourceStrategy
	{
		int GetWorker();
		int GetWorker(object resource);

		int Sequence { get; }
	}

	public class ResourceStrategy<T> : IResourceStrategy
	{
		internal int _numWorkers;
		internal int _workersMask;
		internal int _sequence;
		internal Func<T, Resource> _getResource;
		internal Func<int, int> _getWorkerLoad;

		public ResourceStrategy()
		{
		}

		public int Sequence
		{
			get { return _sequence; }
		}

		public int NumWorkers
		{
			get { return _numWorkers; }
			set
			{
				if (BitMagic.Ones32((uint)value) > 1)
					throw new InvalidOperationException("NumWorkes should be power of 2");
				_numWorkers = value;
				_workersMask = _numWorkers - 1;
			}
		}

		public virtual IEventHandler<T> WrapHandler(IEventHandler<T> src)
		{
			return src;
		}

		public virtual IEventHandler<T> UnwrapHandler(IEventHandler<T> dst)
		{
			return dst;
		}

		/// <summary>
		/// Called by Publisher.
		/// </summary>
		/// <returns></returns>
		public virtual int GetWorker(object objRes)
		{
			Resource resource = objRes as Resource;
			return resource.Index & _workersMask;
		}

		public virtual int GetWorker()
		{
			return (_sequence++) & _workersMask;
		}
	}


}
