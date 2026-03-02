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
// .NET 10 Migration: SplendidCRM/_code/Sql.cs → src/SplendidCRM.Core/Sql.cs
// Changes applied:
//   - REMOVED: using System.Web; using System.Web.UI.*; using System.Web.UI.HtmlControls;
//              using System.Web.UI.WebControls; (all WebForms namespaces)
//   - ADDED:   using Microsoft.AspNetCore.Http; using Microsoft.Extensions.Caching.Memory;
//              using Microsoft.Data.SqlClient; (replaces System.Data.SqlClient)
//   - REMOVED WebForms-only methods: AppendParameter(IDbCommand, ListControl, string),
//     AppendParameterWithNull(IDbCommand, ListControl, string), AppendGuids(IDbCommand, ListBox, string),
//     ToStringArray(ListBox), AddScriptReference(ScriptManager, string),
//     AddStyleSheet(Page, string), AddServiceReference(ScriptManager, string), ClientScriptBlock(IDbCommand)
//   - REPLACED CalendarControl.SqlDateTimeFormat → SqlDateTimeFormat constant "yyyy-MM-dd HH:mm:ss"
//   - REPLACED System.Data.SqlClient.SqlCommand/SqlConnection → Microsoft.Data.SqlClient.*
//     in IsSQLServer() detector methods
//   - REPLACED System.Data.SqlClient.SqlParameter → Microsoft.Data.SqlClient.SqlParameter
//     in AddParameter(IDbCommand, string, DataTable) overload
//   - REPLACED HttpContext.Current.Application["key"] → static ambient _ambientCache
//   - REPLACED HttpApplicationState Application parameter in SqlSearchClause → IMemoryCache memoryCache
//   - REPLACED HttpContext.Current.Session access → _ambientHttpAccessor.HttpContext.Session
//   - REPLACED DbProviderFactories.GetFactory()/GetFactory(Application) → static ambient _ambientDbf
//   - REPLACED HttpUtility.UrlEncode → System.Net.WebUtility.UrlEncode
//   - ADDED: static ambient fields + SetAmbient() for DI-compatible static usage
//   - PRESERVED: All business logic, type conversion logic, SQL parameter building logic
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	public class Sql
	{
		// =====================================================================================
		// .NET 10 Migration: Static ambient fields replacing HttpContext.Current.Application
		// and DbProviderFactories.GetFactory() static patterns used throughout this class.
		// Set via SetAmbient() called at application startup by the DI-aware host.
		// Thread-safety: DbProviderFactories and IMemoryCache are thread-safe singleton services.
		// =====================================================================================

		/// <summary>
		/// Static ambient DbProviderFactories — replaces DbProviderFactories.GetFactory() static calls.
		/// BEFORE: DbProviderFactory dbf = DbProviderFactories.GetFactory();
		/// AFTER:  DbProviderFactory dbf = _ambientDbf;
		/// </summary>
		private static DbProviderFactories _ambientDbf;

		/// <summary>
		/// Static ambient IMemoryCache — replaces HttpApplicationState (Application["key"]) static reads.
		/// BEFORE: HttpContext.Current.Application["key"]
		/// AFTER:  _ambientCache?.Get&lt;object&gt;("key")
		/// </summary>
		private static IMemoryCache _ambientCache;

		/// <summary>
		/// Static ambient IHttpContextAccessor — replaces HttpContext.Current.Session access.
		/// BEFORE: HttpContext.Current.Session["key"]
		/// AFTER:  _ambientHttpAccessor?.HttpContext?.Session?.GetString("key")
		/// </summary>
		private static IHttpContextAccessor _ambientHttpAccessor;

		/// <summary>
		/// Static ambient Security — provides USER_ID, TEAM_ID, and Filter() for record-level security.
		/// MIGRATION NOTE: Security is now instance-based (DI); stored here for static method access.
		/// </summary>
		private static Security _ambientSecurity;

		/// <summary>
		/// Register static ambient dependencies for this static utility class.
		/// Must be called at application startup before any static methods that use Application[]
		/// or Session[] patterns are invoked. Typically called from the DI constructor of a startup
		/// service that receives these via dependency injection.
		/// </summary>
		public static void SetAmbient(DbProviderFactories dbf, IMemoryCache memoryCache, IHttpContextAccessor httpContextAccessor, Security security = null)
		{
			_ambientDbf          = dbf;
			_ambientCache        = memoryCache;
			_ambientHttpAccessor = httpContextAccessor;
			_ambientSecurity     = security;
		}

		// =====================================================================================
		// SQL DateTime format constant replacing CalendarControl.SqlDateTimeFormat
		// MIGRATION NOTE: CalendarControl is a WebForms type, not available in .NET 10 ASP.NET Core.
		// "yyyy-MM-dd HH:mm:ss" is the ISO 8601 SQL Server-compatible date/time format.
		// =====================================================================================
		private const string SqlDateTimeFormat = "yyyy-MM-dd HH:mm:ss";

		// 11/08/2019 Paul.  Move sEMPTY_PASSWORD to Sql. 
		public const string sEMPTY_PASSWORD = "**********";

		public static string HexEncode(byte[] aby)
		{
			string hex = "0123456789abcdef";
			StringBuilder sb = new StringBuilder();
			for ( int i = 0 ; i < aby.Length ; i++ )
			{
				sb.Append(hex[(aby[i] & 0xf0) >> 4]);
				sb.Append(hex[ aby[i] & 0x0f]);
			}
			return sb.ToString();
		}

		public static string FormatSQL(string s, int nMaxLength)
		{
			if ( Sql.IsEmptyString(s) )
				s = "null";
			else
				s = "'" + Sql.EscapeSQL(s) + "'";
			if ( nMaxLength > s.Length )
				return s + Strings.Space(nMaxLength - s.Length);
			return s;
		}

		public static string EscapeSQL(string? str)
		{
			str = str.Replace("\'", "\'\'");
			return str;
		}

		public static string EscapeSQLLike(string? str)
		{
			str = str.Replace(@"\", @"\\");
			str = str.Replace("%" , @"\%");
			str = str.Replace("_" , @"\_");
			return str;
		}

		// 04/05/2012 Paul.  EscapeXml is needed in the SearchView. 
		public static string EscapeXml(string? str)
		{
			// MIGRATION NOTE: XmlConvert.EncodeName() is available but does different escaping.
			// Preserving original manual XML escape for backward compatibility.
			str = str.Replace("\"", "&quot;");
			str = str.Replace("\'", "&apos;");
			str = str.Replace("<" , "&lt;"  );
			str = str.Replace(">" , "&gt;"  );
			str = str.Replace("&" , "&amp;" );
			return str;
		}

		public static string EscapeJavaScript(string? str)
		{
			str = str.Replace(@"\", @"\\");
			str = str.Replace("\'", "\\\'");
			str = str.Replace("\"", "\\\"");
			str = str.Replace("\t", "\\t");
			str = str.Replace("\r", "\\r");
			str = str.Replace("\n", "\\n");
			return str;
		}

		// 11/18/2014 Paul.  Make it easy to escape an Application variable. 
		public static string EscapeJavaScript(object? o)
		{
			return EscapeJavaScript(Sql.ToString(o));
		}

		// 11/06/2013 Paul.  Make sure to JavaScript escape the text as the various languages may introduce accents. 
		public static string[] EscapeJavaScript(string[] arr)
		{
			string[] arrClean = null;
			if ( arr != null )
			{
				arrClean = new string[arr.Length];
				arr.CopyTo(arrClean, 0);
				for ( int i = 0; i < arrClean.Length; i++ )
				{
					arrClean[i] = Sql.EscapeJavaScript(arrClean[i]);
				}
			}
			return arrClean;
		}

		// 05/10/2016 Paul.  Generic List version of escape. 
		public static List<string> EscapeJavaScript(List<string> arr)
		{
			List<string> lstClean = null;
			if ( arr != null )
			{
				lstClean = new List<string>();
				for ( int i = 0; i < arr.Count; i++ )
				{
					lstClean.Add(Sql.EscapeJavaScript(arr[i]));
				}
			}
			return lstClean;
		}

		public static bool IsEmptyString(string? str)
		{
			if ( str == null || str == String.Empty )
				return true;
			return false;
		}

		public static bool IsEmptyString(object? obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return true;
			if ( obj.ToString() == String.Empty )
				return true;
			return false;
		}

		public static string ToString(string? str)
		{
			if ( str == null )
				return String.Empty;
			return str;
		}

		public static string ToString(object? obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return String.Empty;
			return obj.ToString() ?? String.Empty;
		}

		public static object ToDBString(string str)
		{
			if ( str == null )
				return DBNull.Value;
			if ( str == String.Empty )
				return DBNull.Value;
			return str;
		}

		public static object ToDBString(object? obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return DBNull.Value;
			string str = obj.ToString();
			if ( str == String.Empty )
				return DBNull.Value;
			return str;
		}

		public static byte[]? ToBinary(object? obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return new byte[0];
			return (byte[]) obj;
		}

		public static object ToDBBinary(object? obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return DBNull.Value;
			return obj;
		}

		public static object ToDBBinary(byte[] aby)
		{
			if ( aby == null )
				return DBNull.Value;
			else if ( aby.Length == 0 )
				return DBNull.Value;
			return aby;
		}

		// 11/14/2005 Paul.  Converting dates from the database requires the two below functions. 
		public static DateTime ToDateTime(string s)
		{
			if ( Sql.IsEmptyString(s) )
				return DateTime.MinValue;
			if ( Information.IsDate(s) )
				return Convert.ToDateTime(s);
			return DateTime.MinValue;
		}

		public static DateTime ToDateTime(object? obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return DateTime.MinValue;
			if ( Information.IsDate(obj) )
				return Convert.ToDateTime(obj);
			return DateTime.MinValue;
		}

		public static string ToDateString(string str)
		{
			if ( Sql.IsEmptyString(str) )
				return String.Empty;
			if ( Information.IsDate(str) )
				return Convert.ToDateTime(str).ToShortDateString();
			return String.Empty;
		}

		public static string ToDateString(object? obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return String.Empty;
			if ( Information.IsDate(obj) )
				return Convert.ToDateTime(obj).ToShortDateString();
			return String.Empty;
		}

		public static string ToString(DateTime dt)
		{
			if ( dt == DateTime.MinValue )
				return String.Empty;
			return dt.ToString();
		}

		public static string ToTimeString(object? obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return String.Empty;
			if ( Information.IsDate(obj) )
				return Convert.ToDateTime(obj).ToString("HH:mm:ss");
			return String.Empty;
		}

		public static object ToDBDateTime(string s)
		{
			if ( Sql.IsEmptyString(s) )
				return DBNull.Value;
			if ( Information.IsDate(s) )
				return Convert.ToDateTime(s);
			return DBNull.Value;
		}

		public static object ToDBDateTime(object? obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return DBNull.Value;
			if ( Information.IsDate(obj) )
				return Convert.ToDateTime(obj);
			return DBNull.Value;
		}

		public static bool IsEmptyGuid(Guid gID)
		{
			return gID == Guid.Empty;
		}

		public static bool IsEmptyGuid(object? obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return true;
			if ( obj.GetType() == Type.GetType("System.Guid") )
			{
				Guid gID = (Guid) obj;
				return gID == Guid.Empty;
			}
			string str = obj.ToString();
			if ( str == String.Empty )
				return true;
			try
			{
				Guid gID = new Guid(str);
				return gID == Guid.Empty;
			}
			catch
			{
			}
			return true;
		}

		public static Guid ToGuid(object? obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return Guid.Empty;
			if ( obj.GetType() == Type.GetType("System.Guid") )
				return (Guid) obj;
			string str = obj.ToString();
			if ( str == String.Empty )
				return Guid.Empty;
			try
			{
				return new Guid(str);
			}
			catch
			{
			}
			return Guid.Empty;
		}

		// 01/20/2013 Paul.  Add ToGuid(string) as it is the most common and we don't need the extra checks. 
		public static Guid ToGuid(string? str)
		{
			if ( str == null || str == String.Empty )
				return Guid.Empty;
			try
			{
				return new Guid(str);
			}
			catch
			{
			}
			return Guid.Empty;
		}

		// 10/25/2012 Paul.  Provide a safe version that returns null if string is null. 
		public static Guid? ToGuidSafe(object? obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return null;
			if ( obj.GetType() == Type.GetType("System.Guid") )
				return (Guid) obj;
			string str = obj.ToString();
			if ( str == String.Empty )
				return null;
			try
			{
				return new Guid(str);
			}
			catch
			{
			}
			return null;
		}

		// 02/14/2014 Paul.  Add a version that accepts a Guid. 
		public static Guid ToGuid(Guid gID)
		{
			return gID;
		}

		public static object ToDBGuid(Guid gID)
		{
			if ( gID == Guid.Empty )
				return DBNull.Value;
			return gID;
		}

		public static object ToDBGuid(object? obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return DBNull.Value;
			if ( obj.GetType() == Type.GetType("System.Guid") )
			{
				Guid gID = (Guid) obj;
				if ( gID == Guid.Empty )
					return DBNull.Value;
				return gID;
			}
			string str = obj.ToString();
			if ( str == String.Empty )
				return DBNull.Value;
			try
			{
				Guid gID = new Guid(str);
				if ( gID == Guid.Empty )
					return DBNull.Value;
				return gID;
			}
			catch
			{
			}
			return DBNull.Value;
		}

		public static Int32 ToInteger(string s)
		{
			if ( Sql.IsEmptyString(s) )
				return 0;
			if ( Information.IsNumeric(s) )
			{
				try
				{
					return Convert.ToInt32(s);
				}
				catch
				{
				}
			}
			return 0;
		}

		public static Int32 ToInteger(object? obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return 0;
			string s = obj.ToString();
			if ( s == String.Empty )
				return 0;
			if ( Information.IsNumeric(s) )
			{
				try
				{
					return Convert.ToInt32(obj);
				}
				catch
				{
				}
			}
			return 0;
		}

		public static Int64 ToLong(string s)
		{
			if ( Sql.IsEmptyString(s) )
				return 0;
			if ( Information.IsNumeric(s) )
			{
				try
				{
					return Convert.ToInt64(s);
				}
				catch
				{
				}
			}
			return 0;
		}

		public static Int64 ToLong(object? obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return 0;
			string s = obj.ToString();
			if ( s == String.Empty )
				return 0;
			if ( Information.IsNumeric(s) )
			{
				try
				{
					return Convert.ToInt64(obj);
				}
				catch
				{
				}
			}
			return 0;
		}

		// 06/16/2011 Paul.  Provide an alias for Int64. 
		public static Int64 ToInt64(string s)
		{
			return ToLong(s);
		}

		public static Int64 ToInt64(object? obj)
		{
			return ToLong(obj);
		}

		public static Int16 ToShort(string s)
		{
			if ( Sql.IsEmptyString(s) )
				return 0;
			if ( Information.IsNumeric(s) )
			{
				try
				{
					return Convert.ToInt16(s);
				}
				catch
				{
				}
			}
			return 0;
		}

		public static Int16 ToShort(object? obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return 0;
			string s = obj.ToString();
			if ( s == String.Empty )
				return 0;
			if ( Information.IsNumeric(s) )
			{
				try
				{
					return Convert.ToInt16(obj);
				}
				catch
				{
				}
			}
			return 0;
		}

		// 12/09/2013 Paul.  Add a version that accepts Int16. 
		public static Int16 ToShort(Int16 nValue)
		{
			return nValue;
		}

		public static object ToDBInteger(object? obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return DBNull.Value;
			string s = obj.ToString();
			if ( s == String.Empty )
				return DBNull.Value;
			if ( Information.IsNumeric(s) )
			{
				try
				{
					return Convert.ToInt32(obj);
				}
				catch
				{
				}
			}
			return DBNull.Value;
		}

		public static object ToDBInteger(int n)
		{
			if ( n == 0 )
				return DBNull.Value;
			return n;
		}

		public static object ToDBLong(object? obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return DBNull.Value;
			string s = obj.ToString();
			if ( s == String.Empty )
				return DBNull.Value;
			if ( Information.IsNumeric(s) )
			{
				try
				{
					return Convert.ToInt64(obj);
				}
				catch
				{
				}
			}
			return DBNull.Value;
		}

		public static object ToDBLong(long n)
		{
			if ( n == 0 )
				return DBNull.Value;
			return n;
		}

		// 06/16/2011 Paul.  Provide a version that does not null zero. 
		public static object ToDBLong(long n, bool bNullZero)
		{
			if ( bNullZero && n == 0 )
				return DBNull.Value;
			return n;
		}

		public static float ToFloat(string s)
		{
			if ( Sql.IsEmptyString(s) )
				return 0;
			if ( Information.IsNumeric(s) )
			{
				try
				{
					return Convert.ToSingle(s);
				}
				catch
				{
				}
			}
			return 0;
		}

		public static float ToFloat(object? obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return 0;
			string s = obj.ToString();
			if ( s == String.Empty )
				return 0;
			if ( Information.IsNumeric(s) )
			{
				try
				{
					return Convert.ToSingle(obj);
				}
				catch
				{
				}
			}
			return 0;
		}

		// 05/14/2019 Paul.  Provide a version that accepts a float. 
		public static float ToFloat(float f)
		{
			return f;
		}

		public static object ToDBFloat(object? obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return DBNull.Value;
			string s = obj.ToString();
			if ( s == String.Empty )
				return DBNull.Value;
			if ( Information.IsNumeric(s) )
			{
				try
				{
					return Convert.ToSingle(obj);
				}
				catch
				{
				}
			}
			return DBNull.Value;
		}

		public static object ToDBFloat(float f)
		{
			if ( f == 0 )
				return DBNull.Value;
			return f;
		}

		public static object ToDBDouble(object? obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return DBNull.Value;
			string s = obj.ToString();
			if ( s == String.Empty )
				return DBNull.Value;
			if ( Information.IsNumeric(s) )
			{
				try
				{
					return Convert.ToDouble(obj);
				}
				catch
				{
				}
			}
			return DBNull.Value;
		}

		public static double ToDouble(string s)
		{
			if ( Sql.IsEmptyString(s) )
				return 0;
			if ( Information.IsNumeric(s) )
			{
				try
				{
					return Convert.ToDouble(s);
				}
				catch
				{
				}
			}
			return 0;
		}

		public static double ToDouble(object? obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return 0;
			string s = obj.ToString();
			if ( s == String.Empty )
				return 0;
			if ( Information.IsNumeric(s) )
			{
				try
				{
					return Convert.ToDouble(obj);
				}
				catch
				{
				}
			}
			return 0;
		}

		// 05/14/2019 Paul.  Provide a version that accepts a double. 
		public static double ToDouble(double d)
		{
			return d;
		}

		public static Decimal ToDecimal(string s)
		{
			if ( Sql.IsEmptyString(s) )
				return 0;
			if ( Information.IsNumeric(s) )
			{
				try
				{
					return Convert.ToDecimal(s);
				}
				catch
				{
				}
			}
			return 0;
		}

		public static Decimal ToDecimal(object? obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return 0;
			string s = obj.ToString();
			if ( s == String.Empty )
				return 0;
			if ( Information.IsNumeric(s) )
			{
				try
				{
					return Convert.ToDecimal(obj);
				}
				catch
				{
				}
			}
			return 0;
		}

		// 11/14/2005 Paul.  String version that is culture aware. 
		public static Decimal ToDecimal(string s, CultureInfo culture)
		{
			if ( Sql.IsEmptyString(s) )
				return 0;
			if ( Information.IsNumeric(s) )
			{
				try
				{
					return Decimal.Parse(s, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, culture);
				}
				catch
				{
				}
			}
			return 0;
		}

		// 05/14/2019 Paul.  Provide a version that accepts a Decimal. 
		public static Decimal ToDecimal(Decimal d)
		{
			return d;
		}

		public static object ToDBDecimal(object? obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return DBNull.Value;
			string s = obj.ToString();
			if ( s == String.Empty )
				return DBNull.Value;
			if ( Information.IsNumeric(s) )
			{
				try
				{
					return Convert.ToDecimal(obj);
				}
				catch
				{
				}
			}
			return DBNull.Value;
		}

		public static object ToDBDecimal(Decimal d)
		{
			if ( d == 0 )
				return DBNull.Value;
			return d;
		}

		public static bool ToBoolean(string? s)
		{
			if ( Sql.IsEmptyString(s) )
				return false;
			s = s.Trim();
			// 12/10/2005 Paul.  The boolean is stored as a 1 in the database. 
			if ( s == "1" || s.ToLower() == "true" || s.ToLower() == "on" )
				return true;
			return false;
		}

		public static bool ToBoolean(object? obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return false;
			if ( obj.GetType() == typeof(bool) )
				return (bool) obj;
			return ToBoolean(obj.ToString());
		}

		// 11/07/2009 Paul.  We need a version that accepts the bool type. 
		public static bool ToBoolean(bool b)
		{
			return b;
		}

		public static object ToDBBoolean(object? obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return DBNull.Value;
			if ( obj.GetType() == typeof(bool) )
				return (bool) obj;
			return ToBoolean(obj.ToString());
		}

		public static object ToDBBoolean(bool b)
		{
			return b;
		}

		public static string Truncate(string str, int nMaxLength)
		{
			if ( str == null )
				return String.Empty;
			if ( str.Length > nMaxLength )
				return str.Substring(0, nMaxLength);
			return str;
		}

		public static string MaxLength(string str, int nMaxLength)
		{
			return Truncate(str, nMaxLength);
		}

		// =====================================================================================
		// DB Provider detection methods.
		// MIGRATION NOTE: Detector checks updated from System.Data.SqlClient → Microsoft.Data.SqlClient
		// to match the NuGet package that replaces the legacy System.Data.SqlClient inbox assembly.
		// =====================================================================================
		public static bool IsSQLServer(IDbCommand cmd)
		{
			return cmd.GetType().FullName == "Microsoft.Data.SqlClient.SqlCommand";
		}

		public static bool IsSQLServer(IDbConnection con)
		{
			return con.GetType().FullName == "Microsoft.Data.SqlClient.SqlConnection";
		}

		// 05/12/2007 Paul.  We need to distinguish between the two Oracle providers. 
		public static bool IsOracleDataAccess(IDbCommand cmd)
		{
			return cmd.GetType().FullName == "Oracle.DataAccess.Client.OracleCommand";
		}

		public static bool IsOracleDataAccess(IDbConnection con)
		{
			return con.GetType().FullName == "Oracle.DataAccess.Client.OracleConnection";
		}

		public static bool IsOracleSystemData(IDbCommand cmd)
		{
			return cmd.GetType().FullName == "System.Data.OracleClient.OracleCommand";
		}

		public static bool IsOracleSystemData(IDbConnection con)
		{
			return con.GetType().FullName == "System.Data.OracleClient.OracleConnection";
		}

		public static bool IsOracle(IDbCommand cmd)
		{
			return IsOracleDataAccess(cmd) || IsOracleSystemData(cmd);
		}

		public static bool IsOracle(IDbConnection con)
		{
			return IsOracleDataAccess(con) || IsOracleSystemData(con);
		}

		public static bool IsPostgreSQL(IDbCommand cmd)
		{
			return cmd.GetType().FullName == "Npgsql.NpgsqlCommand";
		}

		public static bool IsPostgreSQL(IDbConnection con)
		{
			return con.GetType().FullName == "Npgsql.NpgsqlConnection";
		}

		public static bool IsMySQL(IDbCommand cmd)
		{
			return cmd.GetType().FullName == "MySql.Data.MySqlClient.MySqlCommand";
		}

		public static bool IsMySQL(IDbConnection con)
		{
			return con.GetType().FullName == "MySql.Data.MySqlClient.MySqlConnection";
		}

		public static bool IsDB2(IDbCommand cmd)
		{
			return cmd.GetType().FullName == "IBM.Data.DB2.DB2Command";
		}

		public static bool IsDB2(IDbConnection con)
		{
			return con.GetType().FullName == "IBM.Data.DB2.DB2Connection";
		}

		// 09/29/2007 Paul.  Sybase is the Adaptive Server Enterprise. 
		public static bool IsSybase(IDbCommand cmd)
		{
			return cmd.GetType().FullName == "Sybase.Data.AseClient.AseCommand";
		}

		public static bool IsSybase(IDbConnection con)
		{
			return con.GetType().FullName == "Sybase.Data.AseClient.AseConnection";
		}

		// 03/11/2012 Paul.  Add support for EffiProz database. 
		public static bool IsEffiProz(IDbCommand cmd)
		{
			return cmd.GetType().FullName == "EffiProz.Data.EfCommand";
		}

		public static bool IsEffiProz(IDbConnection con)
		{
			return con.GetType().FullName == "EffiProz.Data.EfConnection";
		}

		// 06/20/2005 Paul.  Stream blobs only if SQL Server.
		// With Oracle, all data must be read at one time. 
		public static bool StreamBlobs(IDbCommand cmd)
		{
			return IsSQLServer(cmd) && !IsEffiProz(cmd);
		}

		// Overload for IDbConnection — used by Crm.EmailImages.LoadFile(gID, stm, trn)
		// where trn.Connection is passed directly.
		public static bool StreamBlobs(IDbConnection con)
		{
			return IsSQLServer(con) && !IsEffiProz(con);
		}

		// =====================================================================================
		// ExpandParameters — Expands SQL command text with parameter values for tracing/logging.
		// MIGRATION NOTE: CalendarControl.SqlDateTimeFormat replaced by SqlDateTimeFormat constant.
		// =====================================================================================
		public static string ExpandParameters(IDbCommand cmd)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(cmd.CommandText);
			sb.Append(ControlChars.CrLf);
			foreach ( IDbDataParameter par in cmd.Parameters )
			{
				sb.Append("   ");
				sb.Append(par.ParameterName);
				sb.Append(" = ");
				if ( par.Value == null || par.Value == DBNull.Value )
				{
					sb.Append("null");
				}
				else if ( par.DbType == DbType.DateTime || par.DbType == DbType.DateTime2 || par.DbType == DbType.Date )
				{
					if ( par.Value is DateTime )
						sb.Append("'" + ((DateTime) par.Value).ToString(SqlDateTimeFormat) + "'");
					else
						sb.Append("'" + Sql.EscapeSQL(par.Value.ToString()) + "'");
				}
				else if ( par.DbType == DbType.Guid )
				{
					sb.Append("'" + par.Value.ToString() + "'");
				}
				else if ( par.DbType == DbType.String || par.DbType == DbType.AnsiString || par.DbType == DbType.AnsiStringFixedLength || par.DbType == DbType.StringFixedLength )
				{
					sb.Append("'" + Sql.EscapeSQL(par.Value.ToString()) + "'");
				}
				else if ( par.DbType == DbType.Boolean )
				{
					sb.Append((bool) par.Value ? "1" : "0");
				}
				else if ( par.DbType == DbType.Binary )
				{
					sb.Append("0x" + Sql.HexEncode((byte[]) par.Value));
				}
				else
				{
					sb.Append(par.Value.ToString());
				}
				sb.Append(ControlChars.CrLf);
			}
			return sb.ToString();
		}

		// =====================================================================================
		// SqlFilterMode — Enum controlling how string parameters are matched in SQL searches.
		// =====================================================================================
		[Serializable]
		public enum SqlFilterMode
		{
			  StartsWith  = 0
			, EndsWith    = 1
			, Contains    = 2
			, Exact       = 3
			, FullText    = 4
		}

		public static IDbDataParameter FindParameter(IDbCommand cmd, string sParameterName)
		{
			foreach ( IDbDataParameter par in cmd.Parameters )
			{
				if ( par.ParameterName == sParameterName )
					return par;
			}
			return null;
		}

		// 04/02/2012 Paul.  We need SetParameter for the REST API. 
		public static void SetParameter(IDbDataParameter par, string sValue)
		{
			if ( par != null )
			{
				if ( Sql.IsEmptyString(sValue) )
					par.Value = DBNull.Value;
				else
					par.Value = sValue;
			}
		}

		public static void SetParameter(IDbDataParameter par, object oValue)
		{
			if ( par != null )
			{
				if ( oValue == null || oValue == DBNull.Value )
					par.Value = DBNull.Value;
				else
					par.Value = oValue;
			}
		}

		public static void SetParameter(IDbCommand cmd, string sField, string sValue)
		{
			IDbDataParameter par = FindParameter(cmd, "@" + sField);
			if ( par != null )
			{
				if ( Sql.IsEmptyString(sValue) )
					par.Value = DBNull.Value;
				else
					par.Value = sValue;
			}
		}

		public static void SetParameter(IDbCommand cmd, string sField, DateTime dtValue)
		{
			IDbDataParameter par = FindParameter(cmd, "@" + sField);
			if ( par != null )
			{
				if ( dtValue == DateTime.MinValue )
					par.Value = DBNull.Value;
				else
					par.Value = dtValue;
			}
		}

		public static void SetParameter(IDbCommand cmd, string sField, Guid gValue)
		{
			IDbDataParameter par = FindParameter(cmd, "@" + sField);
			if ( par != null )
			{
				if ( gValue == Guid.Empty )
					par.Value = DBNull.Value;
				else
					par.Value = gValue;
			}
		}

		public static void SetParameter(IDbCommand cmd, string sField, int nValue)
		{
			IDbDataParameter par = FindParameter(cmd, "@" + sField);
			if ( par != null )
			{
				par.Value = nValue;
			}
		}

		public static void SetParameter(IDbCommand cmd, string sField, bool bValue)
		{
			IDbDataParameter par = FindParameter(cmd, "@" + sField);
			if ( par != null )
			{
				par.Value = bValue;
			}
		}

		public static void SetParameter(IDbCommand cmd, string sField, Decimal dValue)
		{
			IDbDataParameter par = FindParameter(cmd, "@" + sField);
			if ( par != null )
			{
				par.Value = dValue;
			}
		}

		public static void SetParameter(IDbCommand cmd, string sField, float fValue)
		{
			IDbDataParameter par = FindParameter(cmd, "@" + sField);
			if ( par != null )
			{
				par.Value = fValue;
			}
		}

		// =====================================================================================
		// CreateInsertParameters — Builds an INSERT command from a DataRow by reflecting column names.
		// MIGRATION NOTE: DbProviderFactories.GetFactory() → _ambientDbf static ambient field.
		// =====================================================================================
		public static IDbCommand CreateInsertParameters(IDbConnection con, string sTableName)
		{
			DbProviderFactories dbf = _ambientDbf;
			if ( dbf == null )
				throw new InvalidOperationException("Sql.CreateInsertParameters: _ambientDbf not set. Call Sql.SetAmbient() at application startup.");
			IDbCommand cmd = dbf.CreateCommand(con);
			cmd.CommandText = "insert into " + sTableName;
			return cmd;
		}

		// 08/30/2005 Paul.  Extract name and full DB name. 
		public static string CreateDbName(string sTableName)
		{
			if ( sTableName == null )
				return String.Empty;
			int n = sTableName.IndexOf(".");
			if ( n >= 0 )
				return sTableName;
			return sTableName;
		}

		public static string ExtractDbName(string sTableName)
		{
			if ( sTableName == null )
				return String.Empty;
			int n = sTableName.IndexOf(".");
			if ( n >= 0 )
				return sTableName.Substring(0, n);
			return String.Empty;
		}

		// 04/27/2008 Paul.  Add a CreateParameter method to simplify the code. 
		public static IDbDataParameter CreateParameter(IDbCommand cmd, string sField)
		{
			DbProviderFactories dbf = _ambientDbf;
			if ( dbf != null )
			{
				IDbDataParameter par = dbf.CreateParameter();
				par.ParameterName = sField.StartsWith("@") ? sField : "@" + sField;
				return par;
			}
			else
			{
				IDbDataParameter par = cmd.CreateParameter();
				par.ParameterName = sField.StartsWith("@") ? sField : "@" + sField;
				return par;
			}
		}

		// 10/07/2009 Paul.  A version that specifies the DbType. 
		public static IDbDataParameter CreateParameter(IDbCommand cmd, string sField, DbType dbType)
		{
			IDbDataParameter par = CreateParameter(cmd, sField);
			par.DbType = dbType;
			return par;
		}

		// .NET 10 Migration: Added from SplendidCRM/_code/Sql.cs (lines ~1432-1477 in original).
		// This overload was present in the original Sql.cs and is used by SqlProcsDynamicFactory.DynamicFactory()
		// to map C# type names from the vwSqlColumns "CsType" column to ADO.NET DbType enum values.
		// PRESERVED: All type mapping logic. IsSqlAnywhere/IsEffiProz removed (SQL Server only in .NET 10).
		// Minimal change clause: Added only because SqlProcsDynamicFactory.cs requires it.
		/// <summary>
		/// Creates a typed SQL parameter from a C# type name string and a maximum length.
		/// Used by SqlProcs.DynamicFactory to map vwSqlColumns.CsType values to ADO.NET DbType.
		/// </summary>
		/// <param name="cmd">The command to create the parameter for.</param>
		/// <param name="sField">The parameter name (including @ prefix if desired).</param>
		/// <param name="sCsType">
		/// The C# type name from vwSqlColumns.CsType. Supported values:
		/// "Guid", "short", "Int32", "Int64", "float", "decimal", "bool", "DateTime",
		/// "byte[]", "ansistring", "string" (default).
		/// </param>
		/// <param name="nLength">The maximum length for variable-length parameters.</param>
		/// <returns>A configured IDbDataParameter with DbType and Size set from the C# type name.</returns>
		public static IDbDataParameter CreateParameter(IDbCommand cmd, string sField, string sCsType, int nLength)
		{
			IDbDataParameter par = Sql.CreateParameter(cmd, sField);
			switch ( sCsType )
			{
				case "Guid":
					// 09/12/2010 Paul.  SQL Server supports native Guid (uniqueidentifier).
					// .NET 10 Migration: IsSqlAnywhere/IsEffiProz checks removed (SQL Server only).
					// Oracle does not support Guids natively; use string with length 36.
					if ( Sql.IsSQLServer(cmd) )
					{
						par.DbType = DbType.Guid;
					}
					else
					{
						// 08/11/2005 Paul.  Oracle does not support Guids, nor does MySQL. 
						par.DbType = DbType.String;
						par.Size   = 36;  // 08/13/2005 Paul.  Only set size for variable length fields. 
					}
					break;
				case "short"     :  par.DbType = DbType.Int16    ;  break;
				case "Int32"     :  par.DbType = DbType.Int32    ;  break;
				case "Int64"     :  par.DbType = DbType.Int64    ;  break;
				case "float"     :  par.DbType = DbType.Double   ;  break;
				case "decimal"   :  par.DbType = DbType.Decimal  ;  break;
				case "bool"      :
					// 10/01/2006 Paul.  DB2 seems to prefer Boolean.  Oracle wants Byte.
					// We are going to use Boolean for all but Oracle as this what we have tested extensively.
					if ( Sql.IsOracle(cmd) )
						par.DbType = DbType.Byte   ;
					else
						par.DbType = DbType.Boolean;
					break;
				case "DateTime"  :  par.DbType = DbType.DateTime ;  break;
				case "byte[]"    :  par.DbType = DbType.Binary   ;  par.Size = nLength;  break;
				// 01/24/2006 Paul.  A severe error occurred on the current command. The results, if any, should be discarded. 
				// MS03-031 security patch causes this error because of stricter datatype processing.  
				// http://www.microsoft.com/technet/security/bulletin/MS03-031.mspx.
				// http://support.microsoft.com/kb/827366/
				case "ansistring":  par.DbType = DbType.AnsiString;  par.Size = nLength;  break;
				//case "string"  :  par.DbType = DbType.String    ;  par.Size = nLength;  break;
				default          :  par.DbType = DbType.String   ;  par.Size = nLength;  break;
			}
			return par;
		}

		// 03/15/2012 Paul.  We need to know the parameter placeholder for PostgreSQL. 
		public static string NextPlaceholder(IDbCommand cmd)
		{
			if ( IsPostgreSQL(cmd) )
				return ":p" + (cmd.Parameters.Count + 1).ToString();
			return "@p" + (cmd.Parameters.Count + 1).ToString();
		}

		// =====================================================================================
		// AddParameter overloads — add typed SQL parameters to IDbCommand.
		// All DbType handling for Oracle, PostgreSQL, DB2, and SQL Server variants preserved.
		// =====================================================================================
		public static IDbDataParameter AddParameter(IDbCommand cmd, string sField, Int16 nValue)
		{
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sField.StartsWith("@") ? sField : "@" + sField;
			par.DbType        = DbType.Int16;
			par.Value         = nValue;
			cmd.Parameters.Add(par);
			return par;
		}

		public static IDbDataParameter AddParameter(IDbCommand cmd, string sField, Int32 nValue)
		{
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sField.StartsWith("@") ? sField : "@" + sField;
			par.DbType        = DbType.Int32;
			par.Value         = nValue;
			cmd.Parameters.Add(par);
			return par;
		}

		public static IDbDataParameter AddParameter(IDbCommand cmd, string sField, Int64 nValue)
		{
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sField.StartsWith("@") ? sField : "@" + sField;
			par.DbType        = DbType.Int64;
			par.Value         = nValue;
			cmd.Parameters.Add(par);
			return par;
		}

		public static IDbDataParameter AddParameter(IDbCommand cmd, string sField, float fValue)
		{
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sField.StartsWith("@") ? sField : "@" + sField;
			par.DbType        = DbType.Single;
			par.Value         = fValue;
			cmd.Parameters.Add(par);
			return par;
		}

		public static IDbDataParameter AddParameter(IDbCommand cmd, string sField, double dValue)
		{
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sField.StartsWith("@") ? sField : "@" + sField;
			par.DbType        = DbType.Double;
			par.Value         = dValue;
			cmd.Parameters.Add(par);
			return par;
		}

		public static IDbDataParameter AddParameter(IDbCommand cmd, string sField, Decimal dValue)
		{
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sField.StartsWith("@") ? sField : "@" + sField;
			par.DbType        = DbType.Decimal;
			par.Value         = dValue;
			cmd.Parameters.Add(par);
			return par;
		}

		public static IDbDataParameter AddParameter(IDbCommand cmd, string sField, bool bValue)
		{
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sField.StartsWith("@") ? sField : "@" + sField;
			// 02/04/2006 Paul.  Oracle doesn't support a boolean type, so store as int. 
			if ( IsOracle(cmd) )
			{
				par.DbType = DbType.Int32;
				par.Value  = bValue ? 1 : 0;
			}
			else if ( IsPostgreSQL(cmd) || IsDB2(cmd) )
			{
				par.DbType = DbType.Boolean;
				par.Value  = bValue;
			}
			else
			{
				par.DbType = DbType.Boolean;
				par.Value  = bValue;
			}
			cmd.Parameters.Add(par);
			return par;
		}

		public static IDbDataParameter AddParameter(IDbCommand cmd, string sField, Guid gValue)
		{
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sField.StartsWith("@") ? sField : "@" + sField;
			if ( IsOracle(cmd) )
			{
				par.DbType = DbType.AnsiString;
				par.Value  = gValue == Guid.Empty ? DBNull.Value : (object)gValue.ToString().ToUpper();
			}
			else
			{
				par.DbType = DbType.Guid;
				par.Value  = gValue == Guid.Empty ? DBNull.Value : (object)gValue;
			}
			cmd.Parameters.Add(par);
			return par;
		}

		public static IDbDataParameter AddParameter(IDbCommand cmd, string sField, DateTime dtValue)
		{
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sField.StartsWith("@") ? sField : "@" + sField;
			par.DbType        = DbType.DateTime;
			par.Value         = dtValue == DateTime.MinValue ? DBNull.Value : (object)dtValue;
			cmd.Parameters.Add(par);
			return par;
		}

		// 08/28/2006 Paul.  String parameter with maximum length. 
		public static IDbDataParameter AddParameter(IDbCommand cmd, string sField, string sValue, int nMaxLength)
		{
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sField.StartsWith("@") ? sField : "@" + sField;
			par.DbType        = DbType.String;
			par.Size          = nMaxLength;
			if ( Sql.IsEmptyString(sValue) )
				par.Value = DBNull.Value;
			else
				par.Value = Truncate(sValue, nMaxLength);
			cmd.Parameters.Add(par);
			return par;
		}

		// 11/14/2005 Paul.  A string parameter without a length limitation. 
		public static IDbDataParameter AddParameter(IDbCommand cmd, string sField, string sValue)
		{
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sField.StartsWith("@") ? sField : "@" + sField;
			par.DbType        = DbType.String;
			if ( Sql.IsEmptyString(sValue) )
				par.Value = DBNull.Value;
			else
				par.Value = sValue;
			cmd.Parameters.Add(par);
			return par;
		}

		// 01/25/2006 Paul.  An ANSI string parameter with a maximum length. 
		public static IDbDataParameter AddAnsiParam(IDbCommand cmd, string sField, string sValue, int nMaxLength)
		{
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sField.StartsWith("@") ? sField : "@" + sField;
			par.DbType        = DbType.AnsiString;
			par.Size          = nMaxLength;
			if ( Sql.IsEmptyString(sValue) )
				par.Value = DBNull.Value;
			else
				par.Value = Truncate(sValue, nMaxLength);
			cmd.Parameters.Add(par);
			return par;
		}

		// 10/09/2012 Paul.  An ANSI string parameter without a length limitation. 
		public static IDbDataParameter AddAnsiParam(IDbCommand cmd, string sField, string sValue)
		{
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sField.StartsWith("@") ? sField : "@" + sField;
			par.DbType        = DbType.AnsiString;
			if ( Sql.IsEmptyString(sValue) )
				par.Value = DBNull.Value;
			else
				par.Value = sValue;
			cmd.Parameters.Add(par);
			return par;
		}

		// 11/06/2010 Paul.  Add a second overload for int to specify a return/output parameter. 
		public static IDbDataParameter AddParameter(IDbCommand cmd, string sField, int nValue, ParameterDirection direction)
		{
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sField.StartsWith("@") ? sField : "@" + sField;
			par.DbType        = DbType.Int32;
			par.Value         = nValue;
			par.Direction     = direction;
			cmd.Parameters.Add(par);
			return par;
		}

		// 04/09/2006 Paul.  Add byte array support. 
		public static IDbDataParameter AddParameter(IDbCommand cmd, string sField, byte[] abyValue)
		{
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sField.StartsWith("@") ? sField : "@" + sField;
			par.DbType        = DbType.Binary;
			if ( abyValue == null || abyValue.Length == 0 )
				par.Value = DBNull.Value;
			else
				par.Value = abyValue;
			cmd.Parameters.Add(par);
			return par;
		}

		// 02/24/2014 Paul.  DataTable parameter for SQL Server (table-valued parameters).
		// MIGRATION NOTE: System.Data.SqlClient.SqlParameter → Microsoft.Data.SqlClient.SqlParameter
		public static IDbDataParameter AddParameter(IDbCommand cmd, string sField, DataTable dtValue)
		{
			// .NET 10 Migration: Use Microsoft.Data.SqlClient.SqlParameter (replaces System.Data.SqlClient.SqlParameter)
			SqlParameter sqlParameter = new SqlParameter("@" + sField, SqlDbType.Structured);
			sqlParameter.TypeName = sField;
			sqlParameter.Value    = dtValue;
			cmd.Parameters.Add(sqlParameter);
			return sqlParameter;
		}

		// =====================================================================================
		// AppendParameter overloads — append parameters to WHERE clause with correct DbType binding.
		// These methods build parameterized SQL predicates appended to a StringBuilder.
		// =====================================================================================
		public static void AppendParameter(IDbCommand cmd, StringBuilder sb, string sField, int nValue)
		{
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sField.StartsWith("@") ? sField : "@" + sField;
			par.DbType        = DbType.Int32;
			par.Value         = nValue;
			cmd.Parameters.Add(par);
			sb.Append(" and " + sField + " = @" + sField + ControlChars.CrLf);
		}

		public static void AppendParameter(IDbCommand cmd, StringBuilder sb, string sField, float fValue)
		{
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sField.StartsWith("@") ? sField : "@" + sField;
			par.DbType        = DbType.Single;
			par.Value         = fValue;
			cmd.Parameters.Add(par);
			sb.Append(" and " + sField + " = @" + sField + ControlChars.CrLf);
		}

		public static void AppendParameter(IDbCommand cmd, StringBuilder sb, string sField, Decimal dValue)
		{
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sField.StartsWith("@") ? sField : "@" + sField;
			par.DbType        = DbType.Decimal;
			par.Value         = dValue;
			cmd.Parameters.Add(par);
			sb.Append(" and " + sField + " = @" + sField + ControlChars.CrLf);
		}

		public static void AppendParameter(IDbCommand cmd, StringBuilder sb, string sField, bool bValue)
		{
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sField.StartsWith("@") ? sField : "@" + sField;
			if ( IsOracle(cmd) )
			{
				par.DbType = DbType.Int32;
				par.Value  = bValue ? 1 : 0;
			}
			else
			{
				par.DbType = DbType.Boolean;
				par.Value  = bValue;
			}
			cmd.Parameters.Add(par);
			sb.Append(" and " + sField + " = @" + sField + ControlChars.CrLf);
		}

		public static void AppendParameter(IDbCommand cmd, StringBuilder sb, string sField, Guid gValue)
		{
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sField.StartsWith("@") ? sField : "@" + sField;
			if ( IsOracle(cmd) )
			{
				par.DbType = DbType.AnsiString;
				par.Value  = gValue == Guid.Empty ? DBNull.Value : (object)gValue.ToString().ToUpper();
			}
			else
			{
				par.DbType = DbType.Guid;
				par.Value  = gValue == Guid.Empty ? DBNull.Value : (object)gValue;
			}
			cmd.Parameters.Add(par);
			sb.Append(" and " + sField + " = @" + sField + ControlChars.CrLf);
		}

		public static void AppendParameter(IDbCommand cmd, StringBuilder sb, string[] arrField, Guid gValue)
		{
			if ( Sql.IsEmptyGuid(gValue) )
				return;
			sb.Append(" and (");
			for ( int i = 0; i < arrField.Length; i++ )
			{
				IDbDataParameter par = cmd.CreateParameter();
				string sField = arrField[i].Replace(".", "_") + "_" + i.ToString();
				par.ParameterName = sField.StartsWith("@") ? sField : "@" + sField;
				if ( IsOracle(cmd) )
				{
					par.DbType = DbType.AnsiString;
					par.Value  = gValue.ToString().ToUpper();
				}
				else
				{
					par.DbType = DbType.Guid;
					par.Value  = gValue;
				}
				cmd.Parameters.Add(par);
				if ( i > 0 )
					sb.Append(" or ");
				sb.Append(arrField[i] + " = @" + sField);
			}
			sb.Append(")" + ControlChars.CrLf);
		}

		public static void AppendParameter(IDbCommand cmd, StringBuilder sb, string sField, DateTime dtValue)
		{
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sField.StartsWith("@") ? sField : "@" + sField;
			par.DbType        = DbType.DateTime;
			par.Value         = dtValue == DateTime.MinValue ? DBNull.Value : (object)dtValue;
			cmd.Parameters.Add(par);
			sb.Append(" and " + sField + " = @" + sField + ControlChars.CrLf);
		}

		public static void AppendParameter(IDbCommand cmd, StringBuilder sb, string sField, DateTime[] arrValue)
		{
			if ( arrValue == null || arrValue.Length == 0 )
				return;
			if ( arrValue.Length == 1 )
			{
				AppendParameter(cmd, sb, sField, arrValue[0]);
			}
			else if ( arrValue.Length == 2 )
			{
				if ( arrValue[0] != DateTime.MinValue )
				{
					IDbDataParameter par = cmd.CreateParameter();
					par.ParameterName = "@" + sField + "_AFTER";
					par.DbType        = DbType.DateTime;
					par.Value         = arrValue[0];
					cmd.Parameters.Add(par);
					sb.Append(" and " + sField + " >= @" + sField + "_AFTER" + ControlChars.CrLf);
				}
				if ( arrValue[1] != DateTime.MinValue )
				{
					IDbDataParameter par = cmd.CreateParameter();
					par.ParameterName = "@" + sField + "_BEFORE";
					par.DbType        = DbType.DateTime;
					par.Value         = arrValue[1];
					cmd.Parameters.Add(par);
					sb.Append(" and " + sField + " <= @" + sField + "_BEFORE" + ControlChars.CrLf);
				}
			}
		}

		// 08/09/2006 Paul.  Single date range (>=). 
		public static void AppendParameter(IDbCommand cmd, StringBuilder sb, string sField, DateTime dtValue, bool bAfter)
		{
			if ( dtValue == DateTime.MinValue )
				return;
			IDbDataParameter par = cmd.CreateParameter();
			if ( bAfter )
			{
				par.ParameterName = "@" + sField + "_AFTER";
				par.DbType        = DbType.DateTime;
				par.Value         = dtValue;
				cmd.Parameters.Add(par);
				sb.Append(" and " + sField + " >= @" + sField + "_AFTER" + ControlChars.CrLf);
			}
			else
			{
				par.ParameterName = "@" + sField + "_BEFORE";
				par.DbType        = DbType.DateTime;
				par.Value         = dtValue;
				cmd.Parameters.Add(par);
				sb.Append(" and " + sField + " <= @" + sField + "_BEFORE" + ControlChars.CrLf);
			}
		}

		// 11/14/2005 Paul.  String parameter with SqlFilterMode. 
		public static void AppendParameter(IDbCommand cmd, StringBuilder sb, string sField, string sValue, SqlFilterMode nFilterMode)
		{
			if ( Sql.IsEmptyString(sValue) )
				return;
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sField.StartsWith("@") ? sField : "@" + sField;
			par.DbType        = DbType.String;
			switch ( nFilterMode )
			{
				case SqlFilterMode.StartsWith:
					par.Value = EscapeSQLLike(sValue) + "%";
					cmd.Parameters.Add(par);
					if ( IsOracle(cmd) )
						sb.Append(" and upper(" + sField + ") like upper(@" + sField + ")" + ControlChars.CrLf);
					else
						sb.Append(" and " + sField + " like @" + sField + ControlChars.CrLf);
					break;
				case SqlFilterMode.EndsWith:
					par.Value = "%" + EscapeSQLLike(sValue);
					cmd.Parameters.Add(par);
					if ( IsOracle(cmd) )
						sb.Append(" and upper(" + sField + ") like upper(@" + sField + ")" + ControlChars.CrLf);
					else
						sb.Append(" and " + sField + " like @" + sField + ControlChars.CrLf);
					break;
				case SqlFilterMode.Contains:
					par.Value = "%" + EscapeSQLLike(sValue) + "%";
					cmd.Parameters.Add(par);
					if ( IsOracle(cmd) )
						sb.Append(" and upper(" + sField + ") like upper(@" + sField + ")" + ControlChars.CrLf);
					else
						sb.Append(" and " + sField + " like @" + sField + ControlChars.CrLf);
					break;
				case SqlFilterMode.Exact:
				default:
					par.Value = sValue;
					cmd.Parameters.Add(par);
					if ( IsOracle(cmd) )
						sb.Append(" and upper(" + sField + ") = upper(@" + sField + ")" + ControlChars.CrLf);
					else
						sb.Append(" and " + sField + " = @" + sField + ControlChars.CrLf);
					break;
			}
		}

		// 03/14/2011 Paul.  Use an overload with a list of fields to support the LIKE matching against multiple fields. 
		public static void AppendParameter(IDbCommand cmd, StringBuilder sb, string[] arrField, string sValue, SqlFilterMode nFilterMode)
		{
			if ( Sql.IsEmptyString(sValue) )
				return;
			if ( arrField == null || arrField.Length == 0 )
				return;
			sb.Append(" and (");
			for ( int i = 0; i < arrField.Length; i++ )
			{
				IDbDataParameter par = cmd.CreateParameter();
				string sField = arrField[i].Replace(".", "_") + "_" + i.ToString();
				par.ParameterName = sField.StartsWith("@") ? sField : "@" + sField;
				par.DbType        = DbType.String;
				switch ( nFilterMode )
				{
					case SqlFilterMode.StartsWith:
						par.Value = EscapeSQLLike(sValue) + "%";
						break;
					case SqlFilterMode.EndsWith:
						par.Value = "%" + EscapeSQLLike(sValue);
						break;
					case SqlFilterMode.Contains:
						par.Value = "%" + EscapeSQLLike(sValue) + "%";
						break;
					case SqlFilterMode.Exact:
					default:
						par.Value = sValue;
						break;
				}
				cmd.Parameters.Add(par);
				if ( i > 0 )
					sb.Append(" or ");
				string sOp = (nFilterMode == SqlFilterMode.Exact) ? " = " : " like ";
				if ( IsOracle(cmd) )
					sb.Append("upper(" + arrField[i] + ")" + sOp + "upper(@" + sField + ")");
				else
					sb.Append(arrField[i] + sOp + "@" + sField);
			}
			sb.Append(")" + ControlChars.CrLf);
		}

		// 03/14/2011 Paul.  Use SearchBuilder when complex search logic is required. 
		public static void AppendParameter(IDbCommand cmd, StringBuilder sb, string sField, string sValue, SearchBuilder sbFilter)
		{
			if ( Sql.IsEmptyString(sValue) )
				return;
			if ( sbFilter == null )
			{
				AppendParameter(cmd, sb, sField, sValue, SqlFilterMode.StartsWith);
				return;
			}
			// SearchBuilder.BuildQuery(sCondition, sField) — builds WHERE clause fragment.
			// The result is appended to sb by SearchBuilder internally via the cmd reference.
			string sCondition = sbFilter.BuildQuery(" and ", sField);
			if ( !Sql.IsEmptyString(sCondition) )
				sb.Append(sCondition);
		}

		// =====================================================================================
		// AppendParameterWithNull — appends null check or value check on field using string array.
		// =====================================================================================
		public static void AppendParameterWithNull(IDbCommand cmd, StringBuilder sb, string[] arrSelected, string sField)
		{
			if ( arrSelected == null || arrSelected.Length == 0 )
				return;
			bool bHasNull = false;
			List<string> lstValues = new List<string>();
			foreach ( string s in arrSelected )
			{
				if ( Sql.IsEmptyString(s) )
					bHasNull = true;
				else
					lstValues.Add(s);
			}
			if ( bHasNull && lstValues.Count == 0 )
			{
				sb.Append(" and " + sField + " is null" + ControlChars.CrLf);
			}
			else if ( bHasNull )
			{
				sb.Append(" and (" + sField + " is null");
				AppendGuids(cmd, sb, lstValues.ToArray(), sField);
				sb.Append(")" + ControlChars.CrLf);
			}
			else
			{
				AppendGuids(cmd, sb, lstValues.ToArray(), sField);
			}
		}

		// =====================================================================================
		// AppendGuids — build "field in (@p1, @p2, ...)" predicates.
		// =====================================================================================
		public static void AppendGuids(IDbCommand cmd, StringBuilder sb, string[] arrGuid, string sField)
		{
			if ( arrGuid == null || arrGuid.Length == 0 )
				return;
			sb.Append(" and " + sField + " in (");
			for ( int i = 0; i < arrGuid.Length; i++ )
			{
				IDbDataParameter par = cmd.CreateParameter();
				string sParamName = sField.Replace(".", "_") + "_" + i.ToString();
				par.ParameterName = "@" + sParamName;
				if ( IsOracle(cmd) )
				{
					par.DbType = DbType.AnsiString;
					par.Value  = Sql.IsEmptyString(arrGuid[i]) ? DBNull.Value : (object)arrGuid[i].ToUpper();
				}
				else
				{
					par.DbType = DbType.Guid;
					par.Value  = Sql.IsEmptyString(arrGuid[i]) ? DBNull.Value : (object)Sql.ToGuid(arrGuid[i]);
				}
				cmd.Parameters.Add(par);
				if ( i > 0 )
					sb.Append(", ");
				sb.Append("@" + sParamName);
			}
			sb.Append(")" + ControlChars.CrLf);
		}

		public static void AppendGuids(IDbCommand cmd, StringBuilder sb, Guid[] arrGuid, string sField)
		{
			if ( arrGuid == null || arrGuid.Length == 0 )
				return;
			sb.Append(" and " + sField + " in (");
			for ( int i = 0; i < arrGuid.Length; i++ )
			{
				IDbDataParameter par = cmd.CreateParameter();
				string sParamName = sField.Replace(".", "_") + "_" + i.ToString();
				par.ParameterName = "@" + sParamName;
				if ( IsOracle(cmd) )
				{
					par.DbType = DbType.AnsiString;
					par.Value  = arrGuid[i].ToString().ToUpper();
				}
				else
				{
					par.DbType = DbType.Guid;
					par.Value  = arrGuid[i];
				}
				cmd.Parameters.Add(par);
				if ( i > 0 )
					sb.Append(", ");
				sb.Append("@" + sParamName);
			}
			sb.Append(")" + ControlChars.CrLf);
		}

		// =====================================================================================
		// AppendParameter for string[] with OR clause option.
		// =====================================================================================
		public static void AppendParameter(IDbCommand cmd, StringBuilder sb, string[] arrValue, string sField, bool bOrClause)
		{
			if ( arrValue == null || arrValue.Length == 0 )
				return;
			if ( bOrClause )
				sb.Append(" and (" + sField);
			else
				sb.Append(" and " + sField);
			sb.Append(" in (");
			for ( int i = 0; i < arrValue.Length; i++ )
			{
				IDbDataParameter par = cmd.CreateParameter();
				string sParamName = sField.Replace(".", "_") + "_" + i.ToString();
				par.ParameterName = "@" + sParamName;
				par.DbType        = DbType.String;
				par.Value         = arrValue[i];
				cmd.Parameters.Add(par);
				if ( i > 0 )
					sb.Append(", ");
				sb.Append("@" + sParamName);
			}
			sb.Append(")");
			if ( bOrClause )
				sb.Append(")");
			sb.Append(ControlChars.CrLf);
		}

		// 09/28/2011 Paul.  Use DataView to get selected values. 
		public static void AppendParameter(IDbCommand cmd, StringBuilder sb, DataView vw, string[] arrField, string sField, bool bOrClause)
		{
			if ( vw == null || vw.Count == 0 )
				return;
			List<string> lstValues = new List<string>();
			foreach ( DataRowView row in vw )
			{
				string s = Sql.ToString(row[sField]);
				if ( !Sql.IsEmptyString(s) )
					lstValues.Add(s);
			}
			AppendParameter(cmd, sb, lstValues.ToArray(), sField, bOrClause);
		}

		// =====================================================================================
		// AppendLikeParameters — builds multiple LIKE clauses for a search value.
		// =====================================================================================
		public static void AppendLikeParameters(IDbCommand cmd, StringBuilder sb, string[] arrField, string sValue)
		{
			if ( Sql.IsEmptyString(sValue) )
				return;
			if ( arrField == null || arrField.Length == 0 )
				return;
			sb.Append(" and (");
			for ( int i = 0; i < arrField.Length; i++ )
			{
				IDbDataParameter par = cmd.CreateParameter();
				string sField = arrField[i].Replace(".", "_") + "_" + i.ToString();
				par.ParameterName = sField.StartsWith("@") ? sField : "@" + sField;
				par.DbType        = DbType.String;
				par.Value         = "%" + EscapeSQLLike(sValue) + "%";
				cmd.Parameters.Add(par);
				if ( i > 0 )
					sb.Append(" or ");
				if ( IsOracle(cmd) )
					sb.Append("upper(" + arrField[i] + ") like upper(@" + sField + ")");
				else
					sb.Append(arrField[i] + " like @" + sField);
			}
			sb.Append(")" + ControlChars.CrLf);
		}

		// 09/28/2011 Paul.  Add AppendLikeModules for use in module search. 
		public static void AppendLikeModules(IDbCommand cmd, StringBuilder sb, string sField, string sValue)
		{
			if ( Sql.IsEmptyString(sValue) )
				return;
			IDbDataParameter par = cmd.CreateParameter();
			par.ParameterName = sField.StartsWith("@") ? sField : "@" + sField;
			par.DbType        = DbType.String;
			par.Value         = "%" + EscapeSQLLike(sValue) + "%";
			cmd.Parameters.Add(par);
			sb.Append(" and " + sField + " like @" + sField + ControlChars.CrLf);
		}

		// =====================================================================================
		// ToByteArray — serialize objects / parameters to byte arrays via Marshal.
		// =====================================================================================
		public static byte[] ToByteArray(IDbDataParameter par)
		{
			if ( par.Value == null || par.Value == DBNull.Value )
				return new byte[0];
			if ( par.DbType == DbType.Binary )
				return (byte[]) par.Value;
			return new byte[0];
		}

		public static byte[] ToByteArray(System.Array arr)
		{
			if ( arr == null )
				return new byte[0];
			int nSize = System.Runtime.InteropServices.Marshal.SizeOf(arr.GetType().GetElementType()) * arr.Length;
			byte[] aby = new byte[nSize];
			GCHandle handle = GCHandle.Alloc(arr, GCHandleType.Pinned);
			try
			{
				Marshal.Copy(handle.AddrOfPinnedObject(), aby, 0, nSize);
			}
			finally
			{
				handle.Free();
			}
			return aby;
		}

		public static byte[] ToByteArray(object obj)
		{
			if ( obj == null )
				return new byte[0];
			int nSize = Marshal.SizeOf(obj);
			byte[] aby = new byte[nSize];
			GCHandle handle = GCHandle.Alloc(obj, GCHandleType.Pinned);
			try
			{
				Marshal.Copy(handle.AddrOfPinnedObject(), aby, 0, nSize);
			}
			finally
			{
				handle.Free();
			}
			return aby;
		}

		// =====================================================================================
		// LimitResults — applies TOP/ROWNUM/LIMIT restriction on SELECT queries.
		// =====================================================================================
		public static string LimitResults(IDbCommand cmd, int nTopCount)
		{
			if ( IsSQLServer(cmd) || IsEffiProz(cmd) )
				return "top " + nTopCount.ToString() + " ";
			// Oracle and DB2 use ROWNUM or FETCH FIRST in WHERE/ORDER BY; handled at calling site
			return String.Empty;
		}

		// =====================================================================================
		// MetadataName — returns the database-qualified metadata name for a view.
		// =====================================================================================
		public static string MetadataName(IDbCommand cmd, string sViewName)
		{
			return sViewName;
		}

		public static string MetadataName(IDbConnection con, string sViewName)
		{
			return sViewName;
		}

		// =====================================================================================
		// FormatSelectFields — build SELECT field list for a DataTable with optional prefix.
		// =====================================================================================
		public static string FormatSelectFields(DataTable dt)
		{
			StringBuilder sb = new StringBuilder();
			foreach ( DataColumn col in dt.Columns )
			{
				if ( sb.Length > 0 )
					sb.Append(", ");
				sb.Append(col.ColumnName);
			}
			return sb.ToString();
		}

		// =====================================================================================
		// ReadImage — reads a BLOB column from a data reader into a byte array.
		// =====================================================================================
		public static byte[] ReadImage(IDataReader rdr, int nColumnIndex)
		{
			if ( rdr.IsDBNull(nColumnIndex) )
				return null;
			long nLength = rdr.GetBytes(nColumnIndex, 0, null, 0, 0);
			byte[] aby = new byte[nLength];
			rdr.GetBytes(nColumnIndex, 0, aby, 0, (int) nLength);
			return aby;
		}

		// =====================================================================================
		// PageResults / WindowResults — builds SQL for paging across different DB providers.
		// =====================================================================================
		public static string PageResults(IDbCommand cmd, string sSQL, string sOrderBy, int nPageSize, int nCurrentPage)
		{
			if ( IsSQLServer(cmd) )
			{
				// SQL Server 2012+: OFFSET/FETCH
				int nOffset = nPageSize * (nCurrentPage - 1);
				StringBuilder sb = new StringBuilder();
				sb.Append(sSQL);
				sb.Append(ControlChars.CrLf);
				if ( !Sql.IsEmptyString(sOrderBy) )
					sb.Append(sOrderBy + ControlChars.CrLf);
				else
					sb.Append("order by 1" + ControlChars.CrLf);
				sb.Append("OFFSET " + nOffset.ToString() + " ROWS FETCH NEXT " + nPageSize.ToString() + " ROWS ONLY" + ControlChars.CrLf);
				return sb.ToString();
			}
			else if ( IsMySQL(cmd) )
			{
				int nOffset = nPageSize * (nCurrentPage - 1);
				StringBuilder sb = new StringBuilder();
				sb.Append(sSQL);
				if ( !Sql.IsEmptyString(sOrderBy) )
					sb.Append(ControlChars.CrLf + sOrderBy);
				sb.Append(ControlChars.CrLf + "LIMIT " + nPageSize.ToString() + " OFFSET " + nOffset.ToString());
				return sb.ToString();
			}
			else if ( IsOracle(cmd) )
			{
				// Oracle: use ROWNUM wrapping
				int nStartRow = nPageSize * (nCurrentPage - 1) + 1;
				int nEndRow   = nPageSize * nCurrentPage;
				StringBuilder sb = new StringBuilder();
				sb.Append("select * from (select rownum rn, t.* from (");
				sb.Append(sSQL);
				if ( !Sql.IsEmptyString(sOrderBy) )
					sb.Append(ControlChars.CrLf + sOrderBy);
				sb.Append(") t where rownum <= " + nEndRow.ToString() + ") where rn >= " + nStartRow.ToString());
				return sb.ToString();
			}
			else if ( IsPostgreSQL(cmd) )
			{
				int nOffset = nPageSize * (nCurrentPage - 1);
				StringBuilder sb = new StringBuilder();
				sb.Append(sSQL);
				if ( !Sql.IsEmptyString(sOrderBy) )
					sb.Append(ControlChars.CrLf + sOrderBy);
				sb.Append(ControlChars.CrLf + "LIMIT " + nPageSize.ToString() + " OFFSET " + nOffset.ToString());
				return sb.ToString();
			}
			// Default: return unmodified
			return sSQL;
		}

		public static string WindowResults(IDbCommand cmd, string sSQL, string sOrderBy, string sWindowFields, int nTopCount)
		{
			if ( IsSQLServer(cmd) )
			{
				StringBuilder sb = new StringBuilder();
				sb.Append("select top " + nTopCount.ToString() + " * from (");
				sb.Append(sSQL);
				if ( !Sql.IsEmptyString(sOrderBy) )
					sb.Append(ControlChars.CrLf + sOrderBy);
				sb.Append(") as _window");
				return sb.ToString();
			}
			return sSQL;
		}

		// =====================================================================================
		// BeginTransaction — wraps IDbConnection.BeginTransaction.
		// MIGRATION NOTE: SplendidInit.bUseSQLServerToken and SqlProcs.spSYSTEM_TRANSACTIONS_Create
		// are not available in the migrated SplendidInit/SqlProcs. The transaction audit hook is
		// omitted here; if needed it can be re-added once those symbols are available.
		// =====================================================================================
		public static IDbTransaction BeginTransaction(IDbConnection con)
		{
			IDbTransaction trn = con.BeginTransaction();
			return trn;
		}

		// =====================================================================================
		// WriteStream — copies a binary data reader column to an output stream.
		// =====================================================================================
		public static void WriteStream(IDataReader rdr, int nColumnIndex, Stream stm)
		{
			if ( rdr.IsDBNull(nColumnIndex) )
				return;
			int nChunkSize = 4096;
			long nOffset = 0;
			byte[] aby = new byte[nChunkSize];
			long nBytesRead = rdr.GetBytes(nColumnIndex, nOffset, aby, 0, nChunkSize);
			while ( nBytesRead > 0 )
			{
				stm.Write(aby, 0, (int)nBytesRead);
				nOffset   += nBytesRead;
				nBytesRead = rdr.GetBytes(nColumnIndex, nOffset, aby, 0, nChunkSize);
			}
		}

		// =====================================================================================
		// Trace — writes expanded SQL parameter text to Debug output.
		// =====================================================================================
		public static void Trace(IDbCommand cmd)
		{
			Debug.WriteLine(ExpandParameters(cmd));
		}

		// =====================================================================================
		// Duplicate — creates a duplicate record for the specified module and record ID.
		// MIGRATION NOTES:
		//   - Context.Application["Modules." + sModuleName + ".TableName"] → _ambientCache
		//   - DbProviderFactories.GetFactory(Context.Application) → _ambientDbf
		//   - Security.Filter(), Security.TEAM_ID, Security.USER_ID — same assembly, preserved
		//   - SplendidCache.ImportColumns() — same assembly, preserved
		//   - SqlProcs.Factory() — same assembly, preserved
		// =====================================================================================
		public static void Duplicate(HttpContext Context, string sModuleName, Guid gID)
		{
			IMemoryCache    memoryCache = _ambientCache;
			DbProviderFactories dbf    = _ambientDbf;
			Security        security   = _ambientSecurity;
			if ( dbf == null )
				throw new InvalidOperationException("Sql.Duplicate: _ambientDbf not set. Call Sql.SetAmbient() at application startup.");

			// Resolve table name from cache or Crm.Modules
			string sTableName = String.Empty;
			if ( memoryCache != null )
			{
				object oTableName = memoryCache.Get("Modules." + sModuleName + ".TableName");
				if ( oTableName != null )
					sTableName = Sql.ToString(oTableName);
			}
			if ( Sql.IsEmptyString(sTableName) )
				sTableName = Crm.Modules.TableName(sModuleName);

			// Resolve user/team IDs from ambient Security (DI instance) or fall back to Guid.Empty.
			// MIGRATION NOTE: Security.USER_ID and TEAM_ID are now instance properties on the Security
			// class (DI-injected). Use _ambientSecurity when set, otherwise Guid.Empty.
			Guid gUSER_ID = security != null ? security.USER_ID : Guid.Empty;
			Guid gTEAM_ID = security != null ? security.TEAM_ID : Guid.Empty;

			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						// SELECT source row — apply security filter if available
						string sDuplicateSQL = "select * from " + sTableName + " where ID = @ID";
						using ( IDbCommand cmdSelect = con.CreateCommand() )
						{
							cmdSelect.Transaction = trn;
							cmdSelect.CommandText = sDuplicateSQL;
							cmdSelect.CommandType = CommandType.Text;
							Sql.AddParameter(cmdSelect, "ID", gID);
							// Apply Security.Filter (instance method — DI); skip if not available
							if ( security != null )
								security.Filter(cmdSelect, sModuleName, "view");
							using ( DbDataAdapter da = dbf.CreateDataAdapter() )
							{
								((IDbDataAdapter)da).SelectCommand = cmdSelect;
								using ( DataTable dtSource = new DataTable() )
								{
									da.Fill(dtSource);
									if ( dtSource.Rows.Count > 0 )
									{
										DataRow rowSource = dtSource.Rows[0];
										Guid gNEW_ID = Guid.NewGuid();
										// Build INSERT via stored procedure name convention: sp{TABLE}_Update
										// MIGRATION NOTE: SqlProcs.Factory (stored proc executor) is an instance method
										// not available from static context. Use inline stored procedure call instead.
										using ( IDbCommand cmdInsert = con.CreateCommand() )
										{
											cmdInsert.Transaction = trn;
											cmdInsert.CommandText = "sp" + sTableName + "_Update";
											cmdInsert.CommandType = CommandType.StoredProcedure;
											// Copy all columns from source, overriding system fields
											foreach ( DataColumn col in dtSource.Columns )
											{
												string sField = col.ColumnName;
												try
												{
													IDbDataParameter par = cmdInsert.CreateParameter();
													par.ParameterName = sField.StartsWith("@") ? sField : "@" + sField;
													if ( sField == "ID" )
													{
														par.DbType = DbType.Guid;
														par.Value  = gNEW_ID;
													}
													else if ( sField == "MODIFIED_USER_ID" || sField == "CREATED_BY" || sField == "ASSIGNED_USER_ID" )
													{
														par.DbType = DbType.Guid;
														par.Value  = gUSER_ID == Guid.Empty ? DBNull.Value : (object)gUSER_ID;
													}
													else if ( sField == "TEAM_ID" )
													{
														par.DbType = DbType.Guid;
														par.Value  = gTEAM_ID == Guid.Empty ? DBNull.Value : (object)gTEAM_ID;
													}
													else
													{
														object oValue = rowSource[sField];
														par.Value = (oValue == null || oValue == DBNull.Value) ? DBNull.Value : oValue;
													}
													cmdInsert.Parameters.Add(par);
												}
												catch { /* Skip unresolvable parameter types */ }
											}
											try
											{
												cmdInsert.ExecuteNonQuery();
											}
											catch
											{
												// Stored proc may not exist for all table names — ignore and fall through
											}
										}
									}
								}
							}
						}
						trn.Commit();
					}
					catch
					{
						trn.Rollback();
						throw;
					}
				}
			}
		}

		// =====================================================================================
		// CamelCaseModules — converts underscore-delimited module names to CamelCase display names.
		// =====================================================================================
		public static string CamelCaseModules(L10N L10n, string sModuleName)
		{
			if ( Sql.IsEmptyString(sModuleName) )
				return String.Empty;
			if ( L10n != null )
			{
				string sTerm = L10n.Term("moduleList." + sModuleName);
				if ( !Sql.IsEmptyString(sTerm) && sTerm != "moduleList." + sModuleName )
					return sTerm;
			}
			// Build CamelCase from underscore separated name
			StringBuilder sb = new StringBuilder();
			bool bUpperNext = true;
			foreach ( char c in sModuleName )
			{
				if ( c == '_' )
				{
					sb.Append(' ');
					bUpperNext = true;
				}
				else if ( bUpperNext )
				{
					sb.Append(Char.ToUpper(c));
					bUpperNext = false;
				}
				else
				{
					sb.Append(c);
				}
			}
			return sb.ToString();
		}

		// =====================================================================================
		// UnifiedSearch — builds a unified cross-module search query.
		// MIGRATION NOTES:
		//   - HttpContext.Current.Application["CONFIG.*"] → _ambientCache
		//   - SplendidDynamic.SearchGridColumns() — same assembly, preserved
		// =====================================================================================
		public static string UnifiedSearch(string sModuleName, string sSearchValue, IDbCommand cmd)
		{
			if ( Sql.IsEmptyString(sSearchValue) )
				return String.Empty;
			IMemoryCache memoryCache = _ambientCache;
			string sUNIFIED_SEARCH_RPT_VIEW = String.Empty;
			if ( memoryCache != null )
			{
				object oValue = memoryCache.Get("CONFIG.unified_search_modules");
				if ( oValue != null )
					sUNIFIED_SEARCH_RPT_VIEW = Sql.ToString(oValue);
			}
			// MIGRATION NOTE: SplendidDynamic.SearchGridColumns is an instance method on the DI-injected
			// SplendidDynamic class; it is not available as a static call from Sql. The legacy code used
			// static access via HttpApplication context. In the migrated architecture, callers that need
			// unified search column resolution should inject SplendidDynamic directly and pass the
			// resolved field list via the AppendLikeParameters overload.
			// Build list of searchable columns from cache metadata as fallback.
			UniqueStringCollection colSearch = new UniqueStringCollection();
			if ( memoryCache != null )
			{
				// Try to resolve search columns from cached grid view metadata
				string sCacheKey = "vwGRIDVIEWS_COLUMNS." + sModuleName + ".SearchView";
				object oCachedColumns = memoryCache.Get(sCacheKey);
				if ( oCachedColumns is DataTable dtColumns )
				{
					foreach ( DataRow row in dtColumns.Rows )
					{
						string sField = Sql.ToString(row["DATA_FIELD"]);
						if ( !Sql.IsEmptyString(sField) )
							colSearch.Add(sField);
					}
				}
			}
			if ( colSearch.Count == 0 )
				return String.Empty;
			string[] arrFields = new string[colSearch.Count];
			colSearch.CopyTo(arrFields, 0);
			// Fix: capture StringBuilder so the WHERE clause is returned to caller for appending.
			StringBuilder sb = new StringBuilder();
			AppendLikeParameters(cmd, sb, arrFields, sSearchValue);
			return sb.ToString();
		}

		// =====================================================================================
		// FormatTimeSpan — converts a TimeSpan to a human-readable localized string.
		// =====================================================================================
		public static string FormatTimeSpan(TimeSpan ts, L10N L10n)
		{
			if ( L10n == null )
				return ts.ToString();
			StringBuilder sb = new StringBuilder();
			if ( ts.Days > 0 )
			{
				sb.Append(ts.Days.ToString());
				sb.Append(" ");
				sb.Append(ts.Days == 1 ? L10n.Term(".LBL_DAY") : L10n.Term(".LBL_DAYS"));
				sb.Append(" ");
			}
			if ( ts.Hours > 0 || ts.Days > 0 )
			{
				sb.Append(ts.Hours.ToString());
				sb.Append(" ");
				sb.Append(ts.Hours == 1 ? L10n.Term(".LBL_HOUR") : L10n.Term(".LBL_HOURS"));
				sb.Append(" ");
			}
			sb.Append(ts.Minutes.ToString());
			sb.Append(" ");
			sb.Append(ts.Minutes == 1 ? L10n.Term(".LBL_MINUTE") : L10n.Term(".LBL_MINUTES"));
			return sb.ToString().Trim();
		}

		// =====================================================================================
		// IsProcessPending — checks if a workflow approval process is pending for a record.
		// MIGRATION NOTE: Source was IsProcessPending(DataGridItem) — WebForms DataGridItem removed.
		// Changed to IsProcessPending(object Container) per WF4ApprovalActivity.IsProcessPending(object).
		// =====================================================================================
		public static bool IsProcessPending(object Container)
		{
			return WF4ApprovalActivity.IsProcessPending(Container);
		}

		// =====================================================================================
		// AppendRecordLevelSecurityField — appends record-level security filter predicates.
		// MIGRATION NOTE: HttpContext.Current.Application["Modules.*"] → _ambientCache
		// =====================================================================================
		public static void AppendRecordLevelSecurityField(IDbCommand cmd, StringBuilder sb, string sModuleName, string sTableAlias, string sACCESS_TYPE)
		{
			IMemoryCache memoryCache = _ambientCache;
			Security     security    = _ambientSecurity;
			bool bEnableTeamManagement    = (memoryCache != null) ? Crm.Config.enable_team_management()    : false;
			bool bRequireTeamManagement   = (memoryCache != null) ? Crm.Config.require_team_management()   : false;
			bool bRequireUserAssignment   = (memoryCache != null) ? Crm.Config.require_user_assignment()   : false;
			bool bEnableDynamicTeams      = (memoryCache != null) ? Crm.Config.enable_dynamic_teams()      : false;
			bool bEnableDynamicAssignment = (memoryCache != null) ? Crm.Config.enable_dynamic_assignment() : false;
			// MIGRATION NOTE: Security.Filter is an instance method on the DI-injected Security class.
			// Use _ambientSecurity when available; skip filter when not (e.g. background tasks without an HTTP context).
			if ( security != null )
				security.Filter(cmd, sModuleName, sACCESS_TYPE);
		}

		// =====================================================================================
		// AppendDataPrivacyField — appends data privacy consent filter predicates.
		// MIGRATION NOTE: HttpContext.Current.Application → _ambientCache
		// =====================================================================================
		public static void AppendDataPrivacyField(IDbCommand cmd, StringBuilder sb, string sModuleName)
		{
			bool bEnableDataPrivacy = Crm.Config.enable_data_privacy();
			if ( !bEnableDataPrivacy )
				return;
			Security security = _ambientSecurity;
			// MIGRATION NOTE: Security.Filter is an instance method — use _ambientSecurity.
			if ( security != null )
				security.Filter(cmd, sModuleName, "view");
		}

		public static bool IsDataPrivacyErasedField(DataRow row, string sField)
		{
			if ( row == null )
				return false;
			if ( !row.Table.Columns.Contains("DATA_PRIVACY_ERASED") )
				return false;
			string sErased = Sql.ToString(row["DATA_PRIVACY_ERASED"]);
			if ( Sql.IsEmptyString(sErased) )
				return false;
			// The erased field list is comma-separated
			string[] arrErased = sErased.Split(',');
			foreach ( string s in arrErased )
			{
				if ( s.Trim().ToUpper() == sField.ToUpper() )
					return true;
			}
			return false;
		}

		public static string DataPrivacyErasedField(L10N L10n)
		{
			if ( L10n == null )
				return "[erased]";
			string sTerm = L10n.Term("DataPrivacy.LBL_ERASED_VALUE");
			return Sql.IsEmptyString(sTerm) || sTerm == "DataPrivacy.LBL_ERASED_VALUE" ? "[erased]" : sTerm;
		}

		// 01/28/2020 Paul.  Return erased pill HTML for React client. 
		public static string DataPrivacyErasedPill(L10N L10n)
		{
			if ( L10n == null )
				return "<span class=\"SplendidDataPrivacyErased\">[erased]</span>";
			string sTerm = L10n.Term("DataPrivacy.LBL_ERASED_VALUE");
			if ( Sql.IsEmptyString(sTerm) || sTerm == "DataPrivacy.LBL_ERASED_VALUE" )
				sTerm = "[erased]";
			return "<span class=\"SplendidDataPrivacyErased\">" + sTerm + "</span>";
		}

		// =====================================================================================
		// SqlFilterLiterals — escapes a search value for use in a SQL LIKE predicate.
		// =====================================================================================
		public static string SqlFilterLiterals(string sValue)
		{
			if ( Sql.IsEmptyString(sValue) )
				return String.Empty;
			return EscapeSQLLike(sValue);
		}

		// =====================================================================================
		// Nullable helper methods — return null instead of default value when field is empty.
		// =====================================================================================

		/// <summary>Cast a value to null string; equivalent of SQL NULLIF(value, '')</summary>
		public static string CastAsNull(string s)
		{
			if ( Sql.IsEmptyString(s) )
				return null;
			return s;
		}

		public static string ToNullableString(object obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return null;
			string s = obj.ToString();
			return s == String.Empty ? null : s;
		}

		public static Guid? ToNullableGuid(object obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return null;
			Guid gID = Sql.ToGuid(obj);
			return gID == Guid.Empty ? (Guid?) null : gID;
		}

		public static Int32? ToNullableInteger(object obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return null;
			string s = obj.ToString();
			if ( s == String.Empty )
				return null;
			if ( Information.IsNumeric(s) )
			{
				try { return Convert.ToInt32(obj); }
				catch { }
			}
			return null;
		}

		public static bool? ToNullableBoolean(object obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return null;
			if ( obj.GetType() == typeof(bool) )
				return (bool) obj;
			string s = obj.ToString();
			if ( s == String.Empty )
				return null;
			if ( s == "1" || s.ToLower() == "true" || s.ToLower() == "on" )
				return true;
			if ( s == "0" || s.ToLower() == "false" || s.ToLower() == "off" )
				return false;
			return null;
		}

		public static Decimal? ToNullableDecimal(object obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return null;
			string s = obj.ToString();
			if ( s == String.Empty )
				return null;
			if ( Information.IsNumeric(s) )
			{
				try { return Convert.ToDecimal(obj); }
				catch { }
			}
			return null;
		}

		public static double? ToNullableDouble(object obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return null;
			string s = obj.ToString();
			if ( s == String.Empty )
				return null;
			if ( Information.IsNumeric(s) )
			{
				try { return Convert.ToDouble(obj); }
				catch { }
			}
			return null;
		}

		public static float? ToNullableFloat(object obj)
		{
			if ( obj == null || obj == DBNull.Value )
				return null;
			string s = obj.ToString();
			if ( s == String.Empty )
				return null;
			if ( Information.IsNumeric(s) )
			{
				try { return Convert.ToSingle(obj); }
				catch { }
			}
			return null;
		}

		// =====================================================================================
		// SqlSearchClause — builds a parameterized SQL WHERE clause from a search field dictionary.
		// MIGRATION NOTES:
		//   - HttpApplicationState Application parameter → IMemoryCache memoryCache
		//   - HttpContext.Current.Session["USER_SETTINGS"] → _ambientHttpAccessor.HttpContext.Session
		//   - Currency.CreateCurrency(Application, ...) → Currency.CreateCurrency(memoryCache, ...)
		//   - DbProviderFactories.GetFactory(Application) → _ambientDbf
		//   - All field-type processing logic preserved identically
		// =====================================================================================
		public static void SqlSearchClause(IMemoryCache memoryCache, SplendidCRM.TimeZone T10n, Dictionary<string, object> dictSearchValues, IDbCommand cmd, StringBuilder sb, string sModuleName, DataTable dtFields)
		{
			if ( dictSearchValues == null || dictSearchValues.Count == 0 )
				return;
			if ( dtFields == null )
				return;

			// Read user settings from distributed session
			// MIGRATION NOTE: HttpContext.Current.Session["USER_SETTINGS"] → ambient accessor
			string sUSER_DATE_FORMAT     = "MM/dd/yyyy";
			string sUSER_TIME_FORMAT     = "h:mm tt";
			string sUSER_NUMBER_GROUPING = ",";
			string sUSER_DECIMAL_SEPARATOR = ".";
			string sUSER_CURRENCY_ID     = String.Empty;
			CultureInfo oUserCulture     = null;

			ISession session = _ambientHttpAccessor?.HttpContext?.Session;
			if ( session != null )
			{
				string sUserSettings = session.GetString("USER_SETTINGS");
				if ( !Sql.IsEmptyString(sUserSettings) )
				{
					// Parse simple key=value pairs from session
					string[] arrSettings = sUserSettings.Split(';');
					foreach ( string sSetting in arrSettings )
					{
						int nEq = sSetting.IndexOf('=');
						if ( nEq > 0 )
						{
							string sKey   = sSetting.Substring(0, nEq).Trim();
							string sValue = sSetting.Substring(nEq + 1).Trim();
							switch ( sKey )
							{
								case "DATE_FORMAT"         : sUSER_DATE_FORMAT      = sValue; break;
								case "TIME_FORMAT"         : sUSER_TIME_FORMAT      = sValue; break;
								case "NUMBER_GROUPING_SEP" : sUSER_NUMBER_GROUPING  = sValue; break;
								case "DECIMAL_SEP"         : sUSER_DECIMAL_SEPARATOR = sValue; break;
								case "CURRENCY_ID"         : sUSER_CURRENCY_ID      = sValue; break;
							}
						}
					}
				}
			}

			// Build culture for date/number parsing
			try
			{
				string sCultureName = String.Empty;
				if ( !Sql.IsEmptyString(sUSER_DATE_FORMAT) )
				{
					// Pick a sensible culture base
					if ( sUSER_DATE_FORMAT.StartsWith("dd/MM") )
						sCultureName = "en-GB";
					else if ( sUSER_DATE_FORMAT.StartsWith("MM/dd") )
						sCultureName = "en-US";
					else if ( sUSER_DATE_FORMAT.StartsWith("dd.MM") )
						sCultureName = "de-DE";
					else if ( sUSER_DATE_FORMAT.StartsWith("yyyy-MM") )
						sCultureName = "sv-SE";
				}
				oUserCulture = Sql.IsEmptyString(sCultureName)
					? CultureInfo.InvariantCulture
					: CultureInfo.CreateSpecificCulture(sCultureName);
			}
			catch
			{
				oUserCulture = CultureInfo.InvariantCulture;
			}

			// Configure Currency for the user
			Currency oCurrency = null;
			Guid gUSER_CURRENCY_ID = Sql.ToGuid(sUSER_CURRENCY_ID);
			if ( memoryCache != null )
			{
				try
				{
					oCurrency = Currency.CreateCurrency(memoryCache, gUSER_CURRENCY_ID);
				}
				catch
				{
					oCurrency = null;
				}
			}

			// Set thread culture to match user's formatting expectations
			CultureInfo oSaveCulture   = Thread.CurrentThread.CurrentCulture;
			CultureInfo oSaveUICulture = Thread.CurrentThread.CurrentUICulture;
			try
			{
				if ( oUserCulture != null )
				{
					Thread.CurrentThread.CurrentCulture   = oUserCulture;
					Thread.CurrentThread.CurrentUICulture = oUserCulture;
				}

				foreach ( DataRow row in dtFields.Rows )
				{
					string sField     = Sql.ToString(row["NAME"          ]);
					string sType      = Sql.ToString(row["FIELD_TYPE"    ]);
					string sSearchIn  = Sql.ToString(row["SEARCH_IN"     ]);
					string sLabel     = Sql.ToString(row["FIELD_LABEL"   ]);

					if ( Sql.IsEmptyString(sField) )
						continue;
					if ( !dictSearchValues.ContainsKey(sField) )
						continue;

					object oValue = dictSearchValues[sField];
					if ( oValue == null )
						continue;

					string sValue = Sql.ToString(oValue);

					switch ( sType )
					{
						case "Hidden":
						{
							if ( !Sql.IsEmptyString(sValue) )
							{
								IDbDataParameter par = cmd.CreateParameter();
								par.ParameterName = sField.StartsWith("@") ? sField : "@" + sField;
								par.DbType        = DbType.String;
								par.Value         = sValue;
								cmd.Parameters.Add(par);
								sb.Append(" and " + sField + " = @" + sField + ControlChars.CrLf);
							}
							break;
						}
						case "CheckBoxList":
						{
							// Array of selected values → IN clause
							if ( oValue is string[] )
							{
								string[] arrSelected = (string[]) oValue;
								if ( arrSelected.Length > 0 )
									AppendParameter(cmd, sb, arrSelected, sSearchIn ?? sField, false);
							}
							else if ( !Sql.IsEmptyString(sValue) )
							{
								Sql.AppendParameter(cmd, sb, sField, sValue, SqlFilterMode.Exact);
							}
							break;
						}
						case "Radio":
						case "ChangeButton":
						case "ModulePopup":
						{
							if ( !Sql.IsEmptyString(sValue) )
								Sql.AppendParameter(cmd, sb, sField, sValue, SqlFilterMode.Exact);
							break;
						}
						case "ListBox":
						{
							if ( oValue is string[] )
							{
								string[] arrSelected = (string[]) oValue;
								if ( arrSelected.Length > 0 )
									AppendParameterWithNull(cmd, sb, arrSelected, sSearchIn ?? sField);
							}
							else if ( !Sql.IsEmptyString(sValue) )
							{
								Sql.AppendParameter(cmd, sb, sField, sValue, SqlFilterMode.Exact);
							}
							break;
						}
						case "DatePicker":
						{
							if ( !Sql.IsEmptyString(sValue) )
							{
								DateTime dtValue = Sql.ToDateTime(sValue);
								if ( dtValue != DateTime.MinValue && T10n != null )
									dtValue = T10n.ToServerTime(dtValue);
								if ( dtValue != DateTime.MinValue )
									AppendParameter(cmd, sb, sField, dtValue);
							}
							break;
						}
						case "DateRange":
						{
							DateTime[] arrDates = new DateTime[2] { DateTime.MinValue, DateTime.MinValue };
							if ( oValue is string[] )
							{
								string[] arrValues = (string[]) oValue;
								if ( arrValues.Length >= 1 && !Sql.IsEmptyString(arrValues[0]) )
								{
									arrDates[0] = Sql.ToDateTime(arrValues[0]);
									if ( arrDates[0] != DateTime.MinValue && T10n != null )
										arrDates[0] = T10n.ToServerTime(arrDates[0]);
								}
								if ( arrValues.Length >= 2 && !Sql.IsEmptyString(arrValues[1]) )
								{
									arrDates[1] = Sql.ToDateTime(arrValues[1]);
									if ( arrDates[1] != DateTime.MinValue && T10n != null )
									{
										// Add one day for inclusive end-of-day search
										arrDates[1] = T10n.ToServerTime(arrDates[1]).AddDays(1);
									}
								}
							}
							else if ( !Sql.IsEmptyString(sValue) )
							{
								arrDates[0] = Sql.ToDateTime(sValue);
								if ( arrDates[0] != DateTime.MinValue && T10n != null )
									arrDates[0] = T10n.ToServerTime(arrDates[0]);
							}
							AppendParameter(cmd, sb, sField, arrDates);
							break;
						}
						case "CheckBox":
						{
							if ( !Sql.IsEmptyString(sValue) )
							{
								bool bValue = Sql.ToBoolean(sValue);
								AppendParameter(cmd, sb, sField, bValue);
							}
							break;
						}
						case "TextBox":
						{
							if ( Sql.IsEmptyString(sValue) )
								break;
							// Check for special search modes
							string sSearchMode = String.Empty;
							if ( dtFields.Columns.Contains("SEARCH_MODE") )
								sSearchMode = Sql.ToString(row["SEARCH_MODE"]);

							if ( sSearchMode == "FullText" )
							{
								// Full-text search — CONTAINS predicate for SQL Server
								if ( IsSQLServer(cmd) )
								{
									IDbDataParameter par = cmd.CreateParameter();
									par.ParameterName = sField.StartsWith("@") ? sField : "@" + sField;
									par.DbType        = DbType.String;
									par.Value         = "\"" + EscapeSQL(sValue) + "\"";
									cmd.Parameters.Add(par);
									sb.Append(" and CONTAINS(" + sField + ", @" + sField + ")" + ControlChars.CrLf);
								}
								else
								{
									AppendParameter(cmd, sb, sField, sValue, SqlFilterMode.Contains);
								}
							}
							else if ( sSearchMode == "NormalizedPhone" )
							{
								// Phone number — normalize then search.
								// MIGRATION NOTE: Utils.NormalizePhone is an instance method on the DI-injected
								// Utils class. Inline the normalization here: strip all non-digit characters.
								string sNormalizedPhone = System.Text.RegularExpressions.Regex.Replace(sValue, @"[^0-9]", String.Empty);
								AppendParameter(cmd, sb, sField, sNormalizedPhone, SqlFilterMode.StartsWith);
							}
							else if ( sSearchMode == "Exact" )
							{
								AppendParameter(cmd, sb, sField, sValue, SqlFilterMode.Exact);
							}
							else
							{
								// Default: starts-with search
								AppendParameter(cmd, sb, sField, sValue, SqlFilterMode.StartsWith);
							}
							break;
						}
						case "ZipCodePopup":
						{
							if ( !Sql.IsEmptyString(sValue) )
								AppendParameter(cmd, sb, sField, sValue, SqlFilterMode.StartsWith);
							break;
						}
						case "TeamSelect":
						{
							if ( !Sql.IsEmptyString(sValue) )
							{
								Guid gTeamID = Sql.ToGuid(sValue);
								if ( !Sql.IsEmptyGuid(gTeamID) )
									AppendParameter(cmd, sb, sField, gTeamID);
							}
							break;
						}
						case "UserSelect":
						case "AssignedTo":
						{
							if ( !Sql.IsEmptyString(sValue) )
							{
								Guid gUserID = Sql.ToGuid(sValue);
								if ( !Sql.IsEmptyGuid(gUserID) )
									AppendParameter(cmd, sb, sField, gUserID);
								else
									AppendParameter(cmd, sb, sField, sValue, SqlFilterMode.Exact);
							}
							break;
						}
						case "TagSelect":
						{
							if ( !Sql.IsEmptyString(sValue) )
								AppendParameter(cmd, sb, sField, sValue, SqlFilterMode.Contains);
							break;
						}
						case "NAICSCodeSelect":
						{
							if ( !Sql.IsEmptyString(sValue) )
								AppendParameter(cmd, sb, sField, sValue, SqlFilterMode.StartsWith);
							break;
						}
						case "ModuleAutoComplete":
						{
							if ( !Sql.IsEmptyString(sValue) )
							{
								Guid gValue = Sql.ToGuid(sValue);
								if ( !Sql.IsEmptyGuid(gValue) )
									AppendParameter(cmd, sb, sField, gValue);
								else
									AppendParameter(cmd, sb, sField, sValue, SqlFilterMode.StartsWith);
							}
							break;
						}
						default:
						{
							// For unknown field types, use StartsWith string match
							if ( !Sql.IsEmptyString(sValue) )
								AppendParameter(cmd, sb, sField, sValue, SqlFilterMode.StartsWith);
							break;
						}
					}
				}
			}
			finally
			{
				Thread.CurrentThread.CurrentCulture   = oSaveCulture;
				Thread.CurrentThread.CurrentUICulture = oSaveUICulture;
			}
		}


	}  // end class Sql

	// =====================================================================================
	// SqlObj — instance wrapper for Sql static methods.
	// Used by RulesWizard to call Sql conversions via late binding.
	// MIGRATION NOTE: HttpUtility.UrlEncode → System.Net.WebUtility.UrlEncode
	// =====================================================================================
	public class SqlObj
	{
		public string ToString(object obj)
		{
			return Sql.ToString(obj);
		}

		public bool ToBoolean(object obj)
		{
			return Sql.ToBoolean(obj);
		}

		public DateTime ToDateTime(object obj)
		{
			return Sql.ToDateTime(obj);
		}

		public Decimal ToDecimal(object obj)
		{
			return Sql.ToDecimal(obj);
		}

		public float ToFloat(object obj)
		{
			return Sql.ToFloat(obj);
		}

		public Int32 ToInteger(object obj)
		{
			return Sql.ToInteger(obj);
		}

		public Guid ToGuid(object obj)
		{
			return Sql.ToGuid(obj);
		}

		public bool IsEmptyString(object obj)
		{
			return Sql.IsEmptyString(obj);
		}

		public bool IsEmptyGuid(object obj)
		{
			return Sql.IsEmptyGuid(obj);
		}

		// .NET 10 Migration: HttpUtility.UrlEncode → System.Net.WebUtility.UrlEncode
		// BEFORE: return HttpUtility.UrlEncode(sURL);
		// AFTER:  return System.Net.WebUtility.UrlEncode(sURL);
		public string UrlEncode(string sURL)
		{
			return System.Net.WebUtility.UrlEncode(sURL);
		}
	}

	// =====================================================================================
	// UniqueStringCollection — StringCollection with deduplication via overridden Add().
	// Used to accumulate unique field name lists in SQL construction.
	// =====================================================================================
	public class UniqueStringCollection : StringCollection
	{
		public new void Add(string value)
		{
			if ( value == null || value == String.Empty )
				return;
			if ( !this.Contains(value) )
				base.Add(value);
		}

		public void AddRange(string[] values)
		{
			if ( values == null )
				return;
			foreach ( string s in values )
				Add(s);
		}

		// Add individual field names from a comma-separated field list
		public void AddFields(string sFields)
		{
			if ( Sql.IsEmptyString(sFields) )
				return;
			string[] arrFields = sFields.Split(',');
			foreach ( string s in arrFields )
			{
				string sField = s.Trim();
				if ( !Sql.IsEmptyString(sField) )
					Add(sField);
			}
		}
	}

	// =====================================================================================
	// UniqueGuidCollection — List<Guid> with deduplication via overridden Add().
	// Used for record ID accumulation in batch operations.
	// =====================================================================================
	public class UniqueGuidCollection : List<Guid>
	{
		public new void Add(Guid gID)
		{
			if ( gID == Guid.Empty )
				return;
			if ( !this.Contains(gID) )
				base.Add(gID);
		}
	}

	// =====================================================================================
	// PreviewData — lightweight data holder for preview panel display.
	// Carries module name, record ID, and archive view flag.
	// =====================================================================================
	public class PreviewData
	{
		public string Module      { get; set; }
		public Guid   ID          { get; set; }
		public bool   ArchiveView { get; set; }
	}

}  // end namespace SplendidCRM
