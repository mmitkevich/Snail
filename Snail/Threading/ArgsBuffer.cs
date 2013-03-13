#define BYTEBUF
#define FIXEDBUF
#define PINNEDBUF


namespace Snail.Threading
{
	using System.Runtime.InteropServices;
	using System;
	public static class ByteArrayUtils
	{
		public static int SizeOf<T>()
		{
			int r = 0;
#if IL
			sizeof !!T
			stloc r
#endif
			return r;
		}

		public static unsafe void Write<T>(byte[] buffer, int p, ref T obj)
		{
			if (true)
			{
#if IL
			ldarg buffer
			ldarg p
			ldelema [mscorlib]System.Byte 
			ldarg obj 
			cpobj !!T
#endif
			}
		}

		public static unsafe void Write<T,TA>(TA[] buffer, int p, T obj)
		{
			if (true)
			{
#if IL
			ldarg buffer
			ldarg p
			ldelema !!TA
			ldarg obj 
			stobj !!T
#endif
			}
		}

		public static unsafe void Read<T, TA>(TA[] buffer, int p, ref T obj)
		{
			if (true)
			{
#if IL
			ldarg obj 
			ldarg buffer
			ldarg p
			ldelema !!TA
			cpobj !!T
#endif
			}
		}

		public static unsafe void Write<T>(IntPtr p, ref T obj)
		{
			if (true)
			{
#if IL
			ldarg p
			ldarg obj 
			cpobj !!T
#endif
			}
		}

		public static unsafe void Write<T>(IntPtr p, T obj)
		{
			if (true)
			{
#if IL
			ldarg p
			ldarga obj 
			cpobj !!T
#endif
			}
		}

		public static unsafe void Read<T>(IntPtr p, ref T obj)
		{
			if (true)
			{
#if IL
			ldarg obj 
			ldarg p
			cpobj !!T
#endif
			}
		}

		public static unsafe T Read<T>(IntPtr p)
		{
			if (true)
			{
#if IL
			ldarg p
			ldobj !!T
			ret
#endif
			}
			return default(T);
		}

		public static unsafe int Subtract<T>(IntPtr left, IntPtr right)
		{
			if (true)
			{
#if IL
			ldarg left
			ldarg right
			sub
			sizeof !!T
			div.un
			ret
#endif
			}
			return 0;
		}
	}
}
