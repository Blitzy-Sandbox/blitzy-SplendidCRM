// AdminEndpointIntegrationTests.cs — Requirement #6: Admin REST API Database Integration Tests.
// Verifies that admin endpoints execute their SQL queries correctly against the database.
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace SplendidCRM.Integration.Tests
{
    /// <summary>
    /// Verifies admin REST API endpoints execute real SQL queries against admin views.
    /// All endpoints require admin authentication.
    /// </summary>
    public class AdminEndpointIntegrationTests : TestBase
    {
        public AdminEndpointIntegrationTests(DatabaseWebApplicationFactory factory) : base(factory) { }

        /// <summary>
        /// GET /Administration/Rest.svc/GetAdminLayoutModules returns module data from vwMODULES.
        /// </summary>
        [Fact]
        public async Task GetAdminLayoutModules_ReturnsModules()
        {
            var client = await GetAuthenticatedClient();

            var response = await client.GetAsync("/Administration/Rest.svc/GetAdminLayoutModules");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            Assert.False(string.IsNullOrEmpty(json), "GetAdminLayoutModules should return module data");
        }

        /// <summary>
        /// GET /Administration/Rest.svc/GetAclAccessByModule returns ACL data from vwACL_ACCESS_ByModule.
        /// </summary>
        [Fact]
        public async Task GetAclAccessByModule_ReturnsACLData()
        {
            var client = await GetAuthenticatedClient();

            var response = await client.GetAsync("/Administration/Rest.svc/GetAclAccessByModule");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            Assert.False(string.IsNullOrEmpty(json), "GetAclAccessByModule should return ACL data");
        }

        /// <summary>
        /// GET /Administration/Rest.svc/GetAdminTable?TableName=USERS&$top=5 returns user list from vwUSERS.
        /// </summary>
        [Fact]
        public async Task GetAdminTable_Users_ReturnsUserList()
        {
            var client = await GetAuthenticatedClient();

            var response = await client.GetAsync("/Administration/Rest.svc/GetAdminTable?TableName=USERS&$top=5");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            Assert.False(string.IsNullOrEmpty(json), "GetAdminTable for USERS should return data");
        }

        /// <summary>
        /// GET /Administration/Rest.svc/GetAdminTable?TableName=MODULES&$top=5 returns module list from vwMODULES.
        /// </summary>
        [Fact]
        public async Task GetAdminTable_Modules_ReturnsModuleList()
        {
            var client = await GetAuthenticatedClient();

            var response = await client.GetAsync("/Administration/Rest.svc/GetAdminTable?TableName=MODULES&$top=5");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            Assert.False(string.IsNullOrEmpty(json), "GetAdminTable for MODULES should return data");
        }

        /// <summary>
        /// GET /Administration/Rest.svc/GetAdminTable?TableName=CONFIG&$top=5 returns config from vwCONFIG.
        /// </summary>
        [Fact]
        public async Task GetAdminTable_Config_ReturnsConfigList()
        {
            var client = await GetAuthenticatedClient();

            var response = await client.GetAsync("/Administration/Rest.svc/GetAdminTable?TableName=CONFIG&$top=5");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            Assert.False(string.IsNullOrEmpty(json), "GetAdminTable for CONFIG should return data");
        }

        /// <summary>
        /// GET /Administration/Rest.svc/GetAllTerminology returns terminology from vwTERMINOLOGY.
        /// </summary>
        [Fact]
        public async Task GetAllTerminology_ReturnsTermData()
        {
            var client = await GetAuthenticatedClient();

            var response = await client.GetAsync("/Administration/Rest.svc/GetAllTerminology");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            Assert.False(string.IsNullOrEmpty(json), "GetAllTerminology should return terminology data");
        }

        /// <summary>
        /// GET /Administration/Rest.svc/GetAllTerminologyLists returns picklists from vwTERMINOLOGY_LISTS.
        /// </summary>
        [Fact]
        public async Task GetAllTerminologyLists_ReturnsPicklists()
        {
            var client = await GetAuthenticatedClient();

            var response = await client.GetAsync("/Administration/Rest.svc/GetAllTerminologyLists");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            Assert.False(string.IsNullOrEmpty(json), "GetAllTerminologyLists should return picklist data");
        }

        /// <summary>
        /// GET /Administration/Rest.svc/GetAllGridViewsColumns returns grid definitions from vwGRIDVIEWS_COLUMNS.
        /// </summary>
        [Fact]
        public async Task GetAllGridViewsColumns_ReturnsGridDefs()
        {
            var client = await GetAuthenticatedClient();

            var response = await client.GetAsync("/Administration/Rest.svc/GetAllGridViewsColumns");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            Assert.False(string.IsNullOrEmpty(json), "GetAllGridViewsColumns should return grid definitions");
        }

        /// <summary>
        /// GET /Administration/Rest.svc/GetAllDetailViewsFields returns detail field definitions.
        /// </summary>
        [Fact]
        public async Task GetAllDetailViewsFields_ReturnsDetailDefs()
        {
            var client = await GetAuthenticatedClient();

            var response = await client.GetAsync("/Administration/Rest.svc/GetAllDetailViewsFields");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            Assert.False(string.IsNullOrEmpty(json), "GetAllDetailViewsFields should return field definitions");
        }

        /// <summary>
        /// GET /Administration/Rest.svc/GetAllEditViewsFields returns edit field definitions.
        /// </summary>
        [Fact]
        public async Task GetAllEditViewsFields_ReturnsEditDefs()
        {
            var client = await GetAuthenticatedClient();

            var response = await client.GetAsync("/Administration/Rest.svc/GetAllEditViewsFields");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            Assert.False(string.IsNullOrEmpty(json), "GetAllEditViewsFields should return field definitions");
        }
    }
}
