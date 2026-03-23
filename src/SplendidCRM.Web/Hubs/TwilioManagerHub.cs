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
// .NET 10 Migration: SplendidCRM/_code/SignalR/TwilioManagerHub.cs → src/SplendidCRM.Web/Hubs/TwilioManagerHub.cs
// Changes applied:
//   - REMOVED: using System.Web; — not available in .NET 10
//   - REPLACED: using Microsoft.AspNet.SignalR; → using Microsoft.AspNetCore.SignalR;
//   - REMOVED: using Microsoft.AspNet.SignalR.Hubs; — [HubName] attribute not available in ASP.NET Core SignalR
//   - ADDED: using System.Threading.Tasks; — required for Task<string> and Task async return types
//   - REMOVED: [HubName("TwilioManagerHub")] attribute — not available in ASP.NET Core SignalR
//              Hub path set via MapHub<TwilioManagerHub>("/hubs/twilio") in Program.cs
//   - CHANGED: ITwilioServer interface methods return Task (required by ASP.NET Core strongly-typed hub client interface)
//   - REMOVED: parameterless constructor TwilioManagerHub() : this(TwilioManager.Instance)
//              — static singleton TwilioManager.Instance eliminated; TwilioManager now DI-injected service
//   - REMOVED: [SplendidHubAuthorize] attribute — authorization handled by global IHubFilter in Program.cs
//   - CHANGED: JoinGroup return type string → async Task<string>
//   - CHANGED: Groups.Add(sConnectionId, sGroupName).Wait() → await Groups.AddToGroupAsync(sConnectionId, sGroupName)
//   - PRESERVED: namespace SplendidCRM; ITwilioServer interface name; TwilioManagerHub class name;
//               all parameter names; all Paul's date-stamped comments; all commented-out debug code;
//               JoinGroup business logic (NormalizePhone + RemoveCountryCode); CreateSmsMessage delegation;
//               Guid return type on CreateSmsMessage (server-side method, not client interface method)
//   - Minimal change clause: only changes necessary for .NET Framework 4.8 → .NET 10 ASP.NET Core transition
#nullable disable
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;

namespace SplendidCRM
{
	/// <summary>
	/// Strongly-typed CLIENT interface for Twilio SignalR hub.
	/// In ASP.NET Core SignalR, Hub&lt;T&gt; where T defines methods the server can invoke ON clients.
	/// Methods listed here are callable from the server to connected clients via Clients.All/Group/User.
	/// </summary>
	public interface ITwilioClient
	{
		Task IncomingMessage(string sMESSAGE_SID, string sFROM_NUMBER, string sTO_NUMBER, string sSUBJECT);
	}

	/// <summary>
	/// Server-side hub method interface — defines methods that clients can invoke on the server.
	/// Retained from original ITwilioServer for backward-compatible server-side method contracts.
	/// </summary>
	public interface ITwilioServer
	{
		Task JoinGroup(string sConnectionId, string sGroupName);
		Task<Guid> CreateSmsMessage(string sMESSAGE_SID, string sFROM_NUMBER, string sTO_NUMBER, string sSUBJECT);
	}

	/// <summary>
	/// Summary description for TwilioManagerHub.
	/// </summary>
	// Hub path: /hubs/twilio — registered via MapHub<TwilioManagerHub>("/hubs/twilio") in Program.cs
	// [HubName("TwilioManagerHub")] removed — not available in ASP.NET Core SignalR
	// FIXED: Changed Hub<ITwilioServer> → Hub<ITwilioClient> — T must be the CLIENT interface
	// that defines methods the server can call ON clients (e.g., IncomingMessage).
	public class TwilioManagerHub : Hub<ITwilioClient>
	{
		private readonly TwilioManager _twilioManager;

		public TwilioManagerHub(TwilioManager twilioManager)
		{
			_twilioManager = twilioManager ?? throw new ArgumentNullException(nameof(twilioManager));
		}

		// 11/15/2014 Paul.  Hub method should require authorization. 
		// http://eworldproblems.mbaynton.com/2012/12/signalr-hub-authorization/
		// Authorization handled by global SplendidHubAuthorize IHubFilter registered in Program.cs
		public async Task<string> JoinGroup(string sConnectionId, string sGroupName)
		{
			// 09/02/2013 Paul.  The the.Context.User.Identity value is not the same as HttpContext.Current.User, so we don't know who this is. 
			//if ( this.Context.User != null && this.Context.User.Identity != null )
			//	Debug.WriteLine(this.Context.User.Identity.Name);
			if ( !Sql.IsEmptyString(sGroupName) )
			{
				sGroupName = Utils.NormalizePhone(TwilioManager.RemoveCountryCode(sGroupName));
				await Groups.AddToGroupAsync(sConnectionId, sGroupName);
				return sConnectionId + " joined " + sGroupName;
			}
			return "Group not specified.";
		}

		// 11/15/2014 Paul.  Hub method should require authorization. 
		// http://eworldproblems.mbaynton.com/2012/12/signalr-hub-authorization/
		// Authorization handled by global SplendidHubAuthorize IHubFilter registered in Program.cs
		// FIXED: Changed return type from Guid to async Task<Guid> for proper ASP.NET Core SignalR async support
		public async Task<Guid> CreateSmsMessage(string sMESSAGE_SID, string sFROM_NUMBER, string sTO_NUMBER, string sSUBJECT)
		{
			return await Task.FromResult(_twilioManager.CreateSmsMessage(sMESSAGE_SID, sFROM_NUMBER, sTO_NUMBER, sSUBJECT, String.Empty, String.Empty));
		}
	}
}
