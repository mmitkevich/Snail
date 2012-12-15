using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SlothDB.Threading
{
	/// <summary>
	/// Micro log class for cases when speed of logging is more important than other stuff.
	/// </summary>
	public struct MicroLog
	{
		public enum LogLevel:int
		{
			Info = 1,

			Warn = 8,

			Error = 16,

			Fatal = 32
		}

		internal DateTime Time;
		internal int ThreadId;
		internal string Format;
		internal object[] Args;
		internal int Level;
		internal Exception Exception;

		private const int SIZE = 1024;
		private static MicroLog[] _entries = new MicroLog[SIZE];
		private static Volatile.PaddedInteger _dumpIndex = new Volatile.PaddedInteger(0);
		private static Volatile.PaddedInteger _logIndex = new Volatile.PaddedInteger(-1);
		private static Volatile.PaddedInteger _publishIndex = new Volatile.PaddedInteger(-1);

		public static int MinLevel;
		public static int ThrowLevel = (int)LogLevel.Error;

		public static bool OverrideWhenFull = false;

		/// <summary>
		/// Number of errors.
		/// </summary>
		public static int ErrCount;
		public static TextWriter Writer = Console.Out;
		private static Volatile.Integer IsConsuming = new Volatile.Integer(0);
		private static SpinWait wait=new SpinWait() ;


		static MicroLog()
		{
			Task.Factory.StartNew(Dump4Ever, TaskCreationOptions.LongRunning);
		}

		/// <summary>
		/// Add Info log message to internal queue.
		/// </summary>
		/// <param name="format">Format of string.</param>
		/// <param name="args">arguments for string.</param>
		public static void Info(string format, params object[] args)
		{
			Log((int)LogLevel.Info, format,null, args);
		}

		/// <summary>
		/// Add Error log message to internal queue.
		/// </summary>
		/// <param name="format">Format of string.</param>
		/// <param name="args">arguments for string.</param>
		public static void Error(string format, params object[] args)
		{
			Log((int)LogLevel.Error, format,null, args);
		}

		/// <summary>
		/// Add Error log message to internal queue.
		/// </summary>
		/// <param name="format">Format of string.</param>
		/// <param name="args">arguments for string.</param>
		public static void Error(string format, Exception e, params object[] args)
		{
			Log((int)LogLevel.Error, format,e, args);
		}

		internal static void Log(int level, string format, Exception e, params object[] args)
		{
			if (level < MinLevel || Writer==null)
				return;

			int seq = _logIndex.AtomicIncrementAndGet();
			if (OverrideWhenFull)
			{
				if(seq-_dumpIndex.ReadFullFence()>=SIZE)
					_dumpIndex.WriteFullFence(seq-SIZE+1);
			}
			else
			{
				while (seq - _dumpIndex.ReadFullFence() >= SIZE)
				{
					Dump();
				}
			}

			int i = seq & (SIZE - 1);
			_entries[i].Time = DateTime.Now;
			_entries[i].Level = level;
			_entries[i].Format = format;
			_entries[i].Args = args;
			_entries[i].Exception = e;
			_entries[i].ThreadId = Thread.CurrentThread.ManagedThreadId;
			_publishIndex.WriteFullFence(seq);

			if (level >= (int)LogLevel.Error)
				ErrCount++;

			if(level>=ThrowLevel)
				throw new InvalidOperationException(_entries[i].ToString(),e);
		}

		public override string ToString()
		{
			string r =
				string.Format("{2}{0:HH:mm:ss.fffffff} {1,4}| ", Time, ThreadId, Level >= (int) LogLevel.Error ? "E" : " ") +
				string.Format(Format, Args);
			if(Exception!=null)
				r = r + "|" + Exception.Message+"|"+Exception.StackTrace;
			return r;
		}

		internal void Print(TextWriter sw)
		{
			sw.WriteLine(ToString());
		}

		public static void Dump()
		{
			if (IsConsuming.AtomicCompareExchange(1,0))
			{
				while (_dumpIndex.ReadCompilerOnlyFence() <= _publishIndex.ReadFullFence())
				{
					int i = _dumpIndex.ReadCompilerOnlyFence() & (SIZE - 1);
					if(Writer!=null)
						_entries[i].Print(Writer);
					_dumpIndex.WriteCompilerOnlyFence(_dumpIndex.ReadCompilerOnlyFence() + 1);
				}
				IsConsuming.WriteFullFence(0);
			}
		}

		/// <summary>
		/// Dump log messages forever.
		/// </summary>
		/// <param name="sw">TextWriter to write messages to.</param>
		public static void Dump4Ever()
		{
			while (true)
			{
				Dump();
				Thread.Sleep(0);
			}
		}
	}
}
