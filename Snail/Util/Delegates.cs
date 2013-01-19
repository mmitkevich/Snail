using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Snail.Util
{
	public delegate void RefAction<T1>(ref T1 t1);

	public delegate void OutAction<T1>(out T1 t1);

	public delegate T2 RefFunc<T1, T2>(ref T1 t1);

	public delegate void RefAction<in T1, T2>(T1 t1, ref T2 t2);
}
