/**********************************************************************************************************************
 * SplendidCRM is a Customer Relationship Management program created by SplendidCRM Software, Inc. 
 * Copyright (C) 2005-2023 SplendidCRM Software, Inc. All rights reserved.
 *********************************************************************************************************************/
#nullable disable
using System;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	/// <summary>
	/// Timezone handling utility.
	/// Migrated from SplendidCRM/_code/TimeZone.cs for .NET 10 ASP.NET Core.
	/// </summary>
	public class TimeZone
	{
		private readonly IMemoryCache _memoryCache;
		private Guid _gTIMEZONE_ID;
		private TimeZoneInfo _tzInfo;

		public TimeZone(IMemoryCache memoryCache)
		{
			_memoryCache = memoryCache;
			_gTIMEZONE_ID = Guid.Empty;
			_tzInfo = TimeZoneInfo.Local;
		}

		public Guid TIMEZONE_ID
		{
			get { return _gTIMEZONE_ID; }
			set { _gTIMEZONE_ID = value; }
		}

		/// <summary>
		/// Creates a TimeZone from the timezone ID stored in the database.
		/// </summary>
		public static TimeZone CreateTimeZone(IMemoryCache memoryCache, Guid gTIMEZONE_ID)
		{
			TimeZone tz = new TimeZone(memoryCache);
			tz.TIMEZONE_ID = gTIMEZONE_ID;
			return tz;
		}

		/// <summary>
		/// Converts a DateTime from the server timezone to the user's timezone.
		/// </summary>
		public DateTime FromServerTime(DateTime dtServerTime)
		{
			if (dtServerTime == DateTime.MinValue)
				return dtServerTime;
			try
			{
				return TimeZoneInfo.ConvertTime(dtServerTime, TimeZoneInfo.Local, _tzInfo);
			}
			catch
			{
				return dtServerTime;
			}
		}

		/// <summary>
		/// Converts a DateTime from the user's timezone to the server timezone.
		/// </summary>
		public DateTime ToServerTime(DateTime dtLocalTime)
		{
			if (dtLocalTime == DateTime.MinValue)
				return dtLocalTime;
			try
			{
				return TimeZoneInfo.ConvertTime(dtLocalTime, _tzInfo, TimeZoneInfo.Local);
			}
			catch
			{
				return dtLocalTime;
			}
		}
	}
}
