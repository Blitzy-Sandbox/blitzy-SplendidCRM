// HostedServiceRegistrationTests.cs — Verifies all 4 hosted services are registered in DI.
// These tests use a separate factory that does NOT remove hosted services, to verify
// they are actually registered by Program.cs.
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
using Xunit;
using FluentAssertions;

namespace SplendidCRM.Web.Tests.ServiceTests
{
	/// <summary>
	/// Factory that preserves hosted service registrations (unlike the main CustomWebApplicationFactory
	/// which removes them to prevent background DB access during testing).
	/// Used only for verifying service registration.
	/// </summary>
	public class ServiceRegistrationFactory : WebApplicationFactory<Program>
	{
		public ServiceRegistrationFactory()
		{
			Environment.SetEnvironmentVariable("ConnectionStrings__SplendidCRM",
				"Server=localhost,1433;Database=SplendidCRM_Test;User Id=sa;Password=TestPassword123!;TrustServerCertificate=True;");
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
				// Override distributed cache to prevent SQL connection failures
				services.RemoveAll<IDistributedCache>();
				services.AddSingleton<IDistributedCache>(sp =>
					new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
			});
		}
	}

	public class HostedServiceRegistrationTests : IClassFixture<ServiceRegistrationFactory>
	{
		private readonly ServiceRegistrationFactory _factory;

		public HostedServiceRegistrationTests(ServiceRegistrationFactory factory)
		{
			_factory = factory;
		}

		[Fact]
		public void SchedulerHostedService_IsRegistered()
		{
			// Verify the scheduler hosted service is registered in DI
			using var scope = _factory.Services.CreateScope();
			var hostedServices = scope.ServiceProvider.GetServices<IHostedService>();
			hostedServices.Should().Contain(s => s.GetType().Name == "SchedulerHostedService",
				"SchedulerHostedService should be registered as IHostedService");
		}

		[Fact]
		public void EmailPollingHostedService_IsRegistered()
		{
			using var scope = _factory.Services.CreateScope();
			var hostedServices = scope.ServiceProvider.GetServices<IHostedService>();
			hostedServices.Should().Contain(s => s.GetType().Name == "EmailPollingHostedService",
				"EmailPollingHostedService should be registered as IHostedService");
		}

		[Fact]
		public void ArchiveHostedService_IsRegistered()
		{
			using var scope = _factory.Services.CreateScope();
			var hostedServices = scope.ServiceProvider.GetServices<IHostedService>();
			hostedServices.Should().Contain(s => s.GetType().Name == "ArchiveHostedService",
				"ArchiveHostedService should be registered as IHostedService");
		}

		[Fact]
		public void CacheInvalidationService_IsRegistered()
		{
			using var scope = _factory.Services.CreateScope();
			var hostedServices = scope.ServiceProvider.GetServices<IHostedService>();
			hostedServices.Should().Contain(s => s.GetType().Name == "CacheInvalidationService",
				"CacheInvalidationService should be registered as IHostedService");
		}

		[Fact]
		public void AllFourHostedServices_AreRegistered()
		{
			using var scope = _factory.Services.CreateScope();
			var hostedServices = scope.ServiceProvider.GetServices<IHostedService>().ToList();
			var serviceNames = hostedServices.Select(s => s.GetType().Name).ToList();
			serviceNames.Should().Contain("SchedulerHostedService");
			serviceNames.Should().Contain("EmailPollingHostedService");
			serviceNames.Should().Contain("ArchiveHostedService");
			serviceNames.Should().Contain("CacheInvalidationService");
		}
	}
}
