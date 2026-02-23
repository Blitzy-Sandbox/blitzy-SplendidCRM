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
	/// IHostedService wrapping SchedulerUtils.OnArchiveTimer — replaces Global.asax.cs archive timer.
	/// Interval from ARCHIVE_INTERVAL_MS env var (default: 300000ms).
	/// SemaphoreSlim(1,1) reentrancy guard replacing bInsideArchiveTimer.
	/// </summary>
	public class ArchiveHostedService : BackgroundService
	{
		private readonly IServiceProvider _serviceProvider;
		private readonly IConfiguration _configuration;
		private readonly ILogger<ArchiveHostedService> _logger;
		private readonly SemaphoreSlim _reentrancyGuard = new SemaphoreSlim(1, 1);

		public ArchiveHostedService(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<ArchiveHostedService> logger)
		{
			_serviceProvider = serviceProvider;
			_configuration = configuration;
			_logger = logger;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			int nIntervalMs = _configuration.GetValue<int>("Scheduler:ArchiveIntervalMs", 300000);
			if (nIntervalMs < 1000) nIntervalMs = 300000;
			_logger.LogInformation("ArchiveHostedService started with interval {Interval}ms", nIntervalMs);
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
								var schedulerUtils = scope.ServiceProvider.GetRequiredService<SchedulerUtils>();
								schedulerUtils.OnArchiveTimer();
							}
						}
						catch (Exception ex)
						{
							_logger.LogError(ex, "ArchiveHostedService error");
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
				_logger.LogInformation("ArchiveHostedService stopping gracefully.");
			}
		}
	}
}
