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
// .NET 10 Migration: SplendidCRM/_code/SignalR/SplendidHubAuthorize.cs → src/SplendidCRM.Web/SignalR/SplendidHubAuthorize.cs
// Changes applied:
//   - REMOVED: using System.Web;                    → ADDED: using Microsoft.AspNetCore.Http;
//   - REMOVED: using System.Web.SessionState;       (HttpSessionState replaced by ISession in ASP.NET Core)
//   - REMOVED: using Microsoft.AspNet.SignalR;      → ADDED: using Microsoft.AspNetCore.SignalR;
//   - REMOVED: using Microsoft.AspNet.SignalR.Hubs; (HubDescriptor, IHubIncomingInvokerContext not available in ASP.NET Core)
//   - ADDED:   using System.Collections.Concurrent; (ConcurrentDictionary replaces Dictionary for thread safety)
//   - ADDED:   using System.Threading.Tasks;        (ValueTask<object?>, Task for IHubFilter)
//   - SplendidSession.dictSessions: Dictionary<string, SplendidSession>
//     → ConcurrentDictionary<string, SplendidSession> (static readonly initialization)
//     REASON: ASP.NET Core processes requests more concurrently than IIS InProc session pipeline,
//     making the original non-thread-safe Dictionary unsafe for concurrent reads and writes.
//   - SplendidSession.CreateSession: HttpSessionState Session → ISession session, string sessionId
//     REASON: HttpSessionState does not exist in ASP.NET Core. ISession is the distributed-session
//     equivalent. Session.SessionID is not exposed by ISession; sessionId is passed separately from
//     HttpContext.Session.Id at the call site.
//   - SplendidSession.CreateSession: Session["key"] indexer → session.GetString("key")
//     (ASP.NET Core distributed session uses GetString/SetString, not object indexer)
//   - SplendidSession.CreateSession: Session.Timeout removed; nSessionTimeout=20 default preserved
//     (ISession does not expose Timeout; 20 minutes matches original Web.config sessionState timeout)
//   - SplendidSession.PurgeOldSessions: HttpContext Context → HttpContext context (lowercase convention)
//   - SplendidHubAuthorize: Attribute, IAuthorizeHubConnection, IAuthorizeHubMethodInvocation → IHubFilter
//     (OWIN SignalR 1.x authorization attribute replaced by ASP.NET Core SignalR IHubFilter)
//   - REMOVED: AuthorizeHubConnection(HubDescriptor, IRequest) → REPLACED: OnConnectedAsync(HubLifetimeContext, Func)
//   - REMOVED: AuthorizeHubMethodInvocation(IHubIncomingInvokerContext, bool) → REPLACED: InvokeMethodAsync(HubInvocationContext, Func)
//   - Cookie access: Microsoft.AspNet.SignalR.Cookie → Microsoft.AspNetCore.Http.IRequestCookieCollection
//     via HubCallerContext.GetHttpContext().Request.Cookies[cookieName]
//   - Cookie lookup: checks both "ASP.NET_SessionId" (backward compat) and ".AspNetCore.Session" (ASP.NET Core default)
//   - PRESERVED: Session-based authorization logic (cookie → SplendidSession.GetSession → allow/deny)
//   - PRESERVED: 20-minute session timeout default
//   - PRESERVED: USER_ID and USER_NAME session fields
//   - PRESERVED: Expiration-based session cleanup in PurgeOldSessions
//   - PRESERVED: Separate list pattern for safe dictionary iteration during purge
//   - PRESERVED: All Paul's date-stamped comments
#nullable disable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;

namespace SplendidCRM
{
	/// <summary>
	/// In-memory session tracking dictionary for SignalR hub authorization.
	/// Maintained by the SignalR hub filter and refreshed on each authenticated request.
	///
	/// Migrated from SplendidCRM/_code/SignalR/SplendidHubAuthorize.cs for .NET 10 ASP.NET Core.
	///
	/// .NET 10 Migration: The original Dictionary&lt;string, SplendidSession&gt; has been replaced with
	/// ConcurrentDictionary&lt;string, SplendidSession&gt; to support ASP.NET Core's higher concurrency.
	/// The original code relied on IIS InProc session pipeline serialization for thread safety;
	/// ASP.NET Core's Kestrel pipeline has no such serialization guarantee.
	/// </summary>
	public class SplendidSession
	{
		private static int nSessionTimeout = 20;
		// 11/16/2014 Paul.  Using a local session variable means that this system will not work on a web farm unless sticky sessions are used. 
		// The alternative is to use the Claims approach of OWIN, but that system seems to be CPU intensive with all the encrypting and decrypting of the claim data. 
		// The claim data is just an encrypted package of non-sensitive user information, such as User ID, User Name and Email. 
		// The claim data is effectively session data that is encrypted and stored in a cookie. 
		// .NET 10 Migration: Dictionary<string, SplendidSession> replaced with ConcurrentDictionary<string, SplendidSession>.
		// ConcurrentDictionary is initialized as static readonly (no lazy null-check initialization needed).
		// All mutation operations use TryGetValue, TryRemove, and GetOrAdd for lock-free thread safety.
		private static readonly ConcurrentDictionary<string, SplendidSession> dictSessions
			= new ConcurrentDictionary<string, SplendidSession>(StringComparer.Ordinal);

		public DateTime Expiration;
		public Guid     USER_ID   ;
		public string   USER_NAME ;

		/// <summary>
		/// Creates or updates the SplendidSession entry for the given ASP.NET Core session.
		/// Call this from hub OnConnectedAsync and from authenticated request handlers to register
		/// or refresh the session in the hub authorization dictionary.
		///
		/// .NET 10 Migration: HttpSessionState Session → ISession session, string sessionId
		///   - ISession is the ASP.NET Core distributed-session equivalent of HttpSessionState.
		///   - sessionId is passed separately because ISession does not expose a SessionID property.
		///     Obtain it from HttpContext.Session.Id at the call site.
		///   - session.GetString("USER_ID") replaces Session["USER_ID"] indexer (object indexer replaced
		///     by typed string accessor in distributed session API).
		///   - Session.Timeout is not available on ISession; nSessionTimeout=20 default is preserved
		///     to match the original Web.config &lt;sessionState timeout="20"/&gt; setting.
		/// </summary>
		/// <param name="session">ASP.NET Core ISession (from HttpContext.Session)</param>
		/// <param name="sessionId">Session identifier string (from HttpContext.Session.Id)</param>
		public static void CreateSession(ISession session, string sessionId)
		{
			if ( session != null && !string.IsNullOrEmpty(sessionId) )
			{
				// .NET 10 Migration: Sql.ToGuid(session.GetString("USER_ID")) replaces Sql.ToGuid(Session["USER_ID"]).
				// ISession.GetString() returns string or null; Sql.ToGuid() handles both cases.
				Guid gUSER_ID = Sql.ToGuid(session.GetString("USER_ID"));
				if ( !Sql.IsEmptyGuid(gUSER_ID) )
				{
					// .NET 10 Migration: ConcurrentDictionary.GetOrAdd() provides atomic thread-safe
					// creation of a new SplendidSession entry when the sessionId key does not yet exist.
					// If the key already exists, the factory delegate is not called; the existing
					// SplendidSession instance is returned and its properties are refreshed below.
					SplendidSession ss = dictSessions.GetOrAdd(sessionId, _ => new SplendidSession());
					ss.Expiration = DateTime.Now.AddMinutes(nSessionTimeout);
					ss.USER_ID    = gUSER_ID;
					// .NET 10 Migration: Sql.ToString(session.GetString("USER_NAME")) replaces Sql.ToString(Session["USER_NAME"]).
					// session.GetString() returns string or null; Sql.ToString() converts null to empty string.
					ss.USER_NAME  = Sql.ToString(session.GetString("USER_NAME"));
				}
				else
				{
					// User is not authenticated — remove any stale session entry for this session ID.
					// .NET 10 Migration: ConcurrentDictionary.TryRemove replaces Dictionary.Remove.
					dictSessions.TryRemove(sessionId, out _);
				}
			}
		}

		/// <summary>
		/// Retrieves the SplendidSession for the given session ID, validating that it has not expired.
		/// Returns null if the session is not found or has expired. Expired sessions are automatically
		/// removed from the dictionary on access.
		/// </summary>
		/// <param name="sSessionID">Session identifier string (typically from request cookie value)</param>
		/// <returns>Valid SplendidSession, or null if not found or expired</returns>
		public static SplendidSession GetSession(string sSessionID)
		{
			SplendidSession ss = null;
			// .NET 10 Migration: ConcurrentDictionary.TryGetValue replaces ContainsKey + indexer read.
			// TryGetValue is a single atomic operation, avoiding the TOCTOU race of ContainsKey + Read.
			if ( dictSessions.TryGetValue(sSessionID, out ss) )
			{
				if ( ss.Expiration < DateTime.Now )
				{
					// Session has expired — remove it and return null to deny access.
					// .NET 10 Migration: ConcurrentDictionary.TryRemove replaces Dictionary.Remove.
					dictSessions.TryRemove(sSessionID, out _);
					ss = null;
				}
			}
			return ss;
		}

		/// <summary>
		/// Removes all expired session entries from the in-memory session dictionary.
		/// Called periodically by the scheduler or hub cleanup logic to prevent unbounded memory growth.
		///
		/// .NET 10 Migration: HttpContext Context → HttpContext context (lowercase parameter name convention).
		/// The original SplendidError.SystemMessage(Context, ...) call is preserved using the lowercase parameter.
		/// The separate-list iteration pattern is preserved: although ConcurrentDictionary supports
		/// concurrent modification during enumeration, collecting keys first avoids any potential
		/// per-element TryRemove failures due to concurrent insertions.
		/// </summary>
		/// <param name="context">HttpContext for error logging (passed to SplendidError.SystemMessage)</param>
		public static void PurgeOldSessions(HttpContext context)
		{
			try
			{
				DateTime dtNow = DateTime.Now;
				// 11/16/2014 Paul.  We cannot use foreach to remove items from a dictionary, so use a separate list. 
				// .NET 10 Migration: Iterating ConcurrentDictionary<K,V> as KeyValuePair<K,V> is safe;
				// we collect keys first to separate enumeration from removal for clarity and parity with original.
				List<string> arrSessions = new List<string>();
				foreach ( KeyValuePair<string, SplendidSession> kvp in dictSessions )
					arrSessions.Add(kvp.Key);
				foreach ( string sSessionID in arrSessions )
				{
					// .NET 10 Migration: ConcurrentDictionary.TryGetValue replaces Dictionary indexer read.
					// TryGetValue handles the case where another thread may have already removed the entry.
					if ( dictSessions.TryGetValue(sSessionID, out SplendidSession ss) )
					{
						if ( ss.Expiration < dtNow )
							dictSessions.TryRemove(sSessionID, out _);
					}
				}
			}
			catch(Exception ex)
			{
				SplendidError.SystemMessage(context, "Error", new StackTrace(true).GetFrame(0), ex);
			}
		}
	}

	// http://eworldproblems.mbaynton.com/2012/12/signalr-hub-authorization/
	/// <summary>
	/// Hub authorization filter for ASP.NET Core SignalR.
	///
	/// .NET 10 Migration: The original class implemented two OWIN SignalR 1.x authorization interfaces:
	///   [SplendidHubAuthorize] Attribute + IAuthorizeHubConnection + IAuthorizeHubMethodInvocation
	/// Converted to ASP.NET Core SignalR IHubFilter providing:
	///   - OnConnectedAsync: connection-level authorization (replacing IAuthorizeHubConnection)
	///   - InvokeMethodAsync: method-level authorization (replacing IAuthorizeHubMethodInvocation)
	///
	/// Authorization logic preserved: reads ASP.NET_SessionId / .AspNetCore.Session cookie from the
	/// hub connection's HttpContext, looks up the session in SplendidSession.GetSession(), and either
	/// allows the operation to proceed (next()) or throws HubException("Unauthorized").
	///
	/// Registration in Program.cs (global filter applies to ALL hubs):
	///   builder.Services.AddSignalR(options =>
	///   {
	///       options.AddFilter&lt;SplendidHubAuthorize&gt;();
	///   });
	/// OR per-hub via attribute:
	///   [ServiceFilter(typeof(SplendidHubAuthorize))]
	///   public class ChatManagerHub : Hub { ... }
	///
	/// Cookie names checked (both for backward compatibility):
	///   - "ASP.NET_SessionId"   — legacy ASP.NET session cookie name (backward compatibility with existing clients)
	///   - ".AspNetCore.Session" — ASP.NET Core default session cookie name
	/// </summary>
	public class SplendidHubAuthorize : IHubFilter
	{
		/// <summary>
		/// Called for every hub method invocation. Validates that the caller has a valid, non-expired
		/// SplendidSession before allowing the hub method to execute.
		///
		/// .NET 10 Migration: Replaces AuthorizeHubMethodInvocation(IHubIncomingInvokerContext, bool appliesToMethod)
		/// from OWIN SignalR 1.x IAuthorizeHubMethodInvocation interface.
		///
		/// Cookie access change:
		///   BEFORE: hubIncomingInvokerContext.Hub.Context.RequestCookies["ASP.NET_SessionId"] → Cookie
		///   AFTER:  invocationContext.Context.GetHttpContext().Request.Cookies["ASP.NET_SessionId"] → string
		/// </summary>
		/// <param name="invocationContext">Context for the hub method being invoked, provides access to HubCallerContext</param>
		/// <param name="next">Delegate to the next filter or hub method in the pipeline</param>
		/// <returns>The return value of the hub method, or throws HubException if unauthorized</returns>
		public async ValueTask<object> InvokeMethodAsync(
			HubInvocationContext invocationContext,
			Func<HubInvocationContext, ValueTask<object>> next)
		{
			// 11/14/2014 Paul.  SignalR 1.0 does not have access to the ASP.NET Pipeline, so we cannot identify the user. 
			// .NET 10 Migration: ASP.NET Core SignalR provides access to HttpContext via the GetHttpContext() extension method
			// on HubCallerContext, enabling cookie-based session validation within the hub filter pipeline.
			SplendidSession ss = null;
			HttpContext httpContext = invocationContext.Context.GetHttpContext();
			if ( httpContext != null )
			{
				// Check both legacy ASP.NET_SessionId cookie name (backward compatibility with existing browser
				// clients that may still have the old cookie) and the ASP.NET Core default session cookie name.
				// .NET 10 Migration: Microsoft.AspNet.SignalR.Cookie type replaced by IRequestCookieCollection string indexer.
				string sSessionID = httpContext.Request.Cookies["ASP.NET_SessionId"]
				                 ?? httpContext.Request.Cookies[".AspNetCore.Session"];
				if ( !string.IsNullOrEmpty(sSessionID) )
					ss = SplendidSession.GetSession(sSessionID);
			}
			if ( ss != null )
				return await next(invocationContext);
			throw new HubException("Unauthorized");
		}

		/// <summary>
		/// Called when a client establishes a connection to the hub. Validates that the connecting client
		/// has a valid, non-expired SplendidSession before allowing the connection to proceed.
		///
		/// .NET 10 Migration: Replaces AuthorizeHubConnection(HubDescriptor hubDescriptor, IRequest request)
		/// from OWIN SignalR 1.x IAuthorizeHubConnection interface.
		///
		/// Cookie access change:
		///   BEFORE: request.Cookies["ASP.NET_SessionId"] → Cookie.Value
		///   AFTER:  context.Context.GetHttpContext().Request.Cookies["ASP.NET_SessionId"] → string
		/// </summary>
		/// <param name="context">Lifetime context for the hub connection, provides access to HubCallerContext</param>
		/// <param name="next">Delegate to proceed with the connection if authorized</param>
		public async Task OnConnectedAsync(
			HubLifetimeContext context,
			Func<HubLifetimeContext, Task> next)
		{
			// 11/14/2014 Paul.  SignalR 1.0 does not have access to the ASP.NET Pipeline, so we cannot identify the user. 
			// .NET 10 Migration: ASP.NET Core SignalR provides access to HttpContext via the GetHttpContext() extension method
			// on HubCallerContext, enabling cookie-based session validation at connection time.
			SplendidSession ss = null;
			HttpContext httpContext = context.Context.GetHttpContext();
			if ( httpContext != null )
			{
				// Check both legacy ASP.NET_SessionId cookie name (backward compatibility) and
				// the ASP.NET Core default session cookie name (.AspNetCore.Session).
				// .NET 10 Migration: Microsoft.AspNet.SignalR.Cookie type replaced by IRequestCookieCollection string indexer.
				string sSessionID = httpContext.Request.Cookies["ASP.NET_SessionId"]
				                 ?? httpContext.Request.Cookies[".AspNetCore.Session"];
				if ( !string.IsNullOrEmpty(sSessionID) )
					ss = SplendidSession.GetSession(sSessionID);
			}
			if ( ss != null )
				await next(context);
			else
				throw new HubException("Unauthorized");
		}
	}
}
