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
	/// IHostedService wrapping SchedulerUtils.OnTimer — replaces Global.asax.cs scheduler timer.
	/// Interval from SCHEDULER_INTERVAL_MS env var (default: 60000ms).
	/// SemaphoreSlim(1,1) reentrancy guard replaces legacy bInsideTimer boolean.
	/// </summary>
	public class SchedulerHostedService : BackgroundService
	{
		private readonly IServiceProvider _serviceProvider;
		private readonly IConfiguration _configuration;
		private readonly ILogger<SchedulerHostedService> _logger;
		private readonly SemaphoreSlim _reentrancyGuard = new SemaphoreSlim(1, 1);

		public SchedulerHostedService(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<SchedulerHostedService> logger)
		{
			_serviceProvider = serviceProvider;
			_configuration = configuration;
			_logger = logger;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			int nIntervalMs = _configuration.GetValue<int>("Scheduler:IntervalMs", 60000);
			if (nIntervalMs < 1000) nIntervalMs = 60000;
			_logger.LogInformation("SchedulerHostedService started with interval {Interval}ms", nIntervalMs);
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
								schedulerUtils.OnTimer();
							}
						}
						catch (Exception ex)
						{
							_logger.LogError(ex, "SchedulerHostedService.ExecuteAsync error");
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
				_logger.LogInformation("SchedulerHostedService stopping gracefully.");
			}
		}
	}
}
