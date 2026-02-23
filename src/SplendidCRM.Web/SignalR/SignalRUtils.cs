#nullable disable
using System;
using Microsoft.Extensions.Logging;

namespace SplendidCRM
{
	/// <summary>
	/// SignalR utilities — replaces OWIN startup with ASP.NET Core MapHub registration.
	/// Migrated from SplendidCRM/_code/SignalR/SignalRUtils.cs for .NET 10 ASP.NET Core.
	/// OWIN startup removed; hub mapping is done in Program.cs via MapHub<T>().
	/// </summary>
	public class SignalRUtils
	{
		private readonly ILogger<SignalRUtils> _logger;

		public SignalRUtils(ILogger<SignalRUtils> logger)
		{
			_logger = logger;
		}

		/// <summary>
		/// Initializes SignalR utilities during application startup.
		/// In ASP.NET Core, hub registration is done in Program.cs via MapHub<T>().
		/// This method is preserved for API compatibility with existing code that calls SignalRUtils.InitApp().
		/// </summary>
		public void InitApp()
		{
			_logger.LogInformation("SignalRUtils.InitApp: SignalR initialized (hub mapping done in Program.cs).");
		}
	}
}
