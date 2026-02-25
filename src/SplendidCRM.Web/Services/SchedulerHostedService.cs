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
// .NET 10 Migration: SplendidCRM/Global.asax.cs (lines 50-58) + SplendidCRM/_code/SchedulerUtils.cs (lines 653-889)
//   → src/SplendidCRM.Web/Services/SchedulerHostedService.cs
//
// Changes applied:
//   - REPLACED: System.Threading.Timer → BackgroundService (IHostedService) + Task.Delay timer loop
//   - REPLACED: static bool bInsideTimer → SemaphoreSlim(1, 1) with non-blocking Wait(0)
//   - REPLACED: static string sLastJob → private string _lastJob instance field
//   - REPLACED: Context.Application["SplendidJobServer"] → IMemoryCache.Get<object>("SplendidJobServer")
//   - REPLACED: Context.Application["CONFIG.*"] → IMemoryCache.Get<object>("CONFIG.*")
//   - REPLACED: ConfigurationManager.AppSettings["SplendidJobServer"] → IConfiguration["SPLENDID_JOB_SERVER"]
//   - REPLACED: DbProviderFactories.GetFactory(Context.Application) → dbProviderFactories.GetFactory(IMemoryCache)
//   - REPLACED: SplendidError.SystemMessage(Context, "Warning", ...) → ILogger.LogWarning(...)
//   - REPLACED: SplendidError.SystemMessage(Context, "Error", ...) → ILogger.LogError(...)
//   - REPLACED: WorkflowUtils.Process(Context) → workflowUtils.Process(null) (no HttpContext in background service)
//   - REPLACED: RunJob(Context, sJOB) → schedulerUtils.RunJob(null, sJOB) (DI-injected context)
//   - REMOVED: vwSYSTEM_EVENTS cache invalidation section (lines 662-753) — moved to CacheInvalidationService.cs
//   - PRESERVED: Machine-name-based job server election logic (lines 764-789)
//   - PRESERVED: vwSCHEDULERS_Run query with CommandTimeout = 15 (lines 792-814)
//   - PRESERVED: Per-job execution loop with sJOB, gID, dtLAST_RUN tracking (lines 821-871)
//   - PRESERVED: Archive job skipping when CONFIG.Archive.SeparateTimer is true (lines 828-834)
//   - PRESERVED: spSCHEDULERS_UpdateLastRun in finally block — MUST execute even on job failure (lines 851-869)
//   - PRESERVED: suppress_scheduler_warning config flag for job count and start/end logging (lines 816, 839, 844)
//   - PRESERVED: Scheduler.Verbose config flag for "Scheduler Busy" warning (line 885)
//   - PRESERVED: All 12 scheduler job names (Jobs array in SchedulerUtils): CleanSystemLog,
//     CleanSystemSyncLog, pruneDatabase, BackupDatabase, BackupTransactionLog, CheckVersion,
//     RunAllArchiveRules, RunExternalArchive, pollMonitoredInboxes, runMassEmailCampaign,
//     pollMonitoredInboxesForBouncedCampaignEmails, pollOutboundEmails
//   - ADDED: IServiceScopeFactory for creating scoped DI containers per timer tick
//   - ADDED: Initial 60-second delay (matching Global.asax.cs line 56: new TimeSpan(0, 1, 0))
//   - ADDED: Configurable interval via SCHEDULER_INTERVAL_MS environment variable (default 60000ms)
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
	/// IHostedService that replaces the timer-based main scheduler in Global.asax.cs.
	///
	/// Original source:
	///   Global.asax.cs lines 50-58: <c>tSchedulerManager = new Timer(SchedulerUtils.OnTimer, this.Context, new TimeSpan(0, 1, 0), new TimeSpan(0, 5, 0));</c>
	///   SchedulerUtils.cs lines 653-889: <c>OnTimer(Object sender)</c> callback implementation.
	///
	/// Key design decisions:
	/// <list type="bullet">
	///   <item><description>
	///     <c>SemaphoreSlim(1,1)</c> with non-blocking <c>Wait(0)</c> replaces <c>static bool bInsideTimer</c>
	///     — overlapping ticks are SKIPPED (not queued), matching the original behavior exactly.
	///   </description></item>
	///   <item><description>
	///     <c>IServiceScopeFactory</c> creates a new DI scope per timer tick so that scoped services
	///     (SchedulerUtils, WorkflowUtils, DbProviderFactories) are properly lifetime-managed.
	///     BackgroundService is registered as a singleton, but the business logic services may be scoped.
	///   </description></item>
	///   <item><description>
	///     The vwSYSTEM_EVENTS cache invalidation section (SchedulerUtils.cs lines 662-753) is intentionally
	///     excluded from this service — it lives in <see cref="SplendidCRM.Web.Services.CacheInvalidationService"/>.
	///   </description></item>
	///   <item><description>
	///     Machine-name-based job election (<c>SPLENDID_JOB_SERVER</c> env var vs <c>Environment.MachineName</c>)
	///     is fully preserved to support load-balanced deployments where only one server runs scheduled jobs.
	///   </description></item>
	/// </list>
	/// </summary>
	public class SchedulerHostedService : BackgroundService
	{
		// ====================================================================================
		// DI-injected services — replacing Global.asax.cs timer and HttpContext/Application access
		// ====================================================================================

		/// <summary>Factory for creating scoped DI containers per timer tick.</summary>
		private readonly IServiceScopeFactory              _scopeFactory ;
		/// <summary>Reads SCHEDULER_INTERVAL_MS and SPLENDID_JOB_SERVER from configuration hierarchy.</summary>
		private readonly IConfiguration                    _configuration;
		/// <summary>Replaces Application[] state for CONFIG values and job server election caching.</summary>
		private readonly IMemoryCache                      _memoryCache  ;
		/// <summary>Replaces SplendidError.SystemMessage for all diagnostic logging.</summary>
		private readonly ILogger<SchedulerHostedService>   _logger       ;

		// ====================================================================================
		// Reentrancy guard — replaces static bool bInsideTimer (SchedulerUtils.cs line 34)
		//
		// BEFORE (.NET Framework 4.8):
		//   private static bool bInsideTimer = false;
		//   if ( !bInsideTimer ) { bInsideTimer = true; try { ... } finally { bInsideTimer = false; } }
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
		/// Last job name for "Scheduler Busy" logging.
		/// Replaces static string sLastJob (SchedulerUtils.cs line 38).
		/// </summary>
		private string _lastJob = String.Empty;

		/// <summary>
		/// Initializes a new instance of <see cref="SchedulerHostedService"/> with DI-injected services.
		/// </summary>
		/// <param name="scopeFactory">
		/// Factory for creating scoped DI containers per timer tick.
		/// Replaces the <c>HttpContext Context</c> passed as timer state in <c>Global.asax.cs</c> line 56.
		/// </param>
		/// <param name="configuration">
		/// Configuration abstraction for reading <c>SCHEDULER_INTERVAL_MS</c> (timer interval, default 60000ms)
		/// and <c>SPLENDID_JOB_SERVER</c> (machine-name job election).
		/// Replaces <c>System.Configuration.ConfigurationManager.AppSettings</c> from SchedulerUtils.cs line 767.
		/// </param>
		/// <param name="memoryCache">
		/// Memory cache replacing <c>Application[]</c> state for CONFIG values and job server election result.
		/// Replaces <c>Context.Application["SplendidJobServer"]</c>, <c>Context.Application["CONFIG.*"]</c>.
		/// </param>
		/// <param name="logger">
		/// Structured logger replacing <c>SplendidError.SystemMessage</c> for all diagnostic output.
		/// </param>
		public SchedulerHostedService(
			IServiceScopeFactory            scopeFactory ,
			IConfiguration                  configuration,
			IMemoryCache                    memoryCache  ,
			ILogger<SchedulerHostedService> logger       )
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
			_logger.LogInformation("SchedulerHostedService: StartAsync called. Service is starting.");
			await base.StartAsync(cancellationToken);
		}

		/// <summary>
		/// Stops the background service.
		/// Overridden to log graceful shutdown before delegating to the base BackgroundService lifecycle.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token for forced stop.</param>
		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("SchedulerHostedService: StopAsync called. Service is stopping.");
			await base.StopAsync(cancellationToken);
		}

		/// <summary>
		/// Main background service execution loop.
		///
		/// Provides an initial 1-minute delay (matching <c>Global.asax.cs</c> line 56:
		/// <c>new TimeSpan(0, 1, 0)</c>) to allow application startup to complete before the
		/// first scheduler tick fires.
		///
		/// Then fires <see cref="DoWorkAsync"/> on the configured <c>SCHEDULER_INTERVAL_MS</c>
		/// interval (default: 60000ms per AAP), using a <see cref="SemaphoreSlim"/> non-blocking
		/// reentrancy guard to skip overlapping ticks.
		/// </summary>
		/// <param name="stoppingToken">Cancellation token injected by BackgroundService for graceful shutdown.</param>
		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			// Activation log (matching Global.asax.cs line 57 warning message)
			// BEFORE: SplendidError.SystemWarning(new StackTrace(true).GetFrame(0), "The Scheduler Manager timer has been activated.");
			// AFTER:  _logger.LogInformation(...)
			_logger.LogInformation("The Scheduler Manager hosted service has been activated.");

			// Initial delay of 1 minute (matching Global.asax.cs line 56: new TimeSpan(0, 1, 0))
			// Allows the application to fully initialize before the first scheduler tick.
			try
			{
				await Task.Delay(TimeSpan.FromMilliseconds(60000), stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				_logger.LogInformation("SchedulerHostedService: Cancelled during initial delay.");
				return;
			}

			// Read configurable interval from SCHEDULER_INTERVAL_MS (default: 60000ms per AAP)
			// NOTE: The original Global.asax.cs line 56 used new TimeSpan(0, 5, 0) = 5 minutes for the
			// repeating interval. The AAP explicitly specifies 60000ms as the default for this hosted service.
			// BEFORE: new Timer(SchedulerUtils.OnTimer, this.Context, new TimeSpan(0, 1, 0), new TimeSpan(0, 5, 0))
			// AFTER:  Task.Delay(TimeSpan.FromMilliseconds(nIntervalMs), stoppingToken)
			int nIntervalMs = 60000;
			string sIntervalMs = _configuration["SCHEDULER_INTERVAL_MS"];
			if (!String.IsNullOrEmpty(sIntervalMs) && Int32.TryParse(sIntervalMs, out int nParsed) && nParsed >= 1000)
				nIntervalMs = nParsed;

			_logger.LogInformation("SchedulerHostedService: Using interval of {IntervalMs}ms.", nIntervalMs);

			// Timer loop — fires until the application stops
			while (!stoppingToken.IsCancellationRequested)
			{
				// Non-blocking reentrancy guard — replacing: if (!bInsideTimer) { bInsideTimer = true; ... }
				// (SchedulerUtils.cs line 657-659)
				//
				// _semaphore.Wait(0) returns immediately:
				//   true  = lock acquired → this tick runs DoWorkAsync
				//   false = already locked → previous tick still running → skip this tick
				//
				// This preserves SKIP semantics (not queue semantics): an overlapping tick is
				// silently discarded, matching the original bInsideTimer pattern exactly.
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
						// Catch and log — do NOT rethrow; keep the loop alive
						// Matches SchedulerUtils.cs lines 875-878: outer catch around the timer body
						_logger.LogError(ex, "SchedulerHostedService.ExecuteAsync: Unhandled error from DoWorkAsync");
					}
					finally
					{
						// Always release the semaphore — replacing: bInsideTimer = false (SchedulerUtils.cs line 881)
						_semaphore.Release();
					}
				}
				else
				{
					// Timer is still busy from a previous tick — matching SchedulerUtils.cs lines 884-888
					// 11/02/2022 Paul.  Keep track of last job for verbose logging.
					// Original: else if ( !Sql.ToBoolean(Context.Application["CONFIG.Scheduler.Verbose"]) )
					// Preserved: only log "Scheduler Busy" when NOT in verbose mode (matches original behavior)
					if (!Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.Scheduler.Verbose")))
						_logger.LogWarning("Scheduler Busy: {LastJob}", _lastJob);
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

			_logger.LogInformation("SchedulerHostedService: ExecuteAsync loop exited. Service stopped.");
		}

		/// <summary>
		/// Core work method executed on each timer tick when the reentrancy guard is available.
		///
		/// Faithfully reproduces <c>SchedulerUtils.OnTimer</c> (lines 756-873) WITHOUT the
		/// vwSYSTEM_EVENTS cache invalidation section (lines 662-753), which is now handled
		/// by <c>CacheInvalidationService</c>.
		///
		/// Execution sequence per tick:
		/// <list type="number">
		///   <item><description>Workflow processing (if <c>CONFIG.enable_workflow</c> is true).</description></item>
		///   <item><description>Machine-name-based job server election using <c>SPLENDID_JOB_SERVER</c> env var.</description></item>
		///   <item><description>Query <c>vwSCHEDULERS_Run</c> (if this machine is the job server).</description></item>
		///   <item><description>Execute each scheduled job via <see cref="SchedulerUtils.RunJob"/>.</description></item>
		///   <item><description>Update <c>spSCHEDULERS_UpdateLastRun</c> in a <c>finally</c> block — even on job failure.</description></item>
		/// </list>
		/// </summary>
		/// <param name="stoppingToken">Cancellation token for graceful shutdown awareness.</param>
		private async Task DoWorkAsync(CancellationToken stoppingToken)
		{
			// Create a scoped DI container per timer tick.
			// BackgroundService is a singleton, but SchedulerUtils, WorkflowUtils, and DbProviderFactories
			// may be registered as scoped services — a new scope ensures correct service lifetime.
			using (IServiceScope scope = _scopeFactory.CreateScope())
			{
				IServiceProvider sp = scope.ServiceProvider;

				// Resolve scoped services for this tick
				SchedulerUtils      schedulerUtils      = sp.GetRequiredService<SchedulerUtils>     ();
				WorkflowUtils       workflowUtils       = sp.GetRequiredService<WorkflowUtils>      ();
				DbProviderFactories dbProviderFactories = sp.GetRequiredService<DbProviderFactories>();

				try
				{
					// -----------------------------------------------------------------------
					// Step 1: Workflow processing (matching SchedulerUtils.cs lines 756-760)
					// 12/30/2007 Paul.  Workflow events always get processed.
					// 07/26/2008 Paul.  Provide a way to disable workflow.
					//
					// BEFORE: bool bEnableWorkflow = Sql.ToBoolean(Context.Application["CONFIG.enable_workflow"]);
					//         if ( bEnableWorkflow ) WorkflowUtils.Process(Context);
					// AFTER:  bool bEnableWorkflow = Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.enable_workflow"));
					//         if ( bEnableWorkflow ) workflowUtils.Process(null);
					// -----------------------------------------------------------------------
					bool bEnableWorkflow = Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.enable_workflow"));
					if (bEnableWorkflow)
					{
						// .NET 10 Migration: WorkflowUtils.Process(Context) → workflowUtils.Process(null)
						// Background services have no real HttpContext; WorkflowUtils.Process() accepts null
						// in the Community Edition stub (the method body is intentionally empty in CE).
						workflowUtils.Process(null);
					}

					// -----------------------------------------------------------------------
					// Step 2: Job server election (matching SchedulerUtils.cs lines 764-789)
					// 01/27/2009 Paul.  If multiple apps connect to the same database, make sure that
					//   only one is the job server. This is primarily for load-balanced sites.
					//
					// Election result is cached in IMemoryCache["SplendidJobServer"]:
					//   0  = not yet determined (first tick)
					//   1  = this machine IS a job server → run jobs
					//  -1  = this machine is NOT a job server → skip jobs
					//
					// BEFORE: int nSplendidJobServer = Sql.ToInteger(Context.Application["SplendidJobServer"]);
					// AFTER:  int nSplendidJobServer = Sql.ToInteger(_memoryCache.Get<object>("SplendidJobServer"));
					// -----------------------------------------------------------------------
					int nSplendidJobServer = Sql.ToInteger(_memoryCache.Get<object>("SplendidJobServer"));
					if (nSplendidJobServer == 0)
					{
						// First tick — determine whether this machine is the designated job server.
						//
						// BEFORE: string sSplendidJobServer = System.Configuration.ConfigurationManager.AppSettings["SplendidJobServer"];
						// AFTER:  string sSplendidJobServer = _configuration["SPLENDID_JOB_SERVER"];
						// (Also try legacy key for backward compatibility)
						string sSplendidJobServer = _configuration["SPLENDID_JOB_SERVER"];
						if (Sql.IsEmptyString(sSplendidJobServer))
							sSplendidJobServer = _configuration["SplendidJobServer"];

						// Get machine name — with try/catch for Azure/container compatibility (lines 770-777)
						// 09/17/2009 Paul.  If we are running in Azure, then assume that this is the only instance.
						string sMachineName = sSplendidJobServer;
						try
						{
							// 09/17/2009 Paul.  Azure does not support MachineName.  Just ignore the error.
							sMachineName = Environment.MachineName;
						}
						catch
						{
							// Azure / containerized environments may not expose MachineName — ignore and fall back
						}

						// Case-insensitive comparison (matching line 778)
						// If config is empty OR matches this machine → designate as job server
						if (Sql.IsEmptyString(sSplendidJobServer) || String.Compare(sMachineName, sSplendidJobServer, true) == 0)
						{
							// This machine IS the job server (matching lines 780-781)
							nSplendidJobServer = 1;
							// BEFORE: SplendidError.SystemMessage(Context, "Warning", ..., sMachineName + " is a Splendid Job Server.");
							// AFTER:  _logger.LogWarning(...)
							_logger.LogWarning("{MachineName} is a Splendid Job Server.", sMachineName);
						}
						else
						{
							// This machine is NOT the job server (matching lines 785-786)
							nSplendidJobServer = -1;
							// BEFORE: SplendidError.SystemMessage(Context, "Warning", ..., sMachineName + " is not a Splendid Job Server.");
							// AFTER:  _logger.LogWarning(...)
							_logger.LogWarning("{MachineName} is not a Splendid Job Server.", sMachineName);
						}
						// Cache the election result for all subsequent ticks (matching line 788)
						// BEFORE: Context.Application["SplendidJobServer"] = nSplendidJobServer;
						// AFTER:  _memoryCache.Set("SplendidJobServer", nSplendidJobServer)
						_memoryCache.Set("SplendidJobServer", (object)nSplendidJobServer);
					}

					// -----------------------------------------------------------------------
					// Step 3 + 4: Query schedulers and execute jobs (matching lines 792-873)
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
							// Step 3: Query vwSCHEDULERS_Run (matching lines 794-813)
							// ----------------------------------------------------------------
							using (IDbConnection con = dbf.CreateConnection())
							{
								con.Open();
								// Exact SQL preserved from SchedulerUtils.cs lines 798-800
								string sSQL = "select *               " + ControlChars.CrLf
								            + "  from vwSCHEDULERS_Run" + ControlChars.CrLf
								            + " order by NEXT_RUN     " + ControlChars.CrLf;
								using (IDbCommand cmd = con.CreateCommand())
								{
									cmd.CommandText = sSQL;
									// 01/01/2008 Paul.  The scheduler query should always be very fast.
									// In the off chance that there is a problem, abort after 15 seconds.
									cmd.CommandTimeout = 15;

									using (DbDataAdapter da = dbf.CreateDataAdapter())
									{
										// Provider-agnostic cast — matching line 810:
										// ((IDbDataAdapter)da).SelectCommand = cmd;
										((IDbDataAdapter)da).SelectCommand = cmd;
										da.Fill(dt);
									}
								}
							}

							// ----------------------------------------------------------------
							// Step 4: Execute scheduled jobs (matching lines 816-871)
							// ----------------------------------------------------------------

							// 05/14/2009 Paul.  Provide a way to track scheduler events.
							// BEFORE: SplendidError.SystemMessage(Context, "Warning", ..., "Scheduler Jobs to run: " + dt.Rows.Count.ToString());
							// AFTER:  _logger.LogWarning(...)
							if (!Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.suppress_scheduler_warning")))
								_logger.LogWarning("Scheduler Jobs to run: {JobCount}", dt.Rows.Count);

							// 01/13/2008 Paul.  Loop outside the connection so that only one connection will be used.
							foreach (DataRow row in dt.Rows)
							{
								Guid     gID        = Sql.ToGuid    (row["ID"      ]);
								string   sJOB       = Sql.ToString  (row["JOB"     ]);
								// 01/31/2008 Paul.  Next run becomes last run.
								DateTime dtLAST_RUN = Sql.ToDateTime(row["NEXT_RUN"]);

								// 11/08/2022 Paul.  Separate Archive timer.
								// Skip archive jobs when the dedicated archive timer (ArchiveHostedService) is enabled.
								// Matching SchedulerUtils.cs lines 828-834.
								// NOTE: Uses break (not continue) — matching the original source exactly.
								//   This means ALL remaining rows are skipped once an archive job is encountered
								//   when SeparateTimer is enabled. Archive jobs appear last due to ORDER BY NEXT_RUN,
								//   but in practice NEXT_RUN ordering may place them anywhere.
								if (Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.Archive.SeparateTimer")))
								{
									if (sJOB == "function::RunAllArchiveRules" || sJOB == "function::RunExternalArchive")
									{
										// Break out of the foreach — matches original source (line 832: break;)
										break;
									}
								}

								// 11/02/2022 Paul.  Keep track of last job for verbose logging (matching line 836)
								// Used by the "Scheduler Busy" warning in the next timer tick if overlapping occurs.
								_lastJob = sJOB;

								try
								{
									// Log job start (matching lines 839-841)
									// BEFORE: SplendidError.SystemMessage(Context, "Warning", ..., "Scheduler Job Start: " + sJOB + " at " + dtLAST_RUN.ToString());
									// AFTER:  _logger.LogWarning(...)
									if (!Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.suppress_scheduler_warning")))
										_logger.LogWarning("Scheduler Job Start: {Job} at {Time}", sJOB, dtLAST_RUN);

									// Execute the scheduler job (matching line 843)
									// BEFORE: RunJob(Context, sJOB);
									// AFTER:  schedulerUtils.RunJob(null, sJOB);
									// SchedulerUtils.RunJob uses DI-injected services internally;
									// the Context parameter is preserved for schema compliance but not required.
									schedulerUtils.RunJob(null, sJOB);

									// Log job end (matching lines 844-846)
									// BEFORE: SplendidError.SystemMessage(Context, "Warning", ..., "Scheduler Job End: " + sJOB + " at " + DateTime.Now.ToString());
									// AFTER:  _logger.LogWarning(...)
									if (!Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.suppress_scheduler_warning")))
										_logger.LogWarning("Scheduler Job End: {Job} at {Time}", sJOB, DateTime.Now);
								}
								finally
								{
									// CRITICAL: Always update LAST_RUN — even if the job threw an exception.
									// This prevents re-running failed jobs on every subsequent timer tick.
									// Matching SchedulerUtils.cs lines 849-870: finally block around RunJob.
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
													"SchedulerHostedService.DoWorkAsync: Failed to execute spSCHEDULERS_UpdateLastRun for job {Job} (ID={JobId}). Transaction rolled back.",
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
					// Step 4f: Outer error handler (matching SchedulerUtils.cs lines 875-878)
					// BEFORE: SplendidError.SystemMessage(Context, "Error", ..., Utils.ExpandException(ex));
					// AFTER:  _logger.LogError(...)
					// Do NOT rethrow — keep the service loop alive across transient errors.
					_logger.LogError(ex, "SchedulerHostedService.DoWorkAsync: Error in scheduler execution cycle");
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
		/// Replaces: No equivalent disposal in the original timer-based approach
		/// (System.Threading.Timer was disposed in Application_End via tSchedulerManager?.Dispose()).
		/// </summary>
		public override void Dispose()
		{
			_semaphore?.Dispose();
			base.Dispose();
		}
	}
}
