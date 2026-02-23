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
using System.Text;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace SplendidCRM
{
	/// <summary>
	/// SQL helper utilities for parameterized query builders and type-safe conversions.
	/// Migrated from SplendidCRM/_code/Sql.cs (~300 lines) for .NET 10 ASP.NET Core.
	/// Full implementation replacing the minimal stub.
	/// </summary>
	public class Sql
	{
		// =====================================================================================
		// Null/Empty Checking Methods
		// =====================================================================================

		public static bool IsEmptyString(string str)
		{
			if (str == null || str == String.Empty)
				return true;
			return false;
		}

		public static bool IsEmptyString(object obj)
		{
			if (obj == null || obj == DBNull.Value)
				return true;
			if (obj.ToString() == String.Empty)
				return true;
			return false;
		}

		public static bool IsEmptyGuid(Guid g)
		{
			return g == Guid.Empty;
		}

		public static bool IsEmptyGuid(object obj)
		{
			return ToGuid(obj) == Guid.Empty;
		}

		// =====================================================================================
		// Type Conversion Methods — Comprehensive null-safe conversions
		// =====================================================================================

		public static Boolean ToBoolean(Boolean b)
		{
			return b;
		}

		public static Boolean ToBoolean(Int32 n)
		{
			return (n == 0) ? false : true;
		}

		public static Boolean ToBoolean(object obj)
		{
			if (obj == null || obj == DBNull.Value)
				return false;
			if (obj.GetType() == Type.GetType("System.Int32"))
				return (Convert.ToInt32(obj) == 0) ? false : true;
			if (obj.GetType() == Type.GetType("System.Byte"))
				return (Convert.ToByte(obj) == 0) ? false : true;
			if (obj.GetType() == Type.GetType("System.SByte"))
				return (Convert.ToSByte(obj) == 0) ? false : true;
			if (obj.GetType() == Type.GetType("System.Int16"))
				return (Convert.ToInt16(obj) == 0) ? false : true;
			if (obj.GetType() == Type.GetType("System.Decimal"))
				return (Convert.ToDecimal(obj) == 0) ? false : true;
			if (obj.GetType() == Type.GetType("System.String"))
			{
				string s = obj.ToString().ToLower();
				return (s == "true" || s == "on" || s == "1" || s == "y") ? true : false;
			}
			if (obj.GetType() != Type.GetType("System.Boolean"))
				return false;
			bool bValue = false;
			bool.TryParse(obj.ToString(), out bValue);
			return bValue;
		}

		public static Guid ToGuid(object obj)
		{
			if (obj == null || obj == DBNull.Value)
				return Guid.Empty;
			if (obj.GetType() == Type.GetType("System.Guid"))
				return (Guid)obj;
			Guid gValue = Guid.Empty;
			Guid.TryParse(obj.ToString(), out gValue);
			return gValue;
		}

		public static string ToString(object obj)
		{
			if (obj == null || obj == DBNull.Value)
				return String.Empty;
			return obj.ToString();
		}

		public static string ToString(string str)
		{
			if (str == null)
				return String.Empty;
			return str;
		}

		public static Int32 ToInteger(object obj)
		{
			if (obj == null || obj == DBNull.Value)
				return 0;
			Int32 nValue = 0;
			if (obj.GetType() == Type.GetType("System.String"))
			{
				Int32.TryParse(obj.ToString(), out nValue);
				return nValue;
			}
			try
			{
				nValue = Convert.ToInt32(obj);
			}
			catch
			{
			}
			return nValue;
		}

		public static Int64 ToLong(object obj)
		{
			if (obj == null || obj == DBNull.Value)
				return 0;
			Int64 nValue = 0;
			if (obj.GetType() == Type.GetType("System.String"))
			{
				Int64.TryParse(obj.ToString(), out nValue);
				return nValue;
			}
			try
			{
				nValue = Convert.ToInt64(obj);
			}
			catch
			{
			}
			return nValue;
		}

		public static float ToFloat(object obj)
		{
			if (obj == null || obj == DBNull.Value)
				return 0;
			float fValue = 0;
			if (obj.GetType() == Type.GetType("System.String"))
			{
				float.TryParse(obj.ToString(), out fValue);
				return fValue;
			}
			try
			{
				fValue = Convert.ToSingle(obj);
			}
			catch
			{
			}
			return fValue;
		}

		public static double ToDouble(object obj)
		{
			if (obj == null || obj == DBNull.Value)
				return 0;
			double dValue = 0;
			if (obj.GetType() == Type.GetType("System.String"))
			{
				double.TryParse(obj.ToString(), out dValue);
				return dValue;
			}
			try
			{
				dValue = Convert.ToDouble(obj);
			}
			catch
			{
			}
			return dValue;
		}

		public static Decimal ToDecimal(object obj)
		{
			if (obj == null || obj == DBNull.Value)
				return Decimal.Zero;
			Decimal dValue = Decimal.Zero;
			if (obj.GetType() == Type.GetType("System.String"))
			{
				Decimal.TryParse(obj.ToString(), out dValue);
				return dValue;
			}
			try
			{
				dValue = Convert.ToDecimal(obj);
			}
			catch
			{
			}
			return dValue;
		}

		public static DateTime ToDateTime(object obj)
		{
			if (obj == null || obj == DBNull.Value)
				return DateTime.MinValue;
			if (obj is DateTime dt)
				return dt;
			DateTime dtValue = DateTime.MinValue;
			DateTime.TryParse(ToString(obj), out dtValue);
			return dtValue;
		}

		public static byte[] ToBinary(object obj)
		{
			if (obj == null || obj == DBNull.Value)
				return null;
			return obj as byte[];
		}

		public static short ToShort(object obj)
		{
			if (obj == null || obj == DBNull.Value)
				return 0;
			short nValue = 0;
			try
			{
				nValue = Convert.ToInt16(obj);
			}
			catch
			{
			}
			return nValue;
		}

		// =====================================================================================
		// SQL Parameter Helpers — Parameterized query builder utilities
		// =====================================================================================

		/// <summary>
		/// Adds a Guid parameter to a command.
		/// </summary>
		public static IDbDataParameter AddParameter(IDbCommand cmd, string sName, Guid gValue)
		{
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sName;
			par.DbType        = DbType.Guid;
			par.Value         = gValue;
			cmd.Parameters.Add(par);
			return par;
		}

		/// <summary>
		/// Adds a string parameter to a command.
		/// </summary>
		public static IDbDataParameter AddParameter(IDbCommand cmd, string sName, string sValue)
		{
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sName;
			par.DbType        = DbType.String;
			par.Value         = (object)sValue ?? DBNull.Value;
			cmd.Parameters.Add(par);
			return par;
		}

		/// <summary>
		/// Adds a string parameter with a max size to a command.
		/// </summary>
		public static IDbDataParameter AddParameter(IDbCommand cmd, string sName, string sValue, int nSize)
		{
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sName;
			par.DbType        = DbType.String;
			par.Size          = nSize;
			if (sValue != null && sValue.Length > nSize)
				sValue = sValue.Substring(0, nSize);
			par.Value = (object)sValue ?? DBNull.Value;
			cmd.Parameters.Add(par);
			return par;
		}

		/// <summary>
		/// Adds an integer parameter to a command.
		/// </summary>
		public static IDbDataParameter AddParameter(IDbCommand cmd, string sName, Int32 nValue)
		{
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sName;
			par.DbType        = DbType.Int32;
			par.Value         = nValue;
			cmd.Parameters.Add(par);
			return par;
		}

		/// <summary>
		/// Adds a boolean parameter to a command.
		/// </summary>
		public static IDbDataParameter AddParameter(IDbCommand cmd, string sName, bool bValue)
		{
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sName;
			par.DbType        = DbType.Boolean;
			par.Value         = bValue;
			cmd.Parameters.Add(par);
			return par;
		}

		/// <summary>
		/// Adds a DateTime parameter to a command.
		/// </summary>
		public static IDbDataParameter AddParameter(IDbCommand cmd, string sName, DateTime dtValue)
		{
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sName;
			par.DbType        = DbType.DateTime;
			par.Value         = dtValue;
			cmd.Parameters.Add(par);
			return par;
		}

		/// <summary>
		/// Adds a decimal parameter to a command.
		/// </summary>
		public static IDbDataParameter AddParameter(IDbCommand cmd, string sName, Decimal dValue)
		{
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sName;
			par.DbType        = DbType.Decimal;
			par.Value         = dValue;
			cmd.Parameters.Add(par);
			return par;
		}

		/// <summary>
		/// Adds a float parameter to a command.
		/// </summary>
		public static IDbDataParameter AddParameter(IDbCommand cmd, string sName, float fValue)
		{
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sName;
			par.DbType        = DbType.Single;
			par.Value         = fValue;
			cmd.Parameters.Add(par);
			return par;
		}

		/// <summary>
		/// Adds a binary parameter to a command.
		/// </summary>
		public static IDbDataParameter AddParameter(IDbCommand cmd, string sName, byte[] byValue)
		{
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sName;
			par.DbType        = DbType.Binary;
			par.Value         = (object)byValue ?? DBNull.Value;
			cmd.Parameters.Add(par);
			return par;
		}

		// =====================================================================================
		// SQL String Escaping
		// =====================================================================================

		/// <summary>
		/// Escapes single quotes in a string for safe SQL embedding.
		/// </summary>
		public static string EscapeSQL(string str)
		{
			if (str == null)
				return string.Empty;
			return str.Replace("'", "''");
		}

		/// <summary>
		/// Escapes LIKE wildcard characters in a string.
		/// </summary>
		public static string EscapeSQLLike(string str)
		{
			if (str == null)
				return string.Empty;
			str = str.Replace("~", "~0");
			str = str.Replace("%", "~%");
			str = str.Replace("_", "~_");
			str = str.Replace("[", "~[");
			return str;
		}

		/// <summary>
		/// Creates a SQL LIKE clause for filtering.
		/// </summary>
		public static string AppendLikeClause(string sFieldName, string sValue)
		{
			if (Sql.IsEmptyString(sValue))
				return string.Empty;
			return " and " + sFieldName + " like N'%" + EscapeSQLLike(sValue) + "%' escape '~'";
		}

		// =====================================================================================
		// Formatting Helpers
		// =====================================================================================

		/// <summary>
		/// Formats a DateTime for display using the specified format.
		/// </summary>
		public static string FormatDateTime(DateTime dt, string sFormat)
		{
			if (dt == DateTime.MinValue)
				return string.Empty;
			return dt.ToString(sFormat);
		}

		/// <summary>
		/// Formats a DateTime as a SQL string (yyyy-MM-dd HH:mm:ss).
		/// </summary>
		public static string FormatSQL(DateTime dt)
		{
			if (dt == DateTime.MinValue)
				return "null";
			return "'" + dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "'";
		}

		/// <summary>
		/// Formats a Guid as a SQL string.
		/// </summary>
		public static string FormatSQL(Guid g)
		{
			return "'" + g.ToString() + "'";
		}

		/// <summary>
		/// Formats a string as a SQL string, escaping single quotes.
		/// </summary>
		public static string FormatSQL(string s, int nMaxLength)
		{
			if (s == null)
				return "null";
			if (nMaxLength > 0 && s.Length > nMaxLength)
				s = s.Substring(0, nMaxLength);
			return "N'" + EscapeSQL(s) + "'";
		}

		/// <summary>
		/// Creates a filter clause for a Guid value.
		/// </summary>
		public static string AppendGuIdFilter(string sFieldName, Guid gValue)
		{
			if (Sql.IsEmptyGuid(gValue))
				return string.Empty;
			return " and " + sFieldName + " = '" + gValue.ToString() + "'";
		}

		// =====================================================================================
		// Database Provider Detection Methods
		// Migrated from SplendidCRM/_code/Sql.cs for .NET 10 ASP.NET Core.
		// IsSQLServer checks for Microsoft.Data.SqlClient (replaces System.Data.SqlClient).
		// All other providers retain their original FullName checks — these are dormant
		// in the .NET 10 migration (only SQL Server is supported) but must compile.
		// =====================================================================================

		/// <summary>
		/// Returns true if the command is a Microsoft SQL Server command.
		/// Checks for Microsoft.Data.SqlClient.SqlCommand (the .NET 10 replacement for
		/// System.Data.SqlClient.SqlCommand) as well as the legacy System.Data.SqlClient name
		/// for backward compatibility in test/mock contexts.
		/// </summary>
		public static bool IsSQLServer(IDbCommand cmd)
		{
			return (cmd != null) &&
			       (cmd.GetType().FullName == "Microsoft.Data.SqlClient.SqlCommand" ||
			        cmd.GetType().FullName == "System.Data.SqlClient.SqlCommand");
		}

		/// <summary>
		/// Returns true if the connection is a Microsoft SQL Server connection.
		/// </summary>
		public static bool IsSQLServer(IDbConnection con)
		{
			return (con != null) &&
			       (con.GetType().FullName == "Microsoft.Data.SqlClient.SqlConnection" ||
			        con.GetType().FullName == "System.Data.SqlClient.SqlConnection");
		}

		/// <summary>
		/// Returns true if the command is an Oracle DataAccess command.
		/// </summary>
		public static bool IsOracleDataAccess(IDbCommand cmd)
		{
			// 08/15/2005 Paul.  Type.GetType("Oracle.DataAccess.Client.OracleCommand") is returning NULL.  Use FullName instead. 
			return (cmd != null) && (cmd.GetType().FullName == "Oracle.DataAccess.Client.OracleCommand");
		}

		/// <summary>
		/// Returns true if the connection is an Oracle DataAccess connection.
		/// </summary>
		public static bool IsOracleDataAccess(IDbConnection con)
		{
			return (con != null) && (con.GetType().FullName == "Oracle.DataAccess.Client.OracleConnection");
		}

		/// <summary>
		/// Returns true if the command is an Oracle System.Data command.
		/// </summary>
		public static bool IsOracleSystemData(IDbCommand cmd)
		{
			return (cmd != null) && (cmd.GetType().FullName == "System.Data.OracleClient.OracleCommand");
		}

		/// <summary>
		/// Returns true if the connection is an Oracle System.Data connection.
		/// </summary>
		public static bool IsOracleSystemData(IDbConnection con)
		{
			return (con != null) && (con.GetType().FullName == "System.Data.OracleClient.OracleConnection");
		}

		/// <summary>
		/// Returns true if the command is an Oracle command (either DataAccess or System.Data provider).
		/// </summary>
		public static bool IsOracle(IDbCommand cmd)
		{
			return IsOracleDataAccess(cmd) || IsOracleSystemData(cmd);
		}

		/// <summary>
		/// Returns true if the connection is an Oracle connection (either DataAccess or System.Data provider).
		/// </summary>
		public static bool IsOracle(IDbConnection con)
		{
			return IsOracleDataAccess(con) || IsOracleSystemData(con);
		}

		/// <summary>
		/// Returns true if the command is a PostgreSQL (Npgsql) command.
		/// </summary>
		public static bool IsPostgreSQL(IDbCommand cmd)
		{
			return (cmd != null) && (cmd.GetType().FullName == "Npgsql.NpgsqlCommand");
		}

		/// <summary>
		/// Returns true if the connection is a PostgreSQL (Npgsql) connection.
		/// </summary>
		public static bool IsPostgreSQL(IDbConnection con)
		{
			return (con != null) && (con.GetType().FullName == "Npgsql.NpgsqlConnection");
		}

		/// <summary>
		/// Returns true if the command is a MySQL command.
		/// </summary>
		public static bool IsMySQL(IDbCommand cmd)
		{
			return (cmd != null) && (cmd.GetType().FullName == "MySql.Data.MySqlClient.MySqlCommand");
		}

		/// <summary>
		/// Returns true if the connection is a MySQL connection.
		/// </summary>
		public static bool IsMySQL(IDbConnection con)
		{
			return (con != null) && (con.GetType().FullName == "MySql.Data.MySqlClient.MySqlConnection");
		}

		/// <summary>
		/// Returns true if the command is an IBM DB2 command.
		/// </summary>
		public static bool IsDB2(IDbCommand cmd)
		{
			return (cmd != null) && (cmd.GetType().FullName == "IBM.Data.DB2.DB2Command");
		}

		/// <summary>
		/// Returns true if the connection is an IBM DB2 connection.
		/// </summary>
		public static bool IsDB2(IDbConnection con)
		{
			return (con != null) && (con.GetType().FullName == "IBM.Data.DB2.DB2Connection");
		}

		/// <summary>
		/// Returns true if the command is a SAP/iAnywhere SQL Anywhere command.
		/// </summary>
		public static bool IsSqlAnywhere(IDbCommand cmd)
		{
			return (cmd != null) && (cmd.GetType().FullName == "iAnywhere.Data.AsaClient.AsaCommand");
		}

		/// <summary>
		/// Returns true if the connection is a SAP/iAnywhere SQL Anywhere connection.
		/// </summary>
		public static bool IsSqlAnywhere(IDbConnection con)
		{
			return (con != null) && (con.GetType().FullName == "iAnywhere.Data.AsaClient.AsaConnection");
		}

		/// <summary>
		/// Returns true if the command is a Sybase Adaptive Server command.
		/// </summary>
		public static bool IsSybase(IDbCommand cmd)
		{
			return (cmd != null) && (cmd.GetType().FullName == "Sybase.Data.AseClient.AseCommand");
		}

		/// <summary>
		/// Returns true if the connection is a Sybase Adaptive Server connection.
		/// </summary>
		public static bool IsSybase(IDbConnection con)
		{
			return (con != null) && (con.GetType().FullName == "Sybase.Data.AseClient.AseConnection");
		}

		/// <summary>
		/// Returns true if the command is an EffiProz command.
		/// </summary>
		public static bool IsEffiProz(IDbCommand cmd)
		{
			return (cmd != null) && (cmd.GetType().FullName == "System.Data.EffiProz.EfzCommand");
		}

		/// <summary>
		/// Returns true if the connection is an EffiProz connection.
		/// </summary>
		public static bool IsEffiProz(IDbConnection con)
		{
			return (con != null) && (con.GetType().FullName == "System.Data.EffiProz.EfzConnection");
		}

		/// <summary>
		/// Returns true if the provider requires streaming of binary (BLOB) fields.
		/// SQL Server, PostgreSQL, and EffiProz do not require streaming.
		/// Oracle and DB2 require streaming.
		/// </summary>
		public static bool StreamBlobs(IDbConnection con)
		{
			if      ( IsPostgreSQL      (con) ) return false;
			else if ( IsSQLServer       (con) ) return false;
			else if ( IsEffiProz        (con) ) return false;
			else if ( IsOracleDataAccess(con) ) return true;
			else if ( IsOracleSystemData(con) ) return true;
			else if ( IsDB2             (con) ) return true;
			return false;
		}

		/// <summary>
		/// Returns the case-sensitive collation suffix for a LIKE clause, or empty string if not needed.
		/// Only relevant for SQL Server and MySQL — other providers use collation at the database level.
		/// </summary>
		public static string CaseSensitiveCollation(IDbConnection con)
		{
			// 07/24/2010 Paul.  Instead of managing collation in code, it is better to change the collation on the field in the database. 
			return String.Empty;
		}
	}
}
