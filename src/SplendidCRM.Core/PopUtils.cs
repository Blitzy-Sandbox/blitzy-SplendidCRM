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
// .NET 10 Migration: SplendidCRM/_code/PopUtils.cs → src/SplendidCRM.Core/PopUtils.cs
// Changes applied:
//   - REMOVED: using System.Web; using System.Web.SessionState; (System.Web not available in .NET 10)
//   - REMOVED: using System.Net.Mail; using System.Net.Mime; (not needed; MailKit provides equivalent types)
//   - ADDED:   using Microsoft.AspNetCore.Http; (HttpContext, IHttpContextAccessor)
//   - ADDED:   using Microsoft.Extensions.Caching.Memory; (IMemoryCache replaces Application[])
//   - REPLACED: Context.Application["key"] → GetMemoryCache(Context) using service-locator from
//               context.RequestServices (preferred) or static ambient _ambientCache (background fallback).
//               Pattern mirrors MimeUtils.cs migration approach for consistency.
//   - REPLACED: Context.Session["USER_SETTINGS/CULTURE"] → Context.Session.GetString("USER_SETTINGS/CULTURE")
//               (ASP.NET Core ISession.GetString() replaces HttpSessionState indexer)
//   - REPLACED: L10N.Term(Context.Application, sCULTURE, ...) → L10N.Term(GetMemoryCache(Context), sCULTURE, ...)
//               (IMemoryCache overload replaces HttpApplicationState overload)
//   - REMOVED:  HttpSessionState Session parameter from ImportMessage() — HttpSessionState is not
//               available in .NET 10; session is accessed via Context.Session (ISession) instead.
//               All callers must remove this parameter from their call sites.
//   - REPLACED: pop.GetStream(index) → pop.GetStream(index, false) — MailKit 4.x Pop3Client.GetStream()
//               requires explicit bool headersOnly parameter (false = retrieve full message).
//   - REPLACED: pop.GetStreams(start, count) → pop.GetStreams(start, count, false) — same reason.
//   - REPLACED: pop.GetMessageSize(index) return type int → stored as double (implicit widening cast) —
//               GetMessageSize() returns int in MailKit 4.x; no change needed for double assignment.
//   - REPLACED: part.ContentObject.DecodeTo(mem) → part.Content.DecodeTo(mem) — ContentObject was
//               deprecated and removed in MimeKit 4.x; Content is the replacement property.
//   - ADDED:    static ambient fields + GetMemoryCache() service-locator helper for DI integration.
//   - ADDED:    DI constructor: PopUtils(IHttpContextAccessor, IMemoryCache) for service registration.
//   - PRESERVED: namespace SplendidCRM, all public method signatures (except removed HttpSessionState),
//               all MailKit POP3 operations, all business logic, minimal change clause honored.
//   - File MUST compile under net10.0.
#nullable disable
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Data;
using System.Xml;
using System.Diagnostics;

using MimeKit;
using MailKit;
using MailKit.Net.Pop3;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	/// <summary>
	/// POP3 email utility class providing connection validation, folder tree inspection,
	/// message retrieval, attachment access, and email import operations against POP3 mail servers.
	///
	/// Migrated from SplendidCRM/_code/PopUtils.cs for .NET 10 ASP.NET Core.
	///
	/// MIGRATION PATTERN:
	///   BEFORE (.NET Framework 4.8):
	///     All methods were static. HttpContext.Current.Application[] was used for cache access.
	///     HttpContext.Current.Session[] was used for session data.
	///   AFTER (.NET 10 ASP.NET Core):
	///     Methods remain static and accept HttpContext as a parameter (same as before).
	///     IMemoryCache is resolved from context.RequestServices using the service-locator pattern,
	///     consistent with MimeUtils.cs migration approach.
	///     Static ambient fields provide fallback for background-service call paths.
	///
	/// NOTE: HttpSessionState Session parameter removed from ImportMessage() — that type is not
	///   available in .NET 10. Session is accessed via Context.Session (ISession) on HttpContext.
	/// </summary>
	public class PopUtils
	{
		// =====================================================================================
		// .NET 10 Migration: Static ambient field replacing HttpContext.Current.Application[].
		// Set via SetAmbient() called at application startup by the DI-aware host.
		// Used as fallback when no HttpContext is available (background services, timer callbacks).
		// BEFORE: HttpContext.Current.Application["key"]
		// AFTER:  GetMemoryCache(context) → _ambientCache fallback
		// =====================================================================================

		/// <summary>
		/// Static ambient IMemoryCache — replaces HttpApplicationState (Application["key"]) access.
		/// Set via SetAmbient() at application startup. Fallback when no HttpContext is available.
		/// </summary>
		private static IMemoryCache _ambientCache;

		// =====================================================================================
		// DI instance fields — for service registration in the DI container.
		// =====================================================================================

		/// <summary>
		/// IHttpContextAccessor injected via constructor — enables DI-compatible static method callers.
		/// BEFORE: HttpContext.Current (static, thread-local)
		/// AFTER:  _httpContextAccessor.HttpContext (injected, thread-safe via DI scope)
		/// </summary>
		private readonly IHttpContextAccessor _httpContextAccessor;

		/// <summary>
		/// IMemoryCache injected via constructor — enables DI-compatible cache access.
		/// BEFORE: HttpContext.Current.Application["key"] (HttpApplicationState)
		/// AFTER:  _memoryCache.Get&lt;object&gt;("key")
		/// </summary>
		private readonly IMemoryCache _memoryCache;

		// =====================================================================================
		// Constructor
		// =====================================================================================

		/// <summary>
		/// Constructor for dependency injection. Register PopUtils as a scoped service so that
		/// IHttpContextAccessor and IMemoryCache can be injected.
		/// </summary>
		/// <param name="httpContextAccessor">ASP.NET Core HTTP context accessor replacing HttpContext.Current</param>
		/// <param name="memoryCache">In-memory cache replacing HttpApplicationState (Application[])</param>
		public PopUtils(IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache)
		{
			_httpContextAccessor = httpContextAccessor;
			_memoryCache         = memoryCache;
		}

		// =====================================================================================
		// Static ambient registration
		// =====================================================================================

		/// <summary>
		/// Register static ambient dependencies for this utility class.
		/// Must be called at application startup (e.g., from Program.cs or a startup service)
		/// before any static methods are invoked without an HttpContext.
		/// </summary>
		/// <param name="memoryCache">The application-wide IMemoryCache singleton</param>
		public static void SetAmbient(IMemoryCache memoryCache)
		{
			_ambientCache = memoryCache;
		}

		// =====================================================================================
		// Private service-locator helper
		// BEFORE: HttpContext.Current.Application — HttpApplicationState
		// AFTER:  IMemoryCache from context.RequestServices, falling back to static ambient
		// =====================================================================================

		/// <summary>
		/// Resolves IMemoryCache from the HttpContext's RequestServices (preferred)
		/// or from the static ambient field (background service fallback).
		/// BEFORE: Context.Application — HttpApplicationState indexer access
		/// AFTER:  IMemoryCache from DI container via RequestServices
		/// </summary>
		private static IMemoryCache GetMemoryCache(HttpContext context)
		{
			IMemoryCache cache = context?.RequestServices?.GetService(typeof(IMemoryCache)) as IMemoryCache;
			return cache ?? _ambientCache;
		}

		// =====================================================================================
		// Private helper methods
		// =====================================================================================

		/// <summary>
		/// Searches all messages in the POP3 mailbox by header to find the message with the given Message-ID.
		/// Returns the zero-based message index in the POP3 spool if found, or -1 if not found.
		/// Also sets the <paramref name="headers"/> out-ref to the HeaderList for the matched message.
		///
		/// PRESERVED: Original matching logic from SplendidCRM/_code/PopUtils.cs (checks Contains("Message-ID")).
		/// Note: The original code checks that a Message-ID header exists rather than comparing its value;
		/// this behavior is preserved per the minimal change clause.
		/// </summary>
		/// <param name="pop">An authenticated, connected Pop3Client instance.</param>
		/// <param name="sMessageID">The Message-ID value to locate (used for caller context; see note above).</param>
		/// <param name="headers">Output ref — set to the HeaderList of the matched message when found.</param>
		/// <returns>Zero-based message index, or -1 if not found.</returns>
		private static int FindMessageByMessageID(Pop3Client pop, string sMessageID, ref HeaderList headers)
		{
			// .NET 10 Migration: pop.GetMessageHeaders(startIndex, count) signature unchanged in MailKit 4.x.
			IList<HeaderList> lstHeaders = pop.GetMessageHeaders(0, pop.Count);
			for ( int i = 0; i < lstHeaders.Count; i++ )
			{
				if ( lstHeaders[i].Contains("Message-ID") )
				{
					headers = lstHeaders[i];
					return i;
				}
			}
			return -1;
		}

		// =====================================================================================
		// Public API — POP3 operations
		// All methods are static and accept HttpContext as first parameter (unchanged from source).
		// Application[] access replaced with GetMemoryCache(Context) helper.
		// Session[] access replaced with Context.Session.GetString("key") (ISession).
		// =====================================================================================

		/// <summary>
		/// Validates a POP3 connection by connecting, authenticating, and counting messages.
		/// Appends a localized success message or error message to <paramref name="sbErrors"/>.
		/// Returns true if the connection and authentication succeeded.
		///
		/// BEFORE: Context.Application["CONFIG.default_language"] (HttpApplicationState)
		/// AFTER:  GetMemoryCache(Context).Get&lt;object&gt;("CONFIG.default_language") (IMemoryCache)
		///
		/// BEFORE: Context.Session["USER_SETTINGS/CULTURE"] (HttpSessionState indexer)
		/// AFTER:  Context.Session.GetString("USER_SETTINGS/CULTURE") (ISession)
		///
		/// BEFORE: L10N.Term(Context.Application, sCULTURE, ...) (HttpApplicationState overload)
		/// AFTER:  L10N.Term(GetMemoryCache(Context), sCULTURE, ...) (IMemoryCache overload)
		/// </summary>
		public static bool Validate(HttpContext Context, string sSERVER_URL, int nPORT, bool bMAILBOX_SSL, string sEMAIL_USER, string sEMAIL_PASSWORD, StringBuilder sbErrors)
		{
			bool bValid = false;
			using ( Pop3Client pop = new Pop3Client() )
			{
				try
				{
					pop.Timeout = 60 * 1000; //give pop server 60 seconds to answer
					pop.Connect(sSERVER_URL, nPORT, (bMAILBOX_SSL ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.Auto));
					pop.AuthenticationMechanisms.Remove("XOAUTH2");
					pop.Authenticate(sEMAIL_USER, sEMAIL_PASSWORD);

					int nTotalEmails = pop.Count;
					// 08/09/2018 Paul.  Allow translation of connection success. 
					// .NET 10 Migration: Context.Application["CONFIG.default_language"] → IMemoryCache via service-locator.
					IMemoryCache memoryCache = GetMemoryCache(Context);
					string sCULTURE = Sql.ToString(memoryCache?.Get<object>("CONFIG.default_language"));
					// .NET 10 Migration: Context.Session["USER_SETTINGS/CULTURE"] → Context.Session.GetString("USER_SETTINGS/CULTURE")
					if ( Context?.Session != null )
						sCULTURE = Sql.ToString(Context.Session.GetString("USER_SETTINGS/CULTURE"));
					// .NET 10 Migration: L10N.Term(Context.Application, ...) → L10N.Term(memoryCache, ...)
					sbErrors.AppendLine(String.Format(L10N.Term(memoryCache, sCULTURE, "Users.LBL_CONNECTION_SUCCESSFUL"), nTotalEmails.ToString(), "Inbox"));
					bValid = true;
				}
				catch(Exception ex)
				{
					sbErrors.AppendLine(ex.Message);
				}
			}
			return bValid;
		}

		/// <summary>
		/// Retrieves the POP3 mailbox folder tree as an XmlDocument.
		/// POP3 only exposes a single INBOX folder; returns an XML document with a single Folder element.
		///
		/// PRESERVED: Folder tree structure identical to original — single "INBOX" folder with
		/// TotalCount, UnreadCount, and DisplayName attributes.
		/// </summary>
		public static XmlDocument GetFolderTree(HttpContext Context, string sSERVER_URL, int nPORT, bool bMAILBOX_SSL, string sEMAIL_USER, string sEMAIL_PASSWORD)
		{
			XmlDocument xml = new XmlDocument();
			xml.AppendChild(xml.CreateProcessingInstruction("xml", "version=\"1.0\" encoding=\"UTF-8\""));
			xml.AppendChild(xml.CreateElement("Folders"));
			XmlUtil.SetSingleNodeAttribute(xml, xml.DocumentElement, "DisplayName", "Mailbox - " + sEMAIL_USER);

			using ( Pop3Client pop = new Pop3Client() )
			{
				MailKit.Security.SecureSocketOptions options = MailKit.Security.SecureSocketOptions.Auto;
				if ( bMAILBOX_SSL )
					options = MailKit.Security.SecureSocketOptions.SslOnConnect;
				pop.Timeout = 60 * 1000; //give pop server 60 seconds to answer
				pop.Connect(sSERVER_URL, nPORT, options);
				pop.AuthenticationMechanisms.Remove("XOAUTH2");
				pop.Authenticate(sEMAIL_USER, sEMAIL_PASSWORD);

				int nTotalEmails = pop.Count;
				int nUnreadCount = 0;
				string sMAILBOX = "INBOX";

				XmlElement xChild = xml.CreateElement("Folder");
				xml.DocumentElement.AppendChild(xChild);
				XmlUtil.SetSingleNodeAttribute(xml, xChild, "Id"         , sMAILBOX);
				XmlUtil.SetSingleNodeAttribute(xml, xChild, "TotalCount" , nTotalEmails.ToString());
				XmlUtil.SetSingleNodeAttribute(xml, xChild, "UnreadCount", nUnreadCount.ToString());
				if ( nUnreadCount > 0 )
					XmlUtil.SetSingleNodeAttribute(xml, xChild, "DisplayName", "<b>" + sMAILBOX + "</b> <font color=blue>(" + nUnreadCount.ToString() + ")</font>");
				else
					XmlUtil.SetSingleNodeAttribute(xml, xChild, "DisplayName", sMAILBOX);
			}
			return xml;
		}

		/// <summary>
		/// Gets the total message count and unread count from a POP3 mailbox.
		/// POP3 does not natively track read/unread state; nUnreadCount is always set to 0.
		///
		/// PRESERVED: nUnreadCount always 0 — POP3 has no read/unread concept.
		/// </summary>
		public static void GetFolderCount(HttpContext Context, string sSERVER_URL, int nPORT, bool bMAILBOX_SSL, string sEMAIL_USER, string sEMAIL_PASSWORD, ref int nTotalCount, ref int nUnreadCount)
		{
			using ( Pop3Client pop = new Pop3Client() )
			{
				MailKit.Security.SecureSocketOptions options = MailKit.Security.SecureSocketOptions.Auto;
				if ( bMAILBOX_SSL )
					options = MailKit.Security.SecureSocketOptions.SslOnConnect;
				pop.Timeout = 60 * 1000; //give pop server 60 seconds to answer
				pop.Connect(sSERVER_URL, nPORT, options);
				pop.AuthenticationMechanisms.Remove("XOAUTH2");
				pop.Authenticate(sEMAIL_USER, sEMAIL_PASSWORD);

				nTotalCount  = pop.Count;
				nUnreadCount = 0;
			}
		}

		/// <summary>
		/// Deletes a specific message from the POP3 server identified by its Message-ID.
		/// Connects, authenticates, locates the message by Message-ID header, and marks it for deletion.
		/// The deletion is committed when the connection is closed (POP3 protocol behavior).
		///
		/// PRESERVED: Lookup-and-delete logic identical to original.
		/// </summary>
		public static void DeleteMessage(HttpContext Context, string sSERVER_URL, int nPORT, bool bMAILBOX_SSL, string sEMAIL_USER, string sEMAIL_PASSWORD, string sUNIQUE_ID)
		{
			// 07/18/2010 Paul.  The POP3 Message Number is meaningless as it is only valid for the connection, and we disconnect immediately. 
			// We will use the MessageID as the primary key and we will need to lookup the Message Number in order to delete it. 
			using ( Pop3Client pop = new Pop3Client() )
			{
				pop.Timeout = 60 * 1000; //give pop server 60 seconds to answer
				pop.Connect(sSERVER_URL, nPORT, (bMAILBOX_SSL ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.Auto));
				pop.AuthenticationMechanisms.Remove("XOAUTH2");
				pop.Authenticate(sEMAIL_USER, sEMAIL_PASSWORD);

				HeaderList headers = null;
				int nMessageNumber = FindMessageByMessageID(pop, sUNIQUE_ID, ref headers);
				if ( nMessageNumber >= 0 )
				{
					pop.DeleteMessage(nMessageNumber);
				}
			}
		}

		/// <summary>
		/// Retrieves a single email message from a POP3 server as a DataTable row.
		/// Locates the message by Message-ID, downloads it, and creates a structured DataTable
		/// containing the message metadata and content via MimeUtils.CreateMessageRecord().
		///
		/// BEFORE: pop.GetStream(index) — MailKit pre-4.x one-parameter overload
		/// AFTER:  pop.GetStream(index, false) — MailKit 4.x requires explicit headersOnly=false
		/// </summary>
		public static DataTable GetMessage(HttpContext Context, string sSERVER_URL, int nPORT, bool bMAILBOX_SSL, string sEMAIL_USER, string sEMAIL_PASSWORD, string sUNIQUE_ID)
		{
			DataTable dt = MimeUtils.CreateMessageTable();

			using ( Pop3Client pop = new Pop3Client() )
			{
				pop.Timeout = 60 * 1000; //give pop server 60 seconds to answer
				pop.Connect(sSERVER_URL, nPORT, (bMAILBOX_SSL ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.Auto));
				pop.AuthenticationMechanisms.Remove("XOAUTH2");
				pop.Authenticate(sEMAIL_USER, sEMAIL_PASSWORD);

				HeaderList headers = null;
				int nMessageNumber = FindMessageByMessageID(pop, sUNIQUE_ID, ref headers);
				if ( nMessageNumber >= 0 )
				{
					// .NET 10 Migration: pop.GetStream(nMessageNumber) → pop.GetStream(nMessageNumber, false)
					// MailKit 4.x Pop3Client.GetStream() requires explicit headersOnly parameter.
					// false = retrieve the full message (headers + body), not headers only.
					MimeMessage email = MimeMessage.Load(pop.GetStream(nMessageNumber, false));
					if ( email != null )
					{
						// .NET 10 Migration: GetMessageSize returns int in MailKit 4.x; implicit cast to double.
						double dSize = pop.GetMessageSize(nMessageNumber);
						MimeUtils.CreateMessageRecord(Context, dt, email, dSize);
					}
				}
			}
			return dt;
		}

		/// <summary>
		/// Retrieves the raw binary content of a specific attachment from an email message on a POP3 server.
		/// Locates the message by Message-ID, downloads it, iterates attachments to find the one at
		/// <paramref name="nATTACHMENT_ID"/> (zero-based index), and extracts the decoded binary data.
		///
		/// BEFORE: part.ContentObject.DecodeTo(mem) — MimeKit pre-4.x API
		/// AFTER:  part.Content.DecodeTo(mem) — ContentObject renamed to Content in MimeKit 4.x
		///
		/// BEFORE: pop.GetStream(index) — MailKit pre-4.x one-parameter overload
		/// AFTER:  pop.GetStream(index, false) — MailKit 4.x requires explicit headersOnly=false
		/// </summary>
		public static byte[] GetAttachmentData(HttpContext Context, string sSERVER_URL, int nPORT, bool bMAILBOX_SSL, string sEMAIL_USER, string sEMAIL_PASSWORD, string sUNIQUE_ID, int nATTACHMENT_ID, ref string sFILENAME, ref string sCONTENT_TYPE, ref bool bINLINE)
		{
			byte[] byDataBinary = null;
			using ( Pop3Client pop = new Pop3Client() )
			{
				pop.Timeout = 60 * 1000; //give pop server 60 seconds to answer
				pop.Connect(sSERVER_URL, nPORT, (bMAILBOX_SSL ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.Auto));
				pop.AuthenticationMechanisms.Remove("XOAUTH2");
				pop.Authenticate(sEMAIL_USER, sEMAIL_PASSWORD);

				HeaderList headers = null;
				int nMessageNumber = FindMessageByMessageID(pop, sUNIQUE_ID, ref headers);
				if ( nMessageNumber >= 0 )
				{
					// .NET 10 Migration: pop.GetStream(index, false) — MailKit 4.x API
					MimeMessage email = MimeMessage.Load(pop.GetStream(nMessageNumber, false));
					if ( email != null )
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
										sCONTENT_TYPE = att.ContentType.MediaType;
										if ( att.ContentDisposition != null && att.ContentDisposition.FileName != null )
										{
											sFILENAME = Path.GetFileName(att.ContentDisposition.FileName);
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
												// .NET 10 Migration: part.ContentObject.DecodeTo(mem) → part.Content.DecodeTo(mem)
												// ContentObject property was deprecated and removed in MimeKit 4.x.
												// Content is the direct replacement with identical DecodeTo() semantics.
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
			}
			return byDataBinary;
		}

		/// <summary>
		/// Retrieves all messages from a POP3 mailbox INBOX as a DataTable.
		/// Downloads all messages in the spool, creates a DataRow per message via
		/// MimeUtils.CreateMessageRecord(), and returns the populated DataTable.
		///
		/// BEFORE: pop.GetStreams(0, pop.Count) — MailKit pre-4.x two-parameter overload
		/// AFTER:  pop.GetStreams(0, pop.Count, false) — MailKit 4.x requires explicit headersOnly=false
		/// </summary>
		public static DataTable GetFolderMessages(HttpContext Context, string sSERVER_URL, int nPORT, bool bMAILBOX_SSL, string sEMAIL_USER, string sEMAIL_PASSWORD)
		{
			DataTable dt = MimeUtils.CreateMessageTable();

			using ( Pop3Client pop = new Pop3Client() )
			{
				pop.Timeout = 60 * 1000; //give pop server 60 seconds to answer
				pop.Connect(sSERVER_URL, nPORT, (bMAILBOX_SSL ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.Auto));
				pop.AuthenticationMechanisms.Remove("XOAUTH2");
				pop.Authenticate(sEMAIL_USER, sEMAIL_PASSWORD);

				int nMessageIndex = 0;
				// .NET 10 Migration: GetMessageSizes() returns IList<int> in MailKit 4.x; same as before.
				IList<int> lstMessageSizes = pop.GetMessageSizes();
				// .NET 10 Migration: pop.GetStreams(0, pop.Count) → pop.GetStreams(0, pop.Count, false)
				// MailKit 4.x Pop3Client.GetStreams() requires explicit headersOnly parameter.
				// false = retrieve the full message content, not headers only.
				IList<Stream> lstMessages = pop.GetStreams(0, pop.Count, false);
				foreach ( Stream stm in lstMessages )
				{
					MimeMessage email = MimeMessage.Load(stm);
					if ( email != null )
					{
						// .NET 10 Migration: lstMessageSizes[nMessageIndex] is int; implicit cast to double.
						DataRow row = MimeUtils.CreateMessageRecord(Context, dt, email, lstMessageSizes[nMessageIndex]);
					}
					nMessageIndex++;
				}
			}
			return dt;
		}

		/// <summary>
		/// Imports a specific email message from a POP3 server into the SplendidCRM database.
		/// Locates the message by Message-ID, downloads it, and persists it via MimeUtils.ImportMessage().
		/// Returns the new Email record GUID on success, or Guid.Empty on failure.
		///
		/// .NET 10 MIGRATION NOTE: HttpSessionState Session parameter has been removed.
		///   The Session parameter (from System.Web.SessionState) is not available in .NET 10.
		///   Session state is accessed through Context.Session (ISession) when needed.
		///   All callers must update their call sites to remove the Session argument.
		///
		/// BEFORE: pop.GetStream(index) — MailKit pre-4.x one-parameter overload
		/// AFTER:  pop.GetStream(index, false) — MailKit 4.x requires explicit headersOnly=false
		/// </summary>
		public static Guid ImportMessage(HttpContext Context, string sPARENT_TYPE, Guid gPARENT_ID, string sSERVER_URL, int nPORT, bool bMAILBOX_SSL, string sEMAIL_USER, string sEMAIL_PASSWORD, Guid gUSER_ID, Guid gASSIGNED_USER_ID, Guid gTEAM_ID, string sTEAM_SET_LIST, string sUNIQUE_ID)
		{
			Guid gEMAIL_ID = Guid.Empty;

			using ( Pop3Client pop = new Pop3Client() )
			{
				pop.Timeout = 60 * 1000; //give pop server 60 seconds to answer
				pop.Connect(sSERVER_URL, nPORT, (bMAILBOX_SSL ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.Auto));
				pop.AuthenticationMechanisms.Remove("XOAUTH2");
				pop.Authenticate(sEMAIL_USER, sEMAIL_PASSWORD);

				HeaderList headers = null;
				int nMessageNumber = FindMessageByMessageID(pop, sUNIQUE_ID, ref headers);
				if ( nMessageNumber >= 0 )
				{
					MimeKit.MimeMessage email = null;
					bool bLoadSuccessful = false;
					try
					{
						// .NET 10 Migration: pop.GetStream(index, false) — MailKit 4.x API
						email = MimeKit.MimeMessage.Load(pop.GetStream(nMessageNumber, false));
						bLoadSuccessful = true;
					}
					catch(Exception ex)
					{
						string sError = "Error loading email for " + sEMAIL_USER + ", " + sUNIQUE_ID + "." + ControlChars.CrLf;
						sError += Utils.ExpandException(ex) + ControlChars.CrLf;
						SyncError.SystemMessage(Context, "Error", new StackTrace(true).GetFrame(0), sError);
					}
					if ( email != null && bLoadSuccessful )
					{
						gEMAIL_ID = MimeUtils.ImportMessage(Context, sPARENT_TYPE, gPARENT_ID, gUSER_ID, gASSIGNED_USER_ID, gTEAM_ID, sTEAM_SET_LIST, sUNIQUE_ID, email);
					}
				}
				else
				{
					string sError = "Error loading email for " + sEMAIL_USER + ", " + sUNIQUE_ID + "." + ControlChars.CrLf;
					throw(new Exception(sError));
				}
			}
			return gEMAIL_ID;
		}
	}
}
