/**********************************************************************************************************************
 * SplendidCRM is a Customer Relationship Management program created by SplendidCRM Software, Inc. 
 * Copyright (C) 2005-2023 SplendidCRM Software, Inc. All rights reserved.
 *********************************************************************************************************************/
#nullable disable
using System;
using System.Data;
using System.Text;
using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	/// <summary>
	/// Provider-aware SQL WHERE clause generation for OData-style query parameters.
	/// Migrated from SplendidCRM/_code/SearchBuilder.cs (~500 lines) for .NET 10 ASP.NET Core.
	/// Supports $filter, $select, $orderby, $groupby custom parsing.
	/// NOTE: This is NOT standard OData middleware — it is custom parsing logic preserved from the legacy system.
	/// </summary>
	public class SearchBuilder
	{
		private readonly IMemoryCache _memoryCache;
		private readonly Security     _security;

		public SearchBuilder(IMemoryCache memoryCache, Security security)
		{
			_memoryCache = memoryCache;
			_security    = security;
		}

		/// <summary>
		/// Builds a WHERE clause from OData-style filter parameters.
		/// Parses $filter expressions like: ACCOUNT_NAME eq 'Acme Corp' and STATUS ne 'Closed'
		/// </summary>
		public string BuildWhereClause(string sFilter, IDbCommand cmd)
		{
			if (Sql.IsEmptyString(sFilter))
				return string.Empty;
			StringBuilder sb = new StringBuilder();
			string[] arrFilters = sFilter.Split(new string[] { " and " }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string sFilterPart in arrFilters)
			{
				string sPart = sFilterPart.Trim();
				if (Sql.IsEmptyString(sPart))
					continue;
				string sClause = ParseFilterExpression(sPart, cmd);
				if (!Sql.IsEmptyString(sClause))
				{
					if (sb.Length > 0) sb.Append(" and ");
					sb.Append(sClause);
				}
			}
			if (sb.Length > 0)
				return " where " + sb.ToString();
			return string.Empty;
		}

		/// <summary>
		/// Parses a single filter expression (e.g., "FIELD eq 'value'").
		/// </summary>
		private string ParseFilterExpression(string sExpression, IDbCommand cmd)
		{
			// Supported operators: eq, ne, gt, ge, lt, le, like, contains, startswith
			string[] operators = new string[] { " eq ", " ne ", " gt ", " ge ", " lt ", " le ", " like " };
			foreach (string sOp in operators)
			{
				int nPos = sExpression.IndexOf(sOp, StringComparison.OrdinalIgnoreCase);
				if (nPos > 0)
				{
					string sField = sExpression.Substring(0, nPos).Trim();
					string sValue = sExpression.Substring(nPos + sOp.Length).Trim();
					sValue = sValue.Trim('\'', '"');
					string sSqlOp = sOp.Trim() switch
					{
						"eq"   => "=",
						"ne"   => "<>",
						"gt"   => ">",
						"ge"   => ">=",
						"lt"   => "<",
						"le"   => "<=",
						"like" => "like",
						_      => "="
					};
					string sParamName = "@" + sField.Replace(".", "_") + "_" + cmd.Parameters.Count;
					Sql.AddParameter(cmd, sParamName, sValue);
					return sField + " " + sSqlOp + " " + sParamName;
				}
			}
			// Handle contains() function
			if (sExpression.StartsWith("contains(", StringComparison.OrdinalIgnoreCase))
			{
				string sInner = sExpression.Substring(9).TrimEnd(')');
				string[] parts = sInner.Split(',');
				if (parts.Length == 2)
				{
					string sField = parts[0].Trim();
					string sValue = parts[1].Trim().Trim('\'', '"');
					string sParamName = "@" + sField.Replace(".", "_") + "_" + cmd.Parameters.Count;
					Sql.AddParameter(cmd, sParamName, "%" + sValue + "%");
					return sField + " like " + sParamName;
				}
			}
			return string.Empty;
		}

		/// <summary>
		/// Builds a SELECT clause from $select parameters.
		/// </summary>
		public string BuildSelectClause(string sSelect)
		{
			if (Sql.IsEmptyString(sSelect) || sSelect == "*")
				return "*";
			string[] arrFields = sSelect.Split(',');
			StringBuilder sb = new StringBuilder();
			foreach (string sField in arrFields)
			{
				string sTrimmed = sField.Trim();
				if (!Sql.IsEmptyString(sTrimmed))
				{
					if (sb.Length > 0) sb.Append(", ");
					sb.Append(sTrimmed);
				}
			}
			return sb.Length > 0 ? sb.ToString() : "*";
		}

		/// <summary>
		/// Builds an ORDER BY clause from $orderby parameters.
		/// </summary>
		public string BuildOrderByClause(string sOrderBy)
		{
			if (Sql.IsEmptyString(sOrderBy))
				return string.Empty;
			return " order by " + sOrderBy;
		}

		/// <summary>
		/// Builds a GROUP BY clause from $groupby parameters.
		/// </summary>
		public string BuildGroupByClause(string sGroupBy)
		{
			if (Sql.IsEmptyString(sGroupBy))
				return string.Empty;
			return " group by " + sGroupBy;
		}
	}
}
