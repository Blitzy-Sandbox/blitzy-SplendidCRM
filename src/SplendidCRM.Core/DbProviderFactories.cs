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
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	/// <summary>
	/// Provider-agnostic database factory that manages DB connections and commands.
	/// Migrated from SplendidCRM/_code/DbProviderFactories.cs for .NET 10 ASP.NET Core.
	/// Replaces Application["SplendidProvider"] and ConfigurationManager references.
	/// </summary>
	public class DbProviderFactories
	{
		private readonly IConfiguration _configuration;
		private readonly IMemoryCache   _memoryCache  ;

		public DbProviderFactories(IConfiguration configuration, IMemoryCache memoryCache)
		{
			_configuration = configuration;
			_memoryCache   = memoryCache  ;
		}

		/// <summary>
		/// Gets the configured connection string for SplendidCRM.
		/// </summary>
		public string ConnectionString
		{
			get
			{
				return _configuration.GetConnectionString("SplendidCRM") ?? string.Empty;
			}
		}

		/// <summary>
		/// Gets the configured database provider name (defaults to Microsoft.Data.SqlClient).
		/// </summary>
		public string ProviderName
		{
			get
			{
				string sProvider = _configuration["SplendidCRM:SplendidProvider"];
				if (Sql.IsEmptyString(sProvider))
					sProvider = "Microsoft.Data.SqlClient";
				return sProvider;
			}
		}

		/// <summary>
		/// Creates and returns an open database connection using the configured connection string.
		/// </summary>
		public IDbConnection CreateConnection()
		{
			string sConnectionString = ConnectionString;
			SqlConnection con = new SqlConnection(sConnectionString);
			return con;
		}

		/// <summary>
		/// Creates a new IDbCommand for the provided connection.
		/// </summary>
		public IDbCommand CreateCommand(IDbConnection con)
		{
			IDbCommand cmd = con.CreateCommand();
			return cmd;
		}

		/// <summary>
		/// Creates a new SqlDataAdapter.
		/// </summary>
		public SqlDataAdapter CreateDataAdapter()
		{
			return new SqlDataAdapter();
		}

		/// <summary>
		/// Creates a new IDbDataParameter.
		/// </summary>
		public IDbDataParameter CreateParameter()
		{
			return new SqlParameter();
		}

		/// <summary>
		/// Creates a new IDbDataParameter with the specified name and type.
		/// </summary>
		public IDbDataParameter CreateParameter(string sName, DbType dbType)
		{
			SqlParameter par = new SqlParameter();
			par.ParameterName = sName;
			par.DbType = dbType;
			return par;
		}

		/// <summary>
		/// Creates a new IDbDataParameter with the specified name, type, and size.
		/// </summary>
		public IDbDataParameter CreateParameter(string sName, DbType dbType, int nSize)
		{
			SqlParameter par = new SqlParameter();
			par.ParameterName = sName;
			par.DbType = dbType;
			par.Size = nSize;
			return par;
		}
	}
}
