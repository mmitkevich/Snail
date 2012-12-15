#define STRU
#define STRU_AB
#define STRU_C8
#define STRU_REF

using System;
using System.Runtime.InteropServices;
using Snail.Threading;

namespace Snail.Tests.Threading
{
	class ArgsBufferTests
	{

		public static int COUNT = 4 * 1024 * 1024;
		public static int MaxAddr = 0;
		[StructLayout(LayoutKind.Sequential)]
		public struct StructTest
		{
#if STRU_AB
			public int A;
			public int B;
#endif
#if STRU_REF
			public ArgsBuffer Buffer;
#endif
#if STRU_C8
			public int C1,C2,C3,C4,C5,C6,C7;
#endif

			public void Fill(int i, ArgsBuffer ab)
			{
#if STRU_AB
				A = i;
				B = -i;
#endif
#if STRU_REF
				Buffer = ab;
#endif
			}
			public bool Check(int i, ArgsBuffer ab)
			{
				if (false
#if STRU_AB
					|| A != i || B != -i 
#endif
#if STRU_REF
					|| Buffer != ab
#endif
)
					return false;
				return true;
			}
		}

		private static int _q = 0;
		public static void NOP()
		{
			_q++;
		}

		private class StructArray
		{
			private static StructTest[] _stru;
			public StructArray(int count)
			{
				_stru = new StructTest[count];
			}

			public int Write(int i, ref StructTest stru)
			{
				_stru[i] = stru;
				return i + 1;
			}

			public int Read(int i, ref StructTest v)
			{
				v = _stru[i];
				return i + 1;
			}
		}

		private static int[] _arr = new int[2];
		public static int Copy(int i, ref int v)
		{
			_arr[1] = v;
			return i + 1;
		}

		private static StructArray struArr;
		private static ArgsBuffer ab;
		private static int sz;
		public static void test()
		{
			sz = ArgsBuffer.SizeOf<int>();
#if STRU
			sz = ArgsBuffer.SizeOf<StructTest>();
#endif
			MaxAddr = COUNT * sz;


			struArr = new StructArray(COUNT);
			ab = new ArgsBuffer(MaxAddr);

			for (int i = 0; i < 3; i++)
				test1();
		}
		public static unsafe void test1()
		{
			var obj = "abcd";
			var obj2 = "abcd2";


			Report rpt;
			rpt = new Report("CYCLE.NOP n=" + COUNT, "", "sz=" + sz);
			rpt.Run(COUNT, () =>
			{
				int q = 0;
				for (int i = 0; i < COUNT; i++)
				{
					q++;
				}
			});
			Console.WriteLine(rpt);

			rpt = new Report("NOP n=" + COUNT, "", "sz=" + sz);
			rpt.Run(COUNT, () =>
			{
				for (int i = 0; i < COUNT; i++)
					NOP();
			});
			Console.WriteLine(rpt);

			rpt = new Report("WRITE.C# n=" + COUNT, "", "sz=" + sz);

			rpt.Run(COUNT, () =>
			{
				StructTest stru = new StructTest();
				int p = 0;
				int vi = 0;
				for (int i = 0; i < COUNT; i++)
				{
#if STRU	
					stru.Fill(i, ab); 
					p = struArr.Write(p, ref stru);
#elif true
					p = Copy(p, ref vi);
#endif
				}
			});
			Console.WriteLine(rpt);
			rpt = new Report("READ.C#", "Unsafe", "");
			rpt.Run(COUNT, () =>
			{
				StructTest stru = new StructTest();
				int p = 0;
				int v = 0;
				for (int i = 0; i < COUNT; i++)
				{
#if STRU
					p = struArr.Read(p, ref stru);
					if (!stru.Check(i, ab))
						throw new InvalidOperationException();
#elif true
					p = ab.Read(p, ref v);
					if (v != i)
						throw new InvalidOperationException();
#endif

				}
			});
			Console.WriteLine(rpt);

			rpt = new Report("WRITE.SAFE n=" + COUNT, "", "sz=" + sz);

			rpt.Run(COUNT, () =>
			{
				StructTest stru = new StructTest();
				int i = 0;
				int p;
				for (p = 0; p < ab.Length; )
				{
#if STRU
					stru.Fill(i,ab);
					p = ab.Write(p, ref stru);
#elif true
					p = ab.Write(p, ref i);
#endif
					i++;
				}
				Console.WriteLine("i={0} p={1}", i, p);
			});
			Console.WriteLine(rpt);
			rpt = new Report("READ.SAFE", "Unsafe", "");
			rpt.Run(COUNT, () =>
			{
				StructTest stru = new StructTest();
				int i = 0;
				int v = 0;
				for (int p = 0; p < ab.Length; )
				{
#if STRU
					p = ab.Read(p, ref stru);
					if (!stru.Check(i,ab))
						throw new InvalidOperationException();
#elif true
					p = ab.Read(p, ref v);
					if (v != i)
						throw new InvalidOperationException();
#endif
					i++;
				}
			});
			Console.WriteLine(rpt);
			ab = new ArgsBuffer(MaxAddr);
			rpt = new Report("WRITE.PTR n=" + COUNT, "", "sz=" + sz);
			rpt.Run(COUNT, () =>
			{
				StructTest stru = new StructTest();
				int i = 0;
				fixed (byte* pin = &ab.GetBuffer()[0])
				{
					byte* p = pin;
					for (i = 0; i < COUNT; i++)
					{
#if STRU
					stru.Fill(i,ab);
					p = ArgsBuffer.Write(p, ref stru);
#elif true
						p = ArgsBuffer.Write(p, ref i);
#endif
					}
				}
				Console.WriteLine("i={0}", i);
			});
			Console.WriteLine(rpt);

			rpt = new Report("READ.PTR n=" + COUNT, "", "sz=" + sz);
			rpt.Run(COUNT, () =>
			{
				StructTest stru = new StructTest();
				int i = 0;
				fixed (byte* pin = &ab.GetBuffer()[0])
				{
					byte* p = pin;
					int v = 0;
					for (i = 0; i < COUNT; i++)
					{
#if STRU
						p = ArgsBuffer.Read(p, ref stru);
					if (!stru.Check(i,ab))
						throw new InvalidOperationException();
#elif true
						p = ArgsBuffer.Read(p, ref v);
						if (v != i)
							throw new InvalidOperationException();
#endif
					}
				}
				Console.WriteLine("i={0}", i);
			});
			Console.WriteLine(rpt);
		}
	}
}
