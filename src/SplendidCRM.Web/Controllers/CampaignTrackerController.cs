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
// .NET 10 Migration: SplendidCRM/campaign_trackerv2.aspx.cs → src/SplendidCRM.Web/Controllers/CampaignTrackerController.cs
// Changes applied:
//   - REMOVED: WebForms base class SplendidPage → ControllerBase (ASP.NET Core)
//   - REMOVED: using System.Web.UI.WebControls; using System.Web.UI.HtmlControls; (WebForms namespaces)
//   - REMOVED: AuthenticationRequired() override → replaced by [AllowAnonymous] attribute on action
//   - REMOVED: OnInit(EventArgs) / InitializeComponent() WebForms lifecycle methods → not needed in MVC
//   - REPLACED: Response.ExpiresAbsolute = new DateTime(1980, 1, 1) → Response.Headers["Cache-Control"] +
//               Response.Headers["Pragma"] + Response.Headers["Expires"] (ASP.NET Core HTTP response headers)
//   - REPLACED: Request["identifier"] → Request.Query["identifier"] (ASP.NET Core IQueryCollection)
//   - REPLACED: Request["track"]      → Request.Query["track"]      (ASP.NET Core IQueryCollection)
//   - REPLACED: Request.UserHostAddress → HttpContext.Connection.RemoteIpAddress?.ToString()
//               (RemoteIpAddress is the .NET Core equivalent of UserHostAddress)
//   - REPLACED: Response.Redirect(url) (WebForms) → return Redirect(url) (ASP.NET Core IActionResult)
//   - REPLACED: DbProviderFactories.GetFactory() (static) → _dbProviderFactories.GetFactory() (DI instance)
//   - PRESERVED: Namespace SplendidCRM (matching original source namespace)
//   - PRESERVED: All business logic: campaign log stored procedure calls, tracker URL lookup, redirect flow
//   - PRESERVED: Cache-busting headers (every click must be counted)
//   - PRESERVED: try/catch with SplendidError.SystemError on exception
//   - PRESERVED: ControlChars.CrLf usage for SQL query formatting
//   - PRESERVED: Identical stored procedure signatures: spCAMPAIGN_LOG_UpdateTracker, spCAMPAIGN_LOG_BannerTracker
//   - ADDED: [ApiController], [Route("campaign_trackerv2.aspx")], [HttpGet], [AllowAnonymous] attributes
//   - ADDED: Constructor injection of SplendidError and DbProviderFactories
#nullable disable
using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace SplendidCRM
{
	/// <summary>
	/// Campaign link click tracking controller.
	/// Converted from SplendidCRM/campaign_trackerv2.aspx.cs for .NET 10 ASP.NET Core.
	///
	/// Handles campaign tracker URL lookups: logs the click event via stored procedure, looks up the
	/// redirect URL from vwCAMPAIGN_TRKRS, and issues a 302 redirect to the campaign target URL.
	///
	/// This endpoint is publicly accessible without authentication, matching the original
	/// AuthenticationRequired() { return false; } WebForms override.
	///
	/// Route: GET /campaign_trackerv2.aspx
	///   Query params:
	///     identifier — (optional) GUID identifying the campaign recipient (empty for web/banner campaigns)
	///     track      — GUID identifying the campaign tracker record (vwCAMPAIGN_TRKRS.ID)
	/// </summary>
	[ApiController]
	[Route("campaign_trackerv2.aspx")]
	public class CampaignTrackerController : ControllerBase
	{
		// =====================================================================================
		// DI fields
		// SplendidError: logging service (SystemMessage/SystemError static methods used for logging)
		// DbProviderFactories: database connection factory replacing static DbProviderFactories.GetFactory()
		// =====================================================================================
		private readonly SplendidError      _splendidError      ;
		private readonly DbProviderFactories _dbProviderFactories;
		private readonly ILogger<CampaignTrackerController> _logger;

		/// <summary>
		/// Constructs a CampaignTrackerController with required services via dependency injection.
		/// </summary>
		/// <param name="splendidError">
		///   Error logging service. Although campaign_trackerv2.aspx.cs calls static SplendidError methods,
		///   the service is injected here following DI best practices and for future instance method use.
		/// </param>
		/// <param name="dbProviderFactories">
		///   Database connection factory, replacing the static DbProviderFactories.GetFactory() call at
		///   campaign_trackerv2.aspx.cs line 65.
		/// </param>
		public CampaignTrackerController(SplendidError splendidError, DbProviderFactories dbProviderFactories, ILogger<CampaignTrackerController> logger)
		{
			_splendidError       = splendidError      ;
			_dbProviderFactories = dbProviderFactories;
			_logger              = logger             ;
		}

		/// <summary>
		/// Tracks a campaign link click: logs the event, looks up the redirect URL, and issues
		/// a 302 redirect to the campaign target URL. If no redirect URL is found, returns HTTP 200.
		///
		/// MIGRATED FROM: campaign_trackerv2.aspx.cs Page_Load (lines 42–88)
		///
		/// Flow:
		///   1. Set cache-busting headers so every click is counted (from line 45)
		///   2. Log the access event via SplendidError.SystemMessage (from line 47)
		///   3. Parse identifier and track query parameters as GUIDs (from lines 48–49)
		///   4. If identifier is present: call spCAMPAIGN_LOG_UpdateTracker (from line 56)
		///      Else (web/banner campaign): call spCAMPAIGN_LOG_BannerTracker (from line 61)
		///   5. If track ID is present: query vwCAMPAIGN_TRKRS for the redirect URL (from lines 63–81)
		///   6. If URL found: return 302 redirect to the URL (from line 79)
		///   7. On exception: log via SplendidError.SystemError (from lines 84–87)
		///   8. If no URL found or no track ID: return empty 200 response
		/// </summary>
		[HttpGet]
		[AllowAnonymous]
		public IActionResult Track()
		{
			// 04/11/2008 Paul.  Expire immediately so that all clicks are counted.
			// BEFORE (.NET Framework): Response.ExpiresAbsolute = new DateTime(1980, 1, 1, 0, 0, 0, 0);
			// AFTER  (.NET Core):      Set the equivalent Cache-Control, Pragma, and Expires HTTP headers
			//                          directly on the ASP.NET Core HttpResponse.
			Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
			Response.Headers["Pragma"]        = "no-cache";
			// Expires date formatted as RFC 1123 (required by HTTP spec for Expires header)
			Response.Headers["Expires"]       = new DateTime(1980, 1, 1, 0, 0, 0, 0).ToString("R");

			// BEFORE (.NET Framework): Request["identifier"] and Request["track"]
			//   (RequestContext, combined GET+POST lookup via HttpRequest.Params indexer)
			// AFTER  (.NET Core):      Request.Query["identifier"] and Request.Query["track"]
			//   (IQueryCollection — query string parameters only, matching the original GET-based usage)
			string identifier = Request.Query["identifier"];
			string track      = Request.Query["track"     ];

			// Log campaign tracker access event for diagnostics.
			// Static SplendidError.SystemMessage(string, StackFrame, string) — preserved from line 47.
			SplendidError.SystemMessage("Log", new StackTrace(true).GetFrame(0), "Campaign Tracker v2 " + identifier + ", " + track);

			Guid gID      = Sql.ToGuid(identifier);
			Guid gTrackID = Sql.ToGuid(track      );

			try
			{
				if ( !Sql.IsEmptyGuid(gID) )
				{
					// Identified campaign click (email link with recipient identifier).
					// Log tracker update with output parameters for target ID and type.
					// Preserved from campaign_trackerv2.aspx.cs line 56.
					Guid   gTARGET_ID   = Guid.Empty  ;
					string sTARGET_TYPE = string.Empty;
					SqlProcs.spCAMPAIGN_LOG_UpdateTracker(gID, "link", gTrackID, ref gTARGET_ID, ref sTARGET_TYPE);
				}
				else
				{
					// 09/10/2007 Paul.  Web campaigns will not have an identifier.
					// Anonymous web/banner campaign click — log with the visitor's IP address.
					// BEFORE (.NET Framework): Request.UserHostAddress
					// AFTER  (.NET Core):      HttpContext.Connection.RemoteIpAddress?.ToString()
					//                          Connection.RemoteIpAddress is the direct equivalent in ASP.NET Core.
					string sRemoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
					SqlProcs.spCAMPAIGN_LOG_BannerTracker("link", gTrackID, sRemoteIp);
				}

				if ( !Sql.IsEmptyGuid(gTrackID) )
				{
					// Look up the target redirect URL from the campaign tracker record.
					// BEFORE (.NET Framework): DbProviderFactory dbf = DbProviderFactories.GetFactory(); (static)
					// AFTER  (.NET Core):      _dbProviderFactories.GetFactory() (DI instance)
					DbProviderFactory dbf = _dbProviderFactories.GetFactory();
					using ( IDbConnection con = dbf.CreateConnection() )
					{
						con.Open();
						string sSQL ;
						// Preserve ControlChars.CrLf usage from campaign_trackerv2.aspx.cs lines 70–72.
						sSQL = "select TRACKER_URL     " + ControlChars.CrLf
						     + "  from vwCAMPAIGN_TRKRS" + ControlChars.CrLf
						     + " where ID = @ID        " + ControlChars.CrLf;
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.CommandText = sSQL;
							Sql.AddParameter(cmd, "@ID", gTrackID);
							string sTRACKER_URL = Sql.ToString(cmd.ExecuteScalar());
							if ( !Sql.IsEmptyString(sTRACKER_URL) )
							{
								// SECURITY: Validate redirect URL to prevent open redirect attacks.
								// Only allow HTTP(S) URLs; reject javascript:, data:, or protocol-relative URLs.
								if ( Uri.TryCreate(sTRACKER_URL, UriKind.Absolute, out Uri uriResult)
								  && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps) )
								{
									return Redirect(sTRACKER_URL);
								}
								// If the URL is a relative path (no scheme), allow it as a local redirect.
								else if ( Uri.TryCreate(sTRACKER_URL, UriKind.Relative, out _) && sTRACKER_URL.StartsWith("/") )
								{
									return LocalRedirect(sTRACKER_URL);
								}
								// Otherwise, log and ignore the invalid redirect URL.
								SplendidError.SystemError(new StackTrace(true).GetFrame(0), new Exception("Invalid redirect URL in campaign tracker: " + sTRACKER_URL));
							}
						}
					}
				}
			}
			catch(Exception ex)
			{
				// Log exception via static SplendidError.SystemError — preserved from line 86.
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				_logger.LogError(ex, "CampaignTrackerController: Error processing campaign tracker request");
			}

			// No redirect URL found, tracker ID was empty, or an exception was handled.
			// Return an empty HTTP 200 response (equivalent to an ASPX page rendering empty output).
			return Content(string.Empty);
		}
	}
}
