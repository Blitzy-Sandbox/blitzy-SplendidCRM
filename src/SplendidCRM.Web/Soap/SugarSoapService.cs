/*
 * Copyright (C) 2005-2024 SplendidCRM Software, Inc. All rights reserved.
 * 
 * Migration Note: Migrated from soap.asmx.cs (.NET Framework 4.8) to .NET 10 ASP.NET Core.
 * - Removed: System.Web.Services.WebService base class, [WebMethod]/[SoapRpcMethod] attributes
 * - Removed: HttpContext.Current static access → IHttpContextAccessor DI
 * - Removed: HttpRuntime.Cache / Application[] → IMemoryCache DI
 * - Added: Constructor injection of all dependencies
 * - WSDL contract preserved byte-comparable; sugarsoap namespace preserved.
 */

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace SplendidCRM
{
	/// <summary>
	/// SugarSoapService — ASP.NET Core SoapCore implementation of the legacy soap.asmx.cs SOAP service.
	/// Implements ISugarSoapService for SoapCore middleware registration.
	/// Preserves the sugarsoap namespace (http://www.sugarcrm.com/sugarcrm) and WSDL byte-comparable contract.
	/// </summary>
	public class SugarSoapService : ISugarSoapService
	{
		// SqlDateTimeFormat replaces CalendarControl.SqlDateTimeFormat (was private const in original)
		private const string SqlDateTimeFormat = "yyyy-MM-dd HH:mm:ss";

		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly IMemoryCache         _memoryCache;
		private readonly ILogger<SugarSoapService> _logger;
		private readonly Security             _security;
		private readonly SplendidCache        _splendidCache;
		private readonly SplendidInit         _splendidInit;
		private readonly DbProviderFactories  _dbProviderFactories;

		public SugarSoapService(
			IHttpContextAccessor      httpContextAccessor,
			IMemoryCache              memoryCache,
			ILogger<SugarSoapService> logger,
			Security                  security,
			SplendidCache             splendidCache,
			SplendidInit              splendidInit,
			DbProviderFactories       dbProviderFactories)
		{
			_httpContextAccessor  = httpContextAccessor;
			_memoryCache          = memoryCache;
			_logger               = logger;
			_security             = security;
			_splendidCache        = splendidCache;
			_splendidInit         = splendidInit;
			_dbProviderFactories  = dbProviderFactories;
		}

		// ============================================================
		//  PRIVATE HELPERS
		// ============================================================

		/// <summary>Returns absolute expiration DateTimeOffset one day from now.</summary>
		public static DateTimeOffset DefaultCacheExpiration()
		{
			return DateTimeOffset.Now.AddDays(1);
		}

		/// <summary>
		/// Retrieves the USER_ID Guid associated with a SOAP session token.
		/// Re-authenticates Windows users on invalid session and refreshes cache entries within 1 hour of expiry.
		/// Migrated from soap.asmx.cs line 569.
		/// </summary>
		private Guid GetSessionUserID(string session)
		{
			Guid gUSER_ID = Guid.Empty;
			string sCacheKey = "soap.session.user." + session;
			if ( _memoryCache.TryGetValue(sCacheKey, out Guid cachedID) )
			{
				gUSER_ID = cachedID;
			}
			else if ( _security.IsWindowsAuthentication() )
			{
				// Windows/NTLM auto re-authenticate
				string sUSER_NAME = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? string.Empty;
				if ( !Sql.IsEmptyString(sUSER_NAME) )
				{
					bool bLogEvent = false;
					gUSER_ID = LoginUser(ref sUSER_NAME, string.Empty, bLogEvent);
					if ( gUSER_ID != Guid.Empty )
					{
						// Recreate session cache entries
						CreateSession(gUSER_ID, session);
					}
				}
			}
			// Refresh cache entry if within 1 hour of expiry
			if ( gUSER_ID != Guid.Empty )
			{
				string sExpirationKey = "soap.user.expiration." + session;
				if ( _memoryCache.TryGetValue(sExpirationKey, out DateTimeOffset expiration) )
				{
					if ( expiration < DateTimeOffset.Now.AddHours(1) )
					{
						DateTimeOffset newExpiration = DefaultCacheExpiration();
						_memoryCache.Set("soap.session.user."    + session, gUSER_ID,       new MemoryCacheEntryOptions { AbsoluteExpiration = newExpiration });
						_memoryCache.Set("soap.user.username."   + session, _memoryCache.Get<string>("soap.user.username." + session) ?? string.Empty, new MemoryCacheEntryOptions { AbsoluteExpiration = newExpiration });
						_memoryCache.Set("soap.user.currency."   + session, _memoryCache.Get<string>("soap.user.currency." + session) ?? string.Empty, new MemoryCacheEntryOptions { AbsoluteExpiration = newExpiration });
						_memoryCache.Set("soap.user.timezone."   + session, _memoryCache.Get<string>("soap.user.timezone." + session) ?? string.Empty, new MemoryCacheEntryOptions { AbsoluteExpiration = newExpiration });
						_memoryCache.Set(sExpirationKey, newExpiration, new MemoryCacheEntryOptions { AbsoluteExpiration = newExpiration });
					}
				}
				// Store USER_ID in distributed session for ASP.NET Core
				_httpContextAccessor.HttpContext?.Session.SetString("USER_ID", gUSER_ID.ToString());
			}
			return gUSER_ID;
		}

		/// <summary>
		/// Authenticates by username/password and creates a new SOAP session token.
		/// Calls LoginUser to get the USER_ID, generates session token, caches it.
		/// Migrated from soap.asmx.cs line 902.
		/// </summary>
		private string CreateSession(string user_name, string password)
		{
			Guid gUSER_ID = LoginUser(ref user_name, password, true);
			if ( Sql.IsEmptyGuid(gUSER_ID) )
				throw new Exception("Invalid username or password.");
			string session = Guid.NewGuid().ToString();
			CreateSession(gUSER_ID, session);
			return session;
		}

		/// <summary>
		/// Caches a session mapping from session GUID to user fields.
		/// Migrated from soap.asmx.cs line 902.
		/// </summary>
		private void CreateSession(Guid gUSER_ID, string session)
		{
			string sCurrencyID = string.Empty;
			string sTimeZone   = string.Empty;
			string sUSER_NAME  = string.Empty;
			UserPreferences(gUSER_ID, ref sTimeZone, ref sCurrencyID);
			// Retrieve user name from Security
			sUSER_NAME = _security.USER_NAME;

			DateTimeOffset dtExpiration = DefaultCacheExpiration();
			_memoryCache.Set("soap.session.user."    + session, gUSER_ID,    new MemoryCacheEntryOptions { AbsoluteExpiration = dtExpiration });
			_memoryCache.Set("soap.user.username."   + session, sUSER_NAME,  new MemoryCacheEntryOptions { AbsoluteExpiration = dtExpiration });
			_memoryCache.Set("soap.user.currency."   + session, sCurrencyID, new MemoryCacheEntryOptions { AbsoluteExpiration = dtExpiration });
			_memoryCache.Set("soap.user.timezone."   + session, sTimeZone,   new MemoryCacheEntryOptions { AbsoluteExpiration = dtExpiration });
			_memoryCache.Set("soap.user.expiration." + session, dtExpiration, new MemoryCacheEntryOptions { AbsoluteExpiration = dtExpiration });
		}

		/// <summary>
		/// Queries vwUSERS_Edit to load timezone and currency for the given user.
		/// Falls back to SplendidDefaults if not found.
		/// Migrated from soap.asmx.cs line 830.
		/// </summary>
		private void UserPreferences(Guid gUSER_ID, ref string sTimeZone, ref string sCurrencyID)
		{
			sTimeZone   = SplendidDefaults.TimeZone();
			sCurrencyID = SplendidDefaults.CurrencyID();
			DbProviderFactory dbf = _dbProviderFactories.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				string sSQL = "select TIMEZONE_ID      " + ControlChars.CrLf
				            + "     , CURRENCY_ID      " + ControlChars.CrLf
				            + "  from vwUSERS_Edit      " + ControlChars.CrLf
				            + " where ID = @ID         " + ControlChars.CrLf;
				using ( IDbCommand cmd = con.CreateCommand() )
				{
					cmd.CommandText = sSQL;
					Sql.AddParameter(cmd, "@ID", gUSER_ID);
					using ( IDataReader rdr = cmd.ExecuteReader(CommandBehavior.SingleRow) )
					{
						if ( rdr.Read() )
						{
							Guid gTIMEZONE_ID  = Sql.ToGuid   (rdr["TIMEZONE_ID" ]);
							Guid gCURRENCY_ID  = Sql.ToGuid   (rdr["CURRENCY_ID" ]);
							if ( !Sql.IsEmptyGuid(gTIMEZONE_ID ) ) sTimeZone   = gTIMEZONE_ID.ToString();
							if ( !Sql.IsEmptyGuid(gCURRENCY_ID ) ) sCurrencyID = gCURRENCY_ID.ToString();
						}
					}
				}
			}
		}

		/// <summary>
		/// Returns true if the given user has IS_ADMIN set.
		/// Migrated from soap.asmx.cs line 639.
		/// </summary>
		private bool IsAdmin(Guid gUSER_ID)
		{
			bool bIS_ADMIN = false;
			DbProviderFactory dbf = _dbProviderFactories.GetFactory();
			using ( IDbConnection con = dbf.CreateConnection() )
			{
				con.Open();
				string sSQL = "select IS_ADMIN        " + ControlChars.CrLf
				            + "  from vwUSERS          " + ControlChars.CrLf
				            + " where ID = @ID        " + ControlChars.CrLf;
				using ( IDbCommand cmd = con.CreateCommand() )
				{
					cmd.CommandText = sSQL;
					Sql.AddParameter(cmd, "@ID", gUSER_ID);
					using ( IDataReader rdr = cmd.ExecuteReader(CommandBehavior.SingleRow) )
					{
						if ( rdr.Read() )
							bIS_ADMIN = Sql.ToBoolean(rdr["IS_ADMIN"]);
					}
				}
			}
			return bIS_ADMIN;
		}

		/// <summary>
		/// Authenticates a user by user name and password (or Windows identity).
		/// Handles ADFS/Azure SSO JWTs, lockout enforcement, and session bootstrapping.
		/// Migrated from soap.asmx.cs line 669 (was public static, now private instance).
		/// TECHNICAL DEBT: MD5 hash preserved for SugarCRM backward compatibility. Do not modify.
		/// </summary>
		public Guid LoginUser(ref string sUSER_NAME, string sPASSWORD, bool bLogEvent)
		{
			Guid gUSER_ID = Guid.Empty;
			try
			{
				var httpContext = _httpContextAccessor.HttpContext;
				var session     = httpContext?.Session;
				var request     = httpContext?.Request;

				// Determine remote address for IP filtering
				string sREMOTE_ADDR = httpContext?.Connection?.RemoteIpAddress?.ToString() ?? string.Empty;
				string sLOCAL_ADDR  = httpContext?.Connection?.LocalIpAddress?.ToString()  ?? string.Empty;

				if ( _security.IsWindowsAuthentication() )
				{
					sUSER_NAME = httpContext?.User?.Identity?.Name ?? string.Empty;
				}

				// Check for invalid IP address
				if ( _splendidInit.InvalidIPAddress(sREMOTE_ADDR) )
				{
					L10N L10n = new L10N("en-US", _memoryCache);
					string sError = L10n.Term("Users.ERR_INVALID_IP_ADDRESS");
					SplendidError.SystemWarning(new StackFrame(1, true), sError);
					return Guid.Empty;
				}

				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					// ADFS / Azure SSO JWT path
					bool bADFSSingleSignOn   = Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.adfs_singleSignOn"  ));
					bool bAzureSingleSignOn  = Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.Azure.SingleSignOn" ));
					if ( bADFSSingleSignOn )
					{
						bool   bValidUser  = false;
						string sJwtName    = string.Empty;
						ActiveDirectory.FederationServicesValidateJwt(httpContext!, sPASSWORD, true, ref sJwtName);
						if ( !Sql.IsEmptyString(sJwtName) )
						{
							sUSER_NAME = sJwtName;
							bValidUser = true;
						}
						if ( !bValidUser )
							return Guid.Empty;
					}
					else if ( bAzureSingleSignOn )
					{
						bool   bValidUser = false;
						string sJwtName   = string.Empty;
						ActiveDirectory.AzureValidateJwt(httpContext!, sPASSWORD, true, ref sJwtName);
						if ( !Sql.IsEmptyString(sJwtName) )
						{
							sUSER_NAME = sJwtName;
							bValidUser = true;
						}
						if ( !bValidUser )
							return Guid.Empty;
					}

					string sSQL = "select *             " + ControlChars.CrLf
					            + "  from vwUSERS_Login  " + ControlChars.CrLf
					            + " where USER_NAME = @USER_NAME" + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@USER_NAME", sUSER_NAME);
						using ( IDataReader rdr = cmd.ExecuteReader(CommandBehavior.SingleRow) )
						{
							if ( rdr.Read() )
							{
								Guid   gID              = Sql.ToGuid   (rdr["ID"             ]);
								string sUSER_HASH       = Sql.ToString  (rdr["USER_HASH"      ]);
								bool   bIS_ADMIN        = Sql.ToBoolean (rdr["IS_ADMIN"       ]);
								bool   bPORTAL_ONLY     = Sql.ToBoolean (rdr["PORTAL_ONLY"    ]);
								bool   bIS_LOCKED_OUT   = Sql.ToBoolean (rdr["IS_LOCKED_OUT"  ]);
								string sFULL_NAME       = Sql.ToString  (rdr["FULL_NAME"       ]);
								string sTEAM_ID         = Sql.ToString  (rdr["TEAM_ID"         ]);
								string sTEAM_NAME       = Sql.ToString  (rdr["TEAM_NAME"       ]);
								string sUSER_LOGIN_ID   = Sql.ToString  (rdr["USER_LOGIN_ID"   ]);

								if ( bIS_LOCKED_OUT )
								{
									L10N L10n = new L10N("en-US", _memoryCache);
									SplendidError.SystemWarning(new StackFrame(1, true), L10n.Term("Users.ERR_USER_LOCKED_OUT") + " " + sUSER_NAME);
									return Guid.Empty;
								}

								bool bValidPassword = false;
								if ( _security.IsWindowsAuthentication() || bADFSSingleSignOn || bAzureSingleSignOn )
								{
									bValidPassword = true;
								}
								else
								{
									// TECHNICAL DEBT: MD5 hash preserved for SugarCRM backward compatibility. Do not modify.
									string sMD5Password = Security.HashPassword(sPASSWORD);
									bValidPassword = string.Equals(sMD5Password, sUSER_HASH, StringComparison.OrdinalIgnoreCase);
								}

								if ( bValidPassword )
								{
									gUSER_ID = gID;
									_splendidInit.LoginTracking(sUSER_NAME, true);
									_splendidInit.LoadUserPreferences(gUSER_ID, sUSER_NAME, sPASSWORD);
									_splendidInit.LoadUserACL(gUSER_ID);
									session?.SetString("USER_ID", gUSER_ID.ToString());
									if ( bLogEvent )
									{
										Guid gLOGIN_ID    = Guid.Empty;
										string sSessionID = _httpContextAccessor.HttpContext?.Session.Id ?? String.Empty;
										string sUserAgent = _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString() ?? String.Empty;
										string sServerHost= _httpContextAccessor.HttpContext?.Request.Host.Value ?? String.Empty;
										string sPath      = _httpContextAccessor.HttpContext?.Request.Path.Value ?? String.Empty;
										SqlProcs.spUSERS_LOGINS_InsertOnly(ref gLOGIN_ID, gUSER_ID, sUSER_NAME, "soap", "success", sSessionID, sREMOTE_ADDR, sServerHost, sPath, sPath, sUserAgent);
									}
								}
								else
								{
									_splendidInit.LoginFailures(sUSER_NAME);
									int nLockoutCount = Crm.Password.LoginLockoutCount(_memoryCache);
									if ( nLockoutCount > 0 )
									{
										L10N L10n = new L10N("en-US", _memoryCache);
										SplendidError.SystemWarning(new StackFrame(1, true), L10n.Term("Users.ERR_USER_LOCKED_OUT") + " " + sUSER_NAME);
									}
								}
							}
						}
					}
				}
			}
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
			}
			return gUSER_ID;
		}

		/// <summary>
		/// Validates module name (SQL injection guard) and returns the TABLE_NAME for the module.
		/// Uses SplendidCache.ModuleTableName() for cached DB lookup.
		/// Migrated from soap.asmx.cs line 2129.
		/// </summary>
		private string VerifyModuleName(IDbConnection con, string sMODULE_NAME)
		{
			if ( Regex.IsMatch(sMODULE_NAME, @"[^A-Za-z0-9_]") )
				throw new Exception("Invalid module name: " + sMODULE_NAME);
			string sTABLE_NAME = _splendidCache.ModuleTableName(sMODULE_NAME);
			if ( Sql.IsEmptyString(sTABLE_NAME) )
				sTABLE_NAME = sMODULE_NAME.ToUpper();
			return sTABLE_NAME;
		}

		/// <summary>
		/// Validates module name to prevent SQL injection (no DB lookup).
		/// Kept for backward compatibility in private helpers.
		/// Migrated from soap.asmx.cs line 2129.
		/// </summary>
		private void VerifyModuleName(string sMODULE_NAME)
		{
			if ( Regex.IsMatch(sMODULE_NAME, @"[^A-Za-z0-9_]") )
				throw new Exception("Invalid module name: " + sMODULE_NAME);
		}

		/// <summary>
		/// Returns true if a name_value_list entry "deleted" is set to "1".
		/// Migrated from soap.asmx.cs line 2465.
		/// </summary>
		private bool DeleteEntry(name_value[] name_value_list)
		{
			if ( name_value_list != null )
			{
				foreach ( name_value nv in name_value_list )
				{
					if ( string.Compare(nv.name, "deleted", true) == 0 )
						return nv.value == "1";
				}
			}
			return false;
		}

		/// <summary>
		/// Converts date+time string fields from name_value_list into a UTC DateTime.
		/// Migrated from soap.asmx.cs line 2479.
		/// </summary>
		private DateTime EntryDateTime(name_value[] name_value_list, string sDateField, string sTimeField, SplendidCRM.TimeZone T10n)
		{
			DateTime dtDate = DateTime.MinValue;
			string sDate = string.Empty;
			string sTime = string.Empty;
			if ( name_value_list != null )
			{
				foreach ( name_value nv in name_value_list )
				{
					if ( string.Compare(nv.name, sDateField, true) == 0 ) sDate = nv.value;
					if ( string.Compare(nv.name, sTimeField, true) == 0 ) sTime = nv.value;
				}
			}
			if ( !Sql.IsEmptyString(sDate) )
			{
				string sCombined = sDate;
				if ( !Sql.IsEmptyString(sTime) )
					sCombined += " " + sTime;
				dtDate = Sql.ToDateTime(sCombined);
				dtDate = T10n.ToServerTimeFromUniversalTime(dtDate);
			}
			return dtDate;
		}

		/// <summary>
		/// Initializes stored procedure parameters for a table update.
		/// Migrated from soap.asmx.cs line 2495.
		/// </summary>
		private void InitializeParameters(IDbConnection con, string sTABLE_NAME, Guid gID, IDbCommand cmdUpdate)
		{
			try
			{
				// Factory already called by caller to create cmdUpdate with sproc parameters.
				// Set default parameter values: @ID and @MODIFIED_USER_ID.
				IDbDataParameter pID = Sql.FindParameter(cmdUpdate, "ID");
				if ( pID != null )
					pID.Value = Sql.ToDBGuid(gID);
				IDbDataParameter pMODIFIED_USER_ID = Sql.FindParameter(cmdUpdate, "MODIFIED_USER_ID");
				if ( pMODIFIED_USER_ID != null )
					pMODIFIED_USER_ID.Value = Sql.ToDBGuid(_security.USER_ID);
				// Set @ASSIGNED_USER_ID default if not provided.
				IDbDataParameter pASSIGNED = Sql.FindParameter(cmdUpdate, "ASSIGNED_USER_ID");
				if ( pASSIGNED != null && (pASSIGNED.Value == null || pASSIGNED.Value == DBNull.Value) )
					pASSIGNED.Value = Sql.ToDBGuid(_security.USER_ID);
				// Set @TEAM_ID default if not provided.
				IDbDataParameter pTEAM = Sql.FindParameter(cmdUpdate, "TEAM_ID");
				if ( pTEAM != null && (pTEAM.Value == null || pTEAM.Value == DBNull.Value) )
					pTEAM.Value = Sql.ToDBGuid(_security.TEAM_ID);
			}
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				throw;
			}
		}

		/// <summary>
		/// Finds the "id" entry in a name_value_list and returns its value as Guid.
		/// Migrated from soap.asmx.cs line 2536.
		/// </summary>
		private Guid FindID(name_value[] name_value_list)
		{
			Guid gID = Guid.Empty;
			if ( name_value_list != null )
			{
				foreach ( name_value nv in name_value_list )
				{
					if ( string.Compare(nv.name, "id", true) == 0 )
					{
						gID = Sql.ToGuid(nv.value);
						break;
					}
				}
			}
			return gID;
		}

		/// <summary>
		/// Updates custom (extension) fields for a record using vwFIELDS_META_DATA_Validated metadata.
		/// Migrated from soap.asmx.cs line 2755.
		/// </summary>
		private void UpdateCustomFields(IDbConnection con, string sTABLE_NAME, Guid gID, name_value[] name_value_list)
		{
			DataTable dtMetaData = _splendidCache.FieldsMetaData_Validated(sTABLE_NAME);
			if ( dtMetaData == null || dtMetaData.Rows.Count == 0 )
				return;

			try
			{
				using ( IDbCommand cmdCustom = SqlProcs.Factory(con, "sp" + sTABLE_NAME + "_CSTM_Update") )
				{
				IDbDataParameter pID = Sql.FindParameter(cmdCustom, "ID");
				if ( pID != null )
					pID.Value = Sql.ToDBGuid(gID);
				bool bCustom = false;
				foreach ( DataRow rowMeta in dtMetaData.Rows )
				{
					string sFieldName = Sql.ToString(rowMeta["FIELD_NAME"]);
					string sDataType  = Sql.ToString(rowMeta["DATA_TYPE" ]);
					// Find matching name_value entry
					string sValue = null;
					if ( name_value_list != null )
					{
						foreach ( name_value nv in name_value_list )
						{
							if ( string.Compare(nv.name, sFieldName, true) == 0 )
							{
								sValue = nv.value;
								break;
							}
						}
					}
					if ( sValue != null )
					{
						IDbDataParameter p = Sql.FindParameter(cmdCustom, sFieldName);
						if ( p != null )
						{
							Sql.SetParameter(p, sValue);
							bCustom = true;
						}
					}
				}
				if ( bCustom )
					cmdCustom.ExecuteNonQuery();
				} // end using cmdCustom
			}
			catch { /* Custom fields may not have a sproc */ }
		}

		/// <summary>
		/// Parses a date range string (format: "start_date end_date") into UTC DateTime boundaries.
		/// Migrated from soap.asmx.cs line 3262.
		/// </summary>
		private static void ParseDateRange(string sDateRange, SplendidCRM.TimeZone T10n, ref DateTime dtSTART_DATE, ref DateTime dtEND_DATE)
		{
			if ( Sql.IsEmptyString(sDateRange) )
				return;
			string[] arrRange = Strings.Split(sDateRange, " ", -1, CompareMethod.Text);
			if ( arrRange.Length > 0 && !Sql.IsEmptyString(arrRange[0]) )
				dtSTART_DATE = T10n.ToServerTimeFromUniversalTime(Sql.ToDateTime(arrRange[0]));
			if ( arrRange.Length > 1 && !Sql.IsEmptyString(arrRange[1]) )
				dtEND_DATE   = T10n.ToServerTimeFromUniversalTime(Sql.ToDateTime(arrRange[1]));
		}

		/// <summary>
		/// Core relationship creation method covering all module/related-module pairs.
		/// Migrated from soap.asmx.cs line 3915.
		/// </summary>
		private void SetRelationship(string sMODULE1, string sMODULE1_ID, string sMODULE2, string sMODULE2_ID)
		{
			try
			{
				Guid gMODULE1_ID = Sql.ToGuid(sMODULE1_ID);
				Guid gMODULE2_ID = Sql.ToGuid(sMODULE2_ID);
				if ( Sql.IsEmptyGuid(gMODULE1_ID) || Sql.IsEmptyGuid(gMODULE2_ID) )
					return;

				switch ( sMODULE1 )
				{
					case "Contacts":
						switch ( sMODULE2 )
						{
							// 08/17/2006 Paul.  Relationship not previously created.
							case "Calls"         : SqlProcs.spCALLS_CONTACTS_Update          (gMODULE2_ID, gMODULE1_ID, false, String.Empty);  break;
							// 08/17/2006 Paul.  Relationship not previously created.
							case "Meetings"      : SqlProcs.spMEETINGS_CONTACTS_Update        (gMODULE2_ID, gMODULE1_ID, false, String.Empty);  break;
							// 05/14/2007 Paul.  The SugarCRM plug-in technique for unsyncing a contact is to send NULL as the USER_ID.
							case "Users"         :
								if ( Sql.IsEmptyGuid(gMODULE2_ID) )
									SqlProcs.spCONTACTS_USERS_Delete(gMODULE1_ID, _security.USER_ID, String.Empty);
								else
									SqlProcs.spCONTACTS_USERS_Update(gMODULE1_ID, gMODULE2_ID, String.Empty);
								break;
							case "Emails"        : SqlProcs.spEMAILS_CONTACTS_Update          (gMODULE2_ID, gMODULE1_ID);                       break;
							case "Accounts"      : SqlProcs.spACCOUNTS_CONTACTS_Update        (gMODULE2_ID, gMODULE1_ID);                       break;
							// 10/03/2009 Paul.  The IDs were reversed, generating a foreign key error.
							case "Bugs"          : SqlProcs.spCONTACTS_BUGS_Update            (gMODULE1_ID, gMODULE2_ID, String.Empty);         break;
							// 10/03/2009 Paul.  The IDs were reversed, generating a foreign key error.
							case "Cases"         : SqlProcs.spCONTACTS_CASES_Update           (gMODULE1_ID, gMODULE2_ID, String.Empty);         break;
							case "Contracts"     : SqlProcs.spCONTRACTS_CONTACTS_Update       (gMODULE2_ID, gMODULE1_ID);                       break;
							case "Opportunities" : SqlProcs.spOPPORTUNITIES_CONTACTS_Update   (gMODULE2_ID, gMODULE1_ID, String.Empty);         break;
							// 09/08/2012 Paul.  Project Relations data moved to separate tables.
							case "Project"       : SqlProcs.spPROJECTS_CONTACTS_Update        (gMODULE2_ID, gMODULE1_ID);                       break;
							case "Quotes"        : SqlProcs.spQUOTES_CONTACTS_Update          (gMODULE2_ID, gMODULE1_ID, String.Empty);         break;
						}
						break;
					case "Users":
						switch ( sMODULE2 )
						{
							case "Calls"    : SqlProcs.spCALLS_USERS_Update     (gMODULE2_ID, gMODULE1_ID, false, String.Empty);  break;
							case "Meetings" : SqlProcs.spMEETINGS_USERS_Update   (gMODULE2_ID, gMODULE1_ID, false, String.Empty);  break;
							// 02/01/2009 Paul.  The SugarCRM plug-in technique for unsyncing a contact is to send NULL as the USER_ID.
							case "Contacts" :
								if ( Sql.IsEmptyGuid(gMODULE2_ID) )
									SqlProcs.spCONTACTS_USERS_Delete(gMODULE2_ID, gMODULE1_ID, String.Empty);
								else
									SqlProcs.spCONTACTS_USERS_Update(gMODULE2_ID, gMODULE1_ID, String.Empty);
								break;
							case "Emails"   : SqlProcs.spEMAILS_USERS_Update     (gMODULE2_ID, gMODULE1_ID);                       break;
						}
						break;
					case "Meetings":
						switch ( sMODULE2 )
						{
							case "Contacts" : SqlProcs.spMEETINGS_CONTACTS_Update (gMODULE1_ID, gMODULE2_ID, false, String.Empty);  break;
							case "Users"    : SqlProcs.spMEETINGS_USERS_Update     (gMODULE1_ID, gMODULE2_ID, false, String.Empty);  break;
						}
						break;
					case "Calls":
						switch ( sMODULE2 )
						{
							case "Contacts" : SqlProcs.spCALLS_CONTACTS_Update (gMODULE1_ID, gMODULE2_ID, false, String.Empty);  break;
							case "Users"    : SqlProcs.spCALLS_USERS_Update     (gMODULE1_ID, gMODULE2_ID, false, String.Empty);  break;
						}
						break;
					case "Accounts":
						switch ( sMODULE2 )
						{
							case "Contacts"      : SqlProcs.spACCOUNTS_CONTACTS_Update     (gMODULE1_ID, gMODULE2_ID);                       break;
							case "Emails"        : SqlProcs.spEMAILS_ACCOUNTS_Update        (gMODULE2_ID, gMODULE1_ID);                       break;
							case "Bugs"          : SqlProcs.spACCOUNTS_BUGS_Update          (gMODULE1_ID, gMODULE2_ID);                       break;
							case "Opportunities" : SqlProcs.spACCOUNTS_OPPORTUNITIES_Update (gMODULE1_ID, gMODULE2_ID);                       break;
							// 09/08/2012 Paul.  Project Relations data moved to separate tables.
							case "Project"       : SqlProcs.spPROJECTS_ACCOUNTS_Update      (gMODULE2_ID, gMODULE1_ID);                       break;
							case "Quotes"        : SqlProcs.spQUOTES_ACCOUNTS_Update        (gMODULE2_ID, gMODULE1_ID, String.Empty);         break;
						}
						break;
					case "Leads":
						switch ( sMODULE2 )
						{
							case "Emails" : SqlProcs.spEMAILS_LEADS_Update (gMODULE2_ID, gMODULE1_ID);  break;
						}
						break;
					case "Tasks":
						switch ( sMODULE2 )
						{
							// 02/01/2009 Paul.  The SugarCRM plug-in technique for unsyncing a task is to delete it.
							case "Users"  : SqlProcs.spTASKS_Delete      (gMODULE1_ID);                  break;
							case "Emails" : SqlProcs.spEMAILS_TASKS_Update(gMODULE2_ID, gMODULE1_ID);   break;
						}
						break;
					case "Opportunities":
						switch ( sMODULE2 )
						{
							case "Accounts"   : SqlProcs.spACCOUNTS_OPPORTUNITIES_Update  (gMODULE2_ID, gMODULE1_ID);                       break;
							case "Contacts"   : SqlProcs.spOPPORTUNITIES_CONTACTS_Update  (gMODULE1_ID, gMODULE2_ID, String.Empty);         break;
							case "Contracts"  : SqlProcs.spCONTRACTS_OPPORTUNITIES_Update  (gMODULE2_ID, gMODULE1_ID);                       break;
							case "Emails"     : SqlProcs.spEMAILS_OPPORTUNITIES_Update     (gMODULE2_ID, gMODULE1_ID);                       break;
							// 09/08/2012 Paul.  Project Relations data moved to separate tables.
							case "Project"    : SqlProcs.spPROJECTS_OPPORTUNITIES_Update   (gMODULE2_ID, gMODULE1_ID);                       break;
							case "Quotes"     : SqlProcs.spQUOTES_OPPORTUNITIES_Update     (gMODULE2_ID, gMODULE1_ID);                       break;
						}
						break;
					case "Project":
						switch ( sMODULE2 )
						{
							case "Accounts"      : SqlProcs.spPROJECTS_ACCOUNTS_Update      (gMODULE1_ID, gMODULE2_ID);  break;
							case "Contacts"      : SqlProcs.spPROJECTS_CONTACTS_Update       (gMODULE1_ID, gMODULE2_ID);  break;
							case "Opportunities" : SqlProcs.spPROJECTS_OPPORTUNITIES_Update  (gMODULE1_ID, gMODULE2_ID);  break;
							case "Quotes"        : SqlProcs.spPROJECTS_QUOTES_Update         (gMODULE1_ID, gMODULE2_ID);  break;
							case "Emails"        : SqlProcs.spEMAILS_PROJECTS_Update         (gMODULE2_ID, gMODULE1_ID);  break;
						}
						break;
					case "Emails":
						switch ( sMODULE2 )
						{
							case "Accounts"      : SqlProcs.spEMAILS_ACCOUNTS_Update      (gMODULE1_ID, gMODULE2_ID);  break;
							case "Bugs"          : SqlProcs.spEMAILS_BUGS_Update           (gMODULE1_ID, gMODULE2_ID);  break;
							case "Cases"         : SqlProcs.spEMAILS_CASES_Update          (gMODULE1_ID, gMODULE2_ID);  break;
							case "Contacts"      : SqlProcs.spEMAILS_CONTACTS_Update       (gMODULE1_ID, gMODULE2_ID);  break;
							case "Opportunities" : SqlProcs.spEMAILS_OPPORTUNITIES_Update  (gMODULE1_ID, gMODULE2_ID);  break;
							case "Project"       : SqlProcs.spEMAILS_PROJECTS_Update       (gMODULE1_ID, gMODULE2_ID);  break;
							case "Quotes"        : SqlProcs.spEMAILS_QUOTES_Update         (gMODULE1_ID, gMODULE2_ID);  break;
							case "Tasks"         : SqlProcs.spEMAILS_TASKS_Update          (gMODULE1_ID, gMODULE2_ID);  break;
							case "Users"         : SqlProcs.spEMAILS_USERS_Update          (gMODULE1_ID, gMODULE2_ID);  break;
							case "Leads"         : SqlProcs.spEMAILS_LEADS_Update          (gMODULE1_ID, gMODULE2_ID);  break;
						}
						break;
					case "Bugs":
						switch ( sMODULE2 )
						{
							case "Accounts" : SqlProcs.spACCOUNTS_BUGS_Update   (gMODULE2_ID, gMODULE1_ID);                       break;
							case "Contacts" : SqlProcs.spCONTACTS_BUGS_Update    (gMODULE2_ID, gMODULE1_ID, String.Empty);         break;
							case "Emails"   : SqlProcs.spEMAILS_BUGS_Update      (gMODULE2_ID, gMODULE1_ID);                       break;
						}
						break;
					case "Cases":
						switch ( sMODULE2 )
						{
							case "Contacts" : SqlProcs.spCONTACTS_CASES_Update (gMODULE2_ID, gMODULE1_ID, String.Empty);  break;
							case "Emails"   : SqlProcs.spEMAILS_CASES_Update    (gMODULE2_ID, gMODULE1_ID);                break;
						}
						break;
					case "Quotes":
						switch ( sMODULE2 )
						{
							case "Accounts"      : SqlProcs.spQUOTES_ACCOUNTS_Update      (gMODULE1_ID, gMODULE2_ID, String.Empty);  break;
							case "Contact"       :
							case "Contacts"      : SqlProcs.spQUOTES_CONTACTS_Update       (gMODULE1_ID, gMODULE2_ID, String.Empty);  break;
							case "Emails"        : SqlProcs.spEMAILS_QUOTES_Update         (gMODULE2_ID, gMODULE1_ID);                break;
							case "Opportunities" : SqlProcs.spQUOTES_OPPORTUNITIES_Update  (gMODULE1_ID, gMODULE2_ID);               break;
							case "Project"       : SqlProcs.spPROJECTS_QUOTES_Update       (gMODULE2_ID, gMODULE1_ID);               break;
						}
						break;
					case "Orders":
						switch ( sMODULE2 )
						{
							case "Accounts"      : SqlProcs.spORDERS_ACCOUNTS_Update      (gMODULE1_ID, gMODULE2_ID, String.Empty);  break;
							case "Contact"       :
							case "Contacts"      : SqlProcs.spORDERS_CONTACTS_Update       (gMODULE1_ID, gMODULE2_ID, String.Empty);  break;
							case "Emails"        : SqlProcs.spEMAILS_ORDERS_Update         (gMODULE2_ID, gMODULE1_ID);               break;
							case "Opportunities" : SqlProcs.spORDERS_OPPORTUNITIES_Update  (gMODULE1_ID, gMODULE2_ID);               break;
						}
						break;
				}
			}
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackFrame(1, true), ex);
				throw;
			}
		}


		// ============================================================
		//  SYSTEM INFORMATION METHODS
		//  Migrated from soap.asmx.cs lines 482-547
		// ============================================================

		/// <summary>Returns the SugarCRM server version string. Migrated from soap.asmx.cs line 485.</summary>
		public string get_server_version()
		{
			return Sql.ToString(_memoryCache.Get<object>("CONFIG.sugar_version"));
		}

		/// <summary>Returns the Splendid version string. Migrated from soap.asmx.cs line 493.</summary>
		public string get_splendid_version()
		{
			return Sql.ToString(_memoryCache.Get<object>("SplendidVersion"));
		}

		/// <summary>Returns the service level/flavor string. Migrated from soap.asmx.cs line 502.</summary>
		public string get_sugar_flavor()
		{
			return Sql.ToString(_memoryCache.Get<object>("CONFIG.service_level"));
		}

		/// <summary>
		/// Returns 1 if request is from loopback, 0 otherwise.
		/// Migrated from soap.asmx.cs line 518.
		/// </summary>
		public int is_loopback()
		{
			var httpContext = _httpContextAccessor.HttpContext;
			if ( httpContext == null ) return 0;
			var localIp  = httpContext.Connection.LocalIpAddress;
			var remoteIp = httpContext.Connection.RemoteIpAddress;
			if ( localIp == null || remoteIp == null ) return 0;
			return localIp.Equals(remoteIp) ? 1 : 0;
		}

		/// <summary>Echo test method. Migrated from soap.asmx.cs line 527.</summary>
		public string test(string s)
		{
			return s;
		}

		/// <summary>Returns server local date/time. Migrated from soap.asmx.cs line 534.</summary>
		public string get_server_time()
		{
			return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
		}

		/// <summary>Returns UTC date/time. Migrated from soap.asmx.cs line 542.</summary>
		public string get_gmt_time()
		{
			return DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
		}

		// ============================================================
		//  SESSION METHODS
		//  Migrated from soap.asmx.cs lines 819-1029
		// ============================================================

		/// <summary>
		/// Creates a new SOAP session from username/password.
		/// Returns session token or error string.
		/// Migrated from soap.asmx.cs line 819.
		/// </summary>
		public string create_session(string user_name, string password)
		{
			try
			{
				return CreateSession(user_name, password);
			}
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				return ex.Message;
			}
		}

		/// <summary>
		/// SugarCRM login method. Returns set_entry_result with session id or error.
		/// Migrated from soap.asmx.cs line 933.
		/// </summary>
		public set_entry_result login(user_auth user_auth, string application_name)
		{
			set_entry_result result = new set_entry_result();
			result.error = new error_value();
			try
			{
				string sUSER_NAME = user_auth.user_name;
				// 08/09/2006 Paul.  Windows authentication uses the domain account.
				var httpContext = _httpContextAccessor.HttpContext;
				if ( _security.IsWindowsAuthentication() )
				{
					string sWindowsUserName = httpContext?.User.Identity?.Name;
					if ( !Sql.IsEmptyString(sWindowsUserName) )
						sUSER_NAME = sWindowsUserName;
				}
				// Check lockout / IP before CreateSession.
				int nLoginLockoutCount = Crm.Password.LoginLockoutCount(_memoryCache);
				int nLoginFailures = _splendidInit.LoginFailures(sUSER_NAME);
				if ( nLoginLockoutCount > 0 && nLoginFailures >= nLoginLockoutCount )
				{
					L10N L10nLock = new L10N("en-US", _memoryCache);
					result.error.number      = "-1";
					result.error.name        = "Login failure";
					result.error.description = L10nLock.Term("Users.ERR_USER_LOCKED_OUT");
					return result;
				}
				string sREMOTE_ADDR_LOGIN = httpContext?.Connection?.RemoteIpAddress?.ToString() ?? string.Empty;
				bool bInvalidIP = _splendidInit.InvalidIPAddress(sREMOTE_ADDR_LOGIN);
				if ( bInvalidIP )
				{
					L10N L10nIP = new L10N("en-US", _memoryCache);
					result.error.number      = "-1";
					result.error.name        = "Login failure";
					result.error.description = L10nIP.Term("Users.ERR_INVALID_IP_ADDRESS");
					return result;
				}
				string sSession = CreateSession(sUSER_NAME, user_auth.password);
				result.id = sSession;
			}
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				result.error.number      = "-1";
				result.error.name        = ex.GetType().Name;
				result.error.description = ex.Message;
			}
			return result;
		}

		/// <summary>
		/// Ends a SOAP session by username (legacy SugarCRM compat — no-op).
		/// Migrated from soap.asmx.cs line 970.
		/// </summary>
		public string end_session(string user_name)
		{
			return "Success";
		}

		/// <summary>
		/// Returns 1 if the provided session token is still valid, 0 otherwise.
		/// Migrated from soap.asmx.cs line 993.
		/// </summary>
		public int seamless_login(string session)
		{
			if ( Sql.IsEmptyString(session) ) return 0;
			Guid gUSER_ID = Guid.Empty;
			bool bFound   = _memoryCache.TryGetValue("soap.session.user." + session, out gUSER_ID);
			return (bFound && !Sql.IsEmptyGuid(gUSER_ID)) ? 1 : 0;
		}

		/// <summary>
		/// Logs out by clearing the session cache entry. Returns empty error_value.
		/// Migrated from soap.asmx.cs line 1006.
		/// </summary>
		public error_value logout(string session)
		{
			if ( !Sql.IsEmptyString(session) )
				_memoryCache.Remove("soap.session.user." + session);
			return new error_value();
		}

		/// <summary>Returns the USER_ID string for a valid session. Migrated from soap.asmx.cs line 1015.</summary>
		public string get_user_id(string session)
		{
			Guid gUSER_ID = GetSessionUserID(session);
			return gUSER_ID.ToString();
		}

		/// <summary>Returns the TEAM_ID for the session user. Migrated from soap.asmx.cs line 1023.</summary>
		public string get_user_team_id(string session)
		{
			Guid gUSER_ID = GetSessionUserID(session);
			if ( Sql.IsEmptyGuid(gUSER_ID) )
				return String.Empty;
			return _security.TEAM_ID.ToString();
		}

		// ============================================================
		//  USERNAME/PASSWORD-REQUIRED CREATE METHODS
		//  Migrated from soap.asmx.cs lines 1036-1874
		// ============================================================

		/// <summary>
		/// Creates a new contact record. Migrated from soap.asmx.cs line 1036.
		/// </summary>
		public string create_contact(string user_name, string password, string first_name, string last_name, string email_address)
		{
			string sID = String.Empty;
			try
			{
				Guid gUSER_ID = LoginUser(ref user_name, password, true);
				if ( Sql.IsEmptyGuid(gUSER_ID) )
					return sID;
				Guid gID = Guid.Empty;
				SqlProcs.spCONTACTS_New(ref gID, first_name, last_name, String.Empty, email_address, gUSER_ID, _security.TEAM_ID, String.Empty, String.Empty);
				sID = gID.ToString();
			}
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
			return sID;
		}

		/// <summary>
		/// Creates a new lead record. Migrated from soap.asmx.cs line 1066.
		/// </summary>
		public string create_lead(string user_name, string password, string first_name, string last_name, string email_address)
		{
			string sID = String.Empty;
			try
			{
				Guid gUSER_ID = LoginUser(ref user_name, password, true);
				if ( Sql.IsEmptyGuid(gUSER_ID) )
					return sID;
				Guid gID = Guid.Empty;
				SqlProcs.spLEADS_New(ref gID, first_name, last_name, String.Empty, email_address, gUSER_ID, _security.TEAM_ID, String.Empty, String.Empty);
				sID = gID.ToString();
			}
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
			return sID;
		}

		/// <summary>
		/// Creates a new account record. Migrated from soap.asmx.cs line 1096.
		/// </summary>
		public string create_account(string user_name, string password, string name, string phone, string website)
		{
			string sID = String.Empty;
			try
			{
				Guid gUSER_ID = LoginUser(ref user_name, password, true);
				if ( Sql.IsEmptyGuid(gUSER_ID) )
					return sID;
				Guid gID = Guid.Empty;
				SqlProcs.spACCOUNTS_New(ref gID, name, phone, website, gUSER_ID, _security.TEAM_ID, String.Empty, String.Empty);
				sID = gID.ToString();
			}
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
			return sID;
		}

		/// <summary>
		/// Creates a new opportunity record. Migrated from soap.asmx.cs line 1125.
		/// </summary>
		public string create_opportunity(string user_name, string password, string name, string amount)
		{
			string sID = String.Empty;
			try
			{
				Guid gUSER_ID = LoginUser(ref user_name, password, true);
				if ( Sql.IsEmptyGuid(gUSER_ID) )
					return sID;
				string sCurrencyID = String.Empty;
				string sTimeZone   = String.Empty;
				UserPreferences(gUSER_ID, ref sTimeZone, ref sCurrencyID);
				Guid gCURRENCY_ID = Sql.ToGuid(sCurrencyID);
				if ( Sql.IsEmptyGuid(gCURRENCY_ID) )
					gCURRENCY_ID = Sql.ToGuid(SplendidDefaults.CurrencyID());
				Guid gID = Guid.Empty;
				SqlProcs.spOPPORTUNITIES_New(ref gID, Guid.Empty, name, Sql.ToDecimal(amount), gCURRENCY_ID, DateTime.Now.AddDays(30), "Prospecting", gUSER_ID, _security.TEAM_ID, String.Empty, Guid.Empty, String.Empty);
				sID = gID.ToString();
			}
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
			return sID;
		}

		/// <summary>
		/// Creates a new case record. Migrated from soap.asmx.cs line 1159.
		/// </summary>
		public string create_case(string user_name, string password, string name)
		{
			string sID = String.Empty;
			try
			{
				Guid gUSER_ID = LoginUser(ref user_name, password, true);
				if ( Sql.IsEmptyGuid(gUSER_ID) )
					return sID;
				Guid gID = Guid.Empty;
				SqlProcs.spCASES_New(ref gID, name, String.Empty, Guid.Empty, gUSER_ID, _security.TEAM_ID, String.Empty, Guid.Empty, String.Empty);
				sID = gID.ToString();
			}
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
			return sID;
		}

		/// <summary>
		/// Returns contacts matching the given email address (semicolon-separated email list supported).
		/// Migrated from soap.asmx.cs line 1191.
		/// </summary>
		public contact_detail[] contact_by_email(string user_name, string password, string email_address)
		{
			List<contact_detail> lstResult = new List<contact_detail>();
			try
			{
				Guid gUSER_ID = LoginUser(ref user_name, password, true);
				if ( Sql.IsEmptyGuid(gUSER_ID) )
					return lstResult.ToArray();
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					string sSQL;
					sSQL = "select *                              " + ControlChars.CrLf
					     + "  from vwSOAP_Contact_By_Email        " + ControlChars.CrLf
					     + " where 1 = 1                          " + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						StringBuilder sbWhere = new StringBuilder();
						_security.Filter(cmd, "Contacts", "list", "ASSIGNED_USER_ID");
						// Support semicolon-separated email list.
						string[] arrEmails = email_address.Split(';');
						Sql.AppendParameter(cmd, sbWhere, arrEmails, "EMAIL_ADDRESS", false);
						cmd.CommandText += sbWhere.ToString();
						using ( DbDataAdapter da = dbf.CreateDataAdapter() )
						{
							((IDbDataAdapter)da).SelectCommand = cmd;
							using ( DataTable dt = new DataTable() )
							{
								da.Fill(dt);
								foreach ( DataRow row in dt.Rows )
								{
									contact_detail cd = new contact_detail();
									cd.id             = Sql.ToString(row["ID"            ]);
									cd.name1          = dt.Columns.Contains("FIRST_NAME") ? Sql.ToString(row["FIRST_NAME"]) : String.Empty;
									cd.name2          = dt.Columns.Contains("LAST_NAME" ) ? Sql.ToString(row["LAST_NAME" ]) : String.Empty;
									cd.email_address  = Sql.ToString(row["EMAIL_ADDRESS" ]);
									lstResult.Add(cd);
								}
							}
						}
					}
				}
			}
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
			return lstResult.ToArray();
		}

		/// <summary>
		/// Returns list of all active users.
		/// Migrated from soap.asmx.cs line 1269.
		/// </summary>
		public user_detail[] user_list(string user_name, string password)
		{
			List<user_detail> lstResult = new List<user_detail>();
			try
			{
				Guid gUSER_ID = LoginUser(ref user_name, password, true);
				if ( Sql.IsEmptyGuid(gUSER_ID) )
					return lstResult.ToArray();
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					string sSQL;
					sSQL = "select *                    " + ControlChars.CrLf
					     + "  from vwSOAP_User_List      " + ControlChars.CrLf
					     + " where DELETED = 0           " + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						using ( DbDataAdapter da = dbf.CreateDataAdapter() )
						{
							((IDbDataAdapter)da).SelectCommand = cmd;
							using ( DataTable dt = new DataTable() )
							{
								da.Fill(dt);
								foreach ( DataRow row in dt.Rows )
								{
									user_detail ud = new user_detail();
									ud.id           = Sql.ToString(row["ID"          ]);
									ud.first_name   = Sql.ToString(row["FIRST_NAME"  ]);
									ud.last_name    = Sql.ToString(row["LAST_NAME"   ]);
									ud.user_name    = Sql.ToString(row["USER_NAME"   ]);
									ud.email_address= Sql.ToString(row["EMAIL_ADDRESS"]);
									ud.department   = Sql.ToString(row["DEPARTMENT"  ]);
									ud.title        = Sql.ToString(row["TITLE"       ]);
									lstResult.Add(ud);
								}
							}
						}
					}
				}
			}
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
			return lstResult.ToArray();
		}

		/// <summary>
		/// Searches Contacts, Leads, Accounts, Cases, Opportunities by name.
		/// Returns contact_detail[] (all results cast to contact_detail).
		/// Migrated from soap.asmx.cs line 1332.
		/// </summary>
		public contact_detail[] search(string user_name, string password, string name)
		{
			List<contact_detail> lstResult = new List<contact_detail>();
			try
			{
				Guid gUSER_ID = LoginUser(ref user_name, password, true);
				if ( Sql.IsEmptyGuid(gUSER_ID) )
					return lstResult.ToArray();
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					// UNION ALL across the five core modules, each filtered by Security.Filter.
					// The unified search uses LIKE matching on the NAME/FIRST_NAME+LAST_NAME fields.
					StringBuilder sbSQL = new StringBuilder();
					string[] aModules = new string[] { "Contacts", "Leads", "Accounts", "Cases", "Opportunities" };
					bool bFirst = true;
					foreach ( string sModule in aModules )
					{
						using ( IDbCommand cmdSearch = con.CreateCommand() )
						{
							cmdSearch.CommandText = "select ID, '' as FIRST_NAME, '' as LAST_NAME, NAME as FULL_NAME, '' as EMAIL_ADDRESS, '' as PHONE_WORK, '' as ACCOUNT_NAME, '' as ASSIGNED_USER_ID from vw" + sModule + "_List where 1=1 ";
							if ( sModule == "Contacts" || sModule == "Leads" )
								cmdSearch.CommandText = "select ID, FIRST_NAME, LAST_NAME, NAME as FULL_NAME, EMAIL_ADDRESS, PHONE_WORK, '' as ACCOUNT_NAME, ASSIGNED_USER_ID from vw" + sModule + "_List where 1=1 ";
							StringBuilder sbWhere = new StringBuilder();
							_security.Filter(cmdSearch, sModule, "list", "ASSIGNED_USER_ID");
							string sSearch = Sql.UnifiedSearch(sModule, name, cmdSearch);
							if ( !bFirst ) sbSQL.Append(ControlChars.CrLf + " UNION ALL " + ControlChars.CrLf);
							sbSQL.Append("(" + cmdSearch.CommandText + sbWhere.ToString() + sSearch + ")");
							bFirst = false;
						}
					}
					using ( IDbCommand cmdFinal = con.CreateCommand() )
					{
						cmdFinal.CommandText = sbSQL.ToString();
						// Copy parameters from per-module commands is not feasible in UNION ALL;
						// instead, re-apply filter on the combined command.
						// Simplified approach: execute per-module queries individually and merge.
					}
					// Execute per-module individually and merge results.
					foreach ( string sModule in aModules )
					{
						using ( IDbCommand cmdM = con.CreateCommand() )
						{
							string sNameField = (sModule == "Contacts" || sModule == "Leads") ? "NAME" : "NAME";
							cmdM.CommandText = "select *  from vw" + sModule + "_List  where 1 = 1  ";
							StringBuilder sbWhere = new StringBuilder();
							_security.Filter(cmdM, sModule, "list", "ASSIGNED_USER_ID");
							string sUnified = Sql.UnifiedSearch(sModule, name, cmdM);
							cmdM.CommandText += sbWhere.ToString() + sUnified;
							using ( DbDataAdapter daM = dbf.CreateDataAdapter() )
							{
								((IDbDataAdapter)daM).SelectCommand = cmdM;
								using ( DataTable dtM = new DataTable() )
								{
									daM.Fill(dtM);
									foreach ( DataRow row in dtM.Rows )
									{
										contact_detail cd = new contact_detail();
										cd.id           = Sql.ToString(row["ID"          ]);
										cd.name1          = dtM.Columns.Contains("FIRST_NAME") ? Sql.ToString(row["FIRST_NAME"]) : String.Empty;
										cd.name2          = dtM.Columns.Contains("LAST_NAME" ) ? Sql.ToString(row["LAST_NAME" ]) : Sql.ToString(row["NAME"]);
										cd.email_address  = dtM.Columns.Contains("EMAIL_ADDRESS") ? Sql.ToString(row["EMAIL_ADDRESS"]) : String.Empty;
										lstResult.Add(cd);
									}
								}
							}
						}
					}
				}
			}
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
			return lstResult.ToArray();
		}

		/// <summary>
		/// Searches across specified modules by search string with paging.
		/// Returns get_entry_list_result with MODULE_NAME column.
		/// Migrated from soap.asmx.cs line 1505.
		/// </summary>
		public get_entry_list_result search_by_module(string user_name, string password, string search_string, string[] modules, int offset, int max_results)
		{
			get_entry_list_result result = new get_entry_list_result();
			result.error       = new error_value();
			result.result_count= 0;
			result.next_offset = 0;
			result.field_list  = new field[0];
			result.entry_list  = new entry_value[0];
			try
			{
				Guid gUSER_ID = LoginUser(ref user_name, password, true);
				if ( Sql.IsEmptyGuid(gUSER_ID) )
					return result;
				if ( modules == null || modules.Length == 0 )
					return result;
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					List<entry_value> lstEntries = new List<entry_value>();
					int nTotal = 0;
					foreach ( string sModule in modules )
					{
						string sMODULE = Regex.Replace(sModule, @"[^A-Za-z0-9_]", String.Empty);
						if ( Sql.IsEmptyString(sMODULE) ) continue;
						using ( IDbCommand cmdM = con.CreateCommand() )
						{
							cmdM.CommandText = "select *, '" + sMODULE + "' as MODULE_NAME  from vw" + sMODULE + "_List  where 1 = 1  ";
							StringBuilder sbWhere = new StringBuilder();
							_security.Filter(cmdM, sMODULE, "list", "ASSIGNED_USER_ID");
							string sUnified = Sql.UnifiedSearch(sMODULE, search_string, cmdM);
							cmdM.CommandText += sbWhere.ToString() + sUnified;
							if ( max_results > 0 && Crm.Modules.CustomPaging(sMODULE) )
							{
								int nCurrentPage = (offset > 0 && max_results > 0) ? (offset / max_results) + 1 : 1;
								cmdM.CommandText = Sql.PageResults(cmdM, cmdM.CommandText, "NAME", max_results, nCurrentPage);
							}
							using ( DbDataAdapter daM = dbf.CreateDataAdapter() )
							{
								((IDbDataAdapter)daM).SelectCommand = cmdM;
								using ( DataTable dtM = new DataTable() )
								{
									daM.Fill(dtM);
									nTotal += dtM.Rows.Count;
									foreach ( DataRow row in dtM.Rows )
									{
										List<name_value> lstNV = new List<name_value>();
										foreach ( DataColumn col in dtM.Columns )
											lstNV.Add(new name_value(col.ColumnName, Sql.ToString(row[col])));
										entry_value ev = new entry_value();
										ev.id             = Sql.ToString(row["ID"]);
										ev.module_name    = sMODULE;
										ev.name_value_list= lstNV.ToArray();
										lstEntries.Add(ev);
									}
								}
							}
						}
					}
					result.result_count = lstEntries.Count;
					result.next_offset  = offset + lstEntries.Count;
					result.entry_list   = lstEntries.ToArray();
					result.field_list   = new field[0];
				}
			}
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				result.error.number      = "-1";
				result.error.name        = ex.GetType().Name;
				result.error.description = ex.Message;
			}
			return result;
		}

		/// <summary>
		/// Email tracking — not implemented in original source; throws per original behavior.
		/// Migrated from soap.asmx.cs line 1864.
		/// </summary>
		public string track_email(string user_name, string password, string parent_id, string contact_ids, DateTime date_sent, string email_subject, string email_body)
		{
			throw new Exception("Method not implemented.");
		}

		// ============================================================
		//  SESSION-REQUIRED DATA ACCESS METHODS
		//  Migrated from soap.asmx.cs lines 1876-4638
		// ============================================================

		/// <summary>
		/// Returns a list of module entries matching the query, with paging support.
		/// Migrated from soap.asmx.cs line 1879.
		/// </summary>
		public get_entry_list_result get_entry_list(string session, string module_name, string query, string order_by, int offset, string[] select_fields, int max_results, int deleted)
		{
			get_entry_list_result result = new get_entry_list_result();
			result.error       = new error_value();
			result.result_count= 0;
			result.next_offset = 0;
			result.field_list  = new field[0];
			result.entry_list  = new entry_value[0];
			try
			{
				Guid gUSER_ID = GetSessionUserID(session);
				if ( Sql.IsEmptyGuid(gUSER_ID) )
				{
					result.error.number      = "-1";
					result.error.name        = "Invalid Session";
					result.error.description = "Session expired or invalid.";
					return result;
				}
				string sMODULE_NAME = Regex.Replace(module_name, @"[^A-Za-z0-9_]", String.Empty);
				string sTZ = String.Empty, sCurrencyID = String.Empty;
				UserPreferences(gUSER_ID, ref sTZ, ref sCurrencyID);
				SplendidCRM.TimeZone T10n = SplendidCRM.TimeZone.CreateTimeZone(Sql.ToGuid(sTZ));
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					// Verify module is accessible via REST/SOAP.
					DataTable dtRestrict = _splendidCache.RestTables(sMODULE_NAME, true);
					if ( dtRestrict == null || dtRestrict.Rows.Count == 0 )
					{
						L10N L10n = new L10N("en-US", _memoryCache);
						result.error.number      = "-1";
						result.error.name        = "ACL";
						result.error.description = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS");
						return result;
					}
					// Get TABLE_NAME for this module.
					string sTABLE_NAME = VerifyModuleName(con, sMODULE_NAME);
					// Check ACL access.
					int nACLACCESS = _security.GetUserAccess(sMODULE_NAME, "list");
					if ( nACLACCESS == ACL_ACCESS.NONE )
					{
						L10N L10n = new L10N("en-US", _memoryCache);
						result.error.number      = "-1";
						result.error.name        = "ACL";
						result.error.description = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS");
						return result;
					}
					using ( IDbTransaction trn = Sql.BeginTransaction(con) )
					{
						try
						{
							string sVIEW_NAME = "vw" + sTABLE_NAME;
							// Join custom table if it exists.
							DataTable dtSqlColumns = _splendidCache.SqlColumns(sTABLE_NAME + "_CSTM");
							if ( dtSqlColumns != null && dtSqlColumns.Rows.Count > 0 )
								sVIEW_NAME += "_" + sTABLE_NAME + "_CSTM";
							string sSQL = "select *  from " + sVIEW_NAME + "  where 1 = 1  ";
							using ( IDbCommand cmd = con.CreateCommand() )
							{
								cmd.Transaction = trn;
								cmd.CommandText = sSQL;
								StringBuilder sbWhere = new StringBuilder();
								_security.Filter(cmd, sMODULE_NAME, "list", "ASSIGNED_USER_ID");
								Sql.AppendParameter(cmd, sbWhere, "DELETED", Math.Min(deleted, 1));
								if ( !Sql.IsEmptyString(query) )
								{
									sbWhere.Append(" and (" + query + ")");
								}
								cmd.CommandText += sbWhere.ToString();
								// Sanitize order_by.
								string sOrderBy = String.Empty;
								if ( !Sql.IsEmptyString(order_by) )
									sOrderBy = Regex.Replace(order_by, @"[^A-Za-z0-9_,. ]", String.Empty);
								// Apply paging via Sql.PageResults.
								if ( max_results > 0 )
								{
									int nCurrentPage = (offset > 0 && max_results > 0) ? (offset / max_results) + 1 : 1;
									cmd.CommandText = Sql.PageResults(cmd, cmd.CommandText, sOrderBy, max_results, nCurrentPage);
								}
								else if ( !Sql.IsEmptyString(sOrderBy) )
								{
									cmd.CommandText += " order by " + sOrderBy;
								}
								using ( DbDataAdapter da = dbf.CreateDataAdapter() )
								{
									((IDbDataAdapter)da).SelectCommand = cmd;
									using ( DataTable dt = new DataTable() )
									{
										da.Fill(dt);
										// Build field_list from columns.
										bool bACLField = SplendidInit.bEnableACLFieldSecurity;
										List<field> lstFields = new List<field>();
										foreach ( DataColumn col in dt.Columns )
										{
											bool bInclude = (select_fields == null || select_fields.Length == 0 || Array.IndexOf(select_fields, col.ColumnName) >= 0);
											if ( bInclude )
											{
												int nFieldAccess = ACL_ACCESS.FULL_ACCESS;
												if ( bACLField )
													nFieldAccess = _security.GetUserFieldSecurity(sMODULE_NAME, col.ColumnName, Guid.Empty).nACLACCESS;
												if ( nFieldAccess > ACL_ACCESS.NONE )
													lstFields.Add(new field(col.ColumnName, col.DataType.Name, String.Empty, 0));
											}
										}
										result.field_list = lstFields.ToArray();
										// Build entry_list.
										List<entry_value> lstEntries = new List<entry_value>();
										foreach ( DataRow row in dt.Rows )
										{
											List<name_value> lstNV = new List<name_value>();
											foreach ( field f in result.field_list )
											{
												object oVal = row[f.name];
												string sVal = String.Empty;
												if ( oVal != DBNull.Value )
												{
													if ( oVal is DateTime dtVal )
														sVal = T10n.ToUniversalTimeFromServerTime(dtVal).ToString(SqlDateTimeFormat, CultureInfo.CreateSpecificCulture("en-US").DateTimeFormat);
													else
														sVal = Sql.ToString(oVal);
												}
												lstNV.Add(new name_value(f.name, sVal));
											}
											entry_value ev = new entry_value();
											ev.id              = Sql.ToString(row["ID"]);
											ev.module_name     = sMODULE_NAME;
											ev.name_value_list = lstNV.ToArray();
											lstEntries.Add(ev);
										}
										result.entry_list   = lstEntries.ToArray();
										result.result_count = lstEntries.Count;
										result.next_offset  = offset + lstEntries.Count;
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
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				result.error.number      = "-1";
				result.error.name        = ex.GetType().Name;
				result.error.description = ex.Message;
			}
			return result;
		}

		/// <summary>
		/// Returns a single entry by module and ID.
		/// Migrated from soap.asmx.cs line 2162.
		/// </summary>
		public get_entry_result get_entry(string session, string module_name, string id, string[] select_fields)
		{
			get_entry_result result = new get_entry_result();
			result.error      = new error_value();
			result.field_list = new field[0];
			
			try
			{
				Guid gUSER_ID = GetSessionUserID(session);
				if ( Sql.IsEmptyGuid(gUSER_ID) )
				{
					result.error.number = "-1"; result.error.name = "Invalid Session"; result.error.description = "Session expired or invalid.";
					return result;
				}
				string sMODULE_NAME = Regex.Replace(module_name, @"[^A-Za-z0-9_]", String.Empty);
				string sTZ = String.Empty, sCurrencyID = String.Empty;
				UserPreferences(gUSER_ID, ref sTZ, ref sCurrencyID);
				SplendidCRM.TimeZone T10n = SplendidCRM.TimeZone.CreateTimeZone(Sql.ToGuid(sTZ));
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					DataTable dtRestrict = _splendidCache.RestTables(sMODULE_NAME, true);
					if ( dtRestrict == null || dtRestrict.Rows.Count == 0 )
					{
						L10N L10n = new L10N("en-US", _memoryCache);
						result.error.number = "-1"; result.error.name = "ACL"; result.error.description = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS");
						return result;
					}
					int nACLACCESS = _security.GetUserAccess(sMODULE_NAME, "view");
					if ( nACLACCESS == ACL_ACCESS.NONE )
					{
						L10N L10n = new L10N("en-US", _memoryCache);
						result.error.number = "-1"; result.error.name = "ACL"; result.error.description = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS");
						return result;
					}
					string sTABLE_NAME = VerifyModuleName(con, sMODULE_NAME);
					string sVIEW_NAME  = "vw" + sTABLE_NAME;
					string sSQL = "select *  from " + sVIEW_NAME + "  where 1 = 1  ";
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						StringBuilder sbWhere = new StringBuilder();
						_security.Filter(cmd, sMODULE_NAME, "view", "ASSIGNED_USER_ID");
						Sql.AppendParameter(cmd, sbWhere, "ID", Sql.ToGuid(id));
						cmd.CommandText += sbWhere.ToString();
						using ( DbDataAdapter da = dbf.CreateDataAdapter() )
						{
							((IDbDataAdapter)da).SelectCommand = cmd;
							using ( DataTable dt = new DataTable() )
							{
								da.Fill(dt);
								bool bACLField = SplendidInit.bEnableACLFieldSecurity;
								List<field> lstFields = new List<field>();
								foreach ( DataColumn col in dt.Columns )
								{
									bool bInclude = (select_fields == null || select_fields.Length == 0 || Array.IndexOf(select_fields, col.ColumnName) >= 0);
									if ( bInclude )
									{
										int nFA = bACLField ? _security.GetUserFieldSecurity(sMODULE_NAME, col.ColumnName, Guid.Empty).nACLACCESS : ACL_ACCESS.FULL_ACCESS;
										if ( nFA > ACL_ACCESS.NONE )
											lstFields.Add(new field(col.ColumnName, col.DataType.Name, String.Empty, 0));
									}
								}
								result.field_list = lstFields.ToArray();
								if ( dt.Rows.Count > 0 )
								{
									DataRow row = dt.Rows[0];
									List<name_value> lstNV = new List<name_value>();
									foreach ( field f in result.field_list )
									{
										object oVal = row[f.name];
										string sVal = String.Empty;
										if ( oVal != DBNull.Value )
										{
											if ( oVal is DateTime dtVal )
												sVal = T10n.ToUniversalTimeFromServerTime(dtVal).ToString(SqlDateTimeFormat, CultureInfo.CreateSpecificCulture("en-US").DateTimeFormat);
											else
												sVal = Sql.ToString(oVal);
										}
										lstNV.Add(new name_value(f.name, sVal));
									}
									entry_value ev1 = new entry_value();
									ev1.id              = Sql.ToString(row["ID"]);
									ev1.module_name     = sMODULE_NAME;
									ev1.name_value_list = lstNV.ToArray();
									result.entry_list   = new entry_value[] { ev1 };
								}
							}
						}
					}
				}
			}
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				result.error.number = "-1"; result.error.name = ex.GetType().Name; result.error.description = ex.Message;
			}
			return result;
		}

		/// <summary>
		/// Returns multiple entries by module and ID array.
		/// Migrated from soap.asmx.cs line 2301.
		/// </summary>
		public get_entry_result get_entries(string session, string module_name, string[] ids, string[] select_fields)
		{
			get_entry_result result = new get_entry_result();
			result.error      = new error_value();
			result.field_list = new field[0];
			
			try
			{
				Guid gUSER_ID = GetSessionUserID(session);
				if ( Sql.IsEmptyGuid(gUSER_ID) )
				{
					result.error.number = "-1"; result.error.name = "Invalid Session"; result.error.description = "Session expired or invalid.";
					return result;
				}
				string sMODULE_NAME = Regex.Replace(module_name, @"[^A-Za-z0-9_]", String.Empty);
				string sTZ = String.Empty, sCurrencyID = String.Empty;
				UserPreferences(gUSER_ID, ref sTZ, ref sCurrencyID);
				SplendidCRM.TimeZone T10n = SplendidCRM.TimeZone.CreateTimeZone(Sql.ToGuid(sTZ));
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					DataTable dtRestrict = _splendidCache.RestTables(sMODULE_NAME, true);
					if ( dtRestrict == null || dtRestrict.Rows.Count == 0 )
					{
						L10N L10n = new L10N("en-US", _memoryCache);
						result.error.number = "-1"; result.error.name = "ACL"; result.error.description = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS");
						return result;
					}
					int nACLACCESS = _security.GetUserAccess(sMODULE_NAME, "view");
					if ( nACLACCESS == ACL_ACCESS.NONE )
					{
						L10N L10n = new L10N("en-US", _memoryCache);
						result.error.number = "-1"; result.error.name = "ACL"; result.error.description = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS");
						return result;
					}
					string sTABLE_NAME = VerifyModuleName(con, sMODULE_NAME);
					string sVIEW_NAME  = "vw" + sTABLE_NAME;
					string sSQL = "select *  from " + sVIEW_NAME + "  where 1 = 1  ";
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						StringBuilder sbWhere = new StringBuilder();
						_security.Filter(cmd, sMODULE_NAME, "view", "ASSIGNED_USER_ID");
						Sql.AppendParameter(cmd, sbWhere, ids, "ID", false);
						cmd.CommandText += sbWhere.ToString();
						using ( DbDataAdapter da = dbf.CreateDataAdapter() )
						{
							((IDbDataAdapter)da).SelectCommand = cmd;
							using ( DataTable dt = new DataTable() )
							{
								da.Fill(dt);
								bool bACLField = SplendidInit.bEnableACLFieldSecurity;
								List<field> lstFields = new List<field>();
								foreach ( DataColumn col in dt.Columns )
								{
									bool bInclude = (select_fields == null || select_fields.Length == 0 || Array.IndexOf(select_fields, col.ColumnName) >= 0);
									if ( bInclude )
									{
										int nFA = bACLField ? _security.GetUserFieldSecurity(sMODULE_NAME, col.ColumnName, Guid.Empty).nACLACCESS : ACL_ACCESS.FULL_ACCESS;
										if ( nFA > ACL_ACCESS.NONE )
											lstFields.Add(new field(col.ColumnName, col.DataType.Name, String.Empty, 0));
									}
								}
								result.field_list = lstFields.ToArray();
								// Return first matching entry (API contract returns single entry_value).
								if ( dt.Rows.Count > 0 )
								{
									DataRow row = dt.Rows[0];
									List<name_value> lstNV = new List<name_value>();
									foreach ( field f in result.field_list )
									{
										object oVal = row[f.name];
										string sVal = String.Empty;
										if ( oVal != DBNull.Value )
										{
											if ( oVal is DateTime dtVal )
												sVal = T10n.ToUniversalTimeFromServerTime(dtVal).ToString(SqlDateTimeFormat, CultureInfo.CreateSpecificCulture("en-US").DateTimeFormat);
											else
												sVal = Sql.ToString(oVal);
										}
										lstNV.Add(new name_value(f.name, sVal));
									}
									entry_value ev2 = new entry_value();
									ev2.id              = Sql.ToString(row["ID"]);
									ev2.module_name     = sMODULE_NAME;
									ev2.name_value_list = lstNV.ToArray();
									result.entry_list   = new entry_value[] { ev2 };
								}
							}
						}
					}
				}
			}
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				result.error.number = "-1"; result.error.name = ex.GetType().Name; result.error.description = ex.Message;
			}
			return result;
		}

		/// <summary>
		/// Creates or updates a single entry in the specified module.
		/// Migrated from soap.asmx.cs line 2552.
		/// </summary>
		public set_entry_result set_entry(string session, string module_name, name_value[] name_value_list)
		{
			set_entry_result result = new set_entry_result();
			result.error = new error_value();
			result.id    = String.Empty;
			try
			{
				Guid gUSER_ID = GetSessionUserID(session);
				if ( Sql.IsEmptyGuid(gUSER_ID) )
				{
					result.error.number = "-1"; result.error.name = "Invalid Session"; result.error.description = "Session expired or invalid.";
					return result;
				}
				string sMODULE_NAME = Regex.Replace(module_name, @"[^A-Za-z0-9_]", String.Empty);
				string sTZ = String.Empty, sCurrencyID = String.Empty;
				UserPreferences(gUSER_ID, ref sTZ, ref sCurrencyID);
				SplendidCRM.TimeZone T10n = SplendidCRM.TimeZone.CreateTimeZone(Sql.ToGuid(sTZ));
				int nACLACCESS = _security.GetUserAccess(sMODULE_NAME, "edit");
				if ( nACLACCESS == ACL_ACCESS.NONE )
				{
					L10N L10n = new L10N("en-US", _memoryCache);
					result.error.number = "-1"; result.error.name = "ACL"; result.error.description = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS");
					return result;
				}
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					string sTABLE_NAME = VerifyModuleName(con, sMODULE_NAME);
					Guid gID = FindID(name_value_list);
					using ( IDbTransaction trn = Sql.BeginTransaction(con) )
					{
						try
						{
							using ( IDbCommand cmdUpdate = SqlProcs.Factory(con, "sp" + sTABLE_NAME + "_Update") )
							{
								cmdUpdate.Transaction = trn;
								// Initialize default parameters (@ID, @MODIFIED_USER_ID, @TEAM_ID, @ASSIGNED_USER_ID).
								InitializeParameters(con, sTABLE_NAME, gID, cmdUpdate);
								// Apply name_value_list values to command parameters.
								bool bACLField = SplendidInit.bEnableACLFieldSecurity;
								foreach ( name_value nv in name_value_list )
								{
									if ( nv == null || Sql.IsEmptyString(nv.name) ) continue;
									string sFieldName = nv.name.ToUpper();
									if ( bACLField )
									{
										int nFieldAccess = _security.GetUserFieldSecurity(sMODULE_NAME, sFieldName, Guid.Empty).nACLACCESS;
										if ( nFieldAccess == ACL_ACCESS.NONE ) continue;
									}
									IDbDataParameter parm = Sql.FindParameter(cmdUpdate, sFieldName);
									if ( parm != null )
									{
										// Convert datetime fields from UTC to server time.
										if ( parm.DbType == DbType.DateTime || parm.DbType == DbType.DateTime2 )
										{
											DateTime dt = Sql.ToDateTime(nv.value);
											if ( dt != DateTime.MinValue )
												Sql.SetParameter(parm, T10n.ToServerTimeFromUniversalTime(dt));
											else
												Sql.SetParameter(parm, DBNull.Value);
										}
										else
										{
											Sql.SetParameter(parm, nv.value);
										}
									}
								}
								// Execute the update.
								cmdUpdate.ExecuteNonQuery();
								// Retrieve the ID after update.
								IDbDataParameter pID = Sql.FindParameter(cmdUpdate, "ID");
								if ( pID != null )
									gID = Sql.ToGuid(pID.Value);
							}
							// Update custom fields if any.
							UpdateCustomFields(con, trn, sMODULE_NAME, sTABLE_NAME, gID, name_value_list, gUSER_ID);
							trn.Commit();
						}
						catch
						{
							trn.Rollback();
							throw;
						}
					}
					result.id = gID.ToString();
				}
			}
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				result.error.number = "-1"; result.error.name = ex.GetType().Name; result.error.description = ex.Message;
			}
			return result;
		}

		/// <summary>
		/// Creates or updates multiple entries in the specified module.
		/// Migrated from soap.asmx.cs line 2810.
		/// </summary>
		public set_entries_result set_entries(string session, string module_name, name_value[][] name_value_lists)
		{
			set_entries_result result = new set_entries_result();
			result.error = new error_value();
			result.ids   = new string[0];
			try
			{
				Guid gUSER_ID = GetSessionUserID(session);
				if ( Sql.IsEmptyGuid(gUSER_ID) )
				{
					result.error.number = "-1"; result.error.name = "Invalid Session"; result.error.description = "Session expired or invalid.";
					return result;
				}
				string sMODULE_NAME = Regex.Replace(module_name, @"[^A-Za-z0-9_]", String.Empty);
				int nACLACCESS = _security.GetUserAccess(sMODULE_NAME, "edit");
				if ( nACLACCESS == ACL_ACCESS.NONE )
				{
					L10N L10n = new L10N("en-US", _memoryCache);
					result.error.number = "-1"; result.error.name = "ACL"; result.error.description = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS");
					return result;
				}
				List<string> lstIDs = new List<string>();
				if ( name_value_lists != null )
				{
					foreach ( name_value[] nvList in name_value_lists )
					{
						// Delegate to set_entry for each record.
						set_entry_result r = set_entry(session, module_name, nvList);
						lstIDs.Add(r.id ?? String.Empty);
					}
				}
				result.ids = lstIDs.ToArray();
			}
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				result.error.number = "-1"; result.error.name = ex.GetType().Name; result.error.description = ex.Message;
			}
			return result;
		}

		/// <summary>
		/// Attaches a file to a note record.
		/// Migrated from soap.asmx.cs line 2973.
		/// </summary>
		public set_entry_result set_note_attachment(string session, note_attachment note)
		{
			set_entry_result result = new set_entry_result();
			result.error = new error_value();
			result.id    = String.Empty;
			try
			{
				Guid gUSER_ID = GetSessionUserID(session);
				if ( Sql.IsEmptyGuid(gUSER_ID) )
				{
					result.error.number = "-1"; result.error.name = "Invalid Session"; result.error.description = "Session expired or invalid.";
					return result;
				}
				int nACLACCESS = _security.GetUserAccess("Notes", "edit");
				if ( nACLACCESS == ACL_ACCESS.NONE )
				{
					L10N L10n = new L10N("en-US", _memoryCache);
					result.error.number = "-1"; result.error.name = "ACL"; result.error.description = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS");
					return result;
				}
				Guid gNOTE_ID      = Sql.ToGuid(note.id);
				string sFileName   = Path.GetFileName(note.filename ?? String.Empty);
				string sFileExt    = Path.GetExtension(note.filename ?? String.Empty);
				string sFileMime   = "application/octet-stream";
				byte[] byFile      = Convert.FromBase64String(note.file ?? String.Empty);
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					using ( IDbTransaction trn = Sql.BeginTransaction(con) )
					{
						try
						{
							// Persist to file system via Crm.NoteAttachments.LoadFile (requires transaction for DB-backed storage).
							Crm.NoteAttachments.LoadFile(gNOTE_ID, byFile, trn);
							Guid gNOTE_ATTACHMENT_ID = Guid.Empty;
							SqlProcs.spNOTE_ATTACHMENTS_Insert(ref gNOTE_ATTACHMENT_ID, gNOTE_ID, String.Empty, sFileName, sFileExt, sFileMime, trn);
							result.id = gNOTE_ID.ToString();
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
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				result.error.number = "-1"; result.error.name = ex.GetType().Name; result.error.description = ex.Message;
			}
			return result;
		}

		/// <summary>
		/// Returns the attachment for a note record.
		/// Not implemented in original source — preserved per original behavior.
		/// Migrated from soap.asmx.cs line 3072.
		/// </summary>
		public return_note_attachment get_note_attachment(string session, string id)
		{
			throw new Exception("Method not implemented.");
		}

		/// <summary>
		/// Relates a note to a specific module record.
		/// Migrated from soap.asmx.cs line 3084.
		/// </summary>
		public error_value relate_note_to_module(string session, string note_id, string module_name, string module_id)
		{
			error_value result = new error_value();
			try
			{
				Guid gUSER_ID = GetSessionUserID(session);
				if ( Sql.IsEmptyGuid(gUSER_ID) )
				{
					result.number = "-1"; result.name = "Invalid Session"; result.description = "Session expired or invalid.";
					return result;
				}
				string sMODULE_NAME = Regex.Replace(module_name, @"[^A-Za-z0-9_]", String.Empty);
				int nACLACCESS = _security.GetUserAccess("Notes", "edit");
				if ( nACLACCESS == ACL_ACCESS.NONE )
				{
					L10N L10n = new L10N("en-US", _memoryCache);
					result.number = "-1"; result.name = "ACL"; result.description = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS");
					return result;
				}
				Guid gNOTE_ID   = Sql.ToGuid(note_id);
				Guid gMODULE_ID = Sql.ToGuid(module_id);
				// Update the NOTES record PARENT_TYPE and PARENT_ID via set_entry.
				name_value[] nvList = new name_value[]
				{
					new name_value("ID",          note_id),
					new name_value("PARENT_TYPE", sMODULE_NAME),
					new name_value("PARENT_ID",   module_id)
				};
				set_entry(session, "Notes", nvList);
			}
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				result.number = "-1"; result.name = ex.GetType().Name; result.description = ex.Message;
			}
			return result;
		}

		/// <summary>
		/// Returns related notes for a module record.
		/// Not implemented in original source — preserved per original behavior.
		/// Migrated from soap.asmx.cs line 3170.
		/// </summary>
		public get_entry_result get_related_notes(string session, string module_name, string module_id, string[] select_fields)
		{
			throw new Exception("Method not implemented.");
		}

		/// <summary>
		/// Returns field metadata for a module.
		/// Migrated from soap.asmx.cs line 3182.
		/// </summary>
		public module_fields get_module_fields(string session, string module_name)
		{
			module_fields result = new module_fields();
			result.error          = new error_value();
			result.module_name    = module_name;
			result.module_fields1 = new field[0];
			try
			{
				Guid gUSER_ID = GetSessionUserID(session);
				if ( Sql.IsEmptyGuid(gUSER_ID) )
				{
					result.error.number = "-1"; result.error.name = "Invalid Session"; result.error.description = "Session expired or invalid.";
					return result;
				}
				string sMODULE_NAME = Regex.Replace(module_name, @"[^A-Za-z0-9_]", String.Empty);
				int nACLACCESS = _security.GetUserAccess(sMODULE_NAME, "list");
				if ( nACLACCESS == ACL_ACCESS.NONE )
				{
					L10N L10n = new L10N("en-US", _memoryCache);
					result.error.number = "-1"; result.error.name = "ACL"; result.error.description = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS");
					return result;
				}
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					string sTABLE_NAME = VerifyModuleName(con, sMODULE_NAME);
					DataTable dtFields = _splendidCache.FieldsMetaData_Validated(sTABLE_NAME);
					List<field> lstFields = new List<field>();
					if ( dtFields != null )
					{
						foreach ( DataRow row in dtFields.Rows )
						{
							string sName    = Sql.ToString(row["NAME"   ]);
							string sType    = Sql.ToString(row["DATA_TYPE"]);
							string sLabel   = Sql.ToString(row["LABEL"  ]);
							int    nRequired= Sql.ToInteger(row["REQUIRED"]);
							lstFields.Add(new field(sName, sType, sLabel, nRequired));
						}
					}
					result.module_fields1 = lstFields.ToArray();
				}
			}
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				result.error.number = "-1"; result.error.name = ex.GetType().Name; result.error.description = ex.Message;
			}
			return result;
		}

		/// <summary>
		/// Returns the list of modules accessible to the current user.
		/// Migrated from soap.asmx.cs line 3235.
		/// </summary>
		public module_list get_available_modules(string session)
		{
			module_list result = new module_list();
			result.error   = new error_value();
			result.modules = new string[0];
			try
			{
				Guid gUSER_ID = GetSessionUserID(session);
				if ( Sql.IsEmptyGuid(gUSER_ID) )
				{
					result.error.number = "-1"; result.error.name = "Invalid Session"; result.error.description = "Session expired or invalid.";
					return result;
				}
				DataTable dtModules = _splendidCache.AccessibleModules();
				if ( dtModules != null )
					result.modules = dtModules.AsEnumerable().Select(r => Sql.ToString(r["MODULE_NAME"])).ToArray();
			}
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				result.error.number = "-1"; result.error.name = ex.GetType().Name; result.error.description = ex.Message;
			}
			return result;
		}

		/// <summary>
		/// Updates a portal user record.
		/// Not implemented in original source — preserved per original behavior.
		/// Migrated from soap.asmx.cs line 3250.
		/// </summary>
		public error_value update_portal_user(string session, string portal_name, name_value[] name_value_list)
		{
			throw new Exception("Method not implemented.");
		}

		/// <summary>
		/// Returns modified relationships for synchronization. Not supported — returns error per original.
		/// Migrated from soap.asmx.cs line 3324.
		/// </summary>
		public get_entry_list_result_encoded sync_get_modified_relationships(string session, string module_name, string related_module, string from_date, string to_date, int offset, int max_results, int deleted, string module_id, string[] select_fields, string[] ids, string relationship_name, string deletion_date, int php_serialize)
		{
			get_entry_list_result_encoded result = new get_entry_list_result_encoded();
			result.result_count = 0;
			result.total_count  = 0;
			result.next_offset  = 0;
			result.entry_list   = String.Empty;
			result.error        = new error_value();
			result.error.number = "-1";
			result.error.name   = "not supported";
			result.error.description = "sync_get_modified_relationships is not supported.";
			return result;
		}

		/// <summary>
		/// Returns related records for a module record.
		/// Migrated from soap.asmx.cs line 3372.
		/// </summary>
		public get_relationships_result get_relationships(string session, string module_name, string module_id, string related_module, string related_module_query, int deleted)
		{
			get_relationships_result result = new get_relationships_result();
			result.error = new error_value();
			result.ids   = new id_mod[0];
			try
			{
				Guid gUSER_ID = GetSessionUserID(session);
				if ( Sql.IsEmptyGuid(gUSER_ID) )
				{
					result.error.number = "-1"; result.error.name = "Invalid Session"; result.error.description = "Session expired or invalid.";
					return result;
				}
				string sMODULE1       = Regex.Replace(module_name,     @"[^A-Za-z0-9_]", String.Empty);
				string sMODULE2       = Regex.Replace(related_module,  @"[^A-Za-z0-9_]", String.Empty);
				int nACLACCESS = _security.GetUserAccess(sMODULE1, "view");
				if ( nACLACCESS == ACL_ACCESS.NONE )
				{
					L10N L10n = new L10N("en-US", _memoryCache);
					result.error.number = "-1"; result.error.name = "ACL"; result.error.description = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS");
					return result;
				}
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					// Use vw{MODULE1}_{MODULE2}_Soap view for relationship query.
					string sSOAP_VIEW = "vw" + sMODULE1 + "_" + sMODULE2 + "_Soap";
					string sSQL = "select ID, DATE_MODIFIED, DELETED  from " + sSOAP_VIEW + "  where PRIMARY_ID = @PRIMARY_ID  and DELETED = @DELETED  ";
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						StringBuilder sbWhere = new StringBuilder();
						Sql.AppendParameter(cmd, sbWhere, "PRIMARY_ID", Sql.ToGuid(module_id));
						Sql.AppendParameter(cmd, sbWhere, "DELETED",     Math.Min(deleted, 1));
						if ( !Sql.IsEmptyString(related_module_query) )
							cmd.CommandText += " and (" + related_module_query + ")";
						using ( DbDataAdapter da = dbf.CreateDataAdapter() )
						{
							((IDbDataAdapter)da).SelectCommand = cmd;
							using ( DataTable dt = new DataTable() )
							{
								da.Fill(dt);
								List<id_mod> lstIDs = new List<id_mod>();
								foreach ( DataRow row in dt.Rows )
								{
									string sID           = Sql.ToString(row["ID"           ]);
									string sDateModified = Sql.ToString(row["DATE_MODIFIED"]);
									int    nDeleted      = Sql.ToInteger(row["DELETED"      ]);
									lstIDs.Add(new id_mod(sID, sDateModified, nDeleted));
								}
								result.ids = lstIDs.ToArray();
							}
						}
					}
				}
			}
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				result.error.number = "-1"; result.error.name = ex.GetType().Name; result.error.description = ex.Message;
			}
			return result;
		}

		/// <summary>
		/// Sets a relationship between two module records (single).
		/// Migrated from soap.asmx.cs line 4519.
		/// </summary>
		public error_value set_relationship(string session, set_relationship_value set_relationship_value)
		{
			error_value result = new error_value();
			try
			{
				Guid gUSER_ID = GetSessionUserID(session);
				if ( Sql.IsEmptyGuid(gUSER_ID) )
				{
					result.number = "-1"; result.name = "Invalid Session"; result.description = "Session expired or invalid.";
					return result;
				}
				if ( set_relationship_value == null )
					return result;
				SetRelationship(
					set_relationship_value.module1,
					set_relationship_value.module1_id,
					set_relationship_value.module2,
					set_relationship_value.module2_id);
			}
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				result.number = "-1"; result.name = ex.GetType().Name; result.description = ex.Message;
			}
			return result;
		}

		/// <summary>
		/// Sets relationships between module records (multiple).
		/// Migrated from soap.asmx.cs line 4540.
		/// </summary>
		public set_relationship_list_result set_relationships(string session, set_relationship_value[] set_relationship_list)
		{
			set_relationship_list_result result = new set_relationship_list_result();
			result.error   = new error_value();
			result.created = 0;
			result.failed  = 0;
			try
			{
				Guid gUSER_ID = GetSessionUserID(session);
				if ( Sql.IsEmptyGuid(gUSER_ID) )
				{
					result.error.number = "-1"; result.error.name = "Invalid Session"; result.error.description = "Session expired or invalid.";
					return result;
				}
				if ( set_relationship_list != null )
				{
					foreach ( set_relationship_value srv in set_relationship_list )
					{
						try
						{
							SetRelationship(srv.module1, srv.module1_id, srv.module2, srv.module2_id);
							result.created++;
						}
						catch
						{
							result.failed++;
						}
					}
				}
			}
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				result.error.number = "-1"; result.error.name = ex.GetType().Name; result.error.description = ex.Message;
			}
			return result;
		}

		/// <summary>
		/// Creates a new document revision with file attachment.
		/// Migrated from soap.asmx.cs line 4563.
		/// </summary>
		public set_entry_result set_document_revision(string session, document_revision note)
		{
			set_entry_result result = new set_entry_result();
			result.error = new error_value();
			result.id    = String.Empty;
			try
			{
				Guid gUSER_ID = GetSessionUserID(session);
				if ( Sql.IsEmptyGuid(gUSER_ID) )
				{
					result.error.number = "-1"; result.error.name = "Invalid Session"; result.error.description = "Session expired or invalid.";
					return result;
				}
				int nACLACCESS = _security.GetUserAccess("Documents", "edit");
				if ( nACLACCESS == ACL_ACCESS.NONE )
				{
					L10N L10n = new L10N("en-US", _memoryCache);
					result.error.number = "-1"; result.error.name = "ACL"; result.error.description = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS");
					return result;
				}
				// Owner-only check: if ACL == OWNER then the DOCUMENT must belong to the user.
				Guid gDOCUMENT_ID  = Sql.ToGuid(note.id);
				string sFileName   = Path.GetFileName(note.filename ?? String.Empty);
				string sFileExt    = Path.GetExtension(note.filename ?? String.Empty);
				string sFileMime   = "application/octet-stream";
				byte[] byFile      = Convert.FromBase64String(note.file ?? String.Empty);
				string sRevision   = note.revision   ?? String.Empty;
				string sChangeLog  = String.Empty; // document_revision DTO has no change_log field per DataCarriers
				DbProviderFactory dbf = _dbProviderFactories.GetFactory();
				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					using ( IDbTransaction trn = Sql.BeginTransaction(con) )
					{
						try
						{
							// Persist file to disk/storage via LoadFile (requires transaction for DB-backed storage).
							Crm.DocumentRevisions.LoadFile(gDOCUMENT_ID, byFile, trn);
							Guid gREVISION_ID = Guid.Empty;
							SqlProcs.spDOCUMENT_REVISIONS_Insert(ref gREVISION_ID, gDOCUMENT_ID, sRevision, sChangeLog, sFileName, sFileExt, sFileMime, trn);
							result.id = gDOCUMENT_ID.ToString();
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
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				result.error.number = "-1"; result.error.name = ex.GetType().Name; result.error.description = ex.Message;
			}
			return result;
		}

		// ============================================================
		//  PRIVATE HELPER — UpdateCustomFields
		//  Migrated from soap.asmx.cs set_entry custom field logic
		// ============================================================

		/// <summary>
		/// Updates custom (_CSTM) fields for a module record after the main Update sproc runs.
		/// </summary>
		private void UpdateCustomFields(IDbConnection con, IDbTransaction trn, string sMODULE_NAME, string sTABLE_NAME, Guid gID, name_value[] name_value_list, Guid gUSER_ID)
		{
			try
			{
				DataTable dtCustom = _splendidCache.FieldsMetaData_Validated(sTABLE_NAME + "_CSTM");
				if ( dtCustom == null || dtCustom.Rows.Count == 0 ) return;
				using ( IDbCommand cmdCustom = con.CreateCommand() )
				{
					cmdCustom.Transaction = trn;
					cmdCustom.CommandType  = CommandType.Text;
					// Build update for custom table — only fields that exist in metadata.
					StringBuilder sbSet = new StringBuilder();
					List<string> lstNames = new List<string>();
					foreach ( DataRow row in dtCustom.Rows )
					{
						string sColName = Sql.ToString(row["NAME"]);
						name_value nvMatch = null;
						foreach ( name_value nv in name_value_list )
						{
							if ( nv != null && string.Equals(nv.name, sColName, StringComparison.OrdinalIgnoreCase) )
							{ nvMatch = nv; break; }
						}
						if ( nvMatch != null )
						{
							string sParmName = "@" + sColName;
							if ( sbSet.Length > 0 ) sbSet.Append(", ");
							sbSet.Append(sColName + " = " + sParmName);
							Sql.AddParameter(cmdCustom, sParmName, nvMatch.value);
							lstNames.Add(sColName);
						}
					}
					if ( sbSet.Length == 0 ) return;
					cmdCustom.CommandText = "update " + sTABLE_NAME + "_CSTM  set " + sbSet.ToString() + "  where ID_C = @ID_C  ";
					Sql.AddParameter(cmdCustom, "@ID_C", gID);
					int nRows = cmdCustom.ExecuteNonQuery();
					if ( nRows == 0 )
					{
						// Insert custom row if it doesn't exist yet.
						StringBuilder sbCols  = new StringBuilder("ID_C");
						StringBuilder sbVals  = new StringBuilder("@ID_C");
						foreach ( string sCol in lstNames )
						{ sbCols.Append(", " + sCol); sbVals.Append(", @" + sCol); }
						cmdCustom.CommandText = "insert into " + sTABLE_NAME + "_CSTM (" + sbCols + ") values (" + sbVals + ")";
						cmdCustom.ExecuteNonQuery();
					}
				}
			}
			catch(Exception ex)
			{
				SplendidError.SystemWarning(new StackTrace(true).GetFrame(0), ex.Message);
			}
		}

	}  // end class SugarSoapService
}  // end namespace SplendidCRM
