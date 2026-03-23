// CustomWebApplicationFactory.cs — Test host factory for SplendidCRM.Web integration tests.
// Configures required environment variables, disables hosted services and AWS providers,
// and replaces database-dependent services with test stubs.
using System;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SplendidCRM.Web.Services;

namespace SplendidCRM.Web.Tests
{
	/// <summary>
	/// Custom <see cref="WebApplicationFactory{TEntryPoint}"/> for integration testing.
	/// Sets required environment variables BEFORE Program.cs runs its early validation,
	/// disables background hosted services, replaces SqlServer/Redis distributed cache
	/// with in-memory implementation, and suppresses AWS configuration providers.
	/// </summary>
	public class CustomWebApplicationFactory : WebApplicationFactory<Program>
	{
		/// <summary>
		/// Set environment variables before the host builder is created.
		/// This ensures Program.cs early validation and StartupValidator.Validate() see
		/// all required configuration values. SESSION_PROVIDER must be "Redis" or "SqlServer"
		/// to pass validation, but the actual IDistributedCache is replaced with an in-memory
		/// implementation in ConfigureServices below.
		/// </summary>
		public CustomWebApplicationFactory()
		{
			// These must be set BEFORE CreateHost/CreateHostBuilder runs,
			// because Program.cs performs early validation on builder.Configuration
			// which includes environment variables by default.
			Environment.SetEnvironmentVariable("ConnectionStrings__SplendidCRM",
				"Server=localhost,1433;Database=SplendidCRM_Test;User Id=sa;Password=TestPassword123!;TrustServerCertificate=True;");
			// SESSION_PROVIDER must be "Redis" or "SqlServer" to pass StartupValidator (line 128-131).
			// The actual SQL Server distributed cache is replaced with in-memory in ConfigureServices.
			Environment.SetEnvironmentVariable("SESSION_PROVIDER", "SqlServer");
			Environment.SetEnvironmentVariable("SESSION_CONNECTION",
				"Server=localhost,1433;Database=SplendidCRM_Session;User Id=sa;Password=TestPassword123!;TrustServerCertificate=True;");
			Environment.SetEnvironmentVariable("AUTH_MODE", "Forms");
			Environment.SetEnvironmentVariable("SPLENDID_JOB_SERVER", "test-server");
			Environment.SetEnvironmentVariable("CORS_ORIGINS", "http://localhost");
			Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
			Environment.SetEnvironmentVariable("Authentication__Mode", "Forms");
		}

		protected override void ConfigureWebHost(IWebHostBuilder builder)
		{
			builder.UseEnvironment("Testing");

			builder.ConfigureAppConfiguration((context, config) =>
			{
				// Add in-memory configuration to supplement environment variables
				config.AddInMemoryCollection(new Dictionary<string, string?>
				{
					["SCHEDULER_INTERVAL_MS"] = "60000",
					["EMAIL_POLL_INTERVAL_MS"] = "60000",
					["ARCHIVE_INTERVAL_MS"] = "300000",
					["LOG_LEVEL"] = "Warning",
					["Authentication:Mode"] = "Forms",
				});
			});

			builder.ConfigureServices(services =>
			{
				// Remove all hosted services to prevent background tasks during testing.
				// They require database connections that are not available in the test environment.
				var hostedServiceDescriptors = services
					.Where(d => d.ServiceType == typeof(IHostedService))
					.ToList();
				foreach (var descriptor in hostedServiceDescriptors)
				{
					services.Remove(descriptor);
				}

				// Replace any SqlServer/Redis distributed cache with in-memory implementation.
				// This prevents session middleware from failing with SQL connection errors (error 18456)
				// when no real database is available during integration tests.
				services.RemoveAll<IDistributedCache>();
				services.AddSingleton<IDistributedCache>(sp =>
					new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
			});
		}

		// NOTE: Environment variables are intentionally NOT cleared in Dispose.
		// All integration test classes share the same process, and clearing env vars
		// when one class's factory disposes causes subsequent factory instances to fail
		// (Program.cs early validation calls Environment.Exit(1) on missing connection string).
		// The env vars are process-scoped and harmless — they only affect the test process.
	}
}
