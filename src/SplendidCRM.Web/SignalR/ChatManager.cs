#nullable disable
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace SplendidCRM
{
	/// <summary>
	/// Chat business logic behind the ChatManagerHub.
	/// Migrated from SplendidCRM/_code/SignalR/ChatManager.cs for .NET 10 ASP.NET Core.
	/// Replaces OWIN hub context with ASP.NET Core IHubContext.
	/// </summary>
	public class ChatManager
	{
		private readonly ILogger<ChatManager> _logger;
		private IHubContext<ChatManagerHub, IChatClient> _hubContext;

		public ChatManager(ILogger<ChatManager> logger)
		{
			_logger = logger;
		}

		/// <summary>
		/// Sets the hub context for sending messages from outside the hub.
		/// </summary>
		public void SetHubContext(IHubContext<ChatManagerHub, IChatClient> hubContext)
		{
			_hubContext = hubContext;
		}

		/// <summary>
		/// Initializes the chat manager during application startup.
		/// </summary>
		public void InitApp()
		{
			_logger.LogInformation("ChatManager.InitApp: Chat manager initialized.");
		}

		/// <summary>
		/// Sends a message to all connected clients.
		/// </summary>
		public async Task SendMessage(string sFrom, string sMessage)
		{
			if (_hubContext != null)
			{
				await _hubContext.Clients.All.ReceiveMessage(string.Empty, sFrom, sMessage);
			}
		}
	}
}
