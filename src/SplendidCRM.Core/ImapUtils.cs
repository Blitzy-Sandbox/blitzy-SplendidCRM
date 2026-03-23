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
// .NET 10 Migration: SplendidCRM/_code/ImapUtils.cs → src/SplendidCRM.Core/ImapUtils.cs
// Changes applied:
//   - REMOVED: using System.Web; using System.Web.SessionState; (System.Web not available in .NET 10)
//   - ADDED:   using Microsoft.AspNetCore.Http; (HttpContext, ISession)
//   - ADDED:   using Microsoft.Extensions.Caching.Memory; (IMemoryCache replaces Application[])
//   - REPLACED: Context.Application["CONFIG.default_language"]
//               → _memoryCache.Get<object>("CONFIG.default_language")
//   - REPLACED: Context.Application["CONFIG.email_xss"]
//               → _memoryCache.Get<object>("CONFIG.email_xss")
//   - REPLACED: Context.Session["USER_SETTINGS/CULTURE"]
//               → context.Session.GetString("USER_SETTINGS/CULTURE")
//   - REPLACED: L10N.Term(Context.Application, sCULTURE, ...)
//               → L10N.Term(_memoryCache, sCULTURE, ...)
//   - REPLACED: MimePart.ContentObject.DecodeTo(mem) → MimePart.Content.DecodeTo(mem)
//               (MimeKit 4.x API change: ContentObject is deprecated, replaced by Content)
//   - ADDED:    Constructor with IMemoryCache DI parameter (replaces HttpApplicationState)
//   - CONVERTED: All static methods to instance methods (class is now DI-injectable)
//   - PRESERVED: namespace SplendidCRM, all public method signatures, all MailKit IMAP operations
//   - PRESERVED: all business logic, IMAP connect/authenticate/folder patterns
//   - File MUST compile under net10.0, minimal change clause
#nullable disable
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using MimeKit;
using MailKit;
using MailKit.Net.Imap;

namespace SplendidCRM
{
	/// <summary>
	/// IMAP email utility class providing mailbox access, folder management, message retrieval,
	/// attachment extraction, and email import operations via the MailKit IMAP client library.
	///
	/// Migrated from SplendidCRM/_code/ImapUtils.cs for .NET 10 ASP.NET Core.
	///
	/// MIGRATION PATTERN:
	///   BEFORE (.NET Framework 4.8):
	///     All methods were static. HttpContext.Current.Application[] accessed configuration cache.
	///     HttpContext.Current.Session[] accessed per-user settings.
	///   AFTER (.NET 10 ASP.NET Core):
	///     Class is now an injectable service with IMemoryCache constructor injection.
	///     HttpContext is still passed as a parameter to all public methods (consistent with
	///     the rest of the migrated codebase pattern).
	///     Application["key"] → _memoryCache.Get&lt;object&gt;("key")
	///     Session["key"]     → context.Session.GetString("key")
	/// </summary>
	public class ImapUtils
	{
		// =====================================================================================
		// .NET 10 Migration: IMemoryCache replaces HttpApplicationState (Application[])
		// BEFORE: HttpContext.Current.Application["CONFIG.default_language"]
		// AFTER:  _memoryCache.Get<object>("CONFIG.default_language")
		// =====================================================================================

		/// <summary>
		/// IMemoryCache used to replace HttpApplicationState (Application[]) reads for
		/// CONFIG.default_language and CONFIG.email_xss configuration entries.
		/// Injected via the DI constructor; initialized at application startup by SplendidInit.
		/// </summary>
		private readonly IMemoryCache _memoryCache;

		// =====================================================================================
		// Constructor — DI injection entry point
		// =====================================================================================

		/// <summary>
		/// Creates an ImapUtils instance with the required IMemoryCache dependency.
		/// Register this class as a scoped or transient service in Program.cs:
		///   builder.Services.AddScoped&lt;ImapUtils&gt;();
		/// </summary>
		/// <param name="memoryCache">
		/// IMemoryCache replacing HttpApplicationState for config value lookups
		/// (CONFIG.default_language for culture resolution, CONFIG.email_xss for XSS filter level).
		/// </param>
		public ImapUtils(IMemoryCache memoryCache)
		{
			_memoryCache = memoryCache;
		}

		// =====================================================================================
		// Validate — test IMAP connection credentials and folder access
		// BEFORE: public static bool Validate(HttpContext Context, ...)
		// AFTER:  public bool Validate(HttpContext context, ...)
		// =====================================================================================

		/// <summary>
		/// Validates IMAP server connection credentials by connecting, authenticating, and
		/// opening the specified folder. Appends a localized success or error message to sbErrors.
		/// </summary>
		/// <param name="context">
		/// HTTP context providing Session access for USER_SETTINGS/CULTURE.
		/// MIGRATION: No longer used to access Application[] — _memoryCache is used instead.
		/// </param>
		/// <param name="sSERVER_URL">IMAP server hostname or IP address.</param>
		/// <param name="nPORT">IMAP server port (typically 143 plain, 993 SSL).</param>
		/// <param name="bMAILBOX_SSL">True to use SslOnConnect; false for Auto (STARTTLS or plain).</param>
		/// <param name="sEMAIL_USER">IMAP username / email address for authentication.</param>
		/// <param name="sEMAIL_PASSWORD">IMAP password for authentication.</param>
		/// <param name="sFOLDER_ID">Folder ID to open for count verification; defaults to INBOX.</param>
		/// <param name="sbErrors">Ref StringBuilder accumulating success or error messages for the caller.</param>
		/// <returns>True if connection, authentication, and folder open all succeeded; false otherwise.</returns>
		public bool Validate(HttpContext context, string sSERVER_URL, int nPORT, bool bMAILBOX_SSL, string sEMAIL_USER, string sEMAIL_PASSWORD, string sFOLDER_ID, StringBuilder sbErrors)
		{
			bool bValid = false;
			try
			{
				if ( Sql.IsEmptyString(sFOLDER_ID) )
					sFOLDER_ID = "INBOX";
				using ( ImapClient imap = new ImapClient() )
				{
					imap.Connect(sSERVER_URL, nPORT, (bMAILBOX_SSL ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.Auto));
					imap.AuthenticationMechanisms.Remove("XOAUTH2");
					// 01/22/2017 Paul.  There is a bug with NTLM. 
					// http://stackoverflow.com/questions/39573233/mailkit-authenticate-to-imap-fails
					imap.AuthenticationMechanisms.Remove("NTLM");
					imap.Authenticate(sEMAIL_USER, sEMAIL_PASSWORD);

					IMailFolder mailbox = imap.GetFolder(sFOLDER_ID);
					if ( mailbox != null && mailbox.Exists )
					{
						// 12/13/2017 Paul.  The mailbox should be opened in order to get the count. 
						mailbox.Open(FolderAccess.ReadOnly);
						// 08/09/2018 Paul.  Allow translation of connection success.
						// BEFORE: string sCULTURE = Sql.ToString(Context.Application["CONFIG.default_language"]);
						// AFTER:  string sCULTURE = Sql.ToString(_memoryCache.Get<object>("CONFIG.default_language"));
						string sCULTURE = Sql.ToString(_memoryCache.Get<object>("CONFIG.default_language"));
						// BEFORE: if ( Context.Session != null ) sCULTURE = Sql.ToString(Context.Session["USER_SETTINGS/CULTURE"]);
						// AFTER:  if ( context.Session != null ) sCULTURE = Sql.ToString(context.Session.GetString("USER_SETTINGS/CULTURE"));
						if ( context.Session != null )
							sCULTURE = Sql.ToString(context.Session.GetString("USER_SETTINGS/CULTURE"));
						// BEFORE: sbErrors.AppendLine(String.Format(L10N.Term(Context.Application, sCULTURE, "Users.LBL_CONNECTION_SUCCESSFUL"), ...));
						// AFTER:  sbErrors.AppendLine(String.Format(L10N.Term(_memoryCache, sCULTURE, "Users.LBL_CONNECTION_SUCCESSFUL"), ...));
						sbErrors.AppendLine(String.Format(L10N.Term(_memoryCache, sCULTURE, "Users.LBL_CONNECTION_SUCCESSFUL"), mailbox.Count.ToString(), "Inbox"));
						sbErrors.AppendLine("<br />");
						bValid = true;
					}
					imap.Disconnect(true);
				}
			}
			catch(Exception ex)
			{
				sbErrors.AppendLine(ex.Message);
			}
			return bValid;
		}

		// =====================================================================================
		// UpdateFolderTreeNodeCounts (private) — update counts on existing XML folder nodes
		// BEFORE: private static void UpdateFolderTreeNodeCounts(ImapClient imap, XmlNode xFolder)
		// AFTER:  private void UpdateFolderTreeNodeCounts(ImapClient imap, XmlNode xFolder)
		// =====================================================================================

		/// <summary>
		/// Updates TotalCount, UnreadCount, and DisplayName XML attributes on each child folder node
		/// in the folder tree by querying the live IMAP session for message counts.
		/// </summary>
		/// <param name="imap">Active authenticated ImapClient.</param>
		/// <param name="xFolder">Parent XML node whose children represent IMAP sub-folders.</param>
		private void UpdateFolderTreeNodeCounts(ImapClient imap, XmlNode xFolder)
		{
			foreach ( XmlNode xChild in xFolder.ChildNodes )
			{
				int nTotalCount  = 0;
				int nUnreadCount = 0;
				string sMailbox = XmlUtil.GetNamedItem(xChild, "Id"  );
				string sName    = XmlUtil.GetNamedItem(xChild, "Name");
				IMailFolder mailbox = imap.GetFolder(sMailbox);
				// 07/17/2010 Paul.  The [Gmail] folder will not return a mailbox. 
				if ( mailbox != null )
				{
					nTotalCount  = mailbox.Count ;
					nUnreadCount = mailbox.Unread;
				}

				XmlUtil.SetSingleNodeAttribute(xFolder.OwnerDocument, xChild, "TotalCount" , nTotalCount .ToString());
				XmlUtil.SetSingleNodeAttribute(xFolder.OwnerDocument, xChild, "UnreadCount", nUnreadCount.ToString());
				if ( nUnreadCount > 0 )
					XmlUtil.SetSingleNodeAttribute(xFolder.OwnerDocument, xChild, "DisplayName", "<b>" + sName + "</b> <font color=blue>(" + nUnreadCount.ToString() + ")</font>");
				else
					XmlUtil.SetSingleNodeAttribute(xFolder.OwnerDocument, xChild, "DisplayName", sName);
			}
		}

		// =====================================================================================
		// UpdateFolderTreeNodeCounts (public) — connect and update counts on folder tree
		// BEFORE: public static void UpdateFolderTreeNodeCounts(HttpContext Context, ...)
		// AFTER:  public void UpdateFolderTreeNodeCounts(HttpContext context, ...)
		// =====================================================================================

		/// <summary>
		/// Connects to the IMAP server and updates TotalCount and UnreadCount attributes on all
		/// child folder XML nodes under the provided xFolder node.
		/// </summary>
		/// <param name="context">HTTP context (currently unused; preserved for API compatibility).</param>
		/// <param name="sSERVER_URL">IMAP server hostname or IP address.</param>
		/// <param name="nPORT">IMAP server port.</param>
		/// <param name="bMAILBOX_SSL">True for SSL, false for Auto.</param>
		/// <param name="sEMAIL_USER">IMAP username for authentication.</param>
		/// <param name="sEMAIL_PASSWORD">IMAP password for authentication.</param>
		/// <param name="xFolder">XML node whose children are IMAP folder nodes to update.</param>
		public void UpdateFolderTreeNodeCounts(HttpContext context, string sSERVER_URL, int nPORT, bool bMAILBOX_SSL, string sEMAIL_USER, string sEMAIL_PASSWORD, XmlNode xFolder)
		{
			using ( ImapClient imap = new ImapClient() )
			{
				imap.Connect(sSERVER_URL, nPORT, (bMAILBOX_SSL ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.Auto));
				imap.AuthenticationMechanisms.Remove("XOAUTH2");
				// 01/22/2017 Paul.  There is a bug with NTLM. 
				// http://stackoverflow.com/questions/39573233/mailkit-authenticate-to-imap-fails
				imap.AuthenticationMechanisms.Remove("NTLM");
				imap.Authenticate(sEMAIL_USER, sEMAIL_PASSWORD);

				UpdateFolderTreeNodeCounts(imap, xFolder);
				imap.Disconnect(true);
			}
		}

		// =====================================================================================
		// GetFolderTreeFromResults — recursively build folder tree XML from IMAP sub-folders
		// BEFORE: private static void GetFolderTreeFromResults(ImapClient imap, XmlNode xParent, IMailFolder fResults)
		// AFTER:  private void GetFolderTreeFromResults(ImapClient imap, XmlNode xParent, IMailFolder fResults)
		// =====================================================================================

		/// <summary>
		/// Recursively enumerates sub-folders of the given IMailFolder and appends them as
		/// child XML Folder elements under xParent, setting Id, TotalCount, UnreadCount, Name,
		/// and DisplayName attributes. Recurses when a folder has the HasChildren attribute.
		/// </summary>
		/// <param name="imap">Active authenticated ImapClient.</param>
		/// <param name="xParent">Parent XML node to append folder child elements to.</param>
		/// <param name="fResults">IMailFolder whose sub-folders are enumerated.</param>
		private void GetFolderTreeFromResults(ImapClient imap, XmlNode xParent, IMailFolder fResults)
		{
			XmlDocument xml = xParent.OwnerDocument;
			if ( fResults.Exists )
			{
				foreach ( IMailFolder fld in fResults.GetSubfolders() )
				{
					XmlElement xChild = xml.CreateElement("Folder");
					xParent.AppendChild(xChild);

					XmlUtil.SetSingleNodeAttribute(xml, xChild, "Id"         , fld.FullName.ToString());
					XmlUtil.SetSingleNodeAttribute(xml, xChild, "TotalCount" , fld.Count   .ToString());
					XmlUtil.SetSingleNodeAttribute(xml, xChild, "UnreadCount", fld.Unread  .ToString());
					// 07/30/2010 Paul.  We need to separate the Name from the DisplayName due to the formatting differences. 
					XmlUtil.SetSingleNodeAttribute(xml, xChild, "Name"       , fld.Name);
					XmlUtil.SetSingleNodeAttribute(xml, xChild, "DisplayName", fld.Name);
					if ( (fld.Attributes & FolderAttributes.HasChildren) == FolderAttributes.HasChildren )
					{
						GetFolderTreeFromResults(imap, xChild, fld);
					}
				}
			}
		}

		// =====================================================================================
		// GetFolderTree — build complete IMAP mailbox folder hierarchy as XmlDocument
		// BEFORE: public static XmlDocument GetFolderTree(HttpContext Context, ...)
		// AFTER:  public XmlDocument GetFolderTree(HttpContext context, ...)
		// =====================================================================================

		/// <summary>
		/// Connects to the IMAP server and builds a complete XML representation of the mailbox
		/// folder hierarchy, including counts for each folder. Returns the XML document.
		/// </summary>
		/// <param name="context">HTTP context (currently unused; preserved for API compatibility).</param>
		/// <param name="sSERVER_URL">IMAP server hostname or IP address.</param>
		/// <param name="nPORT">IMAP server port.</param>
		/// <param name="bMAILBOX_SSL">True for SSL, false for Auto.</param>
		/// <param name="sEMAIL_USER">IMAP username for authentication and display label.</param>
		/// <param name="sEMAIL_PASSWORD">IMAP password for authentication.</param>
		/// <returns>XmlDocument with Folders root element and nested Folder child elements.</returns>
		public XmlDocument GetFolderTree(HttpContext context, string sSERVER_URL, int nPORT, bool bMAILBOX_SSL, string sEMAIL_USER, string sEMAIL_PASSWORD)
		{
			XmlDocument xml = new XmlDocument();
			xml.AppendChild(xml.CreateProcessingInstruction("xml", "version=\"1.0\" encoding=\"UTF-8\""));
			xml.AppendChild(xml.CreateElement("Folders"));
			XmlUtil.SetSingleNodeAttribute(xml, xml.DocumentElement, "Id"         , String.Empty              );
			XmlUtil.SetSingleNodeAttribute(xml, xml.DocumentElement, "DisplayName", "Mailbox - " + sEMAIL_USER);

			using ( ImapClient imap = new ImapClient() )
			{
				imap.Connect(sSERVER_URL, nPORT, (bMAILBOX_SSL ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.Auto));
				imap.AuthenticationMechanisms.Remove("XOAUTH2");
				// 01/22/2017 Paul.  There is a bug with NTLM. 
				// http://stackoverflow.com/questions/39573233/mailkit-authenticate-to-imap-fails
				imap.AuthenticationMechanisms.Remove("NTLM");
				imap.Authenticate(sEMAIL_USER, sEMAIL_PASSWORD);

				IMailFolder root = imap.GetFolder(imap.PersonalNamespaces[0]);
				GetFolderTreeFromResults(imap, xml.DocumentElement, root);
				UpdateFolderTreeNodeCounts(imap, xml.DocumentElement);
				imap.Disconnect(true);
			}
			return xml;
		}

		// =====================================================================================
		// GetFolderCount — retrieve total and unread message counts for a specific folder
		// BEFORE: public static void GetFolderCount(HttpContext Context, ..., ref int nTotalCount, ref int nUnreadCount)
		// AFTER:  public void GetFolderCount(HttpContext context, ..., ref int nTotalCount, ref int nUnreadCount)
		// =====================================================================================

		/// <summary>
		/// Connects to the IMAP server and populates nTotalCount and nUnreadCount for the
		/// specified folder. Values are unchanged if the folder does not exist.
		/// </summary>
		/// <param name="context">HTTP context (currently unused; preserved for API compatibility).</param>
		/// <param name="sSERVER_URL">IMAP server hostname or IP address.</param>
		/// <param name="nPORT">IMAP server port.</param>
		/// <param name="bMAILBOX_SSL">True for SSL, false for Auto.</param>
		/// <param name="sEMAIL_USER">IMAP username for authentication.</param>
		/// <param name="sEMAIL_PASSWORD">IMAP password for authentication.</param>
		/// <param name="sFOLDER_ID">Folder identifier (full IMAP folder name).</param>
		/// <param name="nTotalCount">Output: total message count in the folder.</param>
		/// <param name="nUnreadCount">Output: unread message count in the folder.</param>
		public void GetFolderCount(HttpContext context, string sSERVER_URL, int nPORT, bool bMAILBOX_SSL, string sEMAIL_USER, string sEMAIL_PASSWORD, string sFOLDER_ID, ref int nTotalCount, ref int nUnreadCount)
		{
			using ( ImapClient imap = new ImapClient() )
			{
				imap.Connect(sSERVER_URL, nPORT, (bMAILBOX_SSL ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.Auto));
				imap.AuthenticationMechanisms.Remove("XOAUTH2");
				// 01/22/2017 Paul.  There is a bug with NTLM. 
				// http://stackoverflow.com/questions/39573233/mailkit-authenticate-to-imap-fails
				imap.AuthenticationMechanisms.Remove("NTLM");
				imap.Authenticate(sEMAIL_USER, sEMAIL_PASSWORD);

				IMailFolder mailbox = imap.GetFolder(sFOLDER_ID);
				if ( mailbox != null && mailbox.Exists )
				{
					nTotalCount  = mailbox.Count ;
					nUnreadCount = mailbox.Unread;
				}
				imap.Disconnect(true);
			}
		}

		// =====================================================================================
		// DeleteMessage — mark a message as deleted in the IMAP folder
		// BEFORE: public static void DeleteMessage(HttpContext Context, ...)
		// AFTER:  public void DeleteMessage(HttpContext context, ...)
		// =====================================================================================

		/// <summary>
		/// Connects to the IMAP server, opens the specified folder in ReadWrite mode, and
		/// marks the message identified by sUNIQUE_ID with the Deleted flag.
		/// </summary>
		/// <param name="context">HTTP context (currently unused; preserved for API compatibility).</param>
		/// <param name="sSERVER_URL">IMAP server hostname or IP address.</param>
		/// <param name="nPORT">IMAP server port.</param>
		/// <param name="bMAILBOX_SSL">True for SSL, false for Auto.</param>
		/// <param name="sEMAIL_USER">IMAP username for authentication.</param>
		/// <param name="sEMAIL_PASSWORD">IMAP password for authentication.</param>
		/// <param name="sFOLDER_ID">Folder containing the message to delete.</param>
		/// <param name="sUNIQUE_ID">String representation of the IMAP UniqueId for the message.</param>
		public void DeleteMessage(HttpContext context, string sSERVER_URL, int nPORT, bool bMAILBOX_SSL, string sEMAIL_USER, string sEMAIL_PASSWORD, string sFOLDER_ID, string sUNIQUE_ID)
		{
			using ( ImapClient imap = new ImapClient() )
			{
				imap.Connect(sSERVER_URL, nPORT, (bMAILBOX_SSL ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.Auto));
				imap.AuthenticationMechanisms.Remove("XOAUTH2");
				// 01/22/2017 Paul.  There is a bug with NTLM. 
				// http://stackoverflow.com/questions/39573233/mailkit-authenticate-to-imap-fails
				imap.AuthenticationMechanisms.Remove("NTLM");
				imap.Authenticate(sEMAIL_USER, sEMAIL_PASSWORD);

				IMailFolder mailbox = imap.GetFolder(sFOLDER_ID);
				if ( mailbox != null && mailbox.Exists )
				{
					mailbox.Open(FolderAccess.ReadWrite);
					MailKit.UniqueId uid = new MailKit.UniqueId((uint) Sql.ToInteger(sUNIQUE_ID));
					mailbox.AddFlags(uid, MessageFlags.Deleted, true);
				}
				imap.Disconnect(true);
			}
		}

		// =====================================================================================
		// GetMessage — retrieve a single IMAP message with full body and attachment info
		// BEFORE: public static DataTable GetMessage(HttpContext Context, ...)
		// AFTER:  public DataTable GetMessage(HttpContext context, ...)
		// Application["CONFIG.email_xss"] → _memoryCache.Get<object>("CONFIG.email_xss")
		// =====================================================================================

		/// <summary>
		/// Connects to the IMAP server, fetches the message summary and full MimeMessage for the
		/// specified unique ID, and returns a DataTable (created by MimeUtils.CreateMessageTable)
		/// populated with envelope, headers, body, and attachment metadata.
		/// XSS filtering is applied to both text and HTML body parts using the CONFIG.email_xss
		/// setting read from IMemoryCache (replaces Application["CONFIG.email_xss"]).
		/// </summary>
		/// <param name="context">HTTP context (currently unused; preserved for API compatibility).</param>
		/// <param name="sSERVER_URL">IMAP server hostname or IP address.</param>
		/// <param name="nPORT">IMAP server port.</param>
		/// <param name="bMAILBOX_SSL">True for SSL, false for Auto.</param>
		/// <param name="sEMAIL_USER">IMAP username for authentication.</param>
		/// <param name="sEMAIL_PASSWORD">IMAP password for authentication.</param>
		/// <param name="sFOLDER_ID">Folder containing the message; defaults to INBOX.</param>
		/// <param name="sUNIQUE_ID">String representation of the IMAP UniqueId for the message.</param>
		/// <returns>DataTable with one row containing the full message data.</returns>
		public DataTable GetMessage(HttpContext context, string sSERVER_URL, int nPORT, bool bMAILBOX_SSL, string sEMAIL_USER, string sEMAIL_PASSWORD, string sFOLDER_ID, string sUNIQUE_ID)
		{
			DataTable dt = MimeUtils.CreateMessageTable();
			DataRow row = dt.NewRow();
			dt.Rows.Add(row);

			using ( ImapClient imap = new ImapClient() )
			{
				imap.Connect(sSERVER_URL, nPORT, (bMAILBOX_SSL ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.Auto));
				imap.AuthenticationMechanisms.Remove("XOAUTH2");
				// 01/22/2017 Paul.  There is a bug with NTLM. 
				// http://stackoverflow.com/questions/39573233/mailkit-authenticate-to-imap-fails
				imap.AuthenticationMechanisms.Remove("NTLM");
				imap.Authenticate(sEMAIL_USER, sEMAIL_PASSWORD);

				if ( Sql.IsEmptyString(sFOLDER_ID) )
					sFOLDER_ID = "INBOX";

				IMailFolder mailbox = imap.GetFolder(sFOLDER_ID);
				if ( mailbox != null && mailbox.Exists )
				{
					mailbox.Open(FolderAccess.ReadOnly);
					MailKit.UniqueId uid = new MailKit.UniqueId((uint) Sql.ToInteger(sUNIQUE_ID));
					// 01/23/2017 Paul.  Need BodyStructure to get attachment info. 
					IList<IMessageSummary> summeries = mailbox.Fetch(new List<MailKit.UniqueId>() { uid }, MessageSummaryItems.All | MessageSummaryItems.BodyStructure | MessageSummaryItems.UniqueId);
					if ( summeries != null && summeries.Count > 0 )
					{
						IMessageSummary summary = summeries[0];
						double dSize = (summary.Size.HasValue ? (double) summary.Size : 0);
						string sSize = String.Empty;
						if ( dSize < 1024 )
							sSize = dSize.ToString() + " B";
						else if ( dSize < 1024 * 1024 )
							sSize = Math.Floor(dSize / 1024).ToString() + " KB";
						else
							sSize = Math.Floor(dSize / (1024 * 1024)).ToString() + " MB";

						row["ID"                ] = Guid.NewGuid().ToString().Replace('-', '_');
						row["UNIQUE_ID"         ] = summary.UniqueId.Id                ;
						row["SIZE"              ] = summary.Size                       ;
						row["SIZE_STRING"       ] = sSize                              ;
						if ( summary.Flags.HasValue )
							row["IS_READ"       ] = ((summary.Flags.Value & MessageFlags.Seen) == MessageFlags.Seen);
						row["TO_ADDRS"          ] = (summary.Envelope.To != null ? summary.Envelope.To.ToString() : String.Empty);
						row["CC_ADDRS"          ] = (summary.Envelope.Cc != null ? summary.Envelope.Cc.ToString() : String.Empty);
						row["NAME"              ] = summary.Envelope.Subject           ;
						row["MESSAGE_ID"        ] = summary.Envelope.MessageId         ;
						row["DATE_MODIFIED"     ] = summary.Date.DateTime.ToLocalTime();
						row["DATE_ENTERED"      ] = summary.Date.DateTime.ToLocalTime();
						row["DATE_START"        ] = summary.Date.DateTime.ToLocalTime();
						if ( summary.Envelope.From != null )
						{
							string sFROM_ADDR = String.Empty;
							string sFROM_NAME = String.Empty;
							foreach ( InternetAddress from in summary.Envelope.From )
							{
								if ( from is MailboxAddress )
								{
									MailboxAddress addr = from as MailboxAddress;
									sFROM_ADDR += addr.Address;
									sFROM_NAME += addr.Name   ;
									break;
								}
							}
							row["FROM"     ] = summary.Envelope.From.ToString();
							row["FROM_ADDR"] = sFROM_ADDR;
							row["FROM_NAME"] = sFROM_NAME;
						}

						XmlDocument xmlInternetHeaders = new XmlDocument();
						xmlInternetHeaders.AppendChild(xmlInternetHeaders.CreateElement("Headers"));
						MimeMessage email = mailbox.GetMessage(summary.UniqueId);
						for ( int i = 0; i < email.Headers.Count; i++ )
						{
							XmlElement xHeader = xmlInternetHeaders.CreateElement("Header");
							xmlInternetHeaders.DocumentElement.AppendChild(xHeader);
							XmlElement xName  = xmlInternetHeaders.CreateElement("Name" );
							XmlElement xValue = xmlInternetHeaders.CreateElement("Value");
							xHeader.AppendChild(xName );
							xHeader.AppendChild(xValue);
							xName .InnerText = email.Headers[i].Field;
							xValue.InnerText = email.Headers[i].Value;
						}
						row["INTERNET_HEADERS"] = xmlInternetHeaders.OuterXml;

						// 01/21/2017 Paul.  Only get the body values once as they may be computed. 
						// http://www.mimekit.net/docs/html/WorkingWithMessages.htm
						string sTextBody = email.TextBody;
						string sHtmlBody = email.HtmlBody;
						// BEFORE: Sql.ToString(Context.Application["CONFIG.email_xss"])
						// AFTER:  Sql.ToString(_memoryCache.Get<object>("CONFIG.email_xss"))
						string sEmailXss          = Sql.ToString(_memoryCache.Get<object>("CONFIG.email_xss"));
						string sDESCRIPTION       = EmailUtils.XssFilter(sTextBody, sEmailXss);
						string sDESCRIPTION_HTML  = EmailUtils.XssFilter(sHtmlBody, sEmailXss);
						sDESCRIPTION_HTML = MimeUtils.EmbedInlineImages(email, sDESCRIPTION_HTML);
						row["DESCRIPTION"     ] = sDESCRIPTION;
						row["DESCRIPTION_HTML"] = sDESCRIPTION_HTML;
						if ( email.Attachments != null )
						{
							row["ATTACHMENTS"] = MimeUtils.GetAttachments(email);
						}
					}
				}
				imap.Disconnect(true);
			}
			return dt;
		}

		// =====================================================================================
		// GetAttachmentData — retrieve raw binary data for a specific email attachment
		// BEFORE: public static byte[] GetAttachmentData(HttpContext Context, ...)
		// AFTER:  public byte[] GetAttachmentData(HttpContext context, ...)
		// MimePart.ContentObject.DecodeTo → MimePart.Content.DecodeTo (MimeKit 4.x API change)
		// =====================================================================================

		/// <summary>
		/// Connects to the IMAP server, retrieves the full MimeMessage for the given unique ID,
		/// and extracts the binary content of the attachment at position nATTACHMENT_ID.
		/// Also populates sFILENAME, sCONTENT_TYPE, and bINLINE via ref parameters.
		/// </summary>
		/// <param name="context">HTTP context (currently unused; preserved for API compatibility).</param>
		/// <param name="sSERVER_URL">IMAP server hostname or IP address.</param>
		/// <param name="nPORT">IMAP server port.</param>
		/// <param name="bMAILBOX_SSL">True for SSL, false for Auto.</param>
		/// <param name="sEMAIL_USER">IMAP username for authentication.</param>
		/// <param name="sEMAIL_PASSWORD">IMAP password for authentication.</param>
		/// <param name="sFOLDER_ID">Folder containing the message; defaults to INBOX.</param>
		/// <param name="sUNIQUE_ID">String representation of the IMAP UniqueId for the message.</param>
		/// <param name="nATTACHMENT_ID">Zero-based index of the attachment to retrieve.</param>
		/// <param name="sFILENAME">Output: filename of the attachment from ContentDisposition.</param>
		/// <param name="sCONTENT_TYPE">Output: media type of the attachment.</param>
		/// <param name="bINLINE">Output: true if the attachment is inline; false otherwise.</param>
		/// <returns>Raw byte array of the decoded attachment content, or null if not found.</returns>
		public byte[] GetAttachmentData(HttpContext context, string sSERVER_URL, int nPORT, bool bMAILBOX_SSL, string sEMAIL_USER, string sEMAIL_PASSWORD, string sFOLDER_ID, string sUNIQUE_ID, int nATTACHMENT_ID, ref string sFILENAME, ref string sCONTENT_TYPE, ref bool bINLINE)
		{
			byte[] byDataBinary = null;
			using ( ImapClient imap = new ImapClient() )
			{
				imap.Connect(sSERVER_URL, nPORT, (bMAILBOX_SSL ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.Auto));
				imap.AuthenticationMechanisms.Remove("XOAUTH2");
				// 01/22/2017 Paul.  There is a bug with NTLM. 
				// http://stackoverflow.com/questions/39573233/mailkit-authenticate-to-imap-fails
				imap.AuthenticationMechanisms.Remove("NTLM");
				imap.Authenticate(sEMAIL_USER, sEMAIL_PASSWORD);

				if ( Sql.IsEmptyString(sFOLDER_ID) )
					sFOLDER_ID = "INBOX";

				IMailFolder mailbox = imap.GetFolder(sFOLDER_ID);
				if ( mailbox != null && mailbox.Exists )
				{
					mailbox.Open(FolderAccess.ReadOnly);
					MimeMessage email = null;
					bool bLoadSuccessful = false;
					try
					{
						MailKit.UniqueId uid = new MailKit.UniqueId((uint) Sql.ToInteger(sUNIQUE_ID));
						email = mailbox.GetMessage(uid);
						bLoadSuccessful = true;
					}
					catch
					{
					}
					if ( email != null && bLoadSuccessful )
					{
						if ( email.Attachments != null )
						{
							int nAttachment = 0;
							foreach ( MimeKit.MimeEntity att in email.Attachments )
							{
								if ( nATTACHMENT_ID == nAttachment )
								{
									if ( att is MessagePart || att is MimePart )
									{
										// http://www.mimekit.net/docs/html/WorkingWithMessages.htm
										bINLINE          = false;
										sFILENAME        = String.Empty;
										string sFILE_EXT = String.Empty;
										sCONTENT_TYPE    = att.ContentType.MediaType;
										if ( att.ContentDisposition != null && att.ContentDisposition.FileName != null )
										{
											sFILENAME = Path.GetFileName (att.ContentDisposition.FileName);
											sFILE_EXT = Path.GetExtension(sFILENAME);
										}
										using ( MemoryStream mem = new MemoryStream() )
										{
											if ( att is MessagePart )
											{
												MessagePart part = att as MessagePart;
												part.Message.WriteTo(mem);
											}
											else if ( att is MimePart )
											{
												MimePart part = att as MimePart;
												// BEFORE: part.ContentObject.DecodeTo(mem); (MimeKit pre-4.x API)
												// AFTER:  part.Content.DecodeTo(mem);       (MimeKit 4.x API change: ContentObject → Content)
												part.Content.DecodeTo(mem);
											}
											byDataBinary = mem.ToArray();
										}
									}
									break;
								}
								nAttachment++;
							}
						}
					}
				}
				imap.Disconnect(true);
			}
			return byDataBinary;
		}

		// =====================================================================================
		// GetFolderMessages — retrieve all message summaries in a folder as a DataTable
		// BEFORE: public static DataTable GetFolderMessages(HttpContext Context, ...)
		// AFTER:  public DataTable GetFolderMessages(HttpContext context, ...)
		// =====================================================================================

		/// <summary>
		/// Connects to the IMAP server and returns a DataTable containing one row per message
		/// in the specified folder, populated with envelope data (subject, from, to, cc, date,
		/// size, read status, attachment flag) from the IMAP FETCH response.
		/// </summary>
		/// <param name="context">HTTP context (currently unused; preserved for API compatibility).</param>
		/// <param name="sSERVER_URL">IMAP server hostname or IP address.</param>
		/// <param name="nPORT">IMAP server port.</param>
		/// <param name="bMAILBOX_SSL">True for SSL, false for Auto.</param>
		/// <param name="sEMAIL_USER">IMAP username for authentication.</param>
		/// <param name="sEMAIL_PASSWORD">IMAP password for authentication.</param>
		/// <param name="sFOLDER_ID">Folder to list; defaults to INBOX.</param>
		/// <returns>DataTable with one row per message in the folder.</returns>
		public DataTable GetFolderMessages(HttpContext context, string sSERVER_URL, int nPORT, bool bMAILBOX_SSL, string sEMAIL_USER, string sEMAIL_PASSWORD, string sFOLDER_ID)
		{
			DataTable dt = MimeUtils.CreateMessageTable();

			using ( ImapClient imap = new ImapClient() )
			{
				imap.Connect(sSERVER_URL, nPORT, (bMAILBOX_SSL ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.Auto));
				imap.AuthenticationMechanisms.Remove("XOAUTH2");
				// 01/22/2017 Paul.  There is a bug with NTLM. 
				// http://stackoverflow.com/questions/39573233/mailkit-authenticate-to-imap-fails
				imap.AuthenticationMechanisms.Remove("NTLM");
				imap.Authenticate(sEMAIL_USER, sEMAIL_PASSWORD);

				if ( Sql.IsEmptyString(sFOLDER_ID) )
					sFOLDER_ID = "INBOX";

				IMailFolder mailbox = imap.GetFolder(sFOLDER_ID);
				if ( mailbox != null && mailbox.Exists )
				{
					mailbox.Open(FolderAccess.ReadOnly);
					// 01/23/2017 Paul.  Need BodyStructure to get attachment info. 
					IList<IMessageSummary> summeries = mailbox.Fetch(0, -1, MessageSummaryItems.All | MessageSummaryItems.BodyStructure | MessageSummaryItems.UniqueId);
					if ( summeries != null && summeries.Count > 0 )
					{
						foreach ( IMessageSummary summary in summeries )
						{
							DataRow row = dt.NewRow();
							dt.Rows.Add(row);
							double dSize = (summary.Size.HasValue ? (double) summary.Size : 0);
							string sSize = String.Empty;
							if ( dSize < 1024 )
								sSize = dSize.ToString() + " B";
							else if ( dSize < 1024 * 1024 )
								sSize = Math.Floor(dSize / 1024).ToString() + " KB";
							else
								sSize = Math.Floor(dSize / (1024 * 1024)).ToString() + " MB";

							row["ID"           ] = Guid.NewGuid().ToString().Replace('-', '_');
							row["UNIQUE_ID"    ] = summary.UniqueId.Id                ;
							row["SIZE"         ] = summary.Size                       ;
							row["SIZE_STRING"  ] = sSize                              ;
							if ( summary.Flags.HasValue )
								row["IS_READ"  ] = ((summary.Flags.Value & MessageFlags.Seen) == MessageFlags.Seen);
							row["TO_ADDRS"     ] = (summary.Envelope.To != null ? summary.Envelope.To.ToString() : String.Empty);
							row["CC_ADDRS"     ] = (summary.Envelope.Cc != null ? summary.Envelope.Cc.ToString() : String.Empty);
							row["NAME"         ] = summary.Envelope.Subject           ;
							row["MESSAGE_ID"   ] = summary.Envelope.MessageId         ;
							row["DATE_MODIFIED"] = summary.Date.DateTime.ToLocalTime();
							row["DATE_ENTERED" ] = summary.Date.DateTime.ToLocalTime();
							row["DATE_START"   ] = summary.Date.DateTime.ToLocalTime();
							if ( summary.Envelope.From != null )
							{
								string sFROM_ADDR = String.Empty;
								string sFROM_NAME = String.Empty;
								foreach ( InternetAddress from in summary.Envelope.From )
								{
									if ( from is MailboxAddress )
									{
										MailboxAddress addr = from as MailboxAddress;
										sFROM_ADDR += addr.Address;
										sFROM_NAME += addr.Name   ;
										break;
									}
								}
								row["FROM"     ] = summary.Envelope.From.ToString();
								row["FROM_ADDR"] = sFROM_ADDR;
								row["FROM_NAME"] = sFROM_NAME;
							}
							if ( summary.Attachments != null )
							{
								foreach ( BodyPartBasic att in summary.Attachments )
								{
									row["HAS_ATTACHMENTS"] = true;
									break;
								}
							}
							// 07/17/2010 Paul.  Another way to detect attachments. 
							// http://stackoverflow.com/questions/36881966/mimekit-imapclient-get-attachment-information-without-downloading-whole-message
							//foreach ( BodyPartBasic part in summary.BodyParts )
							//{
							//	if ( part.IsAttachment )
							//	{
							//		row["HAS_ATTACHMENTS"] = true;
							//		break;
							//	}
							//}
						}
					}
				}
				imap.Disconnect(true);
			}
			return dt;
		}

		// =====================================================================================
		// ImportMessage — fetch IMAP message and import it into SplendidCRM database
		// BEFORE: public static Guid ImportMessage(HttpContext Context, ...)
		// AFTER:  public Guid ImportMessage(HttpContext context, ...)
		// =====================================================================================

		/// <summary>
		/// Connects to the IMAP server, retrieves the full MimeMessage for the specified unique ID,
		/// and delegates to MimeUtils.ImportMessage to persist the email in the SplendidCRM database.
		/// Logs fetch errors via SyncError.SystemMessage for diagnostic tracking.
		/// </summary>
		/// <param name="context">HTTP context required by MimeUtils.ImportMessage for DB operations.</param>
		/// <param name="sPARENT_TYPE">CRM module name of the parent record (e.g., "Cases", "Contacts").</param>
		/// <param name="gPARENT_ID">GUID of the parent record to link the imported email to.</param>
		/// <param name="sSERVER_URL">IMAP server hostname or IP address.</param>
		/// <param name="nPORT">IMAP server port.</param>
		/// <param name="bMAILBOX_SSL">True for SSL, false for Auto.</param>
		/// <param name="sEMAIL_USER">IMAP username for authentication.</param>
		/// <param name="sEMAIL_PASSWORD">IMAP password for authentication.</param>
		/// <param name="gUSER_ID">GUID of the user performing the import.</param>
		/// <param name="gASSIGNED_USER_ID">GUID of the assigned user for the imported email record.</param>
		/// <param name="gTEAM_ID">GUID of the primary team for the imported email record.</param>
		/// <param name="sTEAM_SET_LIST">Comma-separated team GUIDs for team-set assignment.</param>
		/// <param name="sFOLDER_ID">Folder containing the message; defaults to INBOX.</param>
		/// <param name="sUNIQUE_ID">String representation of the IMAP UniqueId for the message.</param>
		/// <returns>GUID of the created email record, or Guid.Empty if fetch or import failed.</returns>
		public Guid ImportMessage(HttpContext context, string sPARENT_TYPE, Guid gPARENT_ID, string sSERVER_URL, int nPORT, bool bMAILBOX_SSL, string sEMAIL_USER, string sEMAIL_PASSWORD, Guid gUSER_ID, Guid gASSIGNED_USER_ID, Guid gTEAM_ID, string sTEAM_SET_LIST, string sFOLDER_ID, string sUNIQUE_ID)
		{
			Guid gEMAIL_ID = Guid.Empty;

			using ( ImapClient imap = new ImapClient() )
			{
				imap.Connect(sSERVER_URL, nPORT, (bMAILBOX_SSL ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.Auto));
				imap.AuthenticationMechanisms.Remove("XOAUTH2");
				// 01/22/2017 Paul.  There is a bug with NTLM. 
				// http://stackoverflow.com/questions/39573233/mailkit-authenticate-to-imap-fails
				imap.AuthenticationMechanisms.Remove("NTLM");
				imap.Authenticate(sEMAIL_USER, sEMAIL_PASSWORD);

				if ( Sql.IsEmptyString(sFOLDER_ID) )
					sFOLDER_ID = "INBOX";

				IMailFolder mailbox = imap.GetFolder(sFOLDER_ID);
				if ( mailbox != null && mailbox.Exists )
				{
					mailbox.Open(FolderAccess.ReadOnly);
					MimeMessage email = null;
					bool bLoadSuccessful = false;
					try
					{
						MailKit.UniqueId uid = new MailKit.UniqueId((uint) Sql.ToInteger(sUNIQUE_ID));
						email = mailbox.GetMessage(uid);
						bLoadSuccessful = true;
					}
					catch(Exception ex)
					{
						string sError = "Error loading email for " + sEMAIL_USER + ", " + sUNIQUE_ID + "." + ControlChars.CrLf;
						sError += Utils.ExpandException(ex) + ControlChars.CrLf;
						SyncError.SystemMessage(context, "Error", new StackTrace(true).GetFrame(0), sError);
					}
					if ( email != null && bLoadSuccessful )
					{
						gEMAIL_ID = MimeUtils.ImportMessage(context, sPARENT_TYPE, gPARENT_ID, gUSER_ID, gASSIGNED_USER_ID, gTEAM_ID, sTEAM_SET_LIST, sUNIQUE_ID, email);
					}
				}
				imap.Disconnect(true);
			}
			return gEMAIL_ID;
		}
	}
}
