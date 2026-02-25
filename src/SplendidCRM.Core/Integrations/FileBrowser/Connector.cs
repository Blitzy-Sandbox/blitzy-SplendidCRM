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
// .NET 10 Migration: SplendidCRM/_code/FileBrowser/Connector.cs -> src/SplendidCRM.Core/Integrations/FileBrowser/Connector.cs
// Changes applied per AAP Section 0.5.1 and 0.7.1 migration rules:
//   - REMOVED:  using System.Web; (replaced with Microsoft.AspNetCore.Http + Microsoft.Extensions.Caching.Memory)
//   - REMOVED:  SplendidPage base class inheritance (WebForms base page does not exist in .NET 10 Core library)
//   - REMOVED:  AuthenticationRequired() override (WebForms virtual method — no longer applicable)
//   - REMOVED:  #region Web Form Designer generated code block (OnInit, InitializeComponent, Load event wiring)
//   - CONVERTED: Page_Load(object, EventArgs) -> public async Task ProcessRequest(HttpContext, IMemoryCache)
//   - ADDED:    DI constructor accepting Security, Utils, DbProviderFactories, FileWorkerUtils instances
//   - REPLACED: Response.ExpiresAbsolute -> context.Response.Headers["Expires"]
//   - REPLACED: Response.CacheControl -> context.Response.Headers["Cache-Control"]
//   - REPLACED: Request["Command"] -> context.Request.Query["Command"]
//   - REPLACED: Request["Type"] -> context.Request.Query["Type"]
//   - REPLACED: Request["CurrentFolder"] -> context.Request.Query["CurrentFolder"]
//   - REPLACED: Security.IsAuthenticated() (static) -> _security.IsAuthenticated() (DI instance)
//   - REPLACED: Utils.MassEmailerSiteURL(Context.Application) -> _utils.MassEmailerSiteURL() (DI instance, no param)
//   - REPLACED: DbProviderFactories.GetFactory() (static) -> _dbProviderFactories.GetFactory() (DI instance)
//   - REPLACED: FileWorkerUtils.LoadImage() (static) -> _fileWorkerUtils.LoadImage() (DI instance)
//   - REPLACED: Response.Write() -> await context.Response.WriteAsync()
//   - REPLACED: Response.ContentEncoding + Response.ContentType -> context.Response.ContentType = "text/xml; charset=utf-8"
//   - PRESERVED: All CKFinder XML response structure, error numbers (1, 103, 202), switch cases
//   - PRESERVED: All business logic, transaction handling, SQL queries, ControlChars.CrLf usage
//   - NOTE: This is a dormant integration stub — it MUST compile on .NET 10 but is NOT expected to execute.
#nullable disable
using System;
using System.Xml;
using System.Data;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM.FileBrowser
{
	/// <summary>
	/// CKEditor/CKFinder file browser connector.
	/// Handles FileUpload, GetFolders, GetFoldersAndFiles, and CreateFolder commands,
	/// returning CKFinder-compliant XML responses.
	///
	/// Migrated from SplendidCRM/_code/FileBrowser/Connector.cs for .NET 10 ASP.NET Core.
	/// Previously inherited from SplendidPage (WebForms); now a standalone class with DI constructor.
	/// ProcessRequest(HttpContext, IMemoryCache) replaces the WebForms Page_Load event handler.
	///
	/// NOTE: This is a dormant integration stub — it MUST compile on .NET 10 but is NOT expected to execute.
	/// </summary>
	public class Connector
	{
		// .NET 10 Migration: DI-injected services replacing static WebForms page lifecycle context.
		// Security replaces static Security.IsAuthenticated() pattern (session-backed).
		// Utils replaces Context.Application usage in MassEmailerSiteURL().
		// DbProviderFactories replaces static DbProviderFactories.GetFactory() call.
		// FileWorkerUtils replaces static FileWorkerUtils.LoadImage() call.
		private readonly Security            _security           ;
		private readonly Utils               _utils              ;
		private readonly DbProviderFactories _dbProviderFactories;
		private readonly FileWorkerUtils     _fileWorkerUtils    ;

		/// <summary>
		/// Initializes a new instance of <see cref="Connector"/> with required DI services.
		/// </summary>
		/// <param name="security">Authentication and ACL service (replaces static Security calls).</param>
		/// <param name="utils">General utility service (replaces Context.Application-based calls).</param>
		/// <param name="dbProviderFactories">Database provider factory registry (replaces static GetFactory).</param>
		/// <param name="fileWorkerUtils">File upload helper (replaces static LoadImage call).</param>
		public Connector(
			Security            security           ,
			Utils               utils              ,
			DbProviderFactories dbProviderFactories,
			FileWorkerUtils     fileWorkerUtils    )
		{
			_security            = security           ;
			_utils               = utils              ;
			_dbProviderFactories = dbProviderFactories;
			_fileWorkerUtils     = fileWorkerUtils    ;
		}

		/// <summary>
		/// Processes a CKFinder connector HTTP request.
		/// Replaces the WebForms Page_Load event handler; accepts ASP.NET Core HttpContext
		/// and IMemoryCache in place of the WebForms page lifecycle context and Application state.
		/// </summary>
		/// <param name="context">
		///   The ASP.NET Core HTTP context for the current request.
		///   Used for reading query parameters (Command, Type, CurrentFolder),
		///   writing cache-control headers, setting content type, and writing the response body.
		/// </param>
		/// <param name="memoryCache">
		///   The in-memory cache replacing ASP.NET HttpApplicationState (Context.Application).
		///   Provided as a parameter for API compatibility; site URL retrieval delegates to
		///   the injected Utils instance which manages its own IMemoryCache reference.
		/// </param>
		public async Task ProcessRequest(HttpContext context, IMemoryCache memoryCache)
		{
			// .NET 10 Migration: Response.ExpiresAbsolute -> context.Response.Headers["Expires"]
			// Sets cache expiry to a past date to prevent client-side caching of connector responses.
			context.Response.Headers["Expires"]       = "Sat, 01 Jan 1980 00:00:00 GMT";
			// .NET 10 Migration: Response.CacheControl -> context.Response.Headers["Cache-Control"]
			context.Response.Headers["Cache-Control"] = "no-cache";

			XmlDocument xml = new XmlDocument();
			xml.PreserveWhitespace = true;
			xml.AppendChild(xml.CreateXmlDeclaration("1.0", "UTF-8", null));
			xml.AppendChild(xml.CreateWhitespace("\n"));
			XmlNode xConnector = xml.CreateElement("Connector");
			xml.AppendChild(xConnector);
			try
			{
				// .NET 10 Migration: Request["key"] -> context.Request.Query["key"]
				// CKFinder passes Command, Type, and CurrentFolder as query string parameters.
				string sCommand       = Sql.ToString(context.Request.Query["Command"      ]);
				string sResourceType  = Sql.ToString(context.Request.Query["Type"         ]);
				string sCurrentFolder = Sql.ToString(context.Request.Query["CurrentFolder"]);

				// .NET 10 Migration: Security.IsAuthenticated() (static) -> _security.IsAuthenticated() (DI instance)
				// Verifies that the current user is authenticated before allowing any connector operations.
				if ( !_security.IsAuthenticated() )
				{
					xConnector.AppendChild(xml.CreateWhitespace("\n\t"));
					XmlUtil.SetSingleNodeAttribute(xml, "Error", "number", "1");
					XmlUtil.SetSingleNodeAttribute(xml, "Error", "text"  , "Authentication is required.");
					xConnector.AppendChild(xml.CreateWhitespace("\n"));
				}
				else if ( Sql.IsEmptyString(sCommand) || Sql.IsEmptyString(sResourceType) || Sql.IsEmptyString(sCurrentFolder) )
				{
					xConnector.AppendChild(xml.CreateWhitespace("\n\t"));
					XmlUtil.SetSingleNodeAttribute(xml, "Error", "number", "1");
					XmlUtil.SetSingleNodeAttribute(xml, "Error", "text"  , "Invalid request.");
					xConnector.AppendChild(xml.CreateWhitespace("\n"));
				}
				else
				{
					// .NET 10 Migration: Utils.MassEmailerSiteURL(Context.Application) -> _utils.MassEmailerSiteURL()
					// The migrated MassEmailerSiteURL() uses the DI-injected IMemoryCache internally;
					// no Application state parameter is required.
					string sSiteURL = _utils.MassEmailerSiteURL();
					string sFileURL = sSiteURL + "Images/EmailImage.aspx?ID=";
					switch ( sCommand )
					{
						case "FileUpload":
						{
							int nErrorNumber = 0;
							string sFileName  = String.Empty;
							string sCustomMsg = String.Empty;
							Guid   gImageID   = Guid.Empty;
							
							// .NET 10 Migration: DbProviderFactories.GetFactory() (static) -> _dbProviderFactories.GetFactory() (DI instance)
							DbProviderFactory dbf = _dbProviderFactories.GetFactory();
							using ( IDbConnection con = dbf.CreateConnection() )
							{
								con.Open();
								// 10/07/2009 Paul.  We need to create our own global transaction ID to support auditing and workflow on SQL Azure, PostgreSQL, Oracle, DB2 and MySQL. 
								using ( IDbTransaction trn = Sql.BeginTransaction(con) )
								{
									try
									{
										// .NET 10 Migration: FileWorkerUtils.LoadImage() (static) -> _fileWorkerUtils.LoadImage() (DI instance)
										_fileWorkerUtils.LoadImage(ref gImageID, ref sFileName, trn);
										if ( Sql.IsEmptyGuid(gImageID) )
											nErrorNumber = 202;
										else
											sFileURL += gImageID.ToString();
										trn.Commit();
									}
									catch
									{
										trn.Rollback();
										throw;
									}
								}
							}
							
							// .NET 10 Migration: Response.Write() -> await context.Response.WriteAsync()
							// The FileUpload command writes an inline JavaScript callback instead of XML.
							await context.Response.WriteAsync("<script type=\"text/javascript\">\n");
							await context.Response.WriteAsync("window.parent.frames['frmUpload'].OnUploadCompleted(" + nErrorNumber.ToString() + ",'" + Sql.EscapeJavaScript(sFileURL) + "','" + Sql.EscapeJavaScript(sFileName) + "','" + Sql.EscapeJavaScript(sCustomMsg) + "');\n");
							await context.Response.WriteAsync("</script>\n");
							return;
						}
						case "GetFolders":
						{
							XmlUtil.SetSingleNodeAttribute(xml, xConnector, "command"     , sCommand     );
							XmlUtil.SetSingleNodeAttribute(xml, xConnector, "resourceType", sResourceType);
							xConnector.AppendChild(xml.CreateWhitespace("\n\t"));
							
							XmlUtil.SetSingleNodeAttribute(xml, "CurrentFolder", "path"        , sCurrentFolder);
							XmlUtil.SetSingleNodeAttribute(xml, "CurrentFolder", "url"         , sFileURL      );
							xConnector.AppendChild(xml.CreateWhitespace("\n\t"));
							
							XmlUtil.SetSingleNode         (xml, "Folders"      , "");
							xConnector.AppendChild(xml.CreateWhitespace("\n"));
							break;
						}
						case "GetFoldersAndFiles":
						{
							XmlUtil.SetSingleNodeAttribute(xml, xConnector, "command"     , sCommand     );
							XmlUtil.SetSingleNodeAttribute(xml, xConnector, "resourceType", sResourceType);
							xConnector.AppendChild(xml.CreateWhitespace("\n\t"));
							
							XmlUtil.SetSingleNodeAttribute(xml, "CurrentFolder", "path"        , sCurrentFolder);
							XmlUtil.SetSingleNodeAttribute(xml, "CurrentFolder", "url"         , sFileURL      );
							xConnector.AppendChild(xml.CreateWhitespace("\n\t"));
							
							XmlUtil.SetSingleNode         (xml, "Folders"      , "");
							xConnector.AppendChild(xml.CreateWhitespace("\n\t"));
							
							XmlUtil.SetSingleNode         (xml, "Files"        , "");
							xConnector.AppendChild(xml.CreateWhitespace("\n"));

							// .NET 10 Migration: DbProviderFactories.GetFactory() (static) -> _dbProviderFactories.GetFactory() (DI instance)
							DbProviderFactory dbf = _dbProviderFactories.GetFactory();
							using ( IDbConnection con = dbf.CreateConnection() )
							{
								con.Open();
								string sSQL ;
								// .NET 10 Migration: ControlChars.CrLf constant preserved (SplendidCRM.ControlChars from VisualBasic.cs)
								sSQL = "select *             " + ControlChars.CrLf
								     + "  from vwEMAIL_IMAGES" + ControlChars.CrLf
								     + " order by FILENAME   " + ControlChars.CrLf;
								using ( IDbCommand cmd = con.CreateCommand() )
								{
									cmd.CommandText = sSQL;
									using ( IDataReader rdr = cmd.ExecuteReader() )
									{
										XmlNode xFiles = xConnector.SelectSingleNode("Files");
										while ( rdr.Read() )
										{
											Guid   gID        = Sql.ToGuid  (rdr["ID"       ]);
											string sFILENAME  = Sql.ToString(rdr["FILENAME" ]);
											long   lFILE_SIZE = Sql.ToLong  (rdr["FILE_SIZE"]);
											XmlNode xFile  = xml.CreateElement("File" );
											XmlUtil.SetSingleNodeAttribute(xml, xFile, "name", sFILENAME);
											XmlUtil.SetSingleNodeAttribute(xml, xFile, "size", lFILE_SIZE.ToString());
											XmlUtil.SetSingleNodeAttribute(xml, xFile, "url", sFileURL + gID.ToString());
											xFiles.AppendChild(xml.CreateWhitespace("\n\t\t"));
											xFiles.AppendChild(xFile);
										}
										xFiles.AppendChild(xml.CreateWhitespace("\n\t"));
									}
								}
							}
							break;
						}
						case "CreateFolder":
						{
							// CreateFolder is intentionally unsupported — returns error 103.
							xConnector.AppendChild(xml.CreateWhitespace("\n\t"));
							XmlUtil.SetSingleNodeAttribute(xml, "Error", "number", "103");
							XmlUtil.SetSingleNodeAttribute(xml, "Error", "text"  , "Folders cannot be created.");
							xConnector.AppendChild(xml.CreateWhitespace("\n"));
							break;
						}
					}
				}
			}
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				xConnector.RemoveAll();
				xConnector.AppendChild(xml.CreateWhitespace("\n\t"));
				XmlUtil.SetSingleNodeAttribute(xml, "Error", "number", "1");
				XmlUtil.SetSingleNodeAttribute(xml, "Error", "text"  , ex.Message);
				xConnector.AppendChild(xml.CreateWhitespace("\n"));
			}
			// .NET 10 Migration: Response.ContentEncoding + Response.ContentType -> context.Response.ContentType
			// Setting ContentType to "text/xml; charset=utf-8" replaces both:
			//   Response.ContentEncoding = System.Text.UTF8Encoding.UTF8;
			//   Response.ContentType     = "text/xml";
			context.Response.ContentType = "text/xml; charset=utf-8";
			// .NET 10 Migration: Response.Write(xml.OuterXml) -> await context.Response.WriteAsync(xml.OuterXml)
			await context.Response.WriteAsync(xml.OuterXml);
		}
	}
}
