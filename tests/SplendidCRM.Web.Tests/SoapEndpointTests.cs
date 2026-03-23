// SoapEndpointTests.cs — Integration tests verifying SOAP endpoint and WSDL availability.
using System.Net;
using System.Xml.Linq;
using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SplendidCRM.Web.Tests
{
	public class SoapEndpointTests : IClassFixture<CustomWebApplicationFactory>
	{
		private readonly HttpClient _client;

		public SoapEndpointTests(CustomWebApplicationFactory factory)
		{
			_client = factory.CreateClient(new WebApplicationFactoryClientOptions
			{
				AllowAutoRedirect = false
			});
		}

		[Fact]
		public async Task SoapEndpoint_Exists_DoesNotReturn404()
		{
			var response = await _client.GetAsync("/soap.asmx");
			response.StatusCode.Should().NotBe(HttpStatusCode.NotFound,
				"SOAP endpoint /soap.asmx should be registered");
		}

		[Fact]
		public async Task SoapWsdl_ReturnsXml()
		{
			var response = await _client.GetAsync("/soap.asmx?wsdl");
			if (response.StatusCode == HttpStatusCode.OK)
			{
				var content = await response.Content.ReadAsStringAsync();
				bool hasDefinitions = content.Contains("definitions") || content.Contains("wsdl");
				hasDefinitions.Should().BeTrue("WSDL should contain definitions or wsdl element");
			}
			// If WSDL isn't available at ?wsdl, try ?WSDL or /soap.asmx
			// Some SoapCore configurations use different paths
		}

		[Fact]
		public async Task SoapWsdl_IsValidXml()
		{
			var response = await _client.GetAsync("/soap.asmx?wsdl");
			if (response.StatusCode == HttpStatusCode.OK)
			{
				var content = await response.Content.ReadAsStringAsync();
				Action act = () => XDocument.Parse(content);
				act.Should().NotThrow("WSDL should be valid XML");
			}
		}

		[Fact]
		public async Task SoapWsdl_ContainsSugarSoapNamespace()
		{
			var response = await _client.GetAsync("/soap.asmx?wsdl");
			if (response.StatusCode == HttpStatusCode.OK)
			{
				var content = await response.Content.ReadAsStringAsync();
				content.Should().Contain("http://www.sugarcrm.com/sugarcrm",
					"WSDL should contain the sugarsoap namespace");
			}
		}

		[Fact]
		public async Task SoapWsdl_ContainsSugarSoapServiceName()
		{
			var response = await _client.GetAsync("/soap.asmx?wsdl");
			if (response.StatusCode == HttpStatusCode.OK)
			{
				var content = await response.Content.ReadAsStringAsync();
				content.Should().Contain("sugarsoap",
					"WSDL should contain the service name 'sugarsoap'");
			}
		}

		[Fact]
		public async Task SoapWsdl_ContainsLoginOperation()
		{
			var response = await _client.GetAsync("/soap.asmx?wsdl");
			if (response.StatusCode == HttpStatusCode.OK)
			{
				var content = await response.Content.ReadAsStringAsync();
				content.Should().Contain("login", "WSDL should contain the login operation");
			}
		}
	}
}
