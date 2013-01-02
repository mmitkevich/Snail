#define STRU
#define STRU_AB
#define STRU_C8
#define STRU_REF

//#define NOP
//#define CYCLE
#define CS
#define SAFE
//#define PTR
#define READ
#define WRITE
#define RW

using System;
using System.Runtime.InteropServices;
using Snail.Threading;

namespace Snail.Tests.Threading
{
	class ArgsBufferTests
	{

		public static int COUNT = 16 * 1024 * 1024;
		public static int SIZE = 8*1024;
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

		private class TArray<T>
		{
			private static T[] _stru;
			public TArray(int count)
			{
				_stru = new T[count];
			}

			public int Write(int i, ref T stru)
			{
				_stru[i] = stru;
				return i + 1>=_stru.Length?0:i+1;
			}

			public int Read(int i, ref T v)
			{
				v = _stru[i];
				return i + 1 >= _stru.Length ? 0 : i + 1;
			}
		}
		private static TArray<StructTest> struArr;
		private static TArray<int> _arr;
		private static ArgsBuffer ab;
		private static int sz;
		public static void test()
		{
			sz = ByteArrayUtils.SizeOf<int>();
#if STRU
			sz = ByteArrayUtils.SizeOf<StructTest>();
#endif
			MaxAddr = SIZE * sz;


			struArr = new TArray<StructTest>(SIZE);
			_arr = new TArray<int>(SIZE);
			ab = new ArgsBuffer(SIZE, sz);
			for (int i = 0; i < 3; i++)
				test1();
		}
		public static unsafe void test1()
		{
			var obj = "abcd";
			var obj2 = "abcd2";


			Report rpt;
#if CYCLE
			rpt = new Report("CYCLE.NOP n=" + SIZE, "", "sz=" + sz);
			rpt.Run(SIZE, () =>
			{
				int q = 0;
				for (int i = 0; i < SIZE; i++)
				{
					q++;
				}
			});
			Console.WriteLine(rpt);
#endif
#if NOP
			rpt = new Report("NOP n=" + SIZE, "", "sz=" + sz);
			rpt.Run(SIZE, () =>
			{
				for (int i = 0; i < SIZE; i++)
					NOP();
			});
			Console.WriteLine(rpt);
#endif
#if CS 
#if WRITE
			rpt = new Report("WRITE.C# n=" + SIZE, "", "sz=" + sz);

			rpt.Run(SIZE, () =>
			{
				StructTest stru = new StructTest();
				int p = 0;
				int vi = 0;
				for (int i = 0; i < SIZE; i++)
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
#endif
#if READ
			rpt = new Report("READ.C#", "Unsafe", "");
			rpt.Run(SIZE, () =>
			{
				StructTest stru = new StructTest();
				int p = 0;
				int v = 0;
				for (int i = 0; i < SIZE; i++)
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
#endif
#if RW
			rpt = new Report("READ/WRITE.C# n=" + SIZE, "", "sz=" + sz);

			rpt.Run(COUNT, () =>
			{
				StructTest stru = new StructTest();
				int p = 0,pr=0;
				int vi = 0;
				for (int i = 0; i < COUNT; i++)
				{
#if STRU
					stru.Fill(i, ab);
					p = struArr.Write(p, ref stru);
					pr = struArr.Read(pr, ref stru);
					if (!stru.Check(i, ab))
						throw new InvalidOperationException();
#elif true
					vi = i;
					p = _arr.Write(p, ref vi);
					pr = _arr.Read(pr, ref vi);
					if (vi != i)
						throw new InvalidOperationException();
#endif
				}
			});
			Console.WriteLine(rpt);
#endif
#endif
#if SAFE
#if WRITE
			rpt = new Report("WRITE.SAFE n=" + SIZE, "", "sz=" + sz);

			rpt.Run(SIZE, () =>
			{
				StructTest stru = new StructTest();
				int p=0,i;
				for (i=0;i< SIZE;i++ )
				{
#if STRU
					stru.Fill(i,ab);
					p = ab.Write(p, ref stru);
#elif true
					p = ab.Write(p, ref i);
#endif
				}
				Console.WriteLine("i={0} p={1}", i, p);
			});
			Console.WriteLine(rpt);
#endif
#if READ
			rpt = new Report("READ.SAFE", "Unsafe", "");
			rpt.Run(SIZE, () =>
			{
				StructTest stru = new StructTest();
				int v = 0;
				int p = 0;
				for (int i=0;i<SIZE;i++)
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
				}
			});
			Console.WriteLine(rpt);
#endif
#if RW
			rpt = new Report("READ/WRITE.SAFE", "Unsafe", "");
			rpt.Run(COUNT, () =>
			{
				StructTest stru = new StructTest();
				int v = 0;
				int pr = 0, pw = 0;
				for (int i = 0; i < COUNT; i++)
				{
#if STRU
					stru.Fill(i, ab);
					pw = ab.Write(pw, ref stru);
					pr = ab.Read(pr, ref stru);
					if (!stru.Check(i, ab))
						throw new InvalidOperationException();
#elif true
					v = i;
					pw = ab.Write(pw, ref v);
					pr = ab.Read(pr, ref v);
					if (v != i)
						throw new InvalidOperationException();
#endif
				}
			});
			Console.WriteLine(rpt);
#endif
#endif
#if PTR
#if WRITE
			rpt = new Report("WRITE.PTR n=" + SIZE, "", "sz=" + sz);
			rpt.Run(SIZE, () =>
			{
				StructTest stru = new StructTest();
				int i = 0;
				fixed (byte* pin = &ab.GetBuffer()[0])
				{
					byte* p = pin;
					byte* pe = pin + ab.Capacity;
					for (i = 0; i < SIZE; i++)
					{
#if STRU
					stru.Fill(i,ab);
					p = ByteArrayUtils.Write(p, ref stru);
#elif true
					p = ByteArrayUtils.Write(p, ref i);
#endif
					}
				}
				Console.WriteLine("i={0}", i);
			});
			Console.WriteLine(rpt);
#endif
#if READ
			rpt = new Report("READ.PTR n=" + SIZE, "", "sz=" + sz);
			rpt.Run(SIZE, () =>
			{
				StructTest stru = new StructTest();
				int i = 0;
				fixed (byte* pin = &ab.GetBuffer()[0])
				{
					byte* p = pin;
					byte* pe = pin + ab.Capacity;
					int v = 0;
					for (i = 0; i < SIZE; i++)
					{
#if STRU
						p = ByteArrayUtils.Read(p, ref stru);
					if (!stru.Check(i,ab))
						throw new InvalidOperationException();
#elif true
						p = ByteArrayUtils.Read(p, ref v);
						if (v != i)
							throw new InvalidOperationException();
#endif
					}
				}
				Console.WriteLine("i={0}", i);
			});
			Console.WriteLine(rpt);
#endif
#if RW
			rpt = new Report("READ/WRITE.PTR n=" + COUNT, "", "sz=" + sz);
			rpt.Run(COUNT, () =>
			{
				StructTest stru = new StructTest();
				int i = 0;
				fixed (byte* pin = &ab.GetBuffer()[0])
				{
					byte* p = pin;
					byte* pr = pin;
					byte* pe = pin + ab.Capacity;
					int v;
					for (i = 0; i < COUNT; i++)
					{
#if STRU
						stru.Fill(i, ab);
						p = ByteArrayUtils.Write(p,  ref stru);
						pr = ByteArrayUtils.Read(pr,  ref stru);
						if (!stru.Check(i, ab))
							throw new InvalidOperationException();
#elif true
						v = i;
						p = ByteArrayUtils.Write(p, ref v);
						pr = ByteArrayUtils.Read(pr, ref v);
						if (v != i)
							throw new InvalidOperationException();
#endif
						if (p >= pe) p = pin;
						if (pr >= pe) pr = pin;
					}
				}
				Console.WriteLine("i={0}", i);
			});
			Console.WriteLine(rpt);
#endif
#endif
		}
	}
}
