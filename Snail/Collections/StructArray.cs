using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Snail.Collections
{
	public struct StructArray<T>
	{
		public T[] Buffer;
		public int Count;

		public const int MinCapacity = 32;

		public StructArray(int capacity)
		{
			Count = 0;
			Buffer = new T[capacity];
		}

		public void EnsureHasIndex(int index)
		{
			if (index >= Count)
			{
				Count = index + 1;
				if (Count >= Buffer.Length)
					Array.Resize(ref Buffer, Count << 1);
			}
		}

		public void Add(T value)
		{
			this[Count] = value;
		}

		public void RemoveAt(int index)
		{
			while (index + 1 < Count)
			{
				Buffer[index] = Buffer[index + 1];
				index++;
			}
		}

		public void Swap(int i, int j)
		{
			T temp = Buffer[i];
			Buffer[i] = Buffer[j];
			Buffer[j] = temp;
		}

		public void Resize(int count)
		{
			Count = count;
			int size = MinCapacity;
			if (count > size)
				size = count;
			size = size << 1;
			if (Buffer.Length >= size)
				Array.Resize(ref Buffer, size);
		}

		public T this[int index]
		{
			get { return Buffer[index]; }
			set
			{
				EnsureHasIndex(index);
				Buffer[index] = value;
			}
		}
	}
}
