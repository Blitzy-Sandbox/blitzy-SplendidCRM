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
// .NET 10 Migration: Extracted from SplendidCRM/_code/Security.cs → src/SplendidCRM.Web/Authorization/ModuleAuthorizationHandler.cs
// Source: Security.cs lines 469–712 (SetModuleAccess, SetUserAccess, GetUserAccess, SetACLRoleAccess,
//         GetACLRoleAccess, AdminUserAccess×2)
// Changes applied:
//   - Removed:  using System.Web; using System.Web.SessionState;
//   - Added:    using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Http;
//               using Microsoft.Extensions.Caching.Memory; using System; using System.Threading.Tasks;
//   - Static class with HttpContext.Current → DI-friendly instance class with:
//       IHttpContextAccessor (replaces HttpContext.Current.Session/Request)
//       IMemoryCache         (replaces HttpApplicationState Application[])
//       Security             (replaces SplendidCRM.Security static property access)
//   - HttpContext.Current.Session["key"] == null guard   → ISession.GetString("key") == null
//   - HttpContext.Current.Session["key"] read             → ISession.GetString("key")
//   - HttpContext.Current.Session["key"] = value write    → ISession.SetString("key", value.ToString())
//   - HttpContext.Current.Application["key"] read         → IMemoryCache.Get<object>("key")
//   - Application["ACLACCESS_*"] = nACLACCESS write       → IMemoryCache.Set("ACLACCESS_*", nACLACCESS)
//   - Security.IS_ADMIN / IS_ADMIN_DELEGATE / USER_ID     → _security.IS_ADMIN / IS_ADMIN_DELEGATE / USER_ID
//   - HttpApplicationState Application parameter          → IMemoryCache _memoryCache (constructor injection)
//   - #if DEBUG bIsAdmin = false; #endif preserved from Security.cs lines 502–504
//   - ACL_ACCESS constants (ACLGrid.cs): FULL_ACCESS=100, NONE=-99, ALL=90, OWNER=75
//   - ModuleAuthorizationRequirement implements IAuthorizationRequirement per ASP.NET Core pipeline
//   - ModuleAuthorizationHandler extends AuthorizationHandler<ModuleAuthorizationRequirement>
//   - Minimal change clause: only framework migration changes; all business logic preserved identically

#nullable disable
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	/// <summary>
	/// Authorization requirement carrying the module name and access type for the
	/// module-level ACL check. Consumed by <see cref="ModuleAuthorizationHandler"/>.
	///
	/// This requirement type plugs into the standard ASP.NET Core authorization pipeline:
	///   var requirement = new ModuleAuthorizationRequirement("Accounts", "view");
	///   var result = await _authorizationService.AuthorizeAsync(user, null, requirement);
	///
	/// The ModuleName + AccessType pair maps directly to the session/cache key pattern:
	///   "ACLACCESS_{ModuleName}_{AccessType}"
	/// which is the canonical key format used by SplendidCRM throughout the ACL system.
	/// </summary>
	public class ModuleAuthorizationRequirement : IAuthorizationRequirement
	{
		/// <summary>
		/// CRM module name (e.g. "Accounts", "Contacts", "Opportunities", "Calendar", "Activities").
		/// Case-sensitive — must match the module names stored in the ACL session/cache keys.
		/// </summary>
		public string ModuleName { get; }

		/// <summary>
		/// ACL operation type. Valid values: "view", "edit", "delete", "import", "export", "list", "access".
		/// The special value "access" is the master gate — a negative "access" level overrides all other types.
		/// </summary>
		public string AccessType { get; }

		/// <summary>
		/// Initializes a new <see cref="ModuleAuthorizationRequirement"/>.
		/// </summary>
		/// <param name="moduleName">CRM module name (case-sensitive).</param>
		/// <param name="accessType">ACL operation type (view/edit/delete/import/export/list/access).</param>
		public ModuleAuthorizationRequirement(string moduleName, string accessType)
		{
			ModuleName = moduleName;
			AccessType = accessType;
		}
	}

	/// <summary>
	/// Module-level ACL authorization handler — the most foundational tier of the SplendidCRM
	/// 4-tier ACL model (Module → Team → Field → Record).
	///
	/// Migrated from <c>SplendidCRM/_code/Security.cs</c> (lines 469–712) for .NET 10 ASP.NET Core.
	/// All static <c>HttpContext.Current</c> and <c>Application[]</c> access patterns replaced with
	/// constructor-injected <see cref="IHttpContextAccessor"/>, <see cref="IMemoryCache"/>, and
	/// <see cref="Security"/> instances.
	///
	/// Key ACL rules preserved identically from the original:
	/// <list type="bullet">
	///   <item>Admin bypass: IS_ADMIN=true → FULL_ACCESS for valid modules, NONE for invalid modules.
	///         #if DEBUG bIsAdmin=false preserved to enable ACL testing in debug builds.</item>
	///   <item>Calendar composite module: max(Calls access, Meetings access).</item>
	///   <item>Activities composite module: max(Calls, Meetings, Tasks, Notes, Emails).</item>
	///   <item>Session-first fallback: user session ACL (SetUserAccess) overrides module-level cache ACL (SetModuleAccess).</item>
	///   <item>Access-type master gate: if "access" ACL for a module is negative, it denies ALL access types on that module.</item>
	///   <item>NONE = -99 (NOT zero): zero means no ACL row found (default permit); -99 is explicit denial.</item>
	/// </list>
	///
	/// Register as SCOPED in DI to receive a fresh <see cref="Security"/> instance per request.
	/// </summary>
	public class ModuleAuthorizationHandler : AuthorizationHandler<ModuleAuthorizationRequirement>
	{
		// =====================================================================================
		// Private fields — DI-injected replacements for static ASP.NET Framework patterns
		// =====================================================================================

		/// <summary>
		/// Replaces <c>HttpContext.Current</c> throughout — provides access to the current
		/// HTTP context's session, request, and response within non-controller classes.
		/// </summary>
		private readonly IHttpContextAccessor _httpContextAccessor;

		/// <summary>
		/// Replaces <c>HttpApplicationState</c> (<c>Application[]</c>) throughout —
		/// holds module-level ACL defaults keyed by "ACLACCESS_{MODULE}_{TYPE}" and
		/// module validity flags keyed by "Modules.{MODULE}.Valid".
		/// </summary>
		private readonly IMemoryCache _memoryCache;

		/// <summary>
		/// DI-injectable Security service replacing static <c>SplendidCRM.Security</c> class.
		/// Provides IS_ADMIN, IS_ADMIN_DELEGATE, and USER_ID from the distributed session.
		/// </summary>
		private readonly Security _security;

		// =====================================================================================
		// Constructor
		// =====================================================================================

		/// <summary>
		/// Constructs the handler with all required DI services.
		/// </summary>
		/// <param name="httpContextAccessor">
		///   Replaces <c>HttpContext.Current</c> for session and request access.
		/// </param>
		/// <param name="memoryCache">
		///   Replaces <c>HttpApplicationState</c> (<c>Application[]</c>) for module-level ACL cache.
		/// </param>
		/// <param name="security">
		///   Replaces static <c>SplendidCRM.Security</c> for IS_ADMIN, IS_ADMIN_DELEGATE, USER_ID.
		/// </param>
		public ModuleAuthorizationHandler(
			IHttpContextAccessor httpContextAccessor,
			IMemoryCache         memoryCache,
			Security             security)
		{
			_httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
			_memoryCache         = memoryCache         ?? throw new ArgumentNullException(nameof(memoryCache));
			_security            = security            ?? throw new ArgumentNullException(nameof(security));
		}

		// =====================================================================================
		// ASP.NET Core AuthorizationHandler<T> — HandleRequirementAsync override
		// This is the entry point used by the ASP.NET Core authorization middleware pipeline.
		// =====================================================================================

		/// <summary>
		/// ASP.NET Core authorization pipeline entry point.
		///
		/// Evaluates module-level ACL for <paramref name="requirement"/>.ModuleName and AccessType
		/// using <see cref="GetUserAccess"/> and signals success or non-success accordingly.
		///
		/// Behavior:
		///   • nACLACCESS >= 0 → <c>context.Succeed(requirement)</c> (access granted)
		///   • nACLACCESS &lt; 0 → do NOT call Fail() (let policy default decide; allows other handlers to override)
		/// </summary>
		protected override Task HandleRequirementAsync(
			AuthorizationHandlerContext        context,
			ModuleAuthorizationRequirement     requirement)
		{
			int nACLACCESS = GetUserAccess(requirement.ModuleName, requirement.AccessType);
			// 08/10/2017 Paul.  Negative values (NONE=-99, DISABLED=-98) indicate denied access.
			// Zero and positive values grant some level of access (0=default permit, OWNER=75, ALL=90, etc.).
			if (nACLACCESS >= 0)
				context.Succeed(requirement);
			// Do NOT call context.Fail() — leave the decision to the policy default handler.
			// This allows other authorization requirements (e.g. TeamAuthorizationHandler) to override.
			return Task.CompletedTask;
		}

		// =====================================================================================
		// SetModuleAccess — module-level ACL default write (Security.cs lines 470–478)
		// BEFORE: Application["ACLACCESS_" + sMODULE_NAME + "_" + sACCESS_TYPE] = nACLACCESS;
		// AFTER:  _memoryCache.Set("ACLACCESS_" + moduleName + "_" + accessType, nACLACCESS)
		// =====================================================================================

		// 02/03/2009 Paul.  This function might be called from a background process.
		/// <summary>
		/// Stores a module-level default ACL value in the application-scoped memory cache.
		/// This acts as the module-level default that applies to all users who have no
		/// user-specific session override (see <see cref="SetUserAccess"/>).
		///
		/// Called during session initialization (SplendidInit.LoadACL) to populate
		/// module-level defaults from the ACL tables.
		///
		/// <para>
		/// BEFORE: <c>Application["ACLACCESS_" + sMODULE_NAME + "_" + sACCESS_TYPE] = nACLACCESS;</c><br/>
		/// AFTER:  <c>_memoryCache.Set("ACLACCESS_" + moduleName + "_" + accessType, nACLACCESS)</c>
		/// </para>
		/// </summary>
		/// <param name="moduleName">CRM module name (case-sensitive, must not be empty).</param>
		/// <param name="accessType">ACL access type (view/edit/delete/import/export/list/access).</param>
		/// <param name="aclAccess">ACL access level integer (see <see cref="ACL_ACCESS"/> constants).</param>
		/// <exception cref="Exception">Thrown when moduleName is null or empty.</exception>
		public void SetModuleAccess(string moduleName, string accessType, int aclAccess)
		{
			// 06/04/2006 Paul.  Verify that sMODULE_NAME is not empty.
			if (Sql.IsEmptyString(moduleName))
				throw new Exception("sMODULE_NAME should not be empty.");
			// BEFORE: Application["ACLACCESS_" + sMODULE_NAME + "_" + sACCESS_TYPE] = nACLACCESS;
			// AFTER:  _memoryCache.Set(key, aclAccess)
			_memoryCache.Set("ACLACCESS_" + moduleName + "_" + accessType, aclAccess);
		}

		// =====================================================================================
		// SetUserAccess — user-level ACL override write (Security.cs lines 480–488)
		// BEFORE: HttpContext.Current.Session["ACLACCESS_" + sMODULE_NAME + "_" + sACCESS_TYPE] = nACLACCESS;
		// AFTER:  ISession.SetString("ACLACCESS_" + moduleName + "_" + accessType, nACLACCESS.ToString())
		// NOTE:   ISession only supports string/byte-array; int is round-tripped via ToString()/ToInteger()
		// =====================================================================================

		/// <summary>
		/// Stores a user-specific ACL override in the distributed session.
		/// This per-user override takes precedence over the module-level default set by
		/// <see cref="SetModuleAccess"/> — the session value is checked first in <see cref="GetUserAccess"/>.
		///
		/// Called during login (SplendidInit.LoginUser) to populate role-specific ACL values.
		///
		/// <para>
		/// BEFORE: <c>HttpContext.Current.Session["ACLACCESS_" + sMODULE_NAME + "_" + sACCESS_TYPE] = nACLACCESS;</c><br/>
		/// AFTER:  <c>ISession.SetString(key, nACLACCESS.ToString())</c>
		/// </para>
		/// </summary>
		/// <param name="moduleName">CRM module name (case-sensitive, must not be empty).</param>
		/// <param name="accessType">ACL access type.</param>
		/// <param name="aclAccess">ACL access level integer.</param>
		/// <exception cref="Exception">Thrown when session is null or moduleName is empty.</exception>
		public void SetUserAccess(string moduleName, string accessType, int aclAccess)
		{
			// BEFORE: if ( HttpContext.Current == null || HttpContext.Current.Session == null ) throw ...
			// AFTER:  check _httpContextAccessor.HttpContext?.Session
			var session = _httpContextAccessor.HttpContext?.Session;
			if (session == null)
				throw new Exception("HttpContext.Current.Session is null");
			// 06/04/2006 Paul.  Verify that sMODULE_NAME is not empty.
			if (Sql.IsEmptyString(moduleName))
				throw new Exception("sMODULE_NAME should not be empty.");
			// BEFORE: HttpContext.Current.Session["ACLACCESS_" + sMODULE_NAME + "_" + sACCESS_TYPE] = nACLACCESS;
			// AFTER:  ISession only supports string values; int is stored as ToString() and retrieved via Sql.ToInteger()
			session.SetString("ACLACCESS_" + moduleName + "_" + accessType, aclAccess.ToString());
		}

		// =====================================================================================
		// GetUserAccess — full module-level ACL lookup (Security.cs lines 490–567)
		// This is the MAIN implementation that HandleRequirementAsync delegates to.
		// All HttpContext.Current / Application[] / static Security patterns replaced with DI.
		// =====================================================================================

		/// <summary>
		/// Returns the effective ACL access level integer for the current session user on the specified module.
		///
		/// Replicates the EXACT logic from <c>Security.GetUserAccess</c> (lines 490–567) with DI substitution.
		///
		/// Access level semantics:
		/// <list type="bullet">
		///   <item>100 (<see cref="ACL_ACCESS.FULL_ACCESS"/>) — Admin full access</item>
		///   <item>90  (<see cref="ACL_ACCESS.ALL"/>)         — All users have access</item>
		///   <item>89  (<see cref="ACL_ACCESS.ENABLED"/>)     — Module is enabled</item>
		///   <item>75  (<see cref="ACL_ACCESS.OWNER"/>)       — Owner-only access</item>
		///   <item>-98 (<see cref="ACL_ACCESS.DISABLED"/>)    — Explicitly disabled</item>
		///   <item>-99 (<see cref="ACL_ACCESS.NONE"/>)        — No access (denied)</item>
		/// </list>
		///
		/// Lookup priority (session-first fallback):
		/// <list type="number">
		///   <item>Admin bypass: IS_ADMIN → FULL_ACCESS (valid module) or NONE (invalid module)</item>
		///   <item>Module validity: "Modules.{MODULE}.Valid" from IMemoryCache</item>
		///   <item>Calendar/Activities composite: Math.Max of sub-module access levels</item>
		///   <item>Session key "ACLACCESS_{MODULE}_{TYPE}" (user-level override, highest priority)</item>
		///   <item>Cache key "ACLACCESS_{MODULE}_{TYPE}" (module-level default, fallback)</item>
		///   <item>Access-type master gate: negative "access" ACL denies all other access types</item>
		/// </list>
		/// </summary>
		/// <param name="moduleName">CRM module name (case-sensitive).</param>
		/// <param name="accessType">ACL operation: "view", "edit", "delete", "import", "export", "list", "access".</param>
		/// <returns>ACL access level integer. Negative = denied; 0+ = some access granted.</returns>
		/// <exception cref="Exception">Thrown when session is null or moduleName is empty.</exception>
		public int GetUserAccess(string moduleName, string accessType)
		{
			// ---- Step 1: Validate inputs (Security.cs lines 492–496) ----
			// BEFORE: if ( HttpContext.Current == null || HttpContext.Current.Session == null ) throw ...
			// AFTER:  check _httpContextAccessor.HttpContext?.Session
			var session = _httpContextAccessor.HttpContext?.Session;
			if (session == null)
				throw new Exception("HttpContext.Current.Session is null");
			// 06/04/2006 Paul.  Verify that sMODULE_NAME is not empty.
			if (Sql.IsEmptyString(moduleName))
				throw new Exception("sMODULE_NAME should not be empty.");

			// ---- Step 2: Admin bypass (Security.cs lines 498–512) ----
			// 08/30/2009 Paul.  Don't apply admin rules when debugging so that we can test the code.
			// 04/27/2006 Paul.  Admins have full access to the site, no matter what the role.
			// BEFORE: bool bIsAdmin = IS_ADMIN;  (static Security property)
			// AFTER:  bool bIsAdmin = _security.IS_ADMIN;  (injected instance)
			bool bIsAdmin = _security.IS_ADMIN;
			// 12/03/2017 Paul.  Don't apply admin rules when debugging so that we can test the code.
			// Preserved from Security.cs lines 502–504: allows ACL testing in debug builds without admin bypass.
#if DEBUG
			bIsAdmin = false;
#endif
			if (bIsAdmin)
			{
				// 04/21/2016 Paul.  We need to make sure that disabled modules do not show related buttons.
				// BEFORE: if ( Sql.ToBoolean(HttpContext.Current.Application["Modules." + sMODULE_NAME + ".Valid"]) )
				// AFTER:  if ( Sql.ToBoolean(_memoryCache.Get<object>("Modules." + moduleName + ".Valid")) )
				if (Sql.ToBoolean(_memoryCache.Get<object>("Modules." + moduleName + ".Valid")))
					return ACL_ACCESS.FULL_ACCESS;
				else
					// 08/10/2017 Paul.  We need to return a negative number to prevent access, not zero.
					// CRITICAL: ACL_ACCESS.NONE = -99 (not zero) — negative number prevents access
					return ACL_ACCESS.NONE;
			}

			// ---- Step 3: Module validity check (Security.cs lines 517–520) ----
			int nACLACCESS = 0;
			// 08/10/2017 Paul.  We need to return a negative number to prevent access, not zero.
			// BEFORE: if ( !Sql.ToBoolean(HttpContext.Current.Application["Modules." + sMODULE_NAME + ".Valid"]) )
			// AFTER:  if ( !Sql.ToBoolean(_memoryCache.Get<object>("Modules." + moduleName + ".Valid")) )
			if (!Sql.ToBoolean(_memoryCache.Get<object>("Modules." + moduleName + ".Valid")))
			{
				// Module is not registered as valid — deny access with NONE (-99), not zero
				nACLACCESS = ACL_ACCESS.NONE;
			}
			// ---- Step 4a: Calendar composite module (Security.cs lines 521–528) ----
			// 12/05/2006 Paul.  We need to combine Activity and Calendar related modules into a single access value.
			else if (moduleName == "Calendar")
			{
				// 12/05/2006 Paul.  The Calendar related views only combine Calls and Meetings.
				int nACLACCESS_Calls    = GetUserAccess("Calls"   , accessType);
				int nACLACCESS_Meetings = GetUserAccess("Meetings", accessType);
				// 12/05/2006 Paul. Use the max value so that Activities will be displayed if either are accessible.
				nACLACCESS = Math.Max(nACLACCESS_Calls, nACLACCESS_Meetings);
			}
			// ---- Step 4b: Activities composite module (Security.cs lines 529–542) ----
			else if (moduleName == "Activities")
			{
				// 12/05/2006 Paul.  The Activities combines Calls, Meetings, Tasks, Notes and Emails.
				int nACLACCESS_Calls    = GetUserAccess("Calls"   , accessType);
				int nACLACCESS_Meetings = GetUserAccess("Meetings", accessType);
				int nACLACCESS_Tasks    = GetUserAccess("Tasks"   , accessType);
				int nACLACCESS_Notes    = GetUserAccess("Notes"   , accessType);
				int nACLACCESS_Emails   = GetUserAccess("Emails"  , accessType);
				nACLACCESS = nACLACCESS_Calls;
				nACLACCESS = Math.Max(nACLACCESS, nACLACCESS_Meetings);
				nACLACCESS = Math.Max(nACLACCESS, nACLACCESS_Tasks   );
				nACLACCESS = Math.Max(nACLACCESS, nACLACCESS_Notes   );
				nACLACCESS = Math.Max(nACLACCESS, nACLACCESS_Emails  );
			}
			else
			{
				// ---- Step 5: Standard module ACL lookup (Security.cs lines 543–564) ----
				// Build the canonical ACL key: "ACLACCESS_{MODULE}_{ACCESS_TYPE}"
				string sAclKey = "ACLACCESS_" + moduleName + "_" + accessType;
				// 04/27/2006 Paul.  If no specific level is provided, then look to the Module level.
				// Session-first priority: user-level session override > module-level cache default
				// BEFORE: if ( HttpContext.Current.Session[sAclKey] == null )
				//             nACLACCESS = Sql.ToInteger(HttpContext.Current.Application[sAclKey]);
				//         else
				//             nACLACCESS = Sql.ToInteger(HttpContext.Current.Session[sAclKey]);
				// AFTER:  ISession.GetString() returns null when key not present (same semantic as Session[] == null)
				string sSessionVal = session.GetString(sAclKey);
				if (sSessionVal == null)
					// No user-level override: use module-level cache default
					// BEFORE: nACLACCESS = Sql.ToInteger(HttpContext.Current.Application[sAclKey]);
					// AFTER:  nACLACCESS = Sql.ToInteger(_memoryCache.Get<object>(sAclKey))
					nACLACCESS = Sql.ToInteger(_memoryCache.Get<object>(sAclKey));
				else
					// User-level override present: use session value
					// BEFORE: nACLACCESS = Sql.ToInteger(HttpContext.Current.Session[sAclKey]);
					// AFTER:  nACLACCESS = Sql.ToInteger(sSessionVal)  (string from ISession.GetString)
					nACLACCESS = Sql.ToInteger(sSessionVal);

				// ---- Step 6: Access-type master gate override (Security.cs lines 551–564) ----
				// 04/27/2006 Paul.  The access type can over-ride any other type.
				// A simple trick is to take the minimum of the two values.
				// If either value is denied, then the result will be negative.
				// RULE: If "access" ACL for this module is negative, it denies ALL access types.
				if (accessType != "access" && nACLACCESS >= 0)
				{
					sAclKey = "ACLACCESS_" + moduleName + "_access";
					int nAccessLevel = 0;
					// Same session-first lookup pattern for the "access" key
					// BEFORE: if ( HttpContext.Current.Session[sAclKey] == null )
					//             nAccessLevel = Sql.ToInteger(HttpContext.Current.Application[sAclKey]);
					//         else
					//             nAccessLevel = Sql.ToInteger(HttpContext.Current.Session[sAclKey]);
					string sAccessSessionVal = session.GetString(sAclKey);
					if (sAccessSessionVal == null)
						nAccessLevel = Sql.ToInteger(_memoryCache.Get<object>(sAclKey));
					else
						nAccessLevel = Sql.ToInteger(sAccessSessionVal);
					// If "access" level is negative (denied), override the specific access type
					if (nAccessLevel < 0)
						nACLACCESS = nAccessLevel;
				}
			}
			return nACLACCESS;
		}

		// =====================================================================================
		// SetACLRoleAccess — ACL role membership write (Security.cs lines 570–572)
		// BEFORE: HttpContext.Current.Session["ACLRoles." + sROLE_NAME] = true;
		// AFTER:  ISession.SetString("ACLRoles." + roleName, "True")
		// =====================================================================================

		// 11/11/2010 Paul.  Provide quick access to ACL Roles and Teams.
		/// <summary>
		/// Marks an ACL role as active for the current session user.
		/// Called during login to record which ACL roles the user is a member of.
		///
		/// <para>
		/// BEFORE: <c>HttpContext.Current.Session["ACLRoles." + sROLE_NAME] = true;</c><br/>
		/// AFTER:  <c>ISession.SetString("ACLRoles." + roleName, "True")</c>
		/// </para>
		/// </summary>
		/// <param name="roleName">ACL role name to mark as active in the session.</param>
		public void SetACLRoleAccess(string roleName)
		{
			// BEFORE: HttpContext.Current.Session["ACLRoles." + sROLE_NAME] = true;
			// AFTER:  ISession.SetString("ACLRoles." + roleName, "True")
			// Note: ISession does not support bool values; "True" round-trips via Sql.ToBoolean()
			_httpContextAccessor.HttpContext?.Session.SetString("ACLRoles." + roleName, "True");
		}

		/// <summary>
		/// Returns whether the specified ACL role is active for the current session user.
		///
		/// <para>
		/// BEFORE: <c>return Sql.ToBoolean(HttpContext.Current.Session["ACLRoles." + sROLE_NAME]);</c><br/>
		/// AFTER:  <c>return Sql.ToBoolean(ISession.GetString("ACLRoles." + roleName))</c>
		/// </para>
		/// </summary>
		/// <param name="roleName">ACL role name to check.</param>
		/// <returns><c>true</c> if the role has been activated for this session; <c>false</c> otherwise.</returns>
		public bool GetACLRoleAccess(string roleName)
		{
			// BEFORE: return Sql.ToBoolean(HttpContext.Current.Session["ACLRoles." + sROLE_NAME]);
			// AFTER:  return Sql.ToBoolean(_httpContextAccessor.HttpContext?.Session.GetString("ACLRoles." + roleName))
			// Sql.ToBoolean(null) returns false, which matches the original null-session behavior.
			return Sql.ToBoolean(_httpContextAccessor.HttpContext?.Session.GetString("ACLRoles." + roleName));
		}

		// =====================================================================================
		// AdminUserAccess — admin-path ACL check (Security.cs lines 688–712)
		// BEFORE: SplendidCRM.Security.IS_ADMIN, HttpContext.Current.Application["CONFIG.*"]
		// AFTER:  _security.IS_ADMIN, _security.IS_ADMIN_DELEGATE, _memoryCache.Get("CONFIG.*")
		// =====================================================================================

		// 03/15/2010 Paul.  New AdminUserAccess functions include Admin Delegation.
		/// <summary>
		/// Returns the effective ACL access level for administrative access scenarios, including
		/// admin delegation support via the <c>CONFIG.allow_admin_roles</c> setting.
		///
		/// Logic (preserving Security.cs lines 688–702 exactly):
		/// <list type="bullet">
		///   <item>IS_ADMIN = true → return <see cref="ACL_ACCESS.ALL"/> (full admin bypass)</item>
		///   <item>CONFIG.allow_admin_roles = true AND IS_ADMIN_DELEGATE = true → call <see cref="GetUserAccess"/> for role-based access</item>
		///   <item>Otherwise → return <see cref="ACL_ACCESS.NONE"/> (-99)</item>
		/// </list>
		///
		/// <para>
		/// BEFORE: <c>SplendidCRM.Security.IS_ADMIN</c> (static property)<br/>
		///         <c>HttpContext.Current.Application["CONFIG.allow_admin_roles"]</c><br/>
		///         <c>SplendidCRM.Security.IS_ADMIN_DELEGATE</c> (static property)<br/>
		/// AFTER:  <c>_security.IS_ADMIN</c> (injected instance)<br/>
		///         <c>_memoryCache.Get&lt;object&gt;("CONFIG.allow_admin_roles")</c><br/>
		///         <c>_security.IS_ADMIN_DELEGATE</c> (injected instance)
		/// </para>
		/// </summary>
		/// <param name="moduleName">CRM module name.</param>
		/// <param name="accessType">ACL access type.</param>
		/// <returns>ACL access level integer.</returns>
		public int AdminUserAccess(string moduleName, string accessType)
		{
			// BEFORE: if ( SplendidCRM.Security.IS_ADMIN ) return ACL_ACCESS.ALL;
			// AFTER:  if ( _security.IS_ADMIN ) return ACL_ACCESS.ALL;
			if (_security.IS_ADMIN)
				return ACL_ACCESS.ALL;
			int nACLACCESS = ACL_ACCESS.NONE;
			// BEFORE: bool bAllowAdminRoles = Sql.ToBoolean(HttpContext.Current.Application["CONFIG.allow_admin_roles"]);
			// AFTER:  bool bAllowAdminRoles = Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.allow_admin_roles"))
			bool bAllowAdminRoles = Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.allow_admin_roles"));
			if (bAllowAdminRoles)
			{
				// BEFORE: if ( SplendidCRM.Security.IS_ADMIN_DELEGATE ) nACLACCESS = Security.GetUserAccess(...);
				// AFTER:  if ( _security.IS_ADMIN_DELEGATE ) nACLACCESS = GetUserAccess(...);
				if (_security.IS_ADMIN_DELEGATE)
				{
					nACLACCESS = GetUserAccess(moduleName, accessType);
				}
			}
			return nACLACCESS;
		}

		/// <summary>
		/// Returns the effective ACL access level for admin/admin-delegate scenarios with an
		/// additional OWNER-level record assignment check (Security.cs lines 704–712).
		///
		/// Logic:
		/// <list type="bullet">
		///   <item>Calls <see cref="AdminUserAccess(string, string)"/> for the base access level</item>
		///   <item>If base level == <see cref="ACL_ACCESS.OWNER"/> AND USER_ID != assignedUserId AND assignedUserId != Guid.Empty:
		///         demote to <see cref="ACL_ACCESS.NONE"/> (user does not own this specific record)</item>
		/// </list>
		///
		/// <para>
		/// BEFORE: <c>Security.USER_ID != gASSIGNED_USER_ID</c> (static property)<br/>
		/// AFTER:  <c>_security.USER_ID != assignedUserId</c> (injected instance)
		/// </para>
		/// </summary>
		/// <param name="moduleName">CRM module name.</param>
		/// <param name="accessType">ACL access type.</param>
		/// <param name="assignedUserId">The ASSIGNED_USER_ID field value from the record being accessed.</param>
		/// <returns>ACL access level integer, adjusted for owner check.</returns>
		public int AdminUserAccess(string moduleName, string accessType, Guid assignedUserId)
		{
			int nACLACCESS = AdminUserAccess(moduleName, accessType);
			// BEFORE: if ( nACLACCESS == ACL_ACCESS.OWNER && Security.USER_ID != gASSIGNED_USER_ID && gASSIGNED_USER_ID != Guid.Empty)
			// AFTER:  if ( nACLACCESS == ACL_ACCESS.OWNER && _security.USER_ID != assignedUserId && assignedUserId != Guid.Empty)
			if (nACLACCESS == ACL_ACCESS.OWNER && _security.USER_ID != assignedUserId && assignedUserId != Guid.Empty)
			{
				nACLACCESS = ACL_ACCESS.NONE;
			}
			return nACLACCESS;
		}
	}
}
