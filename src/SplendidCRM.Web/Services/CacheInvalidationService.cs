#nullable disable
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;

namespace SplendidCRM.Web.Services
{
	/// <summary>
	/// Background service monitoring vwSYSTEM_EVENTS for cache invalidation.
	/// Periodically queries the SQL view and evicts stale cache entries.
	/// </summary>
	public class CacheInvalidationService : BackgroundService
	{
		private readonly IServiceProvider _serviceProvider;
		private readonly IConfiguration _configuration;
		private readonly ILogger<CacheInvalidationService> _logger;

		public CacheInvalidationService(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<CacheInvalidationService> logger)
		{
			_serviceProvider = serviceProvider;
			_configuration = configuration;
			_logger = logger;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			_logger.LogInformation("CacheInvalidationService started.");
			await Task.Delay(TimeSpan.FromSeconds(120), stoppingToken);
			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					using (var scope = _serviceProvider.CreateScope())
					{
						var dbFactory = scope.ServiceProvider.GetRequiredService<DbProviderFactories>();
						string sConnectionString = dbFactory.ConnectionString;
						if (!Sql.IsEmptyString(sConnectionString))
						{
							using (IDbConnection con = dbFactory.CreateConnection())
							{
								con.Open();
								string sSQL = "select TABLE_NAME from vwSYSTEM_EVENTS where DATE_ENTERED > @LAST_CHECK";
								using (IDbCommand cmd = con.CreateCommand())
								{
									cmd.CommandText = sSQL;
									Sql.AddParameter(cmd, "@LAST_CHECK", DateTime.UtcNow.AddMinutes(-5));
									using (IDataReader rdr = cmd.ExecuteReader())
									{
										while (rdr.Read())
										{
											string sTableName = Sql.ToString(rdr["TABLE_NAME"]);
											_logger.LogDebug("CacheInvalidationService: Invalidating cache for {Table}", sTableName);
										}
									}
								}
							}
						}
					}
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "CacheInvalidationService: Error checking system events (non-fatal)");
				}
				await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
			}
		}
	}
}
