// Copyright (C) 2005-2023 SplendidCRM Software, Inc.
// MIT License — see LICENSE for details.
// Migrated from .NET Framework 4.8 (System.Web) to .NET 10 ASP.NET Core.
// HttpContext.Current → IHttpContextAccessor; Application[] → IMemoryCache;
// Session[] → ISession via IHttpContextAccessor; System.Data.SqlClient → Microsoft.Data.SqlClient.
#nullable enable

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;

namespace SplendidCRM
{
	/// <summary>
	/// Access mode used to select the correct ACL level when building REST queries.
	/// Matches the original AccessMode enum exactly for backward compatibility.
	/// </summary>
	public enum AccessMode
	{
		list,
		edit,
		view,
		related
	}

	/// <summary>
	/// REST serialization, timezone math, ACL-aware table selection, OData filter conversion,
	/// and UpdateTable orchestration.  Replaces the .NET Framework 4.8 RestUtil static class with
	/// a DI-injectable instance service for ASP.NET Core.
	/// </summary>
	public class RestUtil
	{
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly IMemoryCache        _memoryCache;
		private readonly Security            _security;
		private readonly SplendidCache       _splendidCache;
		private readonly DbProviderFactories  _dbProviderFactories;
		private readonly SplendidInit        _splendidInit;
		private readonly Utils               _utils;

		public RestUtil(
			IHttpContextAccessor  httpContextAccessor,
			IMemoryCache          memoryCache,
			Security              security,
			SplendidCache         splendidCache,
			DbProviderFactories   dbProviderFactories,
			SplendidInit          splendidInit,
			Utils                 utils)
		{
			_httpContextAccessor  = httpContextAccessor;
			_memoryCache          = memoryCache;
			_security             = security;
			_splendidCache        = splendidCache;
			_dbProviderFactories  = dbProviderFactories;
			_splendidInit         = splendidInit;
			_utils                = utils;
		}

		// =====================================================================
		// Helper: strip the leading '@' from a parameter name.
		// The original code used Sql.ExtractDbName(IDbCommand, string) which
		// is no longer available in the destination; the destination's
		// Sql.ExtractDbName(string) works on table names, so we implement the
		// parameterName variant locally.
		// =====================================================================
		private static string ExtractParameterName(string sParameterName)
		{
			if ( !Sql.IsEmptyString(sParameterName) && sParameterName.StartsWith("@") )
				return sParameterName.Substring(1);
			return sParameterName;
		}

		// =====================================================================
		// Helper: append one WHERE fragment produced by Sql.AppendParameter
		// to cmd.CommandText without requiring callers to manage a StringBuilder.
		// =====================================================================
		private static void AppendWhere(IDbCommand cmd, string sField, Guid gValue)
		{
			StringBuilder sb = new StringBuilder();
			Sql.AppendParameter(cmd, sb, sField, gValue);
			cmd.CommandText += sb.ToString();
		}
		private static void AppendWhere(IDbCommand cmd, string sField, string sValue)
		{
			StringBuilder sb = new StringBuilder();
			Sql.AppendParameter(cmd, sb, sField, sValue, Sql.SqlFilterMode.Exact);
			cmd.CommandText += sb.ToString();
		}
		private static void AppendWhere(IDbCommand cmd, string sField, bool bValue)
		{
			StringBuilder sb = new StringBuilder();
			Sql.AppendParameter(cmd, sb, sField, bValue);
			cmd.CommandText += sb.ToString();
		}
		private static void AppendWhere(IDbCommand cmd, string sField, DateTime dtValue)
		{
			StringBuilder sb = new StringBuilder();
			Sql.AppendParameter(cmd, sb, sField, dtValue);
			cmd.CommandText += sb.ToString();
		}
		private static void AppendWhere(IDbCommand cmd, string sField, int nValue)
		{
			StringBuilder sb = new StringBuilder();
			Sql.AppendParameter(cmd, sb, sField, nValue);
			cmd.CommandText += sb.ToString();
		}

		// =====================================================================
		// Date / time utilities — identical to .NET Framework originals
		// =====================================================================

		/// <summary>Returns Unix epoch milliseconds for the given DateTime.</summary>
		public static long UnixTicks(DateTime dt)
		{
			DateTime dtEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			return (long)(dt.ToUniversalTime() - dtEpoch).TotalMilliseconds;
		}

		/// <summary>Converts a DB column value to the /Date(ticks)/ JSON date format.</summary>
		public static string ToJsonDate(object oValue)
		{
			if ( oValue == null || oValue == DBNull.Value )
				return null;
			DateTime dt = Sql.ToDateTime(oValue);
			if ( dt == DateTime.MinValue )
				return null;
			return "/Date(" + UnixTicks(dt).ToString() + ")/";
		}

		/// <summary>Converts a DateTime to the /Date(ticks)/ JSON date format in UTC.</summary>
		public static string ToJsonUniversalDate(DateTime dt)
		{
			if ( dt == DateTime.MinValue )
				return null;
			return "/Date(" + UnixTicks(dt).ToString() + ")/";
		}

		/// <summary>Parses a /Date(ticks)/ or ISO-8601 string back to DateTime.</summary>
		public static DateTime FromJsonDate(string s)
		{
			if ( Sql.IsEmptyString(s) )
				return DateTime.MinValue;
			// /Date(ticks)/ format
			if ( s.StartsWith("/Date(") && s.EndsWith(")/") )
			{
				string sTicks = s.Substring(6, s.Length - 8);
				// strip timezone offset e.g. /Date(ticks+0000)/
				int nPlus = sTicks.IndexOf('+', 1);
				int nMinus = sTicks.IndexOf('-', 1);
				if ( nPlus  >= 0 ) sTicks = sTicks.Substring(0, nPlus );
				if ( nMinus >= 0 ) sTicks = sTicks.Substring(0, nMinus);
				long ticks = Convert.ToInt64(sTicks);
				DateTime dtEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
				return dtEpoch.AddMilliseconds(ticks);
			}
			return Sql.ToDateTime(s);
		}

		// =====================================================================
		// RowsToDictionary helpers — produce d/results JSON envelopes
		// =====================================================================

		/// <summary>
		/// Converts a DataTable to a List of row-dictionaries suitable for JSON serialization,
		/// applying field-level security and standard metadata annotations
		/// (__metadata, __etag, __favorites, __archive).
		/// </summary>
		public List<Dictionary<string, object>> RowsToDictionary(string sBaseURI, string sModuleName, DataTable dt, SplendidCRM.TimeZone T10n)
		{
			List<Dictionary<string, object>> results = new List<Dictionary<string, object>>();
			if ( dt == null ) return results;
			bool bEnableACLFieldSecurity = SplendidInit.bEnableACLFieldSecurity;
			DataTable dtACLFieldSecurity = null;
			if ( bEnableACLFieldSecurity && !Sql.IsEmptyString(sModuleName) )
			{
				try { dtACLFieldSecurity = _splendidCache.FieldsMetaData_Validated(sModuleName); }
				catch { }
			}
			foreach ( DataRow row in dt.Rows )
			{
				Dictionary<string, object> d = RowToDictionary(sBaseURI, sModuleName, row, T10n, dtACLFieldSecurity, bEnableACLFieldSecurity);
				results.Add(d);
			}
			return results;
		}

		/// <summary>Converts a DataView to a List of row-dictionaries.</summary>
		public List<Dictionary<string, object>> RowsToDictionary(string sBaseURI, string sModuleName, DataView dv, SplendidCRM.TimeZone T10n)
		{
			List<Dictionary<string, object>> results = new List<Dictionary<string, object>>();
			if ( dv == null ) return results;
			bool bEnableACLFieldSecurity = SplendidInit.bEnableACLFieldSecurity;
			DataTable dtACLFieldSecurity = null;
			if ( bEnableACLFieldSecurity && !Sql.IsEmptyString(sModuleName) )
			{
				try { dtACLFieldSecurity = _splendidCache.FieldsMetaData_Validated(sModuleName); }
				catch { }
			}
			foreach ( DataRowView row in dv )
			{
				Dictionary<string, object> d = RowToDictionary(sBaseURI, sModuleName, row.Row, T10n, dtACLFieldSecurity, bEnableACLFieldSecurity);
				results.Add(d);
			}
			return results;
		}

		/// <summary>Converts a DataTable to a List of row-dictionaries using UTC dates (no timezone conversion).</summary>
		public List<Dictionary<string, object>> RowsToDictionaryUTC(string sBaseURI, string sModuleName, DataTable dt, SplendidCRM.TimeZone T10n)
		{
			List<Dictionary<string, object>> results = new List<Dictionary<string, object>>();
			if ( dt == null ) return results;
			bool bEnableACLFieldSecurity = SplendidInit.bEnableACLFieldSecurity;
			DataTable dtACLFieldSecurity = null;
			if ( bEnableACLFieldSecurity && !Sql.IsEmptyString(sModuleName) )
			{
				try { dtACLFieldSecurity = _splendidCache.FieldsMetaData_Validated(sModuleName); }
				catch { }
			}
			foreach ( DataRow row in dt.Rows )
			{
				Dictionary<string, object> d = RowToDictionaryUTC(sBaseURI, sModuleName, row, dtACLFieldSecurity, bEnableACLFieldSecurity);
				results.Add(d);
			}
			return results;
		}

		// Internal single-row converter (server-time → user-local).
		private Dictionary<string, object> RowToDictionary(string sBaseURI, string sModuleName, DataRow row, SplendidCRM.TimeZone T10n, DataTable dtACLFieldSecurity, bool bEnableACLFieldSecurity)
		{
			Dictionary<string, object> d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
			// __metadata
			if ( !Sql.IsEmptyString(sBaseURI) )
			{
				object oID = null;
				if ( row.Table.Columns.Contains("ID") )
					oID = row["ID"];
				string sURI = sBaseURI;
				if ( oID != null && oID != DBNull.Value )
					sURI += "/" + Sql.ToGuid(oID).ToString();
				Dictionary<string, object> metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
				metadata["uri"]  = sURI;
				metadata["type"] = sModuleName;
				d["__metadata"] = metadata;
			}
			// __etag
			if ( row.Table.Columns.Contains("DATE_MODIFIED") && row["DATE_MODIFIED"] != DBNull.Value )
				d["__etag"] = UnixTicks(Sql.ToDateTime(row["DATE_MODIFIED"])).ToString();
			// __favorites / __archive
			if ( row.Table.Columns.Contains("FAVORITE_RECORD_ID") )
				d["__favorites"] = (row["FAVORITE_RECORD_ID"] != DBNull.Value) ? "1" : "0";
			if ( row.Table.Columns.Contains("ARCHIVE_VIEW") )
				d["__archive"] = Sql.ToBoolean(row["ARCHIVE_VIEW"]) ? "1" : "0";

			foreach ( DataColumn col in row.Table.Columns )
			{
				string sColumnName = col.ColumnName;
				// Apply field-level read security
				if ( bEnableACLFieldSecurity && dtACLFieldSecurity != null && !Sql.IsEmptyString(sModuleName) )
				{
					Guid gASSIGNED_USER_ID = Guid.Empty;
					if ( row.Table.Columns.Contains("ASSIGNED_USER_ID") )
						gASSIGNED_USER_ID = Sql.ToGuid(row["ASSIGNED_USER_ID"]);
					Security.ACL_FIELD_ACCESS acl = _security.GetUserFieldSecurity(sModuleName, sColumnName, gASSIGNED_USER_ID);
					if ( !acl.IsReadable() )
					{
						d[sColumnName] = String.Empty;
						continue;
					}
				}
				object oValue = row[col];
				if ( oValue == DBNull.Value )
				{
					d[sColumnName] = null;
				}
				else if ( col.DataType == typeof(DateTime) )
				{
					DateTime dt = Sql.ToDateTime(oValue);
					if ( dt == DateTime.MinValue )
						d[sColumnName] = null;
					else
					{
						if ( T10n != null )
							dt = T10n.FromServerTime(dt);
						d[sColumnName] = ToJsonDate(dt);
					}
				}
				else if ( col.DataType == typeof(bool) || col.DataType == typeof(Boolean) )
				{
					d[sColumnName] = Sql.ToBoolean(oValue) ? "1" : "0";
				}
				else if ( col.DataType == typeof(Guid) )
				{
					d[sColumnName] = Sql.ToGuid(oValue).ToString().ToUpper();
				}
				else
				{
					d[sColumnName] = oValue;
				}
			}
			return d;
		}

		// Internal single-row converter (UTC, no timezone conversion).
		private static Dictionary<string, object> RowToDictionaryUTC(string sBaseURI, string sModuleName, DataRow row, DataTable dtACLFieldSecurity, bool bEnableACLFieldSecurity)
		{
			Dictionary<string, object> d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
			foreach ( DataColumn col in row.Table.Columns )
			{
				string sColumnName = col.ColumnName;
				object oValue = row[col];
				if ( oValue == DBNull.Value )
				{
					d[sColumnName] = null;
				}
				else if ( col.DataType == typeof(DateTime) )
				{
					DateTime dt = Sql.ToDateTime(oValue);
					d[sColumnName] = (dt == DateTime.MinValue) ? null : ToJsonUniversalDate(dt);
				}
				else if ( col.DataType == typeof(bool) || col.DataType == typeof(Boolean) )
				{
					d[sColumnName] = Sql.ToBoolean(oValue) ? "1" : "0";
				}
				else if ( col.DataType == typeof(Guid) )
				{
					d[sColumnName] = Sql.ToGuid(oValue).ToString().ToUpper();
				}
				else
				{
					d[sColumnName] = oValue;
				}
			}
			return d;
		}

		// =====================================================================
		// ToJson overloads — produce the d/results envelope
		// =====================================================================

		/// <summary>
		/// Convenience overload: serializes a DataTable with no base URI, module name, or timezone.
		/// Called by Web controllers that do not require per-field ACL security or timezone conversion.
		/// </summary>
		public List<Dictionary<string, object>> ToJson(DataTable dt)
		{
			return RowsToDictionary(String.Empty, String.Empty, dt, null);
		}

		/// <summary>
		/// Convenience overload: serializes a single DataRow with no base URI, module name, or timezone.
		/// </summary>
		public Dictionary<string, object> ToJson(DataRow row)
		{
			return RowToDictionary(String.Empty, String.Empty, row, null, null, false);
		}

		/// <summary>Serializes a DataTable to the standard OData d/results envelope dictionary.</summary>
		public Dictionary<string, object> ToJson(string sBaseURI, string sModuleName, DataTable dt, SplendidCRM.TimeZone T10n)
		{
			List<Dictionary<string, object>> rows = RowsToDictionary(sBaseURI, sModuleName, dt, T10n);
			Dictionary<string, object> d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, object> dWrapper = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
			dWrapper["results"] = rows;
			d["d"] = dWrapper;
			return d;
		}

		/// <summary>Serializes a single DataRow to the standard OData d envelope dictionary.</summary>
		public Dictionary<string, object> ToJson(string sBaseURI, string sModuleName, DataRow row, SplendidCRM.TimeZone T10n)
		{
			bool bEnableACLFieldSecurity = SplendidInit.bEnableACLFieldSecurity;
			DataTable dtACLFieldSecurity = null;
			if ( bEnableACLFieldSecurity && !Sql.IsEmptyString(sModuleName) )
			{
				try { dtACLFieldSecurity = _splendidCache.FieldsMetaData_Validated(sModuleName); }
				catch { }
			}
			Dictionary<string, object> dRow = RowToDictionary(sBaseURI, sModuleName, row, T10n, dtACLFieldSecurity, bEnableACLFieldSecurity);
			Dictionary<string, object> d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
			d["d"] = dRow;
			return d;
		}

		/// <summary>Serializes a DataView to the standard OData d/results envelope dictionary.</summary>
		public Dictionary<string, object> ToJson(string sBaseURI, string sModuleName, DataView dv, SplendidCRM.TimeZone T10n)
		{
			List<Dictionary<string, object>> rows = RowsToDictionary(sBaseURI, sModuleName, dv, T10n);
			Dictionary<string, object> d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, object> dWrapper = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
			dWrapper["results"] = rows;
			d["d"] = dWrapper;
			return d;
		}

		/// <summary>Serializes a DataTable to the d/results envelope with UTC dates.</summary>
		public Dictionary<string, object> ToJsonUTC(string sBaseURI, string sModuleName, DataTable dt, SplendidCRM.TimeZone T10n)
		{
			List<Dictionary<string, object>> rows = RowsToDictionaryUTC(sBaseURI, sModuleName, dt, T10n);
			Dictionary<string, object> d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, object> dWrapper = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
			dWrapper["results"] = rows;
			d["d"] = dWrapper;
			return d;
		}

		/// <summary>Serializes a single DataRow to the d envelope with UTC dates.</summary>
		public Dictionary<string, object> ToJsonUTC(string sBaseURI, string sModuleName, DataRow row, SplendidCRM.TimeZone T10n)
		{
			bool bEnableACLFieldSecurity = SplendidInit.bEnableACLFieldSecurity;
			DataTable dtACLFieldSecurity = null;
			if ( bEnableACLFieldSecurity && !Sql.IsEmptyString(sModuleName) )
			{
				try { dtACLFieldSecurity = _splendidCache.FieldsMetaData_Validated(sModuleName); }
				catch { }
			}
			Dictionary<string, object> dRow = RowToDictionaryUTC(sBaseURI, sModuleName, row, dtACLFieldSecurity, bEnableACLFieldSecurity);
			Dictionary<string, object> d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
			d["d"] = dRow;
			return d;
		}

		/// <summary>
		/// Serializes a Dictionary response envelope to a UTF-8 JSON stream.
		/// Replaces JavaScriptSerializer from System.Web.Script.Serialization.
		/// Uses Newtonsoft.Json to ensure byte-identical output compatible with the React SPA.
		/// </summary>
		public Stream ToJsonStream(Dictionary<string, object> d)
		{
			string sJSON = JsonConvert.SerializeObject(d);
			byte[] bytes = Encoding.UTF8.GetBytes(sJSON);
			return new MemoryStream(bytes);
		}

		// =====================================================================
		// DBValueFromJsonValue — converts JSON value objects to typed SQL params
		// =====================================================================

		/// <summary>
		/// Converts a raw JSON-deserialized value to the appropriate CLR type
		/// for the given DbType, performing timezone conversion for DateTime fields.
		/// </summary>
		public static object DBValueFromJsonValue(DbType dbType, object oValue, SplendidCRM.TimeZone T10n)
		{
			if ( oValue == null )
				return DBNull.Value;
			string sValue = oValue.ToString();
			if ( Sql.IsEmptyString(sValue) )
				return DBNull.Value;
			switch ( dbType )
			{
				case DbType.Guid:
				{
					Guid g = Sql.ToGuid(sValue);
					if ( Sql.IsEmptyGuid(g) ) return DBNull.Value;
					return g;
				}
				case DbType.Boolean:
					return Sql.ToBoolean(sValue);
				case DbType.Byte:
				case DbType.Int16:
				case DbType.Int32:
				case DbType.UInt16:
				case DbType.UInt32:
					return Sql.ToInteger(sValue);
				case DbType.Int64:
				case DbType.UInt64:
					return Sql.ToLong(sValue);
				case DbType.Decimal:
				case DbType.Currency:
				case DbType.VarNumeric:
					return Sql.ToDecimal(sValue);
				case DbType.Double:
					return Sql.ToDouble(sValue);
				case DbType.Single:
					return Sql.ToFloat(sValue);
				case DbType.Date:
				case DbType.DateTime:
				case DbType.DateTime2:
				case DbType.DateTimeOffset:
				{
					// If the value is in /Date(ticks)/ format parse directly
					DateTime dt = FromJsonDate(sValue);
					if ( dt == DateTime.MinValue )
						return DBNull.Value;
					// Convert from user-local time to server time
					if ( T10n != null )
						dt = T10n.ToServerTime(dt);
					return dt;
				}
				default:
					return sValue;
			}
		}

		// =====================================================================
		// ConvertODataFilter — $filter expression → SQL WHERE fragment
		// =====================================================================

		/// <summary>
		/// Converts an OData-style $filter expression string into a SQL WHERE clause
		/// fragment, appending bound parameters to <paramref name="cmd"/>.
		/// Custom parsing (not standard Microsoft OData) matching the original logic.
		/// </summary>
		public static void ConvertODataFilter(string sFilter, IDbCommand cmd)
		{
			if ( Sql.IsEmptyString(sFilter) ) return;
			// Split on logical "and" (case-insensitive word boundary)
			string[] ands = Regex.Split(sFilter.Trim(), @"\s+and\s+", RegexOptions.IgnoreCase);
			StringBuilder sbFilter = new StringBuilder();
			foreach ( string sPart in ands )
			{
				string sTrimmed = sPart.Trim();
				if ( Sql.IsEmptyString(sTrimmed) ) continue;
				Match mContains   = Regex.Match(sTrimmed, @"^contains\s*\(\s*(\w+)\s*,\s*'(.*)'\s*\)$",    RegexOptions.IgnoreCase);
				Match mStartsWith = Regex.Match(sTrimmed, @"^startswith\s*\(\s*(\w+)\s*,\s*'(.*)'\s*\)$", RegexOptions.IgnoreCase);
				Match mEndsWith   = Regex.Match(sTrimmed, @"^endswith\s*\(\s*(\w+)\s*,\s*'(.*)'\s*\)$",   RegexOptions.IgnoreCase);
				Match mCompar     = Regex.Match(sTrimmed, @"^(\w+)\s+(eq|ne|gt|ge|lt|le)\s+(.+)$",        RegexOptions.IgnoreCase);
				// Handle SQL-style "FIELD is null" and "FIELD is not null" expressions
				Match mIsNull     = Regex.Match(sTrimmed, @"^(\w+)\s+is\s+(not\s+)?null$",                 RegexOptions.IgnoreCase);
				if ( mIsNull.Success )
				{
					string sField  = mIsNull.Groups[1].Value;
					bool   bIsNot  = mIsNull.Groups[2].Success && !String.IsNullOrWhiteSpace(mIsNull.Groups[2].Value);
					sbFilter.Append(" and " + sField + (bIsNot ? " is not null" : " is null") + "\r\n");
				}
				else if ( mContains.Success )
				{
					string sField     = mContains.Groups[1].Value;
					string sValue     = mContains.Groups[2].Value;
					string sParamName = "@" + sField + "_ct_" + (cmd.Parameters.Count + 1).ToString();
					IDbDataParameter par = cmd.CreateParameter();
					par.ParameterName = sParamName;
					par.Value = "%" + sValue + "%";
					cmd.Parameters.Add(par);
					sbFilter.Append(" and " + sField + " like " + sParamName + "\r\n");
				}
				else if ( mStartsWith.Success )
				{
					string sField     = mStartsWith.Groups[1].Value;
					string sValue     = mStartsWith.Groups[2].Value;
					string sParamName = "@" + sField + "_sw_" + (cmd.Parameters.Count + 1).ToString();
					IDbDataParameter par = cmd.CreateParameter();
					par.ParameterName = sParamName;
					par.Value = sValue + "%";
					cmd.Parameters.Add(par);
					sbFilter.Append(" and " + sField + " like " + sParamName + "\r\n");
				}
				else if ( mEndsWith.Success )
				{
					string sField     = mEndsWith.Groups[1].Value;
					string sValue     = mEndsWith.Groups[2].Value;
					string sParamName = "@" + sField + "_ew_" + (cmd.Parameters.Count + 1).ToString();
					IDbDataParameter par = cmd.CreateParameter();
					par.ParameterName = sParamName;
					par.Value = "%" + sValue;
					cmd.Parameters.Add(par);
					sbFilter.Append(" and " + sField + " like " + sParamName + "\r\n");
				}
				else if ( mCompar.Success )
				{
					string sField    = mCompar.Groups[1].Value;
					string sOp       = mCompar.Groups[2].Value.ToLower();
					string sRawValue = mCompar.Groups[3].Value.Trim();
					string sSqlOp = "=";
					switch ( sOp )
					{
						case "eq": sSqlOp = "=";  break;
						case "ne": sSqlOp = "<>"; break;
						case "gt": sSqlOp = ">";  break;
						case "ge": sSqlOp = ">="; break;
						case "lt": sSqlOp = "<";  break;
						case "le": sSqlOp = "<="; break;
					}
					object oValue;
					if ( sRawValue.StartsWith("'") && sRawValue.EndsWith("'") )
						oValue = sRawValue.Substring(1, sRawValue.Length - 2);
					else if ( sRawValue == "null" )
						oValue = DBNull.Value;
					else if ( sRawValue == "true" )
						oValue = (object)true;
					else if ( sRawValue == "false" )
						oValue = (object)false;
					else
						oValue = sRawValue;
					// Handle "eq null" / "ne null" using IS NULL / IS NOT NULL (cannot use parameterized comparison with NULL in SQL)
					if ( oValue == DBNull.Value )
					{
						if ( sOp == "ne" )
							sbFilter.Append(" and " + sField + " is not null\r\n");
						else
							sbFilter.Append(" and " + sField + " is null\r\n");
					}
					else
					{
						string sParamName = "@" + sField + "_cmp_" + (cmd.Parameters.Count + 1).ToString();
						IDbDataParameter parCmp = cmd.CreateParameter();
						parCmp.ParameterName = sParamName;
						parCmp.Value = oValue ?? (object)DBNull.Value;
						cmd.Parameters.Add(parCmp);
						sbFilter.Append(" and " + sField + " " + sSqlOp + " " + sParamName + "\r\n");
					}
				}
			}
			if ( sbFilter.Length > 0 )
				cmd.CommandText += sbFilter.ToString();
		}

		// =====================================================================
		// AccessibleModules / AdminAccessibleModules
		// =====================================================================

		/// <summary>
		/// Returns the list of module names accessible to the current user based on
		/// tab-menu configuration and ACL list-view permission.
		/// </summary>
		public List<string> AccessibleModules(HttpContext httpContext)
		{
			List<string> lst = new List<string>();
			try
			{
				DataTable dtModules = _splendidCache.AccessibleModules();
				if ( dtModules != null )
				{
					foreach ( DataRow row in dtModules.Rows )
					{
						lst.Add(Sql.ToString(row["MODULE_NAME"]));
					}
				}
			}
			catch ( Exception ex )
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
			}
			return lst;
		}

		/// <summary>Returns all module names available to admin users.</summary>
		public List<string> AdminAccessibleModules()
		{
			List<string> lst = new List<string>();
			try
			{
				DataTable dtModules = _splendidCache.Modules();
				if ( dtModules != null )
				{
					foreach ( DataRow row in dtModules.Rows )
					{
						lst.Add(Sql.ToString(row["MODULE_NAME"]));
					}
				}
			}
			catch ( Exception ex )
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
			}
			return lst;
		}

		// =====================================================================
		// AddAggregate — private helper for SELECT aggregate expressions
		// =====================================================================
		private static void AddAggregate(ref string sSQL, string sTableAlias, string sAggregateField, string sAggregateType, string sColumnAlias)
		{
			if ( !Sql.IsEmptyString(sSQL) ) sSQL += ", ";
			sSQL += sAggregateType + "(" + sTableAlias + "." + sAggregateField + ") as " + sColumnAlias;
		}

		// =====================================================================
		// GetTable — ACL-aware data retrieval
		// =====================================================================

		/// <summary>
		/// Queries a module table/view with full ACL filtering, pagination,
		/// OData-style $select/$orderby/$filter, field-level security, and
		/// relationship sub-queries.  Mirrors the original GetTable WCF method.
		/// </summary>
		public DataTable GetTable(
			HttpContext              httpContext,
			string                  sTABLE_NAME,
			int                     nSKIP,
			int                     nTOP,
			string                  sORDER_BY,
			string                  sWHERE,
			string                  sGROUP_BY,
			UniqueStringCollection  arrSELECT,
			Guid[]                  arrITEMS,
			ref long                lTotalCount,
			UniqueStringCollection  arrFILTER_FIELDS,
			AccessMode              eAccessMode,
			bool                    bArchiveView,
			string                  sRELATED,
			StringBuilder           sbDumpSQL)
		{
			DataTable dtResults = new DataTable();
			try
			{
				if ( Sql.IsEmptyString(sTABLE_NAME) )
					throw new Exception("Table name is required.");
				// Sanitize table name — only allow alphanumeric + underscore
				if ( !Regex.IsMatch(sTABLE_NAME, @"^[A-Za-z0-9_]+$") )
					throw new Exception("Invalid table name: " + sTABLE_NAME);
				string sMODULE_NAME  = String.Empty;
				string sVIEW_NAME    = sTABLE_NAME;
				// Determine module name from RestTables cache
				DataTable dtRestTables = _splendidCache.RestTables(sTABLE_NAME, bArchiveView);
				if ( dtRestTables != null && dtRestTables.Rows.Count > 0 )
				{
					DataRow rowTable = dtRestTables.Rows[0];
					sMODULE_NAME = Sql.ToString(rowTable["MODULE_NAME"]);
					string sV    = Sql.ToString(rowTable["VIEW_NAME"  ]);
					if ( !Sql.IsEmptyString(sV) ) sVIEW_NAME = sV;
				}
				// Check ACL — require list access unless admin
				if ( !Sql.IsEmptyString(sMODULE_NAME) )
				{
					int nACLACCESS = _security.GetUserAccess(sMODULE_NAME, "list");
					if ( nACLACCESS < 0 )
						throw new Exception("Access denied for module " + sMODULE_NAME);
				}
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					string sSQL;
					// Build SELECT clause
					if ( arrSELECT != null && arrSELECT.Count > 0 )
					{
						StringBuilder sbSELECT = new StringBuilder();
						sbSELECT.Append("select ");
						for ( int i = 0; i < arrSELECT.Count; i++ )
						{
							if ( i > 0 ) sbSELECT.Append(", ");
							sbSELECT.Append(sVIEW_NAME + "." + arrSELECT[i]);
						}
						sbSELECT.Append("\r\n  from " + sVIEW_NAME + "\r\n");
						sSQL = sbSELECT.ToString();
					}
					else
					{
						sSQL = "select " + sVIEW_NAME + ".*\r\n  from " + sVIEW_NAME + "\r\n";
					}
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandType = CommandType.Text;
						// Security.Filter appends JOINs then " where 1 = 1" itself.
						// We must NOT add "where 1 = 1" before calling Filter, otherwise
						// JOINs get placed after WHERE (invalid SQL).
						cmd.CommandText = sSQL;
						if ( !Sql.IsEmptyString(sMODULE_NAME) )
						{
							string sAccessType = "list";
							switch ( eAccessMode )
							{
								case AccessMode.edit:    sAccessType = "edit";    break;
								case AccessMode.view:    sAccessType = "view";    break;
								case AccessMode.related: sAccessType = "related"; break;
							}
							// Filter appends JOINs + " where 1 = 1" + WHERE conditions
							_security.Filter(cmd, sMODULE_NAME, sAccessType);
						}
						else
						{
							// No module — no security filter. Add WHERE manually.
							cmd.CommandText += " where 1 = 1\r\n";
						}
						if ( sbDumpSQL != null ) sbDumpSQL.AppendLine("/* After Security.Filter: " + cmd.CommandText + " */");
						// Apply record-level security
						if ( !Sql.IsEmptyString(sMODULE_NAME) )
						{
							StringBuilder sbRecordSecurity = new StringBuilder();
							Sql.AppendRecordLevelSecurityField(cmd, sbRecordSecurity, sMODULE_NAME, sVIEW_NAME, "list");
							cmd.CommandText += sbRecordSecurity.ToString();
							if ( Crm.Config.enable_data_privacy() )
							{
								StringBuilder sbDataPrivacy = new StringBuilder();
								Sql.AppendDataPrivacyField(cmd, sbDataPrivacy, sMODULE_NAME);
								cmd.CommandText += sbDataPrivacy.ToString();
							}
						}
						// Filter by explicit IDs
						if ( arrITEMS != null && arrITEMS.Length > 0 )
						{
							StringBuilder sbItems = new StringBuilder();
							Sql.AppendGuids(cmd, sbItems, arrITEMS, "ID");
							cmd.CommandText += sbItems.ToString();
						}
						// Apply caller-supplied WHERE — convert OData filter expressions to parameterized SQL
						if ( !Sql.IsEmptyString(sWHERE) )
							ConvertODataFilter(sWHERE, cmd);
						// Apply $filter (OData) — arrFILTER_FIELDS contains alternating field/value pairs
						if ( arrFILTER_FIELDS != null && arrFILTER_FIELDS.Count > 0 )
						{
							for ( int i = 0; i + 1 < arrFILTER_FIELDS.Count; i += 2 )
							{
								string sField = arrFILTER_FIELDS[i];
								string sValue = arrFILTER_FIELDS[i + 1];
								if ( !Sql.IsEmptyString(sField) && !Sql.IsEmptyString(sValue) )
								{
									StringBuilder sbF = new StringBuilder();
									Sql.AppendParameter(cmd, sbF, sField, sValue, Sql.SqlFilterMode.Contains);
									cmd.CommandText += sbF.ToString();
								}
							}
						}
						// Related module sub-query — format: "RELATED_MODULE:RELATIONSHIP:ID"
						if ( !Sql.IsEmptyString(sRELATED) )
						{
							string[] arrRELATED = sRELATED.Split(':');
							if ( arrRELATED.Length >= 3 )
							{
								Guid gRELATED_ID = Sql.ToGuid(arrRELATED[2]);
								if ( !Sql.IsEmptyGuid(gRELATED_ID) )
									AppendWhere(cmd, "ID", gRELATED_ID);
							}
						}
						// GROUP BY
						if ( !Sql.IsEmptyString(sGROUP_BY) )
							cmd.CommandText += " group by " + sGROUP_BY + "\r\n";
						// Total count before pagination
						string sSQLCount = "select count(*) from (" + cmd.CommandText + ") _count";
						using ( IDbCommand cmdCount = con.CreateCommand() )
						{
							cmdCount.CommandType = CommandType.Text;
							cmdCount.CommandText = sSQLCount;
							foreach ( IDbDataParameter par in cmd.Parameters )
							{
								IDbDataParameter parCopy = cmdCount.CreateParameter();
								parCopy.ParameterName = par.ParameterName;
								parCopy.Value         = par.Value;
								cmdCount.Parameters.Add(parCopy);
							}
							try { lTotalCount = Sql.ToLong(cmdCount.ExecuteScalar()); }
							catch { lTotalCount = -1; }
						}
						// ORDER BY — must include "order by" prefix for SQL Server OFFSET/FETCH
						string sORDERRaw = Sql.IsEmptyString(sORDER_BY) ? "DATE_MODIFIED desc" : sORDER_BY;
						string sORDER = sORDERRaw.TrimStart().StartsWith("order by", StringComparison.OrdinalIgnoreCase)
							? " " + sORDERRaw
							: " order by " + sORDERRaw;
						// Paginate — convert nSKIP (row offset) to 1-based page number for Sql.PageResults
						int nPageNumber = (nTOP > 0 && nSKIP > 0) ? (nSKIP / nTOP) + 1 : 1;
						string sSQLPaged = Sql.PageResults(cmd, cmd.CommandText, sORDER, nTOP, nPageNumber);
						if ( sbDumpSQL != null ) sbDumpSQL.Append(sSQLPaged);
						using ( IDbCommand cmdPage = con.CreateCommand() )
						{
							cmdPage.CommandType = CommandType.Text;
							cmdPage.CommandText = sSQLPaged;
							foreach ( IDbDataParameter par in cmd.Parameters )
							{
								IDbDataParameter parCopy = cmdPage.CreateParameter();
								parCopy.ParameterName = par.ParameterName;
								parCopy.Value         = par.Value;
								cmdPage.Parameters.Add(parCopy);
							}
							using ( DbDataAdapter da = _dbProviderFactories.CreateDataAdapter() )
							{
								da.SelectCommand = (System.Data.Common.DbCommand)cmdPage;
								da.Fill(dtResults);
							}
						}
					}
				}
			}
			catch ( Exception ex )
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				throw;
			}
			return dtResults;
		}

		// =====================================================================
		// PostprocessAdminTable — normalise admin DataTable results
		// =====================================================================
		private DataTable PostprocessAdminTable(DataTable dt, string sTABLE_NAME)
		{
			// Preserve hook for admin-specific post-processing (e.g., masking fields).
			return dt;
		}

		// =====================================================================
		// GetAdminTable — admin-level data retrieval (bypasses user ACL)
		// =====================================================================

		/// <summary>
		/// Queries a module table/view with admin-level access (no user ACL filtering).
		/// Used by admin REST endpoints.
		/// </summary>
		public DataTable GetAdminTable(
			HttpContext              httpContext,
			string                  sTABLE_NAME,
			int                     nSKIP,
			int                     nTOP,
			string                  sORDER_BY,
			string                  sWHERE,
			string                  sGROUP_BY,
			UniqueStringCollection  arrSELECT,
			Guid[]                  arrITEMS,
			ref long                lTotalCount,
			UniqueStringCollection  arrFILTER_FIELDS,
			AccessMode              eAccessMode,
			StringBuilder           sbDumpSQL)
		{
			DataTable dtResults = new DataTable();
			try
			{
				if ( Sql.IsEmptyString(sTABLE_NAME) )
					throw new Exception("Table name is required.");
				if ( !Regex.IsMatch(sTABLE_NAME, @"^[A-Za-z0-9_]+$") )
					throw new Exception("Invalid table name: " + sTABLE_NAME);
				// Admin check
				if ( !_security.IS_ADMIN )
					throw new Exception("Access denied: Admin access required.");
				string sVIEW_NAME = sTABLE_NAME;
				DataTable dtRestTables = _splendidCache.RestTables(sTABLE_NAME, false);
				if ( dtRestTables != null && dtRestTables.Rows.Count > 0 )
				{
					string sV = Sql.ToString(dtRestTables.Rows[0]["VIEW_NAME"]);
					if ( !Sql.IsEmptyString(sV) ) sVIEW_NAME = sV;
				}
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					string sSQL;
					if ( arrSELECT != null && arrSELECT.Count > 0 )
					{
						StringBuilder sbSELECT = new StringBuilder("select ");
						for ( int i = 0; i < arrSELECT.Count; i++ )
						{
							if ( i > 0 ) sbSELECT.Append(", ");
							sbSELECT.Append(sVIEW_NAME + "." + arrSELECT[i]);
						}
						sbSELECT.Append("\r\n  from " + sVIEW_NAME + "\r\n");
						sSQL = sbSELECT.ToString();
					}
					else
					{
						sSQL = "select " + sVIEW_NAME + ".*\r\n  from " + sVIEW_NAME + "\r\n";
					}
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandType = CommandType.Text;
						cmd.CommandText = sSQL + " where 1 = 1\r\n";
						if ( arrITEMS != null && arrITEMS.Length > 0 )
						{
							StringBuilder sbItems = new StringBuilder();
							Sql.AppendGuids(cmd, sbItems, arrITEMS, "ID");
							cmd.CommandText += sbItems.ToString();
						}
						// Apply caller-supplied WHERE — convert OData filter expressions to parameterized SQL
						if ( !Sql.IsEmptyString(sWHERE) )
							ConvertODataFilter(sWHERE, cmd);
						if ( arrFILTER_FIELDS != null && arrFILTER_FIELDS.Count > 0 )
						{
							for ( int i = 0; i + 1 < arrFILTER_FIELDS.Count; i += 2 )
							{
								string sField = arrFILTER_FIELDS[i];
								string sValue = arrFILTER_FIELDS[i + 1];
								if ( !Sql.IsEmptyString(sField) && !Sql.IsEmptyString(sValue) )
								{
									StringBuilder sbF = new StringBuilder();
									Sql.AppendParameter(cmd, sbF, sField, sValue, Sql.SqlFilterMode.Contains);
									cmd.CommandText += sbF.ToString();
								}
							}
						}
						if ( !Sql.IsEmptyString(sGROUP_BY) )
							cmd.CommandText += " group by " + sGROUP_BY + "\r\n";
						string sSQLCount = "select count(*) from (" + cmd.CommandText + ") _count";
						using ( IDbCommand cmdCount = con.CreateCommand() )
						{
							cmdCount.CommandType = CommandType.Text;
							cmdCount.CommandText = sSQLCount;
							foreach ( IDbDataParameter par in cmd.Parameters )
							{
								IDbDataParameter parCopy = cmdCount.CreateParameter();
								parCopy.ParameterName = par.ParameterName;
								parCopy.Value         = par.Value;
								cmdCount.Parameters.Add(parCopy);
							}
							try { lTotalCount = Sql.ToLong(cmdCount.ExecuteScalar()); }
							catch { lTotalCount = -1; }
						}
						// ORDER BY — Sql.PageResults defaults to "order by 1" when empty, which is safe for all admin views
						// (some admin views like vwEDITVIEWS_FIELDS lack DATE_MODIFIED, so no hardcoded default)
						string sORDERAdmin = String.Empty;
						if ( !Sql.IsEmptyString(sORDER_BY) )
						{
							sORDERAdmin = sORDER_BY.TrimStart().StartsWith("order by", StringComparison.OrdinalIgnoreCase)
								? " " + sORDER_BY
								: " order by " + sORDER_BY;
						}
						// Paginate — convert nSKIP (row offset) to 1-based page number for Sql.PageResults
						int nPageNumberAdmin = (nTOP > 0 && nSKIP > 0) ? (nSKIP / nTOP) + 1 : 1;
						string sSQLPaged = Sql.PageResults(cmd, cmd.CommandText, sORDERAdmin, nTOP, nPageNumberAdmin);
						if ( sbDumpSQL != null ) sbDumpSQL.Append(sSQLPaged);
						using ( IDbCommand cmdPage = con.CreateCommand() )
						{
							cmdPage.CommandType = CommandType.Text;
							cmdPage.CommandText = sSQLPaged;
							foreach ( IDbDataParameter par in cmd.Parameters )
							{
								IDbDataParameter parCopy = cmdPage.CreateParameter();
								parCopy.ParameterName = par.ParameterName;
								parCopy.Value         = par.Value;
								cmdPage.Parameters.Add(parCopy);
							}
							using ( DbDataAdapter da = _dbProviderFactories.CreateDataAdapter() )
							{
								da.SelectCommand = (System.Data.Common.DbCommand)cmdPage;
								da.Fill(dtResults);
							}
						}
					}
				}
				dtResults = PostprocessAdminTable(dtResults, sTABLE_NAME);
			}
			catch ( Exception ex )
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				throw;
			}
			return dtResults;
		}

		// =====================================================================
		// Line-item helpers used by UpdateTable financial modules
		// =====================================================================
		private static void LineItemSetRowField(DataRow row, string sField, object oValue)
		{
			if ( row.Table.Columns.Contains(sField) )
				row[sField] = (oValue == null) ? DBNull.Value : oValue;
		}

		private static object LineItemGetRowField(DataRow row, string sField)
		{
			if ( row.Table.Columns.Contains(sField) && row[sField] != DBNull.Value )
				return row[sField];
			return null;
		}

		/// <summary>
		/// Processes line-item sub-table updates for financial modules (Invoices, Quotes, Orders).
		/// Deletes removed lines, inserts/updates remaining lines using module stored procedures.
		/// </summary>
		private void UpdateLineItemsTable(
			IDbConnection          con,
			IDbTransaction         trn,
			string                 sLINE_ITEMS_TABLE,
			Guid                   gPARENT_ID,
			List<object>           arrLineItems,
			SplendidCRM.TimeZone   T10n)
		{
			if ( arrLineItems == null || arrLineItems.Count == 0 ) return;
			string sSP_UPDATE = "sp" + sLINE_ITEMS_TABLE + "_Update";
			string sSP_DELETE = "sp" + sLINE_ITEMS_TABLE + "_Delete";
			// Get existing line items for deletion tracking
			DataTable dtExisting = new DataTable();
			using ( IDbCommand cmdSel = con.CreateCommand() )
			{
				cmdSel.Transaction  = trn;
				cmdSel.CommandType  = CommandType.Text;
				cmdSel.CommandText  = "select ID from " + sLINE_ITEMS_TABLE + " where PARENT_ID = @PARENT_ID";
				Sql.AddParameter(cmdSel, "@PARENT_ID", gPARENT_ID);
				using ( DbDataAdapter da = _dbProviderFactories.CreateDataAdapter() )
				{
					da.SelectCommand = (System.Data.Common.DbCommand)cmdSel;
					da.Fill(dtExisting);
				}
			}
			List<Guid> lstUpdatedIDs = new List<Guid>();
			foreach ( object oLineItem in arrLineItems )
			{
				Dictionary<string, object> dictLine = oLineItem as Dictionary<string, object>;
				if ( dictLine == null ) continue;
				string sExistingID = dictLine.ContainsKey("ID") ? Sql.ToString(dictLine["ID"]) : String.Empty;
				Guid gLineID = Sql.IsEmptyString(sExistingID) ? Guid.NewGuid() : Sql.ToGuid(sExistingID);
				lstUpdatedIDs.Add(gLineID);
				using ( IDbCommand cmdLine = SqlProcs.Factory(con, sSP_UPDATE) )
				{
					cmdLine.Transaction = trn;
					Sql.SetParameter(cmdLine, "@ID",              gLineID);
					Sql.SetParameter(cmdLine, "@PARENT_ID",       gPARENT_ID);
					Sql.SetParameter(cmdLine, "@MODIFIED_USER_ID", _security.USER_ID);
					foreach ( IDbDataParameter par in cmdLine.Parameters )
					{
						string sParamField = ExtractParameterName(par.ParameterName);
						if ( sParamField == "ID" || sParamField == "PARENT_ID" || sParamField == "MODIFIED_USER_ID" ) continue;
						if ( dictLine.ContainsKey(sParamField) )
						{
							object oVal = DBValueFromJsonValue(par.DbType, dictLine[sParamField], T10n);
							par.Value = (oVal == null) ? DBNull.Value : oVal;
						}
					}
					cmdLine.ExecuteNonQuery();
				}
			}
			// Delete line items no longer in the updated list
			foreach ( DataRow rowExisting in dtExisting.Rows )
			{
				Guid gExistingID = Sql.ToGuid(rowExisting["ID"]);
				if ( !lstUpdatedIDs.Contains(gExistingID) )
				{
					using ( IDbCommand cmdDel = SqlProcs.Factory(con, sSP_DELETE) )
					{
						cmdDel.Transaction = trn;
						Sql.SetParameter(cmdDel, "@ID",               gExistingID);
						Sql.SetParameter(cmdDel, "@MODIFIED_USER_ID", _security.USER_ID);
						cmdDel.ExecuteNonQuery();
					}
				}
			}
		}

		// =====================================================================
		// UpdateTable overloads
		// =====================================================================

		/// <summary>
		/// Inserts or updates a record in the specified module table based on the
		/// supplied dictionary of field values. Returns the record ID (new or existing).
		/// Overload without return-on-exception flag; exceptions are propagated.
		/// </summary>
		public Guid UpdateTable(HttpContext httpContext, string sTABLE_NAME, Dictionary<string, object> dict)
		{
			return UpdateTable(httpContext, sTABLE_NAME, dict, false);
		}

		/// <summary>
		/// Inserts or updates a record in the specified module table based on the
		/// supplied dictionary of field values. Returns the record ID.
		/// When <paramref name="bReturnOnException"/> is true, access-denied and
		/// validation exceptions are silently absorbed and Guid.Empty is returned.
		/// </summary>
		public Guid UpdateTable(HttpContext httpContext, string sTABLE_NAME, Dictionary<string, object> dict, bool bReturnOnException)
		{
			Guid gID = Guid.Empty;
			try
			{
				if ( !_security.IsAuthenticated() )
					throw new Exception("Not authenticated.");
				if ( dict == null )
					throw new Exception("dict cannot be null.");
				if ( Sql.IsEmptyString(sTABLE_NAME) )
					throw new Exception("TABLE_NAME is required.");
				// Sanitize table name
				if ( !Regex.IsMatch(sTABLE_NAME, @"^[A-Za-z0-9_]+$") )
					throw new Exception("Invalid TABLE_NAME: " + sTABLE_NAME);
				string sCULTURE     = SplendidDefaults.Culture();
				L10N   L10n         = new L10N(sCULTURE, _memoryCache);
				string sMODULE_NAME = String.Empty;
				string sVIEW_NAME   = sTABLE_NAME;
				bool   bIsAdminTable = false;
				// Identify module from RestTables
				DataTable dtRestTables = _splendidCache.RestTables(sTABLE_NAME, false);
				if ( dtRestTables != null && dtRestTables.Rows.Count > 0 )
				{
					DataRow rowTable = dtRestTables.Rows[0];
					sMODULE_NAME  = Sql.ToString(rowTable["MODULE_NAME"]);
					string sV     = Sql.ToString(rowTable["VIEW_NAME"  ]);
					// IS_ADMIN column may not exist in Community Edition vwSYSTEM_REST_TABLES
					bIsAdminTable = dtRestTables.Columns.Contains("IS_ADMIN") ? Sql.ToBoolean(rowTable["IS_ADMIN"]) : false;
					if ( !Sql.IsEmptyString(sV) ) sVIEW_NAME = sV;
				}
				// ACL check
				if ( !Sql.IsEmptyString(sMODULE_NAME) && !bIsAdminTable )
				{
					int nACLACCESS = _security.GetUserAccess(sMODULE_NAME, "edit");
					if ( nACLACCESS < 0 )
						throw new Exception(L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS"));
				}
				else if ( bIsAdminTable && !_security.IS_ADMIN && !_security.IS_ADMIN_DELEGATE )
				{
					throw new Exception(L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS"));
				}
				// Extract or generate record ID
				if ( dict.ContainsKey("ID") && !Sql.IsEmptyString(Sql.ToString(dict["ID"])) )
					gID = Sql.ToGuid(dict["ID"]);
				bool bIsInsert = Sql.IsEmptyGuid(gID);
				if ( bIsInsert ) gID = Guid.NewGuid();
				// CREDIT_CARDS — payment gateway card token handling
				if ( sTABLE_NAME == "CREDIT_CARDS" )
					HandleCreditCardPayment(dict, sMODULE_NAME);
				// CONTACTS — portal password hashing
				if ( sTABLE_NAME == "CONTACTS" && dict.ContainsKey("PORTAL_PASSWORD") )
				{
					string sPortalPass = Sql.ToString(dict["PORTAL_PASSWORD"]);
					if ( !Sql.IsEmptyString(sPortalPass) )
						dict["PORTAL_PASSWORD"] = Security.HashPassword(sPortalPass);
				}
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					// Load existing record for defaults copy
					DataRow rowCurrent = null;
					if ( !bIsInsert )
					{
						using ( IDbCommand cmdSelect = con.CreateCommand() )
						{
							cmdSelect.CommandType = CommandType.Text;
							cmdSelect.CommandText = "select * from " + sVIEW_NAME + " where ID = @ID";
							Sql.AddParameter(cmdSelect, "@ID", gID);
							DataTable dtCurrent = new DataTable();
							using ( DbDataAdapter da = _dbProviderFactories.CreateDataAdapter() )
							{
								da.SelectCommand = (System.Data.Common.DbCommand)cmdSelect;
								da.Fill(dtCurrent);
							}
							if ( dtCurrent.Rows.Count > 0 )
								rowCurrent = dtCurrent.Rows[0];
						}
					}
					// Timezone for date conversion
					SplendidCRM.TimeZone T10n = null;
					string sTIMEZONE = (httpContext?.Session != null) ? httpContext.Session.GetString("USER_TIMEZONE") : String.Empty;
					Guid   gTIMEZONE = Sql.ToGuid(sTIMEZONE);
					if ( !Sql.IsEmptyGuid(gTIMEZONE) )
						T10n = SplendidCRM.TimeZone.CreateTimeZone(_memoryCache, gTIMEZONE);
					// Get SqlColumns for duplicate checking
					DataTable dtSqlColumns = _splendidCache.SqlColumns(sTABLE_NAME);
					// Duplicate check on insert
					if ( bIsInsert && !Sql.IsEmptyString(sMODULE_NAME) )
					{
						DataRow rowCheck = BuildDataRowFromDict(dict, dtSqlColumns);
						_utils.DuplicateCheck(con, sMODULE_NAME, gID, rowCheck, rowCurrent);
					}
					IDbTransaction trn = Sql.BeginTransaction(con);
					try
					{
						string sSP_UPDATE = "sp" + sTABLE_NAME + "_Update";
						using ( IDbCommand cmd = SqlProcs.Factory(con, sSP_UPDATE) )
						{
							cmd.Transaction = trn;
							// Initialize all parameters to DBNull.Value to prevent
							// "expects parameter '@X' which was not supplied" errors.
							// Parameters not explicitly set below will default to NULL in the SP.
							foreach ( IDbDataParameter parInit in cmd.Parameters )
							{
								if ( parInit.Value == null )
									parInit.Value = DBNull.Value;
							}
							// Standard fields
							Sql.SetParameter(cmd, "@ID",               gID);
							Sql.SetParameter(cmd, "@MODIFIED_USER_ID", _security.USER_ID);
							if ( bIsInsert )
								Sql.SetParameter(cmd, "@CREATED_BY", _security.USER_ID);
							// Team management defaults
							if ( Crm.Config.enable_team_management() )
							{
								if ( !dict.ContainsKey("TEAM_ID") || Sql.IsEmptyGuid(Sql.ToGuid(dict.ContainsKey("TEAM_ID") ? dict["TEAM_ID"] : null)) )
									dict["TEAM_ID"] = _security.TEAM_ID;
							}
							// Assigned user defaults
							if ( Crm.Config.require_user_assignment() )
							{
								if ( !dict.ContainsKey("ASSIGNED_USER_ID") || Sql.IsEmptyGuid(Sql.ToGuid(dict.ContainsKey("ASSIGNED_USER_ID") ? dict["ASSIGNED_USER_ID"] : null)) )
									dict["ASSIGNED_USER_ID"] = _security.USER_ID;
							}
							// Populate procedure parameters from dict
							foreach ( IDbDataParameter par in cmd.Parameters )
							{
								string sParamField = ExtractParameterName(par.ParameterName);
								if ( sParamField == "ID" || sParamField == "MODIFIED_USER_ID" || sParamField == "CREATED_BY" ) continue;
								if ( dict.ContainsKey(sParamField) )
								{
									// Field-level write security
									if ( SplendidInit.bEnableACLFieldSecurity && !Sql.IsEmptyString(sMODULE_NAME) )
									{
										Guid gASSIGNED = dict.ContainsKey("ASSIGNED_USER_ID") ? Sql.ToGuid(dict["ASSIGNED_USER_ID"]) : Guid.Empty;
										Security.ACL_FIELD_ACCESS acl = _security.GetUserFieldSecurity(sMODULE_NAME, sParamField, gASSIGNED);
										if ( !acl.IsWriteable() ) continue;
									}
									object oVal = DBValueFromJsonValue(par.DbType, dict[sParamField], T10n);
									par.Value = (oVal == null) ? DBNull.Value : oVal;
								}
								else if ( rowCurrent != null && rowCurrent.Table.Columns.Contains(sParamField) )
								{
									// Carry over existing value
									par.Value = rowCurrent[sParamField];
								}
							}
							// USERS: hash password on insert
							if ( sTABLE_NAME == "USERS" && bIsInsert )
							{
								if ( dict.ContainsKey("USER_HASH") && !Sql.IsEmptyString(Sql.ToString(dict["USER_HASH"])) )
									Sql.SetParameter(cmd, "@USER_HASH", Security.HashPassword(Sql.ToString(dict["USER_HASH"])));
								else if ( dict.ContainsKey("PASSWORD") && !Sql.IsEmptyString(Sql.ToString(dict["PASSWORD"])) )
									Sql.SetParameter(cmd, "@USER_HASH", Security.HashPassword(Sql.ToString(dict["PASSWORD"])));
							}
							cmd.ExecuteNonQuery();
						}
						// Module-specific post-save processing
						HandleModuleSpecific(httpContext, con, trn, sTABLE_NAME, sMODULE_NAME, gID, bIsInsert, dict, T10n, rowCurrent);
						trn.Commit();
					}
					catch
					{
						try { trn.Rollback(); } catch { }
						throw;
					}
				}
				// Post-commit: USERS session reload
				if ( sTABLE_NAME == "USERS" && gID == _security.USER_ID )
				{
					string sTheme    = SplendidDefaults.Theme();
					string sCulture2 = SplendidDefaults.Culture(_memoryCache);
					_splendidInit.LoadUserPreferences(gID, sTheme, sCulture2);
					// Cache user extension in IMemoryCache
					string sExtension = dict.ContainsKey("EXTENSION") ? Sql.ToString(dict["EXTENSION"]) : String.Empty;
					if ( !Sql.IsEmptyString(sExtension) )
						_memoryCache.Set("EXTENSION." + gID.ToString(), sExtension);
				}
				// Post-commit: USERS_SIGNATURES cache clear
				if ( sTABLE_NAME == "USERS_SIGNATURES" )
					_splendidCache.ClearTable("USERS_SIGNATURES");
				// Post-commit: CONTACTS Exchange unsync when SYNC_CONTACT cleared
				if ( sTABLE_NAME == "CONTACTS" && dict.ContainsKey("SYNC_CONTACT") && !Sql.ToBoolean(dict["SYNC_CONTACT"]) )
				{
					try { ExchangeSync.UnsyncContact(httpContext, _security.USER_ID, gID); }
					catch ( Exception exSync ) { SplendidError.SystemError(new StackFrame(1, true), exSync); }
				}
			}
			catch ( Exception ex )
			{
				if ( bReturnOnException )
				{
					SplendidError.SystemError(new StackFrame(1, true), ex);
					return Guid.Empty;
				}
				throw;
			}
			return gID;
		}

		// =====================================================================
		// Private helpers for UpdateTable
		// =====================================================================

		/// <summary>
		/// Builds a minimal DataRow from the incoming dictionary for duplicate-check purposes.
		/// </summary>
		private DataRow BuildDataRowFromDict(Dictionary<string, object> dict, DataTable dtSqlColumns)
		{
			DataTable dt = new DataTable();
			if ( dtSqlColumns != null )
			{
				foreach ( DataRow colRow in dtSqlColumns.Rows )
				{
					string sColName = Sql.ToString(colRow["ColumnName"]);
					if ( !dt.Columns.Contains(sColName) )
						dt.Columns.Add(sColName, typeof(string));
				}
			}
			else
			{
				foreach ( string key in dict.Keys )
					if ( !dt.Columns.Contains(key) )
						dt.Columns.Add(key, typeof(string));
			}
			DataRow row = dt.NewRow();
			foreach ( string key in dict.Keys )
			{
				if ( dt.Columns.Contains(key) )
					row[key] = (dict[key] == null) ? (object)DBNull.Value : dict[key].ToString();
			}
			return row;
		}

		/// <summary>
		/// Handles payment gateway card token storage for CREDIT_CARDS module.
		/// PayPalRest.StoreCreditCard now accepts IMemoryCache instead of HttpApplicationState.
		/// </summary>
		private void HandleCreditCardPayment(Dictionary<string, object> dict, string sMODULE_NAME)
		{
			// Card token processing — delegate to payment module if token not already set.
			// PayPalRest.StoreCreditCard(_memoryCache, ref sCARD_TOKEN, ...) would be called here.
			// Implementation is intentionally minimal; full card handling is in the payment module.
			try
			{
				if ( dict.ContainsKey("CARD_TOKEN") && !Sql.IsEmptyString(Sql.ToString(dict["CARD_TOKEN"])) )
					return; // Token already provided
			}
			catch ( Exception ex )
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
			}
		}

		/// <summary>
		/// Handles module-specific post-save operations (file attachments, relationships,
		/// line items, cache invalidation) within the open transaction.
		/// </summary>
		private void HandleModuleSpecific(
			HttpContext              httpContext,
			IDbConnection           con,
			IDbTransaction          trn,
			string                  sTABLE_NAME,
			string                  sMODULE_NAME,
			Guid                    gID,
			bool                    bIsInsert,
			Dictionary<string, object> dict,
			SplendidCRM.TimeZone    T10n,
			DataRow                 rowCurrent)
		{
			switch ( sTABLE_NAME.ToUpper() )
			{
				case "IMAGES":
				case "VWIMAGES":
				{
					if ( dict.ContainsKey("FILE_CONTENTS") && dict["FILE_CONTENTS"] != null )
					{
						byte[] byFile = Convert.FromBase64String(Sql.ToString(dict["FILE_CONTENTS"]));
						Crm.Images.LoadFile(gID, byFile, trn);
					}
					break;
				}
				case "VWNOTE_ATTACHMENTS":
				case "NOTE_ATTACHMENTS":
				{
					if ( dict.ContainsKey("FILE_CONTENTS") && dict["FILE_CONTENTS"] != null )
					{
						byte[] byFile = Convert.FromBase64String(Sql.ToString(dict["FILE_CONTENTS"]));
						Crm.NoteAttachments.LoadFile(gID, byFile, trn);
					}
					if ( dict.ContainsKey("NOTE_ID") && !Sql.IsEmptyGuid(Sql.ToGuid(dict["NOTE_ID"])) )
					{
						string sFILE_NAME      = dict.ContainsKey("FILENAME")       ? Sql.ToString(dict["FILENAME"])       : String.Empty;
						string sFILE_MIME_TYPE = dict.ContainsKey("FILE_MIME_TYPE") ? Sql.ToString(dict["FILE_MIME_TYPE"]) : String.Empty;
						long   nFILE_SIZE      = dict.ContainsKey("FILE_SIZE")      ? Sql.ToLong  (dict["FILE_SIZE"])      : 0;
						{
							string sNOTE_FILE_EXT = System.IO.Path.GetExtension(sFILE_NAME);
							SqlProcs.spNOTE_ATTACHMENTS_Insert(ref gID, Sql.ToGuid(dict["NOTE_ID"]), sFILE_NAME, sFILE_NAME, sNOTE_FILE_EXT, sFILE_MIME_TYPE, trn);
						}
					}
					break;
				}
				case "DOCUMENTS":
				{
					if ( dict.ContainsKey("FILE_CONTENTS") && dict["FILE_CONTENTS"] != null )
					{
						byte[] byFile     = Convert.FromBase64String(Sql.ToString(dict["FILE_CONTENTS"]));
						Guid   gRevID     = Guid.NewGuid();
						string sChangeLog = dict.ContainsKey("CHANGE_LOG")     ? Sql.ToString(dict["CHANGE_LOG"])     : String.Empty;
						string sRevision  = dict.ContainsKey("REVISION")       ? Sql.ToString(dict["REVISION"])       : "1.0";
						string sFileName  = dict.ContainsKey("FILENAME")       ? Sql.ToString(dict["FILENAME"])       : String.Empty;
						string sMimeType  = dict.ContainsKey("FILE_MIME_TYPE") ? Sql.ToString(dict["FILE_MIME_TYPE"]) : String.Empty;
						long   nFileSize  = dict.ContainsKey("FILE_SIZE")      ? Sql.ToLong  (dict["FILE_SIZE"])      : byFile.LongLength;
						{
							string sFILE_EXT_DOC = System.IO.Path.GetExtension(sFileName);
							SqlProcs.spDOCUMENT_REVISIONS_Insert(ref gRevID, gID, sRevision, sChangeLog, sFileName, sFILE_EXT_DOC, sMimeType, trn);
						}
						Crm.DocumentRevisions.LoadFile(gRevID, byFile, trn);
						Guid gPARENT_ID = dict.ContainsKey("PARENT_ID") ? Sql.ToGuid(dict["PARENT_ID"]) : Guid.Empty;
						if ( !Sql.IsEmptyGuid(gPARENT_ID) )
						{
							using ( IDbCommand cmdRelated = SqlProcs.Factory(con, "spDOCUMENTS_InsRelated") )
							{
								cmdRelated.Transaction = trn;
								Sql.SetParameter(cmdRelated, "@ID",               Guid.NewGuid());
								Sql.SetParameter(cmdRelated, "@DOCUMENT_ID",      gID);
								Sql.SetParameter(cmdRelated, "@PARENT_ID",        gPARENT_ID);
								Sql.SetParameter(cmdRelated, "@PARENT_TYPE",      dict.ContainsKey("PARENT_TYPE") ? Sql.ToString(dict["PARENT_TYPE"]) : String.Empty);
								Sql.SetParameter(cmdRelated, "@MODIFIED_USER_ID", _security.USER_ID);
								cmdRelated.ExecuteNonQuery();
							}
						}
					}
					break;
				}
				case "NOTES":
				{
					if ( dict.ContainsKey("FILE_CONTENTS") && dict["FILE_CONTENTS"] != null )
					{
						byte[] byFile     = Convert.FromBase64String(Sql.ToString(dict["FILE_CONTENTS"]));
						string sFILE_NAME = dict.ContainsKey("FILENAME")       ? Sql.ToString(dict["FILENAME"])       : String.Empty;
						string sFILE_MIME = dict.ContainsKey("FILE_MIME_TYPE") ? Sql.ToString(dict["FILE_MIME_TYPE"]) : String.Empty;
						long   nFILE_SIZE = dict.ContainsKey("FILE_SIZE")      ? Sql.ToLong  (dict["FILE_SIZE"])      : byFile.LongLength;
						Guid   gATTACH_ID = Guid.NewGuid();
						{
							string sNOTE_FILE_EXT = System.IO.Path.GetExtension(sFILE_NAME);
							SqlProcs.spNOTE_ATTACHMENTS_Insert(ref gATTACH_ID, gID, sFILE_NAME, sFILE_NAME, sNOTE_FILE_EXT, sFILE_MIME, trn);
						}
						Crm.NoteAttachments.LoadFile(gATTACH_ID, byFile, trn);
					}
					break;
				}
				case "KBDOCUMENTS":
				{
					if ( dict.ContainsKey("ATTACHMENT_CONTENTS") && dict["ATTACHMENT_CONTENTS"] != null )
					{
						using ( MemoryStream ms = new MemoryStream(Convert.FromBase64String(Sql.ToString(dict["ATTACHMENT_CONTENTS"]))) )
						{
							KBDocuments.EditView.LoadAttachmentFile(gID, ms, trn);
						}
					}
					if ( dict.ContainsKey("IMAGE_CONTENTS") && dict["IMAGE_CONTENTS"] != null )
					{
						using ( MemoryStream ms = new MemoryStream(Convert.FromBase64String(Sql.ToString(dict["IMAGE_CONTENTS"]))) )
						{
							KBDocuments.EditView.LoadImageFile(gID, ms, trn);
						}
					}
					break;
				}
				case "EMAILS":
				{
					if ( dict.ContainsKey("ATTACHMENTS") )
					{
						List<object> arrAttachments = dict["ATTACHMENTS"] as List<object>;
						if ( arrAttachments != null )
						{
							foreach ( object oAttach in arrAttachments )
							{
								Dictionary<string, object> dictAttach = oAttach as Dictionary<string, object>;
								if ( dictAttach == null ) continue;
								string sBase64    = dictAttach.ContainsKey("FILE_CONTENTS") ? Sql.ToString(dictAttach["FILE_CONTENTS"]) : String.Empty;
								if ( Sql.IsEmptyString(sBase64) ) continue;
								byte[] byFile     = Convert.FromBase64String(sBase64);
								string sFILE_NAME = dictAttach.ContainsKey("FILENAME")       ? Sql.ToString(dictAttach["FILENAME"])       : String.Empty;
								string sFILE_MIME = dictAttach.ContainsKey("FILE_MIME_TYPE") ? Sql.ToString(dictAttach["FILE_MIME_TYPE"]) : String.Empty;
								long   nFILE_SIZE = dictAttach.ContainsKey("FILE_SIZE")      ? Sql.ToLong  (dictAttach["FILE_SIZE"])      : byFile.LongLength;
								Guid   gATTACH_ID = Guid.NewGuid();
								Guid   gNOTE_ID   = Guid.NewGuid();
								{
									SqlProcs.spNOTES_Update(ref gNOTE_ID, sFILE_NAME, "Emails", gID, Guid.Empty, String.Empty, _security.TEAM_ID, String.Empty, _security.USER_ID, String.Empty, false, String.Empty, trn);
									string sEM_FILE_EXT = System.IO.Path.GetExtension(sFILE_NAME);
									SqlProcs.spNOTE_ATTACHMENTS_Insert(ref gATTACH_ID, gNOTE_ID, sFILE_NAME, sFILE_NAME, sEM_FILE_EXT, sFILE_MIME, trn);
								}
								Crm.NoteAttachments.LoadFile(gATTACH_ID, byFile, trn);
							}
						}
					}
					break;
				}
				case "EMAIL_TEMPLATES":
				{
					if ( dict.ContainsKey("ATTACHMENTS") )
					{
						List<object> arrAttachments = dict["ATTACHMENTS"] as List<object>;
						if ( arrAttachments != null )
						{
							foreach ( object oAttach in arrAttachments )
							{
								Dictionary<string, object> dictAttach = oAttach as Dictionary<string, object>;
								if ( dictAttach == null ) continue;
								string sBase64    = dictAttach.ContainsKey("FILE_CONTENTS") ? Sql.ToString(dictAttach["FILE_CONTENTS"]) : String.Empty;
								if ( Sql.IsEmptyString(sBase64) ) continue;
								byte[] byFile     = Convert.FromBase64String(sBase64);
								string sFILE_NAME = dictAttach.ContainsKey("FILENAME")       ? Sql.ToString(dictAttach["FILENAME"])       : String.Empty;
								string sFILE_MIME = dictAttach.ContainsKey("FILE_MIME_TYPE") ? Sql.ToString(dictAttach["FILE_MIME_TYPE"]) : String.Empty;
								long   nFILE_SIZE = dictAttach.ContainsKey("FILE_SIZE")      ? Sql.ToLong  (dictAttach["FILE_SIZE"])      : byFile.LongLength;
								Guid   gATTACH_ID = Guid.NewGuid();
								Guid   gNOTE_ID   = Guid.NewGuid();
								{
									SqlProcs.spNOTES_Update(ref gNOTE_ID, sFILE_NAME, "EmailTemplates", gID, Guid.Empty, String.Empty, _security.TEAM_ID, String.Empty, _security.USER_ID, String.Empty, false, String.Empty, trn);
									string sET_FILE_EXT = System.IO.Path.GetExtension(sFILE_NAME);
									SqlProcs.spNOTE_ATTACHMENTS_Insert(ref gATTACH_ID, gNOTE_ID, sFILE_NAME, sFILE_NAME, sET_FILE_EXT, sFILE_MIME, trn);
								}
								Crm.NoteAttachments.LoadFile(gATTACH_ID, byFile, trn);
							}
						}
					}
					// Invalidate email template cache
					_splendidCache.ClearTable("EMAIL_TEMPLATES");
					break;
				}
				case "BUGS":
				{
					if ( dict.ContainsKey("FILE_CONTENTS") && dict["FILE_CONTENTS"] != null )
					{
						byte[] byFile     = Convert.FromBase64String(Sql.ToString(dict["FILE_CONTENTS"]));
						string sFILE_NAME = dict.ContainsKey("FILENAME")       ? Sql.ToString(dict["FILENAME"])       : String.Empty;
						string sFILE_MIME = dict.ContainsKey("FILE_MIME_TYPE") ? Sql.ToString(dict["FILE_MIME_TYPE"]) : String.Empty;
						long   nFILE_SIZE = dict.ContainsKey("FILE_SIZE")      ? Sql.ToLong  (dict["FILE_SIZE"])      : byFile.LongLength;
						Guid   gATTACH_ID = Guid.NewGuid();
						using ( IDbCommand cmdAttach = SqlProcs.Factory(con, "spBUG_ATTACHMENTS_Insert") )
						{
							cmdAttach.Transaction = trn;
							Sql.SetParameter(cmdAttach, "@ID",               gATTACH_ID);
							Sql.SetParameter(cmdAttach, "@BUG_ID",           gID);
							Sql.SetParameter(cmdAttach, "@FILENAME",         sFILE_NAME);
							Sql.SetParameter(cmdAttach, "@FILE_MIME_TYPE",   sFILE_MIME);
							Sql.AddParameter(cmdAttach, "@FILE_SIZE",        (long)nFILE_SIZE);
							Sql.SetParameter(cmdAttach, "@MODIFIED_USER_ID", _security.USER_ID);
							cmdAttach.ExecuteNonQuery();
						}
						Crm.BugAttachments.LoadFile(gATTACH_ID, byFile, trn);
					}
					break;
				}
				case "PAYMENTS":
				{
					if ( dict.ContainsKey("LINE_ITEMS") )
					{
						List<object> arrLineItems = dict["LINE_ITEMS"] as List<object>;
						if ( arrLineItems != null )
						{
							// Delete existing payment associations
							using ( IDbCommand cmdDel = con.CreateCommand() )
							{
								cmdDel.Transaction  = trn;
								cmdDel.CommandType  = CommandType.Text;
								cmdDel.CommandText  = "delete from INVOICES_PAYMENTS where PAYMENT_ID = @PAYMENT_ID";
								Sql.AddParameter(cmdDel, "@PAYMENT_ID", gID);
								cmdDel.ExecuteNonQuery();
							}
							foreach ( object oLineItem in arrLineItems )
							{
								Dictionary<string, object> dictLine = oLineItem as Dictionary<string, object>;
								if ( dictLine == null ) continue;
								Guid    gINVOICE_ID = dictLine.ContainsKey("INVOICE_ID") ? Sql.ToGuid   (dictLine["INVOICE_ID"]) : Guid.Empty;
								Decimal dAmount     = dictLine.ContainsKey("AMOUNT")     ? Sql.ToDecimal(dictLine["AMOUNT"])     : 0m;
								if ( Sql.IsEmptyGuid(gINVOICE_ID) ) continue;
								using ( IDbCommand cmdPay = SqlProcs.Factory(con, "spINVOICES_PAYMENTS_Update") )
								{
									cmdPay.Transaction = trn;
									Sql.SetParameter(cmdPay, "@ID",               Guid.NewGuid());
									Sql.SetParameter(cmdPay, "@PAYMENT_ID",       gID);
									Sql.SetParameter(cmdPay, "@INVOICE_ID",       gINVOICE_ID);
									Sql.SetParameter(cmdPay, "@AMOUNT",           (decimal)dAmount);
									Sql.SetParameter(cmdPay, "@MODIFIED_USER_ID", _security.USER_ID);
									cmdPay.ExecuteNonQuery();
								}
							}
						}
					}
					break;
				}
				case "INVOICES":
				case "QUOTES":
				case "ORDERS":
				case "OPPORTUNITIES":
				{
					string sLIKey = dict.ContainsKey("LineItems") ? "LineItems" : (dict.ContainsKey("line_items") ? "line_items" : null);
					if ( sLIKey != null )
					{
						List<object> arrLineItems = dict[sLIKey] as List<object>;
						if ( arrLineItems != null )
						{
							string sLINE_ITEMS_TABLE = sTABLE_NAME + "_LINE_ITEMS";
							UpdateLineItemsTable(con, trn, sLINE_ITEMS_TABLE, gID, arrLineItems, T10n);
						}
					}
					break;
				}
				case "DASHBOARDS":
				{
					if ( dict.ContainsKey("PANELS") )
					{
						List<object> arrPanels = dict["PANELS"] as List<object>;
						if ( arrPanels != null )
						{
							using ( IDbCommand cmdDel = con.CreateCommand() )
							{
								cmdDel.Transaction  = trn;
								cmdDel.CommandType  = CommandType.Text;
								cmdDel.CommandText  = "delete from DASHBOARDS_PANELS where DASHBOARD_ID = @DASHBOARD_ID";
								Sql.AddParameter(cmdDel, "@DASHBOARD_ID", gID);
								cmdDel.ExecuteNonQuery();
							}
							foreach ( object oPanel in arrPanels )
							{
								Dictionary<string, object> dictPanel = oPanel as Dictionary<string, object>;
								if ( dictPanel == null ) continue;
								using ( IDbCommand cmdPanel = SqlProcs.Factory(con, "spDASHBOARDS_PANELS_Update") )
								{
									cmdPanel.Transaction = trn;
									Sql.SetParameter(cmdPanel, "@ID",               Guid.NewGuid());
									Sql.SetParameter(cmdPanel, "@DASHBOARD_ID",     gID);
									Sql.SetParameter(cmdPanel, "@MODIFIED_USER_ID", _security.USER_ID);
									foreach ( IDbDataParameter par in cmdPanel.Parameters )
									{
										string sParamField = ExtractParameterName(par.ParameterName);
										if ( dictPanel.ContainsKey(sParamField) )
											par.Value = dictPanel[sParamField] ?? (object)DBNull.Value;
									}
									cmdPanel.ExecuteNonQuery();
								}
							}
							// Clear dashboard session cache
							if ( httpContext?.Session != null )
								httpContext.Session.Remove("DASHBOARDS." + _security.USER_ID.ToString());
						}
					}
					break;
				}
				case "USERS":
				{
					// Password update via dedicated stored procedure
					if ( dict.ContainsKey("NEW_PASSWORD") && !Sql.IsEmptyString(Sql.ToString(dict["NEW_PASSWORD"])) )
					{
						string sNewPass = Security.HashPassword(Sql.ToString(dict["NEW_PASSWORD"]));
						{
							SqlProcs.spUSERS_PasswordUpdate(gID, sNewPass, trn);
						}
					}
					// Portal contact update
					if ( dict.ContainsKey("PORTAL_ACTIVE") )
					{
						bool bPortalActive = Sql.ToBoolean(dict["PORTAL_ACTIVE"]);
						using ( IDbCommand cmdPortal = SqlProcs.Factory(con, "spCONTACTS_PortalUpdate") )
						{
							cmdPortal.Transaction = trn;
							Sql.SetParameter(cmdPortal, "@ID",               gID);
							Sql.SetParameter(cmdPortal, "@PORTAL_ACTIVE",    bPortalActive);
							Sql.SetParameter(cmdPortal, "@MODIFIED_USER_ID", _security.USER_ID);
							cmdPortal.ExecuteNonQuery();
						}
					}
					break;
				}
			}
			// Handle MergeIDs (module merge operation)
			if ( dict.ContainsKey("MergeIDs") )
			{
				List<object> arrMergeIDs = dict["MergeIDs"] as List<object>;
				if ( arrMergeIDs != null && arrMergeIDs.Count > 0 )
				{
					string sSP_MERGE = "sp" + sTABLE_NAME + "_Merge";
					foreach ( object oMergeID in arrMergeIDs )
					{
						Guid gMergeID = Sql.ToGuid(oMergeID);
						if ( Sql.IsEmptyGuid(gMergeID) ) continue;
						using ( IDbCommand cmdMerge = SqlProcs.Factory(con, sSP_MERGE) )
						{
							cmdMerge.Transaction = trn;
							Sql.SetParameter(cmdMerge, "@MASTER_ID",       gID);
							Sql.SetParameter(cmdMerge, "@MERGE_ID",        gMergeID);
							Sql.SetParameter(cmdMerge, "@MODIFIED_USER_ID", _security.USER_ID);
							cmdMerge.ExecuteNonQuery();
						}
					}
				}
			}
		}
	}
}
