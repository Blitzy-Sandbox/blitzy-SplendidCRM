#nullable disable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SplendidCRM.Web.Services
{
	/// <summary>
	/// IHostedService for email polling — replaces Global.asax.cs email timer.
	/// Interval from EMAIL_POLL_INTERVAL_MS env var (default: 60000ms).
	/// SemaphoreSlim(1,1) reentrancy guard.
	/// </summary>
	public class EmailPollingHostedService : BackgroundService
	{
		private readonly IServiceProvider _serviceProvider;
		private readonly IConfiguration _configuration;
		private readonly ILogger<EmailPollingHostedService> _logger;
		private readonly SemaphoreSlim _reentrancyGuard = new SemaphoreSlim(1, 1);

		public EmailPollingHostedService(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<EmailPollingHostedService> logger)
		{
			_serviceProvider = serviceProvider;
			_configuration = configuration;
			_logger = logger;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			int nIntervalMs = _configuration.GetValue<int>("Scheduler:EmailPollIntervalMs", 60000);
			if (nIntervalMs < 1000) nIntervalMs = 60000;
			_logger.LogInformation("EmailPollingHostedService started with interval {Interval}ms", nIntervalMs);
			try
			{
				await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
				while (!stoppingToken.IsCancellationRequested)
				{
					if (_reentrancyGuard.Wait(0))
					{
						try
						{
							using (var scope = _serviceProvider.CreateScope())
							{
								var emailUtils = scope.ServiceProvider.GetRequiredService<EmailUtils>();
								emailUtils.OnTimer();
							}
						}
						catch (Exception ex)
						{
							_logger.LogError(ex, "EmailPollingHostedService error");
						}
						finally
						{
							_reentrancyGuard.Release();
						}
					}
					await Task.Delay(TimeSpan.FromMilliseconds(nIntervalMs), stoppingToken);
				}
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				// Graceful shutdown — cancellation is expected when the host stops.
				_logger.LogInformation("EmailPollingHostedService stopping gracefully.");
			}
		}
	}
}
