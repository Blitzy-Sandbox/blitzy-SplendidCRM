#nullable disable
using System;
using System.Data;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;

namespace SplendidCRM.Web.Controllers
{
	/// <summary>
	/// TwiMLController — converted from TwiML.aspx.cs.
	/// </summary>
	[ApiController]
	[Route("api/TwiML")]
	public class TwiMLController : ControllerBase
	{
		private readonly Security _security;
		private readonly DbProviderFactories _dbProviderFactories;
		private readonly ILogger<TwiMLController> _logger;

		public TwiMLController(Security security, DbProviderFactories dbProviderFactories, ILogger<TwiMLController> logger)
		{
			_security = security;
			_dbProviderFactories = dbProviderFactories;
			_logger = logger;
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
							cmd.CommandText = "select * from vwTwiML where ID = @ID";
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
				_logger.LogError(ex, "TwiMLController.Get error");
				return StatusCode(500, new { error = ex.Message });
			}
		}
	}
}
