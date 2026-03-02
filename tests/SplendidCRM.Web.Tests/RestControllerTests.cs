// RestControllerTests.cs — Integration tests verifying REST route registration.
using System.Net;
using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SplendidCRM.Web.Tests
{
	public class RestControllerTests : IClassFixture<CustomWebApplicationFactory>
	{
		private readonly HttpClient _client;

		public RestControllerTests(CustomWebApplicationFactory factory)
		{
			_client = factory.CreateClient(new WebApplicationFactoryClientOptions
			{
				AllowAutoRedirect = false
			});
		}

		/// <summary>
		/// Verifies that critical REST routes are registered and respond (even if with 401).
		/// A 404 means the route is not registered at all — that's the failure condition.
		/// </summary>
		[Theory]
		[InlineData("/Rest.svc/GetReactState")]
		[InlineData("/Rest.svc/GetModuleList")]
		[InlineData("/Rest.svc/GetModuleItem")]
		[InlineData("/Rest.svc/Login")]
		[InlineData("/Rest.svc/Logout")]
		[InlineData("/Rest.svc/ChangePassword")]
		[InlineData("/Rest.svc/GetSqlColumns")]
		[InlineData("/Rest.svc/GetReactLoginState")]
		public async Task RestRoute_Exists_DoesNotReturn404(string route)
		{
			var response = await _client.GetAsync(route);
			response.StatusCode.Should().NotBe(HttpStatusCode.NotFound,
				$"Route {route} should be registered and not return 404");
		}

		[Theory]
		[InlineData("/Rest.svc/UpdateModule")]
		[InlineData("/Rest.svc/DeleteModuleItem")]
		public async Task RestPostRoute_Exists_DoesNotReturn404(string route)
		{
			var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
			var response = await _client.PostAsync(route, content);
			response.StatusCode.Should().NotBe(HttpStatusCode.NotFound,
				$"Route {route} should be registered and not return 404");
		}

		[Fact]
		public async Task RestRoutes_UseRestSvcPrefix()
		{
			// Verify that the controller responds under the /Rest.svc/ prefix
			var response = await _client.GetAsync("/Rest.svc/GetReactLoginState");
			// Should not be 404 — route is registered
			response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
		}

		[Fact]
		public async Task LoginRoute_IsAllowAnonymous()
		{
			// Login should be accessible without authentication (AllowAnonymous)
			var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
			var response = await _client.PostAsync("/Rest.svc/Login", content);
			// Should NOT return 401 — Login is AllowAnonymous
			response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
				"Login endpoint should be AllowAnonymous");
		}
	}
}
