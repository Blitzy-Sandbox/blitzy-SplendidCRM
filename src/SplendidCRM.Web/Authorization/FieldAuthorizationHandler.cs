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
// .NET 10 Migration: Extracted from SplendidCRM/_code/Security.cs → src/SplendidCRM.Web/Authorization/FieldAuthorizationHandler.cs
// Source: Security.cs lines 714–820 (ACL_FIELD_ACCESS nested class, SetUserFieldSecurity, GetUserFieldSecurity)
// Changes applied:
//   - Removed:  using System.Web; using System.Web.SessionState;
//   - Added:    using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Http;
//               using System; using System.Threading.Tasks;
//   - Static class with HttpContext.Current → DI-friendly instance class with:
//       IHttpContextAccessor (replaces HttpContext.Current.Session/Request)
//       Security             (replaces static SplendidCRM.Security property access: IS_ADMIN, USER_ID)
//   - HttpContext.Current.Session["ACLFIELD_x_y"]   → _httpContextAccessor.HttpContext?.Session.GetString("ACLFIELD_x_y")
//   - HttpContext.Current.Session["ACLFIELD_x_y"] = → session.SetString("ACLFIELD_x_y", value.ToString())
//   - Security.IS_ADMIN static                       → _security.IS_ADMIN (DI instance property)
//   - Security.USER_ID static                        → _security.USER_ID (DI instance property)
//   - ACL_FIELD_ACCESS nested class: constructor gains gCurrentUserId param (breaks static Security.USER_ID dep)
//   - FieldAuthorizationRequirement implements IAuthorizationRequirement per ASP.NET Core pipeline
//   - FieldAuthorizationHandler extends AuthorizationHandler<FieldAuthorizationRequirement>
//   - #if !DEBUG admin bypass from Security.cs lines 801-804 preserved exactly
//   - Session key pattern "ACLFIELD_{MODULE}_{FIELD}" preserved exactly (line 807)
//   - NOT_SET (0) defaults to FULL_ACCESS (lines 810-811) preserved exactly
//   - IsReadable() logic (lines 743–755) preserved exactly
//   - IsWriteable() logic (lines 757–770) preserved exactly
//   - Minimal change clause: only framework migration changes; all ACL business logic preserved identically

#nullable disable
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace SplendidCRM
{
	/// <summary>
	/// Authorization requirement carrying the module name, field name, required access type (read/write),
	/// and optional record owner for the field-level ACL check. Consumed by <see cref="FieldAuthorizationHandler"/>.
	///
	/// This requirement type plugs into the standard ASP.NET Core authorization pipeline:
	///   var requirement = new FieldAuthorizationRequirement("Accounts", "name", "read", gASSIGNED_USER_ID);
	///   var result = await _authorizationService.AuthorizeAsync(user, null, requirement);
	///
	/// The ModuleName + FieldName pair maps directly to the session key pattern:
	///   "ACLFIELD_{ModuleName}_{FieldName}"
	/// which is the canonical key format used by SplendidCRM throughout the field ACL system.
	///
	/// RequiredAccess accepts the values "read" or "write" (case-insensitive).
	///   "read"  → evaluates ACL_FIELD_ACCESS.IsReadable() against the session-stored ACL value.
	///   "write" → evaluates ACL_FIELD_ACCESS.IsWriteable() against the session-stored ACL value.
	///
	/// AssignedUserId is the ASSIGNED_USER_ID (or CREATED_BY_ID) of the record being accessed.
	/// Pass Guid.Empty for new records — ACL_FIELD_ACCESS treats empty owner as bIsNew=true (full access).
	/// </summary>
	public class FieldAuthorizationRequirement : IAuthorizationRequirement
	{
		/// <summary>
		/// CRM module name (e.g. "Accounts", "Contacts", "Opportunities").
		/// Case-sensitive — must match the module names used as the first segment of the
		/// ACLFIELD session key: "ACLFIELD_{ModuleName}_{FieldName}".
		/// </summary>
		public string ModuleName { get; }

		/// <summary>
		/// Field name within the module (e.g. "account_name", "email1", "amount").
		/// Case-sensitive — must match the second segment of the ACLFIELD session key.
		/// </summary>
		public string FieldName { get; }

		/// <summary>
		/// Required access type for this authorization check.
		/// Valid values: "read" (calls IsReadable) or "write" (calls IsWriteable).
		/// Defaults to "read" when null or empty.
		/// </summary>
		public string RequiredAccess { get; }

		/// <summary>
		/// ASSIGNED_USER_ID of the record being accessed. Used to determine record ownership
		/// for owner-conditional access levels (OWNER_READ_ONLY, OWNER_READ_OWNER_WRITE, READ_OWNER_WRITE).
		/// Pass Guid.Empty for new records — treated as bIsNew=true which grants write access.
		/// </summary>
		public Guid AssignedUserId { get; }

		/// <summary>
		/// Initializes a new <see cref="FieldAuthorizationRequirement"/>.
		/// </summary>
		/// <param name="moduleName">CRM module name (case-sensitive).</param>
		/// <param name="fieldName">Field name within the module (case-sensitive).</param>
		/// <param name="requiredAccess">Required access type: "read" or "write" (case-insensitive). Defaults to "read".</param>
		/// <param name="assignedUserId">ASSIGNED_USER_ID of the record; Guid.Empty for new records.</param>
		public FieldAuthorizationRequirement(string moduleName, string fieldName, string requiredAccess, Guid assignedUserId)
		{
			ModuleName     = moduleName;
			FieldName      = fieldName;
			RequiredAccess = string.IsNullOrEmpty(requiredAccess) ? "read" : requiredAccess;
			AssignedUserId = assignedUserId;
		}

		/// <summary>
		/// Convenience overload — defaults AssignedUserId to Guid.Empty (new record / non-owner context).
		/// </summary>
		/// <param name="moduleName">CRM module name (case-sensitive).</param>
		/// <param name="fieldName">Field name within the module (case-sensitive).</param>
		/// <param name="requiredAccess">Required access type: "read" or "write" (case-insensitive).</param>
		public FieldAuthorizationRequirement(string moduleName, string fieldName, string requiredAccess)
			: this(moduleName, fieldName, requiredAccess, Guid.Empty)
		{
		}

		/// <summary>
		/// Convenience overload — defaults to read access and Guid.Empty owner.
		/// </summary>
		/// <param name="moduleName">CRM module name (case-sensitive).</param>
		/// <param name="fieldName">Field name within the module (case-sensitive).</param>
		public FieldAuthorizationRequirement(string moduleName, string fieldName)
			: this(moduleName, fieldName, "read", Guid.Empty)
		{
		}
	}

	/// <summary>
	/// Field-level ACL authorization handler — the third tier of the SplendidCRM
	/// 4-tier ACL model (Module → Team → Field → Record).
	///
	/// Migrated from <c>SplendidCRM/_code/Security.cs</c> (lines 714–820) for .NET 10 ASP.NET Core.
	/// All static <c>HttpContext.Current</c> and <c>Application[]</c> access patterns replaced with
	/// constructor-injected <see cref="IHttpContextAccessor"/> and <see cref="Security"/> instances.
	///
	/// Key field ACL rules preserved identically from the original:
	/// <list type="bullet">
	///   <item>Admin bypass: IS_ADMIN=true → FULL_ACCESS in release builds (not in debug).
	///         #if !DEBUG preserved from Security.cs lines 801-804 for testability.</item>
	///   <item>Session key pattern: "ACLFIELD_{MODULE}_{FIELD}" (line 807) preserved exactly.</item>
	///   <item>NOT_SET (0): when no ACL value is stored in session, FULL_ACCESS is granted (lines 810-811).</item>
	///   <item>NONE (-99): explicit denial — field is hidden/read-only regardless of other conditions.</item>
	///   <item>Ownership context: bIsNew=(AssignedUserId==Guid.Empty), bIsOwner=(USER_ID==AssignedUserId)||bIsNew.</item>
	///   <item>IsReadable(): FULL_ACCESS→true; &lt;NOT_SET→false; bIsNew||bIsOwner||nACLACCESS&gt;OWNER_READ_ONLY→true.</item>
	///   <item>IsWriteable(): FULL_ACCESS→true; &lt;NOT_SET→false; owner-conditional or nACLACCESS&gt;READ_OWNER_WRITE→true.</item>
	/// </list>
	///
	/// Register as SCOPED in DI to receive a fresh <see cref="Security"/> instance per request.
	/// </summary>
	public class FieldAuthorizationHandler : AuthorizationHandler<FieldAuthorizationRequirement>
	{
		// =====================================================================================
		// Private fields — DI-injected replacements for static ASP.NET Framework patterns
		// =====================================================================================

		/// <summary>
		/// Replaces <c>HttpContext.Current</c> throughout — provides access to the current
		/// HTTP context's distributed session for reading and writing ACLFIELD_* session keys.
		/// BEFORE: HttpContext.Current.Session["ACLFIELD_MODULE_FIELD"]
		/// AFTER:  _httpContextAccessor.HttpContext?.Session.GetString("ACLFIELD_MODULE_FIELD")
		/// </summary>
		private readonly IHttpContextAccessor _httpContextAccessor;

		/// <summary>
		/// DI-injectable Security service replacing static <c>SplendidCRM.Security</c> class.
		/// Provides IS_ADMIN (for admin bypass in GetUserFieldSecurity) and USER_ID
		/// (for ownership comparison in ACL_FIELD_ACCESS constructor).
		/// BEFORE: Security.IS_ADMIN (static) / Security.USER_ID (static)
		/// AFTER:  _security.IS_ADMIN / _security.USER_ID (instance properties)
		/// </summary>
		private readonly Security _security;

		// =====================================================================================
		// Constructor
		// =====================================================================================

		/// <summary>
		/// Constructs the handler with all required DI services.
		/// </summary>
		/// <param name="httpContextAccessor">
		///   Replaces <c>HttpContext.Current</c> for distributed session access (read/write ACLFIELD_* keys).
		/// </param>
		/// <param name="security">
		///   Replaces static <c>SplendidCRM.Security</c> for IS_ADMIN (admin bypass) and
		///   USER_ID (ownership comparison in ACL_FIELD_ACCESS constructor).
		/// </param>
		public FieldAuthorizationHandler(
			IHttpContextAccessor httpContextAccessor,
			Security             security)
		{
			_httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
			_security            = security            ?? throw new ArgumentNullException(nameof(security));
		}

		// =====================================================================================
		// ASP.NET Core AuthorizationHandler<T> — HandleRequirementAsync override
		// Entry point used by the ASP.NET Core authorization middleware pipeline.
		// Replicates Security.GetUserFieldSecurity (lines 795-820) in DI-compatible form.
		// =====================================================================================

		/// <summary>
		/// ASP.NET Core authorization pipeline entry point.
		///
		/// Evaluates field-level ACL for <paramref name="requirement"/> by:
		///   1. Checking IS_ADMIN admin bypass (#if !DEBUG, mirroring Security.cs lines 801-804).
		///   2. Reading the ACLFIELD_{MODULE}_{FIELD} session key via IHttpContextAccessor.
		///   3. Applying NOT_SET→FULL_ACCESS rule (Security.cs lines 810-811).
		///   4. Constructing ACL_FIELD_ACCESS with ownership context (Security.USER_ID, gOWNER_ID).
		///   5. Calling IsReadable() or IsWriteable() based on requirement.RequiredAccess.
		///   6. Signaling context.Succeed(requirement) when access is granted.
		///
		/// Behavior:
		///   • Access granted → <c>context.Succeed(requirement)</c> is called.
		///   • Access denied → do NOT call context.Fail() (let policy default decide; allows other handlers to override).
		///   • Session unavailable → do NOT grant access (return Task.CompletedTask without Succeed).
		/// </summary>
		protected override Task HandleRequirementAsync(
			AuthorizationHandlerContext       context,
			FieldAuthorizationRequirement     requirement)
		{
			// ---------------------------------------------------------------------------------
			// Step 1: Admin bypass in release builds (IS_ADMIN → FULL_ACCESS).
			// #if !DEBUG preserves original Security.cs line 801-804 behavior:
			//   In debug builds, admin bypass is disabled so that ACL logic can be tested.
			//   In release builds, admin always gets full access.
			// ---------------------------------------------------------------------------------
#if !DEBUG
			// 01/18/2010 Paul.  Disable Admin access in a debug build so that we can test the logic.
			if (_security.IS_ADMIN)
			{
				context.Succeed(requirement);
				return Task.CompletedTask;
			}
#endif

			// ---------------------------------------------------------------------------------
			// Step 2: Obtain the distributed session — abort if unavailable.
			// BEFORE: HttpContext.Current.Session == null check (Security.cs line 797)
			// AFTER:  _httpContextAccessor.HttpContext?.Session null check
			// ---------------------------------------------------------------------------------
			ISession session = _httpContextAccessor.HttpContext?.Session;
			if (session == null)
			{
				// Session not available (background service context, SOAP call, etc.).
				// Cannot evaluate field ACL — do not grant access.
				return Task.CompletedTask;
			}

			// ---------------------------------------------------------------------------------
			// Step 3: Read field-level ACL value from distributed session.
			// Session key pattern: "ACLFIELD_" + sMODULE_NAME + "_" + sFIELD_NAME (line 807)
			// BEFORE: HttpContext.Current.Session["ACLFIELD_Accounts_name"]
			// AFTER:  session.GetString("ACLFIELD_Accounts_name")
			// ---------------------------------------------------------------------------------
			string sAclKey  = "ACLFIELD_" + requirement.ModuleName + "_" + requirement.FieldName;
			int nACLACCESS  = Sql.ToInteger(session.GetString(sAclKey));

			// ---------------------------------------------------------------------------------
			// Step 4: NOT_SET (0) → FULL_ACCESS (Security.cs lines 810-811).
			// Zero is a special sentinel meaning "no explicit ACL row was stored" — in this
			// case the field inherits unrestricted access.
			// ---------------------------------------------------------------------------------
			if (nACLACCESS == Security.ACL_FIELD_ACCESS.NOT_SET)
				nACLACCESS = Security.ACL_FIELD_ACCESS.FULL_ACCESS;

			// ---------------------------------------------------------------------------------
			// Step 5: Create ownership-aware ACL_FIELD_ACCESS object.
			// BEFORE: new ACL_FIELD_ACCESS(nACLACCESS, gOWNER_ID) — referenced static Security.USER_ID
			// AFTER:  new ACL_FIELD_ACCESS(nACLACCESS, gOWNER_ID, _security.USER_ID)
			//         gCurrentUserId passed explicitly to break static Security.USER_ID dependency.
			// ---------------------------------------------------------------------------------
			Security.ACL_FIELD_ACCESS acl = new Security.ACL_FIELD_ACCESS(
				nACLACCESS,
				requirement.AssignedUserId,
				_security.USER_ID);

			// ---------------------------------------------------------------------------------
			// Step 6: Evaluate field readability or writability.
			// IsReadable() — checks FULL_ACCESS, NONE, bIsNew/bIsOwner/OWNER_READ_ONLY (lines 743-755)
			// IsWriteable() — checks FULL_ACCESS, NONE, owner-conditional write conditions (lines 757-770)
			// ---------------------------------------------------------------------------------
			bool bAccessGranted = string.Equals(requirement.RequiredAccess, "write", StringComparison.OrdinalIgnoreCase)
				? acl.IsWriteable()
				: acl.IsReadable();

			// ---------------------------------------------------------------------------------
			// Step 7: Signal success; do NOT call context.Fail() to allow other handlers to override.
			// ---------------------------------------------------------------------------------
			if (bAccessGranted)
				context.Succeed(requirement);

			return Task.CompletedTask;
		}

		// =====================================================================================
		// SetUserFieldSecurity — field ACL write (Security.cs lines 781-793)
		// Migrated from Security.SetUserFieldSecurity(string, string, int) instance method.
		// Exposed on the handler so that controller actions and session init code (SplendidInit)
		// can populate field ACL session values without depending on Security directly.
		//
		// BEFORE: HttpContext.Current.Session["ACLFIELD_MODULE_FIELD"] = nACLACCESS (int stored by-reference)
		// AFTER:  session.SetString("ACLFIELD_MODULE_FIELD", nACLACCESS.ToString()) (string in distributed session)
		// =====================================================================================

		/// <summary>
		/// Stores a field-level ACL access value in the distributed session.
		///
		/// Migrated from <c>Security.SetUserFieldSecurity(string, string, int)</c> (lines 781–793).
		/// Zero (NOT_SET) is excluded from storage — a missing session key means "inherit full access"
		/// when evaluated by <see cref="HandleRequirementAsync"/>.
		///
		/// <para>
		/// BEFORE: <c>HttpContext.Current.Session["ACLFIELD_" + module + "_" + field] = nACLACCESS;</c><br/>
		/// AFTER:  <c>session.SetString("ACLFIELD_" + module + "_" + field, nACLACCESS.ToString());</c>
		/// </para>
		/// </summary>
		/// <param name="sMODULE_NAME">CRM module name. Must not be empty.</param>
		/// <param name="sFIELD_NAME">Field name within the module. Must not be empty.</param>
		/// <param name="nACLACCESS">
		///   ACL access level to store. Use <see cref="Security.ACL_FIELD_ACCESS"/> constants.
		///   Zero (NOT_SET) is silently ignored — zero is not stored (means "use full access default").
		/// </param>
		/// <exception cref="Exception">
		///   Thrown when the session is unavailable, or when sMODULE_NAME or sFIELD_NAME is empty.
		/// </exception>
		public void SetUserFieldSecurity(string sMODULE_NAME, string sFIELD_NAME, int nACLACCESS)
		{
			// BEFORE: if ( HttpContext.Current == null || HttpContext.Current.Session == null )
			//             throw(new Exception("HttpContext.Current.Session is null"));
			// AFTER:  distributed session via IHttpContextAccessor
			ISession session = _httpContextAccessor.HttpContext?.Session;
			if (session == null)
				throw new Exception("HttpContext.Current.Session is null");

			// 06/04/2006 Paul.  Verify that sMODULE_NAME is not empty.
			// Preserved from Security.cs lines 786-789.
			if (Sql.IsEmptyString(sMODULE_NAME))
				throw new Exception("SetUserFieldSecurity: sMODULE_NAME should not be empty.");
			if (Sql.IsEmptyString(sFIELD_NAME))
				throw new Exception("SetUserFieldSecurity: sFIELD_NAME should not be empty.");

			// 01/17/2010 Paul.  Zero is a special value that means NOT_SET.
			// Do not store zero — a missing session key means "full access" when evaluated.
			// Preserved from Security.cs line 791.
			if (nACLACCESS != 0)
			{
				// BEFORE: HttpContext.Current.Session["ACLFIELD_" + sMODULE_NAME + "_" + sFIELD_NAME] = nACLACCESS;
				// AFTER:  session.SetString(...) — distributed session requires string values
				session.SetString(
					"ACLFIELD_" + sMODULE_NAME + "_" + sFIELD_NAME,
					nACLACCESS.ToString());
			}
		}
	}
}
