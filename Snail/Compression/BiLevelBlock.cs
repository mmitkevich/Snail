using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Snail.Util;

namespace Snail.Compression
{

	public class BiLevelBlock : ByteBlock<byte>
	{
		protected int N0;
		protected int N1;
		protected int BlockSize;
		protected int[] C1;

		public double TheoreticalBPS { get; private set; }

		public override int Count 
		{
			get { return _data.Length==0?0:(_data[0] | (_data[1] << 8) | (_data[2] << 16 | _data[3] << 24)); }
		}

		public BiLevelBlock() 
		{
			N0 = 8;
			N1 = N0 - 2;
			BlockSize = 4;
			TheoreticalBPS = double.PositiveInfinity;
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder(
				string.Format("N0={0}, N1={1}, B={2}, bps={3:F2},", N0, N1, BlockSize, TheoreticalBPS));
			if (C1 != null && C1[0]!=0)
				for (int i = 0; i < C1.Length && C1[i]>0; i++)
					sb.AppendFormat("p{0}={1:F2},", i, (double)C1[i]/C1[0]);
			return sb.ToString();
		}


		public void Preprocess(byte[] input, int start, int count)
		{
			C1 = new int[sizeof(byte)*8];
			N0 = 1;
			//C1[0] = input.Length;

			for(int i=start; i<start+count; i++)
			{
				int b = (sbyte)input[i];

				if (b < 0)
					b = (-b) & 0x7F;

#if false
				int h = BITS_PER_SAMPLE-2;
				int mask = 1 << h;
				while ((b & mask) == 0)
				{
					mask >>= 1;
					if (--h == 0)
						break;
				}
				h++;		// total bits for magnitude
#else
				int h = BitMagic.WordLengthNoLeadingZeros((uint)b);
#endif

				// count of samples with bits requred>h (because we have h plus 1 bit for sign)
				for (int k = 0; k <= h; k++ )
					C1[k]++;
				
				h++;		// total bits for all
				if (h > N0)
					N0 = h;
			}
		}

		public static double BitsPerSample(int N0, int N1, double p0, int bs)
		{
			return 1.0/bs + N0 - (N0 - N1)*Math.Pow(1 - p0, bs);
		}

		public void Optimize()
		{
			N1 = N0;
			BlockSize = C1[0]+1;	// something larger than length
			TheoreticalBPS = BitsPerSample(N0, N1, 0, BlockSize);

			int L = C1[0];

			for(int n1=1;n1<N0;n1++)
			{
				double p0 = (double) C1[n1] / L;
				double t = Math.Sqrt((N0 - n1) * p0);
				double xs = 1.0 / t;
				double k = xs * p0;
				if (k <= 0.5 )
				{
					//double bitRate = 2*t + n1;
					int bs = (int)Math.Round(xs);
					if (bs < 1)
						bs = 1;
					double bitRate = BitsPerSample(N0, n1, p0, bs);//1.0/bs + N0 - (N0 - n1)*Math.Pow(1 - p0, bs);
					if (bitRate < TheoreticalBPS)
					{
						TheoreticalBPS = bitRate;
						N1 = n1;
						BlockSize = bs;
					}
				}
			}
		}

		public override void  Write(int dataIndex, byte[] src, int srcIndex, int count)
		{
			if(dataIndex!=0)
				throw new NotSupportedException("Can write only to the beginning of buffer");
			
			Preprocess(src,srcIndex,count);
			Optimize();

			_data = new byte[
				(count * N0 + count / BlockSize)/ sizeof(byte)/8 + 4 + 1];

			uint mask0 = ~((1u << (N0 - 1)) - 1);
			uint mask1 = ~((1u << (N1 - 1)) - 1);

			int inputIdx = srcIndex;
			int inputEnd = srcIndex+count;
			
			int outputIdx = 4;
			int outputShift = 0;

			while(inputIdx<inputEnd)
			{
				int blockType = 1;
				int blockEnd = inputIdx + BlockSize;
				for(int i = inputIdx; i < blockEnd; i++)
				{
					if(i>=inputEnd)
						break;

					int sample = (sbyte)src[i];
					if (sample < 0)
						sample = (-sample)&0x7F;

					if ((sample & mask1) != 0)	// has bits higher whan N1
					{
						if((sample & mask0) != 0)
							throw new InvalidOperationException("Value exceeded "+N0+"  bits at "+i+":"+sample);
						blockType = 0;
						break;
					}
				}
				
				int N = blockType == 1 ? N1 : N0;

				if(blockType==1)
					_data[outputIdx] |= (byte)(1<<outputShift);
				
				outputShift++;
				outputIdx += outputShift >> 3;
				outputShift &= 7;
				uint m = (1u<<N) - 1;

				for (; inputIdx < blockEnd; inputIdx++)
				{
					uint sample = 0;
					if (inputIdx < inputEnd)
					{
						sample = (uint)(sbyte)src[inputIdx];
						sample &= m;
					}
					_data[outputIdx] |= (byte)(sample << outputShift);
					if (inputIdx >= inputEnd)
					{
						if(sample!=0)
						{
							
						}
						break;
					}

					outputShift += N;
					if(outputShift>=8)
					{
						outputIdx++;
						_data[outputIdx] |= (byte) (sample >> (8 - (outputShift - N)));
						outputShift &= 8 - 1;
					}
					
				}
			}

			outputIdx++;

			if(outputIdx!=_data.Length)
				Array.Resize(ref _data, outputIdx);

			_data[0] = (byte) count;
			_data[1] = (byte)(count >> 8);
			_data[2] = (byte)(count >> 16);
			_data[3] = (byte)(count >> 24);
		}

		public override int  Read(int dataIndex, byte[] dst, int dstIndex, int count)
		{
			if (dataIndex != 0)
				throw new NotSupportedException("Read from the beginning only supported");

			int word = 0;
			int rest = 0;
			int inputLen = _data.Length;
			int outputIdx = dstIndex;
			int outputLen = _data[0] | (_data[1] << 8) | (_data[2] << 16) | (_data[3] << 24);
			if(count>outputLen)
				throw new InvalidOperationException("Cannot read more than buffer size");
			int inputIndex = 4;
			int outputEnd = dstIndex + count;
			while (outputIdx < outputEnd)
			{
				if(rest<1)
				{
					word = _data[inputIndex++];
					rest = 8;
				}

				int blockType = word & 1;
				
				word >>= 1;	// have rest 8-inputOffset
				rest--;

				int N = blockType == 0 ? N0 : N1;
				int sm = 1 << (N-1);
				int m = sm - 1;
				int outputLeft = outputLen - outputIdx;
				if (BlockSize< outputLeft)
					outputLeft = BlockSize;
				for (int i = 0; i < outputLeft; i++)
				{
					if (rest < N)
					{
						word |= _data[inputIndex] << rest;
						inputIndex++;
						rest += 8;
					}
					
					int sample;
					if ((word & sm) != 0)
						sample = ~m | word;
					else
						sample = word & m;
					
					dst[outputIdx++] = (byte) sample;
					rest -= N;
					word >>= N;
				}
			}
			return count;
		}
		
	}
}
