namespace Snail.Compression
{

	public interface ICompressedBlock
	{
		/// <summary>
		/// Count of data elements in this block.
		/// </summary>
		int Count { get; }

		/// <summary>
		/// Length of bytes owned by this block 
		/// </summary>
		int BitLength { get; }

		/// <summary>
		/// Next block in chain
		/// </summary>
		ICompressedBlock Inner { get; }
	}

	public interface ICompressedBlock<T>:ICompressedBlock
	{
		/// <summary>
		/// Write data to the end of the block.
		/// </summary>
		/// <param name="src">Data to write.</param>
		void Write(T[] src);

		/// <summary>
		/// Read all the data from the block.
		/// </summary>
		/// <returns></returns>
		T[] Read();

		/// <summary>
		/// Read at maximum <paramref name="count"/> elements of data 
		/// located at index <paramref name="dataIndex"/> 
		/// into buffer <paramref name="src"/>
		/// starting at position <paramref name="srcIndex"/>
		/// </summary>
		/// <param name="dataIndex">starting index of data elements.</param>
		/// <param name="src">destination buffer.</param>
		/// <param name="srcIndex">index in destination buffer.</param>
		/// <param name="count">maximum count of elemnets to write.</param>
		void Write(int dataIndex, T[] src, int srcIndex, int count);
		
		/// <summary>
		/// Read at maximum <paramref name="count"/> elements of data 
		/// located at index <paramref name="dataIndex"/> 
		/// into buffer <paramref name="dst"/>
		/// starting at position <paramref name="dstIndex"/>
		/// </summary>
		/// <param name="dataIndex">starting index of data elements.</param>
		/// <param name="dst">destination buffer.</param>
		/// <param name="dstIndex">index in destination buffer.</param>
		/// <param name="count">maximum count of elemnets to read.</param>
		int Read(int dataIndex, T[] dst, int dstIndex, int count);
	}
	
	public abstract class CompressedBlock<T, TD> : ICompressedBlock<T>
	{
		internal ICompressedBlock<TD> _inner;

		public abstract int Count { get; }

		protected CompressedBlock()
		{
			
		}

		protected CompressedBlock(ICompressedBlock<TD> inner)
		{
			_inner = inner;
		}

		public ICompressedBlock Inner
		{
			get { return _inner; }
		}

		public void Write(T[] src)
		{
			Write(Count, src, 0, src.Length);
		}

		public T[] Read()
		{
			T[] data = new T[Count];
			Read(0, data, 0, Count);
			return data;
		}

		public virtual int BitLength
		{
			get { return 0; }
		}

		public abstract void Write(int dataIndex, T[] src, int srcIndex, int count);

		public abstract int Read(int dataIndex, T[] dst, int dstIndex, int count);
	}

	public abstract class ByteBlock<T> : CompressedBlock<T,byte>
	{
		internal byte[] _data = new byte[0];

		protected ByteBlock()
		{
		
		}

		protected ByteBlock(ICompressedBlock<byte> inner):base(inner)
		{
			
		}

		public override int BitLength
		{
			get { return _data.Length; }
		}
	
	}
	

}
