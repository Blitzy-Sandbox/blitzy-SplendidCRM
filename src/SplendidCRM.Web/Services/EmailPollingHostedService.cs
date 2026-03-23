/**********************************************************************************************************************
 * SplendidCRM is a Customer Relationship Management program created by SplendidCRM Software, Inc.
 * Copyright (C) 2005-2022 SplendidCRM Software, Inc. All rights reserved.
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
 * "Copyright (C) 2005-2011 SplendidCRM Software, Inc. All rights reserved."
 *********************************************************************************************************************/
// ============================================================================================================================
// Migrated from: SplendidCRM/Global.asax.cs (InitEmailManager) + SplendidCRM/_code/EmailUtils.cs (OnTimer)
//
// Migration rationale (per AAP §0.5.1, §0.6, §0.7.1, §0.8):
//   • System.Threading.Timer callback (Global.asax.cs line 66) → BackgroundService + Task.Delay loop
//       Original: new Timer(EmailUtils.OnTimer, this.Context, new TimeSpan(0,1,0), new TimeSpan(0,1,0))
//       Target  : ExecuteAsync with 60-second initial delay and configurable interval loop
//
//   • Context.Application["SplendidReminderServerFlag"] → IMemoryCache (DI constructor injection)
//       IMemoryCache replaces HttpApplicationState for in-process caching of the machine-name-based
//       server election result (integer flag: 0 = not yet evaluated, 1 = is reminder server,
//       -1 = not the reminder server).  Cache key "SplendidReminderServerFlag" preserved identically.
//
//   • ConfigurationManager.AppSettings["SplendidReminderServer"] → IConfiguration["SplendidReminderServer"]
//       The app-settings key name is unchanged; only the access pattern moves to DI.
//
//   • Static boolean flags bInsideActivityReminder / bInsideSmsActivityReminder (EmailUtils.cs lines 49-50)
//       → SemaphoreSlim(1,1) reentrancy guard.
//       Non-blocking Wait(0) preserves the "skip-on-busy" semantics of the original static flags:
//       if a previous tick is still executing, the current tick is skipped without blocking.
//
//   • SplendidError.SystemMessage / SystemWarning (Context, …) → ILogger<EmailPollingHostedService>
//
//   • HttpContext.Current → null passed to GoogleSync.GoogleWebhook.ProcessAllNotifications()
//       No active HTTP request context exists in a background service; the Community Edition stub
//       is a no-op (GoogleSync.cs), so null is safe.  Enterprise Edition callers should resolve
//       IHttpContextAccessor from the DI scope instead.
//
// Comment from original code (Global.asax.cs line 61):
//   "Use a separate timer for email reminders as they are timely and cannot be stuck behind other scheduler tasks."
// ============================================================================================================================
#nullable disable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SplendidCRM;

namespace SplendidCRM.Web.Services
{
	/// <summary>
	/// Long-running background service that implements the email polling timer.
	/// Replaces <c>Global.asax.cs InitEmailManager()</c> and the <c>EmailUtils.OnTimer</c> callback
	/// from the .NET Framework 4.8 codebase.
	///
	/// <para><b>Interval:</b> Configurable via <c>EMAIL_POLL_INTERVAL_MS</c> environment variable
	/// (default: 60 000 ms / 1 minute), matching the original
	/// <c>new Timer(…, new TimeSpan(0,1,0), new TimeSpan(0,1,0))</c> timer period.</para>
	///
	/// <para><b>Initial delay:</b> 60 000 ms (1 minute), matching the original timer due-time.</para>
	///
	/// <para><b>Reentrancy:</b> <see cref="SemaphoreSlim"/>(1,1) prevents concurrent execution,
	/// replacing the <c>bInsideActivityReminder</c> and <c>bInsideSmsActivityReminder</c> static
	/// boolean flags in the original <c>EmailUtils.cs</c> (lines 49-50).</para>
	///
	/// <para><b>Server election:</b> Machine-name-based election using the
	/// <c>SplendidReminderServer</c> configuration key and the <c>SplendidReminderServerFlag</c>
	/// cache key.  The evaluated result is cached in <see cref="IMemoryCache"/> so the comparison
	/// is performed only once per instance lifetime rather than on every tick.</para>
	/// </summary>
	public class EmailPollingHostedService : BackgroundService
	{
		// -----------------------------------------------------------------------
		// Fields — constructor-injected dependencies (no System.Web, no statics)
		// -----------------------------------------------------------------------

		/// <summary>
		/// Factory for creating per-tick DI scopes.  Required because BackgroundService is a
		/// singleton but EmailUtils has scoped or transient dependencies (e.g. IHttpContextAccessor).
		/// </summary>
		private readonly IServiceScopeFactory               _serviceScopeFactory;

		/// <summary>
		/// Application configuration provider.  Reads EMAIL_POLL_INTERVAL_MS and SplendidReminderServer.
		/// Replaces System.Configuration.ConfigurationManager.AppSettings[…].
		/// </summary>
		private readonly IConfiguration                     _configuration;

		/// <summary>
		/// In-memory cache that stores the evaluated reminder-server election flag.
		/// Replaces HttpApplicationState (Context.Application[…]).
		/// </summary>
		private readonly IMemoryCache                       _memoryCache;

		/// <summary>
		/// Structured logger replacing SplendidError.SystemMessage / SystemWarning.
		/// </summary>
		private readonly ILogger<EmailPollingHostedService> _logger;

		// -----------------------------------------------------------------------
		// Reentrancy guard
		// SemaphoreSlim(1,1) replaces the multiple static bInside* boolean flags
		// from EmailUtils.cs (lines 46-50):
		//   bInsideSendQueue          — SendQueue (not applicable here)
		//   bInsideCheckInbound       — not applicable here
		//   bInsideCheckOutbound      — not applicable here
		//   bInsideActivityReminder   — replaces this flag for email reminders
		//   bInsideSmsActivityReminder— replaces this flag for SMS reminders
		//
		// Wait(0) is non-blocking; if the semaphore cannot be acquired the tick is
		// logged as "Email Polling Busy" and skipped, matching the original pattern.
		// -----------------------------------------------------------------------
		private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

		// -----------------------------------------------------------------------
		// Cache key constants (must match the original Application[] key names)
		// -----------------------------------------------------------------------
		private const string CacheKey_ReminderServerFlag   = "SplendidReminderServerFlag";

		// -----------------------------------------------------------------------
		// Configuration key constants (must match original appSettings key names)
		// -----------------------------------------------------------------------
		private const string ConfigKey_EmailPollIntervalMs = "EMAIL_POLL_INTERVAL_MS";
		private const string ConfigKey_ReminderServer      = "SplendidReminderServer";

		// -----------------------------------------------------------------------
		// Default interval — 60 000 ms = 1 minute, matching Global.asax.cs line 66:
		//   new Timer(EmailUtils.OnTimer, this.Context, new TimeSpan(0,1,0), new TimeSpan(0,1,0))
		// -----------------------------------------------------------------------
		private const int DefaultIntervalMs = 60000;

		/// <summary>
		/// Initialises a new instance of <see cref="EmailPollingHostedService"/> with all required
		/// dependencies injected.  The ASP.NET Core DI container registers this service as a
		/// singleton via <c>AddHostedService&lt;EmailPollingHostedService&gt;()</c> in Program.cs.
		/// </summary>
		/// <param name="serviceScopeFactory">
		///   Factory used to create a short-lived DI scope for each timer tick, allowing scoped
		///   services such as <see cref="EmailUtils"/> to be resolved safely from this singleton.
		/// </param>
		/// <param name="configuration">
		///   Application configuration; supplies EMAIL_POLL_INTERVAL_MS and SplendidReminderServer.
		/// </param>
		/// <param name="memoryCache">
		///   Shared in-process cache; stores the SplendidReminderServerFlag election result.
		/// </param>
		/// <param name="logger">
		///   Structured logger for activation, election, busy-skip, and error messages.
		/// </param>
		public EmailPollingHostedService(
			IServiceScopeFactory               serviceScopeFactory,
			IConfiguration                     configuration,
			IMemoryCache                       memoryCache,
			ILogger<EmailPollingHostedService> logger)
		{
			_serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
			_configuration       = configuration       ?? throw new ArgumentNullException(nameof(configuration));
			_memoryCache         = memoryCache         ?? throw new ArgumentNullException(nameof(memoryCache));
			_logger              = logger              ?? throw new ArgumentNullException(nameof(logger));
		}

		// -----------------------------------------------------------------------
		// ExecuteAsync — timer loop (IHostedService entry point)
		// Replaces: Global.asax.cs InitEmailManager() / System.Threading.Timer
		// -----------------------------------------------------------------------

		/// <summary>
		/// Entry point called by the ASP.NET Core host when the application starts.
		/// Implements the one-minute email polling loop, faithfully reproducing the
		/// behaviour of the original <c>Timer</c>-based email manager in Global.asax.cs.
		/// </summary>
		/// <param name="stoppingToken">
		///   Cancellation token signalled when the host begins a graceful shutdown.
		///   Passed to <see cref="Task.Delay(TimeSpan, CancellationToken)"/> so the
		///   service exits promptly without waiting for the full interval.
		/// </param>
		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			// ---------------------------------------------------------------
			// Resolve timer interval from configuration.
			// Environment variable EMAIL_POLL_INTERVAL_MS overrides the default.
			// Any value below 1 000 ms is rejected to prevent runaway polling;
			// the default 60 000 ms (1 minute) is used instead.
			// ---------------------------------------------------------------
			int nIntervalMs = DefaultIntervalMs;
			string sIntervalConfig = _configuration[ConfigKey_EmailPollIntervalMs];
			if (!Sql.IsEmptyString(sIntervalConfig))
			{
				int nParsed = Sql.ToInteger(sIntervalConfig);
				if (nParsed >= 1000)
					nIntervalMs = nParsed;
			}

			// Activation message — preserves original log entry from Global.asax.cs line 67
			// (originally SplendidError.SystemWarning → now ILogger.LogInformation).
			// Original text: "The Email Manager timer has been activated."
			_logger.LogInformation("The Email Manager hosted service has been activated.");

			try
			{
				// -----------------------------------------------------------
				// Initial delay of 60 000 ms (1 minute) before the first tick.
				// Matches the dueTime parameter of the original timer (line 66):
				//   new Timer(EmailUtils.OnTimer, this.Context, new TimeSpan(0,1,0), new TimeSpan(0,1,0))
				//                                                                     ^^ dueTime = 1 min
				// -----------------------------------------------------------
				await Task.Delay(TimeSpan.FromMilliseconds(DefaultIntervalMs), stoppingToken);

				while (!stoppingToken.IsCancellationRequested)
				{
					await DoWorkAsync(stoppingToken);
					await Task.Delay(TimeSpan.FromMilliseconds(nIntervalMs), stoppingToken);
				}
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				// Graceful shutdown — OperationCanceledException from Task.Delay is expected
				// when the ASP.NET Core host signals a shutdown via the stoppingToken.
				_logger.LogInformation("EmailPollingHostedService stopping gracefully.");
			}
		}

		// -----------------------------------------------------------------------
		// DoWorkAsync — single timer tick (mirrors EmailUtils.OnTimer lines 2176-2214)
		// -----------------------------------------------------------------------

		/// <summary>
		/// Executes one polling cycle.  Faithfully reproduces the logic of
		/// <c>EmailUtils.OnTimer(Object sender)</c> (original lines 2176-2214):
		/// <list type="number">
		///   <item>Machine-name-based reminder-server election (lines 2181-2206)</item>
		///   <item>Email activity reminders via <c>EmailUtils.SendActivityReminders</c> (line 2209)</item>
		///   <item>SMS activity reminders via <c>EmailUtils.SendSmsActivityReminders</c> (line 2210)</item>
		///   <item>Google webhook processing via <c>GoogleSync.GoogleWebhook.ProcessAllNotifications</c> (line 2213)</item>
		/// </list>
		/// </summary>
		/// <param name="stoppingToken">Host shutdown token; not awaited inside (work is synchronous).</param>
		/// <returns>A completed <see cref="Task"/>.</returns>
		private Task DoWorkAsync(CancellationToken stoppingToken)
		{
			// -------------------------------------------------------------------
			// Reentrancy check — non-blocking Wait(0).
			// If the semaphore cannot be acquired immediately, a previous tick is
			// still executing.  Skip this tick with a warning rather than blocking,
			// matching the original "if (bInsideActivityReminder) return" pattern.
			// -------------------------------------------------------------------
			if (!_semaphore.Wait(0))
			{
				_logger.LogWarning(
					"Email Polling Busy — skipping tick (previous execution still running).");
				return Task.CompletedTask;
			}

			try
			{
				// ---------------------------------------------------------------
				// STEP 1 — Reminder server election
				// Matches EmailUtils.OnTimer lines 2181-2206.
				//
				// The election result is cached in IMemoryCache at key
				// "SplendidReminderServerFlag" to avoid repeated machine-name
				// comparisons on every tick (original: Context.Application[…]).
				//
				// Values:
				//   0  — not yet evaluated (default / key missing from cache)
				//   1  — this instance IS the Splendid Reminder Server
				//  -1  — this instance is NOT the Splendid Reminder Server
				// ---------------------------------------------------------------
				int nSplendidReminderServerFlag = 0;

				// Replaces: Sql.ToInteger(Context.Application["SplendidReminderServerFlag"])
				if (_memoryCache.TryGetValue(CacheKey_ReminderServerFlag, out object flagObj))
				{
					nSplendidReminderServerFlag = Sql.ToInteger(flagObj);
				}

				if (nSplendidReminderServerFlag == 0)
				{
					// Replaces: ConfigurationManager.AppSettings["SplendidReminderServer"] (line 2184)
					string sSplendidReminderServer = _configuration[ConfigKey_ReminderServer];

					// Default machine name to configured server name so that, if Environment.MachineName
					// throws (Azure App Service, restricted environments), this instance is treated as the
					// configured server — a safe fallback matching the original comment at line 2189:
					// "Azure does not support MachineName. Just ignore the error."
					string sMachineName = sSplendidReminderServer;
					try
					{
						// Replaces: System.Environment.MachineName (line 2190)
						sMachineName = Environment.MachineName;
					}
					catch
					{
						// Azure / restricted environment compatibility: MachineName may not be accessible.
						// sMachineName retains sSplendidReminderServer so the comparison succeeds.
					}

					// Replaces: String.Compare(sMachineName, sSplendidReminderServer, true) == 0  (line 2195)
					// Empty sSplendidReminderServer means any single instance is the reminder server.
					if (Sql.IsEmptyString(sSplendidReminderServer) ||
					    String.Compare(sMachineName, sSplendidReminderServer, StringComparison.OrdinalIgnoreCase) == 0)
					{
						nSplendidReminderServerFlag = 1;
						// Replaces: SplendidError.SystemMessage(Context, "Warning", …, sMachineName + " is a Splendid Reminder Server.")  (line 2198)
						_logger.LogWarning("{MachineName} is a Splendid Reminder Server.", sMachineName);
					}
					else
					{
						nSplendidReminderServerFlag = -1;
						// Replaces: SplendidError.SystemMessage(Context, "Warning", …, sMachineName + " is not a Splendid Reminder Server.")  (line 2203)
						_logger.LogWarning("{MachineName} is not a Splendid Reminder Server.", sMachineName);
					}

					// Cache the evaluated flag so subsequent ticks skip the election logic.
					// Replaces: Context.Application["SplendidReminderServerFlag"] = nSplendidReminderServerFlag  (line 2205)
					_memoryCache.Set(CacheKey_ReminderServerFlag, (object)nSplendidReminderServerFlag);
				}

				// ---------------------------------------------------------------
				// STEP 2 — Execute email operations within a scoped DI container.
				// A new scope is created for each tick because EmailUtils depends on
				// scoped or transient services (e.g. IHttpContextAccessor) that must
				// not be resolved from the singleton BackgroundService lifetime.
				// ---------------------------------------------------------------
				using (IServiceScope scope = _serviceScopeFactory.CreateScope())
				{
					IServiceProvider sp = scope.ServiceProvider;

					if (nSplendidReminderServerFlag > 0)
					{
						// Resolve EmailUtils from scoped container.
						// It has constructor-injected dependencies (IHttpContextAccessor, IMemoryCache, etc.)
						// that require a proper DI scope lifetime.
						EmailUtils emailUtils = sp.GetRequiredService<EmailUtils>();

						// Line 2209: SendActivityReminders — sends email reminders for Meetings/Calls/Tasks
						// to invitees whose REMINDER_EMAIL_SENT flag is 0 and whose event is upcoming.
						emailUtils.SendActivityReminders();

						// Line 2210: SendSmsActivityReminders — sends SMS reminders via Twilio for
						// the same activity types when CONFIG.enable_activity_sms_reminder is enabled.
						emailUtils.SendSmsActivityReminders();
					}

					// Line 2213: GoogleSync.GoogleWebhook.ProcessAllNotifications — processes all pending
					// Google push notification subscriptions (e.g. Calendar / Contacts webhook callbacks).
					// Called on every tick regardless of reminder-server election result.
					//
					// NOTE: This is a static method on a nested static class (GoogleSync.GoogleWebhook).
					// No active HttpContext exists in a BackgroundService context; null is passed.
					// The Community Edition implementation is a no-op stub (GoogleSync.cs); Enterprise
					// Edition provides the full implementation and must tolerate a null context by
					// resolving IHttpContextAccessor from the injected GoogleSync instance instead.
					GoogleSync.GoogleWebhook.ProcessAllNotifications(null);
				}
			}
			catch (Exception ex)
			{
				// Log error but do NOT rethrow.
				// Keeping the exception within the catch block ensures the polling loop
				// continues on the next tick, matching the resilient error-handling pattern
				// of the original EmailUtils.OnTimer.
				// Replaces: SplendidError.SystemMessage(Context, "Error", …, ex.Message) → ILogger.LogError
				_logger.LogError(ex, "EmailPollingHostedService.DoWorkAsync encountered an error.");
			}
			finally
			{
				// Always release the semaphore so the next tick can enter DoWorkAsync.
				_semaphore.Release();
			}

			return Task.CompletedTask;
		}

		// -----------------------------------------------------------------------
		// Dispose — clean up owned resources before the host releases this service
		// -----------------------------------------------------------------------

		/// <summary>
		/// Disposes the <see cref="SemaphoreSlim"/> reentrancy guard and releases
		/// any resources held by the base <see cref="BackgroundService"/> (primarily
		/// the internal <see cref="CancellationTokenSource"/>).
		/// </summary>
		public override void Dispose()
		{
			_semaphore.Dispose();
			base.Dispose();
		}
	}
}
