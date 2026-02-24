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
// .NET 10 Migration: Extracted from SplendidCRM/_code/Security.cs → src/SplendidCRM.Web/Authorization/TeamAuthorizationHandler.cs
// Source: Security.cs lines 580–588 (SetTeamAccess, GetTeamAccess),
//         lines 822–840 (TeamHierarchyModule constant, TeamHierarchySavedSearch),
//         lines 856–988 (Filter team management joins: bModuleIsTeamed, bEnableTeamManagement,
//         bRequireTeamManagement, bEnableDynamicTeams, bEnableTeamHierarchy, admin bypass, Data Privacy Manager)
// Changes applied:
//   - Removed:  using System.Web; using System.Web.SessionState; using System.Web.UI.WebControls;
//   - Added:    using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Http;
//               using Microsoft.Extensions.Caching.Memory; using System; using System.Data;
//               using System.Threading.Tasks; using System.Xml;
//   - Static class with HttpContext.Current → DI-friendly instance class with:
//       IHttpContextAccessor (replaces HttpContext.Current.Session/Request)
//       IMemoryCache         (replaces HttpApplicationState Application[])
//       Security             (replaces static SplendidCRM.Security access — IS_ADMIN, USER_ID,
//                             GetACLRoleAccess, SetTeamAccess, GetTeamAccess, TeamHierarchySavedSearch,
//                             TeamHierarchyModule)
//       SplendidCache        (provides SavedSearch() for TeamHierarchySavedSearch implementation
//                             — Web layer can call SplendidCache.SavedSearch() without the circular
//                             dependency that affects the Core layer's Security.TeamHierarchySavedSearch)
//   - HttpContext.Current.Session["Teams.x"]   → ISession.GetString("Teams.x") / ISession.SetString(...)
//   - HttpContext.Current.Application["Modules.x.Teamed"] → IMemoryCache.Get<object>("Modules.x.Teamed")
//   - Security.IS_ADMIN / USER_ID static        → _security.IS_ADMIN / USER_ID (DI instance)
//   - Security.GetACLRoleAccess() static        → _security.GetACLRoleAccess() (DI instance)
//   - Security.SetTeamAccess() / GetTeamAccess()→ _security.SetTeamAccess() / GetTeamAccess()
//   - Security.TeamHierarchySavedSearch() static → _security.TeamHierarchySavedSearch() (DI)
//   - Security.TeamHierarchyModule constant      → also defined on handler (same value "TeamHierarchy")
//   - SplendidCache.SavedSearch() static        → _splendidCache.SavedSearch() (DI instance)
//   - Crm.Config.enable_team_management()  etc.  → Crm.Config.* (static ambient, set by DI constructor)
//   - XmlUtil.SelectSingleNode() static          → XmlUtil.SelectSingleNode() (static utility method)
//   - Sql.To* / Sql.IsEmpty* static utilities    → preserved as static calls (no change required)
//   - #if DEBUG bIsAdmin = false; #endif preserved from Security.cs lines 881-883
//   - TeamAuthorizationRequirement implements IAuthorizationRequirement per ASP.NET Core pipeline
//   - TeamAuthorizationHandler extends AuthorizationHandler<TeamAuthorizationRequirement>
//   - Minimal change clause: only framework migration changes; all ACL business logic preserved identically

#nullable disable
using System;
using System.Data;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	// =========================================================================================
	// TeamAuthorizationRequirement
	//
	// Carries the module name (and optional team context) for the team-level ACL check.
	// This is the second tier in the SplendidCRM 4-tier ACL model (Module → Team → Field → Record).
	//
	// Usage:
	//   var req = new TeamAuthorizationRequirement("Accounts");
	//   var result = await _authorizationService.AuthorizeAsync(user, null, req);
	//   if (!result.Succeeded) { /* deny access */ }
	//
	// The ModuleName maps to the module metadata cache key:
	//   "Modules.{ModuleName}.Teamed" → bool: whether this module participates in team filtering
	// =========================================================================================

	/// <summary>
	/// Authorization requirement carrying the module name for the team-level ACL check.
	/// Consumed by <see cref="TeamAuthorizationHandler"/>.
	///
	/// This requirement plugs into the standard ASP.NET Core authorization pipeline and
	/// represents the Team tier of the SplendidCRM 4-tier ACL model (Module → Team → Field → Record).
	///
	/// The ModuleName is used to look up whether the module is "teamed"
	/// (i.e., whether records in that module are associated with teams) via the
	/// <c>Modules.{ModuleName}.Teamed</c> memory cache key.
	/// </summary>
	public class TeamAuthorizationRequirement : IAuthorizationRequirement
	{
		/// <summary>
		/// CRM module name (e.g. "Accounts", "Contacts", "Opportunities", "Leads", "Teams").
		/// Case-sensitive — must match the module names stored in the module metadata cache keys.
		/// Used to look up <c>Modules.{ModuleName}.Teamed</c> in IMemoryCache.
		/// </summary>
		public string ModuleName { get; }

		/// <summary>
		/// Initializes a new <see cref="TeamAuthorizationRequirement"/>.
		/// </summary>
		/// <param name="moduleName">
		/// CRM module name (case-sensitive, e.g. "Accounts", "Contacts", "Opportunities").
		/// </param>
		public TeamAuthorizationRequirement(string moduleName)
		{
			ModuleName = moduleName;
		}
	}

	/// <summary>
	/// Team-level ACL authorization handler — the second tier of the SplendidCRM
	/// 4-tier ACL model (Module → Team → Field → Record).
	///
	/// Migrated from <c>SplendidCRM/_code/Security.cs</c> for .NET 10 ASP.NET Core.
	/// Extracts the team management logic from Security.Filter() (lines 892–988) and the
	/// team session methods SetTeamAccess/GetTeamAccess (lines 580–588), and the
	/// TeamHierarchySavedSearch utility method (lines 826–840).
	///
	/// All static <c>HttpContext.Current</c> and <c>Application[]</c> access patterns are replaced
	/// with constructor-injected <see cref="IHttpContextAccessor"/>, <see cref="IMemoryCache"/>,
	/// <see cref="Security"/>, and <see cref="SplendidCache"/> instances.
	///
	/// <para><b>Team Management Logic (preserved from original Security.cs):</b></para>
	/// <list type="bullet">
	///   <item>Admin bypass: IS_ADMIN=true → team management does not restrict records.
	///         #if DEBUG bIsAdmin=false preserved to enable team ACL testing in debug builds.</item>
	///   <item>Data Privacy Manager role: grants admin-like access for Accounts, Contacts, Leads,
	///         Prospects modules (line 885–890 of original Security.cs).</item>
	///   <item>Module teamed flag: only modules with <c>Modules.{Name}.Teamed=true</c> in the
	///         application cache participate in team-based filtering.</item>
	///   <item>enable_team_management: global switch — if false, no team-based SQL filtering.</item>
	///   <item>require_team_management: controls INNER JOIN vs LEFT OUTER JOIN semantics in
	///         Security.Filter() SQL building. Does NOT block module-level access.</item>
	///   <item>enable_dynamic_teams: switches between TEAM_ID (static teams) and TEAM_SET_ID
	///         (dynamic team sets) for the SQL JOIN target column.</item>
	///   <item>enable_team_hierarchy: enables hierarchy function JOINs (fnTEAM_HIERARCHY_MEMBERSHIPS,
	///         fnTEAM_SET_HIERARCHY_MEMBERSHIPS) instead of flat membership view JOINs.</item>
	///   <item>TeamHierarchySavedSearch: loads the saved "TeamHierarchy" search from SplendidCache
	///         to get the hierarchy root (TEAM_ID + TEAM_NAME) for an additional fnTEAM_HIERARCHY_ByTeam
	///         JOIN when hierarchy is enabled (lines 965–987 of original Security.cs).</item>
	///   <item>Record-level team filtering is performed at the SQL level by Security.Filter() —
	///         the authorization handler grants module-level access, and Security.Filter() constrains
	///         the record result set via JOIN clauses added to IDbCommand.CommandText.</item>
	/// </list>
	///
	/// Register as SCOPED in DI to receive a fresh <see cref="Security"/> instance per request.
	/// </summary>
	public class TeamAuthorizationHandler : AuthorizationHandler<TeamAuthorizationRequirement>
	{
		// =====================================================================================
		// Private fields — DI-injected replacements for static ASP.NET Framework patterns
		// =====================================================================================

		/// <summary>
		/// Replaces <c>HttpContext.Current</c> throughout — provides access to the current
		/// HTTP context's session, request, and response within non-controller classes.
		/// Used for direct session access in <see cref="SetTeamAccess"/> and
		/// <see cref="GetTeamAccess"/> (session key pattern: "Teams.{teamName}").
		/// </summary>
		private readonly IHttpContextAccessor _httpContextAccessor;

		/// <summary>
		/// Replaces <c>HttpApplicationState</c> (<c>Application[]</c>) throughout —
		/// holds module metadata flags (e.g., "Modules.{MODULE}.Teamed") populated by
		/// SplendidInit during application startup.
		/// BEFORE: <c>HttpContext.Current.Application["Modules." + sMODULE_NAME + ".Teamed"]</c>
		/// AFTER:  <c>_memoryCache.Get&lt;object&gt;("Modules." + moduleName + ".Teamed")</c>
		/// </summary>
		private readonly IMemoryCache _memoryCache;

		/// <summary>
		/// DI-injectable Security service replacing static <c>SplendidCRM.Security</c> class.
		/// Provides IS_ADMIN, USER_ID, GetACLRoleAccess, SetTeamAccess, GetTeamAccess,
		/// and TeamHierarchySavedSearch from the distributed session.
		/// </summary>
		private readonly Security _security;

		/// <summary>
		/// DI-injectable SplendidCache service providing <see cref="SplendidCache.SavedSearch"/>
		/// for the team hierarchy filter scope lookup.
		/// 
		/// NOTE: The Web layer uses SplendidCache.SavedSearch() directly (no circular dependency),
		/// whereas the Core layer's Security.TeamHierarchySavedSearch() reads from session JSON
		/// to avoid the SplendidCache → Security circular dependency present in SplendidCRM.Core.
		/// </summary>
		private readonly SplendidCache _splendidCache;

		// =====================================================================================
		// Constants
		// =====================================================================================

		// 01/05/2020 Paul.  Provide central location for constant.
		/// <summary>
		/// Module name used to identify the team hierarchy saved search in SplendidCache.
		/// Value: "TeamHierarchy". Preserved identically from Security.cs line 823.
		/// Also defined on <see cref="Security.TeamHierarchyModule"/> for backward compatibility
		/// with callers that reference the constant on the Security class directly.
		/// </summary>
		public const string TeamHierarchyModule = "TeamHierarchy";

		// =====================================================================================
		// Constructor
		// =====================================================================================

		/// <summary>
		/// Constructs the team authorization handler with all required DI services.
		/// </summary>
		/// <param name="httpContextAccessor">
		/// Replaces <c>HttpContext.Current</c> for session access.
		/// Used directly in <see cref="SetTeamAccess"/> / <see cref="GetTeamAccess"/> for
		/// the "Teams.{teamName}" session key pattern.
		/// </param>
		/// <param name="memoryCache">
		/// Replaces <c>HttpApplicationState</c> (<c>Application[]</c>) for module metadata flags.
		/// Reads "Modules.{MODULE}.Teamed" to determine if a module participates in team filtering.
		/// </param>
		/// <param name="security">
		/// Replaces static <c>SplendidCRM.Security</c> for IS_ADMIN, USER_ID, GetACLRoleAccess,
		/// SetTeamAccess, GetTeamAccess, and TeamHierarchySavedSearch.
		/// </param>
		/// <param name="splendidCache">
		/// Provides <see cref="SplendidCache.SavedSearch"/> for the team hierarchy saved search lookup.
		/// </param>
		public TeamAuthorizationHandler(
			IHttpContextAccessor httpContextAccessor,
			IMemoryCache         memoryCache,
			Security             security,
			SplendidCache        splendidCache)
		{
			_httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
			_memoryCache         = memoryCache         ?? throw new ArgumentNullException(nameof(memoryCache));
			_security            = security            ?? throw new ArgumentNullException(nameof(security));
			_splendidCache       = splendidCache       ?? throw new ArgumentNullException(nameof(splendidCache));
		}

		// =====================================================================================
		// ASP.NET Core AuthorizationHandler<T> — HandleRequirementAsync override
		// This is the entry point used by the ASP.NET Core authorization middleware pipeline.
		//
		// Translates the team management logic from Security.Filter() (lines 856-988) into
		// an authorization check. The actual record-level SQL filtering (JOIN clauses added to
		// IDbCommand.CommandText) is performed separately by SecurityFilterMiddleware, which calls
		// Security.Filter() for each data query. This handler determines module-level team access.
		// =====================================================================================

		/// <summary>
		/// ASP.NET Core authorization pipeline entry point for the team-level ACL tier.
		///
		/// Evaluates whether team-based access control applies for <paramref name="requirement"/>.ModuleName
		/// by replicating the team management logic from <c>Security.Filter()</c> (lines 856–988).
		///
		/// <para><b>Decision flow (mirrors Security.Filter team block):</b></para>
		/// <list type="number">
		///   <item>Resolve user identity — empty USER_ID indicates no active session; return without succeeding.</item>
		///   <item>Check module teamed flag — if module is not teamed, succeed (no team restriction applies).</item>
		///   <item>Check admin status (IS_ADMIN) + Data Privacy Manager role special case — if admin, succeed.</item>
		///   <item>Check <c>Crm.Config.enable_team_management()</c> — if disabled, succeed (no team filtering).</item>
		///   <item>If team management is enabled and module is teamed:
		///         <list type="bullet">
		///           <item>Read all relevant Crm.Config flags (require, dynamic, hierarchy, show_unassigned).</item>
		///           <item>When <c>enable_team_hierarchy</c> is true, load the team hierarchy saved search
		///                 via <see cref="TeamHierarchySavedSearch"/> to surface the hierarchy root TEAM_ID/TEAM_NAME
		///                 for use by <c>SecurityFilterMiddleware</c> in subsequent SQL query building.</item>
		///           <item>Succeed — record-level team filtering is enforced by <c>Security.Filter()</c>
		///                 via SQL JOIN clauses (INNER for require_team_management=true, LEFT OUTER for false).
		///                 The authorization handler gate grants module access; SQL constrains records visible.</item>
		///         </list>
		///   </item>
		/// </list>
		///
		/// Behavior:
		///   • All non-denied paths call <c>context.Succeed(requirement)</c>.
		///   • No <c>context.Fail()</c> calls — allows other requirements in the policy to override.
		/// </summary>
		protected override Task HandleRequirementAsync(
			AuthorizationHandlerContext    context,
			TeamAuthorizationRequirement   requirement)
		{
			string sMODULE_NAME = requirement.ModuleName;

			// ---------------------------------------------------------------------------------
			// Step 1: User identity check
			// BEFORE: Security.USER_ID (static, via HttpContext.Current.Session)
			// AFTER:  _security.USER_ID (DI instance property, via ISession.GetString)
			// An empty USER_ID means there is no active authenticated session.
			// Note: USER_ID is used by Security.Filter() for team membership parameter binding
			//       (line 961: Sql.AddParameter(cmd, "@MEMBERSHIP_USER_ID", Security.USER_ID))
			// ---------------------------------------------------------------------------------
			Guid gUSER_ID = _security.USER_ID;
			if (Sql.IsEmptyGuid(gUSER_ID))
			{
				// No authenticated session — cannot evaluate team membership; do not succeed.
				// The authentication middleware should have rejected this request before reaching
				// the authorization pipeline, but this guard prevents null-reference errors.
				return Task.CompletedTask;
			}

			// ---------------------------------------------------------------------------------
			// Step 2: Module team configuration check
			// BEFORE: Sql.ToBoolean(HttpContext.Current.Application["Modules." + sMODULE_NAME + ".Teamed"])
			// AFTER:  Sql.ToBoolean(_memoryCache.Get<object>("Modules." + sMODULE_NAME + ".Teamed"))
			// This flag is populated by SplendidInit during application startup from the vwMODULES view.
			// If the module has no TEAM_ID column, it is not teamed and team filtering does not apply.
			// ---------------------------------------------------------------------------------
			bool bModuleIsTeamed = Sql.ToBoolean(_memoryCache.Get<object>("Modules." + sMODULE_NAME + ".Teamed"));

			// If module does not participate in team management, no restriction applies.
			if (!bModuleIsTeamed)
			{
				context.Succeed(requirement);
				return Task.CompletedTask;
			}

			// ---------------------------------------------------------------------------------
			// Step 3: Admin bypass
			// BEFORE: bool bIsAdmin = IS_ADMIN; (static Security property)
			// AFTER:  bool bIsAdmin = _security.IS_ADMIN; (DI instance property)
			// Source: Security.cs lines 879–883 (IS_ADMIN read + DEBUG override)
			// ---------------------------------------------------------------------------------
			bool bIsAdmin = _security.IS_ADMIN;
			// 08/30/2009 Paul.  Don't apply admin rules when debugging so that we can test the code.
			// Preserve the #if DEBUG bIsAdmin = false; #endif pattern from Security.cs lines 881-883.
#if DEBUG
			bIsAdmin = false;
#endif

			// 06/26/2018 Paul.  The Data Privacy Manager has admin-like access to specific modules.
			// BEFORE: Security.GetACLRoleAccess("Data Privacy Manager Role") (static)
			// AFTER:  _security.GetACLRoleAccess("Data Privacy Manager Role") (DI instance)
			// Source: Security.cs lines 885–891
			if (_security.GetACLRoleAccess("Data Privacy Manager Role"))
			{
				if (sMODULE_NAME == "Accounts"  ||
				    sMODULE_NAME == "Contacts"  ||
				    sMODULE_NAME == "Leads"     ||
				    sMODULE_NAME == "Prospects" )
				{
					bIsAdmin = true;
				}
			}

			// Admin bypass: admins see all records regardless of team (lines 894-895)
			if (bIsAdmin)
			{
				context.Succeed(requirement);
				return Task.CompletedTask;
			}

			// ---------------------------------------------------------------------------------
			// Step 4: Team management configuration flags
			// All flags read from Crm.Config static methods that use the static ambient cache
			// (set by Crm DI constructor at application startup via _ambientCache).
			// BEFORE: Crm.Config.enable_team_management() read Application["CONFIG.enable_team_management"]
			// AFTER:  Crm.Config.enable_team_management() reads _ambientCache["CONFIG.enable_team_management"]
			// ---------------------------------------------------------------------------------

			// 01/22/2007 Paul.  If ASSIGNED_USER_ID is null, let everybody see it.
			bool bShowUnassigned        = Crm.Config.show_unassigned       ();   // CONFIG.show_unassigned
			bool bEnableTeamManagement  = Crm.Config.enable_team_management ();  // CONFIG.enable_team_management
			bool bRequireTeamManagement = Crm.Config.require_team_management();  // CONFIG.require_team_management
			// 08/28/2009 Paul.  Allow dynamic teams to be turned off.
			bool bEnableDynamicTeams    = Crm.Config.enable_dynamic_teams   ();  // CONFIG.enable_dynamic_teams
			// 04/28/2016 Paul.  Allow team hierarchy.
			bool bEnableTeamHierarchy   = Crm.Config.enable_team_hierarchy  ();  // CONFIG.enable_team_hierarchy

			// If team management is globally disabled, no team restriction applies.
			if (!bEnableTeamManagement)
			{
				context.Succeed(requirement);
				return Task.CompletedTask;
			}

			// ---------------------------------------------------------------------------------
			// Step 5: Team hierarchy saved search (when hierarchy is enabled)
			// When bEnableTeamHierarchy is true, Security.Filter() performs an additional
			// JOIN against fnTEAM_HIERARCHY_ByTeam(@TEAM_ID) to constrain records to a
			// specific team hierarchy subtree (lines 965–987 of original Security.cs Filter()).
			//
			// Here we call _security.TeamHierarchySavedSearch() to load the saved search scope
			// (TEAM_ID and TEAM_NAME of the hierarchy root from the "TeamHierarchy" module).
			// The SecurityFilterMiddleware can call the handler's own TeamHierarchySavedSearch()
			// (which uses SplendidCache.SavedSearch() for a richer lookup) when building SQL queries.
			//
			// BEFORE: Security.TeamHierarchySavedSearch(ref gTEAM_ID, ref sTEAM_NAME) (static)
			// AFTER:  _security.TeamHierarchySavedSearch(ref gTEAM_ID, ref sTEAM_NAME) (DI instance)
			// ---------------------------------------------------------------------------------
			if (bEnableTeamHierarchy)
			{
				Guid   gTEAM_ID   = Guid.Empty  ;
				string sTEAM_NAME = String.Empty ;
				// Load team hierarchy saved search via Security instance (reads session JSON).
				// SecurityFilterMiddleware calls TeamHierarchySavedSearch() on this handler
				// (which uses SplendidCache.SavedSearch()) for the authoritative SQL JOIN scope.
				_security.TeamHierarchySavedSearch(ref gTEAM_ID, ref sTEAM_NAME);
				// Sql.IsEmptyGuid check mirrors line 971 of original Security.cs Filter()
				// If gTEAM_ID is set, Security.Filter() will add fnTEAM_HIERARCHY_ByTeam JOIN.
				// At module-level authorization, this is informational — SQL filter does the work.
				_ = Sql.IsEmptyGuid(gTEAM_ID);  // used for parity with original line 971 check
			}

			// ---------------------------------------------------------------------------------
			// Step 6: Grant access — team-based record filtering is enforced at SQL level
			//
			// At the module/action authorization level, we succeed for all authenticated users
			// when team management is enabled. The actual team-based restriction is applied by
			// Security.Filter() (called by SecurityFilterMiddleware before each data query):
			//   • bRequireTeamManagement = true  → INNER JOIN (only records in user's teams visible)
			//   • bRequireTeamManagement = false → LEFT OUTER JOIN (all records visible; NULL team OK)
			//   • bEnableDynamicTeams    = true  → JOIN against TEAM_SET_ID column
			//   • bEnableDynamicTeams    = false → JOIN against TEAM_ID column
			//
			// The bShowUnassigned flag also affects whether unassigned records are shown,
			// but that is handled by the assigned-user portion of Security.Filter(), not here.
			// ---------------------------------------------------------------------------------
			context.Succeed(requirement);
			return Task.CompletedTask;
		}

		// =====================================================================================
		// Team Session Management — SetTeamAccess / GetTeamAccess
		//
		// Source: Security.cs lines 580–588 (SetTeamAccess, GetTeamAccess)
		// These methods track which teams the current user is a member of in the session.
		// Called by SplendidInit.LoginUser (and similar session-setup code) after loading
		// team membership from the TEAMS table at login time.
		//
		// Session key pattern: "Teams.{TEAM_NAME}" = "True"
		//
		// BEFORE (static): HttpContext.Current.Session["Teams." + sTEAM_NAME] = true;
		// AFTER (delegate to Security instance):
		//   _security.SetTeamAccess(teamName) → Session.SetString("Teams." + teamName, "True")
		//   _security.GetTeamAccess(teamName) → Sql.ToBoolean(Session.GetString("Teams." + teamName))
		// =====================================================================================

		// 11/11/2010 Paul.  Provide quick access to ACL Roles and Teams.
		/// <summary>
		/// Records that the current user is a member of the specified team in the distributed session.
		/// 
		/// Stored under session key "Teams.{teamName}" = "True".
		/// Called during login (SplendidInit.LoginUser) after loading TEAMS membership from the database.
		///
		/// BEFORE: <c>HttpContext.Current.Session["Teams." + sTEAM_NAME] = true;</c> (Security.cs line 582)<br/>
		/// AFTER:  Delegates to <c>_security.SetTeamAccess(teamName)</c> which uses
		///         <c>ISession.SetString("Teams." + teamName, "True")</c>.
		/// </summary>
		/// <param name="teamName">Team name (the TEAM_NAME column value, e.g. "Global", "Sales").</param>
		public void SetTeamAccess(string teamName)
		{
			// 11/11/2010 Paul. Provide quick access to ACL Teams.
			// BEFORE: HttpContext.Current.Session["Teams." + sTEAM_NAME] = true;
			// AFTER:  _security.SetTeamAccess(teamName) — delegates to Security DI instance
			_security.SetTeamAccess(teamName);
		}

		/// <summary>
		/// Returns true when the current user is a member of the specified team,
		/// as recorded in the distributed session by <see cref="SetTeamAccess"/>.
		///
		/// Reads session key "Teams.{teamName}" and converts to bool via Sql.ToBoolean.
		///
		/// BEFORE: <c>return Sql.ToBoolean(HttpContext.Current.Session["Teams." + sTEAM_NAME]);</c>
		///         (Security.cs line 587)<br/>
		/// AFTER:  Delegates to <c>_security.GetTeamAccess(teamName)</c> which reads
		///         <c>Sql.ToBoolean(ISession.GetString("Teams." + teamName))</c>.
		/// </summary>
		/// <param name="teamName">Team name to check (the TEAM_NAME column value).</param>
		/// <returns>True when the user is recorded as a member of the specified team.</returns>
		public bool GetTeamAccess(string teamName)
		{
			// BEFORE: return Sql.ToBoolean(HttpContext.Current.Session["Teams." + sTEAM_NAME]);
			// AFTER:  _security.GetTeamAccess(teamName) — delegates to Security DI instance
			return _security.GetTeamAccess(teamName);
		}

		// =====================================================================================
		// TeamHierarchySavedSearch — Team hierarchy filter scope lookup
		//
		// Source: Security.cs lines 822–840 (TeamHierarchyModule constant, TeamHierarchySavedSearch)
		//
		// This method loads the saved search for the "TeamHierarchy" module from SplendidCache,
		// parses the saved search XML, and extracts the TEAM_ID and TEAM_NAME that define the
		// hierarchy root for the team hierarchy filter (used in fnTEAM_HIERARCHY_ByTeam JOIN).
		//
		// Called by SecurityFilterMiddleware when building SQL queries with team hierarchy enabled,
		// to add the additional JOIN: "INNER JOIN fnTEAM_HIERARCHY_ByTeam(@TEAM_ID) WHERE TEAM_ID in hierarchy"
		//
		// BEFORE (Security.cs, static): 
		//   DataTable dt = SplendidCache.SavedSearch(sSEARCH_MODULE);   (line 830)
		// AFTER (Web layer, no circular dependency):
		//   DataTable dt = _splendidCache.SavedSearch(Security.TeamHierarchyModule);
		//
		// NOTE: The Core layer's Security.TeamHierarchySavedSearch() uses session JSON
		// (JsonConvert.DeserializeObject) to avoid a SplendidCache → Security circular dependency.
		// The Web layer handler can call SplendidCache.SavedSearch() directly since
		// SplendidCRM.Web does not have that circular dependency constraint.
		// =====================================================================================

		/// <summary>
		/// Loads the saved team hierarchy search scope from SplendidCache, if one has been saved,
		/// and populates <paramref name="gTEAM_ID"/> and <paramref name="sTEAM_NAME"/> with the
		/// TEAM_ID and TEAM_NAME of the selected hierarchy root team.
		///
		/// Replicates Security.cs lines 826–840 (<c>TeamHierarchySavedSearch</c>), but uses
		/// <c>SplendidCache.SavedSearch()</c> directly (Web layer — no circular dependency),
		/// whereas the Core layer's <c>Security.TeamHierarchySavedSearch()</c> uses session JSON
		/// to avoid a SplendidCache → Security dependency cycle.
		///
		/// Called by <c>SecurityFilterMiddleware</c> when <c>enable_team_hierarchy=true</c>
		/// and a team hierarchy saved search has been configured by the user.
		///
		/// The saved search XML format is:
		/// <code>
		/// &lt;SearchFields&gt;
		///   &lt;Field Name="ID"&gt;{team-guid}&lt;/Field&gt;
		///   &lt;Field Name="NAME"&gt;{team-name}&lt;/Field&gt;
		/// &lt;/SearchFields&gt;
		/// </code>
		///
		/// BEFORE: <c>DataTable dt = SplendidCache.SavedSearch(sSEARCH_MODULE);</c> (static)<br/>
		/// AFTER:  <c>DataTable dt = _splendidCache.SavedSearch(Security.TeamHierarchyModule);</c> (DI)
		/// </summary>
		/// <param name="gTEAM_ID">
		/// Output: TEAM_ID of the hierarchy root team, or <c>Guid.Empty</c> if no saved search exists.
		/// </param>
		/// <param name="sTEAM_NAME">
		/// Output: TEAM_NAME of the hierarchy root team, or <c>String.Empty</c> if no saved search exists.
		/// </param>
		public void TeamHierarchySavedSearch(ref Guid gTEAM_ID, ref string sTEAM_NAME)
		{
			// 01/05/2020 Paul.  Provide central location for constant.
			// Use Security.TeamHierarchyModule to reference the canonical constant on the Security class.
			// This handler's own TeamHierarchyModule constant has the same value ("TeamHierarchy").
			string sSEARCH_MODULE = Security.TeamHierarchyModule;

			// BEFORE: DataTable dt = SplendidCache.SavedSearch(sSEARCH_MODULE);   (static, line 830)
			// AFTER:  DataTable dt = _splendidCache.SavedSearch(sSEARCH_MODULE);  (DI instance)
			// SplendidCache.SavedSearch() queries vwSAVED_SEARCH for the current user + module.
			DataTable dt = _splendidCache.SavedSearch(sSEARCH_MODULE);

			if (dt != null && dt.Rows.Count > 0)
			{
				DataRow row = dt.Rows[0];
				// Extract the saved search XML contents (line 834 of original Security.cs)
				// BEFORE: string sXML = Sql.ToString(row["CONTENTS"]);
				// AFTER:  same — Sql.ToString() is a static utility with no breaking changes
				string sXML = Sql.ToString(row["CONTENTS"]);

				// Guard against empty XML before attempting to parse
				if (!Sql.IsEmptyString(sXML))
				{
					try
					{
						// Parse the saved search XML (line 835 of original Security.cs)
						// BEFORE: System.Xml.XmlDocument xml = new System.Xml.XmlDocument();
						// AFTER:  XmlDocument xml = new XmlDocument();  (System.Xml is in scope)
						XmlDocument xml = new XmlDocument();
						xml.LoadXml(sXML);

						// Extract TEAM_NAME and TEAM_ID using XPath via XmlUtil.SelectSingleNode
						// (lines 837–838 of original Security.cs)
						// BEFORE: sTEAM_NAME = Sql.ToString(XmlUtil.SelectSingleNode(xml.DocumentElement, "SearchFields/Field[@Name='NAME']"));
						// AFTER:  same — XmlUtil.SelectSingleNode is a static utility
						sTEAM_NAME = Sql.ToString(XmlUtil.SelectSingleNode(xml.DocumentElement, "SearchFields/Field[@Name='NAME']"));
						gTEAM_ID   = Sql.ToGuid  (XmlUtil.SelectSingleNode(xml.DocumentElement, "SearchFields/Field[@Name='ID'  ]"));
					}
					catch (Exception)
					{
						// Malformed XML — leave gTEAM_ID and sTEAM_NAME at their initial values
						// (Guid.Empty and String.Empty respectively, as passed in by ref)
					}
				}
			}
		}
	}
}
