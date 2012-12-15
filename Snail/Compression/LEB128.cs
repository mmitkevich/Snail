using System;
using Snail.Compression;
using Snail.Util;

namespace Snail.Compression
{
	public class TruncByteBlock<T> : ByteBlock<T>
	{
		public TruncByteBlock()
		{
			_data = new byte[128];
		}

		public override int Count
		{
			get { return _data.Length; }
		}

		public void Extend(int count)
		{
			if(_data.Length<count)
			{
				byte[] newData = new byte[count*2];
				Buffer.BlockCopy(_data,0,newData,0,_data.Length);
				_data = newData;
			}
		}

		public override void Write(int dataIndex, T[] src, int srcIndex, int count)
		{
			Extend(dataIndex + count);
			for (int i = 0; i < count; i++)
				_data[dataIndex++] = (byte)Snail.Util.Converter<T,int>.Convert(src[i]);
		}

		public override int Read(int dataIndex, T[] dst, int dstIndex, int count)
		{
			for (int i = 0; i < count; i++)
				dst[dstIndex++] = Snail.Util.Converter<int, T>.Convert(_data[dataIndex++]);
			return count;
		}
	}

	public class LEB128Block<T> : ByteBlock<T>
	{
		internal int _count;

		public LEB128Block(ICompressedBlock<byte> inner):base(inner)
		{
		}

		public LEB128Block()
		{
			
		}
		public override int Count
		{
			get { return _count; }
		}

		public override void Write(int dataIndex, T[] src, int srcIndex, int count)
		{
			if(dataIndex!=0)
				throw new InvalidOperationException("Write to nonzero index is not supported");
			
			_count = dataIndex + count;

			_data = new byte[count*36/8+1];
			int srcEnd = srcIndex + count;
			int dstIndex = 0;
			for (int i = 0; i<count; i++)
			{
				uint v = (uint)Snail.Util.Converter<T, int>.Convert(src[i]);
				bool more = true;
				while(more)
				{
					byte b = (byte)(v & 0x7F);
					v >>= 7;
					if (!(v == 0 && (b & 0x80) == 0 || v == -1 && (b & 0x80) == 1))
						b |= 0x80;
					else 
						more = false;
					_data[dstIndex++] = b;
				}
			}
			Array.Resize(ref _data, dstIndex);
			if(null!=_inner)
			{
				_inner.Write(dataIndex, _data, 0, dstIndex);
				_data = new byte[0];
			}

		}

		public override int Read(int dataIndex, T[] dst, int dstIndex, int count)
		{

			byte[] bytes = _inner!=null ? _inner.Read() : _data;

			int srcIndex = 0;
			for (int i = 0; i < count; i++)
			{
				int res = 0;
				int shift = 0;
				byte b = 0;
				while(true)
				{
					b = bytes[srcIndex++];
					res |= (b & 0x7F) << shift;
					shift += 7;
					if ((b & 0x80) == 0)
					{
						if (shift<32 && (b & 0x40) == 1)
							res |= -(1 << shift);
						break;
					}
				}
				dst[i] = Snail.Util.Converter<int, T>.Convert(res);
			}
			return count;
		}
	}
}
