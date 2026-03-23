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
// .NET 10 Migration: SplendidCRM/Administration/Impersonation.svc.cs → src/SplendidCRM.Web/Controllers/ImpersonationController.cs
// Changes applied:
//   - CONVERTED: [ServiceContract] + [ServiceBehavior(IncludeExceptionDetailInFaults=true)]
//               + [AspNetCompatibilityRequirements(RequirementsMode=Required)]
//               → [ApiController] (ASP.NET Core Web API controller attribute)
//   - CONVERTED: [OperationContract] + [WebInvoke(Method="POST",
//               BodyStyle=WebMessageBodyStyle.WrappedRequest,
//               RequestFormat=WebMessageFormat.Json, ResponseFormat=WebMessageFormat.Json)]
//               → [HttpPost("Impersonate")] with JSON request body
//   - REMOVED:  using System.ServiceModel;         (WCF — not needed in ASP.NET Core)
//   - REMOVED:  using System.ServiceModel.Web;     (WCF — not needed in ASP.NET Core)
//   - REMOVED:  using System.ServiceModel.Activation; (WCF — not needed in ASP.NET Core)
//   - REMOVED:  using System.Web;                  (replaced by Microsoft.AspNetCore.Http)
//   - REMOVED:  using System.Web.SessionState;     (replaced by ASP.NET Core ISession)
//   - ADDED:    using Microsoft.AspNetCore.Mvc;    (ControllerBase, [ApiController], [Route], [HttpPost])
//   - ADDED:    using Microsoft.AspNetCore.Http;   (IHttpContextAccessor, ISession.GetString/SetString)
//   - ADDED:    using Microsoft.AspNetCore.Hosting; (IWebHostEnvironment, ContentRootPath)
//   - ADDED:    using Microsoft.Extensions.Caching.Memory; (IMemoryCache for L10N construction)
//   - RENAMED:  class Impersonation → class ImpersonationController (ASP.NET Core naming convention)
//   - RENAMED:  namespace SplendidCRM.Administration → namespace SplendidCRM (flat namespace per project convention)
//   - REPLACED: HttpContext.Current.Application → not used (ApplicationState was declared but unused in source)
//   - REPLACED: HttpContext.Current.Server.MapPath("~/Administration/Impersonation.svc")
//               → Path.Combine(IWebHostEnvironment.ContentRootPath, "Administration", "Impersonation.svc")
//               NOTE: In .NET 10 deployments, place a marker file at that path to enable impersonation
//               (preserves original file-existence-equals-enabled semantics from IIS/WCF deployment)
//   - REPLACED: new L10N(Sql.ToString(HttpContext.Current.Session["USER_SETTINGS/CULTURE"]))
//               → GetL10N() helper: reads culture from ISession via IHttpContextAccessor,
//                 constructs new L10N(culture, _memoryCache) for distributed session compatibility
//   - REPLACED: static Security.IsAuthenticated() → DI-injected _security.IsAuthenticated()
//   - REPLACED: static Security.IS_ADMIN          → DI-injected _security.IS_ADMIN
//   - REPLACED: static SplendidInit.LoginUser(ID, "Impersonate")
//               → DI-injected _splendidInit.LoginUser(ID, "Impersonate") — identical Guid overload
//   - REPLACED: HttpContext.Current.Session["USER_IMPERSONATION"] = true
//               → _httpContextAccessor.HttpContext.Session.SetString("USER_IMPERSONATION", "true")
//               (bool → "true" string for distributed session Redis/SQL Server compatibility)
//   - CONVERTED: exception throws for auth failure → HTTP 403 StatusCode responses (REST API pattern)
//   - PRESERVED: Admin-only enforcement: IsAuthenticated() && IS_ADMIN checks (original lines 45-48)
//   - PRESERVED: Impersonation feature availability check (original lines 49-52)
//   - PRESERVED: SplendidInit.LoginUser(ID, "Impersonate") call (original line 54)
//   - PRESERVED: USER_IMPERSONATION session flag (original line 55)
//   - PRESERVED: L10N error terms: ACL.LBL_INSUFFICIENT_ACCESS, Users.ERR_IMPERSONATION_DISABLED
//   - ROUTE:     POST /Administration/Impersonation.svc/Impersonate — exact backward-compatible WCF service route
//   - Minimal change clause (AAP 0.8.1): only changes required for .NET Framework 4.8 → .NET 10 ASP.NET Core
#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	/// <summary>
	/// Admin user impersonation controller.
	/// Migrated from SplendidCRM/Administration/Impersonation.svc.cs (.NET Framework 4.8 → .NET 10 ASP.NET Core).
	///
	/// Converts the WCF ServiceContract Impersonation service to an ASP.NET Core Web API controller,
	/// preserving the identical backward-compatible route path, admin-only authorization enforcement,
	/// and the USER_IMPERSONATION distributed session flag set on successful impersonation.
	///
	/// Endpoint: POST /Administration/Impersonation.svc/Impersonate
	/// Authorization: Requires authenticated administrator (IS_ADMIN = true)
	/// Request body: {"ID": "guid-of-user-to-impersonate"} (matching WCF WrappedRequest JSON format)
	/// Response body: {"d": null} on success (matching WCF void return convention)
	/// </summary>
	[ApiController]
	[Route("Administration/Impersonation.svc")]
	public class ImpersonationController : ControllerBase
	{
		// =====================================================================================
		// Private fields (DI-injected services)
		// All replace static access patterns from the legacy WCF service:
		//   _security            ← static Security.IsAuthenticated() / Security.IS_ADMIN
		//   _splendidInit        ← static SplendidInit.LoginUser(ID, "Impersonate")
		//   _httpContextAccessor ← HttpContext.Current
		//   _env                 ← HttpContext.Current.Server (for MapPath)
		//   _memoryCache         ← HttpRuntime.Cache / Application[] (passed to L10N constructor)
		// =====================================================================================

		private readonly Security             _security           ;
		private readonly SplendidInit         _splendidInit       ;
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly IWebHostEnvironment  _env                ;
		private readonly IMemoryCache         _memoryCache        ;
		private readonly ILogger<ImpersonationController> _logger ;

		// =====================================================================================
		// Constructor
		// =====================================================================================

		/// <summary>
		/// Constructs an ImpersonationController with required services via dependency injection.
		/// All service parameters replace static helper calls used in the legacy WCF Impersonation service.
		/// </summary>
		/// <param name="security">
		/// Authentication and ACL service — replaces static Security.IsAuthenticated() and Security.IS_ADMIN
		/// used for the admin-only access gate in the original Impersonate() method (lines 45-48).
		/// </param>
		/// <param name="splendidInit">
		/// Application bootstrap service — provides LoginUser(Guid, string) replacing the static
		/// SplendidInit.LoginUser(ID, "Impersonate") call in the original method (line 54).
		/// </param>
		/// <param name="httpContextAccessor">
		/// HTTP context accessor replacing HttpContext.Current throughout.
		/// Used for session reads (USER_SETTINGS/CULTURE) and session writes (USER_IMPERSONATION).
		/// </param>
		/// <param name="env">
		/// Web host environment providing ContentRootPath — replaces HttpContext.Current.Server.MapPath()
		/// for the impersonation feature availability file-existence check (original line 49).
		/// </param>
		/// <param name="memoryCache">
		/// Memory cache passed to the L10N constructor for terminology lookups.
		/// Replaces the implicit HttpRuntime.Cache / Application[] backing store used by L10N internally.
		/// </param>
		public ImpersonationController
			( Security             security
			, SplendidInit         splendidInit
			, IHttpContextAccessor httpContextAccessor
			, IWebHostEnvironment  env
			, IMemoryCache         memoryCache
			, ILogger<ImpersonationController> logger
			)
		{
			_security            = security           ;
			_splendidInit        = splendidInit       ;
			_httpContextAccessor = httpContextAccessor;
			_env                 = env                ;
			_memoryCache         = memoryCache        ;
			_logger              = logger             ;
		}

		// =====================================================================================
		// Private helpers
		// =====================================================================================

		/// <summary>
		/// Creates an L10N localization instance scoped to the current user's culture setting.
		///
		/// BEFORE (WCF): new L10N(Sql.ToString(HttpContext.Current.Session["USER_SETTINGS/CULTURE"]))
		/// AFTER (.NET 10): new L10N(sCulture, _memoryCache) using IHttpContextAccessor + IMemoryCache
		///
		/// Reads the USER_SETTINGS/CULTURE string key from the distributed session via IHttpContextAccessor.
		/// Falls back to "en-US" when the session is unavailable or the culture key is not present.
		/// </summary>
		/// <returns>L10N instance configured for the current user's locale.</returns>
		private L10N GetL10N()
		{
			// Default culture — matches original fallback behavior when session is unavailable
			string sCulture = "en-US";
			try
			{
				// BEFORE: Sql.ToString(HttpContext.Current.Session["USER_SETTINGS/CULTURE"])
				// AFTER:  ISession.GetString() via IHttpContextAccessor for distributed session compatibility
				// Sql.ToString() used for safe null-to-empty conversion matching the original Sql helper call
				string sSessionCulture = _httpContextAccessor.HttpContext?.Session?.GetString("USER_SETTINGS/CULTURE");
				if ( !Sql.IsEmptyString(sSessionCulture) )
					sCulture = Sql.ToString(sSessionCulture);
			}
			catch
			{
				// Session may be unavailable outside of a request context — fall through to default en-US
			}
			return new L10N(sCulture, _memoryCache);
		}

		// =====================================================================================
		// Impersonate Action
		//
		// BEFORE (WCF service — Impersonation.svc.cs lines 37-56):
		//   [OperationContract]
		//   [WebInvoke(Method="POST", BodyStyle=WebMessageBodyStyle.WrappedRequest,
		//              RequestFormat=WebMessageFormat.Json, ResponseFormat=WebMessageFormat.Json)]
		//   public void Impersonate(Guid ID)
		//
		// AFTER (ASP.NET Core controller):
		//   [HttpPost("Impersonate")]
		//   public IActionResult Impersonate([FromBody] Guid ID)
		//
		// Route preserved: POST /Administration/Impersonation.svc/Impersonate
		// =====================================================================================

		/// <summary>
		/// Impersonates the specified user on behalf of the authenticated administrator.
		/// Migrated from Impersonation.svc.cs Impersonate(Guid ID) (original lines 37-56).
		///
		/// ROUTE: POST /Administration/Impersonation.svc/Impersonate
		///
		/// Request body (JSON): The Guid of the user to impersonate.
		///   WCF WrappedRequest format for backward compatibility with existing clients:
		///   {"ID": "3fa85f64-5717-4562-b3fc-2c963f66afa6"}
		///
		/// Authorization checks (in order, matching original lines 45-52):
		///   1. Caller must be authenticated — Security.IsAuthenticated() == true
		///   2. Caller must be an administrator — Security.IS_ADMIN == true
		///   3. Impersonation feature must be available — marker file must exist at ContentRootPath
		///
		/// On success (matching original lines 54-55):
		///   - SplendidInit.LoginUser(ID, "Impersonate") loads the target user's full session data,
		///     ACL roles, team memberships, and user preferences
		///   - Session["USER_IMPERSONATION"] is set to "true" (string for distributed session compatibility)
		///   - Returns 200 OK {"d": null} matching WCF void-return convention
		///
		/// On authorization failure:
		///   - Returns 403 Forbidden with localized error message from L10N
		///   (Original code threw Exception; REST API pattern uses HTTP status codes instead)
		///
		/// On unexpected error:
		///   - Returns 500 Internal Server Error with exception message
		///   (Preserves WCF [ServiceBehavior(IncludeExceptionDetailInFaults=true)] detail exposure)
		/// </summary>
		/// <param name="ID">
		/// The GUID of the user to impersonate. Bound from the JSON request body.
		/// Matches the single Guid ID parameter of the original WCF WrappedRequest operation.
		/// </param>
		/// <returns>200 OK on success; 403 Forbidden on authorization failure; 500 on unexpected error.</returns>
		[HttpPost("Impersonate")]
		public IActionResult Impersonate([FromBody] Dictionary<string, object> dict)
		{
			// WCF WrappedRequest body format: {"ID":"guid-value"}
			// ASP.NET Core [FromBody] Dictionary matches this JSON structure.
			Guid ID = Guid.Empty;
			if ( dict != null && dict.ContainsKey("ID") )
				ID = Sql.ToGuid(dict["ID"]);

			// Instantiate L10N with the current user's culture for localized error messages
			// BEFORE: L10N L10n = new L10N(Sql.ToString(HttpContext.Current.Session["USER_SETTINGS/CULTURE"]));
			// AFTER:  Per-request L10N via GetL10N() helper using IHttpContextAccessor and IMemoryCache
			L10N L10n = GetL10N();

			// ---- Admin-only access check (original lines 45-48) ----
			// BEFORE: if (!Security.IsAuthenticated() || !Security.IS_ADMIN)
			//             throw(new Exception(L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS")));
			// AFTER:  DI-injected _security instance methods; exception → HTTP 403 (REST API pattern)
			if ( !_security.IsAuthenticated() || !_security.IS_ADMIN )
			{
				return StatusCode(403, new { error = L10n.Term("ACL.LBL_INSUFFICIENT_ACCESS") });
			}

			// ---- Impersonation feature availability check (original lines 49-52) ----
			// BEFORE: if (!File.Exists(Server.MapPath("~/Administration/Impersonation.svc")))
			//             throw(new Exception(L10n.Term("Users.ERR_IMPERSONATION_DISABLED")));
			// AFTER:  HttpContext.Current.Server.MapPath("~/...") →
			//         Path.Combine(IWebHostEnvironment.ContentRootPath, "Administration", "Impersonation.svc")
			//         In .NET 10 deployments: place a marker file at this path to enable impersonation.
			//         This preserves the original file-existence-equals-feature-enabled semantics from IIS/WCF.
			string sImpersonationMarkerPath = Path.Combine(_env.ContentRootPath, "Administration", "Impersonation.svc");
			// NOTE: Use System.IO.File.Exists() with fully qualified name to avoid ambiguity with
			// ControllerBase.File(byte[], string) method inherited from ASP.NET Core ControllerBase.
			if ( !System.IO.File.Exists(sImpersonationMarkerPath) )
			{
				// BEFORE: throw(new Exception(L10n.Term("Users.ERR_IMPERSONATION_DISABLED")));
				// AFTER:  HTTP 403 with localized error message (REST API pattern)
				return StatusCode(403, new { error = L10n.Term("Users.ERR_IMPERSONATION_DISABLED") });
			}

			try
			{
				// ---- Perform the impersonation (original line 54) ----
				// BEFORE: SplendidInit.LoginUser(ID, "Impersonate");
				// AFTER:  DI-injected _splendidInit.LoginUser(ID, "Impersonate") — identical Guid overload,
				//         loads target user's session data, ACL roles, team memberships, and preferences
				_splendidInit.LoginUser(ID, "Impersonate");

				// ---- Set the impersonation session flag (original line 55) ----
				// BEFORE: HttpContext.Current.Session["USER_IMPERSONATION"] = true;
				// AFTER:  bool true → "true" string for distributed session (Redis/SQL Server) compatibility
				//         IHttpContextAccessor replaces HttpContext.Current
				_httpContextAccessor.HttpContext?.Session.SetString("USER_IMPERSONATION", "true");

				// WCF void return → {"d": null} matching WCF null data contract response pattern
				return Ok(new { d = (object)null });
			}
			catch ( Exception ex )
			{
				_logger.LogError(ex, "ImpersonationController: Error processing impersonation request");
				// Environment-conditional error detail: expose exception message only in Development
				string sErrorMessage = _env.EnvironmentName == "Development" ? ex.Message : "An internal error occurred.";
				return StatusCode(500, new { error = sErrorMessage });
			}
		}
	}
}
