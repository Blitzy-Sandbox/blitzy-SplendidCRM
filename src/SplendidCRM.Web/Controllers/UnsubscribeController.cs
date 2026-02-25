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
// .NET 10 Migration: SplendidCRM/RemoveMe.aspx.cs → src/SplendidCRM.Web/Controllers/UnsubscribeController.cs
// Changes applied:
//   - REMOVED: WebForms base class SplendidPage → ControllerBase (ASP.NET Core)
//   - REMOVED: using System.Web.UI.WebControls; using System.Web.UI.HtmlControls; (WebForms namespaces)
//   - REMOVED: WebForms controls (Literal, RadioButtonList, Button, Label) — no longer needed in API
//   - REMOVED: ViewState["TARGET_ID"] / ViewState["TARGET_TYPE"] — replaced by JSON request/response properties
//   - REMOVED: AuthenticationRequired() override → replaced by [AllowAnonymous] attribute on each action
//   - REMOVED: OnInit(EventArgs) / InitializeComponent() WebForms lifecycle methods → not needed in MVC
//   - REMOVED: IsPostBack check → GET corresponds to initial page load (!IsPostBack); POST to Page_Command submit
//   - REPLACED: Request["identifier"] → [FromQuery] string identifier parameter (ASP.NET Core query string)
//   - REPLACED: Request["PrecompileOnly"] → Request.Query["PrecompileOnly"].ToString()
//   - REPLACED: SplendidCache.List() (static call pattern) → _splendidCache.List() (DI instance)
//   - REPLACED: radREASON.DataSource = SplendidCache.List() → returns reason list as JSON array
//   - REPLACED: radREASON.Text (selected radio button text) → body.reason (JSON request body field)
//   - REPLACED: ViewState backing store → stateless request/response: TARGET_ID/TARGET_TYPE in GET response,
//               client echoes them in POST request body
//   - REPLACED: lblError.Text / lblWarning.Text → JSON response properties (error, warning)
//   - REPLACED: litREMOVE_ME_HEADER.Text / litREMOVE_ME_FOOTER.Text → JSON response properties (header, footer)
//   - PRESERVED: Namespace SplendidCRM (matching original source namespace)
//   - PRESERVED: All business logic: tracker update, opt-out, note creation stored procedure calls
//   - PRESERVED: Identical stored procedure signatures: spCAMPAIGN_LOG_UpdateTracker, spCAMPAIGNS_OptOut,
//                spNOTES_Update (all parameters, all Guid.Empty / String.Empty defaults)
//   - PRESERVED: L10n terminology key lookups (Campaigns.LBL_REMOVE_ME_*, Campaigns.LBL_USERS_CANNOT_OPTOUT)
//   - PRESERVED: PrecompileOnly skip behavior (from original line 124)
//   - PRESERVED: try/catch error handling with SplendidError.SystemError on exception
//   - PRESERVED: SplendidError.SystemMessage "Log" call with identifier value (from original line 93)
//   - ADDED: [ApiController], [Route("RemoveMe.aspx")], [HttpGet], [HttpPost], [AllowAnonymous] attributes
//   - ADDED: Constructor injection of SplendidError, SplendidCache, IHttpContextAccessor, IMemoryCache
//   - ADDED: UnsubscribeReasonRequest DTO class replacing WebForms ViewState / RadioButtonList.Text pattern
#nullable disable
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	/// <summary>
	/// Campaign unsubscribe (opt-out) controller.
	/// Converted from SplendidCRM/RemoveMe.aspx.cs for .NET 10 ASP.NET Core.
	///
	/// Handles the two-step campaign unsubscribe flow:
	///   Step 1 (GET):  Validates the tracker identifier, opts out the target contact/lead,
	///                  and returns the reason dropdown list and localized header/footer text.
	///   Step 2 (POST): Accepts the selected opt-out reason and records it as a CRM Note via
	///                  the spNOTES_Update stored procedure, then returns the step-2 confirmation text.
	///
	/// This endpoint is publicly accessible without authentication, matching the original
	/// AuthenticationRequired() { return false; } WebForms override at RemoveMe.aspx.cs line 44.
	/// Campaign unsubscribe links embedded in email bodies must work without requiring user login.
	///
	/// Route: /RemoveMe.aspx (backward-compatible with existing campaign email unsubscribe links)
	///   GET  ?identifier={guid}         — initial unsubscribe page load
	///   POST (JSON body)                — submit selected opt-out reason
	/// </summary>
	[ApiController]
	[Route("RemoveMe.aspx")]
	public class UnsubscribeController : ControllerBase
	{
		// =====================================================================================
		// DI fields
		// =====================================================================================

		/// <summary>
		/// Error logging service (static SystemMessage/SystemError methods used for logging).
		/// Injected via DI following project conventions; provides full HTTP context logging fidelity
		/// when used via the static overloads that accept HttpContext.
		/// </summary>
		private readonly SplendidError      _splendidError      ;

		/// <summary>
		/// Metadata caching service. Provides the remove_me_reason_dom dropdown DataTable
		/// via List("remove_me_reason_dom"), replacing the static SplendidCache.List() call.
		/// BEFORE: radREASON.DataSource = SplendidCache.List("remove_me_reason_dom");
		/// AFTER:  DataTable dtReasons  = _splendidCache.List("remove_me_reason_dom");
		/// </summary>
		private readonly SplendidCache      _splendidCache      ;

		/// <summary>
		/// HTTP context accessor for reading the session USER_LANG value used to construct
		/// the per-request L10N localization instance.
		/// Replaces the implicit HttpContext.Current access from the WebForms SplendidPage base class.
		/// </summary>
		private readonly IHttpContextAccessor _httpContextAccessor;

		/// <summary>
		/// In-memory cache, passed to the L10N constructor for terminology lookups.
		/// Replaces HttpRuntime.Cache and Application[] from the original WebForms infrastructure.
		/// </summary>
		private readonly IMemoryCache         _memoryCache        ;

		// =====================================================================================
		// Constructor
		// =====================================================================================

		/// <summary>
		/// Constructs an UnsubscribeController with required services via dependency injection.
		/// </summary>
		/// <param name="splendidError">Error logging service.</param>
		/// <param name="splendidCache">Metadata cache providing the opt-out reason dropdown list.</param>
		/// <param name="httpContextAccessor">HTTP context accessor for session culture retrieval.</param>
		/// <param name="memoryCache">Memory cache passed to L10N for terminology lookups.</param>
		public UnsubscribeController
			( SplendidError       splendidError
			, SplendidCache       splendidCache
			, IHttpContextAccessor httpContextAccessor
			, IMemoryCache         memoryCache
			)
		{
			_splendidError       = splendidError      ;
			_splendidCache       = splendidCache      ;
			_httpContextAccessor = httpContextAccessor;
			_memoryCache         = memoryCache        ;
		}

		// =====================================================================================
		// Private helpers
		// =====================================================================================

		/// <summary>
		/// Creates an L10N localization instance scoped to the current user's language.
		/// Attempts to read USER_LANG from the distributed session; falls back to "en-US"
		/// when the session is unavailable (anonymous endpoint, no active session).
		///
		/// BEFORE (WebForms): L10n property was inherited from SplendidPage base class, which
		///   read the culture from HttpContext.Current.Session["USER_LANG"] automatically.
		/// AFTER (.NET 10): Explicitly reads the session value via IHttpContextAccessor.
		/// </summary>
		/// <returns>L10N instance for the resolved culture.</returns>
		private L10N GetL10N()
		{
			string sLang = "en-US";
			try
			{
				// Read culture from distributed session — may be null for anonymous requests
				// BEFORE: L10n was automatically set by SplendidPage using HttpContext.Current.Session["USER_LANG"]
				// AFTER:  Explicitly read via IHttpContextAccessor for DI-compatible access
				string sessionLang = _httpContextAccessor.HttpContext?.Session?.GetString("USER_LANG");
				if ( !Sql.IsEmptyString(sessionLang) )
					sLang = sessionLang;
			}
			catch
			{
				// Anonymous endpoint — session may not be configured or active; default to en-US
			}
			return new L10N(sLang, _memoryCache);
		}

		/// <summary>
		/// Converts the reason list DataTable (columns: NAME, DISPLAY_NAME) to a JSON-serializable list.
		///
		/// BEFORE (WebForms):
		///   radREASON.DataSource = SplendidCache.List("remove_me_reason_dom");
		///   radREASON.DataBind();
		/// AFTER (REST API):
		///   Returns the list items as a JSON array of {name, display_name} objects in the GET response.
		///   The React/frontend client renders the reason selection UI from this data.
		/// </summary>
		/// <param name="dt">DataTable with NAME and DISPLAY_NAME columns from vwTERMINOLOGY.</param>
		/// <returns>List of anonymous objects with 'name' and 'display_name' properties.</returns>
		private static List<object> ConvertReasonsList(DataTable dt)
		{
			var result = new List<object>();
			if ( dt != null )
			{
				foreach ( DataRow row in dt.Rows )
				{
					result.Add(new
					{
						name         = Sql.ToString(row["NAME"        ]),
						display_name = Sql.ToString(row["DISPLAY_NAME"])
					});
				}
			}
			return result;
		}

		// =====================================================================================
		// Actions
		// =====================================================================================

		/// <summary>
		/// GET /RemoveMe.aspx?identifier={guid}
		///
		/// Initial unsubscribe request handler. Converted from Page_Load (RemoveMe.aspx.cs lines 89-137).
		///
		/// Corresponds to the !IsPostBack branch of the original Page_Load:
		///   1. Logs the request via SplendidError.SystemMessage (line 93).
		///   2. Loads the opt-out reason dropdown list from cache (line 94-95).
		///   3. Validates the tracker identifier GUID (lines 99-128):
		///      a. If valid: updates the campaign log tracker, checks if target is a User (disallowed),
		///         opts out the target contact/lead, and returns step-1 form data.
		///      b. If invalid and not PrecompileOnly: returns an invalid identifier warning.
		///
		/// Response (application/json):
		///   On valid identifier (non-User):  { success, TARGET_ID, TARGET_TYPE, header, footer, showForm, reasons }
		///   On User target:                  { success: false, error, showForm: false }
		///   On invalid identifier:           { success: false, warning, showForm: false, reasons }
		///   On PrecompileOnly:               { success: true, showForm: false }
		///   On exception:                    HTTP 500 { error }
		///
		/// Backward compatibility: This endpoint is called by campaign email unsubscribe links of the form:
		///   https://crm.example.com/RemoveMe.aspx?identifier={tracker_guid}
		/// </summary>
		/// <param name="identifier">Campaign tracker GUID from the email unsubscribe link query parameter.</param>
		[HttpGet]
		[AllowAnonymous]
		public IActionResult Get([FromQuery] string identifier = null)
		{
			// Log the unsubscribe request with the identifier value for audit purposes
			// BEFORE: SplendidError.SystemMessage("Log", new StackTrace(true).GetFrame(0), "Remove Me " + Request["identifier"]);
			// AFTER:  Use HttpContext overload for full logging fidelity (session ID, IP address)
			SplendidError.SystemMessage(HttpContext, "Log", new StackTrace(true).GetFrame(0), "Remove Me " + identifier);

			// Load the opt-out reason dropdown list from cache
			// BEFORE: radREASON.DataSource = SplendidCache.List("remove_me_reason_dom");
			// AFTER:  Returns as JSON array; client renders the UI
			DataTable        dtReasons = _splendidCache.List("remove_me_reason_dom");
			List<object>     reasons   = ConvertReasonsList(dtReasons);

			try
			{
				// Parse identifier to Guid — identical to original line 99
				// BEFORE: Guid gID = Sql.ToGuid(Request["identifier"]);
				// AFTER:  identifier is passed as [FromQuery] string; Sql.ToGuid(string) handles null/empty
				Guid gID = Sql.ToGuid(identifier);

				if ( !Sql.IsEmptyGuid(gID) )
				{
					// Valid identifier — look up the campaign log tracker record (line 101-104)
					Guid   gTARGET_ID   = Guid.Empty   ;
					string sTARGET_TYPE = string.Empty ;

					// Log the tracker event and retrieve the target contact/lead identity
					// Signature: spCAMPAIGN_LOG_UpdateTracker(Guid KEY, string TYPE, Guid TRKRS_ID, ref Guid TARGET_ID, ref string TARGET_TYPE)
					SqlProcs.spCAMPAIGN_LOG_UpdateTracker(gID, "removed", Guid.Empty, ref gTARGET_ID, ref sTARGET_TYPE);

					if ( sTARGET_TYPE == "Users" )
					{
						// Users cannot opt out of campaign emails (from original lines 105-110)
						// BEFORE: lblError.Text = L10n.Term("Campaigns.LBL_USERS_CANNOT_OPTOUT");
						//         radREASON.Visible = false; btnSubmit.Visible = false;
						// AFTER:  Return JSON error response; client shows message and hides form
						L10N L10n = GetL10N();
						return Ok(new
						{
							success  = false                                             ,
							error    = L10n.Term("Campaigns.LBL_USERS_CANNOT_OPTOUT")   ,
							showForm = false
						});
					}
					else
					{
						// Valid non-User target — opt out and return step-1 form data (lines 113-119)
						// BEFORE: ViewState["TARGET_ID"] = gTARGET_ID; ViewState["TARGET_TYPE"] = sTARGET_TYPE;
						// AFTER:  Return TARGET_ID / TARGET_TYPE in response body; client echoes them in POST

						// Mark the target as opted out of future campaigns (line 119)
						SqlProcs.spCAMPAIGNS_OptOut(gTARGET_ID, sTARGET_TYPE);

						// Return step-1 form data with localized header/footer text and reason options
						// BEFORE: litREMOVE_ME_HEADER.Text = L10n.Term("Campaigns.LBL_REMOVE_ME_HEADER_STEP1");
						//         litREMOVE_ME_FOOTER.Text = L10n.Term("Campaigns.LBL_REMOVE_ME_FOOTER_STEP1");
						L10N L10n = GetL10N();
						return Ok(new
						{
							success     = true                                                  ,
							TARGET_ID   = gTARGET_ID                                            ,
							TARGET_TYPE = sTARGET_TYPE                                          ,
							header      = L10n.Term("Campaigns.LBL_REMOVE_ME_HEADER_STEP1")    ,
							footer      = L10n.Term("Campaigns.LBL_REMOVE_ME_FOOTER_STEP1")    ,
							showForm    = true                                                  ,
							reasons     = reasons
						});
					}
				}
				// 11/23/2012 Paul.  Skip during precompile.
				// BEFORE: else if ( !Sql.ToBoolean(Request["PrecompileOnly"]) )
				// AFTER:  Request.Query["PrecompileOnly"] returns StringValues; .ToString() gives "" for missing
				else if ( !Sql.ToBoolean(Request.Query["PrecompileOnly"].ToString()) )
				{
					// 11/23/2012 Paul.  Don't use the standard error label as it will cause the precompile to stop.
					// BEFORE: lblWarning.Text = L10n.Term("Campaigns.LBL_REMOVE_ME_INVALID_IDENTIFIER");
					// AFTER:  Return JSON warning; client displays the message
					L10N L10n = GetL10N();
					return Ok(new
					{
						success  = false                                                         ,
						warning  = L10n.Term("Campaigns.LBL_REMOVE_ME_INVALID_IDENTIFIER")      ,
						showForm = false                                                         ,
						reasons  = reasons
					});
				}

				// PrecompileOnly=true — identifier is missing by design; return empty success
				return Ok(new { success = true, showForm = false });
			}
			catch ( Exception ex )
			{
				// BEFORE: SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				//         lblError.Text = ex.Message; radREASON.Visible = false; btnSubmit.Visible = false;
				// AFTER:  Log error and return HTTP 500 with message
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>
		/// POST /RemoveMe.aspx
		///
		/// Submit opt-out reason handler. Converted from Page_Command (RemoveMe.aspx.cs lines 49-87).
		///
		/// Records the selected opt-out reason as a CRM Note via spNOTES_Update, then returns
		/// the step-2 confirmation header/footer text.
		///
		/// BEFORE (WebForms): Page_Command was called by the form Submit button click event.
		///   TARGET_ID and TARGET_TYPE were read from ViewState set during Page_Load.
		///   radREASON.Text provided the selected reason text.
		/// AFTER (REST API): All state is passed in the JSON request body:
		///   TARGET_ID and TARGET_TYPE are echoed back from the GET response.
		///   reason contains the selected option value/text.
		///
		/// Request body (application/json):
		///   { "TARGET_ID": "guid", "TARGET_TYPE": "Contacts", "reason": "Selected reason text" }
		///
		/// Response (application/json):
		///   On success: { success: true, header, footer }   (step-2 confirmation)
		///   On error:   HTTP 500 { error }
		/// </summary>
		/// <param name="body">Request body containing target context and selected opt-out reason.</param>
		[HttpPost]
		[AllowAnonymous]
		public IActionResult Post([FromBody] UnsubscribeReasonRequest body)
		{
			try
			{
				if ( body == null )
					return BadRequest(new { error = "Request body is required." });

				// BEFORE: Guid gNOTE_ID = Guid.Empty;
				//         Guid gTARGET_ID = Sql.ToGuid(ViewState["TARGET_ID"]);
				//         string sTARGET_TYPE = Sql.ToString(ViewState["TARGET_TYPE"]);
				// AFTER:  ViewState replaced by JSON body; Sql helpers ensure null-safe conversion
				Guid   gNOTE_ID     = Guid.Empty                    ;
				Guid   gTARGET_ID   = Sql.ToGuid  (body.TARGET_ID  );
				string sTARGET_TYPE = Sql.ToString (body.TARGET_TYPE);
				string sReason      = Sql.ToString (body.reason     );

				// Create a CRM Note recording the selected opt-out reason (from original lines 65-79)
				// All parameter values and ordering are IDENTICAL to the original WebForms call.
				// BEFORE: , "RemoveMe " + radREASON.Text  (selected radio button display text)
				// AFTER:  , "RemoveMe " + sReason         (reason field from JSON body)
				// 05/17/2017 Paul.  Add Tags module.
				// 11/07/2017 Paul.  Add IS_PRIVATE for use by a large customer.
				// 11/30/2017 Paul.  Add ASSIGNED_SET_ID for Dynamic User Assignment.
				SqlProcs.spNOTES_Update
					( ref gNOTE_ID
					, "RemoveMe " + sReason   // sNAME
					, sTARGET_TYPE            // sPARENT_TYPE
					, gTARGET_ID              // gPARENT_ID
					, Guid.Empty              // gCONTACT_ID
					, String.Empty            // sDESCRIPTION
					, Guid.Empty              // gTEAM_ID
					, String.Empty            // sTEAM_SET_LIST
					, Guid.Empty              // gASSIGNED_USER_ID
					, String.Empty            // sTAG_SET_NAME
					, false                   // bIS_PRIVATE
					, String.Empty            // sASSIGNED_SET_LIST
					);

				// Return step-2 confirmation header/footer (from original lines 55-56)
				// BEFORE: litREMOVE_ME_HEADER.Text = L10n.Term("Campaigns.LBL_REMOVE_ME_HEADER_STEP2");
				//         litREMOVE_ME_FOOTER.Text = L10n.Term("Campaigns.LBL_REMOVE_ME_FOOTER_STEP2");
				// AFTER:  Return localized strings in JSON response; client renders the confirmation page
				L10N L10n = GetL10N();
				return Ok(new
				{
					success = true                                                  ,
					header  = L10n.Term("Campaigns.LBL_REMOVE_ME_HEADER_STEP2")   ,
					footer  = L10n.Term("Campaigns.LBL_REMOVE_ME_FOOTER_STEP2")
				});
			}
			catch ( Exception ex )
			{
				// BEFORE: SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				//         lblError.Text = ex.Message;
				// AFTER:  Log error and return HTTP 500 with message
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				return StatusCode(500, new { error = ex.Message });
			}
		}
	}

	/// <summary>
	/// Request body DTO for POST /RemoveMe.aspx.
	///
	/// Carries the unsubscribe reason selected by the user plus the target context
	/// previously returned by GET /RemoveMe.aspx. Replaces the WebForms ViewState pattern:
	///
	/// BEFORE (WebForms ViewState backing):
	///   ViewState["TARGET_ID"]   = gTARGET_ID;    (set in Page_Load, read in Page_Command)
	///   ViewState["TARGET_TYPE"] = sTARGET_TYPE;  (set in Page_Load, read in Page_Command)
	///   radREASON.Text                             (selected RadioButtonList item text)
	///
	/// AFTER (stateless REST API):
	///   Client echoes TARGET_ID and TARGET_TYPE from the GET response in the POST body.
	///   reason field carries the selected opt-out reason text or value.
	/// </summary>
	public class UnsubscribeReasonRequest
	{
		/// <summary>
		/// Target contact/lead ID (Guid as string) from the GET response.
		/// Replaces ViewState["TARGET_ID"] from RemoveMe.aspx.cs line 61.
		/// </summary>
		public string TARGET_ID   { get; set; }

		/// <summary>
		/// Target type string from the GET response (e.g. "Contacts", "Leads", "Prospects").
		/// Replaces ViewState["TARGET_TYPE"] from RemoveMe.aspx.cs line 62.
		/// </summary>
		public string TARGET_TYPE { get; set; }

		/// <summary>
		/// The opt-out reason text selected by the user from the reason dropdown list.
		/// Replaces radREASON.Text from RemoveMe.aspx.cs line 67.
		/// Maps to "RemoveMe " + reason as the CRM Note name in spNOTES_Update.
		/// </summary>
		public string reason      { get; set; }
	}
}
