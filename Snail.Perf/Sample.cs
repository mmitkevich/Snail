using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Snail.Tests
{
	internal class Sample<T>
	{
		public String Name;
		public int Length;
		public Func<int, T[]> Generate;
		public T[] Data;

		public Sample(string name, int len, Func<int, T[]> generate)
		{
			Name = name;
			Length = len;
			Generate = generate;
			Data = Generate(Length);
		}
	}
}
