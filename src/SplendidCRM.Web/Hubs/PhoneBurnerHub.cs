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
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace SplendidCRM
{
	/// <summary>
	/// Strongly-typed client interface for PhoneBurner SignalR hub.
	/// Client-side methods must return Task per ASP.NET Core SignalR convention.
	/// </summary>
	public interface IPhoneBurnerClient
	{
		Task JoinGroup(string sConnectionId, string sGroupName);
	}

	/// <summary>
	/// Summary description for PhoneBurnerHub.
	/// ASP.NET Core SignalR hub for PhoneBurner integration.
	/// Hub path: /hubs/phoneburner (registered via MapHub in Program.cs)
	/// Migrated from OWIN-hosted SignalR — no direct source hub existed;
	/// created to match ChatManagerHub and TwilioManagerHub patterns.
	/// </summary>
	// NOTE: In ASP.NET Core SignalR, hub naming is controlled by MapHub<T>(path)
	// in Program.cs, not by [HubName] attribute (which does not exist in ASP.NET Core).
	// This hub is mapped to "/hubs/phoneburner".
	public class PhoneBurnerHub : Hub<IPhoneBurnerClient>
	{
		private readonly PhoneBurnerManager _phoneBurnerManager;

		public PhoneBurnerHub(PhoneBurnerManager phoneBurnerManager)
		{
			_phoneBurnerManager = phoneBurnerManager;
		}

		// Authorization is handled by the global SplendidHubAuthorize IHubFilter
		// registered in Program.cs via AddSignalR options.AddFilter<SplendidHubAuthorize>().
		// The original [SplendidHubAuthorize] attribute is not needed with ASP.NET Core IHubFilter.
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
	}
}
