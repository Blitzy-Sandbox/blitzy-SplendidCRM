#nullable disable
using System;
using System.Data;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;

namespace SplendidCRM.Web.Controllers
{
	/// <summary>
	/// UnsubscribeController — converted from RemoveMe.aspx.cs.
	/// </summary>
	[ApiController]
	[Route("api/Unsubscribe")]
	[Route("RemoveMe.aspx")]
	public class UnsubscribeController : ControllerBase
	{
		private readonly Security _security;
		private readonly DbProviderFactories _dbProviderFactories;
		private readonly ILogger<UnsubscribeController> _logger;
		private readonly IWebHostEnvironment _env;

		public UnsubscribeController(Security security, DbProviderFactories dbProviderFactories, ILogger<UnsubscribeController> logger, IWebHostEnvironment env)
		{
			_security = security;
			_dbProviderFactories = dbProviderFactories;
			_logger = logger;
			_env = env;
		}

		[HttpGet]
		[AllowAnonymous]
		public IActionResult Get([FromQuery] Guid? ID = null)
		{
			try
			{
				if (ID.HasValue && !Sql.IsEmptyGuid(ID.Value))
				{
					using (IDbConnection con = _dbProviderFactories.CreateConnection())
					{
						con.Open();
						using (IDbCommand cmd = con.CreateCommand())
						{
							cmd.CommandText = "select * from vwUnsubscribe where ID = @ID";
							Sql.AddParameter(cmd, "@ID", ID.Value);
							using (var da = _dbProviderFactories.CreateDataAdapter())
							{
								((SqlDataAdapter)da).SelectCommand = (SqlCommand)cmd;
								DataTable dt = new DataTable();
								da.Fill(dt);
								if (dt.Rows.Count > 0)
									return Ok(dt.Rows[0]);
							}
						}
					}
				}
				return Ok(new { status = "ok" });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "UnsubscribeController.Get error");
				return StatusCode(500, new { error = _env.IsDevelopment() ? ex.Message : "An internal error occurred. Please try again later." });
			}
		}
	}
}
