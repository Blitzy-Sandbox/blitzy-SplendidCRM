// AdminRestControllerTests.cs — Integration tests verifying Admin REST route registration.
using System.Net;
using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SplendidCRM.Web.Tests
{
	public class AdminRestControllerTests : IClassFixture<CustomWebApplicationFactory>
	{
		private readonly HttpClient _client;

		public AdminRestControllerTests(CustomWebApplicationFactory factory)
		{
			_client = factory.CreateClient(new WebApplicationFactoryClientOptions
			{
				AllowAutoRedirect = false
			});
		}

		[Theory]
		[InlineData("/Administration/Rest.svc/GetAdminLayoutModules")]
		[InlineData("/Administration/Rest.svc/GetAdminMenu")]
		public async Task AdminRestRoute_Exists_DoesNotReturn404(string route)
		{
			var response = await _client.GetAsync(route);
			response.StatusCode.Should().NotBe(HttpStatusCode.NotFound,
				$"Admin route {route} should be registered and not return 404");
		}

		[Theory]
		[InlineData("/Administration/Rest.svc/UpdateAdminLayout")]
		[InlineData("/Administration/Rest.svc/PostAdminTable")]
		[InlineData("/Administration/Rest.svc/BuildModuleArchive")]
		public async Task AdminPostRoute_Exists_DoesNotReturn404(string route)
		{
			var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
			var response = await _client.PostAsync(route, content);
			response.StatusCode.Should().NotBe(HttpStatusCode.NotFound,
				$"Admin route {route} should be registered and not return 404");
		}

		[Fact]
		public async Task AdminRoutes_UseAdministrationRestSvcPrefix()
		{
			var response = await _client.GetAsync("/Administration/Rest.svc/GetAdminLayoutModules");
			response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
		}
	}
}
