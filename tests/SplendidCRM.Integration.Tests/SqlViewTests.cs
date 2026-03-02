// SqlViewTests.cs — Requirement #9: SQL View Query Verification.
// Verifies that all critical SQL views referenced by the application exist in the database
// and are queryable. Uses [Theory] with [InlineData] for 24+ views plus column verification tests.
using System.Threading.Tasks;
using Xunit;

namespace SplendidCRM.Integration.Tests
{
    /// <summary>
    /// Verifies all critical SQL views referenced by SplendidCRM application code exist
    /// in the database and are queryable. Views are tested with <c>SELECT TOP 1 *</c>
    /// to confirm the view definition is valid and resolves without <c>SqlException</c>.
    /// </summary>
    public class SqlViewTests : TestBase
    {
        public SqlViewTests(DatabaseWebApplicationFactory factory) : base(factory) { }

        /// <summary>
        /// Parameterized test: verifies each critical view exists and is queryable.
        /// No assertion on row count — just that the query executes without SqlException.
        /// </summary>
        [Theory]
        [InlineData("vwCONFIG")]
        [InlineData("vwMODULES")]
        [InlineData("vwMODULES_AppVars")]
        [InlineData("vwUSERS_Login")]
        [InlineData("vwTERMINOLOGY_Active")]
        [InlineData("vwTERMINOLOGY_ALIASES")]
        [InlineData("vwACL_ACCESS_ByModule")]
        [InlineData("vwACL_ACCESS_ByUser")]
        [InlineData("vwACL_ACCESS_ByRole")]
        [InlineData("vwACL_ROLES_USERS")]
        [InlineData("vwTEAM_MEMBERSHIPS")]
        [InlineData("vwTEAM_MEMBERSHIPS_List")]
        // vwTEAM_SET_MEMBERSHIPS_Security is an Enterprise Edition view — tested conditionally in SecurityFilterIntegrationTests
        //[InlineData("vwTEAM_SET_MEMBERSHIPS_Security")]
        [InlineData("vwTIMEZONES")]
        [InlineData("vwCURRENCIES")]
        [InlineData("vwLANGUAGES")]
        [InlineData("vwSYSTEM_REST_TABLES")]
        [InlineData("vwGRIDVIEWS_COLUMNS")]
        [InlineData("vwDETAILVIEWS_FIELDS")]
        [InlineData("vwEDITVIEWS_FIELDS")]
        [InlineData("vwDYNAMIC_BUTTONS")]
        [InlineData("vwSHORTCUTS")]
        [InlineData("vwSYSTEM_EVENTS")]
        [InlineData("vwFIELD_VALIDATORS")]
        [InlineData("vwACCOUNTS")]
        public async Task View_ExistsAndQueryable(string viewName)
        {
            using var conn = CreateDirectConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT TOP 1 * FROM [{viewName}]";
            using var reader = await cmd.ExecuteReaderAsync();
            // No assertion on rows — just that query executes without SqlException.
            // The test passes if no exception is thrown.
        }

        /// <summary>
        /// vwUSERS_Login has the required columns: ID, USER_NAME, USER_HASH, STATUS, IS_ADMIN.
        /// These columns are read by SplendidInit.LoginUser() during authentication.
        /// </summary>
        [Fact]
        public async Task View_vwUSERS_Login_HasRequiredColumns()
        {
            using var conn = CreateDirectConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT TOP 0 ID, USER_NAME, USER_HASH, STATUS, IS_ADMIN FROM vwUSERS_Login";
            using var reader = await cmd.ExecuteReaderAsync();
            Assert.Equal(5, reader.FieldCount);
        }

        /// <summary>
        /// vwCONFIG has the required columns: NAME, VALUE.
        /// These columns are read by SplendidInit.InitConfig() during app startup.
        /// </summary>
        [Fact]
        public async Task View_vwCONFIG_HasRequiredColumns()
        {
            using var conn = CreateDirectConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT TOP 0 NAME, VALUE FROM vwCONFIG";
            using var reader = await cmd.ExecuteReaderAsync();
            Assert.Equal(2, reader.FieldCount);
        }
    }
}
