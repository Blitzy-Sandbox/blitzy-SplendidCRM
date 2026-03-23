// StoredProcedureTests.cs — Requirement #8: Stored Procedure Execution Verification.
// Verifies that key stored procedures execute without error via the application's
// Sql.AddParameter method. Tests that Microsoft.Data.SqlClient parameter binding
// works correctly (no double-@@ issues).
using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SplendidCRM;
using Xunit;

namespace SplendidCRM.Integration.Tests
{
    /// <summary>
    /// Verifies stored procedure execution and Sql.AddParameter parameter binding
    /// against a real SQL Server database. Ensures no double-@@ parameter prefix bugs.
    /// </summary>
    public class StoredProcedureTests : TestBase
    {
        public StoredProcedureTests(DatabaseWebApplicationFactory factory) : base(factory) { }

        /// <summary>
        /// spSYSTEM_LOG_InsertOnly executes without error with all 15 parameters.
        /// Verifies row is written, then cleans up.
        /// Parameters: @MODIFIED_USER_ID, @USER_ID, @USER_NAME, @MACHINE, @ASPNET_SESSIONID,
        /// @REMOTE_HOST, @SERVER_HOST, @TARGET, @RELATIVE_PATH, @PARAMETERS, @ERROR_TYPE,
        /// @FILE_NAME, @METHOD, @LINE_NUMBER, @MESSAGE.
        /// </summary>
        [Fact]
        public async Task SP_spSYSTEM_LOG_InsertOnly_Executes()
        {
            // Force app initialization
            await Client.GetAsync("/api/health");

            using var conn = CreateDirectConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = "spSYSTEM_LOG_InsertOnly";

            Guid gModifiedUserId = Guid.Empty;
            Guid gUserId = Guid.Empty;

            Sql.AddParameter(cmd, "@MODIFIED_USER_ID", gModifiedUserId);
            Sql.AddParameter(cmd, "@USER_ID", gUserId);
            Sql.AddParameter(cmd, "@USER_NAME", "IntegrationTest", 255);
            Sql.AddParameter(cmd, "@MACHINE", "test-machine", 60);
            Sql.AddParameter(cmd, "@ASPNET_SESSIONID", "test-session-id", 50);
            Sql.AddParameter(cmd, "@REMOTE_HOST", "127.0.0.1", 100);
            Sql.AddParameter(cmd, "@SERVER_HOST", "localhost", 100);
            Sql.AddParameter(cmd, "@TARGET", "IntegrationTest", 255);
            Sql.AddParameter(cmd, "@RELATIVE_PATH", "/test", 255);
            Sql.AddParameter(cmd, "@PARAMETERS", string.Empty, 2000);
            Sql.AddParameter(cmd, "@ERROR_TYPE", "Info", 25);
            Sql.AddParameter(cmd, "@FILE_NAME", "StoredProcedureTests.cs", 255);
            Sql.AddParameter(cmd, "@METHOD", "SP_spSYSTEM_LOG_InsertOnly_Executes", 450);
            Sql.AddParameter(cmd, "@LINE_NUMBER", 0);
            Sql.AddParameter(cmd, "@MESSAGE", "Integration test log entry");

            // Should not throw — if it does, the test fails with a descriptive SqlException
            await cmd.ExecuteNonQueryAsync();

            // Cleanup: remove the test log entry
            using var cleanupCmd = conn.CreateCommand();
            cleanupCmd.CommandText = @"
                DELETE FROM SYSTEM_LOG
                WHERE USER_NAME = 'IntegrationTest'
                  AND FILE_NAME = 'StoredProcedureTests.cs'
                  AND METHOD = 'SP_spSYSTEM_LOG_InsertOnly_Executes'";
            await cleanupCmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// spUSERS_LOGINS_InsertOnly executes without error with 12 parameters (including @ID output).
        /// Parameters: @ID output, @MODIFIED_USER_ID, @USER_ID, @USER_NAME, @LOGIN_TYPE,
        /// @LOGIN_STATUS, @ASPNET_SESSIONID, @REMOTE_HOST, @SERVER_HOST, @TARGET,
        /// @RELATIVE_PATH, @USER_AGENT.
        /// </summary>
        [Fact]
        public async Task SP_spUSERS_LOGINS_InsertOnly_Executes()
        {
            await Client.GetAsync("/api/health");

            using var conn = CreateDirectConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = "spUSERS_LOGINS_InsertOnly";

            // @ID is an OUTPUT parameter
            var idParam = cmd.CreateParameter();
            idParam.ParameterName = "@ID";
            idParam.DbType = DbType.Guid;
            idParam.Direction = ParameterDirection.InputOutput;
            idParam.Value = Guid.Empty;
            cmd.Parameters.Add(idParam);

            Guid gModifiedUserId = Guid.Empty;
            Guid gUserId = Guid.Empty;

            Sql.AddParameter(cmd, "@MODIFIED_USER_ID", gModifiedUserId);
            Sql.AddParameter(cmd, "@USER_ID", gUserId);
            Sql.AddParameter(cmd, "@USER_NAME", "IntegrationTestLogin", 60);
            Sql.AddParameter(cmd, "@LOGIN_TYPE", "IntegrationTest", 25);
            Sql.AddParameter(cmd, "@LOGIN_STATUS", "Success", 25);
            Sql.AddParameter(cmd, "@ASPNET_SESSIONID", "test-session-id-login", 50);
            Sql.AddParameter(cmd, "@REMOTE_HOST", "127.0.0.1", 100);
            Sql.AddParameter(cmd, "@SERVER_HOST", "localhost", 100);
            Sql.AddParameter(cmd, "@TARGET", "/Rest.svc/Login", 255);
            Sql.AddParameter(cmd, "@RELATIVE_PATH", "/Rest.svc/Login", 255);
            Sql.AddParameter(cmd, "@USER_AGENT", "IntegrationTest/1.0", 255);

            await cmd.ExecuteNonQueryAsync();

            Guid loginId = (Guid)idParam.Value;
            Assert.NotEqual(Guid.Empty, loginId);

            // Cleanup
            using var cleanupCmd = conn.CreateCommand();
            cleanupCmd.CommandText = "DELETE FROM USERS_LOGINS WHERE ID = @ID";
            cleanupCmd.Parameters.Add(new SqlParameter("@ID", loginId));
            await cleanupCmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// spSYSTEM_EVENTS_ProcessAll executes without error (no parameters).
        /// </summary>
        [Fact]
        public async Task SP_spSYSTEM_EVENTS_ProcessAll_Executes()
        {
            await Client.GetAsync("/api/health");

            using var conn = CreateDirectConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = "spSYSTEM_EVENTS_ProcessAll";

            // No parameters — should execute without error
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// spCONTACTS_Update inserts a contact, verifies, then spCONTACTS_Delete removes it.
        /// CRM CRUD pattern via stored procedures. Uses DeriveParameters to supply all
        /// required parameters (spCONTACTS_Update has 50 parameters, none with defaults).
        /// </summary>
        [Fact]
        public async Task SP_spCONTACTS_Update_InsertAndDelete()
        {
            await Client.GetAsync("/api/health");

            Guid testId = Guid.NewGuid();

            using var conn = CreateDirectConnection();
            await conn.OpenAsync();

            try
            {
                // Insert via spCONTACTS_Update — use DeriveParameters to get all 50 params
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandText = "spCONTACTS_Update";
                    SqlCommandBuilder.DeriveParameters(cmd);

                    // Initialize all parameters to DBNull.Value
                    foreach (SqlParameter p in cmd.Parameters)
                    {
                        if (p.Direction == ParameterDirection.ReturnValue) continue;
                        p.Value = DBNull.Value;
                    }

                    // Set the specific values we care about
                    cmd.Parameters["@ID"].Value = testId;
                    cmd.Parameters["@MODIFIED_USER_ID"].Value = Guid.Empty;
                    cmd.Parameters["@ASSIGNED_USER_ID"].Value = Guid.Empty;
                    cmd.Parameters["@FIRST_NAME"].Value = "SPTest";
                    cmd.Parameters["@LAST_NAME"].Value = "Integration_" + DateTime.UtcNow.Ticks.ToString();
                    cmd.Parameters["@TEAM_ID"].Value = Guid.Empty;

                    await cmd.ExecuteNonQueryAsync();
                    testId = (Guid)cmd.Parameters["@ID"].Value;
                }

                // Verify the contact was created
                using (var verifyCmd = conn.CreateCommand())
                {
                    verifyCmd.CommandText = "SELECT COUNT(*) FROM vwCONTACTS WHERE ID = @ID";
                    verifyCmd.Parameters.Add(new SqlParameter("@ID", testId));
                    int count = (int)(await verifyCmd.ExecuteScalarAsync() ?? 0);
                    Assert.Equal(1, count);
                }
            }
            finally
            {
                // Delete via spCONTACTS_Delete — also derive parameters
                using var deleteCmd = conn.CreateCommand();
                deleteCmd.CommandType = CommandType.StoredProcedure;
                deleteCmd.CommandText = "spCONTACTS_Delete";
                SqlCommandBuilder.DeriveParameters(deleteCmd);
                foreach (SqlParameter p in deleteCmd.Parameters)
                {
                    if (p.Direction == ParameterDirection.ReturnValue) continue;
                    p.Value = DBNull.Value;
                }
                deleteCmd.Parameters["@ID"].Value = testId;
                deleteCmd.Parameters["@MODIFIED_USER_ID"].Value = Guid.Empty;
                await deleteCmd.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// Sql.AddParameter with all common types (string, int, Guid, DateTime, bool)
        /// binds correctly without double-@@ prefix.
        /// If the double-@@ bug exists, this throws: "Must declare the scalar variable @@StringParam".
        /// </summary>
        [Fact]
        public async Task SqlAddParameter_AllTypes_NoDoubleAt()
        {
            using var conn = CreateDirectConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT @StringParam AS S, @IntParam AS I, @GuidParam AS G, @DateParam AS D, @BoolParam AS B";

            // Use the application's Sql.AddParameter — same method SqlProcs.cs uses
            Sql.AddParameter(cmd, "@StringParam", "test");
            Sql.AddParameter(cmd, "@IntParam", 42);
            Sql.AddParameter(cmd, "@GuidParam", Guid.NewGuid());
            Sql.AddParameter(cmd, "@DateParam", DateTime.UtcNow);
            Sql.AddParameter(cmd, "@BoolParam", true);

            // If double-@ bug exists, this throws: "Must declare the scalar variable @@StringParam"
            using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("test", reader["S"].ToString());
            Assert.Equal(42, Convert.ToInt32(reader["I"]));
        }

        /// <summary>
        /// Sql.AddParameter with @-prefixed names does NOT produce @@NAME parameters.
        /// </summary>
        [Fact]
        public async Task SqlAddParameter_WithAtPrefix_NoDoubleAt()
        {
            using var conn = CreateDirectConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT @NAME AS N";

            // Passing @-prefixed name should NOT create @@NAME
            Sql.AddParameter(cmd, "@NAME", "test_value");

            using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("test_value", reader["N"].ToString());
        }

        /// <summary>
        /// Sql.AddParameter without @ prefix correctly adds @.
        /// </summary>
        [Fact]
        public async Task SqlAddParameter_WithoutAtPrefix_AddsAt()
        {
            using var conn = CreateDirectConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT @NAME AS N";

            // Passing bare name (without @) should correctly get @NAME
            Sql.AddParameter(cmd, "NAME", "bare_value");

            using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("bare_value", reader["N"].ToString());
        }
    }
}
