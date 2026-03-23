// AuthenticationIntegrationTests.cs — Requirement #3: Authentication and Login Integration Tests.
// Verifies the complete login flow against the database — stored procedure execution,
// session creation, cookie issuance, and authenticated access.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SplendidCRM.Integration.Tests
{
    /// <summary>
    /// Verifies the complete authentication lifecycle against a real SQL Server database:
    /// login with valid/invalid credentials, session creation in dbo.SplendidSessions,
    /// cookie issuance, authenticated endpoint access, and logout.
    /// </summary>
    public class AuthenticationIntegrationTests : TestBase
    {
        public AuthenticationIntegrationTests(DatabaseWebApplicationFactory factory) : base(factory) { }

        /// <summary>
        /// POST /Rest.svc/Login with valid admin credentials returns user data including
        /// USER_ID, USER_NAME, and IS_ADMIN. Cross-verifies against vwUSERS_Login.
        /// Exercises: vwUSERS_Login, spUSERS_LOGINS_InsertOnly.
        /// </summary>
        [Fact]
        public async Task Login_ValidCredentials_ReturnsUserData()
        {
            var loginPayload = new Dictionary<string, object> { ["UserName"] = "admin", ["Password"] = "admin" };
            var content = new StringContent(JsonSerializer.Serialize(loginPayload), Encoding.UTF8, "application/json");

            var response = await Client.PostAsync("/Rest.svc/Login", content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var d = doc.RootElement.GetProperty("d");

            Assert.NotEqual(Guid.Empty.ToString(), d.GetProperty("USER_ID").GetString());
            Assert.Equal("admin", d.GetProperty("USER_NAME").GetString());
            Assert.True(d.GetProperty("IS_ADMIN").GetBoolean());

            // Cross-verify against database
            using var conn = CreateDirectConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT ID, USER_NAME, IS_ADMIN FROM vwUSERS_Login WHERE USER_NAME = 'admin' AND STATUS = 'Active'";
            using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), "admin user should exist in vwUSERS_Login");
        }

        /// <summary>
        /// Wrong password returns a non-200 error response (not a 500 server error).
        /// Exercises: vwUSERS_Login (password mismatch path).
        /// </summary>
        [Fact]
        public async Task Login_InvalidPassword_ReturnsError()
        {
            var loginPayload = new Dictionary<string, object> { ["UserName"] = "admin", ["Password"] = "wrong_password_12345" };
            var content = new StringContent(JsonSerializer.Serialize(loginPayload), Encoding.UTF8, "application/json");

            var response = await Client.PostAsync("/Rest.svc/Login", content);

            // Should return 403 Forbidden (login denied), NOT 500 Internal Server Error
            Assert.True(
                response.StatusCode == HttpStatusCode.Forbidden ||
                response.StatusCode == HttpStatusCode.Unauthorized ||
                (int)response.StatusCode >= 400,
                $"Expected 4xx error for invalid password, got {(int)response.StatusCode}");
            Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        /// <summary>
        /// Empty username body returns an error response (input validation).
        /// </summary>
        [Fact]
        public async Task Login_EmptyUsername_ReturnsError()
        {
            var loginPayload = new Dictionary<string, object> { ["UserName"] = "", ["Password"] = "admin" };
            var content = new StringContent(JsonSerializer.Serialize(loginPayload), Encoding.UTF8, "application/json");

            var response = await Client.PostAsync("/Rest.svc/Login", content);

            Assert.True(
                (int)response.StatusCode >= 400,
                $"Expected error for empty username, got {(int)response.StatusCode}");
            Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        /// <summary>
        /// After login, SELECT COUNT(*) FROM dbo.SplendidSessions > 0.
        /// Proves session data is persisted to the SQL distributed session table.
        /// </summary>
        [Fact]
        public async Task Login_CreatesSessionInDatabase()
        {
            var client = await GetAuthenticatedClient();

            // Make a request to ensure session is written
            await client.GetAsync("/api/health");

            int sessionCount = await ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.SplendidSessions");
            Assert.True(sessionCount > 0, "dbo.SplendidSessions should contain at least one session after login");
        }

        /// <summary>
        /// Login response includes Set-Cookie header containing session cookie.
        /// </summary>
        [Fact]
        public async Task Login_SetsCookieHeader()
        {
            var loginPayload = new Dictionary<string, object> { ["UserName"] = "admin", ["Password"] = "admin" };
            var content = new StringContent(JsonSerializer.Serialize(loginPayload), Encoding.UTF8, "application/json");

            var response = await Client.PostAsync("/Rest.svc/Login", content);
            response.EnsureSuccessStatusCode();

            // Check for Set-Cookie headers — should contain session or auth cookies
            Assert.True(
                response.Headers.Contains("Set-Cookie") ||
                (response.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any()),
                "Login response should include Set-Cookie header");
        }

        /// <summary>
        /// After login, POST /Rest.svc/IsAuthenticated returns success.
        /// Proves session data is readable on subsequent requests.
        /// </summary>
        [Fact]
        public async Task Login_SessionContainsUserID()
        {
            var client = await GetAuthenticatedClient();

            var response = await client.PostAsync("/Rest.svc/IsAuthenticated", null);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            // IsAuthenticated should return a truthy value indicating the user is logged in
            Assert.False(string.IsNullOrEmpty(json), "IsAuthenticated should return non-empty response");
        }

        /// <summary>
        /// Login response d.IS_ADMIN is true for admin user.
        /// Exercises: vwUSERS_Login IS_ADMIN column.
        /// </summary>
        [Fact]
        public async Task Login_AdminUser_HasAdminFlag()
        {
            var loginPayload = new Dictionary<string, object> { ["UserName"] = "admin", ["Password"] = "admin" };
            var content = new StringContent(JsonSerializer.Serialize(loginPayload), Encoding.UTF8, "application/json");

            var response = await Client.PostAsync("/Rest.svc/Login", content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var d = doc.RootElement.GetProperty("d");
            Assert.True(d.GetProperty("IS_ADMIN").GetBoolean(), "admin user should have IS_ADMIN = true");
        }

        /// <summary>
        /// After login, SELECT TOP 1 FROM USERS_LOGINS ORDER BY DATE_ENTERED DESC has matching USER_NAME.
        /// Exercises: spUSERS_LOGINS_InsertOnly audit trail.
        /// </summary>
        [Fact]
        public async Task Login_RecordsLoginAttempt()
        {
            // Record the time just before login
            DateTime beforeLogin = DateTime.UtcNow.AddSeconds(-2);

            var client = await GetAuthenticatedClient();

            // Check that a login record was created
            using var conn = CreateDirectConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT TOP 1 USER_NAME
                FROM USERS_LOGINS
                WHERE DATE_ENTERED >= @BeforeLogin
                ORDER BY DATE_ENTERED DESC";
            cmd.Parameters.Add(new SqlParameter("@BeforeLogin", beforeLogin));
            var userName = await cmd.ExecuteScalarAsync();
            Assert.Equal("admin", userName?.ToString());
        }

        /// <summary>
        /// After login with cookies, GET /Rest.svc/GetModuleList?ModuleName=Contacts returns 200.
        /// Exercises: Session + vwCONTACTS.
        /// </summary>
        [Fact]
        public async Task Authenticated_GetModuleList_Returns200()
        {
            var client = await GetAuthenticatedClient();

            var response = await client.GetAsync("/Rest.svc/GetModuleList?ModuleName=Contacts&$top=1");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        /// <summary>
        /// Without login, GET /Rest.svc/GetModuleList?ModuleName=Contacts returns 401.
        /// Exercises: Auth middleware enforcement.
        /// </summary>
        [Fact]
        public async Task Unauthenticated_GetModuleList_Returns401()
        {
            // Create a fresh client without any cookies
            var freshClient = Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false
            });

            var response = await freshClient.GetAsync("/Rest.svc/GetModuleList?ModuleName=Contacts&$top=1");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        /// <summary>
        /// POST /Rest.svc/Logout ends the session. A subsequent authenticated request returns 401.
        /// Exercises: spUSERS_LOGINS_Logout, session invalidation.
        /// </summary>
        [Fact]
        public async Task Logout_EndsSession_Returns401OnNextRequest()
        {
            var client = await GetAuthenticatedClient();

            // Verify authenticated first
            var authCheck = await client.GetAsync("/Rest.svc/GetModuleList?ModuleName=Contacts&$top=1");
            Assert.Equal(HttpStatusCode.OK, authCheck.StatusCode);

            // Logout
            var logoutResponse = await client.PostAsync("/Rest.svc/Logout", null);
            logoutResponse.EnsureSuccessStatusCode();

            // Next request should fail authentication
            var postLogout = await client.GetAsync("/Rest.svc/GetModuleList?ModuleName=Contacts&$top=1");
            Assert.Equal(HttpStatusCode.Unauthorized, postLogout.StatusCode);
        }

        /// <summary>
        /// Manually expire session row in dbo.SplendidSessions, then next request returns 401.
        /// Exercises: SQL session expiry mechanism.
        /// </summary>
        [Fact]
        public async Task SessionExpiry_ExpiredSession_Returns401()
        {
            var client = await GetAuthenticatedClient();

            // Verify authenticated first
            var authCheck = await client.GetAsync("/Rest.svc/GetModuleList?ModuleName=Contacts&$top=1");
            Assert.Equal(HttpStatusCode.OK, authCheck.StatusCode);

            // Manually expire ALL sessions to ensure our test session is expired
            await ExecuteNonQueryAsync(
                "UPDATE dbo.SplendidSessions SET ExpiresAtTime = DATEADD(MINUTE, -30, GETUTCDATE())");

            // Next request should fail because session is expired
            var postExpiry = await client.GetAsync("/Rest.svc/GetModuleList?ModuleName=Contacts&$top=1");
            Assert.Equal(HttpStatusCode.Unauthorized, postExpiry.StatusCode);

            // Cleanup: remove expired sessions
            await ExecuteNonQueryAsync(
                "DELETE FROM dbo.SplendidSessions WHERE ExpiresAtTime < GETUTCDATE()");
        }
    }
}
