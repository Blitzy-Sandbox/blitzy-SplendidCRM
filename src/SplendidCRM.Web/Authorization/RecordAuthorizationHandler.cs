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
// .NET 10 Migration: SplendidCRM/_code/Security.cs → src/SplendidCRM.Web/Authorization/RecordAuthorizationHandler.cs
// Extracts GetRecordAccess(DataRow row, string sMODULE_NAME, string sACCESS_TYPE, string sASSIGNED_USER_ID_FIELD)
// (lines 630–680) into an ASP.NET Core IAuthorizationHandler implementing the Record tier of the 4-tier ACL model.
//
// Changes applied:
//   - REMOVED: using System.Web; using System.Web.SessionState; using System.Web.UI.WebControls;
//   - ADDED:   using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Http;
//              using Microsoft.Extensions.Caching.Memory; using System.Data;
//   - REMOVED: GetRecordAccess(object Container, ...) overloads (lines 607–628) — depend on DataGridItem
//              (System.Web.UI.WebControls), which is WebForms-only and not available in ASP.NET Core.
//   - REPLACED: HttpContext.Current.Application["key"] → IMemoryCache.Get<object>("key")
//   - REPLACED: Security.GetUserAccess(...) static call → injected Security instance method call
//   - REPLACED: Security.USER_ID static property → injected Security instance property
//   - ADDED: RecordAuthorizationRequirement (IAuthorizationRequirement) carrying DataRow, ModuleName,
//              AccessType, and AssignedUserIdField — the parameters of the original GetRecordAccess().
//   - ADDED: RecordAuthorizationHandler (AuthorizationHandler<RecordAuthorizationRequirement>) —
//              HandleRequirementAsync wraps the exact business logic of GetRecordAccess(DataRow, ...).
#nullable disable
using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	// =========================================================================================
	// RecordAuthorizationRequirement
	//
	// Carries the authorization context for a record-level security check.  This replaces the
	// four positional parameters of the original static GetRecordAccess(DataRow, string, string,
	// string) method from Security.cs, packaging them as an IAuthorizationRequirement that can
	// be passed through the ASP.NET Core authorization pipeline.
	//
	// Usage:
	//   var req = new RecordAuthorizationRequirement("Contacts", "edit", dataRow, "ASSIGNED_USER_ID");
	//   var result = await _authorizationService.AuthorizeAsync(user, null, req);
	//   if (!result.Succeeded) { /* deny access */ }
	// =========================================================================================

	/// <summary>
	/// Carries the record-level security context for a single authorization check.
	/// 
	/// Migrated from the parameter list of
	/// <c>Security.GetRecordAccess(DataRow row, string sMODULE_NAME, string sACCESS_TYPE, string sASSIGNED_USER_ID_FIELD)</c>
	/// (SplendidCRM/_code/Security.cs, lines 630–680).
	///
	/// Properties preserve the original parameter names in PascalCase to maintain readable
	/// mapping between the original code and this handler.
	/// </summary>
	public class RecordAuthorizationRequirement : IAuthorizationRequirement
	{
		/// <summary>
		/// The CRM module being accessed (e.g. "Contacts", "Accounts", "Leads").
		/// Corresponds to <c>sMODULE_NAME</c> in the original implementation.
		/// </summary>
		public string ModuleName { get; }

		/// <summary>
		/// The access type being evaluated: "view", "edit", "delete", "import", "export", "access",
		/// or "remove" (which is remapped to "edit" at runtime, preserving line 633–634 of Security.cs).
		/// Corresponds to <c>sACCESS_TYPE</c> in the original implementation.
		/// </summary>
		public string AccessType { get; }

		/// <summary>
		/// The DataRow of the record being accessed.  Used for ownership checks
		/// (<c>ASSIGNED_USER_ID</c>), dynamic assignment set checks (<c>ASSIGNED_SET_LIST</c>),
		/// and dynamically injected record-level security fields (<c>RECORD_LEVEL_SECURITY_*</c>).
		/// May be <c>null</c> when no specific record context is available (e.g. bulk operations).
		/// Corresponds to <c>row</c> in the original implementation.
		/// </summary>
		public DataRow Row { get; }

		/// <summary>
		/// The name of the DataRow column that holds the owning user's GUID.
		/// Typically <c>"ASSIGNED_USER_ID"</c>, but may be <c>"CREATED_BY_ID"</c> for
		/// modules that track creator ownership instead of assignment ownership.
		/// Pass <see cref="String.Empty"/> to skip the ownership check entirely
		/// (matches original behaviour when sASSIGNED_USER_ID_FIELD was empty — line 643).
		/// Corresponds to <c>sASSIGNED_USER_ID_FIELD</c> in the original implementation.
		/// </summary>
		public string AssignedUserIdField { get; }

		/// <summary>
		/// Initialises a new <see cref="RecordAuthorizationRequirement"/> with all context needed
		/// for the record-level security evaluation.
		/// </summary>
		/// <param name="moduleName">CRM module name (e.g. "Contacts").</param>
		/// <param name="accessType">Access type string ("view", "edit", "delete", etc.).</param>
		/// <param name="row">
		/// DataRow of the record being accessed; may be <c>null</c> for module-only checks.
		/// </param>
		/// <param name="assignedUserIdField">
		/// DataRow column name containing the owning user GUID (typically "ASSIGNED_USER_ID").
		/// Pass <see cref="String.Empty"/> to skip the ownership check.
		/// </param>
		public RecordAuthorizationRequirement(string moduleName, string accessType, DataRow row, string assignedUserIdField)
		{
			ModuleName         = moduleName         ?? String.Empty;
			AccessType         = accessType         ?? String.Empty;
			Row                = row;
			AssignedUserIdField = assignedUserIdField ?? String.Empty;
		}

		/// <summary>
		/// Overload that sets <see cref="AssignedUserIdField"/> to <see cref="String.Empty"/>,
		/// triggering the same code path as the original
		/// <c>GetRecordAccess(DataRow, string, string)</c> overload (line 682–685 of Security.cs)
		/// which passes <see cref="String.Empty"/> for the ASSIGNED_USER_ID field.
		/// </summary>
		/// <param name="moduleName">CRM module name.</param>
		/// <param name="accessType">Access type string.</param>
		/// <param name="row">DataRow of the record; may be <c>null</c>.</param>
		public RecordAuthorizationRequirement(string moduleName, string accessType, DataRow row)
			: this(moduleName, accessType, row, String.Empty)
		{
		}
	}

	// =========================================================================================
	// RecordAuthorizationHandler
	//
	// Implements the Record tier of the 4-tier ACL model (Module → Team → Field → Record).
	// The exact business logic from Security.GetRecordAccess(DataRow row, ...) (lines 630–680)
	// is reproduced verbatim, with the following .NET 10 substitutions:
	//
	//   BEFORE: Security.GetUserAccess(sMODULE_NAME, sACCESS_TYPE)   (static)
	//   AFTER:  _security.GetUserAccess(sMODULE_NAME, sACCESS_TYPE)  (injected instance)
	//
	//   BEFORE: Security.USER_ID                                       (static, HttpContext.Current.Session)
	//   AFTER:  _security.USER_ID                                      (injected instance, ISession)
	//
	//   BEFORE: HttpContext.Current.Application["Modules.X.RecordLevelSecurity"]
	//   AFTER:  _memoryCache.Get<object>("Modules.X.RecordLevelSecurity")
	//
	//   Crm.Config.enable_dynamic_assignment() — unchanged (already uses static ambient IMemoryCache)
	//   Sql.ToString() / Sql.ToGuid() / Sql.ToInteger() / Sql.ToBoolean() — unchanged (static helpers)
	// =========================================================================================

	/// <summary>
	/// ASP.NET Core authorization handler that enforces record-level security.
	///
	/// Implements the Record tier of the 4-tier ACL model:
	///   Module → Team → Field → <b>Record</b>
	///
	/// Evaluates three independent record-level checks in order (matching the original
	/// <c>Security.GetRecordAccess(DataRow, string, string, string)</c> logic):
	/// <list type="number">
	///   <item><description>
	///     <b>Base module ACL</b> — calls <see cref="Security.GetUserAccess"/> for the
	///     module and access type to obtain the user's role-based baseline access level.
	///   </description></item>
	///   <item><description>
	///     <b>Owner check (static or dynamic assignment)</b> — if the baseline is
	///     <see cref="ACL_ACCESS.OWNER"/>, verifies either:
	///     <list type="bullet">
	///       <item>Dynamic: user GUID is in <c>ASSIGNED_SET_LIST</c> (when dynamic assignment is enabled)</item>
	///       <item>Static: user GUID matches <c>ASSIGNED_USER_ID</c> (or the configured field)</item>
	///     </list>
	///     If neither match, demotes the access level to <see cref="ACL_ACCESS.NONE"/>.
	///   </description></item>
	///   <item><description>
	///     <b>Record-level security ACL field</b> — if the module has record-level security
	///     enabled (via <c>Modules.{module}.RecordLevelSecurity</c> in IMemoryCache), reads the
	///     dynamically injected <c>RECORD_LEVEL_SECURITY_{ACCESS_TYPE}</c> column from the DataRow
	///     and applies it if it is more restrictive than the current access level.
	///   </description></item>
	/// </list>
	///
	/// Registration (in Program.cs or DI setup):
	/// <code>
	/// services.AddScoped&lt;Security&gt;();
	/// services.AddScoped&lt;IAuthorizationHandler, RecordAuthorizationHandler&gt;();
	/// services.AddAuthorization();
	/// </code>
	///
	/// Migrated from SplendidCRM/_code/Security.cs (.NET Framework 4.8 → .NET 10 ASP.NET Core).
	/// </summary>
	public class RecordAuthorizationHandler : AuthorizationHandler<RecordAuthorizationRequirement>
	{
		// -----------------------------------------------------------------------------------------
		// Injected dependencies — replacing static class access patterns from the original code
		// -----------------------------------------------------------------------------------------

		/// <summary>
		/// DI-injectable Security service instance.
		/// BEFORE: Static Security.GetUserAccess(...) and Security.USER_ID
		/// AFTER:  Instance _security.GetUserAccess(...) and _security.USER_ID
		/// </summary>
		private readonly Security _security;

		/// <summary>
		/// IHttpContextAccessor — injected for completeness (used indirectly via _security).
		/// BEFORE: HttpContext.Current (static access pattern)
		/// AFTER:  IHttpContextAccessor.HttpContext (injected)
		/// </summary>
		private readonly IHttpContextAccessor _httpContextAccessor;

		/// <summary>
		/// IMemoryCache replacing HttpApplicationState (Application[]).
		/// BEFORE: HttpContext.Current.Application["Modules." + sMODULE_NAME + ".RecordLevelSecurity"]
		/// AFTER:  _memoryCache.Get&lt;object&gt;("Modules." + sMODULE_NAME + ".RecordLevelSecurity")
		/// </summary>
		private readonly IMemoryCache _memoryCache;

		// -----------------------------------------------------------------------------------------
		// Constructor
		// -----------------------------------------------------------------------------------------

		/// <summary>
		/// Initialises the handler with all required injected dependencies.
		/// </summary>
		/// <param name="httpContextAccessor">
		/// Provides access to the current HTTP context; used indirectly via the
		/// injected <paramref name="security"/> service for session-backed properties.
		/// </param>
		/// <param name="memoryCache">
		/// Application-wide memory cache replacing <c>HttpApplicationState</c> (Application[]).
		/// Used to read the <c>Modules.{module}.RecordLevelSecurity</c> flag.
		/// </param>
		/// <param name="security">
		/// Migrated DI-injectable Security service providing <see cref="Security.GetUserAccess"/>
		/// and <see cref="Security.USER_ID"/> backed by distributed session.
		/// </param>
		public RecordAuthorizationHandler(
			IHttpContextAccessor httpContextAccessor,
			IMemoryCache         memoryCache,
			Security             security)
		{
			_httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
			_memoryCache         = memoryCache         ?? throw new ArgumentNullException(nameof(memoryCache        ));
			_security            = security            ?? throw new ArgumentNullException(nameof(security           ));
		}

		// -----------------------------------------------------------------------------------------
		// HandleRequirementAsync — Core authorization logic
		//
		// This is a direct translation of:
		//   Security.GetRecordAccess(DataRow row, string sMODULE_NAME, string sACCESS_TYPE, string sASSIGNED_USER_ID_FIELD)
		//   SplendidCRM/_code/Security.cs, lines 630–680
		//
		// The algorithm is preserved exactly; only the static call sites are replaced with
		// their DI equivalents as documented on each line below.
		// -----------------------------------------------------------------------------------------

		/// <summary>
		/// Evaluates record-level security for the current user against the record described
		/// by <paramref name="requirement"/>.
		///
		/// Calls <c>context.Succeed(requirement)</c> when the resulting ACL access level is
		/// non-negative (≥ 0), indicating the user is permitted to perform the requested operation.
		/// Does <b>not</b> call <c>context.Fail()</c> when access is denied, conforming to
		/// ASP.NET Core convention that other handlers may still grant access.
		///
		/// Returns <see cref="Task.CompletedTask"/> in all cases (synchronous logic wrapped
		/// in async method to satisfy the abstract base class signature).
		/// </summary>
		/// <param name="context">The authorization handler context.</param>
		/// <param name="requirement">The record-level security requirement to evaluate.</param>
		protected override Task HandleRequirementAsync(
			AuthorizationHandlerContext       context,
			RecordAuthorizationRequirement    requirement)
		{
			// Extract parameters from the requirement — mirrors the method signature of
			// GetRecordAccess(DataRow row, string sMODULE_NAME, string sACCESS_TYPE, string sASSIGNED_USER_ID_FIELD)
			string  sMODULE_NAME             = requirement.ModuleName          ?? String.Empty;
			string  sACCESS_TYPE             = requirement.AccessType           ?? String.Empty;
			DataRow row                      = requirement.Row;
			string  sASSIGNED_USER_ID_FIELD  = requirement.AssignedUserIdField  ?? String.Empty;

			// ---------------------------------------------------------------------------------
			// Step 1: Map "remove" to "edit" (Security.cs line 633–634)
			// 11/03/2017 Paul.  Remove is the same as edit.  We don't want to define another
			//                   select field.
			// ---------------------------------------------------------------------------------
			if (sACCESS_TYPE == "remove")
				sACCESS_TYPE = "edit";

			// ---------------------------------------------------------------------------------
			// Step 2: Get base module ACL (Security.cs line 635)
			//
			// BEFORE: int nACLACCESS = Security.GetUserAccess(sMODULE_NAME, sACCESS_TYPE);
			// AFTER:  Uses injected Security instance — same logic, same return values.
			// ---------------------------------------------------------------------------------
			int nACLACCESS = _security.GetUserAccess(sMODULE_NAME, sACCESS_TYPE);

			// ---------------------------------------------------------------------------------
			// Step 3: Owner check with optional dynamic assignment (Security.cs lines 636–659)
			// Only evaluated when a row context is provided (non-null DataRow).
			// ---------------------------------------------------------------------------------
			if (row != null)
			{
				// 11/30/2017 Paul.  Add ASSIGNED_SET_ID for Dynamic User Assignment.
				// BEFORE: bool bEnableDynamicAssignment = Crm.Config.enable_dynamic_assignment();
				// AFTER:  Crm.Config.enable_dynamic_assignment() now reads _ambientCache
				//         (set at startup from DI container) — call signature is unchanged.
				bool bEnableDynamicAssignment = Crm.Config.enable_dynamic_assignment();

				if (nACLACCESS == ACL_ACCESS.OWNER)
				{
					// 10/31/2017 Paul.  Don't check if sASSIGNED_USER_ID_FIELD exists in table
					//                   because this is a coding error that we want to catch.
					// (Security.cs line 643)
					if (!Sql.IsEmptyString(sASSIGNED_USER_ID_FIELD))
					{
						// 01/24/2018 Paul.  sASSIGNED_USER_ID_FIELD is either ASSIGNED_USER_ID
						//                   or CREATED_BY_ID.
						// (Security.cs line 645–646)
						string sASSIGNED_SET_LIST_FIELD = "ASSIGNED_SET_LIST";

						// Dynamic assignment branch (Security.cs lines 647–651):
						// When dynamic assignment is enabled AND the ownership field is
						// ASSIGNED_USER_ID AND the row has an ASSIGNED_SET_LIST column,
						// check the comma-separated set instead of a single user GUID.
						if (bEnableDynamicAssignment
							&& (sASSIGNED_USER_ID_FIELD == "ASSIGNED_USER_ID")
							&& row.Table.Columns.Contains(sASSIGNED_SET_LIST_FIELD))
						{
							// BEFORE: string sASSIGNED_SET_LIST = Sql.ToString(row[sASSIGNED_SET_LIST_FIELD]).ToUpper();
							// AFTER:  Same logic — Sql.ToString is a static helper with no System.Web dependency.
							string sASSIGNED_SET_LIST = Sql.ToString(row[sASSIGNED_SET_LIST_FIELD]).ToUpper();

							// Deny if list is non-empty AND current user GUID is not in the set.
							// (Security.cs lines 650–651)
							//
							// BEFORE: Security.USER_ID.ToString().ToUpper()  (static, HttpContext.Current.Session)
							// AFTER:  _security.USER_ID.ToString().ToUpper() (injected instance, ISession)
							if (!sASSIGNED_SET_LIST.Contains(_security.USER_ID.ToString().ToUpper())
								&& !Sql.IsEmptyString(sASSIGNED_SET_LIST))
							{
								nACLACCESS = ACL_ACCESS.NONE;
							}
						}
						else
						{
							// Static assignment branch (Security.cs lines 654–657):
							// Owner check against a single ASSIGNED_USER_ID value.
							//
							// BEFORE: Guid gASSIGNED_USER_ID = Sql.ToGuid(row[sASSIGNED_USER_ID_FIELD]);
							//         if ( Security.USER_ID != gASSIGNED_USER_ID && gASSIGNED_USER_ID != Guid.Empty )
							// AFTER:  Same Sql.ToGuid helper; Security.USER_ID replaced by _security.USER_ID.
							Guid gASSIGNED_USER_ID = Sql.ToGuid(row[sASSIGNED_USER_ID_FIELD]);
							if (_security.USER_ID != gASSIGNED_USER_ID && gASSIGNED_USER_ID != Guid.Empty)
							{
								nACLACCESS = ACL_ACCESS.NONE;
							}
						}
					}
				}

				// ---------------------------------------------------------------------------------
				// Step 4: Record-level security ACL field check (Security.cs lines 661–677)
				//
				// 11/01/2017 Paul.  Use a module-based flag so that Record Level Security is only
				//                   enabled when needed.
				//
				// BEFORE: Sql.ToBoolean(HttpContext.Current.Application["Modules." + sMODULE_NAME + ".RecordLevelSecurity"])
				// AFTER:  Sql.ToBoolean(_memoryCache.Get<object>("Modules." + sMODULE_NAME + ".RecordLevelSecurity"))
				//         IMemoryCache replaces HttpApplicationState (Application[]) throughout the migration.
				// ---------------------------------------------------------------------------------
				if (Sql.ToBoolean(_memoryCache.Get<object>("Modules." + sMODULE_NAME + ".RecordLevelSecurity")))
				{
					// 10/31/2017 Paul.  FULL_ACCESS means that this is an Admin and Record ACL does
					//                   not apply. (Security.cs lines 664–665)
					if (nACLACCESS >= 0 && nACLACCESS < ACL_ACCESS.FULL_ACCESS)
					{
						// 10/31/2017 Paul.  Check if field exists because it is dynamically injected.
						// (Security.cs lines 667–669)
						string sRECORD_ACL_FIELD_NAME = "RECORD_LEVEL_SECURITY_" + sACCESS_TYPE.ToUpper();
						if (row.Table.Columns.Contains(sRECORD_ACL_FIELD_NAME))
						{
							// 10/31/2017 Paul.  Record ACL only applies if it takes away rights.
							// (Security.cs lines 672–674)
							int nRECORD_ACLACCESS = Sql.ToInteger(row[sRECORD_ACL_FIELD_NAME]);
							if (nRECORD_ACLACCESS < nACLACCESS)
								nACLACCESS = nRECORD_ACLACCESS;
						}
					}
				}
			}

			// ---------------------------------------------------------------------------------
			// Step 5: Signal authorization outcome
			//
			// ASP.NET Core convention:
			//   - Call context.Succeed(requirement) to grant access (nACLACCESS >= 0).
			//   - Do NOT call context.Fail() to deny — this allows other handlers in the pipeline
			//     to still grant access if appropriate.
			// ---------------------------------------------------------------------------------
			if (nACLACCESS >= 0)
				context.Succeed(requirement);
			// If nACLACCESS < 0 (e.g. ACL_ACCESS.NONE = -99, ACL_ACCESS.DISABLED = -98):
			// do not call Succeed and do not call Fail — access is passively denied.

			return Task.CompletedTask;
		}
	}
}
