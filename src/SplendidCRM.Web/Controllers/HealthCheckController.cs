#nullable disable
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Data.SqlClient;

namespace SplendidCRM.Web.Controllers
{
	/// <summary>
	/// Health check endpoint at /api/health.
	/// Per AAP §0.4.4: Returns 200 OK with JSON status {"status":"Healthy"}.
	/// Referenced from SystemCheck.aspx.cs for diagnostic patterns.
	/// </summary>
	[ApiController]
	[Route("api/[controller]")]
	[AllowAnonymous]
	public class HealthCheckController : ControllerBase
	{
		private readonly IConfiguration _configuration;
		private readonly DbProviderFactories _dbProviderFactories;
		private readonly IWebHostEnvironment _env;

		public HealthCheckController(IConfiguration configuration, DbProviderFactories dbProviderFactories, IWebHostEnvironment env)
		{
			_configuration = configuration;
			_dbProviderFactories = dbProviderFactories;
			_env = env;
		}

		/// <summary>
		/// GET /api/health — Returns application health status.
		/// </summary>
		[HttpGet("/api/health")]
		public IActionResult GetHealth()
		{
			var result = new Dictionary<string, object>
			{
				{ "status", "Healthy" },
				{ "timestamp", DateTime.UtcNow.ToString("o") },
				{ "version", typeof(HealthCheckController).Assembly.GetName().Version?.ToString() ?? "1.0.0" }
			};
			// Check database connectivity.
			try
			{
				string sConnectionString = _dbProviderFactories.ConnectionString;
				if (!Sql.IsEmptyString(sConnectionString))
				{
					using (var con = _dbProviderFactories.CreateConnection())
					{
						con.Open();
						using (var cmd = con.CreateCommand())
						{
							cmd.CommandText = "SELECT 1";
							cmd.ExecuteScalar();
						}
					}
					result["database"] = "Connected";
				}
				else
				{
					result["database"] = "No connection string configured";
					result["status"] = "Degraded";
				}
			}
			catch (Exception ex)
			{
				// Sanitize error message — do not expose SQL Server connection details in Production.
				result["database"] = _env.IsDevelopment() ? "Error: " + ex.Message : "Error: Database connection failed";
				result["status"] = "Unhealthy";
				return StatusCode(503, result);
			}
			return Ok(result);
		}
	}
}
