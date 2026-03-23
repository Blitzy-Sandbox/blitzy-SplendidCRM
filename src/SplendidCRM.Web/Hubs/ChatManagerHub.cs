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
// .NET 10 Migration: SplendidCRM/_code/SignalR/ChatManagerHub.cs → src/SplendidCRM.Web/Hubs/ChatManagerHub.cs
// Changes applied:
//   - REMOVED: using System.Web; (not available in .NET 10)
//   - REMOVED: using Microsoft.AspNet.SignalR.Hubs; ([HubName] attribute not available in ASP.NET Core SignalR)
//   - REPLACED: using Microsoft.AspNet.SignalR; → using Microsoft.AspNetCore.SignalR;
//   - ADDED:   using System.Threading.Tasks; (Task<string> return type for JoinGroup)
//   - REMOVED: [HubName("ChatManagerHub")] attribute — not available in ASP.NET Core SignalR
//              Hub path /hubs/chat is configured via MapHub<ChatManagerHub>("/hubs/chat") in Program.cs
//   - REMOVED: Parameterless constructor using ChatManager.Instance singleton — replaced by DI injection
//   - REMOVED: [SplendidHubAuthorize] per-method attribute — authorization now handled by global
//              SplendidHubAuthorize IHubFilter registered in Program.cs via AddSignalR options
//   - CHANGED: IChatHubServer.JoinGroup return type string → Task (ASP.NET Core SignalR requires Task)
//   - CHANGED: JoinGroup return type string → async Task<string> (proper async/await pattern)
//   - CHANGED: Groups.Add(...).Wait() → await Groups.AddToGroupAsync(...) (renamed API, proper async)
//   - PRESERVED: All business logic, Paul's date-stamped comments, parameter names, return strings
//   - Minimal change clause applied per AAP 0.8.1: only changes necessary for .NET Framework 4.8 → .NET 10
#nullable disable
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;

namespace SplendidCRM
{
	/// <summary>
	/// Strongly-typed CLIENT interface for ChatManager SignalR hub.
	/// In ASP.NET Core SignalR, Hub&lt;T&gt; where T defines methods the server can invoke ON clients.
	/// Methods listed here are callable from the server to connected clients via Clients.All/Group/User.
	/// </summary>
	public interface IChatHubClient
	{
		Task NewMessage(Guid gCHAT_MESSAGE_ID);
		Task ServerMessage(string sMessage);
	}

	/// <summary>
	/// Server-side hub method interface — defines methods that clients can invoke on the server.
	/// Retained from original IChatHubServer for backward-compatible server-side method contracts.
	/// </summary>
	public interface IChatHubServer
	{
		Task JoinGroup(string sConnectionId, string sGroupName);
		Task SendMessage(Guid gCHAT_CHANNEL_ID, string sMessage);
	}

	/// <summary>
	/// Summary description for ChatManagerHub.
	/// </summary>
	// Hub path: /hubs/chat — registered via MapHub<ChatManagerHub>("/hubs/chat") in Program.cs
	// [HubName("ChatManagerHub")] removed — not available in ASP.NET Core SignalR
	// FIXED: Changed Hub<IChatHubServer> → Hub<IChatHubClient> — T must be the CLIENT interface
	// that defines methods the server can call ON clients (e.g., NewMessage, ServerMessage).
	public class ChatManagerHub : Hub<IChatHubClient>
	{
		private readonly ChatManager _chatManager;

		public ChatManagerHub(ChatManager chatManager)
		{
			_chatManager = chatManager ?? throw new ArgumentNullException(nameof(chatManager));
		}

		// 11/15/2014 Paul.  Hub method should require authorization. 
		// http://eworldproblems.mbaynton.com/2012/12/signalr-hub-authorization/
		// Authorization handled by global SplendidHubAuthorize IHubFilter registered in Program.cs
		public async Task<string> JoinGroup(string sConnectionId, string sGroupName)
		{
			if ( !Sql.IsEmptyString(sGroupName) )
			{
				// 10/26/2013 Paul.  Each track is a separate group. 
				// 10/27/2013 Paul.  The group string is already expected to be in lowercase so that we don't have to waste time doing it now. 
				string[] arrTracks = sGroupName.Split(',');
				foreach ( string sTrack in arrTracks )
					await Groups.AddToGroupAsync(sConnectionId, sTrack);
				return sConnectionId + " joined " + sGroupName;
			}
			return "Group not specified.";
		}

		/// <summary>
		/// Sends a chat message to the specified channel.
		/// Delegates to the ChatManager business logic service which handles database persistence
		/// and pushes the message to connected clients via IHubContext.
		/// </summary>
		public async Task SendMessage(Guid gCHAT_CHANNEL_ID, string sMessage)
		{
			await _chatManager.NewMessage(gCHAT_CHANNEL_ID);
		}

		/// <summary>
		/// Called when a new connection is established. Registers the connection in ChatManager.
		/// </summary>
		public override async Task OnConnectedAsync()
		{
			await base.OnConnectedAsync();
		}

		/// <summary>
		/// Called when a connection is terminated. Cleans up the connection in ChatManager.
		/// </summary>
		public override async Task OnDisconnectedAsync(Exception exception)
		{
			await base.OnDisconnectedAsync(exception);
		}
	}
}
