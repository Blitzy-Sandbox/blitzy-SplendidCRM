#nullable disable
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace SplendidCRM
{
	/// <summary>
	/// Strongly-typed client interface for Chat SignalR hub.
	/// </summary>
	public interface IChatClient
	{
		Task ReceiveMessage(string sConnectionId, string sFrom, string sMessage);
		Task JoinGroup(string sConnectionId, string sGroupName);
	}

	/// <summary>
	/// ASP.NET Core SignalR hub for Chat integration.
	/// Hub path: /hubs/chat (registered via MapHub in Program.cs).
	/// Migrated from OWIN-hosted SignalR ChatManagerHub.cs.
	/// Preserves method signatures for wire-protocol compatibility.
	/// </summary>
	public class ChatManagerHub : Hub<IChatClient>
	{
		private readonly ChatManager _chatManager;

		public ChatManagerHub(ChatManager chatManager)
		{
			_chatManager = chatManager;
		}

		public async Task<string> JoinGroup(string sConnectionId, string sGroupName)
		{
			if (Sql.IsEmptyString(sConnectionId)) sConnectionId = Context.ConnectionId;
			await Groups.AddToGroupAsync(sConnectionId, sGroupName);
			return sConnectionId;
		}

		public async Task SendMessage(string sFrom, string sMessage)
		{
			await Clients.All.ReceiveMessage(Context.ConnectionId, sFrom, sMessage);
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
