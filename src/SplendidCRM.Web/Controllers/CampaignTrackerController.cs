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
	/// CampaignTrackerController — converted from campaign_trackerv2.aspx.cs.
	/// </summary>
	[ApiController]
	[Route("api/CampaignTracker")]
	public class CampaignTrackerController : ControllerBase
	{
		private readonly Security _security;
		private readonly DbProviderFactories _dbProviderFactories;
		private readonly ILogger<CampaignTrackerController> _logger;

		public CampaignTrackerController(Security security, DbProviderFactories dbProviderFactories, ILogger<CampaignTrackerController> logger)
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
							cmd.CommandText = "select * from vwCampaignTracker where ID = @ID";
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
				_logger.LogError(ex, "CampaignTrackerController.Get error");
				return StatusCode(500, new { error = ex.Message });
			}
		}
	}
}
