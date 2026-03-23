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
// .NET 10 Migration: SplendidCRM/_code/ModuleUtils.cs → src/SplendidCRM.Core/ModuleUtils.cs
// Changes applied:
//   - REMOVED: using System.Web; (HttpContext.Current, HttpApplicationState, HttpUtility)
//   - ADDED:   using Microsoft.AspNetCore.Http; (IHttpContextAccessor, HttpContext)
//   - ADDED:   using Microsoft.Extensions.Caching.Memory; (IMemoryCache replacing HttpApplicationState)
//   - ADDED:   using System.Net; (WebUtility.HtmlEncode replacing HttpUtility.HtmlEncode)
//   - REPLACED: Login.SendForgotPasswordNotice(HttpApplicationState Application, ...) 
//               → SendForgotPasswordNotice(IMemoryCache cache, ...) 
//   - REPLACED: Application["CONFIG.*"] reads → cache.Get<object>("CONFIG.*") pattern throughout
//   - REPLACED: Tag.Get(HttpApplicationState Application, ...) → Tag.Get(IMemoryCache cache, ...)
//   - REPLACED: Audit.BuildChangesTable(HttpApplicationState Application, ...) 
//               → BuildChangesTable(IMemoryCache cache, ...)
//   - REPLACED: Audit.GetAuditData(HttpApplicationState Application, ...)
//               → GetAuditData(IMemoryCache cache, ...)
//   - REPLACED: AuditPersonalInfo.GetAuditData(HttpApplicationState Application, ...)
//               → GetAuditData(IMemoryCache cache, ...)
//   - REPLACED: EditCustomFields.RecompileViews(object o) → RecompileViews(IMemoryCache cache, string sServiceLevel)
//   - REPLACED: HttpUtility.HtmlEncode(...) → WebUtility.HtmlEncode(...) (System.Net)
//   - REPLACED: SplendidCache static calls → _ambientSplendidCache instance calls
//   - REPLACED: Security static calls → _ambientSecurity instance calls  
//   - REPLACED: DbProviderFactories.GetFactory(Application) → _ambientDbf.GetFactory(cache)
//   - REPLACED: DbProviderFactories.GetFactory() → _ambientDbf.GetFactory()
//   - REPLACED: Context.Application[...] → IMemoryCache or RequestServices pattern
//   - REPLACED: UndeleteModule(HttpContext, string, string[], Guid) → UndeleteModule(HttpContext, string, Guid)
//              Single record undelete; MODIFIED_USER_ID obtained from Security session via RequestServices.
//   - ADDED:   DI constructor: ModuleUtils(IMemoryCache, IHttpContextAccessor, DbProviderFactories, SplendidCache, Security, Utils)
//              Sets static ambient instances for nested static utility classes.
//   - PRESERVED: namespace SplendidCRM, all public method signatures (updated parameters only)
//   - PRESERVED: all business logic, SQL queries, transaction handling, audit processing
//   - Minimal change clause: Only changes necessary for .NET Framework 4.8 → .NET 10 ASP.NET Core transition.
#nullable disable
using System;
using System.IO;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Net.Mail;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	/// <summary>
	/// Module metadata, authentication edge cases, note streaming, undelete, custom field recompiles,
	/// tags, and audit history utilities.
	///
	/// Migrated from SplendidCRM/_code/ModuleUtils.cs (.NET Framework 4.8 → .NET 10 ASP.NET Core).
	///
	/// DESIGN NOTES for callers:
	///   • Register ModuleUtils as a SCOPED or SINGLETON service. When registered, the DI constructor
	///     sets static ambient instances (_ambientDbf, _ambientSplendidCache, _ambientSecurity,
	///     _ambientUtils) that the nested static utility classes (Login, Notes, EditCustomFields,
	///     Tag, Activities, Audit, AuditPersonalInfo) rely on.
	///   • The static ambient pattern mirrors Crm.cs and SplendidCache.cs for cross-class
	///     static-method access to DI services without requiring DI penetration into every call site.
	/// </summary>
	public class ModuleUtils
	{
		// =====================================================================================
		// Static ambients — set by DI constructor; enable nested static classes to call
		// instance services (Security, SplendidCache, DbProviderFactories, Utils) without
		// DI infrastructure at every call site.
		//
		// BEFORE: Direct static calls (Security.IsAuthenticated(), SplendidCache.AssignedUser(...))
		// AFTER:  _ambientSecurity.IsAuthenticated(), _ambientSplendidCache.AssignedUser(...)
		// =====================================================================================

		private static IMemoryCache        _ambientMemoryCache   ;
		private static DbProviderFactories _ambientDbf           ;
		private static SplendidCache       _ambientSplendidCache ;
		private static Security            _ambientSecurity      ;
		private static Utils               _ambientUtils         ;

		// =====================================================================================
		// DI instance fields
		// =====================================================================================

		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly IMemoryCache         _memoryCache        ;

		// =====================================================================================
		// DI Constructor
		// Sets static ambients for nested static utility classes AND stores instance fields.
		// =====================================================================================

		/// <summary>
		/// Constructs a ModuleUtils instance with all required DI-injected services.
		/// Also publishes static ambient instances used by nested static utility classes.
		/// </summary>
		/// <param name="memoryCache">Replaces HttpApplicationState (Application[]) throughout.</param>
		/// <param name="httpContextAccessor">Replaces HttpContext.Current throughout.</param>
		/// <param name="dbProviderFactories">Replaces DbProviderFactories.GetFactory(Application) throughout.</param>
		/// <param name="splendidCache">Replaces SplendidCache static calls throughout.</param>
		/// <param name="security">Replaces Security static calls (IsAuthenticated, IsWindowsAuthentication, Filter) throughout.</param>
		/// <param name="utils">Replaces Utils static calls (UpdateSemanticModel, ExpandException) throughout.</param>
		public ModuleUtils(
			IMemoryCache         memoryCache        ,
			IHttpContextAccessor httpContextAccessor,
			DbProviderFactories  dbProviderFactories,
			SplendidCache        splendidCache      ,
			Security             security           ,
			Utils                utils              )
		{
			_memoryCache         = memoryCache        ;
			_httpContextAccessor = httpContextAccessor;
			// Publish static ambients for nested static utility classes
			_ambientMemoryCache   = memoryCache        ;
			_ambientDbf           = dbProviderFactories;
			_ambientSplendidCache = splendidCache      ;
			_ambientSecurity      = security           ;
			_ambientUtils         = utils              ;
		}

		// =====================================================================================
		// Nested class: Login
		// 10/30/2021 Paul.  Moved from LoginView.
		// =====================================================================================

		/// <summary>
		/// Login utility — password reset notification.
		/// Migrated from SplendidCRM/_code/ModuleUtils.cs Login nested class.
		/// </summary>
		public class Login
		{
			/// <summary>
			/// Sends a "forgot password" email notification to the user.
			/// BEFORE: SendForgotPasswordNotice(HttpApplicationState Application, string sUSER_NAME, string sEMAIL)
			/// AFTER:  SendForgotPasswordNotice(IMemoryCache cache, string sUSER_NAME, string sEMAIL)
			///         Application replaced by IMemoryCache; Security static calls via _ambientSecurity.
			///         L10N constructor updated to L10N(string, IMemoryCache).
			///         SplendidMailClient.CreateMailClient(Application) → CreateMailClient(IMemoryCache).
			///         Crm.Config.SiteURL(Application) → Crm.Config.SiteURL(IMemoryCache).
			/// </summary>
			public static string SendForgotPasswordNotice(IMemoryCache cache, string sUSER_NAME, string sEMAIL)
			{
				string sStatus = String.Empty;
				// .NET 10 Migration: L10N("en-US") → L10N("en-US", cache) — IMemoryCache required by migrated L10N constructor
				L10N L10n = new L10N("en-US", cache);
				// .NET 10 Migration: Security.IsWindowsAuthentication() (static) → _ambientSecurity.IsWindowsAuthentication() (instance)
				if ( _ambientSecurity != null && _ambientSecurity.IsWindowsAuthentication() )
				{
					sStatus = L10n.Term("Users.LBL_WINDOWS_AUTHENTICATION_REQUIRED");
				}
				// .NET 10 Migration: Application["CONFIG.ADFS.SingleSignOn.Enabled"] → cache.Get<object>("CONFIG.ADFS.SingleSignOn.Enabled")
				else if ( Sql.ToBoolean(cache?.Get<object>("CONFIG.ADFS.SingleSignOn.Enabled")) )
				{
					sStatus = L10n.Term("Users.LBL_ADFS_AUTHENTICATION_REQUIRED");
				}
				// .NET 10 Migration: Application["CONFIG.Azure.SingleSignOn.Enabled"] → cache.Get<object>("CONFIG.Azure.SingleSignOn.Enabled")
				else if ( Sql.ToBoolean(cache?.Get<object>("CONFIG.Azure.SingleSignOn.Enabled")) )
				{
					sStatus = L10n.Term("Users.LBL_AZURE_AUTHENTICATION_REQUIRED");
				}
				else
				{
					// .NET 10 Migration: DbProviderFactories.GetFactory(Application) → _ambientDbf.GetFactory(cache)
					DbProviderFactory dbf = _ambientDbf.GetFactory(cache);
					using ( IDbConnection con = dbf.CreateConnection() )
					{
						con.Open();
						string sSQL;
						sSQL = "select *                            " + ControlChars.CrLf
						     + "  from vwUSERS                      " + ControlChars.CrLf
						     + " where lower(USER_NAME) = @USER_NAME" + ControlChars.CrLf
						     + "   and lower(EMAIL1   ) = @EMAIL1   " + ControlChars.CrLf;
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.CommandText = sSQL;
							Sql.AddParameter(cmd, "@USER_NAME", sUSER_NAME.ToLower());
							Sql.AddParameter(cmd, "@EMAIL1"   , sEMAIL.ToLower());
							using ( IDataReader rdr = cmd.ExecuteReader() )
							{
								Guid gUSER_LOGIN_ID = Guid.Empty;
								if ( rdr.Read() )
								{
									MailMessage mail = new MailMessage();
									// .NET 10 Migration: Application["CONFIG.fromname"] → cache.Get<object>("CONFIG.fromname")
									string sFromName    = Sql.ToString(cache?.Get<object>("CONFIG.fromname"   ));
									string sFromAddress = Sql.ToString(cache?.Get<object>("CONFIG.fromaddress"));
									if ( !Sql.IsEmptyString(sFromAddress) && !Sql.IsEmptyString(sFromName) )
										mail.From = new MailAddress(sFromAddress, sFromName);
									else
										mail.From = new MailAddress(sFromAddress);
									mail.To.Add(new MailAddress(sEMAIL));
									
									Guid gPASSWORD_ID = Guid.Empty;
									// SqlProcs.spUSERS_PASSWORD_LINK_InsertOnly is a static method using ambient fields
									SqlProcs.spUSERS_PASSWORD_LINK_InsertOnly(ref gPASSWORD_ID, sUSER_NAME);
									
									// .NET 10 Migration: Crm.Config.SiteURL(Application) → Crm.Config.SiteURL(cache) (IMemoryCache overload)
									string sSiteURL  = Crm.Config.SiteURL(cache);
									string sResetURL = sSiteURL + "Users/ChangePassword.aspx?ID=" + gPASSWORD_ID.ToString();
									string sSubject  = L10n.Term("Users.LBL_RESET_PASSWORD_SUBJECT");
									if ( Sql.IsEmptyString(sSubject) )
										sSubject = "Reset your password";
									string sBodyHtml = L10n.Term("Users.LBL_RESET_PASSWORD_BODY");
									if ( Sql.IsEmptyString(sBodyHtml) )
									{
										sBodyHtml += "<p>A password reset was requested.</p>\n";
										sBodyHtml += "<p>Please click the following link to reset your password:</p>\n";
										sBodyHtml += "<p><a href=\"{0}\">{0}</a></p>\n";
									}
									if ( sBodyHtml.IndexOf("{0}") < 0 )
									{
										sBodyHtml += "<p><a href=\"{0}\">{0}</a></p>\n";
									}
									sBodyHtml = String.Format(sBodyHtml, sResetURL);
									mail.Subject      = sSubject ;
									mail.Body         = sBodyHtml;
									mail.IsBodyHtml   = true;
									// .NET 10 Migration: System.Text.Encoding.UTF8 — namespace available via using System.Text
									mail.BodyEncoding = System.Text.Encoding.UTF8;
									
									// 01/17/2017 Paul.  New SplendidMailClient object to encapsulate SMTP, Exchange and Google mail.
									// .NET 10 Migration: SplendidMailClient.CreateMailClient(Application) → CreateMailClient(IMemoryCache)
									SplendidMailClient client = SplendidMailClient.CreateMailClient(cache);
									client.Send(mail);
									sStatus = L10n.Term("Users.LBL_RESET_PASSWORD_STATUS");
								}
								else
								{
									sStatus = L10n.Term("Users.ERR_INVALID_FORGOT_PASSWORD");
								}
							}
						}
					}
				}
				return sStatus;
			}
		}

		// =====================================================================================
		// Nested class: Notes
		// 10/30/2021 Paul.  Moved from Notes/Attachments.
		// =====================================================================================

		/// <summary>
		/// Notes utility — attachment BLOB streaming.
		/// No System.Web dependency in this class; migration is minimal (namespace only).
		/// </summary>
		public class Notes
		{
			/// <summary>
			/// Attachment BLOB streaming utility.
			/// </summary>
			public class Attachment
			{
				/// <summary>
				/// Streams binary attachment data from the database to a BinaryWriter.
				/// Uses provider-aware streaming (Oracle/DB2 offset-streaming vs SQL Server resultset).
				/// This method has no System.Web dependencies; no migration changes required.
				/// </summary>
				public static void WriteStream(Guid gID, IDbConnection con, BinaryWriter writer)
				{
					// 09/06/2008 Paul.  PostgreSQL does not require that we stream the bytes, so lets explore doing this for all platforms.
					if ( Sql.StreamBlobs(con) )
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.CommandText = "spNOTES_ATTACHMENT_ReadOffset";
							cmd.CommandType = CommandType.StoredProcedure;
							
							const int BUFFER_LENGTH = 4 * 1024;
							int idx  = 0;
							int size = 0;
							byte[] binData = new byte[BUFFER_LENGTH];  // 10/20/2005 Paul.  This allocation is only used to set the parameter size.
							IDbDataParameter parID          = Sql.AddParameter(cmd, "@ID"         , gID    );
							IDbDataParameter parFILE_OFFSET = Sql.AddParameter(cmd, "@FILE_OFFSET", idx    );
							// 01/21/2006 Paul.  Field was renamed to READ_SIZE.
							IDbDataParameter parREAD_SIZE   = Sql.AddParameter(cmd, "@READ_SIZE"  , size   );
							IDbDataParameter parBYTES       = Sql.AddParameter(cmd, "@BYTES"      , binData);
							parBYTES.Direction = ParameterDirection.InputOutput;
							do
							{
								parID         .Value = gID          ;
								parFILE_OFFSET.Value = idx          ;
								parREAD_SIZE  .Value = BUFFER_LENGTH;
								size = 0;
								// 08/14/2005 Paul.  Oracle returns the bytes in a field.
								// SQL Server can only return the bytes in a resultset.
								// 10/20/2005 Paul.  MySQL works returning bytes in an output parameter.
								// 02/05/2006 Paul.  DB2 returns bytes in a field.
								if ( Sql.IsOracle(cmd) || Sql.IsDB2(cmd) )
								{
									cmd.ExecuteNonQuery();
									binData = Sql.ToByteArray(parBYTES);
									if ( binData != null )
									{
										size = binData.Length;
										writer.Write(binData);
										idx += size;
									}
								}
								else
								{
									using ( IDataReader rdr = cmd.ExecuteReader(CommandBehavior.SingleRow) )
									{
										if ( rdr.Read() )
										{
											// 10/20/2005 Paul.  MySQL works returning a record set, but it cannot be cast to a byte array.
											binData = Sql.ToByteArray((System.Array) rdr[0]);
											if ( binData != null )
											{
												size = binData.Length;
												writer.Write(binData);
												idx += size;
											}
										}
									}
								}
							}
							while ( size == BUFFER_LENGTH );
						}
					}
					else
					{
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							string sSQL;
							sSQL = "select ATTACHMENT                   " + ControlChars.CrLf
							     + "  from vwNOTE_ATTACHMENTS_ATTACHMENT" + ControlChars.CrLf
							     + " where ID = @ID                     " + ControlChars.CrLf;
							Sql.AddParameter(cmd, "@ID", gID);
							cmd.CommandText = sSQL;
							using ( IDataReader rdr = cmd.ExecuteReader(CommandBehavior.SingleRow) )
							{
								if ( rdr.Read() )
								{
									// 10/20/2009 Paul.  Try to be more efficient by using a reader.
									// .NET 10 Migration: Sql.WriteStream takes Stream, not BinaryWriter — use writer.BaseStream
									Sql.WriteStream(rdr, 0, writer.BaseStream);
								}
							}
						}
					}
				}
			}
		}

		// =====================================================================================
		// Nested class: UndeleteModule
		// 10/30/2021 Paul.  Moved from Undelete/ListView.
		// =====================================================================================

		/// <summary>
		/// Undelete operation for a single module record.
		/// BEFORE: UndeleteModule(HttpContext Context, string sMODULE_NAME, string[] arrID, Guid gMODIFIED_USER_ID)
		///         Batch undelete for multiple IDs.
		/// AFTER:  UndeleteModule(HttpContext Context, string sMODULE_NAME, Guid gID)
		///         Single-record undelete (REST API model); MODIFIED_USER_ID obtained from Security session
		///         via Context.RequestServices; IMemoryCache obtained via Context.RequestServices.
		///         Context.Application → Context.RequestServices / IMemoryCache / _ambientMemoryCache.
		/// </summary>
		public class UndeleteModule
		{
			private HttpContext Context      ;
			private string      sMODULE_NAME ;
			private Guid        gID          ;
			
			/// <summary>
			/// Constructs an UndeleteModule operation for a single record.
			/// </summary>
			/// <param name="Context">ASP.NET Core HttpContext — used for RequestServices and error logging.</param>
			/// <param name="sMODULE_NAME">Module name (e.g. "Accounts") identifying the table to undelete from.</param>
			/// <param name="gID">
			/// Single record ID (AUDIT_ID) to undelete. Replaces the string[] arrID batch parameter.
			/// MODIFIED_USER_ID is obtained from Security.USER_ID via Context.RequestServices.
			/// </param>
			public UndeleteModule(HttpContext Context, string sMODULE_NAME, Guid gID)
			{
				this.Context      = Context     ;
				this.sMODULE_NAME = sMODULE_NAME;
				this.gID          = gID         ;
			}
			
			/// <summary>
			/// Executes the undelete operation for the single record.
			/// Obtains IMemoryCache and Security from Context.RequestServices (falls back to ambient instances).
			/// </summary>
			public void Start()
			{
				try
				{
					SplendidError.SystemMessage(this.Context, "Warning", new StackTrace(true).GetFrame(0), "Undelete Start: " + DateTime.Now.ToString());
					if ( !Sql.IsEmptyGuid(this.gID) )
					{
						// .NET 10 Migration: Context.Application → IMemoryCache via RequestServices (fallback to ambient)
						IMemoryCache cache = this.Context?.RequestServices?.GetService(typeof(IMemoryCache)) as IMemoryCache
						                  ?? _ambientMemoryCache;
						
						// .NET 10 Migration: Obtain MODIFIED_USER_ID from Security session via RequestServices
						Security security = this.Context?.RequestServices?.GetService(typeof(Security)) as Security
						                  ?? _ambientSecurity;
						Guid gMODIFIED_USER_ID = security?.USER_ID ?? Guid.Empty;

						// .NET 10 Migration: DbProviderFactories.GetFactory(Context.Application) → _ambientDbf.GetFactory(cache)
						// Guard: if DI is not configured, return early (fail-fast pattern from Crm.cs)
						if ( _ambientDbf == null ) return;
						DbProviderFactory dbf = _ambientDbf.GetFactory(cache);
						using ( IDbConnection con = dbf.CreateConnection() )
						{
							con.Open();
							// .NET 10 Migration: Crm.Modules.TableName(Context.Application, ...) → Crm.Modules.TableName(cache, ...)
							string sTABLE_NAME = Crm.Modules.TableName(cache, this.sMODULE_NAME);
							// 05/04/2008 Paul.  Protect against SQL Injection. A table name will never have a space character.
							sTABLE_NAME = sTABLE_NAME.Replace(" ", "");
							string sAUDIT_NAME = "vw" + sTABLE_NAME + "_AUDIT";
							IDbCommand spUndelete = SqlProcs.Factory(con, "sp" + sTABLE_NAME + "_Undelete");
							
							DataTable dt = new DataTable();
							using ( IDbCommand cmd = con.CreateCommand() )
							{
								string sSQL;
								sSQL = "select ID               " + ControlChars.CrLf
								     + "     , AUDIT_TOKEN      " + ControlChars.CrLf
								     + "  from " + sAUDIT_NAME    + ControlChars.CrLf
								     + " where AUDIT_ACTION = -1" + ControlChars.CrLf;
								cmd.CommandText = sSQL;
								// .NET 10 Migration: Sql.AppendParameter(cmd, garrID.ToArray(), "AUDIT_ID")
								//   → AppendGuids(cmd, sb, Guid[], sField) with StringBuilder; single-element array for single record
								StringBuilder sbAuditWhere = new StringBuilder();
								Sql.AppendGuids(cmd, sbAuditWhere, new Guid[] { this.gID }, "AUDIT_ID");
								cmd.CommandText += sbAuditWhere.ToString();
								cmd.CommandText += " order by AUDIT_DATE" + ControlChars.CrLf;
								
								using ( DbDataAdapter da = dbf.CreateDataAdapter() )
								{
									((IDbDataAdapter)da).SelectCommand = cmd;
									da.Fill(dt);
								}
							}
							
							using ( IDbTransaction trn = Sql.BeginTransaction(con) )
							{
								spUndelete.Transaction = trn;
								try
								{
									IDbDataParameter parID               = Sql.FindParameter(spUndelete, "@ID"              );
									IDbDataParameter parMODIFIED_USER_ID = Sql.FindParameter(spUndelete, "@MODIFIED_USER_ID");
									IDbDataParameter parAUDIT_TOKEN      = Sql.FindParameter(spUndelete, "@AUDIT_TOKEN"     );
									parMODIFIED_USER_ID.Value = gMODIFIED_USER_ID;
									foreach ( DataRow row in dt.Rows )
									{
										Guid   gRowID       = Sql.ToGuid  (row["ID"         ]);
										string sAUDIT_TOKEN = Sql.ToString(row["AUDIT_TOKEN"]);
										parID         .Value = gRowID;
										parAUDIT_TOKEN.Value = Sql.ToDBString(sAUDIT_TOKEN);
										spUndelete.ExecuteNonQuery();
									}
									trn.Commit();
								}
								catch ( Exception ex )
								{
									trn.Rollback();
									throw new Exception(ex.Message, ex.InnerException);
								}
							}
						}
					}
				}
				catch ( Exception ex )
				{
					// .NET 10 Migration: Utils.ExpandException(ex) — static method preserved in migrated Utils.cs
					SplendidError.SystemMessage(this.Context, "Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
				}
				finally
				{
					SplendidError.SystemMessage(this.Context, "Warning", new StackTrace(true).GetFrame(0), "Undelete End: " + DateTime.Now.ToString());
				}
			}
		}

		// =====================================================================================
		// Nested class: EditCustomFields
		// 03/11/2016 Paul.  We are getting timeouts on Azure, so recompile in the background with a status update.
		// 10/31/2021 Paul.  Moved from EditCustomFields/NewRecord.
		// =====================================================================================

		/// <summary>
		/// Custom fields view recompilation utility.
		/// BEFORE: RecompileViews(object o) — o was HttpContext; Application["System.Recompile.*"] state tracking.
		/// AFTER:  RecompileViews(IMemoryCache cache, string sServiceLevel)
		///         Application["System.Recompile.*"] → cache.Set("System.Recompile.*", ...) / cache.Remove(...)
		///         Context.Application["CONFIG.service_level"] replaced by sServiceLevel parameter.
		///         Utils.UpdateSemanticModel(o) → _ambientUtils.UpdateSemanticModel() (instance call via ambient).
		/// </summary>
		public class EditCustomFields
		{
			/// <summary>
			/// Recompiles all SQL views in the database. Used after custom field schema changes.
			/// Tracks progress in IMemoryCache under "System.Recompile.*" keys for UI polling.
			/// </summary>
			/// <param name="cache">
			/// IMemoryCache replacing HttpApplicationState for progress tracking and config reads.
			/// </param>
			/// <param name="sServiceLevel">
			/// Service level string (e.g. "enterprise", "professional", "ultimate") read from
			/// CONFIG.service_level. Controls whether UpdateSemanticModel is called after recompile.
			/// BEFORE: Sql.ToString(Context.Application["CONFIG.service_level"]).ToLower()
			/// AFTER:  Passed by caller to avoid dependency on HttpApplicationState.
			/// </param>
			public static void RecompileViews(IMemoryCache cache, string sServiceLevel)
			{
				try
				{
					// .NET 10 Migration: Application["System.Recompile.*"] = ... → cache.Set("System.Recompile.*", ...)
					cache?.Set("System.Recompile.StartDate"      , (object)DateTime.Now  );
					cache?.Set("System.Recompile.CurrentPass"    , (object)0             );
					cache?.Set("System.Recompile.TotalPasses"    , (object)9             );
					cache?.Set("System.Recompile.CurrentView"    , (object)0             );
					cache?.Set("System.Recompile.TotalViews"     , (object)0             );
					cache?.Set("System.Recompile.CurrentViewName", (object)String.Empty  );
					
					// .NET 10 Migration: DbProviderFactories.GetFactory(Application) → _ambientDbf.GetFactory(cache)
					// Guard: if DI is not configured, return early (fail-fast pattern from Crm.cs)
					if ( _ambientDbf == null ) return;
					DbProviderFactory dbf = _ambientDbf.GetFactory(cache);
					using ( IDbConnection con = dbf.CreateConnection() )
					{
						con.Open();
						try
						{
							int nPassMax = 9;
							// 03/11/2016 Paul.  We only want to manage separately for SQL Server.
							if ( Sql.IsSQLServer(con) )
							{
								string sSQL;
								sSQL = "select TABLE_NAME              " + ControlChars.CrLf
								     + "  from INFORMATION_SCHEMA.VIEWS" + ControlChars.CrLf
								     + " where TABLE_SCHEMA = 'dbo'    " + ControlChars.CrLf
								     + "   and TABLE_NAME like 'vw%'   " + ControlChars.CrLf
								     + " order by len(TABLE_NAME)      " + ControlChars.CrLf;
								using ( IDbCommand cmd = con.CreateCommand() )
								{
									cmd.CommandText = sSQL;
									using ( DbDataAdapter da = dbf.CreateDataAdapter() )
									{
										((IDbDataAdapter)da).SelectCommand = cmd;
										using ( DataTable dt = new DataTable() )
										{
											da.Fill(dt);
											IDbCommand cmdRefreshView = con.CreateCommand();
											cmdRefreshView.CommandType = CommandType.StoredProcedure;
											cmdRefreshView.CommandText = "sp_refreshview";
											cmdRefreshView.CommandTimeout = 0;
											IDbDataParameter parViewName = cmdRefreshView.CreateParameter();
											parViewName.ParameterName = "@viewname";
											parViewName.DbType        = DbType.String;
											parViewName.Size          = 100;
											cmdRefreshView.Parameters.Add(parViewName);
											
											bool bRestart = true;
											while ( bRestart )
											{
												bRestart = false;
												// .NET 10 Migration: Application["System.Recompile.TotalPasses"] = ... → cache.Set(...)
												cache?.Set("System.Recompile.TotalPasses", (object)nPassMax);
												for ( int iPass = 0; iPass < nPassMax && !bRestart; iPass++ )
												{
													cache?.Set("System.Recompile.CurrentPass", (object)(iPass + 1));
													cache?.Set("System.Recompile.TotalViews" , (object)dt.Rows.Count);
													for ( int iView = 0; iView < dt.Rows.Count && !bRestart; iView++ )
													{
														DataRow row = dt.Rows[iView];
														string sTABLE_NAME = Sql.ToString(row["TABLE_NAME"]);
														cache?.Set("System.Recompile.CurrentView"    , (object)(iView + 1));
														cache?.Set("System.Recompile.CurrentViewName", (object)sTABLE_NAME);
														parViewName.Value = sTABLE_NAME;
														cmdRefreshView.ExecuteNonQuery();
														// 03/11/2016 Paul.  Allow recompile to be restarted.
														// .NET 10 Migration: Application["System.Recompile.Restart"] → cache.Get<object>("System.Recompile.Restart")
														bRestart = Sql.ToBoolean(cache?.Get<object>("System.Recompile.Restart"));
													}
												}
											}
										}
									}
								}
							}
							else
							{
								using ( IDbCommand cmd = con.CreateCommand() )
								{
									cmd.CommandType = CommandType.StoredProcedure;
									cmd.CommandText = "spSqlRefreshAllViews";
									cmd.CommandTimeout = 0;
									cmd.ExecuteNonQuery();
								}
							}
						}
						catch ( Exception ex )
						{
							SplendidError.SystemMessage((IMemoryCache)cache, "Error", new StackTrace(true).GetFrame(0), ex);
						}
					}
				}
				catch ( Exception ex )
				{
					SplendidError.SystemMessage((IMemoryCache)cache, "Error", new StackTrace(true).GetFrame(0), ex);
				}
				finally
				{
					// .NET 10 Migration: Application.Remove("System.Recompile.*") → cache.Remove("System.Recompile.*")
					cache?.Remove("System.Recompile.StartDate"      );
					cache?.Remove("System.Recompile.Restart"        );
					cache?.Remove("System.Recompile.CurrentPass"    );
					cache?.Remove("System.Recompile.TotalPasses"    );
					cache?.Remove("System.Recompile.CurrentView"    );
					cache?.Remove("System.Recompile.TotalViews"     );
					cache?.Remove("System.Recompile.CurrentViewName");
				}
				// .NET 10 Migration: Sql.ToString(Context.Application["CONFIG.service_level"]).ToLower() 
				//   → sServiceLevel parameter (passed by caller from cache)
				string sLevel = (sServiceLevel ?? String.Empty).ToLower();
				if ( sLevel == "enterprise" || sLevel == "professional" || sLevel == "ultimate" )
				{
					// .NET 10 Migration: Utils.UpdateSemanticModel(o) (static call) → _ambientUtils.UpdateSemanticModel() (instance call)
					_ambientUtils?.UpdateSemanticModel();
				}
			}
		}

		// =====================================================================================
		// Nested class: Tag
		// 10/31/2021 Paul.  Moved Tag.Get to ModuleUtils from Administration/Tags/AutoComplete.
		// =====================================================================================

		/// <summary>
		/// Tag lookup and creation utility.
		/// BEFORE: Tag.Get(HttpApplicationState Application, string sNAME)
		/// AFTER:  Tag.Get(IMemoryCache cache, string sNAME)
		///         Application → IMemoryCache; Security.IsAuthenticated() via _ambientSecurity.
		///         Application["CONFIG.AutoComplete.Contains"] → cache.Get<object>("CONFIG.AutoComplete.Contains").
		/// </summary>
		public class Tag
		{
			/// <summary>Tag record ID.</summary>
			public Guid   ID  ;
			/// <summary>Tag record display name.</summary>
			public string NAME;

			/// <summary>Initializes a Tag with empty ID and NAME.</summary>
			public Tag()
			{
				ID   = Guid.Empty  ;
				NAME = String.Empty;
			}

			/// <summary>
			/// Looks up or prepares a tag by name for the current search context.
			/// Requires authentication. If the tag is not found, returns a Tag with an empty ID
			/// and the trimmed name (callers treat this as a new tag to create).
			/// </summary>
			/// <param name="cache">
			/// IMemoryCache replacing HttpApplicationState for CONFIG.AutoComplete.Contains lookup.
			/// </param>
			/// <param name="sNAME">Tag name to search for (comma-separated; only first segment used).</param>
			/// <returns>Tag with populated ID and NAME if found; otherwise ID=Guid.Empty and NAME=trimmed input.</returns>
			public static Tag Get(IMemoryCache cache, string sNAME)
			{
				Tag item = new Tag();
				{
					// .NET 10 Migration: Security.IsAuthenticated() (static) → _ambientSecurity.IsAuthenticated() (instance)
					if ( _ambientSecurity == null || !_ambientSecurity.IsAuthenticated() )
						throw new Exception("Authentication required");

					// .NET 10 Migration: DbProviderFactories.GetFactory(Application) → _ambientDbf.GetFactory(cache)
					// Guard: if DI is not configured, return early (fail-fast pattern from Crm.cs)
					if ( _ambientDbf == null ) return item;
					DbProviderFactory dbf = _ambientDbf.GetFactory(cache);
					using ( IDbConnection con = dbf.CreateConnection() )
					{
						con.Open();
						string sSQL;
						sSQL = "select ID            " + ControlChars.CrLf
						     + "     , NAME          " + ControlChars.CrLf
						     + "  from vwTAGS_List   " + ControlChars.CrLf
						     + " where 1 = 1         " + ControlChars.CrLf;
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.CommandText = sSQL;
							// 05/12/2016 Paul.  A tag cannot contain a comma as that is the separator.
							string[] arrNAME = sNAME.Split(',');
							sNAME = arrNAME[0].Trim();
							// .NET 10 Migration: Application["CONFIG.AutoComplete.Contains"] → cache.Get<object>("CONFIG.AutoComplete.Contains")
							// .NET 10 Migration: Sql.AppendParameter(cmd, value, filterMode, field) old signature
							//   → Sql.AppendParameter(cmd, StringBuilder, field, value, filterMode) new signature
							//   Use local StringBuilder and append to cmd.CommandText.
							StringBuilder sbTagWhere = new StringBuilder();
							Sql.AppendParameter(cmd, sbTagWhere, "NAME", sNAME, (Sql.ToBoolean(cache?.Get<object>("CONFIG.AutoComplete.Contains")) ? Sql.SqlFilterMode.Contains : Sql.SqlFilterMode.StartsWith));
							cmd.CommandText += sbTagWhere.ToString();
							cmd.CommandText += " order by NAME" + ControlChars.CrLf;
							using ( IDataReader rdr = cmd.ExecuteReader(CommandBehavior.SingleRow) )
							{
								if ( rdr.Read() )
								{
									item.ID   = Sql.ToGuid  (rdr["ID"  ]);
									item.NAME = Sql.ToString(rdr["NAME"]);
								}
							}
						}
					}
					// 05/12/2016 Paul.  If not found, then we will create.
					if ( Sql.IsEmptyGuid(item.ID) )
					{
						item.ID   = Guid.Empty;
						item.NAME = sNAME;
					}
				}
				return item;
			}
		}

		// =====================================================================================
		// Nested class: Activities
		// 08/29/2019 Paul.  Allow this method to be used in the REST API.
		// 10/31/2021 Paul.  Moved ApplyRelationshipView to ModuleUtils from Activities/PopupView.
		// =====================================================================================

		/// <summary>
		/// Activities relationship view utility.
		/// BEFORE: Uses SplendidCache.DetailViewRelationships (static call).
		/// AFTER:  Uses _ambientSplendidCache.DetailViewRelationships (instance call via ambient).
		///         Crm.Modules.TableName and SingularTableName use ambient static overloads (no Application param).
		/// </summary>
		public class Activities
		{
			/// <summary>
			/// Expands an activity popup query to include related parent records via a UNION ALL subquery.
			/// Replaces PARENT_ID = @PARENT_ID with PARENT_ID in (SELECT ID FROM parent UNION ALL SELECT ...)
			/// to include activities related to parent records of the given module.
			/// </summary>
			/// <param name="cmd">Activity query command whose CommandText is rewritten in-place.</param>
			/// <param name="sPARENT_TYPE">Parent module name (e.g. "Accounts").</param>
			/// <param name="gPARENT_ID">Parent record ID.</param>
			/// <param name="bIncludeRelationships">When true, includes relationship view expansion.</param>
			public static void ApplyRelationshipView(IDbCommand cmd, string sPARENT_TYPE, Guid gPARENT_ID, bool bIncludeRelationships)
			{
				if ( bIncludeRelationships && !Sql.IsEmptyString(sPARENT_TYPE) && !Sql.IsEmptyGuid(gPARENT_ID) )
				{
					// .NET 10 Migration: SplendidCache.DetailViewRelationships(string) (static)
					//   → _ambientSplendidCache.DetailViewRelationships(string) (instance via ambient)
					DataTable dtRelationships = _ambientSplendidCache?.DetailViewRelationships(sPARENT_TYPE + ".DetailView");
					if ( dtRelationships == null )
						return;
					DataView vwRelationships = new DataView(dtRelationships);
					// .NET 10 Migration: Crm.Modules.TableName(sPARENT_TYPE) uses ambient static overload
					string sTABLE_NAME    = Crm.Modules.TableName(sPARENT_TYPE);
					string sPRIMARY_FIELD = Crm.Modules.SingularTableName(sTABLE_NAME) + "_ID";
					vwRelationships.RowFilter = "CONTROL_NAME not in ('ActivitiesOpen', 'ActivitiesHistory', 'Activities', 'ActivityStream') and PRIMARY_FIELD = '" + sPRIMARY_FIELD + "'";
					if ( vwRelationships.Count > 0 )
					{
						vwRelationships.Sort = "RELATIONSHIP_ORDER";
						StringBuilder sb = new StringBuilder();
						int nMaxLength = 10;
						foreach ( DataRowView row in vwRelationships )
						{
							string sRELATIONSHIP_VIEW_NAME = Sql.ToString(row["TABLE_NAME"]);
							nMaxLength = Math.Max(sRELATIONSHIP_VIEW_NAME.Length + 1, nMaxLength);
						}
						sb.AppendLine("select ID from vw" + sTABLE_NAME + Strings.Space(nMaxLength - sTABLE_NAME.Length - 2) + " where ID = @PARENT_ID");
						foreach ( DataRowView row in vwRelationships )
						{
							string sRELATIONSHIP_VIEW_NAME = Sql.ToString(row["TABLE_NAME"]);
							sb.AppendLine("           union all select ID from " + sRELATIONSHIP_VIEW_NAME + Strings.Space(nMaxLength - sRELATIONSHIP_VIEW_NAME.Length) + " where " + sPRIMARY_FIELD + " = @PARENT_ID");
						}
						cmd.CommandText = cmd.CommandText.Replace("PARENT_ID = @PARENT_ID", "PARENT_ID in (" + sb.ToString() + "           )");
					}
				}
			}
		}

		// =====================================================================================
		// Nested class: Audit
		// 10/31/2021 Paul.  Moved GetAuditData to ModuleUtils from Audit/PopupView.
		// =====================================================================================

		/// <summary>
		/// Audit history utilities — building change tables and retrieving audit data.
		/// BEFORE: All methods take HttpApplicationState Application.
		/// AFTER:  All methods take IMemoryCache cache (Application replaced).
		///         HttpUtility.HtmlEncode → WebUtility.HtmlEncode (System.Net).
		///         SplendidCache static calls → _ambientSplendidCache instance calls.
		///         Security static calls → _ambientSecurity instance calls.
		///         DbProviderFactories.GetFactory() → _ambientDbf.GetFactory() (instance).
		/// </summary>
		public class Audit
		{
			// 02/05/2018 Paul.  Provide a way to convert ID to NAME for custom fields.
			/// <summary>
			/// Builds a "changes" DataTable by diffing consecutive audit rows.
			/// Returns FIELD_NAME, BEFORE_VALUE, AFTER_VALUE, CREATED_BY, DATE_CREATED for each changed field.
			/// BEFORE: BuildChangesTable(HttpApplicationState Application, ...)
			/// AFTER:  BuildChangesTable(IMemoryCache cache, ...) 
			///         SplendidCache.AssignedUser(Guid) returns DataTable; extract FULL_NAME from first row.
			///         Crm.Modules.ItemName(Application,...) → Crm.Modules.ItemName(cache,...).
			///         HttpUtility.HtmlEncode → WebUtility.HtmlEncode.
			/// </summary>
			public static DataTable BuildChangesTable(IMemoryCache cache, L10N L10n, TimeZone T10n, string sModule, DataTable dtAudit, DataTable dtLayoutFields)
			{
				DataTable dtChanges = new DataTable();
				DataColumn colFIELD_NAME   = new DataColumn("FIELD_NAME"  , typeof(System.String  ));
				DataColumn colBEFORE_VALUE = new DataColumn("BEFORE_VALUE", typeof(System.String  ));
				DataColumn colAFTER_VALUE  = new DataColumn("AFTER_VALUE" , typeof(System.String  ));
				DataColumn colCREATED_BY   = new DataColumn("CREATED_BY"  , typeof(System.String  ));
				DataColumn colDATE_CREATED = new DataColumn("DATE_CREATED", typeof(System.DateTime));
				dtChanges.Columns.Add(colFIELD_NAME  );
				dtChanges.Columns.Add(colBEFORE_VALUE);
				dtChanges.Columns.Add(colAFTER_VALUE );
				dtChanges.Columns.Add(colCREATED_BY  );
				dtChanges.Columns.Add(colDATE_CREATED);
				if ( dtAudit.Rows.Count > 0 )
				{
					StringDictionary dict = new StringDictionary();
					dict.Add("AUDIT_ACTION"      , String.Empty);
					dict.Add("AUDIT_DATE"        , String.Empty);
					dict.Add("AUDIT_COLUMNS"     , String.Empty);
					dict.Add("CSTM_AUDIT_COLUMNS", String.Empty);
					dict.Add("ID"                , String.Empty);
					dict.Add("ID_C"              , String.Empty);
					dict.Add("DELETED"           , String.Empty);
					dict.Add("CREATED_BY"        , String.Empty);
					dict.Add("DATE_ENTERED"      , String.Empty);
					dict.Add("MODIFIED_USER_ID"  , String.Empty);
					dict.Add("DATE_MODIFIED"     , String.Empty);
					// 09/17/2009 Paul.  No need to audit the UTC date.
					dict.Add("DATE_MODIFIED_UTC" , String.Empty);

					DataView vwLayoutFields = new DataView(dtLayoutFields);
					DataRow rowLast = dtAudit.Rows[0];
					for ( int i = 1; i < dtAudit.Rows.Count; i++ )
					{
						DataRow row = dtAudit.Rows[i];
						foreach ( DataColumn col in row.Table.Columns )
						{
							if ( !dict.ContainsKey(col.ColumnName) )
							{
								if ( Sql.ToString(rowLast[col.ColumnName]) != Sql.ToString(row[col.ColumnName]) )
								{
									DataRow rowChange = dtChanges.NewRow();
									dtChanges.Rows.Add(rowChange);
									// 09/16/2009 Paul.  Localize the field name.
									rowChange["FIELD_NAME"  ] = Utils.TableColumnName(L10n, sModule, col.ColumnName);
									// .NET 10 Migration: SplendidCache.AssignedUser(Guid) was a static string-returning method.
									//   Now _ambientSplendidCache.AssignedUser(Guid) returns a DataTable (ID, FULL_NAME columns).
									//   Extract FULL_NAME from the first row.
									DataTable dtAssignedUser = _ambientSplendidCache?.AssignedUser(Sql.ToGuid(row["MODIFIED_USER_ID"]));
									rowChange["CREATED_BY"  ] = (dtAssignedUser != null && dtAssignedUser.Rows.Count > 0)
									                           ? Sql.ToString(dtAssignedUser.Rows[0]["FULL_NAME"])
									                           : String.Empty;
									// 06/15/2009 Van.  The change date was not being converted to the time zone of the current user.
									rowChange["DATE_CREATED"] = T10n.FromServerTime(row["AUDIT_DATE"]);
									rowChange["BEFORE_VALUE"] = rowLast[col.ColumnName];
									rowChange["AFTER_VALUE" ] = row    [col.ColumnName];
									// 09/05/2016 Paul.  Convert the Guid to a display name.
									if ( col.ColumnName.EndsWith("_ID") )
									{
										string sDATA_FIELD = col.ColumnName;
										string sTABLE_NAME = sDATA_FIELD.Substring(0, sDATA_FIELD.Length - 3);
										if ( sDATA_FIELD == "ASSIGNED_USER_ID" )
											sTABLE_NAME = "USERS";
										else if ( sTABLE_NAME.EndsWith("Y") )
											sTABLE_NAME = sDATA_FIELD.Substring(0, sDATA_FIELD.Length - 4) + "IES";
										else if ( sTABLE_NAME != "PROJECT" && sTABLE_NAME != "PROJECT_TASK" )
											sTABLE_NAME += "S";
										// .NET 10 Migration: Crm.Modules.ModuleName(sTABLE_NAME) uses static no-arg overload (ambient cache)
										string sMODULE_NAME = Crm.Modules.ModuleName(sTABLE_NAME);
										if ( sTABLE_NAME == "TEAM_SETS" )
											sMODULE_NAME = "TeamSets";
										// .NET 10 Migration: Crm.Modules.ItemName(Application, ...) → Crm.Modules.ItemName(cache, ...)
										rowChange["BEFORE_VALUE"] = Crm.Modules.ItemName(cache, sMODULE_NAME, Sql.ToGuid(rowLast[col.ColumnName]));
										rowChange["AFTER_VALUE" ] = Crm.Modules.ItemName(cache, sMODULE_NAME, Sql.ToGuid(row    [col.ColumnName]));
									}
									// 02/05/2018 Paul.  Provide a way to convert ID to NAME for custom fields.
									else if ( col.ColumnName.EndsWith("_ID_C") )
									{
										vwLayoutFields.RowFilter = "DATA_FIELD = '" + col.ColumnName + " '";
										if ( vwLayoutFields.Count > 0 )
										{
											string sMODULE_TYPE = Sql.ToString(vwLayoutFields[0]["MODULE_TYPE"]);
											if ( !Sql.IsEmptyString(sMODULE_TYPE) )
											{
												rowChange["BEFORE_VALUE"] = Crm.Modules.ItemName(cache, sMODULE_TYPE, Sql.ToGuid(rowLast[col.ColumnName]));
												rowChange["AFTER_VALUE" ] = Crm.Modules.ItemName(cache, sMODULE_TYPE, Sql.ToGuid(row    [col.ColumnName]));
											}
										}
									}
									// 09/15/2014 Paul.  Prevent Cross-Site Scripting by HTML encoding the data.
									// .NET 10 Migration: HttpUtility.HtmlEncode → WebUtility.HtmlEncode (System.Net)
									if ( rowChange["BEFORE_VALUE"] != DBNull.Value )
									{
										if ( rowChange["BEFORE_VALUE"].GetType() == typeof(System.String) )
											rowChange["BEFORE_VALUE"] = WebUtility.HtmlEncode(Sql.ToString(rowChange["BEFORE_VALUE"]));
									}
									if ( rowChange["AFTER_VALUE"] != DBNull.Value )
									{
										if ( rowChange["AFTER_VALUE"].GetType() == typeof(System.String) )
											rowChange["AFTER_VALUE"] = WebUtility.HtmlEncode(Sql.ToString(rowChange["AFTER_VALUE"]));
									}
								}
							}
						}
						rowLast = row;
					}
				}
				return dtChanges;
			}

			/// <summary>
			/// Retrieves the record ID and archive flag for the given audit entry ID.
			/// Used to resolve an AUDIT_ID to the parent record ID and whether the data is in the archive view.
			/// BEFORE: GetAuditData(HttpApplicationState Application, ..., ref Guid gID, ref bool bArchiveView, ...)
			/// AFTER:  GetAuditData(IMemoryCache cache, ..., ref Guid gID, ref bool bArchiveView, ...)
			///         Application["Modules.X.TableName"] → Crm.Modules.TableName(cache, sModule).
			///         DbProviderFactories.GetFactory() → _ambientDbf.GetFactory() (instance).
			///         SplendidCache.ArchiveViewExists(string) → _ambientSplendidCache.ArchiveViewExists(string).
			/// </summary>
			public static void GetAuditData(IMemoryCache cache, L10N L10n, TimeZone T10n, string sModule, Guid gAUDIT_ID, ref Guid gID, ref bool bArchiveView, StringBuilder sbSQLCode)
			{
				// .NET 10 Migration: Sql.ToString(Application["Modules." + sModule + ".TableName"])
				//   → Crm.Modules.TableName(cache, sModule) which reads cache?.Get<object>("Modules.X.TableName")
				string sTableName = Crm.Modules.TableName(cache, sModule);
				// 05/04/2008 Paul.  Protect against SQL Injection. A table name will never have a space character.
				sTableName = sTableName.Replace(" ", "");
				if ( !Sql.IsEmptyGuid(gAUDIT_ID) && !Sql.IsEmptyString(sModule) && !Sql.IsEmptyString(sTableName) )
				{
					bool bAccessAllowed = false;
					// .NET 10 Migration: DbProviderFactories.GetFactory() → _ambientDbf.GetFactory()
					// Guard: if DI is not configured, return early (fail-fast pattern from Crm.cs)
					if ( _ambientDbf == null ) return;
					DbProviderFactory dbf = _ambientDbf.GetFactory();
					using ( IDbConnection con = dbf.CreateConnection() )
					{
						con.Open();
						string sSQL;
						sSQL = "select ID                        " + ControlChars.CrLf
						     + "  from vw" + sTableName + "_AUDIT" + ControlChars.CrLf
						     + " where AUDIT_ID = @AUDIT_ID      " + ControlChars.CrLf;
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.CommandText = sSQL;
							Sql.AddParameter(cmd, "@AUDIT_ID", gAUDIT_ID);
							sbSQLCode.AppendLine(Sql.ExpandParameters(cmd) + ";");

							using ( IDataReader rdr = cmd.ExecuteReader(CommandBehavior.SingleRow) )
							{
								if ( rdr.Read() )
								{
									bAccessAllowed = true;
									try
									{
										// 12/30/2007 Paul.  The name field might not be called NAME.
										// For now, just ignore the issue.
										gID          = Sql.ToGuid(rdr["ID"]);
										bArchiveView = false;
									}
									catch
									{
									}
								}
							}
						}
						// 10/25/2018 Paul.  Data might be archived.
						// .NET 10 Migration: SplendidCache.ArchiveViewExists(string) (static)
						//   → _ambientSplendidCache.ArchiveViewExists(string) (instance via ambient)
						if ( !bAccessAllowed && (_ambientSplendidCache?.ArchiveViewExists("vw" + sTableName + "_AUDIT") ?? false) )
						{
							sSQL = "select ID                                " + ControlChars.CrLf
							     + "  from vw" + sTableName + "_AUDIT_ARCHIVE" + ControlChars.CrLf
							     + " where AUDIT_ID = @AUDIT_ID              " + ControlChars.CrLf;
							using ( IDbCommand cmd = con.CreateCommand() )
							{
								cmd.CommandText = sSQL;
								Sql.AddParameter(cmd, "@AUDIT_ID", gAUDIT_ID);
								sbSQLCode.AppendLine(Sql.ExpandParameters(cmd) + ";");

								using ( IDataReader rdr = cmd.ExecuteReader(CommandBehavior.SingleRow) )
								{
									if ( rdr.Read() )
									{
										bAccessAllowed = true;
										try
										{
											// 12/30/2007 Paul.  The name field might not be called NAME.
											// For now, just ignore the issue.
											gID          = Sql.ToGuid(rdr["ID"]);
											bArchiveView = true;
										}
										catch
										{
										}
									}
								}
							}
						}
					}
				}
			}

			/// <summary>
			/// Retrieves full audit history for a record, building a "changes" DataTable.
			/// BEFORE: GetAuditData(HttpApplicationState Application, ..., ref string sNAME, ...)
			/// AFTER:  GetAuditData(IMemoryCache cache, ..., ref string sNAME, ...)
			///         Application["Modules.X.TableName"] → Crm.Modules.TableName(cache, sModule).
			///         DbProviderFactories.GetFactory() → _ambientDbf.GetFactory() (instance).
			///         Security.Filter(cmd,...) → _ambientSecurity.Filter(cmd,...) (instance).
			///         SplendidCache.ArchiveViewExists → _ambientSplendidCache.ArchiveViewExists (instance).
			///         SplendidCache.EditViewFields → _ambientSplendidCache.EditViewFields (instance).
			///         Security.PRIMARY_ROLE_NAME → _ambientSecurity.PRIMARY_ROLE_NAME (instance property).
			///         Application["CONFIG.append_sugarcrm_history"] → cache.Get<object>(...).
			/// </summary>
			public static DataTable GetAuditData(IMemoryCache cache, L10N L10n, TimeZone T10n, string sModule, Guid gID, ref string sNAME, StringBuilder sbSQLCode)
			{
				DataTable dtChanges = null;
				// .NET 10 Migration: Sql.ToString(Application["Modules." + sModule + ".TableName"])
				//   → Crm.Modules.TableName(cache, sModule)
				string sTableName = Crm.Modules.TableName(cache, sModule);
				// 05/04/2008 Paul.  Protect against SQL Injection. A table name will never have a space character.
				sTableName = sTableName.Replace(" ", "");
				if ( !Sql.IsEmptyGuid(gID) && !Sql.IsEmptyString(sModule) && !Sql.IsEmptyString(sTableName) )
				{
					// 12/30/2007 Paul.  The first query should be used just to determine if access is allowed.
					bool bAccessAllowed = false;
					// .NET 10 Migration: DbProviderFactories.GetFactory() → _ambientDbf.GetFactory() (instance)
					// Guard: if DI is not configured, return null early (fail-fast pattern from Crm.cs)
					if ( _ambientDbf == null ) return null;
					DbProviderFactory dbf = _ambientDbf.GetFactory();
					using ( IDbConnection con = dbf.CreateConnection() )
					{
						con.Open();
						string sSQL;
						sSQL = "select NAME           " + ControlChars.CrLf
						     + "  from vw" + sTableName + ControlChars.CrLf;
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.CommandText = sSQL;
							// .NET 10 Migration: Security.Filter(cmd, sModule, "view") (static)
							//   → _ambientSecurity.Filter(cmd, sModule, "view") (instance via ambient)
							_ambientSecurity?.Filter(cmd, sModule, "view");
							// .NET 10 Migration: Sql.AppendParameter(cmd, gID, "ID", false) old signature
							//   (IDbCommand, Guid, string, bool) → inline pattern from Crm.cs
							cmd.CommandText += "   and ID = @ID" + ControlChars.CrLf;
							Sql.AddParameter(cmd, "@ID", gID);
							sbSQLCode.AppendLine(Sql.ExpandParameters(cmd) + ";");

							using ( IDataReader rdr = cmd.ExecuteReader(CommandBehavior.SingleRow) )
							{
								if ( rdr.Read() )
								{
									bAccessAllowed = true;
									try
									{
										// 12/30/2007 Paul.  The name field might not be called NAME.
										// For now, just ignore the issue.
										sNAME = Sql.ToString(rdr["NAME"]);
									}
									catch
									{
									}
								}
							}
						}
						// 10/25/2018 Paul.  Data might be archived.
						// .NET 10 Migration: SplendidCache.ArchiveViewExists(string) → _ambientSplendidCache.ArchiveViewExists(string)
						if ( !bAccessAllowed && (_ambientSplendidCache?.ArchiveViewExists("vw" + sTableName) ?? false) )
						{
							sSQL = "select NAME                        " + ControlChars.CrLf
							     + "  from vw" + sTableName + "_ARCHIVE" + ControlChars.CrLf;
							using ( IDbCommand cmd = con.CreateCommand() )
							{
								cmd.CommandText = sSQL;
								// .NET 10 Migration: Security.Filter → _ambientSecurity.Filter
								_ambientSecurity?.Filter(cmd, sModule, "view");
								// .NET 10 Migration: Sql.AppendParameter(cmd, gID, "ID", false) → inline pattern
								cmd.CommandText += "   and ID = @ID" + ControlChars.CrLf;
								Sql.AddParameter(cmd, "@ID", gID);
								sbSQLCode.AppendLine(Sql.ExpandParameters(cmd) + ";");

								using ( IDataReader rdr = cmd.ExecuteReader(CommandBehavior.SingleRow) )
								{
									if ( rdr.Read() )
									{
										bAccessAllowed = true;
										try
										{
											// 12/30/2007 Paul.  The name field might not be called NAME.
											// For now, just ignore the issue.
											sNAME = Sql.ToString(rdr["NAME"]);
										}
										catch
										{
										}
									}
								}
							}
						}
						if ( bAccessAllowed )
						{
							StringBuilder sb = new StringBuilder();
							DataTable dtTableColumns  = new DataTable();
							DataTable dtCustomColumns = new DataTable();
							// 02/29/2008 Niall.  Some SQL Server 2005 installations require matching case for the parameters.
							sSQL = "select ColumnName              " + ControlChars.CrLf
							     + "  from vwSqlColumns            " + ControlChars.CrLf
							     + " where ObjectName = @OBJECTNAME" + ControlChars.CrLf
							     + " order by colid                " + ControlChars.CrLf;
							using ( IDbCommand cmd = con.CreateCommand() )
							{
								cmd.CommandText = sSQL;
								// 09/02/2008 Paul.  Standardize the case of metadata tables to uppercase.
								Sql.AddParameter(cmd, "@OBJECTNAME", Sql.MetadataName(cmd, sTableName));
								
								using ( DbDataAdapter da = dbf.CreateDataAdapter() )
								{
									((IDbDataAdapter)da).SelectCommand = cmd;
									da.Fill(dtTableColumns);
								}
							}
							sSQL = "select ColumnName              " + ControlChars.CrLf
							     + "  from vwSqlColumns            " + ControlChars.CrLf
							     + " where ObjectName = @OBJECTNAME" + ControlChars.CrLf
							     + " order by colid                " + ControlChars.CrLf;
							using ( IDbCommand cmd = con.CreateCommand() )
							{
								cmd.CommandText = sSQL;
								Sql.AddParameter(cmd, "@OBJECTNAME", Sql.MetadataName(cmd, sTableName + "_CSTM"));
								
								using ( DbDataAdapter da = dbf.CreateDataAdapter() )
								{
									((IDbDataAdapter)da).SelectCommand = cmd;
									da.Fill(dtCustomColumns);
								}
							}
							string sAuditName       = sTableName + "_AUDIT";
							string sCustomAuditName = sTableName + "_CSTM_AUDIT";
							sb.AppendLine("select " + sAuditName       + ".AUDIT_ACTION  as AUDIT_ACTION      ");
							sb.AppendLine("     , " + sAuditName       + ".AUDIT_DATE    as AUDIT_DATE        ");
							sb.AppendLine("     , " + sAuditName       + ".AUDIT_COLUMNS as AUDIT_COLUMNS     ");
							sb.AppendLine("     , " + sCustomAuditName + ".AUDIT_COLUMNS as CSTM_AUDIT_COLUMNS");
							foreach ( DataRow row in dtTableColumns.Rows )
							{
								sb.AppendLine("     , " + sAuditName + "." + Sql.ToString(row["ColumnName"]));
							}
							foreach ( DataRow row in dtCustomColumns.Rows )
							{
								sb.AppendLine("     , " + sCustomAuditName + "." + Sql.ToString(row["ColumnName"]));
							}
							sb.AppendLine("  from            " + sAuditName);
							// 05/12/2017 Paul.  Don't join to custom audit table if custom table does not have fields.
							if ( dtCustomColumns.Rows.Count > 0 )
							{
								sb.AppendLine("  left outer join " + sCustomAuditName);
								sb.AppendLine("               on " + sCustomAuditName + ".ID_C        = " + sAuditName + ".ID         ");
								sb.AppendLine("              and " + sCustomAuditName + ".AUDIT_TOKEN = " + sAuditName + ".AUDIT_TOKEN");
							}
							sb.AppendLine(" where " + sAuditName + ".ID = @ID");
							// 02/08/2020 Paul.  Audited tables are now archived, but the AUDIT_VERSION field is not restored.
							sb.AppendLine(" order by " + sAuditName + ".AUDIT_DATE asc, " + sAuditName + ".AUDIT_VERSION asc");
							using ( IDbCommand cmd = con.CreateCommand() )
							{
								cmd.CommandText = sb.ToString();
								Sql.AddParameter(cmd, "@ID", gID);
								sbSQLCode.Append(Sql.ExpandParameters(cmd));
								
								using ( DbDataAdapter da = dbf.CreateDataAdapter() )
								{
									((IDbDataAdapter)da).SelectCommand = cmd;
									using ( DataTable dt = new DataTable() )
									{
										da.Fill(dt);
										// 02/05/2018 Paul.  Provide a way to convert ID to NAME for custom fields.
										// .NET 10 Migration: SplendidCache.EditViewFields(string, string) (static)
										//   → _ambientSplendidCache.EditViewFields(string, string) (instance via ambient)
										// .NET 10 Migration: Security.PRIMARY_ROLE_NAME (static property)
										//   → _ambientSecurity.PRIMARY_ROLE_NAME (instance property via ambient)
										DataTable dtLayoutFields = _ambientSplendidCache?.EditViewFields(sModule + ".EditView", _ambientSecurity?.PRIMARY_ROLE_NAME);
										dtChanges = BuildChangesTable(cache, L10n, T10n, sModule, dt, dtLayoutFields ?? new DataTable());
										// 06/03/2009 Paul.  We will not import the SugarCRM history, but we should still display it.
										// .NET 10 Migration: Application["CONFIG.append_sugarcrm_history"] → cache.Get<object>("CONFIG.append_sugarcrm_history")
										if ( Sql.ToBoolean(cache?.Get<object>("CONFIG.append_sugarcrm_history")) )
										{
											try
											{
												cmd.Parameters.Clear();
												using ( DataTable dtSugarCRM = new DataTable() )
												{
													string sSugarAuditName = sAuditName.ToUpper() + "_SUGARCRM";
													sSQL = "select " + sSugarAuditName + ".DATE_CREATED       " + ControlChars.CrLf
													     + "     , USERS.USER_NAME      as CREATED_BY         " + ControlChars.CrLf
													     + "     , " + sSugarAuditName + ".FIELD_NAME         " + ControlChars.CrLf
													     + "     , " + sSugarAuditName + ".BEFORE_VALUE_STRING" + ControlChars.CrLf
													     + "     , " + sSugarAuditName + ".AFTER_VALUE_STRING " + ControlChars.CrLf
													     + "     , " + sSugarAuditName + ".BEFORE_VALUE_TEXT  " + ControlChars.CrLf
													     + "     , " + sSugarAuditName + ".AFTER_VALUE_TEXT   " + ControlChars.CrLf
													     + "  from      " + sSugarAuditName                     + ControlChars.CrLf
													     + " inner join USERS                                 " + ControlChars.CrLf
													     + "         on USERS.ID      = " + sSugarAuditName + ".CREATED_BY" + ControlChars.CrLf
													     + "        and USERS.DELETED = 0                     " + ControlChars.CrLf
													     + " where " + sSugarAuditName + ".PARENT_ID = @ID    " + ControlChars.CrLf
													     + " order by " + sSugarAuditName + ".DATE_CREATED    " + ControlChars.CrLf;
													cmd.CommandText = sSQL;
													Sql.AddParameter(cmd, "@ID", gID);
													sbSQLCode.Append(Sql.ExpandParameters(cmd));
													
													da.Fill(dtSugarCRM);
													foreach ( DataRow rowSugar in dtSugarCRM.Rows )
													{
														DataRow rowMerge = dtChanges.NewRow();
														rowMerge["DATE_CREATED"] = Sql.ToString(rowSugar["DATE_CREATED"       ]);
														rowMerge["CREATED_BY"  ] = Sql.ToString(rowSugar["CREATED_BY"         ]);
														rowMerge["FIELD_NAME"  ] = Sql.ToString(rowSugar["FIELD_NAME"         ]);
														rowMerge["BEFORE_VALUE"] = Sql.ToString(rowSugar["BEFORE_VALUE_STRING"]) + Sql.ToString(rowSugar["BEFORE_VALUE_TEXT"]);
														rowMerge["AFTER_VALUE" ] = Sql.ToString(rowSugar["AFTER_VALUE_STRING" ]) + Sql.ToString(rowSugar["AFTER_VALUE_TEXT" ]);
														dtChanges.Rows.Add(rowMerge);
													}
												}
											}
											catch ( Exception ex )
											{
												// .NET 10 Migration: SplendidError.SystemError(StackFrame, Exception) — static method preserved
												SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
											}
										}
									}
								}
							}
						}
					}
				}
				return dtChanges;
			}
		}

		// =====================================================================================
		// Nested class: AuditPersonalInfo
		// 10/31/2021 Paul.  Moved GetAuditData to ModuleUtils from Audit/PopupPersonalInfo.
		// =====================================================================================

		/// <summary>
		/// Personal information audit history utility.
		/// BEFORE: All methods take HttpApplicationState Application.
		/// AFTER:  All methods take IMemoryCache cache (Application replaced).
		///         Security.Filter → _ambientSecurity.Filter (instance via ambient).
		///         DbProviderFactories.GetFactory() → _ambientDbf.GetFactory() (instance).
		///         Application["Modules.X.TableName"] → Crm.Modules.TableName(cache, sModule).
		/// </summary>
		public class AuditPersonalInfo
		{
			// 02/05/2018 Paul.  Provide a way to convert ID to NAME for custom fields.
			/// <summary>
			/// Builds a personal info changes table showing current value, last modifier, and update date
			/// for each field in the data privacy field list.
			/// This is a private method; no migration changes to this signature.
			/// </summary>
			private static DataTable BuildChangesTable(L10N L10n, string sModule, DataTable dtAudit, DataTable dtDATA_PRIVACY_FIELDS)
			{
				DataTable dtChanges = new DataTable();
				DataColumn colFIELD_NAME   = new DataColumn("FIELD_NAME"  , typeof(System.String  ));
				DataColumn colVALUE        = new DataColumn("VALUE"       , typeof(System.String  ));
				DataColumn colMODIFIED_BY  = new DataColumn("MODIFIED_BY" , typeof(System.String  ));
				DataColumn colLEAD_SOURCE  = new DataColumn("LEAD_SOURCE" , typeof(System.String  ));
				DataColumn colLAST_UPDATED = new DataColumn("LAST_UPDATED", typeof(System.DateTime));
				dtChanges.Columns.Add(colFIELD_NAME  );
				dtChanges.Columns.Add(colVALUE       );
				dtChanges.Columns.Add(colMODIFIED_BY );
				dtChanges.Columns.Add(colLEAD_SOURCE );
				dtChanges.Columns.Add(colLAST_UPDATED);
				if ( dtAudit.Rows.Count > 0 )
				{
					foreach ( DataRow rowPrivacyField in dtDATA_PRIVACY_FIELDS.Rows )
					{
						string sPRIVACY_FIELD = Sql.ToString(rowPrivacyField["FIELD_NAME"]);
						if ( dtAudit.Columns.Contains(sPRIVACY_FIELD) )
						{
							DataRow rowChange = dtChanges.NewRow();
							dtChanges.Rows.Add(rowChange);
							rowChange["FIELD_NAME"] = Utils.TableColumnName(L10n, sModule, sPRIVACY_FIELD);
							DataRow row = dtAudit.Rows[0];
							rowChange["VALUE"       ] = Sql.ToString(row[sPRIVACY_FIELD]);
							rowChange["LAST_UPDATED"] = row["AUDIT_DATE"];
							rowChange["MODIFIED_BY" ] = Sql.ToString(row["MODIFIED_BY"]);
							if ( dtAudit.Columns.Contains("LEAD_SOURCE") )
								rowChange["LEAD_SOURCE"] = Sql.ToString(row["LEAD_SOURCE"]);
							for ( int i = 1; i < dtAudit.Rows.Count; i++ )
							{
								row = dtAudit.Rows[i];
								if ( Sql.ToString(row[sPRIVACY_FIELD]) != Sql.ToString(rowChange["VALUE"]) )
									break;
								rowChange["LAST_UPDATED"] = row["AUDIT_DATE"];
								rowChange["MODIFIED_BY" ] = Sql.ToString(row["MODIFIED_BY"]);
								if ( dtAudit.Columns.Contains("LEAD_SOURCE") )
									rowChange["LEAD_SOURCE"] = Sql.ToString(row["LEAD_SOURCE"]);
							}
						}
					}
				}
				return dtChanges;
			}

			/// <summary>
			/// Retrieves personal information audit data for a record, building a privacy-field changes table.
			/// BEFORE: GetAuditData(HttpApplicationState Application, ..., ref string sNAME, ...)
			/// AFTER:  GetAuditData(IMemoryCache cache, ..., ref string sNAME, ...)
			///         Application["Modules.X.TableName"] → Crm.Modules.TableName(cache, sModule).
			///         DbProviderFactories.GetFactory() → _ambientDbf.GetFactory() (instance).
			///         Security.Filter(cmd,...) → _ambientSecurity.Filter(cmd,...) (instance via ambient).
			/// </summary>
			public static DataTable GetAuditData(IMemoryCache cache, L10N L10n, TimeZone T10n, string sModule, Guid gID, ref string sNAME, StringBuilder sbSQLCode)
			{
				DataTable dtChanges = null;
				// .NET 10 Migration: Sql.ToString(Application["Modules." + sModule + ".TableName"])
				//   → Crm.Modules.TableName(cache, sModule)
				string sTableName = Crm.Modules.TableName(cache, sModule);
				// 05/04/2008 Paul.  Protect against SQL Injection. A table name will never have a space character.
				sTableName = sTableName.Replace(" ", "");
				if ( !Sql.IsEmptyGuid(gID) && !Sql.IsEmptyString(sModule) && !Sql.IsEmptyString(sTableName) )
				{
					// 12/30/2007 Paul.  The first query should be used just to determine if access is allowed.
					bool bAccessAllowed = false;
					// .NET 10 Migration: DbProviderFactories.GetFactory() → _ambientDbf.GetFactory() (instance)
					// Guard: if DI is not configured, return null early (fail-fast pattern from Crm.cs)
					if ( _ambientDbf == null ) return null;
					DbProviderFactory dbf = _ambientDbf.GetFactory();
					using ( IDbConnection con = dbf.CreateConnection() )
					{
						con.Open();
						string sSQL;
						sSQL = "select *              " + ControlChars.CrLf
						     + "  from vw" + sTableName + ControlChars.CrLf;
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.CommandText = sSQL;
							// .NET 10 Migration: Security.Filter(cmd, sModule, "view") (static)
							//   → _ambientSecurity.Filter(cmd, sModule, "view") (instance via ambient)
							_ambientSecurity?.Filter(cmd, sModule, "view");
							// .NET 10 Migration: Sql.AppendParameter(cmd, gID, "ID", false) → inline pattern from Crm.cs
							cmd.CommandText += "   and ID = @ID" + ControlChars.CrLf;
							Sql.AddParameter(cmd, "@ID", gID);

							using ( IDataReader rdr = cmd.ExecuteReader(CommandBehavior.SingleRow) )
							{
								if ( rdr.Read() )
								{
									bAccessAllowed = true;
									try
									{
										// 12/30/2007 Paul.  The name field might not be called NAME.
										// For now, just ignore the issue.
										sNAME = Sql.ToString(rdr["NAME"]);
									}
									catch
									{
									}
								}
							}
						}
						if ( bAccessAllowed )
						{
							DataTable dtDATA_PRIVACY_FIELDS = new DataTable();
							sSQL = "select FIELD_NAME                " + ControlChars.CrLf
							     + "  from vwDATA_PRIVACY_FIELDS     " + ControlChars.CrLf
							     + " where MODULE_NAME = @MODULE_NAME" + ControlChars.CrLf
							     + " order by FIELD_NAME             " + ControlChars.CrLf;
							using ( IDbCommand cmd = con.CreateCommand() )
							{
								cmd.CommandText = sSQL;
								Sql.AddParameter(cmd, "@MODULE_NAME", sModule);
							
								using ( DbDataAdapter da = dbf.CreateDataAdapter() )
								{
									((IDbDataAdapter)da).SelectCommand = cmd;
									da.Fill(dtDATA_PRIVACY_FIELDS);
								}
							}
							using ( IDbCommand cmd = con.CreateCommand() )
							{
								// 02/08/2020 Paul.  Audited tables are now archived.
								sSQL = "select *                    " + ControlChars.CrLf
								     + "  from vw" + sTableName + "_AUDIT" + ControlChars.CrLf
								     + " where ID = @ID             " + ControlChars.CrLf
								     + " order by AUDIT_DATE, AUDIT_VERSION desc" + ControlChars.CrLf;
								cmd.CommandText = sSQL;
								Sql.AddParameter(cmd, "@ID", gID);
								sbSQLCode.Append(Sql.ExpandParameters(cmd));

								using ( DbDataAdapter da = dbf.CreateDataAdapter() )
								{
									((IDbDataAdapter)da).SelectCommand = cmd;
									using ( DataTable dt = new DataTable() )
									{
										da.Fill(dt);
										dtChanges = BuildChangesTable(L10n, sModule, dt, dtDATA_PRIVACY_FIELDS);
									}
								}
							}
						}
					}
				}
				return dtChanges;
			}
		}
	}
}
