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
// .NET 10 Migration: SplendidCRM/_code/SignalR/ChatManager.cs → src/SplendidCRM.Web/SignalR/ChatManager.cs
// Changes applied:
//   - REMOVED: using System.Web; using System.Web.UI; using System.Web.SessionState;
//              (WebForms namespaces — not available in ASP.NET Core)
//   - REMOVED: using Microsoft.AspNet.SignalR; using Microsoft.AspNet.SignalR.Hubs;
//              (OWIN SignalR — replaced by Microsoft.AspNetCore.SignalR)
//   - ADDED:   using Microsoft.AspNetCore.Http; (IHttpContextAccessor, ISession.GetString)
//   - ADDED:   using Microsoft.AspNetCore.SignalR; (IHubContext<T>)
//   - ADDED:   using Microsoft.Extensions.Caching.Memory; (IMemoryCache)
//   - ADDED:   using System.Threading.Tasks; (Task for async NewMessage)
//   - REMOVED: static singleton pattern — _instance field, Instance property, InitApp(HttpContext) static method
//              OWIN GlobalHost.ConnectionManager.GetHubContext<ChatManagerHub>() replaced by DI-injected IHubContext<T>
//   - REMOVED: RegisterScripts(HttpContext, ScriptManager) — WebForms ScriptManager not available in ASP.NET Core
//   - REPLACED: private HttpContext Context → private readonly IHttpContextAccessor _httpContextAccessor
//   - REPLACED: private IHubConnectionContext<dynamic> Clients → private readonly IHubContext<ChatManagerHub> _hubContext
//   - ADDED:   private readonly IMemoryCache _memoryCache (replaces Application[] state)
//   - ADDED:   private readonly DbProviderFactories _dbProviderFactories (DI-injected factory registry)
//   - ADDED:   private readonly RestUtil _restUtil (DI-injected REST serialization service)
//   - REPLACED: private ChatManager(HttpContext, IHubConnectionContext<dynamic>) constructor
//              → public ChatManager(IHttpContextAccessor, IMemoryCache, IHubContext<ChatManagerHub>,
//                                    DbProviderFactories, RestUtil) for DI registration
//   - REPLACED: DbProviderFactories.GetFactory(this.Context.Application)
//              → _dbProviderFactories.GetFactory(_memoryCache)
//              (Application[] state replaced by IMemoryCache per AAP Section 0.5.2)
//   - REPLACED: HttpContext.Current.Session["USER_SETTINGS/TIMEZONE"]
//              → _httpContextAccessor.HttpContext?.Session.GetString("USER_SETTINGS/TIMEZONE")
//              (distributed session-compatible pattern per AAP Section 0.7.5)
//   - REPLACED: Clients.Group(...).newMessage(...) [dynamic dispatch, OWIN SignalR]
//              → await _hubContext.Clients.Group(...).SendAsync("newMessage", ...)
//              (ASP.NET Core SignalR SendAsync; "newMessage" client method name preserved per AAP 0.8.1)
//   - MADE ASYNC: NewMessage(Guid gID) → async Task NewMessage(Guid gID)
//              (required by ASP.NET Core SignalR awaitable SendAsync calls)
//   - REPLACED: SplendidError.SystemMessage(Context, "Error", ...)
//              → SplendidError.SystemMessage(_httpContextAccessor.HttpContext, "Error", ...)
//   - PRESERVED: NullID(Guid) helper method — exact logic, no changes
//   - PRESERVED: All SQL queries, field names, variable names, DataRow field extractions (15+ fields)
//   - PRESERVED: RestUtil.ToJson("", "ChatMessages", dt.Rows[0], T10n) call (business logic)
//   - PRESERVED: TimeZone.CreateTimeZone(gTIMEZONE) and T10n.FromServerTime(dtDATE_ENTERED) pattern
//   - PRESERVED: ControlChars.CrLf for SQL query formatting
//   - PRESERVED: namespace SplendidCRM
//   - PRESERVED: All original developer comments (Paul. dated comments)
//   - Minimal change clause applied per AAP 0.8.1: only changes necessary for .NET Framework 4.8 → .NET 10
#nullable disable
using System;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	/// <summary>
	/// Summary description for ChatManager.
	/// Chat business logic service that queries vwCHAT_MESSAGES and broadcasts new chat messages
	/// to connected SignalR clients via IHubContext&lt;ChatManagerHub&gt;.
	/// 
	/// Migrated from SplendidCRM/_code/SignalR/ChatManager.cs for .NET 10 ASP.NET Core.
	/// 
	/// BEFORE (.NET Framework 4.8):
	///   Static singleton initialized via InitApp(HttpContext context).
	///   Hub access via OWIN: GlobalHost.ConnectionManager.GetHubContext&lt;ChatManagerHub&gt;().Clients
	///   Session access via HttpContext.Current.Session["key"] static pattern.
	///   Application state via this.Context.Application for DbProviderFactories.
	///   Dynamic dispatch: Clients.Group(id).newMessage(args) (OWIN SignalR dynamic proxy).
	/// 
	/// AFTER (.NET 10 ASP.NET Core):
	///   DI-registered singleton service (builder.Services.AddSingleton&lt;ChatManager&gt;()).
	///   Hub access via injected IHubContext&lt;ChatManagerHub&gt; (ASP.NET Core SignalR).
	///   Session access via IHttpContextAccessor.HttpContext?.Session.GetString() (distributed session).
	///   DB access via injected DbProviderFactories.GetFactory(IMemoryCache).
	///   REST serialization via injected RestUtil.ToJson().
	///   Strongly-typed broadcast: SendAsync("newMessage", ...) replacing dynamic dispatch.
	/// </summary>
	public class ChatManager
	{
		#region DI-injected fields
		// .NET 10 Migration: Replace static HttpContext Context field and singleton pattern
		// with constructor-injected services for DI-compatible instance registration.
		private readonly IHttpContextAccessor        _httpContextAccessor;
		private readonly IMemoryCache                _memoryCache        ;
		private readonly IHubContext<ChatManagerHub> _hubContext         ;
		private readonly DbProviderFactories         _dbProviderFactories;
		private readonly RestUtil                    _restUtil           ;
		#endregion

		#region Constructor
		/// <summary>
		/// Constructs a ChatManager service with all required DI dependencies.
		/// Replaces the legacy static singleton initialization pattern:
		///   BEFORE: _instance = new ChatManager(Context, GlobalHost.ConnectionManager.GetHubContext&lt;ChatManagerHub&gt;().Clients)
		///   AFTER:  Registered as DI singleton; constructor called by DI container with injected services.
		/// </summary>
		/// <param name="httpContextAccessor">
		/// Provides access to the current HttpContext, replacing HttpContext.Current static access.
		/// Used for distributed session reads (USER_SETTINGS/TIMEZONE) and SplendidError context.
		/// </param>
		/// <param name="memoryCache">
		/// In-memory cache replacing HttpApplicationState (Application[]) state.
		/// Passed to DbProviderFactories.GetFactory() for connection string resolution.
		/// </param>
		/// <param name="hubContext">
		/// ASP.NET Core SignalR hub context for ChatManagerHub.
		/// Replaces OWIN GlobalHost.ConnectionManager.GetHubContext&lt;ChatManagerHub&gt;().
		/// Used to broadcast newMessage events to client groups.
		/// </param>
		/// <param name="dbProviderFactories">
		/// DI-registered database provider factory registry.
		/// Replaces the static GetFactory(HttpApplicationState) call pattern.
		/// </param>
		/// <param name="restUtil">
		/// DI-registered REST serialization service.
		/// Replaces the static RestUtil.ToJson() call (now an instance method in .NET 10 migration).
		/// </param>
		public ChatManager(
			IHttpContextAccessor        httpContextAccessor ,
			IMemoryCache                memoryCache         ,
			IHubContext<ChatManagerHub> hubContext          ,
			DbProviderFactories         dbProviderFactories ,
			RestUtil                    restUtil            )
		{
			_httpContextAccessor = httpContextAccessor  ?? throw new ArgumentNullException(nameof(httpContextAccessor));
			_memoryCache         = memoryCache          ?? throw new ArgumentNullException(nameof(memoryCache));
			_hubContext          = hubContext            ?? throw new ArgumentNullException(nameof(hubContext));
			_dbProviderFactories = dbProviderFactories  ?? throw new ArgumentNullException(nameof(dbProviderFactories));
			_restUtil            = restUtil              ?? throw new ArgumentNullException(nameof(restUtil));
		}
		#endregion

		// .NET 10 Migration: InitApp(HttpContext) static method REMOVED.
		//   BEFORE: _instance = new ChatManager(Context, GlobalHost.ConnectionManager.GetHubContext<ChatManagerHub>().Clients);
		//   AFTER:  DI handles instantiation; IHubContext<ChatManagerHub> is injected directly.

		// .NET 10 Migration: RegisterScripts(HttpContext Context, ScriptManager mgrAjax) static method REMOVED.
		//   WebForms ScriptManager is not available in ASP.NET Core.
		//   JavaScript bundle registration is handled by the React frontend build toolchain (Prompt 2).

		private object NullID(Guid gID)
		{
			return Sql.IsEmptyGuid(gID) ? null : gID.ToString();
		}

		// 04/01/2020 Paul.  Move json utils to RestUtil. 

		/// <summary>
		/// Queries vwCHAT_MESSAGES for the specified chat message and broadcasts it to all connected
		/// members of the message's chat channel group via SignalR.
		/// 
		/// BEFORE (.NET Framework 4.8):
		///   public void NewMessage(Guid gID) — synchronous, dynamic Clients dispatch
		/// AFTER (.NET 10 ASP.NET Core):
		///   public async Task NewMessage(Guid gID) — async, SendAsync dispatch
		/// 
		/// All SQL queries, field extractions (15+ DataRow fields), timezone conversion,
		/// JSON serialization, and SignalR broadcast logic preserved exactly.
		/// The "newMessage" SignalR client method name is preserved per AAP 0.8.1 immutable interface rule.
		/// </summary>
		/// <param name="gID">The GUID of the newly created chat message to broadcast.</param>
		public async Task NewMessage(Guid gID)
		{
			try
			{
				// .NET 10 Migration: Replace DbProviderFactories.GetFactory(this.Context.Application)
				// with _dbProviderFactories.GetFactory(_memoryCache).
				// DbProviderFactories is now a DI singleton; IMemoryCache replaces Application[] state.
				DbProviderFactory dbf = _dbProviderFactories.GetFactory(_memoryCache);
				using ( IDbConnection con = dbf.CreateConnection() )
				{
					con.Open();
					if ( !Sql.IsEmptyGuid(gID) )
					{
						string sSQL ;
						sSQL = "select *              " + ControlChars.CrLf
						     + "  from vwCHAT_MESSAGES" + ControlChars.CrLf
						     + " where ID = @ID       " + ControlChars.CrLf;
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.CommandText = sSQL;
							Sql.AddParameter(cmd, "@ID", gID);
							using ( DbDataAdapter da = dbf.CreateDataAdapter() )
							{
								((IDbDataAdapter)da).SelectCommand = cmd;
								using ( DataTable dt = new DataTable() )
								{
									da.Fill(dt);
									if ( dt.Rows.Count > 0 )
									{
										DataRow row = dt.Rows[0];
										Guid     gCHAT_CHANNEL_ID    = Sql.ToGuid    (row["CHAT_CHANNEL_ID"   ]);
										string   sNAME               = Sql.ToString  (row["NAME"              ]);
										string   sDESCRIPTION        = Sql.ToString  (row["DESCRIPTION"       ]);
										DateTime dtDATE_ENTERED      = Sql.ToDateTime(row["DATE_ENTERED"      ]);
										Guid     gCREATED_BY_ID      = Sql.ToGuid    (row["CREATED_BY_ID"     ]);
										string   sCREATED_BY         = Sql.ToString  (row["CREATED_BY"        ]);
										string   sCREATED_BY_PICTURE = Sql.ToString  (row["CREATED_BY_PICTURE"]);
										Guid     gPARENT_ID          = Sql.ToGuid    (row["PARENT_ID"         ]);
										string   sPARENT_TYPE        = Sql.ToString  (row["PARENT_TYPE"       ]);
										string   sPARENT_NAME        = Sql.ToString  (row["PARENT_NAME"       ]);
										Guid     gNOTE_ATTACHMENT_ID = Sql.ToGuid    (row["NOTE_ATTACHMENT_ID"]);
										string   sFILENAME           = Sql.ToString  (row["FILENAME"          ]);
										string   sFILE_EXT           = Sql.ToString  (row["FILE_EXT"          ]);
										string   sFILE_MIME_TYPE     = Sql.ToString  (row["FILE_MIME_TYPE"    ]);
										long     lFILE_SIZE          = Sql.ToLong    (row["FILE_SIZE"         ]);
										bool     bATTACHMENT_READY   = Sql.ToBoolean (row["ATTACHMENT_READY"  ]);

										// .NET 10 Migration: Replace HttpContext.Current.Session["USER_SETTINGS/TIMEZONE"]
										// with IHttpContextAccessor.HttpContext?.Session.GetString() for distributed session.
										// ISession.GetString() returns string? compatible with Sql.ToGuid(object) overload.
										Guid     gTIMEZONE        = Sql.ToGuid  (_httpContextAccessor.HttpContext?.Session.GetString("USER_SETTINGS/TIMEZONE"));
										TimeZone T10n             = TimeZone.CreateTimeZone(gTIMEZONE);
										// 04/01/2020 Paul.  Move json utils to RestUtil. 
										string   sDATE_ENTERED    = RestUtil.ToJsonDate(T10n.FromServerTime(dtDATE_ENTERED));
										//Clients.Group(gCHAT_CHANNEL_ID.ToString()).newMessage(gCHAT_CHANNEL_ID, gID, sNAME, sDESCRIPTION, sDATE_ENTERED, NullID(gPARENT_ID), sPARENT_TYPE, sPARENT_NAME, NullID(gCREATED_BY_ID), sCREATED_BY, sCREATED_BY_PICTURE, NullID(gNOTE_ATTACHMENT_ID), sFILENAME, sFILE_EXT, sFILE_MIME_TYPE, lFILE_SIZE, bATTACHMENT_READY);
										//Clients.All.allMessage(gCHAT_CHANNEL_ID, gID, sDESCRIPTION, dtDATE_ENTERED, gUSER_ID, sCREATED_BY, NullID(gPARENT_ID), sPARENT_TYPE);
										// 04/27/2024 Paul.  SignalR core does not support more than 10 parameters, so convert to dictionary. 
										Dictionary<string, object> dict = _restUtil.ToJson("", "ChatMessages", dt.Rows[0], T10n);
										// .NET 10 Migration: Replace dynamic OWIN SignalR dispatch:
										//   BEFORE: Clients.Group(gCHAT_CHANNEL_ID.ToString()).newMessage(...)
										//   AFTER:  await _hubContext.Clients.Group(...).SendAsync("newMessage", ...)
										// The "newMessage" client-side method name is preserved exactly per AAP 0.8.1
										// (immutable interface rule: SignalR hub method signatures preserved for wire-protocol compatibility).
										await _hubContext.Clients.Group(gCHAT_CHANNEL_ID.ToString()).SendAsync("newMessage", (dict["d"] as Dictionary<string, object>)["results"]);
									}
								}
							}
						}
					}
				}
			}
			catch(Exception ex)
			{
				// .NET 10 Migration: Replace SplendidError.SystemMessage(Context, "Error", ...)
				// with SplendidError.SystemMessage(_httpContextAccessor.HttpContext, "Error", ...).
				// HttpContext is now obtained via IHttpContextAccessor injection, not HttpContext.Current static access.
				SplendidError.SystemMessage(_httpContextAccessor.HttpContext, "Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
			}
		}
	}
}
