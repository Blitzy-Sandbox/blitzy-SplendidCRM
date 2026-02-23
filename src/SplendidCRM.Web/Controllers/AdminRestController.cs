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
	/// Admin REST API controller — converts 65 WCF operations from Administration/Rest.svc.cs.
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

		public AdminRestController(Security security, SplendidCache splendidCache, RestUtil restUtil, DbProviderFactories dbProviderFactories, IMemoryCache memoryCache, IConfiguration configuration, ILogger<AdminRestController> logger)
		{
			_security = security;
			_splendidCache = splendidCache;
			_restUtil = restUtil;
			_dbProviderFactories = dbProviderFactories;
			_memoryCache = memoryCache;
			_configuration = configuration;
			_logger = logger;
		}

		/// <summary>
		/// GET Administration/Rest.svc/GetAdminModuleList — Returns admin module data.
		/// </summary>
		[HttpGet("GetAdminModuleList")]
		public IActionResult GetAdminModuleList(string ModuleName)
		{
			if (!_security.IS_ADMIN && !_security.IS_ADMIN_DELEGATE)
				return StatusCode(403, new { error = "Admin access required" });
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
				_logger.LogError(ex, "AdminRestController.GetAdminModuleList error");
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>
		/// POST Administration/Rest.svc/UpdateAdminConfig — Updates configuration settings.
		/// </summary>
		[HttpPost("UpdateAdminConfig")]
		public async Task<IActionResult> UpdateAdminConfig()
		{
			if (!_security.IS_ADMIN)
				return StatusCode(403, new { error = "Admin access required" });
			try
			{
				string sBody;
				using (StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8))
					sBody = await reader.ReadToEndAsync();
				JObject json = JObject.Parse(sBody);
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

		/// <summary>
		/// POST Administration/Rest.svc/ClearCache — Clears all cached metadata.
		/// </summary>
		[HttpPost("ClearCache")]
		public IActionResult ClearCache()
		{
			if (!_security.IS_ADMIN)
				return StatusCode(403, new { error = "Admin access required" });
			_splendidCache.ClearAll();
			return Ok(new { d = new { status = "cache_cleared" } });
		}
	}
}
