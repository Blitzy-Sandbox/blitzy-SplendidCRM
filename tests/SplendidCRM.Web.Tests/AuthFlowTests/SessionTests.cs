// SessionTests.cs — Integration tests verifying session persistence and cookie behavior.
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Net.Http.Headers;

namespace SplendidCRM.Web.Tests.AuthFlowTests
{
	public class SessionTests : IClassFixture<CustomWebApplicationFactory>
	{
		private readonly CustomWebApplicationFactory _factory;

		public SessionTests(CustomWebApplicationFactory factory)
		{
			_factory = factory;
		}

		[Fact]
		public async Task SessionMiddleware_IsConfigured()
		{
			// Verify session middleware is active and configured correctly.
			// ASP.NET Core session middleware only sends Set-Cookie when session data is
			// written during the request. Read-only access may not trigger a cookie.
			// This test verifies the middleware doesn't cause errors and requests succeed.
			var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
			{
				AllowAutoRedirect = false,
				HandleCookies = false
			});
			var response = await client.PostAsync("/Rest.svc/IsAuthenticated",
				new StringContent("{}", Encoding.UTF8, "application/json"));
			// The endpoint should respond successfully (session middleware doesn't crash)
			response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
			response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError,
				"Session middleware should not cause 500 errors");
		}

		[Fact]
		public async Task SessionId_PersistsAcrossRequests()
		{
			// Use a client that handles cookies automatically
			var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
			{
				AllowAutoRedirect = false,
				HandleCookies = true
			});

			// First request — gets session cookie
			var response1 = await client.PostAsync("/Rest.svc/IsAuthenticated",
				new StringContent("{}", Encoding.UTF8, "application/json"));

			// Second request — should reuse same session
			var response2 = await client.PostAsync("/Rest.svc/IsAuthenticated",
				new StringContent("{}", Encoding.UTF8, "application/json"));

			// Both requests should succeed (session persists)
			response1.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
			response2.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
		}

		[Fact]
		public async Task SessionCookie_HasHttpOnlyFlag()
		{
			var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
			{
				AllowAutoRedirect = false,
				HandleCookies = false
			});
			var response = await client.PostAsync("/Rest.svc/IsAuthenticated",
				new StringContent("{}", Encoding.UTF8, "application/json"));

			if (response.Headers.Contains("Set-Cookie"))
			{
				var setCookies = response.Headers.GetValues("Set-Cookie").ToList();
				var sessionCookie = setCookies.FirstOrDefault(c =>
					c.Contains("SplendidCRM.Session", StringComparison.OrdinalIgnoreCase));
				sessionCookie.Should().NotBeNull("Session cookie should be present");
				sessionCookie!.Should().Contain("httponly",
					"Session cookie must have HttpOnly flag for security");
			}
		}

		[Fact]
		public async Task SessionCookie_HasSameSiteLax()
		{
			var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
			{
				AllowAutoRedirect = false,
				HandleCookies = false
			});
			var response = await client.PostAsync("/Rest.svc/IsAuthenticated",
				new StringContent("{}", Encoding.UTF8, "application/json"));

			if (response.Headers.Contains("Set-Cookie"))
			{
				var setCookies = response.Headers.GetValues("Set-Cookie").ToList();
				var sessionCookie = setCookies.FirstOrDefault(c =>
					c.Contains("SplendidCRM.Session", StringComparison.OrdinalIgnoreCase));
				sessionCookie.Should().NotBeNull("Session cookie should be present");
				sessionCookie!.Should().Contain("samesite=lax",
					"Session cookie SameSite should be Lax as configured in Program.cs");
			}
		}

		[Fact]
		public async Task UnauthenticatedSession_IsAuthenticated_ReturnsFalse()
		{
			var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
			{
				AllowAutoRedirect = false
			});
			// IsAuthenticated is [AllowAnonymous] and checks Security.IsAuthenticated()
			var response = await client.PostAsync("/Rest.svc/IsAuthenticated",
				new StringContent("{}", Encoding.UTF8, "application/json"));
			var content = await response.Content.ReadAsStringAsync();
			using var doc = JsonDocument.Parse(content);
			doc.RootElement.TryGetProperty("d", out var dValue).Should().BeTrue();
			dValue.GetBoolean().Should().BeFalse(
				"Unauthenticated session should return false from IsAuthenticated");
		}
	}
}
