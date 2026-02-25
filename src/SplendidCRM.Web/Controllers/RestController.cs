// RestController.cs
// Converted from SplendidCRM/Rest.svc.cs (8,369 lines, 152 WCF operations)
// Migration: WCF [ServiceContract]/[WebInvoke] → ASP.NET Core [ApiController]/[HttpGet]/[HttpPost]
// Route compatibility: [Route("Rest.svc")] preserves all original endpoint paths for React SPA.
// Static HttpContext.Current → IHttpContextAccessor DI
// HttpApplicationState Application[] → IMemoryCache
// Session[] → IHttpContextAccessor.HttpContext.Session (distributed)
// ConfigurationManager.AppSettings → IConfiguration
// JavaScriptSerializer → Newtonsoft.Json / System.Text.Json
// Stream return → IActionResult (Content with application/json)
#nullable disable

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

namespace SplendidCRM
{
	/// <summary>
	/// Primary REST API controller for SplendidCRM.
	/// Converted from Rest.svc.cs (WCF) to ASP.NET Core Web API.
	/// Route base: /Rest.svc — all original endpoint paths preserved for React SPA backward compatibility.
	/// </summary>
	[ApiController]
	[Route("Rest.svc")]
	public class RestController : ControllerBase
	{
		// =====================================================================
		// Private DI fields
		// =====================================================================
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly IMemoryCache         _memoryCache;
		private readonly IConfiguration       _configuration;
		private readonly IWebHostEnvironment  _webHostEnvironment;
		private readonly ILogger<RestController> _logger;

		// Core services
		private readonly Security            _security;
		private readonly SplendidCache       _splendidCache;
		private readonly SplendidInit        _splendidInit;
		private readonly SplendidError       _splendidError;
		private readonly RestUtil            _restUtil;
		private readonly ModuleUtils         _moduleUtils;
		private readonly DbProviderFactories _dbProviderFactories;
		private readonly Crm                 _crm;

		// Optional / auxiliary services
		private readonly EmailUtils          _emailUtils;
		private readonly ExchangeUtils       _exchangeUtils;
		private readonly GoogleApps          _googleApps;
		private readonly SplendidExport      _splendidExport;
		private readonly SplendidDynamic     _splendidDynamic;
		private readonly SplendidImport      _splendidImport;
		private readonly ActiveDirectory     _activeDirectory;
		private readonly Utils               _utils;

		// =====================================================================
		// Constructor
		// =====================================================================
		public RestController(
			IHttpContextAccessor    httpContextAccessor,
			IMemoryCache            memoryCache,
			IConfiguration          configuration,
			IWebHostEnvironment     webHostEnvironment,
			ILogger<RestController> logger,
			Security                security,
			SplendidCache           splendidCache,
			SplendidInit            splendidInit,
			SplendidError           splendidError,
			RestUtil                restUtil,
			ModuleUtils             moduleUtils,
			DbProviderFactories     dbProviderFactories,
			Crm                     crm,
			EmailUtils              emailUtils,
			ExchangeUtils           exchangeUtils,
			GoogleApps              googleApps,
			SplendidExport          splendidExport,
			SplendidDynamic         splendidDynamic,
			SplendidImport          splendidImport,
			ActiveDirectory         activeDirectory,
			Utils                   utils)
		{
			_httpContextAccessor = httpContextAccessor;
			_memoryCache         = memoryCache;
			_configuration       = configuration;
			_webHostEnvironment  = webHostEnvironment;
			_logger              = logger;
			_security            = security;
			_splendidCache       = splendidCache;
			_splendidInit        = splendidInit;
			_splendidError       = splendidError;
			_restUtil            = restUtil;
			_moduleUtils         = moduleUtils;
			_dbProviderFactories = dbProviderFactories;
			_crm                 = crm;
			_emailUtils          = emailUtils;
			_exchangeUtils       = exchangeUtils;
			_googleApps          = googleApps;
			_splendidExport      = splendidExport;
			_splendidDynamic     = splendidDynamic;
			_splendidImport      = splendidImport;
			_activeDirectory     = activeDirectory;
			_utils               = utils;
		}

		// =====================================================================
		// Private helpers — common patterns
		// =====================================================================

		/// <summary>Builds the base URI for metadata links in JSON responses.</summary>
		private string GetBaseURI(string suffix = "")
		{
			var req = _httpContextAccessor.HttpContext?.Request;
			if (req == null) return String.Empty;
			string path = req.Path.ToString();
			if (!Sql.IsEmptyString(suffix))
				path = path.Replace(suffix, "");
			return req.Scheme + "://" + req.Host + path;
		}

		/// <summary>Gets current user's timezone for REST serialization.</summary>
		private SplendidCRM.TimeZone GetUserTimezone()
		{
			Guid gTIMEZONE = Sql.ToGuid(_httpContextAccessor.HttpContext?.Session?.GetString("USER_SETTINGS/TIMEZONE"));
			return SplendidCRM.TimeZone.CreateTimeZone(gTIMEZONE);
		}

		/// <summary>Gets current user's culture string for L10N.</summary>
		private string GetUserCulture()
		{
			return Sql.ToString(_httpContextAccessor.HttpContext?.Session?.GetString("USER_SETTINGS/CULTURE")) ?? "en-US";
		}

		/// <summary>Serializes a dictionary to a JSON IActionResult.</summary>
		private IActionResult JsonContent(object obj)
		{
			string json = JsonConvert.SerializeObject(obj);
			return Content(json, "application/json", Encoding.UTF8);
		}

		/// <summary>
		/// Validates authentication and optionally ACL access, returning null on success
		/// or an appropriate error IActionResult.
		/// </summary>
		private bool CheckAuthenticated()
		{
			return _security.IsAuthenticated();
		}

		/// <summary>Applies no-cache headers to the response.</summary>
		private void SetNoCacheHeaders()
		{
			if (_httpContextAccessor.HttpContext != null)
			{
				_httpContextAccessor.HttpContext.Response.Headers.Append("Cache-Control", "no-cache");
				_httpContextAccessor.HttpContext.Response.Headers.Append("Pragma", "no-cache");
			}
		}

		// =====================================================================
		// Private helper — Single Sign-On settings (used by both GetReactLoginState and GetSingleSignOnSettings)
		// =====================================================================
		private Dictionary<string, object> GetSingleSignOnSettingsInternal()
		{
			bool bADFS_SINGLE_SIGN_ON  = Sql.ToBoolean(_memoryCache.Get("CONFIG.ADFS.SingleSignOn.Enabled" ));
			bool bAZURE_SINGLE_SIGN_ON = Sql.ToBoolean(_memoryCache.Get("CONFIG.Azure.SingleSignOn.Enabled"));

			var results = new Dictionary<string, object>();
			if (bADFS_SINGLE_SIGN_ON)
			{
				results.Add("instance"         , Sql.ToString(_memoryCache.Get("CONFIG.ADFS.SingleSignOn.Authority"        )));
				results.Add("tenant"           , "adfs");
				results.Add("clientId"         , Sql.ToString(_memoryCache.Get("CONFIG.ADFS.SingleSignOn.ClientId"         )));
				results.Add("mobileId"         , Sql.ToString(_memoryCache.Get("CONFIG.ADFS.SingleSignOn.MobileClientId"   )));
				results.Add("mobileRedirectUrl", Sql.ToString(_memoryCache.Get("CONFIG.ADFS.SingleSignOn.MobileRedirectUrl")));
				var endpoints = new Dictionary<string, object>();
				endpoints.Add(Sql.ToString(_memoryCache.Get("CONFIG.ADFS.SingleSignOn.Realm")), Sql.ToString(_memoryCache.Get("CONFIG.ADFS.SingleSignOn.MobileClientId")));
				results.Add("endpoints", endpoints);
			}
			else if (bAZURE_SINGLE_SIGN_ON)
			{
				var endpoints = new Dictionary<string, object>();
				endpoints.Add(Sql.ToString(_memoryCache.Get("CONFIG.Azure.SingleSignOn.Realm")), Sql.ToString(_memoryCache.Get("CONFIG.Azure.SingleSignOn.AadClientId")));
				results.Add("instance"         , "https://login.microsoftonline.com/");
				results.Add("tenant"           , Sql.ToString(_memoryCache.Get("CONFIG.Azure.SingleSignOn.AadTenantDomain"  )));
				results.Add("clientId"         , Sql.ToString(_memoryCache.Get("CONFIG.Azure.SingleSignOn.AadClientId"      )));
				results.Add("mobileId"         , Sql.ToString(_memoryCache.Get("CONFIG.Azure.SingleSignOn.MobileClientId"   )));
				results.Add("mobileRedirectUrl", Sql.ToString(_memoryCache.Get("CONFIG.Azure.SingleSignOn.MobileRedirectUrl")));
				results.Add("endpoints"        , endpoints);
			}
			return results;
		}

		// =====================================================================
		// Private helper — User Profile Dictionary (used in GetReactState)
		// =====================================================================
		private Dictionary<string, object> GetUserProfileDict()
		{
			if (!_security.IsAuthenticated())
				return null;

			var session = _httpContextAccessor.HttpContext?.Session;
			string sCulture = Sql.ToString(session?.GetString("USER_SETTINGS/CULTURE")) ?? "en-US";
			Guid gCURRENCY_ID = Sql.ToGuid(session?.GetString("USER_SETTINGS/CURRENCY"));

			string sAUTHENTICATION = "CRM";
			bool bADFS  = Sql.ToBoolean(_memoryCache.Get("CONFIG.ADFS.SingleSignOn.Enabled" ));
			bool bAZURE = Sql.ToBoolean(_memoryCache.Get("CONFIG.Azure.SingleSignOn.Enabled"));
			if (bADFS || bAZURE)
				sAUTHENTICATION = "SingleSignOn";
			else if (_security.IsWindowsAuthentication())
				sAUTHENTICATION = "Windows";

			CultureInfo culture = CultureInfo.CreateSpecificCulture(sCulture);

			var profile = new Dictionary<string, object>();
			profile["USER_ID"                    ] = _security.USER_ID;
			profile["USER_SESSION"               ] = _security.USER_SESSION;
			profile["USER_NAME"                  ] = _security.USER_NAME;
			profile["FULL_NAME"                  ] = _security.FULL_NAME;
			profile["TEAM_ID"                    ] = _security.TEAM_ID;
			profile["TEAM_NAME"                  ] = Sql.ToString(_memoryCache.Get("TEAM_NAME")) != String.Empty ? Sql.ToString(_memoryCache.Get("TEAM_NAME")) : String.Empty;
			profile["USER_LANG"                  ] = sCulture;
			profile["USER_DATE_FORMAT"           ] = Sql.ToString(session?.GetString("USER_SETTINGS/DATEFORMAT"));
			profile["USER_TIME_FORMAT"           ] = Sql.ToString(session?.GetString("USER_SETTINGS/TIMEFORMAT"));
			profile["USER_THEME"                 ] = Sql.ToString(session?.GetString("USER_SETTINGS/THEME"     ));
			profile["USER_CURRENCY_ID"           ] = gCURRENCY_ID.ToString().ToLower();
			profile["USER_TIMEZONE_ID"           ] = Sql.ToString(session?.GetString("USER_SETTINGS/TIMEZONE"  )).ToLower();
			profile["ORIGINAL_TIMEZONE_ID"       ] = Sql.ToString(session?.GetString("USER_SETTINGS/TIMEZONE/ORIGINAL")).ToLower();
			profile["PICTURE"                    ] = _security.PICTURE;
			profile["EXCHANGE_ALIAS"             ] = Sql.ToString(session?.GetString("EXCHANGE_ALIAS"      ));
			profile["EXCHANGE_EMAIL"             ] = Sql.ToString(session?.GetString("EXCHANGE_EMAIL"      ));
			profile["USER_EXTENSION"             ] = Sql.ToString(session?.GetString("EXTENSION"           ));
			profile["USER_FULL_NAME"             ] = Sql.ToString(session?.GetString("FULL_NAME"           ));
			profile["USER_PHONE_WORK"            ] = Sql.ToString(session?.GetString("PHONE_WORK"          ));
			profile["USER_SMS_OPT_IN"            ] = Sql.ToString(session?.GetString("SMS_OPT_IN"          ));
			profile["USER_PHONE_MOBILE"          ] = Sql.ToString(session?.GetString("PHONE_MOBILE"        ));
			profile["USER_TWITTER_TRACKS"        ] = String.Empty;
			profile["USER_CHAT_CHANNELS"         ] = String.Empty;
			profile["PHONEBURNER_TOKEN_EXPIRES_AT"] = String.Empty;
			// Currency format info from culture
			profile["USER_CurrencyDecimalDigits"   ] = culture.NumberFormat.CurrencyDecimalDigits.ToString();
			profile["USER_CurrencyDecimalSeparator"] = culture.NumberFormat.CurrencyDecimalSeparator;
			profile["USER_CurrencyGroupSeparator"  ] = culture.NumberFormat.CurrencyGroupSeparator;
			profile["USER_CurrencyGroupSizes"      ] = culture.NumberFormat.CurrencyGroupSizes[0].ToString();
			profile["USER_CurrencyNegativePattern" ] = culture.NumberFormat.CurrencyNegativePattern.ToString();
			profile["USER_CurrencyPositivePattern" ] = culture.NumberFormat.CurrencyPositivePattern.ToString();
			profile["USER_CurrencySymbol"          ] = culture.NumberFormat.CurrencySymbol;
			profile["PRIMARY_ROLE_ID"              ] = Sql.ToString(session?.GetString("PRIMARY_ROLE_ID"  ));
			profile["PRIMARY_ROLE_NAME"            ] = Sql.ToString(session?.GetString("PRIMARY_ROLE_NAME"));
			profile["IS_ADMIN"                     ] = _security.IS_ADMIN;
			profile["IS_ADMIN_DELEGATE"            ] = _security.IS_ADMIN_DELEGATE;
			profile["SAVE_QUERY"                   ] = Sql.ToBoolean(session?.GetString("USER_SETTINGS/SAVE_QUERY"));
			profile["USER_IMPERSONATION"           ] = _security.IsImpersonating();
			profile["AUTHENTICATION"               ] = sAUTHENTICATION;
			return profile;
		}

		// =====================================================================
		// Private helpers — GetAll* layout methods (from static SplendidCache)
		// Converted from static methods taking HttpContext to instance methods
		// using _memoryCache / _dbProviderFactories / _security / _splendidCache.
		// =====================================================================

		private Dictionary<string, object> GetAllGridViewsInternal(List<string> lstMODULES)
		{
			string sModuleList = String.Join(",", lstMODULES.ToArray());
			string sCacheKey   = "vwGRIDVIEWS.ReactClient." + sModuleList;
			var objs = _memoryCache.Get<Dictionary<string, object>>(sCacheKey);
			if (objs != null) return objs;
			objs = new Dictionary<string, object>();
			try
			{
				if (_security.IsAuthenticated())
				{
					var lstWithAdmin = new List<string>(lstMODULES);
					lstWithAdmin.Add("Users");
					using IDbConnection con = _dbProviderFactories.CreateConnection();
					con.Open();
					using IDbCommand cmd = con.CreateCommand();
					cmd.CommandText =
						"select *          " + ControlChars.CrLf
					  + "  from vwGRIDVIEWS" + ControlChars.CrLf
					  + " order by NAME    " + ControlChars.CrLf;
					using var da = _dbProviderFactories.CreateDataAdapter();
					((IDbDataAdapter)da).SelectCommand = cmd;
					using var dt = new DataTable();
					da.Fill(dt);
					foreach (DataRow row in dt.Rows)
					{
						string sGRID_NAME   = Sql.ToString(row["NAME"]);
						string sMODULE_NAME = String.Empty;
						string[] arr = sGRID_NAME.Split('.');
						if (arr.Length > 0)
						{
							if (arr[0] == "ListView" || arr[0] == "PopupView" || arr[0] == "Activities")
								sMODULE_NAME = arr[0];
							else if (Sql.ToBoolean(_memoryCache.Get("Modules." + (arr.Length > 1 ? arr[1] : arr[0]) + ".Valid")))
								sMODULE_NAME = arr.Length > 1 ? arr[1] : arr[0];
							else
								sMODULE_NAME = arr[0];
						}
						if (!lstWithAdmin.Contains(sMODULE_NAME)) continue;
						var drow = new Dictionary<string, object>();
						for (int j = 0; j < dt.Columns.Count; j++)
						{
							if (dt.Columns[j].ColumnName == "ID") continue;
							drow.Add(dt.Columns[j].ColumnName, row[j]);
						}
						if (!objs.ContainsKey(sGRID_NAME))
							objs.Add(sGRID_NAME, drow);
					}
					_memoryCache.Set(sCacheKey, objs, _splendidCache.DefaultCacheExpiration());
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				throw;
			}
			return objs;
		}

		private Dictionary<string, object> GetAllGridViewsColumnsInternal(List<string> lstMODULES)
		{
			string sModuleList = String.Join(",", lstMODULES.ToArray());
			string sCacheKey   = "vwGRIDVIEWS_COLUMNS.ReactClient." + sModuleList;
			var objs = _memoryCache.Get<Dictionary<string, object>>(sCacheKey);
			if (objs != null) return objs;
			objs = new Dictionary<string, object>();
			try
			{
				if (_security.IsAuthenticated())
				{
					var lstWithAdmin = new List<string>(lstMODULES);
					lstWithAdmin.Add("Home"); lstWithAdmin.Add("Users");
					lstWithAdmin.Add("DnsNames"); lstWithAdmin.Add("ResourceGroups");
					lstWithAdmin.Add("SqlDatabases"); lstWithAdmin.Add("SqlServers");
					lstWithAdmin.Add("StorageAccounts"); lstWithAdmin.Add("VirtualMachines");
					using IDbConnection con = _dbProviderFactories.CreateConnection();
					con.Open();
					using IDbCommand cmd = con.CreateCommand();
					cmd.CommandText =
						"select *                                         " + ControlChars.CrLf
					  + "  from vwGRIDVIEWS_COLUMNS                       " + ControlChars.CrLf
					  + " where (DEFAULT_VIEW = 0 or DEFAULT_VIEW is null)" + ControlChars.CrLf
					  + " order by GRID_NAME, COLUMN_INDEX                " + ControlChars.CrLf;
					using var da = _dbProviderFactories.CreateDataAdapter();
					((IDbDataAdapter)da).SelectCommand = cmd;
					using var dt = new DataTable();
					da.Fill(dt);
					bool bClearScript = false;
					string sLAST_VIEW_NAME = String.Empty;
					List<Dictionary<string, object>> layout = null;
					for (int i = 0; i < dt.Rows.Count; i++)
					{
						DataRow row = dt.Rows[i];
						string sGRID_NAME  = Sql.ToString(row["GRID_NAME" ]);
						string sDATA_FIELD = Sql.ToString(row["DATA_FIELD"]);
						string sMODULE_NAME = String.Empty;
						string[] arr = sGRID_NAME.Split('.');
						if (arr.Length > 0)
						{
							if (arr[0] == "ListView" || arr[0] == "PopupView" || arr[0] == "Activities")
								sMODULE_NAME = arr[0];
							else if (Sql.ToBoolean(_memoryCache.Get("Modules." + (arr.Length > 1 ? arr[1] : arr[0]) + ".Valid")))
								sMODULE_NAME = arr.Length > 1 ? arr[1] : arr[0];
							else
								sMODULE_NAME = arr[0];
						}
						if (!lstWithAdmin.Contains(sMODULE_NAME)) continue;
						if (sLAST_VIEW_NAME != sGRID_NAME)
						{
							bClearScript = false;
							sLAST_VIEW_NAME = sGRID_NAME;
							layout = new List<Dictionary<string, object>>();
							objs.Add(sLAST_VIEW_NAME, layout);
						}
						bool bIsReadable = true;
						if (SplendidInit.bEnableACLFieldSecurity && !Sql.IsEmptyString(sDATA_FIELD))
						{
							Security.ACL_FIELD_ACCESS acl = _security.GetUserFieldSecurity(sMODULE_NAME, sDATA_FIELD, Guid.Empty);
							bIsReadable = acl.IsReadable();
						}
						if (bClearScript)
							row["SCRIPT"] = DBNull.Value;
						bClearScript = true;
						if (bIsReadable)
						{
							var drow = new Dictionary<string, object>();
							for (int j = 0; j < dt.Columns.Count; j++)
							{
								if (dt.Columns[j].ColumnName == "ID") continue;
								drow.Add(dt.Columns[j].ColumnName, row[j]);
							}
							layout.Add(drow);
						}
					}
					_memoryCache.Set(sCacheKey, objs, _splendidCache.DefaultCacheExpiration());
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				throw;
			}
			return objs;
		}

		private Dictionary<string, object> GetAllDetailViewsFieldsInternal(List<string> lstMODULES)
		{
			string sModuleList = String.Join(",", lstMODULES.ToArray());
			string sCacheKey   = "vwDETAILVIEWS_FIELDS.ReactClient." + sModuleList;
			var objs = _memoryCache.Get<Dictionary<string, object>>(sCacheKey);
			if (objs != null) return objs;
			objs = new Dictionary<string, object>();
			try
			{
				if (_security.IsAuthenticated())
				{
					var lstWithAdmin = new List<string>(lstMODULES);
					lstWithAdmin.Add("Home"); lstWithAdmin.Add("Users"); lstWithAdmin.Add("Google");
					using IDbConnection con = _dbProviderFactories.CreateConnection();
					con.Open();
					using IDbCommand cmd = con.CreateCommand();
					cmd.CommandText =
						"select *                                          " + ControlChars.CrLf
					  + "  from vwDETAILVIEWS_FIELDS                       " + ControlChars.CrLf
					  + " where (DEFAULT_VIEW = 0 or DEFAULT_VIEW is null) " + ControlChars.CrLf
					  + " order by DETAIL_NAME, FIELD_INDEX                " + ControlChars.CrLf;
					using var da = _dbProviderFactories.CreateDataAdapter();
					((IDbDataAdapter)da).SelectCommand = cmd;
					using var dt = new DataTable();
					da.Fill(dt);
					bool   bClearScript    = false;
					string sLAST_VIEW_NAME = String.Empty;
					List<Dictionary<string, object>> layout = null;
					for (int i = 0; i < dt.Rows.Count; i++)
					{
						DataRow row         = dt.Rows[i];
						string sDETAIL_NAME = Sql.ToString(row["DETAIL_NAME"]);
						string sDATA_FIELD  = Sql.ToString(row["DATA_FIELD" ]);
						string sMODULE_NAME = sDETAIL_NAME.Split('.')[0];
						if (!lstWithAdmin.Contains(sMODULE_NAME)) continue;
						if (sLAST_VIEW_NAME != sDETAIL_NAME)
						{
							bClearScript          = false;
							sLAST_VIEW_NAME       = sDETAIL_NAME;
							layout                = new List<Dictionary<string, object>>();
							objs[sLAST_VIEW_NAME] = layout;
						}
						bool bIsReadable = true;
						if (SplendidInit.bEnableACLFieldSecurity && !Sql.IsEmptyString(sDATA_FIELD))
						{
							Security.ACL_FIELD_ACCESS acl = _security.GetUserFieldSecurity(sMODULE_NAME, sDATA_FIELD, Guid.Empty);
							bIsReadable = acl.IsReadable();
						}
						if (bClearScript) row["SCRIPT"] = DBNull.Value;
						bClearScript = true;
						if (bIsReadable)
						{
							var drow = new Dictionary<string, object>();
							for (int j = 0; j < dt.Columns.Count; j++)
							{
								if (dt.Columns[j].ColumnName == "ID") continue;
								drow[dt.Columns[j].ColumnName] = row[j];
							}
							layout.Add(drow);
						}
					}
					_memoryCache.Set(sCacheKey, objs, _splendidCache.DefaultCacheExpiration());
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				throw;
			}
			return objs;
		}

		private Dictionary<string, object> GetAllEditViewsFieldsInternal(List<string> lstMODULES)
		{
			string sModuleList = String.Join(",", lstMODULES.ToArray());
			string sCacheKey   = "vwEDITVIEWS_FIELDS_SearchView.ReactClient." + sModuleList;
			var objs = _memoryCache.Get<Dictionary<string, object>>(sCacheKey);
			if (objs != null) return objs;
			objs = new Dictionary<string, object>();
			try
			{
				if (_security.IsAuthenticated())
				{
					var lstWithAdmin = new List<string>(lstMODULES);
					lstWithAdmin.Add("Home"); lstWithAdmin.Add("Users");
					lstWithAdmin.Add("Google"); lstWithAdmin.Add("Configurator");
					using IDbConnection con = _dbProviderFactories.CreateConnection();
					con.Open();
					using IDbCommand cmd = con.CreateCommand();
					cmd.CommandText =
						"select *                                          " + ControlChars.CrLf
					  + "  from vwEDITVIEWS_FIELDS_SearchView              " + ControlChars.CrLf
					  + " where (DEFAULT_VIEW = 0 or DEFAULT_VIEW is null) " + ControlChars.CrLf
					  + " order by EDIT_NAME, FIELD_INDEX                  " + ControlChars.CrLf;
					using var da = _dbProviderFactories.CreateDataAdapter();
					((IDbDataAdapter)da).SelectCommand = cmd;
					using var dt = new DataTable();
					da.Fill(dt);
					bool   bClearScript    = false;
					string sLAST_VIEW_NAME = String.Empty;
					List<Dictionary<string, object>> layout = null;
					for (int i = 0; i < dt.Rows.Count; i++)
					{
						DataRow row        = dt.Rows[i];
						string sEDIT_NAME  = Sql.ToString(row["EDIT_NAME" ]);
						string sDATA_FIELD = Sql.ToString(row["DATA_FIELD"]);
						string sMODULE_NAME = sEDIT_NAME.Split('.')[0];
						if (!lstWithAdmin.Contains(sMODULE_NAME)) continue;
						if (sLAST_VIEW_NAME != sEDIT_NAME)
						{
							bClearScript          = false;
							sLAST_VIEW_NAME       = sEDIT_NAME;
							layout                = new List<Dictionary<string, object>>();
							objs[sLAST_VIEW_NAME] = layout;
						}
						bool bIsWritable = true;
						if (SplendidInit.bEnableACLFieldSecurity && !Sql.IsEmptyString(sDATA_FIELD))
						{
							Security.ACL_FIELD_ACCESS acl = _security.GetUserFieldSecurity(sMODULE_NAME, sDATA_FIELD, Guid.Empty);
							bIsWritable = acl.IsWriteable();
						}
						if (bClearScript) row["SCRIPT"] = DBNull.Value;
						bClearScript = true;
						if (bIsWritable)
						{
							var drow = new Dictionary<string, object>();
							for (int j = 0; j < dt.Columns.Count; j++)
							{
								if (dt.Columns[j].ColumnName == "ID") continue;
								drow[dt.Columns[j].ColumnName] = row[j];
							}
							layout.Add(drow);
						}
					}
					_memoryCache.Set(sCacheKey, objs, _splendidCache.DefaultCacheExpiration());
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				throw;
			}
			return objs;
		}

		private Dictionary<string, object> GetAllDetailViewsRelationshipsInternal(List<string> lstMODULES)
		{
			string sModuleList = String.Join(",", lstMODULES.ToArray());
			string sCacheKey   = "vwDETAILVIEWS_RELATIONSHIPS.ReactClient." + sModuleList;
			var objs = _memoryCache.Get<Dictionary<string, object>>(sCacheKey);
			if (objs != null) return objs;
			objs = new Dictionary<string, object>();
			try
			{
				if (_security.IsAuthenticated())
				{
					using IDbConnection con = _dbProviderFactories.CreateConnection();
					con.Open();
					using IDbCommand cmd = con.CreateCommand();
					cmd.CommandText =
						"select *                                        " + ControlChars.CrLf
					  + "  from vwDETAILVIEWS_RELATIONSHIPS              " + ControlChars.CrLf
					  + " order by DETAIL_NAME, RELATIONSHIP_ORDER       " + ControlChars.CrLf;
					using var da = _dbProviderFactories.CreateDataAdapter();
					((IDbDataAdapter)da).SelectCommand = cmd;
					using var dt = new DataTable();
					da.Fill(dt);
					string sLAST_VIEW_NAME = String.Empty;
					List<Dictionary<string, object>> layout = null;
					foreach (DataRow row in dt.Rows)
					{
						string sDETAIL_NAME = Sql.ToString(row["DETAIL_NAME"]);
						string sMODULE_NAME = String.Empty;
						string[] arr        = sDETAIL_NAME.Split('.');
						sMODULE_NAME        = arr.Length > 0 ? arr[0] : String.Empty;
						if (!lstMODULES.Contains(sMODULE_NAME)) continue;
						if (sLAST_VIEW_NAME != sDETAIL_NAME)
						{
							sLAST_VIEW_NAME       = sDETAIL_NAME;
							layout                = new List<Dictionary<string, object>>();
							objs[sLAST_VIEW_NAME] = layout;
						}
						var drow = new Dictionary<string, object>();
						for (int j = 0; j < dt.Columns.Count; j++)
						{
							if (dt.Columns[j].ColumnName == "ID") continue;
							drow[dt.Columns[j].ColumnName] = row[j];
						}
						layout.Add(drow);
					}
					_memoryCache.Set(sCacheKey, objs, _splendidCache.DefaultCacheExpiration());
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				throw;
			}
			return objs;
		}

		private Dictionary<string, object> GetAllEditViewsRelationshipsInternal(List<string> lstMODULES)
		{
			string sModuleList = String.Join(",", lstMODULES.ToArray());
			string sCacheKey   = "vwEDITVIEWS_RELATIONSHIPS.ReactClient." + sModuleList;
			var objs = _memoryCache.Get<Dictionary<string, object>>(sCacheKey);
			if (objs != null) return objs;
			objs = new Dictionary<string, object>();
			try
			{
				if (_security.IsAuthenticated())
				{
					using IDbConnection con = _dbProviderFactories.CreateConnection();
					con.Open();
					using IDbCommand cmd = con.CreateCommand();
					cmd.CommandText =
						"select *                                      " + ControlChars.CrLf
					  + "  from vwEDITVIEWS_RELATIONSHIPS               " + ControlChars.CrLf
					  + " order by EDIT_NAME, RELATIONSHIP_ORDER        " + ControlChars.CrLf;
					using var da = _dbProviderFactories.CreateDataAdapter();
					((IDbDataAdapter)da).SelectCommand = cmd;
					using var dt = new DataTable();
					da.Fill(dt);
					string sLAST_VIEW_NAME = String.Empty;
					List<Dictionary<string, object>> layout = null;
					foreach (DataRow row in dt.Rows)
					{
						string sEDIT_NAME   = Sql.ToString(row["EDIT_NAME"]);
						string sMODULE_NAME = String.Empty;
						string[] arr        = sEDIT_NAME.Split('.');
						sMODULE_NAME        = arr.Length > 0 ? arr[0] : String.Empty;
						if (!lstMODULES.Contains(sMODULE_NAME)) continue;
						if (sLAST_VIEW_NAME != sEDIT_NAME)
						{
							sLAST_VIEW_NAME       = sEDIT_NAME;
							layout                = new List<Dictionary<string, object>>();
							objs[sLAST_VIEW_NAME] = layout;
						}
						var drow = new Dictionary<string, object>();
						for (int j = 0; j < dt.Columns.Count; j++)
						{
							if (dt.Columns[j].ColumnName == "ID") continue;
							drow[dt.Columns[j].ColumnName] = row[j];
						}
						layout.Add(drow);
					}
					_memoryCache.Set(sCacheKey, objs, _splendidCache.DefaultCacheExpiration());
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				throw;
			}
			return objs;
		}

		private Dictionary<string, object> GetAllDynamicButtonsInternal(List<string> lstMODULES)
		{
			string sModuleList = String.Join(",", lstMODULES.ToArray());
			string sCacheKey   = "vwDYNAMIC_BUTTONS.ReactClient." + sModuleList;
			var objs = _memoryCache.Get<Dictionary<string, object>>(sCacheKey);
			if (objs != null) return objs;
			objs = new Dictionary<string, object>();
			try
			{
				if (_security.IsAuthenticated())
				{
					var lstWithAdmin = new List<string>(lstMODULES);
					lstWithAdmin.Add("Google");
					using IDbConnection con = _dbProviderFactories.CreateConnection();
					con.Open();
					using IDbCommand cmd = con.CreateCommand();
					cmd.CommandText =
						"select *                                          " + ControlChars.CrLf
					  + "  from vwDYNAMIC_BUTTONS                          " + ControlChars.CrLf
					  + " where (DEFAULT_VIEW = 0 or DEFAULT_VIEW is null) " + ControlChars.CrLf
					  + " order by VIEW_NAME, CONTROL_INDEX                " + ControlChars.CrLf;
					using var da = _dbProviderFactories.CreateDataAdapter();
					((IDbDataAdapter)da).SelectCommand = cmd;
					using var dt = new DataTable();
					da.Fill(dt);
					if (!dt.Columns.Contains("MODULE_ACLACCESS" )) dt.Columns.Add("MODULE_ACLACCESS" , typeof(string));
					if (!dt.Columns.Contains("TARGET_ACLACCESS" )) dt.Columns.Add("TARGET_ACLACCESS" , typeof(string));
					string sLAST_VIEW_NAME = String.Empty;
					List<Dictionary<string, object>> layout = null;
					foreach (DataRow row in dt.Rows)
					{
						string sVIEW_NAME   = Sql.ToString(row["VIEW_NAME"  ]);
						string sMODULE_NAME = Sql.ToString(row["MODULE_NAME"]);
						bool   bADMIN_ONLY  = Sql.ToBoolean(row["ADMIN_ONLY"]);
						if (bADMIN_ONLY && !_security.IS_ADMIN) continue;
						if (!lstWithAdmin.Contains(sMODULE_NAME)) continue;
						int nMODULE_ACLACCESS = _security.GetUserAccess(sMODULE_NAME, "edit");
						row["MODULE_ACLACCESS"] = nMODULE_ACLACCESS.ToString();
						string sTARGET_MODULE = Sql.ToString(row["TARGET_MODULE"]);
						int nTARGET_ACLACCESS = Sql.IsEmptyString(sTARGET_MODULE) ? -1 : _security.GetUserAccess(sTARGET_MODULE, "edit");
						row["TARGET_ACLACCESS"] = nTARGET_ACLACCESS.ToString();
						if (sLAST_VIEW_NAME != sVIEW_NAME)
						{
							sLAST_VIEW_NAME       = sVIEW_NAME;
							layout                = new List<Dictionary<string, object>>();
							objs[sLAST_VIEW_NAME] = layout;
						}
						var drow = new Dictionary<string, object>();
						for (int j = 0; j < dt.Columns.Count; j++)
						{
							if (dt.Columns[j].ColumnName == "ID") continue;
							drow[dt.Columns[j].ColumnName] = row[j];
						}
						layout.Add(drow);
					}
					_memoryCache.Set(sCacheKey, objs, _splendidCache.DefaultCacheExpiration());
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				throw;
			}
			return objs;
		}

		private Dictionary<string, object> GetAllTerminologyInternal(List<string> lstMODULES, bool bAdmin)
		{
			string sModuleList = String.Join(",", lstMODULES.ToArray());
			string sCacheKey   = "vwTERMINOLOGY.ReactClient." + sModuleList + (bAdmin ? ".Admin" : "");
			var objs = _memoryCache.Get<Dictionary<string, object>>(sCacheKey);
			if (objs != null) return objs;
			objs = new Dictionary<string, object>();
			try
			{
				if (_security.IsAuthenticated())
				{
					string sCulture = GetUserCulture();
					using IDbConnection con = _dbProviderFactories.CreateConnection();
					con.Open();
					using IDbCommand cmd = con.CreateCommand();
					cmd.CommandText =
						"select MODULE_NAME, NAME, DISPLAY_NAME          " + ControlChars.CrLf
					  + "  from vwTERMINOLOGY                             " + ControlChars.CrLf
					  + " where (LIST_NAME is null or LIST_NAME = '')     " + ControlChars.CrLf
					  + "   and LANG = @LANG                             " + ControlChars.CrLf
					  + " order by MODULE_NAME, NAME                     " + ControlChars.CrLf;
					Sql.AddParameter(cmd, "@LANG", sCulture);
					using var da = _dbProviderFactories.CreateDataAdapter();
					((IDbDataAdapter)da).SelectCommand = cmd;
					using var dt = new DataTable();
					da.Fill(dt);
					foreach (DataRow row in dt.Rows)
					{
						string sMODULE_NAME  = Sql.ToString(row["MODULE_NAME" ]);
						string sNAME         = Sql.ToString(row["NAME"        ]);
						string sDISPLAY_NAME = Sql.ToString(row["DISPLAY_NAME"]);
						if (!Sql.IsEmptyString(sMODULE_NAME) && !lstMODULES.Contains(sMODULE_NAME) && !(bAdmin && _security.IS_ADMIN))
							continue;
						string sKey = Sql.IsEmptyString(sMODULE_NAME) ? sNAME : sMODULE_NAME + "." + sNAME;
						objs[sKey] = sDISPLAY_NAME;
					}
					_memoryCache.Set(sCacheKey, objs, _splendidCache.DefaultCacheExpiration());
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				throw;
			}
			return objs;
		}

		private Dictionary<string, object> GetAllTerminologyListsInternal(bool bAdmin)
		{
			string sCacheKey = "vwTERMINOLOGY_LISTS.ReactClient" + (bAdmin ? ".Admin" : "");
			var objs = _memoryCache.Get<Dictionary<string, object>>(sCacheKey);
			if (objs != null) return objs;
			objs = new Dictionary<string, object>();
			try
			{
				if (_security.IsAuthenticated())
				{
					string sCulture = GetUserCulture();
					using IDbConnection con = _dbProviderFactories.CreateConnection();
					con.Open();
					using IDbCommand cmd = con.CreateCommand();
					cmd.CommandText =
						"select LIST_NAME, NAME, DISPLAY_NAME               " + ControlChars.CrLf
					  + "  from vwTERMINOLOGY                                " + ControlChars.CrLf
					  + " where (LIST_NAME is not null and LIST_NAME <> '')  " + ControlChars.CrLf
					  + "   and LANG = @LANG                                " + ControlChars.CrLf
					  + " order by LIST_NAME, NAME                          " + ControlChars.CrLf;
					Sql.AddParameter(cmd, "@LANG", sCulture);
					using var da = _dbProviderFactories.CreateDataAdapter();
					((IDbDataAdapter)da).SelectCommand = cmd;
					using var dt = new DataTable();
					da.Fill(dt);
					string sLAST_LIST_NAME = String.Empty;
					Dictionary<string, string> listItems = null;
					foreach (DataRow row in dt.Rows)
					{
						string sLIST_NAME    = Sql.ToString(row["LIST_NAME"   ]);
						string sNAME         = Sql.ToString(row["NAME"        ]);
						string sDISPLAY_NAME = Sql.ToString(row["DISPLAY_NAME"]);
						if (sLAST_LIST_NAME != sLIST_NAME)
						{
							sLAST_LIST_NAME  = sLIST_NAME;
							listItems        = new Dictionary<string, string>();
							objs[sLIST_NAME] = listItems;
						}
						listItems[sNAME] = sDISPLAY_NAME;
					}
					_memoryCache.Set(sCacheKey, objs, _splendidCache.DefaultCacheExpiration());
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				throw;
			}
			return objs;
		}

		private Dictionary<string, object> GetModuleAccessInternal(List<string> lstMODULES)
		{
			string sCacheKey = "ACL_ACCESS.ReactClient." + _security.USER_ID.ToString();
			var objs = _memoryCache.Get<Dictionary<string, object>>(sCacheKey);
			if (objs != null) return objs;
			objs = new Dictionary<string, object>();
			try
			{
				if (_security.IsAuthenticated())
				{
					using IDbConnection con = _dbProviderFactories.CreateConnection();
					con.Open();
					using IDbCommand cmd = con.CreateCommand();
					cmd.CommandText =
						"select MODULE_NAME, ACLTYPE, ACLACCESS " + ControlChars.CrLf
					  + "  from vwACL_ACCESS_ByModule             " + ControlChars.CrLf
					  + " where USER_ID = @USER_ID               " + ControlChars.CrLf
					  + " order by MODULE_NAME                   " + ControlChars.CrLf;
					Sql.AddParameter(cmd, "@USER_ID", _security.USER_ID);
					using var da = _dbProviderFactories.CreateDataAdapter();
					((IDbDataAdapter)da).SelectCommand = cmd;
					using var dt = new DataTable();
					da.Fill(dt);
					foreach (DataRow row in dt.Rows)
					{
						string sMODULE_NAME = Sql.ToString(row["MODULE_NAME"]);
						string sACLTYPE     = Sql.ToString(row["ACLTYPE"    ]);
						int    nACLACCESS   = Sql.ToInteger(row["ACLACCESS" ]);
						if (!objs.ContainsKey(sMODULE_NAME))
							objs[sMODULE_NAME] = new Dictionary<string, int>();
						((Dictionary<string, int>)objs[sMODULE_NAME])[sACLTYPE] = nACLACCESS;
					}
					_memoryCache.Set(sCacheKey, objs, _splendidCache.DefaultCacheExpiration());
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				throw;
			}
			return objs;
		}

		private List<object> GetUserACLRolesInternal()
		{
			string sCacheKey = "ACL_ROLES_USERS.ReactClient." + _security.USER_ID.ToString();
			var lst = _memoryCache.Get<List<object>>(sCacheKey);
			if (lst != null) return lst;
			lst = new List<object>();
			try
			{
				if (_security.IsAuthenticated())
				{
					using IDbConnection con = _dbProviderFactories.CreateConnection();
					con.Open();
					using IDbCommand cmd = con.CreateCommand();
					cmd.CommandText =
						"select *                              " + ControlChars.CrLf
					  + "  from vwACL_ROLES_USERS               " + ControlChars.CrLf
					  + " where USER_ID = @USER_ID             " + ControlChars.CrLf;
					Sql.AddParameter(cmd, "@USER_ID", _security.USER_ID);
					using var da = _dbProviderFactories.CreateDataAdapter();
					((IDbDataAdapter)da).SelectCommand = cmd;
					using var dt = new DataTable();
					da.Fill(dt);
					foreach (DataRow row in dt.Rows)
					{
						var drow = new Dictionary<string, object>();
						for (int j = 0; j < dt.Columns.Count; j++)
							drow[dt.Columns[j].ColumnName] = row[j];
						lst.Add(drow);
					}
					_memoryCache.Set(sCacheKey, lst, _splendidCache.DefaultCacheExpiration());
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				throw;
			}
			return lst;
		}

		private Dictionary<string, object> GetAllShortcutsInternal(List<string> lstMODULES)
		{
			string sModuleList = String.Join(",", lstMODULES.ToArray());
			string sCacheKey   = "vwSHORTCUTS.ReactClient." + sModuleList;
			var objs = _memoryCache.Get<Dictionary<string, object>>(sCacheKey);
			if (objs != null) return objs;
			objs = new Dictionary<string, object>();
			try
			{
				if (_security.IsAuthenticated())
				{
					using IDbConnection con = _dbProviderFactories.CreateConnection();
					con.Open();
					using IDbCommand cmd = con.CreateCommand();
					cmd.CommandText =
						"select *                          " + ControlChars.CrLf
					  + "  from vwSHORTCUTS                " + ControlChars.CrLf
					  + " order by MODULE_NAME, ORDER_INDEX" + ControlChars.CrLf;
					using var da = _dbProviderFactories.CreateDataAdapter();
					((IDbDataAdapter)da).SelectCommand = cmd;
					using var dt = new DataTable();
					da.Fill(dt);
					string sLAST_MODULE = String.Empty;
					List<Dictionary<string, object>> moduleShortcuts = null;
					foreach (DataRow row in dt.Rows)
					{
						string sMODULE_NAME = Sql.ToString(row["MODULE_NAME"]);
						if (!lstMODULES.Contains(sMODULE_NAME)) continue;
						if (sLAST_MODULE != sMODULE_NAME)
						{
							sLAST_MODULE       = sMODULE_NAME;
							moduleShortcuts    = new List<Dictionary<string, object>>();
							objs[sMODULE_NAME] = moduleShortcuts;
						}
						var drow = new Dictionary<string, object>();
						for (int j = 0; j < dt.Columns.Count; j++)
							drow[dt.Columns[j].ColumnName] = row[j];
						moduleShortcuts.Add(drow);
					}
					_memoryCache.Set(sCacheKey, objs, _splendidCache.DefaultCacheExpiration());
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				throw;
			}
			return objs;
		}

		private Dictionary<string, object> GetAllSearchColumnsInternal(List<string> lstMODULES)
		{
			string sModuleList = String.Join(",", lstMODULES.ToArray());
			string sCacheKey   = "vwSEARCH_COLUMNS.ReactClient." + sModuleList;
			var objs = _memoryCache.Get<Dictionary<string, object>>(sCacheKey);
			if (objs != null) return objs;
			objs = new Dictionary<string, object>();
			try
			{
				if (_security.IsAuthenticated())
				{
					using IDbConnection con = _dbProviderFactories.CreateConnection();
					con.Open();
					using IDbCommand cmd = con.CreateCommand();
					cmd.CommandText =
						"select *                             " + ControlChars.CrLf
					  + "  from vwSEARCH_COLUMNS               " + ControlChars.CrLf
					  + " order by MODULE_NAME, COLUMN_INDEX   " + ControlChars.CrLf;
					using var da = _dbProviderFactories.CreateDataAdapter();
					((IDbDataAdapter)da).SelectCommand = cmd;
					using var dt = new DataTable();
					da.Fill(dt);
					string sLAST_MODULE = String.Empty;
					List<Dictionary<string, object>> cols = null;
					foreach (DataRow row in dt.Rows)
					{
						string sMODULE_NAME = Sql.ToString(row["MODULE_NAME"]);
						if (!lstMODULES.Contains(sMODULE_NAME)) continue;
						if (sLAST_MODULE != sMODULE_NAME)
						{
							sLAST_MODULE       = sMODULE_NAME;
							cols               = new List<Dictionary<string, object>>();
							objs[sMODULE_NAME] = cols;
						}
						var drow = new Dictionary<string, object>();
						for (int j = 0; j < dt.Columns.Count; j++)
							drow[dt.Columns[j].ColumnName] = row[j];
						cols.Add(drow);
					}
					_memoryCache.Set(sCacheKey, objs, _splendidCache.DefaultCacheExpiration());
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				throw;
			}
			return objs;
		}

		private List<object> GetAllUsersInternal()
		{
			string sCacheKey = "vwUSERS.ReactClient";
			var lst = _memoryCache.Get<List<object>>(sCacheKey);
			if (lst != null) return lst;
			lst = new List<object>();
			try
			{
				if (_security.IsAuthenticated())
				{
					using IDbConnection con = _dbProviderFactories.CreateConnection();
					con.Open();
					using IDbCommand cmd = con.CreateCommand();
					cmd.CommandText =
						"select USER_ID, USER_NAME, FULL_NAME, STATUS " + ControlChars.CrLf
					  + "  from vwUSERS_List                           " + ControlChars.CrLf
					  + " where STATUS = 'Active'                      " + ControlChars.CrLf
					  + " order by FULL_NAME                           " + ControlChars.CrLf;
					using var da = _dbProviderFactories.CreateDataAdapter();
					((IDbDataAdapter)da).SelectCommand = cmd;
					using var dt = new DataTable();
					da.Fill(dt);
					foreach (DataRow row in dt.Rows)
					{
						var drow = new Dictionary<string, object>();
						for (int j = 0; j < dt.Columns.Count; j++)
							drow[dt.Columns[j].ColumnName] = row[j];
						lst.Add(drow);
					}
					_memoryCache.Set(sCacheKey, lst, _splendidCache.DefaultCacheExpiration());
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				throw;
			}
			return lst;
		}

		private List<object> GetAllTeamsInternal()
		{
			string sCacheKey = "vwTEAMS.ReactClient";
			var lst = _memoryCache.Get<List<object>>(sCacheKey);
			if (lst != null) return lst;
			lst = new List<object>();
			try
			{
				if (_security.IsAuthenticated())
				{
					using IDbConnection con = _dbProviderFactories.CreateConnection();
					con.Open();
					using IDbCommand cmd = con.CreateCommand();
					cmd.CommandText =
						"select TEAM_ID, NAME, DESCRIPTION " + ControlChars.CrLf
					  + "  from vwTEAMS_List                " + ControlChars.CrLf
					  + " order by NAME                     " + ControlChars.CrLf;
					using var da = _dbProviderFactories.CreateDataAdapter();
					((IDbDataAdapter)da).SelectCommand = cmd;
					using var dt = new DataTable();
					da.Fill(dt);
					foreach (DataRow row in dt.Rows)
					{
						var drow = new Dictionary<string, object>();
						for (int j = 0; j < dt.Columns.Count; j++)
							drow[dt.Columns[j].ColumnName] = row[j];
						lst.Add(drow);
					}
					_memoryCache.Set(sCacheKey, lst, _splendidCache.DefaultCacheExpiration());
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				throw;
			}
			return lst;
		}

		private Dictionary<string, object> GetAllReactCustomViewsInternal(List<string> lstMODULES)
		{
			string sModuleList = String.Join(",", lstMODULES.ToArray());
			string sCacheKey   = "vwREACT_CUSTOM_VIEWS.ReactClient." + sModuleList;
			var objs = _memoryCache.Get<Dictionary<string, object>>(sCacheKey);
			if (objs != null) return objs;
			objs = new Dictionary<string, object>();
			try
			{
				if (_security.IsAuthenticated())
				{
					using IDbConnection con = _dbProviderFactories.CreateConnection();
					con.Open();
					using IDbCommand cmd = con.CreateCommand();
					cmd.CommandText =
						"select *                      " + ControlChars.CrLf
					  + "  from vwREACT_CUSTOM_VIEWS    " + ControlChars.CrLf
					  + " order by VIEW_NAME            " + ControlChars.CrLf;
					using var da = _dbProviderFactories.CreateDataAdapter();
					((IDbDataAdapter)da).SelectCommand = cmd;
					using var dt = new DataTable();
					da.Fill(dt);
					foreach (DataRow row in dt.Rows)
					{
						string sVIEW_NAME   = Sql.ToString(row["VIEW_NAME"  ]);
						string sMODULE_NAME = Sql.ToString(row["MODULE_NAME"]);
						if (!lstMODULES.Contains(sMODULE_NAME)) continue;
						var drow = new Dictionary<string, object>();
						for (int j = 0; j < dt.Columns.Count; j++)
							drow[dt.Columns[j].ColumnName] = row[j];
						objs[sVIEW_NAME] = drow;
					}
					_memoryCache.Set(sCacheKey, objs, _splendidCache.DefaultCacheExpiration());
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				throw;
			}
			return objs;
		}

		private Dictionary<string, object> GetAllFavoritesInternal()
		{
			var objs = new Dictionary<string, object>();
			try
			{
				if (!_security.IsAuthenticated()) return objs;
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                               " + ControlChars.CrLf
				  + "  from vwSUGARFAVORITES                " + ControlChars.CrLf
				  + " where CREATED_BY = @CREATED_BY        " + ControlChars.CrLf
				  + " order by MODULE_NAME                  " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@CREATED_BY", _security.USER_ID);
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				string sLAST_MODULE = String.Empty;
				List<Dictionary<string, object>> modFavs = null;
				foreach (DataRow row in dt.Rows)
				{
					string sMODULE_NAME = Sql.ToString(row["MODULE_NAME"]);
					if (sLAST_MODULE != sMODULE_NAME)
					{
						sLAST_MODULE       = sMODULE_NAME;
						modFavs            = new List<Dictionary<string, object>>();
						objs[sMODULE_NAME] = modFavs;
					}
					var drow = new Dictionary<string, object>();
					for (int j = 0; j < dt.Columns.Count; j++)
						drow[dt.Columns[j].ColumnName] = row[j];
					modFavs.Add(drow);
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
			}
			return objs;
		}

		private Dictionary<string, object> GetAllSavedSearchInternal()
		{
			var objs = new Dictionary<string, object>();
			try
			{
				if (!_security.IsAuthenticated()) return objs;
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                    " + ControlChars.CrLf
				  + "  from vwSAVED_SEARCH                       " + ControlChars.CrLf
				  + " where ASSIGNED_USER_ID = @ASSIGNED_USER_ID " + ControlChars.CrLf
				  + " order by MODULE_NAME, NAME                 " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@ASSIGNED_USER_ID", _security.USER_ID);
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				string sLAST_MODULE = String.Empty;
				List<Dictionary<string, object>> searches = null;
				foreach (DataRow row in dt.Rows)
				{
					string sMODULE_NAME = Sql.ToString(row["MODULE_NAME"]);
					if (sLAST_MODULE != sMODULE_NAME)
					{
						sLAST_MODULE       = sMODULE_NAME;
						searches           = new List<Dictionary<string, object>>();
						objs[sMODULE_NAME] = searches;
					}
					var drow = new Dictionary<string, object>();
					for (int j = 0; j < dt.Columns.Count; j++)
						drow[dt.Columns[j].ColumnName] = row[j];
					searches.Add(drow);
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
			}
			return objs;
		}

		private List<object> GetAllDashboardsInternal()
		{
			var lst = new List<object>();
			try
			{
				if (!_security.IsAuthenticated()) return lst;
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                    " + ControlChars.CrLf
				  + "  from vwDASHBOARDS                         " + ControlChars.CrLf
				  + " where ASSIGNED_USER_ID = @ASSIGNED_USER_ID " + ControlChars.CrLf
				  + " order by DATE_MODIFIED desc               " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@ASSIGNED_USER_ID", _security.USER_ID);
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				foreach (DataRow row in dt.Rows)
				{
					var drow = new Dictionary<string, object>();
					for (int j = 0; j < dt.Columns.Count; j++)
						drow[dt.Columns[j].ColumnName] = row[j];
					lst.Add(drow);
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
			}
			return lst;
		}

		private Dictionary<string, object> GetAllDashboardPanelsInternal()
		{
			var objs = new Dictionary<string, object>();
			try
			{
				if (!_security.IsAuthenticated()) return objs;
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                       " + ControlChars.CrLf
				  + "  from vwDASHBOARDS_PANELS                     " + ControlChars.CrLf
				  + " where ASSIGNED_USER_ID = @ASSIGNED_USER_ID    " + ControlChars.CrLf
				  + " order by DASHBOARD_ID, PANEL_ORDER           " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@ASSIGNED_USER_ID", _security.USER_ID);
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				string sLAST_DASHBOARD = String.Empty;
				List<Dictionary<string, object>> panels = null;
				foreach (DataRow row in dt.Rows)
				{
					string sDASHBOARD_ID = Sql.ToString(row["DASHBOARD_ID"]);
					if (sLAST_DASHBOARD != sDASHBOARD_ID)
					{
						sLAST_DASHBOARD     = sDASHBOARD_ID;
						panels              = new List<Dictionary<string, object>>();
						objs[sDASHBOARD_ID] = panels;
					}
					var drow = new Dictionary<string, object>();
					for (int j = 0; j < dt.Columns.Count; j++)
						drow[dt.Columns[j].ColumnName] = row[j];
					panels.Add(drow);
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
			}
			return objs;
		}

		private List<object> GetUserSignaturesInternal()
		{
			var lst = new List<object>();
			try
			{
				if (!_security.IsAuthenticated()) return lst;
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                              " + ControlChars.CrLf
				  + "  from vwUSERS_SIGNATURES              " + ControlChars.CrLf
				  + " where CREATED_BY = @CREATED_BY       " + ControlChars.CrLf
				  + " order by DATE_MODIFIED desc          " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@CREATED_BY", _security.USER_ID);
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				foreach (DataRow row in dt.Rows)
				{
					var drow = new Dictionary<string, object>();
					for (int j = 0; j < dt.Columns.Count; j++)
						drow[dt.Columns[j].ColumnName] = row[j];
					lst.Add(drow);
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
			}
			return lst;
		}

		private List<object> GetOutboundMailInternal()
		{
			var lst = new List<object>();
			try
			{
				if (!_security.IsAuthenticated()) return lst;
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                       " + ControlChars.CrLf
				  + "  from vwOUTBOUND_EMAILS                       " + ControlChars.CrLf
				  + " where (USER_ID is null or USER_ID = @USER_ID) " + ControlChars.CrLf
				  + " order by NAME                                 " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@USER_ID", _security.USER_ID);
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				foreach (DataRow row in dt.Rows)
				{
					var drow = new Dictionary<string, object>();
					for (int j = 0; j < dt.Columns.Count; j++)
					{
						string sColumnName = dt.Columns[j].ColumnName;
						if (sColumnName == "MAIL_SMTPPASS")
							drow[sColumnName] = Sql.IsEmptyString(row[j]) ? (object)"" : Sql.sEMPTY_PASSWORD;
						else
							drow[sColumnName] = row[j];
					}
					lst.Add(drow);
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
			}
			return lst;
		}

		private List<object> GetOutboundSmsInternal()
		{
			var lst = new List<object>();
			try
			{
				if (!_security.IsAuthenticated()) return lst;
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                         " + ControlChars.CrLf
				  + "  from vwOUTBOUND_SMS                            " + ControlChars.CrLf
				  + " where (USER_ID is null or USER_ID = @USER_ID)   " + ControlChars.CrLf
				  + " order by NAME                                   " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@USER_ID", _security.USER_ID);
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				foreach (DataRow row in dt.Rows)
				{
					var drow = new Dictionary<string, object>();
					for (int j = 0; j < dt.Columns.Count; j++)
						drow[dt.Columns[j].ColumnName] = row[j];
					lst.Add(drow);
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
			}
			return lst;
		}

		private Dictionary<string, object> GetAllLastViewedInternal()
		{
			var objs = new Dictionary<string, object>();
			try
			{
				if (!_security.IsAuthenticated()) return objs;
				Dictionary<string, DataTable> dtLastViewed = _splendidCache.GetAllLastViewed();
				foreach (string key in dtLastViewed.Keys)
				{
					var rows = new List<Dictionary<string, object>>();
					DataTable dt = dtLastViewed[key];
					foreach (DataRow row in dt.Rows)
					{
						var drow = new Dictionary<string, object>();
						for (int j = 0; j < dt.Columns.Count; j++)
							drow[dt.Columns[j].ColumnName] = row[j];
						rows.Add(drow);
					}
					objs[key] = rows;
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
			}
			return objs;
		}

		private Dictionary<string, object> GetAllTabMenusInternal()
		{
			var objs = new Dictionary<string, object>();
			try
			{
				if (!_security.IsAuthenticated()) return objs;
				Dictionary<Guid, DataTable> dtTabMenus = _splendidCache.GetAllTabMenus();
				foreach (Guid key in dtTabMenus.Keys)
				{
					var rows = new List<Dictionary<string, object>>();
					DataTable dt = dtTabMenus[key];
					foreach (DataRow row in dt.Rows)
					{
						var drow = new Dictionary<string, object>();
						for (int j = 0; j < dt.Columns.Count; j++)
							drow[dt.Columns[j].ColumnName] = row[j];
						rows.Add(drow);
					}
					objs[key.ToString().ToLower()] = rows;
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
			}
			return objs;
		}

		private List<object> GetTaxRatesInternal()
		{
			var lst = new List<object>();
			try
			{
				DataTable dt = _splendidCache.TaxRates();
				foreach (DataRow row in dt.Rows)
				{
					var drow = new Dictionary<string, object>();
					for (int j = 0; j < dt.Columns.Count; j++)
						drow[dt.Columns[j].ColumnName] = row[j];
					lst.Add(drow);
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
			}
			return lst;
		}

		private List<object> GetDiscountsInternal()
		{
			var lst = new List<object>();
			try
			{
				DataTable dt = _splendidCache.Discounts();
				foreach (DataRow row in dt.Rows)
				{
					var drow = new Dictionary<string, object>();
					for (int j = 0; j < dt.Columns.Count; j++)
						drow[dt.Columns[j].ColumnName] = row[j];
					lst.Add(drow);
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
			}
			return lst;
		}

		/// <summary>
		/// DuoUniversal redirect URL generator (private helper).
		/// Preserves original login flow from Rest.svc.cs lines 329-375.
		/// </summary>
		private string GetDuoRedirectUrl(string sUSER_NAME, Guid gUSER_ID)
		{
			try
			{
				string sDUO_INTEGRATION_KEY = _configuration["DUO_INTEGRATION_KEY"];
				string sDUO_SECRET_KEY      = _configuration["DUO_SECRET_KEY"     ];
				string sDUO_API_HOSTNAME    = _configuration["DUO_API_HOSTNAME"   ];
				if (Sql.IsEmptyString(sDUO_INTEGRATION_KEY) || Sql.IsEmptyString(sDUO_SECRET_KEY) || Sql.IsEmptyString(sDUO_API_HOSTNAME))
					return String.Empty;
				string sSiteURL     = Crm.Config.SiteURL(_memoryCache);
				string sRedirectUri = sSiteURL.TrimEnd('/') + "/Rest.svc/LoginDuoUniversal";
				var client = new DuoUniversal.ClientBuilder(sDUO_INTEGRATION_KEY, sDUO_SECRET_KEY, sDUO_API_HOSTNAME, sRedirectUri).Build();
				string sState = DuoUniversal.Client.GenerateState();
				var session = _httpContextAccessor.HttpContext?.Session;
				if (session != null)
				{
					session.SetString("DuoUniversal.state"   , sState     );
					session.SetString("DuoUniversal.username", sUSER_NAME );
					session.SetString("DuoUniversal.UserID"  , gUSER_ID.ToString());
				}
				return client.GenerateAuthUri(sUSER_NAME, sState);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return String.Empty;
			}
		}

		// =====================================================================
		// #region Scalar functions
		// =====================================================================

		/// <summary>POST Rest.svc/Version — Returns application version string.</summary>
		[HttpPost("Version")]
		public IActionResult Version()
		{
			try
			{
				string sVersion = Sql.ToString(_memoryCache.Get("SplendidVersion"));
				return JsonContent(new { d = sVersion });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>POST Rest.svc/Edition — Returns service level / edition string.</summary>
		[HttpPost("Edition")]
		public IActionResult Edition()
		{
			try
			{
				string sEdition = Sql.ToString(_memoryCache.Get("CONFIG.service_level"));
				return JsonContent(new { d = sEdition });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>POST Rest.svc/UtcTime — Returns current UTC timestamp.</summary>
		[HttpPost("UtcTime")]
		public IActionResult UtcTime()
		{
			return JsonContent(new { d = DateTime.UtcNow });
		}

		/// <summary>POST Rest.svc/IsAuthenticated — Returns boolean indicating authentication state.</summary>
		[HttpPost("IsAuthenticated")]
		public IActionResult IsAuthenticated()
		{
			return JsonContent(new { d = _security.IsAuthenticated() });
		}

		/// <summary>POST Rest.svc/GetUserID — Returns current user's GUID.</summary>
		[HttpPost("GetUserID")]
		public IActionResult GetUserID()
		{
			if (!_security.IsAuthenticated())
				return JsonContent(new { d = Guid.Empty });
			return JsonContent(new { d = _security.USER_ID });
		}

		/// <summary>POST Rest.svc/GetUserSession — Returns current user's session GUID.</summary>
		[HttpPost("GetUserSession")]
		public IActionResult GetUserSession()
		{
			if (!_security.IsAuthenticated())
				return JsonContent(new { d = Guid.Empty });
			return JsonContent(new { d = _security.USER_SESSION });
		}

		/// <summary>POST Rest.svc/GetUserName — Returns current user's login name.</summary>
		[HttpPost("GetUserName")]
		public IActionResult GetUserName()
		{
			if (!_security.IsAuthenticated())
				return JsonContent(new { d = String.Empty });
			return JsonContent(new { d = _security.USER_NAME });
		}

		/// <summary>POST Rest.svc/GetTeamID — Returns current user's primary team GUID.</summary>
		[HttpPost("GetTeamID")]
		public IActionResult GetTeamID()
		{
			if (!_security.IsAuthenticated())
				return JsonContent(new { d = Guid.Empty });
			return JsonContent(new { d = _security.TEAM_ID });
		}

		/// <summary>POST Rest.svc/GetTeamName — Returns current user's primary team name.</summary>
		[HttpPost("GetTeamName")]
		public IActionResult GetTeamName()
		{
			if (!_security.IsAuthenticated())
				return JsonContent(new { d = String.Empty });
			string sTeamName = Sql.ToString(_memoryCache.Get("TEAM_NAME"));
			return JsonContent(new { d = sTeamName });
		}

		/// <summary>GET Rest.svc/GetMyUserProfile — Returns user profile record (masks MAIL_SMTPPASS).</summary>
		[HttpGet("GetMyUserProfile")]
		public IActionResult GetMyUserProfile()
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				string sBaseURI = GetBaseURI("/GetMyUserProfile");
				SplendidCRM.TimeZone T10n = GetUserTimezone();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				int nACLACCESS = _security.GetUserAccess("Users", "view");
				if (nACLACCESS < 0)
					return Forbidden(L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS"));
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                 " + ControlChars.CrLf
				  + "  from vwUSERS_Edit       " + ControlChars.CrLf
				  + " where ID = @ID          " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@ID", _security.USER_ID);
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				if (dt.Rows.Count > 0)
				{
					DataRow row = dt.Rows[0];
					// Mask SMTP password
					if (dt.Columns.Contains("MAIL_SMTPPASS") && !Sql.IsEmptyString(row["MAIL_SMTPPASS"]))
						row["MAIL_SMTPPASS"] = Sql.sEMPTY_PASSWORD;
					var d = _restUtil.ToJson(sBaseURI, "Users", row, T10n);
					return JsonContent(new { d });
				}
				return JsonContent(new { d = (object)null });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>GET Rest.svc/SingleSignOnSettings — Returns SSO configuration for ADFS or Azure AD.</summary>
		[HttpGet("SingleSignOnSettings")]
		[HttpPost("GetSingleSignOnSettings")]
		public IActionResult GetSingleSignOnSettings()
		{
			try
			{
				var d = GetSingleSignOnSettingsInternal();
				return JsonContent(new { d });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>GET Rest.svc/ArchiveViewExists — Checks if an archive view exists for a module.</summary>
		[HttpGet("ArchiveViewExists")]
		public IActionResult ArchiveViewExists(string ModuleName)
		{
			if (!_security.IsAuthenticated())
				return JsonContent(new { d = false });
			bool bExists = _splendidCache.ArchiveViewExists(ModuleName);
			return JsonContent(new { d = bExists });
		}

		// =====================================================================
		// #region Login
		// =====================================================================

		/// <summary>
		/// POST Rest.svc/Login — Primary authentication endpoint.
		/// Supports standard DB login, ADFS/Azure AD SSO JWT, Windows Auth, and DuoUniversal 2FA.
		/// Preserves original login flow from Rest.svc.cs including lockout, IP restriction, and audit logging.
		/// </summary>
		[HttpPost("Login")]
		public async Task<IActionResult> Login([FromBody] Dictionary<string, object> dict)
		{
			string sUSER_NAME    = String.Empty;
			string sPASSWORD     = String.Empty;
			string sVERSION      = String.Empty;
			string sMOBILE_CLIENT = "0";
			if (dict != null)
			{
				if (dict.ContainsKey("UserName"    )) sUSER_NAME     = Sql.ToString(dict["UserName"    ]);
				if (dict.ContainsKey("Password"    )) sPASSWORD      = Sql.ToString(dict["Password"    ]);
				if (dict.ContainsKey("Version"     )) sVERSION       = Sql.ToString(dict["Version"     ]);
				if (dict.ContainsKey("MobileClient")) sMOBILE_CLIENT = Sql.ToString(dict["MobileClient"]);
			}
			try
			{
				bool bMOBILE_CLIENT = Sql.ToBoolean(sMOBILE_CLIENT);
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);

				// IP address restriction check
				string sRemoteHost = HttpContext.Connection.RemoteIpAddress?.ToString() ?? String.Empty;
				if (_splendidInit.InvalidIPAddress(sRemoteHost))
				{
					SplendidError.SystemWarning(new StackFrame(1, true), "Invalid IP address: " + sRemoteHost);
					return Forbidden("ERR_INVALID_IP_ADDRESS");
				}

				// Login failure lockout check
				int nMaxLoginFailures = Crm.Password.LoginLockoutCount(_memoryCache);
				if (nMaxLoginFailures > 0)
				{
					int nFailures = _splendidInit.LoginFailures(sUSER_NAME);
					if (nFailures >= nMaxLoginFailures)
					{
						SplendidError.SystemWarning(new StackFrame(1, true), "Login lockout: " + sUSER_NAME);
						return Forbidden("ERR_LOGIN_LOCKOUT");
					}
				}

				// ADFS Single Sign-On JWT validation
				bool bADFS_SINGLE_SIGN_ON = Sql.ToBoolean(_memoryCache.Get("CONFIG.ADFS.SingleSignOn.Enabled"));
				if (bADFS_SINGLE_SIGN_ON && !Sql.IsEmptyString(sPASSWORD))
				{
					string sError = String.Empty;
					Guid gUSER_ID = ActiveDirectory.FederationServicesValidateJwt(HttpContext, sPASSWORD, bMOBILE_CLIENT, ref sError);
					if (!Sql.IsEmptyGuid(gUSER_ID))
					{
						_splendidInit.LoginUser(gUSER_ID, sRemoteHost);
						_splendidInit.InitSession();
						return await BuildLoginResult(gUSER_ID);
					}
					else if (!Sql.IsEmptyString(sError))
					{
						return Forbidden(sError);
					}
				}

				// Azure AD Single Sign-On JWT validation
				bool bAZURE_SINGLE_SIGN_ON = Sql.ToBoolean(_memoryCache.Get("CONFIG.Azure.SingleSignOn.Enabled"));
				if (bAZURE_SINGLE_SIGN_ON && !Sql.IsEmptyString(sPASSWORD))
				{
					string sError = String.Empty;
					Guid gUSER_ID = ActiveDirectory.AzureValidateJwt(HttpContext, sPASSWORD, bMOBILE_CLIENT, ref sError);
					if (!Sql.IsEmptyGuid(gUSER_ID))
					{
						_splendidInit.LoginUser(gUSER_ID, sRemoteHost);
						_splendidInit.InitSession();
						return await BuildLoginResult(gUSER_ID);
					}
					else if (!Sql.IsEmptyString(sError))
					{
						return Forbidden(sError);
					}
				}

				// Windows Authentication (NTLM/Negotiate)
				// LoginUser with empty password + domain extracted from DOMAIN\user Windows identity
				if (_security.IsWindowsAuthentication())
				{
					string sWindowsUser = HttpContext.User?.Identity?.Name ?? String.Empty;
					if (!Sql.IsEmptyString(sWindowsUser))
					{
						// Split DOMAIN\Username into domain and username components
						string sWindowsUserName   = sWindowsUser;
						string sWindowsUserDomain = String.Empty;
						if (sWindowsUser.Contains('\\'))
						{
							int nSlash = sWindowsUser.IndexOf('\\');
							sWindowsUserDomain = sWindowsUser.Substring(0, nSlash);
							sWindowsUserName   = sWindowsUser.Substring(nSlash + 1);
						}
						// LoginUser returns true on success and populates session via _security
						bool bLoggedIn = _splendidInit.LoginUser(sWindowsUserName, String.Empty, String.Empty, String.Empty, sWindowsUserDomain, false);
						if (bLoggedIn)
						{
							Guid gUSER_ID = _security.USER_ID;
							_splendidInit.InitSession();
							return await BuildLoginResult(gUSER_ID);
						}
					}
				}

				// Standard database login
				// TECHNICAL DEBT: MD5 hash preserved for SugarCRM backward compatibility. Do not modify.
				string sHashedPassword = Security.HashPassword(sPASSWORD);
				Guid gLoginUserID = Guid.Empty;
				{
					// LoginUser(string,string,string,string,string,bool) returns bool; USER_ID set in session on success
					bool bLoginSuccess = _splendidInit.LoginUser(sUSER_NAME, sHashedPassword, String.Empty, String.Empty, String.Empty, false);
					if (bLoginSuccess)
						gLoginUserID = _security.USER_ID;
				}

				// DuoUniversal 2FA check
				bool bDUO_ENABLED = !Sql.IsEmptyString(_configuration["DUO_INTEGRATION_KEY"]);
				if (!Sql.IsEmptyGuid(gLoginUserID) && bDUO_ENABLED)
				{
					string sDuoRedirectUrl = GetDuoRedirectUrl(sUSER_NAME, gLoginUserID);
					if (!Sql.IsEmptyString(sDuoRedirectUrl))
					{
						return JsonContent(new
						{
							d = new Dictionary<string, object>
							{
								{ "DuoRedirectUrl", sDuoRedirectUrl }
							}
						});
					}
				}

				if (Sql.IsEmptyGuid(gLoginUserID))
				{
					SplendidError.SystemWarning(new StackFrame(1, true), "Login failed for user: " + sUSER_NAME);
					return Forbidden(L10n.Term("ERR_LOGIN_DENIED"));
				}

				// Login audit
				Guid gAUDIT_ID = Guid.Empty;
				SqlProcs.spUSERS_LOGINS_InsertOnly(ref gAUDIT_ID, gLoginUserID, sUSER_NAME, "REST", "Success",
					HttpContext.Session?.Id ?? String.Empty, sRemoteHost, Environment.MachineName, String.Empty, "/Rest.svc/Login", String.Empty);
				_splendidInit.InitSession();
				return await BuildLoginResult(gLoginUserID);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>Builds the login response dictionary for a successfully authenticated user.</summary>
		private async Task<IActionResult> BuildLoginResult(Guid gUSER_ID)
		{
			var result = new Dictionary<string, object>();
			result["USER_ID"     ] = gUSER_ID;
			result["USER_SESSION"] = _security.USER_SESSION;
			result["USER_NAME"   ] = _security.USER_NAME;
			result["FULL_NAME"   ] = _security.FULL_NAME;
			result["IS_ADMIN"    ] = _security.IS_ADMIN;
			return JsonContent(new { d = result });
		}

		/// <summary>Builds Forbidden result with a message.</summary>
		private IActionResult Forbidden(string message)
		{
			return StatusCode(403, new { error = message });
		}

		/// <summary>
		/// POST Rest.svc/LoginDuoUniversal — DuoUniversal 2FA callback.
		/// Called after user completes DuoUniversal authentication flow.
		/// </summary>
		[HttpPost("LoginDuoUniversal")]
		[HttpGet("LoginDuoUniversal")]
		public async Task<IActionResult> LoginDuoUniversal(string code, string state)
		{
			try
			{
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				var session = _httpContextAccessor.HttpContext?.Session;
				string sStoredState    = session?.GetString("DuoUniversal.state"   ) ?? String.Empty;
				string sStoredUsername = session?.GetString("DuoUniversal.username") ?? String.Empty;
				string sStoredUserID   = session?.GetString("DuoUniversal.UserID"  ) ?? String.Empty;

				if (Sql.IsEmptyString(code) || Sql.IsEmptyString(state))
					return Forbidden(L10n.Term("ERR_INVALID_SESSION_STATE"));
				if (state != sStoredState)
					return Forbidden(L10n.Term("ERR_INVALID_SESSION_STATE"));

				string sDUO_INTEGRATION_KEY = _configuration["DUO_INTEGRATION_KEY"];
				string sDUO_SECRET_KEY      = _configuration["DUO_SECRET_KEY"     ];
				string sDUO_API_HOSTNAME    = _configuration["DUO_API_HOSTNAME"   ];
				string sSiteURL     = Crm.Config.SiteURL(_memoryCache);
				string sRedirectUri = sSiteURL.TrimEnd('/') + "/Rest.svc/LoginDuoUniversal";
				var client = new DuoUniversal.ClientBuilder(sDUO_INTEGRATION_KEY, sDUO_SECRET_KEY, sDUO_API_HOSTNAME, sRedirectUri).Build();
				DuoUniversal.IdToken token = await client.ExchangeAuthorizationCodeFor2faResult(code, sStoredUsername);
				if (token == null || token.AuthResult?.Result != "allow")
					return Forbidden(L10n.Term("ERR_LOGIN_DENIED"));

				Guid gUSER_ID = Sql.ToGuid(sStoredUserID);
				if (Sql.IsEmptyGuid(gUSER_ID))
					return Forbidden(L10n.Term("ERR_INVALID_SESSION_STATE"));

				_splendidInit.LoginUser(gUSER_ID, HttpContext.Connection.RemoteIpAddress?.ToString() ?? String.Empty);
				_splendidInit.InitSession();
				return await BuildLoginResult(gUSER_ID);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>POST Rest.svc/ForgotPassword — Sends password reset email.</summary>
		[HttpPost("ForgotPassword")]
		public IActionResult ForgotPassword([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				string sUSER_NAME = String.Empty;
				string sEMAIL     = String.Empty;
				if (dict != null)
				{
					if (dict.ContainsKey("UserName")) sUSER_NAME = Sql.ToString(dict["UserName"]);
					if (dict.ContainsKey("Email"   )) sEMAIL     = Sql.ToString(dict["Email"   ]);
				}
				string sResult = ModuleUtils.Login.SendForgotPasswordNotice(_memoryCache, sUSER_NAME, sEMAIL);
				return JsonContent(new { d = sResult });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>POST Rest.svc/Logout — Terminates user session.</summary>
		[HttpPost("Logout")]
		public IActionResult Logout()
		{
			try
			{
				if (_security.IsAuthenticated())
				{
					Guid gUSER_ID  = _security.USER_ID;
					string sASPNET_SESSIONID = HttpContext.Session?.Id ?? String.Empty;
					try
					{
						using IDbConnection con = _dbProviderFactories.CreateConnection();
						con.Open();
						using IDbCommand cmd = SqlProcs.Factory(con, "spUSERS_LOGINS_Logout");
						Sql.AddParameter(cmd, "@USER_ID"         , gUSER_ID         );
						Sql.AddParameter(cmd, "@ASPNET_SESSIONID", sASPNET_SESSIONID);
						cmd.ExecuteNonQuery();
					}
					catch (Exception ex)
					{
						SplendidError.SystemError(new StackFrame(1, true), ex);
					}
					HttpContext.Session?.Clear();
				}
				return JsonContent(new { d = "OK" });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		// =====================================================================
		// #region Get System Layout (React SPA metadata)
		// =====================================================================

		/// <summary>GET Rest.svc/GetAllGridViewsColumns — Returns all grid view column definitions for accessible modules.</summary>
		[HttpGet("GetAllGridViewsColumns")]
		public IActionResult GetAllGridViewsColumns()
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				var lstMODULES = _restUtil.AccessibleModules(HttpContext);
				var d = GetAllGridViewsColumnsInternal(lstMODULES);
				return JsonContent(new { d });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>GET Rest.svc/GetAllDetailViewsFields — Returns all detail view field definitions for accessible modules.</summary>
		[HttpGet("GetAllDetailViewsFields")]
		public IActionResult GetAllDetailViewsFields()
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				var lstMODULES = _restUtil.AccessibleModules(HttpContext);
				var d = GetAllDetailViewsFieldsInternal(lstMODULES);
				return JsonContent(new { d });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>GET Rest.svc/GetAllEditViewsFields — Returns all edit/search view field definitions for accessible modules.</summary>
		[HttpGet("GetAllEditViewsFields")]
		public IActionResult GetAllEditViewsFields()
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				var lstMODULES = _restUtil.AccessibleModules(HttpContext);
				var d = GetAllEditViewsFieldsInternal(lstMODULES);
				return JsonContent(new { d });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>GET Rest.svc/GetAllDetailViewsRelationships — Returns all detail view relationship panels.</summary>
		[HttpGet("GetAllDetailViewsRelationships")]
		public IActionResult GetAllDetailViewsRelationships()
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				var lstMODULES = _restUtil.AccessibleModules(HttpContext);
				var d = GetAllDetailViewsRelationshipsInternal(lstMODULES);
				return JsonContent(new { d });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>GET Rest.svc/GetAllEditViewsRelationships — Returns all edit view relationship panels.</summary>
		[HttpGet("GetAllEditViewsRelationships")]
		public IActionResult GetAllEditViewsRelationships()
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				var lstMODULES = _restUtil.AccessibleModules(HttpContext);
				var d = GetAllEditViewsRelationshipsInternal(lstMODULES);
				return JsonContent(new { d });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>GET Rest.svc/GetAllDynamicButtons — Returns all dynamic button definitions with ACL access levels.</summary>
		[HttpGet("GetAllDynamicButtons")]
		public IActionResult GetAllDynamicButtons()
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				var lstMODULES = _restUtil.AccessibleModules(HttpContext);
				var d = GetAllDynamicButtonsInternal(lstMODULES);
				return JsonContent(new { d });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>GET Rest.svc/GetAllTerminology — Returns all terminology strings for accessible modules.</summary>
		[HttpGet("GetAllTerminology")]
		public IActionResult GetAllTerminology()
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				var lstMODULES = _restUtil.AccessibleModules(HttpContext);
				var d = GetAllTerminologyInternal(lstMODULES, _security.IS_ADMIN);
				return JsonContent(new { d });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>GET Rest.svc/GetAllTerminologyLists — Returns all terminology list (dropdown) definitions.</summary>
		[HttpGet("GetAllTerminologyLists")]
		public IActionResult GetAllTerminologyLists()
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				var d = GetAllTerminologyListsInternal(_security.IS_ADMIN);
				return JsonContent(new { d });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		// =====================================================================
		// #region React State
		// =====================================================================

		/// <summary>
		/// GET Rest.svc/GetReactLoginState — Returns minimal state for the login page.
		/// Provides login config, terminology, SSO settings, and DuoUniversal indicator.
		/// </summary>
		[HttpGet("GetReactLoginState")]
		public IActionResult GetReactLoginState()
		{
			try
			{
				SetNoCacheHeaders();
				var result = new Dictionary<string, object>();
				result["loginConfig"         ] = _splendidCache.GetLoginConfig();
				// Use the login-module subset of terminology for the unauthenticated login screen
				result["loginTerminology"    ] = GetAllTerminologyInternal(new List<string> { "Users", "Login", "Errors" }, false);
				result["loginTerminologyLists"] = GetAllTerminologyListsInternal(false);
				result["SingleSignOnSettings"] = GetSingleSignOnSettingsInternal();
				bool bDUO_ENABLED = !Sql.IsEmptyString(_configuration["DUO_INTEGRATION_KEY"]);
				result["DuoEnabled"] = bDUO_ENABLED;
				return JsonContent(new { d = result });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>
		/// GET Rest.svc/GetReactState — Returns comprehensive React SPA bootstrap state.
		/// Includes profile, modules, layouts, terminology, user preferences, and real-time data.
		/// This is the CRITICAL endpoint for React SPA initialization.
		/// </summary>
		[HttpGet("GetReactState")]
		public IActionResult GetReactState()
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				SetNoCacheHeaders();
				var lstMODULES = _restUtil.AccessibleModules(HttpContext);
				var result     = new Dictionary<string, object>();

				// User profile
				result["USER_PROFILE"] = GetUserProfileDict();

				// Module metadata
				result["APP_STATE"] = new Dictionary<string, object>
				{
					{ "MODULE_LIST", lstMODULES }
				};

				// System layouts
				result["GRIDVIEWS_COLUMNS"      ] = GetAllGridViewsColumnsInternal       (lstMODULES);
				result["DETAILVIEWS_FIELDS"      ] = GetAllDetailViewsFieldsInternal      (lstMODULES);
				result["EDITVIEWS_FIELDS"        ] = GetAllEditViewsFieldsInternal        (lstMODULES);
				result["DETAILVIEWS_RELATIONSHIPS"] = GetAllDetailViewsRelationshipsInternal(lstMODULES);
				result["EDITVIEWS_RELATIONSHIPS" ] = GetAllEditViewsRelationshipsInternal (lstMODULES);
				result["DYNAMIC_BUTTONS"         ] = GetAllDynamicButtonsInternal         (lstMODULES);
				result["SHORTCUTS"               ] = GetAllShortcutsInternal              (lstMODULES);

				// Terminology
				result["TERMINOLOGY"      ] = GetAllTerminologyInternal      (lstMODULES, _security.IS_ADMIN);
				result["TERMINOLOGY_LISTS"] = GetAllTerminologyListsInternal (_security.IS_ADMIN);

				// Search columns
				result["SEARCH_COLUMNS"] = GetAllSearchColumnsInternal(lstMODULES);

				// Custom React views
				result["REACT_CUSTOM_VIEWS"] = GetAllReactCustomViewsInternal(lstMODULES);

				// ACL access
				result["MODULE_ACL_ACCESS"] = GetModuleAccessInternal(lstMODULES);
				result["USER_ACL_ROLES"    ] = GetUserACLRolesInternal();

				// User team tree
				result["TEAM_TREE"] = _splendidCache.GetUserTeamTree();

				// Config
				result["CONFIG"] = _splendidCache.GetAllConfig();

				// Tab menus
				result["TAB_MENUS"] = GetAllTabMenusInternal();

				// Users and Teams (for assignment fields)
				if (_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE)
				{
					result["USERS"] = GetAllUsersInternal();
					result["TEAMS"] = GetAllTeamsInternal();
				}

				// Tax rates and discounts (for AOS modules)
				result["TAX_RATES"] = GetTaxRatesInternal();
				result["DISCOUNTS" ] = GetDiscountsInternal();

				// Favorites, last viewed, saved search
				result["FAVORITES"   ] = GetAllFavoritesInternal();
				result["LAST_VIEWED" ] = GetAllLastViewedInternal();
				result["SAVED_SEARCH"] = GetAllSavedSearchInternal();

				// Dashboards
				result["DASHBOARDS"       ] = GetAllDashboardsInternal();
				result["DASHBOARDS_PANELS"] = GetAllDashboardPanelsInternal();

				// Signatures and outbound email/SMS
				result["USER_SIGNATURES"] = GetUserSignaturesInternal();
				result["OUTBOUND_EMAILS"] = GetOutboundMailInternal();
				result["OUTBOUND_SMS"   ] = GetOutboundSmsInternal();

				// Session timeout
				int nSessionStateTimeout = Sql.ToInteger(_configuration["SessionStateTimeout"] ?? "20");
				result["SessionStateTimeout"] = nSessionStateTimeout * 60; // seconds

				return JsonContent(new { d = result });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		// =====================================================================
		// #region Get (CRUD read operations)
		// =====================================================================

		/// <summary>
		/// GET Rest.svc/PhoneSearch — Phone number search across all modules.
		/// Queries vwPHONE_NUMBERS with normalized phone for CTI popup.
		/// </summary>
		[HttpGet("PhoneSearch")]
		public IActionResult PhoneSearch(string Phone)
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				string sBaseURI = GetBaseURI("/PhoneSearch");
				SplendidCRM.TimeZone T10n = GetUserTimezone();
				string sCulture = GetUserCulture();

				string sNORMALIZED_PHONE = Utils.NormalizePhone(Phone);
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                          " + ControlChars.CrLf
				  + "  from vwPHONE_NUMBERS             " + ControlChars.CrLf
				  + " where NORMALIZED_PHONE = @NORMALIZED_PHONE" + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@NORMALIZED_PHONE", sNORMALIZED_PHONE);
				_security.Filter(cmd, "Contacts", "view");
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				var results = _restUtil.RowsToDictionary(sBaseURI, "Contacts", dt, T10n);
				return JsonContent(new { d = new { results } });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>
		/// GET Rest.svc/GetCustomList — Returns dropdown/picklist values for a named list.
		/// Handles special calendar lists and falls back to SplendidCache.List().
		/// </summary>
		[HttpGet("GetCustomList")]
		public IActionResult GetCustomList(string ListName)
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				string sBaseURI = GetBaseURI("/GetCustomList");
				SplendidCRM.TimeZone T10n = GetUserTimezone();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);

				// Special calendar lists
				if (ListName == "month_names_dom" || ListName == "short_month_names_dom" ||
				    ListName == "day_names_dom"   || ListName == "short_day_names_dom")
				{
					using var dt = new DataTable();
					dt.Columns.Add("NAME"        , typeof(string));
					dt.Columns.Add("DISPLAY_NAME", typeof(string));
					if (ListName == "month_names_dom" || ListName == "short_month_names_dom")
					{
						CultureInfo ci = CultureInfo.CreateSpecificCulture(sCulture);
						string[] monthNames = (ListName == "short_month_names_dom") ? ci.DateTimeFormat.AbbreviatedMonthNames : ci.DateTimeFormat.MonthNames;
						for (int i = 0; i < monthNames.Length && !String.IsNullOrEmpty(monthNames[i]); i++)
						{
							DataRow row = dt.NewRow();
							row["NAME"        ] = i.ToString();
							row["DISPLAY_NAME"] = monthNames[i];
							dt.Rows.Add(row);
						}
					}
					else
					{
						CultureInfo ci = CultureInfo.CreateSpecificCulture(sCulture);
						string[] dayNames = (ListName == "short_day_names_dom") ? ci.DateTimeFormat.AbbreviatedDayNames : ci.DateTimeFormat.DayNames;
						for (int i = 0; i < dayNames.Length; i++)
						{
							DataRow row = dt.NewRow();
							row["NAME"        ] = i.ToString();
							row["DISPLAY_NAME"] = dayNames[i];
							dt.Rows.Add(row);
						}
					}
					var calResults = _restUtil.RowsToDictionary(sBaseURI, ListName, dt, T10n);
					return JsonContent(new { d = new { results = calResults } });
				}

				// Standard terminology list
				DataTable dtList = _splendidCache.List(ListName);
				var listResults = _restUtil.RowsToDictionary(sBaseURI, ListName, dtList, T10n);
				return JsonContent(new { d = new { results = listResults } });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>
		/// GET Rest.svc/GetModuleTable — Returns a paged, filtered, sorted list of module records.
		/// Supports OData-style $skip/$top/$filter/$orderby/$groupby/$select query params.
		/// Preserves custom OData parsing via SearchBuilder (NOT Microsoft OData middleware).
		/// </summary>
		[HttpGet("GetModuleTable")]
		public IActionResult GetModuleTable(
			string TableName,
			[FromQuery(Name = "$skip")]        int    skip        = 0,
			[FromQuery(Name = "$top")]         int    top         = 25,
			[FromQuery(Name = "$filter")]      string filter      = null,
			[FromQuery(Name = "$orderby")]     string orderby     = null,
			[FromQuery(Name = "$groupby")]     string groupby     = null,
			[FromQuery(Name = "$select")]      string select      = null,
			[FromQuery(Name = "$archiveView")] string archiveView = null,
			[FromQuery(Name = "$dump")]        string dump        = null)
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				string sBaseURI = GetBaseURI("/GetModuleTable");
				SplendidCRM.TimeZone T10n = GetUserTimezone();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);

				// Validate table access via RestTables
				DataTable dtRestTables = _splendidCache.RestTables(TableName, false);
				if (dtRestTables == null || dtRestTables.Rows.Count == 0)
					return BadRequest(new { error = "Invalid table: " + TableName });

				UniqueStringCollection arrSELECT_USC = null;
				if (!Sql.IsEmptyString(select))
				{
					arrSELECT_USC = new UniqueStringCollection();
					foreach (var s in select.Split(',')) { string t = s.Trim(); if (!string.IsNullOrEmpty(t)) arrSELECT_USC.Add(t); }
				}
				bool bArchiveView  = Sql.ToBoolean(archiveView);
				bool bDumpSQL      = !Sql.IsEmptyString(dump) && _security.IS_ADMIN;
				var sbDumpSQL      = bDumpSQL ? new StringBuilder() : null;

				// NOTE: GetTable parameter order is (skip, top, sORDER_BY, sWHERE, sGROUP_BY, ...)
				// OData $orderby maps to sORDER_BY; OData $filter maps to sWHERE
				long nTotalCount = 0;
				DataTable dt = _restUtil.GetTable(HttpContext, TableName, skip, top, orderby, filter, groupby, arrSELECT_USC, null, ref nTotalCount, null, AccessMode.list, bArchiveView, null, sbDumpSQL);
				var rows = _restUtil.RowsToDictionary(sBaseURI, TableName, dt, T10n);
				var d = new Dictionary<string, object>
				{
					{ "results" , rows       },
					{ "__total" , nTotalCount }
				};
				if (bDumpSQL && sbDumpSQL != null)
					d["__sql"] = sbDumpSQL.ToString();
				return JsonContent(new { d });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>
		/// GET Rest.svc/GetModuleItem — Returns a single module record by ID.
		/// Applies 4-tier ACL (Module→Team→Field→Record) via Security.Filter.
		/// </summary>
		[HttpGet("GetModuleItem")]
		public IActionResult GetModuleItem(string ModuleName, string ID)
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				int nACLACCESS = _security.GetUserAccess(ModuleName, "view");
				if (nACLACCESS < 0)
					return Forbidden(L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS"));

				Guid gID = Sql.ToGuid(ID);
				string sBaseURI  = GetBaseURI("/GetModuleItem");
				SplendidCRM.TimeZone T10n = GetUserTimezone();

				string sTableName = Crm.Modules.TableName(_memoryCache, ModuleName);
				string sVIEW_NAME = "vw" + sTableName;
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *             " + ControlChars.CrLf
				  + "  from " + sVIEW_NAME + ControlChars.CrLf
				  + " where ID = @ID     " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@ID", gID);
				_security.Filter(cmd, ModuleName, "view");
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				if (dt.Rows.Count == 0)
					return NotFound(new { error = ModuleName + " record not found: " + ID });
				var d = _restUtil.ToJson(sBaseURI, ModuleName, dt.Rows[0], T10n);
				return JsonContent(new { d });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>
		/// GET Rest.svc/GetModuleList — Returns module list with OData-style query parameters.
		/// Supports module-specific view overrides (Activities, ProductCatalog, Employees, ReportRules).
		/// </summary>
		[HttpGet("GetModuleList")]
		public IActionResult GetModuleList(
			string ModuleName,
			[FromQuery(Name = "$skip")]        int    skip        = 0,
			[FromQuery(Name = "$top")]         int    top         = 25,
			[FromQuery(Name = "$filter")]      string filter      = null,
			[FromQuery(Name = "$orderby")]     string orderby     = null,
			[FromQuery(Name = "$groupby")]     string groupby     = null,
			[FromQuery(Name = "$select")]      string select      = null,
			[FromQuery(Name = "$archiveView")] string archiveView = null,
			[FromQuery(Name = "$dump")]        string dump        = null)
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				int nACLACCESS = _security.GetUserAccess(ModuleName, "list");
				if (nACLACCESS < 0)
					return Forbidden(L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS"));

				string sBaseURI = GetBaseURI("/GetModuleList");
				SplendidCRM.TimeZone T10n = GetUserTimezone();

				// Resolve table name (module-specific overrides from original Rest.svc.cs)
				string sTableName = Crm.Modules.TableName(_memoryCache, ModuleName);
				if (ModuleName == "ProductCatalog") sTableName = "PRODUCT_CATALOG";
				else if (ModuleName == "Activities" ) sTableName = "vwACTIVITIES"    ;
				else if (ModuleName == "Employees"  ) sTableName = "vwEMPLOYEES_Sync";
				else if (ModuleName == "ReportRules") sTableName = "vwREPORT_RULES"   ;

				UniqueStringCollection arrSELECT_USC = null;
				if (!Sql.IsEmptyString(select))
				{
					arrSELECT_USC = new UniqueStringCollection();
					foreach (var s in select.Split(',')) { string t = s.Trim(); if (!string.IsNullOrEmpty(t)) arrSELECT_USC.Add(t); }
				}
				bool bArchiveView  = Sql.ToBoolean(archiveView);
				bool bDumpSQL      = !Sql.IsEmptyString(dump) && _security.IS_ADMIN;
				var sbDumpSQL      = bDumpSQL ? new StringBuilder() : null;

				// NOTE: GetTable param order is (skip, top, sORDER_BY, sWHERE, sGROUP_BY)
				long nTotalCount = 0;
				DataTable dt = _restUtil.GetTable(HttpContext, sTableName, skip, top, orderby, filter, groupby, arrSELECT_USC, null, ref nTotalCount, null, AccessMode.list, bArchiveView, null, sbDumpSQL);
				var rows = _restUtil.RowsToDictionary(sBaseURI, ModuleName, dt, T10n);
				var d = new Dictionary<string, object>
				{
					{ "results" , rows       },
					{ "__total" , nTotalCount }
				};
				if (bDumpSQL && sbDumpSQL != null)
					d["__sql"] = sbDumpSQL.ToString();
				return JsonContent(new { d });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		// =====================================================================
		// #region Update (CRUD write operations)
		// =====================================================================

		/// <summary>
		/// POST Rest.svc/UpdateModuleTable — Generic update for any module record.
		/// Delegates to RestUtil.UpdateTable which calls the appropriate stored procedure.
		/// </summary>
		[HttpPost("UpdateModuleTable")]
		public IActionResult UpdateModuleTable([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				string sTableName = String.Empty;
				if (dict != null && dict.ContainsKey("TableName"))
					sTableName = Sql.ToString(dict["TableName"]);
				if (Sql.IsEmptyString(sTableName))
					return BadRequest(new { error = "TableName is required" });

				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				string sModuleName = Crm.Modules.ModuleName(_memoryCache, sTableName);
				int nACLACCESS = _security.GetUserAccess(sModuleName, "edit");
				if (nACLACCESS < 0)
					return Forbidden(L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS"));

				Guid gID = _restUtil.UpdateTable(HttpContext, sTableName, dict);
				return JsonContent(new { d = gID });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>
		/// POST Rest.svc/UpdateModule — Updates a module record by module name.
		/// </summary>
		[HttpPost("UpdateModule")]
		public IActionResult UpdateModule([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				string sModuleName = String.Empty;
				if (dict != null && dict.ContainsKey("ModuleName"))
					sModuleName = Sql.ToString(dict["ModuleName"]);
				if (Sql.IsEmptyString(sModuleName))
					return BadRequest(new { error = "ModuleName is required" });

				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				int nACLACCESS = _security.GetUserAccess(sModuleName, "edit");
				if (nACLACCESS < 0)
					return Forbidden(L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS"));

				string sTableName = Crm.Modules.TableName(_memoryCache, sModuleName);
				Guid gID = _restUtil.UpdateTable(HttpContext, sTableName, dict);
				return JsonContent(new { d = gID });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>
		/// POST Rest.svc/MassUpdateModule — Applies an update to multiple module records.
		/// </summary>
		[HttpPost("MassUpdateModule")]
		public IActionResult MassUpdateModule([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				string sModuleName = String.Empty;
				string[] arrIDs    = null;
				if (dict != null)
				{
					if (dict.ContainsKey("ModuleName")) sModuleName = Sql.ToString(dict["ModuleName"]);
					if (dict.ContainsKey("IDs"        ))
					{
						var ids = dict["IDs"];
						if (ids is Newtonsoft.Json.Linq.JArray jArr)
							arrIDs = jArr.ToObject<string[]>();
						else if (ids is string[] sa)
							arrIDs = sa;
					}
				}
				if (Sql.IsEmptyString(sModuleName) || arrIDs == null || arrIDs.Length == 0)
					return BadRequest(new { error = "ModuleName and IDs are required" });

				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				int nACLACCESS = _security.GetUserAccess(sModuleName, "edit");
				if (nACLACCESS < 0)
					return Forbidden(L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS"));

				System.Collections.Stack stkIDs = _utils.FilterByACL_Stack(sModuleName, "edit", arrIDs, Crm.Modules.TableName(_memoryCache, sModuleName));
				string sTableName = Crm.Modules.TableName(_memoryCache, sModuleName);
				while (stkIDs.Count > 0)
				{
					Guid gID = Sql.ToGuid(stkIDs.Pop());
					if (!dict.ContainsKey("ID"))
						dict["ID"] = gID.ToString();
					else
						dict["ID"] = gID.ToString();
					_restUtil.UpdateTable(HttpContext, sTableName, dict);
				}
				return JsonContent(new { d = "OK" });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		// =====================================================================
		// #region Favorites and Subscriptions
		// =====================================================================

		/// <summary>POST Rest.svc/AddToFavorites — Adds a record to user favorites.</summary>
		[HttpPost("AddToFavorites")]
		public IActionResult AddToFavorites([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				string sModuleName = String.Empty;
				string sItemID     = String.Empty;
				if (dict != null)
				{
					if (dict.ContainsKey("ModuleName")) sModuleName = Sql.ToString(dict["ModuleName"]);
					if (dict.ContainsKey("ItemID"    )) sItemID     = Sql.ToString(dict["ItemID"    ]);
				}
				Guid gID     = Guid.NewGuid();
				Guid gItemID = Sql.ToGuid(sItemID);
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = SqlProcs.Factory(con, "spSUGARFAVORITES_Update");
				Sql.AddParameter(cmd, "@ID"         , gID               );
				Sql.AddParameter(cmd, "@MODULE_NAME", sModuleName        );
				Sql.AddParameter(cmd, "@ITEM_ID"    , gItemID            );
				Sql.AddParameter(cmd, "@CREATED_BY" , _security.USER_ID  );
				cmd.ExecuteNonQuery();
				return JsonContent(new { d = gID });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>POST Rest.svc/RemoveFromFavorites — Removes a record from user favorites.</summary>
		[HttpPost("RemoveFromFavorites")]
		public IActionResult RemoveFromFavorites([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				string sModuleName = String.Empty;
				string sItemID     = String.Empty;
				if (dict != null)
				{
					if (dict.ContainsKey("ModuleName")) sModuleName = Sql.ToString(dict["ModuleName"]);
					if (dict.ContainsKey("ItemID"    )) sItemID     = Sql.ToString(dict["ItemID"    ]);
				}
				Guid gItemID = Sql.ToGuid(sItemID);
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = SqlProcs.Factory(con, "spSUGARFAVORITES_Delete");
				Sql.AddParameter(cmd, "@MODULE_NAME", sModuleName      );
				Sql.AddParameter(cmd, "@ITEM_ID"    , gItemID          );
				Sql.AddParameter(cmd, "@CREATED_BY" , _security.USER_ID);
				cmd.ExecuteNonQuery();
				return JsonContent(new { d = "OK" });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>POST Rest.svc/Subscribe — Subscribes current user to a module record.</summary>
		[HttpPost("Subscribe")]
		public IActionResult Subscribe([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				string sModuleName = String.Empty;
				string sItemID     = String.Empty;
				if (dict != null)
				{
					if (dict.ContainsKey("ModuleName")) sModuleName = Sql.ToString(dict["ModuleName"]);
					if (dict.ContainsKey("ItemID"    )) sItemID     = Sql.ToString(dict["ItemID"    ]);
				}
				Guid gID     = Guid.NewGuid();
				Guid gItemID = Sql.ToGuid(sItemID);
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = SqlProcs.Factory(con, "spSUBSCRIPTIONS_Update");
				Sql.AddParameter(cmd, "@ID"          , gID              );
				Sql.AddParameter(cmd, "@PARENT_TYPE" , sModuleName       );
				Sql.AddParameter(cmd, "@PARENT_ID"   , gItemID           );
				Sql.AddParameter(cmd, "@CREATED_BY"  , _security.USER_ID );
				cmd.ExecuteNonQuery();
				return JsonContent(new { d = gID });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>POST Rest.svc/Unsubscribe — Unsubscribes current user from a module record.</summary>
		[HttpPost("Unsubscribe")]
		public IActionResult Unsubscribe([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				string sModuleName = String.Empty;
				string sItemID     = String.Empty;
				if (dict != null)
				{
					if (dict.ContainsKey("ModuleName")) sModuleName = Sql.ToString(dict["ModuleName"]);
					if (dict.ContainsKey("ItemID"    )) sItemID     = Sql.ToString(dict["ItemID"    ]);
				}
				Guid gItemID = Sql.ToGuid(sItemID);
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = SqlProcs.Factory(con, "spSUBSCRIPTIONS_Delete");
				Sql.AddParameter(cmd, "@PARENT_TYPE", sModuleName      );
				Sql.AddParameter(cmd, "@PARENT_ID"  , gItemID          );
				Sql.AddParameter(cmd, "@CREATED_BY" , _security.USER_ID);
				cmd.ExecuteNonQuery();
				return JsonContent(new { d = "OK" });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		// =====================================================================
		// #region Delete
		// =====================================================================

		/// <summary>
		/// POST Rest.svc/DeleteModuleItem — Soft-deletes a single module record.
		/// Enforces delete ACL before calling spMODULE_Delete stored procedure.
		/// </summary>
		[HttpPost("DeleteModuleItem")]
		public IActionResult DeleteModuleItem([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				string sModuleName = String.Empty;
				string sID         = String.Empty;
				if (dict != null)
				{
					if (dict.ContainsKey("ModuleName")) sModuleName = Sql.ToString(dict["ModuleName"]);
					if (dict.ContainsKey("ID"         )) sID         = Sql.ToString(dict["ID"         ]);
				}
				if (Sql.IsEmptyString(sModuleName) || Sql.IsEmptyString(sID))
					return BadRequest(new { error = "ModuleName and ID are required" });

				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				int nACLACCESS = _security.GetUserAccess(sModuleName, "delete");
				if (nACLACCESS < 0)
					return Forbidden(L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS"));

				Guid gID = Sql.ToGuid(sID);
				string sTableName = Crm.Modules.TableName(_memoryCache, sModuleName);
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = SqlProcs.Factory(con, "sp" + sTableName + "_Delete");
				Sql.AddParameter(cmd, "@ID"        , gID               );
				Sql.AddParameter(cmd, "@MODIFIED_BY", _security.USER_ID );
				cmd.ExecuteNonQuery();
				return JsonContent(new { d = "OK" });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>POST Rest.svc/MassDeleteModule — Soft-deletes multiple module records.</summary>
		[HttpPost("MassDeleteModule")]
		public IActionResult MassDeleteModule([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				string   sModuleName = String.Empty;
				string[] arrIDs      = null;
				if (dict != null)
				{
					if (dict.ContainsKey("ModuleName")) sModuleName = Sql.ToString(dict["ModuleName"]);
					if (dict.ContainsKey("IDs"))
					{
						var ids = dict["IDs"];
						if (ids is Newtonsoft.Json.Linq.JArray jArr)
							arrIDs = jArr.ToObject<string[]>();
						else if (ids is string[] sa)
							arrIDs = sa;
					}
				}
				if (Sql.IsEmptyString(sModuleName) || arrIDs == null || arrIDs.Length == 0)
					return BadRequest(new { error = "ModuleName and IDs are required" });

				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				int nACLACCESS = _security.GetUserAccess(sModuleName, "delete");
				if (nACLACCESS < 0)
					return Forbidden(L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS"));

				string sTableName = Crm.Modules.TableName(_memoryCache, sModuleName);
				System.Collections.Stack stkIDs = _utils.FilterByACL_Stack(sModuleName, "delete", arrIDs, sTableName);
				while (stkIDs.Count > 0)
				{
					Guid gID = Sql.ToGuid(stkIDs.Pop());
					using IDbConnection con = _dbProviderFactories.CreateConnection();
					con.Open();
					using IDbCommand cmd = SqlProcs.Factory(con, "sp" + sTableName + "_Delete");
					Sql.AddParameter(cmd, "@ID"         , gID              );
					Sql.AddParameter(cmd, "@MODIFIED_BY", _security.USER_ID );
					cmd.ExecuteNonQuery();
				}
				return JsonContent(new { d = "OK" });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>POST Rest.svc/DeleteModuleRecurrences — Deletes recurring event instances.</summary>
		[HttpPost("DeleteModuleRecurrences")]
		public IActionResult DeleteModuleRecurrences([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				string sModuleName = String.Empty;
				string sID         = String.Empty;
				if (dict != null)
				{
					if (dict.ContainsKey("ModuleName")) sModuleName = Sql.ToString(dict["ModuleName"]);
					if (dict.ContainsKey("ID"         )) sID         = Sql.ToString(dict["ID"         ]);
				}
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				int nACLACCESS = _security.GetUserAccess(sModuleName, "delete");
				if (nACLACCESS < 0)
					return Forbidden(L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS"));
				Guid gID = Sql.ToGuid(sID);
				string sTableName = Crm.Modules.TableName(_memoryCache, sModuleName);
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = SqlProcs.Factory(con, "sp" + sTableName + "_DeleteRecurrences");
				Sql.AddParameter(cmd, "@ID"         , gID              );
				Sql.AddParameter(cmd, "@MODIFIED_BY", _security.USER_ID );
				cmd.ExecuteNonQuery();
				return JsonContent(new { d = "OK" });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		// =====================================================================
		// #region Sync
		// =====================================================================

		/// <summary>POST Rest.svc/MassSync — Mass syncs records to Exchange/Google/iCloud.</summary>
		[HttpPost("MassSync")]
		public IActionResult MassSync([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				string   sModuleName = String.Empty;
				string[] arrIDs      = null;
				string   sSyncTarget = "Exchange";
				if (dict != null)
				{
					if (dict.ContainsKey("ModuleName")) sModuleName = Sql.ToString(dict["ModuleName"]);
					if (dict.ContainsKey("SyncTarget")) sSyncTarget = Sql.ToString(dict["SyncTarget"]);
					if (dict.ContainsKey("IDs"))
					{
						var ids = dict["IDs"];
						if (ids is Newtonsoft.Json.Linq.JArray jArr)
							arrIDs = jArr.ToObject<string[]>();
						else if (ids is string[] sa)
							arrIDs = sa;
					}
				}
				if (Sql.IsEmptyString(sModuleName) || arrIDs == null || arrIDs.Length == 0)
					return BadRequest(new { error = "ModuleName and IDs are required" });

				string sTableName = Crm.Modules.TableName(_memoryCache, sModuleName);
				System.Collections.Stack stkIDs = _utils.FilterByACL_Stack(sModuleName, "edit", arrIDs, sTableName);
				string sMassIDs = Utils.BuildMassIDs(stkIDs);
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = SqlProcs.Factory(con, "sp" + sTableName + "_MassSync");
				Sql.AddParameter(cmd, "@MASS_IDS"  , sMassIDs         );
				Sql.AddParameter(cmd, "@MODIFIED_BY", _security.USER_ID );
				cmd.ExecuteNonQuery();
				return JsonContent(new { d = "OK" });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>POST Rest.svc/MassUnsync — Mass unsyncs records from Exchange/Google/iCloud.</summary>
		[HttpPost("MassUnsync")]
		public IActionResult MassUnsync([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				string   sModuleName = String.Empty;
				string[] arrIDs      = null;
				string   sSyncTarget = "Exchange";
				if (dict != null)
				{
					if (dict.ContainsKey("ModuleName")) sModuleName = Sql.ToString(dict["ModuleName"]);
					if (dict.ContainsKey("SyncTarget")) sSyncTarget = Sql.ToString(dict["SyncTarget"]);
					if (dict.ContainsKey("IDs"))
					{
						var ids = dict["IDs"];
						if (ids is Newtonsoft.Json.Linq.JArray jArr)
							arrIDs = jArr.ToObject<string[]>();
						else if (ids is string[] sa)
							arrIDs = sa;
					}
				}
				if (Sql.IsEmptyString(sModuleName) || arrIDs == null || arrIDs.Length == 0)
					return BadRequest(new { error = "ModuleName and IDs are required" });

				string sTableName = Crm.Modules.TableName(_memoryCache, sModuleName);
				System.Collections.Stack stkIDs = _utils.FilterByACL_Stack(sModuleName, "edit", arrIDs, sTableName);
				// Exchange unsync
				if (sSyncTarget == "Exchange")
				{
					System.Collections.Stack stkTemp = new System.Collections.Stack(stkIDs.ToArray());
					while (stkTemp.Count > 0)
					{
						Guid gItemID = Sql.ToGuid(stkTemp.Pop());
						ExchangeSync.UnsyncContact(HttpContext, _security.USER_ID, gItemID);
					}
				}
				else
				{
					string sMassIDs = Utils.BuildMassIDs(stkIDs);
					using IDbConnection con = _dbProviderFactories.CreateConnection();
					con.Open();
					using IDbCommand cmd = SqlProcs.Factory(con, "sp" + sTableName + "_MassUnsync");
					Sql.AddParameter(cmd, "@MASS_IDS"   , sMassIDs         );
					Sql.AddParameter(cmd, "@MODIFIED_BY" , _security.USER_ID );
					cmd.ExecuteNonQuery();
				}
				return JsonContent(new { d = "OK" });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		// =====================================================================
		// #region Archive
		// =====================================================================

		/// <summary>POST Rest.svc/ArchiveMoveData — Moves module records to archive.</summary>
		[HttpPost("ArchiveMoveData")]
		public IActionResult ArchiveMoveData([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				string   sModuleName = String.Empty;
				string[] arrIDs      = null;
				if (dict != null)
				{
					if (dict.ContainsKey("ModuleName")) sModuleName = Sql.ToString(dict["ModuleName"]);
					if (dict.ContainsKey("IDs"))
					{
						var ids = dict["IDs"];
						if (ids is Newtonsoft.Json.Linq.JArray jArr)
							arrIDs = jArr.ToObject<string[]>();
						else if (ids is string[] sa)
							arrIDs = sa;
					}
				}
				if (Sql.IsEmptyString(sModuleName) || arrIDs == null || arrIDs.Length == 0)
					return BadRequest(new { error = "ModuleName and IDs are required" });

				var archiveUtils = new ArchiveUtils(_httpContextAccessor, _memoryCache, _security, _dbProviderFactories, _splendidCache);
				string sResult = archiveUtils.MoveData(sModuleName, arrIDs);
				return JsonContent(new { d = sResult });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>POST Rest.svc/ArchiveRecoverData — Recovers module records from archive.</summary>
		[HttpPost("ArchiveRecoverData")]
		public IActionResult ArchiveRecoverData([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				string   sModuleName = String.Empty;
				string[] arrIDs      = null;
				if (dict != null)
				{
					if (dict.ContainsKey("ModuleName")) sModuleName = Sql.ToString(dict["ModuleName"]);
					if (dict.ContainsKey("IDs"))
					{
						var ids = dict["IDs"];
						if (ids is Newtonsoft.Json.Linq.JArray jArr)
							arrIDs = jArr.ToObject<string[]>();
						else if (ids is string[] sa)
							arrIDs = sa;
					}
				}
				if (Sql.IsEmptyString(sModuleName) || arrIDs == null || arrIDs.Length == 0)
					return BadRequest(new { error = "ModuleName and IDs are required" });

				var archiveUtils = new ArchiveUtils(_httpContextAccessor, _memoryCache, _security, _dbProviderFactories, _splendidCache);
				string sResult = archiveUtils.RecoverData(sModuleName, arrIDs);
				return JsonContent(new { d = sResult });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

	}  // class RestController
}  // namespace SplendidCRM
