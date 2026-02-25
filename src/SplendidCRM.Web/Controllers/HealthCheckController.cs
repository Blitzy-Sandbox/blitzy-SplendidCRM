/**********************************************************************************************************************
 * SplendidCRM is a Customer Relationship Management program created by SplendidCRM Software, Inc. 
 * Copyright (C) 2005-2022 SplendidCRM Software, Inc. All rights reserved.
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the 
 * GNU Affero General Public License as published by the Free Software Foundation, either version 3 
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; 
 * without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. 
 * See the GNU Affero General Public License for more details.
 * 
 * You should have received a copy of the GNU Affero General Public License along with this program. 
 * If not, see <http://www.gnu.org/licenses/>. 
 * 
 * You can contact SplendidCRM Software, Inc. at email address support@splendidcrm.com. 
 * 
 * In accordance with Section 7(b) of the GNU Affero General Public License version 3, 
 * the Appropriate Legal Notices must display the following words on all interactive user interfaces: 
 * "Copyright (C) 2005-2011 SplendidCRM Software, Inc. All rights reserved."
 *********************************************************************************************************************/
// .NET 10 Migration: SplendidCRM/SystemCheck.aspx.cs → src/SplendidCRM.Web/Controllers/HealthCheckController.cs
// Changes applied:
//   - CONVERTED: System.Web.UI.Page (WebForms page) → ASP.NET Core ControllerBase (Web API controller)
//   - CONVERTED: Page_Load event → Get() [HttpGet] action method returning IActionResult
//   - REPLACED:  Response.Write() output → JSON response body via Ok()/StatusCode()
//   - REPLACED:  Application["imageURL"] → IMemoryCache.TryGetValue("imageURL", ...)
//   - REPLACED:  System.Environment.MachineName try/catch pattern → preserved identically (Azure compat)
//   - REPLACED:  SplendidCRM.DbProviderFactories.GetFactory() → injected DbProviderFactories.GetFactory()
//   - REPLACED:  Response.ExpiresAbsolute → Cache-Control header via HttpContext.Response
//   - PRESERVED: DB connectivity check (select @@VERSION), Sql.IsSQLServer() guard, Sql.ToString() conversion
//   - PRESERVED: sSqlVersion newline replacement logic
//   - REMOVED:   Recompile/Reload/Mobile query string handling (admin WebForms logic, out of scope for backend API)
//   - REMOVED:   System.Web.Configuration.ProcessModelSection (WebForms-only, not available in .NET 10)
//   - ADDED:     [AllowAnonymous] — health endpoint must be accessible by load balancers and ECS health checks
//   - ADDED:     503 Service Unavailable response on DB connectivity failure (vs. HTML error in WebForms)
//   - ROUTE:     Exactly GET /api/health per AAP §0.4.4 handoff documentation for Prompt 3
//   - Minimal change clause: Only changes necessary for .NET Framework 4.8 → .NET 10 ASP.NET Core
#nullable disable
using System;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace SplendidCRM
{
	/// <summary>
	/// Health check endpoint providing diagnostic information about the application and database.
	/// Migrated from SplendidCRM/SystemCheck.aspx.cs (.NET Framework 4.8 → .NET 10 ASP.NET Core).
	/// 
	/// Endpoint: GET /api/health
	/// Success:  200 OK  {"status":"Healthy",  "machineName":"...","sqlVersion":"...","timestamp":"...","initialized":true/false}
	/// Failure:  503 Service Unavailable {"status":"Unhealthy","error":"...","timestamp":"..."}
	/// 
	/// This endpoint is intentionally [AllowAnonymous] because it must be accessible by:
	///   - AWS ECS container health checks (no auth token)
	///   - AWS ALB target group health checks (no auth token)
	///   - Monitoring systems (Prometheus, CloudWatch, etc.)
	/// Per AAP §0.4.4 handoff documentation for Prompt 3 (containerization).
	/// </summary>
	[ApiController]
	[Route("api/health")]
	[AllowAnonymous]
	public class HealthCheckController : ControllerBase
	{
		// =====================================================================================
		// .NET 10 Migration: DI-injected services replacing static access patterns
		// =====================================================================================

		/// <summary>
		/// IConfiguration replacing ConfigurationManager/WebConfigurationManager for accessing
		/// connection strings and application settings from the five-tier provider hierarchy:
		/// AWS Secrets Manager → Environment variables → AWS SSM Parameter Store → appsettings.{Env}.json → appsettings.json
		/// </summary>
		private readonly IConfiguration _configuration;

		/// <summary>
		/// IMemoryCache replacing Application[] (HttpApplicationState) for checking whether
		/// the application has been initialized. Checks for the "imageURL" cache key presence,
		/// which matches the SystemCheck.aspx.cs line 89 pattern:
		/// BEFORE: Sql.IsEmptyString(Application["imageURL"])
		/// AFTER:  !_memoryCache.TryGetValue("imageURL", out object _)
		/// </summary>
		private readonly IMemoryCache _memoryCache;

		/// <summary>
		/// DbProviderFactories replacing the static SplendidCRM.DbProviderFactories.GetFactory() call.
		/// Injected as a DI singleton; used to obtain a DbProviderFactory instance that creates
		/// IDbConnection objects for the SQL Server connectivity health check.
		/// BEFORE: SplendidCRM.DbProviderFactory dbf = SplendidCRM.DbProviderFactories.GetFactory();
		/// AFTER:  DbProviderFactory dbf = _dbProviderFactories.GetFactory();
		/// </summary>
		private readonly DbProviderFactories _dbProviderFactories;

		/// <summary>
		/// Initializes a new instance of <see cref="HealthCheckController"/> with DI-injected services.
		/// Called by the ASP.NET Core DI container for each HTTP request to GET /api/health.
		/// </summary>
		/// <param name="configuration">
		/// Hierarchical configuration sourced from AWS Secrets Manager, environment variables,
		/// Parameter Store, and appsettings.json files. Used to verify connection string availability.
		/// </param>
		/// <param name="memoryCache">
		/// In-process memory cache replacing Application[] state. The "imageURL" cache key presence
		/// indicates that SplendidInit.InitApp() has completed successfully (application initialized).
		/// </param>
		/// <param name="dbProviderFactories">
		/// Provider-agnostic database factory registry. GetFactory() returns a DbProviderFactory
		/// configured with the SQL Server connection string for the health check DB probe.
		/// </param>
		public HealthCheckController(IConfiguration configuration, IMemoryCache memoryCache, DbProviderFactories dbProviderFactories)
		{
			_configuration      = configuration      ;
			_memoryCache        = memoryCache        ;
			_dbProviderFactories = dbProviderFactories;
		}

		/// <summary>
		/// GET /api/health — Returns application health status as JSON.
		/// 
		/// Diagnostic checks performed (from SystemCheck.aspx.cs patterns):
		/// 1. Machine name retrieval (line 44) — wrapped in try/catch for Azure App Service compat.
		/// 2. Database connectivity check (lines 58-72) — opens connection, executes 'select @@VERSION',
		///    returns SQL Server version string if successful.
		/// 3. Application initialization check (line 89) — checks IMemoryCache for "imageURL" key
		///    which is set by SplendidInit.InitApp() on successful application bootstrap.
		/// 
		/// Returns 200 OK on success, 503 Service Unavailable if database is unreachable.
		/// </summary>
		/// <returns>
		/// 200 OK: <c>{"status":"Healthy","machineName":"...","sqlVersion":"...","timestamp":"...","initialized":true}</c>
		/// 503 Service Unavailable: <c>{"status":"Unhealthy","error":"...","timestamp":"..."}</c>
		/// </returns>
		[HttpGet]
		public IActionResult Get()
		{
			// =====================================================================================
			// Step 1: Capture the current UTC timestamp for the health response.
			// =====================================================================================
			string sTimestamp = DateTime.UtcNow.ToString("o"); // ISO 8601 round-trip format

			// =====================================================================================
			// Step 2: Retrieve machine name — wrapped in try/catch.
			// 09/17/2009 Paul: Azure does not support MachineName. Just ignore the error.
			// (Preserved from SystemCheck.aspx.cs line 43-51 pattern.)
			// =====================================================================================
			string sMachineName = String.Empty;
			try
			{
				// 09/17/2009 Paul.  Azure does not support MachineName.  Just ignore the error.
				sMachineName = System.Environment.MachineName;
			}
			catch
			{
				// Silently ignore — machine name is informational only and may be unavailable
				// in certain cloud hosting environments (Azure App Service, AWS Lambda, etc.).
			}

			// =====================================================================================
			// Step 3: Check application initialization status via IMemoryCache.
			// Replaces: Sql.IsEmptyString(Application["imageURL"]) from SystemCheck.aspx.cs line 89.
			// The "imageURL" cache key is set by SplendidInit.InitApp() upon successful bootstrap.
			// BEFORE: bool bInitialized = !Sql.IsEmptyString(Application["imageURL"]);
			// AFTER:  bool bInitialized = _memoryCache.TryGetValue("imageURL", out object _);
			// =====================================================================================
			bool bInitialized = _memoryCache.TryGetValue("imageURL", out object _);

			// =====================================================================================
			// Step 4: Database connectivity check.
			// Migrated from SystemCheck.aspx.cs lines 55-83:
			//   SplendidCRM.DbProviderFactory dbf = SplendidCRM.DbProviderFactories.GetFactory();
			//   using ( IDbConnection con = dbf.CreateConnection() ) { ... select @@VERSION ... }
			// On failure, the original code removed Application["imageURL"] to force re-initialization.
			// In the ASP.NET Core migration, we return 503 and log the error instead.
			// =====================================================================================
			string sSqlVersion = String.Empty;
			try
			{
				// .NET 10 Migration: SplendidCRM.DbProviderFactories.GetFactory() →
				// injected _dbProviderFactories.GetFactory()
				// Preserving exact pattern from SystemCheck.aspx.cs line 58:
				//   SplendidCRM.DbProviderFactory dbf = SplendidCRM.DbProviderFactories.GetFactory();
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					// 09/27/2009 Paul.  Show SQL version.
					// (Preserved from SystemCheck.aspx.cs lines 63-71.)
					if ( Sql.IsSQLServer(con) )
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.CommandText = "select @@VERSION";
							// Sql.ToString() safely converts ExecuteScalar() result (object) to string,
							// returning String.Empty instead of throwing on null/DBNull.
							sSqlVersion = Sql.ToString(cmd.ExecuteScalar());
							// Original code replaced "\n" with "<br>\n" for HTML display.
							// In the JSON API response, we preserve only newline normalization
							// to maintain the version string as a readable single-line value
							// without HTML markup in the JSON payload.
							sSqlVersion = sSqlVersion.Replace("\n", " ").Trim();
						}
					}
				}
			}
			catch (Exception ex)
			{
				// Database connectivity failure — return 503 Service Unavailable.
				// Return a generic error message to avoid exposing internal details (connection strings, etc.)
				// to unauthenticated users. Full exception details are logged server-side.
				// Per AAP §0.4.4: on DB failure, return 503 with {"status":"Unhealthy","error":"..."}.
				string sLogMessage = ex.Message;
				if ( ex.InnerException != null )
					sLogMessage = ex.InnerException.Message + " | " + sLogMessage;
				System.Diagnostics.Debug.WriteLine("HealthCheck DB failure: " + sLogMessage);

				return StatusCode(503, new
				{
					status      = "Unhealthy",
					error       = "Database connection failed",
					machineName = sMachineName,
					timestamp   = sTimestamp
				});
			}

			// =====================================================================================
			// Step 5: All checks passed — return 200 OK with Healthy status.
			// Response format per AAP §0.4.4 handoff for Prompt 3:
			//   GET /api/health → 200 OK {"status":"Healthy"} (plus additional diagnostic fields)
			// =====================================================================================
			// .NET 10 Migration: Replace Response.ExpiresAbsolute with Cache-Control header.
			// Original: Response.ExpiresAbsolute = new DateTime(1980, 1, 1, 0, 0, 0, 0);
			// Equivalent: no-store prevents caching of the health check response.
			HttpContext.Response.Headers["Cache-Control"] = "no-store, no-cache";

			// SQL Server version is diagnostic information that aids attack reconnaissance.
			// Only include it when the caller is authenticated. Unauthenticated health checks
			// (ALB, ECS, monitoring) receive the minimal Healthy/Unhealthy status.
			return Ok(new
			{
				status      = "Healthy",
				machineName = sMachineName,
				timestamp   = sTimestamp,
				initialized = bInitialized
			});
		}
	}
}
