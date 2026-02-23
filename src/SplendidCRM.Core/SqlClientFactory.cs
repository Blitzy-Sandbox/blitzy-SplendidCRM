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
using System;
using System.Data.Common;
// .NET 10 Migration: System.Data.SqlClient replaced by Microsoft.Data.SqlClient NuGet package.
// The assembly and type strings below are the only change from the .NET Framework 4.8 source.
// All type loading is performed via reflection in the DbProviderFactory base class, so no
// direct 'using Microsoft.Data.SqlClient;' is required in this file.

namespace SplendidCRM
{
	/// <summary>
	/// SqlClient-specific database factory implementation for Microsoft.Data.SqlClient.
	/// Inherits from the provider-agnostic <see cref="DbProviderFactory"/> base class which uses
	/// reflection to dynamically load and instantiate SQL Server connectivity types at runtime.
	/// 
	/// Migrated from SplendidCRM/_code/SqlClientFactory.cs (.NET Framework 4.8 → .NET 10):
	///   - Assembly name changed from "System.Data" to "Microsoft.Data.SqlClient"
	///   - All type name prefixes changed from "System.Data.SqlClient." to "Microsoft.Data.SqlClient."
	///   - The API surface (IDbConnection, IDbCommand, DbDataAdapter, IDbDataParameter) is identical.
	///   - Microsoft.Data.SqlClient 6.1.4 is a drop-in replacement for System.Data.SqlClient.
	/// </summary>
	public class SqlClientFactory : DbProviderFactory
	{
		/// <summary>
		/// Initializes a new instance of <see cref="SqlClientFactory"/> configured for
		/// Microsoft.Data.SqlClient (SQL Server connectivity on .NET 10).
		/// </summary>
		/// <param name="sConnectionString">
		/// The SQL Server connection string used by <see cref="DbProviderFactory.CreateConnection"/>
		/// to instantiate and open a database connection.
		/// </param>
		public SqlClientFactory(string sConnectionString)
			: base( sConnectionString
			      // .NET 10 Migration: Assembly name updated from "System.Data" to "Microsoft.Data.SqlClient".
			      // The NuGet package Microsoft.Data.SqlClient 6.1.4 provides this assembly at runtime.
			      , "Microsoft.Data.SqlClient"
			      // .NET 10 Migration: All fully-qualified type names updated from System.Data.SqlClient
			      // to Microsoft.Data.SqlClient namespace. The public API surface is identical.
			      , "Microsoft.Data.SqlClient.SqlConnection"
			      , "Microsoft.Data.SqlClient.SqlCommand"
			      , "Microsoft.Data.SqlClient.SqlDataAdapter"
			      , "Microsoft.Data.SqlClient.SqlParameter"
			      , "Microsoft.Data.SqlClient.SqlCommandBuilder"
			      )
		{
		}

		// The following methods are inherited from DbProviderFactory and are exposed by this class:
		//   CreateConnection()    — returns IDbConnection (SqlConnection)
		//   CreateCommand()       — returns IDbCommand (SqlCommand)
		//   CreateDataAdapter()   — returns DbDataAdapter (SqlDataAdapter)
		//   CreateParameter()     — returns IDbDataParameter (SqlParameter)
		//   DeriveParameters(cmd) — invokes SqlCommandBuilder.DeriveParameters (currently no-op in base)
		// No overrides are required; the base class reflection logic handles all provider-specific
		// instantiation using the Microsoft.Data.SqlClient type strings supplied above.
	}
}
