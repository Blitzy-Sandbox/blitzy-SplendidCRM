// Copyright (C) 2005-2024 SplendidCRM Software, Inc.
// MIT License — .NET 10 migration from WCF Administration/Rest.svc.cs (6,473 lines, 65 operations)
// MIGRATION: WCF [ServiceContract]/[WebInvoke] → ASP.NET Core [ApiController]/[HttpGet]/[HttpPost]
// MIGRATION: HttpContext.Current       → IHttpContextAccessor
// MIGRATION: Application[]            → IMemoryCache
// MIGRATION: HttpRuntime.Cache        → IMemoryCache
// MIGRATION: Session[]                → ISession via IHttpContextAccessor
// MIGRATION: System.Data.SqlClient    → Microsoft.Data.SqlClient (via DbProviderFactories DI)
// MIGRATION: JavaScriptSerializer     → System.Text.Json / Newtonsoft.Json
// MIGRATION: WebOperationContext      → HttpContext.Response.Headers
// MIGRATION: DbProviderFactories      → injected instance (was static)
// MIGRATION: Security.*               → injected _security instance
// ROUTE:     /Administration/Rest.svc/{Operation} preserved for SPA backward compatibility
// Disable nullable reference type warnings — legacy .NET Framework code migration; not all
// reference types are annotated yet (matching RestController.cs pattern in this project).
#nullable enable

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SplendidCRM.Web.Controllers
{
	// =========================================================================
	// AdminRestController — ASP.NET Core Web API replacement for
	//   SplendidCRM/Administration/Rest.svc.cs (WCF, 6,473 lines, 65 operations)
	//
	// Route base: /Administration/Rest.svc
	// All 65 WCF [WebInvoke] operations are mapped to [HttpGet] or [HttpPost]
	// preserving exact route paths and HTTP methods per AAP Section 0.7.3.
	// =========================================================================
	[ApiController]
	[Authorize]
	[Route("Administration/Rest.svc")]
	public class AdminRestController : ControllerBase
	{
		// ------------------------------------------------------------------
		// DI fields — replacing all static HttpContext.Current/Application/Session access
		// ------------------------------------------------------------------
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly IMemoryCache         _memoryCache;
		private readonly Security             _security;
		private readonly SplendidCache        _splendidCache;
		private readonly SplendidInit         _splendidInit;
		private readonly RestUtil             _restUtil;
		private readonly DbProviderFactories  _dbProviderFactories;
		private readonly SplendidDynamic      _splendidDynamic;
		private readonly SplendidExport       _splendidExport;
		private readonly SplendidImport       _splendidImport;
		private readonly IConfiguration       _configuration;
		private readonly IWebHostEnvironment  _webHostEnvironment;
		private readonly ILogger<AdminRestController> _logger;

		public AdminRestController(
			IHttpContextAccessor httpContextAccessor,
			IMemoryCache         memoryCache,
			Security             security,
			SplendidCache        splendidCache,
			SplendidInit         splendidInit,
			RestUtil             restUtil,
			DbProviderFactories  dbProviderFactories,
			SplendidDynamic      splendidDynamic,
			SplendidExport       splendidExport,
			SplendidImport       splendidImport,
			IConfiguration       configuration,
			IWebHostEnvironment  webHostEnvironment,
			ILogger<AdminRestController> logger)
		{
			_httpContextAccessor = httpContextAccessor;
			_memoryCache         = memoryCache;
			_security            = security;
			_splendidCache       = splendidCache;
			_splendidInit        = splendidInit;
			_restUtil            = restUtil;
			_dbProviderFactories = dbProviderFactories;
			_splendidDynamic     = splendidDynamic;
			_splendidExport      = splendidExport;
			_splendidImport      = splendidImport;
			_configuration       = configuration;
			_webHostEnvironment  = webHostEnvironment;
			_logger              = logger;
		}

		// ==================================================================
		// Inner DTO classes — preserved from source lines 48-103
		// ==================================================================

		/// <summary>Describes a single layout view entry in the admin layout tree.</summary>
		public class ViewNode
		{
			public string ViewName   ;
			public string DisplayName;
			public string LayoutType ;
		}

		/// <summary>Represents a module node in the admin layout tree with all view lists.</summary>
		public class ModuleNode
		{
			public string         ModuleName      ;
			public string         DisplayName     ;
			public bool           IsAdmin         ;
			public List<ViewNode> EditViews        = new List<ViewNode>();
			public List<ViewNode> Search           = new List<ViewNode>();
			public List<ViewNode> DetailViews      = new List<ViewNode>();
			public List<ViewNode> ListViews        = new List<ViewNode>();
			public List<ViewNode> SubPanels        = new List<ViewNode>();
			public List<ViewNode> Relationships    = new List<ViewNode>();
			public List<ViewNode> Terminology      = new List<ViewNode>();
			public List<ViewNode> TerminologyLists = new List<ViewNode>();
		}

		/// <summary>Describes a single field in an admin layout with all metadata columns.</summary>
		public class LayoutField
		{
			public string ColumnName          ;
			public string ColumnType          ;
			public string CsType              ;
			public int    length              ;
			public string FIELD_TYPE          ;
			public string DATA_LABEL          ;
			public string DATA_FIELD          ;
			public string DISPLAY_FIELD       ;
			public string MODULE_TYPE         ;
			public string LIST_NAME           ;
			public string DATA_FORMAT         ;
			public int    FORMAT_MAX_LENGTH   ;
			public string URL_FIELD           ;
			public string URL_FORMAT          ;
			public string COLUMN_TYPE         ;
			public string HEADER_TEXT         ;
			public string SORT_EXPRESSION     ;
			public string URL_ASSIGNED_FIELD  ;
		}

		/// <summary>Tracks the progress of an async view recompile operation (source lines 1126-1138).</summary>
		public class RecompileStatus
		{
			public string StartDate       ;
			public bool   Restart         ;
			public int    CurrentPass     ;
			public int    TotalPasses     ;
			public int    CurrentView     ;
			public int    TotalViews      ;
			public string CurrentViewName ;
			public int    ElapseSeconds   ;
			public int    ViewsPerSecond  ;
			public int    RemainingSeconds;
		}

		/// <summary>Represents a module entry in the admin panel menu (source lines 2503-2573).</summary>
		private class AdminModule
		{
			public string MODULE_NAME ;
			public string DISPLAY_NAME;
			public string DESCRIPTION ;
			public string EDIT_LABEL  ;
			public bool   MENU_ENABLED;
			public int    TAB_ORDER   ;
			public string ADMIN_ROUTE ;
			public string ICON_NAME   ;

			// Constructor with tab-order support (for modules with list views)
			public AdminModule(L10N L10n, string sMODULE_NAME, string sDISPLAY_NAME, string sDESCRIPTION, int nDEFAULT_TAB_ORDER, Dictionary<string, int> dictModuleTabOrder, string sEDIT_LABEL, string sICON_NAME)
			{
				MODULE_NAME = sMODULE_NAME;
				if (!Sql.IsEmptyString(MODULE_NAME))
				{
					DISPLAY_NAME = !Sql.IsEmptyString(sDISPLAY_NAME) ? sDISPLAY_NAME : sMODULE_NAME + ".LBL_MANAGE_" + sMODULE_NAME.ToUpper() + "_TITLE";
					DESCRIPTION  = !Sql.IsEmptyString(sDESCRIPTION ) ? sDESCRIPTION  : sMODULE_NAME + ".LBL_MANAGE_" + sMODULE_NAME.ToUpper();
					EDIT_LABEL   = !Sql.IsEmptyString(sEDIT_LABEL  ) ? sEDIT_LABEL   : sMODULE_NAME + ".LBL_NEW_FORM_TITLE";
					MENU_ENABLED = true;
					if (dictModuleTabOrder.ContainsKey(sMODULE_NAME)) TAB_ORDER = dictModuleTabOrder[sMODULE_NAME];
					if (TAB_ORDER == 0) TAB_ORDER = nDEFAULT_TAB_ORDER;
					ICON_NAME = sICON_NAME;
				}
			}

			// Constructor with tab-order, auto icon name
			public AdminModule(L10N L10n, string sMODULE_NAME, string sDISPLAY_NAME, string sDESCRIPTION, int nDEFAULT_TAB_ORDER, Dictionary<string, int> dictModuleTabOrder, string sEDIT_LABEL)
			{
				MODULE_NAME = sMODULE_NAME;
				if (!Sql.IsEmptyString(MODULE_NAME))
				{
					DISPLAY_NAME = !Sql.IsEmptyString(sDISPLAY_NAME) ? sDISPLAY_NAME : sMODULE_NAME + ".LBL_MANAGE_" + sMODULE_NAME.ToUpper() + "_TITLE";
					DESCRIPTION  = !Sql.IsEmptyString(sDESCRIPTION ) ? sDESCRIPTION  : sMODULE_NAME + ".LBL_MANAGE_" + sMODULE_NAME.ToUpper();
					EDIT_LABEL   = !Sql.IsEmptyString(sEDIT_LABEL  ) ? sEDIT_LABEL   : sMODULE_NAME + ".LBL_NEW_FORM_TITLE";
					MENU_ENABLED = true;
					if (dictModuleTabOrder.ContainsKey(sMODULE_NAME)) TAB_ORDER = dictModuleTabOrder[sMODULE_NAME];
					if (TAB_ORDER == 0) TAB_ORDER = nDEFAULT_TAB_ORDER;
					ICON_NAME = sMODULE_NAME + ".gif";
				}
			}

			// Constructor with custom admin route and specific icon
			public AdminModule(L10N L10n, string sMODULE_NAME, string sDISPLAY_NAME, string sDESCRIPTION, string sADMIN_ROUTE, string sICON_NAME)
			{
				MODULE_NAME = sMODULE_NAME;
				if (!Sql.IsEmptyString(MODULE_NAME))
				{
					DISPLAY_NAME = !Sql.IsEmptyString(sDISPLAY_NAME) ? sDISPLAY_NAME : sMODULE_NAME + ".LBL_MANAGE_" + sMODULE_NAME.ToUpper() + "_TITLE";
					DESCRIPTION  = !Sql.IsEmptyString(sDESCRIPTION ) ? sDESCRIPTION  : sMODULE_NAME + ".LBL_MANAGE_" + sMODULE_NAME.ToUpper();
					MENU_ENABLED = false;
					ADMIN_ROUTE  = sADMIN_ROUTE;
					ICON_NAME    = sICON_NAME;
				}
			}

			// Constructor with custom admin route, auto icon
			public AdminModule(L10N L10n, string sMODULE_NAME, string sDISPLAY_NAME, string sDESCRIPTION, string sADMIN_ROUTE)
			{
				MODULE_NAME = sMODULE_NAME;
				if (!Sql.IsEmptyString(MODULE_NAME))
				{
					DISPLAY_NAME = !Sql.IsEmptyString(sDISPLAY_NAME) ? sDISPLAY_NAME : sMODULE_NAME + ".LBL_MANAGE_" + sMODULE_NAME.ToUpper() + "_TITLE";
					DESCRIPTION  = !Sql.IsEmptyString(sDESCRIPTION ) ? sDESCRIPTION  : sMODULE_NAME + ".LBL_MANAGE_" + sMODULE_NAME.ToUpper();
					MENU_ENABLED = false;
					ADMIN_ROUTE  = sADMIN_ROUTE;
					ICON_NAME    = sMODULE_NAME + ".gif";
				}
			}
		}

		// ==================================================================
		// Private utility helpers
		// ==================================================================

		/// <summary>Returns current user culture from session (replaces Session["USER_SETTINGS/CULTURE"]).</summary>
		private string GetUserCulture()
		{
			return Sql.ToString(_httpContextAccessor.HttpContext?.Session.GetString("USER_SETTINGS/CULTURE")) ?? "en-US";
		}

		/// <summary>Returns current user timezone (replaces Session["TIMEZONE"]).</summary>
		private SplendidCRM.TimeZone GetUserTimezone()
		{
			Guid gTIMEZONE = Sql.ToGuid(_httpContextAccessor.HttpContext?.Session.GetString("TIMEZONE"));
			return SplendidCRM.TimeZone.CreateTimeZone(gTIMEZONE);
		}

		/// <summary>Appends no-cache response headers (replaces WebOperationContext and ExpiresAbsolute).</summary>
		private void SetNoCacheHeaders()
		{
			Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
			Response.Headers.Append("Pragma"       , "no-cache");
			Response.Headers.Append("Expires"      , "0");
		}

		/// <summary>Returns a ContentResult with application/json content type.</summary>
		private ContentResult JsonContent(object obj)
		{
			string json = JsonConvert.SerializeObject(obj, Newtonsoft.Json.Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include });
			return Content(json, "application/json", Encoding.UTF8);
		}

		/// <summary>
		/// Returns a 500 error response that includes the correlation ID from the
		/// RequestLoggingMiddleware for distributed tracing.
		/// </summary>
		private IActionResult InternalError(Exception ex)
		{
			string correlationId = HttpContext.Response.Headers["X-Correlation-ID"].FirstOrDefault() ?? string.Empty;
			string errorMessage  = _webHostEnvironment.IsDevelopment() ? ex.Message : "An internal error occurred.";
			return StatusCode(500, new { error = errorMessage, correlationId });
		}

		/// <summary>Reads the entire POST request body as a string (replaces WCF Stream input parameter).</summary>
		private async System.Threading.Tasks.Task<string> ReadRequestBodyAsync()
		{
			using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
			return await reader.ReadToEndAsync();
		}

		/// <summary>Deserializes a JSON body into a Dictionary (replaces JavaScriptSerializer.Deserialize).</summary>
		private Dictionary<string, object> DeserializeBody(string body)
		{
			if (Sql.IsEmptyString(body)) return new Dictionary<string, object>();
			try
			{
				return JsonConvert.DeserializeObject<Dictionary<string, object>>(body) ?? new Dictionary<string, object>();
			}
			catch
			{
				return new Dictionary<string, object>();
			}
		}

		/// <summary>Gets admin-accessible module list (replaces RestUtil.AdminAccessibleModules()).</summary>
		private List<string> GetAdminModuleList()
		{
			return _restUtil.AdminAccessibleModules();
		}

		/// <summary>Returns DataRow value as string, handling DBNull (safe column access).</summary>
		private static string RowString(DataRow row, string col)
		{
			return row.Table.Columns.Contains(col) ? Sql.ToString(row[col]) : String.Empty;
		}

		/// <summary>Returns DataRow boolean value safely.</summary>
		private static bool RowBool(DataRow row, string col)
		{
			return row.Table.Columns.Contains(col) ? Sql.ToBoolean(row[col]) : false;
		}

		// ==================================================================
		// #region Get Operations
		// ==================================================================

		/// <summary>
		/// GET Administration/Rest.svc/GetAdminLayoutModules
		/// Returns module tree with all layout views (EditViews, DetailViews, ListViews, etc.)
		/// for admin layout editing. Source: lines 108-332.
		/// </summary>
		[HttpGet("GetAdminLayoutModules")]
		public IActionResult GetAdminLayoutModules()
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				// Ported from SplendidCRM/Administration/Rest.svc.cs lines 108-332.
				// Uses a single UNION ALL query, Global node, L10N localization, and vwTERMINOLOGY_PickList.
				List<ModuleNode> lstModules = new List<ModuleNode>();
				Dictionary<string, ModuleNode> dict = new Dictionary<string, ModuleNode>();

				using ( IDbConnection con = _dbProviderFactories.CreateConnection() )
				{
					con.Open();
					string sSQL;

					// 9g: Languages with DISPLAY_NAME and exclusion filter
					DataTable dtLANGUAGES = new DataTable();
					sSQL = "select NAME, DISPLAY_NAME               " + ControlChars.CrLf
					     + "  from vwLANGUAGES                      " + ControlChars.CrLf
					     + " where ACTIVE = 1                       " + ControlChars.CrLf
					     + "   and NAME not in ('en-AU','en-GB','en-CA')" + ControlChars.CrLf
					     + " order by NAME                          " + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						using ( var da = _dbProviderFactories.CreateDataAdapter() )
						{
							((IDbDataAdapter)da).SelectCommand = cmd;
							da.Fill(dtLANGUAGES);
						}
					}

					// 9d: Modules dictionary from vwMODULES_Edit (MODULE_NAME, MODULE_ENABLED only)
					Dictionary<string, bool> dictAllModules = new Dictionary<string, bool>();
					sSQL = "select MODULE_NAME                      " + ControlChars.CrLf
					     + "     , MODULE_ENABLED                   " + ControlChars.CrLf
					     + "  from vwMODULES_Edit                   " + ControlChars.CrLf
					     + " order by MODULE_NAME                   " + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						using ( var da = _dbProviderFactories.CreateDataAdapter() )
						{
							((IDbDataAdapter)da).SelectCommand = cmd;
							using ( DataTable dt = new DataTable() )
							{
								da.Fill(dt);
								foreach ( DataRow row in dt.Rows )
								{
									string sMODULE_NAME    = Sql.ToString (row["MODULE_NAME"   ]);
									bool   bMODULE_ENABLED = Sql.ToBoolean(row["MODULE_ENABLED"]);
									if ( !dictAllModules.ContainsKey(sMODULE_NAME) )
										dictAllModules[sMODULE_NAME] = bMODULE_ENABLED;
								}
							}
						}
					}

					// 9h: Global node — first entry (ModuleName = String.Empty)
					ViewNode view = null;
					ModuleNode dictMODULE = new ModuleNode();
					lstModules.Add(dictMODULE);
					dictMODULE.ModuleName  = String.Empty;
					dictMODULE.IsAdmin     = false;
					dictMODULE.DisplayName = "Global";
					foreach ( DataRow rowLang in dtLANGUAGES.Rows )
					{
						view = new ViewNode();
						view.ViewName    = Sql.ToString(rowLang["NAME"]);
						view.LayoutType  = "Terminology";
						view.DisplayName = Sql.ToString(rowLang["DISPLAY_NAME"]);
						dictMODULE.Terminology.Add(view);
					}
					// 9h: vwTERMINOLOGY_PickList for Global node TerminologyLists
					sSQL = "select LIST_NAME             " + ControlChars.CrLf
					     + "  from vwTERMINOLOGY_PickList" + ControlChars.CrLf
					     + " order by LIST_NAME          " + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						using ( var da = _dbProviderFactories.CreateDataAdapter() )
						{
							((IDbDataAdapter)da).SelectCommand = cmd;
							using ( DataTable dt = new DataTable() )
							{
								da.Fill(dt);
								foreach ( DataRow row in dt.Rows )
								{
									view = new ViewNode();
									view.ViewName    = Sql.ToString(row["LIST_NAME"]);
									view.LayoutType  = "TerminologyList";
									view.DisplayName = Sql.ToString(row["LIST_NAME"]);
									dictMODULE.TerminologyLists.Add(view);
								}
							}
						}
					}

					// Main UNION ALL query: 9d/9e/9f combined
					sSQL = "select NAME                                   " + ControlChars.CrLf
					     + "     , MODULE_NAME                            " + ControlChars.CrLf
					     + "     , 'EditView'               as LAYOUT_TYPE" + ControlChars.CrLf
					     + "  from vwEDITVIEWS                            " + ControlChars.CrLf
					     + "union all                                     " + ControlChars.CrLf
					     + "select NAME                                   " + ControlChars.CrLf
					     + "     , MODULE_NAME                            " + ControlChars.CrLf
					     + "     , 'DetailView'             as LAYOUT_TYPE" + ControlChars.CrLf
					     + "  from vwDETAILVIEWS                          " + ControlChars.CrLf
					     + "union all                                     " + ControlChars.CrLf
					     + "select NAME                                   " + ControlChars.CrLf
					     + "     , MODULE_NAME                            " + ControlChars.CrLf
					     + "     , 'ListView'               as LAYOUT_TYPE" + ControlChars.CrLf
					     + "  from vwGRIDVIEWS                            " + ControlChars.CrLf
					     + "union all                                     " + ControlChars.CrLf
					     + "select distinct DETAIL_NAME            as NAME" + ControlChars.CrLf
					     + "     , DETAIL_NAME              as MODULE_NAME" + ControlChars.CrLf
					     + "     , 'DetailViewRelationship'               " + ControlChars.CrLf
					     + "  from vwDETAILVIEWS_RELATIONSHIPS_La         " + ControlChars.CrLf
					     + "union all                                     " + ControlChars.CrLf
					     + "select distinct EDIT_NAME              as NAME" + ControlChars.CrLf
					     + "     , EDIT_NAME                as MODULE_NAME" + ControlChars.CrLf
					     + "     , 'EditViewRelationship'                 " + ControlChars.CrLf
					     + "  from " + Sql.MetadataName(con, "vwEDITVIEWS_RELATIONSHIPS_Layout") + ControlChars.CrLf
					     + " order by MODULE_NAME, NAME                   " + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						using ( var da = _dbProviderFactories.CreateDataAdapter() )
						{
							((IDbDataAdapter)da).SelectCommand = cmd;
							using ( DataTable dt = new DataTable() )
							{
								da.Fill(dt);
								foreach ( DataRow row in dt.Rows )
								{
									string sNAME        = Sql.ToString(row["NAME"       ]);
									string sMODULE_NAME = Sql.ToString(row["MODULE_NAME"]);
									string sLAYOUT_TYPE = Sql.ToString(row["LAYOUT_TYPE"]);
									// For relationship views, extract module from dotted name
									if ( sLAYOUT_TYPE == "DetailViewRelationship" || sLAYOUT_TYPE == "EditViewRelationship" )
									{
										string[] arrMODULE_NAME = sMODULE_NAME.Split('.');
										sMODULE_NAME = arrMODULE_NAME[0];
									}
									// 9d: Exclude disabled modules
									if ( dictAllModules.ContainsKey(sMODULE_NAME) && !dictAllModules[sMODULE_NAME] )
										continue;
									try
									{
										if ( !dict.ContainsKey(sMODULE_NAME) )
										{
											dictMODULE = new ModuleNode();
											dict.Add(sMODULE_NAME, dictMODULE);
											lstModules.Add(dictMODULE);
											dictMODULE.ModuleName = sMODULE_NAME;
											dictMODULE.IsAdmin    = Sql.ToBoolean(_memoryCache.Get("Modules." + sMODULE_NAME + ".IsAdmin"));
											// 9i: L10N localization for DisplayName
											string sDisplayName = L10n.Term(".moduleList." + sMODULE_NAME);
											if ( sDisplayName.StartsWith(".moduleList.") )
												sDisplayName = sMODULE_NAME;
											dictMODULE.DisplayName = sDisplayName;
											// Add terminology nodes per language (using DISPLAY_NAME)
											foreach ( DataRow rowLang in dtLANGUAGES.Rows )
											{
												view = new ViewNode();
												view.ViewName    = Sql.ToString(rowLang["NAME"]);
												view.LayoutType  = "Terminology";
												view.DisplayName = Sql.ToString(rowLang["DISPLAY_NAME"]);
												dictMODULE.Terminology.Add(view);
											}
										}
										else
										{
											dictMODULE = dict[sMODULE_NAME];
										}
										view = new ViewNode();
										view.ViewName    = sNAME;
										view.LayoutType  = sLAYOUT_TYPE;
										view.DisplayName = sNAME;
										if ( sNAME.StartsWith(sMODULE_NAME + ".") )
											view.DisplayName = sNAME.Substring(sMODULE_NAME.Length + 1);
										switch ( sLAYOUT_TYPE )
										{
											case "EditView":
												if ( sNAME.Contains(".Search") )
													dictMODULE.Search.Add(view);
												else
													dictMODULE.EditViews.Add(view);
												break;
											case "DetailView":
												dictMODULE.DetailViews.Add(view);
												break;
											case "ListView":
												if ( sNAME.StartsWith(sMODULE_NAME + ".ArchiveView") || sNAME.StartsWith(sMODULE_NAME + ".ListView") || sNAME.StartsWith(sMODULE_NAME + ".PopupView") || sNAME.StartsWith(sMODULE_NAME + ".Export") || sNAME.Contains("." + sMODULE_NAME) )
													dictMODULE.ListViews.Add(view);
												else
													dictMODULE.SubPanels.Add(view);
												break;
											case "DetailViewRelationship":
												dictMODULE.Relationships.Add(view);
												break;
											case "EditViewRelationship":
												dictMODULE.Relationships.Add(view);
												break;
										}
									}
									catch (Exception ex)
									{
										SplendidError.SystemError(new StackFrame(1, true), ex);
									}
								}
							}
						}
					}
				}
				return JsonContent(new { d = lstModules });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// GET Administration/Rest.svc/GetAdminLayoutModuleFields
		/// Returns field metadata for a specific admin layout view.
		/// Query params: ModuleName, LayoutType, LayoutName. Source: lines 336-703.
		/// </summary>
		[HttpGet("GetAdminLayoutModuleFields")]
		public IActionResult GetAdminLayoutModuleFields(string ModuleName, string LayoutType, string LayoutName)
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if ( !_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE) )
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				// Ported from SplendidCRM/Administration/Rest.svc.cs lines 336-700.
				if ( Sql.IsEmptyString(ModuleName) )
					throw new Exception("The module name must be specified.");
				string sTABLE_NAME = Sql.ToString(_memoryCache.Get<object>("Modules." + ModuleName + ".TableName"));
				string sVIEW_NAME  = "vw" + sTABLE_NAME;
				bool   bValid      = Sql.ToBoolean(_memoryCache.Get<object>("Modules." + ModuleName + ".Valid"));
				if ( Sql.IsEmptyString(sTABLE_NAME) && !bValid )
					throw new Exception("Unknown module: " + ModuleName);

				List<LayoutField> lstFields = new List<LayoutField>();
				if ( LayoutType != "EditView" && LayoutType != "DetailView" && LayoutType != "ListView" )
				{
					LayoutType = "EditView";
				}
				if ( Sql.IsEmptyString(LayoutName) )
				{
					LayoutName = ModuleName + "." + LayoutType;
				}

				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				string sSQL;
				DataTable dtDefaultView = new DataTable();

				if ( LayoutType == "EditView" )
				{
					sSQL = "select *                        " + ControlChars.CrLf
					     + "  from vwEDITVIEWS_FIELDS       " + ControlChars.CrLf
					     + " where EDIT_NAME = @LAYOUT_NAME " + ControlChars.CrLf
					     + "   and DEFAULT_VIEW = 1         " + ControlChars.CrLf
					     + " order by FIELD_INDEX           " + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@LAYOUT_NAME", LayoutName);
						using ( var da = _dbProviderFactories.CreateDataAdapter() )
						{
							((IDbDataAdapter)da).SelectCommand = cmd;
							da.Fill(dtDefaultView);
							if ( dtDefaultView.Rows.Count == 0 )
							{
								sSQL = "select *                        " + ControlChars.CrLf
								     + "  from vwEDITVIEWS_FIELDS       " + ControlChars.CrLf
								     + " where EDIT_NAME = @LAYOUT_NAME " + ControlChars.CrLf
								     + "   and DEFAULT_VIEW = 0         " + ControlChars.CrLf
								     + " order by FIELD_INDEX           " + ControlChars.CrLf;
								cmd.CommandText = sSQL;
								da.Fill(dtDefaultView);
							}
						}
					}
					sSQL = "select VIEW_NAME          " + ControlChars.CrLf
					     + "  from vwEDITVIEWS        " + ControlChars.CrLf
					     + " where NAME = @LAYOUT_NAME" + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@LAYOUT_NAME", LayoutName);
						sVIEW_NAME = Sql.ToString(cmd.ExecuteScalar());
						if ( Sql.IsEmptyString(sVIEW_NAME) )
							sVIEW_NAME = "vw" + sTABLE_NAME + "_Edit";
					}
				}
				else if ( LayoutType == "DetailView" )
				{
					sSQL = "select *                         " + ControlChars.CrLf
					     + "  from vwDETAILVIEWS_FIELDS      " + ControlChars.CrLf
					     + " where DETAIL_NAME = @LAYOUT_NAME" + ControlChars.CrLf
					     + "   and DEFAULT_VIEW = 1          " + ControlChars.CrLf
					     + " order by FIELD_INDEX            " + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@LAYOUT_NAME", LayoutName);
						using ( var da = _dbProviderFactories.CreateDataAdapter() )
						{
							((IDbDataAdapter)da).SelectCommand = cmd;
							da.Fill(dtDefaultView);
							if ( dtDefaultView.Rows.Count == 0 )
							{
								sSQL = "select *                         " + ControlChars.CrLf
								     + "  from vwDETAILVIEWS_FIELDS      " + ControlChars.CrLf
								     + " where DETAIL_NAME = @LAYOUT_NAME" + ControlChars.CrLf
								     + "   and DEFAULT_VIEW = 0          " + ControlChars.CrLf
								     + " order by FIELD_INDEX            " + ControlChars.CrLf;
								cmd.CommandText = sSQL;
								da.Fill(dtDefaultView);
							}
						}
					}
					sSQL = "select VIEW_NAME          " + ControlChars.CrLf
					     + "  from vwDETAILVIEWS      " + ControlChars.CrLf
					     + " where NAME = @LAYOUT_NAME" + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@LAYOUT_NAME", LayoutName);
						sVIEW_NAME = Sql.ToString(cmd.ExecuteScalar());
						if ( Sql.IsEmptyString(sVIEW_NAME) )
							sVIEW_NAME = "vw" + sTABLE_NAME + "_Edit";
					}
				}
				else if ( LayoutType == "ListView" )
				{
					sSQL = "select *                        " + ControlChars.CrLf
					     + "  from vwGRIDVIEWS_COLUMNS      " + ControlChars.CrLf
					     + " where GRID_NAME = @LAYOUT_NAME " + ControlChars.CrLf
					     + "   and DEFAULT_VIEW = 1         " + ControlChars.CrLf
					     + " order by COLUMN_INDEX          " + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@LAYOUT_NAME", LayoutName);
						using ( var da = _dbProviderFactories.CreateDataAdapter() )
						{
							((IDbDataAdapter)da).SelectCommand = cmd;
							da.Fill(dtDefaultView);
							if ( dtDefaultView.Rows.Count == 0 )
							{
								sSQL = "select *                        " + ControlChars.CrLf
								     + "  from vwGRIDVIEWS_COLUMNS      " + ControlChars.CrLf
								     + " where GRID_NAME = @LAYOUT_NAME " + ControlChars.CrLf
								     + "   and DEFAULT_VIEW = 0         " + ControlChars.CrLf
								     + " order by COLUMN_INDEX          " + ControlChars.CrLf;
								cmd.CommandText = sSQL;
								da.Fill(dtDefaultView);
							}
						}
					}
					sSQL = "select VIEW_NAME          " + ControlChars.CrLf
					     + "  from vwGRIDVIEWS        " + ControlChars.CrLf
					     + " where NAME = @LAYOUT_NAME" + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@LAYOUT_NAME", LayoutName);
						sVIEW_NAME = Sql.ToString(cmd.ExecuteScalar());
						if ( Sql.IsEmptyString(sVIEW_NAME) )
							sVIEW_NAME = "vw" + sTABLE_NAME + "_List";
					}
				}

				DataView vwDefaultView = new DataView(dtDefaultView);

				using ( IDbCommand cmd = con.CreateCommand() )
				{
					if ( LayoutType == "EditView" && !LayoutName.Contains(".Search") )
					{
						if ( Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.LayoutEditor.EditView.AllFields")) )
						{
							sSQL = "select *                        " + ControlChars.CrLf
							     + "  from vwSqlColumns             " + ControlChars.CrLf
							     + " where ObjectName = @OBJECTNAME " + ControlChars.CrLf
							     + " order by ColumnName            " + ControlChars.CrLf;
							cmd.CommandText = sSQL;
							if ( !Sql.IsEmptyString(sVIEW_NAME) )
								Sql.AddParameter(cmd, "@OBJECTNAME", Sql.MetadataName(cmd, sVIEW_NAME));
							else
								Sql.AddParameter(cmd, "@OBJECTNAME", Sql.MetadataName(cmd, "vw" + sTABLE_NAME));
						}
						else
						{
							sSQL = "select *                        " + ControlChars.CrLf
							     + "  from vwSqlColumns             " + ControlChars.CrLf
							     + " where ObjectName = @OBJECTNAME " + ControlChars.CrLf
							     + "   and ObjectType = 'P'         " + ControlChars.CrLf
							     + " union all                      " + ControlChars.CrLf
							     + "select *                        " + ControlChars.CrLf
							     + "  from vwSqlColumns             " + ControlChars.CrLf
							     + " where ObjectName = @CUSTOMNAME " + ControlChars.CrLf
							     + "   and ObjectType = 'U'         " + ControlChars.CrLf;
							if ( Sql.IsOracle(con) )
							{
								sSQL = "select *" + ControlChars.CrLf
								     + " from (" + sSQL + ControlChars.CrLf
								     + "      ) vwSqlColumns" + ControlChars.CrLf
								     + " order by ColumnName" + ControlChars.CrLf;
							}
							else
							{
								sSQL += " order by ColumnName" + ControlChars.CrLf;
							}
							cmd.CommandText = sSQL;
							Sql.AddParameter(cmd, "@OBJECTNAME", Sql.MetadataName(cmd, "sp" + sTABLE_NAME + "_Update"));
							Sql.AddParameter(cmd, "@CUSTOMNAME", Sql.MetadataName(cmd, sTABLE_NAME + "_CSTM"));
						}
					}
					else
					{
						sSQL = "select *                        " + ControlChars.CrLf
						     + "  from vwSqlColumns             " + ControlChars.CrLf
						     + " where ObjectName = @OBJECTNAME " + ControlChars.CrLf
						     + " order by ColumnName            " + ControlChars.CrLf;
						cmd.CommandText = sSQL;
						if ( !Sql.IsEmptyString(sVIEW_NAME) )
							Sql.AddParameter(cmd, "@OBJECTNAME", Sql.MetadataName(cmd, sVIEW_NAME));
						else if ( LayoutType == "ListView" )
							Sql.AddParameter(cmd, "@OBJECTNAME", Sql.MetadataName(cmd, "vw" + sTABLE_NAME + "_List"));
						else
							Sql.AddParameter(cmd, "@OBJECTNAME", Sql.MetadataName(cmd, "vw" + sTABLE_NAME));
					}

					using ( var da = _dbProviderFactories.CreateDataAdapter() )
					{
						((IDbDataAdapter)da).SelectCommand = cmd;
						using ( DataTable dt = new DataTable() )
						{
							da.Fill(dt);
							// Add missing columns as in legacy lines 569-586
							if ( !dt.Columns.Contains("FIELD_TYPE"        ) ) dt.Columns.Add("FIELD_TYPE"        , typeof(System.String));
							if ( !dt.Columns.Contains("DATA_LABEL"        ) ) dt.Columns.Add("DATA_LABEL"        , typeof(System.String));
							if ( !dt.Columns.Contains("DATA_FIELD"        ) ) dt.Columns.Add("DATA_FIELD"        , typeof(System.String));
							if ( !dt.Columns.Contains("DISPLAY_FIELD"     ) ) dt.Columns.Add("DISPLAY_FIELD"     , typeof(System.String));
							if ( !dt.Columns.Contains("MODULE_TYPE"       ) ) dt.Columns.Add("MODULE_TYPE"       , typeof(System.String));
							if ( !dt.Columns.Contains("LIST_NAME"         ) ) dt.Columns.Add("LIST_NAME"         , typeof(System.String));
							if ( !dt.Columns.Contains("DATA_FORMAT"       ) ) dt.Columns.Add("DATA_FORMAT"       , typeof(System.String));
							if ( !dt.Columns.Contains("FORMAT_MAX_LENGTH" ) ) dt.Columns.Add("FORMAT_MAX_LENGTH" , typeof(System.Int32 ));
							if ( !dt.Columns.Contains("URL_FIELD"         ) ) dt.Columns.Add("URL_FIELD"         , typeof(System.String));
							if ( !dt.Columns.Contains("URL_FORMAT"        ) ) dt.Columns.Add("URL_FORMAT"        , typeof(System.String));
							if ( !dt.Columns.Contains("COLUMN_TYPE"       ) ) dt.Columns.Add("COLUMN_TYPE"       , typeof(System.String));
							if ( !dt.Columns.Contains("HEADER_TEXT"       ) ) dt.Columns.Add("HEADER_TEXT"       , typeof(System.String));
							if ( !dt.Columns.Contains("SORT_EXPRESSION"   ) ) dt.Columns.Add("SORT_EXPRESSION"   , typeof(System.String));
							if ( !dt.Columns.Contains("URL_ASSIGNED_FIELD") ) dt.Columns.Add("URL_ASSIGNED_FIELD", typeof(System.String));

							foreach ( DataRow row in dt.Rows )
							{
								string sColumnName = Sql.ToString(row["ColumnName"]);
								if ( sColumnName.StartsWith("@") )
									sColumnName = sColumnName.Replace("@", String.Empty);
								else if ( sColumnName.StartsWith("ID_") && Sql.IsOracle(cmd) )
									sColumnName = sColumnName.Substring(3);
								// Filter system columns per legacy lines 602-606
								if ( (sColumnName == "ID" && !LayoutName.Contains(".Export")) || sColumnName == "ID_C" || sColumnName == "MODIFIED_USER_ID" || sColumnName == "TEAM_SET_LIST" || sColumnName == "ASSIGNED_SET_LIST" )
								{
									row.Delete();
									continue;
								}
								row["ColumnName"] = sColumnName;
								row["DATA_LABEL"] = Utils.BuildTermName(ModuleName, sColumnName);
								row["DATA_FIELD"] = sColumnName;
								if ( LayoutType == "EditView" )
								{
									row["FIELD_TYPE"] = "TextBox";
									vwDefaultView.RowFilter = "DATA_FIELD = '" + sColumnName + "'";
									if ( vwDefaultView.Count > 0 )
									{
										row["FIELD_TYPE"       ] = Sql.ToString (vwDefaultView[0]["FIELD_TYPE"       ]);
										row["DISPLAY_FIELD"    ] = Sql.ToString (vwDefaultView[0]["DISPLAY_FIELD"    ]);
										row["LIST_NAME"        ] = Sql.ToString (vwDefaultView[0]["LIST_NAME"        ]);
										row["DATA_FORMAT"      ] = Sql.ToString (vwDefaultView[0]["DATA_FORMAT"      ]);
										row["FORMAT_MAX_LENGTH"] = Sql.ToInteger(vwDefaultView[0]["FORMAT_MAX_LENGTH"]);
										row["MODULE_TYPE"      ] = Sql.ToString (vwDefaultView[0]["MODULE_TYPE"      ]);
									}
								}
								else if ( LayoutType == "DetailView" )
								{
									row["FIELD_TYPE" ] = "String";
									row["DATA_FORMAT"] = "{0}";
									vwDefaultView.RowFilter = "DATA_FIELD = '" + sColumnName + "'";
									if ( vwDefaultView.Count > 0 )
									{
										row["FIELD_TYPE" ] = Sql.ToString(vwDefaultView[0]["FIELD_TYPE" ]);
										row["LIST_NAME"  ] = Sql.ToString(vwDefaultView[0]["LIST_NAME"  ]);
										row["DATA_FORMAT"] = Sql.ToString(vwDefaultView[0]["DATA_FORMAT"]);
										row["URL_FIELD"  ] = Sql.ToString(vwDefaultView[0]["URL_FIELD"  ]);
										row["URL_FORMAT" ] = Sql.ToString(vwDefaultView[0]["URL_FORMAT" ]);
										row["MODULE_TYPE"] = Sql.ToString(vwDefaultView[0]["MODULE_TYPE"]);
									}
								}
								else if ( LayoutType == "ListView" )
								{
									row["COLUMN_TYPE"    ] = "BoundColumn";
									row["DATA_FORMAT"    ] = String.Empty;
									row["SORT_EXPRESSION"] = sColumnName;
									string sMODULE_NAME = ModuleName;
									string[] arrNAME = LayoutName.Split('.');
									if ( arrNAME.Length > 1 && Sql.ToBoolean(_memoryCache.Get<object>("Modules." + arrNAME[1] + ".Valid")) )
									{
										sMODULE_NAME = arrNAME[1];
									}
									row["HEADER_TEXT"] = Utils.BuildTermName(sMODULE_NAME, sColumnName).Replace(".LBL_", ".LBL_LIST_");
									vwDefaultView.RowFilter = "DATA_FIELD = '" + sColumnName + "'";
									if ( vwDefaultView.Count > 0 )
									{
										row["COLUMN_TYPE"       ] = Sql.ToString(vwDefaultView[0]["COLUMN_TYPE"       ]);
										row["DATA_FORMAT"       ] = Sql.ToString(vwDefaultView[0]["DATA_FORMAT"       ]);
										row["HEADER_TEXT"       ] = Sql.ToString(vwDefaultView[0]["HEADER_TEXT"       ]);
										row["SORT_EXPRESSION"   ] = Sql.ToString(vwDefaultView[0]["SORT_EXPRESSION"   ]);
										row["LIST_NAME"         ] = Sql.ToString(vwDefaultView[0]["LIST_NAME"         ]);
										row["URL_FIELD"         ] = Sql.ToString(vwDefaultView[0]["URL_FIELD"         ]);
										row["URL_FORMAT"        ] = Sql.ToString(vwDefaultView[0]["URL_FORMAT"        ]);
										row["MODULE_TYPE"       ] = Sql.ToString(vwDefaultView[0]["MODULE_TYPE"       ]);
										row["URL_ASSIGNED_FIELD"] = Sql.ToString(vwDefaultView[0]["URL_ASSIGNED_FIELD"]);
									}
								}
							}
							dt.AcceptChanges();
							DataView vw = new DataView(dt);
							vw.Sort = "DATA_FIELD asc";
							foreach ( DataRow row in dt.Rows )
							{
								LayoutField lay = new LayoutField();
								lay.ColumnName         = Sql.ToString (row["ColumnName"        ]);
								lay.ColumnType         = Sql.ToString (row["ColumnType"        ]);
								lay.CsType             = Sql.ToString (row["CsType"            ]);
								lay.length             = Sql.ToInteger(row["length"            ]);
								lay.FIELD_TYPE         = Sql.ToString (row["FIELD_TYPE"        ]);
								lay.DATA_LABEL         = Sql.ToString (row["DATA_LABEL"        ]);
								lay.DATA_FIELD         = Sql.ToString (row["DATA_FIELD"        ]);
								lay.MODULE_TYPE        = Sql.ToString (row["MODULE_TYPE"       ]);
								lay.LIST_NAME          = Sql.ToString (row["LIST_NAME"         ]);
								lay.DATA_FORMAT        = Sql.ToString (row["DATA_FORMAT"       ]);
								if ( lay.CsType == "string" )
									lay.FORMAT_MAX_LENGTH = Sql.ToInteger(row["FORMAT_MAX_LENGTH"]);
								lay.URL_FIELD          = Sql.ToString (row["URL_FIELD"         ]);
								lay.URL_FORMAT         = Sql.ToString (row["URL_FORMAT"        ]);
								lay.COLUMN_TYPE        = Sql.ToString (row["COLUMN_TYPE"       ]);
								lay.HEADER_TEXT        = Sql.ToString (row["HEADER_TEXT"       ]);
								lay.SORT_EXPRESSION    = Sql.ToString (row["SORT_EXPRESSION"   ]);
								lay.URL_ASSIGNED_FIELD = Sql.ToString (row["URL_ASSIGNED_FIELD"]);
								lstFields.Add(lay);
							}
						}
					}
				}
				Dictionary<string, object> d = new Dictionary<string, object>();
				d.Add("d", lstFields);
				return JsonContent(d);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		private IActionResult GetAdminLayoutTerminologyInternal(IDbConnection con, string sModuleName, string sLayoutName)
		{
			// sLayoutName format: "MODULE_NAME.LANG"
			string[] parts = sLayoutName.Split('.');
			string sLang   = parts.Length > 1 ? parts[1] : "en-US";
			using IDbCommand cmd = con.CreateCommand();
			cmd.CommandText =
				"select *                          " + ControlChars.CrLf
			  + "  from vwTERMINOLOGY              " + ControlChars.CrLf
			  + " where MODULE_NAME = @MODULE_NAME " + ControlChars.CrLf
			  + "   and LANG = @LANG               " + ControlChars.CrLf
			  + " order by NAME                    " + ControlChars.CrLf;
			Sql.AddParameter(cmd, "@MODULE_NAME", sModuleName);
			Sql.AddParameter(cmd, "@LANG"       , sLang      );
			using var da = _dbProviderFactories.CreateDataAdapter();
			((IDbDataAdapter)da).SelectCommand = cmd;
			using var dt = new DataTable();
			da.Fill(dt);
			var results = new List<Dictionary<string, object>>();
			foreach (DataRow row in dt.Rows)
			{
				var d = new Dictionary<string, object>();
				for (int i = 0; i < dt.Columns.Count; i++)
					d[dt.Columns[i].ColumnName] = row[i];
				results.Add(d);
			}
			return JsonContent(new { d = new { results } });
		}

		private IActionResult GetAdminLayoutTerminologyListsInternal(IDbConnection con, string sModuleName, string sLayoutName)
		{
			string[] parts = sLayoutName.Split('.');
			string sLang   = parts.Length > 1 ? parts[1] : "en-US";
			using IDbCommand cmd = con.CreateCommand();
			cmd.CommandText =
				"select *                          " + ControlChars.CrLf
			  + "  from vwTERMINOLOGY_LISTS        " + ControlChars.CrLf
			  + " where LANG = @LANG               " + ControlChars.CrLf
			  + " order by LIST_NAME, ITEM_VALUE   " + ControlChars.CrLf;
			Sql.AddParameter(cmd, "@LANG", sLang);
			using var da = _dbProviderFactories.CreateDataAdapter();
			((IDbDataAdapter)da).SelectCommand = cmd;
			using var dt = new DataTable();
			da.Fill(dt);
			var results = new List<Dictionary<string, object>>();
			foreach (DataRow row in dt.Rows)
			{
				var d = new Dictionary<string, object>();
				for (int i = 0; i < dt.Columns.Count; i++)
					d[dt.Columns[i].ColumnName] = row[i];
				results.Add(d);
			}
			return JsonContent(new { d = new { results } });
		}
		private DataTable GetSqlColumnsForTable(string sTableName)
		{
			return _splendidCache.SqlColumns(sTableName);
		}

		/// <summary>
		/// GET Administration/Rest.svc/GetAdminMenu
		/// Returns the full admin panel menu structure grouped by category.
		/// Source: lines 2577-2947 (GetAdminMenu + BuildAdminModules).
		/// </summary>
		[HttpGet("GetAdminMenu")]
		public IActionResult GetAdminMenu()
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				// Build module tab order from admin modules
				Dictionary<string, int> dictModuleTabOrder = BuildModuleTabOrder();
				List<Dictionary<string, object>> menu = GetAdminMenuInternal(dictModuleTabOrder);
				return JsonContent(new { d = new { results = menu } });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// GET Administration/Rest.svc/GetAllGridViewsColumns
		/// Returns grid view column definitions for all admin-accessible modules. Source: RestController pattern.
		/// </summary>
		[HttpGet("GetAllGridViewsColumns")]
		public IActionResult GetAllGridViewsColumns()
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });
				List<string> lstMODULES = GetAdminModuleList();
				var d = GetAllGridViewsColumnsInternal(lstMODULES);
				return JsonContent(new { d });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// GET Administration/Rest.svc/GetAllDetailViewsFields
		/// Returns detail view field definitions for all admin-accessible modules.
		/// </summary>
		[HttpGet("GetAllDetailViewsFields")]
		public IActionResult GetAllDetailViewsFields()
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });
				List<string> lstMODULES = GetAdminModuleList();
				var d = GetAllDetailViewsFieldsInternal(lstMODULES);
				return JsonContent(new { d });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// GET Administration/Rest.svc/GetAllEditViewsFields
		/// Returns edit view field definitions for all admin-accessible modules.
		/// </summary>
		[HttpGet("GetAllEditViewsFields")]
		public IActionResult GetAllEditViewsFields()
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });
				List<string> lstMODULES = GetAdminModuleList();
				var d = GetAllEditViewsFieldsInternal(lstMODULES);
				return JsonContent(new { d });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// GET Administration/Rest.svc/GetAllDetailViewsRelationships
		/// Returns detail view relationship panel definitions.
		/// </summary>
		[HttpGet("GetAllDetailViewsRelationships")]
		public IActionResult GetAllDetailViewsRelationships()
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });
				List<string> lstMODULES = GetAdminModuleList();
				var d = GetAllDetailViewsRelationshipsInternal(lstMODULES);
				return JsonContent(new { d });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// GET Administration/Rest.svc/GetAllEditViewsRelationships
		/// Returns edit view relationship panel definitions.
		/// </summary>
		[HttpGet("GetAllEditViewsRelationships")]
		public IActionResult GetAllEditViewsRelationships()
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });
				List<string> lstMODULES = GetAdminModuleList();
				var d = GetAllEditViewsRelationshipsInternal(lstMODULES);
				return JsonContent(new { d });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// GET Administration/Rest.svc/GetAllDynamicButtons
		/// Returns dynamic button definitions with ACL access levels.
		/// </summary>
		[HttpGet("GetAllDynamicButtons")]
		public IActionResult GetAllDynamicButtons()
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });
				List<string> lstMODULES = GetAdminModuleList();
				var d = GetAllDynamicButtonsInternal(lstMODULES);
				return JsonContent(new { d });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// GET Administration/Rest.svc/GetAllTerminology
		/// Returns all terminology strings for all admin-accessible modules.
		/// Admin gets all terms including admin-only terms.
		/// </summary>
		[HttpGet("GetAllTerminology")]
		public IActionResult GetAllTerminology()
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });
				List<string> lstMODULES = GetAdminModuleList();
				var d = GetAllTerminologyInternal(lstMODULES, true);  // true = include admin terminology
				return JsonContent(new { d });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// GET Administration/Rest.svc/GetAllTerminologyLists
		/// Returns all terminology list (dropdown) definitions.
		/// Admin gets all lists including admin-only lists.
		/// </summary>
		[HttpGet("GetAllTerminologyLists")]
		public IActionResult GetAllTerminologyLists()
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });
				var d = GetAllTerminologyListsInternal(true);  // true = include admin lists
				return JsonContent(new { d });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// GET Administration/Rest.svc/GetAdminLayoutModuleData
		/// Returns data rows for a specific admin table (replaces GetAdminTable).
		/// Query param: TableName. Source: lines 1816-2451.
		/// </summary>
		[HttpGet("GetAdminLayoutModuleData")]
		public IActionResult GetAdminLayoutModuleData(string TableName)
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });
				if (Sql.IsEmptyString(TableName))
					return BadRequest(new { error = "TableName is required" });

				// Validate table name — only allow alphanumeric/underscore to prevent SQL injection
				TableName = TableName.ToUpper();
				if (!Regex.IsMatch(TableName, @"^[A-Za-z0-9_]+$"))
					return BadRequest(new { error = "Invalid TableName" });

				// OData-style query parameters (source: lines 1828-1835, GetAdminTable)
				int    nSKIP     = Sql.ToInteger(Request.Query["$skip"   ]);
				int    nTOP      = Sql.ToInteger(Request.Query["$top"    ]);
				string sFILTER   = Sql.ToString (Request.Query["$filter" ]);
				string sORDER_BY = Sql.ToString (Request.Query["$orderby"]);
				string sGROUP_BY = Sql.ToString (Request.Query["$groupby"]);
				string sSELECT   = Sql.ToString (Request.Query["$select" ]);

				long lTotalCount = 0;
				var sbDumpSQL = new StringBuilder();
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();

				string sVIEW_NAME    = String.Empty;
				string sDEFAULT_VIEW = String.Empty;
				string sMATCH_NAME   = "DEFAULT_VIEW";
				Match match = Regex.Match(sFILTER, "\\b" + sMATCH_NAME + "\\s*(=|eq)\\s*\'(?<" + sMATCH_NAME + ">([^(\'|\\s)]*))", RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);
				if (match.Success)
					sDEFAULT_VIEW = match.Groups[sMATCH_NAME].Value;

				using var dt = new DataTable();

				// Use vw prefix for view access (provider-agnostic)
				// SearchBuilder provides OData-style $filter→WHERE clause conversion
				// for the generic table case (source: lines 1850-1940 GetAdminTable)
				using (IDbCommand cmd = con.CreateCommand())
				{
					string sView = Sql.MetadataName(cmd, "vw" + TableName);
					cmd.CommandText =
						"select *              " + ControlChars.CrLf
					  + "  from " + sView      + ControlChars.CrLf
					  + " where 1 = 1          " + ControlChars.CrLf;

					// Apply OData $filter using SearchBuilder — preserves custom OData parsing
					// per AAP Section 0.7.3 immutable interfaces rule (NOT Microsoft OData middleware)
					if (!Sql.IsEmptyString(sFILTER))
					{
						SearchBuilder sb = new SearchBuilder(sFILTER, cmd);
						// BuildQuery appends provider-aware WHERE conditions for matched field names
						string sFilterClause = sb.BuildQuery("   and ", "NAME");
						if (!Sql.IsEmptyString(sFilterClause))
							cmd.CommandText += sFilterClause;
					}

					if (!Sql.IsEmptyString(sORDER_BY))
						cmd.CommandText += " order by " + sORDER_BY + ControlChars.CrLf;
					else
						cmd.CommandText += " order by 1" + ControlChars.CrLf;

					using var da = _dbProviderFactories.CreateDataAdapter();
					((IDbDataAdapter)da).SelectCommand = cmd;
					da.Fill(dt);
					lTotalCount = dt.Rows.Count;
					sbDumpSQL.Append(Sql.ExpandParameters(cmd));
				}

				// Apply $skip and $top paging
				int nStart = nSKIP > 0 ? nSKIP : 0;
				int nEnd   = nTOP  > 0 ? Math.Min(nStart + nTOP, (int)lTotalCount) : (int)lTotalCount;

				var rows = new List<Dictionary<string, object>>();
				for (int i = nStart; i < nEnd && i < dt.Rows.Count; i++)
				{
					DataRow row = dt.Rows[i];
					var d = new Dictionary<string, object>();
					for (int j = 0; j < dt.Columns.Count; j++)
						d[dt.Columns[j].ColumnName] = row[j] == DBNull.Value ? null : row[j];
					rows.Add(d);
				}

				var dictResponse = new Dictionary<string, object>
				{
					{ "results"  , rows        },
					{ "__count"  , lTotalCount },
					{ "__total"  , lTotalCount }
				};
				if (Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.show_sql")))
					dictResponse["__sql"] = sbDumpSQL.ToString();

				return JsonContent(new { d = dictResponse });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		// ==================================================================
		// #region React State
		// ==================================================================

		/// <summary>
		/// GET Administration/Rest.svc/GetReactState
		/// Returns the full admin React SPA bootstrap state (layouts, terminology, config,
		/// ACL, user profile, admin menu, etc.). Source: lines 3021-3204.
		/// CRITICAL: JSON response structure must be 100% parity with .NET Framework version.
		/// </summary>
		[HttpGet("GetReactState")]
		public IActionResult GetReactState()
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				List<string> lstMODULES = GetAdminModuleList();
				Dictionary<string, int> dictModuleTabOrder = BuildModuleTabOrder();

				var results = new Dictionary<string, object>();

				// User profile
				results["USER_PROFILE"] = GetUserProfileDict();

				// Admin modules metadata
				DataTable dtAdminModules = _splendidCache.GetAdminModules();
				var MODULES = new Dictionary<string, object>();
				if (dtAdminModules != null)
				{
					foreach (DataRow row in dtAdminModules.Rows)
					{
						string sName = Sql.ToString(row["MODULE_NAME"]);
						if (!MODULES.ContainsKey(sName))
						{
							var modInfo = new Dictionary<string, object>();
							for (int i = 0; i < dtAdminModules.Columns.Count; i++)
								modInfo[dtAdminModules.Columns[i].ColumnName] = row[i] == DBNull.Value ? null : row[i];
							MODULES[sName] = modInfo;
						}
					}
				}
				results["MODULES"] = MODULES;

				// Search columns
				results["MODULE_COLUMNS"] = GetAllSearchColumnsInternal(lstMODULES);

				// Users and Teams (admin always gets these)
				results["USERS"] = GetAllUsersInternal();
				results["TEAMS"] = GetAllTeamsInternal();

				// Tab menus
				results["TAB_MENU"] = GetAllTabMenusInternal();

				// Admin menu
				results["ADMIN_MENU"] = GetAdminMenuInternal(dictModuleTabOrder);

				// Grid/Detail/Edit view layouts
				results["GRIDVIEWS"               ] = GetAllGridViewsInternal       (lstMODULES);
				results["GRIDVIEWS_COLUMNS"       ] = GetAllGridViewsColumnsInternal(lstMODULES);
				results["DETAILVIEWS_FIELDS"      ] = GetAllDetailViewsFieldsInternal(lstMODULES);
				results["EDITVIEWS_FIELDS"        ] = GetAllEditViewsFieldsInternal (lstMODULES);
				results["DETAILVIEWS_RELATIONSHIPS"] = GetAllDetailViewsRelationshipsInternal(lstMODULES);
				results["EDITVIEWS_RELATIONSHIPS" ] = GetAllEditViewsRelationshipsInternal(lstMODULES);
				results["DYNAMIC_BUTTONS"         ] = GetAllDynamicButtonsInternal  (lstMODULES);
				results["SHORTCUTS"               ] = GetAllShortcutsInternal       (lstMODULES);

				// Terminology — admin version includes admin-only terms
				results["TERMINOLOGY_LISTS"] = GetAllTerminologyListsInternal(true);
				results["TERMINOLOGY"      ] = GetAllTerminologyInternal(lstMODULES, true);

				// Relationships, rates, time-related
				results["RELATIONSHIPS"] = _splendidCache.GetAllRelationships();
				results["TAX_RATES"    ] = GetTaxRatesInternal();
				results["DISCOUNTS"    ] = GetDiscountsInternal();
				results["TIMEZONES"    ] = GetTimezonesInternal();
				results["CURRENCIES"   ] = GetCurrenciesInternal();
				results["LANGUAGES"    ] = GetLanguagesInternal();

				// Favorites, last viewed, saved search
				results["FAVORITES"    ] = GetAllFavoritesInternal();
				results["LAST_VIEWED"  ] = GetAllLastViewedInternal();
				results["SAVED_SEARCH" ] = GetAllSavedSearchInternal();

				// Dashboards
				results["DASHBOARDS"       ] = GetAllDashboardsInternal();
				results["DASHBOARDS_PANELS"] = GetAllDashboardPanelsInternal(lstMODULES);

				// Signatures, outbound email/SMS
				results["SIGNATURES"    ] = GetUserSignaturesInternal();
				results["OUTBOUND_EMAILS"] = GetOutboundMailInternal();
				results["OUTBOUND_SMS"  ] = GetOutboundSmsInternal();

				// React custom views (admin-specific custom views, cached)
				string sModuleList = String.Join(",", lstMODULES.ToArray());
				string sCacheKeyCustomViews = "ReactCustomViews.Admin." + sModuleList;
				Dictionary<string, object> objs = _memoryCache.Get<Dictionary<string, object>>(sCacheKeyCustomViews);
				if (objs == null)
				{
					objs = new Dictionary<string, object>();
					// Admin custom views — in .NET 10, no physical path translation is needed
					// The views are loaded from the React build output
					_memoryCache.Set(sCacheKeyCustomViews, objs, _splendidCache.DefaultCacheExpiration());
				}
				results["REACT_CUSTOM_VIEWS"] = objs;

				// Session timeout (replaces Session.Timeout → IConfiguration)
				int nSessionStateTimeout = Sql.ToInteger(_configuration?["SessionStateTimeout"] ?? "20");
				results["SessionStateTimeout"] = nSessionStateTimeout * 60;

				return JsonContent(new { d = results });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		// ==================================================================
		// #region ACL Access (GET operations)
		// ==================================================================

		/// <summary>
		/// GET Administration/Rest.svc/GetAclAccessByUser
		/// Returns ACL access matrix for a specific user. Source: lines 5674-5767.
		/// </summary>
		[HttpGet("GetAclAccessByUser")]
		public IActionResult GetAclAccessByUser(Guid USER_ID)
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !_security.IS_ADMIN)
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				SplendidCRM.TimeZone T10n = GetUserTimezone();
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				// Ported from SplendidCRM/Administration/Rest.svc.cs lines 5706-5730.
				using var dt = new DataTable();
				string sSQL = "select MODULE_NAME          " + ControlChars.CrLf
				            + "     , DISPLAY_NAME         " + ControlChars.CrLf
				            + "     , ACLACCESS_ADMIN      " + ControlChars.CrLf
				            + "     , ACLACCESS_ACCESS     " + ControlChars.CrLf
				            + "     , ACLACCESS_VIEW       " + ControlChars.CrLf
				            + "     , ACLACCESS_LIST       " + ControlChars.CrLf
				            + "     , ACLACCESS_EDIT       " + ControlChars.CrLf
				            + "     , ACLACCESS_DELETE     " + ControlChars.CrLf
				            + "     , ACLACCESS_IMPORT     " + ControlChars.CrLf
				            + "     , ACLACCESS_EXPORT     " + ControlChars.CrLf
				            + "     , ACLACCESS_ARCHIVE    " + ControlChars.CrLf
				            + "     , IS_ADMIN             " + ControlChars.CrLf
				            + "  from vwACL_ACCESS_ByUser  " + ControlChars.CrLf
				            + " where USER_ID = @USER_ID   " + ControlChars.CrLf;
				using ( IDbCommand cmd = con.CreateCommand() )
				{
					cmd.CommandText = sSQL;
					Sql.AddParameter(cmd, "@USER_ID", USER_ID);
					if ( _security.AdminUserAccess("Users", "edit") < 0 )
					{
						StringBuilder sbSelf = new StringBuilder();
						Sql.AppendParameter(cmd, sbSelf, "USER_ID", _security.USER_ID);
						cmd.CommandText += sbSelf.ToString();
					}
					cmd.CommandText += " order by MODULE_NAME" + ControlChars.CrLf;
					using var da = _dbProviderFactories.CreateDataAdapter();
					((IDbDataAdapter)da).SelectCommand = cmd;
					da.Fill(dt);
				}
				var reqObj = HttpContext.Request;
				string sBaseURI = reqObj.Scheme + "://" + reqObj.Host.Value + reqObj.PathBase.Value + "/Administration/Rest.svc/GetAclAccessByUser";
				Dictionary<string, object> dictResponse = _restUtil.ToJson(sBaseURI, "ACL", dt, T10n);
				return JsonContent(new { d = dictResponse });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// GET Administration/Rest.svc/GetAclAccessByRole
		/// Returns ACL access matrix for a specific role. Source: lines 5771-5843.
		/// </summary>
		[HttpGet("GetAclAccessByRole")]
		public IActionResult GetAclAccessByRole(Guid ROLE_ID)
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !_security.IS_ADMIN)
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				SplendidCRM.TimeZone T10n = GetUserTimezone();
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                           " + ControlChars.CrLf
				  + "  from vwACL_ACCESS_ByRole         " + ControlChars.CrLf
				  + " where ROLE_ID = @ROLE_ID          " + ControlChars.CrLf
				  + " order by MODULE_NAME              " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@ROLE_ID", ROLE_ID);
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				string json = JsonConvert.SerializeObject(_restUtil.ToJson(null, "ACL", dt, T10n), Newtonsoft.Json.Formatting.None);
				return Content(json, "application/json", Encoding.UTF8);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// GET Administration/Rest.svc/GetAclFieldAccessByRole
		/// Returns field-level ACL security matrix for a role/module. Source: lines 6242-6311.
		/// Query params: ROLE_ID, MODULE_NAME.
		/// </summary>
		[HttpGet("GetAclFieldAccessByRole")]
		public IActionResult GetAclFieldAccessByRole(Guid ROLE_ID, string MODULE_NAME)
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !_security.IS_ADMIN)
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				SplendidCRM.TimeZone T10n = GetUserTimezone();
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                 " + ControlChars.CrLf
				  + "  from vwACL_FIELDS_ByRole               " + ControlChars.CrLf
				  + " where ROLE_ID     = @ROLE_ID            " + ControlChars.CrLf
				  + "   and MODULE_NAME = @MODULE_NAME        " + ControlChars.CrLf
				  + " order by FIELD_NAME                     " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@ROLE_ID"    , ROLE_ID    );
				Sql.AddParameter(cmd, "@MODULE_NAME", MODULE_NAME);
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				string json = JsonConvert.SerializeObject(_restUtil.ToJson(null, "ACL_FIELDS", dt, T10n), Newtonsoft.Json.Formatting.None);
				return Content(json, "application/json", Encoding.UTF8);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// GET Administration/Rest.svc/GetAclFieldAliases
		/// Returns field aliases used by the ACL field security system. Source: lines 6437-6469.
		/// </summary>
		[HttpGet("GetAclFieldAliases")]
		public IActionResult GetAclFieldAliases()
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !_security.IS_ADMIN)
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				SplendidCRM.TimeZone T10n = GetUserTimezone();
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                          " + ControlChars.CrLf
				  + "  from vwACL_FIELD_ALIASES        " + ControlChars.CrLf
				  + " order by MODULE_NAME, FIELD_NAME " + ControlChars.CrLf;
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				string json = JsonConvert.SerializeObject(_restUtil.ToJson(null, "ACL_FIELD_ALIASES", dt, T10n), Newtonsoft.Json.Formatting.None);
				return Content(json, "application/json", Encoding.UTF8);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		// ==================================================================
		// #region Update Operations (POST)
		// ==================================================================

		/// <summary>
		/// POST Administration/Rest.svc/UpdateAdminLayout
		/// Dispatches layout updates for EDITVIEWS_FIELDS, DETAILVIEWS_FIELDS,
		/// GRIDVIEWS_COLUMNS, EDITVIEWS_RELATIONSHIPS, DETAILVIEWS_RELATIONSHIPS,
		/// and DYNAMIC_BUTTONS tables. Source: lines 3275-3315.
		/// </summary>
		[HttpPost("UpdateAdminLayout")]
		public async System.Threading.Tasks.Task<IActionResult> UpdateAdminLayout()
		{
			try
			{
				// Ported from SplendidCRM/Administration/Rest.svc.cs lines 3275-3315.
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if ( !_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE) )
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				// Legacy reads TableName and ViewName from query string.
				string sTableName = HttpContext.Request.Query.ContainsKey("TableName")
					? Sql.ToString(HttpContext.Request.Query["TableName"]) : String.Empty;
				string sViewName  = HttpContext.Request.Query.ContainsKey("ViewName")
					? Sql.ToString(HttpContext.Request.Query["ViewName"]) : String.Empty;

				string body = await ReadRequestBodyAsync();
				Dictionary<string, object> dict = DeserializeBody(body);

				// Fallback: if not in query string, try body.
				if ( Sql.IsEmptyString(sTableName) )
					sTableName = Sql.ToString(dict.ContainsKey("TableName") ? dict["TableName"] : null);
				if ( Sql.IsEmptyString(sViewName) )
					sViewName = Sql.ToString(dict.ContainsKey("ViewName") ? dict["ViewName"] : null);

				if ( Sql.IsEmptyString(sTableName) )
					return BadRequest(new { error = "The table name must be specified." });
				if ( Sql.IsEmptyString(sViewName) && sTableName != "TERMINOLOGY" )
					return BadRequest(new { error = "The layout view name must be specified." });

				// Dispatch to the appropriate handler and clear caches.
				// Ported from SplendidCRM/Administration/Rest.svc.cs lines 3297-3313.
				using ( IDbConnection con = _dbProviderFactories.CreateConnection() )
				{
					con.Open();
					switch ( sTableName )
					{
						case "EDITVIEWS_FIELDS":
						{
							using ( IDbTransaction trn = Sql.BeginTransaction(con) )
							{
								try
								{
									UpdateLayoutTable(con, trn, sTableName, dict);
									trn.Commit();
								}
								catch(Exception ex) { trn.Rollback(); throw new Exception("Layout update failed; transaction aborted: " + ex.Message, ex); }
							}
							_splendidCache.ClearEditView(sViewName);
							break;
						}
						case "DETAILVIEWS_FIELDS":
						{
							using ( IDbTransaction trn = Sql.BeginTransaction(con) )
							{
								try
								{
									UpdateLayoutTable(con, trn, sTableName, dict);
									trn.Commit();
								}
								catch(Exception ex) { trn.Rollback(); throw new Exception("Layout update failed; transaction aborted: " + ex.Message, ex); }
							}
							_splendidCache.ClearDetailView(sViewName);
							break;
						}
						case "GRIDVIEWS_COLUMNS":
						{
							using ( IDbTransaction trn = Sql.BeginTransaction(con) )
							{
								try
								{
									UpdateLayoutTable(con, trn, sTableName, dict);
									trn.Commit();
								}
								catch(Exception ex) { trn.Rollback(); throw new Exception("Layout update failed; transaction aborted: " + ex.Message, ex); }
							}
							_splendidCache.ClearGridView(sViewName);
							break;
						}
						case "DETAILVIEWS_RELATIONSHIPS":
							UpdateAdminTableLayoutNameInternal(con, "DETAILVIEWS_RELATIONSHIPS", "DETAIL_NAME", sViewName, dict);
							_splendidCache.ClearDetailViewRelationships();
							break;
						case "EDITVIEWS_RELATIONSHIPS":
							UpdateAdminTableLayoutNameInternal(con, "EDITVIEWS_RELATIONSHIPS", "EDIT_NAME", sViewName, dict);
							_splendidCache.ClearEditViewRelationships();
							break;
						case "TERMINOLOGY":
							UpdateAdminTableLayoutNameInternal(con, "TERMINOLOGY", "MODULE_NAME", sViewName, dict);
							ReloadTerminologyInternal(sViewName);
							break;
						default:
							throw new Exception("Unsupported layout table: " + sTableName);
					}
				}
				return Ok(new { status = "updated" });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>Ported from SplendidCRM/Administration/Rest.svc.cs UpdateAdminTableLayoutName (lines 3635-3706).
		/// Iterates rows in dict[sTABLE_NAME], calling sp{TABLE}_Update for each with type-aware parameter binding.</summary>
		private void UpdateAdminTableLayoutNameInternal(IDbConnection con, string sTABLE_NAME, string sLAYOUT_NAME_FIELD, string sVIEW_NAME, Dictionary<string, object> dict)
		{
			using ( IDbTransaction trn = Sql.BeginTransaction(con) )
			{
				try
				{
					if ( dict.ContainsKey(sTABLE_NAME) )
					{
						IDbCommand cmdUpdate = SqlProcs.Factory(con, "sp" + sTABLE_NAME + "_Update");
						cmdUpdate.Transaction = trn;
						IDbDataParameter parMODIFIED_USER_ID = Sql.FindParameter(cmdUpdate, "@MODIFIED_USER_ID");

						// The body may be a JArray or ArrayList depending on deserialization.
						System.Collections.ArrayList lst = null;
						object raw = dict[sTABLE_NAME];
						if ( raw is System.Collections.ArrayList al )
							lst = al;
						else if ( raw is Newtonsoft.Json.Linq.JArray jArr )
						{
							lst = new System.Collections.ArrayList();
							foreach ( Newtonsoft.Json.Linq.JObject jObj in jArr )
								lst.Add(jObj.ToObject<Dictionary<string, object>>());
						}
						else if ( raw is List<object> oList )
						{
							lst = new System.Collections.ArrayList();
							foreach ( var o in oList )
							{
								if ( o is Newtonsoft.Json.Linq.JObject jObj2 )
									lst.Add(jObj2.ToObject<Dictionary<string, object>>());
								else if ( o is Dictionary<string, object> d )
									lst.Add(d);
							}
						}
						if ( lst != null )
						{
							for ( int i = 0; i < lst.Count; i++ )
							{
								foreach ( IDbDataParameter par in cmdUpdate.Parameters )
								{
									par.Value = DBNull.Value;
								}
								if ( parMODIFIED_USER_ID != null )
									parMODIFIED_USER_ID.Value = _security.USER_ID;

								Dictionary<string, object> dictRow = lst[i] as Dictionary<string, object>;
								if ( dictRow == null ) continue;
								Sql.SetParameter(cmdUpdate, sLAYOUT_NAME_FIELD, sVIEW_NAME);
								foreach ( string sFieldName in dictRow.Keys )
								{
									if ( sFieldName != sLAYOUT_NAME_FIELD && sFieldName != "MODIFIED_USER_ID" )
									{
										IDbDataParameter par = Sql.FindParameter(cmdUpdate, sFieldName);
										if ( par != null )
										{
											switch ( par.DbType )
											{
												case DbType.Guid    :  par.Value = Sql.ToDBGuid    (dictRow[sFieldName]);  break;
												case DbType.Int16   :  par.Value = Sql.ToDBInteger (dictRow[sFieldName]);  break;
												case DbType.Int32   :  par.Value = Sql.ToDBInteger (dictRow[sFieldName]);  break;
												case DbType.Int64   :  par.Value = Sql.ToDBInteger (dictRow[sFieldName]);  break;
												case DbType.Double  :  par.Value = Sql.ToDBFloat   (dictRow[sFieldName]);  break;
												case DbType.Decimal :  par.Value = Sql.ToDBDecimal (dictRow[sFieldName]);  break;
												case DbType.Byte    :  par.Value = Sql.ToDBBoolean (dictRow[sFieldName]);  break;
												case DbType.DateTime:  par.Value = Sql.ToDBDateTime(dictRow[sFieldName]);  break;
												default             :  par.Value = Sql.ToDBString  (dictRow[sFieldName]);  break;
											}
										}
									}
								}
								cmdUpdate.ExecuteNonQuery();
							}
						}
					}
					trn.Commit();
				}
				catch ( Exception ex )
				{
					trn.Rollback();
					throw new Exception("Failed to update, transaction aborted; " + ex.Message, ex);
				}
			}
		}

		/// <summary>Reloads terminology from the database for a specific module after layout save.
		/// Ported from SplendidCRM/Administration/Rest.svc.cs ReloadTerminology (lines 3317-3356).</summary>
		private void ReloadTerminologyInternal(string sMODULE_NAME)
		{
			try
			{
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					string sSQL;
					sSQL = "select NAME                " + ControlChars.CrLf
					     + "     , LANG                " + ControlChars.CrLf
					     + "     , MODULE_NAME         " + ControlChars.CrLf
					     + "     , DISPLAY_NAME        " + ControlChars.CrLf
					     + "  from vwTERMINOLOGY       " + ControlChars.CrLf
					     + " where LIST_NAME is null   " + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						if ( Sql.IsEmptyString(sMODULE_NAME) )
						{
							cmd.CommandText += "   and MODULE_NAME is null " + ControlChars.CrLf;
						}
						else
						{
							StringBuilder sbModule = new StringBuilder();
							Sql.AppendParameter(cmd, sbModule, "MODULE_NAME", sMODULE_NAME, Sql.SqlFilterMode.Exact);
							cmd.CommandText += sbModule.ToString();
						}
						using ( IDataReader rdr = cmd.ExecuteReader() )
						{
							while ( rdr.Read() )
							{
								string sLANG         = Sql.ToString(rdr["LANG"        ]);
								string sNAME         = Sql.ToString(rdr["NAME"        ]);
								string sDISPLAY_NAME = Sql.ToString(rdr["DISPLAY_NAME"]);
								L10N.SetTerm(_memoryCache, sLANG, sMODULE_NAME, sNAME, sDISPLAY_NAME);
							}
						}
					}
				}
			}
			catch ( Exception ex )
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
			}
		}

		/// <summary>
		/// POST Administration/Rest.svc/UpdateAdminModule
		/// Updates module properties (display name, description, tab order, etc.).
		/// Source: lines 4512-4562.
		/// </summary>
		[HttpPost("UpdateAdminModule")]
		public async System.Threading.Tasks.Task<IActionResult> UpdateAdminModule()
		{
			try
			{
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				string body = await ReadRequestBodyAsync();
				Dictionary<string, object> dict = DeserializeBody(body);

				string sModuleName = Sql.ToString(dict.ContainsKey("MODULE_NAME") ? dict["MODULE_NAME"] : null);
				if (Sql.IsEmptyString(sModuleName))
					return BadRequest(new { error = "MODULE_NAME is required" });

				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbTransaction trn = Sql.BeginTransaction(con);
				try
				{
					UpdateAdminTableInternal(con, trn, "MODULES", dict);
					trn.Commit();
				}
				catch
				{
					trn.Rollback();
					throw;
				}
				return Ok(new { status = "updated" });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// POST Administration/Rest.svc/ExportAdminModule
		/// Exports admin module data as CSV/Excel/XML. Source: lines 1501-1815.
		/// </summary>
		[HttpPost("ExportAdminModule")]
		public async System.Threading.Tasks.Task<IActionResult> ExportAdminModule()
		{
			try
			{
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				string body = await ReadRequestBodyAsync();
				Dictionary<string, object> dict = DeserializeBody(body);

				string sModuleName   = Sql.ToString(dict.ContainsKey("ModuleName"  ) ? dict["ModuleName"  ] : null);
				string sExportFormat = Sql.ToString(dict.ContainsKey("ExportFormat") ? dict["ExportFormat"] : "csv");
				string sExportRange  = Sql.ToString(dict.ContainsKey("ExportRange" ) ? dict["ExportRange" ] : "All");

				if (Sql.IsEmptyString(sModuleName))
					return BadRequest(new { error = "ModuleName is required" });

				// Validate module name to prevent injection
				if (!Regex.IsMatch(sModuleName, @"^[A-Za-z0-9_]+$"))
					return BadRequest(new { error = "Invalid ModuleName" });

				string sTableName = Crm.Modules.TableName(sModuleName);
				if (Sql.IsEmptyString(sTableName)) sTableName = sModuleName;

				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *         " + ControlChars.CrLf
				  + "  from vw" + sTableName + ControlChars.CrLf;
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);

				// Use SplendidExport to write to response stream
				DataView dv = dt.DefaultView;
				_splendidExport.Export(dv, sModuleName, sExportFormat, sExportRange, 0, int.MaxValue, null, false);

				// Response written by SplendidExport via IHttpContextAccessor
				return new EmptyResult();
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// POST Administration/Rest.svc/UpdateAdminConfig
		/// Updates one or more CONFIG entries in a single transaction with encryption support.
		/// Source: lines 4566-4677.
		/// </summary>
		[HttpPost("UpdateAdminConfig")]
		public async System.Threading.Tasks.Task<IActionResult> UpdateAdminConfig()
		{
			try
			{
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				// Config updates require full admin or admin delegate
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				string body = await ReadRequestBodyAsync();
				Dictionary<string, object> dict = DeserializeBody(body);

				// First pass: validate access rights for module-scoped keys
				foreach (string sKey in dict.Keys)
				{
					if (sKey.Contains("."))
					{
						string sModuleName = sKey.Split('.')[0];
						if (Sql.ToBoolean(_memoryCache.Get("Modules." + sModuleName + ".Valid")))
						{
							if (_security.AdminUserAccess(sModuleName, "edit") < 0)
								return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") + ": " + sKey });
						}
					}
					else if (!_security.IS_ADMIN)
					{
						return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") + ": " + sKey });
					}
				}

				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbTransaction trn = Sql.BeginTransaction(con);
				try
				{
					foreach (string sKey in dict.Keys)
					{
						string sValue = Sql.ToString(dict[sKey]);
						if (!Sql.IsEmptyString(sValue) && sValue != Sql.sEMPTY_PASSWORD)
						{
							// Encrypt password-type config values
							if (sKey.EndsWith(".Encrypted") || sKey == "Asterisk.Password" || sKey == "Avaya.Password"
							    || sKey == "smtppass" || sKey == "Exchange.Password")
							{
								Guid gKey = Sql.ToGuid(_memoryCache.Get<object>("CONFIG.InboundEmailKey"));
								Guid gIV  = Sql.ToGuid(_memoryCache.Get<object>("CONFIG.InboundEmailIV" ));
								sValue = Security.EncryptPassword(sValue, gKey, gIV);
							}
							else if (sKey == "AuthorizeNet.TransactionKey" || sKey == "Pardot.ApiPassword"
							      || sKey == "PayTrace.Password"          || sKey == "SalesFusion.Password")
							{
								Guid gKey = Sql.ToGuid(_memoryCache.Get<object>("CONFIG.CreditCardKey"));
								Guid gIV  = Sql.ToGuid(_memoryCache.Get<object>("CONFIG.CreditCardIV" ));
								sValue = Security.EncryptPassword(sValue, gKey, gIV);
							}
						}
						using IDbCommand cmd = SqlProcs.Factory(con, "spCONFIG_Update");
						Sql.AddParameter(cmd, "@CATEGORY", "System");
						Sql.AddParameter(cmd, "@NAME"    , sKey  );
						Sql.AddParameter(cmd, "@VALUE"   , sValue);
						cmd.Transaction = trn;
						cmd.ExecuteNonQuery();
					}
					trn.Commit();
					// Update cached config values after successful commit
					foreach (string sKey in dict.Keys)
					{
						string sValue = Sql.ToString(dict[sKey]);
						if (sValue != Sql.sEMPTY_PASSWORD)
							_memoryCache.Set("CONFIG." + sKey, (object)sValue, _splendidCache.DefaultCacheExpiration());
					}
				}
				catch (Exception ex)
				{
					trn.Rollback();
					throw new Exception("Config update failed; transaction aborted: " + ex.Message, ex);
				}
				return Ok(new { status = "updated" });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// POST Administration/Rest.svc/UpdateAdminTerminology
		/// Updates terminology entries (display names, labels) and reloads terminology cache.
		/// </summary>
		[HttpPost("UpdateAdminTerminology")]
		public async System.Threading.Tasks.Task<IActionResult> UpdateAdminTerminology()
		{
			try
			{
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				string body = await ReadRequestBodyAsync();
				Dictionary<string, object> dict = DeserializeBody(body);

				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbTransaction trn = Sql.BeginTransaction(con);
				try
				{
					// Get terminology entries from the dict
					var entries = new List<Dictionary<string, object>>();
					if (dict.ContainsKey("rows") && dict["rows"] is JArray jRows)
					{
						foreach (JObject jObj in jRows)
							entries.Add(jObj.ToObject<Dictionary<string, object>>());
					}
					else if (dict.ContainsKey("rows") && dict["rows"] is IEnumerable<object> oRows)
					{
						foreach (var o in oRows)
						{
							if (o is JObject j) entries.Add(j.ToObject<Dictionary<string, object>>());
						}
					}
					else
					{
						// Single entry
						entries.Add(dict);
					}

					foreach (var entry in entries)
					{
						string sLang        = Sql.ToString(entry.ContainsKey("LANG"        ) ? entry["LANG"        ] : null);
						string sModuleName  = Sql.ToString(entry.ContainsKey("MODULE_NAME" ) ? entry["MODULE_NAME" ] : null);
						string sName        = Sql.ToString(entry.ContainsKey("NAME"        ) ? entry["NAME"        ] : null);
						string sDisplayName = Sql.ToString(entry.ContainsKey("DISPLAY_NAME") ? entry["DISPLAY_NAME"] : null);
						if (!Sql.IsEmptyString(sLang) && !Sql.IsEmptyString(sName))
						{
							using IDbCommand cmd = SqlProcs.Factory(con, "spTERMINOLOGY_Update");
							Sql.AddParameter(cmd, "@LANG"        , sLang       );
							Sql.AddParameter(cmd, "@MODULE_NAME" , sModuleName );
							Sql.AddParameter(cmd, "@NAME"        , sName       );
							Sql.AddParameter(cmd, "@DISPLAY_NAME", sDisplayName);
							cmd.Transaction = trn;
							cmd.ExecuteNonQuery();
						}
					}
					trn.Commit();
					// Reload terminology cache
					_splendidCache.ClearLanguages();
					_splendidInit.InitTerminology();
				}
				catch (Exception ex)
				{
					trn.Rollback();
					throw new Exception("Terminology update failed; transaction aborted: " + ex.Message, ex);
				}
				return Ok(new { status = "updated" });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// POST Administration/Rest.svc/UpdateAdminField
		/// Updates custom field metadata (FIELDS_META_DATA) and triggers view recompilation.
		/// Source: lines 5564-5667 (UpdateAdminEditCustomField).
		/// </summary>
		[HttpPost("UpdateAdminField")]
		public async System.Threading.Tasks.Task<IActionResult> UpdateAdminField()
		{
			try
			{
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !_security.IS_ADMIN)
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				string body = await ReadRequestBodyAsync();
				Dictionary<string, object> dict = DeserializeBody(body);

				Guid   gID         = Sql.ToGuid  (dict.ContainsKey("ID"         ) ? dict["ID"         ] : null);
				string sModuleName = Sql.ToString (dict.ContainsKey("MODULE_NAME") ? dict["MODULE_NAME"] : null);
				string sFieldName  = Sql.ToString (dict.ContainsKey("FIELD_NAME" ) ? dict["FIELD_NAME" ] : null);
				string sFieldType  = Sql.ToString (dict.ContainsKey("FIELD_TYPE" ) ? dict["FIELD_TYPE" ] : null);
				string sLabel      = Sql.ToString (dict.ContainsKey("LABEL"      ) ? dict["LABEL"      ] : null);

				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbTransaction trn = Sql.BeginTransaction(con);
				try
				{
					using IDbCommand cmd = SqlProcs.Factory(con, "spFIELDS_META_DATA_Update");
					Sql.AddParameter(cmd, "@ID"         , gID       );
					Sql.AddParameter(cmd, "@MODULE_NAME", sModuleName);
					Sql.AddParameter(cmd, "@FIELD_NAME" , sFieldName );
					Sql.AddParameter(cmd, "@FIELD_TYPE" , sFieldType );
					Sql.AddParameter(cmd, "@LABEL"      , sLabel     );
					cmd.Transaction = trn;
					cmd.ExecuteNonQuery();
					trn.Commit();
				}
				catch
				{
					trn.Rollback();
					throw;
				}

				// Trigger background view recompilation per source pattern
				// MIGRATION: new Thread(RecompileViews) → ModuleUtils.EditCustomFields.RecompileViews static call
				Thread tRecompile = new Thread(() =>
				{
					try { ModuleUtils.EditCustomFields.RecompileViews(_memoryCache, "Community"); }
					catch (Exception ex) { SplendidError.SystemError(new StackFrame(1, true), ex); }
				});
				tRecompile.IsBackground = true;
				tRecompile.Start();

				return Ok(new { status = "updated", ID = gID });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// POST Administration/Rest.svc/UpdateAdminLayoutTable
		/// Updates a layout table (EDITVIEWS_FIELDS, DETAILVIEWS_FIELDS, etc.) with
		/// full row replacement. Source: lines 3596-3632 (UpdateAdminLayoutTable).
		/// </summary>
		[HttpPost("UpdateAdminLayoutTable")]
		public async System.Threading.Tasks.Task<IActionResult> UpdateAdminLayoutTable()
		{
			try
			{
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				string body = await ReadRequestBodyAsync();
				Dictionary<string, object> dict = DeserializeBody(body);

				string sTableName = Sql.ToString(dict.ContainsKey("TableName") ? dict["TableName"] : null);
				string sViewName  = Sql.ToString(dict.ContainsKey("ViewName" ) ? dict["ViewName" ] : null);
				if (Sql.IsEmptyString(sTableName))
					return BadRequest(new { error = "TableName is required" });

				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbTransaction trn = Sql.BeginTransaction(con);
				try
				{
					ClearLayoutTable(con, trn, sTableName, sViewName);
					UpdateLayoutTable(con, trn, sTableName, dict);
					trn.Commit();
					// Clear appropriate layout cache
					_splendidCache.ClearGridView(sViewName);
					_splendidCache.ClearDetailView(sViewName);
					_splendidCache.ClearEditView(sViewName);
				}
				catch (Exception ex)
				{
					trn.Rollback();
					throw new Exception("Layout table update failed; transaction aborted: " + ex.Message, ex);
				}
				return Ok(new { status = "updated" });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// POST Administration/Rest.svc/DeleteAdminLayoutField
		/// Deletes a single layout field from a layout table.
		/// Source: lines 5046-5074 (DeleteAdminLayout).
		/// </summary>
		[HttpPost("DeleteAdminLayoutField")]
		public async System.Threading.Tasks.Task<IActionResult> DeleteAdminLayoutField()
		{
			try
			{
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				string body = await ReadRequestBodyAsync();
				Dictionary<string, object> dict = DeserializeBody(body);

				Guid   gID        = Sql.ToGuid  (dict.ContainsKey("ID"       ) ? dict["ID"       ] : null);
				string sTableName = Sql.ToString (dict.ContainsKey("TableName") ? dict["TableName"] : null);
				if (Sql.IsEmptyGuid(gID) || Sql.IsEmptyString(sTableName))
					return BadRequest(new { error = "ID and TableName are required" });

				if (!Regex.IsMatch(sTableName, @"^[A-Za-z0-9_]+$"))
					return BadRequest(new { error = "Invalid TableName" });

				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText = "delete from " + sTableName + " where ID = @ID";
				Sql.AddParameter(cmd, "@ID", gID);
				cmd.ExecuteNonQuery();

				return Ok(new { status = "deleted" });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// POST Administration/Rest.svc/DeleteAdminConfig
		/// Deletes a CONFIG entry. Source: from DeleteAdminTableItems targeting CONFIG.
		/// </summary>
		[HttpPost("DeleteAdminConfig")]
		public async System.Threading.Tasks.Task<IActionResult> DeleteAdminConfig()
		{
			try
			{
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !_security.IS_ADMIN)
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				string body = await ReadRequestBodyAsync();
				Dictionary<string, object> dict = DeserializeBody(body);
				string sName = Sql.ToString(dict.ContainsKey("NAME") ? dict["NAME"] : null);
				if (Sql.IsEmptyString(sName))
					return BadRequest(new { error = "NAME is required" });

				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText = "delete from CONFIG where NAME = @NAME";
				Sql.AddParameter(cmd, "@NAME", sName);
				cmd.ExecuteNonQuery();
				// Remove from memory cache
				_memoryCache.Remove("CONFIG." + sName);
				return Ok(new { status = "deleted" });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// POST Administration/Rest.svc/DeleteAdminTerminology
		/// Deletes a TERMINOLOGY entry and reloads terminology cache.
		/// </summary>
		[HttpPost("DeleteAdminTerminology")]
		public async System.Threading.Tasks.Task<IActionResult> DeleteAdminTerminology()
		{
			try
			{
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !_security.IS_ADMIN)
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				string body = await ReadRequestBodyAsync();
				Dictionary<string, object> dict = DeserializeBody(body);
				Guid gID = Sql.ToGuid(dict.ContainsKey("ID") ? dict["ID"] : null);
				if (Sql.IsEmptyGuid(gID))
					return BadRequest(new { error = "ID is required" });

				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText = "delete from TERMINOLOGY where ID = @ID";
				Sql.AddParameter(cmd, "@ID", gID);
				cmd.ExecuteNonQuery();
				_splendidCache.ClearLanguages();
				_splendidInit.InitTerminology();
				return Ok(new { status = "deleted" });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		// ==================================================================
		// #region ACL Update Operations (POST)
		// ==================================================================

		/// <summary>
		/// POST Administration/Rest.svc/UpdateAclAccess
		/// Updates ACL role module access rights (8 access types per module).
		/// Source: lines 5922-6240.
		/// </summary>
		[HttpPost("UpdateAclAccess")]
		public async System.Threading.Tasks.Task<IActionResult> UpdateAclAccess()
		{
			try
			{
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !_security.IS_ADMIN)
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				string body = await ReadRequestBodyAsync();
				Dictionary<string, object> dict = DeserializeBody(body);

				Guid gROLE_ID    = Sql.ToGuid  (dict.ContainsKey("ROLE_ID"   ) ? dict["ROLE_ID"   ] : null);
				bool bAllowAdmin = Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.allow_admin_roles"));

				if (Sql.IsEmptyGuid(gROLE_ID))
					return BadRequest(new { error = "ROLE_ID is required" });

				var entries = new List<Dictionary<string, object>>();
				if (dict.ContainsKey("rows") && dict["rows"] is JArray jArr)
					foreach (JObject j in jArr)
						entries.Add(j.ToObject<Dictionary<string, object>>());
				else if (dict.ContainsKey("rows") && dict["rows"] is IEnumerable<object> oRows)
					foreach (var o in oRows)
						if (o is JObject j2) entries.Add(j2.ToObject<Dictionary<string, object>>());
				else
					entries.Add(dict);

				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbTransaction trn = Sql.BeginTransaction(con);
				try
				{
					foreach (var entry in entries)
					{
						string sModuleName = Sql.ToString(entry.ContainsKey("MODULE_NAME") ? entry["MODULE_NAME"] : null);
						if (Sql.IsEmptyString(sModuleName)) continue;

						// 8 access types: access, view, list, edit, delete, import, export, archive
						string[] arrTypes = { "access", "view", "list", "edit", "delete", "import", "export", "archive" };
						foreach (string sType in arrTypes)
						{
							string sTypeKey = sType.ToUpper();
							if (entry.ContainsKey(sTypeKey))
							{
								int nAccess = Sql.ToInteger(entry[sTypeKey]);
								using IDbCommand cmd = SqlProcs.Factory(con, "spACL_ROLES_ACTIONS_Update");
								Sql.AddParameter(cmd, "@ROLE_ID"    , gROLE_ID    );
								Sql.AddParameter(cmd, "@MODULE_NAME", sModuleName );
								Sql.AddParameter(cmd, "@ACTION_NAME", sType       );
								Sql.AddParameter(cmd, "@ACCESS"     , nAccess     );
								cmd.Transaction = trn;
								cmd.ExecuteNonQuery();
							}
						}
					}
					trn.Commit();
					// Refresh user ACL
					_splendidInit.ClearUserACL();
					_splendidInit.LoadUserACL(_security.USER_ID);
				}
				catch (Exception ex)
				{
					trn.Rollback();
					throw new Exception("ACL access update failed; transaction aborted: " + ex.Message, ex);
				}
				return Ok(new { status = "updated" });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// POST Administration/Rest.svc/UpdateAclFieldAccess
		/// Updates field-level ACL permissions for a role/module.
		/// Source: lines 6314-6433 (UpdateAclAccessFieldSecurity).
		/// </summary>
		[HttpPost("UpdateAclFieldAccess")]
		public async System.Threading.Tasks.Task<IActionResult> UpdateAclFieldAccess()
		{
			try
			{
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !_security.IS_ADMIN)
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				string body = await ReadRequestBodyAsync();
				Dictionary<string, object> dict = DeserializeBody(body);

				Guid   gROLE_ID    = Sql.ToGuid  (dict.ContainsKey("ROLE_ID"    ) ? dict["ROLE_ID"    ] : null);
				string sModuleName = Sql.ToString (dict.ContainsKey("MODULE_NAME") ? dict["MODULE_NAME"] : null);
				if (Sql.IsEmptyGuid(gROLE_ID) || Sql.IsEmptyString(sModuleName))
					return BadRequest(new { error = "ROLE_ID and MODULE_NAME are required" });

				var fields = new List<Dictionary<string, object>>();
				if (dict.ContainsKey("fields") && dict["fields"] is JArray jArr)
					foreach (JObject j in jArr)
						fields.Add(j.ToObject<Dictionary<string, object>>());
				else if (dict.ContainsKey("fields") && dict["fields"] is IEnumerable<object> oArr)
					foreach (var o in oArr)
						if (o is JObject j2) fields.Add(j2.ToObject<Dictionary<string, object>>());

				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbTransaction trn = Sql.BeginTransaction(con);
				try
				{
					foreach (var field in fields)
					{
						Guid   gID         = Sql.ToGuid  (field.ContainsKey("ID"          ) ? field["ID"          ] : null);
						string sFieldName  = Sql.ToString (field.ContainsKey("FIELD_NAME"  ) ? field["FIELD_NAME"  ] : null);
						int    nReadAccess = Sql.ToInteger(field.ContainsKey("READ_ACCESS" ) ? field["READ_ACCESS" ] : -99 );
						int    nEditAccess = Sql.ToInteger(field.ContainsKey("EDIT_ACCESS" ) ? field["EDIT_ACCESS" ] : -99 );
						if (!Sql.IsEmptyString(sFieldName))
						{
							// Use SqlProcs.Factory for spACL_FIELDS_Update since no typed wrapper exists
							using IDbCommand cmd = SqlProcs.Factory(con, "spACL_FIELDS_Update");
							Sql.AddParameter(cmd, "@ID"         , gID       );
							Sql.AddParameter(cmd, "@ROLE_ID"    , gROLE_ID  );
							Sql.AddParameter(cmd, "@MODULE_NAME", sModuleName);
							Sql.AddParameter(cmd, "@FIELD_NAME" , sFieldName );
							Sql.AddParameter(cmd, "@READ_ACCESS", nReadAccess);
							Sql.AddParameter(cmd, "@EDIT_ACCESS", nEditAccess);
							cmd.Transaction = trn;
							cmd.ExecuteNonQuery();
						}
					}
					trn.Commit();
					_splendidInit.ClearUserACL();
					_splendidInit.LoadUserACL(_security.USER_ID);
				}
				catch (Exception ex)
				{
					trn.Rollback();
					throw new Exception("ACL field access update failed; transaction aborted: " + ex.Message, ex);
				}
				return Ok(new { status = "updated" });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// GET Administration/Rest.svc/GetReactMenu
		/// Returns admin tab menu for ConfigureTabs page.
		/// Clears and regenerates tab menu session cache. Source: lines 3207-3237.
		/// </summary>
		[HttpGet("GetReactMenu")]
		public IActionResult GetReactMenu()
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				_httpContextAccessor.HttpContext?.Session.Remove("vwMODULES.TabMenu.ReactClient");
				var TAB_MENU = _splendidCache.GetAllTabMenus();
				var results = new Dictionary<string, object>();
				results["TAB_MENU"] = TAB_MENU;
				return JsonContent(new { d = results });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// GET Administration/Rest.svc/GetRecompileStatus
		/// Returns current recompile progress from memory cache. Source: lines 1140-1176.
		/// </summary>
		[HttpGet("GetRecompileStatus")]
		public IActionResult GetRecompileStatus()
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !_security.IS_ADMIN)
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });
				var status = _memoryCache.Get<RecompileStatus>("System.Recompile.Status");
				if (status == null)
					status = new RecompileStatus { Restart = false, CurrentPass = 0, TotalPasses = 0, CurrentView = 0, TotalViews = 0, ElapseSeconds = 0, RemainingSeconds = 0 };
				return JsonContent(new { d = status });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// POST Administration/Rest.svc/RecompileViews
		/// Triggers background recompile of all SQL views. Source: lines 1178-1199.
		/// </summary>
		[HttpPost("RecompileViews")]
		public IActionResult RecompileViews()
		{
			try
			{
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !_security.IS_ADMIN)
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });
				string sServiceLevel = Sql.ToString(_memoryCache.Get<object>("CONFIG.service_level"));
				if (Sql.IsEmptyString(sServiceLevel)) sServiceLevel = "Community";
				Thread t = new Thread(() => ModuleUtils.EditCustomFields.RecompileViews(_memoryCache, sServiceLevel));
				t.IsBackground = true;
				t.Start();
				return Ok(new { status = "started" });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// POST Administration/Rest.svc/RebuildAudit
		/// Triggers background rebuild of audit tables. Source: lines 1201-1221.
		/// </summary>
		[HttpPost("RebuildAudit")]
		public IActionResult RebuildAudit()
		{
			try
			{
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !_security.IS_ADMIN)
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });
				Thread t = new Thread(() =>
				{
					try
					{
						using IDbConnection con = _dbProviderFactories.CreateConnection();
						con.Open();
						using IDbCommand cmd = SqlProcs.Factory(con, "spAudit_Rebuild");
						cmd.ExecuteNonQuery();
					}
					catch (Exception ex) { SplendidError.SystemError(new StackFrame(1, true), ex); }
				});
				t.IsBackground = true;
				t.Start();
				return Ok(new { status = "started" });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// POST Administration/Rest.svc/BuildModuleArchive
		/// Builds an archive of a module's data. Source: lines 1223-1262.
		/// </summary>
		[HttpPost("BuildModuleArchive")]
		public async System.Threading.Tasks.Task<IActionResult> BuildModuleArchive()
		{
			try
			{
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !_security.IS_ADMIN)
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });
				string body = await ReadRequestBodyAsync();
				Dictionary<string, object> dict = DeserializeBody(body);
				Guid gID = Sql.ToGuid(dict.ContainsKey("ID") ? dict["ID"] : null);
				if ( Sql.IsEmptyGuid(gID) )
					return BadRequest(new { error = "ID is required" });
				// Ported from SplendidCRM/Administration/Rest.svc.cs line 1246.
				// SP is spMODULES_ArchiveBuild (NOT spMODULE_ARCHIVE_Build).
				// Parameter is @ID (Guid), not @MODULE_NAME (string).
				Guid gUSER_ID = _security.USER_ID;
				Thread t = new Thread(() =>
				{
					try
					{
						using IDbConnection con = _dbProviderFactories.CreateConnection();
						con.Open();
						using ( IDbTransaction trn = Sql.BeginTransaction(con) )
						{
							try
							{
								using ( IDbCommand cmd = con.CreateCommand() )
								{
									cmd.Transaction    = trn;
									cmd.CommandType    = CommandType.StoredProcedure;
									cmd.CommandText    = "spMODULES_ArchiveBuild";
									cmd.CommandTimeout = 0;
									Sql.AddParameter(cmd, "@ID"              , gID      );
									Sql.AddParameter(cmd, "@MODIFIED_USER_ID", gUSER_ID );
									cmd.ExecuteNonQuery();
								}
								trn.Commit();
							}
							catch { trn.Rollback(); throw; }
						}
					}
					catch (Exception ex) { SplendidError.SystemError(new StackFrame(1, true), ex); }
				});
				t.IsBackground = true;
				t.Start();
				return Ok(new { status = "started" });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// POST Administration/Rest.svc/PostAdminTable
		/// Generic admin table READ/query operation with POST body search values.
		/// Ported from SplendidCRM/Administration/Rest.svc.cs lines 1265-1500.
		/// This is a READ operation, NOT a write. Uses RestUtil.GetAdminTable().
		/// </summary>
		[HttpPost("PostAdminTable")]
		public async System.Threading.Tasks.Task<IActionResult> PostAdminTable()
		{
			try
			{
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				string body = await ReadRequestBodyAsync();
				Dictionary<string, object> dict = DeserializeBody(body);

				string TableName = Sql.ToString(Request.Query["TableName"].FirstOrDefault());
				int    nSKIP     = Sql.ToInteger(Request.Query["$skip"   ].FirstOrDefault());
				int    nTOP      = Sql.ToInteger(Request.Query["$top"    ].FirstOrDefault());
				string sFILTER   = Sql.ToString (Request.Query["$filter" ].FirstOrDefault());
				string sORDER_BY = Sql.ToString (Request.Query["$orderby"].FirstOrDefault());
				string sGROUP_BY = Sql.ToString (Request.Query["$groupby"].FirstOrDefault());
				string sSELECT   = Sql.ToString (Request.Query["$select" ].FirstOrDefault());
				long lTotalCount = 0;

				SplendidCRM.TimeZone T10n = GetUserTimezone();
				Dictionary<string, object> dictSearchValues = null;
				foreach ( string sName in dict.Keys )
				{
					switch ( sName )
					{
						case "TableName"     : TableName        = Sql.ToString (dict[sName]); break;
						case "$skip"         : nSKIP            = Sql.ToInteger(dict[sName]); break;
						case "$top"          : nTOP             = Sql.ToInteger(dict[sName]); break;
						case "$filter"       : sFILTER          = Sql.ToString (dict[sName]); break;
						case "$orderby"      : sORDER_BY        = Sql.ToString (dict[sName]); break;
						case "$groupby"      : sGROUP_BY        = Sql.ToString (dict[sName]); break;
						case "$select"       : sSELECT          = Sql.ToString (dict[sName]); break;
						case "$searchvalues" : dictSearchValues  = dict[sName] as Dictionary<string, object>; break;
					}
				}
				// Search values are handled by RestUtil.GetAdminTable internally via filter.
				// The legacy code also passes search values as part of the filter string.
				if ( dictSearchValues != null )
				{
					// Build search clause using IDbCommand context — handled in GetAdminTable.
					// Pass search values as a serialized filter string.
					StringBuilder sbSearch = new StringBuilder();
					foreach ( string sKey in dictSearchValues.Keys )
					{
						string sValue = Sql.ToString(dictSearchValues[sKey]);
						if ( !Sql.IsEmptyString(sValue) )
						{
							string sFieldName = new Regex(@"[^A-Za-z0-9_]").Replace(sKey, "");
							if ( !Sql.IsEmptyString(sFieldName) )
								sbSearch.Append(" and " + sFieldName + " like '%" + Sql.EscapeSQL(sValue) + "%'");
						}
					}
					if ( sbSearch.Length > 0 )
					{
						if ( !Sql.IsEmptyString(sFILTER) )
							sFILTER = sFILTER + sbSearch.ToString();
						else
							sFILTER = "1 = 1" + sbSearch.ToString();
					}
				}

				Regex r = new Regex(@"[^A-Za-z0-9_]");
				string sFILTER_KEYWORDS = Sql.SqlFilterLiterals(sFILTER);
				sFILTER_KEYWORDS = (" " + r.Replace(sFILTER_KEYWORDS, " ") + " ").ToLower();
				if ( sFILTER_KEYWORDS.IndexOf(" select ") >= 0 && sFILTER_KEYWORDS.IndexOf(" from ") > sFILTER_KEYWORDS.IndexOf(" select ") )
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

				StringBuilder sbDumpSQL = new StringBuilder();
				DataTable dt = new DataTable();
				string sMODULE_NAME = Sql.ToString(_memoryCache.Get("Modules." + TableName + ".ModuleName"));
				if ( !Sql.IsEmptyString(sMODULE_NAME) && !TableName.StartsWith("OAUTH") && !TableName.StartsWith("USERS_PASSWORD") && !TableName.EndsWith("_AUDIT") && !TableName.EndsWith("_STREAM") )
				{
					bool bIsAdmin = Sql.ToBoolean(_memoryCache.Get("Modules." + sMODULE_NAME + ".IsAdmin"));
					if ( bIsAdmin && _security.AdminUserAccess(sMODULE_NAME, "access") >= 0 )
					{
						using ( DataTable dtSYNC_TABLES = _splendidCache.RestTables(TableName, false) )
						{
							if ( dtSYNC_TABLES != null && dtSYNC_TABLES.Rows.Count > 0 )
							{
								dt = _restUtil.GetAdminTable(HttpContext, TableName, nSKIP, nTOP, sFILTER, sORDER_BY, sGROUP_BY, arrSELECT, null, ref lTotalCount, null, AccessMode.list, sbDumpSQL);
							}
							else
							{
								throw new Exception("Unsupported table: " + TableName);
							}
						}
					}
					else
					{
						throw new Exception(L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS"));
					}
				}
				else
				{
					throw new Exception("Unsupported table: " + TableName);
				}

				var req = HttpContext.Request;
				string sBaseURI = req.Scheme + "://" + req.Host.Value + req.PathBase.Value + "/Administration/Rest.svc/PostAdminTable";
				Dictionary<string, object> dictResponse = _restUtil.ToJson(sBaseURI, TableName, dt, T10n);
				dictResponse.Add("__total", lTotalCount);
				if ( Sql.ToBoolean(_memoryCache.Get("CONFIG.show_sql")) )
				{
					dictResponse.Add("__sql", sbDumpSQL.ToString());
				}
				return JsonContent(new { d = dictResponse });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// POST Administration/Rest.svc/DeleteAdminModuleItem
		/// Deletes a single record from an admin module table. Source: lines 5144-5167.
		/// </summary>
		[HttpPost("DeleteAdminModuleItem")]
		public async System.Threading.Tasks.Task<IActionResult> DeleteAdminModuleItem()
		{
			try
			{
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });
				string body = await ReadRequestBodyAsync();
				Dictionary<string, object> dict = DeserializeBody(body);
				string sModuleName = Sql.ToString(dict.ContainsKey("MODULE_NAME") ? dict["MODULE_NAME"] : null);
				Guid   gID         = Sql.ToGuid  (dict.ContainsKey("ID"         ) ? dict["ID"         ] : null);
				if (Sql.IsEmptyString(sModuleName) || Sql.IsEmptyGuid(gID))
					return BadRequest(new { error = "MODULE_NAME and ID are required" });
				string sTableName = Crm.Modules.TableName(_memoryCache, sModuleName);
				if (Sql.IsEmptyString(sTableName)) sTableName = sModuleName;
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = SqlProcs.Factory(con, "sp" + sTableName + "_Delete");
				Sql.AddParameter(cmd, "@ID"       , gID            );
				Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security.USER_ID);
				cmd.ExecuteNonQuery();
				return Ok(new { status = "deleted", id = gID });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// POST Administration/Rest.svc/MassUpdateAdminModule
		/// Updates multiple records in an admin module table. Source: lines 4124-4510.
		/// </summary>
		[HttpPost("MassUpdateAdminModule")]
		public async System.Threading.Tasks.Task<IActionResult> MassUpdateAdminModule()
		{
			try
			{
				// Ported from SplendidCRM/Administration/Rest.svc.cs lines 4122-4510.
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				// Legacy reads ModuleName from query string.
				string sModuleName = HttpContext.Request.Query.ContainsKey("ModuleName")
					? Sql.ToString(HttpContext.Request.Query["ModuleName"])
					: String.Empty;
				if ( Sql.IsEmptyString(sModuleName) )
					return BadRequest(new { error = "The module name must be specified." });
				int nACLACCESS = _security.AdminUserAccess(sModuleName, "edit");
				if ( !_security.IsAuthenticated() || !Sql.ToBoolean(_memoryCache.Get<object>("Modules." + sModuleName + ".RestEnabled")) || nACLACCESS < 0 )
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") + ": " + sModuleName });

				string sTABLE_NAME = Sql.ToString(_memoryCache.Get<object>("Modules." + sModuleName + ".TableName"));
				if ( Sql.IsEmptyString(sTABLE_NAME) )
					throw new Exception("Unknown module: " + sModuleName);

				string body = await ReadRequestBodyAsync();
				Dictionary<string, object> dict = DeserializeBody(body);
				Guid     gTIMEZONE = Sql.ToGuid(_httpContextAccessor.HttpContext?.Session.GetString("USER_SETTINGS/TIMEZONE"));
				SplendidCRM.TimeZone T10n = SplendidCRM.TimeZone.CreateTimeZone(gTIMEZONE);

				// Build column set, filtering empty values.
				DataTable dtUPDATE = new DataTable(sTABLE_NAME);
				foreach ( string sColumnName in dict.Keys )
				{
					if ( sColumnName != "ID" && sColumnName != "ID_LIST" )
					{
						if ( dict[sColumnName] is System.Collections.ArrayList lst )
						{
							if ( lst.Count > 0 ) dtUPDATE.Columns.Add(sColumnName.ToUpper());
						}
						else if ( dict[sColumnName] is Newtonsoft.Json.Linq.JArray jArr )
						{
							if ( jArr.Count > 0 ) dtUPDATE.Columns.Add(sColumnName.ToUpper());
						}
						else
						{
							if ( !Sql.IsEmptyString(dict[sColumnName]) ) dtUPDATE.Columns.Add(sColumnName.ToUpper());
						}
					}
				}
				List<Guid> arrID_LIST = new List<Guid>();
				DataRow row = dtUPDATE.NewRow();
				dtUPDATE.Rows.Add(row);
				foreach ( string sColumnName in dict.Keys )
				{
					if ( dict[sColumnName] is System.Collections.ArrayList lst2 )
					{
						if ( sColumnName == "ID_LIST" )
						{
							foreach ( object item in lst2 ) arrID_LIST.Add(Sql.ToGuid(item));
						}
						else if ( dtUPDATE.Columns.Contains(sColumnName) )
						{
							System.Xml.XmlDocument xml = new System.Xml.XmlDocument();
							xml.AppendChild(xml.CreateXmlDeclaration("1.0", "UTF-8", null));
							xml.AppendChild(xml.CreateElement("Values"));
							foreach ( object item in lst2 )
							{
								System.Xml.XmlNode xValue = xml.CreateElement("Value");
								xml.DocumentElement.AppendChild(xValue);
								xValue.InnerText = Sql.ToString(item);
							}
							row[sColumnName] = xml.OuterXml;
						}
					}
					else if ( dict[sColumnName] is Newtonsoft.Json.Linq.JArray jArr2 )
					{
						if ( sColumnName == "ID_LIST" )
						{
							foreach ( var jt in jArr2 ) arrID_LIST.Add(Sql.ToGuid(jt.ToString()));
						}
						else if ( dtUPDATE.Columns.Contains(sColumnName) )
						{
							System.Xml.XmlDocument xml = new System.Xml.XmlDocument();
							xml.AppendChild(xml.CreateXmlDeclaration("1.0", "UTF-8", null));
							xml.AppendChild(xml.CreateElement("Values"));
							foreach ( var jt in jArr2 )
							{
								System.Xml.XmlNode xValue = xml.CreateElement("Value");
								xml.DocumentElement.AppendChild(xValue);
								xValue.InnerText = jt.ToString();
							}
							row[sColumnName] = xml.OuterXml;
						}
					}
					else if ( sColumnName != "ID" && sColumnName != "ID_LIST" )
					{
						if ( dtUPDATE.Columns.Contains(sColumnName) )
							row[sColumnName] = dict[sColumnName];
					}
				}
				if ( arrID_LIST.Count == 0 )
					throw new Exception(L10n.Term(".LBL_LISTVIEW_NO_SELECTED"));

				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					using ( DataTable dtSYNC_TABLES = _splendidCache.RestTables(sTABLE_NAME, false) )
					{
						if ( dtSYNC_TABLES != null && dtSYNC_TABLES.Rows.Count > 0 )
						{
							DataRow rowSYNC_TABLE = dtSYNC_TABLES.Rows[0];
							string sMODULE_NAME = Sql.ToString (rowSYNC_TABLE["MODULE_NAME"]);
							string sVIEW_NAME   = Sql.ToString (rowSYNC_TABLE["VIEW_NAME"  ]);
							bool   bHAS_CUSTOM  = Sql.ToBoolean(rowSYNC_TABLE["HAS_CUSTOM" ]);
							if ( Sql.IsEmptyString(sMODULE_NAME) )
								throw new Exception("sMODULE_NAME should not be empty for table " + sTABLE_NAME);

							DataTable dtCustomFields = _splendidCache.FieldsMetaData_Validated(sTABLE_NAME);
							using ( IDbTransaction trn = Sql.BeginTransaction(con) )
							{
								try
								{
									IDbCommand cmdUpdate = SqlProcs.Factory(con, "sp" + sTABLE_NAME + "_Update");
									cmdUpdate.Transaction = trn;
									cmdUpdate.CommandTimeout = 60 * 60;
									foreach ( Guid gID in arrID_LIST )
									{
										bool   bRecordExists   = false;
										bool   bAccessAllowed  = false;
										Guid   gLOCAL_ASSIGNED = Guid.Empty;
										DataRow rowCurrent      = null;
										DataTable dtCurrent     = new DataTable();
										string sSQL = "select *"             + ControlChars.CrLf
										            + "  from " + sVIEW_NAME + ControlChars.CrLf
										            + " where 1 = 1"         + ControlChars.CrLf;
										using ( IDbCommand cmd = con.CreateCommand() )
										{
											cmd.CommandText = sSQL;
											cmd.Transaction = trn;
											StringBuilder sbWhere1 = new StringBuilder();
											Sql.AppendParameter(cmd, sbWhere1, "ID", gID);
											cmd.CommandText += sbWhere1.ToString();
											using ( var da = dbf.CreateDataAdapter() )
											{
												((IDbDataAdapter)da).SelectCommand = cmd;
												da.Fill(dtCurrent);
												if ( dtCurrent.Rows.Count > 0 )
												{
													rowCurrent = dtCurrent.Rows[0];
													bRecordExists = true;
													if ( dtCurrent.Columns.Contains("ASSIGNED_USER_ID") )
														gLOCAL_ASSIGNED = Sql.ToGuid(rowCurrent["ASSIGNED_USER_ID"]);
												}
											}
										}
										if ( bRecordExists )
										{
											sSQL = "select count(*)"       + ControlChars.CrLf
											     + "  from " + sVIEW_NAME  + ControlChars.CrLf;
											using ( IDbCommand cmd = con.CreateCommand() )
											{
												cmd.CommandText = sSQL;
												cmd.Transaction = trn;
												_security.Filter(cmd, sMODULE_NAME, "edit");
												StringBuilder sbWhere2 = new StringBuilder();
												Sql.AppendParameter(cmd, sbWhere2, "ID", gID);
												cmd.CommandText += sbWhere2.ToString();
												if ( Sql.ToInteger(cmd.ExecuteScalar()) > 0 )
												{
													if ( (nACLACCESS > ACL_ACCESS.OWNER) || (nACLACCESS == ACL_ACCESS.OWNER && _security.USER_ID == gLOCAL_ASSIGNED) || !dtCurrent.Columns.Contains("ASSIGNED_USER_ID") )
														bAccessAllowed = true;
												}
											}
										}
										if ( bRecordExists && bAccessAllowed )
										{
											// Handle TEAM_SET_ADD/TAG_SET_ADD/ASSIGNED_SET_ADD/NAICS_SET_ADD.
											string sASSIGNED_SET_LIST = rowCurrent.Table.Columns.Contains("ASSIGNED_SET_LIST") ? Sql.ToString(rowCurrent["ASSIGNED_SET_LIST"]) : String.Empty;
											string sTEAM_SET_LIST     = rowCurrent.Table.Columns.Contains("TEAM_SET_LIST"    ) ? Sql.ToString(rowCurrent["TEAM_SET_LIST"    ]) : String.Empty;
											string sTAG_SET_NAME      = rowCurrent.Table.Columns.Contains("TAG_SET_NAME"     ) ? Sql.ToString(rowCurrent["TAG_SET_NAME"     ]) : String.Empty;
											string sNAICS_SET_NAME    = rowCurrent.Table.Columns.Contains("NAICS_SET_NAME"   ) ? Sql.ToString(rowCurrent["NAICS_SET_NAME"   ]) : String.Empty;
											if ( row.Table.Columns.Contains("ASSIGNED_SET_LIST") && !Sql.IsEmptyString(row["ASSIGNED_SET_LIST"]) )
											{
												if ( row.Table.Columns.Contains("ASSIGNED_SET_ADD") && Sql.ToBoolean(row["ASSIGNED_SET_ADD"]) )
												{ if ( !Sql.IsEmptyString(sASSIGNED_SET_LIST) ) sASSIGNED_SET_LIST += ","; sASSIGNED_SET_LIST += Sql.ToString(row["ASSIGNED_SET_LIST"]); }
												else sASSIGNED_SET_LIST = Sql.ToString(row["ASSIGNED_SET_LIST"]);
											}
											if ( row.Table.Columns.Contains("TEAM_SET_LIST") && !Sql.IsEmptyString(row["TEAM_SET_LIST"]) )
											{
												if ( row.Table.Columns.Contains("TEAM_SET_ADD") && Sql.ToBoolean(row["TEAM_SET_ADD"]) )
												{ if ( !Sql.IsEmptyString(sTEAM_SET_LIST) ) sTEAM_SET_LIST += ","; sTEAM_SET_LIST += Sql.ToString(row["TEAM_SET_LIST"]); }
												else sTEAM_SET_LIST = Sql.ToString(row["TEAM_SET_LIST"]);
											}
											if ( row.Table.Columns.Contains("TAG_SET_NAME") && !Sql.IsEmptyString(row["TAG_SET_NAME"]) )
											{
												if ( row.Table.Columns.Contains("TAG_SET_ADD") && Sql.ToBoolean(row["TAG_SET_ADD"]) )
												{ if ( !Sql.IsEmptyString(sTAG_SET_NAME) ) sTAG_SET_NAME += ","; sTAG_SET_NAME += Sql.ToString(row["TAG_SET_NAME"]); }
												else sTAG_SET_NAME = Sql.ToString(row["TAG_SET_NAME"]);
											}
											if ( row.Table.Columns.Contains("NAICS_SET_NAME") && !Sql.IsEmptyString(row["NAICS_SET_NAME"]) )
											{
												if ( row.Table.Columns.Contains("ADD_NAICS_CODE_SET") && Sql.ToBoolean(row["ADD_NAICS_CODE_SET"]) )
												{ if ( !Sql.IsEmptyString(sNAICS_SET_NAME) ) sNAICS_SET_NAME += ","; sNAICS_SET_NAME += Sql.ToString(row["NAICS_SET_NAME"]); }
												else sNAICS_SET_NAME = Sql.ToString(row["NAICS_SET_NAME"]);
											}
											// Initialize parameters: first null all, set ID and MODIFIED_USER_ID.
											foreach ( IDbDataParameter par in cmdUpdate.Parameters )
											{
												string sParameterName = par.ParameterName.TrimStart('@').ToUpper();
												if ( sParameterName == "ID" )
													par.Value = gID;
												else if ( sParameterName == "MODIFIED_USER_ID" )
													par.Value = Sql.ToDBGuid(_security.USER_ID);
												else
													par.Value = DBNull.Value;
											}
											// Load current record values into parameters.
											foreach ( DataColumn col in rowCurrent.Table.Columns )
											{
												IDbDataParameter par = Sql.FindParameter(cmdUpdate, col.ColumnName);
												if ( par != null && String.Compare(col.ColumnName, "MODIFIED_USER_ID", true) != 0 && String.Compare(col.ColumnName, "DATE_MODIFIED_UTC", true) != 0 )
													par.Value = rowCurrent[col.ColumnName];
											}
											// Overlay mass update values with ACL field security.
											foreach ( DataColumn col in row.Table.Columns )
											{
												bool bIsWriteable = true;
												if ( SplendidInit.bEnableACLFieldSecurity && !Sql.IsEmptyString(sMODULE_NAME) )
												{
													Security.ACL_FIELD_ACCESS acl = _security.GetUserFieldSecurity(sMODULE_NAME, col.ColumnName, Guid.Empty);
													bIsWriteable = acl.IsWriteable();
												}
												if ( bIsWriteable )
												{
													IDbDataParameter par = Sql.FindParameter(cmdUpdate, col.ColumnName);
													if ( par != null && row[col.ColumnName] != null && row[col.ColumnName] != DBNull.Value && !Sql.IsEmptyString(row[col.ColumnName]) )
													{
														if      ( col.ColumnName == "ASSIGNED_SET_LIST" && !Sql.IsEmptyString(sASSIGNED_SET_LIST) ) par.Value = sASSIGNED_SET_LIST;
														else if ( col.ColumnName == "TEAM_SET_LIST"     && !Sql.IsEmptyString(sTEAM_SET_LIST)     ) par.Value = sTEAM_SET_LIST;
														else if ( col.ColumnName == "TAG_SET_NAME"      && !Sql.IsEmptyString(sTAG_SET_NAME)      ) par.Value = sTAG_SET_NAME;
														else if ( col.ColumnName == "NAICS_SET_NAME"    && !Sql.IsEmptyString(sNAICS_SET_NAME)    ) par.Value = sNAICS_SET_NAME;
														else par.Value = RestUtil.DBValueFromJsonValue(par.DbType, row[col.ColumnName], T10n);
													}
												}
											}
											cmdUpdate.ExecuteNonQuery();
											if ( bHAS_CUSTOM )
												_splendidDynamic.UpdateCustomFields(row, trn, gID, sTABLE_NAME, dtCustomFields);
										}
										else
										{
											throw new Exception(L10n.Term("ACL.LBL_NO_ACCESS"));
										}
									}
									trn.Commit();
								}
								catch
								{
									try { trn.Rollback(); } catch { }
									throw;
								}
							}
						}
					}
				}
				return Ok(new { status = "updated", count = arrID_LIST.Count });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// POST Administration/Rest.svc/MassDeleteAdminModule
		/// Deletes multiple records from an admin module table. Source: lines 5172-5191.
		/// </summary>
		[HttpPost("MassDeleteAdminModule")]
		public async System.Threading.Tasks.Task<IActionResult> MassDeleteAdminModule()
		{
			try
			{
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });
				string body = await ReadRequestBodyAsync();
				Dictionary<string, object> dict = DeserializeBody(body);
				string sModuleName = Sql.ToString(dict.ContainsKey("MODULE_NAME") ? dict["MODULE_NAME"] : null);
				if (Sql.IsEmptyString(sModuleName))
					return BadRequest(new { error = "MODULE_NAME is required" });
				string sTableName = Crm.Modules.TableName(_memoryCache, sModuleName);
				if (Sql.IsEmptyString(sTableName)) sTableName = sModuleName;
				List<Guid> arrIDs = new List<Guid>();
				if (dict.ContainsKey("IDs") && dict["IDs"] is JArray jIDs2)
					foreach (var jID in jIDs2)
						arrIDs.Add(Sql.ToGuid(jID.ToString()));
				if (arrIDs.Count == 0)
					return BadRequest(new { error = "IDs array is required" });
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbTransaction trn = Sql.BeginTransaction(con);
				try
				{
					foreach (Guid gID in arrIDs)
					{
						using IDbCommand cmd = SqlProcs.Factory(con, "sp" + sTableName + "_Delete");
						Sql.AddParameter(cmd, "@ID"              , gID            );
						Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security.USER_ID);
						cmd.Transaction = trn;
						cmd.ExecuteNonQuery();
					}
					trn.Commit();
				}
				catch { trn.Rollback(); throw; }
				return Ok(new { status = "deleted", count = arrIDs.Count });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// POST Administration/Rest.svc/UserRoleMakeDefault
		/// Sets a role as the default ACL role. Source: lines 4682-4704.
		/// </summary>
		[HttpPost("UserRoleMakeDefault")]
		public async System.Threading.Tasks.Task<IActionResult> UserRoleMakeDefault()
		{
			try
			{
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !_security.IS_ADMIN)
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });
				string body = await ReadRequestBodyAsync();
				Dictionary<string, object> dict = DeserializeBody(body);
				Guid gROLE_ID = Sql.ToGuid(dict.ContainsKey("ROLE_ID") ? dict["ROLE_ID"] : null);
				if (Sql.IsEmptyGuid(gROLE_ID))
					return BadRequest(new { error = "ROLE_ID is required" });
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = SqlProcs.Factory(con, "spACL_ROLES_MakeDefault");
				Sql.AddParameter(cmd, "@ID"              , gROLE_ID       );
				Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security.USER_ID);
				cmd.ExecuteNonQuery();
				return Ok(new { status = "updated" });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// POST Administration/Rest.svc/AdminProcedure
		/// Executes a named administrative stored procedure. Source: lines 4708-5042.
		/// </summary>
		[HttpPost("AdminProcedure")]
		public async System.Threading.Tasks.Task<IActionResult> AdminProcedure()
		{
			try
			{
				// Ported from SplendidCRM/Administration/Rest.svc.cs lines 4708-4960.
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if ( !_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE) )
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				string body = await ReadRequestBodyAsync();
				Dictionary<string, object> dict = DeserializeBody(body);
				// Legacy reads ProcedureName from query string.
				string sProcedureName = HttpContext.Request.Query.ContainsKey("ProcedureName")
					? Sql.ToString(HttpContext.Request.Query["ProcedureName"])
					: Sql.ToString(dict.ContainsKey("ProcedureName") ? dict["ProcedureName"] : null);
				if ( Sql.IsEmptyString(sProcedureName) )
					return BadRequest(new { error = "ProcedureName is required" });

				Guid gTIMEZONE = Sql.ToGuid(_httpContextAccessor.HttpContext?.Session.GetString("USER_SETTINGS/TIMEZONE"));
				SplendidCRM.TimeZone T10n = SplendidCRM.TimeZone.CreateTimeZone(gTIMEZONE);
				// Build DataTable of input parameters.
				DataTable dtUPDATE = new DataTable(sProcedureName);
				foreach ( string sColumnName in dict.Keys )
				{
					dtUPDATE.Columns.Add(sColumnName);
				}
				DataRow row = dtUPDATE.NewRow();
				dtUPDATE.Rows.Add(row);
				foreach ( string sColumnName in dict.Keys )
				{
					if ( dict[sColumnName] is System.Collections.ArrayList )
					{
						System.Collections.ArrayList lst = dict[sColumnName] as System.Collections.ArrayList;
						System.Xml.XmlDocument xml = new System.Xml.XmlDocument();
						xml.AppendChild(xml.CreateXmlDeclaration("1.0", "UTF-8", null));
						xml.AppendChild(xml.CreateElement("Values"));
						if ( lst.Count > 0 )
						{
							foreach ( object item in lst )
							{
								System.Xml.XmlNode xValue = xml.CreateElement("Value");
								xml.DocumentElement.AppendChild(xValue);
								xValue.InnerText = Sql.ToString(item);
							}
						}
						row[sColumnName] = xml.OuterXml;
					}
					else
					{
						row[sColumnName] = dict[sColumnName];
					}
				}
				Dictionary<string, object> d = new Dictionary<string, object>();
				StringBuilder sbDumpSQL = new StringBuilder();
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				// Validate procedure via RestTables (dynamic whitelist from SYSTEM_REST_TABLES).
				using ( DataTable dtSYNC_TABLES = _splendidCache.RestTables(sProcedureName, false) )
				{
					if ( dtSYNC_TABLES != null && dtSYNC_TABLES.Rows.Count > 0 )
					{
						DataRow rowSYNC_TABLE = dtSYNC_TABLES.Rows[0];
						string sPROCEDURE_NAME  = Sql.ToString (rowSYNC_TABLE["TABLE_NAME"     ]);
						string sMODULE_NAME     = Sql.ToString (rowSYNC_TABLE["MODULE_NAME"    ]);
						string sREQUIRED_FIELDS = Sql.ToString (rowSYNC_TABLE["REQUIRED_FIELDS"]);
						bool   bIS_SYSTEM       = Sql.ToBoolean(rowSYNC_TABLE["IS_SYSTEM"      ]);
						bool   bEnableTeamManagement = Crm.Config.enable_team_management();

						int nACLACCESS = _security.AdminUserAccess(sMODULE_NAME, "edit");
						if ( nACLACCESS >= 0 && bIS_SYSTEM )
						{
							if ( !Sql.IsEmptyString(sREQUIRED_FIELDS) )
							{
								string[] arrREQUIRED_FIELDS = sREQUIRED_FIELDS.ToUpper().Replace(",", " ").Split(' ');
								string sMISSING_FIELDS = String.Empty;
								foreach ( string sREQUIRED_FIELD in arrREQUIRED_FIELDS )
								{
									if ( !Sql.IsEmptyString(sREQUIRED_FIELD) && !dtUPDATE.Columns.Contains(sREQUIRED_FIELD) )
									{
										if ( !Sql.IsEmptyString(sMISSING_FIELDS) )
											sMISSING_FIELDS += " ";
										sMISSING_FIELDS += sREQUIRED_FIELD;
									}
								}
								if ( !Sql.IsEmptyString(sMISSING_FIELDS) )
									throw new Exception("Missing required fields: " + sMISSING_FIELDS);
							}

							using ( IDbConnection con = dbf.CreateConnection() )
							{
								con.Open();
								bool bEnableTransaction = true;
								if ( sPROCEDURE_NAME == "spSqlBackupDatabase" )
									bEnableTransaction = false;
								if ( bEnableTransaction )
								{
									using ( IDbTransaction trn = Sql.BeginTransaction(con) )
									{
										try
										{
											IDbCommand cmd = SqlProcs.Factory(con, sPROCEDURE_NAME);
											cmd.Transaction = trn;
											foreach ( IDbDataParameter par in cmd.Parameters )
											{
												string sParameterName = par.ParameterName.TrimStart('@').ToUpper();
												if ( sParameterName == "TEAM_ID" && bEnableTeamManagement )
													par.Value = Sql.ToDBGuid(_security.TEAM_ID);
												else if ( sParameterName == "ASSIGNED_USER_ID" )
													par.Value = Sql.ToDBGuid(_security.USER_ID);
												else if ( sParameterName == "MODIFIED_USER_ID" )
													par.Value = Sql.ToDBGuid(_security.USER_ID);
												else
													par.Value = DBNull.Value;
											}
											foreach ( DataColumn col in row.Table.Columns )
											{
												IDbDataParameter par = Sql.FindParameter(cmd, col.ColumnName);
												if ( par != null )
													par.Value = RestUtil.DBValueFromJsonValue(par.DbType, row[col.ColumnName], T10n);
											}
											sbDumpSQL.Append(Sql.ExpandParameters(cmd));
											if ( sPROCEDURE_NAME == "spSqlBackupDatabase" )
												cmd.CommandTimeout = 0;
											cmd.ExecuteNonQuery();
											trn.Commit();
											foreach ( IDbDataParameter par in cmd.Parameters )
											{
												if ( par.Direction == ParameterDirection.InputOutput || par.Direction == ParameterDirection.Output )
												{
													string sParameterName = par.ParameterName.TrimStart('@').ToUpper();
													d.Add(sParameterName, par.Value);
												}
											}
											// Post-execution hooks: terminology/module reload.
											if ( sProcedureName == "spLANGUAGES_Enable" )
											{
												_splendidInit.InitTerminology();
												_splendidCache.ClearLanguages();
											}
											else if ( sProcedureName == "spLANGUAGES_Disable" || sProcedureName == "spLANGUAGES_Delete" )
											{
												_splendidCache.ClearLanguages();
											}
											else if ( sProcedureName == "spMODULES_UpdateRelativePath" )
											{
												_splendidInit.InitModules();
											}
										}
										catch
										{
											try { trn.Rollback(); } catch { }
											throw;
										}
									}
								}
								else
								{
									// Non-transactional execution (e.g., spSqlBackupDatabase).
									IDbCommand cmd = SqlProcs.Factory(con, sPROCEDURE_NAME);
									foreach ( IDbDataParameter par in cmd.Parameters )
									{
										string sParameterName = par.ParameterName.TrimStart('@').ToUpper();
										if ( sParameterName == "TEAM_ID" && bEnableTeamManagement )
											par.Value = Sql.ToDBGuid(_security.TEAM_ID);
										else if ( sParameterName == "ASSIGNED_USER_ID" )
											par.Value = Sql.ToDBGuid(_security.USER_ID);
										else if ( sParameterName == "MODIFIED_USER_ID" )
											par.Value = Sql.ToDBGuid(_security.USER_ID);
										else
											par.Value = DBNull.Value;
									}
									foreach ( DataColumn col in row.Table.Columns )
									{
										IDbDataParameter par = Sql.FindParameter(cmd, col.ColumnName);
										if ( par != null )
											par.Value = RestUtil.DBValueFromJsonValue(par.DbType, row[col.ColumnName], T10n);
									}
									sbDumpSQL.Append(Sql.ExpandParameters(cmd));
									if ( sPROCEDURE_NAME == "spSqlBackupDatabase" )
										cmd.CommandTimeout = 0;
									cmd.ExecuteNonQuery();
									foreach ( IDbDataParameter par in cmd.Parameters )
									{
										if ( par.Direction == ParameterDirection.InputOutput || par.Direction == ParameterDirection.Output )
										{
											string sParameterName = par.ParameterName.TrimStart('@').ToUpper();
											d.Add(sParameterName, par.Value);
										}
									}
								}
							}
						}
						else
						{
							throw new Exception(L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS"));
						}
					}
					else
					{
						throw new Exception("Procedure not found in SYSTEM_REST_TABLES: " + sProcedureName);
					}
				}
				Dictionary<string, object> dictResponse = new Dictionary<string, object>();
				dictResponse.Add("d", d);
				if ( Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.show_sql")) )
					dictResponse.Add("__sql", sbDumpSQL.ToString());
				return JsonContent(dictResponse);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// GET Administration/Rest.svc/CheckVersion
		/// Checks for available SplendidCRM software updates. Source: lines 2452-2499.
		/// </summary>
		[HttpGet("CheckVersion")]
		public IActionResult CheckVersion(string CHECK_UPDATES = null)
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if ( !_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE) )
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				// Ported from SplendidCRM/Administration/Rest.svc.cs lines 2471-2498.
				// Resolve Utils from DI to call CheckVersion().
				var utils = HttpContext.RequestServices.GetRequiredService<Utils>();
				DataTable dt = utils.CheckVersion();
				DataView vwMain = dt.DefaultView;
				vwMain.Sort = "Build desc";
				if ( Sql.ToBoolean(CHECK_UPDATES) && vwMain.Count > 0 )
				{
					_memoryCache.Set("available_version"            , Sql.ToString(vwMain[0]["Build"      ]));
					_memoryCache.Set("available_version_description", Sql.ToString(vwMain[0]["Description"]));
				}
				else
				{
					_memoryCache.Remove("available_version"            );
					_memoryCache.Remove("available_version_description");
				}
				vwMain.RowFilter = "New = '1'";
				long lTotalCount = vwMain.Count;

				SplendidCRM.TimeZone T10n = GetUserTimezone();
				var reqObj = HttpContext.Request;
				string sBaseURI = reqObj.Scheme + "://" + reqObj.Host.Value + reqObj.PathBase.Value + "/Administration/Rest.svc/CheckVersion";
				Dictionary<string, object> dictResponse = _restUtil.ToJson(sBaseURI, String.Empty, vwMain, T10n);
				dictResponse.Add("__total", lTotalCount);
				return JsonContent(dictResponse);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// GET Administration/Rest.svc/GetAllLayouts
		/// Returns system layout information for all layout types. Source: lines 2949-3015.
		/// </summary>
		[HttpGet("GetAllLayouts")]
		public IActionResult GetAllLayouts()
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });
				List<string> lstMODULES = GetAdminModuleList();
				var results = new Dictionary<string, object>
				{
					{ "GRIDVIEWS_COLUMNS"          , GetAllGridViewsColumnsInternal       (lstMODULES) },
					{ "DETAILVIEWS_FIELDS"          , GetAllDetailViewsFieldsInternal      (lstMODULES) },
					{ "EDITVIEWS_FIELDS"            , GetAllEditViewsFieldsInternal        (lstMODULES) },
					{ "DETAILVIEWS_RELATIONSHIPS"   , GetAllDetailViewsRelationshipsInternal(lstMODULES) },
					{ "EDITVIEWS_RELATIONSHIPS"     , GetAllEditViewsRelationshipsInternal (lstMODULES) },
					{ "DYNAMIC_BUTTONS"             , GetAllDynamicButtonsInternal         (lstMODULES) }
				};
				return JsonContent(new { d = results });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// POST Administration/Rest.svc/ReloadTerminology
		/// Reloads all terminology from the database into cache. Source: lines 3317-3357.
		/// </summary>
		[HttpPost("ReloadTerminology")]
		public IActionResult ReloadTerminologyEndpoint()
		{
			try
			{
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !_security.IS_ADMIN)
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });
				_splendidCache.ClearLanguages();
				_splendidInit.InitTerminology();
				return Ok(new { status = "reloaded" });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// POST Administration/Rest.svc/InsertAdminEditCustomField
		/// Inserts a custom field definition for a module. Source: lines 5422-5561.
		/// </summary>
		[HttpPost("InsertAdminEditCustomField")]
		public async System.Threading.Tasks.Task<IActionResult> InsertAdminEditCustomField()
		{
			try
			{
				// Ported from SplendidCRM/Administration/Rest.svc.cs lines 5423-5561.
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				string sModuleName = "EditCustomFields";
				int nACLACCESS = _security.AdminUserAccess(sModuleName, "edit");
				if ( !_security.IsAuthenticated() || nACLACCESS < 0 )
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") + ": " + sModuleName });

				string body = await ReadRequestBodyAsync();
				Dictionary<string, object> dict = DeserializeBody(body);

				// Extract each expected field individually (legacy lines 5460-5479).
				string sNAME           = String.Empty;
				string sLABEL          = String.Empty;
				string sLABEL_TERM     = String.Empty;
				string sCUSTOM_MODULE  = String.Empty;
				string sDATA_TYPE      = String.Empty;
				Int32  nMAX_SIZE       = 0           ;
				bool   bREQUIRED       = false       ;
				bool   bAUDITED        = false       ;
				string sDEFAULT_VALUE  = String.Empty;
				string sDROPDOWN_LIST  = String.Empty;
				bool   bMASS_UPDATE    = false       ;
				foreach ( string sName in dict.Keys )
				{
					switch ( sName )
					{
						case "NAME"          :  sNAME           = Sql.ToString (dict[sName]);  break;
						case "LABEL"         :  sLABEL          = Sql.ToString (dict[sName]);  break;
						case "CUSTOM_MODULE" :  sCUSTOM_MODULE  = Sql.ToString (dict[sName]);  break;
						case "DATA_TYPE"     :  sDATA_TYPE      = Sql.ToString (dict[sName]);  break;
						case "MAX_SIZE"      :  nMAX_SIZE       = Sql.ToInteger(dict[sName]);  break;
						case "REQUIRED"      :  bREQUIRED       = Sql.ToBoolean(dict[sName]);  break;
						case "AUDITED"       :  bAUDITED        = Sql.ToBoolean(dict[sName]);  break;
						case "DEFAULT_VALUE" :  sDEFAULT_VALUE  = Sql.ToString (dict[sName]);  break;
						case "DROPDOWN_LIST" :  sDROPDOWN_LIST  = Sql.ToString (dict[sName]);  break;
						case "MASS_UPDATE"   :  bMASS_UPDATE    = Sql.ToBoolean(dict[sName]);  break;
					}
				}
				sNAME  = sNAME .Trim();
				sLABEL = sLABEL.Trim();

				// Validate module existence (legacy lines 5490-5497).
				if ( Sql.IsEmptyString(sCUSTOM_MODULE) )
					throw new Exception("The module name must be specified.");
				string sTABLE_NAME = Sql.ToString (_memoryCache.Get<object>("Modules." + sCUSTOM_MODULE + ".TableName"));
				bool   bValid      = Sql.ToBoolean(_memoryCache.Get<object>("Modules." + sCUSTOM_MODULE + ".Valid"    ));
				if ( Sql.IsEmptyString(sTABLE_NAME) && !bValid )
					throw new Exception("Unknown module: " + sCUSTOM_MODULE);

				// Validate and sanitize field name (legacy lines 5499-5510).
				if ( Sql.IsEmptyString(sNAME) )
					throw new Exception("The field name must be specified.");
				if ( Sql.IsEmptyString(sLABEL) )
					sLABEL = sNAME;
				Regex r = new Regex(@"[^\w]+");
				sNAME = r.Replace(sNAME, "_");
				r = new Regex(@"^[A-Za-z_]\w*");
				if ( !r.IsMatch(sNAME) )
					throw new Exception("invalid field name");

				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					using ( IDbTransaction trn = Sql.BeginTransaction(con) )
					{
						try
						{
							using ( IDbCommand cmd = con.CreateCommand() )
							{
								cmd.Transaction    = trn;
								cmd.CommandType    = CommandType.StoredProcedure;
								cmd.CommandText    = "spFIELDS_META_DATA_Insert";
								cmd.CommandTimeout = 0;
								IDbDataParameter parID                = Sql.AddParameter(cmd, "@ID"               , Guid.Empty          );
								IDbDataParameter parMODIFIED_USER_ID  = Sql.AddParameter(cmd, "@MODIFIED_USER_ID" , _security.USER_ID   );
								IDbDataParameter parNAME              = Sql.AddParameter(cmd, "@NAME"             , sNAME               , 255);
								IDbDataParameter parLABEL             = Sql.AddParameter(cmd, "@LABEL"            , sLABEL              , 255);
								IDbDataParameter parLABEL_TERM        = Sql.AddParameter(cmd, "@LABEL_TERM"       , sLABEL_TERM         , 255);
								IDbDataParameter parCUSTOM_MODULE     = Sql.AddParameter(cmd, "@CUSTOM_MODULE"    , sCUSTOM_MODULE      , 255);
								IDbDataParameter parDATA_TYPE         = Sql.AddParameter(cmd, "@DATA_TYPE"        , sDATA_TYPE          , 255);
								IDbDataParameter parMAX_SIZE          = Sql.AddParameter(cmd, "@MAX_SIZE"         , nMAX_SIZE           );
								IDbDataParameter parREQUIRED          = Sql.AddParameter(cmd, "@REQUIRED"         , bREQUIRED           );
								IDbDataParameter parAUDITED           = Sql.AddParameter(cmd, "@AUDITED"          , bAUDITED            );
								IDbDataParameter parDEFAULT_VALUE     = Sql.AddParameter(cmd, "@DEFAULT_VALUE"    , sDEFAULT_VALUE      , 255);
								IDbDataParameter parDROPDOWN_LIST     = Sql.AddParameter(cmd, "@DROPDOWN_LIST"    , sDROPDOWN_LIST      ,  50);
								IDbDataParameter parMASS_UPDATE       = Sql.AddParameter(cmd, "@MASS_UPDATE"      , bMASS_UPDATE        );
								IDbDataParameter parDISABLE_RECOMPILE = Sql.AddParameter(cmd, "@DISABLE_RECOMPILE", true                );
								parID.Direction = ParameterDirection.InputOutput;
								cmd.ExecuteNonQuery();
							}
							trn.Commit();
						}
						catch ( Exception ex )
						{
							trn.Rollback();
							throw new Exception(ex.Message, ex.InnerException);
						}
					}
				}
				// Trigger background recompile (legacy lines 5549-5560).
				if ( _memoryCache.Get<object>("System.Recompile.Start") == null )
				{
					string sServiceLevel = Sql.ToString(_memoryCache.Get<object>("CONFIG.service_level"));
					if ( Sql.IsEmptyString(sServiceLevel) ) sServiceLevel = "Community";
					Thread t = new Thread(() => ModuleUtils.EditCustomFields.RecompileViews(_memoryCache, sServiceLevel));
					t.IsBackground = true;
					t.Start();
				}
				else
				{
					_memoryCache.Set("System.Recompile.Restart", true);
				}
				return Ok(new { status = "inserted", module = sCUSTOM_MODULE });
			}
			catch ( Exception ex )
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// POST Administration/Rest.svc/DeleteAdminEditCustomField
		/// Deletes a custom field definition for a module. Source: lines 5361-5420.
		/// </summary>
		[HttpPost("DeleteAdminEditCustomField")]
		public async System.Threading.Tasks.Task<IActionResult> DeleteAdminEditCustomField()
		{
			try
			{
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				// Ported from SplendidCRM/Administration/Rest.svc.cs lines 5361-5420.
				string sModuleName = "EditCustomFields";
				int nACLACCESS = _security.AdminUserAccess(sModuleName, "delete");
				if ( !_security.IsAuthenticated() || nACLACCESS < 0 )
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") + ": " + sModuleName });

				string body = await ReadRequestBodyAsync();
				Dictionary<string, object> dict = DeserializeBody(body);
				Guid gID = Sql.ToGuid(dict.ContainsKey("ID") ? dict["ID"] : null);
				if ( Sql.IsEmptyGuid(gID) )
					return BadRequest(new { error = "ID is required" });

				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					using ( IDbTransaction trn = Sql.BeginTransaction(con) )
					{
						try
						{
							using ( IDbCommand cmd = con.CreateCommand() )
							{
								cmd.Transaction    = trn;
								cmd.CommandType    = CommandType.StoredProcedure;
								cmd.CommandText    = "spFIELDS_META_DATA_Delete";
								cmd.CommandTimeout = 0;
								Sql.AddParameter(cmd, "@ID"               , gID               );
								Sql.AddParameter(cmd, "@MODIFIED_USER_ID" , _security.USER_ID );
								Sql.AddParameter(cmd, "@DISABLE_RECOMPILE", true              );
								cmd.ExecuteNonQuery();
							}
							trn.Commit();
						}
						catch ( Exception ex )
						{
							trn.Rollback();
							throw new Exception(ex.Message, ex.InnerException);
						}
					}
				}
				// Trigger background recompile of views after schema change.
				if ( _memoryCache.Get<object>("System.Recompile.Start") == null )
				{
					_memoryCache.Set("System.Recompile.Restart", true);
				}
				else
				{
					_memoryCache.Set("System.Recompile.Restart", true);
				}
				return Ok(new { status = "deleted", id = gID });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		/// <summary>
		/// GET Administration/Rest.svc/GetAclAccessByModule
		/// Returns ACL access rights for all roles for a given module. Source: lines 5848-5918.
		/// </summary>
		[HttpGet("GetAclAccessByModule")]
		public IActionResult GetAclAccessByModule()
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				// Ported from SplendidCRM/Administration/Rest.svc.cs lines 5848-5918.
				// Legacy returns ALL modules (no WHERE clause). No MODULE_NAME parameter.
				if ( !_security.IsAuthenticated() || !(_security.AdminUserAccess("ACLRoles", "view") >= 0 || _security.AdminUserAccess("ACLRoles", "edit") >= 0) )
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });
				SplendidCRM.TimeZone T10n = GetUserTimezone();
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				string sSQL = "select MODULE_NAME          " + ControlChars.CrLf
				            + "     , DISPLAY_NAME         " + ControlChars.CrLf
				            + "     , ACLACCESS_ADMIN      " + ControlChars.CrLf
				            + "     , ACLACCESS_ACCESS     " + ControlChars.CrLf
				            + "     , ACLACCESS_VIEW       " + ControlChars.CrLf
				            + "     , ACLACCESS_LIST       " + ControlChars.CrLf
				            + "     , ACLACCESS_EDIT       " + ControlChars.CrLf
				            + "     , ACLACCESS_DELETE     " + ControlChars.CrLf
				            + "     , ACLACCESS_IMPORT     " + ControlChars.CrLf
				            + "     , ACLACCESS_EXPORT     " + ControlChars.CrLf
				            + "     , ACLACCESS_ARCHIVE    " + ControlChars.CrLf
				            + "     , IS_ADMIN             " + ControlChars.CrLf
				            + "  from vwACL_ACCESS_ByModule" + ControlChars.CrLf
				            + " order by MODULE_NAME       " + ControlChars.CrLf;
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText = sSQL;
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				var reqObj = HttpContext.Request;
				string sBaseURI = reqObj.Scheme + "://" + reqObj.Host.Value + reqObj.PathBase.Value + "/Administration/Rest.svc/GetAclAccessByModule";
				Dictionary<string, object> dictResponse = _restUtil.ToJson(sBaseURI, String.Empty, dt, T10n);
				dictResponse.Add("__total", (long)dt.Rows.Count);
				return JsonContent(dictResponse);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return InternalError(ex);
			}
		}

		// ==================================================================
		// Private internal helpers — layout data retrieval (mirrors RestController pattern)
		// ==================================================================

		/// <summary>Extracts module name from a dot-separated view name (e.g. "Accounts.DetailView" → "Accounts").</summary>
		private string ExtractModuleName(string sViewName)
		{
			if (Sql.IsEmptyString(sViewName)) return sViewName;
			int n = sViewName.IndexOf('.');
			if (n > 0) return sViewName.Substring(0, n);
			return sViewName;
		}


		/// <summary>Returns current user profile dictionary from session data.</summary>
		private Dictionary<string, object> GetUserProfileDict()
		{
			var profile = new Dictionary<string, object>();
			ISession session = _httpContextAccessor.HttpContext?.Session;
			if (session != null)
			{
				profile["USER_ID"]           = _security.USER_ID;
				profile["IS_ADMIN"]          = _security.IS_ADMIN;
				profile["IS_ADMIN_DELEGATE"] = _security.IS_ADMIN_DELEGATE;
				string[] keys = { "USER_NAME", "FIRST_NAME", "LAST_NAME", "USER_LANG",
				                  "TIMEZONE", "CURRENCY_ID", "DATE_FORMAT", "TIME_FORMAT",
				                  "USER_SETTINGS/CULTURE" };
				foreach (string k in keys)
					profile[k] = session.GetString(k);
			}
			return profile;
		}

		/// <summary>Builds module tab order dictionary from admin modules table.</summary>
		private Dictionary<string, int> BuildModuleTabOrder()
		{
			var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			try
			{
				DataTable dt = _splendidCache.GetAdminModules();
				if (dt != null)
				{
					foreach (DataRow row in dt.Rows)
					{
						string sModuleName = Sql.ToString(row["MODULE_NAME"]);
						int nTabOrder = dt.Columns.Contains("TAB_ORDER") ? Sql.ToInteger(row["TAB_ORDER"]) : 0;
						if (!dict.ContainsKey(sModuleName))
							dict[sModuleName] = nTabOrder;
					}
				}
			}
			catch (Exception ex) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return dict;
		}

		/// <summary>Returns all grid view column definitions for the given module list.</summary>
		private Dictionary<string, object> GetAllGridViewsColumnsInternal(List<string> lstMODULES)
		{
			string sCacheKey = "vwGRIDVIEWS_COLUMNS.Admin.ReactClient";
			var objs = _memoryCache.Get<Dictionary<string, object>>(sCacheKey);
			if (objs != null) return objs;
			objs = new Dictionary<string, object>();
			try
			{
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                          " + ControlChars.CrLf
				  + "  from vwGRIDVIEWS_COLUMNS                       " + ControlChars.CrLf
				  + " order by GRID_NAME, COLUMN_INDEX                 " + ControlChars.CrLf;
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				string sLAST_VIEW_NAME = String.Empty;
				bool bClearScript = false;
				List<Dictionary<string, object>> layout = null;
				foreach (DataRow row in dt.Rows)
				{
					string sGRID_NAME  = Sql.ToString(row["GRID_NAME" ]);
					string sDATA_FIELD = Sql.ToString(row["DATA_FIELD"]);
					string sMODULE_NAME = ExtractModuleName(sGRID_NAME);
					if (!lstMODULES.Contains(sMODULE_NAME)) continue;
					if (sLAST_VIEW_NAME != sGRID_NAME)
					{
						bClearScript    = false;
						sLAST_VIEW_NAME = sGRID_NAME;
						layout          = new List<Dictionary<string, object>>();
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
							drow[dt.Columns[j].ColumnName] = row[j] == DBNull.Value ? null : row[j];
						}
						layout?.Add(drow);
					}
				}
				_memoryCache.Set(sCacheKey, objs, _splendidCache.DefaultCacheExpiration());
			}
			catch (Exception ex) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return objs;
		}

		/// <summary>Returns all detail view field definitions for given module list.</summary>
		private Dictionary<string, object> GetAllDetailViewsFieldsInternal(List<string> lstMODULES)
		{
			string sCacheKey = "vwDETAILVIEWS_FIELDS.Admin.ReactClient";
			var objs = _memoryCache.Get<Dictionary<string, object>>(sCacheKey);
			if (objs != null) return objs;
			objs = new Dictionary<string, object>();
			try
			{
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                         " + ControlChars.CrLf
				  + "  from vwDETAILVIEWS_FIELDS      " + ControlChars.CrLf
				  + " order by DETAIL_NAME, FIELD_INDEX" + ControlChars.CrLf;
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				string sLAST_VIEW_NAME = String.Empty;
				List<Dictionary<string, object>> layout = null;
				foreach (DataRow row in dt.Rows)
				{
					string sVIEW_NAME  = Sql.ToString(row["DETAIL_NAME"]);
					string sDATA_FIELD = Sql.ToString(row["DATA_FIELD" ]);
					string sMOD = ExtractModuleName(sVIEW_NAME);
					if (!lstMODULES.Contains(sMOD)) continue;
					if (sLAST_VIEW_NAME != sVIEW_NAME)
					{
						sLAST_VIEW_NAME = sVIEW_NAME;
						layout = new List<Dictionary<string, object>>();
						objs[sLAST_VIEW_NAME] = layout;
					}
					bool bIsReadable = true;
					if (SplendidInit.bEnableACLFieldSecurity && !Sql.IsEmptyString(sDATA_FIELD))
					{
						Security.ACL_FIELD_ACCESS acl = _security.GetUserFieldSecurity(sMOD, sDATA_FIELD, Guid.Empty);
						bIsReadable = acl.IsReadable();
					}
					if (bIsReadable)
					{
						var drow = new Dictionary<string, object>();
						for (int j = 0; j < dt.Columns.Count; j++)
						{
							if (dt.Columns[j].ColumnName == "ID") continue;
							drow[dt.Columns[j].ColumnName] = row[j] == DBNull.Value ? null : row[j];
						}
						layout?.Add(drow);
					}
				}
				_memoryCache.Set(sCacheKey, objs, _splendidCache.DefaultCacheExpiration());
			}
			catch (Exception ex) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return objs;
		}

		/// <summary>Returns all edit view field definitions for given module list.</summary>
		private Dictionary<string, object> GetAllEditViewsFieldsInternal(List<string> lstMODULES)
		{
			string sCacheKey = "vwEDITVIEWS_FIELDS.Admin.ReactClient";
			var objs = _memoryCache.Get<Dictionary<string, object>>(sCacheKey);
			if (objs != null) return objs;
			objs = new Dictionary<string, object>();
			try
			{
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                         " + ControlChars.CrLf
				  + "  from vwEDITVIEWS_FIELDS        " + ControlChars.CrLf
				  + " order by EDIT_NAME, FIELD_INDEX  " + ControlChars.CrLf;
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				string sLAST_VIEW_NAME = String.Empty;
				List<Dictionary<string, object>> layout = null;
				foreach (DataRow row in dt.Rows)
				{
					string sVIEW_NAME  = Sql.ToString(row["EDIT_NAME" ]);
					string sDATA_FIELD = Sql.ToString(row["DATA_FIELD"]);
					string sMOD = ExtractModuleName(sVIEW_NAME);
					if (!lstMODULES.Contains(sMOD)) continue;
					if (sLAST_VIEW_NAME != sVIEW_NAME)
					{
						sLAST_VIEW_NAME = sVIEW_NAME;
						layout = new List<Dictionary<string, object>>();
						objs[sLAST_VIEW_NAME] = layout;
					}
					bool bIsReadable = true;
					if (SplendidInit.bEnableACLFieldSecurity && !Sql.IsEmptyString(sDATA_FIELD))
					{
						Security.ACL_FIELD_ACCESS acl = _security.GetUserFieldSecurity(sMOD, sDATA_FIELD, Guid.Empty);
						bIsReadable = acl.IsReadable();
					}
					if (bIsReadable)
					{
						var drow = new Dictionary<string, object>();
						for (int j = 0; j < dt.Columns.Count; j++)
						{
							if (dt.Columns[j].ColumnName == "ID") continue;
							drow[dt.Columns[j].ColumnName] = row[j] == DBNull.Value ? null : row[j];
						}
						layout?.Add(drow);
					}
				}
				_memoryCache.Set(sCacheKey, objs, _splendidCache.DefaultCacheExpiration());
			}
			catch (Exception ex) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return objs;
		}

		/// <summary>Returns all detail view relationship panels for given module list.</summary>
		private Dictionary<string, object> GetAllDetailViewsRelationshipsInternal(List<string> lstMODULES)
		{
			string sCacheKey = "vwDETAILVIEWS_RELATIONSHIPS.Admin.ReactClient";
			var objs = _memoryCache.Get<Dictionary<string, object>>(sCacheKey);
			if (objs != null) return objs;
			objs = new Dictionary<string, object>();
			try
			{
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                 " + ControlChars.CrLf
				  + "  from vwDETAILVIEWS_RELATIONSHIPS       " + ControlChars.CrLf
				  + " order by DETAIL_NAME, RELATIONSHIP_ORDER" + ControlChars.CrLf;
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				string sLAST_VIEW_NAME = String.Empty;
				List<Dictionary<string, object>> layout = null;
				foreach (DataRow row in dt.Rows)
				{
					string sVIEW_NAME = Sql.ToString(row["DETAIL_NAME"]);
					string sMOD = ExtractModuleName(sVIEW_NAME);
					if (!lstMODULES.Contains(sMOD)) continue;
					if (sLAST_VIEW_NAME != sVIEW_NAME)
					{
						sLAST_VIEW_NAME = sVIEW_NAME;
						layout = new List<Dictionary<string, object>>();
						objs[sLAST_VIEW_NAME] = layout;
					}
					var drow = new Dictionary<string, object>();
					for (int j = 0; j < dt.Columns.Count; j++)
					{
						if (dt.Columns[j].ColumnName == "ID") continue;
						drow[dt.Columns[j].ColumnName] = row[j] == DBNull.Value ? null : row[j];
					}
					layout?.Add(drow);
				}
				_memoryCache.Set(sCacheKey, objs, _splendidCache.DefaultCacheExpiration());
			}
			catch (Exception ex) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return objs;
		}

		/// <summary>Returns all edit view relationship panels for given module list.</summary>
		private Dictionary<string, object> GetAllEditViewsRelationshipsInternal(List<string> lstMODULES)
		{
			string sCacheKey = "vwEDITVIEWS_RELATIONSHIPS.Admin.ReactClient";
			var objs = _memoryCache.Get<Dictionary<string, object>>(sCacheKey);
			if (objs != null) return objs;
			objs = new Dictionary<string, object>();
			try
			{
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                                 " + ControlChars.CrLf
				  + "  from vwEDITVIEWS_RELATIONSHIPS        " + ControlChars.CrLf
				  + " order by EDIT_NAME, RELATIONSHIP_ORDER  " + ControlChars.CrLf;
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				string sLAST_VIEW_NAME = String.Empty;
				List<Dictionary<string, object>> layout = null;
				foreach (DataRow row in dt.Rows)
				{
					string sVIEW_NAME = Sql.ToString(row["EDIT_NAME"]);
					string sMOD = ExtractModuleName(sVIEW_NAME);
					if (!lstMODULES.Contains(sMOD)) continue;
					if (sLAST_VIEW_NAME != sVIEW_NAME)
					{
						sLAST_VIEW_NAME = sVIEW_NAME;
						layout = new List<Dictionary<string, object>>();
						objs[sLAST_VIEW_NAME] = layout;
					}
					var drow = new Dictionary<string, object>();
					for (int j = 0; j < dt.Columns.Count; j++)
					{
						if (dt.Columns[j].ColumnName == "ID") continue;
						drow[dt.Columns[j].ColumnName] = row[j] == DBNull.Value ? null : row[j];
					}
					layout?.Add(drow);
				}
				_memoryCache.Set(sCacheKey, objs, _splendidCache.DefaultCacheExpiration());
			}
			catch (Exception ex) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return objs;
		}

		/// <summary>Returns all dynamic button definitions for given module list.</summary>
		private Dictionary<string, object> GetAllDynamicButtonsInternal(List<string> lstMODULES)
		{
			string sCacheKey = "vwDYNAMIC_BUTTONS.Admin.ReactClient";
			var objs = _memoryCache.Get<Dictionary<string, object>>(sCacheKey);
			if (objs != null) return objs;
			objs = new Dictionary<string, object>();
			try
			{
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                               " + ControlChars.CrLf
				  + "  from vwDYNAMIC_BUTTONS              " + ControlChars.CrLf
				  + " order by VIEW_NAME, CONTROL_INDEX    " + ControlChars.CrLf;
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				string sLAST_VIEW_NAME = String.Empty;
				List<Dictionary<string, object>> layout = null;
				foreach (DataRow row in dt.Rows)
				{
					string sVIEW_NAME = Sql.ToString(row["VIEW_NAME"]);
					string sMOD = ExtractModuleName(sVIEW_NAME);
					if (!lstMODULES.Contains(sMOD)) continue;
					if (sLAST_VIEW_NAME != sVIEW_NAME)
					{
						sLAST_VIEW_NAME = sVIEW_NAME;
						layout = new List<Dictionary<string, object>>();
						objs[sLAST_VIEW_NAME] = layout;
					}
					var drow = new Dictionary<string, object>();
					for (int j = 0; j < dt.Columns.Count; j++)
					{
						if (dt.Columns[j].ColumnName == "ID") continue;
						drow[dt.Columns[j].ColumnName] = row[j] == DBNull.Value ? null : row[j];
					}
					layout?.Add(drow);
				}
				_memoryCache.Set(sCacheKey, objs, _splendidCache.DefaultCacheExpiration());
			}
			catch (Exception ex) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return objs;
		}

		/// <summary>Returns all terminology strings (admin gets all modules).</summary>
		private Dictionary<string, object> GetAllTerminologyInternal(List<string> lstMODULES, bool bAdmin)
		{
			string sCulture  = GetUserCulture();
			string sCacheKey = "vwTERMINOLOGY.Admin.ReactClient." + sCulture;
			var objs = _memoryCache.Get<Dictionary<string, object>>(sCacheKey);
			if (objs != null) return objs;
			objs = new Dictionary<string, object>();
			try
			{
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select NAME, DISPLAY_NAME, MODULE_NAME " + ControlChars.CrLf
				  + "  from vwTERMINOLOGY                   " + ControlChars.CrLf
				  + " where LANG = @LANG                    " + ControlChars.CrLf
				  + " order by MODULE_NAME, NAME            " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@LANG", sCulture);
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				foreach (DataRow row in dt.Rows)
				{
					string sName        = Sql.ToString(row["NAME"        ]);
					string sDisplayName = Sql.ToString(row["DISPLAY_NAME"]);
					string sModuleName  = Sql.ToString(row["MODULE_NAME" ]);
					string sDictKey     = Sql.IsEmptyString(sModuleName) ? sName : sModuleName + "." + sName;
					if (!objs.ContainsKey(sDictKey))
						objs[sDictKey] = sDisplayName;
				}
				_memoryCache.Set(sCacheKey, objs, _splendidCache.DefaultCacheExpiration());
			}
			catch (Exception ex) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return objs;
		}

		/// <summary>Returns all terminology list dropdown definitions.</summary>
		private Dictionary<string, object> GetAllTerminologyListsInternal(bool bAdmin)
		{
			string sCulture  = GetUserCulture();
			string sCacheKey = "vwTERMINOLOGY_LISTS.Admin.ReactClient." + sCulture;
			var objs = _memoryCache.Get<Dictionary<string, object>>(sCacheKey);
			if (objs != null) return objs;
			objs = new Dictionary<string, object>();
			try
			{
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select LIST_NAME, ITEM_VALUE, DISPLAY_NAME " + ControlChars.CrLf
				  + "  from vwTERMINOLOGY_LISTS                  " + ControlChars.CrLf
				  + " where LANG = @LANG                         " + ControlChars.CrLf
				  + " order by LIST_NAME, ITEM_ORDER, ITEM_VALUE " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@LANG", sCulture);
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				string sLAST_LIST = String.Empty;
				List<Dictionary<string, object>> list = null;
				foreach (DataRow row in dt.Rows)
				{
					string sListName    = Sql.ToString(row["LIST_NAME"   ]);
					string sItemValue   = Sql.ToString(row["ITEM_VALUE"  ]);
					string sDisplayName = Sql.ToString(row["DISPLAY_NAME"]);
					if (sLAST_LIST != sListName)
					{
						sLAST_LIST = sListName;
						list = new List<Dictionary<string, object>>();
						objs[sListName] = list;
					}
					list?.Add(new Dictionary<string, object> { { "value", sItemValue }, { "label", sDisplayName } });
				}
				_memoryCache.Set(sCacheKey, objs, _splendidCache.DefaultCacheExpiration());
			}
			catch (Exception ex) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return objs;
		}

		/// <summary>Builds the admin menu structure. Source: lines 2577-2947.</summary>
		private List<Dictionary<string, object>> GetAdminMenuInternal(Dictionary<string, int> dictModuleTabOrder)
		{
			string sCulture = GetUserCulture();
			L10N L10n = new L10N(sCulture, _memoryCache);
			var objs = new List<Dictionary<string, object>>();
			try
			{
				DataTable dtFields = _splendidCache.DetailViewRelationships("Administration.ListView");
				foreach (DataRow row in dtFields.Rows)
				{
					string sCONTROL_NAME = RowString(row, "CONTROL_NAME");
					switch (sCONTROL_NAME)
					{
						case "NetworkView":
						{
							AdminModule[] arr = new AdminModule[]
							{ new AdminModule(L10n, "Config"     , "Administration.LBL_SPLENDIDCRM_UPDATE_TITLE", "Administration.LBL_SPLENDIDCRM_UPDATE", "Updater"       , "sugarupdate.gif"       )
							, new AdminModule(L10n, "Config"     , "Administration.LBL_DOCUMENTATION_TITLE"    , "Administration.LBL_DOCUMENTATION"     , "Documentation" , "OnlineDocumentation.gif")
							};
							BuildAdminModules(L10n, objs, "NetworkView", "Administration.LBL_SPLENDIDCRM_NETWORK_TITLE", arr);
							break;
						}
						case "SystemView":
						{
							AdminModule[] arr = new AdminModule[]
							{ new AdminModule(L10n, "Config"             , "Administration.LBL_CONFIGURE_SETTINGS_TITLE"  , "Administration.LBL_CONFIGURE_SETTINGS"  , 10, dictModuleTabOrder, "Config.LNK_NEW_CONFIG"                    )
							, new AdminModule(L10n, "SystemCheck"        , "Administration.LBL_SYSTEM_CHECK_TITLE"        , "Administration.LBL_SYSTEM_CHECK"        , "SystemCheck"     )
							, new AdminModule(L10n, "Currencies"         , "Administration.LBL_MANAGE_CURRENCIES"         , "Administration.LBL_CURRENCY"            , 12, dictModuleTabOrder, "Currencies.LNK_NEW_CURRENCY"              )
							, new AdminModule(L10n, "SystemLog"          , "Administration.LBL_SYSTEM_LOG_TITLE"          , "Administration.LBL_SYSTEM_LOG"          , "List"            )
							, new AdminModule(L10n, "Schedulers"         , "Administration.LBL_SUGAR_SCHEDULER_TITLE"     , "Administration.LBL_SUGAR_SCHEDULER"     , "List"            )
							, new AdminModule(L10n, "Config"             , "Administration.LBL_BACKUPS_TITLE"             , "Administration.LBL_BACKUPS"             , "BackupDatabase"  , "Backups.gif")
							, new AdminModule(L10n, "ModulesArchiveRules", "Administration.LBL_MODULE_ARCHIVE_RULES_TITLE", "Administration.LBL_MODULE_ARCHIVE_RULES", 18, dictModuleTabOrder, "ModulesArchiveRules.LNK_NEW_ARCHIVE_RULE" , "Backups.gif")
							};
							BuildAdminModules(L10n, objs, "SystemView", "Administration.LBL_ADMINISTRATION_HOME_TITLE", arr);
							break;
						}
						case "UsersView":
						{
							AdminModule[] arr = new AdminModule[]
							{ new AdminModule(L10n, "Users"    , "Administration.LBL_MANAGE_USERS_TITLE" , "Administration.LBL_MANAGE_USERS" ,  1, dictModuleTabOrder, "Users.LNK_NEW_USER"    )
							, new AdminModule(L10n, "ACLRoles" , "Administration.LBL_MANAGE_ROLES_TITLE" , "Administration.LBL_MANAGE_ROLES" ,  3, dictModuleTabOrder, "ACLRoles.LBL_CREATE_ROLE")
							, new AdminModule(L10n, "UserLogins", "Administration.LBL_USERS_LOGINS_TITLE", "Administration.LBL_USERS_LOGINS", "List")
							, new AdminModule(L10n, "Teams"    , "Administration.LBL_TEAMS_TITLE"        , "Administration.LBL_TEAMS_DESC"   ,  2, dictModuleTabOrder, "Teams.LNK_NEW_TEAM"    )
							, new AdminModule(L10n, "AuditEvents", "Administration.LBL_AUDIT_EVENTS_TITLE", "Administration.LBL_AUDIT_EVENTS", "ReadOnlyListView", "UserLogins.gif")
							, new AdminModule(L10n, "Config"   , "Administration.LBL_MANAGE_PASSWORD_TITLE", "Administration.LBL_MANAGE_PASSWORD", "PasswordManager")
							};
							BuildAdminModules(L10n, objs, "UserView", "Administration.LBL_USERS_TITLE", arr);
							break;
						}
						case "StudioView":
						{
							AdminModule[] arr = new AdminModule[]
							{ new AdminModule(L10n, "DynamicLayout"  , "Administration.LBL_MANAGE_LAYOUT"              , "Administration.LBL_MANAGE_LAYOUT"          , "AdminDynamicLayout", "Layout.gif"      )
							, new AdminModule(L10n, "Dropdown"       , "Administration.LBL_DROPDOWN_EDITOR"            , "Administration.DESC_DROPDOWN_EDITOR"        , "List"              )
							, new AdminModule(L10n, "EditCustomFields", "Administration.LBL_EDIT_CUSTOM_FIELDS"        , "Administration.DESC_EDIT_CUSTOM_FIELDS"     , "List"              )
							, new AdminModule(L10n, "Modules"        , "Administration.LBL_CONFIGURE_TABS"             , "Administration.LBL_CHOOSE_WHICH"            , "ConfigureTabs"     , "ConfigureTabs.gif")
							, new AdminModule(L10n, "Terminology"    , "Administration.LBL_RENAME_TABS"               , "Administration.LBL_CHANGE_NAME_TABS"        , "RenameTabs"        , "RenameTabs.gif"  )
							, new AdminModule(L10n, "Languages"      , "Administration.LBL_MANAGE_LANGUAGES"           , "Administration.LBL_MANAGE_LANGUAGES"        , "List"              , "LanguagePacks.gif")
							, new AdminModule(L10n, "Tags"           , "Administration.LBL_MANAGE_TAGS_TITLE"          , "Administration.LBL_MANAGE_TAGS"             , 28, dictModuleTabOrder, "Tags.LNK_NEW_TAG", "Tags.png")
							, new AdminModule(L10n, "ModuleBuilder"  , "Administration.LBL_MODULE_BUILDER_TITLE"       , "Administration.LBL_MODULE_BUILDER"          , "EditView"          , "Administration.gif")
							};
							BuildAdminModules(L10n, objs, "StudioView", "Administration.LBL_STUDIO_TITLE", arr);
							break;
						}
						case "ProductsView":
						{
							AdminModule[] arr = new AdminModule[]
							{ new AdminModule(L10n, "ProductTemplates" , "Administration.LBL_PRODUCT_TEMPLATES_TITLE" , "Administration.LBL_PRODUCT_TEMPLATES_DESC" , 30, dictModuleTabOrder, "ProductTemplates.LNK_NEW_PRODUCT_TEMPLATE" )
							, new AdminModule(L10n, "Manufacturers"    , "Administration.LBL_MANUFACTURERS_TITLE"     , "Administration.LBL_MANUFACTURERS_DESC"     , 31, dictModuleTabOrder, "Manufacturers.LNK_NEW_MANUFACTURER"        )
							, new AdminModule(L10n, "ProductCategories", "Administration.LBL_PRODUCT_CATEGORIES_TITLE", "Administration.LBL_PRODUCT_CATEGORIES_DESC", 32, dictModuleTabOrder, "ProductCategories.LNK_NEW_PRODUCT_CATEGORY")
							, new AdminModule(L10n, "TaxRates"         , "Administration.LBL_TAX_RATES_TITLE"         , "Administration.LBL_TAX_RATES_DESC"         , 35, dictModuleTabOrder, "TaxRates.LNK_NEW_TAX_RATE"                 )
							, new AdminModule(L10n, "Discounts"        , "Administration.LBL_DISCOUNTS_TITLE"         , "Administration.LBL_DISCOUNTS_DESC"         , 36, dictModuleTabOrder, "Discounts.LNK_NEW_DISCOUNT"                )
							};
							BuildAdminModules(L10n, objs, "ProductsView", "Administration.LBL_PRODUCTS_QUOTES_TITLE", arr);
							break;
						}
						case "EmailsView":
						{
							AdminModule[] arr = new AdminModule[]
							{ new AdminModule(L10n, "EmailMan"    , "Administration.LBL_MASS_EMAIL_CONFIG_TITLE" , "Administration.LBL_MASS_EMAIL_CONFIG_DESC" , "ConfigView")
							, new AdminModule(L10n, "EmailMan"    , "Administration.LBL_MASS_EMAIL_MANAGER_TITLE", "Administration.LBL_MASS_EMAIL_MANAGER_DESC", "List"       )
							, new AdminModule(L10n, "InboundEmail", "Administration.LBL_INBOUND_EMAIL_TITLE"     , "Administration.LBL_MAILBOX_DESC"           , 40, dictModuleTabOrder, "InboundEmail.LNK_LIST_CREATE_NEW")
							, new AdminModule(L10n, "OutboundEmail", "Administration.LBL_OUTBOUND_EMAIL_TITLE"   , "Administration.LBL_OUTBOUND_EMAIL_DESC"    , 41, dictModuleTabOrder, "OutboundEmail.LNK_NEW_OUTBOUND_EMAIL", "InboundEmail.gif")
							};
							BuildAdminModules(L10n, objs, "EmailsView", "Administration.LBL_EMAIL_TITLE", arr);
							break;
						}
					}
				}
			}
			catch (Exception ex) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return objs;
		}

		/// <summary>Builds a menu category from an array of admin modules.</summary>
		private void BuildAdminModules(L10N L10n, List<Dictionary<string, object>> objs,
		                               string sCategory, string sCategoryTitle, AdminModule[] arrModules)
		{
			var modules = new List<Dictionary<string, object>>();
			foreach (AdminModule module in arrModules)
			{
				if (Sql.IsEmptyString(module.MODULE_NAME)) continue;
				modules.Add(new Dictionary<string, object>
				{
					{ "MODULE_NAME" , module.MODULE_NAME                                                           },
					{ "DISPLAY_NAME", L10n.Term(module.DISPLAY_NAME)                                              },
					{ "DESCRIPTION" , L10n.Term(module.DESCRIPTION )                                              },
					{ "MENU_ENABLED", module.MENU_ENABLED                                                         },
					{ "ADMIN_ROUTE" , module.ADMIN_ROUTE                                                          },
					{ "ICON_NAME"   , module.ICON_NAME                                                            },
					{ "EDIT_LABEL"  , Sql.IsEmptyString(module.EDIT_LABEL) ? String.Empty : L10n.Term(module.EDIT_LABEL) },
					{ "TAB_ORDER"   , module.TAB_ORDER                                                            }
				});
			}
			if (modules.Count > 0)
			{
				objs.Add(new Dictionary<string, object>
				{
					{ "NAME"   , sCategory                 },
					{ "TITLE"  , L10n.Term(sCategoryTitle) },
					{ "MODULES", modules                   }
				});
			}
		}

		/// <summary>Clears all rows from a layout table for a given view name. Source: lines 3359-3391.</summary>
		private void ClearLayoutTable(IDbConnection con, IDbTransaction trn, string sTableName, string sViewName)
		{
			if (Sql.IsEmptyString(sViewName)) return;
			if (!Regex.IsMatch(sTableName, @"^[A-Za-z0-9_]+$")) return;
			string sKeyField = sTableName.ToUpper() == "GRIDVIEWS_COLUMNS" ? "GRID_NAME" :
			                   (sTableName.ToUpper() == "DETAILVIEWS_FIELDS" ? "DETAIL_NAME" : "EDIT_NAME");
			using IDbCommand cmd = con.CreateCommand();
			cmd.CommandText = "delete from " + sTableName + " where " + sKeyField + " = @VIEW_NAME";
			if (cmd is IDbCommand) cmd.Transaction = trn;
			Sql.AddParameter(cmd, "@VIEW_NAME", sViewName);
			cmd.ExecuteNonQuery();
		}

		/// <summary>Updates layout rows by calling the appropriate stored procedure per row. Source: lines 3393-3471.</summary>
		private void UpdateLayoutTable(IDbConnection con, IDbTransaction trn, string sTableName, Dictionary<string, object> dict)
		{
			if (!Regex.IsMatch(sTableName, @"^[A-Za-z0-9_]+$")) return;
			string sViewKeyField = sTableName.ToUpper() == "GRIDVIEWS_COLUMNS" ? "GRID_NAME" :
			                       (sTableName.ToUpper() == "DETAILVIEWS_FIELDS" ? "DETAIL_NAME" : "EDIT_NAME");
			string sViewName = Sql.ToString(dict.ContainsKey(sViewKeyField) ? dict[sViewKeyField] : null);
			// Clear the existing rows first
			ClearLayoutTable(con, trn, sTableName, sViewName);
			// Re-insert rows from the "rows" array
			var rows = new List<Dictionary<string, object>>();
			if (dict.ContainsKey("rows") && dict["rows"] is JArray jArr)
				foreach (JObject j in jArr)
					rows.Add(j.ToObject<Dictionary<string, object>>());
			else if (dict.ContainsKey("rows") && dict["rows"] is List<object> oList)
				foreach (var o in oList)
					if (o is JObject j2) rows.Add(j2.ToObject<Dictionary<string, object>>());
			foreach (var row in rows)
			{
				using IDbCommand cmd = SqlProcs.Factory(con, "sp" + sTableName + "_Update");
				cmd.Transaction = trn;
				foreach (string sKey in row.Keys)
				{
					IDbDataParameter par = Sql.FindParameter(cmd, "@" + sKey);
					if (par != null) Sql.SetParameter(par, Sql.ToString(row[sKey]));
				}
				cmd.ExecuteNonQuery();
			}
		}

		/// <summary>Updates event-based layout rows (relationships / dynamic buttons). Source: lines 3393-3471.</summary>
		private void UpdateLayoutEvents(IDbConnection con, IDbTransaction trn, string sTableName, Dictionary<string, object> dict)
		{
			if (!Regex.IsMatch(sTableName, @"^[A-Za-z0-9_]+$")) return;
			var rows = new List<Dictionary<string, object>>();
			if (dict.ContainsKey("rows") && dict["rows"] is JArray jArr2)
				foreach (JObject j in jArr2)
					rows.Add(j.ToObject<Dictionary<string, object>>());
			foreach (var row in rows)
			{
				using IDbCommand cmd = SqlProcs.Factory(con, "sp" + sTableName + "_Update");
				cmd.Transaction = trn;
				foreach (string sKey in row.Keys)
				{
					IDbDataParameter par = Sql.FindParameter(cmd, "@" + sKey);
					if (par != null) Sql.SetParameter(par, Sql.ToString(row[sKey]));
				}
				cmd.ExecuteNonQuery();
			}
		}

		/// <summary>Generic admin table update via stored procedure. Source: lines 3708-4123.</summary>
		private void UpdateAdminTableInternal(IDbConnection con, IDbTransaction trn, string sTableName, Dictionary<string, object> dict)
		{
			if (!Regex.IsMatch(sTableName, @"^[A-Za-z0-9_]+$")) return;
			using IDbCommand cmd = SqlProcs.Factory(con, "sp" + sTableName + "_Update");
			cmd.Transaction = trn;
			foreach (string sKey in dict.Keys)
			{
				if (sKey == "TableName") continue;
				IDbDataParameter par = Sql.FindParameter(cmd, "@" + sKey);
				if (par != null) Sql.SetParameter(par, Sql.ToString(dict[sKey]));
			}
			cmd.ExecuteNonQuery();
		}


		/// <summary>Returns all users for admin forms.</summary>
		private List<object> GetAllUsersInternal()
		{
			var list = new List<object>();
			try
			{
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText = "select ID, USER_NAME, FIRST_NAME, LAST_NAME, FULL_NAME from vwUSERS_List where STATUS = 'Active' order by LAST_NAME, FIRST_NAME";
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				foreach (DataRow row in dt.Rows)
				{
					var d = new Dictionary<string, object>();
					for (int j = 0; j < dt.Columns.Count; j++)
						d[dt.Columns[j].ColumnName] = row[j] == DBNull.Value ? null : row[j];
					list.Add(d);
				}
			}
			catch (Exception ex) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return list;
		}

		/// <summary>Returns all teams for admin forms.</summary>
		private List<object> GetAllTeamsInternal()
		{
			var list = new List<object>();
			try
			{
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText = "select ID, NAME, DESCRIPTION from vwTEAMS order by NAME";
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				foreach (DataRow row in dt.Rows)
				{
					var d = new Dictionary<string, object>();
					for (int j = 0; j < dt.Columns.Count; j++)
						d[dt.Columns[j].ColumnName] = row[j] == DBNull.Value ? null : row[j];
					list.Add(d);
				}
			}
			catch (Exception ex) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return list;
		}

		/// <summary>Delegates to SplendidCache.GetAllTabMenus.</summary>
		private Dictionary<Guid, DataTable> GetAllTabMenusInternal()
		{
			return _splendidCache.GetAllTabMenus();
		}

		/// <summary>Returns last-viewed records for current user.</summary>
		private Dictionary<string, DataTable> GetAllLastViewedInternal()
		{
			return _splendidCache.GetAllLastViewed();
		}

		/// <summary>Returns search column metadata for given module list.</summary>
		private Dictionary<string, object> GetAllSearchColumnsInternal(List<string> lstMODULES)
		{
			var objs = new Dictionary<string, object>();
			try
			{
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText = "select * from vwMODULES_FIELDS order by MODULE_NAME";
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				string sLAST = String.Empty;
				List<Dictionary<string, object>> modList = null;
				foreach (DataRow row in dt.Rows)
				{
					string sMOD = Sql.ToString(row["MODULE_NAME"]);
					if (!lstMODULES.Contains(sMOD)) continue;
					if (sLAST != sMOD) { sLAST = sMOD; modList = new List<Dictionary<string, object>>(); objs[sMOD] = modList; }
					var d = new Dictionary<string, object>();
					for (int j = 0; j < dt.Columns.Count; j++)
						d[dt.Columns[j].ColumnName] = row[j] == DBNull.Value ? null : row[j];
					modList?.Add(d);
				}
			}
			catch (Exception ex) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return objs;
		}

		/// <summary>Returns grid view definitions (name + sort) for given module list.</summary>
		private Dictionary<string, object> GetAllGridViewsInternal(List<string> lstMODULES)
		{
			var objs = new Dictionary<string, object>();
			try
			{
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText = "select * from vwGRIDVIEWS order by NAME";
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				foreach (DataRow row in dt.Rows)
				{
					string sGRID_NAME = Sql.ToString(row["NAME"]);
					string sMOD = ExtractModuleName(sGRID_NAME);
					if (!lstMODULES.Contains(sMOD)) continue;
					var d = new Dictionary<string, object>();
					for (int j = 0; j < dt.Columns.Count; j++)
					{
						if (dt.Columns[j].ColumnName == "ID") continue;
						d[dt.Columns[j].ColumnName] = row[j] == DBNull.Value ? null : row[j];
					}
					if (!objs.ContainsKey(sGRID_NAME)) objs[sGRID_NAME] = d;
				}
			}
			catch (Exception ex) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return objs;
		}

		/// <summary>Returns all shortcut definitions for given module list.</summary>
		private Dictionary<string, object> GetAllShortcutsInternal(List<string> lstMODULES)
		{
			var objs = new Dictionary<string, object>();
			try
			{
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText = "select * from vwSHORTCUTS order by MODULE_NAME, SHORT_CUT_ORDER";
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				string sLAST = String.Empty;
				List<Dictionary<string, object>> modList = null;
				foreach (DataRow row in dt.Rows)
				{
					string sMOD = Sql.ToString(row["MODULE_NAME"]);
					if (!lstMODULES.Contains(sMOD)) continue;
					if (sLAST != sMOD) { sLAST = sMOD; modList = new List<Dictionary<string, object>>(); objs[sMOD] = modList; }
					var d = new Dictionary<string, object>();
					for (int j = 0; j < dt.Columns.Count; j++)
						d[dt.Columns[j].ColumnName] = row[j] == DBNull.Value ? null : row[j];
					modList?.Add(d);
				}
			}
			catch (Exception ex) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return objs;
		}

		/// <summary>Returns tax rates list.</summary>
		private List<object> GetTaxRatesInternal()
		{
			var list = new List<object>();
			try
			{
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText = "select * from vwTAX_RATES order by NAME";
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				foreach (DataRow row in dt.Rows)
				{
					var d = new Dictionary<string, object>();
					for (int j = 0; j < dt.Columns.Count; j++)
						d[dt.Columns[j].ColumnName] = row[j] == DBNull.Value ? null : row[j];
					list.Add(d);
				}
			}
			catch (Exception ex) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return list;
		}

		/// <summary>Returns discounts list.</summary>
		private List<object> GetDiscountsInternal()
		{
			var list = new List<object>();
			try
			{
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText = "select * from vwDISCOUNTS order by NAME";
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				foreach (DataRow row in dt.Rows)
				{
					var d = new Dictionary<string, object>();
					for (int j = 0; j < dt.Columns.Count; j++)
						d[dt.Columns[j].ColumnName] = row[j] == DBNull.Value ? null : row[j];
					list.Add(d);
				}
			}
			catch (Exception ex) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return list;
		}

		/// <summary>Returns timezone list.</summary>
		private Dictionary<string, object> GetTimezonesInternal()
		{
			var objs = new Dictionary<string, object>();
			try
			{
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText = "select * from vwTIMEZONES order by BIAS";
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				foreach (DataRow row in dt.Rows)
				{
					string sID = Sql.ToString(row["ID"]);
					if (!Sql.IsEmptyString(sID) && !objs.ContainsKey(sID))
					{
						var d = new Dictionary<string, object>();
						for (int j = 0; j < dt.Columns.Count; j++)
							d[dt.Columns[j].ColumnName] = row[j] == DBNull.Value ? null : row[j];
						objs[sID] = d;
					}
				}
			}
			catch (Exception ex) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return objs;
		}

		/// <summary>Returns currencies list.</summary>
		private Dictionary<string, object> GetCurrenciesInternal()
		{
			var objs = new Dictionary<string, object>();
			try
			{
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText = "select * from vwCURRENCIES order by NAME";
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				foreach (DataRow row in dt.Rows)
				{
					string sID = Sql.ToString(row["ID"]);
					if (!Sql.IsEmptyString(sID) && !objs.ContainsKey(sID))
					{
						var d = new Dictionary<string, object>();
						for (int j = 0; j < dt.Columns.Count; j++)
							d[dt.Columns[j].ColumnName] = row[j] == DBNull.Value ? null : row[j];
						objs[sID] = d;
					}
				}
			}
			catch (Exception ex) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return objs;
		}

		/// <summary>Returns enabled languages list.</summary>
		private Dictionary<string, object> GetLanguagesInternal()
		{
			var objs = new Dictionary<string, object>();
			try
			{
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText = "select * from vwLANGUAGES where ACTIVE = 1 order by NAME";
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				foreach (DataRow row in dt.Rows)
				{
					string sName = Sql.ToString(row["NAME"]);
					if (!Sql.IsEmptyString(sName) && !objs.ContainsKey(sName))
					{
						var d = new Dictionary<string, object>();
						for (int j = 0; j < dt.Columns.Count; j++)
							d[dt.Columns[j].ColumnName] = row[j] == DBNull.Value ? null : row[j];
						objs[sName] = d;
					}
				}
			}
			catch (Exception ex) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return objs;
		}

		/// <summary>Returns favorites for current user grouped by module.</summary>
		private Dictionary<string, object> GetAllFavoritesInternal()
		{
			var objs = new Dictionary<string, object>();
			try
			{
				Guid gUSER_ID = _security.USER_ID;
				if (Sql.IsEmptyGuid(gUSER_ID)) return objs;
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText = "select * from vwSAVED_SEARCH where ASSIGNED_USER_ID = @USER_ID and SEARCH_NAME = 'Favorites' order by MODULE_NAME";
				Sql.AddParameter(cmd, "@USER_ID", gUSER_ID);
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				foreach (DataRow row in dt.Rows)
				{
					string sMOD = Sql.ToString(row["MODULE_NAME"]);
					if (!objs.ContainsKey(sMOD))
					{
						var d = new Dictionary<string, object>();
						for (int j = 0; j < dt.Columns.Count; j++)
							d[dt.Columns[j].ColumnName] = row[j] == DBNull.Value ? null : row[j];
						objs[sMOD] = d;
					}
				}
			}
			catch (Exception ex) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return objs;
		}

		/// <summary>Returns saved searches for current user grouped by module.</summary>
		private Dictionary<string, object> GetAllSavedSearchInternal()
		{
			var objs = new Dictionary<string, object>();
			try
			{
				Guid gUSER_ID = _security.USER_ID;
				if (Sql.IsEmptyGuid(gUSER_ID)) return objs;
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText = "select * from vwSAVED_SEARCH where ASSIGNED_USER_ID = @USER_ID and SEARCH_NAME <> 'Favorites' order by MODULE_NAME, NAME";
				Sql.AddParameter(cmd, "@USER_ID", gUSER_ID);
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				string sLAST = String.Empty;
				List<Dictionary<string, object>> modList = null;
				foreach (DataRow row in dt.Rows)
				{
					string sMOD = Sql.ToString(row["MODULE_NAME"]);
					if (sLAST != sMOD) { sLAST = sMOD; modList = new List<Dictionary<string, object>>(); objs[sMOD] = modList; }
					var d = new Dictionary<string, object>();
					for (int j = 0; j < dt.Columns.Count; j++)
						d[dt.Columns[j].ColumnName] = row[j] == DBNull.Value ? null : row[j];
					modList?.Add(d);
				}
			}
			catch (Exception ex) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return objs;
		}

		/// <summary>Returns dashboards for current user.</summary>
		private List<object> GetAllDashboardsInternal()
		{
			var list = new List<object>();
			try
			{
				Guid gUSER_ID = _security.USER_ID;
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText = "select * from vwDASHBOARDS where ASSIGNED_USER_ID = @USER_ID order by NAME";
				Sql.AddParameter(cmd, "@USER_ID", gUSER_ID);
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				foreach (DataRow row in dt.Rows)
				{
					var d = new Dictionary<string, object>();
					for (int j = 0; j < dt.Columns.Count; j++)
						d[dt.Columns[j].ColumnName] = row[j] == DBNull.Value ? null : row[j];
					list.Add(d);
				}
			}
			catch (Exception ex) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return list;
		}

		/// <summary>Returns dashboard panels grouped by dashboard ID.</summary>
		private Dictionary<string, object> GetAllDashboardPanelsInternal(List<string> lstMODULES)
		{
			var objs = new Dictionary<string, object>();
			try
			{
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText = "select * from vwDASHBOARDS_PANELS order by DASHBOARD_ID, PANEL_ORDER";
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				string sLAST = String.Empty;
				List<Dictionary<string, object>> panelList = null;
				foreach (DataRow row in dt.Rows)
				{
					string sDashID = Sql.ToString(row["DASHBOARD_ID"]);
					if (sLAST != sDashID) { sLAST = sDashID; panelList = new List<Dictionary<string, object>>(); objs[sDashID] = panelList; }
					var d = new Dictionary<string, object>();
					for (int j = 0; j < dt.Columns.Count; j++)
						d[dt.Columns[j].ColumnName] = row[j] == DBNull.Value ? null : row[j];
					panelList?.Add(d);
				}
			}
			catch (Exception ex) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return objs;
		}

		/// <summary>Returns user email signatures for current user.</summary>
		private List<object> GetUserSignaturesInternal()
		{
			var list = new List<object>();
			try
			{
				Guid gUSER_ID = _security.USER_ID;
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText = "select * from vwUSERS_SIGNATURES where CREATED_BY = @USER_ID order by NAME";
				Sql.AddParameter(cmd, "@USER_ID", gUSER_ID);
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				foreach (DataRow row in dt.Rows)
				{
					var d = new Dictionary<string, object>();
					for (int j = 0; j < dt.Columns.Count; j++)
						d[dt.Columns[j].ColumnName] = row[j] == DBNull.Value ? null : row[j];
					list.Add(d);
				}
			}
			catch (Exception ex) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return list;
		}

		/// <summary>Returns outbound email accounts.</summary>
		private List<object> GetOutboundMailInternal()
		{
			var list = new List<object>();
			try
			{
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText = "select * from vwOUTBOUND_EMAIL order by NAME";
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				foreach (DataRow row in dt.Rows)
				{
					var d = new Dictionary<string, object>();
					for (int j = 0; j < dt.Columns.Count; j++)
						d[dt.Columns[j].ColumnName] = row[j] == DBNull.Value ? null : row[j];
					list.Add(d);
				}
			}
			catch (Exception ex) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return list;
		}

		/// <summary>Returns outbound SMS accounts.</summary>
		private List<object> GetOutboundSmsInternal()
		{
			var list = new List<object>();
			try
			{
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText = "select * from vwOUTBOUND_SMS order by NAME";
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				foreach (DataRow row in dt.Rows)
				{
					var d = new Dictionary<string, object>();
					for (int j = 0; j < dt.Columns.Count; j++)
						d[dt.Columns[j].ColumnName] = row[j] == DBNull.Value ? null : row[j];
					list.Add(d);
				}
			}
			catch (Exception ex) { SplendidError.SystemError(new StackFrame(1, true), ex); }
			return list;
		}

		// =====================================================================
		// Missing WCF operations — added to achieve 100% admin operation coverage.
		// Per AAP Goal 4: ALL 65 admin WCF [OperationContract] operations must have
		// corresponding ASP.NET Core controller actions.
		// =====================================================================

		/// <summary>GET Administration/Rest.svc/GetAdminLayoutRelationshipFields — Returns SQL column metadata for a relationship view table.</summary>
		[HttpGet("GetAdminLayoutRelationshipFields")]
		public IActionResult GetAdminLayoutRelationshipFields(string TableName, string ModuleName)
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				if (Sql.IsEmptyString(TableName))
					return BadRequest(new { error = "The table name must be specified." });
				if (!TableName.StartsWith("vw"))
					return BadRequest(new { error = "The table name is not in the correct format." });
				string sTABLE_NAME = Regex.Replace(TableName, @"[^A-Za-z0-9_]", "");

				List<LayoutField> lstFields = new List<LayoutField>();
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using (IDbConnection con = dbf.CreateConnection())
				{
					con.Open();
					string sSQL = "select *                        " + ControlChars.CrLf
					            + "  from vwSqlColumns             " + ControlChars.CrLf
					            + " where ObjectName = @OBJECTNAME " + ControlChars.CrLf
					            + "   and ObjectType = 'V'         " + ControlChars.CrLf
					            + " order by ColumnName            " + ControlChars.CrLf;
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@OBJECTNAME", Sql.MetadataName(cmd, sTABLE_NAME));
						using (DbDataAdapter da = dbf.CreateDataAdapter())
						{
							((IDbDataAdapter)da).SelectCommand = cmd;
							using (DataTable dt = new DataTable())
							{
								da.Fill(dt);
								dt.Columns.Add("FIELD_TYPE", typeof(System.String));
								dt.Columns.Add("DATA_LABEL", typeof(System.String));
								dt.Columns.Add("DATA_FIELD", typeof(System.String));
								foreach (DataRow row in dt.Rows)
								{
									string sColumnName = Sql.ToString(row["ColumnName"]);
									row["DATA_LABEL"] = Utils.BuildTermName(ModuleName, sColumnName);
									row["DATA_FIELD"] = sColumnName;
								}
								foreach (DataRow row in dt.Rows)
								{
									LayoutField lay  = new LayoutField();
									lay.ColumnName   = Sql.ToString (row["ColumnName"]);
									lay.ColumnType   = Sql.ToString (row["ColumnType"]);
									lay.CsType       = Sql.ToString (row["CsType"    ]);
									lay.length        = Sql.ToInteger(row["length"    ]);
									lay.DATA_LABEL   = Sql.ToString (row["DATA_LABEL"]);
									lay.DATA_FIELD   = Sql.ToString (row["DATA_FIELD"]);
									lstFields.Add(lay);
								}
							}
						}
					}
				}
				return JsonContent(new { d = lstFields });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "GetAdminLayoutRelationshipFields");
				return InternalError(ex);
			}
		}

		/// <summary>GET Administration/Rest.svc/GetAdminLayoutTerminologyLists — Returns all terminology list names for admin layout editor.</summary>
		[HttpGet("GetAdminLayoutTerminologyLists")]
		public IActionResult GetAdminLayoutTerminologyLists()
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				List<string> lstLists = new List<string>();
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using (IDbConnection con = dbf.CreateConnection())
				{
					con.Open();
					string sSQL = "select LIST_NAME                         " + ControlChars.CrLf
					            + "  from vwTERMINOLOGY                     " + ControlChars.CrLf
					            + " where LIST_NAME is not null             " + ControlChars.CrLf
					            + " group by LIST_NAME                      " + ControlChars.CrLf
					            + " order by LIST_NAME                      " + ControlChars.CrLf;
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						con.Open();
						using (IDataReader rdr = cmd.ExecuteReader())
						{
							while (rdr.Read())
								lstLists.Add(Sql.ToString(rdr["LIST_NAME"]));
						}
					}
				}
				return JsonContent(new { d = lstLists });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "GetAdminLayoutTerminologyLists");
				return InternalError(ex);
			}
		}

		/// <summary>GET Administration/Rest.svc/GetAdminLayoutTerminology — Returns combined terminology data for admin layout editing.</summary>
		[HttpGet("GetAdminLayoutTerminology")]
		public IActionResult GetAdminLayoutTerminology()
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				Dictionary<string, object> d = new Dictionary<string, object>();
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using (IDbConnection con = dbf.CreateConnection())
				{
					con.Open();
					// Return modules with their terminology keys
					string sSQL = "select MODULE_NAME          " + ControlChars.CrLf
					            + "     , DISPLAY_NAME         " + ControlChars.CrLf
					            + "  from vwTERMINOLOGY        " + ControlChars.CrLf
					            + " where LANG = @LANG         " + ControlChars.CrLf
					            + "   and LIST_NAME is null    " + ControlChars.CrLf
					            + " order by MODULE_NAME       " + ControlChars.CrLf;
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@LANG", sCulture);
						using (DbDataAdapter da = dbf.CreateDataAdapter())
						{
							((IDbDataAdapter)da).SelectCommand = cmd;
							using (DataTable dt = new DataTable())
							{
								da.Fill(dt);
								List<Dictionary<string, object>> lst = new List<Dictionary<string, object>>();
								foreach (DataRow row in dt.Rows)
								{
									Dictionary<string, object> item = new Dictionary<string, object>();
									item["MODULE_NAME"]  = Sql.ToString(row["MODULE_NAME" ]);
									item["DISPLAY_NAME"] = Sql.ToString(row["DISPLAY_NAME"]);
									lst.Add(item);
								}
								d.Add("d", lst);
							}
						}
					}
				}
				return JsonContent(d);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "GetAdminLayoutTerminology");
				return InternalError(ex);
			}
		}

		/// <summary>GET Administration/Rest.svc/GetAdminTable — Returns admin table data with OData-style query support.</summary>
		[HttpGet("GetAdminTable")]
		public IActionResult GetAdminTable(string TableName)
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				var req = _httpContextAccessor.HttpContext?.Request;
				int    nSKIP     = Sql.ToInteger(req?.Query["$skip"   ].FirstOrDefault());
				int    nTOP      = Sql.ToInteger(req?.Query["$top"    ].FirstOrDefault());
				string sFILTER   = Sql.ToString (req?.Query["$filter" ].FirstOrDefault());
				string sORDER_BY = Sql.ToString (req?.Query["$orderby"].FirstOrDefault());
				string sGROUP_BY = Sql.ToString (req?.Query["$groupby"].FirstOrDefault());
				string sSELECT   = Sql.ToString (req?.Query["$select" ].FirstOrDefault());

				SplendidCRM.TimeZone T10n = SplendidCRM.TimeZone.CreateTimeZone(_memoryCache, Sql.ToGuid(_httpContextAccessor.HttpContext?.Session?.GetString("USER_SETTINGS/TIMEZONE")));
				long nTotalCount = 0;
				StringBuilder sbDumpSQL = new StringBuilder();
				UniqueStringCollection arrSELECT = null;
				if (!Sql.IsEmptyString(sSELECT))
				{
					arrSELECT = new UniqueStringCollection();
					foreach (string s in sSELECT.Split(','))
						arrSELECT.Add(s.Trim());
				}
				DataTable dt = _restUtil.GetAdminTable(HttpContext, TableName, nSKIP, nTOP, sORDER_BY, sFILTER, sGROUP_BY, arrSELECT, null, ref nTotalCount, null, AccessMode.list, sbDumpSQL);
				string sBaseURI = req?.Scheme + "://" + req?.Host.Value + req?.PathBase.Value + "/Administration/Rest.svc/GetAdminTable";
				var rows = _restUtil.RowsToDictionary(sBaseURI, TableName, dt, T10n);
				return JsonContent(new { d = new { results = rows, __total = nTotalCount } });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "GetAdminTable");
				return InternalError(ex);
			}
		}

		/// <summary>GET Administration/Rest.svc/GetTeamTree — Returns team hierarchy tree for a given team.</summary>
		[HttpGet("GetTeamTree")]
		public IActionResult GetTeamTree(Guid ID)
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				List<Dictionary<string, object>> lstTree = new List<Dictionary<string, object>>();
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using (IDbConnection con = dbf.CreateConnection())
				{
					con.Open();
					string sSQL = "select ID              " + ControlChars.CrLf
					            + "     , PARENT_ID       " + ControlChars.CrLf
					            + "     , NAME            " + ControlChars.CrLf
					            + "  from vwTEAMS         " + ControlChars.CrLf
					            + " order by NAME         " + ControlChars.CrLf;
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						using (DbDataAdapter da = dbf.CreateDataAdapter())
						{
							((IDbDataAdapter)da).SelectCommand = cmd;
							using (DataTable dt = new DataTable())
							{
								da.Fill(dt);
								foreach (DataRow row in dt.Rows)
								{
									Dictionary<string, object> item = new Dictionary<string, object>();
									item["ID"]        = Sql.ToGuid  (row["ID"       ]);
									item["PARENT_ID"] = Sql.ToGuid  (row["PARENT_ID"]);
									item["NAME"]      = Sql.ToString(row["NAME"     ]);
									lstTree.Add(item);
								}
							}
						}
					}
				}
				return JsonContent(new { d = lstTree });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "GetTeamTree");
				return InternalError(ex);
			}
		}

		/// <summary>GET Administration/Rest.svc/GetModuleItem — Returns a single admin module item by ID.</summary>
		[HttpGet("GetModuleItem")]
		public IActionResult GetModuleItem(string ModuleName, Guid ID)
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				string sTableName = Crm.Modules.TableName(_memoryCache, ModuleName);
				string sBaseURI   = _httpContextAccessor.HttpContext?.Request?.Scheme + "://" + _httpContextAccessor.HttpContext?.Request?.Host.Value + _httpContextAccessor.HttpContext?.Request?.PathBase.Value + "/Administration/Rest.svc/GetModuleItem";
				SplendidCRM.TimeZone T10n = SplendidCRM.TimeZone.CreateTimeZone(_memoryCache, Sql.ToGuid(_httpContextAccessor.HttpContext?.Session?.GetString("USER_SETTINGS/TIMEZONE")));
				long nTotalCount = 0;
				StringBuilder sbDumpSQL = new StringBuilder();
				DataTable dt = _restUtil.GetAdminTable(HttpContext, sTableName, 0, 1, String.Empty, "ID eq '" + ID.ToString() + "'", String.Empty, null, null, ref nTotalCount, null, AccessMode.view, sbDumpSQL);
				Dictionary<string, object> d = new Dictionary<string, object>();
				if (dt.Rows.Count > 0)
					d.Add("d", _restUtil.ToJson(sBaseURI, ModuleName, dt.Rows[0], T10n));
				else
					d.Add("d", new Dictionary<string, object>());
				return JsonContent(d);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "GetModuleItem");
				return InternalError(ex);
			}
		}

		/// <summary>POST Administration/Rest.svc/UndeleteModule — Restores a soft-deleted admin module record.</summary>
		[HttpPost("UndeleteModule")]
		public async System.Threading.Tasks.Task<IActionResult> UndeleteModule()
		{
			try
			{
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !_security.IS_ADMIN)
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				string body = await ReadRequestBodyAsync();
				Dictionary<string, object> dict = DeserializeBody(body);
				string sModuleName = Sql.ToString(dict.ContainsKey("ModuleName") ? dict["ModuleName"] : null);
				Guid   gID         = Sql.ToGuid  (dict.ContainsKey("ID"        ) ? dict["ID"        ] : null);
				if (Sql.IsEmptyString(sModuleName) || Sql.IsEmptyGuid(gID))
					return BadRequest(new { error = "ModuleName and ID are required" });

				string sTABLE_NAME = Crm.Modules.TableName(_memoryCache, sModuleName);
				if (Sql.IsEmptyString(sTABLE_NAME))
					return BadRequest(new { error = "Unknown module: " + sModuleName });
				sTABLE_NAME = Regex.Replace(sTABLE_NAME, @"[^A-Za-z0-9_]", "");

				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using (IDbConnection con = dbf.CreateConnection())
				{
					con.Open();
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = "update " + sTABLE_NAME + " set DELETED = 0, DATE_MODIFIED = @DATE_MODIFIED, DATE_MODIFIED_UTC = @DATE_MODIFIED_UTC, MODIFIED_USER_ID = @MODIFIED_USER_ID where ID = @ID";
						Sql.AddParameter(cmd, "@DATE_MODIFIED"    , DateTime.Now           );
						Sql.AddParameter(cmd, "@DATE_MODIFIED_UTC", DateTime.UtcNow        );
						Sql.AddParameter(cmd, "@MODIFIED_USER_ID" , _security.USER_ID      );
						Sql.AddParameter(cmd, "@ID"               , gID                    );
						cmd.ExecuteNonQuery();
					}
				}
				return Ok(new { status = "undeleted" });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "UndeleteModule");
				return InternalError(ex);
			}
		}

		/// <summary>POST Administration/Rest.svc/DeleteAdminLayout — Deletes all fields for a layout view by TableName and ViewName.</summary>
		[HttpPost("DeleteAdminLayout")]
		public async System.Threading.Tasks.Task<IActionResult> DeleteAdminLayout()
		{
			try
			{
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !_security.IS_ADMIN)
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				string body = await ReadRequestBodyAsync();
				Dictionary<string, object> dict = DeserializeBody(body);
				string sTableName = Sql.ToString(dict.ContainsKey("TableName") ? dict["TableName"] : null);
				string sViewName  = Sql.ToString(dict.ContainsKey("ViewName" ) ? dict["ViewName" ] : null);
				if (Sql.IsEmptyString(sTableName))
					return BadRequest(new { error = "The table name must be specified." });
				if (Sql.IsEmptyString(sViewName) && sTableName != "TERMINOLOGY")
					return BadRequest(new { error = "The layout view name must be specified." });

				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using (IDbConnection con = dbf.CreateConnection())
				{
					con.Open();
					switch (sTableName)
					{
						case "EDITVIEWS_FIELDS"         : DeleteAdminLayoutTableInternal(con, dbf, "EDITVIEWS"  , "EDITVIEWS_FIELDS"  , "EDIT_NAME"  , sViewName); _splendidCache.ClearEditView  (sViewName); break;
						case "DETAILVIEWS_FIELDS"       : DeleteAdminLayoutTableInternal(con, dbf, "DETAILVIEWS", "DETAILVIEWS_FIELDS", "DETAIL_NAME", sViewName); _splendidCache.ClearDetailView(sViewName); break;
						case "GRIDVIEWS_COLUMNS"        : DeleteAdminLayoutTableInternal(con, dbf, "GRIDVIEWS"  , "GRIDVIEWS_COLUMNS" , "GRID_NAME"  , sViewName); _splendidCache.ClearGridView  (sViewName); break;
						case "DYNAMIC_BUTTONS"          : DeleteAdminLayoutTableInternal(con, dbf, null         , "DYNAMIC_BUTTONS"   , "VIEW_NAME"  , sViewName); _splendidCache.ClearDynamicButtons(sViewName); break;
						case "DETAILVIEWS_RELATIONSHIPS": DeleteAdminLayoutTableInternal(con, dbf, "DETAILVIEWS", "DETAILVIEWS_RELATIONSHIPS", "DETAIL_NAME", sViewName); _splendidCache.ClearDetailViewRelationships(); break;
						case "TERMINOLOGY":
						{
							using (IDbCommand cmd = con.CreateCommand())
							{
								cmd.CommandText = "delete from TERMINOLOGY where LANG = @LANG and MODULE_NAME = @MODULE_NAME and LIST_NAME is null";
								Sql.AddParameter(cmd, "@LANG", sCulture);
								Sql.AddParameter(cmd, "@MODULE_NAME", sViewName);
								cmd.ExecuteNonQuery();
							}
							_splendidCache.LoadTerminology(sCulture);
							break;
						}
						default: return BadRequest(new { error = "Unsupported table: " + sTableName });
					}
				}
				return Ok(new { status = "deleted" });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "DeleteAdminLayout");
				return InternalError(ex);
			}
		}

		/// <summary>Helper: Deletes all layout records for a given view and optionally the parent layout record.</summary>
		private void DeleteAdminLayoutTableInternal(IDbConnection con, DbProviderFactory dbf, string sPARENT_TABLE, string sTABLE_NAME, string sLAYOUT_NAME_FIELD, string sVIEW_NAME)
		{
			using (IDbCommand cmd = con.CreateCommand())
			{
				cmd.CommandText = "delete from " + sTABLE_NAME + " where " + sLAYOUT_NAME_FIELD + " = @VIEW_NAME";
				Sql.AddParameter(cmd, "@VIEW_NAME", sVIEW_NAME);
				cmd.ExecuteNonQuery();
			}
			if (!Sql.IsEmptyString(sPARENT_TABLE))
			{
				using (IDbCommand cmd = con.CreateCommand())
				{
					string sParentField = sTABLE_NAME == "DETAILVIEWS_RELATIONSHIPS" ? "DETAIL_NAME" : sLAYOUT_NAME_FIELD;
					cmd.CommandText = "delete from " + sPARENT_TABLE + " where " + sParentField + " = @VIEW_NAME";
					Sql.AddParameter(cmd, "@VIEW_NAME", sVIEW_NAME);
					cmd.ExecuteNonQuery();
				}
			}
		}

		/// <summary>POST Administration/Rest.svc/UpdateAdminEditCustomField — Updates (modifies) a custom field definition.
		/// Ported from SplendidCRM/Administration/Rest.svc.cs lines 5564-5667.</summary>
		[HttpPost("UpdateAdminEditCustomField")]
		public async System.Threading.Tasks.Task<IActionResult> UpdateAdminEditCustomField()
		{
			try
			{
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				string sModuleName = "EditCustomFields";
				int nACLACCESS = _security.AdminUserAccess(sModuleName, "edit");
				if ( !_security.IsAuthenticated() || nACLACCESS < 0 )
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") + ": " + sModuleName });

				string body = await ReadRequestBodyAsync();
				Dictionary<string, object> dict = DeserializeBody(body);
				Guid   gID             = Guid.Empty    ;
				Int32  nMAX_SIZE       = 0             ;
				bool   bREQUIRED       = false         ;
				bool   bAUDITED        = false         ;
				string sDEFAULT_VALUE  = String.Empty  ;
				string sDROPDOWN_LIST  = String.Empty  ;
				bool   bMASS_UPDATE    = false         ;
				foreach ( string sName in dict.Keys )
				{
					switch ( sName )
					{
						case "ID"           :  gID            = Sql.ToGuid   (dict[sName]);  break;
						case "MAX_SIZE"     :  nMAX_SIZE      = Sql.ToInteger(dict[sName]);  break;
						case "REQUIRED"     :  bREQUIRED      = Sql.ToBoolean(dict[sName]);  break;
						case "AUDITED"      :  bAUDITED       = Sql.ToBoolean(dict[sName]);  break;
						case "DEFAULT_VALUE":  sDEFAULT_VALUE = Sql.ToString (dict[sName]);  break;
						case "DROPDOWN_LIST":  sDROPDOWN_LIST = Sql.ToString (dict[sName]);  break;
						case "MASS_UPDATE"  :  bMASS_UPDATE   = Sql.ToBoolean(dict[sName]);  break;
					}
				}
				if ( Sql.IsEmptyGuid(gID) )
					throw new Exception("The ID must be specified.");

				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					using ( IDbTransaction trn = Sql.BeginTransaction(con) )
					{
						try
						{
							using ( IDbCommand cmd = con.CreateCommand() )
							{
								cmd.Transaction    = trn;
								cmd.CommandType    = CommandType.StoredProcedure;
								cmd.CommandText    = "spFIELDS_META_DATA_Update";
								cmd.CommandTimeout = 0;
								Sql.AddParameter(cmd, "@ID"               , gID                );
								Sql.AddParameter(cmd, "@MODIFIED_USER_ID" , _security.USER_ID  );
								Sql.AddParameter(cmd, "@MAX_SIZE"         , nMAX_SIZE          );
								Sql.AddParameter(cmd, "@REQUIRED"         , bREQUIRED          );
								Sql.AddParameter(cmd, "@AUDITED"          , bAUDITED           );
								Sql.AddParameter(cmd, "@DEFAULT_VALUE"    , sDEFAULT_VALUE, 255);
								Sql.AddParameter(cmd, "@DROPDOWN_LIST"    , sDROPDOWN_LIST,  50);
								Sql.AddParameter(cmd, "@MASS_UPDATE"      , bMASS_UPDATE       );
								Sql.AddParameter(cmd, "@DISABLE_RECOMPILE", true               );
								cmd.ExecuteNonQuery();
							}
							trn.Commit();
						}
						catch ( Exception ex )
						{
							trn.Rollback();
							throw new Exception(ex.Message, ex.InnerException);
						}
					}
				}
				// Trigger background recompile of views after schema change.
				if ( _memoryCache.Get<object>("System.Recompile.Start") == null )
				{
					// In the legacy code, ModuleUtils.EditCustomFields.RecompileViews was invoked on a separate thread.
					// For ASP.NET Core, we flag the recompile need in the cache for the scheduler to pick up.
					_memoryCache.Set("System.Recompile.Restart", true);
				}
				else
				{
					_memoryCache.Set("System.Recompile.Restart", true);
				}
				return Ok(new { d = gID });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "UpdateAdminEditCustomField");
				return InternalError(ex);
			}
		}

		/// <summary>GET Administration/Rest.svc/GetAclAccessFieldSecurity — Returns field-level ACL entries for a role and module.</summary>
		[HttpGet("GetAclAccessFieldSecurity")]
		public IActionResult GetAclAccessFieldSecurity(Guid ROLE_ID, string MODULE_NAME)
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				List<Dictionary<string, object>> lstAccess = new List<Dictionary<string, object>>();
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using (IDbConnection con = dbf.CreateConnection())
				{
					con.Open();
					string sSQL = "select ROLE_ID              " + ControlChars.CrLf
					            + "     , FIELD_NAME           " + ControlChars.CrLf
					            + "     , ACLACCESS            " + ControlChars.CrLf
					            + "  from vwACL_FIELD_ACCESS   " + ControlChars.CrLf
					            + " where ROLE_ID = @ROLE_ID   " + ControlChars.CrLf
					            + "   and MODULE_NAME = @MODULE_NAME " + ControlChars.CrLf
					            + " order by FIELD_NAME        " + ControlChars.CrLf;
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@ROLE_ID"    , ROLE_ID    );
						Sql.AddParameter(cmd, "@MODULE_NAME", MODULE_NAME);
						using (IDataReader rdr = cmd.ExecuteReader())
						{
							while (rdr.Read())
							{
								Dictionary<string, object> item = new Dictionary<string, object>();
								item["ROLE_ID"    ] = Sql.ToGuid   (rdr["ROLE_ID"   ]);
								item["FIELD_NAME" ] = Sql.ToString (rdr["FIELD_NAME" ]);
								item["ACLACCESS"  ] = Sql.ToInteger(rdr["ACLACCESS"  ]);
								lstAccess.Add(item);
							}
						}
					}
				}
				return JsonContent(new { d = lstAccess });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "GetAclAccessFieldSecurity");
				return InternalError(ex);
			}
		}

		/// <summary>POST Administration/Rest.svc/UpdateAclAccessFieldSecurity — Updates field-level ACL access entries for a role and module.</summary>
		[HttpPost("UpdateAclAccessFieldSecurity")]
		public async System.Threading.Tasks.Task<IActionResult> UpdateAclAccessFieldSecurity()
		{
			try
			{
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !_security.IS_ADMIN)
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				string body = await ReadRequestBodyAsync();
				Dictionary<string, object> dict = DeserializeBody(body);
				Guid   gROLE_ID     = Sql.ToGuid  (dict.ContainsKey("ROLE_ID"    ) ? dict["ROLE_ID"    ] : null);
				string sMODULE_NAME = Sql.ToString (dict.ContainsKey("MODULE_NAME") ? dict["MODULE_NAME"] : null);
				if (Sql.IsEmptyGuid(gROLE_ID) || Sql.IsEmptyString(sMODULE_NAME))
					return BadRequest(new { error = "ROLE_ID and MODULE_NAME are required" });

				// Expect an ACLACCESS array in the body
				var arrAccess = new List<Dictionary<string, object>>();
				if (dict.ContainsKey("ACLACCESS") && dict["ACLACCESS"] is JArray jArr)
				{
					foreach (JObject jObj in jArr)
						arrAccess.Add(jObj.ToObject<Dictionary<string, object>>());
				}

				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using (IDbConnection con = dbf.CreateConnection())
				{
					con.Open();
					using (IDbTransaction trn = con.BeginTransaction())
					{
						try
						{
							// Delete existing entries for this role and module
							using (IDbCommand cmd = con.CreateCommand())
							{
								cmd.Transaction = trn;
								cmd.CommandText = "delete from ACL_FIELDS where ROLE_ID = @ROLE_ID and MODULE_NAME = @MODULE_NAME and DELETED = 0";
								Sql.AddParameter(cmd, "@ROLE_ID"    , gROLE_ID    );
								Sql.AddParameter(cmd, "@MODULE_NAME", sMODULE_NAME);
								cmd.ExecuteNonQuery();
							}
							// Insert new entries
							foreach (var entry in arrAccess)
							{
								string sFIELD_NAME = Sql.ToString (entry.ContainsKey("FIELD_NAME") ? entry["FIELD_NAME"] : null);
								int    nACLACCESS  = Sql.ToInteger(entry.ContainsKey("ACLACCESS" ) ? entry["ACLACCESS" ] : null);
								if (!Sql.IsEmptyString(sFIELD_NAME))
								{
									using (IDbCommand cmd = con.CreateCommand())
									{
										cmd.Transaction = trn;
										cmd.CommandText = "insert into ACL_FIELDS (ID, CREATED_BY, DATE_ENTERED, MODIFIED_USER_ID, DATE_MODIFIED, DATE_MODIFIED_UTC, ROLE_ID, MODULE_NAME, FIELD_NAME, ACLACCESS, DELETED) values (@ID, @CREATED_BY, @DATE_ENTERED, @MODIFIED_USER_ID, @DATE_MODIFIED, @DATE_MODIFIED_UTC, @ROLE_ID, @MODULE_NAME, @FIELD_NAME, @ACLACCESS, 0)";
										Sql.AddParameter(cmd, "@ID"               , Guid.NewGuid()   );
										Sql.AddParameter(cmd, "@CREATED_BY"       , _security.USER_ID);
										Sql.AddParameter(cmd, "@DATE_ENTERED"     , DateTime.Now     );
										Sql.AddParameter(cmd, "@MODIFIED_USER_ID" , _security.USER_ID);
										Sql.AddParameter(cmd, "@DATE_MODIFIED"    , DateTime.Now     );
										Sql.AddParameter(cmd, "@DATE_MODIFIED_UTC", DateTime.UtcNow  );
										Sql.AddParameter(cmd, "@ROLE_ID"          , gROLE_ID         );
										Sql.AddParameter(cmd, "@MODULE_NAME"      , sMODULE_NAME     );
										Sql.AddParameter(cmd, "@FIELD_NAME"       , sFIELD_NAME      );
										Sql.AddParameter(cmd, "@ACLACCESS"        , nACLACCESS       );
										cmd.ExecuteNonQuery();
									}
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
				return Ok(new { status = "updated" });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "UpdateAclAccessFieldSecurity");
				return InternalError(ex);
			}
		}

		/// <summary>GET Administration/Rest.svc/GetAllReactCustomViews — Returns React custom view and dashlet mappings for admin.</summary>
		[HttpGet("GetAllReactCustomViews")]
		public IActionResult GetAllReactCustomViews()
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				Dictionary<string, object> objs = new Dictionary<string, object>();
				// React custom views and dashlet views are file-system mappings discovered at runtime.
				// The view data is sourced from the React SPA build output (Prompt 2 scope).
				// Return available modules as a stub for admin layout tooling.
				List<string> lstMODULES = _restUtil.AccessibleModules(HttpContext);
				objs.Add("MODULES", lstMODULES);
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
				return InternalError(ex);
			}
		}

	} // end class AdminRestController
} // end namespace SplendidCRM.Web.Controllers
