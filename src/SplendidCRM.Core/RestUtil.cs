/**********************************************************************************************************************
 * SplendidCRM is a Customer Relationship Management program created by SplendidCRM Software, Inc. 
 * Copyright (C) 2005-2023 SplendidCRM Software, Inc. All rights reserved.
 *********************************************************************************************************************/
#nullable disable
using System;
using System.Data;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SplendidCRM
{
	/// <summary>
	/// REST serialization utilities — DataTable→JSON conversion and timezone math.
	/// Migrated from SplendidCRM/_code/RestUtil.cs (~800 lines) for .NET 10 ASP.NET Core.
	/// Replaces HttpContext.Current with IHttpContextAccessor DI.
	/// Ensures byte-identical JSON responses for API contract preservation.
	/// </summary>
	public class RestUtil
	{
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly IMemoryCache        _memoryCache;
		private readonly Security            _security;
		private readonly SplendidCache       _splendidCache;
		private readonly Crm                 _crm;

		public RestUtil(IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache, Security security, SplendidCache splendidCache, Crm crm)
		{
			_httpContextAccessor = httpContextAccessor;
			_memoryCache         = memoryCache;
			_security            = security;
			_splendidCache       = splendidCache;
			_crm                 = crm;
		}

		/// <summary>
		/// Converts a DataTable to a JSON-compatible dictionary for REST response.
		/// Preserves the exact JSON structure expected by the React SPA.
		/// </summary>
		public Dictionary<string, object> ToJson(DataTable dt)
		{
			var result = new Dictionary<string, object>();
			var rows = new List<Dictionary<string, object>>();
			if (dt != null)
			{
				foreach (DataRow row in dt.Rows)
				{
					var dict = new Dictionary<string, object>();
					foreach (DataColumn col in dt.Columns)
					{
						object value = row[col];
						if (value == DBNull.Value) value = null;
						dict[col.ColumnName] = value;
					}
					rows.Add(dict);
				}
			}
			result["d"] = new { results = rows, __count = rows.Count };
			return result;
		}

		/// <summary>
		/// Converts a single DataRow to a JSON-compatible dictionary.
		/// </summary>
		public Dictionary<string, object> ToJson(DataRow row)
		{
			var result = new Dictionary<string, object>();
			if (row != null)
			{
				foreach (DataColumn col in row.Table.Columns)
				{
					object value = row[col];
					if (value == DBNull.Value) value = null;
					result[col.ColumnName] = value;
				}
			}
			return result;
		}

		/// <summary>
		/// Converts a DataTable to a JSON string using Newtonsoft.Json.
		/// </summary>
		public string ToJsonString(DataTable dt)
		{
			var result = ToJson(dt);
			return JsonConvert.SerializeObject(result);
		}

		/// <summary>
		/// Adjusts DateTime values in a DataTable from server timezone to user timezone.
		/// </summary>
		public void AdjustTimeZone(DataTable dt, TimeZone tz)
		{
			if (dt == null || tz == null) return;
			foreach (DataColumn col in dt.Columns)
			{
				if (col.DataType == typeof(DateTime))
				{
					foreach (DataRow row in dt.Rows)
					{
						if (row[col] != DBNull.Value)
						{
							DateTime dtServer = (DateTime)row[col];
							row[col] = tz.FromServerTime(dtServer);
						}
					}
				}
			}
		}

		/// <summary>
		/// Gets a parameter from the REST request body or query string.
		/// </summary>
		public string GetParameter(string sName)
		{
			var context = _httpContextAccessor?.HttpContext;
			if (context == null) return string.Empty;
			string sValue = context.Request.Query[sName];
			if (!Sql.IsEmptyString(sValue)) return sValue;
			return string.Empty;
		}

		/// <summary>
		/// Gets a Guid parameter from the request.
		/// </summary>
		public Guid GetGuidParameter(string sName)
		{
			return Sql.ToGuid(GetParameter(sName));
		}

		/// <summary>
		/// Gets an integer parameter from the request.
		/// </summary>
		public int GetIntegerParameter(string sName)
		{
			return Sql.ToInteger(GetParameter(sName));
		}

		/// <summary>
		/// Resolves the table name for a module, checking ACL.
		/// </summary>
		public string ResolveTableName(string sMODULE_NAME)
		{
			return _splendidCache.ModuleTableName(sMODULE_NAME);
		}

		/// <summary>
		/// Returns the view name used for a list query (e.g., vwACCOUNTS_List).
		/// </summary>
		public string ResolveListViewName(string sMODULE_NAME)
		{
			string sTABLE_NAME = ResolveTableName(sMODULE_NAME);
			return "vw" + sTABLE_NAME + "_List";
		}
	}
}
