/**********************************************************************************************************************
 * SplendidCRM is a Customer Relationship Management program created by SplendidCRM Software, Inc.
 * Copyright (C) 2005-2023 SplendidCRM Software, Inc. All rights reserved.
 *
 * Any use of the contents of this file are subject to the SplendidCRM Community Source Code License
 * Agreement, or other written agreement between you and SplendidCRM ("License"). By installing or
 * using this file, you have unconditionally agreed to the terms and conditions of the License,
 * including but not limited to restrictions on the number of users therein, and you may not use this
 * file except in compliance with the License.
 *********************************************************************************************************************/
// .NET 10 Migration: SplendidCRM/Global.asax.cs (InitArchiveManager, lines 72-80)
//                  + SplendidCRM/_code/SchedulerUtils.cs (OnArchiveTimer, lines 892-1010)
//   → src/SplendidCRM.Web/Services/ArchiveHostedService.cs
//
// Changes applied:
//   - REPLACED: System.Threading.Timer callback (Global.asax.cs line 77) →
//               BackgroundService (IHostedService) + Task.Delay timer loop
//       Original: tArchiveManager = new Timer(SchedulerUtils.OnArchiveTimer, this.Context,
//                     new TimeSpan(0, 1, 0), new TimeSpan(0, 5, 0));
//       Target  : ExecuteAsync with 60 000 ms initial delay and configurable interval loop
//
//   - REPLACED: public static bool bInsideArchiveTimer (SchedulerUtils.cs line 36) →
//               SemaphoreSlim(1, 1) with non-blocking Wait(0)
//       Non-blocking Wait(0) preserves the "skip-on-busy" semantics: if a previous tick is still
//       running when the next tick fires, the new tick is silently skipped (not queued).
//
//   - REPLACED: Context.Application["SplendidJobServer"] → IMemoryCache.Get<object>("SplendidJobServer")
//   - REPLACED: Context.Application["CONFIG.*"] → IMemoryCache.Get<object>("CONFIG.*")
//   - REPLACED: ConfigurationManager.AppSettings["SplendidJobServer"] → IConfiguration["SPLENDID_JOB_SERVER"]
//   - REPLACED: DbProviderFactories.GetFactory(Context.Application) → dbProviderFactories.GetFactory(IMemoryCache)
//   - REPLACED: SplendidError.SystemMessage(Context, "Warning", ...) → ILogger.LogWarning(...)
//   - REPLACED: SplendidError.SystemMessage(Context, "Error", ...) → ILogger.LogError(...)
//
//   - PRESERVED: Machine-name-based job server election logic (SchedulerUtils.cs lines 900-924)
//   - PRESERVED: vwSCHEDULERS_Run query filtered to archive jobs with CommandTimeout = 15
//                (SchedulerUtils.cs lines 927-947)
//   - PRESERVED: Per-job execution loop with sJOB, gID, dtLAST_RUN tracking
//                (SchedulerUtils.cs lines 953-991)
//   - PRESERVED: spSCHEDULERS_UpdateLastRun in finally block — MUST execute even on job failure
//                (SchedulerUtils.cs lines 974-991)
//   - PRESERVED: CONFIG.suppress_scheduler_warning flag for job count and start/end logging
//   - PRESERVED: CONFIG.Scheduler.Verbose flag for "Archive Jobs Busy" warning
//   - PRESERVED: Archive-specific job names: RunAllArchiveRules, RunExternalArchive
//   - PRESERVED: CONFIG.Archive.SeparateTimer conditional activation (Global.asax.cs line 204)
//   - ADDED: IServiceScopeFactory for creating scoped DI containers per timer tick
//   - ADDED: Initial 60-second delay (matching Global.asax.cs line 77: new TimeSpan(0, 1, 0))
//   - ADDED: Configurable interval via ARCHIVE_INTERVAL_MS environment variable (default 300 000 ms)
//   - Minimal change clause: Only changes necessary for .NET Framework 4.8 → .NET 10 ASP.NET Core

#nullable disable
using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SplendidCRM
{
	/// <summary>
	/// IHostedService that replaces the archive timer in Global.asax.cs (<c>InitArchiveManager</c>).
	///
	/// Original source:
	///   Global.asax.cs lines 72-80: <c>tArchiveManager = new Timer(SchedulerUtils.OnArchiveTimer, this.Context,
	///   new TimeSpan(0, 1, 0), new TimeSpan(0, 5, 0));</c>
	///   SchedulerUtils.cs lines 892-1010: <c>OnArchiveTimer(Object sender)</c> callback implementation.
	///
	/// <para>
	/// This service only activates when <c>CONFIG.Archive.SeparateTimer</c> is set to <c>true</c>
	/// in the application cache, matching Global.asax.cs line 204:
	/// <c>if (Sql.ToBoolean(Application["CONFIG.Archive.SeparateTimer"])) { InitArchiveManager(); }</c>
	/// </para>
	///
	/// Key design decisions:
	/// <list type="bullet">
	///   <item><description>
	///     <c>SemaphoreSlim(1,1)</c> with non-blocking <c>Wait(0)</c> replaces
	///     <c>public static bool bInsideArchiveTimer</c> (SchedulerUtils.cs line 36) — overlapping
	///     ticks are SKIPPED (not queued), matching the original behavior exactly.
	///   </description></item>
	///   <item><description>
	///     <c>IServiceScopeFactory</c> creates a new DI scope per timer tick so that scoped services
	///     (SchedulerUtils, DbProviderFactories) are properly lifetime-managed.
	///     BackgroundService is registered as a singleton, but the business logic services may be scoped.
	///   </description></item>
	///   <item><description>
	///     Machine-name-based job election (<c>SPLENDID_JOB_SERVER</c> env var vs
	///     <c>Environment.MachineName</c>) is preserved to support load-balanced deployments where
	///     only one server runs scheduled archive jobs.
	///   </description></item>
	///   <item><description>
	///     Only <c>RunAllArchiveRules</c> and <c>RunExternalArchive</c> jobs are executed here
	///     (not the full scheduler job list), matching the original archive timer filtering.
	///   </description></item>
	/// </list>
	/// </summary>
	public class ArchiveHostedService : BackgroundService
	{
		// ====================================================================================
		// DI-injected services — replacing Global.asax.cs timer and HttpContext/Application access
		// ====================================================================================

		/// <summary>
		/// Factory for creating scoped DI containers per timer tick.
		/// Replaces the <c>HttpContext Context</c> passed as timer state in <c>Global.asax.cs</c> line 77.
		/// BackgroundService is a singleton; SchedulerUtils and DbProviderFactories may be scoped.
		/// </summary>
		private readonly IServiceScopeFactory             _scopeFactory ;

		/// <summary>
		/// Configuration abstraction for reading <c>ARCHIVE_INTERVAL_MS</c> (timer interval,
		/// default 300 000 ms) and <c>SPLENDID_JOB_SERVER</c> (machine-name job election).
		/// Replaces <c>System.Configuration.ConfigurationManager.AppSettings</c>
		/// from SchedulerUtils.cs line 903.
		/// </summary>
		private readonly IConfiguration                   _configuration;

		/// <summary>
		/// Memory cache replacing <c>Application[]</c> state for CONFIG values and job server
		/// election result caching.
		/// Replaces <c>Context.Application["SplendidJobServer"]</c>, <c>Context.Application["CONFIG.*"]</c>.
		/// </summary>
		private readonly IMemoryCache                     _memoryCache  ;

		/// <summary>
		/// Structured logger replacing <c>SplendidError.SystemMessage</c> and
		/// <c>SplendidError.SystemWarning</c> for all diagnostic output.
		/// </summary>
		private readonly ILogger<ArchiveHostedService>    _logger       ;

		// ====================================================================================
		// Reentrancy guard — replaces public static bool bInsideArchiveTimer (SchedulerUtils.cs line 36)
		//
		// BEFORE (.NET Framework 4.8):
		//   public static bool bInsideArchiveTimer = false;
		//   if ( !bInsideArchiveTimer ) { bInsideArchiveTimer = true; try { ... } finally { bInsideArchiveTimer = false; } }
		//
		// AFTER (.NET 10 ASP.NET Core):
		//   private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
		//   if (_semaphore.Wait(0)) { try { ... } finally { _semaphore.Release(); } }
		//
		// The non-blocking Wait(0) preserves SKIP semantics (not queuing) — if the previous tick
		// is still running when the next tick fires, the new tick is silently skipped.
		// ====================================================================================
		private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

		/// <summary>
		/// Last archive job name for "Archive Jobs Busy" logging.
		/// Replaces the shared static <c>sLastJob</c> (SchedulerUtils.cs line 38).
		/// Scoped to the archive timer only — does not conflict with scheduler's _lastJob.
		/// </summary>
		private string _lastJob = String.Empty;

		/// <summary>
		/// Initializes a new instance of <see cref="ArchiveHostedService"/> with DI-injected services.
		/// </summary>
		/// <param name="scopeFactory">
		/// Factory for creating scoped DI containers per timer tick.
		/// Replaces the <c>HttpContext Context</c> passed as timer state in Global.asax.cs line 77.
		/// </param>
		/// <param name="configuration">
		/// Configuration for reading <c>ARCHIVE_INTERVAL_MS</c> (default 300 000 ms = 5 minutes)
		/// and <c>SPLENDID_JOB_SERVER</c> (machine-name job election).
		/// Replaces <c>System.Configuration.ConfigurationManager.AppSettings</c>.
		/// </param>
		/// <param name="memoryCache">
		/// Memory cache replacing <c>Application[]</c> state for CONFIG values and job election caching.
		/// Replaces <c>Context.Application["SplendidJobServer"]</c>, <c>Context.Application["CONFIG.*"]</c>.
		/// Also passed to <see cref="DbProviderFactories.GetFactory(IMemoryCache)"/> for DB factory creation.
		/// </param>
		/// <param name="logger">
		/// Structured logger replacing <c>SplendidError.SystemMessage</c> for all diagnostic output.
		/// </param>
		public ArchiveHostedService(
			IServiceScopeFactory          scopeFactory ,
			IConfiguration                configuration,
			IMemoryCache                  memoryCache  ,
			ILogger<ArchiveHostedService> logger       )
		{
			_scopeFactory  = scopeFactory ;
			_configuration = configuration;
			_memoryCache   = memoryCache  ;
			_logger        = logger       ;
		}

		/// <summary>
		/// Starts the background service.
		/// Overridden to add a startup log message before delegating to the base BackgroundService lifecycle.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token for startup cancellation.</param>
		public override async Task StartAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("ArchiveHostedService: StartAsync called. Service is starting.");
			await base.StartAsync(cancellationToken);
		}

		/// <summary>
		/// Stops the background service.
		/// Overridden to log graceful shutdown before delegating to the base BackgroundService lifecycle.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token for forced stop.</param>
		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("ArchiveHostedService: StopAsync called. Service is stopping.");
			await base.StopAsync(cancellationToken);
		}

		/// <summary>
		/// Main background service execution loop.
		///
		/// Provides an initial 1-minute delay (matching Global.asax.cs line 77:
		/// <c>new TimeSpan(0, 1, 0)</c>) to allow application startup and SplendidInit.InitApp()
		/// to complete before the first archive timer tick fires.
		///
		/// Then fires <see cref="DoWorkAsync"/> on the configured <c>ARCHIVE_INTERVAL_MS</c>
		/// interval (default: 300 000 ms = 5 minutes, matching Global.asax.cs line 77:
		/// <c>new TimeSpan(0, 5, 0)</c>), using a <see cref="SemaphoreSlim"/> non-blocking
		/// reentrancy guard to skip overlapping ticks.
		///
		/// <para>
		/// Activation of the loop is conditional on <c>CONFIG.Archive.SeparateTimer</c> being
		/// <c>true</c> (checked per tick inside <see cref="DoWorkAsync"/>), matching the original
		/// Global.asax.cs line 204 conditional: <c>if (Sql.ToBoolean(Application["CONFIG.Archive.SeparateTimer"]))</c>.
		/// </para>
		/// </summary>
		/// <param name="stoppingToken">
		/// Cancellation token injected by BackgroundService for graceful application shutdown.
		/// </param>
		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			// Activation log — matching Global.asax.cs line 78 warning message:
			// BEFORE: SplendidError.SystemWarning(new StackTrace(true).GetFrame(0),
			//             "The Archive Manager timer has been activated.");
			// AFTER:  _logger.LogInformation(...)
			_logger.LogInformation("The Archive Manager hosted service has been activated.");

			// Initial delay of 1 minute — matching Global.asax.cs line 77: new TimeSpan(0, 1, 0)
			// Allows SplendidInit.InitApp() to populate IMemoryCache with CONFIG values before
			// the first archive timer tick checks CONFIG.Archive.SeparateTimer.
			// BEFORE: new Timer(SchedulerUtils.OnArchiveTimer, this.Context, new TimeSpan(0, 1, 0), ...)
			// AFTER:  await Task.Delay(TimeSpan.FromMilliseconds(60000), stoppingToken)
			try
			{
				await Task.Delay(TimeSpan.FromMilliseconds(60000), stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				_logger.LogInformation("ArchiveHostedService: Cancelled during initial delay.");
				return;
			}

			// Read configurable interval from ARCHIVE_INTERVAL_MS (default: 300 000 ms = 5 minutes)
			// Matching Global.asax.cs line 77: new TimeSpan(0, 5, 0) for the repeating interval.
			// BEFORE: new Timer(SchedulerUtils.OnArchiveTimer, this.Context, ..., new TimeSpan(0, 5, 0))
			// AFTER:  Task.Delay(TimeSpan.FromMilliseconds(nIntervalMs), stoppingToken)
			int    nIntervalMs = 300000;
			string sIntervalMs = _configuration["ARCHIVE_INTERVAL_MS"];
			if (!String.IsNullOrEmpty(sIntervalMs) && Int32.TryParse(sIntervalMs, out int nParsed) && nParsed >= 1000)
				nIntervalMs = nParsed;

			_logger.LogInformation("ArchiveHostedService: Using interval of {IntervalMs}ms.", nIntervalMs);

			// Timer loop — fires until the application stops
			while (!stoppingToken.IsCancellationRequested)
			{
				// Non-blocking reentrancy guard — replacing: if (!bInsideArchiveTimer) { bInsideArchiveTimer = true; ... }
				// (SchedulerUtils.cs line 895)
				//
				// _semaphore.Wait(0) returns immediately:
				//   true  = lock acquired → this tick runs DoWorkAsync
				//   false = already locked → previous tick still running → skip this tick
				//
				// This preserves SKIP semantics (not queue semantics): an overlapping tick is
				// silently discarded, matching the original bInsideArchiveTimer pattern exactly.
				if (_semaphore.Wait(0))
				{
					try
					{
						await DoWorkAsync(stoppingToken);
					}
					catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
					{
						// Graceful shutdown — break out of the loop without logging an error
						break;
					}
					catch (Exception ex)
					{
						// Catch and log — do NOT rethrow; keep the loop alive across transient errors
						// Matches SchedulerUtils.cs lines 996-999: outer catch around the archive timer body
						_logger.LogError(ex, "ArchiveHostedService.ExecuteAsync: Unhandled error from DoWorkAsync");
					}
					finally
					{
						// Always release the semaphore — replacing: bInsideArchiveTimer = false (line 1002)
						_semaphore.Release();
					}
				}
				else
				{
					// Archive timer is still busy from a previous tick — matching SchedulerUtils.cs lines 1005-1009
					// 11/02/2022 Paul.  Keep track of last job for verbose logging.
					// Original: else if ( !Sql.ToBoolean(Context.Application["CONFIG.Scheduler.Verbose"]) )
					// Preserved: only log "Archive Jobs Busy" when NOT in verbose mode (matches original behavior)
					if (!Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.Scheduler.Verbose")))
						_logger.LogWarning("Archive Jobs Busy: {LastJob}", _lastJob);
				}

				// Wait for next interval tick
				try
				{
					await Task.Delay(TimeSpan.FromMilliseconds(nIntervalMs), stoppingToken);
				}
				catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
				{
					break;
				}
			}

			_logger.LogInformation("ArchiveHostedService: ExecuteAsync loop exited. Service stopped.");
		}

		/// <summary>
		/// Core work method executed on each timer tick when the reentrancy guard is available.
		///
		/// Faithfully reproduces <c>SchedulerUtils.OnArchiveTimerCore</c> (SchedulerUtils.cs lines 1184-1312)
		/// using DI-injected services instead of static <c>HttpContext</c> access.
		///
		/// Execution sequence per tick:
		/// <list type="number">
		///   <item><description>
		///     Check <c>CONFIG.Archive.SeparateTimer</c> — if not enabled, skip tick entirely.
		///     Matches Global.asax.cs line 204 conditional activation.
		///   </description></item>
		///   <item><description>
		///     Machine-name-based job server election using <c>SPLENDID_JOB_SERVER</c> env var.
		///     Result cached in <c>IMemoryCache["SplendidJobServer"]</c>.
		///     Matching SchedulerUtils.cs lines 900-924.
		///   </description></item>
		///   <item><description>
		///     Query <c>vwSCHEDULERS_Run</c> filtered to only
		///     <c>function::RunAllArchiveRules</c> and <c>function::RunExternalArchive</c>.
		///     Matching SchedulerUtils.cs lines 927-947.
		///   </description></item>
		///   <item><description>
		///     Execute each archive job via <see cref="SchedulerUtils.RunJob"/>.
		///     Matching SchedulerUtils.cs lines 953-991.
		///   </description></item>
		///   <item><description>
		///     Update <c>spSCHEDULERS_UpdateLastRun</c> in a <c>finally</c> block — MUST execute
		///     even if the job throws an exception. Matching SchedulerUtils.cs lines 974-991.
		///   </description></item>
		/// </list>
		/// </summary>
		/// <param name="stoppingToken">Cancellation token for graceful shutdown awareness.</param>
		private async Task DoWorkAsync(CancellationToken stoppingToken)
		{
			// -----------------------------------------------------------------------
			// Phase 6: Conditional activation — matching Global.asax.cs line 204
			// BEFORE: if (Sql.ToBoolean(Application["CONFIG.Archive.SeparateTimer"]))
			// AFTER:  if (!Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.Archive.SeparateTimer")))
			// -----------------------------------------------------------------------
			if (!Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.Archive.SeparateTimer")))
			{
				_logger.LogInformation("ArchiveHostedService: CONFIG.Archive.SeparateTimer is not enabled. Skipping archive jobs this tick.");
				return;
			}

			// Create a scoped DI container per timer tick.
			// BackgroundService is a singleton, but SchedulerUtils and DbProviderFactories
			// may be registered as scoped services — a new scope ensures correct service lifetime.
			using (IServiceScope scope = _scopeFactory.CreateScope())
			{
				IServiceProvider sp = scope.ServiceProvider;

				// Resolve scoped services for this tick
				SchedulerUtils      schedulerUtils      = sp.GetRequiredService<SchedulerUtils>     ();
				DbProviderFactories dbProviderFactories = sp.GetRequiredService<DbProviderFactories>();

				try
				{
					// -----------------------------------------------------------------------
					// Step 1: Job server election (matching SchedulerUtils.cs lines 900-924)
					// 01/27/2009 Paul.  If multiple apps connect to the same database, make sure
					//   that only one is the job server for load-balanced sites.
					//
					// Election result is cached in IMemoryCache["SplendidJobServer"]:
					//   0  = not yet determined (first tick)
					//   1  = this machine IS a job server → run archive jobs
					//  -1  = this machine is NOT a job server → skip archive jobs
					//
					// BEFORE: int nSplendidJobServer = Sql.ToInteger(Context.Application["SplendidJobServer"]);
					// AFTER:  int nSplendidJobServer = Sql.ToInteger(_memoryCache.Get<object>("SplendidJobServer"));
					// -----------------------------------------------------------------------
					int nSplendidJobServer = Sql.ToInteger(_memoryCache.Get<object>("SplendidJobServer"));
					if (nSplendidJobServer == 0)
					{
						// First tick — determine whether this machine is the designated job server.
						// BEFORE: string sSplendidJobServer = ConfigurationManager.AppSettings["SplendidJobServer"];
						// AFTER:  string sSplendidJobServer = _configuration["SPLENDID_JOB_SERVER"];
						string sSplendidJobServer = _configuration["SPLENDID_JOB_SERVER"];
						// Also check legacy key for backward compatibility (matching SchedulerUtils.cs line 1198)
						if (Sql.IsEmptyString(sSplendidJobServer))
							sSplendidJobServer = _configuration["SplendidJobServer"];

						// Get machine name — with try/catch for Azure/container compatibility
						// 09/17/2009 Paul.  Azure does not support MachineName.  Just ignore the error.
						// Matching SchedulerUtils.cs lines 904-912.
						string sMachineName = sSplendidJobServer;
						try
						{
							sMachineName = System.Environment.MachineName;
						}
						catch
						{
							// Azure / containerized environments may not expose MachineName — ignore and fall back
						}

						// Case-insensitive comparison (matching line 913)
						// If config is empty OR matches this machine → designate as job server
						if (Sql.IsEmptyString(sSplendidJobServer) || String.Compare(sMachineName, sSplendidJobServer, true) == 0)
						{
							// This machine IS the job server (matching lines 915-916)
							nSplendidJobServer = 1;
							// BEFORE: SplendidError.SystemMessage(Context, "Warning", ..., sMachineName + " is a Splendid Job Server.");
							// AFTER:  _logger.LogWarning(...)
							_logger.LogWarning("{MachineName} is a Splendid Job Server.", sMachineName);
						}
						else
						{
							// This machine is NOT the job server (matching lines 920-921)
							nSplendidJobServer = -1;
							// BEFORE: SplendidError.SystemMessage(Context, "Warning", ..., sMachineName + " is not a Splendid Job Server.");
							// AFTER:  _logger.LogWarning(...)
							_logger.LogWarning("{MachineName} is not a Splendid Job Server.", sMachineName);
						}

						// Cache the election result for all subsequent ticks (matching line 923)
						// BEFORE: Context.Application["SplendidJobServer"] = nSplendidJobServer;
						// AFTER:  _memoryCache.Set("SplendidJobServer", ...)
						_memoryCache.Set("SplendidJobServer", (object)nSplendidJobServer);
					}

					// -----------------------------------------------------------------------
					// Step 2 + 3: Query archive schedulers and execute jobs
					// Matching SchedulerUtils.cs lines 925-993
					// -----------------------------------------------------------------------
					if (nSplendidJobServer > 0)
					{
						// Get database factory using IMemoryCache for connection string caching.
						// BEFORE: DbProviderFactory dbf = DbProviderFactories.GetFactory(Context.Application);
						// AFTER:  DbProviderFactory dbf = dbProviderFactories.GetFactory(_memoryCache);
						DbProviderFactory dbf = dbProviderFactories.GetFactory(_memoryCache);

						using (DataTable dt = new DataTable())
						{
							// ----------------------------------------------------------------
							// Step 2: Query vwSCHEDULERS_Run filtered to archive jobs
							// Matching SchedulerUtils.cs lines 927-947
							// ----------------------------------------------------------------
							using (IDbConnection con = dbf.CreateConnection())
							{
								con.Open();
								// Exact SQL preserved from SchedulerUtils.cs lines 934-937
								string sSQL = "select *               " + ControlChars.CrLf
								            + "  from vwSCHEDULERS_Run" + ControlChars.CrLf
								            + " where JOB in ('function::RunAllArchiveRules', 'function::RunExternalArchive')" + ControlChars.CrLf
								            + " order by NEXT_RUN     " + ControlChars.CrLf;
								using (IDbCommand cmd = con.CreateCommand())
								{
									cmd.CommandText    = sSQL;
									// 01/01/2008 Paul.  The scheduler query should always be very fast.
									// In the off chance that there is a problem, abort after 15 seconds.
									cmd.CommandTimeout = 15;
									using (DbDataAdapter da = dbf.CreateDataAdapter())
									{
										// Provider-agnostic cast — matching SchedulerUtils.cs line 944:
										// ((IDbDataAdapter)da).SelectCommand = cmd;
										((IDbDataAdapter)da).SelectCommand = cmd;
										da.Fill(dt);
									}
								}
							}

							// 05/14/2009 Paul.  Provide a way to track scheduler events.
							// Matching SchedulerUtils.cs line 949-951
							// BEFORE: SplendidError.SystemMessage(Context, "Warning", ..., "Archive Jobs to run: " + dt.Rows.Count.ToString());
							// AFTER:  _logger.LogWarning(...)
							if (!Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.suppress_scheduler_warning")))
								_logger.LogWarning("Archive Jobs to run: {JobCount}", dt.Rows.Count);

							// ----------------------------------------------------------------
							// Step 3: Execute archive jobs (matching SchedulerUtils.cs lines 953-991)
							// ----------------------------------------------------------------
							foreach (DataRow row in dt.Rows)
							{
								Guid     gID        = Sql.ToGuid    (row["ID"      ]);
								string   sJOB       = Sql.ToString  (row["JOB"     ]);
								// 01/31/2008 Paul.  Next run becomes last run.
								DateTime dtLAST_RUN = Sql.ToDateTime(row["NEXT_RUN"]);

								// 11/02/2022 Paul.  Keep track of last job for verbose logging (matching line 959)
								// Used by the "Archive Jobs Busy" warning in the next timer tick if overlapping occurs.
								_lastJob = sJOB;

								try
								{
									// Log job start (matching lines 962-964)
									// BEFORE: SplendidError.SystemMessage(Context, "Warning", ..., "Archive Job Start: " + sJOB + " at " + dtLAST_RUN.ToString());
									// AFTER:  _logger.LogWarning(...)
									if (!Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.suppress_scheduler_warning")))
										_logger.LogWarning("Archive Job Start: {Job} at {Time}", sJOB, dtLAST_RUN);

									// Execute the archive job (matching line 966)
									// BEFORE: RunJob(Context, sJOB);
									// AFTER:  schedulerUtils.RunJob(null, sJOB);
									// SchedulerUtils.RunJob uses DI-injected services internally;
									// the Context parameter is preserved for schema compliance but not required.
									schedulerUtils.RunJob(null, sJOB);

									// Log job end (matching lines 967-969)
									// BEFORE: SplendidError.SystemMessage(Context, "Warning", ..., "Archive Job End: " + sJOB + " at " + DateTime.Now.ToString());
									// AFTER:  _logger.LogWarning(...)
									if (!Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.suppress_scheduler_warning")))
										_logger.LogWarning("Archive Job End: {Job} at {Time}", sJOB, DateTime.Now);
								}
								finally
								{
									// CRITICAL: Always update LAST_RUN — even if the job threw an exception.
									// This prevents re-running failed jobs on every subsequent archive timer tick.
									// Matching SchedulerUtils.cs lines 972-991: finally block around RunJob.
									//
									// 10/07/2009 Paul.  We need to create our own global transaction ID to support
									//   auditing and workflow on SQL Azure, PostgreSQL, Oracle, DB2 and MySQL.
									using (IDbConnection conUpdate = dbf.CreateConnection())
									{
										conUpdate.Open();
										// BEFORE: using ( IDbTransaction trn = Sql.BeginTransaction(con) )
										// AFTER:  using (IDbTransaction trn = Sql.BeginTransaction(conUpdate))
										using (IDbTransaction trn = Sql.BeginTransaction(conUpdate))
										{
											try
											{
												// 01/12/2008 Paul.  Make sure the Last Run value is updated after the operation.
												// BEFORE: SqlProcs.spSCHEDULERS_UpdateLastRun(gID, dtLAST_RUN, trn);
												// AFTER:  SqlProcs.spSCHEDULERS_UpdateLastRun(gID, dtLAST_RUN, trn); (static call preserved)
												SqlProcs.spSCHEDULERS_UpdateLastRun(gID, dtLAST_RUN, trn);
												trn.Commit();
											}
											catch (Exception ex)
											{
												trn.Rollback();
												// BEFORE: SplendidError.SystemMessage(Context, "Error", ..., Utils.ExpandException(ex));
												// AFTER:  _logger.LogError(...)
												_logger.LogError(ex,
													"ArchiveHostedService.DoWorkAsync: Failed to execute spSCHEDULERS_UpdateLastRun for job {Job} (ID={JobId}). Transaction rolled back.",
													sJOB, gID);
											}
										}
									}
								}
							} // foreach DataRow row in dt.Rows
						} // using DataTable dt
					} // if nSplendidJobServer > 0
				}
				catch (Exception ex)
				{
					// Outer error handler (matching SchedulerUtils.cs lines 996-999)
					// BEFORE: SplendidError.SystemMessage(Context, "Error", ..., Utils.ExpandException(ex));
					// AFTER:  _logger.LogError(...)
					// Do NOT rethrow — keep the service loop alive across transient errors.
					_logger.LogError(ex, "ArchiveHostedService.DoWorkAsync: Error in archive execution cycle");
				}
			} // using IServiceScope scope

			// Yield control back to the async scheduler after completing work.
			// Ensures cooperative multitasking and allows cancellation tokens to be
			// checked between the work completion and the next Task.Delay call.
			await Task.Yield();
		}

		/// <summary>
		/// Disposes the <see cref="SemaphoreSlim"/> reentrancy guard and calls
		/// <see cref="BackgroundService.Dispose()"/> for base BackgroundService cleanup.
		///
		/// Replaces: Disposal of <c>tArchiveManager</c> timer in Application_End
		/// (Global.asax.cs line 380: <c>if (tArchiveManager != null) tArchiveManager.Dispose();</c>).
		/// </summary>
		public override void Dispose()
		{
			_semaphore?.Dispose();
			base.Dispose();
		}
	}
}
