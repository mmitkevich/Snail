using System.Runtime.InteropServices;

namespace Snail.Threading
{
	public class ArgsBuffer
	{
		private byte[] _buffer;
		
		private const int CacheLineSize = 64;

		[StructLayout(LayoutKind.Explicit, Size = CacheLineSize * 2)]
		private struct PaddedInteger
		{
            [FieldOffset(CacheLineSize)]
            public int Value;
		}

		private PaddedInteger _pread;
		private PaddedInteger _pwrite;
		private PaddedInteger _pavail;	

		private int _size;
		private int _maxSize = 1024;
		private int _mask;

		public ArgsBuffer(int size)
		{
			_size = size;
			_mask = size - 1;
			_buffer = new byte[_size + _maxSize];
		}

		public byte[] GetBuffer()
		{
			return _buffer;
		}

		public int Length
		{
			get { return _size; }
		}
		
		public void Enqueue<T>(ref T obj)
		{
		}


		public static int SizeOf<T>()
		{
			int r = 0;
#if IL
			sizeof !!T
			stloc r
#endif
			return r;
		}

		public static unsafe byte* Write<T>(byte *p, ref T obj)
		{
			if (true)
			{
#if IL
			ldarg p
			ldarg obj 
			cpobj !!T
			sizeof !!T
			ldarg p
			add
			//stloc r
			ret
#endif
			}
			return p;			
		}


		public static unsafe byte* Read<T>(byte* p, ref T obj)
		{
			if (true)
			{
#if IL
			ldarg obj 
			ldarg p
			cpobj !!T
			sizeof !!T
			ldarg p
			add
			ret
#endif
			}
			return p;
		}

		public int  Write<T>(int p, ref T obj)
		{
			if(true)
			{
#if IL
			ldarg.0
			ldfld uint8[] Snail.Threading.ArgsBuffer::_buffer
			ldarg p
			//ldarg.0
			//ldfld int32 Snail.Threading.ArgsBuffer::_mask;
			//and
			ldelema [mscorlib]System.Byte 
			ldarg obj 
			cpobj !!T
			sizeof !!T
			ldarg p
			add
			ret
#endif
			}
			return p;
		}

		public int Read<T>(int p, ref T obj)
		{
			if (true)
			{
#if IL
			ldarg obj 
			ldarg.0
			ldfld uint8[] Snail.Threading.ArgsBuffer::_buffer
			ldarg p
			//ldarg.0
			//ldfld int32 Snail.Threading.ArgsBuffer::_mask;
			//and
			ldelema [mscorlib]System.Byte 
			ldobj !!T
			stobj !!T
			sizeof !!T
			ldarg p
			add
			ret
#endif
			}
			return p;
		}

	}


}
