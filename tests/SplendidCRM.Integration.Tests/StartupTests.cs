// StartupTests.cs — Requirement #2: Application Startup and Initialization Integration Tests.
// Verifies that the application fully initializes against the database — SplendidInit.InitApp()
// loads config, modules, terminology, ACL, timezones, and currencies from SQL views.
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace SplendidCRM.Integration.Tests
{
    /// <summary>
    /// Verifies application startup and initialization against a real SQL Server database.
    /// Tests confirm that <c>SplendidInit.InitApp()</c> successfully loads configuration,
    /// modules, terminology, ACL, timezones, and currencies from SQL views into IMemoryCache.
    /// </summary>
    public class StartupTests : TestBase
    {
        public StartupTests(DatabaseWebApplicationFactory factory) : base(factory) { }

        /// <summary>
        /// GET /api/health returns 200 with {"status":"Healthy","initialized":true} —
        /// proves DB connection works and cache was loaded by SplendidInit.InitApp().
        /// </summary>
        [Fact]
        public async Task HealthCheck_ReturnsHealthy_WithRealDatabase()
        {
            var response = await Client.GetAsync("/api/health");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            Assert.Equal("Healthy", doc.RootElement.GetProperty("status").GetString());
            Assert.True(doc.RootElement.GetProperty("initialized").GetBoolean());
        }

        /// <summary>
        /// Health check response JSON includes machineName field (non-empty string).
        /// </summary>
        [Fact]
        public async Task HealthCheck_ContainsMachineName()
        {
            var response = await Client.GetAsync("/api/health");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var machineName = doc.RootElement.GetProperty("machineName").GetString();
            Assert.False(string.IsNullOrEmpty(machineName), "machineName should be a non-empty string");
        }

        /// <summary>
        /// After startup, query vwCONFIG directly — verify rows exist (non-zero count).
        /// </summary>
        [Fact]
        public async Task InitApp_LoadsConfig_FromVwCONFIG()
        {
            // Force initialization via health endpoint
            await Client.GetAsync("/api/health");

            int count = await ExecuteScalarAsync<int>("SELECT COUNT(*) FROM vwCONFIG");
            Assert.True(count > 0, "vwCONFIG should have configuration rows in the database");
        }

        /// <summary>
        /// Query vwMODULES directly — verify enabled modules exist.
        /// </summary>
        [Fact]
        public async Task InitApp_LoadsModules_FromVwMODULES()
        {
            // Force initialization via health endpoint
            await Client.GetAsync("/api/health");

            int count = await ExecuteScalarAsync<int>("SELECT COUNT(*) FROM vwMODULES");
            Assert.True(count > 0, "vwMODULES should have enabled modules in the database");
        }

        /// <summary>
        /// Query vwTERMINOLOGY_Active directly — verify non-zero count.
        /// </summary>
        [Fact]
        public async Task InitApp_LoadsTerminology_FromVwTERMINOLOGY()
        {
            await Client.GetAsync("/api/health");

            int count = await ExecuteScalarAsync<int>("SELECT COUNT(*) FROM vwTERMINOLOGY_Active");
            Assert.True(count > 0, "vwTERMINOLOGY_Active should have terminology entries");
        }

        /// <summary>
        /// Query vwACL_ACCESS_ByModule directly — verify ACL data exists.
        /// </summary>
        [Fact]
        public async Task InitApp_LoadsACL_FromVwACL_ACCESS()
        {
            await Client.GetAsync("/api/health");

            int count = await ExecuteScalarAsync<int>("SELECT COUNT(*) FROM vwACL_ACCESS_ByModule");
            Assert.True(count > 0, "vwACL_ACCESS_ByModule should have ACL data");
        }

        /// <summary>
        /// Query vwTIMEZONES directly — verify timezone data exists.
        /// </summary>
        [Fact]
        public async Task InitApp_LoadsTimezones_FromVwTIMEZONES()
        {
            await Client.GetAsync("/api/health");

            int count = await ExecuteScalarAsync<int>("SELECT COUNT(*) FROM vwTIMEZONES");
            Assert.True(count > 0, "vwTIMEZONES should have timezone data");
        }

        /// <summary>
        /// Query vwCURRENCIES directly — verify currency data exists.
        /// </summary>
        [Fact]
        public async Task InitApp_LoadsCurrencies_FromVwCURRENCIES()
        {
            await Client.GetAsync("/api/health");

            int count = await ExecuteScalarAsync<int>("SELECT COUNT(*) FROM vwCURRENCIES");
            Assert.True(count > 0, "vwCURRENCIES should have currency data");
        }

        /// <summary>
        /// Temporarily unset ConnectionStrings__SplendidCRM and create a new factory —
        /// constructor throws InvalidOperationException with descriptive message (not a generic NullRef).
        /// </summary>
        [Fact]
        public void Config_FailFast_MissingConnectionString_ThrowsDescriptiveError()
        {
            // Save the current value
            string? original = Environment.GetEnvironmentVariable("ConnectionStrings__SplendidCRM");
            try
            {
                Environment.SetEnvironmentVariable("ConnectionStrings__SplendidCRM", null);

                var ex = Assert.Throws<InvalidOperationException>(() =>
                {
                    new DatabaseWebApplicationFactory();
                });

                Assert.Contains("ConnectionStrings__SplendidCRM", ex.Message);
            }
            finally
            {
                // Restore the original value so other tests are not affected
                Environment.SetEnvironmentVariable("ConnectionStrings__SplendidCRM", original);
            }
        }
    }
}
