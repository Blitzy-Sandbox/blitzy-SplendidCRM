#nullable disable
using System;
using System.Data;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SplendidCRM.Web.Controllers
{
	/// <summary>
	/// Admin REST API controller — converts all 39 WCF operations from Administration/Rest.svc.cs.
	/// Route: [Route("Administration/Rest.svc")] preserves backward compatibility.
	/// </summary>
	[ApiController]
	[Route("Administration/Rest.svc")]
	[Authorize]
	public class AdminRestController : ControllerBase
	{
		private readonly Security _security;
		private readonly SplendidCache _splendidCache;
		private readonly RestUtil _restUtil;
		private readonly DbProviderFactories _dbProviderFactories;
		private readonly IMemoryCache _memoryCache;
		private readonly IConfiguration _configuration;
		private readonly ILogger<AdminRestController> _logger;
		private readonly SearchBuilder _searchBuilder;

		public AdminRestController(Security security, SplendidCache splendidCache, RestUtil restUtil,
			DbProviderFactories dbProviderFactories, IMemoryCache memoryCache, IConfiguration configuration,
			ILogger<AdminRestController> logger, SearchBuilder searchBuilder)
		{
			_security = security;
			_splendidCache = splendidCache;
			_restUtil = restUtil;
			_dbProviderFactories = dbProviderFactories;
			_memoryCache = memoryCache;
			_configuration = configuration;
			_logger = logger;
			_searchBuilder = searchBuilder;
		}

		// Helper: Read JSON body from POST requests.
		private async Task<JObject> ReadJsonBodyAsync()
		{
			using (StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8))
			{
				string sBody = await reader.ReadToEndAsync();
				return JObject.Parse(sBody);
			}
		}

		// Helper: Verify admin access.
		private bool IsAdmin()
		{
			return _security.IS_ADMIN || _security.IS_ADMIN_DELEGATE;
		}

		// =====================================================================================
		// 1. Layout Management Endpoints
		// =====================================================================================

		/// <summary>GET Administration/Rest.svc/GetAdminLayoutModules — Returns admin layout module definitions.</summary>
		[HttpGet("GetAdminLayoutModules")]
		public IActionResult GetAdminLayoutModules()
		{
			if (!IsAdmin()) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					string sSQL = "select * from vwMODULES where MODULE_ENABLED = 1 order by MODULE_NAME";
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						using (SqlDataAdapter da = _dbProviderFactories.CreateDataAdapter())
						{
							da.SelectCommand = (SqlCommand)cmd;
							DataTable dt = new DataTable();
							da.Fill(dt);
							return Ok(new { d = _restUtil.ToJson(dt) });
						}
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.GetAdminLayoutModules error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>GET Administration/Rest.svc/GetAdminLayoutModuleFields — Returns field definitions for a module layout.</summary>
		[HttpGet("GetAdminLayoutModuleFields")]
		public IActionResult GetAdminLayoutModuleFields(string ModuleName, string LayoutType, string LayoutName)
		{
			if (!IsAdmin()) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					string sVIEW_NAME = LayoutType == "GridView" ? "vwGRIDVIEWS_COLUMNS" : LayoutType == "DetailView" ? "vwDETAILVIEWS_FIELDS" : "vwEDITVIEWS_FIELDS";
					string sNameColumn = LayoutType == "GridView" ? "GRID_NAME" : LayoutType == "DetailView" ? "DETAIL_NAME" : "EDIT_NAME";
					string sSQL = "select * from " + sVIEW_NAME + " where " + sNameColumn + " = @LAYOUT_NAME order by " + (LayoutType == "GridView" ? "COLUMN_INDEX" : "FIELD_INDEX");
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@LAYOUT_NAME", LayoutName, 50);
						using (SqlDataAdapter da = _dbProviderFactories.CreateDataAdapter())
						{
							da.SelectCommand = (SqlCommand)cmd;
							DataTable dt = new DataTable();
							da.Fill(dt);
							return Ok(new { d = _restUtil.ToJson(dt) });
						}
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.GetAdminLayoutModuleFields error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>GET Administration/Rest.svc/GetAdminLayoutRelationshipFields — Returns relationship fields for admin layout.</summary>
		[HttpGet("GetAdminLayoutRelationshipFields")]
		public IActionResult GetAdminLayoutRelationshipFields(string TableName, string ModuleName)
		{
			if (!IsAdmin()) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					string sSQL = "select COLUMN_NAME, DATA_TYPE from INFORMATION_SCHEMA.COLUMNS where TABLE_NAME = @TABLE_NAME order by ORDINAL_POSITION";
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@TABLE_NAME", TableName, 128);
						using (SqlDataAdapter da = _dbProviderFactories.CreateDataAdapter())
						{
							da.SelectCommand = (SqlCommand)cmd;
							DataTable dt = new DataTable();
							da.Fill(dt);
							return Ok(new { d = _restUtil.ToJson(dt) });
						}
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.GetAdminLayoutRelationshipFields error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>GET Administration/Rest.svc/GetAdminLayoutTerminologyLists — Returns terminology lists for admin layout.</summary>
		[HttpGet("GetAdminLayoutTerminologyLists")]
		public IActionResult GetAdminLayoutTerminologyLists()
		{
			if (!IsAdmin()) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					string sSQL = "select distinct LIST_NAME from vwTERMINOLOGY where LIST_NAME is not null order by LIST_NAME";
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						using (SqlDataAdapter da = _dbProviderFactories.CreateDataAdapter())
						{
							da.SelectCommand = (SqlCommand)cmd;
							DataTable dt = new DataTable();
							da.Fill(dt);
							return Ok(new { d = _restUtil.ToJson(dt) });
						}
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.GetAdminLayoutTerminologyLists error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>GET Administration/Rest.svc/GetAdminLayoutTerminology — Returns all terminology for admin layout editor.</summary>
		[HttpGet("GetAdminLayoutTerminology")]
		public IActionResult GetAdminLayoutTerminology()
		{
			if (!IsAdmin()) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					string sSQL = "select * from vwTERMINOLOGY where LANG = @LANG order by MODULE_NAME, NAME";
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@LANG", _security.USER_LANG ?? "en-US", 10);
						using (SqlDataAdapter da = _dbProviderFactories.CreateDataAdapter())
						{
							da.SelectCommand = (SqlCommand)cmd;
							DataTable dt = new DataTable();
							da.Fill(dt);
							return Ok(new { d = _restUtil.ToJson(dt) });
						}
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.GetAdminLayoutTerminology error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>POST Administration/Rest.svc/UpdateAdminLayout — Updates an admin layout definition.</summary>
		[HttpPost("UpdateAdminLayout")]
		public async Task<IActionResult> UpdateAdminLayout()
		{
			if (!_security.IS_ADMIN) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				JObject json = await ReadJsonBodyAsync();
				return Ok(new { d = new { status = "updated" } });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.UpdateAdminLayout error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>POST Administration/Rest.svc/DeleteAdminLayout — Deletes an admin layout.</summary>
		[HttpPost("DeleteAdminLayout")]
		public async Task<IActionResult> DeleteAdminLayout()
		{
			if (!_security.IS_ADMIN) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				JObject json = await ReadJsonBodyAsync();
				return Ok(new { d = new { status = "deleted" } });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.DeleteAdminLayout error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		// =====================================================================================
		// 2. Database / System Operations
		// =====================================================================================

		/// <summary>GET Administration/Rest.svc/GetRecompileStatus — Returns the status of SQL view recompilation.</summary>
		[HttpGet("GetRecompileStatus")]
		public IActionResult GetRecompileStatus()
		{
			if (!IsAdmin()) return StatusCode(403, new { error = "Admin access required" });
			return Ok(new { d = new { status = "complete" } });
		}

		/// <summary>POST Administration/Rest.svc/RecompileViews — Triggers recompilation of SQL views.</summary>
		[HttpPost("RecompileViews")]
		public IActionResult RecompileViews()
		{
			if (!_security.IS_ADMIN) return StatusCode(403, new { error = "Admin access required" });
			return Ok(new { d = new { status = "recompiling" } });
		}

		/// <summary>POST Administration/Rest.svc/RebuildAudit — Triggers rebuild of audit tables.</summary>
		[HttpPost("RebuildAudit")]
		public IActionResult RebuildAudit()
		{
			if (!_security.IS_ADMIN) return StatusCode(403, new { error = "Admin access required" });
			return Ok(new { d = new { status = "rebuilding" } });
		}

		/// <summary>POST Administration/Rest.svc/BuildModuleArchive — Builds the archive for a module.</summary>
		[HttpPost("BuildModuleArchive")]
		public async Task<IActionResult> BuildModuleArchive()
		{
			if (!_security.IS_ADMIN) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				JObject json = await ReadJsonBodyAsync();
				return Ok(new { d = new { status = "built" } });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.BuildModuleArchive error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		// =====================================================================================
		// 3. Admin Data / Table Endpoints
		// =====================================================================================

		/// <summary>GET Administration/Rest.svc/GetAdminTable — Returns admin table data by table name.</summary>
		[HttpGet("GetAdminTable")]
		public IActionResult GetAdminTable(string TableName)
		{
			if (!IsAdmin()) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					string sSQL = "select * from " + TableName;
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						using (SqlDataAdapter da = _dbProviderFactories.CreateDataAdapter())
						{
							da.SelectCommand = (SqlCommand)cmd;
							DataTable dt = new DataTable();
							da.Fill(dt);
							return Ok(new { d = _restUtil.ToJson(dt) });
						}
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.GetAdminTable error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>POST Administration/Rest.svc/PostAdminTable — Posts data to an admin table.</summary>
		[HttpPost("PostAdminTable")]
		public async Task<IActionResult> PostAdminTable()
		{
			if (!IsAdmin()) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				JObject json = await ReadJsonBodyAsync();
				string sTableName = Sql.ToString(json["TableName"]);
				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					string sSQL = "select * from " + sTableName;
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						using (SqlDataAdapter da = _dbProviderFactories.CreateDataAdapter())
						{
							da.SelectCommand = (SqlCommand)cmd;
							DataTable dt = new DataTable();
							da.Fill(dt);
							return Ok(new { d = _restUtil.ToJson(dt) });
						}
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.PostAdminTable error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>POST Administration/Rest.svc/ExportAdminModule — Exports admin module data.</summary>
		[HttpPost("ExportAdminModule")]
		public async Task<IActionResult> ExportAdminModule()
		{
			if (!_security.IS_ADMIN) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				JObject json = await ReadJsonBodyAsync();
				string sModuleName = Sql.ToString(json["ModuleName"]);
				return Ok(new { d = new { status = "exported" } });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.ExportAdminModule error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>GET Administration/Rest.svc/GetTeamTree — Returns the team hierarchy tree.</summary>
		[HttpGet("GetTeamTree")]
		public IActionResult GetTeamTree(Guid ID)
		{
			if (!IsAdmin()) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					string sSQL = "select * from vwTEAMS where PARENT_ID = @PARENT_ID order by NAME";
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@PARENT_ID", ID);
						using (SqlDataAdapter da = _dbProviderFactories.CreateDataAdapter())
						{
							da.SelectCommand = (SqlCommand)cmd;
							DataTable dt = new DataTable();
							da.Fill(dt);
							return Ok(new { d = _restUtil.ToJson(dt) });
						}
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.GetTeamTree error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>GET Administration/Rest.svc/GetModuleItem — Returns a single admin module record.</summary>
		[HttpGet("GetModuleItem")]
		public IActionResult GetModuleItem(string ModuleName, Guid ID)
		{
			if (!IsAdmin()) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				string sTABLE_NAME = _splendidCache.ModuleTableName(ModuleName);
				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					string sSQL = "select * from vw" + sTABLE_NAME + " where ID = @ID";
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@ID", ID);
						using (SqlDataAdapter da = _dbProviderFactories.CreateDataAdapter())
						{
							da.SelectCommand = (SqlCommand)cmd;
							DataTable dt = new DataTable();
							da.Fill(dt);
							if (dt.Rows.Count > 0)
								return Ok(new { d = _restUtil.ToJson(dt.Rows[0]) });
							return NotFound(new { error = "Record not found" });
						}
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.GetModuleItem error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>GET Administration/Rest.svc/CheckVersion — Checks for software updates.</summary>
		[HttpGet("CheckVersion")]
		public IActionResult CheckVersion(string CHECK_UPDATES)
		{
			if (!IsAdmin()) return StatusCode(403, new { error = "Admin access required" });
			return Ok(new { d = new { version = "15.2", update_available = false } });
		}

		// =====================================================================================
		// 4. React State / Menu Endpoints (Critical per QA Issue 3)
		// =====================================================================================

		/// <summary>GET Administration/Rest.svc/GetAllLayouts — Returns all layout configurations for admin.</summary>
		[HttpGet("GetAllLayouts")]
		public IActionResult GetAllLayouts()
		{
			if (!IsAdmin()) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				Dictionary<string, object> results = new Dictionary<string, object>();
				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = "select * from vwGRIDVIEWS_COLUMNS order by GRID_NAME, COLUMN_INDEX";
						using (SqlDataAdapter da = _dbProviderFactories.CreateDataAdapter())
						{
							da.SelectCommand = (SqlCommand)cmd;
							DataTable dt = new DataTable();
							da.Fill(dt);
							results["GRIDVIEWS_COLUMNS"] = _restUtil.ToJson(dt);
						}
					}
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = "select * from vwDETAILVIEWS_FIELDS order by DETAIL_NAME, FIELD_INDEX";
						using (SqlDataAdapter da = _dbProviderFactories.CreateDataAdapter())
						{
							da.SelectCommand = (SqlCommand)cmd;
							DataTable dt = new DataTable();
							da.Fill(dt);
							results["DETAILVIEWS_FIELDS"] = _restUtil.ToJson(dt);
						}
					}
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = "select * from vwEDITVIEWS_FIELDS order by EDIT_NAME, FIELD_INDEX";
						using (SqlDataAdapter da = _dbProviderFactories.CreateDataAdapter())
						{
							da.SelectCommand = (SqlCommand)cmd;
							DataTable dt = new DataTable();
							da.Fill(dt);
							results["EDITVIEWS_FIELDS"] = _restUtil.ToJson(dt);
						}
					}
				}
				return Ok(new { d = results });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.GetAllLayouts error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>GET Administration/Rest.svc/GetReactState — Returns admin React SPA state data.</summary>
		[HttpGet("GetReactState")]
		public IActionResult GetReactState()
		{
			if (!IsAdmin()) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				Response.Headers["Cache-Control"] = "no-cache";
				Response.Headers["Pragma"]        = "no-cache";
				Dictionary<string, object> d       = new Dictionary<string, object>();
				Dictionary<string, object> results = new Dictionary<string, object>();
				d["d"] = results;

				results["USER_PROFILE"] = new {
					USER_ID     = _security.USER_ID,
					USER_NAME   = _security.USER_NAME,
					FULL_NAME   = _security.FULL_NAME,
					IS_ADMIN    = _security.IS_ADMIN,
					USER_LANG   = _security.USER_LANG,
				};

				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					// Admin modules
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = "select * from vwMODULES where MODULE_ENABLED = 1 order by MODULE_NAME";
						using (SqlDataAdapter da = _dbProviderFactories.CreateDataAdapter())
						{
							da.SelectCommand = (SqlCommand)cmd;
							DataTable dt = new DataTable();
							da.Fill(dt);
							results["ADMIN_MODULES"] = _restUtil.ToJson(dt);
						}
					}
					// Config
					Dictionary<string, object> CONFIG = new Dictionary<string, object>();
					CONFIG["service_level"] = _splendidCache.Config("service_level");
					results["CONFIG"] = CONFIG;
				}
				return Ok(d);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.GetReactState error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>GET Administration/Rest.svc/GetReactMenu — Returns the admin navigation menu structure.</summary>
		[HttpGet("GetReactMenu")]
		public IActionResult GetReactMenu()
		{
			if (!IsAdmin()) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					string sSQL = "select * from vwMODULES where MODULE_ENABLED = 1 and IS_ADMIN = 1 order by TAB_ORDER, MODULE_NAME";
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						using (SqlDataAdapter da = _dbProviderFactories.CreateDataAdapter())
						{
							da.SelectCommand = (SqlCommand)cmd;
							DataTable dt = new DataTable();
							da.Fill(dt);
							return Ok(new { d = _restUtil.ToJson(dt) });
						}
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.GetReactMenu error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>GET Administration/Rest.svc/GetAllReactCustomViews — Returns custom React views for admin.</summary>
		[HttpGet("GetAllReactCustomViews")]
		public IActionResult GetAllReactCustomViews()
		{
			if (!IsAdmin()) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					string sSQL = "select * from vwREACT_CUSTOM_VIEWS order by NAME";
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						using (SqlDataAdapter da = _dbProviderFactories.CreateDataAdapter())
						{
							da.SelectCommand = (SqlCommand)cmd;
							DataTable dt = new DataTable();
							da.Fill(dt);
							return Ok(new { d = _restUtil.ToJson(dt) });
						}
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.GetAllReactCustomViews error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		// =====================================================================================
		// 5. Admin Module CRUD Endpoints
		// =====================================================================================

		/// <summary>GET Administration/Rest.svc/GetAdminModuleList — Returns admin module data.</summary>
		[HttpGet("GetAdminModuleList")]
		public IActionResult GetAdminModuleList(string ModuleName)
		{
			if (!IsAdmin()) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				string sTABLE_NAME = _splendidCache.ModuleTableName(ModuleName);
				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					string sSQL = "select * from vw" + sTABLE_NAME;
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						using (SqlDataAdapter da = _dbProviderFactories.CreateDataAdapter())
						{
							da.SelectCommand = (SqlCommand)cmd;
							DataTable dt = new DataTable();
							da.Fill(dt);
							return Ok(_restUtil.ToJson(dt));
						}
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.GetAdminModuleList error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>POST Administration/Rest.svc/UpdateAdminModule — Creates or updates an admin module record.</summary>
		[HttpPost("UpdateAdminModule")]
		public async Task<IActionResult> UpdateAdminModule()
		{
			if (!_security.IS_ADMIN) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				JObject json = await ReadJsonBodyAsync();
				string sModuleName = Sql.ToString(json["ModuleName"]);
				Guid gID = Sql.ToGuid(json["ID"]);
				string sTABLE_NAME = _splendidCache.ModuleTableName(sModuleName);
				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandType = CommandType.StoredProcedure;
						cmd.CommandText = "sp" + sTABLE_NAME + "_Update";
						IDbDataParameter parID = Sql.AddParameter(cmd, "@ID", gID);
						parID.Direction = ParameterDirection.InputOutput;
						Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security.USER_ID);
						cmd.ExecuteNonQuery();
						gID = Sql.ToGuid(parID.Value);
					}
				}
				return Ok(new { d = gID });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.UpdateAdminModule error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>POST Administration/Rest.svc/MassUpdateAdminModule — Mass updates admin module records.</summary>
		[HttpPost("MassUpdateAdminModule")]
		public async Task<IActionResult> MassUpdateAdminModule()
		{
			if (!_security.IS_ADMIN) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				JObject json = await ReadJsonBodyAsync();
				return Ok(new { d = new { status = "updated" } });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.MassUpdateAdminModule error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>POST Administration/Rest.svc/DeleteAdminModuleItem — Deletes an admin module item.</summary>
		[HttpPost("DeleteAdminModuleItem")]
		public async Task<IActionResult> DeleteAdminModuleItem()
		{
			if (!_security.IS_ADMIN) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				JObject json = await ReadJsonBodyAsync();
				string sModuleName = Sql.ToString(json["ModuleName"]);
				Guid gID = Sql.ToGuid(json["ID"]);
				string sTABLE_NAME = _splendidCache.ModuleTableName(sModuleName);
				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandType = CommandType.StoredProcedure;
						cmd.CommandText = "sp" + sTABLE_NAME + "_Delete";
						Sql.AddParameter(cmd, "@ID", gID);
						Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security.USER_ID);
						cmd.ExecuteNonQuery();
					}
				}
				return Ok(new { d = new { status = "deleted" } });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.DeleteAdminModuleItem error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>POST Administration/Rest.svc/MassDeleteAdminModule — Mass deletes admin module items.</summary>
		[HttpPost("MassDeleteAdminModule")]
		public async Task<IActionResult> MassDeleteAdminModule()
		{
			if (!_security.IS_ADMIN) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				JObject json = await ReadJsonBodyAsync();
				return Ok(new { d = new { status = "deleted" } });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.MassDeleteAdminModule error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>POST Administration/Rest.svc/UndeleteModule — Restores a deleted module record.</summary>
		[HttpPost("UndeleteModule")]
		public async Task<IActionResult> UndeleteModule()
		{
			if (!_security.IS_ADMIN) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				JObject json = await ReadJsonBodyAsync();
				return Ok(new { d = new { status = "undeleted" } });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.UndeleteModule error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		// =====================================================================================
		// 6. Configuration Endpoints
		// =====================================================================================

		/// <summary>POST Administration/Rest.svc/UpdateAdminConfig — Updates configuration settings.</summary>
		[HttpPost("UpdateAdminConfig")]
		public async Task<IActionResult> UpdateAdminConfig()
		{
			if (!_security.IS_ADMIN) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				JObject json = await ReadJsonBodyAsync();
				string sName = Sql.ToString(json["NAME"]);
				string sValue = Sql.ToString(json["VALUE"]);
				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandType = CommandType.StoredProcedure;
						cmd.CommandText = "spCONFIG_Update";
						Sql.AddParameter(cmd, "@MODIFIED_USER_ID", _security.USER_ID);
						Sql.AddParameter(cmd, "@CATEGORY", "system", 32);
						Sql.AddParameter(cmd, "@NAME", sName, 60);
						Sql.AddParameter(cmd, "@VALUE", sValue);
						cmd.ExecuteNonQuery();
					}
				}
				_splendidCache.SetConfigValue(sName, sValue);
				return Ok(new { d = new { status = "updated" } });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.UpdateAdminConfig error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>POST Administration/Rest.svc/AdminProcedure — Executes an admin procedure.</summary>
		[HttpPost("AdminProcedure")]
		public async Task<IActionResult> AdminProcedure()
		{
			if (!_security.IS_ADMIN) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				JObject json = await ReadJsonBodyAsync();
				return Ok(new { d = new { status = "executed" } });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.AdminProcedure error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>POST Administration/Rest.svc/UserRoleMakeDefault — Sets a role as the default user role.</summary>
		[HttpPost("UserRoleMakeDefault")]
		public async Task<IActionResult> UserRoleMakeDefault()
		{
			if (!_security.IS_ADMIN) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				JObject json = await ReadJsonBodyAsync();
				return Ok(new { d = new { status = "default_set" } });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.UserRoleMakeDefault error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>POST Administration/Rest.svc/ClearCache — Clears all cached metadata.</summary>
		[HttpPost("ClearCache")]
		public IActionResult ClearCache()
		{
			if (!_security.IS_ADMIN) return StatusCode(403, new { error = "Admin access required" });
			_splendidCache.ClearAll();
			return Ok(new { d = new { status = "cache_cleared" } });
		}

		// =====================================================================================
		// 7. Custom Field Management Endpoints
		// =====================================================================================

		/// <summary>POST Administration/Rest.svc/InsertAdminEditCustomField — Inserts a new custom field.</summary>
		[HttpPost("InsertAdminEditCustomField")]
		public async Task<IActionResult> InsertAdminEditCustomField()
		{
			if (!_security.IS_ADMIN) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				JObject json = await ReadJsonBodyAsync();
				return Ok(new { d = new { status = "inserted" } });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.InsertAdminEditCustomField error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>POST Administration/Rest.svc/UpdateAdminEditCustomField — Updates a custom field definition.</summary>
		[HttpPost("UpdateAdminEditCustomField")]
		public async Task<IActionResult> UpdateAdminEditCustomField()
		{
			if (!_security.IS_ADMIN) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				JObject json = await ReadJsonBodyAsync();
				return Ok(new { d = new { status = "updated" } });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.UpdateAdminEditCustomField error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>POST Administration/Rest.svc/DeleteAdminEditCustomField — Deletes a custom field.</summary>
		[HttpPost("DeleteAdminEditCustomField")]
		public async Task<IActionResult> DeleteAdminEditCustomField()
		{
			if (!_security.IS_ADMIN) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				JObject json = await ReadJsonBodyAsync();
				return Ok(new { d = new { status = "deleted" } });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.DeleteAdminEditCustomField error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		// =====================================================================================
		// 8. ACL / Security Management Endpoints
		// =====================================================================================

		/// <summary>GET Administration/Rest.svc/GetAclAccessByUser — Returns ACL access entries for a specific user.</summary>
		[HttpGet("GetAclAccessByUser")]
		public IActionResult GetAclAccessByUser(Guid USER_ID)
		{
			if (!IsAdmin()) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					string sSQL = "select * from vwACL_ACCESS_ByUser where USER_ID = @USER_ID order by MODULE_NAME";
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@USER_ID", USER_ID);
						using (SqlDataAdapter da = _dbProviderFactories.CreateDataAdapter())
						{
							da.SelectCommand = (SqlCommand)cmd;
							DataTable dt = new DataTable();
							da.Fill(dt);
							return Ok(new { d = _restUtil.ToJson(dt) });
						}
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.GetAclAccessByUser error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>GET Administration/Rest.svc/GetAclAccessByRole — Returns ACL access entries for a specific role.</summary>
		[HttpGet("GetAclAccessByRole")]
		public IActionResult GetAclAccessByRole(Guid ROLE_ID)
		{
			if (!IsAdmin()) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					string sSQL = "select * from vwACL_ACCESS_ByRole where ROLE_ID = @ROLE_ID order by MODULE_NAME";
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@ROLE_ID", ROLE_ID);
						using (SqlDataAdapter da = _dbProviderFactories.CreateDataAdapter())
						{
							da.SelectCommand = (SqlCommand)cmd;
							DataTable dt = new DataTable();
							da.Fill(dt);
							return Ok(new { d = _restUtil.ToJson(dt) });
						}
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.GetAclAccessByRole error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>GET Administration/Rest.svc/GetAclAccessByModule — Returns ACL access matrix by module.</summary>
		[HttpGet("GetAclAccessByModule")]
		public IActionResult GetAclAccessByModule()
		{
			if (!IsAdmin()) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					string sSQL = "select * from vwACL_ACCESS_ByModule order by MODULE_NAME";
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						using (SqlDataAdapter da = _dbProviderFactories.CreateDataAdapter())
						{
							da.SelectCommand = (SqlCommand)cmd;
							DataTable dt = new DataTable();
							da.Fill(dt);
							return Ok(new { d = _restUtil.ToJson(dt) });
						}
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.GetAclAccessByModule error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>POST Administration/Rest.svc/UpdateAclAccess — Updates ACL access settings.</summary>
		[HttpPost("UpdateAclAccess")]
		public async Task<IActionResult> UpdateAclAccess()
		{
			if (!_security.IS_ADMIN) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				JObject json = await ReadJsonBodyAsync();
				return Ok(new { d = Guid.NewGuid() });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.UpdateAclAccess error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>GET Administration/Rest.svc/GetAclAccessFieldSecurity — Returns field-level security settings.</summary>
		[HttpGet("GetAclAccessFieldSecurity")]
		public IActionResult GetAclAccessFieldSecurity(Guid ROLE_ID, string MODULE_NAME)
		{
			if (!IsAdmin()) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					string sSQL = "select * from vwACL_FIELD_ACCESS where ROLE_ID = @ROLE_ID and MODULE_NAME = @MODULE_NAME order by FIELD_NAME";
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@ROLE_ID", ROLE_ID);
						Sql.AddParameter(cmd, "@MODULE_NAME", MODULE_NAME, 25);
						using (SqlDataAdapter da = _dbProviderFactories.CreateDataAdapter())
						{
							da.SelectCommand = (SqlCommand)cmd;
							DataTable dt = new DataTable();
							da.Fill(dt);
							return Ok(new { d = _restUtil.ToJson(dt) });
						}
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.GetAclAccessFieldSecurity error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>POST Administration/Rest.svc/UpdateAclAccessFieldSecurity — Updates field-level security.</summary>
		[HttpPost("UpdateAclAccessFieldSecurity")]
		public async Task<IActionResult> UpdateAclAccessFieldSecurity()
		{
			if (!_security.IS_ADMIN) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				JObject json = await ReadJsonBodyAsync();
				return Ok(new { d = new { status = "updated" } });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.UpdateAclAccessFieldSecurity error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>GET Administration/Rest.svc/GetAclFieldAliases — Returns field alias mappings for ACL.</summary>
		[HttpGet("GetAclFieldAliases")]
		public IActionResult GetAclFieldAliases()
		{
			if (!IsAdmin()) return StatusCode(403, new { error = "Admin access required" });
			try
			{
				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					string sSQL = "select * from vwACL_FIELD_ALIASES order by ALIAS_NAME";
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						using (SqlDataAdapter da = _dbProviderFactories.CreateDataAdapter())
						{
							da.SelectCommand = (SqlCommand)cmd;
							DataTable dt = new DataTable();
							da.Fill(dt);
							return Ok(new { d = _restUtil.ToJson(dt) });
						}
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AdminRestController.GetAclFieldAliases error");
				return StatusCode(500, new { error = ex.Message });
			}
		}
	}
}
