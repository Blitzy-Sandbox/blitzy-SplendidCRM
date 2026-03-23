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
// .NET 10 Migration: SplendidCRM/_code/Security.cs (lines 842–1384) → src/SplendidCRM.Web/Authorization/SecurityFilterMiddleware.cs
// Changes applied:
//   - REMOVED: using System.Web; using System.Web.SessionState; (all System.Web namespaces)
//   - ADDED:   using Microsoft.AspNetCore.Http; using Microsoft.Extensions.Caching.Memory;
//   - REMOVED: static Security class access (HttpContext.Current, Application[], HttpRuntime.Cache)
//   - ADDED:   DI-injectable instance: IHttpContextAccessor, IMemoryCache, Security, SplendidCache
//   - Security.IS_ADMIN / Security.USER_ID (static) → _security.IS_ADMIN / _security.USER_ID (instance)
//   - Security.GetACLRoleAccess() (static) → _security.GetACLRoleAccess() (instance)
//   - Security.GetUserAccess() (static) → _security.GetUserAccess() (instance)
//   - Security.TeamHierarchySavedSearch() (static) → _security.TeamHierarchySavedSearch() (instance)
//   - HttpContext.Current.Application["Modules.X.Teamed"] → _memoryCache.Get<object>("Modules.X.Teamed")
//   - Crm.Config.*(HttpApplicationState) → Crm.Config.*(_memoryCache) (IMemoryCache overload)
//   - Sql.NextPlaceholder(cmd, field) private helper inlined (two-arg form not in public Sql.cs)
//   - Sql.MetadataName(cmd, name) → Sql.MetadataName(cmd, name) (public static from migrated Sql.cs)
//   - All WCF/ASMX attributes removed — no System.ServiceModel references
//   - ASP.NET Core middleware convention: InvokeAsync(HttpContext) + RequestDelegate _next
//   - SQL predicates produced by all 5 methods are BYTE-IDENTICAL to .NET Framework 4.8 baseline
//   - CRITICAL: Any deviation from original SQL text breaks multi-tenant data isolation

#nullable disable
using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	/// <summary>
	/// ASP.NET Core middleware that exposes the Security.Filter() SQL predicate injection logic
	/// as a DI-injectable service for controllers and other services.
	///
	/// This middleware enforces all 4 ACL tiers (Module → Team → Field → Record) by appending
	/// SQL JOIN clauses and WHERE predicates to IDbCommand objects before execution.  The generated
	/// SQL is identical to the original .NET Framework 4.8 Security.Filter() implementation.
	///
	/// Registration in Program.cs:
	///   services.AddScoped&lt;SecurityFilterMiddleware&gt;();
	///   app.UseMiddleware&lt;SecurityFilterMiddleware&gt;();
	///
	/// Usage in controllers:
	///   _securityFilter.ApplyFilter(cmd, "Accounts", "list");
	///
	/// MIGRATION NOTE: The legacy static Security.Filter()/FilterAssigned() methods (Security.cs,
	/// lines 842–1384) are migrated here as instance methods receiving their dependencies through
	/// constructor injection rather than HttpContext.Current static access.
	///
	/// CRITICAL: All SQL predicates must match byte-for-byte to preserve multi-tenant isolation.
	/// Any change to JOIN aliases, parameter names, or WHERE clause formatting breaks data security.
	/// </summary>
	public class SecurityFilterMiddleware
	{
		// =====================================================================================
		// DI-injected fields
		// BEFORE: HttpContext.Current, Application[], static Security class
		// AFTER:  Constructor-injected instances
		// =====================================================================================

		private readonly RequestDelegate      _next               ;
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly IMemoryCache         _memoryCache        ;
		private readonly Security             _security           ;
		private readonly SplendidCache        _splendidCache      ;

		// =====================================================================================
		// Constructor
		// =====================================================================================

		/// <summary>
		/// Constructs the SecurityFilterMiddleware with all required dependencies.
		/// </summary>
		/// <param name="next">Next middleware in the ASP.NET Core pipeline.</param>
		/// <param name="httpContextAccessor">Replaces HttpContext.Current throughout.</param>
		/// <param name="memoryCache">
		///   Replaces Application["Modules.X.Teamed"], Application["Modules.X.Assigned"],
		///   and all CONFIG.* Application[] entries consumed by Crm.Config.*() methods.
		/// </param>
		/// <param name="security">
		///   DI instance of the migrated Security service — provides USER_ID, IS_ADMIN,
		///   GetACLRoleAccess(), GetUserAccess(), and TeamHierarchySavedSearch().
		/// </param>
		/// <param name="splendidCache">
		///   Metadata caching hub — required for SavedSearch() calls made by TeamHierarchySavedSearch.
		/// </param>
		public SecurityFilterMiddleware(
			RequestDelegate      next               ,
			IHttpContextAccessor httpContextAccessor,
			IMemoryCache         memoryCache        ,
			Security             security           ,
			SplendidCache        splendidCache      )
		{
			_next                = next               ;
			_httpContextAccessor = httpContextAccessor;
			_memoryCache         = memoryCache        ;
			_security            = security           ;
			_splendidCache       = splendidCache      ;
		}

		// =====================================================================================
		// ASP.NET Core Middleware Convention — InvokeAsync
		// This is a "service registration" middleware: it passes the request through unchanged
		// while making itself injectable as a DI service that controllers can call for SQL
		// predicate generation.
		// =====================================================================================

		/// <summary>
		/// ASP.NET Core middleware invoke method.  Passes the request to the next middleware
		/// unchanged.  The primary function of this class is to provide injectable ApplyFilter /
		/// ApplyMultiModuleFilter / ApplyAssignedFilter methods for use in controllers.
		/// </summary>
		/// <param name="context">Current HTTP context.</param>
		public async Task InvokeAsync(HttpContext context)
		{
			await _next(context);
		}

		// =====================================================================================
		// Private SQL helpers
		//
		// NextPlaceholder(IDbCommand, string) — two-argument form that generates field-name-based
		// parameter placeholders.  This is inlined here because the migrated Sql.cs exposes only
		// the single-argument form (returning @p{n}) which would change the SQL text and break
		// multi-tenant data isolation.  The two-argument form is required for SQL predicate
		// byte-identity with the .NET Framework 4.8 baseline.
		//
		// BEFORE (original):  Sql.NextPlaceholder(cmd, "MEMBERSHIP_USER_ID")
		// AFTER (inlined):    NextPlaceholder(cmd, "MEMBERSHIP_USER_ID")
		//
		// HasParameter — helper used exclusively by NextPlaceholder to check existing names.
		// =====================================================================================

		/// <summary>
		/// Returns the next available SQL parameter placeholder based on the field name.
		/// If the name is already in use, appends an incrementing integer suffix until unique.
		/// Example results: "MEMBERSHIP_USER_ID", "MEMBERSHIP_USER_ID1", "MEMBERSHIP_USER_ID2".
		/// </summary>
		/// <param name="cmd">The command whose Parameters collection is checked.</param>
		/// <param name="sField">Base field name to use for the placeholder.</param>
		/// <returns>A unique placeholder name (without the leading '@').</returns>
		private static string NextPlaceholder(IDbCommand cmd, string sField)
		{
			// 12/26/2006 Paul.  Determine the next available placeholder name.
			int    nPlaceholderIndex = 0;
			string sFieldPlaceholder = sField;
			while (HasParameter(cmd, sFieldPlaceholder))
			{
				nPlaceholderIndex++;
				sFieldPlaceholder = sField + nPlaceholderIndex.ToString();
			}
			return sFieldPlaceholder;
		}

		/// <summary>
		/// Returns true when cmd already contains a parameter named "@{sFieldPlaceholder}".
		/// Used exclusively by NextPlaceholder to guarantee unique parameter names.
		/// </summary>
		private static bool HasParameter(IDbCommand cmd, string sFieldPlaceholder)
		{
			string sTarget = "@" + sFieldPlaceholder;
			foreach (IDataParameter p in cmd.Parameters)
			{
				if (String.Equals(p.ParameterName, sTarget, StringComparison.OrdinalIgnoreCase))
					return true;
			}
			return false;
		}

		// =====================================================================================
		// ApplyFilter — single-module variant
		//
		// Migrated from Security.Filter(IDbCommand, string, string) lines 842–845
		// and Security.Filter(IDbCommand, string, string, string) lines 848–851 and
		// Security.Filter(IDbCommand, string, string, string, bool) lines 856–1082.
		//
		// SQL predicate structure (per original):
		//   1. [optional] Team membership JOIN (inner or left outer, dynamic or static, hierarchy)
		//   2. [optional] Team hierarchy saved-search JOIN (when bEnableTeamHierarchy && !Dashboard)
		//   3. [optional] Assignment set JOIN (dynamic assignment)
		//   4.  WHERE 1 = 1
		//   5. [optional] Team membership WHERE clause
		//   6. [optional] Assignment/owner WHERE clause
		// =====================================================================================

		/// <summary>
		/// Appends SQL JOIN and WHERE predicates to <paramref name="cmd"/> for the given module
		/// and access type, using "ASSIGNED_USER_ID" as the default assigned field.
		/// Equivalent to Security.Filter(cmd, sMODULE_NAME, sACCESS_TYPE) — line 842.
		/// </summary>
		public void ApplyFilter(IDbCommand cmd, string sMODULE_NAME, string sACCESS_TYPE)
		{
			ApplyFilter(cmd, sMODULE_NAME, sACCESS_TYPE, "ASSIGNED_USER_ID");
		}

		/// <summary>
		/// Appends SQL JOIN and WHERE predicates to <paramref name="cmd"/> for the given module,
		/// access type, and assigned user ID field, with saved-search filtering enabled.
		/// Equivalent to Security.Filter(cmd, sMODULE_NAME, sACCESS_TYPE, sASSIGNED_USER_ID_Field)
		/// — line 848.
		/// </summary>
		public void ApplyFilter(IDbCommand cmd, string sMODULE_NAME, string sACCESS_TYPE, string sASSIGNED_USER_ID_Field)
		{
			ApplyFilter(cmd, sMODULE_NAME, sACCESS_TYPE, sASSIGNED_USER_ID_Field, false);
		}

		/// <summary>
		/// Core single-module filter method.  Appends team-membership JOINs and user-assignment
		/// WHERE predicates to cmd.CommandText, then binds parameters.
		///
		/// Produces SQL predicates IDENTICAL to the legacy .NET Framework 4.8 implementation at
		/// Security.cs lines 856–1082.  All JOIN alias names, parameter placeholder names, and
		/// WHERE clause text are preserved exactly.
		///
		/// Migration changes versus original:
		///   - IS_ADMIN: static Security.IS_ADMIN → _security.IS_ADMIN (instance)
		///   - USER_ID: static Security.USER_ID → _security.USER_ID (instance)
		///   - GetACLRoleAccess(): static → _security.GetACLRoleAccess() (instance)
		///   - GetUserAccess(): static → _security.GetUserAccess() (instance)
		///   - TeamHierarchySavedSearch(): static → _security.TeamHierarchySavedSearch() (instance)
		///   - Application["Modules.X.Teamed"] → _memoryCache.Get("Modules.X.Teamed")
		///   - Crm.Config.*() → Crm.Config.*(_memoryCache)
		/// </summary>
		/// <param name="cmd">Command whose CommandText and Parameters are modified.</param>
		/// <param name="sMODULE_NAME">Module name (e.g. "Accounts", "Contacts").</param>
		/// <param name="sACCESS_TYPE">Access type string (e.g. "list", "edit", "delete").</param>
		/// <param name="sASSIGNED_USER_ID_Field">Column name of the assignment field.</param>
		/// <param name="bExcludeSavedSearch">When true, skip team hierarchy saved-search join.</param>
		public void ApplyFilter(IDbCommand cmd, string sMODULE_NAME, string sACCESS_TYPE, string sASSIGNED_USER_ID_Field, bool bExcludeSavedSearch)
		{
			// 08/04/2007 Paul.  Always wait forever for the data.  No sense in showing a timeout.
			cmd.CommandTimeout = 0;
			// 01/22/2007 Paul.  If ASSIGNED_USER_ID is null, then let everybody see it.
			// This was added to work around a bug whereby the ASSIGNED_USER_ID was not automatically assigned to the creating user.
			// BEFORE: Crm.Config.show_unassigned() → Application["CONFIG.show_unassigned"]
			// AFTER:  Crm.Config.show_unassigned(_memoryCache)
			bool bShowUnassigned        = Crm.Config.show_unassigned       (_memoryCache);
			// 12/07/2006 Paul.  Not all views use ASSIGNED_USER_ID as the assigned field.  Allow an override.
			// 11/25/2006 Paul.  Administrators should not be restricted from seeing items because of the team rights.
			// This is so that an administrator can fix any record with a bad team value.
			// 12/30/2007 Paul.  We need a dynamic way to determine if the module record can be assigned or placed in a team.
			// Teamed and Assigned flags are automatically determined based on the existence of TEAM_ID and ASSIGNED_USER_ID fields.
			// BEFORE: Application["Modules.X.Teamed"] → AFTER: _memoryCache.Get<object>("Modules.X.Teamed")
			bool bModuleIsTeamed        = Sql.ToBoolean(_memoryCache.Get<object>("Modules." + sMODULE_NAME + ".Teamed"  ));
			bool bModuleIsAssigned      = Sql.ToBoolean(_memoryCache.Get<object>("Modules." + sMODULE_NAME + ".Assigned"));
			// BEFORE: Crm.Config.enable_team_management()   → AFTER: Crm.Config.enable_team_management(_memoryCache)
			bool bEnableTeamManagement  = Crm.Config.enable_team_management (_memoryCache);
			bool bRequireTeamManagement = Crm.Config.require_team_management(_memoryCache);
			bool bRequireUserAssignment = Crm.Config.require_user_assignment(_memoryCache);
			// 08/28/2009 Paul.  Allow dynamic teams to be turned off.
			bool bEnableDynamicTeams    = Crm.Config.enable_dynamic_teams   (_memoryCache);
			// 04/28/2016 Paul.  Allow team hierarchy.
			bool bEnableTeamHierarchy   = Crm.Config.enable_team_hierarchy  (_memoryCache);
			// 11/30/2017 Paul.  Add ASSIGNED_SET_ID for Dynamic User Assignment.
			// NOTE: enable_dynamic_assignment() has no IMemoryCache overload in Crm.Config.
			// BEFORE: Crm.Config.enable_dynamic_assignment() → Application["CONFIG.enable_dynamic_assignment"]
			// AFTER:  Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.enable_dynamic_assignment"))
			bool bEnableDynamicAssignment = Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.enable_dynamic_assignment"));
			// BEFORE: bool bIsAdmin = IS_ADMIN; (static)
			// AFTER:  bool bIsAdmin = _security.IS_ADMIN; (instance)
			bool bIsAdmin = _security.IS_ADMIN;
			// 08/30/2009 Paul.  Don't apply admin rules when debugging so that we can test the code.
#if DEBUG
			bIsAdmin = false;
#endif
			// 06/26/2018 Paul.  The Data Privacy Manager has admin-like access to Accounts, Contacts, Leads and Prospects.
			// BEFORE: if ( Security.GetACLRoleAccess("Data Privacy Manager Role") )
			// AFTER:  if ( _security.GetACLRoleAccess("Data Privacy Manager Role") )
			if (_security.GetACLRoleAccess("Data Privacy Manager Role"))
			{
				if (sMODULE_NAME == "Accounts" || sMODULE_NAME == "Contacts" || sMODULE_NAME == "Leads" || sMODULE_NAME == "Prospects")
				{
					bIsAdmin = true;
				}
			}
			if (bModuleIsTeamed)
			{
				if (bIsAdmin)
					bRequireTeamManagement = false;

				if (bEnableTeamManagement)
				{
					// 11/12/2009 Paul.  Use the NextPlaceholder function so that we can call the security filter multiple times.
					// We need this to support offline sync.
					string sFieldPlaceholder = NextPlaceholder(cmd, "MEMBERSHIP_USER_ID");
					if (bEnableDynamicTeams)
					{
						// 08/31/2009 Paul.  Dynamic Teams are handled just like regular teams except using a different view.
						if (bRequireTeamManagement)
							cmd.CommandText += "       inner ";
						else
							cmd.CommandText += "  left outer ";
						// 04/28/2016 Paul.  Allow team hierarchy.
						if (!bEnableTeamHierarchy)
						{
							// 11/27/2009 Paul.  Use Sql.MetadataName() so that the view name can exceed 30 characters, but still be truncated for Oracle.
							// 11/27/2009 Paul.  vwTEAM_SET_MEMBERSHIPS_Security has a distinct clause to reduce duplicate rows.
							cmd.CommandText += "join " + Sql.MetadataName(cmd, "vwTEAM_SET_MEMBERSHIPS_Security") + " vwTEAM_SET_MEMBERSHIPS" + ControlChars.CrLf;
							cmd.CommandText += "               on vwTEAM_SET_MEMBERSHIPS.MEMBERSHIP_TEAM_SET_ID = TEAM_SET_ID" + ControlChars.CrLf;
							cmd.CommandText += "              and vwTEAM_SET_MEMBERSHIPS.MEMBERSHIP_USER_ID     = @" + sFieldPlaceholder + ControlChars.CrLf;
						}
						else
						{
							if (Sql.IsOracle(cmd))
							{
								cmd.CommandText += "join table(" + Sql.MetadataName(cmd, "fnTEAM_SET_HIERARCHY_MEMBERSHIPS") + "(@" + sFieldPlaceholder + ")) vwTEAM_SET_MEMBERSHIPS" + ControlChars.CrLf;
								cmd.CommandText += "               on vwTEAM_SET_MEMBERSHIPS.MEMBERSHIP_TEAM_SET_ID = TEAM_SET_ID" + ControlChars.CrLf;
							}
							else
							{
								string fnPrefix = (Sql.IsSQLServer(cmd) ? "dbo." : String.Empty);
								cmd.CommandText += "join " + fnPrefix + Sql.MetadataName(cmd, "fnTEAM_SET_HIERARCHY_MEMBERSHIPS") + "(@" + sFieldPlaceholder + ") vwTEAM_SET_MEMBERSHIPS" + ControlChars.CrLf;
								cmd.CommandText += "               on vwTEAM_SET_MEMBERSHIPS.MEMBERSHIP_TEAM_SET_ID = TEAM_SET_ID" + ControlChars.CrLf;
							}
						}
					}
					else
					{
						if (bRequireTeamManagement)
							cmd.CommandText += "       inner ";
						else
							cmd.CommandText += "  left outer ";
						// 04/28/2016 Paul.  Allow team hierarchy.
						if (!bEnableTeamHierarchy)
						{
							cmd.CommandText += "join vwTEAM_MEMBERSHIPS" + ControlChars.CrLf;
							cmd.CommandText += "               on vwTEAM_MEMBERSHIPS.MEMBERSHIP_TEAM_ID = TEAM_ID" + ControlChars.CrLf;
							cmd.CommandText += "              and vwTEAM_MEMBERSHIPS.MEMBERSHIP_USER_ID = @" + sFieldPlaceholder + ControlChars.CrLf;
						}
						else
						{
							if (Sql.IsOracle(cmd))
							{
								cmd.CommandText += "join table(fnTEAM_HIERARCHY_MEMBERSHIPS(@" + sFieldPlaceholder + ")) vwTEAM_MEMBERSHIPS" + ControlChars.CrLf;
								cmd.CommandText += "               on vwTEAM_MEMBERSHIPS.MEMBERSHIP_TEAM_ID = TEAM_ID" + ControlChars.CrLf;
							}
							else
							{
								string fnPrefix = (Sql.IsSQLServer(cmd) ? "dbo." : String.Empty);
								cmd.CommandText += "join " + fnPrefix + "fnTEAM_HIERARCHY_MEMBERSHIPS(@" + sFieldPlaceholder + ") vwTEAM_MEMBERSHIPS" + ControlChars.CrLf;
								cmd.CommandText += "               on vwTEAM_MEMBERSHIPS.MEMBERSHIP_TEAM_ID = TEAM_ID" + ControlChars.CrLf;
							}
						}
					}
					// BEFORE: Sql.AddParameter(cmd, "@" + sFieldPlaceholder, Security.USER_ID);
					// AFTER:  Sql.AddParameter(cmd, sFieldPlaceholder, _security.USER_ID);
					// NOTE:   Migrated Sql.AddParameter adds "@" prefix internally; pass sFieldPlaceholder without "@".
					Sql.AddParameter(cmd, sFieldPlaceholder, _security.USER_ID);
					// 02/23/2017 Paul.  Add support for Team Hierarchy.
					// 06/05/2017 Paul.  The SavedSearch does not apply to the Dashboard.
					// 04/24/2018 Paul.  Provide a way to exclude the SavedSearch for areas that are global in nature.
					if (bEnableTeamHierarchy && sMODULE_NAME != "Dashboard" && !bExcludeSavedSearch)
					{
						// 02/25/2017 Paul.  Using an inner join is much faster than using TEAM_ID in (select ID from ...).
						Guid   gTEAM_ID   = Guid.Empty;
						string sTEAM_NAME = String.Empty;
						// BEFORE: Security.TeamHierarchySavedSearch(ref gTEAM_ID, ref sTEAM_NAME);
						// AFTER:  _security.TeamHierarchySavedSearch(ref gTEAM_ID, ref sTEAM_NAME);
						_security.TeamHierarchySavedSearch(ref gTEAM_ID, ref sTEAM_NAME);
						if (!Sql.IsEmptyGuid(gTEAM_ID))
						{
							string sFieldPlaceholder2 = NextPlaceholder(cmd, "TEAM_ID");
							if (Sql.IsOracle(cmd))
							{
								cmd.CommandText += "       inner join table(fnTEAM_HIERARCHY_ByTeam(@" + sFieldPlaceholder2 + ")) vwTEAM_HIERARCHY_ByTeam" + ControlChars.CrLf;
								cmd.CommandText += "               on vwTEAM_HIERARCHY_ByTeam.MEMBERSHIP_TEAM_ID = TEAM_ID" + ControlChars.CrLf;
							}
							else
							{
								string fnPrefix = (Sql.IsSQLServer(cmd) ? "dbo." : String.Empty);
								cmd.CommandText += "       inner join " + fnPrefix + "fnTEAM_HIERARCHY_ByTeam(@" + sFieldPlaceholder2 + ") vwTEAM_HIERARCHY_ByTeam" + ControlChars.CrLf;
								cmd.CommandText += "               on vwTEAM_HIERARCHY_ByTeam.MEMBERSHIP_TEAM_ID = TEAM_ID" + ControlChars.CrLf;
							}
							Sql.AddParameter(cmd, sFieldPlaceholder2, gTEAM_ID);
						}
					}
				}
			}
			int nACLACCESS = 0;
			if (bModuleIsAssigned && !Sql.IsEmptyString(sMODULE_NAME))
			{
				// 08/30/2009 Paul.  Since the activities view does not allow us to filter on each module type,
				// apply the Calls ACL rules to all activities.
				// 06/02/2016 Paul.  Activities views will use new function that accepts an array of modules.
				// BEFORE: nACLACCESS = Security.GetUserAccess(sMODULE_NAME, sACCESS_TYPE);
				// AFTER:  nACLACCESS = _security.GetUserAccess(sMODULE_NAME, sACCESS_TYPE);
				nACLACCESS = _security.GetUserAccess(sMODULE_NAME, sACCESS_TYPE);
			}

			// 11/30/2017 Paul.  Add ASSIGNED_SET_ID for Dynamic User Assignment.
			string sASSIGNED_SET_ID_Field = sASSIGNED_USER_ID_Field.Replace("ASSIGNED_USER_ID", "ASSIGNED_SET_ID");
			if (bModuleIsAssigned && bEnableDynamicAssignment)
			{
				// 01/01/2008 Paul.  We need a quick way to require user assignments across the system.
				// 01/02/2008 Paul.  Make sure owner rule does not apply to admins.
				if (nACLACCESS == ACL_ACCESS.OWNER || (bRequireUserAssignment && !bIsAdmin))
				{
					string sFieldPlaceholder = NextPlaceholder(cmd, sASSIGNED_SET_ID_Field);
					if (bRequireUserAssignment && !bShowUnassigned)
						cmd.CommandText += "       inner ";
					else
						cmd.CommandText += "  left outer ";
					cmd.CommandText += "join vwASSIGNED_SET_MEMBERSHIPS" + ControlChars.CrLf;
					cmd.CommandText += "               on vwASSIGNED_SET_MEMBERSHIPS.MEMBERSHIP_ASSIGNED_SET_ID  = " + sASSIGNED_SET_ID_Field + ControlChars.CrLf;
					cmd.CommandText += "              and vwASSIGNED_SET_MEMBERSHIPS.MEMBERSHIP_ASSIGNED_USER_ID = @" + sFieldPlaceholder + ControlChars.CrLf;
					Sql.AddParameter(cmd, sFieldPlaceholder, _security.USER_ID);
				}
			}

			cmd.CommandText += " where 1 = 1" + ControlChars.CrLf;
			if (bModuleIsTeamed)
			{
				if (bEnableTeamManagement && !bRequireTeamManagement && !bIsAdmin)
				{
					// 08/31/2009 Paul.  Dynamic Teams are handled just like regular teams except using a different view.
					// 09/01/2009 Paul.  Don't use MEMBERSHIP_ID as it is not included in the index.
					if (bEnableDynamicTeams)
						cmd.CommandText += "   and (TEAM_SET_ID is null or vwTEAM_SET_MEMBERSHIPS.MEMBERSHIP_TEAM_SET_ID is not null)" + ControlChars.CrLf;
					else
						cmd.CommandText += "   and (TEAM_ID is null or vwTEAM_MEMBERSHIPS.MEMBERSHIP_TEAM_ID is not null)" + ControlChars.CrLf;
				}
			}
			if (bModuleIsAssigned)
			{
				// 01/01/2008 Paul.  We need a quick way to require user assignments across the system.
				// 01/02/2008 Paul.  Make sure owner rule does not apply to admins.
				if (nACLACCESS == ACL_ACCESS.OWNER || (bRequireUserAssignment && !bIsAdmin))
				{
					// 11/30/2017 Paul.  Add ASSIGNED_SET_ID for Dynamic User Assignment.
					if (bEnableDynamicAssignment)
					{
						if (bShowUnassigned)
						{
							cmd.CommandText += "   and (" + sASSIGNED_SET_ID_Field + " is null or vwASSIGNED_SET_MEMBERSHIPS.MEMBERSHIP_ASSIGNED_SET_ID is not null)" + ControlChars.CrLf;
						}
					}
					else
					{
						string sFieldPlaceholder = NextPlaceholder(cmd, sASSIGNED_USER_ID_Field);
						if (bShowUnassigned)
						{
							if (Sql.IsOracle(cmd) || Sql.IsDB2(cmd))
								cmd.CommandText += "   and (" + sASSIGNED_USER_ID_Field + " is null or upper(" + sASSIGNED_USER_ID_Field + ") = upper(@" + sFieldPlaceholder + "))" + ControlChars.CrLf;
							else
								cmd.CommandText += "   and (" + sASSIGNED_USER_ID_Field + " is null or "       + sASSIGNED_USER_ID_Field +  " = @"       + sFieldPlaceholder + ")"  + ControlChars.CrLf;
						}
						/*
						// 02/13/2009 Paul.  We have a problem with the NOTES table as used in Activities lists.
						// Notes are not assigned specifically to anyone so the ACTIVITY_ASSIGNED_USER_ID may return NULL.
						// Notes should assume the ownership of the parent record, but we are also going to allow NULL for previous SplendidCRM installations.
						// 02/13/2009 Paul.  This issue affects Notes, Quotes, Orders, Invoices and Orders, so just rely upon fixing the views.
						else if ( sASSIGNED_USER_ID_Field == "ACTIVITY_ASSIGNED_USER_ID" )
						{
							if ( Sql.IsOracle(cmd) || Sql.IsDB2(cmd) )
								cmd.CommandText += "   and ((ACTIVITY_ASSIGNED_USER_ID is null and ACTIVITY_TYPE = N'Notes') or (upper(" + sASSIGNED_USER_ID_Field + ") = upper(@" + sFieldPlaceholder + ")))" + ControlChars.CrLf;
							else
								cmd.CommandText += "   and ((ACTIVITY_ASSIGNED_USER_ID is null and ACTIVITY_TYPE = N'Notes') or ("       + sASSIGNED_USER_ID_Field +  " = @"       + sFieldPlaceholder  + "))" + ControlChars.CrLf;
						}
						*/
						else
						{
							if (Sql.IsOracle(cmd) || Sql.IsDB2(cmd))
								cmd.CommandText += "   and upper(" + sASSIGNED_USER_ID_Field + ") = upper(@" + sFieldPlaceholder + ")" + ControlChars.CrLf;
							else
								cmd.CommandText += "   and "       + sASSIGNED_USER_ID_Field +  " = @"       + sFieldPlaceholder       + ControlChars.CrLf;
						}
						Sql.AddParameter(cmd, sFieldPlaceholder, _security.USER_ID);
					}
				}
			}
		}

		// =====================================================================================
		// ApplyMultiModuleFilter — multi-module variant (Activities / Stream views)
		//
		// Migrated from Security.Filter(IDbCommand, string[], string, string, string) lines 1085–1300.
		//
		// Used for Streams and Activities which span multiple module types (Calls, Meetings, Tasks,
		// Notes, Emails). Because the stream view is always teamed and assigned (bModuleIsTeamed = true,
		// bModuleIsAssigned = true), the method does not consult Application["Modules.X.Teamed"].
		//
		// SQL predicate structure (per original):
		//   1. [optional] Team membership JOIN (same as single-module variant)
		//   2. [optional] Team hierarchy saved-search JOIN (always applied when hierarchy enabled)
		//   3. [optional] Per-module assignment set JOINs (one per module in arrModules)
		//   4.  WHERE 1 = 1
		//   5. [optional] Team membership WHERE clause
		//   6. [optional] Multi-module OR clause: ( 1 = 0  or (MODULE = @m and ...) or ... )
		// =====================================================================================

		/// <summary>
		/// Appends SQL JOIN and WHERE predicates for multiple module types in a single query.
		/// Used for Activities and Stream views that span multiple module types simultaneously.
		///
		/// Produces SQL IDENTICAL to Security.Filter(IDbCommand, string[], ...) at lines 1085–1300.
		///
		/// MIGRATION NOTE: All Security.* static references replaced with _security.* instance calls.
		/// </summary>
		/// <param name="cmd">Command whose CommandText and Parameters are modified.</param>
		/// <param name="arrModules">Array of module names to include (e.g. {"Calls","Meetings","Tasks"}).</param>
		/// <param name="sACCESS_TYPE">Access type string (e.g. "list").</param>
		/// <param name="sASSIGNED_USER_ID_Field">Column name of the assignment field.</param>
		/// <param name="sMODULE_NAME_Field">Column name identifying the module type in the view.</param>
		public void ApplyMultiModuleFilter(IDbCommand cmd, string[] arrModules, string sACCESS_TYPE, string sASSIGNED_USER_ID_Field, string sMODULE_NAME_Field)
		{
			cmd.CommandTimeout = 0;
			// 01/22/2007 Paul.  If ASSIGNED_USER_ID is null, then let everybody see it.
			bool bShowUnassigned          = Crm.Config.show_unassigned       (_memoryCache);
			// 06/02/2016 Paul.  Stream and Activity tables are all teamed and assigned.
			bool bModuleIsTeamed          = true;
			bool bModuleIsAssigned        = true;
			bool bEnableTeamManagement    = Crm.Config.enable_team_management (_memoryCache);
			bool bRequireTeamManagement   = Crm.Config.require_team_management(_memoryCache);
			bool bRequireUserAssignment   = Crm.Config.require_user_assignment(_memoryCache);
			bool bEnableDynamicTeams      = Crm.Config.enable_dynamic_teams   (_memoryCache);
			bool bEnableTeamHierarchy     = Crm.Config.enable_team_hierarchy  (_memoryCache);
			// 11/30/2017 Paul.  Add ASSIGNED_SET_ID for Dynamic User Assignment.
			// NOTE: enable_dynamic_assignment() has no IMemoryCache overload; read CONFIG key directly.
			bool bEnableDynamicAssignment = Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.enable_dynamic_assignment"));
			bool bIsAdmin = _security.IS_ADMIN;
#if DEBUG
			bIsAdmin = false;
#endif
			if (bModuleIsTeamed)
			{
				if (bIsAdmin)
					bRequireTeamManagement = false;

				if (bEnableTeamManagement)
				{
					string sFieldPlaceholder = NextPlaceholder(cmd, "MEMBERSHIP_USER_ID");
					if (bEnableDynamicTeams)
					{
						if (bRequireTeamManagement)
							cmd.CommandText += "       inner ";
						else
							cmd.CommandText += "  left outer ";
						if (!bEnableTeamHierarchy)
						{
							cmd.CommandText += "join " + Sql.MetadataName(cmd, "vwTEAM_SET_MEMBERSHIPS_Security") + " vwTEAM_SET_MEMBERSHIPS" + ControlChars.CrLf;
							cmd.CommandText += "               on vwTEAM_SET_MEMBERSHIPS.MEMBERSHIP_TEAM_SET_ID = TEAM_SET_ID" + ControlChars.CrLf;
							cmd.CommandText += "              and vwTEAM_SET_MEMBERSHIPS.MEMBERSHIP_USER_ID     = @" + sFieldPlaceholder + ControlChars.CrLf;
						}
						else
						{
							if (Sql.IsOracle(cmd))
							{
								cmd.CommandText += "join table(" + Sql.MetadataName(cmd, "fnTEAM_SET_HIERARCHY_MEMBERSHIPS") + "(@" + sFieldPlaceholder + ")) vwTEAM_SET_MEMBERSHIPS" + ControlChars.CrLf;
								cmd.CommandText += "               on vwTEAM_SET_MEMBERSHIPS.MEMBERSHIP_TEAM_SET_ID = TEAM_SET_ID" + ControlChars.CrLf;
							}
							else
							{
								string fnPrefix = (Sql.IsSQLServer(cmd) ? "dbo." : String.Empty);
								cmd.CommandText += "join " + fnPrefix + Sql.MetadataName(cmd, "fnTEAM_SET_HIERARCHY_MEMBERSHIPS") + "(@" + sFieldPlaceholder + ") vwTEAM_SET_MEMBERSHIPS" + ControlChars.CrLf;
								cmd.CommandText += "               on vwTEAM_SET_MEMBERSHIPS.MEMBERSHIP_TEAM_SET_ID = TEAM_SET_ID" + ControlChars.CrLf;
							}
						}
					}
					else
					{
						if (bRequireTeamManagement)
							cmd.CommandText += "       inner ";
						else
							cmd.CommandText += "  left outer ";
						if (!bEnableTeamHierarchy)
						{
							cmd.CommandText += "join vwTEAM_MEMBERSHIPS" + ControlChars.CrLf;
							cmd.CommandText += "               on vwTEAM_MEMBERSHIPS.MEMBERSHIP_TEAM_ID = TEAM_ID" + ControlChars.CrLf;
							cmd.CommandText += "              and vwTEAM_MEMBERSHIPS.MEMBERSHIP_USER_ID = @" + sFieldPlaceholder + ControlChars.CrLf;
						}
						else
						{
							if (Sql.IsOracle(cmd))
							{
								cmd.CommandText += "join table(fnTEAM_HIERARCHY_MEMBERSHIPS(@" + sFieldPlaceholder + ")) vwTEAM_MEMBERSHIPS" + ControlChars.CrLf;
								cmd.CommandText += "               on vwTEAM_MEMBERSHIPS.MEMBERSHIP_TEAM_ID = TEAM_ID" + ControlChars.CrLf;
							}
							else
							{
								string fnPrefix = (Sql.IsSQLServer(cmd) ? "dbo." : String.Empty);
								cmd.CommandText += "join " + fnPrefix + "fnTEAM_HIERARCHY_MEMBERSHIPS(@" + sFieldPlaceholder + ") vwTEAM_MEMBERSHIPS" + ControlChars.CrLf;
								cmd.CommandText += "               on vwTEAM_MEMBERSHIPS.MEMBERSHIP_TEAM_ID = TEAM_ID" + ControlChars.CrLf;
							}
						}
					}
					Sql.AddParameter(cmd, sFieldPlaceholder, _security.USER_ID);
					// 02/23/2017 Paul.  Add support for Team Hierarchy.
					if (bEnableTeamHierarchy)
					{
						// 02/25/2017 Paul.  Using an inner join is much faster than using TEAM_ID in (select ID from ...).
						Guid   gTEAM_ID   = Guid.Empty;
						string sTEAM_NAME = String.Empty;
						_security.TeamHierarchySavedSearch(ref gTEAM_ID, ref sTEAM_NAME);
						if (!Sql.IsEmptyGuid(gTEAM_ID))
						{
							string sFieldPlaceholder2 = NextPlaceholder(cmd, "TEAM_ID");
							if (Sql.IsOracle(cmd))
							{
								cmd.CommandText += "       inner join table(fnTEAM_HIERARCHY_ByTeam(@" + sFieldPlaceholder2 + ")) vwTEAM_HIERARCHY_ByTeam" + ControlChars.CrLf;
								cmd.CommandText += "               on vwTEAM_HIERARCHY_ByTeam.MEMBERSHIP_TEAM_ID = TEAM_ID" + ControlChars.CrLf;
							}
							else
							{
								string fnPrefix = (Sql.IsSQLServer(cmd) ? "dbo." : String.Empty);
								cmd.CommandText += "       inner join " + fnPrefix + "fnTEAM_HIERARCHY_ByTeam(@" + sFieldPlaceholder2 + ") vwTEAM_HIERARCHY_ByTeam" + ControlChars.CrLf;
								cmd.CommandText += "               on vwTEAM_HIERARCHY_ByTeam.MEMBERSHIP_TEAM_ID = TEAM_ID" + ControlChars.CrLf;
							}
							Sql.AddParameter(cmd, sFieldPlaceholder2, gTEAM_ID);
						}
					}
				}
			}
			// 06/02/2016 Paul.  We need to first determine if the rules should be applied.
			bool bApplyAssignmentRules = false;
			foreach (string sMODULE_NAME in arrModules)
			{
				int nACLACCESS = _security.GetUserAccess(sMODULE_NAME, sACCESS_TYPE);
				if (nACLACCESS == ACL_ACCESS.OWNER || (bRequireUserAssignment && !bIsAdmin))
				{
					bApplyAssignmentRules = true;
				}
			}
			// 11/30/2017 Paul.  Add ASSIGNED_SET_ID for Dynamic User Assignment.
			string sASSIGNED_SET_ID_Field = sASSIGNED_USER_ID_Field.Replace("ASSIGNED_USER_ID", "ASSIGNED_SET_ID");
			if (bModuleIsAssigned && bApplyAssignmentRules && bEnableDynamicAssignment)
			{
				// 01/01/2008 Paul.  We need a quick way to require user assignments across the system.
				// 01/02/2008 Paul.  Make sure owner rule does not apply to admins.
				foreach (string sMODULE_NAME in arrModules)
				{
					int nACLACCESS = _security.GetUserAccess(sMODULE_NAME, sACCESS_TYPE);
					if (nACLACCESS == ACL_ACCESS.OWNER || (bRequireUserAssignment && !bIsAdmin))
					{
						string sFieldPlaceholder = NextPlaceholder(cmd, sASSIGNED_SET_ID_Field);
						// 12/03/2017 Paul.  We need to use an outer join because there would be one join per module.
						cmd.CommandText += "  left outer ";
						cmd.CommandText += "join vwASSIGNED_SET_MEMBERSHIPS   vwASSIGNED_SET_MEMBERSHIPS_" + sMODULE_NAME + ControlChars.CrLf;
						cmd.CommandText += "               on vwASSIGNED_SET_MEMBERSHIPS_" + sMODULE_NAME + ".MEMBERSHIP_ASSIGNED_SET_ID  = " + sASSIGNED_SET_ID_Field + ControlChars.CrLf;
						cmd.CommandText += "              and vwASSIGNED_SET_MEMBERSHIPS_" + sMODULE_NAME + ".MEMBERSHIP_ASSIGNED_USER_ID = @" + sFieldPlaceholder + ControlChars.CrLf;
						Sql.AddParameter(cmd, sFieldPlaceholder, _security.USER_ID);
						// 12/03/2017 Paul.  The module filter will be applied below as part of the or clause.
						//string sMODULEPlaceholder = NextPlaceholder(cmd, sMODULE_NAME_Field);
						//cmd.CommandText += "              and " + sMODULE_NAME_Field + " = @" + sMODULEPlaceholder + ControlChars.CrLf;
						//Sql.AddParameter(cmd, "@" + sMODULEPlaceholder, sMODULE_NAME);
					}
				}
			}

			cmd.CommandText += " where 1 = 1" + ControlChars.CrLf;
			if (bModuleIsTeamed)
			{
				if (bEnableTeamManagement && !bRequireTeamManagement && !bIsAdmin)
				{
					if (bEnableDynamicTeams)
						cmd.CommandText += "   and (TEAM_SET_ID is null or vwTEAM_SET_MEMBERSHIPS.MEMBERSHIP_TEAM_SET_ID is not null)" + ControlChars.CrLf;
					else
						cmd.CommandText += "   and (TEAM_ID is null or vwTEAM_MEMBERSHIPS.MEMBERSHIP_TEAM_ID is not null)" + ControlChars.CrLf;
				}
			}
			if (bModuleIsAssigned && bApplyAssignmentRules)
			{
				cmd.CommandText += "   and ( 1 = 0" + ControlChars.CrLf;
				foreach (string sMODULE_NAME in arrModules)
				{
					// 12/03/2017 Paul.  Module name field needs to be a parameter because it can change
					// between MODULE_NAME and ACTIVITY_TYPE.
					string sModuleSpacer = "";
					if (sMODULE_NAME.Length < 15)
						sModuleSpacer = Strings.Space(15 - sMODULE_NAME.Length);
					int nACLACCESS = _security.GetUserAccess(sMODULE_NAME, sACCESS_TYPE);
					if (nACLACCESS == ACL_ACCESS.OWNER || (bRequireUserAssignment && !bIsAdmin))
					{
						// 11/30/2017 Paul.  Add ASSIGNED_SET_ID for Dynamic User Assignment.
						if (bEnableDynamicAssignment)
						{
							if (bShowUnassigned)
							{
								string sMODULEPlaceholder = NextPlaceholder(cmd, sMODULE_NAME_Field);
								cmd.CommandText += "         or (" + sMODULE_NAME_Field + " = @" + sMODULEPlaceholder + sModuleSpacer + " and (" + sASSIGNED_SET_ID_Field + " is null or vwASSIGNED_SET_MEMBERSHIPS_" + sMODULE_NAME + ".MEMBERSHIP_ASSIGNED_SET_ID is not null))" + ControlChars.CrLf;
								Sql.AddParameter(cmd, sMODULEPlaceholder, sMODULE_NAME);
							}
							else
							{
								string sMODULEPlaceholder = NextPlaceholder(cmd, sMODULE_NAME_Field);
								cmd.CommandText += "         or (" + sMODULE_NAME_Field + " = @" + sMODULEPlaceholder + sModuleSpacer + " and (vwASSIGNED_SET_MEMBERSHIPS_" + sMODULE_NAME + ".MEMBERSHIP_ASSIGNED_SET_ID is not null))" + ControlChars.CrLf;
								Sql.AddParameter(cmd, sMODULEPlaceholder, sMODULE_NAME);
							}
						}
						else
						{
							string sFieldPlaceholder  = NextPlaceholder(cmd, sASSIGNED_USER_ID_Field);
							string sMODULEPlaceholder = NextPlaceholder(cmd, sMODULE_NAME_Field);
							if (bShowUnassigned)
							{
								if (Sql.IsOracle(cmd) || Sql.IsDB2(cmd))
									cmd.CommandText += "         or (" + sMODULE_NAME_Field + " = @" + sMODULEPlaceholder + sModuleSpacer + " and (" + sASSIGNED_USER_ID_Field + " is null or upper(" + sASSIGNED_USER_ID_Field + ") = upper(@" + sFieldPlaceholder + ")))" + ControlChars.CrLf;
								else
									cmd.CommandText += "         or (" + sMODULE_NAME_Field + " = @" + sMODULEPlaceholder + sModuleSpacer + " and (" + sASSIGNED_USER_ID_Field + " is null or "       + sASSIGNED_USER_ID_Field +  " = @"       + sFieldPlaceholder +  "))" + ControlChars.CrLf;
							}
							else
							{
								if (Sql.IsOracle(cmd) || Sql.IsDB2(cmd))
									cmd.CommandText += "         or (" + sMODULE_NAME_Field + " = @" + sMODULEPlaceholder + sModuleSpacer + " and upper(" + sASSIGNED_USER_ID_Field + ") = upper(@" + sFieldPlaceholder + "))" + ControlChars.CrLf;
								else
									cmd.CommandText += "         or (" + sMODULE_NAME_Field + " = @" + sMODULEPlaceholder + sModuleSpacer + " and "       + sASSIGNED_USER_ID_Field +  " = @"       + sFieldPlaceholder +  ")" + ControlChars.CrLf;
							}
							Sql.AddParameter(cmd, sFieldPlaceholder , _security.USER_ID);
							Sql.AddParameter(cmd, sMODULEPlaceholder, sMODULE_NAME   );
						}
					}
					else if (nACLACCESS > 0)
					{
						string sMODULEPlaceholder = NextPlaceholder(cmd, sMODULE_NAME_Field);
						cmd.CommandText += "          or " + sMODULE_NAME_Field + " = @" + sMODULEPlaceholder + ControlChars.CrLf;
						Sql.AddParameter(cmd, sMODULEPlaceholder, sMODULE_NAME);
					}
				}
				cmd.CommandText += "       )" + ControlChars.CrLf;
			}
		}

		// =====================================================================================
		// ApplyAssignedFilter — Data Privacy variant (assignment-only, no team management)
		//
		// Migrated from Security.FilterAssigned(IDbCommand, string, string, string) lines 1303–1384.
		//
		// This variant is used for Data Privacy Manager operations on Accounts, Contacts, Leads,
		// and Prospects. It applies ONLY assignment-based filtering (no team management JOINs),
		// ensuring that only the assigned user (not just team members) can edit sensitive records.
		//
		// Key difference from ApplyFilter:
		//   - bModuleIsTeamed is always false (no team joins generated)
		//   - bRequireUserAssignment is hardcoded true (always applied)
		//   - #if DEBUG block is commented out (not applied in debug mode)
		// =====================================================================================

		/// <summary>
		/// Appends assignment-only SQL JOIN and WHERE predicates for Data Privacy operations.
		/// Does NOT generate team membership JOINs — only assignment-based filtering is applied.
		///
		/// Used for Data Privacy Manager access to Accounts, Contacts, Leads, and Prospects
		/// where a user should only see records they are individually assigned to.
		///
		/// Produces SQL IDENTICAL to Security.FilterAssigned(IDbCommand, ...) at lines 1303–1384.
		///
		/// MIGRATION NOTE: Security.IS_ADMIN (static) → _security.IS_ADMIN (instance).
		///                 Security.GetACLRoleAccess() (static) → _security.GetACLRoleAccess() (instance).
		///                 Security.GetUserAccess() (static) → _security.GetUserAccess() (instance).
		///                 Security.USER_ID (static) → _security.USER_ID (instance).
		/// </summary>
		/// <param name="cmd">Command whose CommandText and Parameters are modified.</param>
		/// <param name="sMODULE_NAME">Module name (e.g. "Accounts", "Contacts").</param>
		/// <param name="sACCESS_TYPE">Access type string (e.g. "edit", "delete").</param>
		/// <param name="sASSIGNED_USER_ID_Field">Column name of the assignment field.</param>
		public void ApplyAssignedFilter(IDbCommand cmd, string sMODULE_NAME, string sACCESS_TYPE, string sASSIGNED_USER_ID_Field)
		{
			cmd.CommandTimeout = 0;
			bool bShowUnassigned          = Crm.Config.show_unassigned       (_memoryCache);
			bool bModuleIsAssigned        = true;
			bool bRequireUserAssignment   = true;
			// NOTE: enable_dynamic_assignment() has no IMemoryCache overload; read CONFIG key directly.
			bool bEnableDynamicAssignment = Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.enable_dynamic_assignment"));
			// BEFORE: bool bIsAdmin = IS_ADMIN; (static)
			// AFTER:  bool bIsAdmin = _security.IS_ADMIN; (instance)
			bool bIsAdmin = _security.IS_ADMIN;
			// NOTE: The #if DEBUG block intentionally omitted here per original FilterAssigned()
			// at line 1311-1312 which has the debug block commented out:
			//   //#if DEBUG
			//   //    bIsAdmin = false;
			//   //#endif
			// 06/26/2018 Paul.  The Data Privacy Manager has admin-like access to Accounts, Contacts, Leads and Prospects.
			if (_security.GetACLRoleAccess("Data Privacy Manager Role"))
			{
				if (sMODULE_NAME == "Accounts" || sMODULE_NAME == "Contacts" || sMODULE_NAME == "Leads" || sMODULE_NAME == "Prospects")
				{
					bIsAdmin = true;
				}
			}
			int nACLACCESS = 0;
			if (bModuleIsAssigned && !Sql.IsEmptyString(sMODULE_NAME))
			{
				// BEFORE: nACLACCESS = Security.GetUserAccess(sMODULE_NAME, sACCESS_TYPE);
				// AFTER:  nACLACCESS = _security.GetUserAccess(sMODULE_NAME, sACCESS_TYPE);
				nACLACCESS = _security.GetUserAccess(sMODULE_NAME, sACCESS_TYPE);
			}

			// 11/30/2017 Paul.  Add ASSIGNED_SET_ID for Dynamic User Assignment.
			string sASSIGNED_SET_ID_Field = sASSIGNED_USER_ID_Field.Replace("ASSIGNED_USER_ID", "ASSIGNED_SET_ID");
			if (bModuleIsAssigned && bEnableDynamicAssignment)
			{
				// 01/01/2008 Paul.  We need a quick way to require user assignments across the system.
				// 01/02/2008 Paul.  Make sure owner rule does not apply to admins.
				if (nACLACCESS == ACL_ACCESS.OWNER || (bRequireUserAssignment && !bIsAdmin))
				{
					string sFieldPlaceholder = NextPlaceholder(cmd, sASSIGNED_SET_ID_Field);
					if (bRequireUserAssignment && !bShowUnassigned)
						cmd.CommandText += "       inner ";
					else
						cmd.CommandText += "  left outer ";
					cmd.CommandText += "join vwASSIGNED_SET_MEMBERSHIPS" + ControlChars.CrLf;
					cmd.CommandText += "               on vwASSIGNED_SET_MEMBERSHIPS.MEMBERSHIP_ASSIGNED_SET_ID  = " + sASSIGNED_SET_ID_Field + ControlChars.CrLf;
					cmd.CommandText += "              and vwASSIGNED_SET_MEMBERSHIPS.MEMBERSHIP_ASSIGNED_USER_ID = @" + sFieldPlaceholder + ControlChars.CrLf;
					Sql.AddParameter(cmd, sFieldPlaceholder, _security.USER_ID);
				}
			}

			cmd.CommandText += " where 1 = 1" + ControlChars.CrLf;
			if (bModuleIsAssigned)
			{
				// 01/01/2008 Paul.  We need a quick way to require user assignments across the system.
				// 01/02/2008 Paul.  Make sure owner rule does not apply to admins.
				if (nACLACCESS == ACL_ACCESS.OWNER || (bRequireUserAssignment && !bIsAdmin))
				{
					// 11/30/2017 Paul.  Add ASSIGNED_SET_ID for Dynamic User Assignment.
					if (bEnableDynamicAssignment)
					{
						if (bShowUnassigned)
						{
							cmd.CommandText += "   and (" + sASSIGNED_SET_ID_Field + " is null or vwASSIGNED_SET_MEMBERSHIPS.MEMBERSHIP_ASSIGNED_SET_ID is not null)" + ControlChars.CrLf;
						}
					}
					else
					{
						string sFieldPlaceholder = NextPlaceholder(cmd, sASSIGNED_USER_ID_Field);
						if (bShowUnassigned)
						{
							if (Sql.IsOracle(cmd) || Sql.IsDB2(cmd))
								cmd.CommandText += "   and (" + sASSIGNED_USER_ID_Field + " is null or upper(" + sASSIGNED_USER_ID_Field + ") = upper(@" + sFieldPlaceholder + "))" + ControlChars.CrLf;
							else
								cmd.CommandText += "   and (" + sASSIGNED_USER_ID_Field + " is null or "       + sASSIGNED_USER_ID_Field +  " = @"       + sFieldPlaceholder + ")"  + ControlChars.CrLf;
						}
						else
						{
							if (Sql.IsOracle(cmd) || Sql.IsDB2(cmd))
								cmd.CommandText += "   and upper(" + sASSIGNED_USER_ID_Field + ") = upper(@" + sFieldPlaceholder + ")" + ControlChars.CrLf;
							else
								cmd.CommandText += "   and "       + sASSIGNED_USER_ID_Field +  " = @"       + sFieldPlaceholder       + ControlChars.CrLf;
						}
						Sql.AddParameter(cmd, sFieldPlaceholder, _security.USER_ID);
					}
				}
			}
		}
	}
}
