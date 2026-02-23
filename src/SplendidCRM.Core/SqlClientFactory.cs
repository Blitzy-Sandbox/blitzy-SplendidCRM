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

namespace SplendidCRM
{
	/// <summary>
	/// SqlClient-specific factory implementation for Microsoft.Data.SqlClient.
	/// Migrated from SplendidCRM/_code/SqlClientFactory.cs for .NET 10 ASP.NET Core.
	/// Replaces System.Data.SqlClient with Microsoft.Data.SqlClient.
	/// </summary>
	public class SqlClientFactory
	{
		/// <summary>
		/// Creates a new SqlConnection with the specified connection string.
		/// </summary>
		public static SqlConnection CreateConnection(string sConnectionString)
		{
			return new SqlConnection(sConnectionString);
		}

		/// <summary>
		/// Creates a new SqlCommand.
		/// </summary>
		public static SqlCommand CreateCommand()
		{
			return new SqlCommand();
		}

		/// <summary>
		/// Creates a new SqlDataAdapter.
		/// </summary>
		public static SqlDataAdapter CreateDataAdapter()
		{
			return new SqlDataAdapter();
		}

		/// <summary>
		/// Creates a new SqlParameter.
		/// </summary>
		public static SqlParameter CreateParameter()
		{
			return new SqlParameter();
		}

		/// <summary>
		/// Creates a new SqlParameter with the specified name and value.
		/// </summary>
		public static SqlParameter CreateParameter(string sName, object oValue)
		{
			return new SqlParameter(sName, oValue);
		}
	}
}
