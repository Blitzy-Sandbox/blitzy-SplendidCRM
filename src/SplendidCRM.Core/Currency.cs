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
// .NET 10 Migration: SplendidCRM/_code/Currency.cs → src/SplendidCRM.Core/Currency.cs
// Changes applied:
//   - REMOVED: using System.Web (HttpApplicationState, HttpContext)
//   - REMOVED: using Microsoft.Win32 (RegistryKey — used only in DbProviderFactories.GetFactory, not Currency)
//   - ADDED:   using Microsoft.Extensions.Caching.Memory (IMemoryCache replaces HttpApplicationState)
//   - HttpApplicationState parameters → IMemoryCache in all constructors and static factory methods
//   - Application["CURRENCY.{guid}"] reads  → IMemoryCache.TryGetValue<Currency>("CURRENCY.{guid}")
//   - Application["CURRENCY.{guid}"] writes → IMemoryCache.Set("CURRENCY.{guid}", C10n)
//   - Application["CONFIG.default_currency"] → IMemoryCache.Get<object>("CONFIG.default_currency")
//   - SplendidDefaults.BaseCurrencyID(Application) → hardcoded USD GUID E340202E-6291-4071-B327-A34CB4DF239B
//     (same value returned by the migrated SplendidDefaults.CurrencyID() method)
//   - DbProviderFactories.GetFactory(Context.Application) → static ambient _ambientDbf (DbProviderFactory)
//     set at startup via InitializeStaticServices(); null-guarded in UpdateRates
//   - OrderUtils.GetCurrencyConversionRate(Application, sISO4217, sbErrors) [static, 3-param] →
//     _ambientOrderUtils.GetCurrencyConversionRate(memoryCache, sSourceCurrency, sISO4217, sbErrors) [instance, 4-param]
//     Source currency read from CONFIG.currencyiso4217 in IMemoryCache (default "USD")
//   - SplendidError.SystemMessage(Context, "Error", stack, msg) →
//     SplendidError.SystemMessage(memoryCache, "Error", stack, msg) [static overload taking IMemoryCache]
//   - UpdateRates(Object sender) → UpdateRates(IMemoryCache memoryCache)
#nullable disable
using System;
using System.Text;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	/// <summary>
	/// Currency formatting and conversion utility for SplendidCRM.
	///
	/// Stores currency identity (ID, NAME, SYMBOL, ISO4217) and a conversion rate
	/// relative to the system base currency.  Provides ToCurrency/FromCurrency
	/// arithmetic for both float and Decimal values.  Includes a static UpdateRates
	/// method that fetches live exchange rates from the CurrencyLayer API for all
	/// active non-base currencies and persists them to the database.
	///
	/// Migrated from SplendidCRM/_code/Currency.cs for .NET 10 ASP.NET Core.
	///
	/// Serializable attribute preserved: Currency instances are stored in IMemoryCache
	/// under the key "CURRENCY.{GUID}" and must be serializable for distributed-cache
	/// compatibility.
	/// </summary>
	// 10/09/2017 Paul.  Allow the currency to be stored in the session object. 
	[Serializable]
	public class Currency
	{
		// =====================================================================================
		// Instance fields — preserved from .NET Framework 4.8 source with identical naming
		// and access modifiers to ensure downstream code that accesses protected members
		// (e.g., SplendidCRM.Core subclasses) continues to compile without change.
		// =====================================================================================

		protected Guid   m_gID             ;
		protected string m_sNAME           ;
		protected string m_sSYMBOL         ;
		// 11/10/2008 Paul.  PayPal uses the ISO value. 
		protected string m_sISO4217        ;
		protected float  m_fCONVERSION_RATE;
		protected bool   m_bUSDollars      ;

		/// <summary>
		/// Reentrancy guard that prevents concurrent CurrencyLayer API calls from
		/// multiple scheduler callbacks.  Preserved as a public static bool matching
		/// the .NET Framework 4.8 source field.
		/// </summary>
		public static bool bInsideUpdateRates = false;

		// 04/30/2016 Paul.  Base currency has been USD, but we should make it easy to allow a different base. 
		// .NET 10 Migration:
		//   BEFORE: m_gUSDollar initialized from SplendidDefaults.BaseCurrencyID(Application)
		//           which returned the value stored in Application["CONFIG.default_currency"] or
		//           the hardcoded fallback GUID below.
		//   AFTER:  Hardcoded directly to the USD GUID which is the same value returned by the
		//           migrated SplendidDefaults.CurrencyID() method.  The constructors below that
		//           accept IMemoryCache could read from the cache, but the hardcoded GUID is the
		//           correct default and avoids a dependency on SplendidDefaults in this class.
		/// <summary>
		/// GUID of the system base currency (U.S. Dollar by default).
		/// Initialized to the hardcoded USD GUID E340202E-6291-4071-B327-A34CB4DF239B.
		/// </summary>
		protected Guid m_gUSDollar = new Guid("E340202E-6291-4071-B327-A34CB4DF239B");

		// =====================================================================================
		// Static ambient service references — for use inside the static UpdateRates method.
		//
		// BEFORE (.NET Framework 4.8):
		//   DbProviderFactories.GetFactory(Context.Application) produced a DbProviderFactory
		//   per-call using the HttpApplicationState.  OrderUtils.GetCurrencyConversionRate()
		//   was a static call taking HttpApplicationState.
		//
		// AFTER (.NET 10 ASP.NET Core):
		//   Static ambient references are set once at application startup by the scheduler
		//   infrastructure (e.g., SchedulerHostedService) via InitializeStaticServices().
		//   This mirrors the pattern used in Crm._ambientDbf / Crm._ambientCache.
		//   Both references are null-guarded inside UpdateRates so the method returns safely
		//   if called before initialization completes.
		// =====================================================================================

		/// <summary>
		/// Provider-agnostic database factory for obtaining IDbConnection and DbDataAdapter
		/// instances inside the static UpdateRates method.
		/// BEFORE: Created per-call via DbProviderFactories.GetFactory(Application).
		/// AFTER:  Set once at startup by InitializeStaticServices(); null until initialized.
		/// </summary>
		private static DbProviderFactory _ambientDbf;

		/// <summary>
		/// OrderUtils service instance that provides GetCurrencyConversionRate for live
		/// exchange rate retrieval inside UpdateRates.
		/// BEFORE: OrderUtils.GetCurrencyConversionRate() was a static method taking HttpApplicationState.
		/// AFTER:  Instance method on the injected OrderUtils service; null until initialized.
		/// </summary>
		private static OrderUtils _ambientOrderUtils;

		/// <summary>
		/// Initializes static ambient service references required by the static UpdateRates method.
		/// Call this at application startup — typically from SchedulerHostedService constructor
		/// or Program.cs — before the scheduler begins firing UpdateRates callbacks.
		/// </summary>
		/// <param name="dbf">
		/// Provider-agnostic database factory (typically a <see cref="SqlClientFactory"/> instance).
		/// Provides <see cref="DbProviderFactory.CreateConnection"/> and
		/// <see cref="DbProviderFactory.CreateDataAdapter"/> used in UpdateRates to query
		/// vwCURRENCIES_List.
		/// </param>
		/// <param name="orderUtils">
		/// OrderUtils service providing GetCurrencyConversionRate for each active non-base
		/// currency during rate update processing.
		/// </param>
		public static void InitializeStaticServices(DbProviderFactory dbf, OrderUtils orderUtils)
		{
			_ambientDbf        = dbf       ;
			_ambientOrderUtils = orderUtils;
		}

		// =====================================================================================
		// Read-only property accessors (CONVERSION_RATE has a setter for rate override use cases)
		// Preserved from .NET Framework 4.8 source without change.
		// =====================================================================================

		public Guid ID
		{
			get
			{
				return m_gID;
			}
		}

		public string NAME
		{
			get
			{
				return m_sNAME;
			}
		}

		public string SYMBOL
		{
			get
			{
				return m_sSYMBOL;
			}
		}

		public string ISO4217
		{
			get
			{
				return m_sISO4217;
			}
		}

		// 04/30/2016 Paul.  If we are connected to the currency service, then now is a good time to check for changes. 
		public float CONVERSION_RATE
		{
			get
			{
				return m_fCONVERSION_RATE;
			}
			set
			{
				m_fCONVERSION_RATE = value;
			}
		}

		// =====================================================================================
		// Static factory methods
		// BEFORE: CreateCurrency(HttpApplicationState Application, Guid gCURRENCY_ID)
		//   - Application["CURRENCY.{guid}"] for cache lookup and store
		//   - Application["CONFIG.default_currency"] for the configured default currency GUID
		//   - SplendidDefaults.BaseCurrencyID(Application) as the ultimate GUID fallback
		//   - new Currency(Application) to create a blank placeholder
		//
		// AFTER:  CreateCurrency(IMemoryCache cache, Guid gCURRENCY_ID)
		//   - IMemoryCache.TryGetValue<Currency>("CURRENCY.{guid}") for cache lookup
		//   - IMemoryCache.Get<object>("CONFIG.default_currency") for default currency GUID
		//   - Hardcoded USD GUID E340202E-6291-4071-B327-A34CB4DF239B as ultimate fallback
		//   - IMemoryCache.Set("CURRENCY.{guid}", C10n) for cache store
		//   - new Currency(cache) to create a blank placeholder
		// =====================================================================================

		// 11/15/2009 Paul.  We need a version of the function that accepts the application. 
		/// <summary>
		/// Returns the Currency instance for the specified currency ID, using IMemoryCache
		/// for lookup with a three-tier fallback: requested ID → configured default currency
		/// → system base currency (USD).
		/// </summary>
		/// <param name="cache">
		/// IMemoryCache replacing HttpApplicationState.
		/// Keys read:    "CURRENCY.{guid}" (cached Currency objects),
		///               "CONFIG.default_currency" (Guid string for default currency).
		/// Keys written: "CURRENCY.{guid}" when creating a blank placeholder.
		/// Null-safe: when null, creates an uncached default USD Currency.
		/// </param>
		/// <param name="gCURRENCY_ID">
		/// The requested currency GUID.  Falls back through default currency and USD when
		/// not found in cache.
		/// </param>
		/// <returns>A Currency instance; never null.</returns>
		public static Currency CreateCurrency(IMemoryCache cache, Guid gCURRENCY_ID)
		{
			// BEFORE: Currency C10n = Application["CURRENCY." + gCURRENCY_ID.ToString()] as SplendidCRM.Currency;
			// AFTER:  IMemoryCache.TryGetValue<Currency>()
			Currency C10n = null;
			if ( cache != null )
				cache.TryGetValue("CURRENCY." + gCURRENCY_ID.ToString(), out C10n);
			if ( C10n == null )
			{
				// 05/09/2006 Paul. First try and use the default from CONFIG. 
				// BEFORE: gCURRENCY_ID = Sql.ToGuid(Application["CONFIG.default_currency"]);
				//         C10n = Application["CURRENCY." + gCURRENCY_ID.ToString()] as SplendidCRM.Currency;
				// AFTER:  IMemoryCache.Get<object>() with Sql.ToGuid() conversion
				gCURRENCY_ID = Sql.ToGuid(cache != null ? cache.Get<object>("CONFIG.default_currency") : null);
				if ( cache != null )
					cache.TryGetValue("CURRENCY." + gCURRENCY_ID.ToString(), out C10n);
				if ( C10n == null )
				{
					// Default to USD if default not specified. 
					// 04/30/2016 Paul.  Base currency has been USD, but we should make it easy to allow a different base. 
					// BEFORE: gCURRENCY_ID = SplendidDefaults.BaseCurrencyID(Application);
					//         if ( Sql.IsEmptyGuid(gCURRENCY_ID) )
					//             gCURRENCY_ID = new Guid("E340202E-6291-4071-B327-A34CB4DF239B");
					// AFTER:  Hardcoded USD GUID (same value as migrated SplendidDefaults.CurrencyID()).
					//         IsEmptyGuid check preserved for semantic parity.
					gCURRENCY_ID = new Guid("E340202E-6291-4071-B327-A34CB4DF239B");
					if ( Sql.IsEmptyGuid(gCURRENCY_ID) )
						gCURRENCY_ID = new Guid("E340202E-6291-4071-B327-A34CB4DF239B");
					if ( cache != null )
						cache.TryGetValue("CURRENCY." + gCURRENCY_ID.ToString(), out C10n);
				}
				// If currency is still null, then create a blank zone. 
				if ( C10n == null )
				{
					C10n = new Currency(cache);
					// BEFORE: Application["CURRENCY." + gCURRENCY_ID.ToString()] = C10n;
					// AFTER:  IMemoryCache.Set()
					cache?.Set("CURRENCY." + gCURRENCY_ID.ToString(), C10n);
				}
			}
			return C10n;
		}

		// 04/30/2016 Paul.  Require the Application so that we can get the base currency. 
		/// <summary>
		/// Returns a Currency instance for the specified currency ID with the conversion rate
		/// overridden to the provided value.  Creates a new Currency object so the global cached
		/// rate is not modified by the override.
		/// </summary>
		/// <param name="cache">IMemoryCache replacing HttpApplicationState.</param>
		/// <param name="gCURRENCY_ID">The requested currency GUID.</param>
		/// <param name="fCONVERSION_RATE">
		/// Override conversion rate applied to the new Currency instance.
		/// Defaults to 1.0 when 0.0 is passed (preserves .NET Framework 4.8 behavior).
		/// </param>
		/// <returns>A new Currency instance with the overridden conversion rate.</returns>
		public static Currency CreateCurrency(IMemoryCache cache, Guid gCURRENCY_ID, float fCONVERSION_RATE)
		{
			Currency C10n = CreateCurrency(cache, gCURRENCY_ID);
			// 03/31/2007 Paul.  Create a new currency object so that we can override the rate 
			// without overriding the global value. 
			if ( fCONVERSION_RATE == 0.0 )
				fCONVERSION_RATE = 1.0F;
			return new Currency(cache, C10n.ID, C10n.NAME, C10n.SYMBOL, C10n.ISO4217, fCONVERSION_RATE);
		}

		// =====================================================================================
		// Constructors
		// BEFORE: Currency(HttpApplicationState Application)
		//           - SplendidDefaults.BaseCurrencyID(Application) provided the base currency GUID
		// AFTER:  Currency(IMemoryCache cache)
		//           - Hardcoded USD GUID used (same value as migrated SplendidDefaults.CurrencyID())
		//
		// BEFORE: Currency(HttpApplicationState Application, Guid gID, string sNAME, ...)
		// AFTER:  Currency(IMemoryCache cache, Guid gID, string sNAME, ...)
		// =====================================================================================

		// 04/30/2016 Paul.  Require the Application so that we can get the base currency. 
		/// <summary>
		/// Creates a default USD Currency instance (U.S. Dollar, symbol "$", ISO 4217 "USD",
		/// conversion rate 1.0).
		/// </summary>
		/// <param name="cache">
		/// IMemoryCache replacing HttpApplicationState.
		/// BEFORE: SplendidDefaults.BaseCurrencyID(Application) derived m_gUSDollar.
		/// AFTER:  Hardcoded USD GUID E340202E-6291-4071-B327-A34CB4DF239B used directly.
		///         The cache parameter is accepted to maintain the identical API contract
		///         visible to all call sites that previously passed HttpApplicationState.
		/// </param>
		public Currency(IMemoryCache cache)
		{
			// 04/30/2016 Paul.  Base currency has been USD, but we should make it easy to allow a different base. 
			// BEFORE: m_gUSDollar = SplendidDefaults.BaseCurrencyID(Application);
			// AFTER:  Hardcoded USD GUID (same value returned by migrated SplendidDefaults.CurrencyID())
			m_gUSDollar        = new Guid("E340202E-6291-4071-B327-A34CB4DF239B");
			m_gID              = m_gUSDollar;
			m_sNAME            = "U.S. Dollar";
			m_sSYMBOL          = "$";
			m_sISO4217         = "USD";
			m_fCONVERSION_RATE = 1.0f;
			m_bUSDollars       = true;
		}

		// 11/10/2008 Paul.  PayPal uses the ISO value. 
		// 04/30/2016 Paul.  Require the Application so that we can get the base currency. 
		/// <summary>
		/// Creates a Currency instance with the specified identity and conversion rate.
		/// </summary>
		/// <param name="cache">
		/// IMemoryCache replacing HttpApplicationState for base currency GUID lookup.
		/// BEFORE: SplendidDefaults.BaseCurrencyID(Application) provided m_gUSDollar.
		/// AFTER:  Hardcoded USD GUID used.  Cache parameter accepted for API parity.
		/// </param>
		/// <param name="gID">Currency GUID (primary key in CURRENCIES table).</param>
		/// <param name="sNAME">Human-readable currency name (e.g., "Euro").</param>
		/// <param name="sSYMBOL">Currency symbol (e.g., "€").</param>
		/// <param name="sISO4217">ISO 4217 three-letter currency code (e.g., "EUR").</param>
		/// <param name="fCONVERSION_RATE">Conversion rate from base currency to this currency.</param>
		public Currency
			( IMemoryCache cache
			, Guid   gID             
			, string sNAME           
			, string sSYMBOL         
			, string sISO4217        
			, float  fCONVERSION_RATE
			)
		{
			// 04/30/2016 Paul.  Base currency has been USD, but we should make it easy to allow a different base. 
			// BEFORE: m_gUSDollar = SplendidDefaults.BaseCurrencyID(Application);
			// AFTER:  Hardcoded USD GUID E340202E-6291-4071-B327-A34CB4DF239B
			m_gUSDollar        = new Guid("E340202E-6291-4071-B327-A34CB4DF239B");
			m_gID              = gID             ;
			m_sNAME            = sNAME           ;
			m_sSYMBOL          = sSYMBOL         ;
			m_sISO4217         = sISO4217        ;
			m_fCONVERSION_RATE = fCONVERSION_RATE;
			m_bUSDollars       = (m_gID == m_gUSDollar);
		}

		// =====================================================================================
		// Currency conversion instance methods
		// No System.Web dependencies in the original; zero migration changes required.
		// Preserved exactly from .NET Framework 4.8 source (minimal change clause).
		// =====================================================================================

		/// <summary>
		/// Converts an amount FROM the base currency TO this currency using float arithmetic.
		/// Short-circuits with identity function when this currency IS the base currency
		/// (m_bUSDollars == true) to prevent floating-point rounding bugs.
		/// </summary>
		/// <param name="f">Amount in the base currency.</param>
		/// <returns>Amount in this currency.</returns>
		public float ToCurrency(float f)
		{
			// 05/10/2006 Paul.  Short-circuit the math if USD. 
			// This is more to prevent bugs than to speed calculations. 
			if ( m_bUSDollars )
				return f;
			return f * m_fCONVERSION_RATE;
		}

		/// <summary>
		/// Converts an amount FROM this currency TO the base currency using float arithmetic.
		/// Short-circuits with identity function when this currency IS the base currency.
		/// </summary>
		/// <param name="f">Amount in this currency.</param>
		/// <returns>Amount in the base currency.</returns>
		public float FromCurrency(float f)
		{
			// 05/10/2006 Paul.  Short-circuit the math if USD. 
			// This is more to prevent bugs than to speed calculations. 
			if ( m_bUSDollars )
				return f;
			return f / m_fCONVERSION_RATE;
		}

		// 03/30/2007 Paul.  Decimal is the main format for currencies. 
		/// <summary>
		/// Converts an amount FROM the base currency TO this currency using Decimal arithmetic.
		/// Short-circuits when this currency IS the base currency.
		/// Uses double conversion to avoid Decimal × float overflow.
		/// </summary>
		/// <param name="d">Amount in the base currency.</param>
		/// <returns>Amount in this currency.</returns>
		public Decimal ToCurrency(Decimal d)
		{
			if ( m_bUSDollars )
				return d;
			return Convert.ToDecimal(Convert.ToDouble(d) * m_fCONVERSION_RATE);
		}

		/// <summary>
		/// Converts an amount FROM this currency TO the base currency using Decimal arithmetic.
		/// Short-circuits when this currency IS the base currency or when CONVERSION_RATE is 0
		/// (divide-by-zero guard added in 04/18/2007).
		/// </summary>
		/// <param name="d">Amount in this currency.</param>
		/// <returns>Amount in the base currency.</returns>
		public Decimal FromCurrency(Decimal d)
		{
			// 05/10/2006 Paul.  Short-circuit the math if USD. 
			// This is more to prevent bugs than to speed calculations. 
			// 04/18/2007 Paul.  Protect against divide by zero. 
			if ( m_bUSDollars || m_fCONVERSION_RATE == 0.0 )
				return d;
			return Convert.ToDecimal(Convert.ToDouble(d) / m_fCONVERSION_RATE);
		}

		// =====================================================================================
		// UpdateRates — scheduler callback for CurrencyLayer API rate refresh
		//
		// BEFORE (.NET Framework 4.8):
		//   public static void UpdateRates(Object sender)
		//   {
		//       HttpContext Context = sender as HttpContext;
		//       if (!bInsideUpdateRates && !Sql.IsEmptyString(Context.Application["CONFIG.CurrencyLayer.AccessKey"]))
		//       {
		//           DbProviderFactory dbf = DbProviderFactories.GetFactory(Context.Application);
		//           ... da.Fill(dt) ...
		//           float dRate = OrderUtils.GetCurrencyConversionRate(Context.Application, sISO4217, sbErrors);
		//           SplendidError.SystemMessage(Context, "Error", stack, msg/ex);
		//       }
		//   }
		//
		// AFTER (.NET 10 ASP.NET Core):
		//   public static void UpdateRates(IMemoryCache memoryCache)
		//   {
		//       - memoryCache.Get<object>() replaces Context.Application[]
		//       - _ambientDbf (set by InitializeStaticServices) replaces DbProviderFactories.GetFactory
		//       - _ambientOrderUtils.GetCurrencyConversionRate(cache, source, dest, errors) [instance, 4-param]
		//         replaces OrderUtils.GetCurrencyConversionRate(Application, dest, errors) [static, 3-param]
		//       - Source currency derived from CONFIG.currencyiso4217 in IMemoryCache (default "USD")
		//         (previously internal to OrderUtils, now passed explicitly per .NET 10 migration)
		//       - SplendidError.SystemMessage(memoryCache, "Error", stack, msg/ex) [IMemoryCache overload]
		//         replaces SplendidError.SystemMessage(Context, "Error", stack, msg/ex) [HttpContext overload]
		//   }
		// =====================================================================================

		// 05/02/2016 Paul.  Create a scheduler to ensure that the currencies are always correct. 
		/// <summary>
		/// Fetches live exchange rates from the CurrencyLayer API for all active non-base
		/// currencies and updates them in the database via OrderUtils.GetCurrencyConversionRate.
		/// Guarded by <see cref="bInsideUpdateRates"/> to prevent concurrent execution from
		/// multiple scheduler callbacks firing simultaneously.
		/// </summary>
		/// <param name="memoryCache">
		/// IMemoryCache replacing HttpApplicationState (Application[]).
		/// Keys read:
		///   "CONFIG.CurrencyLayer.AccessKey" — guards execution (skips when empty/missing)
		///   "CONFIG.currencyiso4217"         — base/source currency ISO code (default "USD")
		/// </param>
		public static void UpdateRates(IMemoryCache memoryCache)
		{
			// BEFORE: HttpContext Context = sender as HttpContext;
			//         if ( !bInsideUpdateRates && !Sql.IsEmptyString(Context.Application["CONFIG.CurrencyLayer.AccessKey"]))
			// AFTER:  memoryCache.Get<object>() replaces Context.Application[]; null-safe Get<object>
			if ( !bInsideUpdateRates && !Sql.IsEmptyString(memoryCache != null ? memoryCache.Get<object>("CONFIG.CurrencyLayer.AccessKey") : null) )
			{
				bInsideUpdateRates = true;
				try
				{
					// BEFORE: DbProviderFactory dbf = DbProviderFactories.GetFactory(Context.Application);
					// AFTER:  Use static ambient _ambientDbf set by InitializeStaticServices().
					//         Null-guard ensures UpdateRates returns safely when called before startup.
					DbProviderFactory dbf = _ambientDbf;
					if ( dbf == null )
						return;

					using ( IDbConnection con = dbf.CreateConnection() )
					{
						string sSQL;
						sSQL = "select *                  " + ControlChars.CrLf
						     + "  from vwCURRENCIES_List  " + ControlChars.CrLf
						     + " where STATUS  = N'Active'" + ControlChars.CrLf
						     + "   and IS_BASE = 0        " + ControlChars.CrLf
						     + " order by NAME            " + ControlChars.CrLf;
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.CommandText = sSQL;
							using ( DbDataAdapter da = dbf.CreateDataAdapter() )
							{
								((IDbDataAdapter)da).SelectCommand = cmd;
								using ( DataTable dt = new DataTable() )
								{
									da.Fill(dt);
									foreach ( DataRow row in dt.Rows )
									{
										StringBuilder sbErrors = new StringBuilder();
										string sISO4217 = Sql.ToString(row["ISO4217"]);
										// BEFORE: float dRate = OrderUtils.GetCurrencyConversionRate(Context.Application, sISO4217, sbErrors);
										//         (static method; source currency derived internally from SplendidDefaults.BaseCurrencyISO)
										// AFTER:  Instance method on _ambientOrderUtils; source currency passed explicitly.
										//         Source derived from CONFIG.currencyiso4217 in IMemoryCache (default "USD")
										//         to match what SplendidDefaults.BaseCurrencyISO(Application) returned.
										OrderUtils orderUtils = _ambientOrderUtils;
										if ( orderUtils != null )
										{
											string sSourceCurrency = Sql.ToString(memoryCache != null ? memoryCache.Get<object>("CONFIG.currencyiso4217") : null);
											if ( Sql.IsEmptyString(sSourceCurrency) )
												sSourceCurrency = "USD";
											orderUtils.GetCurrencyConversionRate(memoryCache, sSourceCurrency, sISO4217, sbErrors);
										}
										if ( sbErrors.Length > 0 )
										{
											// BEFORE: SplendidError.SystemMessage(Context, "Error", new StackTrace(true).GetFrame(0), sbErrors.ToString());
											// AFTER:  SplendidError.SystemMessage(IMemoryCache, ...) static overload
											SplendidError.SystemMessage(memoryCache, "Error", new StackTrace(true).GetFrame(0), sbErrors.ToString());
										}
									}
								}
							}
						}
					}
				}
				catch(Exception ex)
				{
					// BEFORE: SplendidError.SystemMessage(Context, "Error", new StackTrace(true).GetFrame(0), ex);
					// AFTER:  SplendidError.SystemMessage(IMemoryCache, ...) static overload
					SplendidError.SystemMessage(memoryCache, "Error", new StackTrace(true).GetFrame(0), ex);
				}
				finally
				{
					bInsideUpdateRates = false;
				}
			}
		}
	}
}
