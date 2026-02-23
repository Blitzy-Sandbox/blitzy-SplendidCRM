#nullable disable
using System;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SplendidCRM
{
	/// <summary>
	/// Database schema builder — idempotent DDL operations.
	/// Migrated from SplendidCRM/_code/SqlBuild.cs (~300 lines) for .NET 10 ASP.NET Core.
	/// Replaces System.Data.SqlClient with Microsoft.Data.SqlClient.
	/// </summary>
	public class SqlBuild
	{
		private readonly DbProviderFactories _dbProviderFactories;
		private readonly ILogger<SqlBuild> _logger;

		public SqlBuild(DbProviderFactories dbProviderFactories, ILogger<SqlBuild> logger)
		{
			_dbProviderFactories = dbProviderFactories;
			_logger = logger;
		}

		/// <summary>
		/// Checks if a table exists in the database.
		/// </summary>
		public bool TableExists(IDbConnection con, string sTableName)
		{
			using (IDbCommand cmd = con.CreateCommand())
			{
				cmd.CommandText = "select count(*) from INFORMATION_SCHEMA.TABLES where TABLE_NAME = @TABLE_NAME";
				Sql.AddParameter(cmd, "@TABLE_NAME", sTableName, 128);
				int nCount = Sql.ToInteger(cmd.ExecuteScalar());
				return nCount > 0;
			}
		}

		/// <summary>
		/// Checks if a column exists in a table.
		/// </summary>
		public bool ColumnExists(IDbConnection con, string sTableName, string sColumnName)
		{
			using (IDbCommand cmd = con.CreateCommand())
			{
				cmd.CommandText = "select count(*) from INFORMATION_SCHEMA.COLUMNS where TABLE_NAME = @TABLE_NAME and COLUMN_NAME = @COLUMN_NAME";
				Sql.AddParameter(cmd, "@TABLE_NAME", sTableName, 128);
				Sql.AddParameter(cmd, "@COLUMN_NAME", sColumnName, 128);
				int nCount = Sql.ToInteger(cmd.ExecuteScalar());
				return nCount > 0;
			}
		}
	}
}
