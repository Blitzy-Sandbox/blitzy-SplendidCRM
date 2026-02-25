/**********************************************************************************************************************
 * SplendidCRM is a Customer Relationship Management program created by SplendidCRM Software, Inc.
 * Copyright (C) 2005-2025 SplendidCRM Software, Inc. All rights reserved.
 *
 * This program is free software: you can redistribute it and/or modify it under the terms of the
 * GNU Affero General Public License as published by the Free Software Foundation, either version 3
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
 * without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
 * See the GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License along with this program.
 * If not, see <http://www.gnu.org/licenses/>.
 *
 * You can contact SplendidCRM Software, Inc. at email address support@splendidcrm.com.
 *
 * In accordance with Section 7(b) of the GNU Affero General Public License version 3,
 * the Appropriate Legal Notices must display the following words on all interactive user interfaces:
 * "Copyright (C) 2005-2025 SplendidCRM Software, Inc. All rights reserved."
 *********************************************************************************************************************/

// .NET 10 Migration Notes:
// - Extracted from SchedulerUtils.cs OnTimer() lines 662-728, 735-753 (SplendidCRM/_code/SchedulerUtils.cs)
// - Runs as dedicated IHostedService for more frequent, targeted cache invalidation
// - Context.Application["SYSTEM_EVENTS.MaxDate"] → IMemoryCache.TryGetValue/Set
// - DbProviderFactories.GetFactory(Context.Application) → DbProviderFactories.GetFactory(IMemoryCache)
// - SplendidError.SystemMessage(Context, "Warning", ...) → ILogger<T>.LogWarning(...)
// - SplendidError.SystemMessage(Context, "Error", ...)   → ILogger<T>.LogError(...)
// - SplendidCache.ClearTable(sTABLE_NAME) preserved identically (instance method call via DI scope)
// - SplendidInit.InitTerminology/InitModuleACL/InitConfig/InitTimeZones/InitCurrencies preserved
//   identically as instance method calls (HttpContext parameter removed in .NET 10 migration)
// - SemaphoreSlim not required here (single async loop, no separate thread contention)
// - Poll interval configurable via CACHE_INVALIDATION_INTERVAL_MS (default: 30000ms = 30 seconds)
//   Previously embedded in the 5-minute scheduler; more frequent here for distributed cache coherency.

#nullable disable
using System;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Internal imports from SplendidCRM.Core (namespace SplendidCRM)
using SplendidCRM;

namespace SplendidCRM.Web.Services
{
	/// <summary>
	/// Background service that monitors the <c>vwSYSTEM_EVENTS</c> SQL view for changed tables
	/// and invalidates corresponding <see cref="SplendidCache"/> entries.
	///
	/// This logic was originally embedded inside <c>SchedulerUtils.OnTimer()</c>
	/// (SplendidCRM/_code/SchedulerUtils.cs, lines 662–728 and 735–753) and is extracted here
	/// to run as an independent, more frequently polling service for improved cache coherency
	/// in distributed, load-balanced deployments.
	///
	/// <para><b>Key behaviors (faithfully replicated from SchedulerUtils.cs):</b></para>
	/// <list type="bullet">
	///   <item>Queries <c>vwSYSTEM_EVENTS</c> for rows newer than last processed timestamp.</item>
	///   <item>Calls <see cref="SplendidCache.ClearTable"/> for each changed table name.</item>
	///   <item>
	///     Invokes specialized re-initialization for TERMINOLOGY, MODULES/ACL_, CONFIG,
	///     TIMEZONES, CURRENCIES table changes via <see cref="SplendidInit"/>.
	///   </item>
	///   <item>
	///     Executes stored procedure <c>spSYSTEM_EVENTS_ProcessAll</c> inside a transaction
	///     to purge processed events, keeping future queries fast.
	///   </item>
	///   <item>
	///     Tracks last processed timestamp in <see cref="IMemoryCache"/> using key
	///     <c>"SYSTEM_EVENTS.MaxDate"</c> (replaces <c>Context.Application["SYSTEM_EVENTS.MaxDate"]</c>).
	///   </item>
	/// </list>
	/// </summary>
	public class CacheInvalidationService : BackgroundService
	{
		// -----------------------------------------------------------------------
		// Constants
		// -----------------------------------------------------------------------

		/// <summary>Default polling interval when CACHE_INVALIDATION_INTERVAL_MS is not configured.</summary>
		private const int DefaultIntervalMs = 30000;

		/// <summary>Minimum allowable polling interval to prevent runaway loops.</summary>
		private const int MinIntervalMs = 1000;

		// -----------------------------------------------------------------------
		// DI-injected fields
		// -----------------------------------------------------------------------

		/// <summary>
		/// Scope factory used to create a scoped DI container for each poll tick.
		/// Required because BackgroundService is a singleton but SplendidCache, SplendidInit,
		/// and DbProviderFactories may be registered as scoped services.
		/// </summary>
		private readonly IServiceScopeFactory _scopeFactory;

		/// <summary>
		/// Configuration for reading CACHE_INVALIDATION_INTERVAL_MS.
		/// Sourced from five-tier provider hierarchy:
		/// AWS Secrets Manager → Env vars → Parameter Store → appsettings.{Env}.json → appsettings.json.
		/// </summary>
		private readonly IConfiguration _configuration;

		/// <summary>
		/// Singleton memory cache that replaces HttpApplicationState (Application[]).
		/// Used to read/write "SYSTEM_EVENTS.MaxDate" — the timestamp of the last processed system event.
		/// Also passed to DbProviderFactories.GetFactory(IMemoryCache) for database connection resolution.
		/// Matches: Context.Application["SYSTEM_EVENTS.MaxDate"] (SchedulerUtils.cs line 667).
		/// </summary>
		private readonly IMemoryCache _memoryCache;

		/// <summary>
		/// Logger replacing SplendidError.SystemMessage(Context, "Warning"/Error", ...) calls.
		/// Matches: SplendidError.SystemMessage(Context, "Warning", ...) at lines 710–711.
		/// Matches: SplendidError.SystemMessage(Context, "Error", ...) at line 751.
		/// </summary>
		private readonly ILogger<CacheInvalidationService> _logger;

		// -----------------------------------------------------------------------
		// Constructor
		// -----------------------------------------------------------------------

		/// <summary>
		/// Initializes a new instance of <see cref="CacheInvalidationService"/>.
		/// All dependencies are injected via the ASP.NET Core DI container at startup.
		/// </summary>
		/// <param name="scopeFactory">
		/// Factory for creating per-poll-tick DI scopes to resolve scoped services.
		/// </param>
		/// <param name="configuration">
		/// Configuration for reading the CACHE_INVALIDATION_INTERVAL_MS polling interval.
		/// </param>
		/// <param name="memoryCache">
		/// Singleton memory cache for SYSTEM_EVENTS.MaxDate timestamp and DB factory caching.
		/// </param>
		/// <param name="logger">
		/// Logger for warning/error events from cache invalidation polling cycles.
		/// </param>
		public CacheInvalidationService(
			IServiceScopeFactory scopeFactory,
			IConfiguration configuration,
			IMemoryCache memoryCache,
			ILogger<CacheInvalidationService> logger)
		{
			_scopeFactory  = scopeFactory  ?? throw new ArgumentNullException(nameof(scopeFactory));
			_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
			_memoryCache   = memoryCache   ?? throw new ArgumentNullException(nameof(memoryCache));
			_logger        = logger        ?? throw new ArgumentNullException(nameof(logger));
		}

		// -----------------------------------------------------------------------
		// BackgroundService overrides
		// -----------------------------------------------------------------------

		/// <summary>
		/// Starts the polling loop that monitors <c>vwSYSTEM_EVENTS</c> for changed tables.
		/// Runs until <paramref name="stoppingToken"/> is cancelled (graceful shutdown).
		/// No initial delay — polling starts immediately for fastest cache coherency.
		/// </summary>
		/// <param name="stoppingToken">Token signalled when the host is stopping.</param>
		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			// Read configurable polling interval — CACHE_INVALIDATION_INTERVAL_MS
			// Defaults to 30000ms (30 seconds) for responsive distributed cache invalidation.
			// Previously embedded in the 5-minute (300000ms) scheduler timer in Global.asax.cs.
			int nIntervalMs = DefaultIntervalMs;
			string sConfiguredInterval = _configuration["CACHE_INVALIDATION_INTERVAL_MS"];
			if (!string.IsNullOrEmpty(sConfiguredInterval)
				&& int.TryParse(sConfiguredInterval, out int nParsed)
				&& nParsed >= MinIntervalMs)
			{
				nIntervalMs = nParsed;
			}

			_logger.LogInformation(
				"CacheInvalidationService started — polling vwSYSTEM_EVENTS every {IntervalMs}ms",
				nIntervalMs);

			try
			{
				// Begin polling immediately (no initial delay).
				// vwSYSTEM_EVENTS.MaxDate is initialized on first poll tick if not yet set.
				while (!stoppingToken.IsCancellationRequested)
				{
					await PollSystemEventsAsync(stoppingToken);

					// Wait configured interval before next poll.
					// OperationCanceledException from Task.Delay is caught in the outer try/catch.
					await Task.Delay(TimeSpan.FromMilliseconds(nIntervalMs), stoppingToken);
				}
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				// Expected during graceful host shutdown — not an error condition.
				_logger.LogInformation("CacheInvalidationService stopping gracefully.");
			}
		}

		// -----------------------------------------------------------------------
		// Private polling implementation
		// -----------------------------------------------------------------------

		/// <summary>
		/// Executes one poll cycle against <c>vwSYSTEM_EVENTS</c>.
		///
		/// <para>
		/// Faithfully replicates SchedulerUtils.cs OnTimer() lines 662–728, 735–753
		/// with the following .NET 10 substitutions:
		/// </para>
		/// <list type="bullet">
		///   <item>
		///     <c>Context.Application["SYSTEM_EVENTS.MaxDate"]</c>
		///     → <c>_memoryCache.TryGetValue/Set("SYSTEM_EVENTS.MaxDate")</c>
		///   </item>
		///   <item>
		///     <c>DbProviderFactories.GetFactory(Context.Application)</c>
		///     → <c>dbProviderFactories.GetFactory(_memoryCache)</c>
		///   </item>
		///   <item>
		///     <c>SplendidError.SystemMessage(Context, "Warning", ...)</c>
		///     → <c>_logger.LogWarning(...)</c>
		///   </item>
		///   <item>
		///     <c>SplendidError.SystemMessage(Context, "Error", ...)</c>
		///     → <c>_logger.LogError(...)</c>
		///   </item>
		/// </list>
		/// </summary>
		/// <param name="stoppingToken">Cancellation token — re-throws OperationCanceledException.</param>
		private async Task PollSystemEventsAsync(CancellationToken stoppingToken)
		{
			// Wrap the entire poll cycle in a try/catch to keep the polling loop alive
			// even after transient database or network errors.
			try
			{
				// Create a scoped DI container per poll tick.
				// This correctly handles scoped services (SplendidCache, SplendidInit, DbProviderFactories)
				// within a singleton BackgroundService.
				using (IServiceScope scope = _scopeFactory.CreateScope())
				{
					IServiceProvider scopedProvider = scope.ServiceProvider;

					// Resolve scoped services for this poll cycle.
					DbProviderFactories dbProviderFactories = scopedProvider.GetRequiredService<DbProviderFactories>();
					SplendidCache       splendidCache       = scopedProvider.GetRequiredService<SplendidCache>();
					SplendidInit        splendidInit        = scopedProvider.GetRequiredService<SplendidInit>();

					// Get database factory using IMemoryCache for connection string caching.
					// Matches: DbProviderFactory dbf = DbProviderFactories.GetFactory(Context.Application); (line 662)
					DbProviderFactory dbf = dbProviderFactories.GetFactory(_memoryCache);

					using (IDbConnection con = dbf.CreateConnection())
					{
						con.Open();

						// ------------------------------------------------------------------
						// Step 3a: Initialize / read last update timestamp (lines 667–673)
						// Matches: DateTime dtLastUpdate = Sql.ToDateTime(Context.Application["SYSTEM_EVENTS.MaxDate"]);
						// ------------------------------------------------------------------
						if (!_memoryCache.TryGetValue("SYSTEM_EVENTS.MaxDate", out DateTime dtLastUpdate)
							|| dtLastUpdate == DateTime.MinValue)
						{
							// First run — record current time so we only process future events.
							// Matches: dtLastUpdate = DateTime.Now; Context.Application["SYSTEM_EVENTS.MaxDate"] = dtLastUpdate; (lines 670–672)
							dtLastUpdate = DateTime.Now;
							_memoryCache.Set("SYSTEM_EVENTS.MaxDate", dtLastUpdate);
						}

						// ------------------------------------------------------------------
						// Step 3b: Query vwSYSTEM_EVENTS for tables changed after dtLastUpdate (lines 678–692)
						// Exact SQL preserved from SchedulerUtils.cs.
						// ------------------------------------------------------------------
						string sSQL = "select TABLE_NAME                  " + ControlChars.CrLf
						            + "  from vwSYSTEM_EVENTS             " + ControlChars.CrLf
						            + " where DATE_ENTERED > @DATE_ENTERED" + ControlChars.CrLf
						            + " group by TABLE_NAME               " + ControlChars.CrLf
						            + " order by TABLE_NAME               " + ControlChars.CrLf;

						using (IDbCommand cmd = con.CreateCommand())
						{
							cmd.CommandText = sSQL;
							// Matches: Sql.AddParameter(cmd, "@DATE_ENTERED", dtLastUpdate); (line 686)
							Sql.AddParameter(cmd, "@DATE_ENTERED", dtLastUpdate);

							using (DataTable dt = new DataTable())
							{
								using (DbDataAdapter da = dbf.CreateDataAdapter())
								{
									// Provider-agnostic cast — matches: ((IDbDataAdapter)da).SelectCommand = cmd; (line 691)
									((IDbDataAdapter)da).SelectCommand = cmd;
									da.Fill(dt);

									if (dt.Rows.Count > 0)
									{
										// --------------------------------------------------
										// Step 3c: Update max date in cache (lines 694–700)
										// Matches: cmd.Parameters.Clear(); sSQL = "select max(DATE_ENTERED)..."; dtLastUpdate = Sql.ToDateTime(cmd.ExecuteScalar()); Context.Application["SYSTEM_EVENTS.MaxDate"] = dtLastUpdate;
										// --------------------------------------------------
										cmd.Parameters.Clear();
										sSQL = "select max(DATE_ENTERED)" + ControlChars.CrLf
										     + "  from vwSYSTEM_EVENTS  " + ControlChars.CrLf;
										cmd.CommandText = sSQL;
										// Matches: dtLastUpdate = Sql.ToDateTime(cmd.ExecuteScalar()); (line 699)
										dtLastUpdate = Sql.ToDateTime(cmd.ExecuteScalar());
										// Matches: Context.Application["SYSTEM_EVENTS.MaxDate"] = dtLastUpdate; (line 700)
										_memoryCache.Set("SYSTEM_EVENTS.MaxDate", dtLastUpdate);

										// --------------------------------------------------
										// Step 3d: Build table list and log (lines 702–711)
										// Matches: SplendidError.SystemMessage(Context, "Warning", ..., "System Events: " + sbTables.ToString());
										// Matches: SplendidError.SystemMessage(Context, "Warning", ..., "System Events Last Update on " + dtLastUpdate.ToString());
										// --------------------------------------------------
										StringBuilder sbTables = new StringBuilder();
										foreach (DataRow row in dt.Rows)
										{
											if (sbTables.Length > 0)
												sbTables.Append(", ");
											// Matches: sbTables.Append(Sql.ToString(row["TABLE_NAME"])); (line 707)
											sbTables.Append(Sql.ToString(row["TABLE_NAME"]));
										}
										// Matches: SplendidError.SystemMessage(..., "System Events: " + sbTables.ToString()); (line 710)
										_logger.LogWarning("System Events: {TableList}", sbTables.ToString());
										// Matches: SplendidError.SystemMessage(..., "System Events Last Update on " + dtLastUpdate.ToString()); (line 711)
										_logger.LogWarning("System Events Last Update on {LastUpdate}", dtLastUpdate.ToString());

										// --------------------------------------------------
										// Step 3e: Clear caches for changed tables (lines 713–728)
										// --------------------------------------------------
										foreach (DataRow row in dt.Rows)
										{
											// Matches: string sTABLE_NAME = Sql.ToString(row["TABLE_NAME"]); (line 715)
											string sTABLE_NAME = Sql.ToString(row["TABLE_NAME"]);

											// KEY invalidation call — evicts stale cache entries for this table.
											// Matches: SplendidCache.ClearTable(sTABLE_NAME); (line 716)
											// Cache key families preserved (AAP 0.7.2):
											//   vwMODULES_*, vwTERMINOLOGY_*, vwGRIDVIEWS_*, vwDETAILVIEWS_*,
											//   vwEDITVIEWS_*, vwDYNAMIC_BUTTONS_*, CONFIG_*, vwTIMEZONES, vwCURRENCIES
											splendidCache.ClearTable(sTABLE_NAME);

											// Special re-initialization based on table name (lines 718–727).
											// Instance method calls — HttpContext parameter removed in .NET 10 migration;
											// SplendidInit resolves required context via IHttpContextAccessor DI.
											if (sTABLE_NAME.StartsWith("TERMINOLOGY"))
											{
												// Matches: SplendidInit.InitTerminology(Context); (line 719)
												splendidInit.InitTerminology();
											}
											else if (sTABLE_NAME == "MODULES" || sTABLE_NAME.StartsWith("ACL_"))
											{
												// Matches: SplendidInit.InitModuleACL(Context); (line 721)
												splendidInit.InitModuleACL();
											}
											else if (sTABLE_NAME == "CONFIG")
											{
												// Matches: SplendidInit.InitConfig(Context); (line 723)
												splendidInit.InitConfig();
											}
											else if (sTABLE_NAME == "TIMEZONES")
											{
												// Matches: SplendidInit.InitTimeZones(Context); (line 725)
												splendidInit.InitTimeZones();
											}
											else if (sTABLE_NAME == "CURRENCIES")
											{
												// Matches: SplendidInit.InitCurrencies(Context); (line 727)
												splendidInit.InitCurrencies();
											}
										}
									} // if ( dt.Rows.Count > 0 )
								} // using DbDataAdapter da
							} // using DataTable dt
						} // using IDbCommand cmd (SELECT from vwSYSTEM_EVENTS)

						// ------------------------------------------------------------------
						// Step 3f: Execute spSYSTEM_EVENTS_ProcessAll to purge old events (lines 735–753)
						// Clears processed events so future vwSYSTEM_EVENTS queries remain fast.
						// Wraps execution in a transaction matching original behavior.
						// ------------------------------------------------------------------
						using (IDbTransaction trn = Sql.BeginTransaction(con))
						{
							try
							{
								// Matches lines 739–744: create command, set CommandType.StoredProcedure,
								// set CommandText = "spSYSTEM_EVENTS_ProcessAll", ExecuteNonQuery
								using (IDbCommand cmdSP = con.CreateCommand())
								{
									cmdSP.Transaction   = trn;
									cmdSP.CommandType   = CommandType.StoredProcedure;
									cmdSP.CommandText   = "spSYSTEM_EVENTS_ProcessAll";
									cmdSP.ExecuteNonQuery();
								}
								trn.Commit();
							}
							catch (Exception exSP)
							{
								trn.Rollback();
								// Matches: SplendidError.SystemMessage(Context, "Error", ..., Utils.ExpandException(ex)); (line 751)
								_logger.LogError(exSP,
									"CacheInvalidationService: Error executing spSYSTEM_EVENTS_ProcessAll — transaction rolled back");
							}
						} // using IDbTransaction trn

					} // using IDbConnection con
				} // using IServiceScope scope

				// Yield control back to the caller — ensures async cooperative scheduling
				// without blocking the thread pool between poll ticks.
				await Task.Yield();
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				// Re-throw cancellation so ExecuteAsync can handle graceful shutdown.
				throw;
			}
			catch (Exception ex)
			{
				// Log and swallow transient errors (network, DB timeout, etc.) to keep the
				// polling loop alive after failures. Matches the intent of the original
				// SchedulerUtils.OnTimer which does not rethrow errors from its timer callback.
				_logger.LogError(ex,
					"CacheInvalidationService.PollSystemEventsAsync: Unhandled error in poll cycle — service will retry on next interval");
			}
		}
	}
}
