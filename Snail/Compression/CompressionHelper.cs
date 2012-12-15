using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Snail.Util;

namespace Snail.Compression
{
	public static class CompressionHelper
	{
		public static double GetBitsPerSample(this ICompressedBlock block)
		{
			int bits = block.BitLength;
			while(block.Inner!=null)
			{
				block = block.Inner;
				bits += block.BitLength;
			}
			return bits / block.Count;
		}
		
		public static double GetCompressionRatio<T>(this ICompressedBlock<T> block)
		{
			return block.GetBitsPerSample() / Arithmetics<T>.SizeOf;
		}
	}
}
