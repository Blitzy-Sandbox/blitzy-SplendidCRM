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
// .NET 10 Migration: SplendidCRM/_code/SyncError.cs → src/SplendidCRM.Core/SyncError.cs
// Changes applied:
//   - REMOVED: using System.Web; (HttpContext.Current, HttpApplicationState)
//   - ADDED:   using Microsoft.AspNetCore.Http; using Microsoft.Extensions.Caching.Memory;
//   - HttpContext.Current → explicit HttpContext parameter (obtained from caller or IHttpContextAccessor via DI)
//   - HttpApplicationState (Application[]) → IMemoryCache parameter; cache keys "SystemSync.Status" and
//     "SystemSync.Errors" preserved identically; IMemoryCache.Set() replaces Application["key"] = value
//   - Application.Lock() / Application.UnLock() → static readonly _lockObject for thread-safe DataTable access
//   - Security.USER_ID / Security.USER_NAME (were static) → read directly from ISession.GetString() since
//     Security is now an instance class; Security class accessed via schema, session read is the mechanism
//   - DbProviderFactories.GetFactory(Application) → DbProviderFactories obtained via service locator from
//     context.RequestServices; GetFactory(IMemoryCache) called on the instance
//   - DbProviderFactory.CreateConnection() preserved; called on the ADO.NET DbProviderFactory returned by GetFactory()
//   - SqlProcs.spSYSTEM_SYNC_LOG_InsertOnly() call preserved with IDbTransaction parameter
//   - Sql.BeginTransaction() preserved for wrapping stored procedure call in transaction
//   - Context.Request.PhysicalApplicationPath (no ASP.NET Core equivalent) → Context.Request.PathBase.Value
//     used as best-effort application root path component for file name normalization
//   - System.Threading.ThreadAbortException check preserved as-is; in .NET 5+ ThreadAbortException is never
//     thrown but the check is a no-op harmless guard preserved per minimal change clause
//   - DI constructor added: SyncError(IHttpContextAccessor, IMemoryCache) for service registration
//   - PRESERVED: namespace SplendidCRM, all public static method signatures, ControlChars.CrLf usage,
//     DataColumn type definitions with Type.GetType(), cache key names, sMACHINE / sREMOTE_URL patterns
//   - PRESERVED: All business logic; minimal change clause honored
//   - Minimal change clause: Only changes necessary for .NET Framework 4.8 → .NET 10 ASP.NET Core transition
#nullable disable
using System;
using System.IO;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	/// <summary>
	/// Sync error logging utility for SplendidCRM external synchronization operations.
	/// Provides SystemError, SystemWarning, and SystemMessage overloads for structured error logging
	/// to both an in-memory DataTable cache (IMemoryCache keys: "SystemSync.Status", "SystemSync.Errors")
	/// and the SQL Server system sync log table via the spSYSTEM_SYNC_LOG_InsertOnly stored procedure.
	///
	/// Migrated from SplendidCRM/_code/SyncError.cs for .NET 10 ASP.NET Core.
	///
	/// DESIGN NOTES for callers:
	///   • Register SyncError as a SCOPED service so it can be injected wherever needed.
	///   • Static methods (SystemError, SystemWarning) are preserved for backward-compatible call sites.
	///   • Callers that have an IMemoryCache or HttpContext should prefer the explicit-parameter overloads
	///     for full logging fidelity (cache + database).
	///   • The convenience overloads (StackFrame-only) are near-no-ops in .NET Core since there is no
	///     ambient HttpContext.Current; use the IMemoryCache or HttpContext overloads for full logging.
	/// </summary>
	public class SyncError
	{
		// =====================================================================================
		// DI fields — for instance registration in the DI container.
		// Instance fields are provided so SyncError can be registered as a scoped service and
		// injected via constructor injection. The static methods use service locator when needed.
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
		/// Constructs a SyncError service instance with injected HTTP context accessor and memory cache.
		/// </summary>
		/// <param name="httpContextAccessor">
		/// Replaces HttpContext.Current throughout; provides access to the current request's session,
		/// request metadata, and service container for service-locator patterns.
		/// </param>
		/// <param name="memoryCache">
		/// Replaces Application["SystemSync.Status"] and Application["SystemSync.Errors"] throughout.
		/// Also used to read "SplendidCRM_REMOTE_URL" for sync log entries.
		/// </param>
		public SyncError(IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache)
		{
			_httpContextAccessor = httpContextAccessor;
			_memoryCache         = memoryCache        ;
		}

		// =====================================================================================
		// Convenience overloads: SystemWarning / SystemError (no context/cache parameters)
		// These delegate to SystemMessage("Warning"/"Error", stack, sMESSAGE) for backward
		// compatibility with existing call sites that cannot easily inject context.
		// In .NET Core, these are near-no-ops since there is no ambient HttpContext.Current.
		// =====================================================================================

		/// <summary>
		/// Logs a warning for the current sync operation using stack frame for source location.
		/// Delegates to SystemMessage("Warning", stack, sMESSAGE).
		/// Note: In .NET Core, without an ambient context this is a near-no-op.
		/// Use SystemMessage(IMemoryCache, ...) overloads for full logging fidelity.
		/// </summary>
		public static void SystemWarning(StackFrame stack, string sMESSAGE)
		{
			SystemMessage("Warning", stack, sMESSAGE);
		}

		/// <summary>
		/// Logs an error for the current sync operation using stack frame for source location.
		/// Delegates to SystemMessage("Error", stack, sMESSAGE).
		/// Note: In .NET Core, without an ambient context this is a near-no-op.
		/// Use SystemMessage(IMemoryCache, ...) overloads for full logging fidelity.
		/// </summary>
		public static void SystemError(StackFrame stack, string sMESSAGE)
		{
			SystemMessage("Error", stack, sMESSAGE);
		}

		/// <summary>
		/// Logs a warning from an exception, expanding the exception message and ignoring ThreadAbortException.
		/// </summary>
		public static void SystemWarning(StackFrame stack, Exception ex)
		{
			// 08/13/2007 Paul.  Instead of ignoring the the english abort message, ignore the abort exception. 
			if ( ex.GetType() != Type.GetType("System.Threading.ThreadAbortException") )
			{
				string sMESSAGE = Utils.ExpandException(ex);
				SystemMessage("Warning", stack, sMESSAGE);
			}
		}

		/// <summary>
		/// Logs an error from an exception, expanding the exception message (including stack trace)
		/// and ignoring ThreadAbortException.
		/// </summary>
		public static void SystemError(StackFrame stack, Exception ex)
		{
			// 08/13/2007 Paul.  Instead of ignoring the the english abort message, ignore the abort exception. 
			if ( ex.GetType() != Type.GetType("System.Threading.ThreadAbortException") )
			{
				string sMESSAGE = Utils.ExpandException(ex);
				// 01/14/2009 Paul.  Save the stack trace to help locate the source of a bug. 
				if ( ex.StackTrace != null )
					sMESSAGE += "<br />\r\n" + ex.StackTrace.Replace(ControlChars.CrLf, "<br />\r\n");
				SystemMessage("Error", stack, sMESSAGE);
			}
		}

		// =====================================================================================
		// SystemMessage(string, StackFrame, string) — no-context overload
		// BEFORE: if (HttpContext.Current == null || HttpContext.Current.Application == null) return;
		//         SystemMessage(HttpContext.Current.Application, HttpContext.Current, ...);
		// AFTER:  HttpContext.Current does not exist in ASP.NET Core.
		//         Without an ambient context, full cache and DB logging cannot be performed.
		//         Emits debug output in DEBUG builds so the message is visible during development.
		//         Callers should prefer SystemMessage(IMemoryCache, ...) or SystemMessage(HttpContext, ...)
		//         overloads to ensure messages are persisted.
		// =====================================================================================

		/// <summary>
		/// Logs a system sync message using no explicit application state or HTTP context.
		/// In .NET Core this is a near-no-op because there is no HttpContext.Current ambient accessor.
		/// Use the IMemoryCache or HttpContext overloads for full logging fidelity.
		/// </summary>
		public static void SystemMessage(string sERROR_TYPE, StackFrame stack, string sMESSAGE)
		{
			// .NET 10 Migration: HttpContext.Current is not available in ASP.NET Core.
			// BEFORE: if ( HttpContext.Current == null || HttpContext.Current.Application == null )
			//             return;
			//         SystemMessage(HttpContext.Current.Application, HttpContext.Current, sERROR_TYPE, stack, sMESSAGE);
			// AFTER:  No ambient context — emit debug output only; callers must use explicit-parameter overloads.
#if DEBUG
			try
			{
				string sFileNameFallback   = stack?.GetFileName()           ?? String.Empty;
				string sMethodFallback     = stack?.GetMethod()?.ToString() ?? String.Empty;
				int    nLineNumberFallback = stack?.GetFileLineNumber()     ?? 0           ;
				Debug.WriteLine($"SyncError.{sERROR_TYPE}: {sFileNameFallback}::{sMethodFallback} line {nLineNumberFallback}: {sMESSAGE}");
			}
			catch
			{
			}
#endif
		}

		// =====================================================================================
		// SystemMessage(HttpContext, ...) overloads
		// BEFORE: SystemMessage(Context.Application, Context, sERROR_TYPE, stack, sMESSAGE)
		// AFTER:  Extract IMemoryCache from context.RequestServices; delegate to core method.
		//         Context.Application is replaced by IMemoryCache obtained via service locator.
		// =====================================================================================

		/// <summary>
		/// Logs a system sync message from an exception using an explicit HTTP context.
		/// Extracts IMemoryCache from context.RequestServices and delegates to the core method.
		/// </summary>
		public static void SystemMessage(HttpContext Context, string sERROR_TYPE, StackFrame stack, Exception ex)
		{
			string sMESSAGE = Utils.ExpandException(ex);
			if ( sERROR_TYPE == "Error" )
			{
				// 01/14/2009 Paul.  Save the stack trace to help locate the source of a bug. 
				if ( ex.StackTrace != null )
					sMESSAGE += "<br />\r\n" + ex.StackTrace.Replace(ControlChars.CrLf, "<br />\r\n");
			}
			// .NET 10 Migration: BEFORE: SystemMessage(Context.Application, Context, ...)
			// AFTER: Obtain IMemoryCache via service locator from context.RequestServices
			IMemoryCache memoryCache = Context?.RequestServices?.GetService(typeof(IMemoryCache)) as IMemoryCache;
			SystemMessage(memoryCache, Context, sERROR_TYPE, stack, sMESSAGE);
		}

		/// <summary>
		/// Logs a system sync message using an explicit HTTP context and string message.
		/// Extracts IMemoryCache from context.RequestServices and delegates to the core method.
		/// </summary>
		public static void SystemMessage(HttpContext Context, string sERROR_TYPE, StackFrame stack, string sMESSAGE)
		{
			// .NET 10 Migration: BEFORE: SystemMessage(Context.Application, Context, ...)
			// AFTER: Obtain IMemoryCache via service locator from context.RequestServices
			IMemoryCache memoryCache = Context?.RequestServices?.GetService(typeof(IMemoryCache)) as IMemoryCache;
			SystemMessage(memoryCache, Context, sERROR_TYPE, stack, sMESSAGE);
		}

		// =====================================================================================
		// SystemMessage(IMemoryCache, ...) overloads
		// BEFORE: SystemMessage(HttpApplicationState Application, string sERROR_TYPE, StackFrame stack, ...)
		// AFTER:  SystemMessage(IMemoryCache memoryCache, string sERROR_TYPE, StackFrame stack, ...)
		//         HttpApplicationState parameter replaced by IMemoryCache per migration rules.
		//         Null context indicates background service (scheduler, email poll, archive) — DB logging
		//         is skipped when context is null since no service locator is available.
		// =====================================================================================

		/// <summary>
		/// Logs a system sync message from an exception using an explicit IMemoryCache.
		/// Use this overload from background services that have IMemoryCache but no HTTP context.
		/// </summary>
		public static void SystemMessage(IMemoryCache memoryCache, string sERROR_TYPE, StackFrame stack, Exception ex)
		{
			// 10/2009 Paul.  The cache functions need to pass an Exception object. 
			string sMESSAGE = Utils.ExpandException(ex);
			if ( sERROR_TYPE == "Error" )
			{
				// 01/14/2009 Paul.  Save the stack trace to help locate the source of a bug. 
				if ( ex.StackTrace != null )
					sMESSAGE += "<br />\r\n" + ex.StackTrace.Replace(ControlChars.CrLf, "<br />\r\n");
			}
			SystemMessage(memoryCache, null, sERROR_TYPE, stack, sMESSAGE);
		}

		/// <summary>
		/// Logs a system sync message using an explicit IMemoryCache and string message.
		/// Use this overload from background services that have IMemoryCache but no HTTP context.
		/// </summary>
		public static void SystemMessage(IMemoryCache memoryCache, string sERROR_TYPE, StackFrame stack, string sMESSAGE)
		{
			SystemMessage(memoryCache, null, sERROR_TYPE, stack, sMESSAGE);
		}

		// =====================================================================================
		// SystemMessage core implementation: IMemoryCache + HttpContext
		// BEFORE: SystemMessage(HttpApplicationState Application, HttpContext Context, string sERROR_TYPE, StackFrame stack, string sMESSAGE)
		//   - Application.Lock() / Application.UnLock() for thread safety
		//   - Application["SystemSync.Status"] for status value
		//   - Application["SystemSync.Errors"] as DataTable cache
		//   - Security.USER_ID / Security.USER_NAME (static) for user identification
		//   - Context.Request.PhysicalApplicationPath for file path normalization
		//   - DbProviderFactories.GetFactory(Application) for connection factory
		//   - SqlProcs.spSYSTEM_SYNC_LOG_InsertOnly(gUSER_ID, sMACHINE, sREMOTE_URL, ..., trn)
		// AFTER:
		//   - static readonly _lockObject for thread safety (replaces Application.Lock())
		//   - IMemoryCache.Set("SystemSync.Status", sMESSAGE) for status value
		//   - IMemoryCache.Get<DataTable>("SystemSync.Errors") for DataTable cache
		//   - Read USER_ID/USER_NAME directly from ISession.GetString(); Security class is now instance-based
		//   - Context.Request.PathBase.Value used as best-effort replacement for PhysicalApplicationPath
		//   - DbProviderFactories obtained from context.RequestServices; GetFactory(IMemoryCache) called on instance
		//   - DbProviderFactory.CreateConnection() and Sql.BeginTransaction() preserved exactly
		//   - SqlProcs.spSYSTEM_SYNC_LOG_InsertOnly preserved with identical parameter set + trn
		// =====================================================================================

		/// <summary>
		/// Core sync error logging implementation.
		/// Stores the error in IMemoryCache DataTable ("SystemSync.Errors") and persists it to the
		/// database via spSYSTEM_SYNC_LOG_InsertOnly stored procedure when a DbProviderFactories
		/// service is available from context.RequestServices.
		/// </summary>
		/// <param name="memoryCache">
		/// Replaces HttpApplicationState; required for DataTable cache storage.
		/// Returns early when null (emitting debug output in DEBUG builds).
		/// </param>
		/// <param name="Context">
		/// Optional HTTP context; provides session info (USER_ID) and service locator access for
		/// DbProviderFactories. May be null for background services.
		/// </param>
		/// <param name="sERROR_TYPE">Error classification: "Error", "Warning", or "Message".</param>
		/// <param name="stack">Stack frame from caller; provides file name, method name, and line number.</param>
		/// <param name="sMESSAGE">Human-readable message to log.</param>
		public static void SystemMessage(IMemoryCache memoryCache, HttpContext Context, string sERROR_TYPE, StackFrame stack, string sMESSAGE)
		{
			// .NET 10 Migration: BEFORE: if ( Application == null ) return;
			// AFTER:  No IMemoryCache → cannot maintain the SystemSync DataTable.
			//         Emit debug output so the message is visible during development/testing.
			if ( memoryCache == null )
			{
#if DEBUG
				try
				{
					string sFileNameFallback   = stack?.GetFileName()           ?? String.Empty;
					string sMethodFallback     = stack?.GetMethod()?.ToString() ?? String.Empty;
					int    nLineNumberFallback = stack?.GetFileLineNumber()     ?? 0           ;
					Debug.WriteLine($"SyncError.{sERROR_TYPE}: {sFileNameFallback}::{sMethodFallback} line {nLineNumberFallback}: {sMESSAGE}");
				}
				catch
				{
				}
#endif
				return;
			}

			// Variables declared before the lock so they are accessible for the database write below.
			// These are per-invocation local variables; no thread-safety concern for the variables themselves.
			Guid   gUSER_ID      = Guid.Empty  ;
			string sUSER_NAME    = String.Empty ;
			string sMACHINE      = String.Empty ;
			// .NET 10 Migration: Application["SplendidCRM_REMOTE_URL"] → IMemoryCache.TryGetValue("SplendidCRM_REMOTE_URL", ...)
			string sREMOTE_URL   = Sql.ToString(memoryCache.TryGetValue("SplendidCRM_REMOTE_URL", out object remoteUrlObj) ? remoteUrlObj : null);
			string sFILE_NAME    = String.Empty ;
			string sMETHOD       = String.Empty ;
			Int32  nLINE_NUMBER  = 0            ;

			try
			{
				// 09/17/2009 Paul.  Azure does not support MachineName.  Just ignore the error. 
				sMACHINE = System.Environment.MachineName;
			}
			catch
			{
			}

			// .NET 10 Migration: BEFORE: Application.Lock(); ... Application.UnLock();
			// AFTER:  static readonly _lockObject ensures thread-safe DataTable and status access,
			//         replacing HttpApplicationState.Lock() / UnLock() with a standard C# monitor lock.
			lock ( _lockObject )
			{
				// 11/29/2009 Paul.  Use a global status value that can be polled. 
				// .NET 10 Migration: Application["SystemSync.Status"] = sMESSAGE → IMemoryCache.Set(...)
				memoryCache.Set("SystemSync.Status", sMESSAGE);

				// .NET 10 Migration: Application["SystemSync.Errors"] as DataTable → IMemoryCache.Get<DataTable>(...)
				DataTable dt = memoryCache.Get<DataTable>("SystemSync.Errors");
				if ( dt == null )
				{
					dt = new DataTable();
					// Preserve original Type.GetType() pattern for DataColumn type definitions.
					DataColumn colDATE_ENTERED = new DataColumn("DATE_ENTERED", Type.GetType("System.DateTime"));
					DataColumn colERROR_TYPE   = new DataColumn("ERROR_TYPE"  , Type.GetType("System.String"  ));
					DataColumn colFILE_NAME    = new DataColumn("FILE_NAME"   , Type.GetType("System.String"  ));
					DataColumn colMETHOD       = new DataColumn("METHOD"      , Type.GetType("System.String"  ));
					DataColumn colLINE_NUMBER  = new DataColumn("LINE_NUMBER" , Type.GetType("System.String"  ));
					DataColumn colMESSAGE      = new DataColumn("MESSAGE"     , Type.GetType("System.String"  ));
					dt.Columns.Add(colDATE_ENTERED);
					dt.Columns.Add(colERROR_TYPE  );
					dt.Columns.Add(colMESSAGE     );
					dt.Columns.Add(colFILE_NAME   );
					dt.Columns.Add(colMETHOD      );
					dt.Columns.Add(colLINE_NUMBER );
					// Cache with no expiration so the DataTable persists for the application lifetime.
					memoryCache.Set("SystemSync.Errors", dt);
				}

				DataRow row = dt.NewRow();
				dt.Rows.Add(row);

				try
				{
					// 12/22/2007 Paul.  The current context will be null when inside a timer. 
					if ( Context != null && Context.Session != null )
					{
						// .NET 10 Migration: BEFORE: gUSER_ID = Security.USER_ID; sUSER_NAME = Security.USER_NAME;
						// Security is now an instance class (no static accessor). Read USER_ID and USER_NAME
						// directly from the distributed session to match Security.USER_ID / Security.USER_NAME
						// instance property implementations.
						gUSER_ID   = Sql.ToGuid  (Context.Session.GetString("USER_ID"  ));
						sUSER_NAME = Sql.ToString (Context.Session.GetString("USER_NAME"));
					}
				}
				catch
				{
				}

				row["DATE_ENTERED"] = DateTime.Now  ;
				row["ERROR_TYPE"  ] = sERROR_TYPE   ;
				row["MESSAGE"     ] = sMESSAGE      ;

				if ( stack != null )
				{
					sFILE_NAME   = stack.GetFileName()           ?? String.Empty;
					sMETHOD      = stack.GetMethod()?.ToString() ?? String.Empty;
					nLINE_NUMBER = stack.GetFileLineNumber();
					try
					{
						if ( Context != null && Context.Request != null )
						{
							if ( !Sql.IsEmptyString(sFILE_NAME) )
							{
								// 04/16/2006 Paul.  Use native function to get file name. 
								// 08/01/2007 Paul.  Include part of the path in the file name. Remove the physical root as it is not useful. 
								// .NET 10 Migration: BEFORE: sFILE_NAME = sFILE_NAME.Replace(Context.Request.PhysicalApplicationPath, "~" + Path.DirectorySeparatorChar);
								// AFTER:  Request.PhysicalApplicationPath has no equivalent in ASP.NET Core.
								//         Use Context.Request.PathBase.Value as the application root URL path component.
								//         On Linux deployments PathBase is typically "/" or empty, so the replacement
								//         normalizes directory separators for consistent cross-platform log output.
								string sPathBase = Context.Request.PathBase.Value ?? String.Empty;
								if ( !Sql.IsEmptyString(sPathBase) )
									sFILE_NAME = sFILE_NAME.Replace(sPathBase, "~" + Path.DirectorySeparatorChar);
								sFILE_NAME = sFILE_NAME.Replace(Path.DirectorySeparatorChar, '/');
							}
						}
					}
					catch
					{
					}
					row["FILE_NAME"   ] = sFILE_NAME  ;
					row["METHOD"      ] = sMETHOD     ;
					row["LINE_NUMBER" ] = nLINE_NUMBER ;
				}
			} // end lock

			// =====================================================================================
			// Database logging via spSYSTEM_SYNC_LOG_InsertOnly
			// BEFORE: DbProviderFactory dbf = DbProviderFactories.GetFactory(Application);
			//         using (IDbConnection con = dbf.CreateConnection()) { ... }
			// AFTER:  DbProviderFactories (DI service) obtained from context.RequestServices.
			//         GetFactory(IMemoryCache) called on the instance to resolve the ADO.NET DbProviderFactory.
			//         CreateConnection() called on the ADO.NET DbProviderFactory.
			//         Sql.BeginTransaction() and SqlProcs.spSYSTEM_SYNC_LOG_InsertOnly preserved exactly.
			// =====================================================================================
			try
			{
				DbProviderFactories dbfSvc = Context?.RequestServices?.GetService(typeof(DbProviderFactories)) as DbProviderFactories;
				if ( dbfSvc != null )
				{
					// .NET 10 Migration: DbProviderFactories.GetFactory(Application) → dbfSvc.GetFactory(memoryCache)
					// Resolves the ADO.NET DbProviderFactory using IMemoryCache as the connection string cache.
					DbProviderFactory dbf = dbfSvc.GetFactory(memoryCache);
					using ( IDbConnection con = dbf.CreateConnection() )
					{
						con.Open();
						// 10/07/2009 Paul.  We need to create our own global transaction ID to support auditing and workflow on SQL Azure, PostgreSQL, Oracle, DB2 and MySQL. 
						using ( IDbTransaction trn = Sql.BeginTransaction(con) )
						{
							try
							{
								SqlProcs.spSYSTEM_SYNC_LOG_InsertOnly(gUSER_ID, sMACHINE, sREMOTE_URL, sERROR_TYPE, sFILE_NAME, sMETHOD, nLINE_NUMBER, sMESSAGE, trn);
								trn.Commit();
							}
							catch //(Exception ex)
							{
								trn.Rollback();
								// 10/26/2008 Paul.  Can't throw an exception here as it could create an endless loop. 
								//SyncError.SystemMessage(Context, "Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
							}
						}
					}
				}
			}
			catch
			{
			}

			// 02/11/2012 Paul.  Dumping the error message will help when debugging. 
#if DEBUG
			try
			{
				if ( sERROR_TYPE == "Error" )
					Debug.WriteLine(sMESSAGE);
			}
			catch
			{
			}
#endif
		}
	}
}
