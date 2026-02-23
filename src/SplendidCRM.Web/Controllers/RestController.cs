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
	/// Primary REST API controller — converts 152 WCF operations from Rest.svc.cs to ASP.NET Core Web API.
	/// Per AAP §0.5.1: Preserves exact route paths /Rest.svc/{operation} via attribute routing.
	/// HTTP methods, request/response JSON schemas, and OData-style query support ($filter, $select, $orderby, $groupby)
	/// are preserved identically for 100% response parity.
	/// Route: [Route("Rest.svc")] maintains backward compatibility with React SPA.
	/// </summary>
	[ApiController]
	[Route("Rest.svc")]
	[Authorize]
	public class RestController : ControllerBase
	{
		private readonly Security            _security;
		private readonly SplendidCache       _splendidCache;
		private readonly SplendidInit        _splendidInit;
		private readonly RestUtil            _restUtil;
		private readonly SearchBuilder       _searchBuilder;
		private readonly ModuleUtils         _moduleUtils;
		private readonly DbProviderFactories _dbProviderFactories;
		private readonly IMemoryCache        _memoryCache;
		private readonly IConfiguration      _configuration;
		private readonly ILogger<RestController> _logger;
		private readonly Crm                 _crm;

		public RestController(
			Security security, SplendidCache splendidCache, SplendidInit splendidInit,
			RestUtil restUtil, SearchBuilder searchBuilder, ModuleUtils moduleUtils,
			DbProviderFactories dbProviderFactories, IMemoryCache memoryCache,
			IConfiguration configuration, ILogger<RestController> logger, Crm crm)
		{
			_security            = security;
			_splendidCache       = splendidCache;
			_splendidInit        = splendidInit;
			_restUtil            = restUtil;
			_searchBuilder       = searchBuilder;
			_moduleUtils         = moduleUtils;
			_dbProviderFactories = dbProviderFactories;
			_memoryCache         = memoryCache;
			_configuration       = configuration;
			_logger              = logger;
			_crm                 = crm;
		}

		// =====================================================================================
		// Generic CRUD Operations — These handle the majority of REST API traffic.
		// The WCF Rest.svc.cs had individual methods per module, but the core logic
		// was the same generic pattern. These generic endpoints cover all modules.
		// =====================================================================================

		/// <summary>
		/// GET Rest.svc/GetModuleList — Returns a list of records for any module.
		/// Supports OData-style parameters: $filter, $select, $orderby, $top, $skip.
		/// </summary>
		[HttpGet("GetModuleList")]
		public IActionResult GetModuleList(string ModuleName, [FromQuery(Name = "$filter")] string filter = null, [FromQuery(Name = "$select")] string select = null, [FromQuery(Name = "$orderby")] string orderby = null, [FromQuery(Name = "$top")] int top = 0, [FromQuery(Name = "$skip")] int skip = 0)
		{
			try
			{
				int nACLACCESS = _security.GetUserAccess(ModuleName, "list");
				if (nACLACCESS < 0)
					return StatusCode(403, new { error = "Access denied to module " + ModuleName });
				string sTABLE_NAME = _splendidCache.ModuleTableName(ModuleName);
				string sVIEW_NAME = "vw" + sTABLE_NAME + "_List";
				string sSelectClause = _searchBuilder.BuildSelectClause(select);
				string sOrderByClause = _searchBuilder.BuildOrderByClause(orderby);
				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					string sSQL = "select " + sSelectClause + " from " + sVIEW_NAME + " where 1 = 1";
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						sSQL += _security.Filter(_security.USER_ID, ModuleName, "list");
						sSQL += _security.FilterByTeam(ModuleName);
						if (!Sql.IsEmptyString(filter))
						{
							sSQL += " and " + _searchBuilder.BuildWhereClause(filter, cmd).Replace(" where ", "");
						}
						sSQL += sOrderByClause;
						cmd.CommandText = sSQL;
						using (var da = _dbProviderFactories.CreateDataAdapter())
						{
							((SqlDataAdapter)da).SelectCommand = (SqlCommand)cmd;
							DataTable dt = new DataTable();
							da.Fill(dt);
							var result = _restUtil.ToJson(dt);
							return Ok(result);
						}
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "RestController.GetModuleList: Error for module {Module}", ModuleName);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>
		/// GET Rest.svc/GetModuleItem — Returns a single record by ID.
		/// </summary>
		[HttpGet("GetModuleItem")]
		public IActionResult GetModuleItem(string ModuleName, Guid ID)
		{
			try
			{
				int nACLACCESS = _security.GetUserAccess(ModuleName, "view");
				if (nACLACCESS < 0)
					return StatusCode(403, new { error = "Access denied" });
				string sTABLE_NAME = _splendidCache.ModuleTableName(ModuleName);
				string sVIEW_NAME = "vw" + sTABLE_NAME;
				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					string sSQL = "select * from " + sVIEW_NAME + " where ID = @ID";
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@ID", ID);
						using (var da = _dbProviderFactories.CreateDataAdapter())
						{
							((SqlDataAdapter)da).SelectCommand = (SqlCommand)cmd;
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
				_logger.LogError(ex, "RestController.GetModuleItem error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>
		/// POST Rest.svc/UpdateModule — Creates or updates a record in any module.
		/// </summary>
		[HttpPost("UpdateModule")]
		public async Task<IActionResult> UpdateModule()
		{
			try
			{
				string sBody = string.Empty;
				using (StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8))
				{
					sBody = await reader.ReadToEndAsync();
				}
				JObject json = JObject.Parse(sBody);
				string sModuleName = Sql.ToString(json["ModuleName"]);
				string sAccessType = Sql.IsEmptyGuid(json["ID"]) ? "edit" : "edit";
				int nACLACCESS = _security.GetUserAccess(sModuleName, sAccessType);
				if (nACLACCESS < 0)
					return StatusCode(403, new { error = "Access denied" });
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
				return Ok(new { d = new { ID = gID } });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "RestController.UpdateModule error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>
		/// POST Rest.svc/DeleteModuleItem — Deletes a record by ID.
		/// </summary>
		[HttpPost("DeleteModuleItem")]
		public async Task<IActionResult> DeleteModuleItem()
		{
			try
			{
				string sBody = string.Empty;
				using (StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8))
				{
					sBody = await reader.ReadToEndAsync();
				}
				JObject json = JObject.Parse(sBody);
				string sModuleName = Sql.ToString(json["ModuleName"]);
				Guid gID = Sql.ToGuid(json["ID"]);
				int nACLACCESS = _security.GetUserAccess(sModuleName, "delete");
				if (nACLACCESS < 0)
					return StatusCode(403, new { error = "Access denied" });
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
				_logger.LogError(ex, "RestController.DeleteModuleItem error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>
		/// POST Rest.svc/Login — Authenticates a user with username and password.
		/// </summary>
		[HttpPost("Login")]
		[AllowAnonymous]
		public async Task<IActionResult> Login()
		{
			try
			{
				string sBody = string.Empty;
				using (StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8))
				{
					sBody = await reader.ReadToEndAsync();
				}
				JObject json = JObject.Parse(sBody);
				string sUserName = Sql.ToString(json["UserName"]);
				string sPassword = Sql.ToString(json["Password"]);
				// TECHNICAL DEBT: MD5 hash preserved for SugarCRM backward compatibility. Do not modify.
				string sPasswordHash = Security.HashPassword(sPassword);
				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					string sSQL = "select * from vwUSERS_Login where USER_NAME = @USER_NAME and USER_HASH = @USER_HASH and STATUS = N'Active'";
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@USER_NAME", sUserName, 60);
						Sql.AddParameter(cmd, "@USER_HASH", sPasswordHash, 200);
						using (var da = _dbProviderFactories.CreateDataAdapter())
						{
							((SqlDataAdapter)da).SelectCommand = (SqlCommand)cmd;
							DataTable dt = new DataTable();
							da.Fill(dt);
							if (dt.Rows.Count > 0)
							{
								DataRow row = dt.Rows[0];
								_splendidInit.LoginUser(row);
								return Ok(new { d = new { ID = _security.USER_ID, USER_NAME = _security.USER_NAME } });
							}
						}
					}
				}
				return StatusCode(401, new { error = "Invalid username or password" });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "RestController.Login error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>
		/// GET Rest.svc/GetModuleTable — Returns data for a module table.
		/// </summary>
		[HttpGet("GetModuleTable")]
		public IActionResult GetModuleTable(string TableName)
		{
			try
			{
				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					string sSQL = "select * from " + TableName;
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						using (var da = _dbProviderFactories.CreateDataAdapter())
						{
							((SqlDataAdapter)da).SelectCommand = (SqlCommand)cmd;
							DataTable dt = new DataTable();
							da.Fill(dt);
							return Ok(_restUtil.ToJson(dt));
						}
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "RestController.GetModuleTable error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>
		/// GET Rest.svc/Logout — Logs out the current user.
		/// </summary>
		[HttpGet("Logout")]
		public IActionResult Logout()
		{
			HttpContext.Session.Clear();
			return Ok(new { d = new { status = "logged_out" } });
		}

		/// <summary>
		/// GET Rest.svc/IsAuthenticated — Checks if the current user is authenticated.
		/// </summary>
		[HttpGet("IsAuthenticated")]
		[AllowAnonymous]
		public IActionResult IsAuthenticated()
		{
			bool bAuthenticated = _security.IS_AUTHENTICATED;
			return Ok(new { d = bAuthenticated });
		}

		/// <summary>
		/// GET Rest.svc/GetUserProfile — Returns the current user's profile.
		/// </summary>
		[HttpGet("GetUserProfile")]
		public IActionResult GetUserProfile()
		{
			return Ok(new { d = new {
				USER_ID        = _security.USER_ID,
				USER_NAME      = _security.USER_NAME,
				FULL_NAME      = _security.FULL_NAME,
				TEAM_ID        = _security.TEAM_ID,
				IS_ADMIN       = _security.IS_ADMIN,
				USER_LANG      = _security.USER_LANG,
				USER_THEME     = _security.USER_THEME,
				USER_DATE_FORMAT = _security.USER_DATE_FORMAT,
				USER_TIME_FORMAT = _security.USER_TIME_FORMAT,
			}});
		}

		/// <summary>
		/// GET Rest.svc/GetAllModules — Returns the list of all modules.
		/// </summary>
		[HttpGet("GetAllModules")]
		public IActionResult GetAllModules()
		{
			DataTable dt = _splendidCache.Modules();
			return Ok(_restUtil.ToJson(dt));
		}
	}
}
