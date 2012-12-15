using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Snail.Tests
{
	internal class Report
	{
		public string Algo;
		public string Sample;
		public string Status = "OK";
		public string Method;

		public List<object> CustomReports = new List<object>(); 

		public double NanosPerOne
		{
			get { return Elapsed.Ticks * 100.0 / Iterations; }
		}

		public double MillionsPerSecond
		{
			get { return Iterations / 1.0e06 / Elapsed.TotalSeconds; }
		}

		public DateTime StartTime;
		public DateTime EndTime;

		public TimeSpan Elapsed;

		public int Iterations;

	
		public Report(string algo, string method, string sample)
		{
			Algo = algo;
			Sample = sample;
			Method = method;
		}

		public void Run(int count, Action a)
		{
			Iterations += count;
			System.Diagnostics.Stopwatch sw = new Stopwatch();
			StartTime = DateTime.Now;
			sw.Start();
			a();
			sw.Stop();
			Elapsed += sw.Elapsed;
			EndTime = DateTime.Now;
		}

		public override string ToString()
		{
			var s = string.Format(
				"{0,40}{1:F3} M done, {2,8:F3} M/s, {3,8:F2} ns/one",
				(Algo + "." + Sample + ",").PadRight(40), Iterations/1.0e06, MillionsPerSecond, NanosPerOne);

			for (int i = 0; i < CustomReports.Count; i++)
				s = s + "," + CustomReports[i];

			return s;
		}
	}
}
