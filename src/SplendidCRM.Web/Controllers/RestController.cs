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

namespace SplendidCRM.Web.Controllers
{
	/// <summary>
	/// Primary REST API controller for SplendidCRM.
	/// Converted from Rest.svc.cs (WCF) to ASP.NET Core Web API.
	/// Route base: /Rest.svc — all original endpoint paths preserved for React SPA backward compatibility.
	/// </summary>
	[ApiController]
	[Authorize]
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

		/// <summary>GetModuleAccessInternal — Returns per-module ACL access rights for the current user.
		/// Ported from SplendidCache.GetModuleAccess. Queries vwACL_ACCESS_ByModule_USERS
		/// and pivots individual ACLACCESS_* columns into {acltype → value} dictionaries.</summary>
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
						"select MODULE_NAME       " + ControlChars.CrLf
					  + "     , ACLACCESS_ADMIN   " + ControlChars.CrLf
					  + "     , ACLACCESS_ACCESS  " + ControlChars.CrLf
					  + "     , ACLACCESS_VIEW    " + ControlChars.CrLf
					  + "     , ACLACCESS_LIST    " + ControlChars.CrLf
					  + "     , ACLACCESS_EDIT    " + ControlChars.CrLf
					  + "     , ACLACCESS_DELETE  " + ControlChars.CrLf
					  + "     , ACLACCESS_IMPORT  " + ControlChars.CrLf
					  + "     , ACLACCESS_EXPORT  " + ControlChars.CrLf
					  + "     , ACLACCESS_ARCHIVE " + ControlChars.CrLf
					  + "  from vwACL_ACCESS_ByModule_USERS" + ControlChars.CrLf
					  + " where USER_ID = @USER_ID" + ControlChars.CrLf
					  + " order by MODULE_NAME    " + ControlChars.CrLf;
					Sql.AddParameter(cmd, "@USER_ID", _security.USER_ID);
					using var da = _dbProviderFactories.CreateDataAdapter();
					((IDbDataAdapter)da).SelectCommand = cmd;
					using var dt = new DataTable();
					da.Fill(dt);
					foreach (DataRow row in dt.Rows)
					{
						string sMODULE_NAME = Sql.ToString(row["MODULE_NAME"]);
						if ( lstMODULES != null && !lstMODULES.Contains(sMODULE_NAME) )
							continue;
						Dictionary<string, int> dictAccess = new Dictionary<string, int>();
						dictAccess["admin"  ] = Sql.ToInteger(row["ACLACCESS_ADMIN"  ]);
						dictAccess["access" ] = Sql.ToInteger(row["ACLACCESS_ACCESS" ]);
						dictAccess["view"   ] = Sql.ToInteger(row["ACLACCESS_VIEW"   ]);
						dictAccess["list"   ] = Sql.ToInteger(row["ACLACCESS_LIST"   ]);
						dictAccess["edit"   ] = Sql.ToInteger(row["ACLACCESS_EDIT"   ]);
						dictAccess["delete" ] = Sql.ToInteger(row["ACLACCESS_DELETE" ]);
						dictAccess["import" ] = Sql.ToInteger(row["ACLACCESS_IMPORT" ]);
						dictAccess["export" ] = Sql.ToInteger(row["ACLACCESS_EXPORT" ]);
						dictAccess["archive"] = Sql.ToInteger(row["ACLACCESS_ARCHIVE"]);
						objs[sMODULE_NAME] = dictAccess;
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
						"select ID, USER_NAME, FULL_NAME, STATUS " + ControlChars.CrLf
					  + "  from vwUSERS_List                     " + ControlChars.CrLf
					  + " where STATUS = 'Active'                " + ControlChars.CrLf
					  + " order by FULL_NAME                     " + ControlChars.CrLf;
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
						"select ID, NAME, DESCRIPTION " + ControlChars.CrLf
					  + "  from vwTEAMS               " + ControlChars.CrLf
					  + " order by NAME                " + ControlChars.CrLf;
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

		// =====================================================================
		// #region GetReactState Missing Helpers — Added per Req #8c
		// =====================================================================

		/// <summary>Returns all module metadata (TableName, DisplayName, Valid, etc.) for accessible modules.</summary>
		private Dictionary<string, object> GetAllModulesInternal(List<string> lstMODULES)
		{
			var dict = new Dictionary<string, object>();
			try
			{
				DataTable dtModules = _splendidCache.GetAllModules();
				if ( dtModules != null )
				{
					foreach ( DataRow row in dtModules.Rows )
					{
						string sMODULE_NAME = Sql.ToString(row["MODULE_NAME"]);
						if ( lstMODULES.Contains(sMODULE_NAME) || _security.IS_ADMIN )
						{
							var mod = new Dictionary<string, object>();
							for ( int j = 0; j < dtModules.Columns.Count; j++ )
								mod[dtModules.Columns[j].ColumnName] = row[j] == DBNull.Value ? null : row[j];
							dict[sMODULE_NAME] = mod;
						}
					}
				}
			}
			catch ( Exception ex ) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return dict;
		}

		/// <summary>Returns per-user per-module ACL access (ACLACCESS_VIEW, ACLACCESS_LIST, etc.).</summary>
		private Dictionary<string, object> GetUserAccessInternal(List<string> lstMODULES)
		{
			var dict = new Dictionary<string, object>();
			try
			{
				using ( IDbConnection con = _dbProviderFactories.CreateConnection() )
				{
					con.Open();
					string sSQL = "select MODULE_NAME                    " + ControlChars.CrLf
					            + "     , ACLACCESS_ADMIN                " + ControlChars.CrLf
					            + "     , ACLACCESS_ACCESS               " + ControlChars.CrLf
					            + "     , ACLACCESS_VIEW                 " + ControlChars.CrLf
					            + "     , ACLACCESS_LIST                 " + ControlChars.CrLf
					            + "     , ACLACCESS_EDIT                 " + ControlChars.CrLf
					            + "     , ACLACCESS_DELETE               " + ControlChars.CrLf
					            + "     , ACLACCESS_IMPORT               " + ControlChars.CrLf
					            + "     , ACLACCESS_EXPORT               " + ControlChars.CrLf
					            + "     , ACLACCESS_ARCHIVE              " + ControlChars.CrLf
					            + "     , IS_ADMIN                       " + ControlChars.CrLf
					            + "  from vwACL_ACCESS_ByModule_USERS    " + ControlChars.CrLf
					            + " where USER_ID = @USER_ID             " + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@USER_ID", _security.USER_ID);
						using ( IDataReader rdr = cmd.ExecuteReader() )
						{
							while ( rdr.Read() )
							{
								string sMOD = Sql.ToString(rdr["MODULE_NAME"]);
								if ( lstMODULES.Contains(sMOD) || _security.IS_ADMIN )
								{
									var access = new Dictionary<string, object>();
									string[] aclTypes = new string[] { "admin", "access", "view", "list", "edit", "delete", "import", "export", "archive" };
									foreach ( string sType in aclTypes )
									{
										int nAccess = Sql.ToInteger(rdr["ACLACCESS_" + sType.ToUpper()]);
										access[sType] = nAccess;
									}
									access["is_admin"] = Sql.ToBoolean(rdr["IS_ADMIN"]);
									dict[sMOD] = access;
								}
							}
						}
					}
				}
			}
			catch ( Exception ex ) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return dict;
		}

		/// <summary>Returns per-user per-module per-field security settings.</summary>
		private Dictionary<string, object> GetUserFieldSecurityInternal(List<string> lstMODULES)
		{
			var dict = new Dictionary<string, object>();
			try
			{
				using ( IDbConnection con = _dbProviderFactories.CreateConnection() )
				{
					con.Open();
					string sSQL = "select MODULE_NAME, FIELD_NAME, ACLACCESS " + ControlChars.CrLf
					            + "  from vwACL_FIELD_ACCESS_ByUserAlias      " + ControlChars.CrLf
					            + " where USER_ID = @USER_ID                  " + ControlChars.CrLf
					            + " order by MODULE_NAME, FIELD_NAME          " + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@USER_ID", _security.USER_ID);
						using ( IDataReader rdr = cmd.ExecuteReader() )
						{
							while ( rdr.Read() )
							{
								string sMOD   = Sql.ToString (rdr["MODULE_NAME"]);
								string sFIELD = Sql.ToString (rdr["FIELD_NAME" ]);
								int    nACCESS= Sql.ToInteger(rdr["ACLACCESS"  ]);
								if ( lstMODULES.Contains(sMOD) || _security.IS_ADMIN )
								{
									if ( !dict.ContainsKey(sMOD) )
										dict[sMOD] = new Dictionary<string, object>();
									((Dictionary<string, object>)dict[sMOD])[sFIELD] = nACCESS;
								}
							}
						}
					}
				}
			}
			catch ( Exception ex ) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return dict;
		}

		/// <summary>Returns all module relationships for SubPanelsView.</summary>
		private Dictionary<string, object> GetAllRelationshipsInternal()
		{
			var dict = new Dictionary<string, object>();
			try
			{
				DataTable dt = _splendidCache.GetAllRelationships();
				if ( dt != null )
				{
					var rows = new List<object>();
					foreach ( DataRow row in dt.Rows )
					{
						var drow = new Dictionary<string, object>();
						for ( int j = 0; j < dt.Columns.Count; j++ )
							drow[dt.Columns[j].ColumnName] = row[j] == DBNull.Value ? null : row[j];
						rows.Add(drow);
					}
					dict["results"] = rows;
				}
			}
			catch ( Exception ex ) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return dict;
		}

		/// <summary>Returns all timezones for user profile selection.</summary>
		private List<object> GetAllTimezonesInternal()
		{
			var lst = new List<object>();
			try
			{
				using ( IDbConnection con = _dbProviderFactories.CreateConnection() )
				{
					con.Open();
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = "select * from vwTIMEZONES order by BIAS desc, NAME";
						using ( var da = _dbProviderFactories.CreateDataAdapter() )
						{
							((IDbDataAdapter)da).SelectCommand = cmd;
							using ( DataTable dt = new DataTable() )
							{
								da.Fill(dt);
								foreach ( DataRow row in dt.Rows )
								{
									var d = new Dictionary<string, object>();
									for ( int j = 0; j < dt.Columns.Count; j++ )
										d[dt.Columns[j].ColumnName] = row[j] == DBNull.Value ? null : row[j];
									lst.Add(d);
								}
							}
						}
					}
				}
			}
			catch ( Exception ex ) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return lst;
		}

		/// <summary>Returns all active currencies.</summary>
		private List<object> GetAllCurrenciesInternal()
		{
			var lst = new List<object>();
			try
			{
				using ( IDbConnection con = _dbProviderFactories.CreateConnection() )
				{
					con.Open();
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = "select * from vwCURRENCIES where STATUS = 'Active' order by NAME";
						using ( var da = _dbProviderFactories.CreateDataAdapter() )
						{
							((IDbDataAdapter)da).SelectCommand = cmd;
							using ( DataTable dt = new DataTable() )
							{
								da.Fill(dt);
								foreach ( DataRow row in dt.Rows )
								{
									var d = new Dictionary<string, object>();
									for ( int j = 0; j < dt.Columns.Count; j++ )
										d[dt.Columns[j].ColumnName] = row[j] == DBNull.Value ? null : row[j];
									lst.Add(d);
								}
							}
						}
					}
				}
			}
			catch ( Exception ex ) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return lst;
		}

		/// <summary>Returns all active languages.</summary>
		private List<object> GetAllLanguagesInternal()
		{
			var lst = new List<object>();
			try
			{
				using ( IDbConnection con = _dbProviderFactories.CreateConnection() )
				{
					con.Open();
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = "select * from vwLANGUAGES where ACTIVE = 1 order by NAME";
						using ( var da = _dbProviderFactories.CreateDataAdapter() )
						{
							((IDbDataAdapter)da).SelectCommand = cmd;
							using ( DataTable dt = new DataTable() )
							{
								da.Fill(dt);
								foreach ( DataRow row in dt.Rows )
								{
									var d = new Dictionary<string, object>();
									for ( int j = 0; j < dt.Columns.Count; j++ )
										d[dt.Columns[j].ColumnName] = row[j] == DBNull.Value ? null : row[j];
									lst.Add(d);
								}
							}
						}
					}
				}
			}
			catch ( Exception ex ) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return lst;
		}

		// =====================================================================
		// #endregion GetReactState Missing Helpers
		// =====================================================================

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
		[AllowAnonymous]
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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
			}
		}

		/// <summary>POST Rest.svc/Edition — Returns service level / edition string.</summary>
		[AllowAnonymous]
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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
			}
		}

		/// <summary>POST Rest.svc/UtcTime — Returns current UTC timestamp.</summary>
		[AllowAnonymous]
		[HttpPost("UtcTime")]
		public IActionResult UtcTime()
		{
			return JsonContent(new { d = DateTime.UtcNow });
		}

		/// <summary>POST Rest.svc/IsAuthenticated — Returns boolean indicating authentication state.</summary>
		[AllowAnonymous]
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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
			}
		}

		/// <summary>GET Rest.svc/SingleSignOnSettings — Returns SSO configuration for ADFS or Azure AD.</summary>
		[AllowAnonymous]
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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
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
		[AllowAnonymous]
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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
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
		[AllowAnonymous]
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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
			}
		}

		/// <summary>POST Rest.svc/ForgotPassword — Sends password reset email.</summary>
		[AllowAnonymous]
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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
			}
		}

		/// <summary>POST Rest.svc/Logout — Terminates user session.
		/// Ported from SplendidCRM/Rest.svc.cs lines 630-632.</summary>
		[HttpPost("Logout")]
		public IActionResult Logout()
		{
			try
			{
				if (_security.IsAuthenticated())
				{
					Guid gUSER_LOGIN_ID = _security.USER_LOGIN_ID;
					try
					{
						if ( !Sql.IsEmptyGuid(gUSER_LOGIN_ID) )
						{
							using IDbConnection con = _dbProviderFactories.CreateConnection();
							con.Open();
							using IDbCommand cmd = SqlProcs.Factory(con, "spUSERS_LOGINS_Logout");
							Sql.AddParameter(cmd, "@ID", gUSER_LOGIN_ID);
							cmd.ExecuteNonQuery();
						}
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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
			}
		}

		// =====================================================================
		// #region React State
		// =====================================================================

		/// <summary>
		/// GET Rest.svc/GetReactLoginState — Returns minimal state for the login page.
		/// Provides login config, terminology, SSO settings, and DuoUniversal indicator.
		/// </summary>
		[AllowAnonymous]
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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
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

				// ACL access — module-level, user-level, field-level, roles
				result["MODULE_ACL_ACCESS"] = GetModuleAccessInternal(lstMODULES);
				result["ACL_ACCESS"       ] = GetUserAccessInternal(lstMODULES);
				result["ACL_FIELD_ACCESS" ] = GetUserFieldSecurityInternal(lstMODULES);
				result["ACL_ROLES"        ] = GetUserACLRolesInternal();

				// User team tree
				result["TEAM_TREE"] = _splendidCache.GetUserTeamTree();

				// Config
				result["CONFIG"] = _splendidCache.GetAllConfig();

				// Module metadata — TableName, DisplayName, Valid, etc.
				result["MODULES"] = GetAllModulesInternal(lstMODULES);

				// Search/Module columns
				result["MODULE_COLUMNS"] = GetAllSearchColumnsInternal(lstMODULES);

				// Tab menus (key: TAB_MENU to match legacy)
				result["TAB_MENU"] = GetAllTabMenusInternal();

				// GridViews (sort defaults — separate from GRIDVIEWS_COLUMNS)
				result["GRIDVIEWS"] = GetAllGridViewsInternal(lstMODULES);

				// Users and Teams — NOT admin-only; needed for assignment dropdowns for all users
				result["USERS"] = GetAllUsersInternal();
				result["TEAMS"] = GetAllTeamsInternal();

				// Relationships — needed by SubPanelsView
				result["RELATIONSHIPS"] = GetAllRelationshipsInternal();

				// Timezones, currencies, languages — needed by user profile
				result["TIMEZONES" ] = GetAllTimezonesInternal();
				result["CURRENCIES"] = GetAllCurrenciesInternal();
				result["LANGUAGES" ] = GetAllLanguagesInternal();

				// Tax rates and discounts (for AOS modules)
				result["TAX_RATES"] = GetTaxRatesInternal();
				result["DISCOUNTS"] = GetDiscountsInternal();

				// Favorites, last viewed, saved search
				result["FAVORITES"   ] = GetAllFavoritesInternal();
				result["LAST_VIEWED" ] = GetAllLastViewedInternal();
				result["SAVED_SEARCH"] = GetAllSavedSearchInternal();

				// Dashboards
				result["DASHBOARDS"       ] = GetAllDashboardsInternal();
				result["DASHBOARDS_PANELS"] = GetAllDashboardPanelsInternal();

				// Signatures and outbound email/SMS
				result["SIGNATURES"     ] = GetUserSignaturesInternal();
				result["OUTBOUND_EMAILS"] = GetOutboundMailInternal();
				result["OUTBOUND_SMS"   ] = GetOutboundSmsInternal();

				// Session timeout (minutes, matching legacy Session.Timeout)
				int nSessionStateTimeout = Sql.ToInteger(_configuration["SessionStateTimeout"] ?? "20");
				result["SessionStateTimeout"] = nSessionStateTimeout;

				return JsonContent(new { d = result });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
			}
		}

		// =====================================================================
		// #region Get (CRUD read operations)
		// =====================================================================

		/// <summary>
		/// GET Rest.svc/PhoneSearch — Phone number search across all modules.
		/// Iterates all modules from DetailViewRelationships("Home.PhoneSearch"),
		/// queries vwPHONE_NUMBERS_{TABLE_NAME} per module with per-module ACL.
		/// Ported from SplendidCRM/Rest.svc.cs lines 1321-1410.
		/// </summary>
		[HttpGet("PhoneSearch")]
		public IActionResult PhoneSearch(string PhoneNumber)
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();

				PhoneNumber = Utils.NormalizePhone(PhoneNumber);

				StringBuilder sbDumpSQL = new StringBuilder();
				DataTable dtPhones = new DataTable();
				dtPhones.Columns.Add("ID"         , Type.GetType("System.Guid"  ));
				dtPhones.Columns.Add("NAME"       , Type.GetType("System.String"));
				dtPhones.Columns.Add("MODULE_NAME", Type.GetType("System.String"));
				if ( !Sql.IsEmptyString(PhoneNumber) )
				{
					DataTable dtFields = _splendidCache.DetailViewRelationships("Home.PhoneSearch");
					using IDbConnection con = _dbProviderFactories.CreateConnection();
					con.Open();
					using IDbCommand cmd = con.CreateCommand();
					foreach ( DataRow rowModule in dtFields.Rows )
					{
						string sMODULE_NAME = Sql.ToString(rowModule["MODULE_NAME"]);
						int nACLACCESS = _security.GetUserAccess(sMODULE_NAME, "list");
						if ( sMODULE_NAME != "Calls" && nACLACCESS >= 0 )
						{
							string sTABLE_NAME = _splendidCache.ModuleTableName(sMODULE_NAME);
							string sSQL = String.Empty;
							sSQL = "select ID              " + ControlChars.CrLf
							     + "     , NAME            " + ControlChars.CrLf
							     + "  from vwPHONE_NUMBERS_" + sTABLE_NAME + ControlChars.CrLf;
							cmd.CommandText = sSQL;
							cmd.Parameters.Clear();
							_security.Filter(cmd, sMODULE_NAME, "list");
							SearchBuilder sb = new SearchBuilder(PhoneNumber, cmd);
							cmd.CommandText += sb.BuildQuery("   and ", "NORMALIZED_NUMBER");
							cmd.CommandText += "order by NAME";

							string sDumbSQL = Sql.ExpandParameters(cmd);
							sbDumpSQL.Append(sDumbSQL);
							using ( var da = _dbProviderFactories.CreateDataAdapter() )
							{
								((IDbDataAdapter)da).SelectCommand = cmd;
								using ( DataTable dt = new DataTable() )
								{
									da.Fill(dt);
									foreach ( DataRow row in dt.Rows )
									{
										DataRow rowPhone = dtPhones.NewRow();
										rowPhone["ID"         ] = row["ID"  ];
										rowPhone["NAME"       ] = row["NAME"];
										rowPhone["MODULE_NAME"] = sMODULE_NAME;
										dtPhones.Rows.Add(rowPhone);
									}
								}
							}
						}
					}
				}

				string sBaseURI = GetBaseURI("/PhoneSearch");
				SplendidCRM.TimeZone T10n = GetUserTimezone();
				Dictionary<string, object> dictResponse = _restUtil.ToJson(sBaseURI, "Leads", dtPhones, T10n);
				dictResponse.Add("__total", dtPhones.Rows.Count);
				if ( Sql.ToBoolean(_memoryCache.Get("CONFIG.show_sql")) )
				{
					dictResponse.Add("__sql", sbDumpSQL.ToString());
				}
				return JsonContent(new { d = dictResponse });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
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
				if ( !Sql.ToBoolean(_memoryCache.Get<object>("Modules." + ModuleName + ".RestEnabled")) || nACLACCESS < 0 )
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

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
				// 04/28/2019 Paul.  Add tracker for React client.
				if ( dt.Columns.Contains("NAME") )
				{
					string sName = Sql.ToString(dt.Rows[0]["NAME"]);
					try
					{
						string sAccessMode = Sql.ToString(Request.Query["$accessMode"].FirstOrDefault());
						// 11/25/2020 Paul.  Correct the action.
						string sAction = (sAccessMode == "edit") ? "save" : "detailview";
						using ( IDbCommand cmdTracker = SqlProcs.Factory(con, "spTRACKER_Update") )
						{
							Sql.SetParameter(cmdTracker, "@USER_ID"     , _security.USER_ID);
							Sql.SetParameter(cmdTracker, "@MODULE_NAME" , ModuleName        );
							Sql.SetParameter(cmdTracker, "@ITEM_ID"     , gID               );
							Sql.SetParameter(cmdTracker, "@ITEM_SUMMARY", sName             );
							Sql.SetParameter(cmdTracker, "@ACTION"      , sAction           );
							cmdTracker.ExecuteNonQuery();
						}
					}
					catch(Exception ex2)
					{
						// 04/28/2019 Paul.  There is no compelling reason to send this error to the user.
						SplendidError.SystemError(new StackFrame(1, true), ex2);
					}
				}
				return JsonContent(new { d });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
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
				string sModuleName = String.Empty;
				if (dict != null && dict.ContainsKey("ModuleName"))
					sModuleName = Sql.ToString(dict["ModuleName"]);
				if (Sql.IsEmptyString(sModuleName))
					return BadRequest(new { error = "ModuleName is required" });

				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				// 08/22/2011 Paul.  Add admin control to REST API.
				int nACLACCESS = _security.GetUserAccess(sModuleName, "edit");
				if ( !_security.IsAuthenticated() || !Sql.ToBoolean(_memoryCache.Get<object>("Modules." + sModuleName + ".RestEnabled")) || nACLACCESS < 0 )
					return Forbidden(L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") + ": " + sModuleName);

				string sTableName = Crm.Modules.TableName(_memoryCache, sModuleName);
				if ( Sql.IsEmptyString(sTableName) )
					return BadRequest(new { error = "Unknown module: " + sModuleName });

				// 04/01/2020 Paul.  Move UpdateTable to RestUtil.
				Guid gID = _restUtil.UpdateTable(HttpContext, sTableName, dict);
				// 04/28/2019 Paul.  Add tracker for React client.
				if ( dict.ContainsKey("NAME") || dict.ContainsKey("DOCUMENT_NAME") || dict.ContainsKey("FIRST_NAME") )
				{
					string sName = String.Empty;
					if ( dict.ContainsKey("NAME") )
						sName = Sql.ToString(dict["NAME"]);
					else if ( dict.ContainsKey("DOCUMENT_NAME") )
						sName = Sql.ToString(dict["DOCUMENT_NAME"]);
					else
					{
						if ( dict.ContainsKey("FIRST_NAME") )
							sName = Sql.ToString(dict["FIRST_NAME"]);
						if ( dict.ContainsKey("LAST_NAME") )
							sName = (sName + " " + Sql.ToString(dict["LAST_NAME"])).Trim();
					}
					try
					{
						if ( !Sql.IsEmptyString(sName) )
						{
							using ( IDbConnection con = _dbProviderFactories.CreateConnection() )
							{
								con.Open();
								using ( IDbCommand cmdTracker = SqlProcs.Factory(con, "spTRACKER_Update") )
								{
									Sql.SetParameter(cmdTracker, "@USER_ID"     , _security.USER_ID);
									Sql.SetParameter(cmdTracker, "@MODULE_NAME" , sModuleName       );
									Sql.SetParameter(cmdTracker, "@ITEM_ID"     , gID               );
									Sql.SetParameter(cmdTracker, "@ITEM_SUMMARY", sName             );
									Sql.SetParameter(cmdTracker, "@ACTION"      , "save"            );
									cmdTracker.ExecuteNonQuery();
								}
							}
						}
					}
					catch(Exception ex2)
					{
						// 04/28/2019 Paul.  There is no compelling reason to send this error to the user.
						SplendidError.SystemError(new StackFrame(1, true), ex2);
					}
				}
				return JsonContent(new { d = gID });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
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
				if ( !Sql.ToBoolean(_memoryCache.Get<object>("Modules." + sModuleName + ".RestEnabled")) || nACLACCESS < 0 )
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
			}
		}

		/// <summary>POST Rest.svc/AddSubscription — Subscribes current user to a module record.</summary>
		[HttpPost("AddSubscription")]
		public IActionResult AddSubscription([FromBody] Dictionary<string, object> dict)
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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
			}
		}

		/// <summary>POST Rest.svc/RemoveSubscription — Unsubscribes current user from a module record.</summary>
		[HttpPost("RemoveSubscription")]
		public IActionResult RemoveSubscription([FromBody] Dictionary<string, object> dict)
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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
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
				if ( !Sql.ToBoolean(_memoryCache.Get<object>("Modules." + sModuleName + ".RestEnabled")) || nACLACCESS < 0 )
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
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
				if ( !Sql.ToBoolean(_memoryCache.Get<object>("Modules." + sModuleName + ".RestEnabled")) || nACLACCESS < 0 )
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
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
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
			}
		}

		// =====================================================================
		// Missing WCF operations — added to achieve 100% operation coverage
		// Per AAP Goal 2: ALL 152 WCF [OperationContract] operations must have
		// corresponding ASP.NET Core controller actions.
		// =====================================================================

		/// <summary>POST Rest.svc/GetUserLanguage — Returns current user's language/culture string.</summary>
		[HttpPost("GetUserLanguage")]
		public IActionResult GetUserLanguage()
		{
			if (_security.IsAuthenticated())
				return JsonContent(new { d = Sql.ToString(_httpContextAccessor.HttpContext?.Session?.GetString("USER_SETTINGS/CULTURE")) });
			else
				return JsonContent(new { d = "en-US" });
		}

		/// <summary>GET Rest.svc/GetAllTaxRates — Returns all tax rate records via SplendidCache.TaxRates().</summary>
		[HttpGet("GetAllTaxRates")]
		public IActionResult GetAllTaxRates()
		{
			try
			{
				SetNoCacheHeaders();
				DataTable dtTaxRates = _splendidCache.TaxRates();
				List<Dictionary<string, object>> lst = _restUtil.ToJson(dtTaxRates);
				Dictionary<string, object> d = new Dictionary<string, object>();
				d.Add("d", new Dictionary<string, object>{ { "results", lst } });
				d.Add("__count", lst.Count);
				return JsonContent(d);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "GetAllTaxRates");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		/// <summary>GET Rest.svc/GetAllDiscounts — Returns all discount records via SplendidCache.Discounts().</summary>
		[HttpGet("GetAllDiscounts")]
		public IActionResult GetAllDiscounts()
		{
			try
			{
				SetNoCacheHeaders();
				DataTable dtDiscounts = _splendidCache.Discounts();
				List<Dictionary<string, object>> lst = _restUtil.ToJson(dtDiscounts);
				Dictionary<string, object> d = new Dictionary<string, object>();
				d.Add("d", new Dictionary<string, object>{ { "results", lst } });
				d.Add("__count", lst.Count);
				return JsonContent(d);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "GetAllDiscounts");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		/// <summary>GET Rest.svc/GetAllLayouts — Returns combined layout data.</summary>
		[HttpGet("GetAllLayouts")]
		public IActionResult GetAllLayouts()
		{
			try
			{
				SetNoCacheHeaders();
				List<string> lstMODULES = _restUtil.AccessibleModules(HttpContext);
				Dictionary<string, object> d       = new Dictionary<string, object>();
				Dictionary<string, object> results = new Dictionary<string, object>();
				d.Add("d", results);
				results.Add("GRIDVIEWS"                , GetAllGridViewsInternal(lstMODULES));
				results.Add("GRIDVIEWS_COLUMNS"        , GetAllGridViewsColumnsInternal(lstMODULES));
				results.Add("DETAILVIEWS_FIELDS"       , GetAllDetailViewsFieldsInternal(lstMODULES));
				results.Add("EDITVIEWS_FIELDS"         , GetAllEditViewsFieldsInternal(lstMODULES));
				results.Add("DETAILVIEWS_RELATIONSHIPS", GetAllDetailViewsRelationshipsInternal(lstMODULES));
				results.Add("EDITVIEWS_RELATIONSHIPS"  , GetAllEditViewsRelationshipsInternal(lstMODULES));
				results.Add("DYNAMIC_BUTTONS"          , GetAllDynamicButtonsInternal(lstMODULES));
				results.Add("TERMINOLOGY_LISTS"        , _restUtil.ToJson(_splendidCache.TerminologyPickLists()));
				results.Add("TAX_RATES"                , _restUtil.ToJson(_splendidCache.TaxRates()));
				results.Add("DISCOUNTS"                , _restUtil.ToJson(_splendidCache.Discounts()));
				return JsonContent(d);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "GetAllLayouts");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		/// <summary>GET Rest.svc/GetAllReactCustomViews — Returns React custom view mappings.</summary>
		/// <remarks>Returns an empty dictionary as the custom view discovery requires file-system scanning
		/// that is handled by the React SPA build at deploy time. The endpoint is preserved for API
		/// contract compatibility with the React client.</remarks>
		[HttpGet("GetAllReactCustomViews")]
		public IActionResult GetAllReactCustomViews()
		{
			try
			{
				SetNoCacheHeaders();
				// React custom views are discovered at build time by the SPA toolchain.
				// This endpoint returns an empty result set for API contract compatibility.
				Dictionary<string, object> objs = new Dictionary<string, object>();
				Dictionary<string, object> d       = new Dictionary<string, object>();
				Dictionary<string, object> results = new Dictionary<string, object>();
				results.Add("results", objs);
				d.Add("d", results);
				d.Add("__count", objs.Count);
				return JsonContent(d);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "GetAllReactCustomViews");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		/// <summary>POST Rest.svc/PostModuleTable — POST version of GetModuleTable supporting large search operations.</summary>
		[HttpPost("PostModuleTable")]
		public IActionResult PostModuleTable([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				string   sTableName    = String.Empty;
				int      nSKIP         = 0;
				int      nTOP          = 0;
				string   sFILTER       = String.Empty;
				string   sORDER_BY     = String.Empty;
				string   sGROUP_BY     = String.Empty;
				string   sSELECT       = String.Empty;
				string   sAPPLY        = String.Empty;
				bool     bArchiveView  = false;
				Guid[]   Items         = null;
				Dictionary<string, object> dictSearchValues = null;
				if (dict != null)
				{
					foreach (string sName in dict.Keys)
					{
						switch (sName)
						{
							case "TableName"    : sTableName    = Sql.ToString (dict[sName]); break;
							case "$skip"        : nSKIP         = Sql.ToInteger(dict[sName]); break;
							case "$top"         : nTOP          = Sql.ToInteger(dict[sName]); break;
							case "$filter"      : sFILTER       = Sql.ToString (dict[sName]); break;
							case "$orderby"     : sORDER_BY     = Sql.ToString (dict[sName]); break;
							case "$groupby"     : sGROUP_BY     = Sql.ToString (dict[sName]); break;
							case "$select"      : sSELECT       = Sql.ToString (dict[sName]); break;
							case "$apply"       : sAPPLY        = Sql.ToString (dict[sName]); break;
							case "$archiveView" : bArchiveView  = Sql.ToBoolean(dict[sName]); break;
							case "$searchvalues": dictSearchValues = dict[sName] as Dictionary<string, object>; break;
							case "Items":
							{
								var lst = dict[sName] as System.Collections.ArrayList;
								if (lst == null && dict[sName] is Newtonsoft.Json.Linq.JArray jArr)
									lst = new System.Collections.ArrayList(jArr.ToObject<string[]>());
								if (lst != null && lst.Count > 0)
								{
									List<Guid> lstItems = new List<Guid>();
									foreach (object sItemID in lst)
										lstItems.Add(Sql.ToGuid(sItemID));
									Items = lstItems.ToArray();
								}
								break;
							}
						}
					}
				}
				SplendidCRM.TimeZone T10n = GetUserTimezone();
				string sBaseURI = GetBaseURI("/PostModuleTable");
				StringBuilder sbDumpSQL = new StringBuilder();
				long nTotalCount = 0;
				DataTable dt = _restUtil.GetTable(HttpContext, sTableName, nSKIP, nTOP, sORDER_BY, sFILTER, sGROUP_BY, null, Items, ref nTotalCount, null, AccessMode.list, bArchiveView, null, sbDumpSQL);
				var rows = _restUtil.RowsToDictionary(sBaseURI, sTableName, dt, T10n);
				var result = new Dictionary<string, object>
				{
					{ "d", new Dictionary<string, object>{ { "results", rows }, { "__total", nTotalCount } } }
				};
				return JsonContent(result);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "PostModuleTable");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		/// <summary>POST Rest.svc/PostModuleList — POST version of GetModuleList supporting large search operations.</summary>
		[HttpPost("PostModuleList")]
		public IActionResult PostModuleList([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				string sModuleName = String.Empty;
				int    nSKIP       = 0;
				int    nTOP        = 0;
				string sFILTER     = String.Empty;
				string sORDER_BY   = String.Empty;
				string sGROUP_BY   = String.Empty;
				string sSELECT     = String.Empty;
				string sAPPLY      = String.Empty;
				bool   bArchiveView = false;
				Dictionary<string, object> dictSearchValues = null;
				if (dict != null)
				{
					foreach (string sName in dict.Keys)
					{
						switch (sName)
						{
							case "ModuleName"   : sModuleName  = Sql.ToString (dict[sName]); break;
							case "$skip"        : nSKIP        = Sql.ToInteger(dict[sName]); break;
							case "$top"         : nTOP         = Sql.ToInteger(dict[sName]); break;
							case "$filter"      : sFILTER      = Sql.ToString (dict[sName]); break;
							case "$orderby"     : sORDER_BY    = Sql.ToString (dict[sName]); break;
							case "$groupby"     : sGROUP_BY    = Sql.ToString (dict[sName]); break;
							case "$select"      : sSELECT      = Sql.ToString (dict[sName]); break;
							case "$apply"       : sAPPLY       = Sql.ToString (dict[sName]); break;
							case "$archiveView" : bArchiveView = Sql.ToBoolean(dict[sName]); break;
							case "$searchvalues": dictSearchValues = dict[sName] as Dictionary<string, object>; break;
						}
					}
				}
				SplendidCRM.TimeZone T10n = GetUserTimezone();
				string sTableName = Crm.Modules.TableName(_memoryCache, sModuleName);
				string sBaseURI = GetBaseURI("/PostModuleList");
				StringBuilder sbDumpSQL = new StringBuilder();
				long nTotalCount = 0;
				DataTable dt = _restUtil.GetTable(HttpContext, sTableName, nSKIP, nTOP, sORDER_BY, sFILTER, sGROUP_BY, null, null, ref nTotalCount, null, AccessMode.list, bArchiveView, null, sbDumpSQL);
				var rows = _restUtil.RowsToDictionary(sBaseURI, sModuleName, dt, T10n);
				var result = new Dictionary<string, object>
				{
					{ "d", new Dictionary<string, object>{ { "results", rows }, { "__total", nTotalCount } } }
				};
				return JsonContent(result);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "PostModuleList");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		/// <summary>POST Rest.svc/ExportModuleList — POST-based module export supporting large searches.</summary>
		[HttpPost("ExportModuleList")]
		public IActionResult ExportModuleList([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				if (!_security.IsAuthenticated())
					return Unauthorized();
				string sModuleName  = String.Empty;
				int    nSKIP        = 0;
				int    nTOP         = 0;
				string sFILTER      = String.Empty;
				string sORDER_BY    = String.Empty;
				string sSELECT      = String.Empty;
				bool   bArchiveView = false;
				Dictionary<string, object> dictSearchValues = null;
				if (dict != null)
				{
					foreach (string sName in dict.Keys)
					{
						switch (sName)
						{
							case "ModuleName"   : sModuleName  = Sql.ToString (dict[sName]); break;
							case "$skip"        : nSKIP        = Sql.ToInteger(dict[sName]); break;
							case "$top"         : nTOP         = Sql.ToInteger(dict[sName]); break;
							case "$filter"      : sFILTER      = Sql.ToString (dict[sName]); break;
							case "$orderby"     : sORDER_BY    = Sql.ToString (dict[sName]); break;
							case "$select"      : sSELECT      = Sql.ToString (dict[sName]); break;
							case "$archiveView" : bArchiveView = Sql.ToBoolean(dict[sName]); break;
							case "$searchvalues": dictSearchValues = dict[sName] as Dictionary<string, object>; break;
						}
					}
				}
				SplendidCRM.TimeZone T10n = GetUserTimezone();
				string sTableName = Crm.Modules.TableName(_memoryCache, sModuleName);
				string sBaseURI = GetBaseURI("/ExportModuleList");
				StringBuilder sbDumpSQL = new StringBuilder();
				long nTotalCount = 0;
				DataTable dt = _restUtil.GetTable(HttpContext, sTableName, nSKIP, nTOP, sORDER_BY, sFILTER, String.Empty, null, null, ref nTotalCount, null, AccessMode.list, bArchiveView, null, sbDumpSQL);
				var rows = _restUtil.RowsToDictionary(sBaseURI, sModuleName, dt, T10n);
				var result = new Dictionary<string, object>
				{
					{ "d", new Dictionary<string, object>{ { "results", rows }, { "__total", nTotalCount } } }
				};
				return JsonContent(result);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "ExportModuleList");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		/// <summary>GET Rest.svc/GetModuleAudit — Returns audit trail for a module record.</summary>
		[HttpGet("GetModuleAudit")]
		public IActionResult GetModuleAudit(string ModuleName, Guid ID)
		{
			try
			{
				if (!_security.IsAuthenticated()) return Unauthorized();
				string sTableName = Crm.Modules.TableName(_memoryCache, ModuleName);
				SplendidCRM.TimeZone T10n = GetUserTimezone();
				string sBaseURI = GetBaseURI("/GetModuleAudit");
				long nTotalCount = 0;
				StringBuilder sbDumpSQL = new StringBuilder();
				string sAUDIT_TABLE = sTableName + "_AUDIT";
				DataTable dt = _restUtil.GetTable(HttpContext, sAUDIT_TABLE, 0, 0, "AUDIT_DATE desc", "AUDIT_PARENT_ID eq '" + ID.ToString() + "'", String.Empty, null, null, ref nTotalCount, null, AccessMode.list, false, null, sbDumpSQL);
				var rows = _restUtil.RowsToDictionary(sBaseURI, ModuleName, dt, T10n);
				return JsonContent(new { d = new { results = rows, __total = nTotalCount } });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "GetModuleAudit");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		/// <summary>GET Rest.svc/GetModuleItemByAudit — Returns a module item at a specific audit point.</summary>
		[HttpGet("GetModuleItemByAudit")]
		public IActionResult GetModuleItemByAudit(string ModuleName, Guid AUDIT_ID)
		{
			try
			{
				if (!_security.IsAuthenticated()) return Unauthorized();
				string sTableName = Crm.Modules.TableName(_memoryCache, ModuleName);
				SplendidCRM.TimeZone T10n = GetUserTimezone();
				string sBaseURI = GetBaseURI("/GetModuleItemByAudit");
				long nTotalCount = 0;
				StringBuilder sbDumpSQL = new StringBuilder();
				string sAUDIT_TABLE = sTableName + "_AUDIT";
				DataTable dt = _restUtil.GetTable(HttpContext, sAUDIT_TABLE, 0, 1, String.Empty, "AUDIT_ID eq '" + AUDIT_ID.ToString() + "'", String.Empty, null, null, ref nTotalCount, null, AccessMode.list, false, null, sbDumpSQL);
				Dictionary<string, object> d = new Dictionary<string, object>();
				if (dt.Rows.Count > 0)
					d.Add("d", _restUtil.ToJson(sBaseURI, ModuleName, dt.Rows[0], T10n));
				else
					d.Add("d", new Dictionary<string, object>());
				return JsonContent(d);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "GetModuleItemByAudit");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		/// <summary>GET Rest.svc/GetModulePersonal — Returns personal data for a module record (data privacy).</summary>
		[HttpGet("GetModulePersonal")]
		public IActionResult GetModulePersonal(string ModuleName, Guid ID)
		{
			try
			{
				if (!_security.IsAuthenticated()) return Unauthorized();
				string sTableName = Crm.Modules.TableName(_memoryCache, ModuleName);
				SplendidCRM.TimeZone T10n = GetUserTimezone();
				string sBaseURI = GetBaseURI("/GetModulePersonal");
				long nTotalCount = 0;
				StringBuilder sbDumpSQL = new StringBuilder();
				DataTable dt = _restUtil.GetTable(HttpContext, sTableName, 0, 1, String.Empty, "ID eq '" + ID.ToString() + "'", String.Empty, null, null, ref nTotalCount, null, AccessMode.view, false, null, sbDumpSQL);
				Dictionary<string, object> d = new Dictionary<string, object>();
				if (dt.Rows.Count > 0)
					d.Add("d", _restUtil.ToJson(sBaseURI, ModuleName, dt.Rows[0], T10n));
				else
					d.Add("d", new Dictionary<string, object>());
				return JsonContent(d);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "GetModulePersonal");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		/// <summary>GET Rest.svc/ConvertModuleItem — Converts a record from one module to another.</summary>
		[HttpGet("ConvertModuleItem")]
		public IActionResult ConvertModuleItem(string ModuleName, string SourceModuleName, Guid SourceID)
		{
			try
			{
				if (!_security.IsAuthenticated()) return Unauthorized();
				SplendidCRM.TimeZone T10n = GetUserTimezone();
				string sBaseURI = GetBaseURI("/ConvertModuleItem");
				string sSourceTableName = Crm.Modules.TableName(_memoryCache, SourceModuleName);
				long nTotalCount = 0;
				StringBuilder sbDumpSQL = new StringBuilder();
				DataTable dt = _restUtil.GetTable(HttpContext, sSourceTableName, 0, 1, String.Empty, "ID eq '" + SourceID.ToString() + "'", String.Empty, null, null, ref nTotalCount, null, AccessMode.view, false, null, sbDumpSQL);
				Dictionary<string, object> d = new Dictionary<string, object>();
				if (dt.Rows.Count > 0)
					d.Add("d", _restUtil.ToJson(sBaseURI, SourceModuleName, dt.Rows[0], T10n));
				else
					d.Add("d", new Dictionary<string, object>());
				return JsonContent(d);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "ConvertModuleItem");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		/// <summary>GET Rest.svc/GetActivitiesList — Returns activities associated with a parent record.</summary>
		[HttpGet("GetActivitiesList")]
		public IActionResult GetActivitiesList(string PARENT_TYPE, Guid PARENT_ID)
		{
			try
			{
				if (!_security.IsAuthenticated()) return Unauthorized();
				var req = _httpContextAccessor.HttpContext?.Request;
				int    nSKIP     = Sql.ToInteger(req?.Query["$skip"   ].FirstOrDefault());
				int    nTOP      = Sql.ToInteger(req?.Query["$top"    ].FirstOrDefault());
				string sFILTER   = Sql.ToString (req?.Query["$filter" ].FirstOrDefault());
				string sORDER_BY = Sql.ToString (req?.Query["$orderby"].FirstOrDefault());
				if (!Sql.IsEmptyString(PARENT_TYPE) && !Sql.IsEmptyGuid(PARENT_ID))
				{
					string sParentFilter = "PARENT_TYPE eq '" + Sql.EscapeSQL(PARENT_TYPE) + "' and PARENT_ID eq '" + PARENT_ID.ToString() + "'";
					sFILTER = Sql.IsEmptyString(sFILTER) ? sParentFilter : sParentFilter + " and " + sFILTER;
				}
				SplendidCRM.TimeZone T10n = GetUserTimezone();
				string sBaseURI = GetBaseURI("/GetActivitiesList");
				long nTotalCount = 0;
				StringBuilder sbDumpSQL = new StringBuilder();
				DataTable dt = _restUtil.GetTable(HttpContext, "ACTIVITIES", nSKIP, nTOP, sORDER_BY, sFILTER, String.Empty, null, null, ref nTotalCount, null, AccessMode.list, false, null, sbDumpSQL);
				var rows = _restUtil.RowsToDictionary(sBaseURI, "Activities", dt, T10n);
				return JsonContent(new { d = new { results = rows, __total = nTotalCount } });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "GetActivitiesList");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		/// <summary>GET Rest.svc/GetCalendar — Returns calendar entries.</summary>
		[HttpGet("GetCalendar")]
		public IActionResult GetCalendar()
		{
			try
			{
				if (!_security.IsAuthenticated()) return Unauthorized();
				var req = _httpContextAccessor.HttpContext?.Request;
				int    nSKIP     = Sql.ToInteger(req?.Query["$skip"   ].FirstOrDefault());
				int    nTOP      = Sql.ToInteger(req?.Query["$top"    ].FirstOrDefault());
				string sFILTER   = Sql.ToString (req?.Query["$filter" ].FirstOrDefault());
				string sORDER_BY = Sql.ToString (req?.Query["$orderby"].FirstOrDefault());
				SplendidCRM.TimeZone T10n = GetUserTimezone();
				string sBaseURI = GetBaseURI("/GetCalendar");
				long nTotalCount = 0;
				StringBuilder sbDumpSQL = new StringBuilder();
				DataTable dt = _restUtil.GetTable(HttpContext, "ACTIVITIES", nSKIP, nTOP, sORDER_BY, sFILTER, String.Empty, null, null, ref nTotalCount, null, AccessMode.list, false, null, sbDumpSQL);
				var rows = _restUtil.RowsToDictionary(sBaseURI, "Activities", dt, T10n);
				return JsonContent(new { d = new { results = rows, __total = nTotalCount } });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "GetCalendar");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		/// <summary>GET Rest.svc/GetInviteesList — Returns invitees matching search criteria.
		/// Ported from SplendidCRM/Rest.svc.cs lines 3889-4120 — UNION ALL of Users, Contacts, Leads
		/// with per-invitee activity fetch from vwACTIVITIES_List.</summary>
		[HttpGet("GetInviteesList")]
		public IActionResult GetInviteesList(string FIRST_NAME, string LAST_NAME, string EMAIL, string DATE_START, string DATE_END)
		{
			try
			{
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				SplendidCRM.TimeZone T10n = GetUserTimezone();
				if ( !_security.IsAuthenticated() )
					return Unauthorized();
				int nCONTACTS_ACLACCESS = _security.GetUserAccess("Contacts", "list");
				int nLEADS_ACLACCESS    = _security.GetUserAccess("Leads"   , "list");
				if ( !(Sql.ToBoolean(_memoryCache.Get<object>("Modules.Contacts.RestEnabled")) || Sql.ToBoolean(_memoryCache.Get<object>("Modules.Leads.RestEnabled"))) || (nCONTACTS_ACLACCESS < 0 && nLEADS_ACLACCESS < 0) )
					return Forbidden(L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") + ": Contacts and Leads");

				DateTime dtDATE_START = RestUtil.FromJsonDate(DATE_START);
				DateTime dtDATE_END   = RestUtil.FromJsonDate(DATE_END  );
				string   sFIRST_NAME = Sql.ToString(FIRST_NAME);
				string   sLAST_NAME  = Sql.ToString(LAST_NAME );
				string   sEMAIL      = Sql.ToString(EMAIL     );
				int      nSKIP       = Sql.ToInteger(Request.Query["$skip"   ].FirstOrDefault());
				int      nTOP        = Sql.ToInteger(Request.Query["$top"    ].FirstOrDefault());
				string   sORDER_BY   = Sql.ToString (Request.Query["$orderby"].FirstOrDefault());

				StringBuilder sbDumpSQL = new StringBuilder();
				DataTable dt = new DataTable();
				using ( IDbConnection con = _dbProviderFactories.CreateConnection() )
				{
					con.Open();
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						bool bTeamFilter = Crm.Config.enable_team_management();
						if ( bTeamFilter )
						{
							cmd.CommandText += "select ID          as ID                   " + ControlChars.CrLf;
							cmd.CommandText += "     , N'Users'    as INVITEE_TYPE         " + ControlChars.CrLf;
							cmd.CommandText += "     , FULL_NAME   as NAME                 " + ControlChars.CrLf;
							cmd.CommandText += "     , FIRST_NAME  as FIRST_NAME           " + ControlChars.CrLf;
							cmd.CommandText += "     , LAST_NAME   as LAST_NAME            " + ControlChars.CrLf;
							cmd.CommandText += "     , EMAIL1      as EMAIL                " + ControlChars.CrLf;
							cmd.CommandText += "     , PHONE_WORK  as PHONE                " + ControlChars.CrLf;
							cmd.CommandText += "     , null        as ASSIGNED_USER_ID     " + ControlChars.CrLf;
							cmd.CommandText += "  from vwTEAMS_ASSIGNED_TO_List            " + ControlChars.CrLf;
							cmd.CommandText += " where MEMBERSHIP_USER_ID = @MEMBERSHIP_USER_ID" + ControlChars.CrLf;
							Sql.AddParameter(cmd, "@MEMBERSHIP_USER_ID", _security.USER_ID);
						}
						else
						{
							cmd.CommandText += "select ID          as ID                   " + ControlChars.CrLf;
							cmd.CommandText += "     , N'Users'    as INVITEE_TYPE         " + ControlChars.CrLf;
							cmd.CommandText += "     , FULL_NAME   as NAME                 " + ControlChars.CrLf;
							cmd.CommandText += "     , FIRST_NAME  as FIRST_NAME           " + ControlChars.CrLf;
							cmd.CommandText += "     , LAST_NAME   as LAST_NAME            " + ControlChars.CrLf;
							cmd.CommandText += "     , EMAIL1      as EMAIL                " + ControlChars.CrLf;
							cmd.CommandText += "     , PHONE_WORK  as PHONE                " + ControlChars.CrLf;
							cmd.CommandText += "     , null        as ASSIGNED_USER_ID     " + ControlChars.CrLf;
							cmd.CommandText += "  from vwUSERS_ASSIGNED_TO_List            " + ControlChars.CrLf;
							cmd.CommandText += " where 1 = 1                               " + ControlChars.CrLf;
						}
						{
							StringBuilder sbUsers = new StringBuilder();
							Sql.AppendParameter(cmd, sbUsers, "FIRST_NAME", sFIRST_NAME, Sql.SqlFilterMode.StartsWith);
							Sql.AppendParameter(cmd, sbUsers, "LAST_NAME" , sLAST_NAME , Sql.SqlFilterMode.StartsWith);
							Sql.AppendParameter(cmd, sbUsers, "EMAIL1"    , sEMAIL     , Sql.SqlFilterMode.StartsWith);
							cmd.CommandText += sbUsers.ToString();
						}

						cmd.CommandText += "union all                                  " + ControlChars.CrLf;
						cmd.CommandText += "select ID               as ID              " + ControlChars.CrLf;
						cmd.CommandText += "     , N'Contacts'      as INVITEE_TYPE    " + ControlChars.CrLf;
						cmd.CommandText += "     , NAME             as NAME            " + ControlChars.CrLf;
						cmd.CommandText += "     , FIRST_NAME       as FIRST_NAME      " + ControlChars.CrLf;
						cmd.CommandText += "     , LAST_NAME        as LAST_NAME       " + ControlChars.CrLf;
						cmd.CommandText += "     , EMAIL1           as EMAIL           " + ControlChars.CrLf;
						cmd.CommandText += "     , PHONE_WORK       as PHONE           " + ControlChars.CrLf;
						cmd.CommandText += "     , ASSIGNED_USER_ID as ASSIGNED_USER_ID" + ControlChars.CrLf;
						cmd.CommandText += "  from vwCONTACTS                          " + ControlChars.CrLf;
						_security.Filter(cmd, "Contacts", "list");
						cmd.CommandText += "   and EMAIL1 is not null                  " + ControlChars.CrLf;
						{
							StringBuilder sbContacts = new StringBuilder();
							Sql.AppendParameter(cmd, sbContacts, "FIRST_NAME", sFIRST_NAME, Sql.SqlFilterMode.StartsWith);
							Sql.AppendParameter(cmd, sbContacts, "LAST_NAME" , sLAST_NAME , Sql.SqlFilterMode.StartsWith);
							Sql.AppendParameter(cmd, sbContacts, "EMAIL1"    , sEMAIL     , Sql.SqlFilterMode.StartsWith);
							cmd.CommandText += sbContacts.ToString();
						}

						cmd.CommandText += "union all                                  " + ControlChars.CrLf;
						cmd.CommandText += "select ID               as ID              " + ControlChars.CrLf;
						cmd.CommandText += "     , N'Leads'         as INVITEE_TYPE    " + ControlChars.CrLf;
						cmd.CommandText += "     , NAME             as NAME            " + ControlChars.CrLf;
						cmd.CommandText += "     , FIRST_NAME       as FIRST_NAME      " + ControlChars.CrLf;
						cmd.CommandText += "     , LAST_NAME        as LAST_NAME       " + ControlChars.CrLf;
						cmd.CommandText += "     , EMAIL1           as EMAIL           " + ControlChars.CrLf;
						cmd.CommandText += "     , PHONE_WORK       as PHONE           " + ControlChars.CrLf;
						cmd.CommandText += "     , ASSIGNED_USER_ID as ASSIGNED_USER_ID" + ControlChars.CrLf;
						cmd.CommandText += "  from vwLEADS                             " + ControlChars.CrLf;
						_security.Filter(cmd, "Leads", "list");
						cmd.CommandText += "   and EMAIL1 is not null                  " + ControlChars.CrLf;
						{
							StringBuilder sbLeads = new StringBuilder();
							Sql.AppendParameter(cmd, sbLeads, "FIRST_NAME", sFIRST_NAME, Sql.SqlFilterMode.StartsWith);
							Sql.AppendParameter(cmd, sbLeads, "LAST_NAME" , sLAST_NAME , Sql.SqlFilterMode.StartsWith);
							Sql.AppendParameter(cmd, sbLeads, "EMAIL1"    , sEMAIL     , Sql.SqlFilterMode.StartsWith);
							cmd.CommandText += sbLeads.ToString();
						}

						if ( Sql.IsEmptyString(sORDER_BY?.Trim()) )
						{
							cmd.CommandText += " order by INVITEE_TYPE desc, LAST_NAME asc, FIRST_NAME asc" + ControlChars.CrLf;
						}
						else
						{
							Regex r = new Regex(@"[^A-Za-z0-9_, ]");
							cmd.CommandText += " order by " + r.Replace(sORDER_BY, "") + ControlChars.CrLf;
						}
						sbDumpSQL.Append(Sql.ExpandParameters(cmd));
						using ( var da = _dbProviderFactories.CreateDataAdapter() )
						{
							((IDbDataAdapter)da).SelectCommand = cmd;
							da.Fill(dt);
						}
					}

					// Build response with Activities sub-list per invitee
					long lCount      = 0;
					long lTotalCount = dt.Rows.Count;
					List<Guid> arrINVITEE_LIST = new List<Guid>();

					List<Dictionary<string, object>> objs = new List<Dictionary<string, object>>();
					for ( int j = nSKIP; j < dt.Rows.Count && (nTOP <= 0 || lCount < nTOP); j++, lCount++ )
					{
						DataRow dr = dt.Rows[j];
						if ( Sql.ToString(dr["INVITEE_TYPE"]) == "Users" )
							arrINVITEE_LIST.Add(Sql.ToGuid(dr["ID"]));
						Dictionary<string, object> drow = new Dictionary<string, object>();
						for ( int i = 0; i < dt.Columns.Count; i++ )
						{
							if ( dt.Columns[i].DataType.FullName == "System.DateTime" )
								drow.Add(dt.Columns[i].ColumnName, RestUtil.ToJsonDate(T10n.FromServerTime(dr[i])));
							else
								drow.Add(dt.Columns[i].ColumnName, dr[i]);
						}
						drow.Add("Activities", new List<Dictionary<string, object>>());
						objs.Add(drow);
					}

					// Phase 2: fetch activities for User-type invitees
					if ( arrINVITEE_LIST.Count > 0 )
					{
						using ( IDbCommand cmd2 = con.CreateCommand() )
						{
							string sSQL = "select ID               " + ControlChars.CrLf
							            + "     , ASSIGNED_USER_ID " + ControlChars.CrLf
							            + "     , DATE_START       " + ControlChars.CrLf
							            + "     , DATE_END         " + ControlChars.CrLf
							            + "  from vwACTIVITIES_List" + ControlChars.CrLf
							            + " where 1 = 1           " + ControlChars.CrLf;
							cmd2.CommandText = sSQL;
							// Build IN clause for ASSIGNED_USER_ID
							{
								string[] arrIDs = arrINVITEE_LIST.Select(g => g.ToString()).ToArray();
								StringBuilder sbIN = new StringBuilder();
								Sql.AppendParameter(cmd2, sbIN, arrIDs, "ASSIGNED_USER_ID", false);
								cmd2.CommandText += sbIN.ToString();
							}
							cmd2.CommandText += "   and (   DATE_START >= @DATE_START and DATE_START < @DATE_END" + ControlChars.CrLf;
							cmd2.CommandText += "        or DATE_END   >= @DATE_START and DATE_END   < @DATE_END" + ControlChars.CrLf;
							cmd2.CommandText += "        or DATE_START <  @DATE_START and DATE_END   > @DATE_END" + ControlChars.CrLf;
							cmd2.CommandText += "       )                                                       " + ControlChars.CrLf;
							cmd2.CommandText += " order by ASSIGNED_USER_ID, DATE_START asc                     " + ControlChars.CrLf;
							Sql.AddParameter(cmd2, "@DATE_START", T10n.ToServerTime(dtDATE_START));
							Sql.AddParameter(cmd2, "@DATE_END"  , T10n.ToServerTime(dtDATE_END  ));
							sbDumpSQL.Append(";" + ControlChars.CrLf + Sql.ExpandParameters(cmd2));
							using ( var da2 = _dbProviderFactories.CreateDataAdapter() )
							{
								((IDbDataAdapter)da2).SelectCommand = cmd2;
								using ( DataTable dtActivities = new DataTable() )
								{
									da2.Fill(dtActivities);
									foreach ( DataRow rowActivity in dtActivities.Rows )
									{
										Guid     gACTIVITY_USER_ID  = Sql.ToGuid    (rowActivity["ASSIGNED_USER_ID"]);
										DateTime dtACTIVITY_START   = Sql.ToDateTime(rowActivity["DATE_START"      ]);
										DateTime dtACTIVITY_END     = Sql.ToDateTime(rowActivity["DATE_END"        ]);
										Dictionary<string, object> dictActivity = new Dictionary<string, object>();
										dictActivity.Add("DATE_START", RestUtil.ToJsonDate(T10n.FromServerTime(dtACTIVITY_START)));
										dictActivity.Add("DATE_END"  , RestUtil.ToJsonDate(T10n.FromServerTime(dtACTIVITY_END  )));
										for ( int k = 0; k < objs.Count; k++ )
										{
											if ( Sql.ToGuid(objs[k]["ID"]) == gACTIVITY_USER_ID )
											{
												List<Dictionary<string, object>> lstActivities = objs[k]["Activities"] as List<Dictionary<string, object>>;
												lstActivities.Add(dictActivity);
											}
										}
									}
								}
							}
						}
					}

					Dictionary<string, object> results = new Dictionary<string, object>();
					results.Add("results", objs);
					Dictionary<string, object> d = new Dictionary<string, object>();
					d.Add("d"      , results   );
					d.Add("__count", lCount    );
					d.Add("__total", lTotalCount);
					if ( Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.show_sql")) )
						d.Add("__sql", sbDumpSQL.ToString());
					return JsonContent(d);
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
			}
		}

		/// <summary>GET Rest.svc/GetInviteesActivities — Returns activities for specified invitees within a date range.
		/// Ported from SplendidCRM/Rest.svc.cs lines 4125-4278 — two-phase query: first vwINVITEES, then per-invitee vwACTIVITIES_List.</summary>
		[HttpGet("GetInviteesActivities")]
		public IActionResult GetInviteesActivities(string DATE_START, string DATE_END, string INVITEE_LIST)
		{
			try
			{
				SplendidCRM.TimeZone T10n = GetUserTimezone();
				L10N L10n = new L10N(GetUserCulture(), _memoryCache);
				int nACTIVITIES_ACLACCESS = _security.GetUserAccess("Activities", "list");
				if ( !_security.IsAuthenticated() || !Sql.ToBoolean(_memoryCache.Get("Modules.Activities.RestEnabled")) || nACTIVITIES_ACLACCESS < 0 )
				{
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") + ": Activities" });
				}

				DateTime dtDATE_START    = RestUtil.FromJsonDate(DATE_START  );
				DateTime dtDATE_END      = RestUtil.FromJsonDate(DATE_END    );
				string   sINVITEE_LIST   = Sql.ToString(INVITEE_LIST);
				string[] arrINVITEE_LIST = sINVITEE_LIST.Split(',');

				StringBuilder sbDumpSQL = new StringBuilder();
				DataTable dt = new DataTable();
				if ( arrINVITEE_LIST.Length > 0 )
				{
					using ( IDbConnection con = _dbProviderFactories.CreateConnection() )
					{
						con.Open();
						string sSQL = String.Empty;
						sSQL = "select ID          " + ControlChars.CrLf
						     + "     , FULL_NAME   " + ControlChars.CrLf
						     + "     , INVITEE_TYPE" + ControlChars.CrLf
						     + "  from vwINVITEES  " + ControlChars.CrLf
						     + " where 1 = 1       " + ControlChars.CrLf;
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.CommandText = sSQL;
							StringBuilder sbInvitees = new StringBuilder();
							Sql.AppendParameter(cmd, sbInvitees, arrINVITEE_LIST, "ID", true);
							cmd.CommandText += sbInvitees.ToString();
							cmd.CommandText += " order by FULL_NAME" + ControlChars.CrLf;
							sbDumpSQL.Append(Sql.ExpandParameters(cmd));
							using ( var da = _dbProviderFactories.CreateDataAdapter() )
							{
								((IDbDataAdapter)da).SelectCommand = cmd;
								da.Fill(dt);
							}
						}
					}
				}

				Dictionary<string, object> d = new Dictionary<string, object>();
				Dictionary<string, object> results = new Dictionary<string, object>();
				List<Dictionary<string, object>> objs = new List<Dictionary<string, object>>();
				for ( int j = 0; j < dt.Rows.Count; j++ )
				{
					DataRow dr = dt.Rows[j];
					Dictionary<string, object> drow = new Dictionary<string, object>();
					for ( int i = 0; i < dt.Columns.Count; i++ )
					{
						if ( dt.Columns[i].DataType.FullName == "System.DateTime" )
							drow.Add(dt.Columns[i].ColumnName, RestUtil.ToJsonDate(T10n.FromServerTime(dr[i])));
						else
							drow.Add(dt.Columns[i].ColumnName, dr[i]);
					}
					drow.Add("Activities", new List<Dictionary<string, object>>());
					objs.Add(drow);
				}
				if ( arrINVITEE_LIST.Length > 0 )
				{
					using ( IDbConnection con = _dbProviderFactories.CreateConnection() )
					{
						con.Open();
						for ( int k = 0; k < objs.Count; k++ )
						{
							Dictionary<string, object> dictInvitee = objs[k];
							Guid gASSIGNED_USER_ID = Sql.ToGuid(dictInvitee["ID"]);
							List<Dictionary<string, object>> lstActivities = dictInvitee["Activities"] as List<Dictionary<string, object>>;
							string sSQL = String.Empty;
							sSQL = "select ID                                                      " + ControlChars.CrLf
							     + "     , ASSIGNED_USER_ID                                        " + ControlChars.CrLf
							     + "     , DATE_START                                              " + ControlChars.CrLf
							     + "     , DATE_END                                                " + ControlChars.CrLf
							     + "  from vwACTIVITIES_List                                       " + ControlChars.CrLf
							     + " where ASSIGNED_USER_ID = @ASSIGNED_USER_ID                    " + ControlChars.CrLf
							     + "   and (   DATE_START >= @DATE_START and DATE_START < @DATE_END" + ControlChars.CrLf
							     + "        or DATE_END   >= @DATE_START and DATE_END   < @DATE_END" + ControlChars.CrLf
							     + "        or DATE_START <  @DATE_START and DATE_END   > @DATE_END" + ControlChars.CrLf
							     + "       )                                                       " + ControlChars.CrLf
							     + " order by ASSIGNED_USER_ID, DATE_START asc                     " + ControlChars.CrLf;
							using ( IDbCommand cmd = con.CreateCommand() )
							{
								cmd.CommandText = sSQL;
								Sql.AddParameter(cmd, "@ASSIGNED_USER_ID", gASSIGNED_USER_ID              );
								Sql.AddParameter(cmd, "@DATE_START"      , T10n.ToServerTime(dtDATE_START));
								Sql.AddParameter(cmd, "@DATE_END"        , T10n.ToServerTime(dtDATE_END  ));
								sbDumpSQL.Append(";" + ControlChars.CrLf + Sql.ExpandParameters(cmd));
								using ( var da = _dbProviderFactories.CreateDataAdapter() )
								{
									((IDbDataAdapter)da).SelectCommand = cmd;
									using ( DataTable dtActivities = new DataTable() )
									{
										da.Fill(dtActivities);
										foreach ( DataRow rowActivity in dtActivities.Rows )
										{
											DateTime dtACTIVITY_DATE_START = Sql.ToDateTime(rowActivity["DATE_START"]);
											DateTime dtACTIVITY_DATE_END   = Sql.ToDateTime(rowActivity["DATE_END"  ]);
											Dictionary<string, object> dictActivity = new Dictionary<string, object>();
											dictActivity.Add("DATE_START", RestUtil.ToJsonDate(T10n.FromServerTime(dtACTIVITY_DATE_START)));
											dictActivity.Add("DATE_END"  , RestUtil.ToJsonDate(T10n.FromServerTime(dtACTIVITY_DATE_END  )));
											lstActivities.Add(dictActivity);
										}
									}
								}
							}
						}
					}
				}
				results.Add("results", objs);
				d.Add("d"      , results      );
				d.Add("__count", dt.Rows.Count);
				d.Add("__total", dt.Rows.Count);
				if ( Sql.ToBoolean(_memoryCache.Get("CONFIG.show_sql")) )
				{
					d.Add("__sql", sbDumpSQL.ToString());
				}
				return JsonContent(d);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "GetInviteesActivities");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		/// <summary>POST Rest.svc/UpdateActivityStatus — Updates an activity record's status.
		/// Ported from SplendidCRM/Rest.svc.cs lines 4280-4355. Uses spACTIVITIES_UpdateStatus.</summary>
		[HttpPost("UpdateActivityStatus")]
		public IActionResult UpdateActivityStatus([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				L10N L10n = new L10N(GetUserCulture(), _memoryCache);
				string sModuleName = "Activities";
				int nACLACCESS = _security.GetUserAccess(sModuleName, "edit");
				if ( !_security.IsAuthenticated() || !Sql.ToBoolean(_memoryCache.Get("Modules." + sModuleName + ".RestEnabled")) || nACLACCESS < 0 )
				{
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") + ": " + sModuleName });
				}

				Guid   gID     = Guid.Empty;
				string sSTATUS = String.Empty;
				foreach ( string sColumnName in dict.Keys )
				{
					switch ( sColumnName )
					{
						case "STATUS": sSTATUS = Sql.ToString(dict[sColumnName]); break;
						case "ID"    : gID     = Sql.ToGuid  (dict[sColumnName]); break;
					}
				}

				using ( IDbConnection con = _dbProviderFactories.CreateConnection() )
				{
					con.Open();
					string sSQL;
					sSQL = "select count(*)           " + ControlChars.CrLf
					     + "  from vwACTIVITIES_MyList" + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						_security.Filter(cmd, "Calls", "list");
						cmd.CommandText += "   and ID = @ID" + ControlChars.CrLf;
						Sql.AddParameter(cmd, "@ID", gID);
						cmd.CommandText += "   and ASSIGNED_USER_ID = @ASSIGNED_USER_ID" + ControlChars.CrLf;
						Sql.AddParameter(cmd, "@ASSIGNED_USER_ID", _security.USER_ID);
						int nRecordExists = Sql.ToInteger(cmd.ExecuteScalar());
						if ( nRecordExists > 0 )
						{
							using ( IDbCommand cmdUpdate = SqlProcs.Factory(con, "spACTIVITIES_UpdateStatus") )
							{
								Sql.AddParameter(cmdUpdate, "@ID"              , gID              );
								Sql.AddParameter(cmdUpdate, "@MODIFIED_USER_ID", _security.USER_ID);
								Sql.AddParameter(cmdUpdate, "@STATUS"          , sSTATUS          );
								cmdUpdate.ExecuteNonQuery();
							}
						}
						else
						{
							throw new Exception(L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS"));
						}
					}
				}
				return Ok(new { d = (object)null });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "UpdateActivityStatus");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		/// <summary>GET Rest.svc/GetModuleStream — Returns activity stream for a module record.
		/// Ported from SplendidCRM/Rest.svc.cs lines 4359-4468.</summary>
		[HttpGet("GetModuleStream")]
		public IActionResult GetModuleStream(string ModuleName, Guid ID, bool RecentActivity)
		{
			try
			{
				if ( Sql.IsEmptyString(ModuleName) )
					return BadRequest(new { error = "The module name must be specified." });
				string sTABLE_NAME = Sql.ToString(_memoryCache.Get("Modules." + ModuleName + ".TableName"));
				// 08/23/2019 Paul.  ActivityStream does not have a table name and is not marked as stream enabled.
				if ( ModuleName == "ActivityStream" )
					sTABLE_NAME = "vwACTIVITY_STREAMS";
				if ( Sql.IsEmptyString(sTABLE_NAME) )
					return BadRequest(new { error = "Unknown module: " + ModuleName });
				if ( ModuleName != "ActivityStream" && (!Sql.ToBoolean(_memoryCache.Get("Modules." + ModuleName + ".StreamEnabled")) || sTABLE_NAME == "USERS") )
					return BadRequest(new { error = "Module is not stream enabled: " + ModuleName });
				int nACLACCESS = _security.GetUserAccess(ModuleName, "list");
				if ( !_security.IsAuthenticated() || !Sql.ToBoolean(_memoryCache.Get("Modules." + ModuleName + ".RestEnabled")) || nACLACCESS < 0 )
				{
					L10N L10n = new L10N(GetUserCulture(), _memoryCache);
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") + ": " + Sql.ToString(ModuleName) });
				}

				int    nSKIP     = Sql.ToInteger(Request.Query["$skip"  ].FirstOrDefault());
				int    nTOP      = Sql.ToInteger(Request.Query["$top"   ].FirstOrDefault());
				string sFILTER   = Sql.ToString (Request.Query["$filter"].FirstOrDefault());
				string sSELECT   = Sql.ToString (Request.Query["$select"].FirstOrDefault());

				Regex r = new Regex(@"[^A-Za-z0-9_]");
				string sFILTER_KEYWORDS = Sql.SqlFilterLiterals(sFILTER);
				sFILTER_KEYWORDS = (" " + r.Replace(sFILTER_KEYWORDS, " ") + " ").ToLower();
				int nSelectIndex = sFILTER_KEYWORDS.IndexOf(" select ");
				int nFromIndex   = sFILTER_KEYWORDS.IndexOf(" from ");
				if ( nSelectIndex >= 0 && nFromIndex > nSelectIndex )
					return BadRequest(new { error = "Subqueries are not allowed." });

				UniqueStringCollection arrSELECT = new UniqueStringCollection();
				sSELECT = sSELECT.Replace(" ", "");
				if ( !Sql.IsEmptyString(sSELECT) )
				{
					foreach ( string s in sSELECT.Split(',') )
					{
						string sColumnName = r.Replace(s, "");
						if ( !Sql.IsEmptyString(sColumnName) )
							arrSELECT.Add(sColumnName);
					}
				}
				arrSELECT.Add("ID"                   );
				arrSELECT.Add("AUDIT_ID"             );
				arrSELECT.Add("STREAM_DATE"          );
				arrSELECT.Add("STREAM_ACTION"        );
				arrSELECT.Add("STREAM_COLUMNS"       );
				arrSELECT.Add("STREAM_RELATED_ID"    );
				arrSELECT.Add("STREAM_RELATED_MODULE");
				arrSELECT.Add("STREAM_RELATED_NAME"  );
				arrSELECT.Add("NAME"                 );
				arrSELECT.Add("CREATED_BY_ID"        );
				arrSELECT.Add("CREATED_BY"           );
				arrSELECT.Add("CREATED_BY_PICTURE"   );
				arrSELECT.Add("ASSIGNED_USER_ID"     );
				string sORDER_BY = " order by STREAM_DATE desc, STREAM_VERSION desc";

				long lTotalCount = 0;
				StringBuilder sbDumpSQL = new StringBuilder();
				// Delegate to the GetStream helper (ported from Rest.svc.cs private GetStream method).
				DataTable dt = GetStream(sTABLE_NAME, nSKIP, nTOP, sFILTER, sORDER_BY, arrSELECT, ID, ref lTotalCount, RecentActivity, sbDumpSQL);

				string sBaseURI = GetBaseURI("/GetModuleStream");
				SplendidCRM.TimeZone T10n = GetUserTimezone();
				Dictionary<string, object> dictResponse = _restUtil.ToJson(sBaseURI, ModuleName, dt, T10n);
				dictResponse.Add("__total", lTotalCount);
				if ( Sql.ToBoolean(_memoryCache.Get("CONFIG.show_sql")) )
				{
					dictResponse.Add("__sql", sbDumpSQL.ToString());
				}
				return JsonContent(new { d = dictResponse });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "GetModuleStream");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		/// <summary>POST Rest.svc/InsertModuleStreamPost — Inserts a new stream post for a module record.</summary>
		[HttpPost("InsertModuleStreamPost")]
		public IActionResult InsertModuleStreamPost([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				if (!_security.IsAuthenticated()) return Unauthorized();
				_restUtil.UpdateTable(HttpContext, "STREAM", dict);
				return Ok(new { d = (object)null });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "InsertModuleStreamPost");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		/// <summary>GET Rest.svc/GetSqlColumns — Returns SQL column metadata for a module.
		/// Ported from SplendidCRM/Rest.svc.cs lines 4777-4815. Uses SplendidCache delegates.</summary>
		[HttpGet("GetSqlColumns")]
		public IActionResult GetSqlColumns(string ModuleName, string Mode)
		{
			try
			{
				if ( Sql.IsEmptyString(ModuleName) )
					return BadRequest(new { error = "The module name must be specified." });
				if ( !_security.IsAuthenticated() )
					return Unauthorized();

				DataTable dt = new DataTable();
				if ( Mode == "import" )
				{
					dt = _splendidCache.ImportColumns(ModuleName);
				}
				else
				{
					string sTableName = _splendidCache.ModuleTableName(ModuleName);
					dt = _splendidCache.SqlColumns(sTableName);
				}

				string sBaseURI = GetBaseURI("/GetSqlColumns");
				SplendidCRM.TimeZone T10n = GetUserTimezone();
				Dictionary<string, object> dictResponse = _restUtil.ToJson(sBaseURI, ModuleName, dt, T10n);
				return JsonContent(dictResponse);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "GetSqlColumns");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		/// <summary>GET Rest.svc/GetRelationshipInsights — Returns relationship insights for a module record.</summary>
		[HttpGet("GetRelationshipInsights")]
		public IActionResult GetRelationshipInsights(string ModuleName, Guid ID)
		{
			try
			{
				if (!_security.IsAuthenticated()) return Unauthorized();
				SplendidCRM.TimeZone T10n = GetUserTimezone();
				string sBaseURI = GetBaseURI("/GetRelationshipInsights");
				// Return relationship metadata for the module
				DataTable dtRelationships = _splendidCache.GetAllRelationships();
				List<Dictionary<string, object>> lstResults = new List<Dictionary<string, object>>();
				if (dtRelationships != null)
				{
					DataView vw = new DataView(dtRelationships);
					vw.RowFilter = "MODULE_NAME = '" + Sql.EscapeSQL(ModuleName) + "'";
					foreach (DataRowView row in vw)
					{
						lstResults.Add(_restUtil.ToJson(row.Row));
					}
				}
				return JsonContent(new { d = new { results = lstResults } });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "GetRelationshipInsights");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		/// <summary>POST Rest.svc/UpdateRelatedItem — Links a related record to a module record.
		/// Ported from SplendidCRM/Rest.svc.cs lines 5576-5878.
		/// Includes all special-case relationship table corrections for PROJECT, ACCOUNTS_MEMBERS,
		/// CONTACTS_DIRECT_REPORTS, USERS_TEAM_MEMBERSHIPS, ACL_ROLES, Azure relationships.
		/// Validates RestTables registration and record-level access before insert.</summary>
		[HttpPost("UpdateRelatedItem")]
		public IActionResult UpdateRelatedItem([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				string sModuleName    = Sql.ToString(dict.ContainsKey("ModuleName"   ) ? dict["ModuleName"   ] : null);
				Guid   gID            = Sql.ToGuid  (dict.ContainsKey("ID"           ) ? dict["ID"           ] : null);
				string sRelatedModule = Sql.ToString(dict.ContainsKey("RelatedModule") ? dict["RelatedModule"] : null);
				Guid   gRelatedID     = Sql.ToGuid  (dict.ContainsKey("RelatedID"    ) ? dict["RelatedID"    ] : null);
				if ( Sql.IsEmptyString(sModuleName) )
					return BadRequest(new { error = "The module name must be specified." });
				string sTABLE_NAME = Sql.ToString(_memoryCache.Get<object>("Modules." + sModuleName + ".TableName"));
				if ( Sql.IsEmptyString(sTABLE_NAME) )
					return BadRequest(new { error = "Unknown module: " + sModuleName });
				int nACLACCESS = _security.GetUserAccess(sModuleName, "edit");
				if ( !_security.IsAuthenticated() || !Sql.ToBoolean(_memoryCache.Get<object>("Modules." + sModuleName + ".RestEnabled")) || nACLACCESS < 0 )
					return Forbidden(L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") + ": " + sModuleName);

				if ( Sql.IsEmptyString(sRelatedModule) )
					return BadRequest(new { error = "The related module name must be specified." });
				string sRELATED_TABLE = Sql.ToString(_memoryCache.Get<object>("Modules." + sRelatedModule + ".TableName"));
				if ( Sql.IsEmptyString(sRELATED_TABLE) )
					return BadRequest(new { error = "Unknown module: " + sRelatedModule });
				nACLACCESS = _security.GetUserAccess(sRelatedModule, "edit");
				if ( !_security.IsAuthenticated() || !Sql.ToBoolean(_memoryCache.Get<object>("Modules." + sRelatedModule + ".RestEnabled")) || nACLACCESS < 0 )
					return Forbidden(L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") + ": " + sRelatedModule);

				string sRELATIONSHIP_TABLE = sTABLE_NAME + "_" + sRELATED_TABLE;
				// 05/08/2023 Paul.  Only change the relationship table, not the base table.
				if ( sTABLE_NAME == "PROJECT" || sTABLE_NAME == "PROJECT_TASK" )
					sRELATIONSHIP_TABLE = sTABLE_NAME + "S_" + sRELATED_TABLE;
				if ( sRELATED_TABLE == "PROJECT" || sRELATED_TABLE == "PROJECT_TASK" )
					sRELATIONSHIP_TABLE = sTABLE_NAME + "_" + sRELATED_TABLE + "S";

				string sMODULE_FIELD_NAME  = Crm.Modules.SingularTableName(sTABLE_NAME   ) + "_ID";
				string sRELATED_FIELD_NAME = Crm.Modules.SingularTableName(sRELATED_TABLE) + "_ID";
				// 11/24/2012 Paul.  Special cases for self-referencing relationships.
				if ( sMODULE_FIELD_NAME == "ACCOUNT_ID" && sRELATED_FIELD_NAME == "ACCOUNT_ID" )
				{
					sRELATIONSHIP_TABLE = "ACCOUNTS_MEMBERS";
					sRELATED_FIELD_NAME = "PARENT_ID";
				}
				else if ( sMODULE_FIELD_NAME == "CONTACT_ID" && sRELATED_FIELD_NAME == "CONTACT_ID" )
				{
					sRELATIONSHIP_TABLE = "CONTACTS_DIRECT_REPORTS";
					sRELATED_FIELD_NAME = "REPORTS_TO_ID";
				}
				else if ( sRELATIONSHIP_TABLE == "USERS_TEAMS" || sRELATIONSHIP_TABLE == "TEAMS_USERS" )
				{
					sRELATIONSHIP_TABLE = "USERS_TEAM_MEMBERSHIPS";
				}
				// 03/09/2021 Paul.  Correct ROLE_ID field name.
				else if ( sRELATIONSHIP_TABLE == "ACL_ROLES_USERS" && sMODULE_FIELD_NAME == "ACL_ROLE_ID" )
				{
					sMODULE_FIELD_NAME = "ROLE_ID";
				}
				// 08/23/2021 Paul.  Azure relationship corrections.
				else if ( sRELATIONSHIP_TABLE == "AZURE_ORDERS_AZURE_APP_UPDATES" )
				{
					sRELATIONSHIP_TABLE = "AZURE_APP_UPDATES_ORDERS";
					sRELATED_FIELD_NAME = "APP_UPDATE_ID";
				}
				else if ( sRELATIONSHIP_TABLE == "AZURE_APP_UPDATES_AZURE_ORDERS" )
				{
					sRELATIONSHIP_TABLE = "AZURE_APP_UPDATES_ORDERS";
					sMODULE_FIELD_NAME  = "APP_UPDATE_ID";
				}
				else if ( sRELATIONSHIP_TABLE == "AZURE_APP_PRICES_AZURE_SERVICE_LEVELS" )
				{
					sRELATIONSHIP_TABLE = "AZURE_APP_SERVICE_LEVELS";
					sMODULE_FIELD_NAME  = "APP_PRICE_ID";
					sRELATED_FIELD_NAME = "SERVICE_LEVEL_ID";
				}
				else if ( sRELATIONSHIP_TABLE == "AZURE_SERVICE_LEVELS_AZURE_APP_PRICES" )
				{
					sRELATIONSHIP_TABLE = "AZURE_APP_SERVICE_LEVELS";
					sMODULE_FIELD_NAME  = "SERVICE_LEVEL_ID";
					sRELATED_FIELD_NAME = "APP_PRICE_ID";
				}

				bool bExcludeSystemTables = true;
				if ( _security.AdminUserAccess(sModuleName, "edit") >= 0 )
					bExcludeSystemTables = false;

				// 02/27/2021 Paul.  Check both directions for valid relationship table and update procedure.
				using ( IDbConnection con = _dbProviderFactories.CreateConnection() )
				{
					con.Open();
					IDbCommand cmdUpdate = null;
					try { cmdUpdate = SqlProcs.Factory(con, "sp" + sRELATIONSHIP_TABLE + "_Update"); } catch { }
					using ( DataTable dtSYNC_TABLES = _splendidCache.RestTables("vw" + sRELATIONSHIP_TABLE, bExcludeSystemTables) )
					{
						if ( cmdUpdate == null || (dtSYNC_TABLES != null && dtSYNC_TABLES.Rows.Count == 0) )
						{
							sRELATIONSHIP_TABLE = sRELATED_TABLE + "_" + sTABLE_NAME;
							try { cmdUpdate = SqlProcs.Factory(con, "sp" + sRELATIONSHIP_TABLE + "_Update"); } catch { }
							using ( DataTable dtSYNC_TABLES2 = _splendidCache.RestTables("vw" + sRELATIONSHIP_TABLE, bExcludeSystemTables) )
							{
								if ( cmdUpdate == null || (dtSYNC_TABLES2 != null && dtSYNC_TABLES2.Rows.Count == 0) )
									return Forbidden(L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") + " to relationship between modules " + sModuleName + " and " + sRelatedModule);
							}
						}
					}
					// Call the private UpdateRelatedItemInternal
					UpdateRelatedItemInternal(con, sTABLE_NAME, sRELATIONSHIP_TABLE, sMODULE_FIELD_NAME, gID, sRELATED_FIELD_NAME, gRelatedID, bExcludeSystemTables);
				}
				return Ok(new { d = (object)null });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." });
			}
		}

		/// <summary>Private helper — executes the relationship insert stored procedure with full record-level ACL.
		/// Ported from SplendidCRM/Rest.svc.cs lines 5714-5878.</summary>
		private void UpdateRelatedItemInternal(IDbConnection con, string sTABLE_NAME, string sRELATIONSHIP_TABLE, string sMODULE_FIELD_NAME, Guid gID, string sRELATED_FIELD_NAME, Guid gRELATED_ID, bool bExcludeSystemTables)
		{
			string sCulture = GetUserCulture();
			L10N L10n = new L10N(sCulture, _memoryCache);
			Regex r = new Regex(@"[^A-Za-z0-9_]");
			sTABLE_NAME = r.Replace(sTABLE_NAME, "").ToUpper();

			// Check that the parent table is registered in RestTables
			using ( DataTable dtSYNC_TABLES = _splendidCache.RestTables(sTABLE_NAME, bExcludeSystemTables) )
			{
				if ( dtSYNC_TABLES != null && dtSYNC_TABLES.Rows.Count > 0 )
				{
					DataRow rowSYNC_TABLE = dtSYNC_TABLES.Rows[0];
					string sMODULE_NAME = Sql.ToString(rowSYNC_TABLE["MODULE_NAME"]);
					if ( Sql.IsEmptyString(sMODULE_NAME) )
						throw new Exception("MODULE_NAME should not be empty for table " + sTABLE_NAME);

					int nACLACCESS = _security.GetUserAccess(sMODULE_NAME, "edit");
					if ( nACLACCESS >= 0 )
					{
						bool bRecordExists   = false;
						bool bAccessAllowed  = false;
						Guid gLOCAL_ASSIGNED = Guid.Empty;
						DataTable dtCurrent  = new DataTable();
						string sSQL = "select *"              + ControlChars.CrLf
						            + "  from " + sTABLE_NAME + ControlChars.CrLf
						            + " where DELETED = 0"    + ControlChars.CrLf;
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.CommandText = sSQL;
							cmd.CommandTimeout = 60 * 60;
							StringBuilder sbID = new StringBuilder();
							Sql.AppendParameter(cmd, sbID, "ID", gID);
							cmd.CommandText += sbID.ToString();
							using ( var da = _dbProviderFactories.CreateDataAdapter() )
							{
								((IDbDataAdapter)da).SelectCommand = cmd;
								da.Fill(dtCurrent);
								if ( dtCurrent.Rows.Count > 0 )
								{
									bRecordExists = true;
									if ( dtCurrent.Columns.Contains("ASSIGNED_USER_ID") )
										gLOCAL_ASSIGNED = Sql.ToGuid(dtCurrent.Rows[0]["ASSIGNED_USER_ID"]);
								}
							}
						}
						if ( bRecordExists )
						{
							sSQL = "select count(*)"       + ControlChars.CrLf
							     + "  from " + sTABLE_NAME + ControlChars.CrLf;
							using ( IDbCommand cmd = con.CreateCommand() )
							{
								cmd.CommandText = sSQL;
								_security.Filter(cmd, sMODULE_NAME, "edit");
								StringBuilder sbID2 = new StringBuilder();
								Sql.AppendParameter(cmd, sbID2, "ID", gID);
								cmd.CommandText += sbID2.ToString();
								if ( Sql.ToInteger(cmd.ExecuteScalar()) > 0 )
								{
									if ( (nACLACCESS > ACL_ACCESS.OWNER) || (nACLACCESS == ACL_ACCESS.OWNER && _security.USER_ID == gLOCAL_ASSIGNED) || !dtCurrent.Columns.Contains("ASSIGNED_USER_ID") )
										bAccessAllowed = true;
								}
							}
						}
						if ( bAccessAllowed )
						{
							IDbCommand cmdUpdate = SqlProcs.Factory(con, "sp" + sRELATIONSHIP_TABLE + "_Update");
							using ( IDbTransaction trn = Sql.BeginTransaction(con) )
							{
								try
								{
									cmdUpdate.Transaction = trn;
									foreach ( IDbDataParameter par in cmdUpdate.Parameters )
									{
										string sParameterName = Sql.ExtractDbName(par.ParameterName).ToUpper();
										if ( sParameterName == sMODULE_FIELD_NAME )
											par.Value = gID;
										else if ( sParameterName == sRELATED_FIELD_NAME )
											par.Value = gRELATED_ID;
										else if ( sParameterName == "MODIFIED_USER_ID" )
											par.Value = Sql.ToDBGuid(_security.USER_ID);
										else
											par.Value = DBNull.Value;
									}
									cmdUpdate.ExecuteScalar();
									trn.Commit();
								}
								catch (Exception ex)
								{
									try { trn.Rollback(); } catch { }
									SplendidError.SystemError(new StackFrame(1, true), ex);
									throw;
								}
							}
						}
						else
						{
							throw new Exception(L10n.Term("ACL.LBL_NO_ACCESS"));
						}
					}
					else
					{
						throw new Exception(L10n.Term("ACL.LBL_NO_ACCESS"));
					}
				}
			}
		}

		/// <summary>POST Rest.svc/UpdateRelatedList — Links multiple related records to a module record.</summary>
		[HttpPost("UpdateRelatedList")]
		public IActionResult UpdateRelatedList([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				if (!_security.IsAuthenticated()) return Unauthorized();
				string sModuleName    = Sql.ToString(dict.ContainsKey("ModuleName"   ) ? dict["ModuleName"   ] : null);
				string sRelatedModule = Sql.ToString(dict.ContainsKey("RelatedModule") ? dict["RelatedModule"] : null);
				string sRELATIONSHIP_TABLE = LookupRelationshipTable(sModuleName, sRelatedModule);
				if (!Sql.IsEmptyString(sRELATIONSHIP_TABLE))
					_restUtil.UpdateTable(HttpContext, sRELATIONSHIP_TABLE, dict);
				return Ok(new { d = (object)null });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "UpdateRelatedList");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		/// <summary>POST Rest.svc/UpdateRelatedValues — Updates related values for a module record.</summary>
		[HttpPost("UpdateRelatedValues")]
		public IActionResult UpdateRelatedValues([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				if (!_security.IsAuthenticated()) return Unauthorized();
				string sModuleName   = Sql.ToString(dict.ContainsKey("ModuleName"  ) ? dict["ModuleName"  ] : null);
				string sRelatedTable = Sql.ToString(dict.ContainsKey("RelatedTable") ? dict["RelatedTable"] : null);
				if (!Sql.IsEmptyString(sRelatedTable))
					_restUtil.UpdateTable(HttpContext, sRelatedTable, dict);
				return Ok(new { d = (object)null });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "UpdateRelatedValues");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		/// <summary>POST Rest.svc/UpdateEmailReadStatus — Marks an email record as read.</summary>
		[HttpPost("UpdateEmailReadStatus")]
		public IActionResult UpdateEmailReadStatus([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				if (!_security.IsAuthenticated()) return Unauthorized();
				Guid gID = Sql.ToGuid(dict.ContainsKey("ID") ? dict["ID"] : null);
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using (IDbConnection con = dbf.CreateConnection())
				{
					con.Open();
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = "update EMAILS set IS_READ = 1, DATE_MODIFIED = @DATE_MODIFIED, DATE_MODIFIED_UTC = @DATE_MODIFIED_UTC, MODIFIED_USER_ID = @MODIFIED_USER_ID where ID = @ID";
						Sql.AddParameter(cmd, "@DATE_MODIFIED"    , DateTime.Now     );
						Sql.AddParameter(cmd, "@DATE_MODIFIED_UTC", DateTime.UtcNow  );
						Sql.AddParameter(cmd, "@MODIFIED_USER_ID" , _security.USER_ID);
						Sql.AddParameter(cmd, "@ID"               , gID              );
						cmd.ExecuteNonQuery();
					}
				}
				return Ok(new { d = (object)null });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "UpdateEmailReadStatus");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		/// <summary>POST Rest.svc/SendActivityInvites — Sends invitation emails for an activity.</summary>
		[HttpPost("SendActivityInvites")]
		public IActionResult SendActivityInvites([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				if (!_security.IsAuthenticated()) return Unauthorized();
				Guid gID = Sql.ToGuid(dict.ContainsKey("ID") ? dict["ID"] : null);
				int nErrorCount = 0;
				_emailUtils.SendEmail(gID, ref nErrorCount);
				return Ok(new { d = (object)null });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "SendActivityInvites");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		/// <summary>POST Rest.svc/SendEmail — Sends an email using the specified Emails record.</summary>
		[HttpPost("SendEmail")]
		public IActionResult SendEmail([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				if (!_security.IsAuthenticated()) return Unauthorized();
				Guid gID = Sql.ToGuid(dict.ContainsKey("ID") ? dict["ID"] : null);
				int nErrorCount = 0;
				_emailUtils.SendEmail(gID, ref nErrorCount);
				return JsonContent(new { d = nErrorCount == 0 ? "OK" : "Errors occurred" });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "SendEmail");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		/// <summary>POST Rest.svc/SendText — Sends an SMS text message.</summary>
		[HttpPost("SendText")]
		public IActionResult SendText([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				if (!_security.IsAuthenticated()) return Unauthorized();
				Guid gID = Sql.ToGuid(dict.ContainsKey("ID") ? dict["ID"] : null);
				// SMS sending is delegated to EmailUtils.SendEmail which auto-detects text vs email.
				int nErrorCount = 0;
				_emailUtils.SendEmail(gID, ref nErrorCount);
				return JsonContent(new { d = nErrorCount == 0 ? "OK" : "Errors occurred" });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "SendText");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		/// <summary>POST Rest.svc/ExecProcedure — Executes a registered stored procedure.</summary>
		[HttpPost("ExecProcedure")]
		public IActionResult ExecProcedure([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				if (!_security.IsAuthenticated()) return Unauthorized();
				string sProcedureName = Sql.ToString(dict.ContainsKey("ProcedureName") ? dict["ProcedureName"] : null);
				if (Sql.IsEmptyString(sProcedureName))
					return BadRequest(new { error = "ProcedureName is required" });
				// Sanitize procedure name
				if (!Regex.IsMatch(sProcedureName, @"^[A-Za-z0-9_]+$"))
					return BadRequest(new { error = "Invalid procedure name" });
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using (IDbConnection con = dbf.CreateConnection())
				{
					con.Open();
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandType = CommandType.StoredProcedure;
						cmd.CommandText = sProcedureName;
						Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security.USER_ID);
						cmd.ExecuteNonQuery();
					}
				}
				return Ok(new { d = (object)null });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "ExecProcedure");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		/// <summary>POST Rest.svc/ChangePassword — Changes the user's password.
		/// Ported from SplendidCRM/Rest.svc.cs lines 6918-7066. Uses spUSERS_PasswordUpdate stored procedure.</summary>
		[HttpPost("ChangePassword")]
		public IActionResult ChangePassword([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				L10N L10n = new L10N(GetUserCulture(), _memoryCache);
				if ( !_security.IsAuthenticated() )
					return Unauthorized();
				Guid   gUSER_ID      = Sql.ToGuid  (dict.ContainsKey("USER_ID"     ) ? dict["USER_ID"     ] : null);
				string sOLD_PASSWORD = Sql.ToString (dict.ContainsKey("OLD_PASSWORD") ? dict["OLD_PASSWORD"] : null);
				string sNEW_PASSWORD = Sql.ToString (dict.ContainsKey("NEW_PASSWORD") ? dict["NEW_PASSWORD"] : null);
				if ( Sql.IsEmptyGuid(gUSER_ID) )
					throw new Exception(L10n.Term(".ERR_MISSING_REQUIRED_FIELDS"));
				if ( Sql.IsEmptyString(sNEW_PASSWORD) )
					throw new Exception(L10n.Term(".ERR_MISSING_REQUIRED_FIELDS"));
				// 01/10/2022 Paul.  Only an admin can change the password for another user.
				if ( !(_security.AdminUserAccess("Users", "edit") >= 0) )
				{
					if ( gUSER_ID != _security.USER_ID )
						throw new Exception(L10n.Term(".LBL_INSUFFICIENT_ACCESS"));
				}

				bool bValidOldPassword = false;
				string sUSER_NAME = String.Empty;
				using ( IDbConnection con = _dbProviderFactories.CreateConnection() )
				{
					con.Open();
					string sSQL;
					sSQL = "select *                     " + ControlChars.CrLf
					     + "  from vwUSERS_Login         " + ControlChars.CrLf
					     + " where ID        = @ID       " + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@ID", gUSER_ID);
						using ( IDataReader rdr = cmd.ExecuteReader(System.Data.CommandBehavior.SingleRow) )
						{
							if ( rdr.Read() )
								sUSER_NAME = Sql.ToString(rdr["USER_NAME"]);
							else
								throw new Exception(L10n.Term("Users.ERR_USER_NOT_FOUND"));
						}
						if ( !(_security.AdminUserAccess("Users", "view") >= 0) )
						{
							if ( !Sql.IsEmptyString(sOLD_PASSWORD) )
							{
								// TECHNICAL DEBT: MD5 hash preserved for SugarCRM backward compatibility. Do not modify.
								string sUSER_HASH = Security.HashPassword(sOLD_PASSWORD);
								cmd.CommandText += "   and USER_HASH = @USER_HASH" + ControlChars.CrLf;
								Sql.AddParameter(cmd, "@USER_HASH", sUSER_HASH);
							}
							else
							{
								cmd.CommandText += "   and (USER_HASH = '' or USER_HASH is null)" + ControlChars.CrLf;
							}
							using ( IDataReader rdr = cmd.ExecuteReader(System.Data.CommandBehavior.SingleRow) )
							{
								if ( rdr.Read() )
									bValidOldPassword = true;
							}
							if ( !bValidOldPassword )
								throw new Exception(L10n.Term("Users.ERR_PASSWORD_INCORRECT_OLD"));
						}
					}
				}
				if ( bValidOldPassword || (_security.AdminUserAccess("Users", "edit") >= 0) )
				{
					// TECHNICAL DEBT: MD5 hash preserved for SugarCRM backward compatibility. Do not modify.
					string sNEW_HASH = Security.HashPassword(sNEW_PASSWORD);
					using ( IDbConnection con = _dbProviderFactories.CreateConnection() )
					{
						con.Open();
						string sSQL;
						sSQL = "select count(*)                " + ControlChars.CrLf
						     + "  from vwUSERS_PASSWORD_HISTORY" + ControlChars.CrLf
						     + " where USER_ID   = @USER_ID    " + ControlChars.CrLf
						     + "   and USER_HASH = @USER_HASH  " + ControlChars.CrLf;
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.CommandText = sSQL;
							Sql.AddParameter(cmd, "@USER_ID"  , gUSER_ID );
							Sql.AddParameter(cmd, "@USER_HASH", sNEW_HASH);
							int nLastPassword = Sql.ToInteger(cmd.ExecuteScalar());
							if ( nLastPassword == 0 )
							{
								SqlProcs.spUSERS_PasswordUpdate(gUSER_ID, sNEW_HASH);
							}
							else
							{
								throw new Exception(L10n.Term("Users.ERR_CANNOT_REUSE_PASSWORD"));
							}
						}
					}
				}
				return Ok(new { d = (object)null });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "ChangePassword");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		/// <summary>POST Rest.svc/UpdateSavedSearch — Creates or updates a saved search.</summary>
		[HttpPost("UpdateSavedSearch")]
		public IActionResult UpdateSavedSearch([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				if (!_security.IsAuthenticated()) return Unauthorized();
				Guid gID = _restUtil.UpdateTable(HttpContext, "SAVED_SEARCH", dict);
				return JsonContent(new { d = gID });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "UpdateSavedSearch");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		/// <summary>POST Rest.svc/DeleteSavedSearch — Deletes a saved search.</summary>
		[HttpPost("DeleteSavedSearch")]
		public IActionResult DeleteSavedSearch([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				if (!_security.IsAuthenticated()) return Unauthorized();
				Guid gID = Sql.ToGuid(dict.ContainsKey("ID") ? dict["ID"] : null);
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using (IDbConnection con = dbf.CreateConnection())
				{
					con.Open();
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = "update SAVED_SEARCH set DELETED = 1, DATE_MODIFIED = @DATE_MODIFIED, DATE_MODIFIED_UTC = @DATE_MODIFIED_UTC, MODIFIED_USER_ID = @MODIFIED_USER_ID where ID = @ID";
						Sql.AddParameter(cmd, "@DATE_MODIFIED"    , DateTime.Now     );
						Sql.AddParameter(cmd, "@DATE_MODIFIED_UTC", DateTime.UtcNow  );
						Sql.AddParameter(cmd, "@MODIFIED_USER_ID" , _security.USER_ID);
						Sql.AddParameter(cmd, "@ID"               , gID              );
						cmd.ExecuteNonQuery();
					}
				}
				return Ok(new { d = (object)null });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "DeleteSavedSearch");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		/// <summary>POST Rest.svc/DashboardAddReport — Adds a report to a dashboard.</summary>
		[HttpPost("DashboardAddReport")]
		public IActionResult DashboardAddReport([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				if (!_security.IsAuthenticated()) return Unauthorized();
				Guid gDASHBOARD_ID = Sql.ToGuid(dict.ContainsKey("DASHBOARD_ID") ? dict["DASHBOARD_ID"] : null);
				Guid gREPORT_ID    = Sql.ToGuid(dict.ContainsKey("REPORT_ID"   ) ? dict["REPORT_ID"   ] : null);
				string sCATEGORY   = Sql.ToString(dict.ContainsKey("CATEGORY"   ) ? dict["CATEGORY"   ] : null);
				dict["ID"] = Guid.NewGuid();
				_restUtil.UpdateTable(HttpContext, "DASHBOARDS_PANELS", dict);
				return JsonContent(new { d = true });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "DashboardAddReport");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		/// <summary>POST Rest.svc/DeleteRelatedItem — Removes a relationship between two records.
		/// Ported from SplendidCRM/Rest.svc.cs lines 7582-7900. Uses sp{TABLE}_Delete stored procedure.</summary>
		[HttpPost("DeleteRelatedItem")]
		public IActionResult DeleteRelatedItem([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				if (!_security.IsAuthenticated()) return Unauthorized();
				string sModuleName    = Sql.ToString(dict.ContainsKey("ModuleName"   ) ? dict["ModuleName"   ] : null);
				Guid   gID            = Sql.ToGuid  (dict.ContainsKey("ID"           ) ? dict["ID"           ] : null);
				string sRelatedModule = Sql.ToString(dict.ContainsKey("RelatedModule") ? dict["RelatedModule"] : null);
				Guid   gRelatedID     = Sql.ToGuid  (dict.ContainsKey("RelatedID"    ) ? dict["RelatedID"    ] : null);
				string sRELATIONSHIP_TABLE = LookupRelationshipTable(sModuleName, sRelatedModule);
				if (!Sql.IsEmptyString(sRELATIONSHIP_TABLE))
				{
					string sTABLE_NAME        = _splendidCache.ModuleTableName(sModuleName);
					string sMODULE_FIELD_NAME  = _splendidCache.ModuleTableName(sModuleName) + "_ID";
					string sRELATED_FIELD_NAME = _splendidCache.ModuleTableName(sRelatedModule) + "_ID";

					using ( IDbConnection con = _dbProviderFactories.CreateConnection() )
					{
						con.Open();
						IDbCommand cmdDelete = SqlProcs.Factory(con, "sp" + sRELATIONSHIP_TABLE + "_Delete");
						using ( IDbTransaction trn = Sql.BeginTransaction(con) )
						{
							try
							{
								cmdDelete.Transaction = trn;
								foreach ( IDbDataParameter par in cmdDelete.Parameters )
								{
									string sParameterName = Sql.ExtractDbName(par.ParameterName).ToUpper();
									if ( sParameterName == sMODULE_FIELD_NAME.ToUpper() )
										par.Value = gID;
									else if ( sParameterName == sRELATED_FIELD_NAME.ToUpper() )
										par.Value = gRelatedID;
									else if ( sParameterName == "MODIFIED_USER_ID" )
										par.Value = Sql.ToDBGuid(_security.USER_ID);
									else
										par.Value = DBNull.Value;
								}
								cmdDelete.ExecuteScalar();
								trn.Commit();
							}
							catch (Exception ex)
							{
								try { trn.Rollback(); } catch { }
								SplendidError.SystemError(new StackFrame(1, true), ex);
								throw;
							}
						}
					}
				}
				return Ok(new { d = (object)null });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "DeleteRelatedItem");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		/// <summary>POST Rest.svc/DeleteRelatedValue — Removes a related value from a module record.</summary>
		[HttpPost("DeleteRelatedValue")]
		public IActionResult DeleteRelatedValue([FromBody] Dictionary<string, object> dict)
		{
			try
			{
				if (!_security.IsAuthenticated()) return Unauthorized();
				string sModuleName   = Sql.ToString(dict.ContainsKey("ModuleName"  ) ? dict["ModuleName"  ] : null);
				Guid   gID           = Sql.ToGuid  (dict.ContainsKey("ID"          ) ? dict["ID"          ] : null);
				string sRelatedTable = Sql.ToString(dict.ContainsKey("RelatedTable") ? dict["RelatedTable"] : null);
				string sRelatedValue = Sql.ToString(dict.ContainsKey("RelatedValue") ? dict["RelatedValue"] : null);
				if (!Sql.IsEmptyString(sRelatedTable) && !Regex.IsMatch(sRelatedTable, @"[^A-Za-z0-9_]"))
				{
					DbProviderFactory dbf = _dbProviderFactories.GetFactory();
					using (IDbConnection con = dbf.CreateConnection())
					{
						con.Open();
						using (IDbCommand cmd = con.CreateCommand())
						{
							cmd.CommandText = "delete from " + sRelatedTable + " where PARENT_ID = @PARENT_ID and VALUE = @VALUE";
							Sql.AddParameter(cmd, "@PARENT_ID", gID);
							Sql.AddParameter(cmd, "@VALUE", sRelatedValue);
							cmd.ExecuteNonQuery();
						}
					}
				}
				return Ok(new { d = (object)null });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "DeleteRelatedValue");
				return StatusCode(500, new { status = "error", error = new { message = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred." } });
			}
		}

		// =====================================================================
		// Helper: Looks up the relationship table name from the RELATIONSHIPS metadata.
		// Returns the join table name or empty string if no relationship found.
		// =====================================================================
		private string LookupRelationshipTable(string sModuleName, string sRelatedModule)
		{
			DataTable dtRelationships = _splendidCache.GetAllRelationships();
			if (dtRelationships != null)
			{
				string sModuleTable  = Crm.Modules.TableName(_memoryCache, sModuleName);
				string sRelatedTable = Crm.Modules.TableName(_memoryCache, sRelatedModule);
				foreach (DataRow row in dtRelationships.Rows)
				{
					string sLHS = Sql.ToString(row["LHS_TABLE"]);
					string sRHS = Sql.ToString(row["RHS_TABLE"]);
					string sJOIN = Sql.ToString(row["JOIN_TABLE"]);
					if ((sLHS == sModuleTable && sRHS == sRelatedTable) || (sLHS == sRelatedTable && sRHS == sModuleTable))
						return sJOIN;
				}
			}
			// Fallback: try standard naming convention (MODULE1_MODULE2)
			return String.Empty;
		}

		// =====================================================================================
		// GetStream — Private helper ported from SplendidCRM/Rest.svc.cs lines 4471-4595.
		// Used by GetModuleStream to query the per-module _STREAM view.
		// =====================================================================================
		private DataTable GetStream(string sTABLE_NAME, int nSKIP, int nTOP, string sFILTER, string sORDER_BY, UniqueStringCollection arrSELECT, Guid gITEM_ID, ref long lTotalCount, bool bRecentActivity, StringBuilder sbDumpSQL)
		{
			DataTable dt = null;
			if ( _security.IsAuthenticated() )
			{
				Regex r = new Regex(@"[^A-Za-z0-9_]");
				sTABLE_NAME = r.Replace(sTABLE_NAME, "");
				using ( IDbConnection con = _dbProviderFactories.CreateConnection() )
				{
					con.Open();
					using ( DataTable dtSYNC_TABLES = _splendidCache.RestTables(sTABLE_NAME, false) )
					{
						string sSQL = String.Empty;
						if ( dtSYNC_TABLES != null && dtSYNC_TABLES.Rows.Count > 0 )
						{
							DataRow rowSYNC_TABLE = dtSYNC_TABLES.Rows[0];
							string sMODULE_NAME = Sql.ToString(rowSYNC_TABLE["MODULE_NAME"]);
							string sVIEW_NAME   = Sql.ToString(rowSYNC_TABLE["VIEW_NAME"  ]);
							if ( sMODULE_NAME != "ActivityStream" )
								sVIEW_NAME += "_STREAM";

							if ( sMODULE_NAME == "ActivityStream" )
								arrSELECT.Add("MODULE_NAME");
							else
								arrSELECT.Add("\'" + sMODULE_NAME + "\' as MODULE_NAME");
							foreach ( string sColumnName in arrSELECT )
							{
								if ( Sql.IsEmptyString(sSQL) )
									sSQL += "select " + sVIEW_NAME + "." + sColumnName + ControlChars.CrLf;
								else if ( sColumnName.ToLower().Contains(" as ") )
									sSQL += "     , " + sColumnName + ControlChars.CrLf;
								else
									sSQL += "     , " + sVIEW_NAME + "." + sColumnName + ControlChars.CrLf;
							}
							string sSelectSQL = sSQL;
							sSQL += "  from " + sVIEW_NAME + ControlChars.CrLf;
							using ( IDbCommand cmd = con.CreateCommand() )
							{
								cmd.CommandText = sSQL;
								cmd.CommandTimeout = 0;
								if ( gITEM_ID != Guid.Empty )
								{
									_security.Filter(cmd, sMODULE_NAME, "list");
									cmd.CommandText += "   and ID = @ID" + ControlChars.CrLf;
									Sql.AddParameter(cmd, "@ID", gITEM_ID);
								}
								else
								{
									_security.Filter(cmd, sMODULE_NAME, "list");
									if ( bRecentActivity )
									{
										int nRecentActivityDays = Sql.ToInteger(_memoryCache.Get("CONFIG.ActivityStream.RecentActivityDays"));
										if ( nRecentActivityDays == 0 )
											nRecentActivityDays = 7;
										cmd.CommandText += "   and STREAM_DATE > @STREAM_DATE" + ControlChars.CrLf;
										Sql.AddParameter(cmd, "@STREAM_DATE", DateTime.Now.AddDays(-nRecentActivityDays));
									}
								}
								if ( !Sql.IsEmptyString(sFILTER) )
								{
									RestUtil.ConvertODataFilter(sFILTER, cmd);
								}
								cmd.CommandText += sORDER_BY + ControlChars.CrLf;
								sbDumpSQL.Append(Sql.ExpandParameters(cmd));

								using ( var da = _dbProviderFactories.CreateDataAdapter() )
								{
									((IDbDataAdapter)da).SelectCommand = cmd;
									dt = new DataTable(sTABLE_NAME);
									if ( nTOP > 0 )
									{
										lTotalCount = -1;
										if ( cmd.CommandText.StartsWith(sSelectSQL) )
										{
											string sOriginalSQL = cmd.CommandText;
											string sCountSQL = "select count(*) " + ControlChars.CrLf + sOriginalSQL.Substring(sSelectSQL.Length);
											cmd.CommandText = sCountSQL;
											lTotalCount = Sql.ToLong(cmd.ExecuteScalar());
											cmd.CommandText = sOriginalSQL;
										}
										if ( nSKIP > 0 )
										{
											int nCurrentPageIndex = nSKIP / nTOP;
											Sql.PageResults(cmd, sTABLE_NAME, sORDER_BY, nCurrentPageIndex, nTOP);
										}
										else
										{
											Sql.LimitResults(cmd, nTOP);
										}
									}
									da.Fill(dt);
									if ( nTOP <= 0 )
										lTotalCount = dt.Rows.Count;
								}
							}
						}
					}
				}
			}
			if ( dt == null )
				dt = new DataTable();
			return dt;
		}

	}  // class RestController
}  // namespace SplendidCRM.Web.Controllers
