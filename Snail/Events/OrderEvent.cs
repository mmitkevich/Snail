using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SlothDB.Events
{
	enum TimeFrameMask
	{
		S1	= 1,			// 1
		S5	= S1 << 1,
		S10 = S5 << 1,
		S30 = S10 << 1,
		M1 = S30 << 1,
		M5 = M1 << 1,
		M10 = M5 << 1,
		M30 = M10 << 1,
		H1 = M30 << 1,
		H2 = H1 << 1,
		H4 = H2 << 1,
		H8 = H4 << 1,
		D = H8 << 1,
		W = D << 1,
		M = W << 1,
		Y = M << 1		// 16
	}

	class OrderEvent
	{
		internal long _id;
		internal int _priceIdx;
		internal int _volumeIdx;

		internal uint _timeFrameMask;
	}
}
