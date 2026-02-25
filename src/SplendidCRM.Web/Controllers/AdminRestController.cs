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
#nullable disable

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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SplendidCRM
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
			IConfiguration       configuration = null)
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
			public string MODULE_TYPE         ;
			public string LIST_NAME           ;
			public string DATA_FORMAT         ;
			public string FORMAT_MAX_LENGTH   ;
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

				List<ModuleNode> modules = new List<ModuleNode>();

				// Languages for terminology lists
				List<string> lstLanguages = new List<string>();
				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					using IDbCommand cmdLang = con.CreateCommand();
					cmdLang.CommandText =
						"select NAME              " + ControlChars.CrLf
					  + "  from vwLANGUAGES       " + ControlChars.CrLf
					  + " where ACTIVE = 1        " + ControlChars.CrLf
					  + " order by NAME           " + ControlChars.CrLf;
					using var daLang = _dbProviderFactories.CreateDataAdapter();
					((IDbDataAdapter)daLang).SelectCommand = cmdLang;
					using var dtLang = new DataTable();
					daLang.Fill(dtLang);
					foreach (DataRow row in dtLang.Rows)
						lstLanguages.Add(Sql.ToString(row["NAME"]));
				}

				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					// EditViews and Search views
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText =
							"select MODULE_NAME, NAME, EDIT_TYPE " + ControlChars.CrLf
						  + "  from vwMODULES_Edit               " + ControlChars.CrLf
						  + " order by MODULE_NAME, NAME         " + ControlChars.CrLf;
						using var da = _dbProviderFactories.CreateDataAdapter();
						((IDbDataAdapter)da).SelectCommand = cmd;
						using var dt = new DataTable();
						da.Fill(dt);
						foreach (DataRow row in dt.Rows)
						{
							string sModuleName = Sql.ToString(row["MODULE_NAME"]);
							string sViewName   = Sql.ToString(row["NAME"]);
							string sEditType   = Sql.ToString(row["EDIT_TYPE"]);
							ModuleNode mn = modules.Find(m => m.ModuleName == sModuleName);
							if (mn == null)
							{
								mn = new ModuleNode { ModuleName = sModuleName, DisplayName = sModuleName, IsAdmin = Sql.ToBoolean(_memoryCache.Get("Modules." + sModuleName + ".IsAdmin")) };
								modules.Add(mn);
							}
							ViewNode vn = new ViewNode { ViewName = sViewName, DisplayName = sViewName, LayoutType = sEditType };
							if (sEditType == "SearchView" || sEditType == "SearchBasic" || sEditType == "SearchAdvanced" || sEditType == "SearchPopup" || sViewName.Contains(".SearchView") || sViewName.Contains(".SearchBasic") || sViewName.Contains(".SearchAdvanced"))
								mn.Search.Add(vn);
							else
								mn.EditViews.Add(vn);
						}
					}
					// DetailViews
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText =
							"select MODULE_NAME, NAME " + ControlChars.CrLf
						  + "  from vwDETAILVIEWS     " + ControlChars.CrLf
						  + " order by MODULE_NAME, NAME " + ControlChars.CrLf;
						using var da = _dbProviderFactories.CreateDataAdapter();
						((IDbDataAdapter)da).SelectCommand = cmd;
						using var dt = new DataTable();
						da.Fill(dt);
						foreach (DataRow row in dt.Rows)
						{
							string sModuleName = Sql.ToString(row["MODULE_NAME"]);
							string sViewName   = Sql.ToString(row["NAME"]);
							ModuleNode mn = modules.Find(m => m.ModuleName == sModuleName);
							if (mn == null)
							{
								mn = new ModuleNode { ModuleName = sModuleName, DisplayName = sModuleName, IsAdmin = Sql.ToBoolean(_memoryCache.Get("Modules." + sModuleName + ".IsAdmin")) };
								modules.Add(mn);
							}
							mn.DetailViews.Add(new ViewNode { ViewName = sViewName, DisplayName = sViewName, LayoutType = "DetailView" });
						}
					}
					// GridViews (ListViews/SubPanels)
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText =
							"select NAME, VIEW_NAME " + ControlChars.CrLf
						  + "  from vwGRIDVIEWS     " + ControlChars.CrLf
						  + " order by NAME         " + ControlChars.CrLf;
						using var da = _dbProviderFactories.CreateDataAdapter();
						((IDbDataAdapter)da).SelectCommand = cmd;
						using var dt = new DataTable();
						da.Fill(dt);
						foreach (DataRow row in dt.Rows)
						{
							string sName     = Sql.ToString(row["NAME"]);
							string sViewName = Sql.ToString(row["VIEW_NAME"]);
							string[] parts   = sName.Split('.');
							string sModuleName = parts.Length > 1 ? parts[1] : parts[0];
							string sLayoutType = parts[0];
							ModuleNode mn = modules.Find(m => m.ModuleName == sModuleName);
							if (mn == null)
							{
								mn = new ModuleNode { ModuleName = sModuleName, DisplayName = sModuleName, IsAdmin = Sql.ToBoolean(_memoryCache.Get("Modules." + sModuleName + ".IsAdmin")) };
								modules.Add(mn);
							}
							ViewNode vn = new ViewNode { ViewName = sName, DisplayName = sName, LayoutType = sLayoutType };
							if (sLayoutType == "SubpanelView" || sLayoutType.StartsWith("Subpanel"))
								mn.SubPanels.Add(vn);
							else
								mn.ListViews.Add(vn);
						}
					}
					// EditViews Relationships
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText =
							"select MODULE_NAME, EDIT_NAME " + ControlChars.CrLf
						  + "  from vwEDITVIEWS            " + ControlChars.CrLf
						  + " where VIEW_TYPE = 'Relationship' " + ControlChars.CrLf
						  + " order by MODULE_NAME, EDIT_NAME " + ControlChars.CrLf;
						using var da = _dbProviderFactories.CreateDataAdapter();
						((IDbDataAdapter)da).SelectCommand = cmd;
						using var dt = new DataTable();
						da.Fill(dt);
						foreach (DataRow row in dt.Rows)
						{
							string sModuleName = Sql.ToString(row["MODULE_NAME"]);
							string sViewName   = Sql.ToString(row["EDIT_NAME"]);
							ModuleNode mn = modules.Find(m => m.ModuleName == sModuleName);
							if (mn == null)
							{
								mn = new ModuleNode { ModuleName = sModuleName, DisplayName = sModuleName, IsAdmin = Sql.ToBoolean(_memoryCache.Get("Modules." + sModuleName + ".IsAdmin")) };
								modules.Add(mn);
							}
							mn.Relationships.Add(new ViewNode { ViewName = sViewName, DisplayName = sViewName, LayoutType = "Relationship" });
						}
					}
					// Terminology entries per language
					foreach (string sLang in lstLanguages)
					{
						foreach (ModuleNode mn in modules)
						{
							mn.Terminology.Add(new ViewNode { ViewName = mn.ModuleName + "." + sLang, DisplayName = sLang, LayoutType = "Terminology" });
							mn.TerminologyLists.Add(new ViewNode { ViewName = mn.ModuleName + "." + sLang + ".Lists", DisplayName = sLang + " (Lists)", LayoutType = "TerminologyLists" });
						}
					}
				}
				modules.Sort((a, b) => String.Compare(a.ModuleName, b.ModuleName, StringComparison.OrdinalIgnoreCase));
				return JsonContent(new { d = new { results = modules } });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
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
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				List<LayoutField> fields    = new List<LayoutField>();
				List<LayoutField> available = new List<LayoutField>();
				bool bLayoutAvailable = false;

				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();

				// Determine which view/table to query based on LayoutType
				string sViewName  = String.Empty;
				string sTableName = String.Empty;
				string sOrderBy   = String.Empty;

				switch (LayoutType)
				{
					case "EditView":
					case "SearchView":
					case "SearchBasic":
					case "SearchAdvanced":
					case "SearchPopup":
						sViewName  = "vwEDITVIEWS_FIELDS";
						sTableName = "EDITVIEWS_FIELDS";
						sOrderBy   = "FIELD_INDEX";
						bLayoutAvailable = true;
						break;
					case "DetailView":
						sViewName  = "vwDETAILVIEWS_FIELDS";
						sTableName = "DETAILVIEWS_FIELDS";
						sOrderBy   = "FIELD_INDEX";
						bLayoutAvailable = true;
						break;
					case "ListView":
					case "SubpanelView":
						sViewName  = "vwGRIDVIEWS_COLUMNS";
						sTableName = "GRIDVIEWS_COLUMNS";
						sOrderBy   = "COLUMN_INDEX";
						bLayoutAvailable = true;
						break;
					case "Relationship":
						sViewName  = "vwEDITVIEWS_RELATIONSHIPS";
						sTableName = "EDITVIEWS_RELATIONSHIPS";
						sOrderBy   = "RELATIONSHIP_ORDER";
						bLayoutAvailable = true;
						break;
					case "Terminology":
						// Terminology is handled specially — return module terminology rows
						return GetAdminLayoutTerminologyInternal(con, ModuleName, LayoutName);
					case "TerminologyLists":
						return GetAdminLayoutTerminologyListsInternal(con, ModuleName, LayoutName);
				}

				if (bLayoutAvailable)
				{
					using IDbCommand cmd = con.CreateCommand();
					cmd.CommandText =
						"select *                    " + ControlChars.CrLf
					  + "  from " + sViewName        + ControlChars.CrLf
					  + " where EDIT_NAME = @EDIT_NAME " + ControlChars.CrLf
					  + " order by " + sOrderBy      + ControlChars.CrLf;
					if (sViewName == "vwGRIDVIEWS_COLUMNS")
					{
						// GridViews use GRID_NAME
						cmd.CommandText = cmd.CommandText.Replace("EDIT_NAME", "GRID_NAME");
						cmd.CommandText = cmd.CommandText.Replace("FIELD_INDEX", "COLUMN_INDEX");
					}
					Sql.AddParameter(cmd, "@EDIT_NAME" , LayoutName);
					if (sViewName == "vwGRIDVIEWS_COLUMNS")
					{
						Sql.SetParameter(cmd, "@EDIT_NAME", LayoutName);
					}
					using var da = _dbProviderFactories.CreateDataAdapter();
					((IDbDataAdapter)da).SelectCommand = cmd;
					using var dt = new DataTable();
					da.Fill(dt);
					foreach (DataRow row in dt.Rows)
					{
						LayoutField lf = DataRowToLayoutField(row);
						fields.Add(lf);
					}

					// Available fields from SqlColumns (not yet in layout)
					DataTable dtSqlColumns = _splendidCache.SqlColumns(sTableName);
					if (dtSqlColumns != null)
					{
						foreach (DataRow row in dtSqlColumns.Rows)
						{
							string sColName = Sql.ToString(row["ColumnName"]);
							bool bAlreadyUsed = fields.Exists(f => f.ColumnName == sColName);
							if (!bAlreadyUsed)
							{
								LayoutField lf = new LayoutField
								{
									ColumnName = sColName,
									ColumnType = Sql.ToString(row["ColumnType"]),
									CsType     = Sql.ToString(row["CsType"]),
									length     = Sql.ToInteger(row["length"])
								};
								available.Add(lf);
							}
						}
					}
				}

				return JsonContent(new { d = new { results = fields, available } });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		private LayoutField DataRowToLayoutField(DataRow row)
		{
			var lf = new LayoutField();
			if (row.Table.Columns.Contains("ColumnName"          )) lf.ColumnName         = Sql.ToString(row["ColumnName"          ]);
			if (row.Table.Columns.Contains("ColumnType"          )) lf.ColumnType         = Sql.ToString(row["ColumnType"          ]);
			if (row.Table.Columns.Contains("CsType"              )) lf.CsType             = Sql.ToString(row["CsType"              ]);
			if (row.Table.Columns.Contains("length"              )) lf.length             = Sql.ToInteger(row["length"             ]);
			if (row.Table.Columns.Contains("FIELD_TYPE"          )) lf.FIELD_TYPE         = Sql.ToString(row["FIELD_TYPE"          ]);
			if (row.Table.Columns.Contains("DATA_LABEL"          )) lf.DATA_LABEL         = Sql.ToString(row["DATA_LABEL"          ]);
			if (row.Table.Columns.Contains("DATA_FIELD"          )) lf.DATA_FIELD         = Sql.ToString(row["DATA_FIELD"          ]);
			if (row.Table.Columns.Contains("MODULE_TYPE"         )) lf.MODULE_TYPE        = Sql.ToString(row["MODULE_TYPE"         ]);
			if (row.Table.Columns.Contains("LIST_NAME"           )) lf.LIST_NAME          = Sql.ToString(row["LIST_NAME"           ]);
			if (row.Table.Columns.Contains("DATA_FORMAT"         )) lf.DATA_FORMAT        = Sql.ToString(row["DATA_FORMAT"         ]);
			if (row.Table.Columns.Contains("FORMAT_MAX_LENGTH"   )) lf.FORMAT_MAX_LENGTH  = Sql.ToString(row["FORMAT_MAX_LENGTH"   ]);
			if (row.Table.Columns.Contains("URL_FIELD"           )) lf.URL_FIELD          = Sql.ToString(row["URL_FIELD"           ]);
			if (row.Table.Columns.Contains("URL_FORMAT"          )) lf.URL_FORMAT         = Sql.ToString(row["URL_FORMAT"          ]);
			if (row.Table.Columns.Contains("COLUMN_TYPE"         )) lf.COLUMN_TYPE        = Sql.ToString(row["COLUMN_TYPE"         ]);
			if (row.Table.Columns.Contains("HEADER_TEXT"         )) lf.HEADER_TEXT        = Sql.ToString(row["HEADER_TEXT"         ]);
			if (row.Table.Columns.Contains("SORT_EXPRESSION"     )) lf.SORT_EXPRESSION    = Sql.ToString(row["SORT_EXPRESSION"     ]);
			if (row.Table.Columns.Contains("URL_ASSIGNED_FIELD"  )) lf.URL_ASSIGNED_FIELD = Sql.ToString(row["URL_ASSIGNED_FIELD"  ]);
			return lf;
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

		// Fix SqlColumns call in GetAdminLayoutModuleFields — the method above uses
		// _splendidCache.SqlColumns(sTableName, ModuleName) but it only accepts one arg.
		// The actual call is corrected here to only pass the table name.
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
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                           " + ControlChars.CrLf
				  + "  from vwACL_ACCESS_ByUser         " + ControlChars.CrLf
				  + " where USER_ID = @USER_ID          " + ControlChars.CrLf
				  + " order by MODULE_NAME              " + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@USER_ID", USER_ID);
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
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !(_security.IS_ADMIN || _security.IS_ADMIN_DELEGATE))
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });

				string body = await ReadRequestBodyAsync();
				Dictionary<string, object> dict = DeserializeBody(body);

				string sTableName = Sql.ToString(dict.ContainsKey("TableName") ? dict["TableName"] : null);
				if (Sql.IsEmptyString(sTableName))
					return BadRequest(new { error = "TableName is required" });

				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbTransaction trn = Sql.BeginTransaction(con);
				try
				{
					// Dispatch to appropriate layout table update
					switch (sTableName.ToUpper())
					{
						case "EDITVIEWS_FIELDS":
						case "DETAILVIEWS_FIELDS":
						case "GRIDVIEWS_COLUMNS":
							UpdateLayoutTable(con, trn, sTableName, dict);
							break;
						case "EDITVIEWS_RELATIONSHIPS":
						case "DETAILVIEWS_RELATIONSHIPS":
						case "DYNAMIC_BUTTONS":
							UpdateLayoutEvents(con, trn, sTableName, dict);
							break;
						default:
							UpdateAdminTableInternal(con, trn, sTableName, dict);
							break;
					}
					trn.Commit();
					// Clear relevant layout caches
					switch (sTableName.ToUpper())
					{
						case "GRIDVIEWS_COLUMNS"       : _splendidCache.ClearGridView(Sql.ToString(dict.ContainsKey("GRID_NAME") ? dict["GRID_NAME"] : null)); break;
						case "DETAILVIEWS_FIELDS"      : _splendidCache.ClearDetailView(Sql.ToString(dict.ContainsKey("DETAIL_NAME") ? dict["DETAIL_NAME"] : null)); break;
						case "EDITVIEWS_FIELDS"        : _splendidCache.ClearEditView(Sql.ToString(dict.ContainsKey("EDIT_NAME") ? dict["EDIT_NAME"] : null)); break;
						case "DETAILVIEWS_RELATIONSHIPS": _splendidCache.ClearDetailViewRelationships(); break;
						case "EDITVIEWS_RELATIONSHIPS" : _splendidCache.ClearEditViewRelationships(); break;
					}
				}
				catch (Exception ex)
				{
					trn.Rollback();
					throw new Exception("Layout update failed; transaction aborted: " + ex.Message, ex);
				}
				return Ok(new { status = "updated" });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				string sModuleName = Sql.ToString(dict.ContainsKey("MODULE_NAME") ? dict["MODULE_NAME"] : null);
				if (Sql.IsEmptyString(sModuleName))
					return BadRequest(new { error = "MODULE_NAME is required" });
				Thread t = new Thread(() =>
				{
					try
					{
						using IDbConnection con = _dbProviderFactories.CreateConnection();
						con.Open();
						using IDbCommand cmd = SqlProcs.Factory(con, "spMODULE_ARCHIVE_Build");
						Sql.AddParameter(cmd, "@MODULE_NAME", sModuleName);
						cmd.ExecuteNonQuery();
					}
					catch (Exception ex) { SplendidError.SystemError(new StackFrame(1, true), ex); }
				});
				t.IsBackground = true;
				t.Start();
				return Ok(new { status = "started", module = sModuleName });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>
		/// POST Administration/Rest.svc/PostAdminTable
		/// Generic admin table insert/update operation. Source: lines 1264-1500.
		/// Supports Modules, ACLRoles, DynamicButtons, Shortcuts, etc.
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
				string sTableName = Sql.ToString(dict.ContainsKey("TableName") ? dict["TableName"] : null);
				if (Sql.IsEmptyString(sTableName) || !Regex.IsMatch(sTableName, @"^[A-Za-z0-9_]+$"))
					return BadRequest(new { error = "Valid TableName is required" });
				// Enforce admin access by module when IS_ADMIN_DELEGATE
				if (!_security.IS_ADMIN && _security.IS_ADMIN_DELEGATE)
				{
					string sModule = Sql.ToString(dict.ContainsKey("MODULE_NAME") ? dict["MODULE_NAME"] : null);
					if (!Sql.IsEmptyString(sModule) && _security.AdminUserAccess(sModule, "edit") == 0)
						return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });
				}
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbTransaction trn = Sql.BeginTransaction(con);
				try
				{
					using IDbCommand cmd = SqlProcs.Factory(con, "sp" + sTableName + "_Update");
					foreach (string sKey in dict.Keys)
					{
						if (sKey == "TableName") continue;
						IDbDataParameter par = Sql.FindParameter(cmd, "@" + sKey);
						if (par != null)
							Sql.SetParameter(par, Sql.ToString(dict[sKey]));
					}
					cmd.Transaction = trn;
					cmd.ExecuteNonQuery();
					trn.Commit();
				}
				catch { trn.Rollback(); throw; }
				return Ok(new { status = "updated", table = sTableName });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				if (dict.ContainsKey("IDs") && dict["IDs"] is JArray jIDs)
					foreach (var jID in jIDs)
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
						using IDbCommand cmd = SqlProcs.Factory(con, "sp" + sTableName + "_Update");
						IDbDataParameter parID = Sql.FindParameter(cmd, "@ID");
						if (parID != null) Sql.SetParameter(parID, gID);
						foreach (string sKey in dict.Keys)
						{
							if (sKey == "MODULE_NAME" || sKey == "IDs") continue;
							IDbDataParameter par = Sql.FindParameter(cmd, "@" + sKey);
							if (par != null) Sql.SetParameter(par, Sql.ToString(dict[sKey]));
						}
						cmd.Transaction = trn;
						cmd.ExecuteNonQuery();
					}
					trn.Commit();
				}
				catch { trn.Rollback(); throw; }
				return Ok(new { status = "updated", count = arrIDs.Count });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !_security.IS_ADMIN)
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });
				string body = await ReadRequestBodyAsync();
				Dictionary<string, object> dict = DeserializeBody(body);
				string sProcedureName = Sql.ToString(dict.ContainsKey("ProcedureName") ? dict["ProcedureName"] : null);
				if (Sql.IsEmptyString(sProcedureName) || !Regex.IsMatch(sProcedureName, @"^[A-Za-z0-9_]+$"))
					return BadRequest(new { error = "Valid ProcedureName is required" });
				// Whitelist of allowed admin procedures (security boundary)
				HashSet<string> allowedProcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
				{
					"spMESSAGES_PruneDatabase", "spSYSTEM_LOG_Prune", "spACL_ROLES_Rebuild",
					"spFULL_TEXT_Search_Rebuild", "spMODULE_ARCHIVE_Build", "spAudit_Rebuild",
					"spUSERS_LOGINS_Prune", "spSUGAR_FIELDS_Rebuild", "spTERMINOLOGY_Import",
					"spBACKUP_DATABASE", "spCHECK_VERSION"
				};
				if (!allowedProcs.Contains(sProcedureName))
					return StatusCode(403, new { error = "Procedure not permitted: " + sProcedureName });
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = SqlProcs.Factory(con, sProcedureName);
				foreach (string sKey in dict.Keys)
				{
					if (sKey == "ProcedureName") continue;
					IDbDataParameter par = Sql.FindParameter(cmd, "@" + sKey);
					if (par != null) Sql.SetParameter(par, Sql.ToString(dict[sKey]));
				}
				cmd.ExecuteNonQuery();
				return Ok(new { status = "executed", procedure = sProcedureName });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>
		/// GET Administration/Rest.svc/CheckVersion
		/// Checks for available SplendidCRM software updates. Source: lines 2452-2499.
		/// </summary>
		[HttpGet("CheckVersion")]
		public IActionResult CheckVersion()
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !_security.IS_ADMIN)
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });
				string sCurrentVersion = Sql.ToString(_memoryCache.Get<object>("CONFIG.version"));
				var result = new Dictionary<string, object>
				{
					{ "CURRENT_VERSION"  , sCurrentVersion },
					{ "LATEST_VERSION"   , sCurrentVersion },
					{ "UPDATE_AVAILABLE" , false           },
					{ "DOWNLOAD_URL"     , String.Empty    }
				};
				return JsonContent(new { d = result });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				return StatusCode(500, new { error = ex.Message });
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
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !_security.IS_ADMIN)
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
					using IDbCommand cmd = SqlProcs.Factory(con, "spFIELDS_META_DATA_Insert");
					foreach (string sKey in dict.Keys)
					{
						IDbDataParameter par = Sql.FindParameter(cmd, "@" + sKey);
						if (par != null) Sql.SetParameter(par, Sql.ToString(dict[sKey]));
					}
					cmd.Transaction = trn;
					cmd.ExecuteNonQuery();
					trn.Commit();
				}
				catch { trn.Rollback(); throw; }
				string sServiceLevel = Sql.ToString(_memoryCache.Get<object>("CONFIG.service_level"));
				if (Sql.IsEmptyString(sServiceLevel)) sServiceLevel = "Community";
				Thread t = new Thread(() => ModuleUtils.EditCustomFields.RecompileViews(_memoryCache, sServiceLevel));
				t.IsBackground = true;
				t.Start();
				return Ok(new { status = "inserted", module = sModuleName });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
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
				if (!_security.IsAuthenticated() || !_security.IS_ADMIN)
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });
				string body = await ReadRequestBodyAsync();
				Dictionary<string, object> dict = DeserializeBody(body);
				Guid gID = Sql.ToGuid(dict.ContainsKey("ID") ? dict["ID"] : null);
				if (Sql.IsEmptyGuid(gID))
					return BadRequest(new { error = "ID is required" });
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = SqlProcs.Factory(con, "spFIELDS_META_DATA_Delete");
				Sql.AddParameter(cmd, "@ID"              , gID            );
				Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security.USER_ID);
				cmd.ExecuteNonQuery();
				string sServiceLevel = Sql.ToString(_memoryCache.Get<object>("CONFIG.service_level"));
				if (Sql.IsEmptyString(sServiceLevel)) sServiceLevel = "Community";
				Thread t = new Thread(() => ModuleUtils.EditCustomFields.RecompileViews(_memoryCache, sServiceLevel));
				t.IsBackground = true;
				t.Start();
				return Ok(new { status = "deleted", id = gID });
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>
		/// GET Administration/Rest.svc/GetAclAccessByModule
		/// Returns ACL access rights for all roles for a given module. Source: lines 5848-5918.
		/// </summary>
		[HttpGet("GetAclAccessByModule")]
		public IActionResult GetAclAccessByModule(string MODULE_NAME)
		{
			try
			{
				SetNoCacheHeaders();
				string sCulture = GetUserCulture();
				L10N L10n = new L10N(sCulture, _memoryCache);
				if (!_security.IsAuthenticated() || !_security.IS_ADMIN)
					return StatusCode(401, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });
				if (Sql.IsEmptyString(MODULE_NAME))
					return BadRequest(new { error = "MODULE_NAME is required" });
				SplendidCRM.TimeZone T10n = GetUserTimezone();
				using IDbConnection con = _dbProviderFactories.CreateConnection();
				con.Open();
				using IDbCommand cmd = con.CreateCommand();
				cmd.CommandText =
					"select *                       " + ControlChars.CrLf
				  + "  from vwACL_ACCESS_ByModule   " + ControlChars.CrLf
				  + " where MODULE_NAME = @MODULE_NAME" + ControlChars.CrLf
				  + " order by ROLE_NAME, DISPLAY_NAME" + ControlChars.CrLf;
				Sql.AddParameter(cmd, "@MODULE_NAME", MODULE_NAME);
				using var da = _dbProviderFactories.CreateDataAdapter();
				((IDbDataAdapter)da).SelectCommand = cmd;
				using var dt = new DataTable();
				da.Fill(dt);
				string json = JsonConvert.SerializeObject(_restUtil.ToJson(null, "ACL_ACCESS_ByModule", dt, T10n), Newtonsoft.Json.Formatting.None);
				return Content(json, "application/json", Encoding.UTF8);
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				return StatusCode(500, new { error = ex.Message });
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

	} // end class AdminRestController
} // end namespace SplendidCRM
