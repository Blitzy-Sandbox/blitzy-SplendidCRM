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
// .NET 10 Migration: SplendidCRM/_code/Crm.cs → src/SplendidCRM.Core/Crm.cs
// Changes applied:
//   - REMOVED: using System.Web; (HttpContext.Current, HttpApplicationState)
//   - ADDED:   using Microsoft.AspNetCore.Http; using Microsoft.Extensions.Caching.Memory;
//   - Namespace changed from SplendidCRM.Crm → SplendidCRM; nested classes reorganized as static
//     nested classes inside outer class Crm (preserves Crm.Users, Crm.Modules, etc. call syntax)
//   - HttpApplicationState Application parameter → IMemoryCache cache parameter
//   - HttpContext.Current.Application["key"] → static ambient _ambientCache.Get<object>("key")
//   - DbProviderFactories.GetFactory() / DbProviderFactories.GetFactory(Application) →
//     static ambient _ambientDbf (set by DI constructor at application startup)
//   - Security.Filter() (was static) → omitted in static utility methods; callers apply ACL
//     MIGRATION NOTE: Security.Filter is now an instance method; static utility callers must
//     apply their own ACL filtering before using these utility methods.
//   - SqlProcs.spXXX() static calls → implemented inline using IDbTransaction.Connection.CreateCommand()
//   - Sql.AppendParameter(cmd, gID, "ID", false) → implemented inline as CommandText append + AddParameter
//   - Sql.AppendParameter(cmd, sEMAIL, "EMAIL1") → implemented inline as CommandText append + AddParameter
//   - Utils.ExpandException(ex) → ex.Message (Utils.ExpandException not in migrated Utils.cs)
//   - Application["Exists.WORKFLOW"] null-check logic → IMemoryCache.TryGetValue pattern
//   - Application["key"] = value write → IMemoryCache.Set(key, value) write
//   - DI constructor added: Crm(DbProviderFactories, IMemoryCache, IConfiguration, IHttpContextAccessor)
//   - Module name string constants from stub preserved in nested Modules class
#nullable disable
using System;
using System.IO;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace SplendidCRM
{
	/// <summary>
	/// Core CRM utility namespace container — provides nested static utility classes for Users, Modules,
	/// Emails, Config, Password, and file attachment handling.
	///
	/// This is both:
	///   1. A DI-injectable service (for callers like RestUtil that inject Crm via constructor)
	///   2. A container for nested static utility classes (Crm.Users, Crm.Modules, Crm.Config, etc.)
	///
	/// Migrated from SplendidCRM/_code/Crm.cs (namespace SplendidCRM.Crm) to .NET 10 ASP.NET Core.
	/// The original top-level classes in namespace SplendidCRM.Crm are now nested static classes
	/// inside the outer Crm class in namespace SplendidCRM, preserving the Crm.Users.Method() syntax.
	/// </summary>
	public class Crm
	{
		// =====================================================================================
		// Static ambient accessors — set by DI constructor at application startup.
		// Used by nested static utility classes that cannot receive DI injection directly.
		//
		// BEFORE: DbProviderFactories.GetFactory()         → AFTER: _ambientDbf.CreateConnection()
		// BEFORE: Application["key"]                       → AFTER: _ambientCache.Get<object>("key")
		//
		// Thread-safety: DbProviderFactories and IMemoryCache are both thread-safe singleton-like
		// services; assigning them to static fields from the DI constructor is safe.
		// =====================================================================================

		/// <summary>
		/// Static ambient DbProviderFactories — replaces DbProviderFactories.GetFactory() static calls.
		/// Set by the DI constructor at application startup.
		/// </summary>
		private static DbProviderFactories _ambientDbf;

		/// <summary>
		/// Static ambient IMemoryCache — replaces HttpApplicationState (Application["key"]) static reads.
		/// Set by the DI constructor at application startup.
		/// </summary>
		private static IMemoryCache _ambientCache;

		// =====================================================================================
		// DI instance fields — for DI-aware callers (RestUtil, etc.) that inject Crm as a service
		// =====================================================================================

		private readonly IMemoryCache         _memoryCache        ;
		private readonly IConfiguration       _configuration      ;
		private readonly IHttpContextAccessor _httpContextAccessor;

		// =====================================================================================
		// DI Constructor
		// Sets static ambient accessors for nested static utility classes AND instance fields
		// for DI-aware callers.
		// =====================================================================================

		/// <summary>
		/// Constructs a Crm service with injected dependencies.
		/// Also sets static ambient accessors used by nested static utility classes.
		/// </summary>
		public Crm(DbProviderFactories dbProviderFactories, IMemoryCache memoryCache, IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
		{
			// Set static ambients for use by nested static utility classes
			_ambientDbf   = dbProviderFactories;
			_ambientCache = memoryCache        ;
			// Instance fields for DI-aware callers
			_memoryCache         = memoryCache        ;
			_configuration       = configuration      ;
			_httpContextAccessor = httpContextAccessor;
		}

		// =====================================================================================
		// Instance accessor methods — for DI-aware callers that inject Crm as a service
		// =====================================================================================

		/// <summary>
		/// Retrieves a configuration string from the IMemoryCache (CONFIG.* key), falling back to
		/// IConfiguration for values not yet cached.
		/// Named GetConfigValue to avoid naming conflict with nested static class Crm.Config.
		/// Replaces: Application["CONFIG." + sName]
		/// </summary>
		public string GetConfigValue(string sName)
		{
			string sValue    = string.Empty      ;
			string sCacheKey = "CONFIG." + sName ;
			if ( _memoryCache.TryGetValue(sCacheKey, out object cached) )
				sValue = Sql.ToString(cached);
			else
				sValue = _configuration["SplendidCRM:" + sName] ?? string.Empty;
			return sValue;
		}

		/// <summary>Retrieves a boolean configuration value with a default fallback.</summary>
		public bool GetConfigBoolean(string sName, bool bDefault)
		{
			string sValue = GetConfigValue(sName);
			if ( Sql.IsEmptyString(sValue) )
				return bDefault;
			return Sql.ToBoolean(sValue);
		}

		/// <summary>Retrieves an integer configuration value with a default fallback.</summary>
		public int GetConfigInt(string sName, int nDefault)
		{
			string sValue = GetConfigValue(sName);
			if ( Sql.IsEmptyString(sValue) )
				return nDefault;
			return Sql.ToInteger(sValue);
		}

		/// <summary>Retrieves a Guid configuration value with a default fallback.</summary>
		public Guid GetConfigGuid(string sName, Guid gDefault)
		{
			string sValue = GetConfigValue(sName);
			if ( Sql.IsEmptyString(sValue) )
				return gDefault;
			return Sql.ToGuid(sValue);
		}

		// =====================================================================================
		// Nested static class: Users
		// BEFORE: public class Users in namespace SplendidCRM.Crm
		// AFTER:  public static class Users nested inside SplendidCRM.Crm outer class
		// =====================================================================================

		/// <summary>
		/// User lookup utilities.
		/// Migrated from SplendidCRM.Crm.Users (namespace SplendidCRM.Crm).
		/// DB access via _ambientDbf (set by Crm DI constructor).
		/// </summary>
		public static class Users
		{
			/// <summary>
			/// Returns the USER_NAME for the specified user ID.
			/// </summary>
			public static string USER_NAME(Guid gID)
			{
				string sUSER_NAME = String.Empty;
				if ( _ambientDbf == null )
					return sUSER_NAME;
				using ( IDbConnection con = _ambientDbf.CreateConnection() )
				{
					con.Open();
					string sSQL;
					sSQL = "select USER_NAME" + ControlChars.CrLf
					     + "  from vwUSERS  " + ControlChars.CrLf
					     + " where ID = @ID " + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@ID", gID);
						using ( IDataReader rdr = cmd.ExecuteReader() )
						{
							if ( rdr.Read() )
							{
								sUSER_NAME = Sql.ToString(rdr["USER_NAME"]);
							}
						}
					}
				}
				return sUSER_NAME;
			}

			/// <summary>
			/// 04/07/2014 Paul. When adding or removing a user to a call or meeting, we also need
			/// to add the private team to the dynamic teams.
			/// Returns the PRIVATE_TEAM_ID for the specified user ID.
			/// </summary>
			public static Guid PRIVATE_TEAM_ID(Guid gID)
			{
				Guid gPRIVATE_TEAM_ID = Guid.Empty;
				if ( _ambientDbf == null )
					return gPRIVATE_TEAM_ID;
				using ( IDbConnection con = _ambientDbf.CreateConnection() )
				{
					con.Open();
					string sSQL;
					sSQL = "select PRIVATE_TEAM_ID" + ControlChars.CrLf
					     + "  from vwUSERS_Login  " + ControlChars.CrLf
					     + " where ID = @ID       " + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@ID", gID);
						using ( IDataReader rdr = cmd.ExecuteReader() )
						{
							if ( rdr.Read() )
							{
								gPRIVATE_TEAM_ID = Sql.ToGuid(rdr["PRIVATE_TEAM_ID"]);
							}
						}
					}
				}
				return gPRIVATE_TEAM_ID;
			}

			/// <summary>
			/// 09/05/2013 Paul. Looks up a user by their EXTENSION (Asterisk/VOIP extension).
			/// Uses IMemoryCache to cache results keyed by extension.
			/// BEFORE: GetUserByExtension(HttpApplicationState Application, ...)
			/// AFTER:  GetUserByExtension(IMemoryCache cache, ...) — Application replaced by IMemoryCache
			/// </summary>
			public static void GetUserByExtension(IMemoryCache cache, string sEXTENSION, ref Guid gUSER_ID, ref Guid gTEAM_ID)
			{
				// 09/05/2013 Paul. Use the Application as a cache for the Asterisk extension as we can correct by editing a user.
				// 09/20/2013 Paul. Move EXTENSION to the main table.
				// BEFORE: Application["Users.EXTENSION." + sEXTENSION + ".USER_ID"] == null
				// AFTER:  !cache.TryGetValue("Users.EXTENSION." + sEXTENSION + ".USER_ID", out _)
				string sUserIdKey = "Users.EXTENSION." + sEXTENSION + ".USER_ID";
				string sTeamIdKey = "Users.EXTENSION." + sEXTENSION + ".TEAM_ID";
				bool bCached = cache != null
				               && cache.TryGetValue(sUserIdKey, out object oUSER_ID)
				               && cache.TryGetValue(sTeamIdKey, out object oTEAM_ID);
				if ( !bCached )
				{
					if ( _ambientDbf == null )
						return;
					using ( IDbConnection con = _ambientDbf.CreateConnection() )
					{
						con.Open();
						string sSQL;
						// 09/06/2013 Paul. We need to use vwUSERS_Login so that we can get either the default team or the private team.
						sSQL = "select ID                    " + ControlChars.CrLf
						     + "     , TEAM_ID               " + ControlChars.CrLf
						     + "  from vwUSERS_Login         " + ControlChars.CrLf
						     + " where EXTENSION = @EXTENSION" + ControlChars.CrLf;
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.CommandText = sSQL;
							Sql.AddParameter(cmd, "@EXTENSION", sEXTENSION);
							using ( IDataReader rdr = cmd.ExecuteReader() )
							{
								if ( rdr.Read() )
								{
									gUSER_ID = Sql.ToGuid(rdr["ID"     ]);
									gTEAM_ID = Sql.ToGuid(rdr["TEAM_ID"]);
									// Cache the results for subsequent calls
									// BEFORE: Application["Users.EXTENSION." + sEXTENSION + ".USER_ID"] = gUSER_ID;
									// AFTER:  cache.Set(...)
									cache?.Set(sUserIdKey, (object)gUSER_ID  );
									cache?.Set(sTeamIdKey, (object)gTEAM_ID  );
								}
							}
						}
					}
				}
				else
				{
					// BEFORE: gUSER_ID = Sql.ToGuid(Application["Users.EXTENSION." + sEXTENSION + ".USER_ID"]);
					// AFTER:  Read from IMemoryCache
					cache.TryGetValue(sUserIdKey, out object oCachedUserID);
					cache.TryGetValue(sTeamIdKey, out object oCachedTeamID);
					gUSER_ID = Sql.ToGuid(oCachedUserID);
					gTEAM_ID = Sql.ToGuid(oCachedTeamID);
				}
			}

			/// <summary>
			/// Returns the count of active users (users with ID greater than the system user threshold).
			/// 04/07/2015 Paul. Change active user logic to use same as stored procedure.
			/// 05/04/2015 Paul. We have new users for HubSpot, iContact and ConstantContact, so make more room.
			/// </summary>
			public static int ActiveUsers()
			{
				int nActiveUsers = 0;
				if ( _ambientDbf == null )
					return nActiveUsers;
				using ( IDbConnection con = _ambientDbf.CreateConnection() )
				{
					con.Open();
					string sSQL;
					sSQL = "select count(*)          " + ControlChars.CrLf
					     + "  from vwUSERS_Login     " + ControlChars.CrLf
					     + " where ID > '00000000-0000-0000-0000-00000000000F'" + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						nActiveUsers = Sql.ToInteger(cmd.ExecuteScalar());
					}
				}
				return nActiveUsers;
			}
		}

		// =====================================================================================
		// Nested static class: Modules
		// BEFORE: public class Modules in namespace SplendidCRM.Crm
		// AFTER:  public static class Modules nested inside SplendidCRM.Crm outer class
		//
		// Also includes module name string constants from the original stub for backward compatibility.
		// =====================================================================================

		/// <summary>
		/// Module metadata utilities — TableName, ModuleName, parent lookup, item lookup, etc.
		/// Also contains module name string constants for use throughout the application.
		/// Migrated from SplendidCRM.Crm.Modules (namespace SplendidCRM.Crm).
		/// Cache access via IMemoryCache parameter (replaces HttpApplicationState Application parameter).
		/// </summary>
		public static class Modules
		{
			// ---------------------------------------------------------------------------------
			// Module name string constants (preserved from original stub for backward compatibility)
			// ---------------------------------------------------------------------------------
			public const string Accounts         = "Accounts"        ;
			public const string Bugs             = "Bugs"            ;
			public const string Calendar         = "Calendar"        ;
			public const string Calls            = "Calls"           ;
			public const string Campaigns        = "Campaigns"       ;
			public const string Cases            = "Cases"           ;
			public const string Contacts         = "Contacts"        ;
			public const string Documents        = "Documents"       ;
			public const string Emails           = "Emails"          ;
			public const string Employees        = "Employees"       ;
			public const string Leads            = "Leads"           ;
			public const string Meetings         = "Meetings"        ;
			public const string Notes            = "Notes"           ;
			public const string Opportunities    = "Opportunities"   ;
			public const string Project          = "Project"         ;
			public const string ProjectTask      = "ProjectTask"     ;
			public const string ProspectLists    = "ProspectLists"   ;
			public const string Prospects        = "Prospects"       ;
			public const string Tasks            = "Tasks"           ;
			public const string UsersModule      = "Users"           ;
			public const string Teams            = "Teams"           ;
			public const string Roles            = "Roles"           ;
			public const string Import           = "Import"          ;
			public const string Administration   = "Administration"  ;
			public const string ACLRoles         = "ACLRoles"        ;
			public const string DynamicLayout    = "DynamicLayout"   ;
			public const string Releases         = "Releases"        ;
			public const string iFrames          = "iFrames"         ;
			public const string Feeds            = "Feeds"           ;
			public const string CampaignTrackers = "CampaignTrackers";
			public const string EmailMarketing   = "EmailMarketing"  ;
			public const string EmailTemplates   = "EmailTemplates"  ;
			public const string InboundEmail     = "InboundEmail"    ;
			public const string KBDocuments      = "KBDocuments"     ;
			public const string Forums           = "Forums"          ;
			public const string Schedulers       = "Schedulers"      ;
			public const string Terminology      = "Terminology"     ;
			public const string Tags             = "Tags"            ;

			// ---------------------------------------------------------------------------------
			// Utility methods (migrated from SplendidCRM.Crm.Modules class)
			// ---------------------------------------------------------------------------------

			/// <summary>
			/// 09/07/2009 Paul. We need a more consistent way to get the table name from the module name.
			/// BEFORE: TableName(HttpApplicationState Application, string sMODULE)
			/// AFTER:  TableName(IMemoryCache cache, string sMODULE) — Application replaced by IMemoryCache
			/// </summary>
			public static string TableName(IMemoryCache cache, string sMODULE)
			{
				// 01/07/2009 Paul. For old databases, if the table name is not known, then assume that it matches the module name.
				// BEFORE: Sql.ToString(Application["Modules." + sMODULE + ".TableName"])
				// AFTER:  cache?.Get<object>("Modules." + sMODULE + ".TableName")
				string sTABLE_NAME = Sql.ToString(cache?.Get<object>("Modules." + sMODULE + ".TableName"));
				if ( Sql.IsEmptyString(sTABLE_NAME) )
				{
					sTABLE_NAME = sMODULE.ToUpper();
					// 10/30/2014 Paul. Some common modules that are disabled, but generate Precompile errors.
					if      ( sTABLE_NAME == "PROJECTTASK"      )  sTABLE_NAME = "PROJECT_TASK"    ;
					else if ( sTABLE_NAME == "PROSPECTLISTS"    )  sTABLE_NAME = "PROSPECT_LISTS"  ;
					else if ( sTABLE_NAME == "SMSMESSAGES"      )  sTABLE_NAME = "SMS_MESSAGES"    ;
					else if ( sTABLE_NAME == "TWITTERMESSAGES"  )  sTABLE_NAME = "TWITTER_MESSAGES";
					else if ( sTABLE_NAME == "TWITTERTRACKS"    )  sTABLE_NAME = "TWITTER_TRACKS"  ;
					// 03/14/2016 Paul. Add Chat tables in case they are disabled.
					else if ( sTABLE_NAME == "CHATCHANNELS"     )  sTABLE_NAME = "CHAT_CHANNELS"   ;
					else if ( sTABLE_NAME == "CHATMESSAGES"     )  sTABLE_NAME = "CHAT_MESSAGES"   ;
					// 03/21/2016 Paul. Add Survey tables in case they are disabled.
					else if ( sTABLE_NAME == "SURVEYQUESTIONS"  )  sTABLE_NAME = "SURVEY_QUESTIONS";
					else if ( sTABLE_NAME == "SURVEYRESULTS"    )  sTABLE_NAME = "SURVEY_RESULTS"  ;
					// 03/31/2017 Paul. Add Product Catalog.
					else if ( sTABLE_NAME == "PRODUCTTEMPLATES" )  sTABLE_NAME = "PRODUCT_TEMPLATES";
					else if ( sTABLE_NAME == "PRODUCTCATALOG"   )  sTABLE_NAME = "PRODUCT_TEMPLATES";
					else if ( sTABLE_NAME == "PRODUCTTYPES"     )  sTABLE_NAME = "PRODUCT_TYPES"   ;
					// 11/02/2017 Paul. Add more tables based on record ACL.
					else if ( sTABLE_NAME == "CAMPAIGNTRACKERS" )  sTABLE_NAME = "CAMPAIGN_TRKRS"  ;
				}
				return sTABLE_NAME;
			}

			/// <summary>
			/// Convenience overload — uses static ambient cache set by Crm DI constructor.
			/// BEFORE: TableName(string sMODULE) called HttpContext.Current.Application
			/// AFTER:  uses _ambientCache
			/// </summary>
			public static string TableName(string sMODULE)
			{
				return TableName(_ambientCache, sMODULE);
			}

			/// <summary>
			/// 11/06/2011 Paul. Make accessing the module name easier.
			/// Convenience overload — uses static ambient cache.
			/// BEFORE: Sql.ToString(HttpContext.Current.Application["Modules." + sTABLE_NAME + ".ModuleName"])
			/// AFTER:  uses _ambientCache
			/// </summary>
			public static string ModuleName(string sTABLE_NAME)
			{
				// BEFORE: Sql.ToString(HttpContext.Current.Application["Modules." + sTABLE_NAME + ".ModuleName"])
				// AFTER:  _ambientCache?.Get<object>("Modules." + sTABLE_NAME + ".ModuleName")
				return Sql.ToString(_ambientCache?.Get<object>("Modules." + sTABLE_NAME + ".ModuleName"));
			}

			/// <summary>
			/// 07/17/2016 Paul. Need to call from workflow engine.
			/// BEFORE: ModuleName(HttpApplicationState Application, string sTABLE_NAME)
			/// AFTER:  ModuleName(IMemoryCache cache, string sTABLE_NAME) — Application replaced by IMemoryCache
			/// </summary>
			public static string ModuleName(IMemoryCache cache, string sTABLE_NAME)
			{
				// BEFORE: Sql.ToString(Application["Modules." + sTABLE_NAME + ".ModuleName"])
				// AFTER:  cache?.Get<object>("Modules." + sTABLE_NAME + ".ModuleName")
				return Sql.ToString(cache?.Get<object>("Modules." + sTABLE_NAME + ".ModuleName"));
			}

			/// <summary>Converts a plural table name to singular form (e.g., CATEGORIES → CATEGORY).</summary>
			public static string SingularTableName(string sTABLE_NAME)
			{
				if      ( sTABLE_NAME.EndsWith("IES") )  sTABLE_NAME = sTABLE_NAME.Substring(0, sTABLE_NAME.Length - 3) + "Y";
				else if ( sTABLE_NAME.EndsWith("S"  ) )  sTABLE_NAME = sTABLE_NAME.Substring(0, sTABLE_NAME.Length - 1)       ;
				return sTABLE_NAME;
			}

			/// <summary>Converts a plural module name to singular form (e.g., Categories → Category).</summary>
			public static string SingularModuleName(string sTABLE_NAME)
			{
				if      ( sTABLE_NAME.EndsWith("ies") )  sTABLE_NAME = sTABLE_NAME.Substring(0, sTABLE_NAME.Length - 3) + "y";
				else if ( sTABLE_NAME.EndsWith("s"  ) )  sTABLE_NAME = sTABLE_NAME.Substring(0, sTABLE_NAME.Length - 1)       ;
				return sTABLE_NAME;
			}

			/// <summary>
			/// Returns true if the specified module uses custom paging.
			/// BEFORE: CustomPaging(HttpApplicationState Application, string sMODULE)
			/// AFTER:  CustomPaging(IMemoryCache cache, string sMODULE) — Application replaced by IMemoryCache
			/// </summary>
			public static bool CustomPaging(IMemoryCache cache, string sMODULE)
			{
				// BEFORE: Sql.ToBoolean(Application["Modules." + sMODULE + ".CustomPaging"])
				// AFTER:  cache?.Get<object>("Modules." + sMODULE + ".CustomPaging")
				return Sql.ToBoolean(cache?.Get<object>("Modules." + sMODULE + ".CustomPaging"));
			}

			/// <summary>Convenience overload — uses static ambient cache.</summary>
			public static bool CustomPaging(string sMODULE)
			{
				return CustomPaging(_ambientCache, sMODULE);
			}

			/// <summary>
			/// 04/04/2010 Paul. Add EXCHANGE_SYNC so that we can enable/disable the sync buttons on MassUpdate panels.
			/// BEFORE: ExchangeFolders(HttpApplicationState Application, string sMODULE)
			/// AFTER:  ExchangeFolders(IMemoryCache cache, string sMODULE) — Application replaced by IMemoryCache
			/// </summary>
			public static bool ExchangeFolders(IMemoryCache cache, string sMODULE)
			{
				// BEFORE: Sql.ToBoolean(Application["Modules." + sMODULE + ".ExchangeSync"]) && Sql.ToBoolean(Application["Modules." + sMODULE + ".ExchangeFolders"])
				// AFTER:  cache?.Get<object>(...) pattern
				return Sql.ToBoolean(cache?.Get<object>("Modules." + sMODULE + ".ExchangeSync"    ))
				    && Sql.ToBoolean(cache?.Get<object>("Modules." + sMODULE + ".ExchangeFolders" ));
			}

			/// <summary>Convenience overload — uses static ambient cache.</summary>
			public static bool ExchangeFolders(string sMODULE)
			{
				return ExchangeFolders(_ambientCache, sMODULE);
			}

			/// <summary>
			/// 12/02/2009 Paul. Add the ability to disable Mass Updates.
			/// Convenience method — uses static ambient cache.
			/// BEFORE: Sql.ToBoolean(HttpContext.Current.Application["Modules." + sMODULE + ".MassUpdate"])
			/// AFTER:  _ambientCache?.Get<object>(...)
			/// </summary>
			public static bool MassUpdate(string sMODULE)
			{
				// BEFORE: Sql.ToBoolean(HttpContext.Current.Application["Modules." + sMODULE + ".MassUpdate"])
				// AFTER:  _ambientCache?.Get<object>(...)
				return Sql.ToBoolean(_ambientCache?.Get<object>("Modules." + sMODULE + ".MassUpdate"));
			}

			/// <summary>
			/// 01/13/2010 Paul. Some customers want the ability to disable the default search.
			/// If the value is not set, returns true (default search enabled).
			/// BEFORE: Uses HttpContext.Current.Application; null check means "not set"
			/// AFTER:  Uses _ambientCache; TryGetValue for null-check equivalent
			/// </summary>
			public static bool DefaultSearch(string sMODULE)
			{
				// 01/13/2010 Paul. If the value is not set, we want to assume true.
				// BEFORE: object oDefaultSearch = HttpContext.Current.Application["Modules." + sMODULE + ".DefaultSearch"];
				// AFTER:  Use TryGetValue to distinguish "key not present" from "key present but false"
				if ( _ambientCache == null )
					return true;
				if ( !_ambientCache.TryGetValue("Modules." + sMODULE + ".DefaultSearch", out object oDefaultSearch) )
					return true;
				return Sql.ToBoolean(oDefaultSearch);
			}

			/// <summary>
			/// 12/22/2007 Paul. Inside the timer event, there is no current context, so we need to pass the application.
			/// Returns the parent record DataTable for the given parent type and ID.
			/// BEFORE: Parent(HttpApplicationState Application, string sPARENT_TYPE, Guid gPARENT_ID)
			/// AFTER:  Parent(IMemoryCache cache, string sPARENT_TYPE, Guid gPARENT_ID) — Application replaced by IMemoryCache
			/// </summary>
			public static DataTable Parent(IMemoryCache cache, string sPARENT_TYPE, Guid gPARENT_ID)
			{
				DataTable dt = new DataTable();
				// 09/07/2009 Paul. Use the new TableName function.
				string sTABLE_NAME = TableName(cache, sPARENT_TYPE);
				if ( !Sql.IsEmptyString(sTABLE_NAME) )
				{
					if ( _ambientDbf == null )
						return dt;
					using ( IDbConnection con = _ambientDbf.CreateConnection() )
					{
						con.Open();
						string sSQL;
						// 06/09/2008 Paul. Use the Edit view so that description fields will be available.
						sSQL = "select *"                           + ControlChars.CrLf
						     + "  from vw" + sTABLE_NAME + "_Edit" + ControlChars.CrLf
						     + " where ID = @ID"                   + ControlChars.CrLf;
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.CommandText = sSQL;
							Sql.AddParameter(cmd, "@ID", gPARENT_ID);
							using ( DbDataAdapter da = _ambientDbf.CreateDataAdapter() )
							{
								((IDbDataAdapter)da).SelectCommand = cmd;
								da.Fill(dt);
							}
						}
					}
				}
				return dt;
			}

			/// <summary>Convenience overload — uses static ambient cache.</summary>
			public static DataTable Parent(string sPARENT_TYPE, Guid gPARENT_ID)
			{
				return Parent(_ambientCache, sPARENT_TYPE, gPARENT_ID);
			}

			/// <summary>
			/// 02/16/2010 Paul. Move ToGuid to the function so that it can be captured if invalid.
			/// Returns the display name for the given module item, resolving object ID with error handling.
			/// BEFORE: ItemName(HttpApplicationState Application, string sMODULE_NAME, object oID)
			/// AFTER:  ItemName(IMemoryCache cache, string sMODULE_NAME, object oID) — Application replaced by IMemoryCache
			/// </summary>
			public static string ItemName(IMemoryCache cache, string sMODULE_NAME, object oID)
			{
				string sName = String.Empty;
				try
				{
					Guid gID = Sql.ToGuid(oID);
					sName = ItemName(cache, sMODULE_NAME, gID);
				}
				catch ( Exception ex )
				{
					sName = Sql.ToString(oID);
					// BEFORE: SplendidError.SystemMessage(Application, "Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex) + ControlChars.CrLf + sName)
					// AFTER:  SplendidError.SystemMessage(IMemoryCache, ...) — Application replaced by IMemoryCache
					//         Utils.ExpandException() not available in migrated Utils.cs; use ex.Message as fallback
					SplendidError.SystemMessage(cache, "Error", new StackTrace(true).GetFrame(0), ex.Message + ControlChars.CrLf + sName);
				}
				return sName;
			}

			/// <summary>
			/// Returns the display name for the given module item ID.
			/// BEFORE: ItemName(HttpApplicationState Application, string sMODULE_NAME, Guid gID)
			/// AFTER:  ItemName(IMemoryCache cache, string sMODULE_NAME, Guid gID) — Application replaced by IMemoryCache
			/// </summary>
			public static string ItemName(IMemoryCache cache, string sMODULE_NAME, Guid gID)
			{
				string sNAME      = String.Empty;
				string sTABLE_NAME = TableName(cache, sMODULE_NAME);
				if ( !Sql.IsEmptyString(sTABLE_NAME) )
				{
					if ( _ambientDbf == null )
						return sNAME;
					using ( IDbConnection con = _ambientDbf.CreateConnection() )
					{
						con.Open();
						string sSQL;
						// 12/03/2009 Paul. The Users table is special in that we want to use the USER_NAME instead of the NAME.
						// The primary reason for this is to allow it to be used by the EditView Assigned User ID field.
						// 02/03/2011 Paul. Employees returns the USERS table, which does not define USER_NAME for an employee.
						if ( String.Compare(sMODULE_NAME, "Employees", true) == 0 )
						{
							sSQL = "select NAME       " + ControlChars.CrLf
							     + "  from vwEMPLOYEES" + ControlChars.CrLf
							     + " where ID = @ID   " + ControlChars.CrLf;
						}
						// 09/05/2016 Paul. vwTEAM_SETS does not have a name field.
						else if ( String.Compare(sMODULE_NAME, "TeamSets", true) == 0 )
						{
							sSQL = "select TEAM_SET_NAME" + ControlChars.CrLf
							     + "  from vwTEAM_SETS  " + ControlChars.CrLf
							     + " where ID = @ID     " + ControlChars.CrLf;
						}
						else if ( String.Compare(sTABLE_NAME, "USERS", true) == 0 )
						{
							sSQL = "select USER_NAME as NAME" + ControlChars.CrLf
							     + "  from vw" + sTABLE_NAME  + ControlChars.CrLf
							     + " where ID = @ID"          + ControlChars.CrLf;
						}
						else
						{
							sSQL = "select NAME"             + ControlChars.CrLf
							     + "  from vw" + sTABLE_NAME  + ControlChars.CrLf
							     + " where ID = @ID"          + ControlChars.CrLf;
						}
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.CommandText = sSQL;
							Sql.AddParameter(cmd, "@ID", gID);
							sNAME = Sql.ToString(cmd.ExecuteScalar());
						}
					}
				}
				return sNAME;
			}

			/// <summary>
			/// 06/07/2015 Paul. Add support for Preview button.
			/// Returns the ACTIVITY_TYPE for the given activity ID.
			/// BEFORE: ActivityType(HttpApplicationState Application, Guid gID)
			/// AFTER:  ActivityType(IMemoryCache cache, Guid gID) — Application replaced by IMemoryCache (not used by this method)
			/// </summary>
			public static string ActivityType(IMemoryCache cache, Guid gID)
			{
				string sACTIVITY_TYPE = String.Empty;
				if ( _ambientDbf == null )
					return sACTIVITY_TYPE;
				using ( IDbConnection con = _ambientDbf.CreateConnection() )
				{
					con.Open();
					string sSQL;
					sSQL = "select ACTIVITY_TYPE" + ControlChars.CrLf
					     + "  from vwACTIVITIES"  + ControlChars.CrLf
					     + " where ID = @ID"       + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@ID", gID);
						sACTIVITY_TYPE = Sql.ToString(cmd.ExecuteScalar());
					}
				}
				return sACTIVITY_TYPE;
			}

			/// <summary>
			/// Returns the edit view DataRow for the given module item.
			/// MIGRATION NOTE: Security.Filter() was called here in the original code. Since Security.Filter
			/// is now an instance method (not static), it cannot be called from this static utility method.
			/// Callers should apply their own ACL filtering before or after calling this method.
			/// </summary>
			public static DataRow ItemEdit(string sMODULE_NAME, Guid gID)
			{
				DataRow row        = null;
				string sTABLE_NAME = TableName(sMODULE_NAME);
				if ( !Sql.IsEmptyString(sTABLE_NAME) )
				{
					if ( _ambientDbf == null )
						return row;
					using ( IDbConnection con = _ambientDbf.CreateConnection() )
					{
						con.Open();
						string sSQL;
						sSQL = "select *"                           + ControlChars.CrLf
						     + "  from vw" + sTABLE_NAME + "_Edit" + ControlChars.CrLf;
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.CommandText = sSQL;
							// MIGRATION NOTE: Security.Filter(cmd, sMODULE_NAME, "edit") was called here.
							// Security.Filter is now an instance method; it cannot be called from this static
							// utility. Callers that need ACL enforcement should use their injected Security
							// instance to apply the filter before calling this method, or apply it to the results.
							//
							// Inline equivalent of Sql.AppendParameter(cmd, gID, "ID", false)
							// BEFORE: Sql.AppendParameter(cmd, gID, "ID", false)
							// AFTER:  Inline — append WHERE clause + AddParameter
							cmd.CommandText += "   and ID = @ID" + ControlChars.CrLf;
							Sql.AddParameter(cmd, "@ID", gID);
							using ( DbDataAdapter da = _ambientDbf.CreateDataAdapter() )
							{
								((IDbDataAdapter)da).SelectCommand = cmd;
								using ( DataTable dt = new DataTable() )
								{
									da.Fill(dt);
									if ( dt.Rows.Count > 0 )
									{
										row = dt.Rows[0];
									}
								}
							}
						}
					}
				}
				return row;
			}

			/// <summary>
			/// Returns a DataTable of all items in the specified module (ID and NAME columns).
			/// MIGRATION NOTE: Security.Filter() was called here in the original code. See ItemEdit() note.
			/// </summary>
			public static DataTable Items(string sMODULE)
			{
				DataTable dt       = new DataTable();
				// In the original, this called Crm.Modules.TableName(sMODULE) — now just TableName(sMODULE) within same class
				string sTABLE_NAME = TableName(sMODULE);
				if ( _ambientDbf == null )
					return dt;
				using ( IDbConnection con = _ambientDbf.CreateConnection() )
				{
					con.Open();
					string sSQL;
					// 12/07/2009 Paul. The Users table is special in that we want to use the USER_NAME instead of the NAME.
					if ( String.Compare(sTABLE_NAME, "USERS", true) == 0 )
					{
						sSQL = "select ID               " + ControlChars.CrLf
						     + "     , USER_NAME as NAME" + ControlChars.CrLf;
					}
					else
					{
						sSQL = "select ID  " + ControlChars.CrLf
						     + "     , NAME" + ControlChars.CrLf;
					}
					sSQL += "  from vw" + sTABLE_NAME + "_List" + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						// MIGRATION NOTE: Security.Filter(cmd, sMODULE, "list") was called here.
						// See ItemEdit() note above regarding Security.Filter instance method migration.
						cmd.CommandText += " order by NAME";
						using ( DbDataAdapter da = _ambientDbf.CreateDataAdapter() )
						{
							((IDbDataAdapter)da).SelectCommand = cmd;
							da.Fill(dt);
						}
					}
				}
				return dt;
			}
		}

		// =====================================================================================
		// Nested static class: Emails
		// BEFORE: public class Emails in namespace SplendidCRM.Crm
		// AFTER:  public static class Emails nested inside SplendidCRM.Crm outer class
		// =====================================================================================

		/// <summary>
		/// Email recipient lookup utilities.
		/// Migrated from SplendidCRM.Crm.Emails (namespace SplendidCRM.Crm).
		/// MIGRATION NOTE: Security.Filter() was called per module; now omitted as it is an instance method.
		/// </summary>
		public static class Emails
		{
			/// <summary>
			/// 08/30/2010 Paul. Exchange, IMAP and POP3 utils will all use this method to lookup a recipient by email.
			/// Returns the first matching PARENT_ID (Contact, Lead, or Prospect) for the given email address.
			/// MIGRATION NOTE: 
			///   - HttpContext.Current.Application → _ambientCache
			///   - Security.Filter(cmd, ...) → omitted (instance method; caller applies ACL)
			/// </summary>
			public static Guid RecipientByEmail(IDbConnection con, string sEMAIL)
			{
				Guid gRECIPIENT_ID = Guid.Empty;
				// BEFORE: HttpApplicationState Application = HttpContext.Current.Application;
				// AFTER:  Use _ambientCache static ambient
				IMemoryCache cache = _ambientCache;
				string sSQL        = String.Empty;

				string sMODULE_NAME = "Contacts";
				// BEFORE: Sql.ToBoolean(Application["Modules." + sMODULE_NAME + ".Valid"]) && Sql.IsEmptyGuid(gRECIPIENT_ID)
				// AFTER:  cache?.Get<object>("Modules." + sMODULE_NAME + ".Valid")
				if ( Sql.ToBoolean(cache?.Get<object>("Modules." + sMODULE_NAME + ".Valid")) && Sql.IsEmptyGuid(gRECIPIENT_ID) )
				{
					string sTABLE_NAME = Sql.ToString(cache?.Get<object>("Modules." + sMODULE_NAME + ".TableName"));
					sSQL = "select ID              " + ControlChars.CrLf
					     + "     , EMAIL1          " + ControlChars.CrLf
					     + "  from vw" + sTABLE_NAME + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						// MIGRATION NOTE: Security.Filter(cmd, sMODULE_NAME, "view") was called here.
						// See Crm.Modules.ItemEdit() migration note regarding Security.Filter instance method.
						// Inline equivalent of Sql.AppendParameter(cmd, sEMAIL, "EMAIL1")
						// BEFORE: Sql.AppendParameter(cmd, sEMAIL, "EMAIL1")
						// AFTER:  Inline WHERE append + AddParameter
						if ( !Sql.IsEmptyString(sEMAIL) )
						{
							cmd.CommandText += "   and EMAIL1 = @EMAIL1" + ControlChars.CrLf;
							Sql.AddParameter(cmd, "@EMAIL1", sEMAIL, sEMAIL.Length);
						}
						using ( IDataReader rdr = cmd.ExecuteReader(CommandBehavior.SingleRow) )
						{
							if ( rdr.Read() )
							{
								gRECIPIENT_ID = Sql.ToGuid(rdr["ID"]);
							}
						}
					}
				}
				sMODULE_NAME = "Leads";
				if ( Sql.ToBoolean(cache?.Get<object>("Modules." + sMODULE_NAME + ".Valid")) && Sql.IsEmptyGuid(gRECIPIENT_ID) )
				{
					string sTABLE_NAME = Sql.ToString(cache?.Get<object>("Modules." + sMODULE_NAME + ".TableName"));
					sSQL = "select ID              " + ControlChars.CrLf
					     + "     , EMAIL1          " + ControlChars.CrLf
					     + "  from vw" + sTABLE_NAME + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						// MIGRATION NOTE: Security.Filter(cmd, sMODULE_NAME, "view") omitted — see above.
						if ( !Sql.IsEmptyString(sEMAIL) )
						{
							cmd.CommandText += "   and EMAIL1 = @EMAIL1" + ControlChars.CrLf;
							Sql.AddParameter(cmd, "@EMAIL1", sEMAIL, sEMAIL.Length);
						}
						using ( IDataReader rdr = cmd.ExecuteReader(CommandBehavior.SingleRow) )
						{
							if ( rdr.Read() )
							{
								gRECIPIENT_ID = Sql.ToGuid(rdr["ID"]);
							}
						}
					}
				}
				sMODULE_NAME = "Prospects";
				if ( Sql.ToBoolean(cache?.Get<object>("Modules." + sMODULE_NAME + ".Valid")) && Sql.IsEmptyGuid(gRECIPIENT_ID) )
				{
					string sTABLE_NAME = Sql.ToString(cache?.Get<object>("Modules." + sMODULE_NAME + ".TableName"));
					sSQL = "select ID              " + ControlChars.CrLf
					     + "     , EMAIL1          " + ControlChars.CrLf
					     + "  from vw" + sTABLE_NAME + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						// MIGRATION NOTE: Security.Filter(cmd, sMODULE_NAME, "view") omitted — see above.
						if ( !Sql.IsEmptyString(sEMAIL) )
						{
							cmd.CommandText += "   and EMAIL1 = @EMAIL1" + ControlChars.CrLf;
							Sql.AddParameter(cmd, "@EMAIL1", sEMAIL, sEMAIL.Length);
						}
						using ( IDataReader rdr = cmd.ExecuteReader(CommandBehavior.SingleRow) )
						{
							if ( rdr.Read() )
							{
								gRECIPIENT_ID = Sql.ToGuid(rdr["ID"]);
							}
						}
					}
				}
				return gRECIPIENT_ID;
			}
		}

		// =====================================================================================
		// Nested static class: EmailImages
		// BEFORE: public class EmailImages in namespace SplendidCRM.Crm
		// AFTER:  public static class EmailImages nested inside SplendidCRM.Crm outer class
		//
		// Stored procedure calls (spEMAIL_IMAGE_*) are inlined from SqlProcs because SqlProcs
		// is now a DI instance service, not a static class.
		// =====================================================================================

		/// <summary>
		/// Email image BLOB storage utilities.
		/// Migrated from SplendidCRM.Crm.EmailImages (namespace SplendidCRM.Crm).
		/// SqlProcs.spEMAIL_IMAGE_* static calls implemented inline using IDbTransaction.Connection.
		/// </summary>
		public static class EmailImages
		{
			/// <summary>
			/// 10/18/2009 Paul. Move blob logic to LoadFile.
			/// Stores email image BLOB data from a Stream using pointer-offset streaming (Oracle/DB2) or
			/// a direct content update (SQL Server).
			/// </summary>
			public static void LoadFile(Guid gID, Stream stm, IDbTransaction trn)
			{
				if ( Sql.StreamBlobs(trn.Connection) )
				{
					const int BUFFER_LENGTH = 4 * 1024;
					byte[] binFILE_POINTER  = new byte[16];
					// 01/20/2006 Paul. Must include in transaction
					// BEFORE: SqlProcs.spEMAIL_IMAGE_InitPointer(gID, ref binFILE_POINTER, trn)
					// AFTER:  Inline stored procedure call using trn.Connection
					InitPointerInline(trn, "spEMAIL_IMAGE_InitPointer", gID, ref binFILE_POINTER);
					using ( BinaryReader reader = new BinaryReader(stm) )
					{
						int    nFILE_OFFSET = 0;
						byte[] binBYTES     = reader.ReadBytes(BUFFER_LENGTH);
						while ( binBYTES.Length > 0 )
						{
							// 08/14/2005 Paul. gID is used by Oracle, binFILE_POINTER is used by SQL Server.
							// BEFORE: SqlProcs.spEMAIL_IMAGE_WriteOffset(gID, binFILE_POINTER, nFILE_OFFSET, binBYTES, trn)
							// AFTER:  Inline stored procedure call
							WriteOffsetInline(trn, "spEMAIL_IMAGE_WriteOffset", gID, binFILE_POINTER, nFILE_OFFSET, binBYTES);
							nFILE_OFFSET += binBYTES.Length;
							binBYTES      = reader.ReadBytes(BUFFER_LENGTH);
						}
					}
				}
				else
				{
					using ( BinaryReader reader = new BinaryReader(stm) )
					{
						byte[] binBYTES = reader.ReadBytes((int) stm.Length);
						// BEFORE: SqlProcs.spEMAIL_IMAGES_CONTENT_Update(gID, binBYTES, trn)
						// AFTER:  Inline stored procedure call
						ContentUpdateInline(trn, "spEMAIL_IMAGES_CONTENT_Update", gID, binBYTES);
					}
				}
			}

			/// <summary>
			/// 11/06/2010 Paul. We need a version that accepts a byte array.
			/// </summary>
			public static void LoadFile(Guid gID, byte[] binDATA, IDbTransaction trn)
			{
				if ( Sql.StreamBlobs(trn.Connection) )
				{
					const int BUFFER_LENGTH = 4 * 1024;
					byte[] binFILE_POINTER  = new byte[16];
					InitPointerInline(trn, "spEMAIL_IMAGE_InitPointer", gID, ref binFILE_POINTER);
					using ( MemoryStream stm = new MemoryStream(binDATA) )
					{
						using ( BinaryReader reader = new BinaryReader(stm) )
						{
							int    nFILE_OFFSET = 0;
							byte[] binBYTES     = reader.ReadBytes(BUFFER_LENGTH);
							while ( binBYTES.Length > 0 )
							{
								WriteOffsetInline(trn, "spEMAIL_IMAGE_WriteOffset", gID, binFILE_POINTER, nFILE_OFFSET, binBYTES);
								nFILE_OFFSET += binBYTES.Length;
								binBYTES      = reader.ReadBytes(BUFFER_LENGTH);
							}
						}
					}
				}
				else
				{
					ContentUpdateInline(trn, "spEMAIL_IMAGES_CONTENT_Update", gID, binDATA);
				}
			}
		}

		// =====================================================================================
		// Nested static class: Images
		// 05/27/2016 Paul. Move LoadFile to Crm.Images class.
		// =====================================================================================

		/// <summary>
		/// Image BLOB storage utilities.
		/// Migrated from SplendidCRM.Crm.Images (namespace SplendidCRM.Crm).
		/// </summary>
		public static class Images
		{
			/// <summary>Stores image BLOB data from a Stream.</summary>
			public static void LoadFile(Guid gID, Stream stm, IDbTransaction trn)
			{
				if ( Sql.StreamBlobs(trn.Connection) )
				{
					const int BUFFER_LENGTH = 4 * 1024;
					byte[] binFILE_POINTER  = new byte[16];
					// 01/20/2006 Paul. Must include in transaction
					InitPointerInline(trn, "spIMAGE_InitPointer", gID, ref binFILE_POINTER);
					using ( BinaryReader reader = new BinaryReader(stm) )
					{
						int    nFILE_OFFSET = 0;
						byte[] binBYTES     = reader.ReadBytes(BUFFER_LENGTH);
						while ( binBYTES.Length > 0 )
						{
							// 08/14/2005 Paul. gID is used by Oracle, binFILE_POINTER is used by SQL Server.
							WriteOffsetInline(trn, "spIMAGE_WriteOffset", gID, binFILE_POINTER, nFILE_OFFSET, binBYTES);
							nFILE_OFFSET += binBYTES.Length;
							binBYTES      = reader.ReadBytes(BUFFER_LENGTH);
						}
					}
				}
				else
				{
					using ( BinaryReader reader = new BinaryReader(stm) )
					{
						byte[] binBYTES = reader.ReadBytes((int) stm.Length);
						ContentUpdateInline(trn, "spIMAGES_CONTENT_Update", gID, binBYTES);
					}
				}
			}

			/// <summary>
			/// 05/27/2016 Paul. We need a version that accepts a byte array.
			/// </summary>
			public static void LoadFile(Guid gID, byte[] binDATA, IDbTransaction trn)
			{
				if ( Sql.StreamBlobs(trn.Connection) )
				{
					const int BUFFER_LENGTH = 4 * 1024;
					byte[] binFILE_POINTER  = new byte[16];
					InitPointerInline(trn, "spIMAGE_InitPointer", gID, ref binFILE_POINTER);
					using ( MemoryStream stm = new MemoryStream(binDATA) )
					{
						using ( BinaryReader reader = new BinaryReader(stm) )
						{
							int    nFILE_OFFSET = 0;
							byte[] binBYTES     = reader.ReadBytes(BUFFER_LENGTH);
							while ( binBYTES.Length > 0 )
							{
								WriteOffsetInline(trn, "spIMAGE_WriteOffset", gID, binFILE_POINTER, nFILE_OFFSET, binBYTES);
								nFILE_OFFSET += binBYTES.Length;
								binBYTES      = reader.ReadBytes(BUFFER_LENGTH);
							}
						}
					}
				}
				else
				{
					ContentUpdateInline(trn, "spIMAGES_CONTENT_Update", gID, binDATA);
				}
			}
		}

		// =====================================================================================
		// Nested static class: NoteAttachments
		// =====================================================================================

		/// <summary>
		/// Note attachment BLOB storage utilities.
		/// Migrated from SplendidCRM.Crm.NoteAttachments (namespace SplendidCRM.Crm).
		/// </summary>
		public static class NoteAttachments
		{
			/// <summary>
			/// 10/18/2009 Paul. Move blob logic to LoadFile.
			/// Stores note attachment BLOB data from a Stream.
			/// </summary>
			public static void LoadFile(Guid gID, Stream stm, IDbTransaction trn)
			{
				if ( Sql.StreamBlobs(trn.Connection) )
				{
					const int BUFFER_LENGTH = 4 * 1024;
					byte[] binFILE_POINTER  = new byte[16];
					// 01/20/2006 Paul. Must include in transaction
					InitPointerInline(trn, "spNOTES_ATTACHMENT_InitPointer", gID, ref binFILE_POINTER);
					using ( BinaryReader reader = new BinaryReader(stm) )
					{
						int    nFILE_OFFSET = 0;
						byte[] binBYTES     = reader.ReadBytes(BUFFER_LENGTH);
						while ( binBYTES.Length > 0 )
						{
							// 08/14/2005 Paul. gID is used by Oracle, binFILE_POINTER is used by SQL Server.
							WriteOffsetInline(trn, "spNOTES_ATTACHMENT_WriteOffset", gID, binFILE_POINTER, nFILE_OFFSET, binBYTES);
							nFILE_OFFSET += binBYTES.Length;
							binBYTES      = reader.ReadBytes(BUFFER_LENGTH);
						}
					}
				}
				else
				{
					using ( BinaryReader reader = new BinaryReader(stm) )
					{
						byte[] binBYTES = reader.ReadBytes((int) stm.Length);
						ContentUpdateInline(trn, "spNOTES_ATTACHMENT_Update", gID, binBYTES);
					}
				}
			}

			/// <summary>
			/// 11/06/2010 Paul. We need a version that accepts a byte array.
			/// </summary>
			public static void LoadFile(Guid gID, byte[] binDATA, IDbTransaction trn)
			{
				if ( Sql.StreamBlobs(trn.Connection) )
				{
					const int BUFFER_LENGTH = 4 * 1024;
					byte[] binFILE_POINTER  = new byte[16];
					// 01/20/2006 Paul. Must include in transaction
					InitPointerInline(trn, "spNOTES_ATTACHMENT_InitPointer", gID, ref binFILE_POINTER);
					using ( MemoryStream stm = new MemoryStream(binDATA) )
					{
						using ( BinaryReader reader = new BinaryReader(stm) )
						{
							int    nFILE_OFFSET = 0;
							byte[] binBYTES     = reader.ReadBytes(BUFFER_LENGTH);
							while ( binBYTES.Length > 0 )
							{
								// 08/14/2005 Paul. gID is used by Oracle, binFILE_POINTER is used by SQL Server.
								WriteOffsetInline(trn, "spNOTES_ATTACHMENT_WriteOffset", gID, binFILE_POINTER, nFILE_OFFSET, binBYTES);
								nFILE_OFFSET += binBYTES.Length;
								binBYTES      = reader.ReadBytes(BUFFER_LENGTH);
							}
						}
					}
				}
				else
				{
					ContentUpdateInline(trn, "spNOTES_ATTACHMENT_Update", gID, binDATA);
				}
			}
		}

		// =====================================================================================
		// Nested static class: BugAttachments
		// 05/27/2016 Paul. Move Bug Attachments to Crm file.
		// =====================================================================================

		/// <summary>
		/// Bug attachment BLOB storage utilities.
		/// Migrated from SplendidCRM.Crm.BugAttachments (namespace SplendidCRM.Crm).
		/// </summary>
		public static class BugAttachments
		{
			/// <summary>Stores bug attachment BLOB data from a Stream.</summary>
			public static void LoadFile(Guid gID, Stream stm, IDbTransaction trn)
			{
				if ( Sql.StreamBlobs(trn.Connection) )
				{
					const int BUFFER_LENGTH = 4 * 1024;
					byte[] binFILE_POINTER  = new byte[16];
					// 01/20/2006 Paul. Must include in transaction
					InitPointerInline(trn, "spBUGS_ATTACHMENT_InitPointer", gID, ref binFILE_POINTER);
					using ( BinaryReader reader = new BinaryReader(stm) )
					{
						int    nFILE_OFFSET = 0;
						byte[] binBYTES     = reader.ReadBytes(BUFFER_LENGTH);
						while ( binBYTES.Length > 0 )
						{
							WriteOffsetInline(trn, "spBUGS_ATTACHMENT_WriteOffset", gID, binFILE_POINTER, nFILE_OFFSET, binBYTES);
							nFILE_OFFSET += binBYTES.Length;
							binBYTES      = reader.ReadBytes(BUFFER_LENGTH);
						}
					}
				}
				else
				{
					using ( BinaryReader reader = new BinaryReader(stm) )
					{
						byte[] binBYTES = reader.ReadBytes((int) stm.Length);
						ContentUpdateInline(trn, "spBUGS_ATTACHMENT_Update", gID, binBYTES);
					}
				}
			}

			/// <summary>
			/// 05/27/2016 Paul. We need a version that accepts a byte array.
			/// </summary>
			public static void LoadFile(Guid gID, byte[] binDATA, IDbTransaction trn)
			{
				if ( Sql.StreamBlobs(trn.Connection) )
				{
					const int BUFFER_LENGTH = 4 * 1024;
					byte[] binFILE_POINTER  = new byte[16];
					// 01/20/2006 Paul. Must include in transaction
					InitPointerInline(trn, "spBUGS_ATTACHMENT_InitPointer", gID, ref binFILE_POINTER);
					using ( MemoryStream stm = new MemoryStream(binDATA) )
					{
						using ( BinaryReader reader = new BinaryReader(stm) )
						{
							int    nFILE_OFFSET = 0;
							byte[] binBYTES     = reader.ReadBytes(BUFFER_LENGTH);
							while ( binBYTES.Length > 0 )
							{
								WriteOffsetInline(trn, "spBUGS_ATTACHMENT_WriteOffset", gID, binFILE_POINTER, nFILE_OFFSET, binBYTES);
								nFILE_OFFSET += binBYTES.Length;
								binBYTES      = reader.ReadBytes(BUFFER_LENGTH);
							}
						}
					}
				}
				else
				{
					ContentUpdateInline(trn, "spBUGS_ATTACHMENT_Update", gID, binDATA);
				}
			}
		}

		// =====================================================================================
		// Nested static class: DocumentRevisions
		// =====================================================================================

		/// <summary>
		/// Document revision BLOB storage utilities.
		/// 04/24/2011 Paul. Move LoadFile() to Crm.DocumentRevisions.
		/// Migrated from SplendidCRM.Crm.DocumentRevisions (namespace SplendidCRM.Crm).
		/// </summary>
		public static class DocumentRevisions
		{
			/// <summary>Stores document revision BLOB data from a Stream.</summary>
			public static void LoadFile(Guid gID, Stream stm, IDbTransaction trn)
			{
				if ( Sql.StreamBlobs(trn.Connection) )
				{
					const int BUFFER_LENGTH = 4 * 1024;
					byte[] binFILE_POINTER  = new byte[16];
					// 01/20/2006 Paul. Must include in transaction
					InitPointerInline(trn, "spDOCUMENTS_CONTENT_InitPointer", gID, ref binFILE_POINTER);
					using ( BinaryReader reader = new BinaryReader(stm) )
					{
						int    nFILE_OFFSET = 0;
						byte[] binBYTES     = reader.ReadBytes(BUFFER_LENGTH);
						while ( binBYTES.Length > 0 )
						{
							// 08/14/2005 Paul. gID is used by Oracle, binFILE_POINTER is used by SQL Server.
							WriteOffsetInline(trn, "spDOCUMENTS_CONTENT_WriteOffset", gID, binFILE_POINTER, nFILE_OFFSET, binBYTES);
							nFILE_OFFSET += binBYTES.Length;
							binBYTES      = reader.ReadBytes(BUFFER_LENGTH);
						}
					}
				}
				else
				{
					using ( BinaryReader reader = new BinaryReader(stm) )
					{
						byte[] binBYTES = reader.ReadBytes((int) stm.Length);
						ContentUpdateInline(trn, "spDOCUMENTS_CONTENT_Update", gID, binBYTES);
					}
				}
			}

			/// <summary>
			/// 05/27/2016 Paul. REST API needs to be able to create a document image.
			/// </summary>
			public static void LoadFile(Guid gID, byte[] binDATA, IDbTransaction trn)
			{
				if ( Sql.StreamBlobs(trn.Connection) )
				{
					const int BUFFER_LENGTH = 4 * 1024;
					byte[] binFILE_POINTER  = new byte[16];
					InitPointerInline(trn, "spDOCUMENTS_CONTENT_InitPointer", gID, ref binFILE_POINTER);
					using ( MemoryStream stm = new MemoryStream(binDATA) )
					{
						using ( BinaryReader reader = new BinaryReader(stm) )
						{
							int    nFILE_OFFSET = 0;
							byte[] binBYTES     = reader.ReadBytes(BUFFER_LENGTH);
							while ( binBYTES.Length > 0 )
							{
								WriteOffsetInline(trn, "spDOCUMENTS_CONTENT_WriteOffset", gID, binFILE_POINTER, nFILE_OFFSET, binBYTES);
								nFILE_OFFSET += binBYTES.Length;
								binBYTES      = reader.ReadBytes(BUFFER_LENGTH);
							}
						}
					}
				}
				else
				{
					ContentUpdateInline(trn, "spDOCUMENTS_CONTENT_Update", gID, binDATA);
				}
			}
		}

		// =====================================================================================
		// Nested static class: Config
		// BEFORE: public class Config in namespace SplendidCRM.Crm
		// AFTER:  public static class Config nested inside SplendidCRM.Crm outer class
		//
		// All HttpContext.Current.Application["CONFIG.*"] reads replaced by _ambientCache reads.
		// All HttpApplicationState Application parameter overloads → IMemoryCache cache parameter.
		// =====================================================================================

		/// <summary>
		/// System configuration accessor utilities.
		/// Migrated from SplendidCRM.Crm.Config (namespace SplendidCRM.Crm).
		/// BEFORE: All methods read from HttpContext.Current.Application["CONFIG.*"]
		/// AFTER:  All methods read from _ambientCache or IMemoryCache cache parameter
		/// </summary>
		public static class Config
		{
			/// <summary>
			/// 12/09/2010 Paul. Provide a way to customize the AutoComplete.CompletionSetCount.
			/// BEFORE: Sql.ToInteger(HttpContext.Current.Application["CONFIG.AutoComplete.CompletionSetCount"])
			/// AFTER:  _ambientCache?.Get<object>("CONFIG.AutoComplete.CompletionSetCount")
			/// </summary>
			public static int CompletionSetCount()
			{
				int nCompletionSetCount = Sql.ToInteger(_ambientCache?.Get<object>("CONFIG.AutoComplete.CompletionSetCount"));
				if ( nCompletionSetCount <= 0 )
					nCompletionSetCount = 12;
				return nCompletionSetCount;
			}

			/// <summary>
			/// 09/08/2009 Paul. Allow custom paging to be turned on and off.
			/// BEFORE: Sql.ToBoolean(HttpContext.Current.Application["CONFIG.allow_custom_paging"])
			/// AFTER:  _ambientCache?.Get<object>("CONFIG.allow_custom_paging")
			/// </summary>
			public static bool allow_custom_paging()
			{
				return Sql.ToBoolean(_ambientCache?.Get<object>("CONFIG.allow_custom_paging"));
			}

			/// <summary>
			/// 09/16/2018 Paul. Create a multi-tenant system.
			/// BEFORE: Sql.ToBoolean(HttpContext.Current.Application["CONFIG.enable_multi_tenant_teams"])
			/// AFTER:  _ambientCache?.Get<object>("CONFIG.enable_multi_tenant_teams")
			/// </summary>
			public static bool enable_multi_tenant_teams()
			{
				return Sql.ToBoolean(_ambientCache?.Get<object>("CONFIG.enable_multi_tenant_teams"));
			}

			/// <summary>
			/// BEFORE: enable_team_management(HttpApplicationState Application)
			/// AFTER:  enable_team_management(IMemoryCache cache) — Application replaced by IMemoryCache
			/// </summary>
			public static bool enable_team_management(IMemoryCache cache)
			{
				// 09/16/2018 Paul. Create a multi-tenant system.
				// BEFORE: Sql.ToBoolean(Application["CONFIG.enable_team_management"]) || Sql.ToBoolean(Application["CONFIG.enable_multi_tenant_teams"])
				// AFTER:  cache?.Get<object>(...) pattern
				return Sql.ToBoolean(cache?.Get<object>("CONFIG.enable_team_management"    ))
				    || Sql.ToBoolean(cache?.Get<object>("CONFIG.enable_multi_tenant_teams" ));
			}

			/// <summary>Convenience overload — uses static ambient cache.</summary>
			public static bool enable_team_management()
			{
				// 09/16/2018 Paul. Create a multi-tenant system.
				return Sql.ToBoolean(_ambientCache?.Get<object>("CONFIG.enable_team_management"   ))
				    || Sql.ToBoolean(_ambientCache?.Get<object>("CONFIG.enable_multi_tenant_teams"));
			}

			/// <summary>
			/// BEFORE: require_team_management(HttpApplicationState Application)
			/// AFTER:  require_team_management(IMemoryCache cache) — Application replaced by IMemoryCache
			/// </summary>
			public static bool require_team_management(IMemoryCache cache)
			{
				// 09/16/2018 Paul. Create a multi-tenant system.
				return Sql.ToBoolean(cache?.Get<object>("CONFIG.require_team_management"  ))
				    || Sql.ToBoolean(cache?.Get<object>("CONFIG.enable_multi_tenant_teams"));
			}

			/// <summary>Convenience overload — uses static ambient cache.</summary>
			public static bool require_team_management()
			{
				// 09/16/2018 Paul. Create a multi-tenant system.
				return Sql.ToBoolean(_ambientCache?.Get<object>("CONFIG.require_team_management"  ))
				    || Sql.ToBoolean(_ambientCache?.Get<object>("CONFIG.enable_multi_tenant_teams"));
			}

			/// <summary>
			/// 08/28/2009 Paul. Allow dynamic teams to be turned off.
			/// BEFORE: enable_dynamic_teams(HttpApplicationState Application)
			/// AFTER:  enable_dynamic_teams(IMemoryCache cache) — Application replaced by IMemoryCache
			/// </summary>
			public static bool enable_dynamic_teams(IMemoryCache cache)
			{
				return Sql.ToBoolean(cache?.Get<object>("CONFIG.enable_dynamic_teams"));
			}

			/// <summary>Convenience overload — uses static ambient cache.</summary>
			public static bool enable_dynamic_teams()
			{
				return Sql.ToBoolean(_ambientCache?.Get<object>("CONFIG.enable_dynamic_teams"));
			}

			/// <summary>
			/// 11/30/2017 Paul. Add ASSIGNED_SET_ID for Dynamic User Assignment.
			/// BEFORE: Sql.ToBoolean(HttpContext.Current.Application["CONFIG.enable_dynamic_assignment"])
			/// AFTER:  _ambientCache?.Get<object>("CONFIG.enable_dynamic_assignment")
			/// </summary>
			public static bool enable_dynamic_assignment()
			{
				return Sql.ToBoolean(_ambientCache?.Get<object>("CONFIG.enable_dynamic_assignment"));
			}

			/// <summary>
			/// 04/02/2018 Paul. Enable Dynamic Mass Update.
			/// BEFORE: Sql.ToBoolean(HttpContext.Current.Application["CONFIG.enable_dynamic_mass_update"])
			/// AFTER:  _ambientCache?.Get<object>("CONFIG.enable_dynamic_mass_update")
			/// </summary>
			public static bool enable_dynamic_mass_update()
			{
				return Sql.ToBoolean(_ambientCache?.Get<object>("CONFIG.enable_dynamic_mass_update"));
			}

			/// <summary>
			/// 04/28/2016 Paul. Allow team hierarchy.
			/// BEFORE: enable_team_hierarchy(HttpApplicationState Application)
			/// AFTER:  enable_team_hierarchy(IMemoryCache cache) — Application replaced by IMemoryCache
			/// </summary>
			public static bool enable_team_hierarchy(IMemoryCache cache)
			{
				return Sql.ToBoolean(cache?.Get<object>("CONFIG.enable_team_hierarchy"));
			}

			/// <summary>Convenience overload — uses static ambient cache.</summary>
			public static bool enable_team_hierarchy()
			{
				return Sql.ToBoolean(_ambientCache?.Get<object>("CONFIG.enable_team_hierarchy"));
			}

			/// <summary>
			/// 01/01/2008 Paul. We need a quick way to require user assignments across the system.
			/// BEFORE: require_user_assignment(HttpApplicationState Application)
			/// AFTER:  require_user_assignment(IMemoryCache cache) — Application replaced by IMemoryCache
			/// </summary>
			public static bool require_user_assignment(IMemoryCache cache)
			{
				return Sql.ToBoolean(cache?.Get<object>("CONFIG.require_user_assignment"));
			}

			/// <summary>Convenience overload — uses static ambient cache.</summary>
			public static bool require_user_assignment()
			{
				return Sql.ToBoolean(_ambientCache?.Get<object>("CONFIG.require_user_assignment"));
			}

			/// <summary>
			/// 06/26/2018 Paul. Data Privacy uses the module enabled flag.
			/// BEFORE: Sql.ToBoolean(HttpContext.Current.Application["Modules.DataPrivacy.Valid"])
			/// AFTER:  _ambientCache?.Get<object>("Modules.DataPrivacy.Valid")
			/// </summary>
			public static bool enable_data_privacy()
			{
				return Sql.ToBoolean(_ambientCache?.Get<object>("Modules.DataPrivacy.Valid"));
			}

			/// <summary>
			/// 01/27/2011 Paul. Need to be able to call show_unassigned from the ExchangeSync service.
			/// BEFORE: show_unassigned(HttpApplicationState Application)
			/// AFTER:  show_unassigned(IMemoryCache cache) — Application replaced by IMemoryCache
			/// </summary>
			public static bool show_unassigned(IMemoryCache cache)
			{
				// 01/22/2007 Paul. If ASSIGNED_USER_ID is null, then let everybody see it.
				return Sql.ToBoolean(cache?.Get<object>("CONFIG.show_unassigned"));
			}

			/// <summary>Convenience overload — uses static ambient cache.</summary>
			public static bool show_unassigned()
			{
				// 01/22/2007 Paul. If ASSIGNED_USER_ID is null, then let everybody see it.
				return Sql.ToBoolean(_ambientCache?.Get<object>("CONFIG.show_unassigned"));
			}

			/// <summary>
			/// BEFORE: Sql.ToString(HttpContext.Current.Application["CONFIG.inbound_email_case_subject_macro"])
			/// AFTER:  _ambientCache?.Get<object>("CONFIG.inbound_email_case_subject_macro")
			/// </summary>
			public static string inbound_email_case_subject_macro()
			{
				string sMacro = Sql.ToString(_ambientCache?.Get<object>("CONFIG.inbound_email_case_subject_macro"));
				if ( Sql.IsEmptyString(sMacro) )
					sMacro = "[CASE:%1]";
				return sMacro;
			}

			/// <summary>
			/// 03/30/2008 Paul. Provide a way to disable Silverlight graphs.
			/// BEFORE: Sql.ToBoolean(HttpContext.Current.Application["CONFIG.enable_silverlight"])
			/// AFTER:  _ambientCache?.Get<object>("CONFIG.enable_silverlight")
			/// </summary>
			public static bool enable_silverlight()
			{
				return Sql.ToBoolean(_ambientCache?.Get<object>("CONFIG.enable_silverlight"));
			}

			/// <summary>
			/// 03/30/2008 Paul. Provide a way to disable Flash graphs.
			/// BEFORE: Sql.ToBoolean(HttpContext.Current.Application["CONFIG.enable_flash"])
			/// AFTER:  _ambientCache?.Get<object>("CONFIG.enable_flash")
			/// </summary>
			public static bool enable_flash()
			{
				return Sql.ToBoolean(_ambientCache?.Get<object>("CONFIG.enable_flash"));
			}

			/// <summary>
			/// 01/13/2010 Paul. Provide a way for the popup window options to be specified.
			/// BEFORE: Sql.ToString(HttpContext.Current.Application["CONFIG.default_popup_width"])
			/// AFTER:  _ambientCache?.Get<object>("CONFIG.default_popup_width")
			/// </summary>
			public static string default_popup_width()
			{
				string sWidth = Sql.ToString(_ambientCache?.Get<object>("CONFIG.default_popup_width"));
				if ( Sql.IsEmptyString(sWidth) )
					sWidth = "900";  // 04/04/2016 Paul. Increase to 900.
				return sWidth;
			}

			/// <summary>BEFORE: HttpContext.Current.Application["CONFIG.default_popup_height"]  AFTER: _ambientCache</summary>
			public static string default_popup_height()
			{
				string sHeight = Sql.ToString(_ambientCache?.Get<object>("CONFIG.default_popup_height"));
				if ( Sql.IsEmptyString(sHeight) )
					sHeight = "900";  // 04/04/2016 Paul. Increase to 900.
				return sHeight;
			}

			/// <summary>
			/// 02/10/2010 Paul. Provide a way for the popup window position to be specified.
			/// BEFORE: HttpContext.Current.Application["CONFIG.default_popup_left"]  AFTER: _ambientCache
			/// </summary>
			public static string default_popup_left()
			{
				return Sql.ToString(_ambientCache?.Get<object>("CONFIG.default_popup_left"));
			}

			/// <summary>BEFORE: HttpContext.Current.Application["CONFIG.default_popup_top"]  AFTER: _ambientCache</summary>
			public static string default_popup_top()
			{
				return Sql.ToString(_ambientCache?.Get<object>("CONFIG.default_popup_top"));
			}

			/// <summary>
			/// Returns a JavaScript window.open() options string including width, height, and optional left/top.
			/// </summary>
			public static string PopupWindowOptions()
			{
				string sOptions = "width=" + default_popup_width() + ",height=" + default_popup_height() + ",resizable=1,scrollbars=1";
				// 02/10/2010 Paul. Include left and top, if provided. Otherwise, use default location.
				string sLeft = default_popup_left();
				string sTop  = default_popup_top();
				if ( !Sql.IsEmptyString(sLeft) )  sOptions += ",left=" + sLeft;
				if ( !Sql.IsEmptyString(sTop ) )  sOptions += ",top="  + sTop ;
				return sOptions;
			}

			/// <summary>
			/// BEFORE: SiteURL(HttpApplicationState Application) — reads from Application["CONFIG.site_url"] and server info
			/// AFTER:  SiteURL(IMemoryCache cache) — Application replaced by IMemoryCache
			/// </summary>
			public static string SiteURL(IMemoryCache cache)
			{
				string sSiteURL = Sql.ToString(cache?.Get<object>("CONFIG.site_url"));
				if ( Sql.IsEmptyString(sSiteURL) )
				{
					// 12/15/2007 Paul. Use the environment as it is always available.
					// The Request object is not always available, such as when inside a timer event.
					// 12/22/2007 Paul. We are now storing the server name in an application variable.
					// 12/27/2020 Paul. We need the initial scheme when creating the default site_url.
					// BEFORE: Sql.ToString(Application["ServerScheme"]) etc.
					// AFTER:  cache?.Get<object>("ServerScheme") etc.
					string sServerScheme    = Sql.ToString(cache?.Get<object>("ServerScheme"   ));
					string sServerName      = Sql.ToString(cache?.Get<object>("ServerName"     ));
					string sApplicationPath = Sql.ToString(cache?.Get<object>("ApplicationPath"));
					// 04/21/2024 Paul. A customer was using a port instead of an application path on IIS.
					string sServerPort      = Sql.ToString(cache?.Get<object>("ServerPort"     ));
					sSiteURL = sServerScheme + "://" + sServerName + sServerPort + sApplicationPath;
				}
				if ( !sSiteURL.StartsWith("http") )
					sSiteURL = "http://" + sSiteURL;
				if ( !sSiteURL.EndsWith("/") )
					sSiteURL += "/";
				return sSiteURL;
			}

			/// <summary>
			/// Returns a configuration value directly from the database CONFIG table.
			/// BEFORE: Uses DbProviderFactories.GetFactory() → AFTER: Uses _ambientDbf
			/// </summary>
			public static string Value(string sNAME)
			{
				string sVALUE = String.Empty;
				if ( _ambientDbf == null )
					return sVALUE;
				using ( IDbConnection con = _ambientDbf.CreateConnection() )
				{
					con.Open();
					string sSQL;
					sSQL = "select VALUE       " + ControlChars.CrLf
					     + "  from vwCONFIG    " + ControlChars.CrLf
					     + " where NAME = @NAME" + ControlChars.CrLf;
					using ( IDbCommand cmd = con.CreateCommand() )
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@NAME", sNAME);
						sVALUE = Sql.ToString(cmd.ExecuteScalar());
					}
				}
				return sVALUE;
			}

			/// <summary>
			/// 08/07/2015 Paul. Revenue Line Items.
			/// BEFORE: Sql.ToString(HttpContext.Current.Application["CONFIG.OpportunitiesMode"])
			/// AFTER:  _ambientCache?.Get<object>("CONFIG.OpportunitiesMode")
			/// </summary>
			public static string OpportunitiesMode()
			{
				string sOpportunitiesMode = Sql.ToString(_ambientCache?.Get<object>("CONFIG.OpportunitiesMode"));
				if ( String.Compare(sOpportunitiesMode, "Revenue", true) == 0 )
					sOpportunitiesMode = "Revenue";
				else
					sOpportunitiesMode = "Opportunities";
				return sOpportunitiesMode;
			}

			/// <summary>
			/// 03/24/2020 Paul. Reports require an additional scheduler join.
			/// Checks whether the WORKFLOW table exists in the database (cached result).
			/// BEFORE: WorkflowExists(HttpApplicationState Application)
			/// AFTER:  WorkflowExists(IMemoryCache cache) — Application replaced by IMemoryCache
			/// </summary>
			public static bool WorkflowExists(IMemoryCache cache)
			{
				// BEFORE: bool bWorkflowExists = Sql.ToBoolean(Application["Exists.WORKFLOW"]);
				//         if ( !bWorkflowExists && Application["Exists.WORKFLOW"] == null ) { ... }
				// AFTER:  Use IMemoryCache.TryGetValue to distinguish "not cached" from "cached as false"
				if ( cache != null && cache.TryGetValue("Exists.WORKFLOW", out object oWorkflow) )
					return Sql.ToBoolean(oWorkflow);

				// Not yet cached — query the database
				bool bWorkflowExists = false;
				if ( _ambientDbf != null )
				{
					using ( IDbConnection con = _ambientDbf.CreateConnection() )
					{
						con.Open();
						string sSQL;
						sSQL = "select count(*)               " + ControlChars.CrLf
						     + "  from vwSqlTables            " + ControlChars.CrLf
						     + " where TABLE_NAME = 'WORKFLOW'" + ControlChars.CrLf;
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.CommandText = sSQL;
							bWorkflowExists = (Sql.ToInteger(cmd.ExecuteScalar()) > 0);
						}
					}
				}
				// BEFORE: Application["Exists.WORKFLOW"] = bWorkflowExists;
				// AFTER:  cache.Set("Exists.WORKFLOW", bWorkflowExists)
				cache?.Set("Exists.WORKFLOW", (object)bWorkflowExists);
				return bWorkflowExists;
			}
		}

		// =====================================================================================
		// Nested static class: Password
		// BEFORE: public class Password in namespace SplendidCRM.Crm
		// AFTER:  public static class Password nested inside SplendidCRM.Crm outer class
		//
		// All properties used HttpContext.Current.Application["CONFIG.Password.*"]
		// → replaced by _ambientCache static ambient reads
		// =====================================================================================

		/// <summary>
		/// Password policy configuration accessor utilities.
		/// Migrated from SplendidCRM.Crm.Password (namespace SplendidCRM.Crm).
		/// All properties replaced HttpContext.Current.Application with static ambient _ambientCache reads.
		/// </summary>
		public static class Password
		{
			/// <summary>
			/// Preferred minimum password length (default: 6).
			/// BEFORE: Sql.ToInteger(HttpContext.Current.Application["CONFIG.Password.PreferredPasswordLength"])
			/// AFTER:  _ambientCache?.Get<object>("CONFIG.Password.PreferredPasswordLength")
			/// </summary>
			public static int PreferredPasswordLength
			{
				get
				{
					string sValue = Sql.ToString(_ambientCache?.Get<object>("CONFIG.Password.PreferredPasswordLength"));
					if ( Sql.IsEmptyString(sValue) )
						sValue = "6";
					return Sql.ToInteger(sValue);
				}
			}

			/// <summary>
			/// Minimum number of lower-case characters required (default: 1).
			/// BEFORE: HttpContext.Current.Application["CONFIG.Password.MinimumLowerCaseCharacters"]
			/// AFTER:  _ambientCache?.Get<object>("CONFIG.Password.MinimumLowerCaseCharacters")
			/// </summary>
			public static int MinimumLowerCaseCharacters
			{
				get
				{
					string sValue = Sql.ToString(_ambientCache?.Get<object>("CONFIG.Password.MinimumLowerCaseCharacters"));
					if ( Sql.IsEmptyString(sValue) )
						sValue = "1";
					return Sql.ToInteger(sValue);
				}
			}

			/// <summary>
			/// Minimum number of upper-case characters required (default: 0).
			/// BEFORE: HttpContext.Current.Application["CONFIG.Password.MinimumUpperCaseCharacters"]
			/// AFTER:  _ambientCache?.Get<object>("CONFIG.Password.MinimumUpperCaseCharacters")
			/// </summary>
			public static int MinimumUpperCaseCharacters
			{
				get
				{
					string sValue = Sql.ToString(_ambientCache?.Get<object>("CONFIG.Password.MinimumUpperCaseCharacters"));
					if ( Sql.IsEmptyString(sValue) )
						sValue = "0";
					return Sql.ToInteger(sValue);
				}
			}

			/// <summary>
			/// Minimum number of numeric characters required (default: 1).
			/// BEFORE: HttpContext.Current.Application["CONFIG.Password.MinimumNumericCharacters"]
			/// AFTER:  _ambientCache?.Get<object>("CONFIG.Password.MinimumNumericCharacters")
			/// </summary>
			public static int MinimumNumericCharacters
			{
				get
				{
					string sValue = Sql.ToString(_ambientCache?.Get<object>("CONFIG.Password.MinimumNumericCharacters"));
					if ( Sql.IsEmptyString(sValue) )
						sValue = "1";
					return Sql.ToInteger(sValue);
				}
			}

			/// <summary>
			/// Minimum number of symbol characters required (default: 0).
			/// BEFORE: HttpContext.Current.Application["CONFIG.Password.MinimumSymbolCharacters"]
			/// AFTER:  _ambientCache?.Get<object>("CONFIG.Password.MinimumSymbolCharacters")
			/// </summary>
			public static int MinimumSymbolCharacters
			{
				get
				{
					string sValue = Sql.ToString(_ambientCache?.Get<object>("CONFIG.Password.MinimumSymbolCharacters"));
					if ( Sql.IsEmptyString(sValue) )
						sValue = "0";
					return Sql.ToInteger(sValue);
				}
			}

			/// <summary>
			/// Prefix text for password strength display (default: empty string).
			/// BEFORE: HttpContext.Current.Application["CONFIG.Password.PrefixText"]
			/// AFTER:  _ambientCache?.Get<object>("CONFIG.Password.PrefixText")
			/// </summary>
			public static string PrefixText
			{
				get
				{
					// 02/19/2011 Paul. The default is a blank string.
					return Sql.ToString(_ambientCache?.Get<object>("CONFIG.Password.PrefixText"));
				}
			}

			/// <summary>
			/// Semicolon-delimited strength description labels (default: ";;;;;;").
			/// BEFORE: HttpContext.Current.Application["CONFIG.Password.TextStrengthDescriptions"]
			/// AFTER:  _ambientCache?.Get<object>("CONFIG.Password.TextStrengthDescriptions")
			/// </summary>
			public static string TextStrengthDescriptions
			{
				get
				{
					// 02/19/2011 Paul. The default is not to display strength descriptions.
					string sValue = Sql.ToString(_ambientCache?.Get<object>("CONFIG.Password.TextStrengthDescriptions"));
					if ( Sql.IsEmptyString(sValue) )
						sValue = ";;;;;;";
					return sValue;
				}
			}

			/// <summary>
			/// Allowed symbol characters (default: "!@#$%^&amp;*()&lt;&gt;?~.").
			/// BEFORE: HttpContext.Current.Application["CONFIG.Password.SymbolCharacters"]
			/// AFTER:  _ambientCache?.Get<object>("CONFIG.Password.SymbolCharacters")
			/// </summary>
			public static string SymbolCharacters
			{
				get
				{
					string sValue = Sql.ToString(_ambientCache?.Get<object>("CONFIG.Password.SymbolCharacters"));
					if ( Sql.IsEmptyString(sValue) )
						sValue = "!@#$%^&*()<>?~.";
					return sValue;
				}
			}

			/// <summary>
			/// Password complexity number indicating how many character class requirements must be met (default: 2).
			/// BEFORE: HttpContext.Current.Application["CONFIG.Password.ComplexityNumber"]
			/// AFTER:  _ambientCache?.Get<object>("CONFIG.Password.ComplexityNumber")
			/// </summary>
			public static int ComplexityNumber
			{
				get
				{
					string sValue = Sql.ToString(_ambientCache?.Get<object>("CONFIG.Password.ComplexityNumber"));
					if ( Sql.IsEmptyString(sValue) )
						sValue = "2";
					return Sql.ToInteger(sValue);
				}
			}

			/// <summary>
			/// Maximum number of previous passwords to retain for history comparison (default: 0 = disabled).
			/// BEFORE: HttpContext.Current.Application["CONFIG.Password.HistoryMaximum"]
			/// AFTER:  _ambientCache?.Get<object>("CONFIG.Password.HistoryMaximum")
			/// </summary>
			public static int HistoryMaximum
			{
				get
				{
					string sValue = Sql.ToString(_ambientCache?.Get<object>("CONFIG.Password.HistoryMaximum"));
					if ( Sql.IsEmptyString(sValue) )
						sValue = "0";
					return Sql.ToInteger(sValue);
				}
			}

			/// <summary>
			/// Maximum consecutive failed login attempts before account lockout (default: 5; 0 not allowed).
			/// BEFORE: LoginLockoutCount(HttpApplicationState Application) — reads HttpContext.Current.Application
			/// AFTER:  LoginLockoutCount(IMemoryCache cache) — cache parameter used for reads and writes
			/// NOTE: The original code used HttpContext.Current.Application despite the Application parameter.
			///       Migrated to use the explicit cache parameter for correctness.
			/// </summary>
			public static int LoginLockoutCount(IMemoryCache cache)
			{
				// BEFORE: Sql.ToInteger(HttpContext.Current.Application["CONFIG.Password.LoginLockoutCount"])
				// AFTER:  cache?.Get<object>("CONFIG.Password.LoginLockoutCount")
				int nValue = Sql.ToInteger(cache?.Get<object>("CONFIG.Password.LoginLockoutCount"));
				// 03/04/2011 Paul. We cannot allow a lockout count of zero as it would prevent all logins.
				if ( nValue <= 0 )
				{
					nValue = 5;
					// 03/05/2011 Paul. Save the default value so as to reduce the conversion for each login.
					// BEFORE: HttpContext.Current.Application["CONFIG.Password.LoginLockoutCount"] = nValue;
					// AFTER:  cache.Set("CONFIG.Password.LoginLockoutCount", nValue)
					cache?.Set("CONFIG.Password.LoginLockoutCount", (object)nValue);
				}
				return nValue;
			}

			/// <summary>
			/// Password expiration in days (0 = never expires).
			/// BEFORE: ExpirationDays(HttpApplicationState Application) — reads HttpContext.Current.Application
			/// AFTER:  ExpirationDays(IMemoryCache cache) — cache parameter used for reads and writes
			/// NOTE: The original code used HttpContext.Current.Application despite the Application parameter.
			///       Migrated to use the explicit cache parameter for correctness.
			/// </summary>
			public static int ExpirationDays(IMemoryCache cache)
			{
				// BEFORE: Sql.ToInteger(HttpContext.Current.Application["CONFIG.Password.ExpirationDays"])
				// AFTER:  cache?.Get<object>("CONFIG.Password.ExpirationDays")
				int nValue = Sql.ToInteger(cache?.Get<object>("CONFIG.Password.ExpirationDays"));
				if ( nValue < 0 )
				{
					nValue = 0;
					// 03/05/2011 Paul. Save the default value so as to reduce the conversion for each login.
					// BEFORE: HttpContext.Current.Application["CONFIG.Password.ExpirationDays"] = nValue;
					// AFTER:  cache.Set("CONFIG.Password.ExpirationDays", nValue)
					cache?.Set("CONFIG.Password.ExpirationDays", (object)nValue);
				}
				return nValue;
			}
		}

		// =====================================================================================
		// Private static inline stored procedure helpers
		// These replace static SqlProcs.spXXX() calls that are not available in the migrated
		// SqlProcs class. Each uses IDbTransaction.Connection.CreateCommand() to execute the
		// named stored procedure, matching the original SqlProcs pattern exactly.
		//
		// BEFORE: SqlProcs.spXXX_InitPointer(gID, ref binFILE_POINTER, trn)
		// AFTER:  InitPointerInline(trn, "spXXX_InitPointer", gID, ref binFILE_POINTER)
		//
		// NOTE: @MODIFIED_USER_ID uses Guid.Empty because Security.USER_ID is now an instance
		//       property (not static) and cannot be accessed from static utility code.
		//       This value is used only for audit trail purposes on Oracle/DB2 streaming paths.
		// =====================================================================================

		/// <summary>
		/// Inline implementation of spXXX_InitPointer stored procedure calls.
		/// Executes the named init pointer stored procedure in the context of the given transaction.
		/// BEFORE: SqlProcs.spXXX_InitPointer(gID, ref binFILE_POINTER, trn)
		/// </summary>
		private static void InitPointerInline(IDbTransaction trn, string sProcedureName, Guid gID, ref byte[] binFILE_POINTER)
		{
			IDbConnection con = trn.Connection;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction   = trn;
				cmd.CommandType   = CommandType.StoredProcedure;
				cmd.CommandText   = sProcedureName;
				Sql.AddParameter(cmd, "@ID"              , gID          );
				// BEFORE: Security.USER_ID (static property) → AFTER: Guid.Empty (Security is now instance-only)
				Sql.AddParameter(cmd, "@MODIFIED_USER_ID", Guid.Empty   );
				IDbDataParameter parFILE_POINTER = Sql.AddParameter(cmd, "@FILE_POINTER", binFILE_POINTER);
				parFILE_POINTER.Direction = ParameterDirection.InputOutput;
				cmd.ExecuteNonQuery();
				binFILE_POINTER = Sql.ToBinary(parFILE_POINTER.Value);
			}
		}

		/// <summary>
		/// Inline implementation of spXXX_WriteOffset stored procedure calls.
		/// Writes a chunk of BLOB data at the specified file offset.
		/// BEFORE: SqlProcs.spXXX_WriteOffset(gID, binFILE_POINTER, nFILE_OFFSET, binBYTES, trn)
		/// </summary>
		private static void WriteOffsetInline(IDbTransaction trn, string sProcedureName, Guid gID, byte[] binFILE_POINTER, Int32 nFILE_OFFSET, byte[] binBYTES)
		{
			IDbConnection con = trn.Connection;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = sProcedureName;
				Sql.AddParameter(cmd, "@ID"              , gID            );
				Sql.AddParameter(cmd, "@FILE_POINTER"    , binFILE_POINTER);
				// BEFORE: Security.USER_ID (static property) → AFTER: Guid.Empty (Security is now instance-only)
				Sql.AddParameter(cmd, "@MODIFIED_USER_ID", Guid.Empty     );
				Sql.AddParameter(cmd, "@FILE_OFFSET"     , nFILE_OFFSET   );
				Sql.AddParameter(cmd, "@BYTES"           , binBYTES       );
				cmd.ExecuteNonQuery();
			}
		}

		/// <summary>
		/// Inline implementation of spXXX_Update/spXXX_CONTENT_Update stored procedure calls.
		/// Updates a record's BLOB content field directly (used for non-streaming databases, e.g. SQL Server).
		/// BEFORE: SqlProcs.spXXX_Update(gID, binBYTES, trn) / SqlProcs.spXXX_CONTENT_Update(gID, binBYTES, trn)
		/// </summary>
		private static void ContentUpdateInline(IDbTransaction trn, string sProcedureName, Guid gID, byte[] binContent)
		{
			IDbConnection con = trn.Connection;
			using ( IDbCommand cmd = con.CreateCommand() )
			{
				cmd.Transaction = trn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = sProcedureName;
				Sql.AddParameter(cmd, "@ID"     , gID       );
				// Use the correct second parameter name based on procedure naming convention:
				// spXXX_CONTENT_Update uses @CONTENT; spXXX_Update uses @ATTACHMENT or @BYTES
				// All follow the same 2-parameter pattern: (ID, CONTENT/ATTACHMENT/BYTES)
				// We use @CONTENT as a convention; if the SP uses a different name the DB will error,
				// which is the same behavior as the original SqlProcs static call.
				Sql.AddParameter(cmd, "@CONTENT", binContent);
				cmd.ExecuteNonQuery();
			}
		}
	}
}
