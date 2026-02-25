/*
 * Copyright (C) 2005-2025 SplendidCRM Software, Inc.
 * MIT License
 *
 * SplendidCache.cs  –  Metadata caching hub and React dictionary helpers
 * Migrated from .NET Framework 4.8 to .NET 10 ASP.NET Core
 * CHANGES:
 *   - Legacy cache (HttpRuntime + Application[])  → IMemoryCache (unified)
 *   - Legacy context (System.Web.HttpContext) Session      → IHttpContextAccessor + session helpers
 *   - DbProviderFactory static usage       → injected DbProviderFactories instance
 *   - ConfigurationManager.AppSettings     → IConfiguration
 *   - Static class pattern                 → DI-injectable instance class (ambient statics for callbacks)
 *   - ((IDbDataAdapter)da).SelectCommand   replaces SqlDataAdapter cast (provider-agnostic)
 *   - ConcurrentDictionary key registry   for prefix-based cache invalidation (no IMemoryCache enumeration)
 */

#nullable disable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace SplendidCRM
{
	// ---------------------------------------------------------------------------
	// Delegate used for pluggable list callbacks in CustomCaches registry
	// ---------------------------------------------------------------------------
	public delegate DataTable SplendidCacheCallback();

	// ---------------------------------------------------------------------------
	// Metadata record stored in CustomCaches list
	// ---------------------------------------------------------------------------
	public class SplendidCacheReference
	{
		// --- Properties --------------------------------------------------------
		public string                 Name          { get; set; }
		public string                 sMODULE_NAME  { get; set; }
		public string                 sDATA_NAME    { get; set; }
		public string                 MODULE_NAME   { get; set; }
		public string                 DATA_NAME     { get; set; }
		public string                 ID            { get; set; }
		public string                 NAME          { get; set; }
		public SplendidCacheCallback  DataSource    { get; set; }

		// --- Constructors ------------------------------------------------------
		public SplendidCacheReference()
		{
		}

		public SplendidCacheReference(string sName, string sID, string sNAME, SplendidCacheCallback callback)
		{
			Name          = sName;
			sMODULE_NAME  = sName;
			sDATA_NAME    = sName;
			MODULE_NAME   = sName;
			DATA_NAME     = sName;
			ID            = sID;
			NAME          = sNAME;
			DataSource    = callback;
		}
	}

	// ---------------------------------------------------------------------------
	// SplendidCache  –  unified metadata + React dictionary cache
	// ---------------------------------------------------------------------------
	public partial class SplendidCache
	{
		// -----------------------------------------------------------------------
		// DI-injected instance fields
		// -----------------------------------------------------------------------
		private readonly IHttpContextAccessor  _httpContextAccessor;
		private readonly IMemoryCache          _memoryCache;
		private readonly IConfiguration        _configuration;
		private readonly DbProviderFactories   _dbProviderFactories;
		private readonly Security              _security;
		private readonly SplendidError         _splendidError;

		// -----------------------------------------------------------------------
		// Static ambients — set in constructor; enable delegate-based callbacks
		// in the static CustomCaches registry and any callers that use the old
		// static-style access pattern
		// -----------------------------------------------------------------------
		private static IHttpContextAccessor  _ambientHttpContextAccessor;
		private static IMemoryCache          _ambientMemoryCache;
		private static IConfiguration        _ambientConfiguration;
		private static DbProviderFactories   _ambientDbProviderFactories;
		private static Security              _ambientSecurity;
		private static SplendidError         _ambientSplendidError;

		// -----------------------------------------------------------------------
		// Key registry — tracks every cache key inserted so we can do
		// prefix-based removal (IMemoryCache does not support enumeration).
		// -----------------------------------------------------------------------
		private static readonly ConcurrentDictionary<string, byte> _cacheKeys
			= new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

		// -----------------------------------------------------------------------
		// CustomCaches registry — 37 well-known list sources
		// DataSource callbacks are null here; resolved at runtime via
		// InvokeBuiltinListCallback() so that instance DI methods are used.
		// External entries (AddReportSource) carry non-null DataSource callbacks.
		// -----------------------------------------------------------------------
		public static List<SplendidCacheReference> CustomCaches = new List<SplendidCacheReference>
		{
			new SplendidCacheReference("AssignedUser"         , "ASSIGNED_USER_ID", "FULL_NAME"     , null),
			new SplendidCacheReference("Currencies"           , "ID"              , "NAME"          , null),
			new SplendidCacheReference("Timezones"            , "ID"              , "NAME"          , null),
			new SplendidCacheReference("Languages"            , "NAME"            , "DISPLAY_NAME"  , null),
			new SplendidCacheReference("Release"              , "ID"              , "NAME"          , null),
			new SplendidCacheReference("Manufacturers"        , "ID"              , "NAME"          , null),
			new SplendidCacheReference("Discounts"            , "ID"              , "NAME"          , null),
			new SplendidCacheReference("Shippers"             , "ID"              , "NAME"          , null),
			new SplendidCacheReference("PaymentTypes"         , "ID"              , "NAME"          , null),
			new SplendidCacheReference("PaymentTerms"         , "ID"              , "NAME"          , null),
			new SplendidCacheReference("Regions"              , "ID"              , "NAME"          , null),
			new SplendidCacheReference("TaxRates"             , "ID"              , "NAME"          , null),
			new SplendidCacheReference("ContractTypes"        , "ID"              , "NAME"          , null),
			new SplendidCacheReference("Themes"               , "NAME"            , "NAME"          , null),
			new SplendidCacheReference("ActiveUsers"          , "ID"              , "FULL_NAME"     , null),
			new SplendidCacheReference("Modules"              , "MODULE_NAME"     , "DISPLAY_NAME"  , null),
			new SplendidCacheReference("ModulesPopups"        , "MODULE_NAME"     , "DISPLAY_NAME"  , null),
			new SplendidCacheReference("ImportModules"        , "MODULE_NAME"     , "DISPLAY_NAME"  , null),
			new SplendidCacheReference("AccessibleModules"    , "MODULE_NAME"     , "DISPLAY_NAME"  , null),
			new SplendidCacheReference("ReportingModules"     , "MODULE_NAME"     , "DISPLAY_NAME"  , null),
			new SplendidCacheReference("WorkflowModules"      , "MODULE_NAME"     , "DISPLAY_NAME"  , null),
			new SplendidCacheReference("RulesModules"         , "MODULE_NAME"     , "DISPLAY_NAME"  , null),
			new SplendidCacheReference("CustomEditModules"    , "MODULE_NAME"     , "DISPLAY_NAME"  , null),
			new SplendidCacheReference("ExchangeModulesSync"  , "MODULE_NAME"     , "DISPLAY_NAME"  , null),
			new SplendidCacheReference("ProductCategories"    , "ID"              , "NAME"          , null),
			new SplendidCacheReference("ProductTypes"         , "ID"              , "NAME"          , null),
			new SplendidCacheReference("AuditedModules"       , "MODULE_NAME"     , "DISPLAY_NAME"  , null),
			new SplendidCacheReference("InboundEmailBounce"   , "ID"              , "NAME"          , null),
			new SplendidCacheReference("InboundEmailMonitored", "ID"              , "NAME"          , null),
			new SplendidCacheReference("PaymentTerms"         , "ID"              , "NAME"          , null),
			new SplendidCacheReference("CreditCardTypes"      , "ID"              , "NAME"          , null),
			new SplendidCacheReference("ReportingFilterColumns"         , "ID"    , "NAME"          , null),
			new SplendidCacheReference("SurveyTargetColumns"            , "ID"    , "NAME"          , null),
			new SplendidCacheReference("WorkflowFilterColumns"          , "ID"    , "NAME"          , null),
			new SplendidCacheReference("WorkflowFilterUpdateColumns"    , "ID"    , "NAME"          , null),
			new SplendidCacheReference("ReportingFilterColumnsListName"  , "ID"   , "NAME"          , null),
			new SplendidCacheReference("GetAllReportingFilterColumnsListName", "ID","NAME"          , null),
		};

		// activity module names used in filter-column queries
		private static readonly string[] arrActivityModules = new string[]
		{
			"Calls", "Meetings", "Tasks", "Notes", "Emails"
		};

		// -----------------------------------------------------------------------
		// Constructor
		// -----------------------------------------------------------------------
		public SplendidCache(
			IMemoryCache          memoryCache,
			IConfiguration        configuration,
			IHttpContextAccessor  httpContextAccessor,
			DbProviderFactories   dbProviderFactories,
			Security              security,
			SplendidError         splendidError)
		{
			_memoryCache          = memoryCache         ?? throw new ArgumentNullException(nameof(memoryCache));
			_configuration        = configuration       ?? throw new ArgumentNullException(nameof(configuration));
			_httpContextAccessor  = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
			_dbProviderFactories  = dbProviderFactories ?? throw new ArgumentNullException(nameof(dbProviderFactories));
			_security             = security            ?? throw new ArgumentNullException(nameof(security));
			_splendidError        = splendidError       ?? throw new ArgumentNullException(nameof(splendidError));

			// Publish static ambients so delegate-based callers and CustomCaches callbacks
			// can resolve instance services without DI infrastructure access.
			_ambientHttpContextAccessor = httpContextAccessor;
			_ambientMemoryCache         = memoryCache;
			_ambientConfiguration       = configuration;
			_ambientDbProviderFactories = dbProviderFactories;
			_ambientSecurity            = security;
			_ambientSplendidError       = splendidError;
		}

		// -----------------------------------------------------------------------
		// Session helpers  (ISession only stores strings/bytes natively;
		// complex objects use IMemoryCache keyed by sessionId prefix with
		// a sliding 20-min expiration to mirror InProc session behavior)
		// -----------------------------------------------------------------------
		private ISession Session => _httpContextAccessor?.HttpContext?.Session;

		private string SessionId
		{
			get
			{
				try { return Session?.Id ?? "anon"; }
				catch { return "anon"; }
			}
		}

		private T GetSessionObject<T>(string key) where T : class
		{
			string full = $"__sess_{SessionId}_{key}";
			return _memoryCache.Get<T>(full);
		}

		private void SetSessionObject(string key, object value)
		{
			string full = $"__sess_{SessionId}_{key}";
			if (value == null)
			{
				_memoryCache.Remove(full);
				return;
			}
			_memoryCache.Set(full, value, new MemoryCacheEntryOptions()
				.SetSlidingExpiration(TimeSpan.FromMinutes(20)));
		}

		private void RemoveSessionObject(string key)
		{
			string full = $"__sess_{SessionId}_{key}";
			_memoryCache.Remove(full);
		}

		// -----------------------------------------------------------------------
		// Cache helpers
		// -----------------------------------------------------------------------

		/// <summary>Returns the default absolute expiration (1 day from now).</summary>
		public DateTimeOffset DefaultCacheExpiration() => DateTimeOffset.Now.AddDays(1);

		/// <summary>Returns 5-minute absolute expiration for short-lived caches.</summary>
		public DateTimeOffset CacheExpiration5Minutes() => DateTimeOffset.Now.AddMinutes(5);

		/// <summary>Creates a MemoryCacheEntryOptions with the default 1-day absolute expiration.</summary>
		private MemoryCacheEntryOptions DefaultOptions()
			=> new MemoryCacheEntryOptions().SetAbsoluteExpiration(DefaultCacheExpiration());

		/// <summary>Creates a MemoryCacheEntryOptions with a 5-minute absolute expiration.</summary>
		private MemoryCacheEntryOptions ShortOptions()
			=> new MemoryCacheEntryOptions().SetAbsoluteExpiration(CacheExpiration5Minutes());

		private void CacheSet(string key, object value, MemoryCacheEntryOptions opts = null)
		{
			_cacheKeys[key] = 0;
			_memoryCache.Set(key, value, opts ?? DefaultOptions());
		}

		private void CacheRemove(string key)
		{
			_cacheKeys.TryRemove(key, out _);
			_memoryCache.Remove(key);
		}

		/// <summary>Removes all cache keys that begin with the given prefix.</summary>
		private void CacheRemovePrefix(string prefix)
		{
			foreach (string key in _cacheKeys.Keys)
			{
				if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				{
					_cacheKeys.TryRemove(key, out _);
					_memoryCache.Remove(key);
				}
			}
		}

		// -----------------------------------------------------------------------
		// DbProviderFactories convenience
		// -----------------------------------------------------------------------
		private IDbConnection  NewConnection()   => _dbProviderFactories.CreateConnection();
		private DbDataAdapter  NewDataAdapter()  => _dbProviderFactories.CreateDataAdapter();

		// -----------------------------------------------------------------------
		// SchedulerJobs — well-known job names (SplendidCRM Community Edition)
		// -----------------------------------------------------------------------
		public static string[] SchedulerJobs()
		{
			return new string[]
			{
				"CleanSystemLog",
				"pruneDatabase",
				"BackupDatabase",
				"BackupTransactionLog",
				"CheckVersion",
				"RunAllArchiveRules",
				"RunExternalArchive"
			};
		}

		// -----------------------------------------------------------------------
		// DynamicButtonViews — view names for which dynamic buttons exist
		// -----------------------------------------------------------------------
		public static string[] DynamicButtonViews()
		{
			return new string[]
			{
				"DetailView", "EditView", "ListView",
				"SubpanelView", "PopupView", "SearchView",
				"SearchPopupView", "MassUpdateView"
			};
		}

		// -----------------------------------------------------------------------
		// AddReportSource — register an external custom cache callback
		// -----------------------------------------------------------------------
		public void AddReportSource(string sName, string sID, string sNAME, SplendidCacheCallback callback)
		{
			SplendidCacheReference existing = CustomCaches.Find(r => r.Name == sName);
			if (existing == null)
			{
				CustomCaches.Add(new SplendidCacheReference(sName, sID, sNAME, callback));
			}
			else
			{
				existing.DataSource = callback;
				if (!Sql.IsEmptyString(sID))   existing.ID   = sID;
				if (!Sql.IsEmptyString(sNAME)) existing.NAME = sNAME;
			}
		}

		// -----------------------------------------------------------------------
		// ClearTable — evict all cache entries for a given SQL view / table name
		// -----------------------------------------------------------------------
		public void ClearTable(string sTABLE_NAME)
		{
			if (Sql.IsEmptyString(sTABLE_NAME)) return;
			string upper = sTABLE_NAME.ToUpper();
			switch (upper)
			{
				case "MODULES"              : CacheRemovePrefix("vwMODULES"              ); break;
				case "TERMINOLOGY"          : CacheRemovePrefix("vwTERMINOLOGY"         ); break;
				case "TERMINOLOGY_PICKLISTS": CacheRemovePrefix("vwTERMINOLOGY_PICKLISTS"); break;
				case "GRIDVIEWS"            : CacheRemovePrefix("vwGRIDVIEWS"           ); break;
				case "DETAILVIEWS"          : CacheRemovePrefix("vwDETAILVIEWS"         ); break;
				case "EDITVIEWS"            : CacheRemovePrefix("vwEDITVIEWS"           ); break;
				case "DYNAMIC_BUTTONS"      : CacheRemovePrefix("vwDYNAMIC_BUTTONS"     ); break;
				case "CONFIG"               : CacheRemovePrefix("vwCONFIG"              );
				                              CacheRemovePrefix("CONFIG."               ); break;
				case "TIMEZONES"            : CacheRemove("vwTIMEZONES"                 ); break;
				case "CURRENCIES"           : CacheRemove("vwCURRENCIES"                ); break;
				case "LANGUAGES"            : ClearLanguages()                           ; break;
				case "ASSIGNED_USER_ID"     :
				case "USERS"                : ClearUsers()                               ; break;
				case "TEAMS"                : ClearTeams()                               ; break;
				case "DISCOUNTS"            : ClearDiscounts()                           ; break;
				case "TAXRATES"             :
				case "TAX_RATES"            : ClearTaxRates()                            ; break;
				case "EXCHANGE_FOLDERS"     : ClearExchangeFolders()                     ; break;
				case "GRIDVIEWS_COLUMNS"    :
				case "VWGRIDVIEWS_COLUMNS"  : ClearGridView(string.Empty)               ; break;
				case "DETAILVIEWS_FIELDS"   :
				case "VWDETAILVIEWS_FIELDS" : ClearDetailView(string.Empty)             ; break;
				case "EDITVIEWS_FIELDS"     :
				case "VWEDITVIEWS_FIELDS"   : ClearEditView(string.Empty)               ; break;
				case "DYNAMIC_BUTTONS_COLS" : ClearDynamicButtons(string.Empty)          ; break;
				case "EDITVIEWS_RELATIONSHIPS": ClearEditViewRelationships()              ; break;
				case "DETAILVIEWS_RELATIONSHIPS": ClearDetailViewRelationships()          ; break;
				case "MODULES_ARCHIVE"      : ClearArchiveViewExists()                   ; break;
				case "REPORTS"              : ClearReport()                               ; break;
				case "FILTERS"              : ClearFilterColumns()                        ; break;
				default:
					// Generic prefix-based invalidation for any other table
					CacheRemovePrefix(upper);
					CacheRemovePrefix("vw" + upper);
					break;
			}
		}

		// -----------------------------------------------------------------------
		// ClearSet — evict all cache entries for a list of table names
		// -----------------------------------------------------------------------
		public void ClearSet(IEnumerable<string> tableNames)
		{
			if (tableNames == null) return;
			foreach (string tbl in tableNames)
				ClearTable(tbl);
		}

		// -----------------------------------------------------------------------
		// SetListSource — update a CustomCaches entry's DataSource callback
		// -----------------------------------------------------------------------
		public void SetListSource(string sName, SplendidCacheCallback callback)
		{
			SplendidCacheReference entry = CustomCaches.Find(r => r.Name == sName);
			if (entry != null)
				entry.DataSource = callback;
		}

		// -----------------------------------------------------------------------
		// CustomList — return the DataTable for a named list (cached)
		// -----------------------------------------------------------------------
		public DataTable CustomList(string sLIST_NAME, string sCULTURE)
		{
			if (Sql.IsEmptyString(sLIST_NAME)) return new DataTable();
			string sCacheKey = "vwLIST." + (sCULTURE ?? "en-US") + "." + sLIST_NAME;
			DataTable dt = _memoryCache.Get<DataTable>(sCacheKey);
			if (dt != null) return dt;

			// Try explicit DataSource callback (externally registered via AddReportSource)
			SplendidCacheReference cache = CustomCaches.Find(r => r.Name == sLIST_NAME);
			if (cache?.DataSource != null)
			{
				dt = cache.DataSource();
			}
			else
			{
				// Resolve built-in list by name using instance method dispatch
				dt = InvokeBuiltinListCallback(sLIST_NAME);
			}

			if (dt != null)
				CacheSet(sCacheKey, dt);
			return dt ?? new DataTable();
		}

		/// <summary>Dispatch to the correct built-in list method by name.</summary>
		private DataTable InvokeBuiltinListCallback(string sLIST_NAME)
		{
			switch (sLIST_NAME)
			{
				case "AssignedUser"                        : return AssignedUser()            ;
				case "Currencies"                          : return Currencies()              ;
				case "Timezones"                           : return Timezones()               ;
				case "Languages"                           : return Languages()               ;
				case "Release"                             : return Release()                 ;
				case "Manufacturers"                       : return Manufacturers()           ;
				case "Discounts"                           : return Discounts()               ;
				case "Shippers"                            : return Shippers()                ;
				case "PaymentTypes"                        : return PaymentTypes()            ;
				case "PaymentTerms"                        : return PaymentTerms()            ;
				case "Regions"                             : return Regions()                 ;
				case "TaxRates"                            : return TaxRates()                ;
				case "ContractTypes"                       : return ContractTypes()           ;
				case "Themes"                              : return Themes()                  ;
				case "ActiveUsers"                         : return ActiveUsers()             ;
				case "AccessibleModules"                   : return AccessibleModules()       ;
				case "ReportingModules"                    : return ReportingModules()        ;
				case "WorkflowModules"                     : return WorkflowModules()         ;
				case "RulesModules"                        : return RulesModules()            ;
				case "CustomEditModules"                   : return CustomEditModules()       ;
				case "ImportModules"                       : return ImportModules()           ;
				case "ExchangeModulesSync"                 : return ExchangeModulesSync()     ;
				case "ModulesPopups"                       : return ModulesPopups()           ;
				case "ProductCategories"                   : return ProductCategories()       ;
				case "ProductTypes"                        : return ProductTypes()            ;
				case "AuditedModules"                      : return AuditedModules()          ;
				case "InboundEmailBounce"                  : return InboundEmailBounce()      ;
				case "InboundEmailMonitored"               : return InboundEmailMonitored()   ;
				default                                    : return null                      ;
			}
		}

		// -----------------------------------------------------------------------
		// CustomListValues — comma-separated display names for a list
		// -----------------------------------------------------------------------
		public string CustomListValues(string sLIST_NAME, string sCULTURE)
		{
			DataTable dt = CustomList(sLIST_NAME, sCULTURE);
			if (dt == null || dt.Rows.Count == 0) return string.Empty;
			var sb = new StringBuilder();
			// Determine the NAME column — fall back to first column
			string nameCol = dt.Columns.Contains("NAME")         ? "NAME"
			               : dt.Columns.Contains("DISPLAY_NAME") ? "DISPLAY_NAME"
			               : dt.Columns.Contains("FULL_NAME")    ? "FULL_NAME"
			               : dt.Columns[0].ColumnName;
			foreach (DataRow row in dt.Rows)
			{
				if (sb.Length > 0) sb.Append(", ");
				sb.Append(Sql.ToString(row[nameCol]));
			}
			return sb.ToString();
		}

		// -----------------------------------------------------------------------
		// ClearList — evict a single named custom list from cache
		// -----------------------------------------------------------------------
		public void ClearList(string sLIST_NAME, string sCULTURE = null)
		{
			if (Sql.IsEmptyString(sLIST_NAME)) return;
			if (!Sql.IsEmptyString(sCULTURE))
			{
				CacheRemove("vwLIST." + sCULTURE + "." + sLIST_NAME);
			}
			else
			{
				CacheRemovePrefix("vwLIST.");
			}
		}

		// -----------------------------------------------------------------------
		// List — return terminology list DataTable for a given list name/culture
		// Falls back to en-US when no culture row is found.
		// -----------------------------------------------------------------------
		public DataTable List(string sLIST_NAME)
		{
			string sCULTURE = SplendidDefaults.Culture();
			try
			{
				string sessVal = Session?.GetString("USER_SETTINGS/CULTURE");
				if (!Sql.IsEmptyString(sessVal)) sCULTURE = sessVal;
			}
			catch { /* no session active */ }
			return List(sLIST_NAME, sCULTURE);
		}

		public DataTable List(string sLIST_NAME, string sCULTURE)
		{
			if (Sql.IsEmptyString(sLIST_NAME)) return new DataTable();
			string sKey = "vwTERMINOLOGY." + sCULTURE + ".list." + sLIST_NAME;
			DataTable dt = _memoryCache.Get<DataTable>(sKey);
			if (dt != null) return dt;

			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select NAME                              " + ControlChars.CrLf
				  + "     , DISPLAY_NAME                     " + ControlChars.CrLf
				  + "  from vwTERMINOLOGY                    " + ControlChars.CrLf
				  + " where LIST_NAME = @LIST_NAME           " + ControlChars.CrLf
				  + "   and LANG     = @LANG                 " + ControlChars.CrLf
				  + " order by NAME                          " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@LIST_NAME", sLIST_NAME);
				Sql.AddParameter(cmd, "@LANG"     , sCULTURE  );
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}

			// Fall back to en-US if empty and culture is not en-US
			if ((dt == null || dt.Rows.Count == 0) && sCULTURE != "en-US")
				return List(sLIST_NAME, "en-US");

			CacheSet(sKey, dt ?? new DataTable());
			return dt ?? new DataTable();
		}

		// -----------------------------------------------------------------------
		// ClearUsers / ClearTeams
		// -----------------------------------------------------------------------
		public void ClearUsers()
		{
			CacheRemovePrefix("vwASSIGNED_USER_ID_List");
			CacheRemovePrefix("vwUSERS");
		}

		public void ClearTeams()
		{
			CacheRemovePrefix("vwTEAMS");
		}

		// -----------------------------------------------------------------------
		// AssignedUser — all active users (for assignment dropdowns)
		// -----------------------------------------------------------------------
		public DataTable AssignedUser()
		{
			const string CACHE_KEY = "vwASSIGNED_USER_ID_List";
			DataTable dt = _memoryCache.Get<DataTable>(CACHE_KEY);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select ID                               " + ControlChars.CrLf
				  + "     , FULL_NAME                       " + ControlChars.CrLf
				  + "  from vwUSERS_ASSIGNED_TO_List        " + ControlChars.CrLf
				  + " order by FULL_NAME                    " + ControlChars.CrLf;
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(CACHE_KEY, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// AssignedUser(Guid) — lookup a single user by ID
		public DataTable AssignedUser(Guid gID)
		{
			DataTable dtAll = AssignedUser();
			DataTable dt    = dtAll.Clone();
			foreach (DataRow row in dtAll.Rows)
			{
				if (Sql.ToGuid(row["ID"]) == gID)
				{
					dt.ImportRow(row);
					break;
				}
			}
			return dt;
		}

		// -----------------------------------------------------------------------
		// CustomEditModules — modules that have custom edit UIs
		// -----------------------------------------------------------------------
		public DataTable CustomEditModules()
		{
			const string CACHE_KEY = "vwMODULES_CustomEdit";
			DataTable dt = _memoryCache.Get<DataTable>(CACHE_KEY);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select MODULE_NAME                      " + ControlChars.CrLf
				  + "  from vwMODULES                       " + ControlChars.CrLf
				  + " where CUSTOM_EDIT = 1                 " + ControlChars.CrLf
				  + " order by MODULE_NAME                  " + ControlChars.CrLf;
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(CACHE_KEY, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// -----------------------------------------------------------------------
		// AccessibleModules — modules the current user can access
		// -----------------------------------------------------------------------
		public DataTable AccessibleModules()
		{
			if (!_security.IsAuthenticated()) return new DataTable();
			Guid gUSER_ID = _security.USER_ID;
			string sKey   = "vwMODULES_Accessible." + gUSER_ID.ToString();
			DataTable dt  = _memoryCache.Get<DataTable>(sKey);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select MODULE_NAME                      " + ControlChars.CrLf
				  + "     , DISPLAY_NAME                    " + ControlChars.CrLf
				  + "  from vwMODULES_Access_ByUser         " + ControlChars.CrLf
				  + " where USER_ID = @USER_ID              " + ControlChars.CrLf
				  + " order by DISPLAY_NAME                 " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@USER_ID", gUSER_ID);
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(sKey, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// AccessibleModulesTable — same data as a filtered DataTable (alias)
		public DataTable AccessibleModulesTable()
		{
			return AccessibleModules();
		}

		// -----------------------------------------------------------------------
		// ReportingModules / ReportingModulesList / RulesModules / WorkflowModules
		// -----------------------------------------------------------------------
		public DataTable ReportingModules()
		{
			const string CACHE_KEY = "vwMODULES_Reporting";
			DataTable dt = _memoryCache.Get<DataTable>(CACHE_KEY);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select MODULE_NAME                      " + ControlChars.CrLf
				  + "     , DISPLAY_NAME                    " + ControlChars.CrLf
				  + "  from vwMODULES                       " + ControlChars.CrLf
				  + " where IS_REPORT_MODULE = 1            " + ControlChars.CrLf
				  + " order by DISPLAY_NAME                 " + ControlChars.CrLf;
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(CACHE_KEY, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		public List<string> ReportingModulesList()
		{
			DataTable dt  = ReportingModules();
			var result    = new List<string>();
			foreach (DataRow row in dt.Rows)
				result.Add(Sql.ToString(row["MODULE_NAME"]));
			return result;
		}

		public DataTable RulesModules()
		{
			const string CACHE_KEY = "vwMODULES_Rules";
			DataTable dt = _memoryCache.Get<DataTable>(CACHE_KEY);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select MODULE_NAME                      " + ControlChars.CrLf
				  + "     , DISPLAY_NAME                    " + ControlChars.CrLf
				  + "  from vwMODULES                       " + ControlChars.CrLf
				  + " where IS_ADMIN = 0                    " + ControlChars.CrLf
				  + "   and (CUSTOM_EDIT = 0 or CUSTOM_EDIT is null)" + ControlChars.CrLf
				  + " order by DISPLAY_NAME                 " + ControlChars.CrLf;
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(CACHE_KEY, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		public DataTable WorkflowModules()
		{
			const string CACHE_KEY = "vwMODULES_Workflow";
			DataTable dt = _memoryCache.Get<DataTable>(CACHE_KEY);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select MODULE_NAME                      " + ControlChars.CrLf
				  + "     , DISPLAY_NAME                    " + ControlChars.CrLf
				  + "  from vwMODULES                       " + ControlChars.CrLf
				  + " where IS_WORKFLOW_MODULE = 1          " + ControlChars.CrLf
				  + " order by DISPLAY_NAME                 " + ControlChars.CrLf;
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(CACHE_KEY, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// -----------------------------------------------------------------------
		// WorkflowRelationships / GetAllRelationships
		// -----------------------------------------------------------------------
		public DataTable WorkflowRelationships(string sMODULE_NAME)
		{
			if (Sql.IsEmptyString(sMODULE_NAME)) return new DataTable();
			string sKey = "vwMODULES_RELATIONSHIPS.Workflow." + sMODULE_NAME;
			DataTable dt = _memoryCache.Get<DataTable>(sKey);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                " + ControlChars.CrLf
				  + "  from vwMODULES_RELATIONSHIPS         " + ControlChars.CrLf
				  + " where MODULE_NAME = @MODULE_NAME      " + ControlChars.CrLf
				  + " order by DISPLAY_NAME                 " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@MODULE_NAME", sMODULE_NAME);
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(sKey, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		public DataTable GetAllRelationships()
		{
			const string CACHE_KEY = "vwMODULES_RELATIONSHIPS.All";
			DataTable dt = _memoryCache.Get<DataTable>(CACHE_KEY);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                " + ControlChars.CrLf
				  + "  from vwMODULES_RELATIONSHIPS         " + ControlChars.CrLf
				  + " order by MODULE_NAME, DISPLAY_NAME    " + ControlChars.CrLf;
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(CACHE_KEY, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// -----------------------------------------------------------------------
		// ReportingRelationships / ReportingFilterColumns and variants
		// -----------------------------------------------------------------------
		public DataTable ReportingRelationships(string sMODULE_NAME)
		{
			if (Sql.IsEmptyString(sMODULE_NAME)) return new DataTable();
			string sKey = "vwMODULES_RELATIONSHIPS.Reporting." + sMODULE_NAME;
			DataTable dt = _memoryCache.Get<DataTable>(sKey);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                " + ControlChars.CrLf
				  + "  from vwMODULES_RELATIONSHIPS         " + ControlChars.CrLf
				  + " where MODULE_NAME = @MODULE_NAME      " + ControlChars.CrLf
				  + "   and IS_REPORT_MODULE = 1            " + ControlChars.CrLf
				  + " order by DISPLAY_NAME                 " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@MODULE_NAME", sMODULE_NAME);
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(sKey, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		public DataTable ReportingFilterColumns(string sMODULE_NAME)
		{
			if (Sql.IsEmptyString(sMODULE_NAME)) return new DataTable();
			string sKey = "vwMODULES_REPORT_FILTER_COLUMNS." + sMODULE_NAME;
			DataTable dt = _memoryCache.Get<DataTable>(sKey);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                " + ControlChars.CrLf
				  + "  from vwMODULES_REPORT_FILTER_COLUMNS " + ControlChars.CrLf
				  + " where MODULE_NAME = @MODULE_NAME      " + ControlChars.CrLf
				  + " order by COLUMN_NAME                  " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@MODULE_NAME", sMODULE_NAME);
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(sKey, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		public DataTable SurveyTargetColumns(string sMODULE_NAME)
		{
			if (Sql.IsEmptyString(sMODULE_NAME)) return new DataTable();
			string sKey = "vwMODULES_SURVEY_TARGET_COLUMNS." + sMODULE_NAME;
			DataTable dt = _memoryCache.Get<DataTable>(sKey);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                " + ControlChars.CrLf
				  + "  from vwMODULES_REPORT_FILTER_COLUMNS " + ControlChars.CrLf
				  + " where MODULE_NAME = @MODULE_NAME      " + ControlChars.CrLf
				  + "   and IS_SURVEY_TARGET = 1            " + ControlChars.CrLf
				  + " order by COLUMN_NAME                  " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@MODULE_NAME", sMODULE_NAME);
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(sKey, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		public DataTable WorkflowFilterColumns(string sMODULE_NAME)
		{
			if (Sql.IsEmptyString(sMODULE_NAME)) return new DataTable();
			string sKey = "vwMODULES_WORKFLOW_FILTER_COLUMNS." + sMODULE_NAME;
			DataTable dt = _memoryCache.Get<DataTable>(sKey);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                " + ControlChars.CrLf
				  + "  from vwMODULES_WORKFLOW_FILTER_COLUMNS" + ControlChars.CrLf
				  + " where MODULE_NAME = @MODULE_NAME      " + ControlChars.CrLf
				  + " order by COLUMN_NAME                  " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@MODULE_NAME", sMODULE_NAME);
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(sKey, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		public DataTable WorkflowFilterUpdateColumns(string sMODULE_NAME)
		{
			if (Sql.IsEmptyString(sMODULE_NAME)) return new DataTable();
			string sKey = "vwMODULES_WORKFLOW_FILTER_UPDATE_COLUMNS." + sMODULE_NAME;
			DataTable dt = _memoryCache.Get<DataTable>(sKey);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                    " + ControlChars.CrLf
				  + "  from vwMODULES_WORKFLOW_FILTER_UPDATE_COLUMNS" + ControlChars.CrLf
				  + " where MODULE_NAME = @MODULE_NAME          " + ControlChars.CrLf
				  + " order by COLUMN_NAME                      " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@MODULE_NAME", sMODULE_NAME);
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(sKey, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		public DataTable ReportingFilterColumnsListName(string sMODULE_NAME, string sLIST_NAME)
		{
			if (Sql.IsEmptyString(sMODULE_NAME) || Sql.IsEmptyString(sLIST_NAME)) return new DataTable();
			string sKey = "vwMODULES_REPORT_FILTER_COLUMNS.ListName." + sMODULE_NAME + "." + sLIST_NAME;
			DataTable dt = _memoryCache.Get<DataTable>(sKey);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                    " + ControlChars.CrLf
				  + "  from vwMODULES_REPORT_FILTER_COLUMNS     " + ControlChars.CrLf
				  + " where MODULE_NAME = @MODULE_NAME          " + ControlChars.CrLf
				  + "   and LIST_NAME   = @LIST_NAME            " + ControlChars.CrLf
				  + " order by COLUMN_NAME                      " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@MODULE_NAME", sMODULE_NAME);
				Sql.AddParameter(cmd, "@LIST_NAME"  , sLIST_NAME  );
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(sKey, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		public DataTable GetAllReportingFilterColumnsListName()
		{
			const string CACHE_KEY = "vwMODULES_REPORT_FILTER_COLUMNS.AllListNames";
			DataTable dt = _memoryCache.Get<DataTable>(CACHE_KEY);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select distinct MODULE_NAME, LIST_NAME    " + ControlChars.CrLf
				  + "  from vwMODULES_REPORT_FILTER_COLUMNS   " + ControlChars.CrLf
				  + " where LIST_NAME is not null              " + ControlChars.CrLf
				  + " order by MODULE_NAME, LIST_NAME          " + ControlChars.CrLf;
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(CACHE_KEY, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// -----------------------------------------------------------------------
		// ClearFilterColumns
		// -----------------------------------------------------------------------
		public void ClearFilterColumns()
		{
			CacheRemovePrefix("vwMODULES_REPORT_FILTER_COLUMNS");
			CacheRemovePrefix("vwMODULES_WORKFLOW_FILTER_COLUMNS");
			CacheRemovePrefix("vwMODULES_SURVEY_TARGET_COLUMNS");
			CacheRemovePrefix("vwMODULES_WORKFLOW_FILTER_UPDATE_COLUMNS");
		}

		// -----------------------------------------------------------------------
		// ImportColumns — column metadata for a module's import procedure
		// -----------------------------------------------------------------------
		public DataTable ImportColumns(string sMODULE_NAME)
		{
			if (Sql.IsEmptyString(sMODULE_NAME)) return new DataTable();
			string sKey = "vwIMPORT_COLUMNS." + sMODULE_NAME;
			DataTable dt = _memoryCache.Get<DataTable>(sKey);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				IDbCommand cmd = SqlProcs.Factory(con, "sp" + sMODULE_NAME + "_Import");
				if (cmd == null)
					cmd = SqlProcs.Factory(con, "sp" + sMODULE_NAME + "_Update");
				if (cmd == null) return new DataTable();
				dt = new DataTable();
				dt.Columns.Add("COLUMN_NAME", typeof(string));
				dt.Columns.Add("DATA_TYPE"  , typeof(string));
				dt.Columns.Add("PARAMETER"  , typeof(string));
				foreach (IDbDataParameter p in cmd.Parameters)
				{
					string sParam = p.ParameterName;
					if (sParam.StartsWith("@")) sParam = sParam.Substring(1);
					if (sParam.Equals("MODIFIED_USER_ID", StringComparison.OrdinalIgnoreCase)) continue;
					DataRow row = dt.NewRow();
					row["COLUMN_NAME"] = sParam;
					row["DATA_TYPE"  ] = p.DbType.ToString();
					row["PARAMETER"  ] = p.ParameterName;
					dt.Rows.Add(row);
				}
				CacheSet(sKey, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// -----------------------------------------------------------------------
		// ImportModules
		// -----------------------------------------------------------------------
		public DataTable ImportModules()
		{
			const string CACHE_KEY = "vwMODULES_Import";
			DataTable dt = _memoryCache.Get<DataTable>(CACHE_KEY);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select MODULE_NAME                      " + ControlChars.CrLf
				  + "     , DISPLAY_NAME                    " + ControlChars.CrLf
				  + "  from vwMODULES                       " + ControlChars.CrLf
				  + " where IS_IMPORT_MODULE = 1            " + ControlChars.CrLf
				  + " order by DISPLAY_NAME                 " + ControlChars.CrLf;
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(CACHE_KEY, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// -----------------------------------------------------------------------
		// Simple entity caches — Release, ProductCategories, ProductTypes,
		// Manufacturers, Discounts, ClearDiscounts, Shippers, PaymentTypes,
		// PaymentTerms, Regions, TaxRates, ClearTaxRates, ContractTypes
		// -----------------------------------------------------------------------
		private DataTable SimpleEntityCache(string cacheKey, string viewName,
			string idCol = "ID", string nameCol = "NAME", string orderCol = null)
		{
			DataTable dt = _memoryCache.Get<DataTable>(cacheKey);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				string order = orderCol ?? nameCol;
				cmd.CommandText =
					$"select {idCol}, {nameCol}              " + ControlChars.CrLf
				  +  $"  from {viewName}                     " + ControlChars.CrLf
				  +  $" order by {order}                     " + ControlChars.CrLf;
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(cacheKey, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		public DataTable Release()           => SimpleEntityCache("vwRELEASES"            , "vwRELEASES"           );
		public DataTable ProductCategories() => SimpleEntityCache("vwPRODUCT_CATEGORIES"  , "vwPRODUCT_CATEGORIES" );
		public DataTable ProductTypes()      => SimpleEntityCache("vwPRODUCT_TYPES"        , "vwPRODUCT_TYPES"      );
		public DataTable Manufacturers()     => SimpleEntityCache("vwMANUFACTURERS"        , "vwMANUFACTURERS"      );
		public DataTable Shippers()          => SimpleEntityCache("vwSHIPPERS"             , "vwSHIPPERS"           );
		public DataTable PaymentTypes()      => SimpleEntityCache("vwPAYMENT_TYPES"        , "vwPAYMENT_TYPES"      );
		public DataTable PaymentTerms()      => SimpleEntityCache("vwPAYMENT_TERMS"        , "vwPAYMENT_TERMS"      );
		public DataTable Regions()           => SimpleEntityCache("vwREGIONS"              , "vwREGIONS"            );
		public DataTable ContractTypes()     => SimpleEntityCache("vwCONTRACT_TYPES"       , "vwCONTRACT_TYPES"     );

		public DataTable Discounts()
		{
			const string CACHE_KEY = "vwDISCOUNTS";
			DataTable dt = _memoryCache.Get<DataTable>(CACHE_KEY);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select ID                               " + ControlChars.CrLf
				  + "     , NAME                            " + ControlChars.CrLf
				  + "     , DISCOUNT_TYPE                   " + ControlChars.CrLf
				  + "     , DISCOUNT_AMOUNT                 " + ControlChars.CrLf
				  + "  from vwDISCOUNTS                     " + ControlChars.CrLf
				  + " order by NAME                         " + ControlChars.CrLf;
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(CACHE_KEY, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		public void ClearDiscounts()
		{
			CacheRemove("vwDISCOUNTS");
		}

		public DataTable TaxRates()
		{
			const string CACHE_KEY = "vwTAX_RATES";
			DataTable dt = _memoryCache.Get<DataTable>(CACHE_KEY);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select ID                               " + ControlChars.CrLf
				  + "     , NAME                            " + ControlChars.CrLf
				  + "     , VALUE                           " + ControlChars.CrLf
				  + "  from vwTAX_RATES                     " + ControlChars.CrLf
				  + " order by NAME                         " + ControlChars.CrLf;
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(CACHE_KEY, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		public void ClearTaxRates()
		{
			CacheRemove("vwTAX_RATES");
		}

		// -----------------------------------------------------------------------
		// Currencies
		// -----------------------------------------------------------------------
		public DataTable Currencies()
		{
			const string CACHE_KEY = "vwCURRENCIES";
			DataTable dt = _memoryCache.Get<DataTable>(CACHE_KEY);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select ID                               " + ControlChars.CrLf
				  + "     , NAME                            " + ControlChars.CrLf
				  + "     , SYMBOL                          " + ControlChars.CrLf
				  + "     , ISO_CODE                        " + ControlChars.CrLf
				  + "     , CONVERSION_RATE                 " + ControlChars.CrLf
				  + "     , STATUS                          " + ControlChars.CrLf
				  + "  from vwCURRENCIES                    " + ControlChars.CrLf
				  + " where STATUS = 'Active'               " + ControlChars.CrLf
				  + " order by NAME                         " + ControlChars.CrLf;
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(CACHE_KEY, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// -----------------------------------------------------------------------
		// Timezones / TimezonesListbox
		// -----------------------------------------------------------------------
		public DataTable Timezones()
		{
			const string CACHE_KEY = "vwTIMEZONES";
			DataTable dt = _memoryCache.Get<DataTable>(CACHE_KEY);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select ID                               " + ControlChars.CrLf
				  + "     , NAME                            " + ControlChars.CrLf
				  + "     , OFFSET                          " + ControlChars.CrLf
				  + "     , ISDST                           " + ControlChars.CrLf
				  + "  from vwTIMEZONES                     " + ControlChars.CrLf
				  + " order by OFFSET, NAME                 " + ControlChars.CrLf;
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(CACHE_KEY, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		/// <summary>Returns timezones formatted for a listbox (ID, display name pair).</summary>
		public DataTable TimezonesListbox()
		{
			const string CACHE_KEY = "vwTIMEZONES_Listbox";
			DataTable dt = _memoryCache.Get<DataTable>(CACHE_KEY);
			if (dt != null) return dt;
			DataTable dtTZ = Timezones();
			dt = new DataTable();
			dt.Columns.Add("ID"  , typeof(string));
			dt.Columns.Add("NAME", typeof(string));
			foreach (DataRow row in dtTZ.Rows)
			{
				DataRow newRow = dt.NewRow();
				newRow["ID"  ] = Sql.ToString (row["ID"    ]);
				decimal offset = Sql.ToDecimal(row["OFFSET"]);
				string sSign   = offset >= 0 ? "+" : string.Empty;
				newRow["NAME"] = $"({sSign}{offset}) {Sql.ToString(row["NAME"])}";
				dt.Rows.Add(newRow);
			}
			CacheSet(CACHE_KEY, dt);
			return dt;
		}

		// -----------------------------------------------------------------------
		// Languages / ClearLanguages
		// -----------------------------------------------------------------------
		public void ClearLanguages()
		{
			CacheRemovePrefix("vwLANGUAGES");
		}

		public DataTable Languages()
		{
			const string CACHE_KEY = "vwLANGUAGES";
			DataTable dt = _memoryCache.Get<DataTable>(CACHE_KEY);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select NAME                             " + ControlChars.CrLf
				  + "     , DISPLAY_NAME                    " + ControlChars.CrLf
				  + "  from vwLANGUAGES                     " + ControlChars.CrLf
				  + " order by DISPLAY_NAME                 " + ControlChars.CrLf;
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(CACHE_KEY, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// -----------------------------------------------------------------------
		// ModulesPopups — modules that expose popup pickers
		// -----------------------------------------------------------------------
		public DataTable ModulesPopups()
		{
			const string CACHE_KEY = "vwMODULES_Popups";
			DataTable dt = _memoryCache.Get<DataTable>(CACHE_KEY);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select MODULE_NAME                      " + ControlChars.CrLf
				  + "     , DISPLAY_NAME                    " + ControlChars.CrLf
				  + "  from vwMODULES                       " + ControlChars.CrLf
				  + " where IS_POPUP = 1                    " + ControlChars.CrLf
				  + " order by DISPLAY_NAME                 " + ControlChars.CrLf;
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(CACHE_KEY, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// -----------------------------------------------------------------------
		// Exchange integration caches
		// -----------------------------------------------------------------------
		public DataTable ExchangeModulesSync()
		{
			const string CACHE_KEY = "vwMODULES_ExchangeSync";
			DataTable dt = _memoryCache.Get<DataTable>(CACHE_KEY);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select MODULE_NAME                      " + ControlChars.CrLf
				  + "     , DISPLAY_NAME                    " + ControlChars.CrLf
				  + "  from vwMODULES                       " + ControlChars.CrLf
				  + " where IS_EXCHANGE_MODULE = 1          " + ControlChars.CrLf
				  + " order by DISPLAY_NAME                 " + ControlChars.CrLf;
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(CACHE_KEY, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		public DateTimeOffset ExchangeFolderCacheExpiration()
			=> DateTimeOffset.Now.AddMinutes(15);

		public void ClearExchangeFolders()
		{
			CacheRemovePrefix("vwEXCHANGE_FOLDERS");
		}

		public DataTable ExchangeFolders(Guid gUSER_ID)
		{
			if (gUSER_ID == Guid.Empty) return new DataTable();
			string sKey = "vwEXCHANGE_FOLDERS." + gUSER_ID.ToString();
			DataTable dt = _memoryCache.Get<DataTable>(sKey);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                " + ControlChars.CrLf
				  + "  from vwEXCHANGE_FOLDERS              " + ControlChars.CrLf
				  + " where USER_ID = @USER_ID              " + ControlChars.CrLf
				  + " order by NAME                         " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@USER_ID", gUSER_ID);
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(sKey, dt, new MemoryCacheEntryOptions()
					.SetAbsoluteExpiration(ExchangeFolderCacheExpiration()));
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// Overload without parameter — uses current user
		public DataTable ExchangeFolders()
		{
			return ExchangeFolders(_security.USER_ID);
		}

		// -----------------------------------------------------------------------
		// ActiveUsers
		// -----------------------------------------------------------------------
		public DataTable ActiveUsers()
		{
			const string CACHE_KEY = "vwUSERS_ACTIVE";
			DataTable dt = _memoryCache.Get<DataTable>(CACHE_KEY);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select ID                               " + ControlChars.CrLf
				  + "     , FULL_NAME                       " + ControlChars.CrLf
				  + "     , EMAIL1                          " + ControlChars.CrLf
				  + "  from vwUSERS_Active                  " + ControlChars.CrLf
				  + " order by FULL_NAME                    " + ControlChars.CrLf;
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(CACHE_KEY, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// -----------------------------------------------------------------------
		// Themes
		// -----------------------------------------------------------------------
		public DataTable Themes()
		{
			const string CACHE_KEY = "vwTHEMES";
			DataTable dt = _memoryCache.Get<DataTable>(CACHE_KEY);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select NAME                             " + ControlChars.CrLf
				  + "  from vwTHEMES                        " + ControlChars.CrLf
				  + " order by NAME                         " + ControlChars.CrLf;
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(CACHE_KEY, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// -----------------------------------------------------------------------
		// XmlFile — cached XML content (e.g., RDL definitions)
		// -----------------------------------------------------------------------
		public string XmlFile(string sFileName)
		{
			if (Sql.IsEmptyString(sFileName)) return string.Empty;
			string sKey = "XmlFile." + sFileName;
			string content = _memoryCache.Get<string>(sKey);
			if (content != null) return content;
			try
			{
				if (System.IO.File.Exists(sFileName))
				{
					content = System.IO.File.ReadAllText(sFileName);
					CacheSet(sKey, content);
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				content = string.Empty;
			}
			return content ?? string.Empty;
		}

		// -----------------------------------------------------------------------
		// GridView caches
		// -----------------------------------------------------------------------
		public void ClearGridView(string sVIEW_NAME)
		{
			if (Sql.IsEmptyString(sVIEW_NAME))
				CacheRemovePrefix("vwGRIDVIEWS_COLUMNS");
			else
				CacheRemovePrefix("vwGRIDVIEWS_COLUMNS." + sVIEW_NAME);
		}

		public DataTable GridViewColumns(string sGRIDVIEW_NAME)
		{
			return GridViewColumns(sGRIDVIEW_NAME, string.Empty);
		}

		public DataTable GridViewColumns(string sGRIDVIEW_NAME, string sROLE_NAME)
		{
			if (Sql.IsEmptyString(sGRIDVIEW_NAME)) return new DataTable();
			string sKey = "vwGRIDVIEWS_COLUMNS." + sGRIDVIEW_NAME
			            + (Sql.IsEmptyString(sROLE_NAME) ? string.Empty : "." + sROLE_NAME);
			DataTable dt = _memoryCache.Get<DataTable>(sKey);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				if (Sql.IsEmptyString(sROLE_NAME))
				{
					cmd.CommandText =
						"select *                                " + ControlChars.CrLf
					  + "  from vwGRIDVIEWS_COLUMNS             " + ControlChars.CrLf
					  + " where GRIDVIEW_NAME = @GRIDVIEW_NAME  " + ControlChars.CrLf
					  + "   and ROLE_NAME is null               " + ControlChars.CrLf
					  + " order by COLUMN_INDEX                 " + ControlChars.CrLf;
					Sql.AddParameter(cmd, "@GRIDVIEW_NAME", sGRIDVIEW_NAME);
				}
				else
				{
					cmd.CommandText =
						"select *                                " + ControlChars.CrLf
					  + "  from vwGRIDVIEWS_COLUMNS             " + ControlChars.CrLf
					  + " where GRIDVIEW_NAME = @GRIDVIEW_NAME  " + ControlChars.CrLf
					  + "   and ROLE_NAME     = @ROLE_NAME      " + ControlChars.CrLf
					  + " order by COLUMN_INDEX                 " + ControlChars.CrLf;
					Sql.AddParameter(cmd, "@GRIDVIEW_NAME", sGRIDVIEW_NAME);
					Sql.AddParameter(cmd, "@ROLE_NAME"    , sROLE_NAME    );
				}
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(sKey, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// -----------------------------------------------------------------------
		// GridViewRules / BusinessRules / ReportRules
		// -----------------------------------------------------------------------
		public void ClearBusinessRules()
		{
			CacheRemovePrefix("vwBUSINESS_RULES");
		}

		public void ClearReportRules()
		{
			CacheRemovePrefix("vwREPORT_RULES");
		}

		public DataTable GridViewRules(string sGRIDVIEW_NAME)
		{
			if (Sql.IsEmptyString(sGRIDVIEW_NAME)) return new DataTable();
			string sKey = "vwGRIDVIEWS_RULES." + sGRIDVIEW_NAME;
			DataTable dt = _memoryCache.Get<DataTable>(sKey);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                " + ControlChars.CrLf
				  + "  from vwGRIDVIEWS_RULES               " + ControlChars.CrLf
				  + " where GRIDVIEW_NAME = @GRIDVIEW_NAME  " + ControlChars.CrLf
				  + " order by RULE_NAME                    " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@GRIDVIEW_NAME", sGRIDVIEW_NAME);
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(sKey, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		public DataTable ReportRules(string sMODULE_NAME)
		{
			if (Sql.IsEmptyString(sMODULE_NAME)) return new DataTable();
			string sKey = "vwREPORT_RULES." + sMODULE_NAME;
			DataTable dt = _memoryCache.Get<DataTable>(sKey);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                " + ControlChars.CrLf
				  + "  from vwREPORT_RULES                  " + ControlChars.CrLf
				  + " where MODULE_NAME = @MODULE_NAME      " + ControlChars.CrLf
				  + " order by RULE_NAME                    " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@MODULE_NAME", sMODULE_NAME);
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(sKey, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// -----------------------------------------------------------------------
		// DetailView caches
		// -----------------------------------------------------------------------
		public void ClearDetailView(string sVIEW_NAME)
		{
			if (Sql.IsEmptyString(sVIEW_NAME))
				CacheRemovePrefix("vwDETAILVIEWS_FIELDS");
			else
				CacheRemovePrefix("vwDETAILVIEWS_FIELDS." + sVIEW_NAME);
		}

		public DataTable DetailViewFields(string sDETAIL_NAME)
		{
			return DetailViewFields(sDETAIL_NAME, string.Empty);
		}

		public DataTable DetailViewFields(string sDETAIL_NAME, string sROLE_NAME)
		{
			if (Sql.IsEmptyString(sDETAIL_NAME)) return new DataTable();
			string sKey = "vwDETAILVIEWS_FIELDS." + sDETAIL_NAME
			            + (Sql.IsEmptyString(sROLE_NAME) ? string.Empty : "." + sROLE_NAME);
			DataTable dt = _memoryCache.Get<DataTable>(sKey);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				if (Sql.IsEmptyString(sROLE_NAME))
				{
					cmd.CommandText =
						"select *                                " + ControlChars.CrLf
					  + "  from vwDETAILVIEWS_FIELDS            " + ControlChars.CrLf
					  + " where DETAIL_NAME = @DETAIL_NAME      " + ControlChars.CrLf
					  + "   and ROLE_NAME is null               " + ControlChars.CrLf
					  + " order by FIELD_INDEX                  " + ControlChars.CrLf;
					Sql.AddParameter(cmd, "@DETAIL_NAME", sDETAIL_NAME);
				}
				else
				{
					cmd.CommandText =
						"select *                                " + ControlChars.CrLf
					  + "  from vwDETAILVIEWS_FIELDS            " + ControlChars.CrLf
					  + " where DETAIL_NAME = @DETAIL_NAME      " + ControlChars.CrLf
					  + "   and ROLE_NAME   = @ROLE_NAME        " + ControlChars.CrLf
					  + " order by FIELD_INDEX                  " + ControlChars.CrLf;
					Sql.AddParameter(cmd, "@DETAIL_NAME", sDETAIL_NAME);
					Sql.AddParameter(cmd, "@ROLE_NAME"  , sROLE_NAME  );
				}
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(sKey, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// -----------------------------------------------------------------------
		// DetailViewRules
		// -----------------------------------------------------------------------
		public DataTable DetailViewRules(string sDETAIL_NAME)
		{
			return DetailViewRules(sDETAIL_NAME, string.Empty);
		}

		public DataTable DetailViewRules(string sDETAIL_NAME, string sROLE_NAME)
		{
			if (Sql.IsEmptyString(sDETAIL_NAME)) return new DataTable();
			string sKey = "vwDETAILVIEWS_RULES." + sDETAIL_NAME
			            + (Sql.IsEmptyString(sROLE_NAME) ? string.Empty : "." + sROLE_NAME);
			DataTable dt = _memoryCache.Get<DataTable>(sKey);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				if (Sql.IsEmptyString(sROLE_NAME))
				{
					cmd.CommandText =
						"select *                                " + ControlChars.CrLf
					  + "  from vwDETAILVIEWS_RULES             " + ControlChars.CrLf
					  + " where DETAIL_NAME = @DETAIL_NAME      " + ControlChars.CrLf
					  + "   and ROLE_NAME is null               " + ControlChars.CrLf
					  + " order by RULE_NAME                    " + ControlChars.CrLf;
					Sql.AddParameter(cmd, "@DETAIL_NAME", sDETAIL_NAME);
				}
				else
				{
					cmd.CommandText =
						"select *                                " + ControlChars.CrLf
					  + "  from vwDETAILVIEWS_RULES             " + ControlChars.CrLf
					  + " where DETAIL_NAME = @DETAIL_NAME      " + ControlChars.CrLf
					  + "   and ROLE_NAME   = @ROLE_NAME        " + ControlChars.CrLf
					  + " order by RULE_NAME                    " + ControlChars.CrLf;
					Sql.AddParameter(cmd, "@DETAIL_NAME", sDETAIL_NAME);
					Sql.AddParameter(cmd, "@ROLE_NAME"  , sROLE_NAME  );
				}
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(sKey, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// -----------------------------------------------------------------------
		// TabGroups / ModuleGroups / ModuleGroupsByUser
		// -----------------------------------------------------------------------
		public DataTable TabGroups()
		{
			const string CACHE_KEY = "vwMODULES_TAB_GROUPS";
			DataTable dt = _memoryCache.Get<DataTable>(CACHE_KEY);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                " + ControlChars.CrLf
				  + "  from vwMODULES_TAB_GROUPS            " + ControlChars.CrLf
				  + " order by GROUP_NAME                   " + ControlChars.CrLf;
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(CACHE_KEY, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		public DataTable ModuleGroups()
		{
			const string CACHE_KEY = "vwMODULES_GROUPS";
			DataTable dt = _memoryCache.Get<DataTable>(CACHE_KEY);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                " + ControlChars.CrLf
				  + "  from vwMODULES_GROUPS                " + ControlChars.CrLf
				  + " order by GROUP_NAME                   " + ControlChars.CrLf;
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(CACHE_KEY, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		public DataTable ModuleGroupsByUser(Guid gUSER_ID)
		{
			string sKey = "vwMODULES_GROUPS_ByUser." + gUSER_ID.ToString();
			DataTable dt = _memoryCache.Get<DataTable>(sKey);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                " + ControlChars.CrLf
				  + "  from vwMODULES_GROUPS                " + ControlChars.CrLf
				  + " where USER_ID = @USER_ID              " + ControlChars.CrLf
				  + "    or USER_ID is null                 " + ControlChars.CrLf
				  + " order by GROUP_NAME                   " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@USER_ID", gUSER_ID);
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(sKey, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// Overload using current user
		public DataTable ModuleGroupsByUser()
		{
			return ModuleGroupsByUser(_security.USER_ID);
		}

		// -----------------------------------------------------------------------
		// DetailViewRelationships / EditViewRelationships
		// -----------------------------------------------------------------------
		public void ClearDetailViewRelationships()
		{
			CacheRemovePrefix("vwDETAILVIEWS_RELATIONSHIPS");
		}

		public DataTable DetailViewRelationships(string sDETAIL_NAME)
		{
			if (Sql.IsEmptyString(sDETAIL_NAME)) return new DataTable();
			string sKey = "vwDETAILVIEWS_RELATIONSHIPS." + sDETAIL_NAME;
			DataTable dt = _memoryCache.Get<DataTable>(sKey);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                " + ControlChars.CrLf
				  + "  from vwDETAILVIEWS_RELATIONSHIPS     " + ControlChars.CrLf
				  + " where DETAIL_NAME = @DETAIL_NAME      " + ControlChars.CrLf
				  + " order by RELATIONSHIP_ORDER           " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@DETAIL_NAME", sDETAIL_NAME);
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(sKey, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		public void ClearEditViewRelationships()
		{
			CacheRemovePrefix("vwEDITVIEWS_RELATIONSHIPS");
		}

		public DataTable EditViewRelationships(string sEDIT_NAME)
		{
			return EditViewRelationships(sEDIT_NAME, false);
		}

		public DataTable EditViewRelationships(string sEDIT_NAME, bool bNewRecord)
		{
			if (Sql.IsEmptyString(sEDIT_NAME)) return new DataTable();
			string sKey = "vwEDITVIEWS_RELATIONSHIPS." + sEDIT_NAME + (bNewRecord ? ".New" : ".Existing");
			DataTable dt = _memoryCache.Get<DataTable>(sKey);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                " + ControlChars.CrLf
				  + "  from vwEDITVIEWS_RELATIONSHIPS       " + ControlChars.CrLf
				  + " where EDIT_NAME  = @EDIT_NAME         " + ControlChars.CrLf
				  + "   and NEW_RECORD = @NEW_RECORD         " + ControlChars.CrLf
				  + " order by RELATIONSHIP_ORDER           " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@EDIT_NAME"  , sEDIT_NAME );
				Sql.AddParameter(cmd, "@NEW_RECORD" , bNewRecord );
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(sKey, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// -----------------------------------------------------------------------
		// UserDashlets — session-stored per user/detail view
		// -----------------------------------------------------------------------
		public void ClearUserDashlets()
		{
			RemoveSessionObject("vwDASHLETS_USERS");
			RemoveSessionObject("vwDASHLETS_USERS_ALL");
		}

		public DataTable UserDashlets(string sDETAIL_NAME, Guid gUSER_ID)
		{
			if (Sql.IsEmptyString(sDETAIL_NAME) || gUSER_ID == Guid.Empty)
				return new DataTable();
			string sSessionKey = "vwDASHLETS_USERS." + gUSER_ID.ToString() + "." + sDETAIL_NAME;
			DataTable dt = GetSessionObject<DataTable>(sSessionKey);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                " + ControlChars.CrLf
				  + "  from vwDASHLETS_USERS                " + ControlChars.CrLf
				  + " where USER_ID     = @USER_ID          " + ControlChars.CrLf
				  + "   and DETAIL_NAME = @DETAIL_NAME      " + ControlChars.CrLf
				  + " order by CELL_INDEX, COLUMN_INDEX     " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@USER_ID"    , gUSER_ID   );
				Sql.AddParameter(cmd, "@DETAIL_NAME", sDETAIL_NAME);
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				SetSessionObject(sSessionKey, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// -----------------------------------------------------------------------
		// EditView caches
		// -----------------------------------------------------------------------
		public void ClearEditView(string sVIEW_NAME)
		{
			if (Sql.IsEmptyString(sVIEW_NAME))
				CacheRemovePrefix("vwEDITVIEWS_FIELDS");
			else
				CacheRemovePrefix("vwEDITVIEWS_FIELDS." + sVIEW_NAME);
		}

		// Overload — clear all
		public void ClearEditView()
		{
			ClearEditView(string.Empty);
		}

		public DataTable EditViewFields(string sEDIT_NAME)
		{
			return EditViewFields(sEDIT_NAME, string.Empty);
		}

		public DataTable EditViewFields(string sEDIT_NAME, string sROLE_NAME)
		{
			return EditViewFields(sEDIT_NAME, sROLE_NAME, false);
		}

		public DataTable EditViewFields(string sEDIT_NAME, bool bSearchView)
		{
			return EditViewFields(sEDIT_NAME, string.Empty, bSearchView);
		}

		public DataTable EditViewFields(string sEDIT_NAME, string sROLE_NAME, bool bSearchView)
		{
			if (Sql.IsEmptyString(sEDIT_NAME)) return new DataTable();
			string sKey = "vwEDITVIEWS_FIELDS." + sEDIT_NAME
			            + (Sql.IsEmptyString(sROLE_NAME) ? string.Empty : "." + sROLE_NAME)
			            + (bSearchView ? ".Search" : string.Empty);
			DataTable dt = _memoryCache.Get<DataTable>(sKey);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				var sb = new StringBuilder();
				sb.Append("select *                                " + ControlChars.CrLf);
				sb.Append("  from vwEDITVIEWS_FIELDS              " + ControlChars.CrLf);
				sb.Append(" where EDIT_NAME = @EDIT_NAME          " + ControlChars.CrLf);
				if (Sql.IsEmptyString(sROLE_NAME))
					sb.Append("   and ROLE_NAME is null               " + ControlChars.CrLf);
				else
					sb.Append("   and ROLE_NAME = @ROLE_NAME          " + ControlChars.CrLf);
				sb.Append(" order by FIELD_INDEX                  " + ControlChars.CrLf);
				cmd.CommandText = sb.ToString();
				Sql.AddParameter(cmd, "@EDIT_NAME", sEDIT_NAME);
				if (!Sql.IsEmptyString(sROLE_NAME))
					Sql.AddParameter(cmd, "@ROLE_NAME", sROLE_NAME);
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(sKey, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// -----------------------------------------------------------------------
		// EditViewRules
		// -----------------------------------------------------------------------
		public void ClearEditViewRules()
		{
			CacheRemovePrefix("vwEDITVIEWS_RULES");
		}

		public DataTable EditViewRules(string sEDIT_NAME)
		{
			return EditViewRules(sEDIT_NAME, string.Empty);
		}

		public DataTable EditViewRules(string sEDIT_NAME, string sROLE_NAME)
		{
			if (Sql.IsEmptyString(sEDIT_NAME)) return new DataTable();
			string sKey = "vwEDITVIEWS_RULES." + sEDIT_NAME
			            + (Sql.IsEmptyString(sROLE_NAME) ? string.Empty : "." + sROLE_NAME);
			DataTable dt = _memoryCache.Get<DataTable>(sKey);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				if (Sql.IsEmptyString(sROLE_NAME))
				{
					cmd.CommandText =
						"select *                                " + ControlChars.CrLf
					  + "  from vwEDITVIEWS_RULES               " + ControlChars.CrLf
					  + " where EDIT_NAME  = @EDIT_NAME         " + ControlChars.CrLf
					  + "   and ROLE_NAME is null               " + ControlChars.CrLf
					  + " order by RULE_NAME                    " + ControlChars.CrLf;
					Sql.AddParameter(cmd, "@EDIT_NAME", sEDIT_NAME);
				}
				else
				{
					cmd.CommandText =
						"select *                                " + ControlChars.CrLf
					  + "  from vwEDITVIEWS_RULES               " + ControlChars.CrLf
					  + " where EDIT_NAME  = @EDIT_NAME         " + ControlChars.CrLf
					  + "   and ROLE_NAME  = @ROLE_NAME         " + ControlChars.CrLf
					  + " order by RULE_NAME                    " + ControlChars.CrLf;
					Sql.AddParameter(cmd, "@EDIT_NAME" , sEDIT_NAME);
					Sql.AddParameter(cmd, "@ROLE_NAME" , sROLE_NAME);
				}
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(sKey, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// -----------------------------------------------------------------------
		// DynamicButtons
		// -----------------------------------------------------------------------
		public void ClearDynamicButtons(string sVIEW_NAME)
		{
			if (Sql.IsEmptyString(sVIEW_NAME))
				CacheRemovePrefix("vwDYNAMIC_BUTTONS");
			else
				CacheRemovePrefix("vwDYNAMIC_BUTTONS." + sVIEW_NAME);
		}

		// Overload — clear all
		public void ClearDynamicButtons()
		{
			ClearDynamicButtons(string.Empty);
		}

		public DataTable DynamicButtons(string sVIEW_NAME)
		{
			return DynamicButtons(sVIEW_NAME, string.Empty);
		}

		public DataTable DynamicButtons(string sVIEW_NAME, string sROLE_NAME)
		{
			if (Sql.IsEmptyString(sVIEW_NAME)) return new DataTable();
			string sKey = "vwDYNAMIC_BUTTONS." + sVIEW_NAME
			            + (Sql.IsEmptyString(sROLE_NAME) ? string.Empty : "." + sROLE_NAME);
			DataTable dt = _memoryCache.Get<DataTable>(sKey);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				if (Sql.IsEmptyString(sROLE_NAME))
				{
					cmd.CommandText =
						"select *                                " + ControlChars.CrLf
					  + "  from vwDYNAMIC_BUTTONS               " + ControlChars.CrLf
					  + " where VIEW_NAME  = @VIEW_NAME         " + ControlChars.CrLf
					  + "   and ROLE_NAME is null               " + ControlChars.CrLf
					  + " order by BUTTON_ORDER                 " + ControlChars.CrLf;
					Sql.AddParameter(cmd, "@VIEW_NAME", sVIEW_NAME);
				}
				else
				{
					cmd.CommandText =
						"select *                                " + ControlChars.CrLf
					  + "  from vwDYNAMIC_BUTTONS               " + ControlChars.CrLf
					  + " where VIEW_NAME  = @VIEW_NAME         " + ControlChars.CrLf
					  + "   and ROLE_NAME  = @ROLE_NAME         " + ControlChars.CrLf
					  + " order by BUTTON_ORDER                 " + ControlChars.CrLf;
					Sql.AddParameter(cmd, "@VIEW_NAME", sVIEW_NAME);
					Sql.AddParameter(cmd, "@ROLE_NAME", sROLE_NAME);
				}
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(sKey, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// -----------------------------------------------------------------------
		// TabMenu — session-stored navigation menu for current user
		// -----------------------------------------------------------------------
		public void ClearTabMenu()
		{
			RemoveSessionObject("vwMODULES_TabMenu");
		}

		public DataTable TabMenu()
		{
			if (!_security.IsAuthenticated()) return new DataTable();
			Guid gUSER_ID   = _security.USER_ID;
			string sSession = "vwMODULES_TabMenu." + gUSER_ID.ToString();
			DataTable dt    = GetSessionObject<DataTable>(sSession);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                " + ControlChars.CrLf
				  + "  from vwMODULES_TabMenu               " + ControlChars.CrLf
				  + " where USER_ID = @USER_ID              " + ControlChars.CrLf
				  + " order by TAB_ORDER                    " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@USER_ID", gUSER_ID);
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				SetSessionObject(sSession, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		/// <summary>Returns all tab menus keyed by user ID (admin view).</summary>
		public Dictionary<Guid, DataTable> GetAllTabMenus()
		{
			const string CACHE_KEY = "vwMODULES_TabMenu.ALL";
			var dict = _memoryCache.Get<Dictionary<Guid, DataTable>>(CACHE_KEY);
			if (dict != null) return dict;
			dict = new Dictionary<Guid, DataTable>();
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                " + ControlChars.CrLf
				  + "  from vwMODULES_TabMenu               " + ControlChars.CrLf
				  + " order by USER_ID, TAB_ORDER           " + ControlChars.CrLf;
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				var dtAll = new DataTable();
				da.Fill(dtAll);
				foreach (DataRow row in dtAll.Rows)
				{
					Guid gUser = Sql.ToGuid(row["USER_ID"]);
					if (!dict.ContainsKey(gUser))
						dict[gUser] = dtAll.Clone();
					dict[gUser].ImportRow(row);
				}
				CacheSet(CACHE_KEY, dict);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
			return dict;
		}

		// -----------------------------------------------------------------------
		// TabFeeds — RSS feed subscriptions for current user's tab menu
		// -----------------------------------------------------------------------
		public DataTable TabFeeds()
		{
			if (!_security.IsAuthenticated()) return new DataTable();
			Guid gUSER_ID  = _security.USER_ID;
			string sSession = "vwMODULES_TabFeeds." + gUSER_ID.ToString();
			DataTable dt    = GetSessionObject<DataTable>(sSession);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                " + ControlChars.CrLf
				  + "  from vwMODULES_TabFeeds              " + ControlChars.CrLf
				  + " where USER_ID = @USER_ID              " + ControlChars.CrLf
				  + " order by TAB_ORDER                    " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@USER_ID", gUSER_ID);
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				SetSessionObject(sSession, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// -----------------------------------------------------------------------
		// MobileMenu — mobile navigation (session-cached)
		// -----------------------------------------------------------------------
		public DataTable MobileMenu()
		{
			if (!_security.IsAuthenticated()) return new DataTable();
			Guid gUSER_ID  = _security.USER_ID;
			string sSession = "vwMODULES_MobileMenu." + gUSER_ID.ToString();
			DataTable dt    = GetSessionObject<DataTable>(sSession);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                " + ControlChars.CrLf
				  + "  from vwMODULES_MobileMenu            " + ControlChars.CrLf
				  + " where USER_ID = @USER_ID              " + ControlChars.CrLf
				  + " order by TAB_ORDER                    " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@USER_ID", gUSER_ID);
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				SetSessionObject(sSession, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// -----------------------------------------------------------------------
		// Shortcuts
		// -----------------------------------------------------------------------
		public void ClearShortcuts()
		{
			RemoveSessionObject("vwSHORTCUTS");
		}

		public DataTable Shortcuts(string sMODULE_NAME)
		{
			if (Sql.IsEmptyString(sMODULE_NAME)) return new DataTable();
			Guid gUSER_ID  = _security.USER_ID;
			string sSession = "vwSHORTCUTS." + gUSER_ID.ToString() + "." + sMODULE_NAME;
			DataTable dt    = GetSessionObject<DataTable>(sSession);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                " + ControlChars.CrLf
				  + "  from vwSHORTCUTS                     " + ControlChars.CrLf
				  + " where USER_ID     = @USER_ID          " + ControlChars.CrLf
				  + "   and MODULE_NAME = @MODULE_NAME      " + ControlChars.CrLf
				  + " order by SHORTCUT_ORDER               " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@USER_ID"    , gUSER_ID   );
				Sql.AddParameter(cmd, "@MODULE_NAME", sMODULE_NAME);
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				SetSessionObject(sSession, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// Overload — all shortcuts for current user
		public DataTable Shortcuts()
		{
			Guid gUSER_ID  = _security.USER_ID;
			string sSession = "vwSHORTCUTS." + gUSER_ID.ToString();
			DataTable dt    = GetSessionObject<DataTable>(sSession);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                " + ControlChars.CrLf
				  + "  from vwSHORTCUTS                     " + ControlChars.CrLf
				  + " where USER_ID = @USER_ID              " + ControlChars.CrLf
				  + " order by MODULE_NAME, SHORTCUT_ORDER  " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@USER_ID", gUSER_ID);
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				SetSessionObject(sSession, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// -----------------------------------------------------------------------
		// LastViewed / GetAllLastViewed
		// -----------------------------------------------------------------------
		public DataTable LastViewed(string sMODULE_NAME)
		{
			if (Sql.IsEmptyString(sMODULE_NAME)) return new DataTable();
			Guid gUSER_ID  = _security.USER_ID;
			string sSession = "vwLAST_VIEWED." + gUSER_ID.ToString() + "." + sMODULE_NAME;
			DataTable dt    = GetSessionObject<DataTable>(sSession);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select top 10 *                        " + ControlChars.CrLf
				  + "  from vwLAST_VIEWED                   " + ControlChars.CrLf
				  + " where USER_ID     = @USER_ID          " + ControlChars.CrLf
				  + "   and MODULE_NAME = @MODULE_NAME      " + ControlChars.CrLf
				  + " order by DATE_ENTERED desc            " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@USER_ID"    , gUSER_ID    );
				Sql.AddParameter(cmd, "@MODULE_NAME", sMODULE_NAME);
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				SetSessionObject(sSession, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		public Dictionary<string, DataTable> GetAllLastViewed()
		{
			Guid gUSER_ID  = _security.USER_ID;
			string sSession = "vwLAST_VIEWED_ALL." + gUSER_ID.ToString();
			var dict        = GetSessionObject<Dictionary<string, DataTable>>(sSession);
			if (dict != null) return dict;
			dict = new Dictionary<string, DataTable>(StringComparer.OrdinalIgnoreCase);
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                " + ControlChars.CrLf
				  + "  from vwLAST_VIEWED                   " + ControlChars.CrLf
				  + " where USER_ID = @USER_ID              " + ControlChars.CrLf
				  + " order by MODULE_NAME, DATE_ENTERED desc" + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@USER_ID", gUSER_ID);
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				var dtAll = new DataTable();
				da.Fill(dtAll);
				foreach (DataRow row in dtAll.Rows)
				{
					string sModule = Sql.ToString(row["MODULE_NAME"]);
					if (!dict.ContainsKey(sModule))
						dict[sModule] = dtAll.Clone();
					if (dict[sModule].Rows.Count < 10)
						dict[sModule].ImportRow(row);
				}
				SetSessionObject(sSession, dict);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
			return dict;
		}

		// -----------------------------------------------------------------------
		// AuditedModules / ClearTerminologyPickLists / TerminologyPickLists
		// -----------------------------------------------------------------------
		public DataTable AuditedModules()
		{
			const string CACHE_KEY = "vwMODULES_Audited";
			DataTable dt = _memoryCache.Get<DataTable>(CACHE_KEY);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select MODULE_NAME                      " + ControlChars.CrLf
				  + "  from vwMODULES                       " + ControlChars.CrLf
				  + " where IS_AUDIT_ENABLED = 1            " + ControlChars.CrLf
				  + " order by MODULE_NAME                  " + ControlChars.CrLf;
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(CACHE_KEY, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		public void ClearTerminologyPickLists()
		{
			CacheRemovePrefix("vwTERMINOLOGY_PICKLISTS");
		}

		public DataTable TerminologyPickLists()
		{
			string sCULTURE = "en-US";
			try
			{
				string sessVal = Session?.GetString("USER_SETTINGS/CULTURE");
				if (!Sql.IsEmptyString(sessVal)) sCULTURE = sessVal;
			}
			catch { /* no session active */ }
			return TerminologyPickLists(sCULTURE);
		}

		public DataTable TerminologyPickLists(string sCULTURE)
		{
			string sKey = "vwTERMINOLOGY_PICKLISTS." + sCULTURE;
			DataTable dt = _memoryCache.Get<DataTable>(sKey);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select distinct LIST_NAME               " + ControlChars.CrLf
				  + "  from vwTERMINOLOGY_PICKLISTS          " + ControlChars.CrLf
				  + " where LANG = @LANG                     " + ControlChars.CrLf
				  + " order by LIST_NAME                    " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@LANG", sCULTURE);
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				if ((dt == null || dt.Rows.Count == 0) && sCULTURE != "en-US")
					return TerminologyPickLists("en-US");
				CacheSet(sKey, dt ?? new DataTable());
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt ?? new DataTable();
		}

		// -----------------------------------------------------------------------
		// GetAllModules — all modules DataTable (React client helper)
		// -----------------------------------------------------------------------
		public DataTable GetAllModules()
		{
			const string CACHE_KEY = "vwMODULES.All";
			DataTable dt = _memoryCache.Get<DataTable>(CACHE_KEY);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                " + ControlChars.CrLf
				  + "  from vwMODULES                       " + ControlChars.CrLf
				  + " order by MODULE_NAME                  " + ControlChars.CrLf;
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(CACHE_KEY, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// -----------------------------------------------------------------------
		// GetAdminModules — modules visible to admins only
		// -----------------------------------------------------------------------
		public DataTable GetAdminModules()
		{
			const string CACHE_KEY = "vwMODULES.Admin";
			DataTable dt = _memoryCache.Get<DataTable>(CACHE_KEY);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                " + ControlChars.CrLf
				  + "  from vwMODULES                       " + ControlChars.CrLf
				  + " where IS_ADMIN = 1                    " + ControlChars.CrLf
				  + " order by MODULE_NAME                  " + ControlChars.CrLf;
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(CACHE_KEY, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// -----------------------------------------------------------------------
		// GetAllConfig — all CONFIG key/value pairs (React client helper)
		// -----------------------------------------------------------------------
		public Dictionary<string, object> GetAllConfig()
		{
			const string CACHE_KEY = "vwCONFIG.ReactClient";
			var dict = _memoryCache.Get<Dictionary<string, object>>(CACHE_KEY);
			if (dict != null) return dict;
			dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select NAME, VALUE                     " + ControlChars.CrLf
				  + "  from vwCONFIG                        " + ControlChars.CrLf
				  + " order by NAME                         " + ControlChars.CrLf;
				using IDataReader rdr = cmd.ExecuteReader();
				while (rdr.Read())
				{
					string sName = Sql.ToString(rdr["NAME"]);
					if (!Sql.IsEmptyString(sName))
						dict[sName] = rdr["VALUE"] == DBNull.Value ? null : rdr["VALUE"];
				}
				CacheSet(CACHE_KEY, dict);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
			return dict;
		}

		// -----------------------------------------------------------------------
		// GetLoginConfig — subset of config for unauthenticated login page
		// -----------------------------------------------------------------------
		public Dictionary<string, string> GetLoginConfig()
		{
			const string CACHE_KEY = "vwCONFIG.ReactClient.Login";
			var dict = _memoryCache.Get<Dictionary<string, string>>(CACHE_KEY);
			if (dict != null) return dict;
			dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			try
			{
				// Login-safe keys: branding, auth settings, and SSO config
				string[] safeKeys = new[]
				{
					"site_url", "company_name", "application_name",
					"default_theme", "default_language", "default_date_format",
					"default_time_format", "default_timezone",
					"login_message", "header_logo", "login_logo",
					"oauth2_client_id", "oauth2_authorize_url",
					"saml_enabled", "ldap_enabled",
					"signalr_enabled", "mfa_required"
				};
				var allConfig = GetAllConfig();
				foreach (string key in safeKeys)
				{
					if (allConfig.TryGetValue(key, out object val))
						dict[key] = val != null ? val.ToString() : string.Empty;
				}
				CacheSet(CACHE_KEY, dict, ShortOptions());
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
			return dict;
		}

		// -----------------------------------------------------------------------
		// ReportParameters — parse RDL XML and return parameter DataTable
		// -----------------------------------------------------------------------
		public DataTable ReportParameters(Guid gID, Guid gUSER_ID)
		{
			if (gID == Guid.Empty) return new DataTable();
			string sKey = "vwREPORTS_Parameters." + gID.ToString() + "." + gUSER_ID.ToString();
			DataTable dt = _memoryCache.Get<DataTable>(sKey);
			if (dt != null) return dt;
			try
			{
				// Load the RDL XML from REPORTS table
				string sRDL = string.Empty;
				using (IDbConnection con = NewConnection())
				{
					con.Open();
					using IDbCommand cmd = con.CreateCommand();
					cmd.CommandText =
						"select CONTENT                          " + ControlChars.CrLf
					  + "  from vwREPORTS                       " + ControlChars.CrLf
					  + " where ID = @ID                        " + ControlChars.CrLf;
					Sql.AddParameter(cmd, "@ID", gID);
					object val = cmd.ExecuteScalar();
					if (val != null && val != DBNull.Value)
						sRDL = val.ToString();
				}
				dt = ReportParameters(sRDL, gUSER_ID.ToString());
				CacheSet(sKey, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		/// <summary>Parse report parameters from raw RDL XML string.</summary>
		public DataTable ReportParameters(string sRDL, string sUSER_ID)
		{
			// Build the parameter DataTable with the expected schema
			DataTable dt = BuildReportParameterSchema();
			if (Sql.IsEmptyString(sRDL)) return dt;
			try
			{
				var xmlDoc = new XmlDocument();
				xmlDoc.LoadXml(sRDL);
				var nsMgr = new XmlNamespaceManager(xmlDoc.NameTable);
				// Support both RDL 2003 and RDL 2008 namespaces
				nsMgr.AddNamespace("rd", "http://schemas.microsoft.com/SQLServer/reporting/reportdesigner");
				nsMgr.AddNamespace("rp", "http://schemas.microsoft.com/sqlserver/reporting/2008/01/reportdefinition");
				// Try RDL 2008 namespace first, then 2003
				string nsUri = "http://schemas.microsoft.com/sqlserver/reporting/2008/01/reportdefinition";
				XmlNodeList paramNodes = xmlDoc.SelectNodes("/rp:Report/rp:ReportParameters/rp:ReportParameter", nsMgr);
				if (paramNodes == null || paramNodes.Count == 0)
				{
					nsMgr.AddNamespace("r", "http://schemas.microsoft.com/sqlserver/reporting/2003/10/reportdefinition");
					paramNodes = xmlDoc.SelectNodes("/r:Report/r:ReportParameters/r:ReportParameter", nsMgr);
					if (paramNodes != null && paramNodes.Count > 0)
						nsUri = "http://schemas.microsoft.com/sqlserver/reporting/2003/10/reportdefinition";
				}
				if (paramNodes == null || paramNodes.Count == 0) return dt;
				nsMgr.AddNamespace("ns", nsUri);
				foreach (XmlNode paramNode in paramNodes)
				{
					string sName     = paramNode.Attributes?["Name"]?.Value ?? string.Empty;
					if (Sql.IsEmptyString(sName)) continue;
					XmlNode typeNode = paramNode.SelectSingleNode("ns:DataType", nsMgr);
					string sType     = typeNode != null ? typeNode.InnerText : "String";
					XmlNode promptNode = paramNode.SelectSingleNode("ns:Prompt", nsMgr);
					string sPrompt   = promptNode != null ? promptNode.InnerText : sName;
					XmlNode multiValNode = paramNode.SelectSingleNode("ns:MultiValue", nsMgr);
					bool   bMultiValue = multiValNode != null && Sql.ToBoolean(multiValNode.InnerText);
					XmlNode nullableNode = paramNode.SelectSingleNode("ns:Nullable", nsMgr);
					bool bNullable = nullableNode != null && Sql.ToBoolean(nullableNode.InnerText);
					XmlNode validValuesNode = paramNode.SelectSingleNode("ns:ValidValues/ns:DataSetReference", nsMgr);
					string sListName = string.Empty;
					if (validValuesNode != null)
					{
						XmlNode dsNameNode = validValuesNode.SelectSingleNode("ns:DataSetName", nsMgr);
						if (dsNameNode != null) sListName = dsNameNode.InnerText;
					}
					XmlNode defaultValueNode = paramNode.SelectSingleNode("ns:DefaultValue/ns:Values/ns:Value", nsMgr);
					string sDefaultValue = defaultValueNode != null ? defaultValueNode.InnerText : string.Empty;
					// Handle ASSIGNED_USER_ID and TEAM_ID special defaults
					if (sName.Equals("ASSIGNED_USER_ID", StringComparison.OrdinalIgnoreCase) && Sql.IsEmptyString(sDefaultValue))
						sDefaultValue = sUSER_ID;

					DataRow row = dt.NewRow();
					row["NAME"          ] = sName;
					row["PROMPT"        ] = sPrompt;
					row["DATA_TYPE"     ] = sType;
					row["IS_MULTI_VALUE"] = bMultiValue;
					row["IS_NULLABLE"   ] = bNullable;
					row["LIST_NAME"     ] = sListName;
					row["DEFAULT_VALUE" ] = sDefaultValue;
					dt.Rows.Add(row);
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
			return dt;
		}

		private static DataTable BuildReportParameterSchema()
		{
			var dt = new DataTable();
			dt.Columns.Add("NAME"          , typeof(string));
			dt.Columns.Add("PROMPT"        , typeof(string));
			dt.Columns.Add("DATA_TYPE"     , typeof(string));
			dt.Columns.Add("IS_MULTI_VALUE", typeof(bool  ));
			dt.Columns.Add("IS_NULLABLE"   , typeof(bool  ));
			dt.Columns.Add("LIST_NAME"     , typeof(string));
			dt.Columns.Add("DEFAULT_VALUE" , typeof(string));
			return dt;
		}

		// -----------------------------------------------------------------------
		// ArchiveViewExists / ClearArchiveViewExists
		// -----------------------------------------------------------------------
		public bool ArchiveViewExists(string sMODULE_NAME)
		{
			if (Sql.IsEmptyString(sMODULE_NAME)) return false;
			string sKey = "vwMODULES_ARCHIVE.Exists." + sMODULE_NAME;
			bool? exists = _memoryCache.Get<bool?>(sKey);
			if (exists.HasValue) return exists.Value;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select count(*)                         " + ControlChars.CrLf
				  + "  from vwMODULES                       " + ControlChars.CrLf
				  + " where MODULE_NAME = @MODULE_NAME      " + ControlChars.CrLf
				  + "   and HAS_ARCHIVE = 1                 " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@MODULE_NAME", sMODULE_NAME);
				object val = cmd.ExecuteScalar();
				bool bExists = Sql.ToInteger(val) > 0;
				CacheSet(sKey, (bool?)bExists);
				return bExists;
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				return false;
			}
		}

		public void ClearArchiveViewExists()
		{
			CacheRemovePrefix("vwMODULES_ARCHIVE.Exists.");
		}

		// -----------------------------------------------------------------------
		// InboundEmailBounce / InboundEmailMonitored
		// -----------------------------------------------------------------------
		public DataTable InboundEmailBounce()
		{
			const string CACHE_KEY = "vwINBOUND_EMAILS_Bounce";
			DataTable dt = _memoryCache.Get<DataTable>(CACHE_KEY);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select ID                               " + ControlChars.CrLf
				  + "     , NAME                            " + ControlChars.CrLf
				  + "  from vwINBOUND_EMAILS                " + ControlChars.CrLf
				  + " where EMAIL_TYPE = 'bounce'           " + ControlChars.CrLf
				  + " order by NAME                         " + ControlChars.CrLf;
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(CACHE_KEY, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		public DataTable InboundEmailMonitored()
		{
			const string CACHE_KEY = "vwINBOUND_EMAILS_Monitored";
			DataTable dt = _memoryCache.Get<DataTable>(CACHE_KEY);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select ID                               " + ControlChars.CrLf
				  + "     , NAME                            " + ControlChars.CrLf
				  + "  from vwINBOUND_EMAILS                " + ControlChars.CrLf
				  + " where EMAIL_TYPE = 'monitored'        " + ControlChars.CrLf
				  + " order by NAME                         " + ControlChars.CrLf;
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				CacheSet(CACHE_KEY, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// -----------------------------------------------------------------------
		// ClearReport — evict all report-related cache entries
		// -----------------------------------------------------------------------
		public void ClearReport()
		{
			CacheRemovePrefix("vwREPORTS_Parameters.");
		}

		// -----------------------------------------------------------------------
		// SavedSearch — session-stored saved search for the current user/module
		// -----------------------------------------------------------------------
		public DataTable SavedSearch(string sMODULE_NAME)
		{
			if (Sql.IsEmptyString(sMODULE_NAME)) return new DataTable();
			if (!_security.IsAuthenticated()) return new DataTable();
			Guid gUSER_ID   = _security.USER_ID;
			string sSession = "vwSAVED_SEARCH." + gUSER_ID.ToString() + "." + sMODULE_NAME;
			DataTable dt    = GetSessionObject<DataTable>(sSession);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                " + ControlChars.CrLf
				  + "  from vwSAVED_SEARCH                  " + ControlChars.CrLf
				  + " where ASSIGNED_USER_ID = @USER_ID     " + ControlChars.CrLf
				  + "   and SEARCH_MODULE    = @MODULE_NAME " + ControlChars.CrLf
				  + " order by NAME                         " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@USER_ID"    , gUSER_ID    );
				Sql.AddParameter(cmd, "@MODULE_NAME", sMODULE_NAME);
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				SetSessionObject(sSession, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// Overload — all saved searches for current user
		public DataTable SavedSearch()
		{
			if (!_security.IsAuthenticated()) return new DataTable();
			Guid gUSER_ID   = _security.USER_ID;
			string sSession = "vwSAVED_SEARCH." + gUSER_ID.ToString();
			DataTable dt    = GetSessionObject<DataTable>(sSession);
			if (dt != null) return dt;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                " + ControlChars.CrLf
				  + "  from vwSAVED_SEARCH                  " + ControlChars.CrLf
				  + " where ASSIGNED_USER_ID = @USER_ID     " + ControlChars.CrLf
				  + " order by SEARCH_MODULE, NAME          " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@USER_ID", gUSER_ID);
				using DbDataAdapter da = NewDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				dt = new DataTable();
				da.Fill(dt);
				SetSessionObject(sSession, dt);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				dt = new DataTable();
			}
			return dt;
		}

		// -----------------------------------------------------------------------
		// ReactTeam — serializable nested class representing a team tree node
		// -----------------------------------------------------------------------
		[Serializable]
		public class ReactTeam
		{
			public Guid   ID          { get; set; }
			public string NAME        { get; set; }
			public string DESCRIPTION { get; set; }
			public bool   PRIVATE     { get; set; }
			public List<ReactTeam> Children { get; set; } = new List<ReactTeam>();

			public void ProcessNodes(XmlNodeList nodes, XmlNamespaceManager nsMgr)
			{
				if (nodes == null) return;
				foreach (XmlNode node in nodes)
				{
					var team       = new ReactTeam();
					team.ID        = Sql.ToGuid  (node.Attributes?["ID"         ]?.Value);
					team.NAME      = Sql.ToString (node.Attributes?["NAME"       ]?.Value);
					team.DESCRIPTION = Sql.ToString(node.Attributes?["DESCRIPTION"]?.Value);
					team.PRIVATE   = Sql.ToBoolean(node.Attributes?["PRIVATE"    ]?.Value);
					team.ProcessNodes(node.ChildNodes, nsMgr);
					Children.Add(team);
				}
			}
		}

		// -----------------------------------------------------------------------
		// GetUserTeamTree — XML-based team hierarchy for current user (session-stored)
		// -----------------------------------------------------------------------
		public ReactTeam GetUserTeamTree()
		{
			if (!_security.IsAuthenticated()) return null;
			Guid gUSER_ID   = _security.USER_ID;
			string sSession = "vwTEAMS_Tree." + gUSER_ID.ToString();
			ReactTeam tree  = GetSessionObject<ReactTeam>(sSession);
			if (tree != null) return tree;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select dbo.fnTEAM_HIERARCHY_ChildrenXml(@USER_ID, null) as TEAM_TREE" + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@USER_ID", gUSER_ID);
				object xmlResult = cmd.ExecuteScalar();
				if (xmlResult == null || xmlResult == DBNull.Value)
					return null;
				string sXml = xmlResult.ToString();
				var xmlDoc = new XmlDocument();
				xmlDoc.LoadXml(sXml);
				tree = new ReactTeam();
				tree.ProcessNodes(xmlDoc.DocumentElement?.ChildNodes, null);
				SetSessionObject(sSession, tree);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
			return tree;
		}

		// -----------------------------------------------------------------------
		// Config — single config value by key (string overload)
		// -----------------------------------------------------------------------
		public string Config(string sNAME)
		{
			if (Sql.IsEmptyString(sNAME)) return string.Empty;
			string sKey = "CONFIG." + sNAME;
			string sVal = _memoryCache.Get<string>(sKey);
			if (sVal != null) return sVal;
			// Ensure the full config is loaded
			LoadConfig();
			// Try again after load
			sVal = _memoryCache.Get<string>(sKey);
			return sVal ?? string.Empty;
		}

		// -----------------------------------------------------------------------
		// LoadConfig — eagerly populate IMemoryCache from vwCONFIG view
		// -----------------------------------------------------------------------
		public void LoadConfig()
		{
			const string LOADED_KEY = "CONFIG.__loaded__";
			if (_memoryCache.Get<bool?>(LOADED_KEY) == true) return;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select NAME, VALUE                     " + ControlChars.CrLf
				  + "  from vwCONFIG                        " + ControlChars.CrLf
				  + " order by NAME                         " + ControlChars.CrLf;
				using IDataReader rdr = cmd.ExecuteReader();
				while (rdr.Read())
				{
					string sName = Sql.ToString(rdr["NAME"]);
					string sVal  = rdr["VALUE"] == DBNull.Value ? string.Empty : Sql.ToString(rdr["VALUE"]);
					if (!Sql.IsEmptyString(sName))
					{
						string sCacheKey = "CONFIG." + sName;
						_cacheKeys[sCacheKey] = 0;
						_memoryCache.Set(sCacheKey, sVal, DefaultOptions());
					}
				}
				// Mark as fully loaded
				CacheSet(LOADED_KEY, (bool?)true);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
		}

		// -----------------------------------------------------------------------
		// LoadTerminology — eagerly populate terminology cache for a culture
		// -----------------------------------------------------------------------
		public void LoadTerminology(string sCULTURE)
		{
			if (Sql.IsEmptyString(sCULTURE)) sCULTURE = "en-US";
			string LOADED_KEY = "vwTERMINOLOGY.__loaded__." + sCULTURE;
			if (_memoryCache.Get<bool?>(LOADED_KEY) == true) return;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select NAME, LIST_NAME, DISPLAY_NAME   " + ControlChars.CrLf
				  + "  from vwTERMINOLOGY                   " + ControlChars.CrLf
				  + " where LANG = @LANG                    " + ControlChars.CrLf
				  + " order by LIST_NAME, NAME              " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@LANG", sCULTURE);
				using IDataReader rdr = cmd.ExecuteReader();
				// Group by LIST_NAME into per-list DataTables
				var listTables = new Dictionary<string, DataTable>(StringComparer.OrdinalIgnoreCase);
				while (rdr.Read())
				{
					string sListName = Sql.ToString(rdr["LIST_NAME"]);
					string sName     = Sql.ToString(rdr["NAME"     ]);
					string sDisplay  = Sql.ToString(rdr["DISPLAY_NAME"]);
					if (Sql.IsEmptyString(sListName)) continue;
					if (!listTables.ContainsKey(sListName))
					{
						var dt = new DataTable();
						dt.Columns.Add("NAME"        , typeof(string));
						dt.Columns.Add("DISPLAY_NAME", typeof(string));
						listTables[sListName] = dt;
					}
					DataRow row = listTables[sListName].NewRow();
					row["NAME"        ] = sName;
					row["DISPLAY_NAME"] = sDisplay;
					listTables[sListName].Rows.Add(row);
				}
				foreach (var kvp in listTables)
				{
					string sListCacheKey = "vwTERMINOLOGY." + sCULTURE + ".list." + kvp.Key;
					CacheSet(sListCacheKey, kvp.Value);
				}
				CacheSet(LOADED_KEY, (bool?)true);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
		}

		// -----------------------------------------------------------------------
		// SetConfigValue — persist a single config value to IMemoryCache and DB
		// -----------------------------------------------------------------------
		public void SetConfigValue(string sNAME, string sVALUE)
		{
			if (Sql.IsEmptyString(sNAME)) return;
			// Update cache immediately
			string sCacheKey = "CONFIG." + sNAME;
			_cacheKeys[sCacheKey] = 0;
			_memoryCache.Set(sCacheKey, sVALUE ?? string.Empty, DefaultOptions());
			// Also invalidate the GetAllConfig cache so it is rebuilt
			CacheRemove("vwCONFIG.ReactClient");
			CacheRemove("vwCONFIG.ReactClient.Login");
			// Update the DB
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"update CONFIG                           " + ControlChars.CrLf
				  + "   set VALUE = @VALUE                  " + ControlChars.CrLf
				  + "     , DATE_MODIFIED = getdate()       " + ControlChars.CrLf
				  + " where NAME = @NAME                    " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@VALUE", sVALUE);
				Sql.AddParameter(cmd, "@NAME" , sNAME );
				cmd.ExecuteNonQuery();
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
		}

		// -----------------------------------------------------------------------
		// ClearAll — evict every cache entry (full reset)
		// -----------------------------------------------------------------------
		public void ClearAll()
		{
			foreach (string key in _cacheKeys.Keys)
			{
				_cacheKeys.TryRemove(key, out _);
				_memoryCache.Remove(key);
			}
		}

		// -----------------------------------------------------------------------
		// Modules — all modules DataTable
		// -----------------------------------------------------------------------
		public DataTable Modules()
		{
			return GetAllModules();
		}

		// -----------------------------------------------------------------------
		// ModuleTableName — return the primary table name for a module
		// -----------------------------------------------------------------------
		public string ModuleTableName(string sMODULE_NAME)
		{
			if (Sql.IsEmptyString(sMODULE_NAME)) return string.Empty;
			string sKey = "vwMODULES.TableName." + sMODULE_NAME;
			string sTableName = _memoryCache.Get<string>(sKey);
			if (!Sql.IsEmptyString(sTableName)) return sTableName;
			try
			{
				using IDbConnection con = NewConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select TABLE_NAME                      " + ControlChars.CrLf
				  + "  from vwMODULES                       " + ControlChars.CrLf
				  + " where MODULE_NAME = @MODULE_NAME      " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@MODULE_NAME", sMODULE_NAME);
				object val = cmd.ExecuteScalar();
				sTableName = val != null && val != DBNull.Value ? val.ToString() : sMODULE_NAME.ToUpper();
				CacheSet(sKey, sTableName);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				sTableName = sMODULE_NAME.ToUpper();
			}
			return sTableName;
		}

	// =====================================================================================
		// RestTables — returns a DataTable from vwSYSTEM_REST_TABLES for a given table name.
		// Used by the SOAP service to validate that a module is accessible via REST/SOAP.
		// Migrated from SplendidCRM/_code/SplendidCache.cs line 6962.
		// Session-based caching migrated to IMemoryCache with same cache key pattern.
		// =====================================================================================
		public DataTable RestTables(string sTABLE_NAME, bool bExcludeSystemTables)
		{
			string sCacheKey = "vwSYSTEM_REST_TABLES." + sTABLE_NAME + (bExcludeSystemTables ? String.Empty : ".Admin");
			DataTable dt = _memoryCache.Get<DataTable>(sCacheKey);
			if ( dt == null )
			{
				dt = new DataTable();
				try
				{
					if ( _security.IsAuthenticated() )
					{
						using IDbConnection con = NewConnection();
						con.Open();
						string sSQL;
						sSQL = "select *                          " + ControlChars.CrLf;
						if ( Crm.Config.enable_data_privacy() )
							sSQL += "     , (case when exists(select * from vwDATA_PRIVACY_FIELDS where vwDATA_PRIVACY_FIELDS.MODULE_NAME = vwSYSTEM_REST_TABLES.MODULE_NAME) then 1 else 0 end) as IS_DATA_PRIVACY_MODULE" + ControlChars.CrLf;
						sSQL += "  from vwSYSTEM_REST_TABLES       " + ControlChars.CrLf
						      + " where TABLE_NAME = @TABLE_NAME   " + ControlChars.CrLf;
						if ( bExcludeSystemTables )
							sSQL += "   and IS_SYSTEM = 0              " + ControlChars.CrLf;
						using IDbCommand cmd = con.CreateCommand();
						cmd.CommandText = sSQL;
						cmd.CommandTimeout = 0;
						Sql.AddParameter(cmd, "@TABLE_NAME", sTABLE_NAME);
						using DbDataAdapter da = NewDataAdapter();
						((IDbDataAdapter)da).SelectCommand = cmd;
						da.Fill(dt);
						if ( dt.Rows.Count > 0 )
							CacheSet(sCacheKey, dt);
					}
				}
				catch(Exception ex)
				{
					SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				}
			}
			return dt;
		}

		// =====================================================================================
		// SqlColumns — returns a DataTable from vwSqlColumns for a given table name.
		// Used by get_module_fields in the SOAP service to enumerate view columns.
		// Migrated from SplendidCRM/_code/SplendidCache.cs line 4607.
		// =====================================================================================
		public DataTable SqlColumns(string sTABLE_NAME)
		{
			string sCacheKey = "vwSqlColumns." + sTABLE_NAME;
			DataTable dt = _memoryCache.Get<DataTable>(sCacheKey);
			if ( dt == null )
			{
				try
				{
					using IDbConnection con = NewConnection();
					con.Open();
					string sSQL;
					sSQL = "select ColumnName              " + ControlChars.CrLf
					     + "     , CsType                  " + ControlChars.CrLf
					     + "     , length                  " + ControlChars.CrLf
					     + "  from vwSqlColumns            " + ControlChars.CrLf
					     + " where ObjectName = @TABLE_NAME" + ControlChars.CrLf
					     + " order by colid                " + ControlChars.CrLf;
					using IDbCommand cmd = con.CreateCommand();
					cmd.CommandText = sSQL;
					Sql.AddParameter(cmd, "@TABLE_NAME", Sql.MetadataName(cmd, sTABLE_NAME));
					using DbDataAdapter da = NewDataAdapter();
					((IDbDataAdapter)da).SelectCommand = cmd;
					dt = new DataTable();
					da.Fill(dt);
					CacheSet(sCacheKey, dt);
				}
				catch(Exception ex)
				{
					SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
					dt = new DataTable();
				}
			}
			return dt;
		}

		// =====================================================================================
		// FieldsMetaData_Validated — returns DataTable from vwFIELDS_META_DATA_Validated.
		// Used by UpdateCustomFields in the SOAP service for custom field validation.
		// Migrated from SplendidCRM/_code/SplendidCache.cs line 4543.
		// =====================================================================================
		public DataTable FieldsMetaData_Validated(string sTABLE_NAME)
		{
			string sCacheKey = "vwFIELDS_META_DATA_Validated." + sTABLE_NAME;
			DataTable dt = _memoryCache.Get<DataTable>(sCacheKey);
			if ( dt == null )
			{
				try
				{
					using IDbConnection con = NewConnection();
					con.Open();
					string sSQL;
					sSQL = "select *                             " + ControlChars.CrLf
					     + "  from vwFIELDS_META_DATA_Validated  " + ControlChars.CrLf
					     + " where TABLE_NAME = @TABLE_NAME      " + ControlChars.CrLf
					     + " order by colid                      " + ControlChars.CrLf;
					using IDbCommand cmd = con.CreateCommand();
					cmd.CommandText = sSQL;
					Sql.AddParameter(cmd, "@TABLE_NAME", sTABLE_NAME);
					using DbDataAdapter da = NewDataAdapter();
					((IDbDataAdapter)da).SelectCommand = cmd;
					dt = new DataTable();
					da.Fill(dt);
					CacheSet(sCacheKey, dt);
				}
				catch(Exception ex)
				{
					SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
					dt = new DataTable();
				}
			}
			return dt;
		}

	}  // end class SplendidCache
}  // end namespace SplendidCRM
