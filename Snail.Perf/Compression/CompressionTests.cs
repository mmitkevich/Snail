//#define W4
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Snail.Compression;

namespace Snail.Tests.Compression
{
	internal class CompressionTests
	{
		private static int Compare<T>(T[] a, T[] b,  out int diffIndex)
		{
			diffIndex = -1;
			if (a.Length != b.Length)
				return a.Length-b.Length;
			for (int i = 0; i < b.Length; i++)
			{
				int r = Comparer.Default.Compare(a[i], b[i]);
				if (r != 0)
				{
					diffIndex = i;
					return r;
				}
			}
			return 0;
		}

		private static List<Report> MeasureCompressDecompressTime<T>(ICompressedBlock<T> block, Sample<T> sample)
		{
			T[] data = sample.Data;
			T[] data2 = null;

			var rep = new List<Report>(new[]
			    {
			               	new Report(block.GetType().Name, "Compress", sample.Name),
			               	new Report(block.GetType().Name, "Decompress", sample.Name)
			    });
				
			rep[0].Run(data.Length, () => block.Write(data));
			rep[0].CustomReports.Add(CompressionReport.Create(block,sample));
			rep[1].Run(data.Length, () => data2 = block.Read());
			int diffIndex;
			if (Compare(data, data2, out diffIndex) != 0)
			{
				rep[1].Status = rep[0].Status = diffIndex < 0
				             	? string.Format("{0}=length(a)!=length(b)={1}", data.Length, data2.Length)
				             	: string.Format("0x{0:X}=a[i]!=b[i]=0x{1:X} at i={2}", data[diffIndex], data2[diffIndex], diffIndex);
				
				throw new InvalidOperationException(rep[0].Status);
			}

			return rep;
		}

		private const int NUMBER = 100000;
		private const double SIGMA = 15;

		private static T[] GetNormalSample<T>(int len) 
		{
			T[] sample = new T[len];

			for (int i = 0; i < sample.Length; i++)
				sample[i] = Snail.Util.Converter<double,T>.Convert(RandomGen.GetNormal(0, SIGMA));
			return sample;
		}

		private static T[] GetLinearSample<T>(int len)
		{
			T[] sample = new T[len];

			for (int i = 0; i < len; i++)
				sample[i] = Snail.Util.Converter<int, T>.Convert(-len / 2 + i);
			return sample;
		}

		private static void Measure<T>(Func<ICompressedBlock<T>> newBlock, params Sample<T>[] samples)
		{
			foreach (Sample<T> s in samples)
				_reports.AddRange(MeasureCompressDecompressTime(newBlock(), s));
		}

		private static Sample<T>[] GetSamples<T>()
		{
			return new Sample<T>[]
			       {
			       	new Sample<T>("Normal", NUMBER,(i)=>GetNormalSample<T>(i)), 
					//new Sample<T>("Linear", NUMBER,(i)=>GetLinearSample<T>(i)), 
			       };
		}

		private static List<Report> _reports = new List<Report>();

		public static void Run()
		{
			Measure(() => new BiLevelBlock(), GetSamples<byte>());
			Measure(() => new LZOBlock(), GetSamples<byte>());
			
			Measure(
				() => new DiffBlock<int>(new LEB128Block<int>(new TruncByteBlock<byte>())),
				GetSamples<int>());
			
			Measure(() => new LEB128Block<int>(), GetSamples<int>());


			foreach(var r in _reports.OrderBy(r => r.MillionsPerSecond))
				Console.WriteLine(r);
		}
	}
}
