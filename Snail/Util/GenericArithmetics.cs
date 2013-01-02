using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Text;

namespace Snail.Util
{
	public class Arithmetics<T>
	{
		public static Func<T, T, T> Add;
		public static Func<T, T, T> Subtract;
		public static int SizeOf;

		static Arithmetics()
		{
			{
				// declare the parameters
				ParameterExpression paramA = Expression.Parameter(typeof(T), "a"),
									paramB = Expression.Parameter(typeof(T), "b");
				// add the parameters together
				BinaryExpression body = Expression.Add(paramA, paramB);

				// compile it
				Add = Expression.Lambda<Func<T, T, T>>(body, paramA, paramB).Compile();
			}
			{
				ParameterExpression paramA = Expression.Parameter(typeof(T), "a"),
									paramB = Expression.Parameter(typeof(T), "b");

				// add the parameters together
				BinaryExpression body = Expression.Subtract(paramA, paramB);
				// compile it
				Subtract = Expression.Lambda<Func<T, T, T>>(body, paramA, paramB).Compile();
			}
			{
				SizeOf = Marshal.SizeOf (typeof(T));
			}
		}
	}
	
	public class Converter<T,TD>
	{
		public static Func<T, TD> Convert;

		static Converter()
		{
			{
				ParameterExpression paramA = Expression.Parameter(typeof (T), "a");
				// add the parameters together
				UnaryExpression body = Expression.Convert(paramA, typeof (TD));
				// compile it
				Convert = Expression.Lambda<Func<T, TD>>(body, paramA).Compile();
			}
		}
	}

}
