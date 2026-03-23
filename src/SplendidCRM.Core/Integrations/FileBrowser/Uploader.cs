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
// .NET 10 Migration: SplendidCRM/_code/FileBrowser/Uploader.cs → src/SplendidCRM.Core/Integrations/FileBrowser/Uploader.cs
// Changes applied per AAP Section 0.5.1 and 0.7.1 migration rules:
//   - REMOVED:  using System.Web; (replaced with Microsoft.AspNetCore.Http + Microsoft.Extensions.Caching.Memory)
//   - REMOVED:  SplendidPage base class inheritance — WebForms base page not available in .NET 10 class library
//   - REMOVED:  override protected bool AuthenticationRequired() — WebForms virtual method, no longer applicable
//   - REMOVED:  #region Web Form Designer generated code (OnInit, InitializeComponent, Load event wiring)
//   - CONVERTED: Page_Load(object, EventArgs) → public async Task ProcessRequest(HttpContext, IMemoryCache)
//   - CONVERTED: Static HTTP context / Application state access → constructor-injected DI services
//   - REPLACED:  Context.Application passed to Utils.MassEmailerSiteURL() → instance method _utils.MassEmailerSiteURL()
//   - REPLACED:  Response.ExpiresAbsolute → context.Response.Headers["Expires"] (no-cache directive)
//   - REPLACED:  Response.CacheControl → context.Response.Headers["Cache-Control"]
//   - REPLACED:  Request["CKEditorFuncNum"] → context.Request.Query["CKEditorFuncNum"]
//   - REPLACED:  Response.Write() → await context.Response.WriteAsync()
//   - PRESERVED: Authentication check via Security.IsAuthenticated() (line 48 in source)
//   - PRESERVED: Database transaction wrapping (Sql.BeginTransaction / Commit / Rollback pattern)
//   - PRESERVED: FileWorkerUtils.LoadImage() call for CKEditor image persistence
//   - PRESERVED: CKEditor JavaScript callback pattern (window.parent.CKEDITOR.tools.callFunction)
//   - PRESERVED: Exact error messages ("Authentication is required.", "Failed to upload message.")
//   - PRESERVED: SplendidError.SystemError() exception logging with StackTrace
//   - PRESERVED: Sql.EscapeJavaScript() calls for safe script output
//   - PRESERVED: Original developer comments (Paul)
//   - NOTE: This is a dormant integration stub — compiles on .NET 10 but not activated at runtime.
#nullable disable
using System;
using System.Xml;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM.FileBrowser
{
	/// <summary>
	/// CKEditor image uploader endpoint.
	/// Handles authenticated image upload requests from CKEditor, persists the image binary data
	/// and metadata into the EMAIL_IMAGES table via FileWorkerUtils.LoadImage(), and returns a
	/// CKEditor-compatible JavaScript callback response containing the uploaded image URL or error.
	///
	/// Migrated from SplendidCRM/_code/FileBrowser/Uploader.cs for .NET 10 ASP.NET Core.
	/// The WebForms Page_Load event handler is replaced by ProcessRequest(HttpContext, IMemoryCache).
	/// All static Application[] state access is replaced by injected IMemoryCache via constructor DI.
	/// </summary>
	public class Uploader
	{
		// .NET 10 Migration: DI fields replacing static HTTP context and Application state access.
		// Security provides IsAuthenticated(); DbProviderFactories creates the DB connection;
		// FileWorkerUtils handles the actual binary upload; Utils constructs the image base URL.
		private readonly Security            _security           ;
		private readonly DbProviderFactories _dbProviderFactories;
		private readonly FileWorkerUtils     _fileWorkerUtils    ;
		private readonly Utils               _utils              ;

		/// <summary>
		/// Initializes a new instance of <see cref="Uploader"/> with the required DI services.
		/// </summary>
		/// <param name="security">Authentication and ACL service (replaces static Security.IsAuthenticated()).</param>
		/// <param name="dbProviderFactories">Database provider factory registry (replaces static DbProviderFactories.GetFactory(Application)).</param>
		/// <param name="fileWorkerUtils">File upload helper (replaces static FileWorkerUtils.LoadImage()).</param>
		/// <param name="utils">General utility class (replaces static Utils.MassEmailerSiteURL(Context.Application)).</param>
		public Uploader(Security security, DbProviderFactories dbProviderFactories, FileWorkerUtils fileWorkerUtils, Utils utils)
		{
			_security            = security           ;
			_dbProviderFactories = dbProviderFactories;
			_fileWorkerUtils     = fileWorkerUtils    ;
			_utils               = utils              ;
		}

		/// <summary>
		/// Processes a CKEditor image upload request and writes the JavaScript callback response.
		/// Authenticates the request, persists the uploaded image, constructs the image URL,
		/// and outputs window.parent.CKEDITOR.tools.callFunction(...) with the result.
		///
		/// BEFORE (.NET Framework 4.8): Page_Load(object sender, System.EventArgs e) on SplendidPage
		/// AFTER  (.NET 10 ASP.NET Core): ProcessRequest(HttpContext context, IMemoryCache memoryCache)
		/// </summary>
		/// <param name="context">
		/// ASP.NET Core HTTP context providing request (CKEditorFuncNum, uploaded file)
		/// and response (Expires header, Cache-Control header, JavaScript callback output).
		/// Replaces the implicit WebForms Response/Request/Context properties.
		/// </param>
		/// <param name="memoryCache">
		/// In-memory cache passed to DbProviderFactories.GetFactory() for resolving the DB
		/// connection string. Replaces Context.Application passed to Utils.MassEmailerSiteURL()
		/// in the original source (line 73).
		/// </param>
		public async Task ProcessRequest(HttpContext context, IMemoryCache memoryCache)
		{
			// .NET 10 Migration: Response.ExpiresAbsolute = new DateTime(1980,1,1,0,0,0,0) →
			//   context.Response.Headers["Expires"] with equivalent RFC 7231 HTTP date string.
			context.Response.Headers["Expires"]       = "Sat, 01 Jan 1980 00:00:00 GMT";
			// .NET 10 Migration: Response.CacheControl = "no-cache" → Cache-Control response header.
			context.Response.Headers["Cache-Control"] = "no-cache";

			string sCustomMsg = String.Empty;
			string sFileURL   = String.Empty;
			try
			{
				// .NET 10 Migration: Security.IsAuthenticated() is now an instance method on injected _security.
				// BEFORE: if ( !Security.IsAuthenticated() )  [static call via WebForms base page context]
				// AFTER:  if ( !_security.IsAuthenticated() )  [instance call via constructor DI]
				if ( !_security.IsAuthenticated() )
				{
					sCustomMsg = "Authentication is required.";
				}
				else
				{
					Guid gImageID = Guid.Empty;

					// .NET 10 Migration: DbProviderFactories.GetFactory(Application) → _dbProviderFactories.GetFactory(memoryCache)
					// IMemoryCache replaces HttpApplicationState for provider/connection-string caching.
					DbProviderFactory dbf = _dbProviderFactories.GetFactory(memoryCache);
					using ( IDbConnection con = dbf.CreateConnection() )
					{
						con.Open();
						// 10/07/2009 Paul.  We need to create our own global transaction ID to support auditing and workflow on SQL Azure, PostgreSQL, Oracle, DB2 and MySQL. 
						using ( IDbTransaction trn = Sql.BeginTransaction(con) )
						{
							try
							{
								string sFileName = String.Empty;
								// .NET 10 Migration: FileWorkerUtils.LoadImage() is now an instance method on injected _fileWorkerUtils.
								// BEFORE: FileWorkerUtils.LoadImage(ref gImageID, ref sFileName, trn)  [static call]
								// AFTER:  _fileWorkerUtils.LoadImage(ref gImageID, ref sFileName, trn)  [instance call via DI]
								_fileWorkerUtils.LoadImage(ref gImageID, ref sFileName, trn);
								if ( Sql.IsEmptyGuid(gImageID) )
								{
									sCustomMsg = "Failed to upload message.";
								}
								else
								{
									// .NET 10 Migration: Utils.MassEmailerSiteURL(Context.Application) →
									//   _utils.MassEmailerSiteURL() — instance method using injected IMemoryCache internally.
									// BEFORE (line 73): Utils.MassEmailerSiteURL(Context.Application) + "Images/EmailImage.aspx?ID=" + gImageID.ToString()
									// AFTER:            _utils.MassEmailerSiteURL() + "Images/EmailImage.aspx?ID=" + gImageID.ToString()
									sFileURL = _utils.MassEmailerSiteURL() + "Images/EmailImage.aspx?ID=" + gImageID.ToString();
								}
								trn.Commit();
							}
							catch
							{
								trn.Rollback();
								throw;
							}
						}
					}
				}
			}
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				sCustomMsg = ex.Message;
			}
			// 04/26/2012 Paul.  CKEditor 3.6.2 has a new technique for returning the uploaded image. 
			// http://stackoverflow.com/questions/9720734/image-upload-on-ckeditor-asp-net-4-response-to-upload-iframe-error
			// .NET 10 Migration: Response.Write() → await context.Response.WriteAsync()
			// .NET 10 Migration: Request["CKEditorFuncNum"] → context.Request.Query["CKEditorFuncNum"]
			await context.Response.WriteAsync("<script type=\"text/javascript\">\n");
			await context.Response.WriteAsync("window.parent.CKEDITOR.tools.callFunction(" + context.Request.Query["CKEditorFuncNum"] + ",'" + Sql.EscapeJavaScript(sFileURL) + "','" + Sql.EscapeJavaScript(sCustomMsg) + "');\n");
			await context.Response.WriteAsync("</script>\n");
		}
	}
}
