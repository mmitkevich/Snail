namespace Snail.Threading
{
	//[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
	public unsafe struct FixedArgs
	{
		public const int MAX_WORD_ARGS = 4
			;
		public fixed ulong WordArgs[MAX_WORD_ARGS];

		public T Read<T>(ref int p)
		{
			T value = default(T);
			p = Read<T>(p, ref value);
			return value;
		}

		public int Read<T>(int p, ref T obj)
		{
			fixed (ulong* p0 = WordArgs)
			{
				ulong* ptr = p0 + p;
#if IL
			ldloc ptr
			ldobj !!T
			stobj !!T
			ldarg p
			sizeof !!T
			ldc.i4 3
			shr
			add
			ret
#endif
				ulong* fake = ptr;
			}
			return p;
		}

		public int Write<T>(int p, ref T obj)
		{
			fixed (ulong* p0 = WordArgs)
			{
				ulong* ptr = p0 + p;
#if IL
			ldloc ptr
			ldarg obj 
			ldobj !!T
			stobj !!T
			ldarg p
			sizeof !!T
			ldc.i4 3
			shr
			add
			ret
#endif
				ulong* fake = ptr;
			}
			return p;
		}
	}
}
