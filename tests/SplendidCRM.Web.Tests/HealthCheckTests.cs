// HealthCheckTests.cs — Integration tests for GET /api/health endpoint.
// The health endpoint returns 200 OK when DB is reachable, or 503 Service Unavailable when
// the database is unreachable. Both are valid health check responses with JSON bodies.
// In the test environment (no live DB), 503 is the expected behavior.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SplendidCRM.Web.Tests
{
	public class HealthCheckTests : IClassFixture<CustomWebApplicationFactory>
	{
		private readonly HttpClient _client;

		public HealthCheckTests(CustomWebApplicationFactory factory)
		{
			_client = factory.CreateClient(new WebApplicationFactoryClientOptions
			{
				AllowAutoRedirect = false
			});
		}

		[Fact]
		public async Task HealthEndpoint_ReturnsHealthCheckResponse()
		{
			// The health endpoint returns 200 when DB is reachable, or 503 when DB is
			// unreachable. Both are valid health check responses — the endpoint itself
			// is correctly registered and handling requests. In the test environment
			// (no live SQL Server), 503 is the expected behavior.
			var response = await _client.GetAsync("/api/health");
			var validStatuses = new[] { HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable };
			validStatuses.Should().Contain(response.StatusCode,
				"health endpoint should return 200 (DB up) or 503 (DB down), not 404 or 500");
		}

		[Fact]
		public async Task HealthEndpoint_ReturnsJsonWithStatusField()
		{
			// Both 200 and 503 responses include a "status" field ("Healthy" or "Unhealthy").
			var response = await _client.GetAsync("/api/health");
			var content = await response.Content.ReadAsStringAsync();
			// The response must contain either "Healthy" (200) or "Unhealthy" (503)
			bool hasHealthStatus = content.Contains("Healthy", StringComparison.Ordinal)
				|| content.Contains("Unhealthy", StringComparison.Ordinal);
			hasHealthStatus.Should().BeTrue("health response should contain a status field with Healthy or Unhealthy");
		}

		[Fact]
		public async Task HealthEndpoint_ContainsMachineName()
		{
			var response = await _client.GetAsync("/api/health");
			var content = await response.Content.ReadAsStringAsync();
			// Health endpoint should include machine name or hostname in both 200 and 503 responses
			bool hasMachineName = content.Contains("machineName", StringComparison.OrdinalIgnoreCase)
				|| content.Contains("machine", StringComparison.OrdinalIgnoreCase);
			hasMachineName.Should().BeTrue("health response should contain a machine name field");
		}

		[Fact]
		public async Task HealthEndpoint_ContainsTimestamp()
		{
			var response = await _client.GetAsync("/api/health");
			var content = await response.Content.ReadAsStringAsync();
			bool hasTimestamp = content.Contains("timestamp", StringComparison.OrdinalIgnoreCase)
				|| content.Contains("time", StringComparison.OrdinalIgnoreCase);
			hasTimestamp.Should().BeTrue("health response should contain a timestamp field");
		}

		[Fact]
		public async Task HealthEndpoint_ResponseIsValidJson()
		{
			var response = await _client.GetAsync("/api/health");
			var content = await response.Content.ReadAsStringAsync();
			Action act = () => JsonDocument.Parse(content);
			act.Should().NotThrow("health endpoint should return valid JSON");
		}
	}
}
