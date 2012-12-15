using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Snail.Util;

namespace Snail.Compression
{
	public class DiffBlock<T> : CompressedBlock<T,T> where T : struct
	{
		internal T _first;
		internal T _last;
		internal int _count;

		public DiffBlock(ICompressedBlock<T> inner) : base(inner)
		{
			
		}

		public override int Count
		{
			get { return _count; }
		}

		public override void Write(int dstIndex, T[] src, int srcIndex, int count)
		{
			if (dstIndex != Count)
				throw new InvalidOperationException("DiffCompressedBlock supports write to the end only");

			_count = dstIndex + count;

			if (dstIndex == 0)
				_first = _last = src[srcIndex];

			T[] diffs = new T[count - 1];
			for (int i = 0; i < count - 1; i++, srcIndex++)
			{
				diffs[i] = Arithmetics<T>.Subtract(src[srcIndex + 1], _last);
				_last = src[srcIndex + 1];
			}

			_inner.Write(dstIndex, diffs, 0, diffs.Length);
		}

		public override int Read(int srcIndex, T[] dst, int dstIndex, int count)
		{
			T[] diffs = new T[_inner.Count];

			_inner.Read(0, diffs, 0, diffs.Length);
			dst[dstIndex++] = _first;
			for (int i = 0; i < diffs.Length; i++, dstIndex++)
			{
				dst[dstIndex] = Arithmetics<T>.Add(dst[dstIndex - 1], diffs[i]);
			}
			return count;
		}

	}
}
