#nullable disable
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace SplendidCRM
{
	/// <summary>
	/// Strongly-typed client interface for Twilio SignalR hub.
	/// </summary>
	public interface ITwilioClient
	{
		Task IncomingCall(string sConnectionId, string sFrom, string sTo);
		Task JoinGroup(string sConnectionId, string sGroupName);
	}

	/// <summary>
	/// ASP.NET Core SignalR hub for Twilio integration.
	/// Hub path: /hubs/twilio (registered via MapHub in Program.cs).
	/// Migrated from OWIN-hosted SignalR TwilioManagerHub.cs.
	/// Preserves method signatures for wire-protocol compatibility.
	/// </summary>
	public class TwilioManagerHub : Hub<ITwilioClient>
	{
		private readonly TwilioManager _twilioManager;

		public TwilioManagerHub(TwilioManager twilioManager)
		{
			_twilioManager = twilioManager;
		}

		public async Task<string> JoinGroup(string sConnectionId, string sGroupName)
		{
			if (Sql.IsEmptyString(sConnectionId)) sConnectionId = Context.ConnectionId;
			await Groups.AddToGroupAsync(sConnectionId, sGroupName);
			return sConnectionId;
		}

		public override async Task OnConnectedAsync()
		{
			await base.OnConnectedAsync();
		}

		public override async Task OnDisconnectedAsync(Exception exception)
		{
			await base.OnDisconnectedAsync(exception);
		}
	}
}
