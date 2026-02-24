/**********************************************************************************************************************
 * SplendidCRM is a Customer Relationship Management program created by SplendidCRM Software, Inc. 
 * Copyright (C) 2005-2022 SplendidCRM Software, Inc. All rights reserved.
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the 
 * GNU Affero General Public License as published by the Free Software Foundation, either version 3 
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; 
 * without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. 
 * See the GNU Affero General Public License for more details.
 * 
 * You should have received a copy of the GNU Affero General Public License along with this program. 
 * If not, see <http://www.gnu.org/licenses/>. 
 * 
 * You can contact SplendidCRM Software, Inc. at email address support@splendidcrm.com. 
 * 
 * In accordance with Section 7(b) of the GNU Affero General Public License version 3, 
 * the Appropriate Legal Notices must display the following words on all interactive user interfaces: 
 * "Copyright (C) 2005-2011 SplendidCRM Software, Inc. All rights reserved."
 *********************************************************************************************************************/
// .NET 10 Migration: SplendidCRM/_code/SqlProcsDynamicFactory.cs → src/SplendidCRM.Core/SqlProcsDynamicFactory.cs
// Changes applied:
//   - REMOVED: using System.Web; (HttpContext, HttpApplicationState, HttpContext.Current)
//   - ADDED:   using Microsoft.Extensions.Caching.Memory; (IMemoryCache replacing Application[])
//   - REPLACED: HttpContext.Current.Application → static ambient _dynamicFactoryCache (IMemoryCache)
//   - REPLACED: HttpContext.Current.Application == null check → _dynamicFactoryCache == null check
//   - REPLACED: DbProviderFactories.GetFactory(Application) → _dynamicFactoryDbf.GetFactory(_dynamicFactoryCache)
//   - REPLACED: Application["SqlProcs." + sProcedureName] as DataTable
//              → _dynamicFactoryCache.TryGetValue("SqlProcs." + sProcedureName, out DataTable dt)
//   - REPLACED: Application["SqlProcs." + sProcedureName] = dt
//              → _dynamicFactoryCache.Set("SqlProcs." + sProcedureName, dt)
//   - ADDED:   static ambient fields _dynamicFactoryCache + _dynamicFactoryDbf with SetDynamicFactoryAmbient()
//              for DI-compatible static method usage — same pattern as Sql.SetAmbient()
//   - PRESERVED: namespace SplendidCRM, partial class SqlProcs, DynamicFactory public static signature
//   - PRESERVED: All business logic — vwSqlProcedures existence check, vwSqlColumns metadata query,
//              parameter direction setting, CommandType.StoredProcedure, ControlChars.CrLf formatting
//   - Minimal change clause: Only changes necessary for .NET Framework 4.8 → .NET 10 ASP.NET Core
#nullable disable
using System;
using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	/// <summary>
	/// Dynamic stored procedure factory — partial class extension to SqlProcs.
	/// Migrated from SplendidCRM/_code/SqlProcsDynamicFactory.cs for .NET 10 ASP.NET Core.
	/// 
	/// BEFORE (.NET Framework 4.8):
	///   Static method on partial class SqlProcs.
	///   Used HttpContext.Current.Application[] for caching stored procedure column metadata.
	///   Used static DbProviderFactories.GetFactory(Application) to create a separate DB connection.
	/// 
	/// AFTER (.NET 10 ASP.NET Core):
	///   Same static method on partial class SqlProcs.
	///   Uses static ambient IMemoryCache (_dynamicFactoryCache) replacing Application[].
	///   Uses static ambient DbProviderFactories (_dynamicFactoryDbf) replacing static GetFactory(Application).
	///   SetDynamicFactoryAmbient() must be called at application startup by the DI host
	///   (same pattern as Sql.SetAmbient()).
	/// </summary>
	public partial class SqlProcs
	{
		// =====================================================================================
		// .NET 10 Migration: Static ambient fields replacing HttpContext.Current.Application
		// and static DbProviderFactories.GetFactory(Application) access patterns.
		//
		// BEFORE (.NET Framework 4.8):
		//   HttpApplicationState Application = HttpContext.Current.Application;
		//   DataTable dt = Application["SqlProcs.{name}"] as DataTable;
		//   DbProviderFactory dbf = DbProviderFactories.GetFactory(Application);
		//
		// AFTER (.NET 10 ASP.NET Core):
		//   IMemoryCache _dynamicFactoryCache (injected via SetDynamicFactoryAmbient)
		//   DbProviderFactories _dynamicFactoryDbf (injected via SetDynamicFactoryAmbient)
		//
		// Thread-safety: IMemoryCache and DbProviderFactories are singleton DI services,
		// both thread-safe for concurrent read/write access.
		// =====================================================================================

		/// <summary>
		/// Static ambient IMemoryCache replacing HttpApplicationState (Application["key"]).
		/// Cache key pattern: "SqlProcs.{procedureName}" → DataTable of column metadata.
		/// Set via <see cref="SetDynamicFactoryAmbient"/> at application startup.
		/// </summary>
		private static IMemoryCache _dynamicFactoryCache;

		/// <summary>
		/// Static ambient DbProviderFactories replacing static DbProviderFactories.GetFactory(Application).
		/// Used to create a separate IDbConnection for querying vwSqlProcedures and vwSqlColumns.
		/// Set via <see cref="SetDynamicFactoryAmbient"/> at application startup.
		/// </summary>
		private static DbProviderFactories _dynamicFactoryDbf;

		/// <summary>
		/// Register static ambient dependencies for the DynamicFactory method.
		/// 
		/// MIGRATION NOTE: This method replaces the HttpContext.Current.Application static access
		/// pattern used throughout the .NET Framework 4.8 version. It must be called once at
		/// application startup (e.g., from Program.cs or a startup service that receives these
		/// via constructor injection) before any call to DynamicFactory().
		/// 
		/// BEFORE (.NET Framework 4.8):
		///   HttpApplicationState Application = HttpContext.Current.Application;
		///   (implicitly available via HttpContext.Current in every ASP.NET request)
		/// 
		/// AFTER (.NET 10 ASP.NET Core):
		///   SqlProcs.SetDynamicFactoryAmbient(memoryCache, dbProviderFactories);
		///   (called once from DI host startup, before first request is handled)
		/// </summary>
		/// <param name="memoryCache">
		/// IMemoryCache instance replacing Application[] state for caching DataTable column metadata
		/// with keys in the pattern "SqlProcs.{procedureName}".
		/// </param>
		/// <param name="dbProviderFactories">
		/// DbProviderFactories instance replacing static DbProviderFactories.GetFactory(Application)
		/// for creating a separate database connection to query stored procedure metadata views.
		/// </param>
		public static void SetDynamicFactoryAmbient(IMemoryCache memoryCache, DbProviderFactories dbProviderFactories)
		{
			_dynamicFactoryCache = memoryCache        ;
			_dynamicFactoryDbf   = dbProviderFactories;
		}

		// 11/26/2021 Paul.  In order to support dynamically created modules in the React client, we need to load the procedures dynamically. 
		/// <summary>
		/// Dynamically constructs an IDbCommand for the named stored procedure by querying
		/// SQL Server metadata views (vwSqlProcedures, vwSqlColumns) and building typed parameters.
		/// Column metadata is cached in IMemoryCache to avoid repeated database lookups.
		/// 
		/// MIGRATION NOTE:
		///   BEFORE: Application["SqlProcs." + sProcedureName] → IMemoryCache cache key
		///   BEFORE: DbProviderFactories.GetFactory(Application) → _dynamicFactoryDbf.GetFactory(_dynamicFactoryCache)
		///   All business logic preserved exactly. Only dependency injection mechanism changed.
		/// </summary>
		/// <param name="con">
		/// The active database connection on which to build the command.
		/// A separate connection is used internally for metadata queries to avoid transaction interference.
		/// </param>
		/// <param name="sProcedureName">
		/// The name of the stored procedure to build a command for.
		/// Must exist in the vwSqlProcedures metadata view.
		/// </param>
		/// <returns>
		/// A configured IDbCommand with CommandType.StoredProcedure and all parameters
		/// added with correct types and directions based on vwSqlColumns metadata.
		/// </returns>
		/// <exception cref="Exception">
		/// Thrown if SetDynamicFactoryAmbient() has not been called (null IMemoryCache or DbProviderFactories),
		/// or if the stored procedure does not exist in vwSqlProcedures.
		/// </exception>
		public static IDbCommand DynamicFactory(IDbConnection con, string sProcedureName)
		{
			// .NET 10 Migration: HttpContext.Current.Application == null check
			//   → _dynamicFactoryCache == null check.
			// In ASP.NET Core, IMemoryCache is a singleton DI service. If null, SetDynamicFactoryAmbient()
			// was not called at startup. This preserves the original fail-fast behavior.
			if ( _dynamicFactoryCache == null )
				throw new Exception("SqlProcs.DynamicFactory: IMemoryCache cannot be NULL. Ensure SetDynamicFactoryAmbient() is called at application startup.");
			if ( _dynamicFactoryDbf == null )
				throw new Exception("SqlProcs.DynamicFactory: DbProviderFactories cannot be NULL. Ensure SetDynamicFactoryAmbient() is called at application startup.");

			// 11/26/2021 Paul.  Store the data table of rows instead of the command so that connection does not stay referenced. 
			// .NET 10 Migration: Application["SqlProcs." + sProcedureName] as DataTable
			//   → _dynamicFactoryCache.TryGetValue("SqlProcs." + sProcedureName, out DataTable dt)
			// Cache key pattern is identical to the original Application[] key for cache parity.
			string sCacheKey = "SqlProcs." + sProcedureName;
			if ( !_dynamicFactoryCache.TryGetValue(sCacheKey, out DataTable dt) )
			{
				// .NET 10 Migration: DbProviderFactories.GetFactory(Application)
				//   → _dynamicFactoryDbf.GetFactory(_dynamicFactoryCache)
				// GetFactory(IMemoryCache) is the migrated replacement for GetFactory(HttpApplicationState).
				// It reads provider configuration from IMemoryCache (on hit) or IConfiguration (on miss).
				DbProviderFactory dbf = _dynamicFactoryDbf.GetFactory(_dynamicFactoryCache);
				// 11/26/2021 Paul.  We can't use the same connection as provided as it may already be inside a transaction. 
				using ( IDbConnection con2 = dbf.CreateConnection() )
				{
					con2.Open();
					using ( IDbCommand cmd = con2.CreateCommand() )
					{
						string sSQL;
						sSQL = "select count(*)       " + ControlChars.CrLf
						     + "  from vwSqlProcedures" + ControlChars.CrLf
						     + " where name = @NAME   " + ControlChars.CrLf;
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@NAME", Sql.MetadataName(cmd, Sql.ToString(sProcedureName)));
						int nExists = Sql.ToInteger(cmd.ExecuteScalar());
						if ( nExists == 0 )
						{
							throw new Exception("Unknown stored procedure " + sProcedureName);
						}
					}
					using ( IDbCommand cmd = con2.CreateCommand() )
					{
						string sSQL;
						sSQL = "select *                       " + ControlChars.CrLf
						     + "  from vwSqlColumns            " + ControlChars.CrLf
						     + " where ObjectName = @OBJECTNAME" + ControlChars.CrLf
						     + "   and ObjectType = 'P'        " + ControlChars.CrLf
						     + " order by colid                " + ControlChars.CrLf;
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@OBJECTNAME", Sql.MetadataName(cmd, Sql.ToString(sProcedureName)));
						using ( DbDataAdapter da = dbf.CreateDataAdapter() )
						{
							((IDbDataAdapter)da).SelectCommand = cmd;
							dt = new DataTable();
							da.Fill(dt);
							// .NET 10 Migration: Application["SqlProcs." + sProcedureName] = dt
							//   → _dynamicFactoryCache.Set("SqlProcs." + sProcedureName, dt)
							// IMemoryCache.Set without expiration stores the value until evicted by memory pressure,
							// matching the original Application[] behavior (persistent for the application lifetime).
							_dynamicFactoryCache.Set(sCacheKey, dt);
						}
					}
				}
			}

			IDbCommand cmdDynamicProcedure = null;
			cmdDynamicProcedure = con.CreateCommand();
			cmdDynamicProcedure.CommandType = CommandType.StoredProcedure;
			cmdDynamicProcedure.CommandText = Sql.MetadataName(con, Sql.ToString(sProcedureName));
			for ( int j = 0; j < dt.Rows.Count; j++ )
			{
				DataRow row = dt.Rows[j];
				string sName     = Sql.ToString (row["ColumnName"]);
				string sCsType   = Sql.ToString (row["CsType"    ]);
				int    nLength   = Sql.ToInteger(row["length"    ]);
				bool   bIsOutput = Sql.ToBoolean(row["isoutparam"]);
				string sBareName = sName.Replace("@", "");
				IDbDataParameter par = Sql.CreateParameter(cmdDynamicProcedure, sName, sCsType, nLength);
				if ( bIsOutput )
					par.Direction = ParameterDirection.InputOutput;
			}
			return cmdDynamicProcedure;
		}
	}
}
