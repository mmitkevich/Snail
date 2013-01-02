using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Snail.Threading
{
	public interface IConcurrentQueue<T>
	{
		bool TryEnqueue(T value);
		bool TryDequeue(out T value);
		bool Enqueue(T value);
		T Dequeue();
	}
}
