using System;
using System.Collections.Generic;

namespace Snail.Util
{
	// http://aggregate.org/MAGIC
	public static class BitMagic
	{
		public static int Ones32(uint x)
		{
			/* 32-bit recursive reduction using SWAR...
			   but first step is mapping 2-bit values
			   into sum of 2 1-bit values in sneaky way
			*/
			x -= ((x >> 1) & 0x55555555);
			x = (((x >> 2) & 0x33333333) + (x & 0x33333333));
			x = (((x >> 4) + x) & 0x0f0f0f0f);
			x += (x >> 8);
			x += (x >> 16);
			return (int)(x & 0x0000003f);
		}

		public static int WordLengthNoLeadingZeros(uint x)
		{
			x |= (x >> 1);
			x |= (x >> 2);
			x |= (x >> 4);
			x |= (x >> 8);
			x |= (x >> 16);

			return (int)Ones32(x);
		}

		public static double Entropy<T>(T [] input)
		{
			Dictionary<T, int> c = new Dictionary<T, int>(256);
			for (int i = 0; i < input.Length; i++)
			{
				int n = 0;
				c.TryGetValue(input[i], out n);
				c[input[i]] = n + 1;
			}
			double e = 0;
			foreach (int n in c.Values)
			{
				double p = (double)n / input.Length;
				e += -p * Math.Log(p, 2);
			}
			return e;
		}
	}
}
