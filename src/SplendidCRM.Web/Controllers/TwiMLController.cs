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
	/// TwiMLController — converted from TwiML.aspx.cs.
	/// Handles both GET (TwiML retrieval) and POST (Twilio webhook callbacks).
	/// Does not use [ApiController] to allow flexible content type handling (form-encoded and JSON).
	/// </summary>
	[Route("api/TwiML")]
	[Route("TwiML.aspx")]
	public class TwiMLController : Controller
	{
		private readonly Security _security;
		private readonly DbProviderFactories _dbProviderFactories;
		private readonly ILogger<TwiMLController> _logger;
		private readonly IWebHostEnvironment _env;

		public TwiMLController(Security security, DbProviderFactories dbProviderFactories, ILogger<TwiMLController> logger, IWebHostEnvironment env)
		{
			_security = security;
			_dbProviderFactories = dbProviderFactories;
			_logger = logger;
			_env = env;
		}

		/// <summary>GET handler for TwiML document retrieval.</summary>
		[HttpGet]
		[AllowAnonymous]
		public IActionResult GetTwiml([FromQuery] Guid? ID = null)
		{
			return ProcessTwiMLRequest(ID);
		}

		/// <summary>POST handler for Twilio webhook callbacks (SMS status updates, incoming messages).
		/// Twilio sends form-encoded POST data: AccountSid, SmsStatus, Body, SmsSid, To, From, etc.</summary>
		[HttpPost]
		[AllowAnonymous]
		public IActionResult PostTwiml([FromQuery] Guid? ID = null)
		{
			return ProcessTwiMLRequest(ID);
		}

		/// <summary>Common handler for both GET and POST. The original TwiML.aspx.cs handled both HTTP methods in Page_Load,
		/// reading POST body for Twilio callback data and query parameters for ID-based TwiML retrieval.</summary>
		private IActionResult ProcessTwiMLRequest(Guid? ID)
		{
			try
			{
				// Read form body if POST (Twilio sends form-encoded webhook data)
				string sFormBody = string.Empty;
				if (HttpContext.Request.Method == "POST" && HttpContext.Request.HasFormContentType)
				{
					// Twilio sends form-encoded data: AccountSid, SmsStatus, Body, SmsSid, To, From, etc.
					// Read all form values for processing
					foreach (var key in HttpContext.Request.Form.Keys)
					{
						if (!string.IsNullOrEmpty(sFormBody)) sFormBody += "&";
						sFormBody += $"{key}={HttpContext.Request.Form[key]}";
					}
				}

				// Process TwiML request — retrieve TwiML document or handle Twilio callback
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
				_logger.LogError(ex, "TwiMLController request error");
				return StatusCode(500, new { error = _env.IsDevelopment() ? ex.Message : "An internal error occurred. Please try again later." });
			}
		}
	}
}
