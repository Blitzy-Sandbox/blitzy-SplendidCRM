// .NET 10 ASP.NET Core Migration
// Source: SplendidCRM/Administration/Impersonation.svc.cs
// Migration: WCF [ServiceContract] + [WebInvoke] → ASP.NET Core [ApiController] + [HttpPost]
// Pattern changes:
//   - HttpContext.Current.Session → IHttpContextAccessor
//   - Security.IsAuthenticated() / Security.IS_ADMIN → injected Security service
//   - SplendidInit.LoginUser(ID, "Impersonate") → DB query + injected SplendidInit.LoginUser(DataRow)
//   - File.Exists(Server.MapPath(...)) → IWebHostEnvironment.ContentRootPath
#nullable disable
using System;
using System.Data;
using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SplendidCRM.Web.Controllers
{
	/// <summary>
	/// Admin impersonation controller — migrated from Administration/Impersonation.svc.cs.
	/// Route: POST Administration/Impersonation.svc/Impersonate
	/// </summary>
	[ApiController]
	[Route("Administration/Impersonation.svc")]
	[Authorize]
	public class ImpersonationController : ControllerBase
	{
		private readonly Security                         _security;
		private readonly SplendidInit                     _splendidInit;
		private readonly IHttpContextAccessor             _httpContextAccessor;
		private readonly IWebHostEnvironment              _env;
		private readonly IConfiguration                   _config;
		private readonly ILogger<ImpersonationController> _logger;

		public ImpersonationController(
			Security                         security,
			SplendidInit                     splendidInit,
			IHttpContextAccessor             httpContextAccessor,
			IWebHostEnvironment              env,
			IConfiguration                   config,
			ILogger<ImpersonationController> logger)
		{
			_security            = security;
			_splendidInit        = splendidInit;
			_httpContextAccessor = httpContextAccessor;
			_env                 = env;
			_config              = config;
			_logger              = logger;
		}

		/// <summary>
		/// POST Administration/Impersonation.svc/Impersonate
		/// Impersonates the specified user.  Requires administrator access.
		/// Migrated from: SplendidInit.LoginUser(ID, "Impersonate")
		/// </summary>
		[HttpPost("Impersonate")]
		public IActionResult Impersonate([FromBody] ImpersonateRequest request)
		{
			try
			{
				// Verify the caller is an authenticated admin — mirrors original Security.IsAuthenticated() && Security.IS_ADMIN
				if ( !_security.IsAuthenticated() || !_security.IS_ADMIN )
					return StatusCode(403, new { error = "Insufficient access" });

				// Original: check physical file exists to confirm impersonation is enabled
				string impersonationFile = Path.Combine(_env.ContentRootPath, "Administration", "Impersonation.svc");
				if ( !System.IO.File.Exists(impersonationFile) )
					return StatusCode(403, new { error = "Impersonation is disabled" });

				// Load the target user row from the database to pass to LoginUser(DataRow)
				DataRow rowUser = null;
				string connStr = _config.GetConnectionString("SplendidCRM");
				using ( SqlConnection con = new SqlConnection(connStr) )
				{
					con.Open();
					using ( SqlCommand cmd = new SqlCommand("select * from vwUSERS where ID = @ID", con) )
					{
						cmd.Parameters.AddWithValue("@ID", request.ID);
						using ( SqlDataAdapter da = new SqlDataAdapter(cmd) )
						{
							DataTable dt = new DataTable();
							da.Fill(dt);
							if ( dt.Rows.Count > 0 )
								rowUser = dt.Rows[0];
						}
					}
				}

				if ( rowUser == null )
					return NotFound(new { error = "User not found" });

				// Perform the impersonation login — delegates to SplendidInit.LoginUser(DataRow)
				_splendidInit.LoginUser(rowUser);

				// Preserve original Session["USER_IMPERSONATION"] = true
				_httpContextAccessor.HttpContext?.Session.SetString("USER_IMPERSONATION", "true");

				return Ok(new { d = (object)null });
			}
			catch ( Exception ex )
			{
				_logger.LogError(ex, "ImpersonationController.Impersonate failed for user {ID}", request?.ID);
				return StatusCode(500, new { error = _env.IsDevelopment() ? ex.Message : "An internal error occurred. Please try again later." });
			}
		}
	}

	/// <summary>
	/// Request body for the Impersonate endpoint.
	/// </summary>
	public class ImpersonateRequest
	{
		/// <summary>The GUID of the user to impersonate.</summary>
		public Guid ID { get; set; }
	}
}
