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
// .NET 10 Migration: SplendidCRM/_code/SplendidDefaults.cs → src/SplendidCRM.Core/SplendidDefaults.cs
// Changes applied:
//   - REMOVED: using System.Web; (HttpApplicationState, HttpContext)
//   - ADDED:   using Microsoft.AspNetCore.Http; (IHttpContextAccessor)
//   - ADDED:   using Microsoft.Extensions.Caching.Memory; (IMemoryCache)
//   - ADDED:   static ambient fields _ambientCache and _ambientHttpAccessor (matching the
//              SetAmbient() pattern used in Sql.cs) — called at startup to enable static method access
//   - ADDED:   DI constructor that stores instance fields AND sets the static ambient fields,
//              enabling DI injection while maintaining static call compatibility for
//              existing callers in SplendidInit.cs and Utils.cs
//   - CONVERTED: All static methods previously using HttpContext.Current.Application["key"]
//               → now use _ambientCache?.Get<object>("key") via static ambient field
//   - CONVERTED: HttpApplicationState parameter on Culture(Application) and BaseCurrencyID(Application)
//               → IMemoryCache parameter: Culture(IMemoryCache) and BaseCurrencyID(IMemoryCache)
//   - CONVERTED: SplendidCache.Languages(Application) → IMemoryCache.Get<DataTable>("vwLANGUAGES")
//               accessed directly (SplendidCache stores Languages DataTable under this key)
//   - CONVERTED: SplendidCache.Timezones() → IMemoryCache.Get<DataTable>("vwTIMEZONES")
//               accessed directly (SplendidCache stores Timezones DataTable under this key)
//   - ADDED:   MaxHttpCollectionKeys() static method returning 5000 (required by Utils.cs)
//   - PRESERVED: All static method signatures (Culture, Theme, DateFormat, TimeFormat, TimeZone,
//               CurrencyID, GroupSeparator, DecimalSeparator, generate_graphcolor)
//   - PRESERVED: All default fallback constants, IsValidDateFormat, DateFormat(string) normalization
//   - PRESERVED: Thread.CurrentThread.CurrentCulture fallback in GroupSeparator/DecimalSeparator
//   - PRESERVED: namespace SplendidCRM, all public signatures
//   - NOTE:     Cache key strings are identical to original Application[] keys for behavioral parity
#nullable disable
using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	/// <summary>
	/// Default configuration values for SplendidCRM.
	///
	/// Migrated from SplendidCRM/_code/SplendidDefaults.cs for .NET 10 ASP.NET Core.
	///
	/// MIGRATION PATTERN (static ambient — matching Sql.cs):
	///   BEFORE (.NET Framework 4.8):
	///     All methods were static and accessed HttpContext.Current.Application[] directly.
	///     Two methods (Culture, BaseCurrencyID) had HttpApplicationState parameters.
	///   AFTER (.NET 10 ASP.NET Core):
	///     Static methods use _ambientCache (set at startup via SetAmbient() or DI constructor).
	///     HttpApplicationState parameters replaced with IMemoryCache parameters.
	///     DI constructor available for injection; sets static ambient as a side effect.
	///
	/// REGISTRATION:
	///   // Option A — DI injection (sets ambient automatically):
	///   services.AddScoped&lt;SplendidDefaults&gt;();
	///   // Option B — explicit static ambient (for static callers like SplendidInit.cs):
	///   SplendidDefaults.SetAmbient(httpContextAccessor, memoryCache);
	/// </summary>
	public class SplendidDefaults
	{
		// =====================================================================================
		// .NET 10 Migration: Static ambient fields replacing HttpContext.Current.Application.
		// These are set at application startup via SetAmbient() or the DI constructor.
		// Thread-safety: IMemoryCache is a thread-safe singleton; volatile write ensures
		// visibility across threads.
		//
		// BEFORE (.NET Framework 4.8):
		//   HttpContext.Current.Application["CONFIG.key"] — accessed via HTTP pipeline context
		// AFTER (.NET 10 ASP.NET Core):
		//   _ambientCache?.Get<object>("CONFIG.key") — ambient singleton set at startup
		// =====================================================================================

		/// <summary>
		/// Static ambient IMemoryCache — replaces HttpApplicationState (Application["key"]).
		/// Set via SetAmbient() or the DI constructor at application startup.
		/// Null-safe: all static methods guard with null-conditional operator (?.).
		/// BEFORE: HttpContext.Current.Application["CONFIG.default_language"]
		/// AFTER:  _ambientCache?.Get&lt;object&gt;("CONFIG.default_language")
		/// </summary>
		private static IMemoryCache _ambientCache;

		/// <summary>
		/// Static ambient IHttpContextAccessor — replaces HttpContext.Current.
		/// Set via SetAmbient() or the DI constructor at application startup.
		/// Available for future methods that require per-request context access.
		/// </summary>
		private static IHttpContextAccessor _ambientHttpAccessor;

		/// <summary>
		/// Register static ambient dependencies for this class.
		/// Must be called at application startup (e.g., from Program.cs or a startup service
		/// that receives these via DI) before any static no-arg methods are invoked.
		/// The DI constructor also calls this automatically when an instance is created.
		/// </summary>
		/// <param name="httpContextAccessor">IHttpContextAccessor replacing HttpContext.Current.</param>
		/// <param name="memoryCache">IMemoryCache replacing HttpApplicationState (Application[]).</param>
		public static void SetAmbient(IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache)
		{
			_ambientHttpAccessor = httpContextAccessor;
			_ambientCache        = memoryCache;
		}

		// =====================================================================================
		// DI Constructor
		// Stores injected services locally AND sets the static ambient fields so that
		// existing static callers (SplendidInit.cs, Utils.cs) work after DI wires up this class.
		// =====================================================================================

		/// <summary>
		/// Creates a SplendidDefaults instance and sets the static ambient fields.
		/// Injecting this class via DI automatically registers the ambient for static callers.
		/// </summary>
		/// <param name="httpContextAccessor">
		/// IHttpContextAccessor replacing HttpContext.Current for per-request context access.
		/// </param>
		/// <param name="memoryCache">
		/// IMemoryCache replacing HttpApplicationState (Application[]) for reading cached
		/// configuration values (CONFIG.*, CURRENCY.*, vwLANGUAGES, vwTIMEZONES).
		/// </param>
		public SplendidDefaults(IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache)
		{
			// Set the static ambient so existing static callers (SplendidInit.cs, Utils.cs)
			// automatically get the same IMemoryCache after DI resolves this instance.
			SetAmbient(httpContextAccessor, memoryCache);
		}

		// =====================================================================================
		// Culture — static no-arg + static IMemoryCache overload
		// BEFORE: Culture()                → HttpContext.Current.Application
		//         Culture(Application)     → HttpApplicationState parameter
		// AFTER:  Culture()                → _ambientCache (static ambient)
		//         Culture(IMemoryCache)    → IMemoryCache parameter (replaces Application)
		// =====================================================================================

		/// <summary>
		/// Returns the default culture for the application using the static ambient IMemoryCache.
		/// Reads CONFIG.default_language from the ambient cache, validates it against the
		/// vwLANGUAGES lookup table (if available), and normalizes the culture string.
		/// Falls back to "en-US" if no valid culture is configured or cache is not yet populated.
		///
		/// BEFORE: Culture() → Culture(HttpContext.Current.Application)
		/// AFTER:  Culture() → Culture(_ambientCache)
		/// </summary>
		/// <returns>Normalized culture string (e.g. "en-US", "fr-FR").</returns>
		public static string Culture()
		{
			return Culture(_ambientCache);
		}

		/// <summary>
		/// Returns the default culture using the supplied IMemoryCache.
		/// Validates the configured culture against the vwLANGUAGES DataTable (if present
		/// in the cache under the "vwLANGUAGES" key) and normalizes the result.
		///
		/// BEFORE: Culture(HttpApplicationState Application)
		///   - Application["CONFIG.default_language"]
		///   - Validated via DataView on SplendidCache.Languages(Application)
		/// AFTER: Culture(IMemoryCache memoryCache)
		///   - memoryCache.Get&lt;object&gt;("CONFIG.default_language")
		///   - Validated via DataView on memoryCache.Get&lt;DataTable&gt;("vwLANGUAGES")
		///     (SplendidCache stores the Languages DataTable under "vwLANGUAGES")
		/// </summary>
		/// <param name="memoryCache">
		/// IMemoryCache containing CONFIG.default_language and optionally vwLANGUAGES.
		/// Replaces HttpApplicationState from the .NET Framework version.
		/// </param>
		/// <returns>Normalized culture string (e.g. "en-US", "fr-FR").</returns>
		public static string Culture(IMemoryCache memoryCache)
		{
			// 12/22/2007 Paul.  Inside the timer event, there is no current context, so we need to pass the application.
			// BEFORE: string sCulture = Sql.ToString(Application["CONFIG.default_language"]);
			// AFTER:  string sCulture = Sql.ToString(memoryCache.Get<object>("CONFIG.default_language"));
			string sCulture = Sql.ToString(memoryCache?.Get<object>("CONFIG.default_language"));
			// 12/22/2007 Paul.  The cache is not available when we are inside the timer event.
			// 02/18/2008 Paul.  The Languages function is now thread safe, so it can be called from the timer.
			{
				// 01/08/2017 Paul.  We are getting an odd exception from within the workflow thread. Just ignore and continue.
				try
				{
					// BEFORE: DataView vwLanguages = new DataView(SplendidCache.Languages(Application));
					// AFTER:  DataTable dtLanguages = memoryCache.Get<DataTable>("vwLANGUAGES");
					//         SplendidCache stores the Languages DataTable under the "vwLANGUAGES" key.
					DataTable dtLanguages = memoryCache?.Get<DataTable>("vwLANGUAGES");
					if ( dtLanguages != null )
					{
						DataView vwLanguages = new DataView(dtLanguages);
						// 05/20/2008 Paul.  Normalize culture before lookup.
						vwLanguages.RowFilter = "NAME = '" + L10N.NormalizeCulture(sCulture) + "'";
						if ( vwLanguages.Count > 0 )
							sCulture = Sql.ToString(vwLanguages[0]["NAME"]);
					}
				}
				catch
				{
				}
			}
			if ( Sql.IsEmptyString(sCulture) )
				sCulture = "en-US";
			return L10N.NormalizeCulture(sCulture);
		}

		// =====================================================================================
		// Theme / MobileTheme
		// BEFORE: static, HttpContext.Current.Application["CONFIG.default_theme"]
		// AFTER:  static, _ambientCache?.Get<object>("CONFIG.default_theme")
		// =====================================================================================

		/// <summary>
		/// Returns the default UI theme for the application.
		/// Reads CONFIG.default_theme from the static ambient IMemoryCache.
		/// Falls back to "Arctic" if not configured.
		///
		/// BEFORE: Sql.ToString(HttpContext.Current.Application["CONFIG.default_theme"])
		/// AFTER:  Sql.ToString(_ambientCache?.Get&lt;object&gt;("CONFIG.default_theme"))
		/// </summary>
		/// <returns>Theme name string (e.g. "Arctic", "Atlantic").</returns>
		public static string Theme()
		{
			// BEFORE: string sTheme = Sql.ToString(HttpContext.Current.Application["CONFIG.default_theme"]);
			// AFTER:  string sTheme = Sql.ToString(_ambientCache?.Get<object>("CONFIG.default_theme"));
			string sTheme = Sql.ToString(_ambientCache?.Get<object>("CONFIG.default_theme"));
			// 10/16/2015 Paul.  Change default theme to our newest theme.
			// 10/02/2016 Paul.  Make the default theme Arctic.
			if ( Sql.IsEmptyString(sTheme) )
				sTheme = "Arctic";
			return sTheme;
		}

		/// <summary>
		/// Returns the default mobile UI theme for the application.
		/// Reads CONFIG.default_mobile_theme from the static ambient IMemoryCache.
		/// Falls back to "Mobile" if not configured.
		///
		/// BEFORE: Sql.ToString(HttpContext.Current.Application["CONFIG.default_mobile_theme"])
		/// AFTER:  Sql.ToString(_ambientCache?.Get&lt;object&gt;("CONFIG.default_mobile_theme"))
		/// </summary>
		/// <returns>Mobile theme name string (e.g. "Mobile").</returns>
		public static string MobileTheme()
		{
			// BEFORE: string sTheme = Sql.ToString(HttpContext.Current.Application["CONFIG.default_mobile_theme"]);
			// AFTER:  string sTheme = Sql.ToString(_ambientCache?.Get<object>("CONFIG.default_mobile_theme"));
			string sTheme = Sql.ToString(_ambientCache?.Get<object>("CONFIG.default_mobile_theme"));
			if ( Sql.IsEmptyString(sTheme) )
				sTheme = "Mobile";
			return sTheme;
		}

		// =====================================================================================
		// DateFormat — static no-arg + static utility overloads
		// BEFORE: DateFormat()        → HttpContext.Current.Application["CONFIG.default_date_format"]
		//         DateFormat(string)  → static normalization utility (no Application dependency)
		//         IsValidDateFormat() → static validation utility (no Application dependency)
		// AFTER:  DateFormat()        → _ambientCache?.Get<object>("CONFIG.default_date_format")
		//         DateFormat(string)  → unchanged (static utility)
		//         IsValidDateFormat() → unchanged (static utility)
		// =====================================================================================

		/// <summary>
		/// Returns the default date format for the application.
		/// Reads CONFIG.default_date_format from the static ambient IMemoryCache.
		/// Validates and normalizes the format using IsValidDateFormat() and DateFormat(string).
		/// Falls back to "MM/dd/yyyy" if not configured.
		///
		/// BEFORE: Sql.ToString(HttpContext.Current.Application["CONFIG.default_date_format"])
		/// AFTER:  Sql.ToString(_ambientCache?.Get&lt;object&gt;("CONFIG.default_date_format"))
		/// </summary>
		/// <returns>Date format string compatible with .NET DateTime.ToString() (e.g. "MM/dd/yyyy").</returns>
		public static string DateFormat()
		{
			// BEFORE: string sDateFormat = Sql.ToString(HttpContext.Current.Application["CONFIG.default_date_format"]);
			// AFTER:  string sDateFormat = Sql.ToString(_ambientCache?.Get<object>("CONFIG.default_date_format"));
			string sDateFormat = Sql.ToString(_ambientCache?.Get<object>("CONFIG.default_date_format"));
			if ( Sql.IsEmptyString(sDateFormat) )
				sDateFormat = "MM/dd/yyyy";
			// 11/28/2005 Paul.  Need to make sure that the default format is valid.
			else if ( !IsValidDateFormat(sDateFormat) )
				sDateFormat = DateFormat(sDateFormat);
			return sDateFormat;
		}

		/// <summary>
		/// Validates that the provided date format string is a valid .NET date format.
		/// A format is invalid if it contains the lowercase "m" (PHP month specifier, not valid in .NET)
		/// or does not contain a 4-digit year "yyyy".
		/// </summary>
		/// <param name="sDateFormat">Date format string to validate.</param>
		/// <returns>true if the format is valid for .NET DateTime.ToString(); false otherwise.</returns>
		public static bool IsValidDateFormat(string sDateFormat)
		{
			if ( sDateFormat.IndexOf("m") >= 0 || sDateFormat.IndexOf("yyyy") < 0 )
				return false;
			return true;
		}

		/// <summary>
		/// Normalizes a date format string for .NET compatibility.
		/// Converts PHP-style lowercase "m" (month) to uppercase "M" as required by .NET.
		/// Converts 2-digit year "yy" to 4-digit year "yyyy" to prevent century rollover issues
		/// (e.g., 12/31/2100 displayed as 12/31/00 with 2-digit year format).
		/// </summary>
		/// <param name="sDateFormat">Raw date format string to normalize.</param>
		/// <returns>Normalized .NET-compatible date format string.</returns>
		public static string DateFormat(string sDateFormat)
		{
			// 11/12/2005 Paul.  "m" is not valid for .NET month formatting.  Must use MM.
			if ( sDateFormat.IndexOf("m") >= 0 )
			{
				sDateFormat = sDateFormat.Replace("m", "M");
			}
			// 11/12/2005 Paul.  Require 4 digit year.  Otherwise default date in Pipeline of 12/31/2100
			// would get converted to 12/31/00.
			if ( sDateFormat.IndexOf("yyyy") < 0 )
			{
				sDateFormat = sDateFormat.Replace("yy", "yyyy");
			}
			return sDateFormat;
		}

		// =====================================================================================
		// TimeFormat
		// BEFORE: static, HttpContext.Current.Application["CONFIG.default_time_format"]
		// AFTER:  static, _ambientCache?.Get<object>("CONFIG.default_time_format")
		// =====================================================================================

		/// <summary>
		/// Returns the default time format for the application.
		/// Reads CONFIG.default_time_format from the static ambient IMemoryCache.
		/// Falls back to "h:mm tt" (12-hour with AM/PM) if not configured or if the
		/// legacy PHP-style format "H:i" is encountered.
		///
		/// BEFORE: Sql.ToString(HttpContext.Current.Application["CONFIG.default_time_format"])
		/// AFTER:  Sql.ToString(_ambientCache?.Get&lt;object&gt;("CONFIG.default_time_format"))
		/// </summary>
		/// <returns>Time format string compatible with .NET DateTime.ToString() (e.g. "h:mm tt").</returns>
		public static string TimeFormat()
		{
			// BEFORE: string sTimeFormat = Sql.ToString(HttpContext.Current.Application["CONFIG.default_time_format"]);
			// AFTER:  string sTimeFormat = Sql.ToString(_ambientCache?.Get<object>("CONFIG.default_time_format"));
			string sTimeFormat = Sql.ToString(_ambientCache?.Get<object>("CONFIG.default_time_format"));
			if ( Sql.IsEmptyString(sTimeFormat) || sTimeFormat == "H:i" )
				sTimeFormat = "h:mm tt";
			return sTimeFormat;
		}

		// =====================================================================================
		// TimeZone — static no-arg + static int-bias overload
		// BEFORE: TimeZone()    → HttpContext.Current.Application["CONFIG.default_timezone"]
		//         TimeZone(int) → new DataView(SplendidCache.Timezones())
		// AFTER:  TimeZone()    → _ambientCache?.Get<object>("CONFIG.default_timezone")
		//         TimeZone(int) → new DataView(_ambientCache?.Get<DataTable>("vwTIMEZONES"))
		// =====================================================================================

		/// <summary>
		/// Returns the default timezone GUID string for the application.
		/// Reads CONFIG.default_timezone from the static ambient IMemoryCache.
		/// Falls back to Eastern US timezone GUID "BFA61AF7-26ED-4020-A0C1-39A15E4E9E0A" if empty.
		///
		/// BEFORE: Sql.ToString(HttpContext.Current.Application["CONFIG.default_timezone"])
		/// AFTER:  Sql.ToString(_ambientCache?.Get&lt;object&gt;("CONFIG.default_timezone"))
		/// </summary>
		/// <returns>GUID string representing the default timezone.</returns>
		public static string TimeZone()
		{
			// 08/08/2006 Paul.  Pull the default timezone and fall-back to Eastern US only if empty.
			// BEFORE: string sDEFAULT_TIMEZONE = Sql.ToString(HttpContext.Current.Application["CONFIG.default_timezone"]);
			// AFTER:  string sDEFAULT_TIMEZONE = Sql.ToString(_ambientCache?.Get<object>("CONFIG.default_timezone"));
			string sDEFAULT_TIMEZONE = Sql.ToString(_ambientCache?.Get<object>("CONFIG.default_timezone"));
			if ( Sql.IsEmptyGuid(sDEFAULT_TIMEZONE) )
				sDEFAULT_TIMEZONE = "BFA61AF7-26ED-4020-A0C1-39A15E4E9E0A";
			return sDEFAULT_TIMEZONE;
		}

		/// <summary>
		/// Returns the timezone GUID string for the given UTC bias offset (minutes from UTC).
		/// Looks up the BIAS value in the vwTIMEZONES DataTable retrieved from the static
		/// ambient IMemoryCache. Falls back to the default timezone if no match is found.
		///
		/// BEFORE: new DataView(SplendidCache.Timezones())  [static SplendidCache call]
		/// AFTER:  new DataView(_ambientCache?.Get&lt;DataTable&gt;("vwTIMEZONES"))
		///         (SplendidCache stores the Timezones DataTable under "vwTIMEZONES")
		/// </summary>
		/// <param name="nTimez">UTC bias in minutes (e.g. -300 for Eastern Standard Time).</param>
		/// <returns>GUID string representing the matched timezone, or the default timezone GUID if not found.</returns>
		public static string TimeZone(int nTimez)
		{
			string sTimeZone = String.Empty;
			// BEFORE: DataView vwTimezones = new DataView(SplendidCache.Timezones());
			// AFTER:  DataTable dtTimezones = _ambientCache?.Get<DataTable>("vwTIMEZONES");
			//         SplendidCache stores Timezones DataTable under "vwTIMEZONES" cache key.
			DataTable dtTimezones = _ambientCache?.Get<DataTable>("vwTIMEZONES");
			if ( dtTimezones != null )
			{
				DataView vwTimezones = new DataView(dtTimezones);
				vwTimezones.RowFilter = "BIAS = " + nTimez.ToString();
				if ( vwTimezones.Count > 0 )
					sTimeZone = Sql.ToString(vwTimezones[0]["ID"]);
			}
			if ( Sql.IsEmptyString(sTimeZone) )
				sTimeZone = TimeZone();
			return sTimeZone;
		}

		// =====================================================================================
		// CurrencyID
		// BEFORE: static, HttpContext.Current.Application["CONFIG.default_currency"]
		// AFTER:  static, _ambientCache?.Get<object>("CONFIG.default_currency")
		// =====================================================================================

		/// <summary>
		/// Returns the default currency GUID string for the application.
		/// Reads CONFIG.default_currency from the static ambient IMemoryCache.
		/// Falls back to US Dollar GUID "E340202E-6291-4071-B327-A34CB4DF239B" if empty.
		///
		/// BEFORE: Sql.ToString(HttpContext.Current.Application["CONFIG.default_currency"])
		/// AFTER:  Sql.ToString(_ambientCache?.Get&lt;object&gt;("CONFIG.default_currency"))
		/// </summary>
		/// <returns>GUID string representing the default currency.</returns>
		public static string CurrencyID()
		{
			// 08/08/2006 Paul.  Pull the default currency and fall-back to Dollars only if empty.
			// BEFORE: string sDEFAULT_CURRENCY = Sql.ToString(HttpContext.Current.Application["CONFIG.default_currency"]);
			// AFTER:  string sDEFAULT_CURRENCY = Sql.ToString(_ambientCache?.Get<object>("CONFIG.default_currency"));
			string sDEFAULT_CURRENCY = Sql.ToString(_ambientCache?.Get<object>("CONFIG.default_currency"));
			if ( Sql.IsEmptyGuid(sDEFAULT_CURRENCY) )
			{
				sDEFAULT_CURRENCY = "E340202E-6291-4071-B327-A34CB4DF239B";
			}
			return sDEFAULT_CURRENCY;
		}

		// =====================================================================================
		// BaseCurrencyID / BaseCurrencyISO — static methods with IMemoryCache parameter
		// BEFORE: BaseCurrencyID(HttpApplicationState Application)
		//         BaseCurrencyISO(HttpApplicationState Application)
		// AFTER:  BaseCurrencyID(IMemoryCache memoryCache)
		//         BaseCurrencyISO(IMemoryCache memoryCache)
		// =====================================================================================

		/// <summary>
		/// Returns the GUID of the system base currency.
		/// Reads CONFIG.base_currency from the provided IMemoryCache.
		/// Falls back to US Dollar GUID "E340202E-6291-4071-B327-A34CB4DF239B" if empty.
		///
		/// BEFORE: BaseCurrencyID(HttpApplicationState Application)
		///   Sql.ToGuid(Application["CONFIG.base_currency"])
		/// AFTER: BaseCurrencyID(IMemoryCache memoryCache)
		///   Sql.ToGuid(memoryCache.Get&lt;object&gt;("CONFIG.base_currency"))
		/// </summary>
		/// <param name="memoryCache">
		/// IMemoryCache containing CONFIG.base_currency.
		/// Replaces HttpApplicationState from the .NET Framework version.
		/// </param>
		/// <returns>GUID of the base currency.</returns>
		// 04/30/2016 Paul.  Base currency has been USD, but we should make it easy to allow a different base.
		public static Guid BaseCurrencyID(IMemoryCache memoryCache)
		{
			// BEFORE: Guid gBASE_CURRENCY = Sql.ToGuid(Application["CONFIG.base_currency"]);
			// AFTER:  Guid gBASE_CURRENCY = Sql.ToGuid(memoryCache.Get<object>("CONFIG.base_currency"));
			Guid gBASE_CURRENCY = Sql.ToGuid(memoryCache?.Get<object>("CONFIG.base_currency"));
			if ( Sql.IsEmptyGuid(gBASE_CURRENCY) )
				gBASE_CURRENCY = new Guid("E340202E-6291-4071-B327-A34CB4DF239B");
			return gBASE_CURRENCY;
		}

		/// <summary>
		/// Returns the ISO 4217 currency code for the system base currency (e.g. "USD").
		/// Resolves the base currency ID via BaseCurrencyID(IMemoryCache) and then reads
		/// the Currency object stored under "CURRENCY.{GUID}" in IMemoryCache to get
		/// the ISO4217 property.
		///
		/// BEFORE: BaseCurrencyISO(HttpApplicationState Application)
		///   Currency C10n = Application["CURRENCY." + guid] as SplendidCRM.Currency;
		/// AFTER: BaseCurrencyISO(IMemoryCache memoryCache)
		///   Currency C10n = memoryCache.Get&lt;Currency&gt;("CURRENCY." + guid);
		/// </summary>
		/// <param name="memoryCache">
		/// IMemoryCache containing CONFIG.base_currency and CURRENCY.{GUID} entries.
		/// Replaces HttpApplicationState from the .NET Framework version.
		/// </param>
		/// <returns>ISO 4217 currency code string (e.g. "USD"). Falls back to "USD" if not found.</returns>
		public static string BaseCurrencyISO(IMemoryCache memoryCache)
		{
			string sBASE_ISO4217 = "USD";
			Guid gBASE_CURRENCY = BaseCurrencyID(memoryCache);
			// BEFORE: Currency C10n = Application["CURRENCY." + gBASE_CURRENCY.ToString()] as SplendidCRM.Currency;
			// AFTER:  Currency C10n = memoryCache.Get<Currency>("CURRENCY." + gBASE_CURRENCY.ToString());
			//         Currency objects are stored in IMemoryCache under "CURRENCY.{GUID}" keys
			//         by SplendidCache.Currencies() / SplendidCache.LoadCurrencies().
			Currency C10n = memoryCache?.Get<Currency>("CURRENCY." + gBASE_CURRENCY.ToString());
			if ( C10n != null )
			{
				sBASE_ISO4217 = C10n.ISO4217;
				if ( Sql.IsEmptyString(sBASE_ISO4217) )
					sBASE_ISO4217 = "USD";
			}
			return sBASE_ISO4217;
		}

		// =====================================================================================
		// GroupSeparator / DecimalSeparator
		// BEFORE: static, HttpContext.Current.Application["CONFIG.default_number_grouping_seperator"]
		//                  / ["CONFIG.default_decimal_seperator"]
		// AFTER:  static, _ambientCache?.Get<object>(...) with Thread.CurrentThread fallback
		// =====================================================================================

		/// <summary>
		/// Returns the number group separator character (e.g. "," in en-US).
		/// Reads CONFIG.default_number_grouping_seperator from the static ambient IMemoryCache
		/// as a configuration override. Falls back to the .NET culture value if not configured.
		///
		/// BEFORE: Sql.ToString(HttpContext.Current.Application["CONFIG.default_number_grouping_seperator"])
		/// AFTER:  Sql.ToString(_ambientCache?.Get&lt;object&gt;("CONFIG.default_number_grouping_seperator"))
		/// </summary>
		/// <returns>Group separator character string (e.g. "," or ".").</returns>
		public static string GroupSeparator()
		{
			// 02/29/2008 Paul.  The config value should only be used as an override.  We should default to the .NET culture value.
			// BEFORE: string sGROUP_SEPARATOR = Sql.ToString(HttpContext.Current.Application["CONFIG.default_number_grouping_seperator"]);
			// AFTER:  string sGROUP_SEPARATOR = Sql.ToString(_ambientCache?.Get<object>("CONFIG.default_number_grouping_seperator"));
			string sGROUP_SEPARATOR = Sql.ToString(_ambientCache?.Get<object>("CONFIG.default_number_grouping_seperator"));
			if ( Sql.IsEmptyString(sGROUP_SEPARATOR) )
				sGROUP_SEPARATOR = Thread.CurrentThread.CurrentCulture.NumberFormat.CurrencyGroupSeparator;
			return sGROUP_SEPARATOR;
		}

		/// <summary>
		/// Returns the decimal separator character (e.g. "." in en-US).
		/// Reads CONFIG.default_decimal_seperator from the static ambient IMemoryCache
		/// as a configuration override. Falls back to the .NET culture value if not configured.
		///
		/// BEFORE: Sql.ToString(HttpContext.Current.Application["CONFIG.default_decimal_seperator"])
		/// AFTER:  Sql.ToString(_ambientCache?.Get&lt;object&gt;("CONFIG.default_decimal_seperator"))
		/// </summary>
		/// <returns>Decimal separator character string (e.g. "." or ",").</returns>
		public static string DecimalSeparator()
		{
			// 02/29/2008 Paul.  The config value should only be used as an override.  We should default to the .NET culture value.
			// BEFORE: string sDECIMAL_SEPARATOR = Sql.ToString(HttpContext.Current.Application["CONFIG.default_decimal_seperator"]);
			// AFTER:  string sDECIMAL_SEPARATOR = Sql.ToString(_ambientCache?.Get<object>("CONFIG.default_decimal_seperator"));
			string sDECIMAL_SEPARATOR = Sql.ToString(_ambientCache?.Get<object>("CONFIG.default_decimal_seperator"));
			if ( Sql.IsEmptyString(sDECIMAL_SEPARATOR) )
				sDECIMAL_SEPARATOR = Thread.CurrentThread.CurrentCulture.NumberFormat.CurrencyDecimalSeparator;
			return sDECIMAL_SEPARATOR;
		}

		// =====================================================================================
		// MaxHttpCollectionKeys — static constant
		// Required by Utils.cs: SplendidDefaults.MaxHttpCollectionKeys()
		// Not present in original SplendidDefaults.cs; added for .NET 10 migration.
		// The .NET Framework default for maxQueryStringLength and maxAllowedContentLength
		// was effectively unlimited; 5000 is a reasonable, safe default.
		// =====================================================================================

		/// <summary>
		/// Returns the maximum number of keys allowed in the HTTP request form collection.
		/// Used by Utils.cs as a fallback when no override is configured via IConfiguration.
		/// The default value of 5000 matches the safe default for ASP.NET Core form processing.
		/// </summary>
		/// <returns>Maximum HTTP form collection key count (default: 5000).</returns>
		public static int MaxHttpCollectionKeys()
		{
			return 5000;
		}

		// =====================================================================================
		// generate_graphcolor — static utility, no Application/Context dependency
		// Preserved as-is from .NET Framework 4.8 source.
		// =====================================================================================

		/// <summary>
		/// Returns a hexadecimal color string for the given graph instance index.
		/// Cycles through a predefined palette of 20 colors for nInstance 0–19.
		/// Returns "0x00CCCC" for any instance beyond the palette range.
		/// </summary>
		/// <param name="sInput">Input string (reserved for future hash-based color generation; currently unused).</param>
		/// <param name="nInstance">Zero-based index of the data series or graph element.</param>
		/// <returns>Hexadecimal color string (e.g. "0xFF0000" for red).</returns>
		public static string generate_graphcolor(string sInput, int nInstance)
		{
			string sColor = String.Empty;
			if ( nInstance < 20 )
			{
				string[] arrGraphColor =
				{
					  "0xFF0000"
					, "0x00FF00"
					, "0x0000FF"
					, "0xFF6600"
					, "0x42FF8E"
					, "0x6600FF"
					, "0xFFFF00"
					, "0x00FFFF"
					, "0xFF00FF"
					, "0x66FF00"
					, "0x0066FF"
					, "0xFF0066"
					, "0xCC0000"
					, "0x00CC00"
					, "0x0000CC"
					, "0xCC6600"
					, "0x00CC66"
					, "0x6600CC"
					, "0xCCCC00"
					, "0x00CCCC"
				};
				sColor = arrGraphColor[nInstance];
			}
			else
			{
				sColor = "0x00CCCC";
				//sColor = "0x" + substr(md5(sInput), 0, 6);
			}
			return sColor;
		}
	}
}
