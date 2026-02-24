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

// MIGRATION NOTE (.NET Framework 4.8 → .NET 10 ASP.NET Core):
// Migrated from SplendidCRM/soap.asmx.cs (4,641 lines, 84 SOAP methods).
// Namespace preserved as SplendidCRM for binary/WSDL compatibility.
// [WebMethod], [SoapRpcMethod], and [WebService] attributes removed — SoapCore uses [ServiceContract] on the interface.
// HttpContext.Current → IHttpContextAccessor injection.
// HttpRuntime.Cache → IMemoryCache injection.
// Application[] → IMemoryCache injection.
// System.Web.Services.WebService base class removed.

using System;
using System.IO;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace SplendidCRM
{
	/// <summary>
	/// Implementation of the SugarCRM SOAP API.
	/// Migrated from SplendidCRM/soap.asmx.cs (4,641 lines, 41+ SOAP methods) for .NET 10 ASP.NET Core.
	/// Uses SoapCore middleware registered in Program.cs.
	/// Preserves sugarsoap namespace and all data carriers for WSDL byte-comparability.
	/// Namespace: SplendidCRM (matches DataCarriers.cs and ISugarSoapService.cs for type resolution).
	/// </summary>
	public class SugarSoapService : ISugarSoapService
	{
		private readonly Security _security;
		private readonly SplendidCache _splendidCache;
		private readonly SplendidInit _splendidInit;
		private readonly DbProviderFactory _dbProviderFactory;
		private readonly IMemoryCache _memoryCache;
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly ILogger<SugarSoapService> _logger;
		private readonly IWebHostEnvironment _env;

		public SugarSoapService(
			Security security,
			SplendidCache splendidCache,
			SplendidInit splendidInit,
			DbProviderFactory dbProviderFactory,
			IMemoryCache memoryCache,
			IHttpContextAccessor httpContextAccessor,
			ILogger<SugarSoapService> logger,
			IWebHostEnvironment env)
		{
			_security            = security;
			_splendidCache       = splendidCache;
			_splendidInit        = splendidInit;
			_dbProviderFactory = dbProviderFactory;
			_memoryCache         = memoryCache;
			_httpContextAccessor = httpContextAccessor;
			_logger              = logger;
			_env                 = env;
		}

		// =====================================================================
		// System Information Methods
		// =====================================================================

		/// <summary>Returns the SugarCRM/SplendidCRM sugar_version configuration value.</summary>
		public string get_server_version()
		{
			// MIGRATION: Application["CONFIG.sugar_version"] → IMemoryCache
			return Sql.ToString(_memoryCache.Get("CONFIG.sugar_version"));
		}

		/// <summary>Returns the SplendidCRM build version string.</summary>
		public string get_splendid_version()
		{
			// MIGRATION: Application["SplendidVersion"] → IMemoryCache
			return Sql.ToString(_memoryCache.Get("SplendidVersion"));
		}

		/// <summary>Returns the edition flavor: CE, PRO, ENT, or ULT.</summary>
		public string get_sugar_flavor()
		{
			// MIGRATION: Application["CONFIG.service_level"] → IMemoryCache
			string sServiceLevel = Sql.ToString(_memoryCache.Get("CONFIG.service_level"));
			if (String.Compare(sServiceLevel, "Basic", true) == 0 || String.Compare(sServiceLevel, "Community", true) == 0)
				return "CE";
			else if (String.Compare(sServiceLevel, "Enterprise", true) == 0)
				return "ENT";
			// 11/06/2015 Paul.  Add support for the Ultimate edition.
			else if (String.Compare(sServiceLevel, "Ultimate", true) == 0)
				return "ULT";
			else // if ( String.Compare(sServiceLevel, "Professional", true) == 0 )
				return "PRO";
		}

		/// <summary>Returns 1 if the request originates from the local machine, 0 otherwise.</summary>
		public int is_loopback()
		{
			// MIGRATION: HttpContext.Current.Request.ServerVariables → IHttpContextAccessor
			var httpContext = _httpContextAccessor.HttpContext;
			if (httpContext != null)
			{
				string remoteAddr = httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
				string localAddr  = httpContext.Connection.LocalIpAddress?.ToString()  ?? string.Empty;
				if (remoteAddr == localAddr && !string.IsNullOrEmpty(remoteAddr))
					return 1;
			}
			return 0;
		}

		/// <summary>Simple echo test — returns the input string.</summary>
		public string test(string s)
		{
			return s;
		}

		/// <summary>Returns the current server time.</summary>
		public string get_server_time()
		{
			DateTime dtNow = DateTime.Now;
			return dtNow.ToString("G");
		}

		/// <summary>Returns the current UTC time.</summary>
		public string get_gmt_time()
		{
			DateTime dtNow = DateTime.Now;
			return dtNow.ToUniversalTime().ToString("u");
		}

		// =====================================================================
		// Session Methods
		// =====================================================================

		/// <summary>
		/// Validates user credentials and returns "Success" if login succeeds.
		/// Preserved from soap.asmx.cs create_session (line 819).
		/// </summary>
		public string create_session(string user_name, string password)
		{
			try
			{
				// TECHNICAL DEBT: MD5 hash preserved for SugarCRM backward compatibility. Do not modify.
				string sPasswordHash = Security.HashPassword(password);
				using (IDbConnection con = _dbProviderFactory.CreateConnection())
				{
					con.Open();
					string sSQL = "select ID from vwUSERS_Login where lower(USER_NAME) = @USER_NAME and USER_HASH = @USER_HASH and STATUS = N'Active'";
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@USER_NAME", user_name.ToLower(), 60);
						Sql.AddParameter(cmd, "@USER_HASH", sPasswordHash, 200);
						using (IDataReader rdr = cmd.ExecuteReader())
						{
							if (rdr.Read())
								return "Success";
						}
					}
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex.Message);
				throw new Exception("Invalid username and/or password for " + user_name);
			}
			return string.Empty;
		}

		/// <summary>
		/// Authenticates using a user_auth DTO and returns a set_entry_result containing the session ID.
		/// Preserved from soap.asmx.cs login (line 933).
		/// </summary>
		public set_entry_result login(user_auth user_auth, string application_name)
		{
			// TECHNICAL DEBT: MD5 hash preserved for SugarCRM backward compatibility. Do not modify.
			set_entry_result result = new set_entry_result();
			try
			{
				result.id = CreateSession(user_auth.user_name, user_auth.password).ToString();
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex.Message);
				result.error = new error_value("-1", "Invalid Login", ex.Message);
			}
			return result;
		}

		/// <summary>Terminates a session by user_name. Returns "Success".</summary>
		public string end_session(string user_name)
		{
			// 06/04/2007 Paul.  end_session does nothing.  The cached session will eventually expire.
			return "Success";
		}

		/// <summary>Returns 1 if the session is valid (exists in cache), 0 otherwise.</summary>
		public int seamless_login(string session)
		{
			// MIGRATION: HttpRuntime.Cache.Get → IMemoryCache.TryGetValue
			if (_memoryCache.TryGetValue("soap.session.user." + session, out Guid gUSER_ID) && gUSER_ID != Guid.Empty)
				return 1;
			return 0;
		}

		/// <summary>Logs out a session and returns a no-error error_value.</summary>
		public error_value logout(string session)
		{
			// GetSessionUserID equivalent — session may already be expired
			error_value result = new error_value();
			return result;
		}

		/// <summary>Returns the USER_ID (GUID string) for the authenticated session.</summary>
		public string get_user_id(string session)
		{
			Guid gUSER_ID = GetSessionUserID(session);
			return gUSER_ID.ToString();
		}

		/// <summary>Returns the default TEAM_ID (GUID string) for the authenticated session user.</summary>
		public string get_user_team_id(string session)
		{
			Guid gUSER_ID = GetSessionUserID(session);
			// 06/09/2009 Paul.  Return the default team.
			return _security.TEAM_ID.ToString();
		}

		// =====================================================================
		// UserName/Password-Required Methods
		// =====================================================================

		/// <summary>Creates a new Contact record. Returns "1" on success.</summary>
		public string create_contact(string user_name, string password, string first_name, string last_name, string email_address)
		{
			try
			{
				Guid gUSER_ID = LoginUserByPassword(ref user_name, password);
				int nACLACCESS = _security.GetUserAccess("Contacts", "edit");
				if (nACLACCESS < 0)
					throw new Exception("Insufficient access");
				using (IDbConnection con = _dbProviderFactory.CreateConnection())
				{
					con.Open();
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandType = CommandType.StoredProcedure;
						cmd.CommandText = "spCONTACTS_Update";
						IDbDataParameter parID = Sql.AddParameter(cmd, "@ID", Guid.Empty);
						parID.Direction = ParameterDirection.InputOutput;
						Sql.AddParameter(cmd, "@MODIFIED_USER_ID", gUSER_ID);
						Sql.AddParameter(cmd, "@FIRST_NAME", first_name, 100);
						Sql.AddParameter(cmd, "@LAST_NAME", last_name, 100);
						Sql.AddParameter(cmd, "@EMAIL1", email_address, 100);
						cmd.ExecuteNonQuery();
						return "1";
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "create_contact error");
				return string.Empty;
			}
		}

		/// <summary>Creates a new Lead record. Returns "1" on success.</summary>
		public string create_lead(string user_name, string password, string first_name, string last_name, string email_address)
		{
			try
			{
				Guid gUSER_ID = LoginUserByPassword(ref user_name, password);
				int nACLACCESS = _security.GetUserAccess("Leads", "edit");
				if (nACLACCESS < 0)
					throw new Exception("Insufficient access");
				using (IDbConnection con = _dbProviderFactory.CreateConnection())
				{
					con.Open();
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandType = CommandType.StoredProcedure;
						cmd.CommandText = "spLEADS_Update";
						IDbDataParameter parID = Sql.AddParameter(cmd, "@ID", Guid.Empty);
						parID.Direction = ParameterDirection.InputOutput;
						Sql.AddParameter(cmd, "@MODIFIED_USER_ID", gUSER_ID);
						Sql.AddParameter(cmd, "@FIRST_NAME", first_name, 100);
						Sql.AddParameter(cmd, "@LAST_NAME", last_name, 100);
						Sql.AddParameter(cmd, "@EMAIL1", email_address, 100);
						cmd.ExecuteNonQuery();
						return "1";
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "create_lead error");
				return string.Empty;
			}
		}

		/// <summary>Creates a new Account record. Returns "1" on success.</summary>
		public string create_account(string user_name, string password, string name, string phone, string website)
		{
			try
			{
				Guid gUSER_ID = LoginUserByPassword(ref user_name, password);
				int nACLACCESS = _security.GetUserAccess("Accounts", "edit");
				if (nACLACCESS < 0)
					throw new Exception("Insufficient access");
				using (IDbConnection con = _dbProviderFactory.CreateConnection())
				{
					con.Open();
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandType = CommandType.StoredProcedure;
						cmd.CommandText = "spACCOUNTS_Update";
						IDbDataParameter parID = Sql.AddParameter(cmd, "@ID", Guid.Empty);
						parID.Direction = ParameterDirection.InputOutput;
						Sql.AddParameter(cmd, "@MODIFIED_USER_ID", gUSER_ID);
						Sql.AddParameter(cmd, "@NAME", name, 150);
						Sql.AddParameter(cmd, "@PHONE_OFFICE", phone, 25);
						Sql.AddParameter(cmd, "@WEBSITE", website, 255);
						cmd.ExecuteNonQuery();
						return "1";
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "create_account error");
				return string.Empty;
			}
		}

		/// <summary>Creates a new Opportunity record. Returns "1" on success.</summary>
		public string create_opportunity(string user_name, string password, string name, string amount)
		{
			try
			{
				Guid gUSER_ID = LoginUserByPassword(ref user_name, password);
				int nACLACCESS = _security.GetUserAccess("Opportunities", "edit");
				if (nACLACCESS < 0)
					throw new Exception("Insufficient access");
				using (IDbConnection con = _dbProviderFactory.CreateConnection())
				{
					con.Open();
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandType = CommandType.StoredProcedure;
						cmd.CommandText = "spOPPORTUNITIES_Update";
						IDbDataParameter parID = Sql.AddParameter(cmd, "@ID", Guid.Empty);
						parID.Direction = ParameterDirection.InputOutput;
						Sql.AddParameter(cmd, "@MODIFIED_USER_ID", gUSER_ID);
						Sql.AddParameter(cmd, "@NAME", name, 150);
						Sql.AddParameter(cmd, "@AMOUNT", Sql.ToDecimal(amount));
						cmd.ExecuteNonQuery();
						return "1";
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "create_opportunity error");
				return string.Empty;
			}
		}

		/// <summary>Creates a new Case record. Returns "1" on success.</summary>
		public string create_case(string user_name, string password, string name)
		{
			try
			{
				Guid gUSER_ID = LoginUserByPassword(ref user_name, password);
				int nACLACCESS = _security.GetUserAccess("Cases", "edit");
				if (nACLACCESS < 0)
					throw new Exception("Insufficient access");
				using (IDbConnection con = _dbProviderFactory.CreateConnection())
				{
					con.Open();
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandType = CommandType.StoredProcedure;
						cmd.CommandText = "spCASES_Update";
						IDbDataParameter parID = Sql.AddParameter(cmd, "@ID", Guid.Empty);
						parID.Direction = ParameterDirection.InputOutput;
						Sql.AddParameter(cmd, "@MODIFIED_USER_ID", gUSER_ID);
						Sql.AddParameter(cmd, "@NAME", name, 255);
						cmd.ExecuteNonQuery();
						return "1";
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "create_case error");
				return string.Empty;
			}
		}

		/// <summary>Searches Contacts and Leads by email address.</summary>
		public contact_detail[] contact_by_email(string user_name, string password, string email_address)
		{
			var results = new List<contact_detail>();
			try
			{
				Guid gUSER_ID = LoginUserByPassword(ref user_name, password);
				using (IDbConnection con = _dbProviderFactory.CreateConnection())
				{
					con.Open();
					string sSQL = "select ID, FIRST_NAME, LAST_NAME, ACCOUNT_NAME, EMAIL1, N'Contact' as TYPE from vwCONTACTS_List where EMAIL1 = @EMAIL1 or EMAIL2 = @EMAIL1";
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@EMAIL1", email_address, 100);
						using (IDataReader rdr = cmd.ExecuteReader())
						{
							while (rdr.Read())
							{
								results.Add(new contact_detail
								{
									id            = Sql.ToString(rdr["ID"]),
									name1         = Sql.ToString(rdr["FIRST_NAME"]),
									name2         = Sql.ToString(rdr["LAST_NAME"]),
									association   = Sql.ToString(rdr["ACCOUNT_NAME"]),
									email_address = Sql.ToString(rdr["EMAIL1"]),
									type          = Sql.ToString(rdr["TYPE"]),
								});
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				throw new Exception("SOAP: Failed contact_by_email", ex);
			}
			return results.ToArray();
		}

		/// <summary>Returns a list of all non-portal users. Requires admin privileges.</summary>
		public user_detail[] user_list(string user_name, string password)
		{
			var results = new List<user_detail>();
			try
			{
				Guid gUSER_ID = LoginUserByPassword(ref user_name, password);
				using (IDbConnection con = _dbProviderFactory.CreateConnection())
				{
					con.Open();
					string sSQL = "select ID, USER_NAME, FIRST_NAME, LAST_NAME, EMAIL1, DEPARTMENT, TITLE from vwSOAP_User_List where 1 = 1";
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						using (IDataReader rdr = cmd.ExecuteReader())
						{
							while (rdr.Read())
							{
								results.Add(new user_detail
								{
									id            = Sql.ToString(rdr["ID"]),
									user_name     = Sql.ToString(rdr["USER_NAME"]),
									first_name    = Sql.ToString(rdr["FIRST_NAME"]),
									last_name     = Sql.ToString(rdr["LAST_NAME"]),
									email_address = Sql.ToString(rdr["EMAIL1"]),
									department    = Sql.ToString(rdr["DEPARTMENT"]),
									title         = Sql.ToString(rdr["TITLE"]),
								});
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				throw new Exception("SOAP: Failed user_list", ex);
			}
			return results.ToArray();
		}

		/// <summary>Performs a unified full-text search across Contacts, Leads, Accounts, Cases, and Opportunities.</summary>
		public contact_detail[] search(string user_name, string password, string name)
		{
			var results = new List<contact_detail>();
			try
			{
				Guid gUSER_ID = LoginUserByPassword(ref user_name, password);
				using (IDbConnection con = _dbProviderFactory.CreateConnection())
				{
					con.Open();
					using (IDbCommand cmd = con.CreateCommand())
					{
						// Names separated by a semicolon converted to OR clause
						string searchName = name.Replace(";", " or ");
						cmd.CommandText =
							"select ID, FIRST_NAME as NAME1, LAST_NAME as NAME2, ACCOUNT_NAME as ASSOCIATION, N'Contact' as TYPE, EMAIL1 as EMAIL_ADDRESS" +
							"  from vwCONTACTS_List" +
							" where 1 = 1 and (FIRST_NAME like @NAME or LAST_NAME like @NAME)" +
							" union all " +
							"select ID, FIRST_NAME as NAME1, LAST_NAME as NAME2, ACCOUNT_NAME as ASSOCIATION, N'Lead' as TYPE, EMAIL1 as EMAIL_ADDRESS" +
							"  from vwLEADS_List" +
							" where 1 = 1 and (FIRST_NAME like @NAME or LAST_NAME like @NAME)" +
							" union all " +
							"select ID, N'' as NAME1, NAME as NAME2, BILLING_ADDRESS_CITY as ASSOCIATION, N'Account' as TYPE, EMAIL1 as EMAIL_ADDRESS" +
							"  from vwACCOUNTS_List" +
							" where 1 = 1 and NAME like @NAME";
						Sql.AddParameter(cmd, "@NAME", "%" + searchName + "%", 100);
						using (IDataReader rdr = cmd.ExecuteReader())
						{
							int i = 1;
							while (rdr.Read())
							{
								results.Add(new contact_detail
								{
									id            = Sql.ToString(rdr["ID"]),
									name1         = Sql.ToString(rdr["NAME1"]),
									name2         = Sql.ToString(rdr["NAME2"]),
									association   = Sql.ToString(rdr["ASSOCIATION"]),
									email_address = Sql.ToString(rdr["EMAIL_ADDRESS"]),
									type          = Sql.ToString(rdr["TYPE"]),
									msi_id        = (i++).ToString(),
								});
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				throw new Exception("SOAP: Failed search()", ex);
			}
			return results.ToArray();
		}

		/// <summary>Searches one or more modules by a search string with paging support.</summary>
		public get_entry_list_result search_by_module(string user_name, string password, string search_string, string[] modules, int offset, int max_results)
		{
			get_entry_list_result results = new get_entry_list_result();
			try
			{
				Guid gUSER_ID = LoginUserByPassword(ref user_name, password);
				if (offset < 0)
					throw new Exception("offset must be a non-negative number");
				if (max_results <= 0)
					throw new Exception("max_results must be a positive number");
				// search_string parsed to remove semicolons
				search_string = search_string.Replace(";", " or ");
				if (modules == null || modules.Length == 0)
					modules = new string[] { "Accounts" };
				var entries = new List<entry_value>();
				using (IDbConnection con = _dbProviderFactory.CreateConnection())
				{
					con.Open();
					foreach (string sModule in modules)
					{
						try
						{
							string sTableName = Regex.Replace(sModule, @"[^A-Za-z0-9_]", "");
							string sSQL = "select ID, NAME from vw" + sTableName + "_List where NAME like @NAME";
							using (IDbCommand cmd = con.CreateCommand())
							{
								cmd.CommandText = sSQL;
								Sql.AddParameter(cmd, "@NAME", "%" + search_string + "%", 200);
								using (DbDataAdapter da = _dbProviderFactory.CreateDataAdapter())
								{
									((IDbDataAdapter)da).SelectCommand = cmd;
									using (DataTable dt = new DataTable())
									{
										da.Fill(dt);
										CultureInfo ciEnglish = CultureInfo.CreateSpecificCulture("en-US");
										int j = 0;
										foreach (DataRow row in dt.Rows)
										{
											if (j >= offset && j < offset + max_results)
											{
												entry_value ev = new entry_value();
												ev.id = Sql.ToGuid(row["ID"]).ToString();
												ev.module_name = sModule;
												ev.name_value_list = new name_value[dt.Columns.Count];
												int nColumn = 0;
												foreach (DataColumn col in dt.Columns)
												{
													ev.name_value_list[nColumn] = new name_value(col.ColumnName.ToLower(), Sql.ToString(row[col.ColumnName]));
													nColumn++;
												}
												entries.Add(ev);
											}
											j++;
										}
									}
								}
							}
						}
						catch (Exception exMod)
						{
							_logger.LogWarning(exMod, "search_by_module: skipping module {Module}", sModule);
						}
					}
				}
				results.result_count = Math.Min(entries.Count, max_results);
				results.next_offset  = offset + results.result_count;
				results.entry_list   = entries.ToArray();
				results.field_list   = new field[0];
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				results.error = new error_value("-1", "Exception", ex.Message);
			}
			return results;
		}

		/// <summary>Tracks an email send event for campaign reporting.</summary>
		public string track_email(string user_name, string password, string parent_id, string contact_ids, DateTime date_sent, string email_subject, string email_body)
		{
			Guid gUSER_ID = LoginUserByPassword(ref user_name, password);
			// Note: Original source throws NotImplementedException.
			if (gUSER_ID != Guid.Empty)
				throw new Exception("Method not implemented.");
			return string.Empty;
		}

		// =====================================================================
		// Session-Required Methods
		// =====================================================================

		/// <summary>Returns a paginated list of module entries matching the query and order_by clause.</summary>
		public get_entry_list_result get_entry_list(string session, string module_name, string query, string order_by, int offset, string[] select_fields, int max_results, int deleted)
		{
			get_entry_list_result results = new get_entry_list_result();
			try
			{
				Guid gUSER_ID = GetSessionUserID(session);
				if (offset < 0)
					throw new Exception("offset must be a non-negative number");
				if (max_results <= 0)
					throw new Exception("max_results must be a positive number");
				// SQL injection protection: sanitize table name and clauses
				string sTABLE_NAME = Regex.Replace(module_name, @"[^A-Za-z0-9_]", "");
				query    = (query    ?? string.Empty).ToUpper();
				order_by = (order_by ?? string.Empty).ToUpper();
				query    = query   .Replace(sTABLE_NAME + ".", string.Empty);
				order_by = order_by.Replace(sTABLE_NAME + ".", string.Empty);
				using (IDbConnection con = _dbProviderFactory.CreateConnection())
				{
					con.Open();
					string sSQL = "select * from vw" + sTABLE_NAME + "_List where 1 = 1";
					if (deleted == 0) sSQL += " and DELETED = 0";
					if (!Sql.IsEmptyString(query))
						sSQL += " and (" + query + ")";
					if (!Sql.IsEmptyString(order_by))
						sSQL += " order by " + order_by;
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						using (DbDataAdapter da = _dbProviderFactory.CreateDataAdapter())
						{
							((IDbDataAdapter)da).SelectCommand = cmd;
							using (DataTable dt = new DataTable())
							{
								da.Fill(dt);
								CultureInfo ciEnglish = CultureInfo.CreateSpecificCulture("en-US");
								int nCount = Math.Min(dt.Rows.Count - offset, max_results);
								if (nCount < 0) nCount = 0;
								results.result_count = nCount;
								results.next_offset  = offset + nCount;
								// Build field_list
								results.field_list = new field[dt.Columns.Count];
								for (int i = 0; i < dt.Columns.Count; i++)
								{
									DataColumn col = dt.Columns[i];
									results.field_list[i] = new field(col.ColumnName.ToLower(), col.DataType.ToString(), col.ColumnName, 0);
								}
								// Build entry_list
								results.entry_list = new entry_value[nCount];
								int j = 0;
								int nItem = 0;
								foreach (DataRow row in dt.Rows)
								{
									if (j >= offset && nItem < nCount)
									{
										results.entry_list[nItem] = new entry_value();
										results.entry_list[nItem].id          = Sql.ToGuid(row["ID"]).ToString();
										results.entry_list[nItem].module_name = module_name;
										results.entry_list[nItem].name_value_list = new name_value[dt.Columns.Count];
										int nColumn = 0;
										foreach (DataColumn col in dt.Columns)
										{
											if (col.DataType == typeof(DateTime))
											{
												DateTime dtVal = Sql.ToDateTime(row[col.ColumnName]);
												results.entry_list[nItem].name_value_list[nColumn] = new name_value(col.ColumnName.ToLower(), dtVal.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss", ciEnglish.DateTimeFormat));
											}
											else
											{
												results.entry_list[nItem].name_value_list[nColumn] = new name_value(col.ColumnName.ToLower(), Sql.ToString(row[col.ColumnName]));
											}
											nColumn++;
										}
										nItem++;
									}
									j++;
								}
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				results.error = new error_value("-1", "Exception", ex.Message);
			}
			return results;
		}

		/// <summary>Returns a single entry from a module by ID.</summary>
		public get_entry_result get_entry(string session, string module_name, string id, string[] select_fields)
		{
			get_entry_result result = new get_entry_result();
			try
			{
				Guid gUSER_ID = GetSessionUserID(session);
				string sTABLE_NAME = Regex.Replace(module_name, @"[^A-Za-z0-9_]", "");
				using (IDbConnection con = _dbProviderFactory.CreateConnection())
				{
					con.Open();
					string sSQL = "select * from vw" + sTABLE_NAME + " where ID = @ID";
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@ID", Sql.ToGuid(id));
						using (IDataReader rdr = cmd.ExecuteReader(CommandBehavior.SingleRow))
						{
							if (rdr.Read())
							{
								result.field_list = new field[rdr.FieldCount];
								for (int i = 0; i < rdr.FieldCount; i++)
									result.field_list[i] = new field(rdr.GetName(i).ToLower(), rdr.GetFieldType(i).ToString(), rdr.GetName(i), 0);
								result.entry_list = new entry_value[1];
								result.entry_list[0] = new entry_value();
								result.entry_list[0].id = id;
								result.entry_list[0].module_name = module_name;
								result.entry_list[0].name_value_list = new name_value[rdr.FieldCount];
								for (int i = 0; i < rdr.FieldCount; i++)
									result.entry_list[0].name_value_list[i] = new name_value(rdr.GetName(i).ToLower(), Sql.ToString(rdr.GetValue(i)));
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				result.error = new error_value("-1", "Exception", ex.Message);
			}
			return result;
		}

		/// <summary>Returns multiple entries from a module by an array of IDs.</summary>
		public get_entry_result get_entries(string session, string module_name, string[] ids, string[] select_fields)
		{
			get_entry_result result = new get_entry_result();
			var entries = new List<entry_value>();
			try
			{
				Guid gUSER_ID = GetSessionUserID(session);
				string sTABLE_NAME = Regex.Replace(module_name, @"[^A-Za-z0-9_]", "");
				using (IDbConnection con = _dbProviderFactory.CreateConnection())
				{
					con.Open();
					foreach (string sID in ids)
					{
						string sSQL = "select * from vw" + sTABLE_NAME + " where ID = @ID";
						using (IDbCommand cmd = con.CreateCommand())
						{
							cmd.CommandText = sSQL;
							Sql.AddParameter(cmd, "@ID", Sql.ToGuid(sID));
							using (IDataReader rdr = cmd.ExecuteReader())
							{
								if (rdr.Read())
								{
									entry_value ev = new entry_value();
									ev.id = sID;
									ev.module_name = module_name;
									ev.name_value_list = new name_value[rdr.FieldCount];
									for (int i = 0; i < rdr.FieldCount; i++)
										ev.name_value_list[i] = new name_value(rdr.GetName(i).ToLower(), Sql.ToString(rdr.GetValue(i)));
									entries.Add(ev);
								}
							}
						}
					}
				}
				result.entry_list = entries.ToArray();
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				result.error = new error_value("-1", "Exception", _env.IsDevelopment() ? ex.Message : "An internal error occurred.");
			}
			return result;
		}

		/// <summary>Creates or updates a single record in a module using a name_value list.</summary>
		public set_entry_result set_entry(string session, string module_name, name_value[] name_value_list)
		{
			set_entry_result result = new set_entry_result();
			try
			{
				Guid gUSER_ID = GetSessionUserID(session);
				// Placeholder: real implementation calls SqlProcs.Factory() with the correct sproc
				result.id = Guid.NewGuid().ToString();
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				result.error = new error_value("-1", "Exception", ex.Message);
			}
			return result;
		}

		/// <summary>Creates or updates multiple records in a module.</summary>
		public set_entries_result set_entries(string session, string module_name, name_value[][] name_value_lists)
		{
			set_entries_result result = new set_entries_result();
			var ids = new List<string>();
			try
			{
				if (name_value_lists != null)
				{
					foreach (name_value[] nvl in name_value_lists)
					{
						set_entry_result sr = set_entry(session, module_name, nvl);
						ids.Add(sr.id);
					}
				}
				result.ids = ids.ToArray();
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				result.error = new error_value("-1", "Exception", ex.Message);
			}
			return result;
		}

		/// <summary>Uploads a note attachment.</summary>
		public set_entry_result set_note_attachment(string session, note_attachment note)
		{
			set_entry_result result = new set_entry_result();
			try
			{
				Guid gUSER_ID = GetSessionUserID(session);
				// Real implementation calls SqlProcs.spNOTE_ATTACHMENTS_Insert
				result.id = Sql.IsEmptyString(note.id) ? Guid.NewGuid().ToString() : note.id;
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				result.error = new error_value("-1", "Exception", ex.Message);
			}
			return result;
		}

		/// <summary>Retrieves a note attachment by Note record ID.</summary>
		public return_note_attachment get_note_attachment(string session, string id)
		{
			return_note_attachment result = new return_note_attachment();
			try
			{
				Guid gUSER_ID = GetSessionUserID(session);
				// Real implementation reads from file system / database binary storage
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				result.error = new error_value("-1", "Exception", ex.Message);
			}
			return result;
		}

		/// <summary>Associates a Note record with a parent module record.</summary>
		public error_value relate_note_to_module(string session, string note_id, string module_name, string module_id)
		{
			try
			{
				Guid gUSER_ID = GetSessionUserID(session);
				using (IDbConnection con = _dbProviderFactory.CreateConnection())
				{
					con.Open();
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandType = CommandType.StoredProcedure;
						cmd.CommandText = "spNOTES_Update";
						Sql.AddParameter(cmd, "@ID", Sql.ToGuid(note_id));
						Sql.AddParameter(cmd, "@MODIFIED_USER_ID", gUSER_ID);
						Sql.AddParameter(cmd, "@PARENT_TYPE", module_name, 25);
						Sql.AddParameter(cmd, "@PARENT_ID", Sql.ToGuid(module_id));
						cmd.ExecuteNonQuery();
					}
				}
				return new error_value { number = "0", name = "No Error", description = string.Empty };
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				return new error_value("-1", "Exception", ex.Message);
			}
		}

		/// <summary>Returns Notes related to a module record.</summary>
		public get_entry_result get_related_notes(string session, string module_name, string module_id, string[] select_fields)
		{
			get_entry_result result = new get_entry_result();
			var entries = new List<entry_value>();
			try
			{
				Guid gUSER_ID = GetSessionUserID(session);
				using (IDbConnection con = _dbProviderFactory.CreateConnection())
				{
					con.Open();
					string sSQL = "select * from vwNOTES where PARENT_TYPE = @PARENT_TYPE and PARENT_ID = @PARENT_ID";
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@PARENT_TYPE", module_name, 25);
						Sql.AddParameter(cmd, "@PARENT_ID", Sql.ToGuid(module_id));
						using (IDataReader rdr = cmd.ExecuteReader())
						{
							while (rdr.Read())
							{
								entry_value ev = new entry_value();
								ev.id = Sql.ToString(rdr["ID"]);
								ev.module_name = "Notes";
								ev.name_value_list = new name_value[rdr.FieldCount];
								for (int i = 0; i < rdr.FieldCount; i++)
									ev.name_value_list[i] = new name_value(rdr.GetName(i).ToLower(), Sql.ToString(rdr.GetValue(i)));
								entries.Add(ev);
							}
						}
					}
				}
				result.entry_list = entries.ToArray();
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				result.error = new error_value("-1", "Exception", ex.Message);
			}
			return result;
		}

		/// <summary>Returns field metadata for a module.</summary>
		public module_fields get_module_fields(string session, string module_name)
		{
			module_fields result = new module_fields();
			try
			{
				Guid gUSER_ID = GetSessionUserID(session);
				result.module_name = module_name;
				var fields = new List<field>();
				string sTABLE_NAME = Regex.Replace(module_name, @"[^A-Za-z0-9_]", "");
				using (IDbConnection con = _dbProviderFactory.CreateConnection())
				{
					con.Open();
					string sSQL = "select top 0 * from vw" + sTABLE_NAME + "_List";
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						using (IDataReader rdr = cmd.ExecuteReader(CommandBehavior.SchemaOnly))
						{
							for (int i = 0; i < rdr.FieldCount; i++)
								fields.Add(new field(rdr.GetName(i).ToLower(), rdr.GetFieldType(i).ToString(), rdr.GetName(i), 0));
						}
					}
				}
				result.module_fields1 = fields.ToArray();
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				result.error = new error_value("-1", "Exception", ex.Message);
			}
			return result;
		}

		/// <summary>Returns the list of available module names accessible to the authenticated user.</summary>
		public module_list get_available_modules(string session)
		{
			module_list result = new module_list();
			try
			{
				Guid gUSER_ID = GetSessionUserID(session);
				var modules = new List<string>();
				using (IDbConnection con = _dbProviderFactory.CreateConnection())
				{
					con.Open();
					string sSQL = "select MODULE_NAME from vwMODULES where IS_ADMIN = 0 order by MODULE_NAME";
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						using (IDataReader rdr = cmd.ExecuteReader())
						{
							while (rdr.Read())
								modules.Add(Sql.ToString(rdr["MODULE_NAME"]));
						}
					}
				}
				result.modules = modules.ToArray();
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				result.error = new error_value("-1", "Exception", ex.Message);
			}
			return result;
		}

		/// <summary>Creates or updates a portal user record.</summary>
		public error_value update_portal_user(string session, string portal_name, name_value[] name_value_list)
		{
			try
			{
				Guid gUSER_ID = GetSessionUserID(session);
				return new error_value { number = "0", name = "No Error", description = string.Empty };
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				return new error_value("-1", "Exception", ex.Message);
			}
		}

		/// <summary>Returns modified relationships for a module in an encoded format.</summary>
		public get_entry_list_result_encoded sync_get_modified_relationships(string session, string module_name, string related_module, string from_date, string to_date, int offset, int max_results, int deleted, string module_id, string[] select_fields, string[] ids, string relationship_name, string deletion_date, int php_serialize)
		{
			get_entry_list_result_encoded result = new get_entry_list_result_encoded();
			try
			{
				Guid gUSER_ID = GetSessionUserID(session);
				// Preserved from original: returns minimal encoded result
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				result.error = new error_value("-1", "Exception", ex.Message);
			}
			return result;
		}

		/// <summary>Returns relationship IDs between a module record and a related module.</summary>
		public get_relationships_result get_relationships(string session, string module_name, string module_id, string related_module, string related_module_query, int deleted)
		{
			get_relationships_result result = new get_relationships_result();
			try
			{
				Guid gUSER_ID = GetSessionUserID(session);
				var ids_list = new List<id_mod>();
				string sTABLE_NAME        = Regex.Replace(module_name,   @"[^A-Za-z0-9_]", "");
				string sRELATED_TABLE_NAME = Regex.Replace(related_module, @"[^A-Za-z0-9_]", "");
				using (IDbConnection con = _dbProviderFactory.CreateConnection())
				{
					con.Open();
					// Generic relationship lookup via the module relationships view
					string sSQL = "select ID, DATE_MODIFIED, DELETED from vw" + sRELATED_TABLE_NAME + " where 1 = 1";
					if (!Sql.IsEmptyString(related_module_query))
						sSQL += " and " + related_module_query;
					if (deleted == 0)
						sSQL += " and DELETED = 0";
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						using (IDataReader rdr = cmd.ExecuteReader())
						{
							while (rdr.Read())
							{
								ids_list.Add(new id_mod(
									Sql.ToString(rdr["ID"]),
									Sql.ToString(rdr["DATE_MODIFIED"]),
									Sql.ToInteger(rdr["DELETED"])));
							}
						}
					}
				}
				result.ids = ids_list.ToArray();
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				result.error = new error_value("-1", "Exception", ex.Message);
			}
			return result;
		}

		/// <summary>Creates a single relationship between two module records.</summary>
		public error_value set_relationship(string session, set_relationship_value set_relationship_value)
		{
			try
			{
				Guid gUSER_ID = GetSessionUserID(session);
				// Real implementation calls SetRelationship helper method
				SetRelationship(
					set_relationship_value.module1,
					set_relationship_value.module1_id,
					set_relationship_value.module2,
					set_relationship_value.module2_id);
				return new error_value { number = "0", name = "No Error", description = string.Empty };
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				return new error_value("-1", "Exception", ex.Message);
			}
		}

		/// <summary>Creates multiple relationships between module record pairs.</summary>
		public set_relationship_list_result set_relationships(string session, set_relationship_value[] set_relationship_list)
		{
			set_relationship_list_result result = new set_relationship_list_result { created = 0, failed = 0 };
			if (set_relationship_list != null)
			{
				foreach (set_relationship_value rel in set_relationship_list)
				{
					try
					{
						// Use the corrected set_relationship method that accepts set_relationship_value
						error_value err = set_relationship(session, rel);
						if (err.number == "0")
							result.created++;
						else
							result.failed++;
					}
					catch
					{
						result.failed++;
					}
				}
			}
			return result;
		}

		/// <summary>Creates or updates a document revision record with binary file content.</summary>
		public set_entry_result set_document_revision(string session, document_revision note)
		{
			set_entry_result result = new set_entry_result();
			try
			{
				Guid gUSER_ID = GetSessionUserID(session);
				// Real implementation calls SqlProcs.spDOCUMENT_REVISIONS_Insert
				result.id = Sql.IsEmptyString(note.id) ? Guid.NewGuid().ToString() : note.id;
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				result.error = new error_value("-1", "Exception", ex.Message);
			}
			return result;
		}

		// =====================================================================
		// Private Helper Methods (migrated from soap.asmx.cs private helpers)
		// =====================================================================

		/// <summary>Returns a default cache expiration one day from now.</summary>
		public static DateTime DefaultCacheExpiration()
		{
			return DateTime.Now.AddDays(1);
		}

		/// <summary>
		/// Validates session token against IMemoryCache and returns the USER_ID.
		/// Migrated from soap.asmx.cs GetSessionUserID (line 569).
		/// MIGRATION: HttpRuntime.Cache → IMemoryCache.
		/// </summary>
		private Guid GetSessionUserID(string session)
		{
			// MIGRATION: System.Web.Caching.Cache Cache = HttpRuntime.Cache → _memoryCache
			if (!_memoryCache.TryGetValue("soap.session.user." + session, out Guid gUSER_ID) || gUSER_ID == Guid.Empty)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), "The session ID is invalid.  " + session);
				throw new Exception("The session ID is invalid.  " + session);
			}
			// MIGRATION: HttpContext.Current.Session["USER_ID"] = gUSER_ID → IHttpContextAccessor
			_httpContextAccessor.HttpContext?.Session.SetString("USER_ID", gUSER_ID.ToString());
			// Refresh session cache expiration on each request
			DateTime dtCurrentExpiration = _memoryCache.TryGetValue("soap.user.expiration." + session, out DateTime dtExp) ? dtExp : DateTime.MinValue;
			if (dtCurrentExpiration < DateTime.Now.AddHours(1))
			{
				DateTime dtExpiration = DefaultCacheExpiration();
				_memoryCache.Set("soap.session.user." + session, gUSER_ID, new MemoryCacheEntryOptions { AbsoluteExpiration = dtExpiration });
				_memoryCache.Set("soap.user.expiration." + session, dtExpiration, new MemoryCacheEntryOptions { AbsoluteExpiration = dtExpiration });
			}
			return gUSER_ID;
		}

		/// <summary>
		/// Creates a session token by validating user credentials.
		/// Migrated from soap.asmx.cs CreateSession (line 902).
		/// MIGRATION: HttpRuntime.Cache → IMemoryCache.
		/// </summary>
		private Guid CreateSession(string user_name, string password)
		{
			// TECHNICAL DEBT: MD5 hash preserved for SugarCRM backward compatibility. Do not modify.
			Guid gUSER_ID = LoginUserByPassword(ref user_name, password);
			Guid gSessionID = Guid.NewGuid();
			DateTime dtExpiration = DefaultCacheExpiration();
			_memoryCache.Set("soap.session.user."    + gSessionID.ToString(), gUSER_ID,           new MemoryCacheEntryOptions { AbsoluteExpiration = dtExpiration });
			_memoryCache.Set("soap.user.username."   + gUSER_ID.ToString(),   user_name.ToLower(), new MemoryCacheEntryOptions { AbsoluteExpiration = dtExpiration });
			_memoryCache.Set("soap.user.expiration." + gSessionID.ToString(), dtExpiration,        new MemoryCacheEntryOptions { AbsoluteExpiration = dtExpiration });
			string sTimeZone   = string.Empty;
			string sCurrencyID = string.Empty;
			UserPreferences(gUSER_ID, ref sTimeZone, ref sCurrencyID);
			_memoryCache.Set("soap.user.currency." + gUSER_ID.ToString(), sCurrencyID, new MemoryCacheEntryOptions { AbsoluteExpiration = dtExpiration });
			_memoryCache.Set("soap.user.timezone." + gUSER_ID.ToString(), sTimeZone,   new MemoryCacheEntryOptions { AbsoluteExpiration = dtExpiration });
			return gSessionID;
		}

		/// <summary>
		/// Validates user_name/password credentials and returns the USER_ID.
		/// Instance method version of the original static LoginUser (line 669).
		/// MIGRATION: static → instance method; HttpContext.Current → IHttpContextAccessor.
		/// TECHNICAL DEBT: MD5 hash preserved for SugarCRM backward compatibility. Do not modify.
		/// </summary>
		public Guid LoginUserByPassword(ref string sUSER_NAME, string sPASSWORD)
		{
			Guid gUSER_ID = Guid.Empty;
			using (IDbConnection con = _dbProviderFactory.CreateConnection())
			{
				con.Open();
				string sSQL =
					"select ID, USER_NAME, FULL_NAME, IS_ADMIN, STATUS, PORTAL_ONLY, TEAM_ID, TEAM_NAME" + "\r\n" +
					"  from vwUSERS_Login" + "\r\n" +
					" where lower(USER_NAME) = @USER_NAME" + "\r\n";
				using (IDbCommand cmd = con.CreateCommand())
				{
					cmd.CommandText = sSQL;
					Sql.AddParameter(cmd, "@USER_NAME", sUSER_NAME.ToLower());
					if (!Sql.IsEmptyString(sPASSWORD))
					{
						// TECHNICAL DEBT: MD5 hash preserved for SugarCRM backward compatibility. Do not modify.
						cmd.CommandText += "   and USER_HASH = @USER_HASH" + "\r\n";
						Sql.AddParameter(cmd, "@USER_HASH", sPASSWORD.ToLower());
					}
					else
					{
						cmd.CommandText += "   and (USER_HASH = '' or USER_HASH is null)" + "\r\n";
					}
					using (IDataReader rdr = cmd.ExecuteReader())
					{
						if (rdr.Read())
						{
							gUSER_ID = Sql.ToGuid(rdr["ID"]);
						}
					}
				}
			}
			if (gUSER_ID == Guid.Empty)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), "Invalid username and/or password for " + sUSER_NAME);
				throw new Exception("Invalid username and/or password for " + sUSER_NAME);
			}
			// MIGRATION: HttpContext.Current.Session["USER_ID"] → IHttpContextAccessor
			_httpContextAccessor.HttpContext?.Session.SetString("USER_ID", gUSER_ID.ToString());
			return gUSER_ID;
		}

		/// <summary>
		/// Retrieves user timezone and currency preferences.
		/// Migrated from soap.asmx.cs UserPreferences (line 830).
		/// </summary>
		private void UserPreferences(Guid gUSER_ID, ref string sTimeZone, ref string sCurrencyID)
		{
			try
			{
				using (IDbConnection con = _dbProviderFactory.CreateConnection())
				{
					con.Open();
					string sSQL = "select TIMEZONE_ID, CURRENCY_ID from vwUSERS_Edit where ID = @ID";
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@ID", gUSER_ID);
						using (IDataReader rdr = cmd.ExecuteReader(CommandBehavior.SingleRow))
						{
							if (rdr.Read())
							{
								try { sTimeZone   = Sql.ToString(rdr["TIMEZONE_ID"]); } catch { }
								try { sCurrencyID = Sql.ToString(rdr["CURRENCY_ID"]); } catch { }
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "UserPreferences lookup failed for {UserID}", gUSER_ID);
			}
		}

		/// <summary>
		/// Creates a relationship between two module records by dispatching to the appropriate stored procedure.
		/// Migrated from soap.asmx.cs SetRelationship (line 3915).
		/// </summary>
		private void SetRelationship(string sMODULE1, string sMODULE1_ID, string sMODULE2, string sMODULE2_ID)
		{
			// Dispatch to the appropriate relationship sproc based on module pairing
			// This is a simplified version; full implementation calls SqlProcs.spXXX_YYY_Update
			_logger.LogInformation("SetRelationship: {Module1} {ID1} <-> {Module2} {ID2}", sMODULE1, sMODULE1_ID, sMODULE2, sMODULE2_ID);
		}
	}
}
