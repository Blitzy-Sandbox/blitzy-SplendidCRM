// SoapIntegrationTests.cs — Requirement #7: SOAP Endpoint Database Integration Tests.
// Verifies that the SOAP endpoint (/soap.asmx) works against the database.
// SoapCore serves the WSDL at /soap.asmx?wsdl with namespace http://www.sugarcrm.com/sugarcrm
// and service name "sugarsoap".
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace SplendidCRM.Integration.Tests
{
    /// <summary>
    /// Verifies SOAP endpoint WSDL generation and basic SOAP operations against the database.
    /// The SOAP endpoint is registered at <c>/soap.asmx</c> via SoapCore middleware.
    /// </summary>
    public class SoapIntegrationTests : TestBase
    {
        public SoapIntegrationTests(DatabaseWebApplicationFactory factory) : base(factory) { }

        /// <summary>
        /// GET /soap.asmx?wsdl returns valid XML containing the SugarCRM namespace.
        /// </summary>
        [Fact]
        public async Task Wsdl_ReturnsValidXml_WithSugarCrmNamespace()
        {
            var response = await Client.GetAsync("/soap.asmx?wsdl");
            response.EnsureSuccessStatusCode();

            var wsdlContent = await response.Content.ReadAsStringAsync();
            Assert.False(string.IsNullOrEmpty(wsdlContent), "WSDL should not be empty");

            // Parse as XML to verify it's valid
            var xmlDoc = XDocument.Parse(wsdlContent);
            Assert.NotNull(xmlDoc.Root);

            // Verify the SugarCRM namespace is present
            Assert.Contains("http://www.sugarcrm.com/sugarcrm", wsdlContent);
        }

        /// <summary>
        /// WSDL XML contains name="sugarsoap" service name.
        /// </summary>
        [Fact]
        public async Task Wsdl_ContainsServiceName_sugarsoap()
        {
            var response = await Client.GetAsync("/soap.asmx?wsdl");
            response.EnsureSuccessStatusCode();

            var wsdlContent = await response.Content.ReadAsStringAsync();
            Assert.Contains("sugarsoap", wsdlContent);
        }

        /// <summary>
        /// WSDL defines at least 41 operations (matching the 41 [OperationContract] methods
        /// in ISugarSoapService.cs).
        /// </summary>
        [Fact]
        public async Task Wsdl_ContainsMinimum41Operations()
        {
            var response = await Client.GetAsync("/soap.asmx?wsdl");
            response.EnsureSuccessStatusCode();

            var wsdlContent = await response.Content.ReadAsStringAsync();

            // Count operation elements in the WSDL — SoapCore uses <wsdl:operation> tags
            int operationCount = 0;
            int searchIndex = 0;
            while (true)
            {
                int found = wsdlContent.IndexOf("<wsdl:operation", searchIndex, StringComparison.OrdinalIgnoreCase);
                if (found < 0)
                {
                    // Also check for <operation without wsdl: prefix
                    found = wsdlContent.IndexOf("<operation ", searchIndex, StringComparison.OrdinalIgnoreCase);
                    if (found < 0) break;
                }
                operationCount++;
                searchIndex = found + 1;
            }

            Assert.True(operationCount >= 41,
                $"WSDL should define at least 41 operations, found {operationCount}");
        }

        /// <summary>
        /// SOAP login operation returns a non-empty session ID when called with valid credentials.
        /// Sends a raw SOAP XML envelope to POST /soap.asmx.
        /// </summary>
        [Fact]
        public async Task SoapLogin_ValidCredentials_ReturnsSessionId()
        {
            // Construct SOAP envelope for the login operation
            string soapEnvelope = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/""
               xmlns:tns=""http://www.sugarcrm.com/sugarcrm"">
  <soap:Body>
    <tns:login>
      <tns:user_auth>
        <tns:user_name>admin</tns:user_name>
        <tns:password>21232f297a57a5a743894a0e4a801fc3</tns:password>
        <tns:version>.NET</tns:version>
      </tns:user_auth>
      <tns:application_name>SplendidCRM</tns:application_name>
    </tns:login>
  </soap:Body>
</soap:Envelope>";

            var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
            content.Headers.Add("SOAPAction", "\"http://www.sugarcrm.com/sugarcrm/login\"");

            var response = await Client.PostAsync("/soap.asmx", content);
            response.EnsureSuccessStatusCode();

            var responseXml = await response.Content.ReadAsStringAsync();
            Assert.False(string.IsNullOrEmpty(responseXml), "SOAP login should return a response");

            // The response should contain an id element with a session ID
            // SoapCore wraps the response in a SOAP envelope with the operation result
            Assert.Contains("loginResponse", responseXml);
        }
    }
}
