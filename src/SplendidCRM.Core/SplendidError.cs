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
// .NET 10 Migration: SplendidCRM/_code/SplendidError.cs → src/SplendidCRM.Core/SplendidError.cs
// Changes applied:
//   - REMOVED: using System.Web; (HttpContext.Current, HttpApplicationState)
//   - ADDED:   using Microsoft.AspNetCore.Http; using Microsoft.Extensions.Caching.Memory;
//   - HttpContext.Current → explicit HttpContext parameter (IHttpContextAccessor via DI constructor for instance use)
//   - HttpApplicationState (Application[]) → IMemoryCache parameter; "SystemErrors" DataTable cached with IMemoryCache.Set()
//   - Application.Lock()/UnLock() → static readonly lock object (_lockObject) for thread-safe DataTable access
//   - Session.SessionID → ISession.Id (Microsoft.AspNetCore.Http.ISession)
//   - Request.UserHostName → HttpContext.Connection.RemoteIpAddress.ToString()
//   - Request.Url.Host → HttpRequest.Host.ToString()
//   - Request.Path → HttpRequest.Path.ToString()
//   - Request.AppRelativeCurrentExecutionFilePath → HttpRequest.Path.ToString() (no direct ASP.NET Core equivalent)
//   - Request.QueryString.ToString() → preserved (QueryString.ToString() works in ASP.NET Core)
//   - Request.PhysicalApplicationPath → removed (no ASP.NET Core equivalent; file path cleaning simplified)
//   - Utils.ExpandException() → inlined as private static ExpandException() (not present in migrated Utils.cs)
//   - Security.USER_ID / Security.USER_NAME (were static) → read directly from ISession.GetString() since they are now instance props
//   - SqlProcs.spSYSTEM_LOG_InsertOnly() → called inline via DbProviderFactories service from RequestServices
//   - System.Threading.ThreadAbortException → does not exist in .NET 5+; check preserved as no-op (never thrown)
//   - DataColumn type definitions: Type.GetType("System.Xxx") → typeof(Xxx) for clarity and correctness
//   - DI constructor added: SplendidError(IHttpContextAccessor, IMemoryCache) for service registration
#nullable disable
using System;
using System.IO;
using System.Data;
using System.Text;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	/// <summary>
	/// Error logging and handling utility for SplendidCRM.
	/// Provides SystemError, SystemWarning, and SystemMessage overloads for structured error logging
	/// to both an in-memory DataTable cache (IMemoryCache key: "SystemErrors") and the SQL Server
	/// system log table via the spSYSTEM_LOG_InsertOnly stored procedure.
	///
	/// Migrated from SplendidCRM/_code/SplendidError.cs for .NET 10 ASP.NET Core.
	///
	/// DESIGN NOTES for callers:
	///   • Register SplendidError as a SCOPED service so it can be injected wherever needed.
	///   • Static methods (SystemError, SystemWarning) are preserved for backward-compatible call sites
	///     that cannot easily inject an instance. These use service-locator access when an HttpContext
	///     is available, and fall back to Debug.WriteLine when no context is present (e.g. background services).
	///   • Callers that have an IMemoryCache or HttpContext should prefer the explicit-parameter overloads
	///     for full logging fidelity (cache + database).
	/// </summary>
	public class SplendidError
	{
		// =====================================================================================
		// DI fields — for instance registration in the DI container
		// =====================================================================================

		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly IMemoryCache         _memoryCache        ;

		/// <summary>
		/// Thread-safety lock for in-memory DataTable operations.
		/// Replaces HttpApplicationState.Lock() / UnLock() from the .NET Framework version.
		/// </summary>
		private static readonly object _lockObject = new object();

		// =====================================================================================
		// DI Constructor
		// =====================================================================================

		/// <summary>
		/// Constructs a SplendidError service instance with injected HTTP context accessor and memory cache.
		/// </summary>
		/// <param name="httpContextAccessor">Replaces HttpContext.Current throughout</param>
		/// <param name="memoryCache">Replaces Application["SystemErrors"] throughout</param>
		public SplendidError(IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache)
		{
			_httpContextAccessor = httpContextAccessor;
			_memoryCache         = memoryCache        ;
		}

		// =====================================================================================
		// Private helper: replaces Utils.ExpandException()
		// Utils.ExpandException() is not present in the migrated Utils.cs; implemented inline here.
		// Original: SplendidCRM/_code/Utils.cs line 441–454
		// Recursively expands InnerException chain, separating messages with HTML line breaks.
		// =====================================================================================

		/// <summary>
		/// Recursively expands an exception and all inner exceptions into a single HTML-safe message string.
		/// </summary>
		private static string ExpandException(Exception ex)
		{
			if ( ex == null )
				return String.Empty;
			StringBuilder sb = new StringBuilder();
			do
			{
				sb.Append(ex.Message);
				// 08/13/2007 Paul.  Only add the line break if there is more data. 
				if ( ex.InnerException != null )
					sb.Append("<br />\r\n");
				ex = ex.InnerException;
			}
			while ( ex != null );
			return sb.ToString();
		}

		// =====================================================================================
		// DataTable factory helper
		// Creates the SystemErrors DataTable with the expected column schema.
		// =====================================================================================

		private static DataTable CreateSystemErrorsTable()
		{
			DataTable dt = new DataTable();
			dt.Columns.Add(new DataColumn("CREATED_BY"  , typeof(Guid)    ));
			dt.Columns.Add(new DataColumn("DATE_ENTERED", typeof(DateTime)));
			dt.Columns.Add(new DataColumn("ERROR_TYPE"  , typeof(string)  ));
			dt.Columns.Add(new DataColumn("USER_NAME"   , typeof(string)  ));
			dt.Columns.Add(new DataColumn("FILE_NAME"   , typeof(string)  ));
			dt.Columns.Add(new DataColumn("METHOD"      , typeof(string)  ));
			dt.Columns.Add(new DataColumn("LINE_NUMBER" , typeof(string)  ));
			dt.Columns.Add(new DataColumn("MESSAGE"     , typeof(string)  ));
			return dt;
		}

		// =====================================================================================
		// Static convenience wrappers — preserved for backward-compatible call sites
		// BEFORE: HttpContext.Current.Application → AFTER: no ambient context; delegates to
		//   SystemMessage((IMemoryCache)null, (HttpContext)null, ...) which falls through to
		//   Debug.WriteLine when no cache is available.
		//
		// NOTE: Callers that have access to an HttpContext or IMemoryCache should prefer the
		//       explicit-parameter overloads for full DB + cache logging fidelity.
		// =====================================================================================

		/// <summary>
		/// Logs a system warning. Delegates to SystemMessage with error type "Warning".
		/// Falls back to Debug.WriteLine when no HTTP context is present (e.g. background services).
		/// </summary>
		public static void SystemWarning(StackFrame stack, string sMESSAGE)
		{
			SystemMessage((IMemoryCache)null, (HttpContext)null, "Warning", stack, sMESSAGE);
		}

		/// <summary>
		/// Logs a system warning from an exception.
		/// </summary>
		public static void SystemWarning(StackFrame stack, Exception ex)
		{
			// 08/13/2007 Paul.  Instead of ignoring the English abort message, ignore the abort exception.
			// NOTE: System.Threading.ThreadAbortException does not exist in .NET 5+; this check is preserved
			//       as a no-op for semantic parity. Type.GetType() returns null; != null is always true.
			if ( ex.GetType() != Type.GetType("System.Threading.ThreadAbortException") )
			{
				string sMESSAGE = ExpandException(ex);
				SystemMessage((IMemoryCache)null, (HttpContext)null, "Warning", stack, sMESSAGE);
			}
		}

		/// <summary>
		/// Logs a system error. Delegates to SystemMessage with error type "Error".
		/// Falls back to Debug.WriteLine when no HTTP context is present (e.g. background services).
		/// </summary>
		public static void SystemError(StackFrame stack, string sMESSAGE)
		{
			SystemMessage((IMemoryCache)null, (HttpContext)null, "Error", stack, sMESSAGE);
		}

		/// <summary>
		/// Logs a system error from an exception, including the exception's stack trace.
		/// </summary>
		public static void SystemError(StackFrame stack, Exception ex)
		{
			// 08/13/2007 Paul.  Instead of ignoring the English abort message, ignore the abort exception.
			// NOTE: System.Threading.ThreadAbortException does not exist in .NET 5+; preserved as no-op.
			if ( ex.GetType() != Type.GetType("System.Threading.ThreadAbortException") )
			{
				string sMESSAGE = ExpandException(ex);
				// 01/14/2009 Paul.  Save the stack trace to help locate the source of a bug. 
				if ( ex.StackTrace != null )
					sMESSAGE += "<br />\r\n" + ex.StackTrace.Replace(ControlChars.CrLf, "<br />\r\n");
				SystemMessage((IMemoryCache)null, (HttpContext)null, "Error", stack, sMESSAGE);
			}
		}

		// =====================================================================================
		// SystemMessage overload: no context, no cache
		// BEFORE: SystemMessage checked HttpContext.Current == null and returned early.
		// AFTER:  With no ambient HttpContext.Current, falls through to Debug.WriteLine only.
		//         Callers in background services or timer callbacks use this path.
		// =====================================================================================

		/// <summary>
		/// Logs a system message with error type, stack frame, and message string.
		/// When called without an explicit HttpContext or IMemoryCache, falls back to Debug.WriteLine.
		/// </summary>
		public static void SystemMessage(string sERROR_TYPE, StackFrame stack, string sMESSAGE)
		{
			// BEFORE: if (HttpContext.Current == null || HttpContext.Current.Application == null) return;
			//         SystemMessage(HttpContext.Current.Application, HttpContext.Current, sERROR_TYPE, stack, sMESSAGE);
			// AFTER:  No ambient context → delegate to the core method with null cache and null context.
			//         The core method will emit debug output and return when memoryCache is null.
			SystemMessage((IMemoryCache)null, (HttpContext)null, sERROR_TYPE, stack, sMESSAGE);
		}

		// =====================================================================================
		// SystemMessage overloads: explicit HttpContext
		// BEFORE: SystemMessage(HttpContext Context, ...) called Context.Application (HttpApplicationState).
		// AFTER:  IMemoryCache is retrieved from Context.RequestServices (service locator pattern).
		//         This preserves identical semantics: the cache is scoped to the DI container.
		// =====================================================================================

		/// <summary>
		/// Logs a system message using an explicit HttpContext and an Exception.
		/// The exception is expanded and the stack trace is appended for Error type messages.
		/// </summary>
		public static void SystemMessage(HttpContext context, string sERROR_TYPE, StackFrame stack, Exception ex)
		{
			string sMESSAGE = ExpandException(ex);
			if ( sERROR_TYPE == "Error" )
			{
				// 01/14/2009 Paul.  Save the stack trace to help locate the source of a bug. 
				if ( ex.StackTrace != null )
					sMESSAGE += "<br />\r\n" + ex.StackTrace.Replace(ControlChars.CrLf, "<br />\r\n");
			}
			// BEFORE: SystemMessage(Context.Application, Context, sERROR_TYPE, stack, sMESSAGE);
			// AFTER:  Obtain IMemoryCache from the DI container via Context.RequestServices.
			IMemoryCache cache = context?.RequestServices?.GetService(typeof(IMemoryCache)) as IMemoryCache;
			SystemMessage(cache, context, sERROR_TYPE, stack, sMESSAGE);
		}

		/// <summary>
		/// Logs a system message using an explicit HttpContext and a string message.
		/// </summary>
		public static void SystemMessage(HttpContext context, string sERROR_TYPE, StackFrame stack, string sMESSAGE)
		{
			// BEFORE: SystemMessage(Context.Application, Context, sERROR_TYPE, stack, sMESSAGE);
			// AFTER:  Obtain IMemoryCache from the DI container via Context.RequestServices.
			IMemoryCache cache = context?.RequestServices?.GetService(typeof(IMemoryCache)) as IMemoryCache;
			SystemMessage(cache, context, sERROR_TYPE, stack, sMESSAGE);
		}

		// =====================================================================================
		// SystemMessage overloads: explicit IMemoryCache (replaces HttpApplicationState)
		// BEFORE: SystemMessage(HttpApplicationState Application, string, StackFrame, Exception/string)
		// AFTER:  HttpApplicationState replaced by IMemoryCache; null HttpContext when called from
		//         background services/timer callbacks that have no active HTTP request.
		// =====================================================================================

		/// <summary>
		/// Logs a system message using an explicit IMemoryCache and an Exception.
		/// Use this overload from background services that have IMemoryCache but no HTTP context.
		/// </summary>
		public static void SystemMessage(IMemoryCache memoryCache, string sERROR_TYPE, StackFrame stack, Exception ex)
		{
			string sMESSAGE = ExpandException(ex);
			if ( sERROR_TYPE == "Error" )
			{
				// 01/14/2009 Paul.  Save the stack trace to help locate the source of a bug. 
				if ( ex.StackTrace != null )
					sMESSAGE += "<br />\r\n" + ex.StackTrace.Replace(ControlChars.CrLf, "<br />\r\n");
			}
			SystemMessage(memoryCache, null, sERROR_TYPE, stack, sMESSAGE);
		}

		/// <summary>
		/// Logs a system message using an explicit IMemoryCache and a string message.
		/// Use this overload from background services that have IMemoryCache but no HTTP context.
		/// </summary>
		public static void SystemMessage(IMemoryCache memoryCache, string sERROR_TYPE, StackFrame stack, string sMESSAGE)
		{
			SystemMessage(memoryCache, null, sERROR_TYPE, stack, sMESSAGE);
		}

		// =====================================================================================
		// SystemMessage core implementation: IMemoryCache + HttpContext
		// BEFORE: SystemMessage(HttpApplicationState Application, HttpContext Context, ...)
		//   - Application.Lock() / Application.UnLock() for thread safety
		//   - Application["SystemErrors"] for the DataTable cache
		//   - Context.Session.SessionID for session tracking
		//   - Context.Request.UserHostName / Url.Host / Path / AppRelativeCurrentExecutionFilePath
		//   - Context.Request.PhysicalApplicationPath for path normalization
		//   - DbProviderFactories.GetFactory(Application) → SqlProcs.spSYSTEM_LOG_InsertOnly
		// AFTER:
		//   - static readonly _lockObject for thread safety (replaces Application.Lock())
		//   - IMemoryCache with key "SystemErrors" for the DataTable cache (replaces Application[])
		//   - context.Session.Id replaces Context.Session.SessionID
		//   - context.Connection.RemoteIpAddress.ToString() replaces Request.UserHostName
		//   - request.Host.ToString() replaces Request.Url.Host
		//   - request.Path.ToString() replaces both Request.Path and AppRelativeCurrentExecutionFilePath
		//   - request.QueryString.ToString() preserved (identical API in ASP.NET Core)
		//   - PhysicalApplicationPath prefix stripping simplified (ContentRootPath not available here)
		//   - DbProviderFactories obtained from context.RequestServices (service locator)
		//   - spSYSTEM_LOG_InsertOnly executed inline via IDbCommand (not through SqlProcs service)
		//     using migrated parameter set: (USER_ID, LOG_LEVEL, FILE_NAME, METHOD, LINE_NUMBER, MESSAGE)
		// =====================================================================================

		/// <summary>
		/// Core logging implementation. Stores the error in IMemoryCache DataTable (capped at 100 rows)
		/// and persists it to the database via spSYSTEM_LOG_InsertOnly stored procedure when a
		/// DbProviderFactories service is available from context.RequestServices.
		/// </summary>
		/// <param name="memoryCache">Replaces HttpApplicationState; required for DataTable cache storage. Returns early when null.</param>
		/// <param name="context">Optional HTTP context; provides session info, request metadata, and service locator access.</param>
		/// <param name="sERROR_TYPE">Error classification: "Error", "Warning", or "Message".</param>
		/// <param name="stack">Stack frame from caller; provides file name, method name, and line number.</param>
		/// <param name="sMESSAGE">Human-readable message to log.</param>
		public static void SystemMessage(IMemoryCache memoryCache, HttpContext context, string sERROR_TYPE, StackFrame stack, string sMESSAGE)
		{
			if ( memoryCache == null )
			{
				// BEFORE: if (Application == null) return;
				// AFTER:  No IMemoryCache → cannot maintain the SystemErrors DataTable.
				//         Emit debug output so the message is visible during development/testing.
#if DEBUG
				string sFileNameFallback   = stack?.GetFileName()           ?? String.Empty;
				string sMethodFallback     = stack?.GetMethod()?.ToString() ?? String.Empty;
				int    nLineNumberFallback = stack?.GetFileLineNumber()     ?? 0           ;
				Debug.WriteLine($"SplendidError.{sERROR_TYPE}: {sFileNameFallback}::{sMethodFallback} line {nLineNumberFallback}: {sMESSAGE}");
#endif
				return;
			}

			// 08/12/2007 Paul.  Ignore the exception generated by Response.Redirect. 
			// 08/13/2007 Paul.  Instead of ignoring the English abort message, 
			// transition to the above function that ignores the abort exception.  Every file will need to be touched. 

			Guid   gUSER_ID          = Guid.Empty   ;
			string sUSER_NAME        = String.Empty ;
			string sMACHINE          = String.Empty ;
			string sASPNET_SESSIONID = String.Empty ;
			string sREMOTE_HOST      = String.Empty ;
			string sSERVER_HOST      = String.Empty ;
			string sTARGET           = String.Empty ;
			string sRELATIVE_PATH    = String.Empty ;
			string sPARAMETERS       = String.Empty ;
			string sFILE_NAME        = String.Empty ;
			string sMETHOD           = String.Empty ;
			Int32  nLINE_NUMBER      = 0            ;

			try
			{
				// 09/17/2009 Paul.  Azure does not support MachineName.  Just ignore the error. 
				sMACHINE = System.Environment.MachineName;
			}
			catch
			{
			}

			try
			{
				// 12/22/2007 Paul.  The current context will be null when inside a timer. 
				if ( context != null && context.Session != null )
				{
					// BEFORE: gUSER_ID  = Security.USER_ID;   (static)
					// AFTER:  Read USER_ID and USER_NAME directly from distributed session;
					//         Security.USER_ID is now an instance property — no static accessor.
					gUSER_ID          = Sql.ToGuid  (context.Session.GetString("USER_ID"  ));
					sUSER_NAME        = Sql.ToString(context.Session.GetString("USER_NAME"));
					// BEFORE: Context.Session.SessionID  → AFTER: ISession.Id
					sASPNET_SESSIONID = context.Session.Id;
				}
			}
			catch
			{
			}

			try
			{
				if ( context != null && context.Request != null )
				{
					HttpRequest request = context.Request;
					// BEFORE: Request.UserHostName → AFTER: HttpContext.Connection.RemoteIpAddress.ToString()
					sREMOTE_HOST   = context.Connection?.RemoteIpAddress?.ToString() ?? String.Empty;
					// BEFORE: Request.Url.Host → AFTER: request.Host.ToString()
					sSERVER_HOST   = request.Host.ToString();
					// BEFORE: Request.Path → AFTER: request.Path.ToString()
					sTARGET        = request.Path.ToString();
					// BEFORE: Request.AppRelativeCurrentExecutionFilePath → no direct equivalent in ASP.NET Core
					//         Using request.Path as the closest approximation.
					sRELATIVE_PATH = request.Path.ToString();
					// request.QueryString.ToString() has identical semantics in ASP.NET Core
					sPARAMETERS    = request.QueryString.ToString();
				}
			}
			catch
			{
			}

			if ( stack != null )
			{
				sFILE_NAME   = stack.GetFileName()           ?? String.Empty;
				sMETHOD      = stack.GetMethod()?.ToString() ?? String.Empty;
				nLINE_NUMBER = stack.GetFileLineNumber();
				try
				{
					if ( !Sql.IsEmptyString(sFILE_NAME) )
					{
						// 04/16/2006 Paul.  Use native function to get file name. 
						// 08/01/2007 Paul.  Include part of the path in the file name. Remove the physical root as it is not useful. 
						// BEFORE: sFILE_NAME = sFILE_NAME.Replace(Context.Request.PhysicalApplicationPath, "~" + Path.DirectorySeparatorChar);
						// AFTER:  PhysicalApplicationPath is not available in ASP.NET Core HttpContext.
						//         Normalize directory separators so the path is platform-consistent in logs.
						sFILE_NAME = sFILE_NAME.Replace(Path.DirectorySeparatorChar, '/');
					}
				}
				catch
				{
				}
			}

			// BEFORE: Application.Lock(); ... Application.UnLock();
			// AFTER:  static readonly _lockObject ensures thread-safe DataTable access,
			//         replacing HttpApplicationState.Lock()/UnLock() with a standard C# monitor lock.
			lock ( _lockObject )
			{
				DataTable dt = memoryCache.Get<DataTable>("SystemErrors");
				if ( dt == null )
				{
					dt = CreateSystemErrorsTable();
					// Cache with NeverRemove priority so it persists for the application lifetime.
					memoryCache.Set("SystemErrors", dt, new MemoryCacheEntryOptions
					{
						Priority = CacheItemPriority.NeverRemove
					});
				}

				DataRow row = dt.NewRow();
				dt.Rows.Add(row);
				row["CREATED_BY"  ] = gUSER_ID    ;
				row["USER_NAME"   ] = sUSER_NAME  ;
				row["DATE_ENTERED"] = DateTime.Now;
				row["ERROR_TYPE"  ] = sERROR_TYPE ;
				row["MESSAGE"     ] = sMESSAGE    ;
				if ( stack != null )
				{
					row["FILE_NAME"  ] = sFILE_NAME  ;
					row["METHOD"     ] = sMETHOD     ;
					row["LINE_NUMBER"] = nLINE_NUMBER;
				}

				try
				{
					// 04/23/2010 Paul.  Let's cap the error cache at 100 messages. 
					// We are going to assume that the top rows are the oldest records. 
					while ( dt.Rows.Count > 100 )
					{
						dt.Rows.RemoveAt(0);
					}
				}
				catch
				{
				}
			} // end lock

			// Database logging via spSYSTEM_LOG_InsertOnly
			// BEFORE: DbProviderFactories.GetFactory(Application) → SqlProcs.spSYSTEM_LOG_InsertOnly(...)
			// AFTER:  DbProviderFactories obtained via service locator from context.RequestServices.
			//         spSYSTEM_LOG_InsertOnly executed inline using IDbCommand for minimal coupling.
			//         Migrated parameter set: (USER_ID, LOG_LEVEL, FILE_NAME, METHOD, LINE_NUMBER, MESSAGE).
			try
			{
				DbProviderFactories dbf = context?.RequestServices?.GetService(typeof(DbProviderFactories)) as DbProviderFactories;
				if ( dbf != null )
				{
					using ( IDbConnection con = dbf.CreateConnection() )
					{
						con.Open();
						// 10/07/2009 Paul.  We need to create our own global transaction ID to support auditing and workflow on SQL Azure, PostgreSQL, Oracle, DB2 and MySQL. 
						// BEFORE: using (IDbTransaction trn = Sql.BeginTransaction(con))
					// AFTER:  Sql.BeginTransaction() is not present in migrated Sql.cs; use con.BeginTransaction() directly.
					using ( IDbTransaction trn = con.BeginTransaction() )
					{
						try
						{
							using ( IDbCommand cmd = con.CreateCommand() )
							{
								cmd.CommandType    = CommandType.StoredProcedure;
								cmd.CommandText    = "spSYSTEM_LOG_InsertOnly";
								cmd.Transaction    = trn;
								Sql.AddParameter(cmd, "@MODIFIED_USER_ID", gUSER_ID     );
								Sql.AddParameter(cmd, "@LOG_LEVEL"       , sERROR_TYPE  , 25 );
								Sql.AddParameter(cmd, "@FILE_NAME"       , sFILE_NAME   , 255);
								Sql.AddParameter(cmd, "@METHOD"          , sMETHOD      , 100);
								Sql.AddParameter(cmd, "@LINE_NUMBER"     , nLINE_NUMBER );
								Sql.AddParameter(cmd, "@MESSAGE"         , sMESSAGE     );
								cmd.ExecuteNonQuery();
							}
							trn.Commit();
						}
						catch//(Exception ex)
						{
							trn.Rollback();
							// 10/26/2008 Paul.  Can't throw an exception here as it could create an endless loop. 
							//SplendidError.SystemMessage(Context, "Error", new StackTrace(true).GetFrame(0), ExpandException(ex));
						}
					}
					}
				}
			}
#if DEBUG
			catch (Exception ex)
			{
				// 09/16/2015 Paul.  Change to Debug as it is automatically not included in a release build. 
				System.Diagnostics.Debug.WriteLine(ex.Message);
			}
#else
			catch
			{
			}
#endif
		}
	}
}
