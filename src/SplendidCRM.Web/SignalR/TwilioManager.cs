#nullable disable
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace SplendidCRM
{
	/// <summary>
	/// Twilio business logic behind the TwilioManagerHub.
	/// Migrated from SplendidCRM/_code/SignalR/TwilioManager.cs for .NET 10 ASP.NET Core.
	/// Replaces OWIN hub context with ASP.NET Core IHubContext.
	/// </summary>
	public class TwilioManager
	{
		private readonly ILogger<TwilioManager> _logger;
		private IHubContext<TwilioManagerHub, ITwilioClient> _hubContext;

		public TwilioManager(ILogger<TwilioManager> logger)
		{
			_logger = logger;
		}

		public void SetHubContext(IHubContext<TwilioManagerHub, ITwilioClient> hubContext)
		{
			_hubContext = hubContext;
		}

		public void InitApp()
		{
			_logger.LogInformation("TwilioManager.InitApp: Twilio manager initialized.");
		}

		public async Task NotifyIncomingCall(string sFrom, string sTo)
		{
			if (_hubContext != null)
			{
				await _hubContext.Clients.All.IncomingCall(string.Empty, sFrom, sTo);
			}
		}
	}
}
