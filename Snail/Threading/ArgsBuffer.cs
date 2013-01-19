#define BYTEBUF
//#define SAFE


namespace Snail.Threading
{
	using System.Runtime.InteropServices;

	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	public unsafe struct ArgsBuffer
	{
		public const int DefaultCapacity = 128*1024;
		public const int DefaultElementMaxSize = 8;

		private object[] _args;
		private int _capacity;

	#if BYTEBUF
		private  byte[] _buffer;
		private int _bufferCapacity;
		private byte* _pbuffer;
	#endif


		
		//private unsafe struct Volatiles
		//{
			public fixed long _pad1[8];
			public int _head;
			public int _headRef;
			private byte* _phead;
			public fixed long _pad2[8];
			public int _tail;
			public int _tailRef;
		//}

		//private Volatiles _x;

		public int Tail
		{
			get { return _tail; }
			set { _tail = Wrap(value); }
		}

		private int Wrap(int ofs)
		{
			if (ofs >= _bufferCapacity)
				ofs = 0;
			return ofs;
		}

		public ArgsBuffer(int capacity, int argsPerMessage
//#if BYTEBUF
			, int bytesPerMessage
//#endif
			)
		{
			//_x = default(Volatiles);
			_tail = _tailRef = _head = _headRef = 0;
			_args = new object[capacity * argsPerMessage];
			_capacity = (capacity-1) * argsPerMessage;
#if BYTEBUF
			_buffer = new byte[capacity * bytesPerMessage];
			_bufferCapacity = (capacity-1) * bytesPerMessage;
			GCHandle gch = GCHandle.Alloc(_buffer,GCHandleType.Pinned);
			_phead = _pbuffer = (byte*)gch.AddrOfPinnedObject();
#endif
		}

#if BYTEBUF
		public byte[] Buffer
		{
			get{return _buffer;}
		}
		public int BufferCapacity
		{
			get{return _bufferCapacity;}
		}
#endif
		
		public object[] Args
		{
			get { return _args; }
		}

		public int Capacity
		{
			get { return _capacity; }
		}

#if BYTEBUF		
		public void FreeTail<T>()
		{
			var ofs = _tail;
			ofs += ByteArrayUtils.SizeOf<T>();
			if (ofs >= _bufferCapacity)
				ofs = 0;
			_tail = ofs;
		}
#endif

		public T Read<T>() where T:struct
		{
			T obj = default(T);

			var ofs = _tail;
		
#if !BYTEBUF
			obj = (T) _args[ofs++];
			if (ofs >= _capacity)
				ofs = 0;
#else
			ByteArrayUtils.Read(_buffer, ofs, ref obj);
			FreeTail<T>();
#endif
			
			_tail = ofs;
			return obj;
		}

		public unsafe void Read<T>(ref T obj) where T : struct
		{
			var ofs = _tail;
#if !BYTEBUF
			obj = (T) _args[ofs++];
			if (ofs >= _capacity)
				ofs = 0;
#else
			ByteArrayUtils.Read(_buffer, ofs, ref obj);
			FreeTail<T>();
#endif

			_tail = ofs;
		}

		public T ReadRef<T>() where T:class
		{
#if !GCHANDLE
			T obj = default(T);
			var ofs = _tailRef;
			obj = (T) _args[ofs++];
			if (ofs >= _capacity)
				ofs = 0;
			_tailRef = ofs;
			return obj;
#else			
			GCHandle gch = GCHandle.FromIntPtr(Read<IntPtr>());
			return (T)gch.Target;
#endif
		}

		public void Write<T>(T obj) where T:struct
		{
#if !BYTEBUF
			var ofs = _head;
			_args[ofs] = obj;
			ofs++;
			if (ofs >= _capacity)
				ofs = 0;
			_head = ofs;
#elif SAFE
			_head = Wrap(_head+Write(_head, ref obj));
#else
			//_phead+=ByteArrayUtils.Write(_phead, ref obj);
			//_phead += ByteArrayUtils.SizeOf<T>();klrea
			//fixed(byte*p0 = &_buffer[0])
			//{
			//	_head = Wrap(_head + ByteArrayUtils.Write(p0+_head, ref obj));
			//}
			_phead = WrapPtr(_phead + ByteArrayUtils.Write(_phead, ref obj));
#endif
		}

		public void Write<T>(ref T obj) where T : struct
		{
#if !BYTEBUF
			var ofs = _head;
			_args[ofs] = obj;
			ofs++;
			if (ofs >= _capacity)
				ofs = 0;
			_head = ofs;
#elif SAFE
			var ofs = _head;
			ofs += Write(ofs, ref obj);

			//ofs %=_bufferCapacity;
			if (ofs >= _bufferCapacity)
				ofs = 0;
			_head = ofs;
#else
			//_phead+=ByteArrayUtils.Write(_phead, ref obj);
			//_phead += ByteArrayUtils.SizeOf<T>();klrea
			_phead = WrapPtr(_phead + ByteArrayUtils.Write(_phead, ref obj));
#endif
		}
		public byte* WrapPtr(byte*p)
		{
			if (p >= _pbuffer + _bufferCapacity)
				p = _pbuffer;
			return p;
		}

		public void WriteRef<T>(T obj) where T:class
		{
#if !GCHANDLE
			var ofs = _headRef;
			_args[ofs] = obj;
			ofs++;
			if (ofs >= _capacity)
				ofs = 0;
			_headRef = ofs;
#else
			var gch = GCHandle.Alloc(obj);
			Write(GCHandle.ToIntPtr(gch));
#endif
		}

		public int Write<T>(int p, ref T obj)
		{
			if (true)
			{
#if IL
			ldarg.0
			ldfld uint8[] Snail.Threading.ArgsBuffer::_buffer
			ldarg p
			ldelema [mscorlib]System.Byte 
			ldarg obj 
			cpobj !!T
			sizeof !!T
			ret
#endif
			}
			return 0;
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
			ldelema [mscorlib]System.Byte 
			cpobj !!T
			sizeof !!T
			ret
#endif
			}
			return 0;
		}
	

	}


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

		public static unsafe void Write<T>(byte[] buffer, int p, T obj)
		{
			if (true)
			{
#if IL
			ldarg buffer
			ldarg p
			ldelema [mscorlib]System.Byte 
			ldarg obj 
			stobj !!T
#endif
			}
		}

		public static unsafe void Read<T>(byte[] buffer, int p, ref T obj)
		{
			if (true)
			{
#if IL
			ldarg obj 
			ldarg buffer
			ldarg p
			ldelema [mscorlib]System.Byte 
			cpobj !!T
#endif
			}
		}

		public static unsafe int Write<T>(byte* p, ref T obj)
		{
			if (true)
			{
#if IL
			ldarg p
			ldarg obj 
			cpobj !!T
			sizeof !!T
			ret
#endif
			}
			return 0;
		}


		public static unsafe int Read<T>(byte* p, ref T obj)
		{
			if (true)
			{
#if IL
			ldarg obj 
			ldarg p
			cpobj !!T
			sizeof !!T
			ret
#endif
			}
			return 0;
		}
	}
}
