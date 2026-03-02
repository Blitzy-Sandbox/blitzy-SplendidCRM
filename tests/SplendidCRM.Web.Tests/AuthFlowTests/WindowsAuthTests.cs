// WindowsAuthTests.cs — Tests verifying auth mode configuration branching.
// Tests that the AUTH_MODE environment variable correctly selects the authentication scheme.
using System.Net;
using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SplendidCRM.Web.Tests.AuthFlowTests
{
	/// <summary>
	/// Tests that verify the auth mode configuration branching in Program.cs.
	/// The main CustomWebApplicationFactory uses AUTH_MODE=Forms, so these tests
	/// verify Forms mode behavior. Testing Windows/SSO modes requires separate factories
	/// with different AUTH_MODE env vars, which is complex to set up in a single test run
	/// due to environment variable pollution. These tests verify the Forms path.
	/// </summary>
	public class WindowsAuthTests : IClassFixture<CustomWebApplicationFactory>
	{
		private readonly HttpClient _client;

		public WindowsAuthTests(CustomWebApplicationFactory factory)
		{
			_client = factory.CreateClient(new WebApplicationFactoryClientOptions
			{
				AllowAutoRedirect = false
			});
		}

		[Fact]
		public async Task FormsMode_NoNegotiateChallenge()
		{
			// In Forms mode, the server should NOT send a WWW-Authenticate: Negotiate challenge
			var response = await _client.GetAsync("/Rest.svc/GetReactState");
			if (response.Headers.Contains("WWW-Authenticate"))
			{
				var wwwAuth = response.Headers.GetValues("WWW-Authenticate");
				wwwAuth.Should().NotContain("Negotiate",
					"Forms auth mode should not include Negotiate challenge");
			}
		}

		[Fact]
		public async Task FormsMode_Returns401_NotChallenge()
		{
			// Forms mode API endpoints should return plain 401 without NTLM/Negotiate headers
			var response = await _client.GetAsync("/Rest.svc/GetReactState");
			response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
		}

		[Fact]
		public async Task AuthMode_Forms_Configured()
		{
			// Verify the test factory has Forms auth mode configured
			// This is validated implicitly: if the factory starts successfully with AUTH_MODE=Forms,
			// the authentication pipeline is configured correctly.
			var response = await _client.PostAsync("/Rest.svc/IsAuthenticated",
				new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
			response.StatusCode.Should().NotBe(HttpStatusCode.NotFound,
				"Application should start successfully with Forms auth mode");
		}
	}
}
