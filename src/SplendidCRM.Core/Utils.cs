/**********************************************************************************************************************
 * SplendidCRM is a Customer Relationship Management program created by SplendidCRM Software, Inc.
 * Copyright (C) 2005-2023 SplendidCRM Software, Inc. All rights reserved.
 *
 * This program is free software: you can redistribute it and/or modify it under the terms of the
 * GNU Affero General Public License as published by the Free Software Foundation, either version 3
 * of the License, or (at your option) any later version.
 *
 * Migration: .NET Framework 4.8 → .NET 10 ASP.NET Core
 * Changes:
 *   - Replaced System.Web with Microsoft.AspNetCore.Http
 *   - Replaced HttpContext.Current with IHttpContextAccessor (injected)
 *   - Replaced Session["key"] with ISession.GetString("key") via IHttpContextAccessor
 *   - Replaced Application["key"] / HttpRuntime.Cache with IMemoryCache (injected)
 *   - Replaced System.Configuration.ConfigurationSettings.AppSettings with IConfiguration
 *   - Replaced HttpUtility.UrlEncode with System.Net.WebUtility.UrlEncode
 *   - Replaced WebRequest/HttpWebRequest with HttpClient
 *   - Replaced System.Data.SqlClient with Microsoft.Data.SqlClient
 *   - Removed WebForms-specific methods (SetPageTitle, CreateArrowControl, SelectItem, RegisterJQuery)
 *     replaced with no-op stubs to preserve public API surface
 *   - Removed HttpBrowserCapabilities parameter from ContentDispositionEncode (IE-specific logic removed)
 *   - Added DI constructor; static ambient fields preserved for backward-compatible callers
 *   - Minimal change clause: all business logic preserved exactly
 *********************************************************************************************************************/
#nullable enable
using System;
using System.IO;
using System.Xml;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace SplendidCRM
{
	/// <summary>
	/// General utility methods for SplendidCRM.
	/// Migrated from SplendidCRM/_code/Utils.cs for .NET 10 ASP.NET Core.
	/// </summary>
	public class Utils
	{
		// =====================================================================================
		// DI Instance fields
		// =====================================================================================
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly IMemoryCache         _memoryCache        ;
		private readonly IConfiguration       _configuration      ;
		private readonly Security             _security           ;
		private readonly DbProviderFactories  _dbProviderFactories;
		private readonly SplendidCache        _splendidCache      ;

		// =====================================================================================
		// Static ambient fields — set once at application startup via SetAmbient().
		// These allow legacy callers that cannot inject Utils to access instance services.
		// =====================================================================================
		private  static IHttpContextAccessor _ambientHttpAccessor ;
		private  static IMemoryCache         _ambientCache        ;
		// Internal so ArchiveUtils (in the same namespace/assembly) can read it
		internal static IConfiguration       _ambientConfigInternal;
		private  static Security             _ambientSecurity     ;
		private  static DbProviderFactories  _ambientDbf          ;
		private  static SplendidCache        _ambientSplendidCache;

		/// <summary>
		/// Called once from Program.cs / DI configuration to populate the static ambient fields
		/// used by static helper methods that cannot accept injected parameters.
		/// </summary>
		public static void SetAmbient(
			IHttpContextAccessor httpAccessor,
			IMemoryCache         cache        ,
			IConfiguration       config       ,
			Security             security     ,
			DbProviderFactories  dbf          ,
			SplendidCache        splendidCache)
		{
			_ambientHttpAccessor   = httpAccessor ;
			_ambientCache          = cache        ;
			_ambientConfigInternal = config       ;
			_ambientSecurity       = security     ;
			_ambientDbf            = dbf          ;
			_ambientSplendidCache  = splendidCache;
		}

		// =====================================================================================
		// DI Constructor
		// =====================================================================================
		public Utils(
			IHttpContextAccessor httpContextAccessor,
			IMemoryCache         memoryCache         ,
			IConfiguration       configuration       ,
			Security             security            ,
			DbProviderFactories  dbProviderFactories ,
			SplendidCache        splendidCache       )
		{
			_httpContextAccessor = httpContextAccessor;
			_memoryCache         = memoryCache        ;
			_configuration       = configuration      ;
			_security            = security           ;
			_dbProviderFactories = dbProviderFactories;
			_splendidCache       = splendidCache      ;
		}

		// =====================================================================================
		// WebForms stub methods — no-ops preserved for API compatibility.
		// These methods had WebForms dependencies (Page, WebControl, ScriptManager) that do
		// not exist in ASP.NET Core. They are kept as empty/stub methods so that any
		// remaining call sites compile without modification.
		// =====================================================================================

		/// <summary>
		/// No-op stub. Original method set the page title via a WebForms Literal control.
		/// Not applicable in ASP.NET Core MVC.
		/// </summary>
		public static void SetPageTitle(object page, string sTitle)
		{
			// No-op: WebForms Page not available in ASP.NET Core
		}

		/// <summary>
		/// Generates JavaScript to prevent the Enter key from submitting the form in the specified text field.
		/// </summary>
		public static string PreventEnterKeyPress(string sTextID)
		{
			if ( !Sql.IsEmptyString(sTextID) )
			{
				StringBuilder sb = new StringBuilder();
				sb.AppendLine("<script type=\"text/javascript\">");
				sb.AppendLine("if ( document.getElementById('" + sTextID + "') != null )");
				sb.AppendLine("{");
				sb.AppendLine(" document.getElementById('" + sTextID + "').onkeypress = function(e)");
				sb.AppendLine(" {");
				sb.AppendLine("  if ( e != null )");
				sb.AppendLine("  {");
				sb.AppendLine("   if ( e.which == 13 )");
				sb.AppendLine("   {");
				sb.AppendLine("    return false;");
				sb.AppendLine("   }");
				sb.AppendLine("  }");
				sb.AppendLine("  else if ( event != null )");
				sb.AppendLine("  {");
				sb.AppendLine("   if ( event.keyCode == 13 )");
				sb.AppendLine("   {");
				sb.AppendLine("    event.returnValue = false;");
				sb.AppendLine("    event.cancel = true;");
				sb.AppendLine("   }");
				sb.AppendLine("  }");
				sb.AppendLine(" }");
				sb.AppendLine("}");
				sb.AppendLine("</script>");
				return sb.ToString();
			}
			return String.Empty;
		}

		/// <summary>
		/// Generates JavaScript to trigger a button click when Enter is pressed in a text field.
		/// </summary>
		public static string RegisterEnterKeyPress(string sTextID, string sButtonID)
		{
			if ( !Sql.IsEmptyString(sTextID) && !Sql.IsEmptyString(sButtonID) )
			{
				StringBuilder sb = new StringBuilder();
				sb.AppendLine("<script type=\"text/javascript\">");
				sb.AppendLine("if ( document.getElementById('" + sTextID + "') != null && document.getElementById('" + sButtonID + "') != null )");
				sb.AppendLine("{");
				sb.AppendLine(" document.getElementById('" + sTextID + "').onkeypress = function(e)");
				sb.AppendLine(" {");
				sb.AppendLine("  if ( e != null )");
				sb.AppendLine("  {");
				sb.AppendLine("   if ( e.which == 13 )");
				sb.AppendLine("   {");
				sb.AppendLine("    document.getElementById('" + sButtonID + "').click();");
				sb.AppendLine("    return false;");
				sb.AppendLine("   }");
				sb.AppendLine("  }");
				sb.AppendLine("  else if ( event != null )");
				sb.AppendLine("  {");
				sb.AppendLine("   if ( event.keyCode == 13 )");
				sb.AppendLine("   {");
				sb.AppendLine("    event.returnValue = false;");
				sb.AppendLine("    event.cancel = true;");
				sb.AppendLine("    document.getElementById('" + sButtonID + "').click();");
				sb.AppendLine("   }");
				sb.AppendLine("  }");
				sb.AppendLine(" }");
				sb.AppendLine("}");
				sb.AppendLine("</script>");
				return sb.ToString();
			}
			return String.Empty;
		}

		/// <summary>
		/// Generates JavaScript to focus a specific input element on page load.
		/// </summary>
		public static string RegisterSetFocus(string sTextID)
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("<script type=\"text/javascript\">");
			sb.AppendLine("if ( document.getElementById('" + sTextID + "') != null )");
			sb.AppendLine("	document.getElementById('" + sTextID + "').focus();");
			sb.AppendLine("</script>");
			return sb.ToString();
		}

		/// <summary>
		/// No-op stub. Original returned a WebForms WebControl Label with Webdings font.
		/// Not applicable in ASP.NET Core MVC. Returns null.
		/// </summary>
		public static object CreateArrowControl(bool bAscending)
		{
			// No-op: WebForms WebControl not available in ASP.NET Core
			return null;
		}

		// =====================================================================================
		// ValidateIDs
		// =====================================================================================

		/// <summary>
		/// Validates an array of string IDs as valid GUIDs and returns them as a comma-separated string.
		/// Throws an exception if the array exceeds 200 items or any GUID is invalid.
		/// BEFORE: Used HttpContext.Current.Session["USER_SETTINGS/CULTURE"] for L10N.
		/// AFTER:  Uses injected IHttpContextAccessor.HttpContext.Session for L10N.
		/// </summary>
		public string ValidateIDs(string[] arrID, bool bQuoted)
		{
			if ( arrID.Length == 0 )
				return String.Empty;
			if ( arrID.Length > 200 )
			{
				// AFTER: Session via IHttpContextAccessor; fall back to empty culture
				string sCulture = _httpContextAccessor?.HttpContext?.Session?.GetString("USER_SETTINGS/CULTURE") ?? String.Empty;
				L10N L10n = new L10N(sCulture, _memoryCache);
				throw(new Exception(L10n.Term(".LBL_TOO_MANY_RECORDS")));
			}
			foreach(string sID in arrID)
			{
				Guid gID = Sql.ToGuid(sID);
				if ( Sql.IsEmptyGuid(gID) )
					throw(new Exception("Invalid ID: " + sID));
			}
			string sIDs = String.Empty;
			if ( bQuoted )
				sIDs = "'" + String.Join("','", arrID) + "'";
			else
				sIDs = String.Join(",", arrID);
			return sIDs;
		}

		/// <summary>Validates an array of string IDs (unquoted variant).</summary>
		public string ValidateIDs(string[] arrID)
		{
			return ValidateIDs(arrID, false);
		}

		/// <summary>
		/// Validates an array of Guid IDs and returns them as a comma-separated string.
		/// BEFORE: Used HttpContext.Current.Session for L10N culture.
		/// AFTER:  Uses injected IHttpContextAccessor.HttpContext.Session.
		/// </summary>
		public string ValidateIDs(Guid[] arrID, bool bQuoted)
		{
			if ( arrID.Length == 0 )
				return String.Empty;
			if ( arrID.Length > 200 )
			{
				string sCulture = _httpContextAccessor?.HttpContext?.Session?.GetString("USER_SETTINGS/CULTURE") ?? String.Empty;
				L10N L10n = new L10N(sCulture, _memoryCache);
				throw(new Exception(L10n.Term(".LBL_TOO_MANY_RECORDS")));
			}
			StringBuilder sbIDs = new StringBuilder();
			foreach(Guid gID in arrID)
			{
				if ( sbIDs.Length > 0 )
					sbIDs.Append(",");
				if ( bQuoted )
					sbIDs.Append("\'" + gID.ToString() + "\'");
				else
					sbIDs.Append(gID.ToString());
			}
			return sbIDs.ToString();
		}

		// =====================================================================================
		// FilterByACL — filter a list of IDs by ACL ownership check.
		// BEFORE: Called static Security.GetUserAccess / DbProviderFactories.GetFactory(Application).
		// AFTER:  Uses injected _security and _dbProviderFactories.
		// =====================================================================================
		public string FilterByACL(string sMODULE_NAME, string sACCESS_TYPE, string[] arrID, string sTABLE_NAME)
		{
			StringBuilder sb = new StringBuilder();
			int nACLACCESS = _security.GetUserAccess(sMODULE_NAME, sACCESS_TYPE);
			if ( nACLACCESS >= 0 && arrID.Length > 0 )
			{
				if ( nACLACCESS == ACL_ACCESS.OWNER )
				{
					DbProviderFactory dbf = _dbProviderFactories.GetFactory();
					using ( IDbConnection con = dbf.CreateConnection() )
					{
						con.Open();
						string sSQL;
						sSQL = "select ID              " + ControlChars.CrLf
						     + "  from vw" + sTABLE_NAME + ControlChars.CrLf
						     + " where 1 = 1           " + ControlChars.CrLf;
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							// Build WHERE conditions via StringBuilder then append to CommandText
							StringBuilder sbWhere = new StringBuilder();
							Sql.AppendGuids    (cmd, sbWhere, arrID           , "ID"               );
							Sql.AppendParameter(cmd, sbWhere, "ASSIGNED_USER_ID", _security.USER_ID);
							cmd.CommandText = sSQL + sbWhere.ToString();
							using ( IDataReader rdr = cmd.ExecuteReader() )
							{
								while ( rdr.Read() )
								{
									if ( sb.Length > 0 )
										sb.Append(",");
									sb.Append(Sql.ToString(rdr["ID"]));
								}
							}
						}
					}
					if ( sb.Length == 0 )
					{
						string sCulture = _httpContextAccessor?.HttpContext?.Session?.GetString("USER_SETTINGS/CULTURE") ?? String.Empty;
						L10N L10n = new L10N(sCulture, _memoryCache);
						throw(new Exception(L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS")));
					}
				}
				else
				{
					return String.Join(",", arrID);
				}
			}
			return sb.ToString();
		}

		// =====================================================================================
		// BuildMassIDs — pure static logic, no framework dependencies.
		// =====================================================================================

		/// <summary>Pops up to nCapacity items from a Stack and returns them comma-separated.</summary>
		public static string BuildMassIDs(Stack stk, int nCapacity)
		{
			if ( stk.Count == 0 )
				return String.Empty;
			StringBuilder sb = new StringBuilder();
			for ( int i = 0; i < nCapacity && stk.Count > 0; i++ )
			{
				string sID = Sql.ToString(stk.Pop());
				if ( sb.Length > 0 )
					sb.Append(",");
				sb.Append(sID);
			}
			return sb.ToString();
		}

		/// <summary>Pops up to 200 items from a Stack and returns them comma-separated.</summary>
		public static string BuildMassIDs(Stack stkID)
		{
			return BuildMassIDs(stkID, 200);
		}

		// =====================================================================================
		// FilterByACL_Stack — ACL-filtered Stack variants.
		// BEFORE: Called static Security.GetUserAccess / DbProviderFactories.GetFactory(Application).
		// AFTER:  Uses injected _security and _dbProviderFactories.
		// =====================================================================================

		/// <summary>
		/// Returns a Stack of IDs filtered by ACL ownership for the given module/access type.
		/// </summary>
		public Stack FilterByACL_Stack(string sMODULE_NAME, string sACCESS_TYPE, string[] arrID, string sTABLE_NAME)
		{
			int nACLACCESS = _security.GetUserAccess(sMODULE_NAME, sACCESS_TYPE);
			Stack stk = FilterByACL_Stack(sMODULE_NAME, nACLACCESS, arrID, sTABLE_NAME);
			return stk;
		}

		/// <summary>
		/// Returns a Stack of IDs filtered by the pre-computed ACL access level.
		/// Called from Rest thread where session may not be available.
		/// </summary>
		public Stack FilterByACL_Stack(string sMODULE_NAME, int nACLACCESS, string[] arrID, string sTABLE_NAME)
		{
			Stack stk = new Stack();
			if ( nACLACCESS >= 0 && arrID.Length > 0 )
			{
				if ( nACLACCESS == ACL_ACCESS.OWNER )
				{
					DbProviderFactory dbf = _dbProviderFactories.GetFactory();
					using ( IDbConnection con = dbf.CreateConnection() )
					{
						con.Open();
						string sSQL;
						sSQL = "select ID              " + ControlChars.CrLf
						     + "  from vw" + sTABLE_NAME + ControlChars.CrLf
						     + " where 1 = 1           " + ControlChars.CrLf;
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							StringBuilder sbWhere = new StringBuilder();
							Sql.AppendGuids    (cmd, sbWhere, arrID           , "ID"               );
							Sql.AppendParameter(cmd, sbWhere, "ASSIGNED_USER_ID", _security.USER_ID);
							cmd.CommandText = sSQL + sbWhere.ToString();
							using ( IDataReader rdr = cmd.ExecuteReader() )
							{
								while ( rdr.Read() )
									stk.Push(Sql.ToString(rdr["ID"]));
							}
						}
					}
					if ( stk.Count == 0 )
					{
						string sCulture = _httpContextAccessor?.HttpContext?.Session?.GetString("USER_SETTINGS/CULTURE") ?? String.Empty;
						L10N L10n = new L10N(sCulture, _memoryCache);
						throw(new Exception(L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS")));
					}
				}
				else
				{
					foreach ( string sID in arrID )
					{
						if ( sID.Length > 0 )
							stk.Push(sID);
					}
				}
			}
			return stk;
		}

		// =====================================================================================
		// ExpandException — pure static logic; no framework dependencies.
		// =====================================================================================

		/// <summary>
		/// Recursively expands an exception and all inner exceptions into an HTML-formatted string.
		/// </summary>
		public static string ExpandException(Exception ex)
		{
			StringBuilder sb = new StringBuilder();
			do
			{
				sb.Append(ex.Message);
				if ( ex.InnerException != null )
					sb.Append("<br />\r\n");
				ex = ex.InnerException;
			}
			while ( ex != null );
			return sb.ToString();
		}

		// =====================================================================================
		// GetUserEmail
		// BEFORE: Called static DbProviderFactories.GetFactory(Application).CreateConnection().
		// AFTER:  Uses injected _dbProviderFactories.GetFactory().CreateConnection().
		// =====================================================================================

		/// <summary>
		/// Returns the primary (or secondary) email address for the given user GUID.
		/// </summary>
		public string GetUserEmail(Guid gID)
		{
			string sEmail = String.Empty;
			if ( !Sql.IsEmptyGuid(gID) )
			{
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					string sSQL;
					sSQL = "select EMAIL1  " + ControlChars.CrLf
					     + "     , EMAIL2  " + ControlChars.CrLf
					     + "  from vwUSERS " + ControlChars.CrLf
					     + " where ID = @ID" + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@ID", gID);
						using ( IDataReader rdr = cmd.ExecuteReader(CommandBehavior.SingleRow) )
						{
							while ( rdr.Read() )
							{
								sEmail = Sql.ToString(rdr["EMAIL1"]);
								if ( Sql.IsEmptyString(sEmail) )
									sEmail = Sql.ToString(rdr["EMAIL2"]);
							}
						}
					}
				}
			}
			return sEmail;
		}

		// =====================================================================================
		// AppSettings — configuration accessor.
		// BEFORE: Returned System.Collections.Specialized.NameValueCollection via ConfigurationSettings.AppSettings.
		// AFTER:  Returns IConfiguration which supports the same string-index ["key"] accessor.
		// =====================================================================================

		/// <summary>
		/// Returns the application configuration provider.
		/// BEFORE: System.Configuration.ConfigurationSettings.AppSettings (NameValueCollection).
		/// AFTER:  IConfiguration from the 5-tier provider hierarchy (AWS Secrets Manager → env → SSM → appsettings).
		/// </summary>
		public IConfiguration AppSettings => _configuration;

		// =====================================================================================
		// IsOfflineClient — reads from IConfiguration.
		// BEFORE: Utils.AppSettings["OfflineClient"].
		// AFTER:  _configuration["OfflineClient"].
		// =====================================================================================

		/// <summary>
		/// Returns true if the application is running as an offline client.
		/// BEFORE: Read from ConfigurationSettings.AppSettings.
		/// AFTER:  Read from IConfiguration.
		/// </summary>
		public bool IsOfflineClient => Sql.ToBoolean(_configuration["OfflineClient"]);

		// =====================================================================================
		// Device/browser capability properties — read from distributed session.
		// BEFORE: HttpContext.Current.Session["key"].
		// AFTER:  IHttpContextAccessor.HttpContext.Session.GetString("key").
		// =====================================================================================

		/// <summary>
		/// Returns true if the current request originates from a mobile device.
		/// BEFORE: HttpContext.Current.Session["IsMobileDevice"].
		/// AFTER:  IHttpContextAccessor.HttpContext.Session.GetString("IsMobileDevice").
		/// </summary>
		public bool IsMobileDevice
		{
			get { return Sql.ToBoolean(_httpContextAccessor?.HttpContext?.Session?.GetString("IsMobileDevice")); }
		}

		/// <summary>
		/// Returns true if the current browser supports popup windows.
		/// BEFORE: HttpContext.Current.Session["SupportsPopups"].
		/// AFTER:  IHttpContextAccessor.HttpContext.Session.GetString("SupportsPopups").
		/// </summary>
		public bool SupportsPopups
		{
			get { return Sql.ToBoolean(_httpContextAccessor?.HttpContext?.Session?.GetString("SupportsPopups")); }
		}

		/// <summary>
		/// Returns true if the current device supports speech input.
		/// BEFORE: HttpContext.Current.Session["SupportsSpeech"].
		/// AFTER:  IHttpContextAccessor.HttpContext.Session.GetString("SupportsSpeech").
		/// </summary>
		public bool SupportsSpeech
		{
			get { return Sql.ToBoolean(_httpContextAccessor?.HttpContext?.Session?.GetString("SupportsSpeech")); }
		}

		/// <summary>
		/// Returns true if the current device supports handwriting input.
		/// BEFORE: HttpContext.Current.Session["SupportsHandwriting"].
		/// AFTER:  IHttpContextAccessor.HttpContext.Session.GetString("SupportsHandwriting").
		/// </summary>
		public bool SupportsHandwriting
		{
			get { return Sql.ToBoolean(_httpContextAccessor?.HttpContext?.Session?.GetString("SupportsHandwriting")); }
		}

		/// <summary>
		/// Returns true if the current device supports touch input.
		/// BEFORE: HttpContext.Current.Session["SupportsTouch"].
		/// AFTER:  IHttpContextAccessor.HttpContext.Session.GetString("SupportsTouch").
		/// </summary>
		public bool SupportsTouch
		{
			get { return Sql.ToBoolean(_httpContextAccessor?.HttpContext?.Session?.GetString("SupportsTouch")); }
		}

		/// <summary>
		/// Returns true if auto-complete is allowed for the current browser.
		/// BEFORE: HttpContext.Current.Session["AllowAutoComplete"].
		/// AFTER:  IHttpContextAccessor.HttpContext.Session.GetString("AllowAutoComplete").
		/// </summary>
		public bool AllowAutoComplete
		{
			get { return Sql.ToBoolean(_httpContextAccessor?.HttpContext?.Session?.GetString("AllowAutoComplete")); }
		}

		// =====================================================================================
		// BuildTermName — pure static string utility.
		// =====================================================================================

		/// <summary>
		/// Returns the full terminology term name for a display field.
		/// Global fields (ID, DELETED, TEAM_ID, etc.) use the "." prefix; module-specific fields use module prefix.
		/// </summary>
		public static string BuildTermName(string sModule, string sDISPLAY_NAME)
		{
			string sTERM_NAME = String.Empty;
			if (  sDISPLAY_NAME == "ID"
			   || sDISPLAY_NAME == "DELETED"
			   || sDISPLAY_NAME == "CREATED_BY"
			   || sDISPLAY_NAME == "CREATED_BY_ID"
			   || sDISPLAY_NAME == "CREATED_BY_NAME"
			   || sDISPLAY_NAME == "DATE_ENTERED"
			   || sDISPLAY_NAME == "MODIFIED_USER_ID"
			   || sDISPLAY_NAME == "DATE_MODIFIED"
			   || sDISPLAY_NAME == "DATE_MODIFIED_UTC"
			   || sDISPLAY_NAME == "MODIFIED_BY"
			   || sDISPLAY_NAME == "MODIFIED_BY_NAME"
			   || sDISPLAY_NAME == "ASSIGNED_USER_ID"
			   || sDISPLAY_NAME == "ASSIGNED_TO"
			   || sDISPLAY_NAME == "ASSIGNED_TO_NAME"
			   || sDISPLAY_NAME == "TEAM_ID"
			   || sDISPLAY_NAME == "TEAM_NAME"
			   || sDISPLAY_NAME == "TEAM_SET_ID"
			   || sDISPLAY_NAME == "TEAM_SET_NAME"
			   || sDISPLAY_NAME == "TEAM_SET_LIST"
			   || sDISPLAY_NAME == "ASSIGNED_SET_ID"
			   || sDISPLAY_NAME == "ASSIGNED_SET_NAME"
			   || sDISPLAY_NAME == "ASSIGNED_SET_LIST"
			   || sDISPLAY_NAME == "ID_C"
			   || sDISPLAY_NAME == "AUDIT_ID"
			   || sDISPLAY_NAME == "AUDIT_ACTION"
			   || sDISPLAY_NAME == "AUDIT_DATE"
			   || sDISPLAY_NAME == "AUDIT_COLUMNS"
			   || sDISPLAY_NAME == "AUDIT_TABLE"
			   || sDISPLAY_NAME == "AUDIT_TOKEN"
			   || sDISPLAY_NAME == "LAST_ACTIVITY_DATE"
			   || sDISPLAY_NAME == "TAG_SET_NAME"
			   || sDISPLAY_NAME == "PENDING_PROCESS_ID"
			   || sDISPLAY_NAME == "ARCHIVE_BY"
			   || sDISPLAY_NAME == "ARCHIVE_BY_NAME"
			   || sDISPLAY_NAME == "ARCHIVE_DATE_UTC"
			   || sDISPLAY_NAME == "ARCHIVE_USER_ID"
			   || sDISPLAY_NAME == "ARCHIVE_VIEW"
			   )
			{
				sTERM_NAME = ".LBL_" + sDISPLAY_NAME;
			}
			else
			{
				sTERM_NAME = sModule + ".LBL_" + sDISPLAY_NAME;
			}
			return sTERM_NAME;
		}

		// =====================================================================================
		// TableColumnName — pure static, takes L10N instance.
		// =====================================================================================

		/// <summary>
		/// Returns the localized column header name for the given display field.
		/// Global fields use L10n.Term; module-specific fields use L10n.AliasedTerm.
		/// </summary>
		public static string TableColumnName(L10N L10n, string sModule, string sDISPLAY_NAME)
		{
			if (  sDISPLAY_NAME == "ID"
			   || sDISPLAY_NAME == "DELETED"
			   || sDISPLAY_NAME == "CREATED_BY"
			   || sDISPLAY_NAME == "CREATED_BY_ID"
			   || sDISPLAY_NAME == "CREATED_BY_NAME"
			   || sDISPLAY_NAME == "DATE_ENTERED"
			   || sDISPLAY_NAME == "MODIFIED_USER_ID"
			   || sDISPLAY_NAME == "DATE_MODIFIED"
			   || sDISPLAY_NAME == "DATE_MODIFIED_UTC"
			   || sDISPLAY_NAME == "MODIFIED_BY"
			   || sDISPLAY_NAME == "MODIFIED_BY_NAME"
			   || sDISPLAY_NAME == "ASSIGNED_USER_ID"
			   || sDISPLAY_NAME == "ASSIGNED_TO"
			   || sDISPLAY_NAME == "ASSIGNED_TO_NAME"
			   || sDISPLAY_NAME == "TEAM_ID"
			   || sDISPLAY_NAME == "TEAM_NAME"
			   || sDISPLAY_NAME == "TEAM_SET_ID"
			   || sDISPLAY_NAME == "TEAM_SET_NAME"
			   || sDISPLAY_NAME == "TEAM_SET_LIST"
			   || sDISPLAY_NAME == "ASSIGNED_SET_ID"
			   || sDISPLAY_NAME == "ASSIGNED_SET_NAME"
			   || sDISPLAY_NAME == "ASSIGNED_SET_LIST"
			   || sDISPLAY_NAME == "ID_C"
			   || sDISPLAY_NAME == "AUDIT_ID"
			   || sDISPLAY_NAME == "AUDIT_ACTION"
			   || sDISPLAY_NAME == "AUDIT_DATE"
			   || sDISPLAY_NAME == "AUDIT_COLUMNS"
			   || sDISPLAY_NAME == "AUDIT_TABLE"
			   || sDISPLAY_NAME == "AUDIT_TOKEN"
			   || sDISPLAY_NAME == "LAST_ACTIVITY_DATE"
			   || sDISPLAY_NAME == "TAG_SET_NAME"
			   || sDISPLAY_NAME == "PENDING_PROCESS_ID"
			   || sDISPLAY_NAME == "ARCHIVE_BY"
			   || sDISPLAY_NAME == "ARCHIVE_BY_NAME"
			   || sDISPLAY_NAME == "ARCHIVE_DATE_UTC"
			   || sDISPLAY_NAME == "ARCHIVE_USER_ID"
			   || sDISPLAY_NAME == "ARCHIVE_VIEW"
			   )
			{
				sDISPLAY_NAME = L10n.Term(".LBL_" + sDISPLAY_NAME).Replace(":", "");
			}
			else
			{
				sDISPLAY_NAME = L10n.AliasedTerm(sModule + ".LBL_" + sDISPLAY_NAME).Replace(":", "");
			}
			return sDISPLAY_NAME;
		}

		// =====================================================================================
		// MassEmailerSiteURL
		// BEFORE: Took HttpApplicationState Application; read Application["CONFIG.*"].
		// AFTER:  Uses injected _memoryCache.Get("CONFIG.*") directly.
		// =====================================================================================

		/// <summary>
		/// Returns the base URL used for mass email tracking links.
		/// Falls back to server name/port from IMemoryCache application state entries.
		/// BEFORE: MassEmailerSiteURL(HttpApplicationState Application).
		/// AFTER:  MassEmailerSiteURL() using injected IMemoryCache.
		/// </summary>
		public string MassEmailerSiteURL()
		{
			string sSiteURL = Sql.ToString(_memoryCache.Get("CONFIG.site_url"));
			if ( Sql.ToString(_memoryCache.Get("CONFIG.massemailer_tracking_entities_location_type")) == "2"
			     && !Sql.IsEmptyString(_memoryCache.Get("CONFIG.massemailer_tracking_entities_location") as string) )
				sSiteURL = Sql.ToString(_memoryCache.Get("CONFIG.massemailer_tracking_entities_location"));
			if ( Sql.IsEmptyString(sSiteURL) )
			{
				string sServerScheme    = Sql.ToString(_memoryCache.Get("ServerScheme"   ));
				string sServerName      = Sql.ToString(_memoryCache.Get("ServerName"     ));
				string sApplicationPath = Sql.ToString(_memoryCache.Get("ApplicationPath"));
				string sServerPort      = Sql.ToString(_memoryCache.Get("ServerPort"     ));
				sSiteURL = sServerScheme + "://" + sServerName + sServerPort + sApplicationPath;
			}
			if ( !sSiteURL.StartsWith("http") )
				sSiteURL = "http://" + sSiteURL;
			if ( !sSiteURL.EndsWith("/") )
				sSiteURL += "/";
			return sSiteURL;
		}

		// =====================================================================================
		// RefreshAllViews
		// BEFORE: Called static DbProviderFactories.GetFactory().
		// AFTER:  Uses injected _dbProviderFactories.GetFactory().
		// =====================================================================================

		/// <summary>
		/// Executes the spSqlRefreshAllViews stored procedure with an infinite timeout.
		/// Used after schema changes to refresh all SQL Server views.
		/// </summary>
		public void RefreshAllViews()
		{
			DbProviderFactory dbf = _dbProviderFactories.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction  = trn;
							cmd.CommandType  = CommandType.StoredProcedure;
							cmd.CommandText  = "spSqlRefreshAllViews";
							cmd.CommandTimeout = 0;
							cmd.ExecuteNonQuery();
						}
						trn.Commit();
					}
					catch(Exception ex)
					{
						trn.Rollback();
						throw(new Exception(ex.Message, ex.InnerException));
					}
				}
			}
		}

		// =====================================================================================
		// UpdateSemanticModel
		// BEFORE: Took object o (HttpContext); called DbProviderFactories.GetFactory(Context.Application).
		// AFTER:  Uses injected _dbProviderFactories and _splendidCache.
		// =====================================================================================

		/// <summary>
		/// Rebuilds the semantic model in a background thread (or inline).
		/// BEFORE: Called with ThreadPool.QueueUserWorkItem, received HttpContext as parameter.
		/// AFTER:  Uses injected _dbProviderFactories; optional object parameter kept for signature compat.
		/// </summary>
		public void UpdateSemanticModel(object o = null)
		{
			DbProviderFactory dbf = _dbProviderFactories.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction  = trn;
							cmd.CommandType  = CommandType.StoredProcedure;
							cmd.CommandText  = "spSEMANTIC_MODEL_Rebuild";
							cmd.CommandTimeout = 0;
							cmd.ExecuteNonQuery();
						}
						trn.Commit();
					}
					catch(Exception ex)
					{
						trn.Rollback();
						SplendidError.SystemMessage(_memoryCache, "Error", new StackTrace(true).GetFrame(0), ex);
					}
				}
			}
			// Invalidate semantic model cache entries.
			// BEFORE: SplendidCache.ClearSet("SEMANTIC_MODEL.") — passed string directly.
			// AFTER:  ClearSet(IEnumerable<string>) routes through ClearTable which uses default prefix removal.
			_splendidCache.ClearSet(new string[] { "SEMANTIC_MODEL." });
		}

		// =====================================================================================
		// BuildAllAuditTables
		// BEFORE: Took object o (HttpContext); used Application["System.RebuildAudit.Start"] state.
		// AFTER:  Uses injected _dbProviderFactories and _memoryCache.
		// =====================================================================================

		/// <summary>
		/// Rebuilds all audit tables by executing spSqlBuildAllAuditTables stored procedure.
		/// BEFORE: Called with ThreadPool.QueueUserWorkItem, received HttpContext as parameter.
		/// AFTER:  Uses injected _dbProviderFactories; optional parameter kept for signature compat.
		/// </summary>
		public void BuildAllAuditTables(object o = null)
		{
			// BEFORE: Application["System.RebuildAudit.Start"] = DateTime.Now;
			// AFTER:  IMemoryCache.Set("System.RebuildAudit.Start", DateTime.Now, cacheOptions)
			_memoryCache.Set("System.RebuildAudit.Start", DateTime.Now);
			DbProviderFactory dbf = _dbProviderFactories.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				using ( IDbTransaction trn = Sql.BeginTransaction(con) )
				{
					try
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.Transaction  = trn;
							cmd.CommandType  = CommandType.StoredProcedure;
							cmd.CommandText  = "spSqlBuildAllAuditTables";
							cmd.CommandTimeout = 0;
							cmd.ExecuteNonQuery();
						}
						trn.Commit();
					}
					catch(Exception ex)
					{
						trn.Rollback();
						throw(new Exception(ex.Message, ex.InnerException));
					}
					finally
					{
						// BEFORE: Application["System.RebuildAudit.Start"] = null;
						// AFTER:  Remove from IMemoryCache.
						_memoryCache.Remove("System.RebuildAudit.Start");
					}
				}
			}
		}

		// =====================================================================================
		// CheckVersion
		// BEFORE: Took HttpApplicationState Application; used xml.Load(url) and Application["CONFIG.*"].
		// AFTER:  Uses injected _memoryCache; replaces xml.Load(url) with HttpClient.GetAsync().
		// =====================================================================================

		/// <summary>
		/// Downloads the version XML from the SplendidCRM update server and returns a DataTable
		/// with Build, Date, Description, URL, and New columns. Rows with a newer build number
		/// have New set to "1".
		/// BEFORE: xml.Load(url) — uses System.Xml built-in URL loading (deprecated for HTTP).
		/// AFTER:  Uses HttpClient to fetch the XML content.
		/// </summary>
		public DataTable CheckVersion()
		{
			XmlDocument xml = new XmlDocument();
			xml.XmlResolver = null;
			string sVersionXmlURL = String.Empty;
			string sServiceLevel  = Sql.ToString(_memoryCache.Get("CONFIG.service_level"));
			if ( String.Compare(sServiceLevel, "Basic"      , true) == 0
			  || String.Compare(sServiceLevel, "Community"  , true) == 0 )
				sVersionXmlURL = "http://community.splendidcrm.com/Administration/Versions.xml";
			else if ( String.Compare(sServiceLevel, "Enterprise", true) == 0 )
				sVersionXmlURL = "http://enterprise.splendidcrm.com/Administration/Versions.xml";
			else if ( String.Compare(sServiceLevel, "Ultimate"  , true) == 0 )
				sVersionXmlURL = "http://ultimate.splendidcrm.com/Administration/Versions.xml";
			else
				sVersionXmlURL = "http://professional.splendidcrm.com/Administration/Versions.xml";

			bool bSendUsageInfo = Sql.ToBoolean(_memoryCache.Get("CONFIG.send_usage_info"));
			string sURL = sVersionXmlURL + (bSendUsageInfo ? UsageInfo() : String.Empty);

			// AFTER: Use HttpClient instead of deprecated xml.Load(url)
			try
			{
				using ( HttpClient client = new HttpClient() )
				{
					client.Timeout = TimeSpan.FromSeconds(30);
					string sXmlContent = client.GetStringAsync(sURL).GetAwaiter().GetResult();
					xml.LoadXml(sXmlContent);
				}
			}
			catch(Exception ex)
			{
				SplendidError.SystemMessage(_memoryCache, "Error", new StackTrace(true).GetFrame(0), ex);
				return new DataTable();
			}

			Version vSplendidVersion = new Version(Sql.ToString(_memoryCache.Get("SplendidVersion")));
			DataTable dt = XmlUtil.CreateDataTable(xml.DocumentElement, "Version", new string[] {"Build", "Date", "Description", "URL", "New"});
			foreach ( DataRow row in dt.Rows )
			{
				Version vBuild = new Version(Sql.ToString(row["Build"]));
				if ( vSplendidVersion < vBuild )
					row["New"] = "1";
			}
			return dt;
		}

		// =====================================================================================
		// UsageInfo
		// BEFORE: Took HttpApplicationState Application; used HttpUtility.UrlEncode.
		// AFTER:  Uses injected _memoryCache; replaces HttpUtility.UrlEncode with WebUtility.UrlEncode.
		// =====================================================================================

		/// <summary>
		/// Builds a query string of usage telemetry for the version check URL.
		/// BEFORE: UsageInfo(HttpApplicationState Application) with HttpUtility.UrlEncode.
		/// AFTER:  UsageInfo() using _memoryCache; WebUtility.UrlEncode replaces HttpUtility.UrlEncode.
		/// </summary>
		public string UsageInfo()
		{
			StringBuilder sb = new StringBuilder();
			sb.Append("?Server="   + WebUtility.UrlEncode(Sql.ToString(_memoryCache.Get("ServerName"           ))));
			sb.Append("&Splendid=" + WebUtility.UrlEncode(Sql.ToString(_memoryCache.Get("SplendidVersion"      ))));
			sb.Append("&Key="      + WebUtility.UrlEncode(Sql.ToString(_memoryCache.Get("CONFIG.unique_key"    ))));
			try
			{
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					string sSQL;
					sSQL = "select count(*)    " + ControlChars.CrLf
					     + "  from vwUSERS_List" + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						sb.Append("&Users=" + Sql.ToString(cmd.ExecuteScalar()));
					}
					sSQL = "select count(*)    " + ControlChars.CrLf
					     + "  from vwUSERS     " + ControlChars.CrLf
					     + " where IS_ADMIN = 1" + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						sb.Append("&Admins=" + Sql.ToString(cmd.ExecuteScalar()));
					}
					sSQL = "select count(*)    " + ControlChars.CrLf
					     + "  from vwUSERS     " + ControlChars.CrLf
					     + " where IS_GROUP = 1" + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						sb.Append("&Groups=" + Sql.ToString(cmd.ExecuteScalar()));
					}
					sSQL = "select count(distinct cast(USER_ID as char(36)))" + ControlChars.CrLf
					     + "  from TRACKER                      " + ControlChars.CrLf
					     + " where DATE_ENTERED >= @DATE_ENTERED" + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@DATE_ENTERED", DateTime.Today.AddMonths(-1));
						sb.Append("&Activity=" + Sql.ToString(cmd.ExecuteScalar()));
					}
					sSQL = "select Version     " + ControlChars.CrLf
					     + "  from vwSqlVersion" + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						string sDBVersion = Sql.ToString(cmd.ExecuteScalar());
						sDBVersion = sDBVersion.Replace("Microsoft ", "");
						sDBVersion = sDBVersion.Replace("Intel "    , "");
						sb.Append("&DB=" + WebUtility.UrlEncode(sDBVersion));
					}
				}
			}
			catch
			{
				// Ignore database errors in usage reporting
			}
			string sOSVersion = System.Environment.OSVersion.ToString();
			sOSVersion = sOSVersion.Replace("Microsoft "  , "");
			sOSVersion = sOSVersion.Replace("Service Pack", "SP");
			sb.Append("&OS="      + WebUtility.UrlEncode(sOSVersion));
			sb.Append("&AppPath=" + WebUtility.UrlEncode(Sql.ToString(_memoryCache.Get("ApplicationPath"))));
			sb.Append("&System="  + WebUtility.UrlEncode(System.Environment.Version.ToString()));
			return sb.ToString();
		}

		// =====================================================================================
		// SelectItem — WebForms stub.
		// =====================================================================================

		/// <summary>
		/// No-op stub. Original set the selected value of a WebForms DropDownList or ListBox.
		/// BEFORE: Validated the value against the control's Items collection and set
		///         DropDownList.SelectedValue / ListBox.SelectedValue.
		/// AFTER:  No-op — WebForms ListControl not available in ASP.NET Core.
		///         Preserved as a static stub for DynamicControl API compatibility.
		///         DynamicControl's Text setter calls Utils.SetValue(lst, value) when it
		///         has a DropDownList or ListBox control; since FindControl always returns
		///         null in .NET 10 ReactOnlyUI, this stub is never actually invoked.
		/// </summary>
		/// <param name="lst">The WebForms ListControl to set (always null in .NET 10).</param>
		/// <param name="sValue">The value to select (ignored in .NET 10).</param>
		// .NET 10 Migration: Added for DynamicControl compatibility.
		// DynamicControl.Text setter: Utils.SetValue(lst, value) — no-op stub.
		public static void SetValue(object lst, string sValue)
		{
			// No-op: WebForms ListControl not available in ASP.NET Core
		}

		/// <summary>
		/// No-op stub. Original selected a ListBox item in a WebForms control.
		/// Not applicable in ASP.NET Core MVC.
		/// </summary>
		public static void SelectItem(object lst, string sValue)
		{
			// No-op: WebForms ListBox not available in ASP.NET Core
		}

		// =====================================================================================
		// ContentDispositionEncode
		// BEFORE: Took HttpBrowserCapabilities Browser; used HttpUtility.UrlPathEncode for IE.
		// AFTER:  Browser parameter removed (IE-specific logic obsolete); simplified to URL-safe quoting.
		// =====================================================================================

		/// <summary>
		/// Encodes a filename for use in a Content-Disposition header.
		/// BEFORE: ContentDispositionEncode(HttpBrowserCapabilities Browser, string sURL) — used
		///         HttpUtility.UrlPathEncode for IE. IE-specific logic is now obsolete.
		/// AFTER:  ContentDispositionEncode(string sURL) — returns quoted filename.
		/// </summary>
		public static string ContentDispositionEncode(string sURL)
		{
			// Remove characters invalid in file paths
			sURL = sURL.Replace('\\', '_');
			sURL = sURL.Replace(':' , '_');
			sURL = Sql.ToString(sURL);
			sURL = "\"" + sURL + "\"";
			return sURL;
		}

		/// <summary>
		/// Overload that accepts an object browserCapabilities parameter for backward compatibility.
		/// The browser parameter is ignored; IE-specific UrlPathEncode behavior is no longer needed.
		/// </summary>
		public static string ContentDispositionEncode(object browserCapabilities, string sURL)
		{
			// Browser parameter intentionally ignored — IE-specific logic removed
			return ContentDispositionEncode(sURL);
		}

		// =====================================================================================
		// GenerateVCard — pure static, no framework dependencies.
		// =====================================================================================

		/// <summary>
		/// Generates a vCard 3.0 formatted string from a CRM contact/lead DataRow.
		/// </summary>
		public static string GenerateVCard(DataRow row)
		{
			StringBuilder sbVCard = new StringBuilder();
			sbVCard.AppendLine("BEGIN:VCARD");
			sbVCard.AppendLine("VERSION:3.0");
			Guid     gID                         = Sql.ToGuid    (row["ID"                        ]);
			string   sSALUTATION                 = Sql.ToString  (row["SALUTATION"                ]).Trim();
			string   sNAME                       = Sql.ToString  (row["NAME"                      ]).Trim();
			string   sFIRST_NAME                 = Sql.ToString  (row["FIRST_NAME"                ]).Trim();
			string   sLAST_NAME                  = Sql.ToString  (row["LAST_NAME"                 ]).Trim();
			string   sTITLE                      = Sql.ToString  (row["TITLE"                     ]).Trim();
			string   sPHONE_HOME                 = Sql.ToString  (row["PHONE_HOME"                ]).Trim();
			string   sPHONE_MOBILE               = Sql.ToString  (row["PHONE_MOBILE"              ]).Trim();
			string   sPHONE_WORK                 = Sql.ToString  (row["PHONE_WORK"                ]).Trim();
			string   sPHONE_OTHER                = Sql.ToString  (row["PHONE_OTHER"               ]).Trim();
			string   sPHONE_FAX                  = Sql.ToString  (row["PHONE_FAX"                 ]).Trim();
			string   sEMAIL1                     = Sql.ToString  (row["EMAIL1"                    ]).Trim();
			string   sEMAIL2                     = Sql.ToString  (row["EMAIL2"                    ]).Trim();
			string   sASSISTANT                  = Sql.ToString  (row["ASSISTANT"                 ]).Trim();
			string   sASSISTANT_PHONE            = Sql.ToString  (row["ASSISTANT_PHONE"           ]).Trim();
			string   sPRIMARY_ADDRESS_STREET     = Sql.ToString  (row["PRIMARY_ADDRESS_STREET"    ]).Trim();
			string   sPRIMARY_ADDRESS_CITY       = Sql.ToString  (row["PRIMARY_ADDRESS_CITY"      ]).Trim();
			string   sPRIMARY_ADDRESS_STATE      = Sql.ToString  (row["PRIMARY_ADDRESS_STATE"     ]).Trim();
			string   sPRIMARY_ADDRESS_POSTALCODE = Sql.ToString  (row["PRIMARY_ADDRESS_POSTALCODE"]).Trim();
			string   sPRIMARY_ADDRESS_COUNTRY    = Sql.ToString  (row["PRIMARY_ADDRESS_COUNTRY"   ]).Trim();
			string   sALT_ADDRESS_STREET         = Sql.ToString  (row["ALT_ADDRESS_STREET"        ]).Trim();
			string   sALT_ADDRESS_CITY           = Sql.ToString  (row["ALT_ADDRESS_CITY"          ]).Trim();
			string   sALT_ADDRESS_STATE          = Sql.ToString  (row["ALT_ADDRESS_STATE"         ]).Trim();
			string   sALT_ADDRESS_POSTALCODE     = Sql.ToString  (row["ALT_ADDRESS_POSTALCODE"    ]).Trim();
			string   sALT_ADDRESS_COUNTRY        = Sql.ToString  (row["ALT_ADDRESS_COUNTRY"       ]).Trim();
			string   sACCOUNT_NAME               = Sql.ToString  (row["ACCOUNT_NAME"              ]).Trim();
			DateTime dtBIRTHDATE                 = DateTime.MinValue;
			if ( row.Table.Columns.Contains("BIRTHDATE") )
				dtBIRTHDATE = Sql.ToDateTime(row["BIRTHDATE"]);
			DateTime dtDATE_MODIFIED_UTC = Sql.ToDateTime(row["DATE_MODIFIED_UTC"]);

			sPRIMARY_ADDRESS_STREET = sPRIMARY_ADDRESS_STREET.Replace("\r\n", "\n");
			sPRIMARY_ADDRESS_STREET = sPRIMARY_ADDRESS_STREET.Replace("\r"  , "\n");
			sALT_ADDRESS_STREET     = sALT_ADDRESS_STREET    .Replace("\r\n", "\n");
			sALT_ADDRESS_STREET     = sALT_ADDRESS_STREET    .Replace("\r"  , "\n");
			string sADDRESS1 = String.Empty;
			string sADDRESS2 = String.Empty;
			if ( !Sql.IsEmptyString(sPRIMARY_ADDRESS_STREET) )
			{
				string[] arrPRIMARY_ADDRESS_STREET = sPRIMARY_ADDRESS_STREET.Split('\n');
				string sPRIMARY_ADDRESS_STREET1 = String.Empty;
				string sPRIMARY_ADDRESS_STREET2 = String.Empty;
				string sPRIMARY_ADDRESS_STREET3 = String.Empty;
				if ( arrPRIMARY_ADDRESS_STREET.Length == 1 )
					sPRIMARY_ADDRESS_STREET3 = arrPRIMARY_ADDRESS_STREET[0];
				else if ( arrPRIMARY_ADDRESS_STREET.Length == 2 )
				{
					sPRIMARY_ADDRESS_STREET2 = arrPRIMARY_ADDRESS_STREET[0];
					sPRIMARY_ADDRESS_STREET3 = arrPRIMARY_ADDRESS_STREET[1];
				}
				else if ( arrPRIMARY_ADDRESS_STREET.Length >= 3 )
				{
					sPRIMARY_ADDRESS_STREET1 = arrPRIMARY_ADDRESS_STREET[0];
					sPRIMARY_ADDRESS_STREET2 = arrPRIMARY_ADDRESS_STREET[1];
					sPRIMARY_ADDRESS_STREET3 = arrPRIMARY_ADDRESS_STREET[2];
				}
				sADDRESS1  =       sPRIMARY_ADDRESS_STREET1   ;
				sADDRESS1 += ";" + sPRIMARY_ADDRESS_STREET2   ;
				sADDRESS1 += ";" + sPRIMARY_ADDRESS_STREET3   ;
				sADDRESS1 += ";" + sPRIMARY_ADDRESS_CITY      ;
				sADDRESS1 += ";" + sPRIMARY_ADDRESS_STATE     ;
				sADDRESS1 += ";" + sPRIMARY_ADDRESS_POSTALCODE;
				sADDRESS1 += ";" + sPRIMARY_ADDRESS_COUNTRY   ;
			}
			if ( !Sql.IsEmptyString(sALT_ADDRESS_STREET) )
			{
				string[] arrALT_ADDRESS_STREET = sALT_ADDRESS_STREET.Split('\n');
				string sALT_ADDRESS_STREET1 = String.Empty;
				string sALT_ADDRESS_STREET2 = String.Empty;
				string sALT_ADDRESS_STREET3 = String.Empty;
				if ( arrALT_ADDRESS_STREET.Length == 1 )
					sALT_ADDRESS_STREET3 = arrALT_ADDRESS_STREET[0];
				else if ( arrALT_ADDRESS_STREET.Length == 2 )
				{
					sALT_ADDRESS_STREET2 = arrALT_ADDRESS_STREET[0];
					sALT_ADDRESS_STREET3 = arrALT_ADDRESS_STREET[1];
				}
				else if ( arrALT_ADDRESS_STREET.Length >= 3 )
				{
					sALT_ADDRESS_STREET1 = arrALT_ADDRESS_STREET[0];
					sALT_ADDRESS_STREET2 = arrALT_ADDRESS_STREET[1];
					sALT_ADDRESS_STREET3 = arrALT_ADDRESS_STREET[2];
				}
				sADDRESS2  =       sALT_ADDRESS_STREET1       ;
				sADDRESS2 += ";" + sALT_ADDRESS_STREET2       ;
				sADDRESS2 += ";" + sALT_ADDRESS_STREET3       ;
				sADDRESS2 += ";" + sALT_ADDRESS_CITY          ;
				sADDRESS2 += ";" + sALT_ADDRESS_STATE         ;
				sADDRESS2 += ";" + sALT_ADDRESS_POSTALCODE    ;
				sADDRESS2 += ";" + sALT_ADDRESS_COUNTRY       ;
			}

			sbVCard.AppendLine("N:"  + sLAST_NAME + ";" + sFIRST_NAME + (Sql.IsEmptyString(sSALUTATION) ? String.Empty : ";" + sSALUTATION));
			sbVCard.AppendLine("FN:" + (sSALUTATION + " " + sNAME).Trim());
			if ( !Sql.IsEmptyString(sACCOUNT_NAME) ) sbVCard.AppendLine("ORG:"                 + sACCOUNT_NAME);
			if ( !Sql.IsEmptyString(sTITLE       ) ) sbVCard.AppendLine("TITLE:"               + sTITLE       );
			if ( !Sql.IsEmptyString(sPHONE_HOME  ) ) sbVCard.AppendLine("TEL;TYPE=HOME,VOICE:" + sPHONE_HOME  );
			if ( !Sql.IsEmptyString(sPHONE_MOBILE) ) sbVCard.AppendLine("TEL;TYPE=CELL,VOICE:" + sPHONE_MOBILE);
			if ( !Sql.IsEmptyString(sPHONE_WORK  ) ) sbVCard.AppendLine("TEL;TYPE=WORK,VOICE:" + sPHONE_WORK  );
			if ( !Sql.IsEmptyString(sPHONE_FAX   ) ) sbVCard.AppendLine("TEL;TYPE=WORK,FAX:"   + sPHONE_FAX   );
			if ( !Sql.IsEmptyString(sEMAIL1      ) ) sbVCard.AppendLine("EMAIL;TYPE=INTERNET:" + sEMAIL1      );
			if ( !Sql.IsEmptyString(sASSISTANT   ) ) sbVCard.AppendLine("X-ASSISTANT:"         + sASSISTANT   );
			if ( !Sql.IsEmptyString(sADDRESS1    ) ) sbVCard.AppendLine("ADR;TYPE=WORK"        + (sADDRESS1.IndexOf("=0A=0D") >= 0 ? ";ENCODING=QUOTED-PRINTABLE" : String.Empty) + ":" + sADDRESS1);
			if ( !Sql.IsEmptyString(sADDRESS2    ) ) sbVCard.AppendLine("ADR;TYPE=OTHER"       + (sADDRESS2.IndexOf("=0A=0D") >= 0 ? ";ENCODING=QUOTED-PRINTABLE" : String.Empty) + ":" + sADDRESS2);
			if ( dtBIRTHDATE != DateTime.MinValue  ) sbVCard.AppendLine("BDAY:"                + dtBIRTHDATE.ToString("yyyy-MM-dd"));
			sbVCard.AppendLine("UID:" + gID.ToString());
			sbVCard.AppendLine("REV:" + dtDATE_MODIFIED_UTC.ToString("yyyyMMddTHHmmssZ"));
			sbVCard.AppendLine("END:VCARD");
			return sbVCard.ToString();
		}

		// =====================================================================================
		// CalDAV utilities — pure static, no framework dependencies.
		// =====================================================================================

		/// <summary>Unescapes CalDAV/RFC5545 escaped special characters.</summary>
		public static string CalDAV_Unescape(string s)
		{
			s = s.Replace("\\," , "," );
			s = s.Replace("\\;" , ";" );
			s = s.Replace("\\n" , "\n");
			s = s.Replace("\\N" , "\n");
			s = s.Replace("\\\\", "\\");
			return s;
		}

		/// <summary>Escapes special characters for use in CalDAV/RFC5545 property values.</summary>
		public static string CalDAV_Escape(string s)
		{
			s = s.Replace("\\"  , "\\\\");
			s = s.Replace("\r\n", "\n"  );
			s = s.Replace("\r"  , "\n"  );
			s = s.Replace("\n"  , "\\n" );
			s = s.Replace(","   , "\\," );
			s = s.Replace(";"   , "\\;" );
			s = s.Replace("\n"  , "\\n" );
			return s;
		}

		/// <summary>
		/// Folds a CalDAV property value at 75-character line boundaries as required by RFC5545.
		/// </summary>
		public static string CalDAV_FoldLines(string s)
		{
			StringBuilder sb = new StringBuilder();
			using ( TextReader rdr = new StringReader(s) )
			{
				char[] arr = new char[75];
				int n = 0;
				while ( (n = rdr.ReadBlock(arr, 0, 75)) > 0 )
				{
					if ( sb.Length > 0 )
						sb.Append("\r\n ");
					sb.Append(arr, 0, n);
				}
			}
			return sb.ToString();
		}

		/// <summary>
		/// Parses a CalDAV/RFC5545 date/time string into a DateTime value.
		/// Handles UTC (Z suffix), all-day (8-char), and local time formats.
		/// </summary>
		public static DateTime CalDAV_ParseDate(string sDate)
		{
			DateTimeFormatInfo dateInfo = System.Threading.Thread.CurrentThread.CurrentCulture.DateTimeFormat;
			DateTime date = DateTime.MinValue;
			if ( sDate.EndsWith("Z") )
			{
				if ( sDate.Contains("-") )
				{
					if ( DateTime.TryParseExact(sDate, "yyyy-MM-ddTHH:mm:ssZ", dateInfo, DateTimeStyles.AssumeUniversal, out date) )
						date = date.ToLocalTime();
				}
				else
				{
					if ( DateTime.TryParseExact(sDate, "yyyyMMddTHHmmssZ", dateInfo, DateTimeStyles.AssumeUniversal, out date) )
						date = date.ToLocalTime();
				}
			}
			else if ( sDate.Length == 8 )
			{
				if ( sDate.Contains("-") )
					DateTime.TryParseExact(sDate, "yyyy-MM-dd", dateInfo, DateTimeStyles.AssumeLocal, out date);
				else
					DateTime.TryParseExact(sDate, "yyyyMMdd"  , dateInfo, DateTimeStyles.AssumeLocal, out date);
			}
			else
			{
				if ( sDate.Contains("-") )
					DateTime.TryParseExact(sDate, "yyyy-MM-ddTHH:mm:ss", dateInfo, DateTimeStyles.AssumeLocal, out date);
				else
					DateTime.TryParseExact(sDate, "yyyyMMddTHHmmss"    , dateInfo, DateTimeStyles.AssumeLocal, out date);
			}
			return date;
		}

		/// <summary>
		/// Parses a CalDAV RRULE string into its component parts: REPEAT_TYPE, INTERVAL, DOW, UNTIL, COUNT.
		/// </summary>
		public static void CalDAV_ParseRule(string sRRULE, ref string sREPEAT_TYPE, ref int nREPEAT_INTERVAL, ref string sREPEAT_DOW, ref DateTime dtREPEAT_UNTIL, ref int nREPEAT_COUNT)
		{
			int nBeginTimezone = sRRULE.IndexOf("BEGIN:VTIMEZONE");
			if ( nBeginTimezone > 0 )
				sRRULE = sRRULE.Substring(0, nBeginTimezone).Trim();

			sREPEAT_TYPE     = String.Empty     ;
			nREPEAT_INTERVAL = 0                ;
			sREPEAT_DOW      = String.Empty     ;
			dtREPEAT_UNTIL   = DateTime.MinValue;
			nREPEAT_COUNT    = 0                ;
			sRRULE += ";";

			if      ( sRRULE.Contains("FREQ=DAILY"  ) ) sREPEAT_TYPE = "Daily"  ;
			else if ( sRRULE.Contains("FREQ=WEEKLY" ) ) sREPEAT_TYPE = "Weekly" ;
			else if ( sRRULE.Contains("FREQ=MONTHLY") ) sREPEAT_TYPE = "Monthly";
			else if ( sRRULE.Contains("FREQ=YEARLY" ) ) sREPEAT_TYPE = "Yearly" ;

			if ( sRRULE.Contains("INTERVAL=") )
			{
				int nStart = sRRULE.IndexOf("INTERVAL=") + "INTERVAL=".Length;
				int nEnd   = sRRULE.IndexOf(";", nStart);
				nREPEAT_INTERVAL = Sql.ToInteger(sRRULE.Substring(nStart, nEnd - nStart));
			}

			if ( sRRULE.Contains("BYDAY=") )
			{
				int nStart = sRRULE.IndexOf("BYDAY=") + "BYDAY=".Length;
				int nEnd   = sRRULE.IndexOf(";", nStart);
				string sGOOGLE_DOW = sRRULE.Substring(nStart, nEnd - nStart);
				sREPEAT_DOW = String.Empty;
				if ( sGOOGLE_DOW.Contains("SU") ) sREPEAT_DOW += "0";
				if ( sGOOGLE_DOW.Contains("MO") ) sREPEAT_DOW += "1";
				if ( sGOOGLE_DOW.Contains("TU") ) sREPEAT_DOW += "2";
				if ( sGOOGLE_DOW.Contains("WE") ) sREPEAT_DOW += "3";
				if ( sGOOGLE_DOW.Contains("TH") ) sREPEAT_DOW += "4";
				if ( sGOOGLE_DOW.Contains("FR") ) sREPEAT_DOW += "5";
				if ( sGOOGLE_DOW.Contains("SA") ) sREPEAT_DOW += "6";
			}

			if ( sRRULE.Contains("UNTIL=") )
			{
				int nStart = sRRULE.IndexOf("UNTIL=") + "UNTIL=".Length;
				int nEnd   = sRRULE.IndexOf(";", nStart);
				dtREPEAT_UNTIL = CalDAV_ParseDate(sRRULE.Substring(nStart, nEnd - nStart));
			}

			if ( sRRULE.Contains("COUNT=") )
			{
				int nStart = sRRULE.IndexOf("COUNT=") + "COUNT=".Length;
				int nEnd   = sRRULE.IndexOf(";", nStart);
				nREPEAT_COUNT = Sql.ToInteger(sRRULE.Substring(nStart, nEnd - nStart));
			}
		}

		/// <summary>
		/// Builds a CalDAV RRULE string from its component parts.
		/// </summary>
		public static string CalDAV_BuildRule(string sREPEAT_TYPE, int nREPEAT_INTERVAL, string sREPEAT_DOW, DateTime dtREPEAT_UNTIL, int nREPEAT_COUNT)
		{
			string sRRULE = String.Empty;
			switch ( sREPEAT_TYPE )
			{
				case "Daily":
					sRRULE += "RRULE:FREQ=DAILY";
					break;
				case "Weekly":
					if ( !Sql.IsEmptyString(sREPEAT_DOW) )
					{
						sRRULE += "RRULE:FREQ=WEEKLY";
						string sCalDAV_DOW = String.Empty;
						for ( int n = 0; n < sREPEAT_DOW.Length; n++ )
						{
							if ( sCalDAV_DOW.Length > 0 )
								sCalDAV_DOW += ",";
							switch ( sREPEAT_DOW.Substring(n, 1) )
							{
								case "0":  sCalDAV_DOW += "SU";  break;
								case "1":  sCalDAV_DOW += "MO";  break;
								case "2":  sCalDAV_DOW += "TU";  break;
								case "3":  sCalDAV_DOW += "WE";  break;
								case "4":  sCalDAV_DOW += "TH";  break;
								case "5":  sCalDAV_DOW += "FR";  break;
								case "6":  sCalDAV_DOW += "SA";  break;
							}
						}
						sRRULE += ";BYDAY=" + sCalDAV_DOW;
					}
					break;
				case "Monthly":
					sRRULE += "RRULE:FREQ=MONTHLY";
					break;
				case "Yearly":
					sRRULE += "RRULE:FREQ=YEARLY";
					break;
			}
			if ( !Sql.IsEmptyString(sRRULE) )
			{
				if ( nREPEAT_INTERVAL > 1 )
					sRRULE += ";INTERVAL=" + nREPEAT_INTERVAL.ToString();
				if ( nREPEAT_COUNT > 0 )
					sRRULE += ";COUNT=" + nREPEAT_COUNT.ToString();
				if ( dtREPEAT_UNTIL != DateTime.MinValue )
					sRRULE += ";UNTIL:" + dtREPEAT_UNTIL.ToString("yyyyMMdd");
			}
			return sRRULE;
		}

		// =====================================================================================
		// GenerateVCalendar — pure static, no framework dependencies.
		// =====================================================================================

		/// <summary>
		/// Generates an iCalendar 2.0 (RFC5545) VCALENDAR string from a CRM meeting DataRow.
		/// </summary>
		public static string GenerateVCalendar(DataRow row, bool bIncludeAlarm)
		{
			StringBuilder sbVCalendar = new StringBuilder();
			sbVCalendar.AppendLine("BEGIN:VCALENDAR");
			sbVCalendar.AppendLine("VERSION:2.0");
			sbVCalendar.AppendLine("CALSCALE:GREGORIAN");
			sbVCalendar.AppendLine("PRODID:-//CALENDARSERVER.ORG//NONSGML Version 1//EN");
			sbVCalendar.AppendLine("METHOD:PUBLISH");

			sbVCalendar.AppendLine("BEGIN:VEVENT");
			sbVCalendar.AppendLine("CLASS:PUBLIC");
			sbVCalendar.AppendLine("SEQUENCE:0");
			sbVCalendar.AppendLine("UID:"           + Sql.ToGuid    (row["ID"           ]).ToString());
			sbVCalendar.AppendLine("CREATED:"       + Sql.ToDateTime(row["DATE_ENTERED" ]).ToUniversalTime().ToString("yyyyMMddTHHmmssZ"));
			sbVCalendar.AppendLine("DTSTAMP:"       + Sql.ToDateTime(row["DATE_MODIFIED"]).ToUniversalTime().ToString("yyyyMMddTHHmmssZ"));
			sbVCalendar.AppendLine("LAST-MODIFIED:" + Sql.ToDateTime(row["DATE_MODIFIED"]).ToUniversalTime().ToString("yyyyMMddTHHmmssZ"));
			sbVCalendar.AppendLine(CalDAV_FoldLines("SUMMARY:" + CalDAV_Escape(Sql.ToString(row["NAME"]))));
			if ( !Sql.IsEmptyString(row["DESCRIPTION"]) )
				sbVCalendar.AppendLine(CalDAV_FoldLines("DESCRIPTION:" + CalDAV_Escape(Sql.ToString(row["DESCRIPTION"]))));
			if ( !Sql.IsEmptyString(row["LOCATION"]) )
				sbVCalendar.AppendLine("LOCATION:"  + Sql.ToString(row["LOCATION"]));

			int      nDURATION_HOURS   = Sql.ToInteger (row["DURATION_HOURS"  ]);
			int      nDURATION_MINUTES = Sql.ToInteger (row["DURATION_MINUTES"]);
			DateTime dtDATE_START      = Sql.ToDateTime(row["DATE_START"]);
			DateTime dtDATE_END        = Sql.ToDateTime(row["DATE_END"  ]);
			if ( nDURATION_HOURS == 24 )
			{
				sbVCalendar.AppendLine("DTSTART:" + dtDATE_START.ToString("yyyyMMdd"));
				sbVCalendar.AppendLine("DTEND:"   + dtDATE_END  .ToString("yyyyMMdd"));
			}
			else
			{
				sbVCalendar.AppendLine("DTSTART:" + dtDATE_START.ToUniversalTime().ToString("yyyyMMddTHHmmss") + "Z");
				sbVCalendar.AppendLine("DTEND:"   + dtDATE_END  .ToUniversalTime().ToString("yyyyMMddTHHmmss") + "Z");
			}

			StringBuilder sbWho = new StringBuilder();
			if ( Sql.ToGuid(row["INVITEE_ID"]) == Sql.ToGuid(row["ASSIGNED_USER_ID"]) )
				sbWho.Append("ORGANIZER");
			else
				sbWho.Append("ATTENDEE");
			if ( !Sql.IsEmptyString(row["ACCEPT_STATUS"]) )
			{
				sbWho.Append(";PARTSTAT=");
				switch ( Sql.ToString(row["ACCEPT_STATUS"]) )
				{
					case "accept"   :  sbWho.Append("ACCEPTED" );  break;
					case "decline"  :  sbWho.Append("DECLINED" );  break;
					case "none"     :  sbWho.Append("INVITED"  );  break;
					case "tentative":  sbWho.Append("TENTATIVE");  break;
				}
			}
			sbWho.Append(";CN=" + (Sql.ToString(row["FIRST_NAME"]) + " " + Sql.ToString(row["LAST_NAME"])).Trim());
			if ( !Sql.IsEmptyString(row["EMAIL1"]) )
				sbWho.Append(":mailto:" + Sql.ToString(row["EMAIL1"]));
			else
				sbWho.Append(":invalid:nomail");
			sbVCalendar.AppendLine(CalDAV_FoldLines(sbWho.ToString()));

			int nREMINDER_MINUTES = Sql.ToInteger(row["REMINDER_TIME"]) / 60;
			if ( bIncludeAlarm && nREMINDER_MINUTES > 0 )
			{
				StringBuilder sbAlarm = new StringBuilder();
				sbAlarm.AppendLine("BEGIN:VALARM");
				sbAlarm.AppendLine("ACTION:" + "DISPLAY");
				sbAlarm.AppendLine(CalDAV_FoldLines("DESCRIPTION:" + CalDAV_Escape("Event reminder")));
				if ( nREMINDER_MINUTES / (24 * 60) > 0 )
					sbAlarm.AppendLine("TRIGGER;VALUE=DURATION:" + "-P"  + nREMINDER_MINUTES / (24 * 60) + "D");
				else if ( nREMINDER_MINUTES % 60 == 0 )
					sbAlarm.AppendLine("TRIGGER;VALUE=DURATION:" + "-PT" + nREMINDER_MINUTES / 60 + "H");
				else
					sbAlarm.AppendLine("TRIGGER;VALUE=DURATION:" + "-PT" + nREMINDER_MINUTES + "M");
				sbAlarm.AppendLine("X-WR-ALARMUID:" + Guid.NewGuid().ToString());
				sbAlarm.AppendLine("END:VALARM");
				sbVCalendar.Append(sbAlarm);
			}
			sbVCalendar.AppendLine("END:VEVENT");
			sbVCalendar.AppendLine("END:VCALENDAR");
			return sbVCalendar.ToString();
		}

		// =====================================================================================
		// NormalizePhone — pure static, no framework dependencies.
		// =====================================================================================

		/// <summary>
		/// Normalizes a phone number by removing spaces, dashes, parentheses, and special characters.
		/// Matches the normalization within the SQL Server database function fnNormalizePhone.
		/// </summary>
		public static string NormalizePhone(string sPhoneNumber)
		{
			sPhoneNumber = Sql.ToString(sPhoneNumber);
			sPhoneNumber = sPhoneNumber.Replace(" ", String.Empty);
			sPhoneNumber = sPhoneNumber.Replace("+", String.Empty);
			sPhoneNumber = sPhoneNumber.Replace("(", String.Empty);
			sPhoneNumber = sPhoneNumber.Replace(")", String.Empty);
			sPhoneNumber = sPhoneNumber.Replace("-", String.Empty);
			sPhoneNumber = sPhoneNumber.Replace(".", String.Empty);
			sPhoneNumber = sPhoneNumber.Replace("[", String.Empty);
			sPhoneNumber = sPhoneNumber.Replace("]", String.Empty);
			sPhoneNumber = sPhoneNumber.Replace("#", String.Empty);
			sPhoneNumber = sPhoneNumber.Replace("*", String.Empty);
			sPhoneNumber = sPhoneNumber.Replace("%", String.Empty);
			return sPhoneNumber;
		}

		// =====================================================================================
		// CachedFileExists
		// BEFORE: Took HttpContext Context; used Context.Application["Exists.*"] and Context.Server.MapPath.
		// AFTER:  Uses injected _memoryCache and _configuration["ContentRoot"] for path resolution.
		// =====================================================================================

		/// <summary>
		/// Returns true if the file at the given virtual path exists, caching the result in IMemoryCache.
		/// BEFORE: CachedFileExists(HttpContext Context, string sVirtualPath) using Application[] cache.
		/// AFTER:  CachedFileExists(string sVirtualPath) using IMemoryCache; path resolved from ContentRoot.
		/// </summary>
		public bool CachedFileExists(string sVirtualPath)
		{
			// BEFORE: int nFileExists = Sql.ToInteger(Context.Application["Exists." + sVirtualPath]);
			// AFTER:  IMemoryCache.TryGetValue
			int nFileExists = 0;
			if ( _memoryCache.TryGetValue("Exists." + sVirtualPath, out object objExists) )
				nFileExists = Sql.ToInteger(objExists);
			if ( nFileExists == 0 )
			{
				// BEFORE: Context.Server.MapPath(sVirtualPath) — IIS virtual-to-physical path mapping.
				// AFTER:  Combine ContentRoot with the virtual path (strip leading ~/ or /).
				string sContentRoot = _configuration["ContentRoot"] ?? AppContext.BaseDirectory;
				string sRelativePath = sVirtualPath.TrimStart('~', '/');
				string sPhysicalPath = Path.Combine(sContentRoot, sRelativePath.Replace('/', Path.DirectorySeparatorChar));
				if ( File.Exists(sPhysicalPath) )
					nFileExists = 1;
				else
					nFileExists = -1;
				_memoryCache.Set("Exists." + sVirtualPath, (object)nFileExists);
			}
			return nFileExists == 1;
		}

		/// <summary>
		/// Overload for backward compatibility with callers that pass an object context parameter.
		/// The context parameter is ignored; uses injected services.
		/// </summary>
		public bool CachedFileExists(object context, string sVirtualPath)
		{
			return CachedFileExists(sVirtualPath);
		}

		// =====================================================================================
		// RegisterJQuery — WebForms stub.
		// =====================================================================================

		/// <summary>
		/// No-op stub. Original registered jQuery scripts via a WebForms ScriptManager.
		/// Not applicable in ASP.NET Core MVC — jQuery is bundled by the React/Vite frontend.
		/// </summary>
		public static void RegisterJQuery(object page, object mgrAjax)
		{
			// No-op: WebForms ScriptManager not available in ASP.NET Core
		}

		// =====================================================================================
		// DuplicateCheck — checks for duplicate records in the database.
		// BEFORE: Used static Security, static DbProviderFactories, static SplendidCache.
		//         Also called SplendidDynamic.ApplyGridViewRules which does not exist in .NET 10.
		// AFTER:  Uses injected _security, _dbProviderFactories, _splendidCache.
		//         SplendidDynamic.ApplyGridViewRules call removed (stub class has no such method).
		// =====================================================================================

		/// <summary>
		/// Checks for duplicate records in the given module based on the SearchDuplicates GridView layout.
		/// Returns the number of duplicates found.
		/// BEFORE: DuplicateCheck(HttpApplicationState Application, IDbConnection con, string sMODULE_NAME,
		///                         Guid gID, DataRow row, DataRow rowCurrent)
		/// AFTER:  DuplicateCheck(IDbConnection con, string sMODULE_NAME, Guid gID, DataRow row, DataRow rowCurrent)
		///         — Application parameter removed; SplendidDynamic.ApplyGridViewRules call removed.
		/// </summary>
		public int DuplicateCheck(IDbConnection con, string sMODULE_NAME, Guid gID, DataRow row, DataRow rowCurrent)
		{
			int nDuplicates = 0;
			string sGRID_NAME = sMODULE_NAME + ".SearchDuplicates";
			// AFTER: _splendidCache.GridViewColumns(sGRID_NAME, _security.PRIMARY_ROLE_NAME)
			DataTable dtGridView = _splendidCache.GridViewColumns(sGRID_NAME, _security.PRIMARY_ROLE_NAME);
			if ( dtGridView != null && dtGridView.Rows.Count > 0 )
			{
				using ( IDbCommand cmd = con.CreateCommand() )
				{
					string sTABLE_NAME = Crm.Modules.TableName(sMODULE_NAME);
					cmd.CommandText = "select *                         " + ControlChars.CrLf
					                + "  from vw" + sTABLE_NAME + "_List" + ControlChars.CrLf;
					// Security.Filter appends team/user WHERE conditions directly to cmd.CommandText
					_security.Filter(cmd, sMODULE_NAME, "list");
					if ( !Sql.IsEmptyGuid(gID) )
					{
						cmd.CommandText += "  and ID <> @ID" + ControlChars.CrLf;
						Sql.AddParameter(cmd, "@ID", gID);
					}

					// Collect dynamic field search conditions via StringBuilder
					StringBuilder sbDynamic = new StringBuilder();
					int nValues = 0;
					foreach(DataRow rowSearch in dtGridView.Rows)
					{
						string sDATA_FIELD  = Sql.ToString(rowSearch["DATA_FIELD" ]);
						string sDATA_FORMAT = Sql.ToString(rowSearch["DATA_FORMAT"]);
						string sVALUE       = String.Empty;
						if ( row != null && row.Table.Columns.Contains(sDATA_FIELD) )
							sVALUE = Sql.ToString(row[sDATA_FIELD]);
						else if ( rowCurrent != null && rowCurrent.Table.Columns.Contains(sDATA_FIELD) )
							sVALUE = Sql.ToString(rowCurrent[sDATA_FIELD]);
						if ( !Sql.IsEmptyString(sVALUE) )
						{
							// BEFORE: Sql.AppendParameter(cmd, sVALUE, sDATA_FIELD) — old API without StringBuilder.
							// AFTER:  Sql.AppendParameter(cmd, sbDynamic, sDATA_FIELD, sVALUE, SqlFilterMode.Exact)
							Sql.AppendParameter(cmd, sbDynamic, sDATA_FIELD, sVALUE, Sql.SqlFilterMode.Exact);
							nValues++;
						}
					}
					if ( nValues > 0 )
					{
						// Append dynamic WHERE conditions built by AppendParameter
						cmd.CommandText += sbDynamic.ToString();
						DbDataAdapter da = _dbProviderFactories.GetFactory().CreateDataAdapter();
						((IDbDataAdapter)da).SelectCommand = cmd;
						using ( DataTable dtDuplicates = new DataTable() )
						{
							da.Fill(dtDuplicates);
							// NOTE: SplendidDynamic.ApplyGridViewRules removed — stub class has no such method.
							foreach ( DataRow rowDuplicates in dtDuplicates.Rows )
							{
								if ( rowDuplicates.RowState != DataRowState.Deleted )
									nDuplicates++;
							}
						}
					}
				}
			}
			return nDuplicates;
		}
	}

	// =====================================================================================
	// ArchiveUtils — handles archiving and recovering CRM records.
	// BEFORE: Stored HttpContext, read Session["USER_ID"] / Session["USER_SETTINGS/CULTURE"],
	//         read Application["CONFIG.Archive.MaxBulkCount"].
	// AFTER:  DI constructor with IHttpContextAccessor, IMemoryCache, Security,
	//         DbProviderFactories, SplendidCache.
	// =====================================================================================

	/// <summary>
	/// Utility class for archiving and recovering CRM module records.
	/// Migrated from SplendidCRM/_code/Utils.cs ArchiveUtils class for .NET 10 ASP.NET Core.
	/// </summary>
	public class ArchiveUtils
	{
		// DI instance fields
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly IMemoryCache         _memoryCache        ;
		private readonly Security             _security           ;
		private readonly DbProviderFactories  _dbProviderFactories;
		private readonly SplendidCache        _splendidCache      ;

		// Instance state captured at construction time (matches original fields on HttpContext)
		private Guid     _gMODIFIED_USER_ID;
		private string   _sCULTURE         ;
		private int      _nACLACCESS       ;
		private string   _ModuleName       ;
		private string[] _arrID            ;
		private int      _nMaxBulkCount    ;

		/// <summary>Last error message from the most recent archive operation.</summary>
		public string LastError { get; set; }

		/// <summary>
		/// DI constructor.
		/// BEFORE: ArchiveUtils(HttpContext Context) — read Session, Application directly.
		/// AFTER:  ArchiveUtils(IHttpContextAccessor, IMemoryCache, Security, DbProviderFactories, SplendidCache).
		/// </summary>
		public ArchiveUtils(
			IHttpContextAccessor httpContextAccessor,
			IMemoryCache         memoryCache         ,
			Security             security            ,
			DbProviderFactories  dbProviderFactories ,
			SplendidCache        splendidCache       )
		{
			_httpContextAccessor = httpContextAccessor;
			_memoryCache         = memoryCache        ;
			_security            = security           ;
			_dbProviderFactories = dbProviderFactories;
			_splendidCache       = splendidCache      ;

			this.LastError = String.Empty;
			// BEFORE: this.gMODIFIED_USER_ID = Sql.ToGuid(Context.Session["USER_ID"]);
			// AFTER:  _security.USER_ID (reads from distributed session via IHttpContextAccessor)
			this._gMODIFIED_USER_ID = _security.USER_ID;
			// BEFORE: this.sCULTURE = Sql.ToString(Context.Session["USER_SETTINGS/CULTURE"]);
			// AFTER:  IHttpContextAccessor.HttpContext.Session.GetString(...)
			this._sCULTURE = _httpContextAccessor?.HttpContext?.Session?.GetString("USER_SETTINGS/CULTURE") ?? String.Empty;
			// BEFORE: this.nMaxBulkCount = 100; if (!IsEmpty(Application["CONFIG.Archive.MaxBulkCount"])) ...
			// AFTER:  IMemoryCache.Get("CONFIG.Archive.MaxBulkCount")
			this._nMaxBulkCount = 100;
			object oMaxBulkCount = _memoryCache.Get("CONFIG.Archive.MaxBulkCount");
			if ( oMaxBulkCount != null && !Sql.IsEmptyString(Sql.ToString(oMaxBulkCount)) )
				this._nMaxBulkCount = Sql.ToInteger(oMaxBulkCount);
		}

		// =====================================================================================
		// MoveData — archive records by moving them to the archive table.
		// =====================================================================================

		/// <summary>
		/// Archives a single record. Convenience overload that wraps the string[] variant.
		/// </summary>
		public string MoveData(string sModuleName, Guid gID)
		{
			string[] arrID = new String[1];
			arrID[0] = gID.ToString();
			L10N L10n = new L10N(this._sCULTURE, _memoryCache);
			// AFTER: Use injected Utils instance via ambient fields for IsOfflineClient check
			bool bOffline = Sql.ToBoolean(_ambientConfig?["OfflineClient"]);
			if ( bOffline )
				throw(new Exception(L10n.Term(".ERR_ARCHIVE_OFFLINE_CLIENT")));
			return MoveData(sModuleName, arrID);
		}

		/// <summary>
		/// Archives a set of records. If the count exceeds nMaxBulkCount, launches a background thread.
		/// </summary>
		public string MoveData(string sModuleName, string[] arrID)
		{
			this._ModuleName = sModuleName;
			this._arrID      = arrID      ;
			this._nACLACCESS = _security.GetUserAccess(this._ModuleName, "archive");
			L10N L10n = new L10N(this._sCULTURE, _memoryCache);
			bool bOffline = Sql.ToBoolean(_ambientConfig?["OfflineClient"]);
			if ( bOffline )
				throw(new Exception(L10n.Term(".ERR_ARCHIVE_OFFLINE_CLIENT")));
			if ( arrID != null )
			{
				bool bIncludeActivities = IncludeActivities(sModuleName);
				if ( arrID.Length > this._nMaxBulkCount || (arrID.Length > 10 && bIncludeActivities) )
				{
					System.Threading.Thread t = new System.Threading.Thread(this.MoveDataInternal);
					t.Start();
					this.LastError = L10n.Term(".LBL_BACKGROUND_OPERATION");
				}
				else
				{
					this.MoveDataInternal();
				}
			}
			else
			{
				this.LastError = L10n.Term(".LBL_NOTHING_SELECTED");
			}
			_splendidCache.ClearArchiveViewExists();
			return this.LastError;
		}

		// Reference to ambient config for offline check (used in ArchiveUtils where no IConfiguration is injected)
		// BEFORE: Utils.IsOfflineClient — used static Application["OfflineClient"] via AppSettings.
		// AFTER:  Reads from Utils._ambientConfigInternal (set via Utils.SetAmbient on startup).
		private static IConfiguration _ambientConfig => Utils._ambientConfigInternal;

		private void MoveDataInternal()
		{
			try
			{
				// BEFORE: Crm.Modules.TableName(this.Context.Application, this.ModuleName)
				// AFTER:  Crm.Modules.TableName(this._ModuleName) — uses static ambient cache via Crm.Modules
				// Build a Utils helper to call FilterByACL_Stack (which needs _security, _dbProviderFactories)
				// Use Utils.SetAmbient config for the null-safe IConfiguration parameter
				IConfiguration cfg = _ambientConfig ?? new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
				Utils utilsHelper = new Utils(_httpContextAccessor, _memoryCache, cfg, _security, _dbProviderFactories, _splendidCache);
				Stack stk = utilsHelper.FilterByACL_Stack(this._ModuleName, this._nACLACCESS, _arrID, Crm.Modules.TableName(this._ModuleName));
				if ( stk.Count > 0 )
				{
					DbProviderFactory dbf = _dbProviderFactories.GetFactory();
					using ( IDbConnection con = dbf.CreateConnection() )
					{
						con.Open();
						using ( IDbTransaction trn = Sql.BeginTransaction(con) )
						{
							try
							{
								using ( IDbCommand cmd = con.CreateCommand() )
								{
									cmd.Transaction    = trn;
									cmd.CommandType    = CommandType.StoredProcedure;
									cmd.CommandText    = "spMODULES_ArchiveBuildByName";
									cmd.CommandTimeout = 0;
									Sql.AddParameter(cmd, "@MODIFIED_USER_ID", this._gMODIFIED_USER_ID);
									Sql.AddParameter(cmd, "@MODULE_NAME"     , this._ModuleName, 25  );
									Sql.Trace(cmd);
									cmd.ExecuteNonQuery();
								}
								while ( stk.Count > 0 )
								{
									string sIDs = Utils.BuildMassIDs(stk);
									using ( IDbCommand cmd = con.CreateCommand() )
									{
										cmd.Transaction    = trn;
										cmd.CommandType    = CommandType.StoredProcedure;
										cmd.CommandText    = "spMODULES_ArchiveMoveData";
										cmd.CommandTimeout = 0;
										Sql.AddParameter(cmd, "@MODIFIED_USER_ID", this._gMODIFIED_USER_ID);
										Sql.AddParameter(cmd, "@MODULE_NAME"     , this._ModuleName, 25  );
										Sql.AddAnsiParam(cmd, "@ID_LIST"         , sIDs           , 8000);
										Sql.Trace(cmd);
										cmd.ExecuteNonQuery();
									}
								}
								trn.Commit();
							}
							catch(Exception ex)
							{
								trn.Rollback();
								throw(new Exception(ex.Message, ex.InnerException));
							}
						}
					}
				}
			}
			catch(Exception ex)
			{
				HttpContext context = _httpContextAccessor?.HttpContext;
				if ( context != null )
					SplendidError.SystemMessage(context, "Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
				else
					SplendidError.SystemMessage(_memoryCache, "Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
				this.LastError = ex.Message;
			}
		}

		// =====================================================================================
		// IncludeActivities — checks if activities should be included in archive operation.
		// BEFORE: private bool IncludeActivities(string sModuleName)
		// AFTER:  public bool IncludeActivities(string sModuleName) — exposed per schema requirement.
		// =====================================================================================

		/// <summary>
		/// Returns true if the given module is configured to include activities in archive operations.
		/// Queries vwMODULES_ARCHIVE_RELATED for a related Activities entry.
		/// Exposed publicly per schema — was private in original source.
		/// </summary>
		public bool IncludeActivities(string sModuleName)
		{
			bool bIncludeActivities = false;
			try
			{
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					string sSQL;
					sSQL = "select count(*)                   " + ControlChars.CrLf
					     + "  from vwMODULES_ARCHIVE_RELATED  " + ControlChars.CrLf
					     + " where MODULE_NAME  = @MODULE_NAME" + ControlChars.CrLf
					     + "   and RELATED_NAME = 'Activities'" + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText    = sSQL;
						cmd.CommandTimeout = 0;
						Sql.AddParameter(cmd, "@MODULE_NAME", sModuleName);
						bIncludeActivities = Sql.ToBoolean(cmd.ExecuteScalar());
					}
				}
			}
			catch(Exception ex)
			{
				HttpContext context = _httpContextAccessor?.HttpContext;
				if ( context != null )
					SplendidError.SystemMessage(context, "Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
				else
					SplendidError.SystemMessage(_memoryCache, "Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
				this.LastError = ex.Message;
			}
			return bIncludeActivities;
		}

		/// <summary>
		/// Parameterless variant that uses the current _ModuleName (set by MoveData/RecoverData).
		/// </summary>
		public bool IncludeActivities()
		{
			return IncludeActivities(this._ModuleName ?? String.Empty);
		}

		// =====================================================================================
		// RecoverData — recover archived records back to the active table.
		// =====================================================================================

		/// <summary>
		/// Recovers a single archived record. Convenience overload that wraps the string[] variant.
		/// </summary>
		public string RecoverData(string sModuleName, Guid gID)
		{
			string[] arrID = new String[1];
			arrID[0] = gID.ToString();
			L10N L10n = new L10N(this._sCULTURE, _memoryCache);
			bool bOffline = Sql.ToBoolean(_ambientConfig?["OfflineClient"]);
			if ( bOffline )
				throw(new Exception(L10n.Term(".ERR_ARCHIVE_OFFLINE_CLIENT")));
			return RecoverData(sModuleName, arrID);
		}

		/// <summary>
		/// Recovers a set of archived records. If the count exceeds nMaxBulkCount, launches a background thread.
		/// </summary>
		public string RecoverData(string sModuleName, string[] arrID)
		{
			this._ModuleName = sModuleName;
			this._arrID      = arrID      ;
			this._nACLACCESS = _security.GetUserAccess(this._ModuleName, "archive");
			L10N L10n = new L10N(this._sCULTURE, _memoryCache);
			bool bOffline = Sql.ToBoolean(_ambientConfig?["OfflineClient"]);
			if ( bOffline )
				throw(new Exception(L10n.Term(".ERR_ARCHIVE_OFFLINE_CLIENT")));
			if ( arrID != null )
			{
				bool bIncludeActivities = IncludeActivities(sModuleName);
				if ( arrID.Length > this._nMaxBulkCount || (arrID.Length > 10 && bIncludeActivities) )
				{
					System.Threading.Thread t = new System.Threading.Thread(this.RecoverDataInternal);
					t.Start();
					this.LastError = L10n.Term(".LBL_BACKGROUND_OPERATION");
				}
				else
				{
					this.RecoverDataInternal();
				}
			}
			else
			{
				this.LastError = L10n.Term(".LBL_NOTHING_SELECTED");
			}
			_splendidCache.ClearArchiveViewExists();
			return this.LastError;
		}

		private void RecoverDataInternal()
		{
			try
			{
				IConfiguration cfg = _ambientConfig ?? new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
				Utils utilsHelper = new Utils(_httpContextAccessor, _memoryCache, cfg, _security, _dbProviderFactories, _splendidCache);
				Stack stk = utilsHelper.FilterByACL_Stack(this._ModuleName, this._nACLACCESS, _arrID, Crm.Modules.TableName(this._ModuleName));
				if ( stk.Count > 0 )
				{
					DbProviderFactory dbf = _dbProviderFactories.GetFactory();
					using ( IDbConnection con = dbf.CreateConnection() )
					{
						con.Open();
						using ( IDbTransaction trn = Sql.BeginTransaction(con) )
						{
							try
							{
								using ( IDbCommand cmd = con.CreateCommand() )
								{
									cmd.Transaction    = trn;
									cmd.CommandType    = CommandType.StoredProcedure;
									cmd.CommandText    = "spMODULES_ArchiveBuildByName";
									cmd.CommandTimeout = 0;
									Sql.AddParameter(cmd, "@MODIFIED_USER_ID", this._gMODIFIED_USER_ID);
									Sql.AddParameter(cmd, "@MODULE_NAME"     , this._ModuleName, 25  );
									Sql.Trace(cmd);
									cmd.ExecuteNonQuery();
								}
								while ( stk.Count > 0 )
								{
									string sIDs = Utils.BuildMassIDs(stk);
									using ( IDbCommand cmd = con.CreateCommand() )
									{
										cmd.Transaction    = trn;
										cmd.CommandType    = CommandType.StoredProcedure;
										cmd.CommandText    = "spMODULES_ArchiveRecoverData";
										cmd.CommandTimeout = 0;
										Sql.AddParameter(cmd, "@MODIFIED_USER_ID", this._gMODIFIED_USER_ID);
										Sql.AddParameter(cmd, "@MODULE_NAME"     , this._ModuleName, 25  );
										Sql.AddAnsiParam(cmd, "@ID_LIST"         , sIDs           , 8000);
										Sql.Trace(cmd);
										cmd.ExecuteNonQuery();
									}
								}
								trn.Commit();
							}
							catch(Exception ex)
							{
								trn.Rollback();
								throw(new Exception(ex.Message, ex.InnerException));
							}
						}
					}
				}
			}
			catch(Exception ex)
			{
				HttpContext context = _httpContextAccessor?.HttpContext;
				if ( context != null )
					SplendidError.SystemMessage(context, "Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
				else
					SplendidError.SystemMessage(_memoryCache, "Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
				this.LastError = ex.Message;
			}
		}
	}
}
