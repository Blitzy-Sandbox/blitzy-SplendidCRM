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
// .NET 10 Migration: SplendidCRM/image.aspx.cs → src/SplendidCRM.Web/Controllers/ImageController.cs
// Changes applied:
//   - REMOVED: WebForms base class SplendidPage → ControllerBase (ASP.NET Core)
//   - REMOVED: using System.Web.UI.WebControls; using System.Web.UI.HtmlControls; (WebForms namespaces)
//   - REMOVED: AuthenticationRequired() override → replaced by [AllowAnonymous] attribute on Track() action
//   - REMOVED: OnInit(EventArgs) / InitializeComponent() WebForms lifecycle methods → not needed in MVC
//   - REPLACED: Response.ExpiresAbsolute = new DateTime(1980, 1, 1) → Response.Headers["Cache-Control"] +
//               Response.Headers["Pragma"] + Response.Headers["Expires"] (ASP.NET Core HTTP response headers)
//   - REPLACED: Request["identifier"] → Request.Query["identifier"] (ASP.NET Core IQueryCollection)
//   - REPLACED: Response.ContentType = "image/gif"; Response.WriteFile(Request.MapPath("~/include/images/blank.gif"))
//               → return File(gifBytes, "image/gif") (IActionResult)
//   - REPLACED: Request.MapPath("~/include/images/blank.gif") → IWebHostEnvironment.WebRootPath +
//               Path.Combine for physical file resolution; falls back to embedded 1×1 transparent GIF
//   - PRESERVED: Namespace SplendidCRM (matching original source namespace)
//   - PRESERVED: All business logic: cache-busting headers, identifier parsing, stored procedure call
//   - PRESERVED: try/catch with SplendidError.SystemError on exception (lines 58–61)
//   - PRESERVED: Identical stored procedure signature: spCAMPAIGN_LOG_UpdateTracker(gID, "viewed", Guid.Empty, ...)
//   - PRESERVED: Cache-busting headers (every view must be counted — from line 45)
//   - PRESERVED: SplendidError.SystemMessage diagnostic log for each tracking pixel request (line 47)
//   - ADDED: [ApiController], [Route("image.aspx")], [HttpGet], [AllowAnonymous] attributes
//   - ADDED: Constructor injection of SplendidError (logging) and IWebHostEnvironment (file path resolution)
//   - ADDED: Embedded 1×1 transparent GIF byte array as fallback when blank.gif is absent from disk
#nullable disable
using System;
using System.Diagnostics;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace SplendidCRM
{
	/// <summary>
	/// Campaign tracking pixel controller.
	/// Converted from SplendidCRM/image.aspx.cs for .NET 10 ASP.NET Core.
	///
	/// Serves a 1×1 transparent GIF image used as a tracking pixel in campaign emails.
	/// When a recipient opens the email, the browser requests this image, recording the open event
	/// via the spCAMPAIGN_LOG_UpdateTracker stored procedure.
	///
	/// This endpoint is publicly accessible without authentication, matching the original
	/// AuthenticationRequired() { return false; } WebForms override at image.aspx.cs line 37–40.
	///
	/// Route: GET /image.aspx
	///   Query params:
	///     identifier — (optional) GUID identifying the campaign log tracker record for this recipient
	/// </summary>
	[ApiController]
	[Route("image.aspx")]
	public class ImageController : ControllerBase
	{
		// =====================================================================================
		// DI fields
		// SplendidError: logging service (static SystemMessage/SystemError methods used for tracing)
		// IWebHostEnvironment: resolves WebRootPath for blank.gif, replacing Request.MapPath("~/...")
		// =====================================================================================
		private readonly SplendidError       _splendidError;
		private readonly IWebHostEnvironment _env          ;

		/// <summary>
		/// Embedded 1×1 transparent GIF tracking pixel byte array.
		/// Used as the primary response payload when blank.gif is not found on disk.
		///
		/// GIF89a format:
		///   • Header:              GIF89a (47 49 46 38 39 61)
		///   • Logical screen:      1×1, 2-color global color table (80 00 00)
		///   • Global color table:  color 0 = black (00 00 00), color 1 = white (FF FF FF)
		///   • Graphic Ctrl Ext:    transparent color flag set (01), transparent index = 0
		///   • Image descriptor:    1×1 at (0,0), no local color table
		///   • Image data:          LZW min code size 2; encoded stream = Clear(4)+Index0(0)+EOI(5)
		///                          resulting in bytes 0x44 0x01 (LSB-first GIF bit packing)
		///   • Trailer:             0x3B
		/// </summary>
		private static readonly byte[] TransparentGif = new byte[]
		{
			0x47, 0x49, 0x46, 0x38, 0x39, 0x61,  // GIF89a header
			0x01, 0x00,                            // Logical screen width:  1 pixel (little-endian)
			0x01, 0x00,                            // Logical screen height: 1 pixel (little-endian)
			0x80, 0x00, 0x00,                      // Packed fields: GCT flag=1, color depth=0(2 colors), BG=0, aspect=0
			0x00, 0x00, 0x00,                      // Color 0: black — designated as the transparent color
			0xFF, 0xFF, 0xFF,                      // Color 1: white
			0x21, 0xF9, 0x04,                      // Graphic Control Extension introducer + block size
			0x01,                                  // Flags: bit0=1 → transparent color flag set
			0x00, 0x00,                            // Delay time: 0 centiseconds (still image)
			0x00,                                  // Transparent color index: 0 (color 0 / black is transparent)
			0x00,                                  // GCE block terminator
			0x2C,                                  // Image Descriptor introducer
			0x00, 0x00, 0x00, 0x00,               // Image left=0, top=0
			0x01, 0x00, 0x01, 0x00,               // Image width=1, height=1
			0x00,                                  // Local color table: none; not interlaced
			0x02,                                  // Minimum LZW code size: 2
			0x02,                                  // Sub-block size: 2 bytes follow
			0x44, 0x01,                            // LZW-encoded pixel: color index 0 (transparent)
			                                       // Decoded: Clear(4) + Index0(0) + EOI(5) in LSB-first 3-bit codes
			0x00,                                  // Image data sub-block terminator
			0x3B                                   // GIF file trailer
		};

		/// <summary>
		/// Constructs an ImageController with required services via dependency injection.
		/// </summary>
		/// <param name="splendidError">
		///   Error logging service. The original image.aspx.cs calls static SplendidError methods;
		///   the service is injected here following DI best practices and to ensure the DI container
		///   manages SplendidError's lifecycle alongside the controller.
		/// </param>
		/// <param name="env">
		///   Web host environment used to resolve the physical file path of blank.gif via WebRootPath,
		///   replacing the legacy Request.MapPath("~/include/images/blank.gif") call at line 63.
		/// </param>
		public ImageController(SplendidError splendidError, IWebHostEnvironment env)
		{
			_splendidError = splendidError;
			_env           = env          ;
		}

		/// <summary>
		/// Serves the 1×1 tracking pixel GIF and records the campaign email open event.
		///
		/// MIGRATED FROM: image.aspx.cs Page_Load (lines 42–64)
		///
		/// Flow:
		///   1. Set cache-busting headers so every open is individually counted (from line 45)
		///   2. Log the tracking pixel request via SplendidError.SystemMessage (from line 47)
		///   3. Parse identifier query parameter as GUID via Sql.ToGuid (from line 48)
		///   4. If valid GUID (Sql.IsEmptyGuid returns false): call spCAMPAIGN_LOG_UpdateTracker
		///      with "viewed" activity type (from lines 51–56)
		///   5. Wrap database call in try/catch; log exceptions via SplendidError.SystemError (lines 58–61)
		///   6. Resolve blank.gif from WebRootPath/include/images/blank.gif if present on disk;
		///      otherwise return the embedded 1×1 transparent GIF byte array (from lines 62–63)
		///   7. Return response with content-type image/gif (from line 62)
		///
		/// Access: anonymous — no authentication required, matching original [AllowAnonymous] behavior.
		/// </summary>
		[HttpGet]
		[AllowAnonymous]
		public IActionResult Track()
		{
			// 04/11/2008 Paul.  Expire immediately so that all views are counted.
			// BEFORE (.NET Framework): Response.ExpiresAbsolute = new DateTime(1980, 1, 1, 0, 0, 0, 0);
			// AFTER  (.NET Core):      Set equivalent Cache-Control, Pragma, and Expires HTTP headers
			//                          directly on the ASP.NET Core HttpResponse.
			Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
			Response.Headers["Pragma"]        = "no-cache";
			// Expires date formatted as RFC 1123 (required format for the Expires HTTP header)
			Response.Headers["Expires"]       = new DateTime(1980, 1, 1, 0, 0, 0, 0).ToString("R");

			// BEFORE (.NET Framework): Request["identifier"]
			//   (combined GET+POST lookup via HttpRequest.Params indexer)
			// AFTER  (.NET Core):      Request.Query["identifier"]
			//   (IQueryCollection — query string only, which is correct for email tracking pixels)
			string identifier = Request.Query["identifier"];

			// Log the tracking pixel request for diagnostics.
			// Static SplendidError.SystemMessage(string, StackFrame, string) — preserved from image.aspx.cs line 47.
			SplendidError.SystemMessage("Log", new StackTrace(true).GetFrame(0), "Tracker image request for " + identifier);

			// Parse the identifier query parameter into a Guid.
			// Sql.ToGuid handles null/empty/invalid strings by returning Guid.Empty.
			// Preserved from image.aspx.cs line 48.
			Guid gID = Sql.ToGuid(identifier);

			try
			{
				if ( !Sql.IsEmptyGuid(gID) )
				{
					// Valid campaign identifier — record the email open event via stored procedure.
					// Preserved from image.aspx.cs lines 51–56.
					Guid   gTARGET_ID   = Guid.Empty  ;
					string sTARGET_TYPE = string.Empty;
					SqlProcs.spCAMPAIGN_LOG_UpdateTracker(gID, "viewed", Guid.Empty, ref gTARGET_ID, ref sTARGET_TYPE);
				}
			}
			catch(Exception ex)
			{
				// Log exception via static SplendidError.SystemError — preserved from image.aspx.cs lines 58–61.
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}

			// BEFORE (.NET Framework):
			//   Response.ContentType = "image/gif";
			//   Response.WriteFile(Request.MapPath("~/include/images/blank.gif"));
			//
			// AFTER (.NET Core):
			//   Attempt to serve blank.gif from the web root for deployments that provide the file.
			//   IWebHostEnvironment.WebRootPath replaces Request.MapPath("~/...").
			//   Fall back to the embedded 1×1 transparent GIF byte array when the file is absent.
			if ( _env != null && !string.IsNullOrEmpty(_env.WebRootPath) )
			{
				string gifPath = Path.Combine(_env.WebRootPath, "include", "images", "blank.gif");
				if ( System.IO.File.Exists(gifPath) )
				{
					return File(System.IO.File.ReadAllBytes(gifPath), "image/gif");
				}
			}

			// Fallback: return the embedded 1×1 transparent GIF byte array.
			// This is the primary code path when blank.gif is not deployed to the web root.
			return File(TransparentGif, "image/gif");
		}
	}
}
