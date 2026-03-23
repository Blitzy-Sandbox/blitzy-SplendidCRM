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
// .NET 10 Migration: SplendidCRM/_code/OrderUtils.cs → src/SplendidCRM.Core/OrderUtils.cs
// Changes applied:
//   - REMOVED: using System.Web (HttpApplicationState, HttpRuntime.Cache)
//   - REMOVED: using System.Web.Script.Serialization (WebScriptSerializer)
//   - REMOVED: using System.Drawing / System.Drawing.Imaging (unused in this file)
//   - REMOVED: using Spring.Json (JsonValue, JsonValue.Parse — discontinued library with no .NET 10 NuGet)
//   - ADDED:   using System.Text.Json (JsonDocument/JsonElement replaces Spring.Json.JsonValue)
//   - ADDED:   using Microsoft.Extensions.Caching.Memory (IMemoryCache replaces HttpApplicationState + HttpRuntime.Cache)
//   - HttpApplicationState (Application[]) parameter → IMemoryCache parameter in GetCurrencyConversionRate overloads
//   - HttpRuntime.Cache.Get() → IMemoryCache.TryGetValue() for rate cache lookup
//   - HttpRuntime.Cache.Insert() → IMemoryCache.Set() with MemoryCacheEntryOptions.SetAbsoluteExpiration()
//   - Application["CONFIG.CurrencyLayer.*"] → IMemoryCache.Get<object>("CONFIG.CurrencyLayer.*") with Sql.ToXxx() conversion
//   - Application["CurrencyLayer.ETag.*"] → IMemoryCache.Get<CurrencyLayerETag>() with distinct "CurrencyLayer.ETag." prefix
//     (original used same key for float in HttpRuntime.Cache and CurrencyLayerETag in Application; migrated uses distinct keys)
//   - Spring.Json.JsonValue.Parse() → System.Text.Json.JsonDocument.Parse() (using block for disposal)
//   - json.GetValueOrDefault<bool>("success") → JsonElement.TryGetProperty() + GetBoolean()
//   - json.ContainsName("quotes") → JsonElement.TryGetProperty("quotes", out JsonElement)
//   - jsonQuotes.GetValueOrDefault<float>(key) → JsonElement.TryGetProperty(key, out JsonElement) + GetSingle()
//   - json.ContainsName("error") → JsonElement.TryGetProperty("error", out JsonElement)
//   - jsonError.GetValue<string>("info") → JsonElement.TryGetProperty("info", ...).GetString()
//   - SplendidCache.Discounts() → DataTable dtDISCOUNTS parameter (DI decoupling: caller obtains from injected SplendidCache)
//   - SplendidDefaults.BaseCurrencyISO(Application) → IMemoryCache.Get<object>("CONFIG.currencyiso4217") with "USD" default
//   - Sql.BeginTransaction(con) → con.BeginTransaction() (Sql.BeginTransaction not present in migrated Sql.cs)
//   - SqlProcs.spSYSTEM_CURRENCY_LOG_InsertOnly() → inlined IDbCommand (StoredProcedure + parameters)
//   - SqlProcs.spCURRENCIES_UpdateRateByISO() → inlined IDbCommand (StoredProcedure + parameters)
//   - Utils.ExpandException(ex) → ex.Message (Utils.ExpandException removed in migrated Utils.cs)
//   - SplendidError.SystemMessage(Application, ...) → SplendidError.SystemMessage(IMemoryCache, ...)
//   - DI constructor added: OrderUtils(DbProviderFactories)
//   - GetCurrencyConversionRate overloads changed from static to instance methods (require _dbProviderFactories for DB access)
//   - DiscountPrice / DiscountValue static calculation methods preserved as static (no DB access required)
#nullable disable
using System;
using System.IO;
using System.Net;
using System.Data;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	/// <summary>
	/// CurrencyLayer API ETag cache entry for efficient conditional HTTP requests (304 Not Modified).
	/// Stores the ETag header value, last-modified date, and cached conversion rate from the last
	/// successful API response so subsequent requests can use conditional GET requests.
	///
	/// Migrated from the nested class in SplendidCRM/_code/OrderUtils.cs.
	/// Moved to top-level class for accessibility and schema compliance.
	/// </summary>
	public class CurrencyLayerETag
	{
		/// <summary>ETag header value from the CurrencyLayer API response.</summary>
		public string   ETag;
		/// <summary>Date header value from the CurrencyLayer API response, used as If-Modified-Since.</summary>
		public DateTime Date;
		/// <summary>Cached conversion rate from the last successful 200 OK response.</summary>
		public float    Rate;
	}

	/// <summary>
	/// Order processing utilities for SplendidCRM — pricing discount calculations and live
	/// currency conversion rate retrieval via the CurrencyLayer API.
	///
	/// Migrated from SplendidCRM/_code/OrderUtils.cs for .NET 10 ASP.NET Core.
	///
	/// Key changes from .NET Framework 4.8 version:
	///   • HttpApplicationState (Application[]) replaced with IMemoryCache (injected via DI constructor).
	///   • HttpRuntime.Cache replaced with IMemoryCache for currency rate caching.
	///   • Spring.Json.JsonValue replaced with System.Text.Json.JsonDocument.
	///   • SplendidCache.Discounts() calls replaced with explicit DataTable parameter to break static coupling.
	///   • SqlProcs stored procedure calls inlined as IDbCommand operations.
	///   • GetCurrencyConversionRate overloads are now instance methods (require injected DbProviderFactories).
	///   • Static discount calculation methods (DiscountPrice/DiscountValue) preserved as static.
	/// </summary>
	public class OrderUtils
	{
		// =====================================================================================
		// DI fields
		// =====================================================================================

		/// <summary>
		/// Provider-agnostic database factory for obtaining IDbConnection instances.
		/// BEFORE: DbProviderFactories.GetFactory(Application) returned a DbProviderFactory.
		/// AFTER:  Injected DbProviderFactories service (wraps Microsoft.Data.SqlClient).
		/// </summary>
		private readonly DbProviderFactories _dbProviderFactories;

		// =====================================================================================
		// DI Constructor
		// =====================================================================================

		/// <summary>
		/// Constructs an OrderUtils service instance.
		/// </summary>
		/// <param name="dbProviderFactories">
		/// Replaces DbProviderFactories.GetFactory(Application) static calls.
		/// Used in GetCurrencyConversionRate to open DB connections for rate logging and currency updates.
		/// </param>
		public OrderUtils(DbProviderFactories dbProviderFactories)
		{
			_dbProviderFactories = dbProviderFactories;
		}

		// =====================================================================================
		// Pricing discount calculations — preserved as static (no database access required)
		// =====================================================================================

		/// <summary>
		/// Applies a named pricing formula to compute the discount price from cost and list prices.
		/// Preserved as a pure static calculation method — no infrastructure dependencies required.
		/// </summary>
		/// <param name="sPRICING_FORMULA">
		/// Pricing formula identifier: "Fixed", "ProfitMargin", "PercentageMarkup",
		/// "PercentageDiscount", "FixedDiscount", or "IsList".
		/// </param>
		/// <param name="fPRICING_FACTOR">Numeric factor for the formula (percentage, markup, or fixed amount).</param>
		/// <param name="dCOST_PRICE">Cost price (purchase cost).</param>
		/// <param name="dLIST_PRICE">List price (catalog price).</param>
		/// <param name="dDISCOUNT_PRICE">Output: the computed discount price; unchanged for "Fixed" or unknown formulas.</param>
		public static void DiscountPrice(string sPRICING_FORMULA, float fPRICING_FACTOR, Decimal dCOST_PRICE, Decimal dLIST_PRICE, ref Decimal dDISCOUNT_PRICE)
		{
			if ( fPRICING_FACTOR > 0 )
			{
				switch ( sPRICING_FORMULA )
				{
					case "Fixed"             :
						break;
					case "ProfitMargin"      :
						dDISCOUNT_PRICE = dCOST_PRICE * 100 / (100 - (Decimal) fPRICING_FACTOR);
						break;
					case "PercentageMarkup"  :
						dDISCOUNT_PRICE = dCOST_PRICE * (1 + (Decimal) (fPRICING_FACTOR /100));
						break;
					case "PercentageDiscount":
						dDISCOUNT_PRICE = (dLIST_PRICE * (Decimal) (1 - (fPRICING_FACTOR /100))*100)/100;
						break;
					case "FixedDiscount":
						dDISCOUNT_PRICE = dLIST_PRICE - (Decimal) fPRICING_FACTOR;
						break;
					case "IsList"            :
						dDISCOUNT_PRICE = dLIST_PRICE;
						break;
				}
			}
		}

		/// <summary>
		/// Computes the discount value (amount of discount) for percentage and fixed discount formulas.
		/// Preserved as a pure static calculation method — no infrastructure dependencies required.
		/// </summary>
		/// <param name="sPRICING_FORMULA">Pricing formula identifier; only "PercentageDiscount" and "FixedDiscount" produce a non-zero value.</param>
		/// <param name="fPRICING_FACTOR">Numeric factor for the formula.</param>
		/// <param name="dCOST_PRICE">Cost price (unused for these formulas, preserved for signature parity).</param>
		/// <param name="dLIST_PRICE">List price used as the base for percentage calculations.</param>
		/// <param name="dDISCOUNT_VALUE">Output: the discount value amount.</param>
		public static void DiscountValue(string sPRICING_FORMULA, float fPRICING_FACTOR, Decimal dCOST_PRICE, Decimal dLIST_PRICE, ref Decimal dDISCOUNT_VALUE)
		{
			if ( fPRICING_FACTOR > 0 )
			{
				switch ( sPRICING_FORMULA )
				{
					case "PercentageDiscount":
						dDISCOUNT_VALUE = (dLIST_PRICE * (Decimal) (fPRICING_FACTOR /100)*100)/100;
						break;
					case "FixedDiscount"     :
						dDISCOUNT_VALUE = (Decimal) fPRICING_FACTOR;
						break;
				}
			}
		}

		/// <summary>
		/// Looks up a discount record by ID in the provided discounts DataTable and applies its
		/// pricing formula to compute the discount price.
		///
		/// BEFORE: Called SplendidCache.Discounts() internally to retrieve the discounts DataTable.
		/// AFTER:  Caller passes the discounts DataTable directly (obtained from injected SplendidCache).
		///         This removes the static coupling to SplendidCache and makes the method testable.
		/// </summary>
		/// <param name="gDISCOUNT_ID">ID of the discount record to look up in dtDISCOUNTS.</param>
		/// <param name="dCOST_PRICE">Cost price passed through to the pricing formula.</param>
		/// <param name="dLIST_PRICE">List price passed through to the pricing formula.</param>
		/// <param name="dtDISCOUNTS">
		/// Discounts DataTable (previously obtained via SplendidCache.Discounts()).
		/// Must contain columns: ID, PRICING_FORMULA, PRICING_FACTOR.
		/// No action taken when null.
		/// </param>
		/// <param name="dDISCOUNT_PRICE">Output: the computed discount price.</param>
		/// <param name="sPRICING_FORMULA">Output: the PRICING_FORMULA from the matched discount record.</param>
		/// <param name="fPRICING_FACTOR">Output: the PRICING_FACTOR from the matched discount record.</param>
		public static void DiscountPrice(Guid gDISCOUNT_ID, Decimal dCOST_PRICE, Decimal dLIST_PRICE, DataTable dtDISCOUNTS, ref Decimal dDISCOUNT_PRICE, ref string sPRICING_FORMULA, ref float fPRICING_FACTOR)
		{
			// BEFORE: DataTable dtDISCOUNTS = SplendidCache.Discounts();
			// AFTER:  DataTable is passed as parameter by the caller.
			if ( dtDISCOUNTS != null )
			{
				DataRow[] row = dtDISCOUNTS.Select("ID = '" + gDISCOUNT_ID.ToString() + "'");
				if ( row.Length == 1 )
				{
					sPRICING_FORMULA = Sql.ToString(row[0]["PRICING_FORMULA"]);
					fPRICING_FACTOR  = Sql.ToFloat (row[0]["PRICING_FACTOR" ]);
					DiscountPrice(sPRICING_FORMULA, fPRICING_FACTOR, dCOST_PRICE, dLIST_PRICE, ref dDISCOUNT_PRICE);
				}
			}
		}

		/// <summary>
		/// Looks up a discount record by ID in the provided discounts DataTable and computes the discount value.
		///
		/// BEFORE: Called SplendidCache.Discounts() internally to retrieve the discounts DataTable.
		/// AFTER:  Caller passes the discounts DataTable directly (obtained from injected SplendidCache).
		/// </summary>
		/// <param name="gDISCOUNT_ID">ID of the discount record to look up in dtDISCOUNTS.</param>
		/// <param name="dCOST_PRICE">Cost price passed through to the pricing formula.</param>
		/// <param name="dLIST_PRICE">List price passed through to the pricing formula.</param>
		/// <param name="dtDISCOUNTS">
		/// Discounts DataTable (previously obtained via SplendidCache.Discounts()).
		/// Must contain columns: ID, PRICING_FORMULA, PRICING_FACTOR, NAME.
		/// No action taken when null.
		/// </param>
		/// <param name="dDISCOUNT_VALUE">Output: the discount value (amount of discount).</param>
		/// <param name="sDISCOUNT_NAME">Output: the NAME from the matched discount record.</param>
		/// <param name="sPRICING_FORMULA">Output: the PRICING_FORMULA from the matched discount record.</param>
		/// <param name="fPRICING_FACTOR">Output: the PRICING_FACTOR from the matched discount record.</param>
		public static void DiscountValue(Guid gDISCOUNT_ID, Decimal dCOST_PRICE, Decimal dLIST_PRICE, DataTable dtDISCOUNTS, ref Decimal dDISCOUNT_VALUE, ref string sDISCOUNT_NAME, ref string sPRICING_FORMULA, ref float fPRICING_FACTOR)
		{
			// BEFORE: DataTable dtDISCOUNTS = SplendidCache.Discounts();
			// AFTER:  DataTable is passed as parameter by the caller.
			if ( dtDISCOUNTS != null )
			{
				DataRow[] row = dtDISCOUNTS.Select("ID = '" + gDISCOUNT_ID.ToString() + "'");
				if ( row.Length == 1 )
				{
					sPRICING_FORMULA = Sql.ToString(row[0]["PRICING_FORMULA"]);
					fPRICING_FACTOR  = Sql.ToFloat (row[0]["PRICING_FACTOR" ]);
					sDISCOUNT_NAME   = Sql.ToString(row[0]["NAME"           ]);
					DiscountValue(sPRICING_FORMULA, fPRICING_FACTOR, dCOST_PRICE, dLIST_PRICE, ref dDISCOUNT_VALUE);
				}
			}
		}

		// =====================================================================================
		// Currency conversion via CurrencyLayer API
		// These are instance methods (not static) because they require _dbProviderFactories for DB access.
		// BEFORE: Both were public static methods taking HttpApplicationState Application.
		// AFTER:  Both are public instance methods taking IMemoryCache cache.
		// =====================================================================================

		/// <summary>
		/// Gets the currency conversion rate from source to destination currency using the CurrencyLayer API.
		/// Checks IMemoryCache for a previously cached rate before making an API call.
		/// Reads access key and log settings from IMemoryCache config entries.
		///
		/// BEFORE: GetCurrencyConversionRate(HttpApplicationState Application, string sDestinationCurrency, StringBuilder sbErrors)
		///         - Source currency obtained via SplendidDefaults.BaseCurrencyISO(Application)
		///         - Rate cache checked via HttpRuntime.Cache.Get("CurrencyLayer." + sSource + sDest)
		///         - Config values read from Application["CONFIG.CurrencyLayer.xxx"]
		/// AFTER:  Source currency passed explicitly; rate cache and config read from IMemoryCache.
		/// </summary>
		/// <param name="cache">
		/// IMemoryCache replacing both HttpApplicationState (config values) and HttpRuntime.Cache (rate cache).
		/// Reads: "CurrencyLayer.{source}{dest}" (rate), "CONFIG.CurrencyLayer.AccessKey", "CONFIG.CurrencyLayer.LogConversions".
		/// </param>
		/// <param name="sSourceCurrency">
		/// ISO 4217 source currency code (e.g., "USD").
		/// BEFORE: Was derived internally from SplendidDefaults.BaseCurrencyISO(Application).
		/// AFTER:  Passed explicitly by the caller (typically obtained from SplendidCache or config).
		/// </param>
		/// <param name="sDestinationCurrency">ISO 4217 destination currency code (e.g., "EUR").</param>
		/// <param name="sbErrors">Accumulates API error messages; empty on success.</param>
		/// <returns>Conversion rate as float; 1.0 if currencies are equal, cache hit exists, or on error.</returns>
		public float GetCurrencyConversionRate(IMemoryCache cache, string sSourceCurrency, string sDestinationCurrency, StringBuilder sbErrors)
		{
			// 04/30/2016 Paul.  The primary function uses the default currency of the user as the source. 
			// BEFORE: string sSourceCurrency = SplendidDefaults.BaseCurrencyISO(Application);
			// AFTER:  sSourceCurrency is passed explicitly.

			// BEFORE: object oRate = HttpRuntime.Cache.Get("CurrencyLayer." + sSourceCurrency + sDestinationCurrency);
			// AFTER:  IMemoryCache.TryGetValue() — float stored with key "CurrencyLayer.{source}{dest}"
			float dRate = 1.0F;
			string sRateCacheKey = "CurrencyLayer." + sSourceCurrency + sDestinationCurrency;
			if ( cache != null && cache.TryGetValue(sRateCacheKey, out float fCachedRate) )
			{
				dRate = fCachedRate;
			}
			else if ( String.Compare(sSourceCurrency, sDestinationCurrency, true) != 0 )
			{
				// BEFORE: string sAccessKey      = Sql.ToString (Application["CONFIG.CurrencyLayer.AccessKey"]);
				//         bool   bLogConversions = Sql.ToBoolean(Application["CONFIG.CurrencyLayer.LogConversions"]);
				// AFTER:  IMemoryCache.Get<object>() with Sql.ToXxx() conversion
				string sAccessKey      = Sql.ToString (cache != null ? cache.Get<object>("CONFIG.CurrencyLayer.AccessKey"     ) : null);
				bool   bLogConversions = Sql.ToBoolean(cache != null ? cache.Get<object>("CONFIG.CurrencyLayer.LogConversions") : null);
				// BEFORE: dRate = GetCurrencyConversionRate(Application, sAccessKey, bLogConversions, sSourceCurrency, sDestinationCurrency, sbErrors);
				// AFTER:  GetCurrencyConversionRate is now an instance method.
				dRate = GetCurrencyConversionRate(cache, bLogConversions, sAccessKey, sSourceCurrency, sDestinationCurrency, sbErrors);
			}
			return dRate;
		}

		/// <summary>
		/// Core implementation: Calls the CurrencyLayer API and optionally logs the conversion to the database.
		/// Supports ETags (If-None-Match / If-Modified-Since) for efficient conditional GET requests.
		/// Caches the returned rate in IMemoryCache with absolute expiration based on CONFIG.CurrencyLayer.RateLifetime.
		///
		/// BEFORE: GetCurrencyConversionRate(HttpApplicationState Application, string sAccessKey,
		///             bool bLogConversions, string sSourceCurrency, string sDestinationCurrency, StringBuilder sbErrors)
		///         - HttpRuntime.Cache used for rate caching
		///         - Application[] used for ETag caching and config values
		///         - Spring.Json.JsonValue used for JSON parsing
		///         - SqlProcs static methods called for DB logging
		///         - Sql.BeginTransaction(con) used to begin a transaction
		/// AFTER:  IMemoryCache used for all caching; System.Text.Json for parsing; IDbCommand inline for stored procs.
		/// </summary>
		/// <param name="cache">
		/// IMemoryCache for config lookup, rate caching, and ETag caching.
		/// Keys written: "CurrencyLayer.{source}{dest}" (float rate), "CurrencyLayer.ETag.{source}{dest}" (CurrencyLayerETag).
		/// Config keys read: "CONFIG.CurrencyLayer.UseEncryptedUrl", "CONFIG.CurrencyLayer.RateLifetime", "CONFIG.currencyiso4217".
		/// </param>
		/// <param name="bLogConversions">When true, inserts a record into SYSTEM_CURRENCY_LOG via spSYSTEM_CURRENCY_LOG_InsertOnly.</param>
		/// <param name="sAccessKey">CurrencyLayer API access key. Returns 1.0 with error message when empty.</param>
		/// <param name="sSourceCurrency">ISO 4217 source currency code (e.g., "USD").</param>
		/// <param name="sDestinationCurrency">ISO 4217 destination currency code (e.g., "EUR").</param>
		/// <param name="sbErrors">Accumulates API error messages; empty on success.</param>
		/// <returns>Conversion rate as float; 1.0 if currencies equal, on access key missing, or on error.</returns>
		public float GetCurrencyConversionRate(IMemoryCache cache, bool bLogConversions, string sAccessKey, string sSourceCurrency, string sDestinationCurrency, StringBuilder sbErrors)
		{
			float dRate = 1.0F;
			try
			{
				if ( String.Compare(sSourceCurrency, sDestinationCurrency, true) == 0 )
				{
					dRate = 1.0F;
				}
				else if ( !Sql.IsEmptyString(sAccessKey) )
				{
					// BEFORE: bool bUseEncryptedUrl = Sql.ToBoolean(Application["CONFIG.CurrencyLayer.UseEncryptedUrl"]);
					// AFTER:  IMemoryCache config lookup
					bool bUseEncryptedUrl = Sql.ToBoolean(cache != null ? cache.Get<object>("CONFIG.CurrencyLayer.UseEncryptedUrl") : null);
					string sBaseURL = (bUseEncryptedUrl ? "https" : "http") + "://apilayer.net/api/live?access_key=";
					HttpWebRequest objRequest = (HttpWebRequest) WebRequest.Create(sBaseURL + sAccessKey + "&source=" + sSourceCurrency.ToUpper() + "&currencies=" + sDestinationCurrency.ToUpper());
					objRequest.KeepAlive         = false;
					objRequest.AllowAutoRedirect = false;
					objRequest.Timeout           = 15000;  // 15 seconds
					objRequest.Method            = "GET";

					// 04/30/2016 Paul.  Support ETags for efficient lookups.
					// BEFORE: CurrencyLayerETag oETag = Application["CurrencyLayer." + sSourceCurrency + sDestinationCurrency] as CurrencyLayerETag;
					// AFTER:  IMemoryCache.Get<CurrencyLayerETag>() with distinct "CurrencyLayer.ETag." prefix.
					//         The original used the same key for two different stores (HttpRuntime.Cache for float, Application for CurrencyLayerETag).
					//         The migrated version uses distinct cache keys to avoid type conflicts in the single IMemoryCache store.
					string sETagCacheKey = "CurrencyLayer.ETag." + sSourceCurrency + sDestinationCurrency;
					CurrencyLayerETag oETag = cache != null ? cache.Get<CurrencyLayerETag>(sETagCacheKey) : null;
					if ( oETag != null )
					{
						objRequest.Headers.Add("If-None-Match", oETag.ETag);
						objRequest.IfModifiedSince = oETag.Date;
					}
					using ( HttpWebResponse objResponse = (HttpWebResponse) objRequest.GetResponse() )
					{
						if ( objResponse != null )
						{
							if ( objResponse.StatusCode == HttpStatusCode.OK || objResponse.StatusCode == HttpStatusCode.Found )
							{
								using ( StreamReader readStream = new StreamReader(objResponse.GetResponseStream(), Encoding.UTF8) )
								{
									string sJsonResponse = readStream.ReadToEnd();

									// BEFORE: JsonValue json = JsonValue.Parse(sJsonResponse);  (Spring.Json)
									// AFTER:  System.Text.Json.JsonDocument (wrapped in using for disposal)
									using ( JsonDocument jsonDoc = JsonDocument.Parse(sJsonResponse) )
									{
										JsonElement json = jsonDoc.RootElement;

										// BEFORE: bool bSuccess = json.GetValueOrDefault<bool>("success");
										// AFTER:  TryGetProperty + GetBoolean()
										bool bSuccess = json.TryGetProperty("success", out JsonElement jSuccess) && jSuccess.GetBoolean();

										// {"success":false,"error":{"code":105,"info":"Access Restricted - Your current Subscription Plan does not support HTTPS Encryption."}}
										// BEFORE: if ( bSuccess && json.ContainsName("quotes") )
										// AFTER:  TryGetProperty("quotes", out JsonElement jQuotes)
										if ( bSuccess && json.TryGetProperty("quotes", out JsonElement jQuotes) )
										{
											// BEFORE: dRate = jsonQuotes.GetValueOrDefault<float>(sSourceCurrency.ToUpper() + sDestinationCurrency.ToUpper());
											// AFTER:  TryGetProperty + GetSingle()
											string sRateKey = sSourceCurrency.ToUpper() + sDestinationCurrency.ToUpper();
											dRate = jQuotes.TryGetProperty(sRateKey, out JsonElement jRate) ? jRate.GetSingle() : 1.0F;

											// BEFORE: int nRateLifetime = Sql.ToInteger(Application["CONFIG.CurrencyLayer.RateLifetime"]);
											// AFTER:  IMemoryCache config lookup
											int nRateLifetime = Sql.ToInteger(cache != null ? cache.Get<object>("CONFIG.CurrencyLayer.RateLifetime") : null);
											if ( nRateLifetime <= 0 )
												nRateLifetime = 90;

											// BEFORE: HttpRuntime.Cache.Insert("CurrencyLayer." + ..., dRate, null,
											//             DateTime.Now.AddMinutes(nRateLifetime), System.Web.Caching.Cache.NoSlidingExpiration);
											// AFTER:  IMemoryCache.Set() with absolute expiration (replaces NoSlidingExpiration pattern)
											if ( cache != null )
											{
												var rateOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(DateTime.Now.AddMinutes(nRateLifetime));
												cache.Set("CurrencyLayer." + sSourceCurrency + sDestinationCurrency, dRate, rateOptions);

												// BEFORE: Application["CurrencyLayer." + sSourceCurrency + sDestinationCurrency] = oETag;
												// AFTER:  Stored under "CurrencyLayer.ETag." prefix to distinguish from float rate in same IMemoryCache
												oETag      = new CurrencyLayerETag();
												oETag.ETag = objResponse.Headers.Get("ETag");
												oETag.Rate = dRate;
												DateTime.TryParse(objResponse.Headers.Get("Date"), out oETag.Date);
												cache.Set(sETagCacheKey, oETag);
											}

											// Database logging: log the conversion and update the live currency rate record
											// BEFORE: DbProviderFactory dbf = DbProviderFactories.GetFactory(Application);
											// AFTER:  _dbProviderFactories (injected DbProviderFactories service)
											if ( _dbProviderFactories != null )
											{
												using ( IDbConnection con = _dbProviderFactories.CreateConnection() )
												{
													con.Open();
													// BEFORE: using (IDbTransaction trn = Sql.BeginTransaction(con))
													// AFTER:  Sql.BeginTransaction() not in migrated Sql.cs; use con.BeginTransaction() directly
													using ( IDbTransaction trn = con.BeginTransaction() )
													{
														try
														{
															Guid gSYSTEM_CURRENCY_LOG = Guid.Empty;
															if ( bLogConversions )
															{
																// BEFORE: SqlProcs.spSYSTEM_CURRENCY_LOG_InsertOnly(ref gSYSTEM_CURRENCY_LOG, "CurrencyLayer", sSourceCurrency, sDestinationCurrency, dRate, sJsonResponse, trn)
																// AFTER:  Inline IDbCommand (SqlProcs.spSYSTEM_CURRENCY_LOG_InsertOnly not in migrated SqlProcs.cs)
																using ( IDbCommand cmd = con.CreateCommand() )
																{
																	cmd.Transaction    = trn;
																	cmd.CommandType    = CommandType.StoredProcedure;
																	cmd.CommandText    = "spSYSTEM_CURRENCY_LOG_InsertOnly";
																	IDbDataParameter parID = Sql.AddParameter(cmd, "@ID", gSYSTEM_CURRENCY_LOG);
																	parID.Direction = ParameterDirection.InputOutput;
																	Sql.AddParameter(cmd, "@MODIFIED_USER_ID"   , Guid.Empty          );
																	Sql.AddParameter(cmd, "@SERVICE_NAME"       , "CurrencyLayer"      ,  50);
																	Sql.AddParameter(cmd, "@SOURCE_ISO4217"     , sSourceCurrency      ,   3);
																	Sql.AddParameter(cmd, "@DESTINATION_ISO4217", sDestinationCurrency ,   3);
																	Sql.AddParameter(cmd, "@CONVERSION_RATE"    , dRate                );
																	Sql.AddParameter(cmd, "@RAW_CONTENT"        , sJsonResponse        );
																	cmd.ExecuteNonQuery();
																	gSYSTEM_CURRENCY_LOG = Sql.ToGuid(parID.Value);
																}
															}

															// 04/30/2016 Paul.  We have to update the currency record as it is used inside stored procedures.
															// BEFORE: if ( sSourceCurrency == SplendidDefaults.BaseCurrencyISO(Application) )
															// AFTER:  Read base currency ISO from IMemoryCache config entry "CONFIG.currencyiso4217"
															//         (BaseCurrencyISO(Application) read Application["CONFIG.currencyiso4217"] originally)
															string sBaseCurrencyISO = Sql.ToString(cache != null ? cache.Get<object>("CONFIG.currencyiso4217") : null);
															if ( Sql.IsEmptyString(sBaseCurrencyISO) )
																sBaseCurrencyISO = "USD";
															if ( String.Compare(sSourceCurrency, sBaseCurrencyISO, true) == 0 )
															{
																// BEFORE: SqlProcs.spCURRENCIES_UpdateRateByISO(sDestinationCurrency, dRate, gSYSTEM_CURRENCY_LOG, trn)
																// AFTER:  Inline IDbCommand (SqlProcs.spCURRENCIES_UpdateRateByISO not in migrated SqlProcs.cs)
																using ( IDbCommand cmd = con.CreateCommand() )
																{
																	cmd.Transaction = trn;
																	cmd.CommandType = CommandType.StoredProcedure;
																	cmd.CommandText = "spCURRENCIES_UpdateRateByISO";
																	Sql.AddParameter(cmd, "@MODIFIED_USER_ID"      , Guid.Empty          );
																	Sql.AddParameter(cmd, "@ISO4217"               , sDestinationCurrency ,   3);
																	Sql.AddParameter(cmd, "@CONVERSION_RATE"       , dRate                );
																	Sql.AddParameter(cmd, "@SYSTEM_CURRENCY_LOG_ID", gSYSTEM_CURRENCY_LOG );
																	cmd.ExecuteNonQuery();
																}
															}
															trn.Commit();
														}
														catch
														{
															trn.Rollback();
															throw;
														}
													}
												}
											}
										}
										else if ( json.TryGetProperty("error", out JsonElement jError) )
										{
											// BEFORE: JsonValue jsonError = json.GetValue("error");
											//         string sInfo = jsonError.GetValue<string>("info");
											// AFTER:  JsonElement.TryGetProperty() + GetString()
											string sInfo = jError.TryGetProperty("info", out JsonElement jInfo) ? jInfo.GetString() : String.Empty;
											sbErrors.Append(sInfo);
										}
										else
										{
											sbErrors.Append("Conversion not found for " + sSourceCurrency + " to " + sDestinationCurrency + ".");
										}
									} // end using JsonDocument
								} // end using StreamReader
							}
							else if ( objResponse.StatusCode == HttpStatusCode.NotModified )
							{
								// 304 Not Modified: use the previously cached rate from the ETag object
								if ( oETag != null )
									dRate = oETag.Rate;
							}
							else
							{
								sbErrors.Append(objResponse.StatusDescription);
							}
						}
					} // end using HttpWebResponse
				}
				else
				{
					sbErrors.Append("CurrencyLayer access key is empty.");
				}

				if ( sbErrors.Length > 0 )
				{
					// BEFORE: SplendidError.SystemMessage(Application, "Error", new StackTrace(true).GetFrame(0), ...)
					// AFTER:  SplendidError.SystemMessage(IMemoryCache, ...) — Application replaced by IMemoryCache
					SplendidError.SystemMessage(cache, "Error", new StackTrace(true).GetFrame(0), "CurrencyLayer " + sSourceCurrency + sDestinationCurrency + ": " + sbErrors.ToString());
				}
			}
			catch(Exception ex)
			{
				sbErrors.AppendLine(ex.Message);
				// BEFORE: SplendidError.SystemMessage(Application, "Error", ..., "CurrencyLayer ... " + Utils.ExpandException(ex))
				// AFTER:  Utils.ExpandException() not in migrated Utils.cs; use ex.Message directly
				SplendidError.SystemMessage(cache, "Error", new StackTrace(true).GetFrame(0), "CurrencyLayer " + sSourceCurrency + sDestinationCurrency + ": " + ex.Message);
			}
			return dRate;
		}
	}
}
