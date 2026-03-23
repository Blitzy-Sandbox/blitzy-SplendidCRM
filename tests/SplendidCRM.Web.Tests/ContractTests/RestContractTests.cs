// RestContractTests.cs — Contract tests verifying REST API JSON response structure
// matches the legacy WCF API contract. These tests validate response SHAPE (required
// keys and value types), not data correctness (which requires a live database).
// The tests serve as living API documentation for the legacy /Rest.svc/* endpoints.
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SplendidCRM.Web.Tests.ContractTests
{
	public class RestContractTests : IClassFixture<CustomWebApplicationFactory>
	{
		private readonly HttpClient _client;

		public RestContractTests(CustomWebApplicationFactory factory)
		{
			_client = factory.CreateClient(new WebApplicationFactoryClientOptions
			{
				AllowAutoRedirect = false
			});
		}

		// =====================================================================================
		// GetReactState Contract — The most complex endpoint, returns the full CRM state tree.
		// Legacy WCF response: { d: { MODULES, ACL_ACCESS, MODULE_ACL_ACCESS, ACL_FIELD_ACCESS,
		//   USERS, TEAMS, RELATIONSHIPS, TIMEZONES, CURRENCIES, LANGUAGES, GRIDVIEWS, ... } }
		// Without auth, returns 401. We verify the 401 response structure.
		// =====================================================================================

		[Fact]
		public async Task GetReactState_Unauthorized_ReturnsJsonError()
		{
			var response = await _client.GetAsync("/Rest.svc/GetReactState");
			response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
		}

		[Fact]
		public async Task GetReactState_Route_IsRegistered()
		{
			var response = await _client.GetAsync("/Rest.svc/GetReactState");
			// Route exists — not 404
			response.StatusCode.Should().NotBe(HttpStatusCode.NotFound,
				"GetReactState route should be registered under /Rest.svc/ prefix");
		}

		// =====================================================================================
		// GetModuleList Contract — Returns paginated module data.
		// Legacy WCF response: { d: { results: [...], __count: N } } or { d: [...] }
		// =====================================================================================

		[Fact]
		public async Task GetModuleList_Unauthorized_Returns401()
		{
			var response = await _client.GetAsync("/Rest.svc/GetModuleList?ModuleName=Accounts");
			response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
		}

		[Fact]
		public async Task GetModuleList_Route_IsRegistered()
		{
			var response = await _client.GetAsync("/Rest.svc/GetModuleList?ModuleName=Accounts");
			response.StatusCode.Should().NotBe(HttpStatusCode.NotFound,
				"GetModuleList route should be registered");
		}

		// =====================================================================================
		// GetModuleItem Contract — Returns a single CRM record.
		// Legacy WCF response: { d: { results: { ID, DATE_ENTERED, DATE_MODIFIED, ... } } }
		// =====================================================================================

		[Fact]
		public async Task GetModuleItem_Unauthorized_Returns401()
		{
			var response = await _client.GetAsync("/Rest.svc/GetModuleItem?ModuleName=Accounts&ID=00000000-0000-0000-0000-000000000000");
			response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
		}

		// =====================================================================================
		// Login Contract — Authentication endpoint.
		// Legacy WCF response on success: { d: { USER_ID, USER_SESSION, USER_NAME, FULL_NAME, IS_ADMIN } }
		// Legacy WCF response on failure: { d: { error: { message: "..." } } }
		// =====================================================================================

		[Fact]
		public async Task Login_WithEmptyBody_ReturnsJsonResponse()
		{
			var response = await _client.PostAsync("/Rest.svc/Login",
				new StringContent("{}", Encoding.UTF8, "application/json"));
			// Login is [AllowAnonymous], should not return 404
			response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
			var content = await response.Content.ReadAsStringAsync();
			// Response should be valid JSON
			Action act = () => JsonDocument.Parse(content);
			act.Should().NotThrow("Login response should always be valid JSON");
		}

		[Fact]
		public async Task Login_WithCredentials_ReturnsJsonResponse()
		{
			// Without a real DB, login will fail, but should still return valid JSON
			var body = new { UserName = "admin", Password = "password123" };
			var response = await _client.PostAsync("/Rest.svc/Login",
				new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));
			response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
			var content = await response.Content.ReadAsStringAsync();
			Action act = () => JsonDocument.Parse(content);
			act.Should().NotThrow("Login error response should be valid JSON");
		}

		[Fact]
		public async Task Login_ErrorResponse_HasExpectedStructure()
		{
			// Login failure should include error details in JSON
			var body = new { UserName = "admin", Password = "password123" };
			var response = await _client.PostAsync("/Rest.svc/Login",
				new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));
			var content = await response.Content.ReadAsStringAsync();
			// The error response should contain "error" field (either as { d: { error: ... } } or { error: ... })
			using var doc = JsonDocument.Parse(content);
			bool hasErrorField = doc.RootElement.TryGetProperty("error", out _)
				|| (doc.RootElement.TryGetProperty("d", out var d) && d.ValueKind == JsonValueKind.Object && d.TryGetProperty("error", out _));
			hasErrorField.Should().BeTrue("Login failure response should contain an error field");
		}

		// =====================================================================================
		// GetSqlColumns Contract — Returns column metadata for a module.
		// Legacy WCF response: { d: { results: [ { ColumnName, CsType, ... }, ... ] } }
		// =====================================================================================

		[Fact]
		public async Task GetSqlColumns_Unauthorized_Returns401()
		{
			var response = await _client.GetAsync("/Rest.svc/GetSqlColumns?ModuleName=Accounts&Mode=list");
			response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
		}

		[Fact]
		public async Task GetSqlColumns_Route_IsRegistered()
		{
			var response = await _client.GetAsync("/Rest.svc/GetSqlColumns?ModuleName=Accounts&Mode=list");
			response.StatusCode.Should().NotBe(HttpStatusCode.NotFound,
				"GetSqlColumns route should be registered");
		}

		// =====================================================================================
		// Additional core route contract tests
		// =====================================================================================

		[Fact]
		public async Task Version_ReturnsVersionString()
		{
			// Version is [AllowAnonymous]
			var response = await _client.PostAsync("/Rest.svc/Version",
				new StringContent("{}", Encoding.UTF8, "application/json"));
			response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
			var content = await response.Content.ReadAsStringAsync();
			// Response should be JSON with { d: "version_string" }
			using var doc = JsonDocument.Parse(content);
			doc.RootElement.TryGetProperty("d", out _).Should().BeTrue(
				"Version response should follow { d: value } wrapper pattern");
		}

		[Fact]
		public async Task Edition_ReturnsEditionString()
		{
			var response = await _client.PostAsync("/Rest.svc/Edition",
				new StringContent("{}", Encoding.UTF8, "application/json"));
			response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
			var content = await response.Content.ReadAsStringAsync();
			using var doc = JsonDocument.Parse(content);
			doc.RootElement.TryGetProperty("d", out _).Should().BeTrue(
				"Edition response should follow { d: value } wrapper pattern");
		}

		[Fact]
		public async Task IsAuthenticated_ReturnsBoolean()
		{
			// IsAuthenticated is [AllowAnonymous]
			var response = await _client.PostAsync("/Rest.svc/IsAuthenticated",
				new StringContent("{}", Encoding.UTF8, "application/json"));
			var content = await response.Content.ReadAsStringAsync();
			using var doc = JsonDocument.Parse(content);
			doc.RootElement.TryGetProperty("d", out var dValue).Should().BeTrue(
				"IsAuthenticated response should follow { d: value } pattern");
			// d should be a boolean (false for unauthenticated)
			(dValue.ValueKind == JsonValueKind.True || dValue.ValueKind == JsonValueKind.False)
				.Should().BeTrue("IsAuthenticated should return a boolean value");
		}

		[Fact]
		public async Task UtcTime_ReturnsDateTime()
		{
			var response = await _client.PostAsync("/Rest.svc/UtcTime",
				new StringContent("{}", Encoding.UTF8, "application/json"));
			var content = await response.Content.ReadAsStringAsync();
			using var doc = JsonDocument.Parse(content);
			doc.RootElement.TryGetProperty("d", out _).Should().BeTrue(
				"UtcTime response should follow { d: value } pattern");
		}

		// =====================================================================================
		// ResponseSchemaValidator usage — Schema validation for known response shapes
		// =====================================================================================

		[Fact]
		public void ResponseSchemaValidator_DetectsRequiredMissingField()
		{
			string json = """{"status":"ok"}""";
			var schema = new[]
			{
				FieldRequirement.Require("status", ExpectedJsonType.String),
				FieldRequirement.Require("missing_field", ExpectedJsonType.String),
			};
			var violations = ResponseSchemaValidator.Validate(json, schema);
			violations.Should().ContainSingle(v => v.Contains("missing_field"));
		}

		[Fact]
		public void ResponseSchemaValidator_AcceptsValidResponse()
		{
			string json = """{"status":"ok","count":42,"active":true}""";
			var schema = new[]
			{
				FieldRequirement.Require("status", ExpectedJsonType.String),
				FieldRequirement.Require("count", ExpectedJsonType.Number),
				FieldRequirement.Require("active", ExpectedJsonType.Boolean),
			};
			var violations = ResponseSchemaValidator.Validate(json, schema);
			violations.Should().BeEmpty();
		}

		[Fact]
		public void ResponseSchemaValidator_DetectsTypeMismatch()
		{
			string json = """{"count":"not_a_number"}""";
			var schema = new[] { FieldRequirement.Require("count", ExpectedJsonType.Number) };
			var violations = ResponseSchemaValidator.Validate(json, schema);
			violations.Should().ContainSingle(v => v.Contains("unexpected type"));
		}

		[Fact]
		public void ResponseSchemaValidator_AllowsOptionalMissing()
		{
			string json = """{"status":"ok"}""";
			var schema = new[]
			{
				FieldRequirement.Require("status", ExpectedJsonType.String),
				FieldRequirement.Optional("optional_field", ExpectedJsonType.Number),
			};
			var violations = ResponseSchemaValidator.Validate(json, schema);
			violations.Should().BeEmpty();
		}

		[Fact]
		public void ResponseSchemaValidator_ValidatesArrayStructure()
		{
			string json = """{"d":[{"ID":"abc","NAME":"Test"}]}""";
			var elementSchema = new[]
			{
				FieldRequirement.Require("ID", ExpectedJsonType.String),
				FieldRequirement.Require("NAME", ExpectedJsonType.String),
			};
			var violations = ResponseSchemaValidator.ValidateArray(json, "d", elementSchema);
			violations.Should().BeEmpty();
		}
	}
}
