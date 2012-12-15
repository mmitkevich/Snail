using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SlothDB;
using Snail.Compression;
using Snail.Tests;
using Snail.Util;

namespace Snail.Tests
{
	internal class CompressionReport
	{
		public string Sample;

		public double Entropy;
		public double BPS;
		public double TheorBPS;
		
		public CompressionReport(string sample)
		{
			Sample = sample;
		}

		public static CompressionReport Create<T>(ICompressedBlock block, Sample<T> sample)
		{
			CompressionReport r = new CompressionReport(sample.Name);
			r.BPS = block.GetBitsPerSample();
			r.Entropy = BitMagic.Entropy(sample.Data);
			
			if(block is BiLevelBlock)
			{
				r.TheorBPS = ((BiLevelBlock) block).TheoreticalBPS;
			}
			return r;
		}

		

		public override string ToString()
		{
			return string.Format("{3,4:F2} ratio, {0,6:F2} ent, {1,4:F2} bps, {2,4:F2} tbps", Entropy, BPS, TheorBPS);
		}
	}
}
