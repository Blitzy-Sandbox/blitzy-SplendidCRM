// DatabaseWebApplicationFactory.cs — Integration test host factory for SplendidCRM.Web.
// Connects to the REAL SQL Server database (not in-memory stubs) for database integration testing.
// Reads ConnectionStrings__SplendidCRM from the environment and provisions the distributed session table.
using System;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SplendidCRM.Integration.Tests
{
    /// <summary>
    /// Custom <see cref="WebApplicationFactory{TEntryPoint}"/> for database integration testing.
    /// Unlike <c>CustomWebApplicationFactory</c> (which replaces IDistributedCache with in-memory stubs),
    /// this factory connects to the REAL SQL Server database specified by the
    /// <c>ConnectionStrings__SplendidCRM</c> environment variable.
    /// Background hosted services are disabled to prevent interference with test execution.
    /// </summary>
    public class DatabaseWebApplicationFactory : WebApplicationFactory<Program>
    {
        /// <summary>
        /// The SQL Server connection string used for direct ADO.NET verification queries in tests.
        /// </summary>
        public string ConnectionString { get; private set; }

        public DatabaseWebApplicationFactory()
        {
            // Read from environment — already set by the CI/CD or local setup script.
            ConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__SplendidCRM")
                ?? throw new InvalidOperationException(
                    "ConnectionStrings__SplendidCRM environment variable is not set. " +
                    "Database integration tests require a real SQL Server connection.");

            // Resolve $SQL_PASSWORD placeholder if present (setup scripts may store the literal token).
            string? sqlPassword = Environment.GetEnvironmentVariable("SQL_PASSWORD");
            if (!string.IsNullOrEmpty(sqlPassword) && ConnectionString.Contains("$SQL_PASSWORD"))
            {
                ConnectionString = ConnectionString.Replace("$SQL_PASSWORD", sqlPassword);
                Environment.SetEnvironmentVariable("ConnectionStrings__SplendidCRM", ConnectionString);
            }

            // These supplementary variables are needed for Program.cs startup validation.
            // They are set idempotently — if already set by the unit test factory, these overwrite
            // with integration-test-appropriate values.
            Environment.SetEnvironmentVariable("SESSION_PROVIDER", "SqlServer");
            Environment.SetEnvironmentVariable("SESSION_CONNECTION", ConnectionString);
            Environment.SetEnvironmentVariable("AUTH_MODE", "Forms");
            Environment.SetEnvironmentVariable("SPLENDID_JOB_SERVER", "test-server");
            Environment.SetEnvironmentVariable("CORS_ORIGINS", "http://localhost");
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
            Environment.SetEnvironmentVariable("Authentication__Mode", "Forms");

            EnsureSessionTable();
        }

        /// <summary>
        /// Ensures the <c>dbo.SplendidSessions</c> table exists for ASP.NET Core distributed SQL Server cache.
        /// Uses idempotent <c>IF NOT EXISTS</c> check so it is safe to call multiple times.
        /// Schema matches the ASP.NET Core distributed cache provider expectations:
        /// <c>Id nvarchar(449) PK, Value varbinary(max), ExpiresAtTime datetimeoffset(7),
        /// SlidingExpirationInSeconds bigint NULL, AbsoluteExpiration datetimeoffset(7) NULL</c>.
        /// </summary>
        private void EnsureSessionTable()
        {
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES
                               WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'SplendidSessions')
                BEGIN
                    CREATE TABLE dbo.SplendidSessions (
                        Id                           nvarchar(449)      NOT NULL PRIMARY KEY,
                        Value                        varbinary(max)     NOT NULL,
                        ExpiresAtTime                datetimeoffset(7)  NOT NULL,
                        SlidingExpirationInSeconds   bigint             NULL,
                        AbsoluteExpiration           datetimeoffset(7)  NULL
                    );
                    CREATE NONCLUSTERED INDEX IX_SplendidSessions_ExpiresAtTime
                        ON dbo.SplendidSessions(ExpiresAtTime);
                END";
            cmd.ExecuteNonQuery();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                // Remove hosted services ONLY — keep ALL database services real.
                // Hosted services (SchedulerHostedService, EmailPollingHostedService, etc.)
                // would interfere with test execution by running background database queries.
                var hostedServiceDescriptors = services
                    .Where(d => d.ServiceType == typeof(IHostedService))
                    .ToList();
                foreach (var descriptor in hostedServiceDescriptors)
                    services.Remove(descriptor);

                // Do NOT replace IDistributedCache — use real SQL Server session store.
                // This is the key difference from CustomWebApplicationFactory.
            });
        }
    }
}
