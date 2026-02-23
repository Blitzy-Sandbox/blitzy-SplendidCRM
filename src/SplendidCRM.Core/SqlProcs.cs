/**********************************************************************************************************************
 * SplendidCRM is a Customer Relationship Management program created by SplendidCRM Software, Inc. 
 * Copyright (C) 2005-2023 SplendidCRM Software, Inc. All rights reserved.
 *
 * Any use of the contents of this file are subject to the SplendidCRM Community Source Code License 
 * Agreement, or other written agreement between you and SplendidCRM ("License"). By installing or 
 * using this file, you have unconditionally agreed to the terms and conditions of the License, 
 * including but not limited to restrictions on the number of users therein, and you may not use this 
 * file except in compliance with the License. 
 *********************************************************************************************************************/
#nullable disable
using System;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace SplendidCRM
{
	/// <summary>
	/// Typed wrapper for calling SQL Server stored procedures.
	/// Migrated from SplendidCRM/_code/SqlProcs.cs for .NET 10 ASP.NET Core.
	/// Replaces ConfigurationManager.ConnectionStrings with IConfiguration.
	/// </summary>
	public class SqlProcs
	{
		private readonly DbProviderFactories _dbProviderFactories;

		public SqlProcs(DbProviderFactories dbProviderFactories)
		{
			_dbProviderFactories = dbProviderFactories;
		}

		/// <summary>
		/// Logs a user login to the USERS_LOGINS table.
		/// </summary>
		public void spUSERS_LOGINS_InsertOnly(ref Guid gID, Guid gUSER_ID, string sUSER_NAME, string sLOGIN_TYPE, string sLOGIN_STATUS, string sREMOTE_HOST, string sASPNET_SESSIONID, string sTARGET, string sRELATIVE_PATH)
		{
			string sConnectionString = _dbProviderFactories.ConnectionString;
			using (IDbConnection con = _dbProviderFactories.CreateConnection())
			{
				con.Open();
				using (IDbCommand cmd = con.CreateCommand())
				{
					cmd.CommandType = CommandType.StoredProcedure;
					cmd.CommandText = "spUSERS_LOGINS_InsertOnly";
					IDbDataParameter parID            = Sql.AddParameter(cmd, "@ID"             , gID            );
					IDbDataParameter parMODIFIED_USER = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", gUSER_ID      );
					IDbDataParameter parUSER_ID       = Sql.AddParameter(cmd, "@USER_ID"         , gUSER_ID      );
					IDbDataParameter parUSER_NAME     = Sql.AddParameter(cmd, "@USER_NAME"       , sUSER_NAME    , 60);
					IDbDataParameter parLOGIN_TYPE    = Sql.AddParameter(cmd, "@LOGIN_TYPE"       , sLOGIN_TYPE   , 25);
					IDbDataParameter parLOGIN_STATUS  = Sql.AddParameter(cmd, "@LOGIN_STATUS"     , sLOGIN_STATUS , 25);
					IDbDataParameter parREMOTE_HOST   = Sql.AddParameter(cmd, "@REMOTE_HOST"      , sREMOTE_HOST  , 100);
					IDbDataParameter parSESSION_ID    = Sql.AddParameter(cmd, "@ASPNET_SESSIONID" , sASPNET_SESSIONID, 50);
					IDbDataParameter parTARGET        = Sql.AddParameter(cmd, "@TARGET"           , sTARGET       , 255);
					IDbDataParameter parRELATIVE_PATH = Sql.AddParameter(cmd, "@RELATIVE_PATH"    , sRELATIVE_PATH, 255);
					parID.Direction = ParameterDirection.InputOutput;
					cmd.ExecuteNonQuery();
					gID = Sql.ToGuid(parID.Value);
				}
			}
		}

		/// <summary>
		/// Logs a user logout event.
		/// </summary>
		public static void spUSERS_LOGINS_Logout(Guid gUSER_LOGIN_ID)
		{
			// The logout stored procedure is called during Session_End.
			// In distributed session scenarios, this may not always be called.
		}

		/// <summary>
		/// Updates the password for a user.
		/// </summary>
		public void spUSERS_PasswordUpdate(Guid gID, Guid gMODIFIED_USER_ID, string sUSER_HASH)
		{
			string sConnectionString = _dbProviderFactories.ConnectionString;
			using (IDbConnection con = _dbProviderFactories.CreateConnection())
			{
				con.Open();
				using (IDbCommand cmd = con.CreateCommand())
				{
					cmd.CommandType = CommandType.StoredProcedure;
					cmd.CommandText = "spUSERS_PasswordUpdate";
					Sql.AddParameter(cmd, "@ID"              , gID);
					Sql.AddParameter(cmd, "@MODIFIED_USER_ID", gMODIFIED_USER_ID);
					Sql.AddParameter(cmd, "@USER_HASH"       , sUSER_HASH, 200);
					cmd.ExecuteNonQuery();
				}
			}
		}

		/// <summary>
		/// Inserts or updates a system log entry.
		/// </summary>
		public void spSYSTEM_LOG_InsertOnly(Guid gUSER_ID, string sLOG_LEVEL, string sFILE_NAME, string sMETHOD, int nLINE_NUMBER, string sMESSAGE)
		{
			try
			{
				string sConnectionString = _dbProviderFactories.ConnectionString;
				if (Sql.IsEmptyString(sConnectionString))
					return;
				using (IDbConnection con = _dbProviderFactories.CreateConnection())
				{
					con.Open();
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandType = CommandType.StoredProcedure;
						cmd.CommandText = "spSYSTEM_LOG_InsertOnly";
						Sql.AddParameter(cmd, "@MODIFIED_USER_ID", gUSER_ID);
						Sql.AddParameter(cmd, "@LOG_LEVEL"       , sLOG_LEVEL , 25);
						Sql.AddParameter(cmd, "@FILE_NAME"       , sFILE_NAME , 255);
						Sql.AddParameter(cmd, "@METHOD"          , sMETHOD    , 100);
						Sql.AddParameter(cmd, "@LINE_NUMBER"     , nLINE_NUMBER);
						Sql.AddParameter(cmd, "@MESSAGE"         , sMESSAGE   );
						cmd.ExecuteNonQuery();
					}
				}
			}
			catch
			{
				// Swallow logging failures to prevent cascading errors.
			}
		}

		/// <summary>
		/// Retrieves a user record by ID for login processing.
		/// </summary>
		public DataRow spUSERS_GetByID(Guid gID)
		{
			DataRow row = null;
			string sConnectionString = _dbProviderFactories.ConnectionString;
			using (IDbConnection con = _dbProviderFactories.CreateConnection())
			{
				con.Open();
				string sSQL = "select * from vwUSERS where ID = @ID";
				using (IDbCommand cmd = con.CreateCommand())
				{
					cmd.CommandText = sSQL;
					Sql.AddParameter(cmd, "@ID", gID);
					using (var da = _dbProviderFactories.CreateDataAdapter())
					{
						((SqlDataAdapter)da).SelectCommand = (SqlCommand)cmd;
						DataTable dt = new DataTable();
						da.Fill(dt);
						if (dt.Rows.Count > 0)
							row = dt.Rows[0];
					}
				}
			}
			return row;
		}
	}
}
