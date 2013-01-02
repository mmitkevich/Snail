#define BOXING

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Snail.Util;

namespace Snail.Threading
{

	public class ArgsBuffer
	{
		public const int ELEMENTS_COUNT = 128*1024;
		public const int ELEMENT_SIZE = 128;
#if BOXING
		private object[] _data;
#else
		private byte[] _data;
#endif
		private int _maxSize = 1024;
		private int _size;

		private BQueue<int,BQueueElement<int>> _offsets;
		private Volatile.PaddedInteger _batchOffset = new Volatile.PaddedInteger(0);
		private Volatile.PaddedInteger _headOffset = new Volatile.PaddedInteger(0);
		private Volatile.PaddedInteger _tailOffset = new Volatile.PaddedInteger(0);

		public ArgsBuffer() : this(ELEMENTS_COUNT, ELEMENT_SIZE){}

		public ArgsBuffer(int count, int maxSize)
		{
			_offsets = new BQueue<int,BQueueElement<int>>(count, -1, true);
			_maxSize = maxSize;
#if BOXING
			_data = new object[count*_maxSize];
			_size = count * _maxSize - _maxSize;
#else
			_data = new byte[count * _maxSize];
			_size = count*_maxSize-_maxSize;
#endif
		}
#if BOXING
		public object[] GetBuffer()
#else
		public byte[] GetBuffer()
#endif
		{
			return _data;
		}

		public int Capacity
		{
			get { return _offsets.Capacity; }
		}
		
		public int Count
		{
			get { return _offsets.Count; }
		}

		public bool BeginWrite(int ticksWait = -1)
		{
			if (0 == _offsets.WaitForFreeSlots(1, ticksWait))
				return false;
			_batchOffset.WriteUnfenced(_headOffset.ReadUnfenced());
			return true;
		}

		public void EndWrite()
		{
			if (_headOffset.ReadUnfenced() >= _size)
				_headOffset.WriteUnfenced(0);
			_offsets.Enqueue(_batchOffset.ReadUnfenced());
		}
	
		public bool BeginRead(int ticksWait=-1)
		{
			int seq;
			if (0 == _offsets.WaitForData(ticksWait))
				return false;
			var ofs = _offsets.Dequeue();
			if(_tailOffset.ReadUnfenced()!=ofs)
				throw new InvalidOperationException();
			return true;
		}

		public void EndRead()
		{
			if (_tailOffset.ReadUnfenced()>= _size)
				_tailOffset.WriteUnfenced(0);
		}

		public T Read<T>() where T:struct
		{
			T obj = default(T);
			var ofs = _tailOffset.ReadUnfenced();
		
#if BOXING
			obj = (T) _data[ofs++];
#else
			ofs+=Read(ofs, ref obj);
#endif
			_tailOffset.WriteUnfenced(ofs);
			return obj;
		}

		public T ReadRef<T>() where T:class
		{
#if BOXING
			T obj = default(T);
			var ofs = _tailOffset.ReadUnfenced();

			obj = (T) _data[ofs++];

			_tailOffset.WriteUnfenced(ofs);
			return obj;
#else			
			GCHandle gch = GCHandle.FromIntPtr(Read<IntPtr>());
			return (T)gch.Target;
#endif
		}

		public T GetTail<T>(int shift = 0)
		{
			T obj = default(T);
			var ofs = _tailOffset.ReadUnfenced() + shift*ByteArrayUtils.SizeOf<T>();
			if (ofs < 0)
				ofs += _size;
			else if (ofs >= _size)
				ofs -= _size;
#if BOXING
			obj = (T) _data[ofs];
#else
			Read(ofs, ref obj);
#endif
			return obj;
		}

		public T GetHead<T>(int shift = 0)
		{
			T obj = default(T);
			var ofs = _headOffset.ReadUnfenced() + shift * ByteArrayUtils.SizeOf<T>();
			if (ofs < 0)
				ofs += _size;
			else if (ofs >= _size)
				ofs -= _size;
#if BOXING
			obj = (T) _data[ofs];
#else
			Read(ofs, ref obj);
#endif
			return obj;
		}

		public void Write<T>(T obj) where T:struct 
		{
			var ofs = _headOffset.ReadUnfenced();
#if BOXING
			_data[ofs]=obj;
			ofs++;
#else			
			ofs += Write(ofs, ref obj);
#endif
			//if (ofs >= _size)
			//	ofs = 0;
			_headOffset.WriteUnfenced(ofs);
		}

		public void WriteRef<T>(T obj) where T:class
		{
#if BOXING
			var ofs = _headOffset.ReadUnfenced();
			_data[ofs] = obj;
			ofs++;
			_headOffset.WriteUnfenced(ofs);
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
			ldfld uint8[] Snail.Threading.ArgsBuffer::_data
			ldarg p
			ldelema [mscorlib]System.Byte 
			ldarg obj 
			ldobj !!T
			stobj !!T
			sizeof !!T
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
			ldfld uint8[] Snail.Threading.ArgsBuffer::_data
			ldarg p
			ldelema [mscorlib]System.Byte 
			ldobj !!T
			stobj !!T
			sizeof !!T
			ret
#endif
			}
			return p;
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

		public static unsafe byte* Write<T>(byte* p, ref T obj)
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
			starg p
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
			starg p
#endif
			}
			return p;
		}
	}
}
