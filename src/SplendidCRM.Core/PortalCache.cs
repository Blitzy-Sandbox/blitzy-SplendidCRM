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
// .NET 10 Migration: SplendidCRM/_code/PortalCache.cs → src/SplendidCRM.Core/PortalCache.cs
// Changes applied:
//   - REMOVED: using System.Web; using System.Web.SessionState; using System.Web.Caching;
//              using System.Collections; using System.Security.Principal; using System.IO;
//   - ADDED:   using Microsoft.AspNetCore.Http; using Microsoft.Extensions.Caching.Memory;
//   - CONVERTED: Static class → DI-friendly instance class (same pattern as Security.cs, SplendidCache.cs)
//   - HttpContext.Current.Session["key"] as DataTable
//             → IMemoryCache.TryGetValue<DataTable>(key) guarded by ISession sentinel string
//     Rationale: DataTable cannot be directly serialized to distributed session (Redis/SQL Server);
//     IMemoryCache stores the DataTable per user; ISession stores a sentinel string per session to
//     correlate cache validity with session lifetime (sentinel absent after logout → re-query DB).
//   - Session["key"] = dt
//             → IMemoryCache.Set(key, dt, SlidingExpiration=20min) + ISession.SetString(key, "1")
//   - Security.USER_ID (static) → _security.USER_ID (instance property via DI)
//   - Security.IsAuthenticated() (static) → _security.IsAuthenticated() (instance method via DI)
//   - DbProviderFactories.GetFactory() (static) → _dbProviderFactories.GetFactory(_memoryCache) (instance)
//   - IsPortal() converted from static to instance method — behavior unchanged (returns false)
//   - DI constructor added: PortalCache(IHttpContextAccessor, IMemoryCache, Security, DbProviderFactories)
//   - Minimal change clause: Only changes necessary for .NET Framework 4.8 → .NET 10 ASP.NET Core
#nullable disable
using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	/// <summary>
	/// Portal-specific menu caching for SplendidCRM Community Edition.
	/// Provides tab and mobile navigation menus for portal (customer-facing) users,
	/// cached per authenticated user to avoid repeated database queries on every request.
	///
	/// Migrated from SplendidCRM/_code/PortalCache.cs for .NET 10 ASP.NET Core.
	///
	/// DESIGN NOTES for callers:
	///   • Register PortalCache as a SCOPED service (per-request lifetime) alongside Security.
	///   • DataTable results are cached in IMemoryCache (not distributed session) because
	///     DataTable cannot be directly serialized for Redis/SQL Server session backends.
	///   • ISession stores sentinel strings to correlate cache validity with session lifetime;
	///     when a session is cleared on logout, sentinels disappear and menus are re-queried.
	///   • Cache keys preserve the original key patterns:
	///       vwMODULES_PortalMenu_ByUser.{USER_ID}   (TabMenu)
	///       vwMODULES_MobileMenu_ByUser.{USER_ID}   (MobileMenu)
	///   • SlidingExpiration of 20 minutes matches the original Web.config session timeout.
	/// </summary>
	public class PortalCache
	{
		// =====================================================================================
		// DI fields — replacing HttpContext.Current, Application[], and static service calls
		// =====================================================================================

		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly IMemoryCache         _memoryCache        ;
		private readonly Security             _security           ;
		private readonly DbProviderFactories  _dbProviderFactories;

		/// <summary>
		/// Sliding expiration for menu DataTable entries in IMemoryCache.
		/// 20 minutes matches the original InProc session timeout from Web.config:
		///   &lt;sessionState timeout="20" /&gt;
		/// </summary>
		private static readonly TimeSpan MenuCacheDuration = TimeSpan.FromMinutes(20);

		// =====================================================================================
		// Constructor — DI injection replacing the .NET Framework 4.8 static access pattern
		// =====================================================================================

		/// <summary>
		/// Constructs a PortalCache instance with all required DI services.
		/// </summary>
		/// <param name="httpContextAccessor">
		/// Replaces HttpContext.Current throughout.
		/// Provides ISession for per-session sentinel tracking of cached menu items.
		/// BEFORE: HttpSessionState Session = HttpContext.Current.Session;
		/// AFTER:  ISession via _httpContextAccessor.HttpContext?.Session
		/// </param>
		/// <param name="memoryCache">
		/// Replaces Session["key"] as DataTable storage.
		/// DataTable cannot be serialized to distributed session (Redis/SQL Server), so
		/// IMemoryCache stores the DataTable per user with a 20-minute sliding expiration.
		/// BEFORE: Session["vwMODULES_PortalMenu_ByUser.{USER_ID}"] = dt;
		/// AFTER:  _memoryCache.Set(key, dt, SlidingExpiration=20min);
		/// </param>
		/// <param name="security">
		/// Replaces static Security.USER_ID property and Security.IsAuthenticated() method.
		/// Must be a SCOPED service so it reflects the current request's authenticated user.
		/// BEFORE: Security.USER_ID (static), Security.IsAuthenticated() (static)
		/// AFTER:  _security.USER_ID (instance), _security.IsAuthenticated() (instance)
		/// </param>
		/// <param name="dbProviderFactories">
		/// Replaces static DbProviderFactories.GetFactory() call.
		/// BEFORE: DbProviderFactory dbf = DbProviderFactories.GetFactory(); (static)
		/// AFTER:  DbProviderFactory dbf = _dbProviderFactories.GetFactory(_memoryCache); (instance)
		/// </param>
		public PortalCache(
			IHttpContextAccessor httpContextAccessor,
			IMemoryCache         memoryCache        ,
			Security             security           ,
			DbProviderFactories  dbProviderFactories)
		{
			_httpContextAccessor = httpContextAccessor;
			_memoryCache         = memoryCache        ;
			_security            = security           ;
			_dbProviderFactories = dbProviderFactories;
		}

		// =====================================================================================
		// Private helper — ISession access
		// BEFORE: HttpSessionState Session = HttpContext.Current.Session;
		// AFTER:  ISession via _httpContextAccessor.HttpContext?.Session (null-safe)
		// =====================================================================================

		/// <summary>
		/// Gets the ISession from the current HTTP context, or null when outside a request.
		/// Used to store/check sentinel strings that correlate menu cache validity with session lifetime.
		/// </summary>
		private ISession Session
		{
			get { return _httpContextAccessor?.HttpContext?.Session; }
		}

		// =====================================================================================
		// Public API
		// =====================================================================================

		/// <summary>
		/// Returns false — this SplendidCRM Community Edition deployment does not activate
		/// a portal-only product mode.
		/// Preserved from original static method; converted to instance method for DI consistency.
		/// BEFORE: public static bool IsPortal() { return false; }
		/// AFTER:  public bool IsPortal() { return false; }
		/// </summary>
		/// <returns>Always returns false.</returns>
		public bool IsPortal()
		{
			return false;
		}

		/// <summary>
		/// Returns the portal tab menu items for the current authenticated user.
		/// Results are cached in IMemoryCache per user (keyed by USER_ID), with an ISession
		/// sentinel string to detect session invalidation on logout.
		///
		/// BEFORE (.NET Framework 4.8):
		///   HttpSessionState Session = HttpContext.Current.Session;
		///   DataTable dt = Session["vwMODULES_PortalMenu_ByUser.{USER_ID}"] as DataTable;
		///   Session["vwMODULES_PortalMenu_ByUser.{USER_ID}"] = dt;
		///
		/// AFTER (.NET 10 ASP.NET Core):
		///   string sSentinel = Session?.GetString(sCacheKey);
		///   _memoryCache.TryGetValue&lt;DataTable&gt;(sCacheKey, out dt) — guarded by sentinel
		///   _memoryCache.Set(sCacheKey, dt, SlidingExpiration=20min)
		///   Session?.SetString(sCacheKey, "1")
		///
		/// 06/23/2010 Paul.  Allow tabs for Contracts, Quotes, Orders and Invoices.
		/// 09/13/2012 Paul.  Use a separate view so that Portal tabs can be different than CRM tabs.
		/// Modules included: Home, Bugs, Cases, KBDocuments, Contracts, Quotes, Orders, Invoices.
		/// </summary>
		/// <returns>
		/// DataTable with columns MODULE_NAME, DISPLAY_NAME, RELATIVE_PATH ordered by TAB_ORDER.
		/// Returns empty DataTable on DB error or when the user is not authenticated.
		/// </returns>
		public DataTable TabMenu()
		{
			// 04/28/2006 Paul.  The menu is now cached in the Session, so it will only get cleared when the user logs out.
			// 04/28/2006 Paul.  Include the GUID in the USER_ID so that the user does not have to log-out to get the correct menu.
			// BEFORE: HttpSessionState Session = HttpContext.Current.Session;
			// AFTER:  Use Session property (ISession via IHttpContextAccessor) for sentinel tracking.
			string sCacheKey = "vwMODULES_PortalMenu_ByUser." + _security.USER_ID.ToString();

			// BEFORE: DataTable dt = Session["vwMODULES_PortalMenu_ByUser." + Security.USER_ID.ToString()] as DataTable;
			// AFTER:  Two-phase lookup:
			//   1. Check ISession for sentinel string (proves this session loaded the menu).
			//   2. If sentinel present, retrieve DataTable from IMemoryCache.
			//   If either check fails (new session, cache eviction), re-query the database.
			string sSentinel = Session?.GetString(sCacheKey);
			DataTable dt     = null;
			if ( sSentinel != null )
			{
				_memoryCache.TryGetValue<DataTable>(sCacheKey, out dt);
			}

			if ( dt == null )
			{
				dt = new DataTable();
				try
				{
					// 11/17/2007 Paul.  New function to determine if user is authenticated.
					// BEFORE: if (Security.IsAuthenticated()) — static call on legacy static Security class
					// AFTER:  if (_security.IsAuthenticated()) — instance call on DI-injected Security
					if ( _security.IsAuthenticated() )
					{
						// BEFORE: DbProviderFactory dbf = DbProviderFactories.GetFactory(); — static call
						// AFTER:  DbProviderFactory dbf = _dbProviderFactories.GetFactory(_memoryCache); — instance call
						DbProviderFactory dbf = _dbProviderFactories.GetFactory(_memoryCache);
						using ( IDbConnection con = dbf.CreateConnection() )
						{
							con.Open();
							string sSQL;
							// 06/23/2010 Paul.  Allow tabs for Contracts, Quotes, Orders and Invoices.
							// This is still manual as the code must be added for a tab to function.
							// 09/13/2012 Paul.  Use a separate view so that Portal tabs can be different than CRM tabs.
							sSQL = "select MODULE_NAME                            " + ControlChars.CrLf
							     + "     , DISPLAY_NAME                           " + ControlChars.CrLf
							     + "     , RELATIVE_PATH                          " + ControlChars.CrLf
							     + "  from vwMODULES_PortalMenu_ByUser            " + ControlChars.CrLf
							     + " where (USER_ID = @USER_ID or USER_ID is null)" + ControlChars.CrLf
							     + "   and MODULE_NAME in (N'Home', N'Bugs', N'Cases', N'KBDocuments', N'Contracts', N'Quotes', N'Orders', N'Invoices')" + ControlChars.CrLf
							     + " order by TAB_ORDER                           " + ControlChars.CrLf;
							using ( IDbCommand cmd = con.CreateCommand() )
							{
								cmd.CommandText = sSQL;
								// BEFORE: Sql.AddParameter(cmd, "@USER_ID", Security.USER_ID);  — static Security.USER_ID
								// AFTER:  Sql.AddParameter(cmd, "@USER_ID", _security.USER_ID); — instance USER_ID
								Sql.AddParameter(cmd, "@USER_ID", _security.USER_ID);
								using ( DbDataAdapter da = dbf.CreateDataAdapter() )
								{
									((IDbDataAdapter)da).SelectCommand = cmd;
									da.Fill(dt);
									// BEFORE: Session["vwMODULES_PortalMenu_ByUser." + Security.USER_ID.ToString()] = dt;
									// AFTER:  IMemoryCache.Set for DataTable + ISession.SetString for sentinel.
									//         Sentinel ties cache validity to the current session lifetime:
									//         when the session is cleared (logout), sentinel is absent and
									//         the next request re-queries the database even if IMemoryCache
									//         still holds a stale entry for this USER_ID.
									_memoryCache.Set(sCacheKey, dt, new MemoryCacheEntryOptions
									{
										SlidingExpiration = MenuCacheDuration
									});
									Session?.SetString(sCacheKey, "1");
								}
							}
						}
					}
				}
				catch(Exception ex)
				{
					SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
					// 11/21/2005 Paul.  Ignore error, but then we need to find a way to display the connection error.
					// The most likely failure here is a connection failure.
				}
			}
			return dt;
		}

		/// <summary>
		/// Returns the portal mobile menu items for the current authenticated user.
		/// Results are cached in IMemoryCache per user (keyed by USER_ID), with an ISession
		/// sentinel string to detect session invalidation on logout.
		///
		/// BEFORE (.NET Framework 4.8):
		///   HttpSessionState Session = HttpContext.Current.Session;
		///   DataTable dt = Session["vwMODULES_MobileMenu_ByUser.{USER_ID}"] as DataTable;
		///   Session["vwMODULES_MobileMenu_ByUser.{USER_ID}"] = dt;
		///
		/// AFTER (.NET 10 ASP.NET Core):
		///   string sSentinel = Session?.GetString(sCacheKey);
		///   _memoryCache.TryGetValue&lt;DataTable&gt;(sCacheKey, out dt) — guarded by sentinel
		///   _memoryCache.Set(sCacheKey, dt, SlidingExpiration=20min)
		///   Session?.SetString(sCacheKey, "1")
		///
		/// Modules included: Home, Bugs, Cases.
		/// </summary>
		/// <returns>
		/// DataTable with columns MODULE_NAME, DISPLAY_NAME, RELATIVE_PATH ordered by TAB_ORDER.
		/// Returns empty DataTable on DB error or when the user is not authenticated.
		/// </returns>
		public DataTable MobileMenu()
		{
			// 04/28/2006 Paul.  The menu is now cached in the Session, so it will only get cleared when the user logs out.
			// 04/28/2006 Paul.  Include the GUID in the USER_ID so that the user does not have to log-out to get the correct menu.
			// BEFORE: HttpSessionState Session = HttpContext.Current.Session;
			// AFTER:  Use Session property (ISession via IHttpContextAccessor) for sentinel tracking.
			string sCacheKey = "vwMODULES_MobileMenu_ByUser." + _security.USER_ID.ToString();

			// BEFORE: DataTable dt = Session["vwMODULES_MobileMenu_ByUser." + Security.USER_ID.ToString()] as DataTable;
			// AFTER:  Two-phase lookup:
			//   1. Check ISession for sentinel string (proves this session loaded the menu).
			//   2. If sentinel present, retrieve DataTable from IMemoryCache.
			string sSentinel = Session?.GetString(sCacheKey);
			DataTable dt     = null;
			if ( sSentinel != null )
			{
				_memoryCache.TryGetValue<DataTable>(sCacheKey, out dt);
			}

			if ( dt == null )
			{
				dt = new DataTable();
				try
				{
					// 11/17/2007 Paul.  New function to determine if user is authenticated.
					// BEFORE: if (Security.IsAuthenticated()) — static call on legacy static Security class
					// AFTER:  if (_security.IsAuthenticated()) — instance call on DI-injected Security
					if ( _security.IsAuthenticated() )
					{
						// BEFORE: DbProviderFactory dbf = DbProviderFactories.GetFactory(); — static call
						// AFTER:  DbProviderFactory dbf = _dbProviderFactories.GetFactory(_memoryCache); — instance call
						DbProviderFactory dbf = _dbProviderFactories.GetFactory(_memoryCache);
						using ( IDbConnection con = dbf.CreateConnection() )
						{
							con.Open();
							string sSQL;
							sSQL = "select MODULE_NAME                " + ControlChars.CrLf
							     + "     , DISPLAY_NAME               " + ControlChars.CrLf
							     + "     , RELATIVE_PATH              " + ControlChars.CrLf
							     + "  from vwMODULES_MobileMenu_ByUser" + ControlChars.CrLf
							     + " where (USER_ID = @USER_ID or USER_ID is null)    " + ControlChars.CrLf
							     + "   and MODULE_NAME in (N'Home', N'Bugs', N'Cases')" + ControlChars.CrLf
							     + " order by TAB_ORDER               " + ControlChars.CrLf;
							using ( IDbCommand cmd = con.CreateCommand() )
							{
								cmd.CommandText = sSQL;
								// BEFORE: Sql.AddParameter(cmd, "@USER_ID", Security.USER_ID);  — static Security.USER_ID
								// AFTER:  Sql.AddParameter(cmd, "@USER_ID", _security.USER_ID); — instance USER_ID
								Sql.AddParameter(cmd, "@USER_ID", _security.USER_ID);
								using ( DbDataAdapter da = dbf.CreateDataAdapter() )
								{
									((IDbDataAdapter)da).SelectCommand = cmd;
									da.Fill(dt);
									// BEFORE: Session["vwMODULES_MobileMenu_ByUser." + Security.USER_ID.ToString()] = dt;
									// AFTER:  IMemoryCache.Set for DataTable + ISession.SetString for sentinel.
									//         Sentinel ties cache validity to the current session lifetime.
									_memoryCache.Set(sCacheKey, dt, new MemoryCacheEntryOptions
									{
										SlidingExpiration = MenuCacheDuration
									});
									Session?.SetString(sCacheKey, "1");
								}
							}
						}
					}
				}
				catch(Exception ex)
				{
					SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
					// 11/21/2005 Paul.  Ignore error, but then we need to find a way to display the connection error.
					// The most likely failure here is a connection failure.
				}
			}
			return dt;
		}
	}
}
