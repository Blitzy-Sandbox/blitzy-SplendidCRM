// TestBase.cs — Abstract base class for all database integration tests.
// Provides authenticated HTTP client creation, direct SQL connection helpers, and shared factory access.
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SplendidCRM.Integration.Tests
{
    /// <summary>
    /// Base class for integration tests. Uses <see cref="IClassFixture{T}"/> to share a single
    /// <see cref="DatabaseWebApplicationFactory"/> instance across all tests in the class,
    /// ensuring the application is started once and reused.
    /// </summary>
    public abstract class TestBase : IClassFixture<DatabaseWebApplicationFactory>
    {
        protected readonly DatabaseWebApplicationFactory Factory;
        protected readonly HttpClient Client;
        protected readonly string ConnectionString;

        protected TestBase(DatabaseWebApplicationFactory factory)
        {
            Factory = factory;
            Client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                HandleCookies = true,    // Critical: preserve session cookies between requests
                AllowAutoRedirect = false
            });
            ConnectionString = factory.ConnectionString;
        }

        /// <summary>
        /// Login via POST /Rest.svc/Login and return a client with session cookies.
        /// The Login endpoint accepts <c>[FromBody] Dictionary&lt;string, object&gt;</c>
        /// with keys: <c>UserName</c>, <c>Password</c>.
        /// The returned HttpClient shares cookies with subsequent requests to maintain session.
        /// </summary>
        /// <param name="userName">Username for login (default: "admin").</param>
        /// <param name="password">Password for login (default: "admin").</param>
        /// <returns>An HttpClient with session cookies set from the login response.</returns>
        protected async Task<HttpClient> GetAuthenticatedClient(string userName = "admin", string password = "admin")
        {
            // Create a new client with its own cookie container for session isolation.
            // WebApplicationFactoryClientOptions.HandleCookies = true creates a CookieContainerHandler internally.
            var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false
            });

            var loginPayload = new Dictionary<string, object>
            {
                ["UserName"] = userName,
                ["Password"] = password
            };
            var content = new StringContent(
                JsonSerializer.Serialize(loginPayload), Encoding.UTF8, "application/json");

            var response = await client.PostAsync("/Rest.svc/Login", content);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Login returned {(int)response.StatusCode}: {(errorBody.Length > 500 ? errorBody.Substring(0, 500) : errorBody)}",
                    null, response.StatusCode);
            }
            return client;
        }

        /// <summary>
        /// Creates a direct ADO.NET connection to the SQL Server database for verification queries.
        /// Caller is responsible for opening and disposing the connection.
        /// </summary>
        protected SqlConnection CreateDirectConnection()
        {
            return new SqlConnection(ConnectionString);
        }

        /// <summary>
        /// Executes a scalar SQL query and returns the typed result.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="sql">The SQL query to execute.</param>
        /// <returns>The scalar result, or default if null/DBNull.</returns>
        protected async Task<T?> ExecuteScalarAsync<T>(string sql)
        {
            using var conn = CreateDirectConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            var result = await cmd.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value) return default;
            return (T)Convert.ChangeType(result, typeof(T));
        }

        /// <summary>
        /// Executes a non-query SQL command (INSERT, UPDATE, DELETE, SP call).
        /// </summary>
        /// <param name="sql">The SQL command to execute.</param>
        /// <returns>The number of rows affected.</returns>
        protected async Task<int> ExecuteNonQueryAsync(string sql)
        {
            using var conn = CreateDirectConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            return await cmd.ExecuteNonQueryAsync();
        }
    }
}
