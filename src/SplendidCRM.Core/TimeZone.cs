/**********************************************************************************************************************
 * SplendidCRM is a Customer Relationship Management program created by SplendidCRM Software, Inc. 
 * Copyright (C) 2005-2022 SplendidCRM Software, Inc. All rights reserved.
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the 
 * GNU Affero General Public License as published by the Free Software Foundation, either version 3 
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; 
 * without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. 
 * See the GNU Affero General Public License for more details.
 * 
 * You should have received a copy of the GNU Affero General Public License along with this program. 
 * If not, see <http://www.gnu.org/licenses/>. 
 * 
 * You can contact SplendidCRM Software, Inc. at email address support@splendidcrm.com. 
 * 
 * In accordance with Section 7(b) of the GNU Affero General Public License version 3, 
 * the Appropriate Legal Notices must display the following words on all interactive user interfaces: 
 * "Copyright (C) 2005-2011 SplendidCRM Software, Inc. All rights reserved."
 *********************************************************************************************************************/
// .NET 10 Migration: SplendidCRM/_code/TimeZone.cs → src/SplendidCRM.Core/TimeZone.cs
// Changes applied:
//   - REMOVED: using System.Web; (HttpContext.Current, HttpApplicationState)
//   - ADDED:   using Microsoft.Extensions.Caching.Memory;
//   - HttpApplicationState (Application[]) in CreateTimeZone() → IMemoryCache parameter
//     with identical cache keys "TIMEZONE.{guid}" and "TIMEZONE.TZID.{tzid}" and "CONFIG.default_timezone"
//   - CreateTimeZone(HttpApplicationState, Guid) → CreateTimeZone(IMemoryCache, Guid)
//   - CreateTimeZone(Guid) uses static ambient IMemoryCache (_ambientCache) instead of HttpContext.Current.Application
//   - SplendidError.SystemMessage(Application, "Error", ...) → SplendidError.SystemMessage(memoryCache, "Error", ...)
//   - Windows Registry access in parameterless constructor preserved with existing try/catch graceful fallback
//     on non-Windows platforms (Linux: Registry.LocalMachine returns null / throws, fallback to hardcoded EST)
//   - All business logic, timezone conversion methods, struct definitions, field and property names preserved exactly
//   - Namespace SplendidCRM preserved
//   - Minimal change clause applied: only framework-level API substitutions made
#nullable disable
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Win32;

namespace SplendidCRM
{
	/// <summary>
	/// Summary description for TimeZone.
	/// Provides custom timezone-aware date/time conversion methods for SplendidCRM.
	/// 
	/// Migrated from SplendidCRM/_code/TimeZone.cs for .NET 10 ASP.NET Core.
	/// 
	/// DESIGN NOTES:
	///   • Timezone instances are cached in IMemoryCache with keys "TIMEZONE.{guid}" and "TIMEZONE.TZID.{tzid}".
	///   • Call TimeZone.SetAmbient(IMemoryCache) at startup to enable the single-argument CreateTimeZone(Guid) overload.
	///   • All date conversion methods preserve the original SugarCRM-compatible logic exactly.
	/// </summary>
	public class TimeZone
	{
		// =====================================================================================
		// Private Windows Registry interop structs
		// SYSTEMTIME and TZI are used in the parameterless constructor to read timezone
		// data from the Windows Registry HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Time Zones\
		// On non-Windows platforms (Linux), the try/catch in the constructor catches the
		// PlatformNotSupportedException / NullReferenceException and falls back to hardcoded EST values.
		// =====================================================================================

		[StructLayout( LayoutKind.Sequential )]
		private struct SYSTEMTIME
		{
			public UInt16 wYear        ;
			public UInt16 wMonth       ;
			public UInt16 wDayOfWeek   ;
			public UInt16 wDay         ;
			public UInt16 wHour        ;
			public UInt16 wMinute      ;
			public UInt16 wSecond      ;
			public UInt16 wMilliseconds;
		}

		[StructLayout( LayoutKind.Sequential )]
		private struct TZI
		{
			public int        nBias         ;
			public int        nStandardBias ;
			public int        nDaylightBias ;
			public SYSTEMTIME dtStandardDate;
			public SYSTEMTIME dtDaylightDate;
		}

		// =====================================================================================
		// Protected instance fields — unchanged from original source
		// =====================================================================================

		protected Guid   m_gID                    ;
		protected string m_sNAME                  ;
		protected string m_sSTANDARD_NAME         ;
		protected string m_sSTANDARD_ABBREVIATION ;
		protected string m_sDAYLIGHT_NAME         ;
		protected string m_sDAYLIGHT_ABBREVIATION ;
		protected int    m_nBIAS                  ;
		protected int    m_nSTANDARD_BIAS         ;
		protected int    m_nDAYLIGHT_BIAS         ;
		protected int    m_nSTANDARD_YEAR         ;
		protected int    m_nSTANDARD_MONTH        ;
		protected int    m_nSTANDARD_WEEK         ;
		protected int    m_nSTANDARD_DAYOFWEEK    ;
		protected int    m_nSTANDARD_HOUR         ;
		protected int    m_nSTANDARD_MINUTE       ;
		protected int    m_nDAYLIGHT_YEAR         ;
		protected int    m_nDAYLIGHT_MONTH        ;
		protected int    m_nDAYLIGHT_WEEK         ;
		protected int    m_nDAYLIGHT_DAYOFWEEK    ;
		protected int    m_nDAYLIGHT_HOUR         ;
		protected int    m_nDAYLIGHT_MINUTE       ;
		protected bool   m_bGMTStorage            ;
		// 01/02/2012 Paul.  Add iCal TZID. 
		protected string m_sTZID                  ;

		// =====================================================================================
		// Static ambient IMemoryCache — replaces HttpContext.Current.Application static access
		// in the single-argument CreateTimeZone(Guid) overload.
		// BEFORE: HttpApplicationState Application = HttpContext.Current.Application;
		// AFTER:  Use _ambientCache registered at startup via SetAmbient().
		// Call TimeZone.SetAmbient(memoryCache) from application startup (DI container setup).
		// =====================================================================================
		private static IMemoryCache _ambientCache;

		/// <summary>
		/// Registers the static ambient IMemoryCache for use by CreateTimeZone(Guid).
		/// Must be called at application startup before any call to CreateTimeZone(Guid).
		/// </summary>
		public static void SetAmbient(IMemoryCache memoryCache)
		{
			_ambientCache = memoryCache;
		}

		// =====================================================================================
		// Public properties — unchanged from original source
		// =====================================================================================

		public Guid ID
		{
			get
			{
				return m_gID;
			}
		}

		// 09/25/2010 Paul.  We need access to the time zone properties in ReportService2010.asmx. 
		public int Bias                     { get { return m_nBIAS              ; } }
		public int StandardBias             { get { return m_nSTANDARD_BIAS     ; } }
		public int StandardDateYear         { get { return m_nSTANDARD_YEAR     ; } }
		public int StandardDateMonth        { get { return m_nSTANDARD_MONTH    ; } }
		public int StandardDateWeek         { get { return m_nSTANDARD_WEEK     ; } }
		public int StandardDateDay          { get { return m_nSTANDARD_DAYOFWEEK; } }
		public int StandardDateHour         { get { return m_nSTANDARD_HOUR     ; } }
		public int StandardDateMinute       { get { return m_nSTANDARD_MINUTE   ; } }
		public int DaylightBias             { get { return m_nDAYLIGHT_BIAS     ; } }
		public int DaylightDateYear         { get { return m_nDAYLIGHT_YEAR     ; } }
		public int DaylightDateMonth        { get { return m_nDAYLIGHT_MONTH    ; } }
		public int DaylightDateWeek         { get { return m_nDAYLIGHT_WEEK     ; } }
		public int DaylightDateDay          { get { return m_nDAYLIGHT_DAYOFWEEK; } }
		public int DaylightDateHour         { get { return m_nDAYLIGHT_HOUR     ; } }
		public int DaylightDateMinute       { get { return m_nDAYLIGHT_MINUTE   ; } }
		public string StandardAbbreviation  { get { return m_sSTANDARD_ABBREVIATION; } }
		public string DaylightAbbreviation  { get { return m_sDAYLIGHT_ABBREVIATION; } }
		// 01/02/2012 Paul.  Add iCal TZID. 
		public string TZID                  { get { return m_sTZID              ; } }

		// =====================================================================================
		// Static factory methods
		// BEFORE: CreateTimeZone(HttpApplicationState Application, Guid gTIMEZONE) using Application["key"]
		// AFTER:  CreateTimeZone(IMemoryCache memoryCache, Guid gTIMEZONE) using IMemoryCache.Get<T>() / Set()
		// =====================================================================================

		/// <summary>
		/// Creates or retrieves a cached TimeZone instance for the given timezone GUID.
		/// Uses the static ambient IMemoryCache registered via SetAmbient().
		/// BEFORE: HttpApplicationState Application = HttpContext.Current.Application; → CreateTimeZone(Application, gTIMEZONE)
		/// AFTER:  Delegates to CreateTimeZone(_ambientCache, gTIMEZONE).
		/// </summary>
		public static TimeZone CreateTimeZone(Guid gTIMEZONE)
		{
			// BEFORE: HttpApplicationState Application = HttpContext.Current.Application;
			//         return CreateTimeZone(Application, gTIMEZONE);
			// AFTER:  Use static ambient IMemoryCache — set at startup via SetAmbient().
			return CreateTimeZone(_ambientCache, gTIMEZONE);
		}

		// 06/26/2010 Paul.  We need to be able to create a timezone from within the Workflow engine. 
		/// <summary>
		/// Creates or retrieves a cached TimeZone instance for the given timezone GUID using explicit IMemoryCache.
		/// Falls back to the configured default timezone, then to EST (BFA61AF7-26ED-4020-A0C1-39A15E4E9E0A),
		/// and finally to a hardcoded EST object if no cached timezone can be found.
		/// BEFORE: CreateTimeZone(HttpApplicationState Application, Guid gTIMEZONE)
		/// AFTER:  CreateTimeZone(IMemoryCache memoryCache, Guid gTIMEZONE)
		///         Application["key"] as T → memoryCache?.Get&lt;TimeZone&gt;("key")
		///         Application["key"] = val → memoryCache?.Set("key", val)
		///         Sql.ToGuid(Application["CONFIG.default_timezone"]) → Sql.ToGuid(memoryCache?.Get&lt;object&gt;("CONFIG.default_timezone"))
		///         SplendidError.SystemMessage(Application, ...) → SplendidError.SystemMessage(memoryCache, ...)
		/// </summary>
		public static TimeZone CreateTimeZone(IMemoryCache memoryCache, Guid gTIMEZONE)
		{
			// BEFORE: T10z = Application["TIMEZONE." + gTIMEZONE.ToString()] as SplendidCRM.TimeZone;
			// AFTER:  IMemoryCache.Get<TimeZone>() returns null if key not present (equivalent to Application[] cast returning null)
			TimeZone T10z = memoryCache?.Get<TimeZone>("TIMEZONE." + gTIMEZONE.ToString());
			if ( T10z == null )
			{
				// 08/29/2005 Paul. First try and use the default from CONFIG. 
				// BEFORE: gTIMEZONE = Sql.ToGuid(Application["CONFIG.default_timezone"]);
				// AFTER:  Read from IMemoryCache using the same key
				gTIMEZONE = Sql.ToGuid(memoryCache?.Get<object>("CONFIG.default_timezone"));
				// BEFORE: T10z = Application["TIMEZONE." + gTIMEZONE.ToString()] as SplendidCRM.TimeZone;
				T10z = memoryCache?.Get<TimeZone>("TIMEZONE." + gTIMEZONE.ToString());
				if ( T10z == null )
				{
					// Default to EST if default not specified. 
					gTIMEZONE = new Guid("BFA61AF7-26ED-4020-A0C1-39A15E4E9E0A");
					// BEFORE: T10z = Application["TIMEZONE." + gTIMEZONE.ToString()] as SplendidCRM.TimeZone;
					T10z = memoryCache?.Get<TimeZone>("TIMEZONE." + gTIMEZONE.ToString());
				}
				// If timezone is still null, then create a blank zone. 
				if ( T10z == null )
				{
					// BEFORE: SplendidError.SystemMessage(Application, "Error", new StackTrace(true).GetFrame(0), sMessage);
					// AFTER:  SplendidError.SystemMessage(memoryCache, "Error", new StackTrace(true).GetFrame(0), sMessage)
					string sMessage = "Could not load default timezone " + Sql.ToString(memoryCache?.Get<object>("CONFIG.default_timezone")) + " nor EST timezone BFA61AF7-26ED-4020-A0C1-39A15E4E9E0A. "
					                + "Eastern Standard Time will be extracted from the Windows Registry and used as the default.";
					SplendidError.SystemMessage(memoryCache, "Error", new StackTrace(true).GetFrame(0), sMessage);
					// T10z = new TimeZone();
					// 07/25/2006  Paul.  Still having a problem with the hosting company.  
					// Try and skip the entire registry code. 
					// 01/02/2012 Paul.  Add iCal TZID. 
					T10z = new TimeZone
						( new Guid("BFA61AF7-26ED-4020-A0C1-39A15E4E9E0A")
						, "(GMT-05:00) Eastern Time (US & Canada)"
						, "EST"
						, "Eastern Standard Time"
						, "Eastern Daylight Time"
						, "EDT"
						, 300
						,   0
						, -60
						,   0
						,  10
						,   5
						,   0
						,   2
						,   0
						,   0
						,   4
						,   1
						,   0
						,   2
						,   0
						, false
						, "America/New_York"
						);
					// BEFORE: Application["TIMEZONE." + gTIMEZONE.ToString()] = T10z;
					// AFTER:  IMemoryCache.Set() — no expiry needed since timezone data doesn't change
					memoryCache?.Set("TIMEZONE." + gTIMEZONE.ToString(), T10z);
					// 09/14/2015 Paul.  TZID is used with Google Sync and iCloud Sync. 
					// BEFORE: Application["TIMEZONE.TZID." + T10z.TZID] = T10z;
					memoryCache?.Set("TIMEZONE.TZID." + T10z.TZID, T10z);
				}
			}
			return T10z;
		}

		// =====================================================================================
		// Constructors
		// =====================================================================================

		/// <summary>
		/// Parameterless default constructor.
		/// Attempts to read Eastern Standard Time from the Windows Registry.
		/// Falls back to hardcoded EST values on non-Windows platforms or when registry access is denied.
		/// MIGRATION NOTE: This constructor behavior is preserved identically.
		///   On Linux/macOS, Registry.LocalMachine returns null or throws PlatformNotSupportedException;
		///   the catch block populates the hardcoded EST fallback values as it did under IIS on
		///   hosting providers with restricted registry access.
		/// </summary>
		public TimeZone()
		{
			m_gID                    = Guid.Empty  ;
			m_sNAME                  = String.Empty;
			m_sSTANDARD_NAME         = String.Empty;
			m_sSTANDARD_ABBREVIATION = String.Empty;
			m_sDAYLIGHT_NAME         = String.Empty;
			m_sDAYLIGHT_ABBREVIATION = String.Empty;
			try
			{
				RegistryKey keyEST = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Time Zones\Eastern Standard Time");
				if ( keyEST != null )
				{
					m_sSTANDARD_NAME = keyEST.GetValue("Std"    ).ToString();
					m_sNAME          = keyEST.GetValue("Display").ToString();
					m_sDAYLIGHT_NAME = keyEST.GetValue("Dlt"    ).ToString();
					byte[] byTZI         = (byte[]) keyEST.GetValue("TZI");

					TZI tzi ;
					GCHandle h = GCHandle.Alloc(byTZI, GCHandleType.Pinned);
					try
					{
						tzi = (TZI) Marshal.PtrToStructure( h.AddrOfPinnedObject(), typeof(TZI) );
						m_nBIAS                = tzi.nBias                    ;
						m_nSTANDARD_BIAS       = tzi.nStandardBias            ;
						m_nDAYLIGHT_BIAS       = tzi.nDaylightBias            ;
						m_nSTANDARD_YEAR       = tzi.dtStandardDate.wYear     ;
						m_nSTANDARD_MONTH      = tzi.dtStandardDate.wMonth    ;
						m_nSTANDARD_WEEK       = tzi.dtStandardDate.wDay      ;
						m_nSTANDARD_DAYOFWEEK  = tzi.dtStandardDate.wDayOfWeek;
						m_nSTANDARD_HOUR       = tzi.dtStandardDate.wHour     ;
						m_nSTANDARD_MINUTE     = tzi.dtStandardDate.wMinute   ;
						m_nDAYLIGHT_YEAR       = tzi.dtDaylightDate.wYear     ;
						m_nDAYLIGHT_MONTH      = tzi.dtDaylightDate.wMonth    ;
						m_nDAYLIGHT_WEEK       = tzi.dtDaylightDate.wDay      ;
						m_nDAYLIGHT_DAYOFWEEK  = tzi.dtDaylightDate.wDayOfWeek;
						m_nDAYLIGHT_HOUR       = tzi.dtDaylightDate.wHour     ;
						m_nDAYLIGHT_MINUTE     = tzi.dtDaylightDate.wMinute   ;
					}
					finally
					{
						h.Free();
					}
				}
			}
			catch
			{
				// 07/25/2006 Paul.  Some web hosting companies have tight security and block all access to the registry. 
				// In those cases, just assume EST. 
				// MIGRATION NOTE: On Linux/macOS, Registry access is unavailable at runtime; this catch block
				//   provides the same fallback behavior that was observed in restricted Windows hosting environments.
				m_sNAME                  = "(GMT-05:00) Eastern Time (US & Canada)";
				m_sSTANDARD_ABBREVIATION = "EST";
				m_sSTANDARD_NAME         = "Eastern Standard Time";
				m_sDAYLIGHT_NAME         = "Eastern Daylight Time";
				m_sDAYLIGHT_ABBREVIATION = "EDT";
				m_nBIAS                  = 300;
				m_nSTANDARD_BIAS         =   0;
				m_nDAYLIGHT_BIAS         = -60;
				m_nSTANDARD_YEAR         =   0;
				m_nSTANDARD_MONTH        =  10;
				m_nSTANDARD_WEEK         =   5;
				m_nSTANDARD_DAYOFWEEK    =   0;
				m_nSTANDARD_HOUR         =   2;
				m_nSTANDARD_MINUTE       =   0;
				m_nDAYLIGHT_YEAR         =   0;
				m_nDAYLIGHT_MONTH        =   4;
				m_nDAYLIGHT_WEEK         =   1;
				m_nDAYLIGHT_DAYOFWEEK    =   0;
				m_nDAYLIGHT_HOUR         =   2;
				m_nDAYLIGHT_MINUTE       =   0;
				// 01/02/2012 Paul.  Add iCal TZID. 
				m_sTZID                  = "America/New_York";
			}
		}

		/// <summary>
		/// Full constructor accepting all timezone definition parameters.
		/// Used to hydrate a TimeZone instance from database row data or from the EST fallback literal
		/// in CreateTimeZone() when no cached timezone could be located.
		/// </summary>
		public TimeZone
			( Guid   gID                   
			, string sNAME                 
			, string sSTANDARD_NAME        
			, string sSTANDARD_ABBREVIATION
			, string sDAYLIGHT_NAME        
			, string sDAYLIGHT_ABBREVIATION
			, int    nBIAS                 
			, int    nSTANDARD_BIAS        
			, int    nDAYLIGHT_BIAS        
			, int    nSTANDARD_YEAR        
			, int    nSTANDARD_MONTH       
			, int    nSTANDARD_WEEK        
			, int    nSTANDARD_DAYOFWEEK   
			, int    nSTANDARD_HOUR        
			, int    nSTANDARD_MINUTE      
			, int    nDAYLIGHT_YEAR        
			, int    nDAYLIGHT_MONTH       
			, int    nDAYLIGHT_WEEK        
			, int    nDAYLIGHT_DAYOFWEEK   
			, int    nDAYLIGHT_HOUR        
			, int    nDAYLIGHT_MINUTE      
			, bool   bGMTStorage           
			, string sTZID                 
			)
		{
			m_gID                    = gID                    ;
			m_sNAME                  = sNAME                  ;
			m_sSTANDARD_NAME         = sSTANDARD_NAME         ;
			m_sSTANDARD_ABBREVIATION = sSTANDARD_ABBREVIATION ;
			m_sDAYLIGHT_NAME         = sDAYLIGHT_NAME         ;
			m_sDAYLIGHT_ABBREVIATION = sDAYLIGHT_ABBREVIATION ;
			m_nBIAS                  = nBIAS                  ;
			m_nSTANDARD_BIAS         = nSTANDARD_BIAS         ;
			m_nDAYLIGHT_BIAS         = nDAYLIGHT_BIAS         ;
			m_nSTANDARD_YEAR         = nSTANDARD_YEAR         ;
			m_nSTANDARD_MONTH        = nSTANDARD_MONTH        ;
			m_nSTANDARD_WEEK         = nSTANDARD_WEEK         ;
			m_nSTANDARD_DAYOFWEEK    = nSTANDARD_DAYOFWEEK    ;
			m_nSTANDARD_HOUR         = nSTANDARD_HOUR         ;
			m_nSTANDARD_MINUTE       = nSTANDARD_MINUTE       ;
			m_nDAYLIGHT_YEAR         = nDAYLIGHT_YEAR         ;
			m_nDAYLIGHT_MONTH        = nDAYLIGHT_MONTH        ;
			m_nDAYLIGHT_WEEK         = nDAYLIGHT_WEEK         ;
			m_nDAYLIGHT_DAYOFWEEK    = nDAYLIGHT_DAYOFWEEK    ;
			m_nDAYLIGHT_HOUR         = nDAYLIGHT_HOUR         ;
			m_nDAYLIGHT_MINUTE       = nDAYLIGHT_MINUTE       ;
			m_bGMTStorage            = bGMTStorage            ;
			// 01/02/2012 Paul.  Add iCal TZID. 
			m_sTZID                  = sTZID                  ;
		}

		// =====================================================================================
		// Private helper: Daylight Saving Time transition date calculator
		// Calculates the exact transition DateTime for a given year based on month, week-of-month,
		// day-of-week, hour, and minute — matching the Windows SYSTEMTIME wDay (occurrence) pattern.
		// Preserved exactly from original source; no migration changes required.
		// =====================================================================================

		private static DateTime TransitionDate(int nYEAR, int nMONTH, int nWEEK, int nDAYOFWEEK, int nHOUR, int nMINUTE)
		{
			DateTime dtTransitionDate = new DateTime(nYEAR, nMONTH, 1, nHOUR, nMINUTE, 0);
			// First DAYOFWEEK (typically Sunday) in the month. 
			int nFirstDayOfWeek = nDAYOFWEEK + (DayOfWeek.Sunday - dtTransitionDate.DayOfWeek);
			if ( nFirstDayOfWeek < 0 )
				nFirstDayOfWeek += 7;
			dtTransitionDate = dtTransitionDate.AddDays(nFirstDayOfWeek);
			// Now add the weeks, but watch for overflow to next month.  
			dtTransitionDate = dtTransitionDate.AddDays(7 * (nWEEK - 1));
			// In case of overflow, subtract a week until the month matches. 
			while ( dtTransitionDate.Month != nMONTH )
				dtTransitionDate = dtTransitionDate.AddDays(-7);
			return dtTransitionDate;
		}

		// =====================================================================================
		// Instance conversion methods — preserved exactly from original source
		// All business logic, DST calculation logic, and bGMTStorage logic are unchanged.
		// =====================================================================================

		/// <summary>
		/// Converts a server-stored datetime (object) to the user's local timezone.
		/// Handles both GMT storage mode (SugarCRM 3.5+) and legacy local storage mode.
		/// </summary>
		public DateTime FromServerTime(object objServerTime)
		{
			DateTime dtServerTime = Sql.ToDateTime(objServerTime);
			if ( dtServerTime == DateTime.MinValue )
				return dtServerTime;
			// 03/25/2013 Paul.  Force the kind so that ToUniversalTime() will work. 
			if ( dtServerTime.Kind != DateTimeKind.Local )
				dtServerTime = DateTime.SpecifyKind(dtServerTime, DateTimeKind.Local);
			// 11/07/2005 Paul.  SugarCRM 3.5 now stores time in GMT. 
			if ( m_bGMTStorage )
				return FromUniversalTime(dtServerTime);
			else
				return FromUniversalTime(dtServerTime.ToUniversalTime());
		}

		/// <summary>
		/// Converts a server-stored datetime (DateTime) to the user's local timezone.
		/// Handles both GMT storage mode (SugarCRM 3.5+) and legacy local storage mode.
		/// </summary>
		public DateTime FromServerTime(DateTime dtServerTime)
		{
			// 11/29/2008 Paul.  The time cannot be converted if it is already at the min value. 
			if ( dtServerTime == DateTime.MinValue )
				return dtServerTime;
			// 03/25/2013 Paul.  Force the kind so that ToUniversalTime() will work. 
			if ( dtServerTime.Kind != DateTimeKind.Local )
				dtServerTime = DateTime.SpecifyKind(dtServerTime, DateTimeKind.Local);
			// 11/07/2005 Paul.  SugarCRM 3.5 now stores time in GMT. 
			if ( m_bGMTStorage )
				return FromUniversalTime(dtServerTime);
			else
				return FromUniversalTime(dtServerTime.ToUniversalTime());
		}

		/// <summary>
		/// Converts a UTC datetime to the timezone's local time by applying bias and DST offset.
		/// </summary>
		public DateTime FromUniversalTime(DateTime dtUniversalTime)
		{
			// 11/07/2005 Paul.  Don't modify if value is MinValue.
			if ( dtUniversalTime == DateTime.MinValue )
				return dtUniversalTime;
			DateTime dtZoneTime = dtUniversalTime.AddMinutes(-m_nBIAS);
			int nLocalMonth = dtZoneTime.Month;

			// 09/15/2008 Jake.  Changed to use IsDaylightSavings method making To and From universal time methods consistant.
			if (IsDaylightSavings(dtZoneTime))
			{
				dtZoneTime = dtZoneTime.AddMinutes(-m_nDAYLIGHT_BIAS);
			}
			return dtZoneTime;
		}

		/// <summary>
		/// Determines whether the given zone-local datetime falls within daylight saving time.
		/// Uses transition date calculation for the two boundary months; simple month comparison otherwise.
		/// Handles both northern hemisphere (DST month &lt; Standard month) and
		/// southern hemisphere (DST month &gt; Standard month) configurations.
		/// </summary>
		public bool IsDaylightSavings(DateTime dtZoneTime)
		{
			bool bDaylightSavings = false;
			int nLocalMonth = dtZoneTime.Month;
			// This date/time conversion function will be called with a very high frequency.  It is therefore important to optimize as much as possible. 
			// For example, we only have to worry about complicated daylight savings calculations during the transition months.  
			// Otherwise, we are either in daylight savings or not in daylight savings. 
			// If a timezone does not observer daylight savings, then the months will be 0 and no calculations will be performed. 
			if ( nLocalMonth == m_nDAYLIGHT_MONTH )
			{
				// The transition date needs to be calculated every time because the Local year may change, and the date changes every year. 
				DateTime dtTransitionDate = TransitionDate(dtZoneTime.Year, m_nDAYLIGHT_MONTH, m_nDAYLIGHT_WEEK, m_nDAYLIGHT_DAYOFWEEK, m_nDAYLIGHT_HOUR, m_nDAYLIGHT_MINUTE);
				// 03/19/2008 Jason.  Switch if daylight month is before standard because that indicates southern hemisphere. 
				if ( m_nDAYLIGHT_MONTH > m_nSTANDARD_MONTH )
				{
					if ( dtZoneTime < dtTransitionDate )
						bDaylightSavings = true;
				}
				else
				{
					if ( dtZoneTime > dtTransitionDate )
						bDaylightSavings = true;
				}
			}
			else if ( nLocalMonth == m_nSTANDARD_MONTH )
			{
				// The transition date needs to be calculated every time because the Local year may change, and the date changes every year. 
				DateTime dtTransitionDate = TransitionDate(dtZoneTime.Year, m_nSTANDARD_MONTH, m_nSTANDARD_WEEK, m_nSTANDARD_DAYOFWEEK, m_nSTANDARD_HOUR, m_nSTANDARD_MINUTE);
				// Don't add the bias here because it is already part of the zone time. 
				// Since there is an overlap due to the drop back in time, we cannot fully be sure that the 
				// supplied time is before or after the daylight transition.  We will always assume that it is before. 
				// 03/19/2008 Jason.  Switch if daylight month is before standard because that indicates southern hemisphere. 
				if ( m_nDAYLIGHT_MONTH > m_nSTANDARD_MONTH )
				{
					if ( dtZoneTime > dtTransitionDate )
						bDaylightSavings = true;
				}
				else
				{
					if ( dtZoneTime < dtTransitionDate )
						bDaylightSavings = true;
				}
			}
			else
			{
				// 03/19/2008 Jason.  Switch if daylight month is before standard because that indicates southern hemisphere. 
				if ( m_nDAYLIGHT_MONTH > m_nSTANDARD_MONTH )
				{
					// If we are solidly in the daylight savings months, then the calculation is simple. 
					// 09/15/2008 Jake.  Fixed calculation of DLS in southern hemisphere. 
					// Correct way is current month has to be AFTER DLS month, and before standard month. 
					if ( nLocalMonth < m_nSTANDARD_MONTH && nLocalMonth > m_nDAYLIGHT_MONTH )
						bDaylightSavings = true;
				}
				else
				{
					// If we are solidly in the daylight savings months, then the calculation is simple. 
					if ( nLocalMonth > m_nDAYLIGHT_MONTH && nLocalMonth < m_nSTANDARD_MONTH )
						bDaylightSavings = true;
				}
			}
			return bDaylightSavings;
		}

		// 04/04/2006 Paul.  SOAP needs a quick way to convert from UniversalTime to ServerTime. 
		/// <summary>
		/// Converts UTC time to server-stored time.
		/// Returns the UTC value unchanged if bGMTStorage is true;
		/// otherwise converts to local time via DateTime.ToLocalTime().
		/// </summary>
		public DateTime ToServerTimeFromUniversalTime(DateTime dtUniversalTime)
		{
			if ( dtUniversalTime == DateTime.MinValue )
				return dtUniversalTime;
			// 03/25/2013 Paul.  Force the kind so that ToLocalTime() will work. 
			if ( dtUniversalTime.Kind != DateTimeKind.Utc )
				dtUniversalTime = DateTime.SpecifyKind(dtUniversalTime, DateTimeKind.Utc);
			// 11/07/2005 Paul.  SugarCRM 3.5 now stores time in GMT. 
			if ( m_bGMTStorage )
				return dtUniversalTime;
			else
				return dtUniversalTime.ToLocalTime();
		}

		// 08/17/2006 Paul.  SOAP needs a quick way to convert from ServerTime to UniversalTime. 
		/// <summary>
		/// Converts server-stored time to UTC.
		/// Returns the value unchanged if bGMTStorage is true;
		/// otherwise converts to UTC via DateTime.ToUniversalTime().
		/// </summary>
		public DateTime ToUniversalTimeFromServerTime(DateTime dtServerTime)
		{
			if ( dtServerTime == DateTime.MinValue )
				return dtServerTime;
			// 03/25/2013 Paul.  Force the kind so that ToUniversalTime() will work. 
			if ( dtServerTime.Kind != DateTimeKind.Local )
				dtServerTime = DateTime.SpecifyKind(dtServerTime, DateTimeKind.Local);
			// 11/07/2005 Paul.  SugarCRM 3.5 now stores time in GMT. 
			if ( m_bGMTStorage )
				return dtServerTime;
			else
				return dtServerTime.ToUniversalTime();
		}

		/// <summary>
		/// Converts UTC time (as string) to server-stored time.
		/// </summary>
		public DateTime ToServerTimeFromUniversalTime(string sUniversalTime)
		{
			DateTime dtUniversalTime = DateTime.Parse(sUniversalTime);
			// 03/25/2013 Paul.  Force the kind so that ToLocalTime() will work. 
			if ( dtUniversalTime.Kind != DateTimeKind.Utc )
				dtUniversalTime = DateTime.SpecifyKind(dtUniversalTime, DateTimeKind.Utc);
			return ToServerTimeFromUniversalTime(dtUniversalTime);
		}

		/// <summary>
		/// Converts a timezone-local DateTime to server-stored time.
		/// Converts to UTC first via ToUniversalTime(), then to local server time if not in GMT storage mode.
		/// </summary>
		public DateTime ToServerTime(DateTime dtZoneTime)
		{
			DateTime dtUniversalTime = ToUniversalTime(dtZoneTime);
			if ( dtUniversalTime == DateTime.MinValue )
				return dtUniversalTime;
			// 11/07/2005 Paul.  SugarCRM 3.5 now stores time in GMT. 
			if ( m_bGMTStorage )
				return dtUniversalTime;
			else
				return dtUniversalTime.ToLocalTime();
		}

		/// <summary>
		/// Converts a timezone-local time string to server-stored time.
		/// Returns DateTime.MinValue for empty or unparseable input.
		/// </summary>
		public DateTime ToServerTime(string sZoneTime)
		{
			if ( sZoneTime == String.Empty )
				return DateTime.MinValue;
			DateTime dtZoneTime = Sql.ToDateTime(sZoneTime);
			if ( dtZoneTime == DateTime.MinValue )
				return dtZoneTime ;
			DateTime dtUniversalTime = ToUniversalTime(dtZoneTime);
			if ( dtUniversalTime == DateTime.MinValue )
				return dtUniversalTime;
			// 11/07/2005 Paul.  SugarCRM 3.5 now stores time in GMT. 
			if ( m_bGMTStorage )
				return dtUniversalTime;
			else
				return dtUniversalTime.ToLocalTime();
		}

		/// <summary>
		/// Converts a timezone-local DateTime to UTC by applying bias and DST offset.
		/// The bias is added (removed from local perspective) to get UTC.
		/// </summary>
		public DateTime ToUniversalTime(DateTime dtZoneTime)
		{
			// 11/07/2005 Paul.  Don't modify if value is MinValue.
			if ( dtZoneTime == DateTime.MinValue )
				return dtZoneTime;
			DateTime dtUniversalTime = dtZoneTime;
			if ( IsDaylightSavings(dtZoneTime) )
			{
				dtUniversalTime = dtUniversalTime.AddMinutes(m_nDAYLIGHT_BIAS);
			}
			// When converting to Universal Time, the bias is removed after any daylight calculations. 
			dtUniversalTime = dtUniversalTime.AddMinutes(m_nBIAS);
			// 03/25/2013 Paul.  Force the kind so that ToLocalTime() will work.  
			// This was first detected when parsing Google Calendar entries. 
			if ( dtUniversalTime.Kind != DateTimeKind.Utc )
				dtUniversalTime = DateTime.SpecifyKind(dtUniversalTime, DateTimeKind.Utc);
			return dtUniversalTime;
		}

		/// <summary>
		/// Returns the timezone abbreviation for the given zone-local datetime.
		/// Returns the daylight abbreviation (e.g. "EDT") if the time is in DST, 
		/// otherwise returns the standard abbreviation (e.g. "EST").
		/// </summary>
		public string Abbreviation(DateTime dtZoneTime)
		{
			string sZone = String.Empty;
			if ( IsDaylightSavings(dtZoneTime) )
			{
				sZone = m_sDAYLIGHT_ABBREVIATION;
			}
			else
			{
				sZone = m_sSTANDARD_ABBREVIATION;
			}
			return sZone;
		}
	}
}
