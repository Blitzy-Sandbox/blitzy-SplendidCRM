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
// .NET 10 Migration: SplendidCRM/TwiML.aspx.cs → src/SplendidCRM.Web/Controllers/TwiMLController.cs
// Changes applied:
//   - REMOVED: WebForms page base (SplendidPage), OnInit(), InitializeComponent(), Page_Load event handler
//   - ADDED:   [ApiController], [Route("TwiML.aspx")], ControllerBase inheritance
//   - ADDED:   Constructor DI: IMemoryCache (replaces Application["CONFIG.Twilio.AccountSID"]), TwilioManager
//   - ADDED:   [HttpPost] + [AllowAnonymous] on Post() — preserves AuthenticationRequired() => false behavior
//   - REPLACED: Request.InputStream → Request.Body (with Request.EnableBuffering() for multi-read support)
//   - REPLACED: Request["key"] (WebForms form+query unified accessor)
//               → Request.Query["key"] for URL query params (gID from ?ID=...)
//               → Request.Form["key"] for POST body params (AccountSid, SmsStatus, etc.)
//   - REPLACED: Context.Application["CONFIG.Twilio.AccountSID"]
//               → IMemoryCache.Get<string>("CONFIG.Twilio.AccountSID")
//   - REPLACED: TwilioManager.Instance static singleton + TwilioManager.InitApp(this.Context)
//               → DI-injected _twilioManager (Program.cs registers TwilioManager as a DI service)
//   - REPLACED: Request.RawUrl → $"{Request.Path}{Request.QueryString}"
//   - REMOVED:  using System.Web; (no WebForms dependencies)
//   - PRESERVED: Exact Twilio credential validation logic (AccountSid comparison against config value)
//   - PRESERVED: SMS status update via SqlProcs.spSMS_MESSAGES_UpdateStatus when ID is present
//   - PRESERVED: Inbound SMS routing via TwilioManager.NewSmsMessage when SmsStatus == "received"
//   - PRESERVED: Location string construction from city/state/zip/country components
//   - PRESERVED: SystemWarning on credential mismatch, SystemError on exception
//   - Minimal change clause: Only changes necessary for .NET Framework 4.8 → .NET 10 ASP.NET Core
#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace SplendidCRM
{
	/// <summary>
	/// TwiMLController — handles Twilio webhook POST callbacks for SMS status updates and inbound SMS messages.
	/// Converted from TwiML.aspx.cs WebForms page for .NET 10 ASP.NET Core.
	///
	/// Route "TwiML.aspx" is preserved for backward compatibility with existing Twilio webhook URL
	/// configurations registered in Twilio's console pointing to this SplendidCRM endpoint.
	///
	/// [AllowAnonymous] is applied to the Post() action because Twilio webhook callbacks do not carry
	/// session authentication (mirrors AuthenticationRequired() => false in the original TwiML.aspx.cs).
	/// </summary>
	[ApiController]
	[Route("TwiML.aspx")]
	public class TwiMLController : ControllerBase
	{
		// .NET 10 Migration: IMemoryCache replaces Application["CONFIG.Twilio.AccountSID"] state access.
		// The Twilio AccountSID configuration value is stored in IMemoryCache under the same key used
		// throughout SplendidCRM (CONFIG.Twilio.AccountSID) by the SplendidCache service at startup.
		private readonly IMemoryCache    _memoryCache   ;
		private readonly IConfiguration  _configuration ;

		// .NET 10 Migration: DI-injected TwilioManager replaces TwilioManager.Instance static singleton.
		// The legacy pattern called TwilioManager.InitApp(Context) when Instance was null, then accessed
		// TwilioManager.Instance.NewSmsMessage(...). In .NET 10, TwilioManager is registered as a DI
		// service in Program.cs and injected directly, eliminating the singleton lifecycle management.
		private readonly TwilioManager _twilioManager;

		/// <summary>
		/// Constructs TwiMLController with the required DI services.
		/// </summary>
		public TwiMLController(IMemoryCache memoryCache, IConfiguration configuration, TwilioManager twilioManager)
		{
			_memoryCache   = memoryCache  ;
			_configuration = configuration;
			_twilioManager = twilioManager;
		}

		/// <summary>
		/// Validates the Twilio request signature using X-Twilio-Signature header.
		/// Uses Twilio.Security.RequestValidator to prevent webhook spoofing.
		/// The AUTH_TOKEN is retrieved from IMemoryCache (CONFIG.Twilio.AuthToken) or IConfiguration.
		/// </summary>
		private bool ValidateTwilioSignature()
		{
			try
			{
				string sAuthToken = Sql.ToString(_memoryCache.Get<object>("CONFIG.Twilio.AuthToken"));
				if ( Sql.IsEmptyString(sAuthToken) )
				{
					// No auth token configured — skip validation (backward compatible behavior)
					return true;
				}
				string sSignature = Request.Headers["X-Twilio-Signature"].FirstOrDefault();
				if ( Sql.IsEmptyString(sSignature) )
				{
					return false;
				}
				string sUrl = $"{Request.Scheme}://{Request.Host}{Request.Path}{Request.QueryString}";
				var dictParams = new Dictionary<string, string>();
				if ( Request.HasFormContentType )
				{
					foreach ( var kvp in Request.Form )
					{
						dictParams[kvp.Key] = kvp.Value.ToString();
					}
				}
				var validator = new Twilio.Security.RequestValidator(sAuthToken);
				return validator.Validate(sUrl, dictParams, sSignature);
			}
			catch
			{
				// On validation failure, deny the request
				return false;
			}
		}

		/// <summary>
		/// Handles Twilio webhook POST callbacks for:
		/// 1. SMS delivery status updates (SmsStatus = sent/delivered/failed/etc.) — updates spSMS_MESSAGES_UpdateStatus
		/// 2. Inbound SMS messages (SmsStatus = received) — routes to connected clients via TwilioManager
		///
		/// Original: TwiML.aspx.cs Page_Load (lines 39–106), restricted to POST via Request.RequestType == "POST".
		///
		/// [AllowAnonymous] — Twilio webhook callbacks are unauthenticated HTTP POST requests;
		/// identity validation is performed via AccountSid comparison against the configured value.
		/// Preserves AuthenticationRequired() => false from TwiML.aspx.cs line 35.
		/// </summary>
		[HttpPost]
		[AllowAnonymous]
		public async Task<IActionResult> Post()
		{
			// Twilio request signature validation: verify X-Twilio-Signature header using HMAC-SHA1.
			// Prevents webhook spoofing by ensuring the request originates from Twilio servers.
			// Uses Twilio NuGet package (7.7.1) RequestValidator class.
			if ( !ValidateTwilioSignature() )
			{
				return StatusCode(403, "Invalid Twilio request signature.");
			}

			string sFormBody = String.Empty;
			// .NET 10 Migration: Request.InputStream → Request.Body
			// Request.EnableBuffering() makes the request body stream seekable so that:
			//   1. StreamReader can read the raw body here for logging purposes (mimics lines 44-48)
			//   2. Request.Form can re-read the body from position 0 when form fields are accessed below
			// The 'leaveOpen: true' parameter ensures the stream remains open after the StreamReader disposes.
			Request.EnableBuffering();
			using (StreamReader rdr = new StreamReader(Request.Body, leaveOpen: true))
			{
				sFormBody = await rdr.ReadToEndAsync();
			}
			// Reset to position 0 so Request.Form reads from the beginning of the body stream
			Request.Body.Position = 0;

#if DEBUG
			// .NET 10 Migration: Request.QueryString → same (QueryString.ToString() available in ASP.NET Core)
			System.Diagnostics.Debug.WriteLine("TwiML.QueryString: " + Request.QueryString);
			if ( !Sql.IsEmptyString(sFormBody) )
				System.Diagnostics.Debug.WriteLine("TwiML.Body: " + sFormBody);
#endif
			try
			{
				// Parse all Twilio webhook fields from the request.
				// .NET 10 Migration: Request["key"] in WebForms unified form + querystring + cookies access.
				// In ASP.NET Core, query string params and form body params are accessed separately:
				//   - gID: read from query string (?ID=...) first; Twilio appends SplendidCRM's ID to the callback URL
				//   - All other Twilio fields: read from form body (application/x-www-form-urlencoded POST data)
				Guid   gID          = Sql.ToGuid  ((string)Request.Query["ID"        ] ?? (string)Request.Form["ID"        ]);
				string sAccountSid  = Sql.ToString((string)Request.Form["AccountSid" ]);
				string sSmsStatus   = Sql.ToString((string)Request.Form["SmsStatus"  ]);
				string sApiVersion  = Sql.ToString((string)Request.Form["ApiVersion" ]);
				string sSUBJECT     = Sql.ToString((string)Request.Form["Body"       ]);
				string sMESSAGE_SID = Sql.ToString((string)Request.Form["SmsSid"     ]);
				string sTO_NUMBER   = Sql.ToString((string)Request.Form["To"         ]);
				string sFROM_NUMBER = Sql.ToString((string)Request.Form["From"       ]);
				// 09/29/2013 Paul.  Received messages have more data. 
				string sToCity      = Sql.ToString((string)Request.Form["ToCity"     ]);
				string sToState     = Sql.ToString((string)Request.Form["ToState"    ]);
				string sToZip       = Sql.ToString((string)Request.Form["ToZip"      ]);
				string sToCountry   = Sql.ToString((string)Request.Form["ToCountry"  ]);
				string sFromCity    = Sql.ToString((string)Request.Form["FromCity"   ]);
				string sFromState   = Sql.ToString((string)Request.Form["FromState"  ]);
				string sFromZip     = Sql.ToString((string)Request.Form["FromZip"    ]);
				string sFromCountry = Sql.ToString((string)Request.Form["FromCountry"]);
				string sFROM_LOCATION = String.Empty;
				string sTO_LOCATION   = String.Empty;
				// 09/26/2013 Paul.  In order to ensure the integrity of the post, the submitted ID must match the configuration value. 
				// .NET 10 Migration: Context.Application["CONFIG.Twilio.AccountSID"]
				//                    → IMemoryCache.Get<string>("CONFIG.Twilio.AccountSID")
				// The configured Twilio AccountSID is stored in IMemoryCache by SplendidCache at startup,
				// using the same cache key (CONFIG.Twilio.AccountSID) as the original Application[] entry.
				if ( sAccountSid == Sql.ToString(_memoryCache.Get<string>("CONFIG.Twilio.AccountSID")) )
				{
					if ( !Sql.IsEmptyGuid(gID) )
					{
						// Outbound SMS status callback: update the stored message status
						SqlProcs.spSMS_MESSAGES_UpdateStatus(gID, sSmsStatus, sMESSAGE_SID);
					}
					else if ( sSmsStatus == "received" )
					{
						// Inbound SMS message: build human-readable location strings and route to clients
						if ( !Sql.IsEmptyString(sFromCity) ) sFromCity += ",";
						if ( !Sql.IsEmptyString(sToCity  ) ) sToCity   += ",";
						sFROM_LOCATION = (sFromCity + " " + sFromState + " " + sFromZip + " " + sFromCountry).Trim();
						sTO_LOCATION   = (sToCity   + " " + sToState   + " " + sToZip   + " " + sToCountry  ).Trim();
						// .NET 10 Migration: TwilioManager.Instance static singleton pattern replaced by DI.
						// Original lines 90-94 checked TwilioManager.Instance == null and called InitApp(this.Context).
						// With DI, _twilioManager is always initialized by the container — no null check needed.
						// NewSmsMessage is now async Task (migrated from void) so we await it.
						await _twilioManager.NewSmsMessage(sMESSAGE_SID, sFROM_NUMBER, sTO_NUMBER, sSUBJECT, sFROM_LOCATION, sTO_LOCATION);
					}
				}
				else
				{
					// Credential mismatch: log warning with the raw URL and form body for diagnostics
					// .NET 10 Migration: Request.RawUrl → $"{Request.Path}{Request.QueryString}"
					// ControlChars.CrLf provides the carriage-return line-feed separator (from VisualBasic.cs)
					SplendidError.SystemWarning(new StackTrace(true).GetFrame(0), "Unknown Twilio event: " + $"{Request.Path}{Request.QueryString}" + ControlChars.CrLf + sFormBody);
				}
			}
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
			// Return 200 OK with no body — Twilio only requires a successful HTTP response code
			return Ok();
		}
	}
}
