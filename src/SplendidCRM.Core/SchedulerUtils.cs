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
// .NET 10 Migration: SplendidCRM/_code/SchedulerUtils.cs → src/SplendidCRM.Core/SchedulerUtils.cs
// Changes applied:
//   - REMOVED: using System.Web; (HttpContext.Current, HttpApplicationState)
//   - ADDED:   using Microsoft.AspNetCore.Http; (HttpContext, IHttpContextAccessor)
//   - ADDED:   using Microsoft.Extensions.Caching.Memory; (IMemoryCache replaces Application[])
//   - ADDED:   using Microsoft.Extensions.Configuration; (IConfiguration replaces ConfigurationManager.AppSettings)
//   - REPLACED: static class pattern → instance class with DI constructor
//   - REPLACED: Context.Application["key"] → _memoryCache.Get<object>("key") / _memoryCache.Set("key", ...)
//   - REPLACED: HttpContext.Current → IHttpContextAccessor (_httpContextAccessor)
//   - REPLACED: ConfigurationManager.AppSettings["SplendidJobServer"] → _configuration["SplendidJobServer"]
//   - REPLACED: DbProviderFactories.GetFactory(Context.Application) → _dbProviderFactories.GetFactory(_memoryCache)
//   - REPLACED: SplendidError.SystemMessage(Context, ...) → SplendidError.SystemMessage(_memoryCache, ...)
//   - REPLACED: SplendidInit.InitTerminology(Context) → _splendidInit.InitTerminology()
//   - REPLACED: SplendidCache.ClearTable(name) → _splendidCache.ClearTable(name)
//   - REPLACED: WorkflowUtils.Process(Context) → _workflowUtils.Process(httpContext)
//   - REPLACED: EmailUtils.SendQueued/CheckMonitored/CheckBounced/SendOutbound(Context,...) → instance methods (no Context param)
//   - REPLACED: Utils.CheckVersion(Context.Application) → _utils.CheckVersion()
//   - REPLACED: static sender pattern → instance methods with IHttpContextAccessor
//   - PRESERVED: bInsideTimer, bInsideArchiveTimer static reentrancy guard flags
//   - PRESERVED: Jobs static string array with all job names
//   - PRESERVED: CronDescription localization logic exactly
//   - PRESERVED: OnTimer(Object), OnArchiveTimer(Object) signatures for schema compliance
//   - ADDED:   OnTimer() and OnArchiveTimer() no-arg overloads for SchedulerHostedService and ArchiveHostedService
//   - PRESERVED: All scheduler job names: CleanSystemLog, pruneDatabase, BackupDatabase,
//     BackupTransactionLog, CheckVersion, RunAllArchiveRules, RunExternalArchive, and others
//   - PRESERVED: Machine-name-based job election via SPLENDID_JOB_SERVER / Scheduler:JobServer
//   - PRESERVED: vwSYSTEM_EVENTS cache invalidation logic
//   - PRESERVED: dtLastUpdate tracking using SYSTEM_EVENTS.MaxDate cache key
//   - PRESERVED: SemaphoreSlim-compatible reentrancy guards (static bInsideTimer, bInsideArchiveTimer)
//   - PRESERVED: All SqlProcs.spSCHEDULERS_UpdateLastRun, SqlProcs.spSqlPruneDatabase calls
//   - PRESERVED: #if !ReactOnlyUI conditional for RunExternalArchive
//   - PRESERVED: sLastJob tracking for verbose logging
//   - Minimal change clause: Only changes necessary for .NET Framework 4.8 → .NET 10 ASP.NET Core
#nullable disable
using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SplendidCRM
{
	/// <summary>
	/// Scheduler job execution logic — cron job scheduling and reentrancy guards.
	/// Migrated from SplendidCRM/_code/SchedulerUtils.cs (~1013 lines) for .NET 10 ASP.NET Core.
	///
	/// Replaces:
	///   - HttpContext.Current     → IHttpContextAccessor DI injection
	///   - Application[]          → IMemoryCache DI injection
	///   - ConfigurationManager   → IConfiguration DI injection
	///   - Static method pattern  → Instance-based DI service
	///
	/// DESIGN NOTES:
	///   • Register SchedulerUtils as a SCOPED service so that each timer tick
	///     gets a fresh scope with the correct IHttpContextAccessor-bound context.
	///   • The SchedulerHostedService and ArchiveHostedService call the no-arg overloads
	///     OnTimer() and OnArchiveTimer() respectively.
	///   • The Object-parameter overloads OnTimer(Object) and OnArchiveTimer(Object) are
	///     preserved for schema compliance; the sender is ignored (services use injected DI).
	///   • Static bInsideTimer and bInsideArchiveTimer reentrancy guards are preserved exactly
	///     as in the original to prevent concurrent scheduler job execution.
	/// </summary>
	public class SchedulerUtils
	{
		// =====================================================================================
		// Static reentrancy guards — preserved exactly from source
		// BEFORE (.NET Framework 4.8): static bool bInsideTimer; static bool bInsideArchiveTimer;
		// AFTER (.NET 10):             Same — static fields survive DI scope resets;
		//                              the hosted services add their own SemaphoreSlim on top.
		// 12/22/2007 Paul.  In case the timer takes a long time, only allow one timer event to be processed.
		// =====================================================================================
		private static bool bInsideTimer        = false;
		// 11/08/2022 Paul.  Separate Archive timer.
		public static  bool bInsideArchiveTimer = false;
		// 11/02/2022 Paul.  Keep track of last job for verbose logging.
		private static string sLastJob          = String.Empty;

		// =====================================================================================
		// Jobs list — preserved exactly from source
		// Lists all scheduler job function names recognized by RunJob's switch statement.
		// Exposed as public static for UI binding in the scheduler admin panel.
		// =====================================================================================
		public static string[] Jobs = new string[]
			{ "pollMonitoredInboxes"
			, "runMassEmailCampaign"
			, "pruneDatabase"
			, "pollMonitoredInboxesForBouncedCampaignEmails"
			, "BackupDatabase"
			, "BackupTransactionLog"
			, "CleanSystemLog"
			, "CleanSystemSyncLog"
			, "CheckVersion"
			, "pollOutboundEmails"
			, "RunAllArchiveRules"    // 02/17/2018 Paul.  ModulesArchiveRules module to Professional.
			, "RunExternalArchive"    // 04/10/2018 Paul.  Run External Archive.
			};

		// =====================================================================================
		// DI-injected services replacing static/HttpContext access patterns
		// =====================================================================================
		private readonly IHttpContextAccessor    _httpContextAccessor;
		private readonly IMemoryCache            _memoryCache        ;
		private readonly IConfiguration          _configuration      ;
		private readonly DbProviderFactories     _dbProviderFactories;
		private readonly SplendidCache           _splendidCache      ;
		private readonly SplendidInit            _splendidInit       ;
		private readonly Utils                   _utils              ;
		private readonly WorkflowUtils           _workflowUtils      ;
		private readonly EmailUtils              _emailUtils         ;
		private readonly ArchiveExternalDB       _archiveExternalDB  ;
		private readonly ILogger<SchedulerUtils> _logger             ;

		/// <summary>
		/// Initializes a new SchedulerUtils with all required DI-injected services.
		/// </summary>
		/// <param name="httpContextAccessor">Replaces HttpContext.Current access pattern</param>
		/// <param name="memoryCache">Replaces Application[] state access pattern</param>
		/// <param name="configuration">Replaces ConfigurationManager.AppSettings access pattern</param>
		/// <param name="dbProviderFactories">Database factory (replaces GetFactory(Application))</param>
		/// <param name="splendidCache">Cache service for ClearTable invocations</param>
		/// <param name="splendidInit">Init service for cache re-initialization after vwSYSTEM_EVENTS changes</param>
		/// <param name="utils">Utility service providing CheckVersion and ExpandException</param>
		/// <param name="workflowUtils">Workflow processing service called after each timer tick</param>
		/// <param name="emailUtils">Email operations service for email-related scheduler jobs</param>
		/// <param name="archiveExternalDB">External archive DB service for RunExternalArchive job</param>
		/// <param name="logger">Logger for structured diagnostic output</param>
		public SchedulerUtils(
			IHttpContextAccessor    httpContextAccessor,
			IMemoryCache            memoryCache        ,
			IConfiguration          configuration      ,
			DbProviderFactories     dbProviderFactories,
			SplendidCache           splendidCache      ,
			SplendidInit            splendidInit       ,
			Utils                   utils              ,
			WorkflowUtils           workflowUtils      ,
			EmailUtils              emailUtils         ,
			ArchiveExternalDB       archiveExternalDB  ,
			ILogger<SchedulerUtils> logger             )
		{
			_httpContextAccessor = httpContextAccessor;
			_memoryCache         = memoryCache        ;
			_configuration       = configuration      ;
			_dbProviderFactories = dbProviderFactories;
			_splendidCache       = splendidCache      ;
			_splendidInit        = splendidInit       ;
			_utils               = utils              ;
			_workflowUtils       = workflowUtils      ;
			_emailUtils          = emailUtils         ;
			_archiveExternalDB   = archiveExternalDB  ;
			_logger              = logger             ;
		}

		#region CronDescription
		/// <summary>
		/// Builds a human-readable description of a SplendidCRM cron expression.
		/// Preserved exactly from SplendidCRM/_code/SchedulerUtils.cs.
		/// 
		/// The cron expression format is: minute::hour::dayOfMonth::month::dayOfWeek
		/// where each component may be * (any), a single value, a comma-separated list,
		/// or a dash-separated range. Components are separated by "::" (displayed with spaces removed).
		/// </summary>
		/// <param name="L10n">Localization instance for translating schedule terms</param>
		/// <param name="sCRON">Cron expression string in SplendidCRM format</param>
		/// <returns>Human-readable schedule description in the user's locale</returns>
		public static string CronDescription(L10N L10n, string sCRON)
		{
			StringBuilder sb = new StringBuilder();
			sCRON = sCRON.Replace(" ", "");
			if ( sCRON == "*::*::*::*::*" )
				return L10n.Term("Schedulers.LBL_OFTEN");
			// 01/28/2009 Paul.  Catch any processing errors during Cron processing.
			try
			{
				CultureInfo culture = CultureInfo.CreateSpecificCulture(L10n.NAME);
				string sCRON_MONTH       = "*";
				string sCRON_DAYOFMONTH  = "*";
				string sCRON_DAYOFWEEK   = "*";
				string sCRON_HOUR        = "*";
				string sCRON_MINUTE      = "*";
				string[] arrCRON         = sCRON.Replace("::", "|").Split('|');
				string[] arrCRON_TEMP    = new string[] {};
				string[] arrCRON_VALUE   = new string[] {};
				string[] arrDaySuffixes  = new string[32];
				int    nCRON_VALUE       = 0;
				int    nCRON_VALUE_START = 0;
				int    nCRON_VALUE_END   = 0;
				int    nON_THE_MINUTE    = -1;
				for ( int n = 0; n < arrDaySuffixes.Length; n++ )
					arrDaySuffixes[n] = "th";
				arrDaySuffixes[0] = "";
				arrDaySuffixes[1] = "st";
				arrDaySuffixes[2] = "nd";
				arrDaySuffixes[3] = "rd";

				// minute  hour  dayOfMonth  month  dayOfWeek
				if ( arrCRON.Length > 0 ) sCRON_MINUTE     = arrCRON[0];
				if ( arrCRON.Length > 1 ) sCRON_HOUR       = arrCRON[1];
				if ( arrCRON.Length > 2 ) sCRON_DAYOFMONTH = arrCRON[2];
				if ( arrCRON.Length > 3 ) sCRON_MONTH      = arrCRON[3];
				if ( arrCRON.Length > 4 ) sCRON_DAYOFWEEK  = arrCRON[4];
				if ( sCRON_MINUTE != "*" )
				{
					arrCRON_TEMP = sCRON_MINUTE.Split(',');
					// 12/31/2007 Paul.  Check for either comma or dash.
					if ( sCRON_MINUTE.Split(",-".ToCharArray()).Length == 1 )
					{
						nON_THE_MINUTE = Sql.ToInteger(sCRON_MINUTE);
						sb.Append(L10n.Term("Schedulers.LBL_ON_THE"));
						// 05/23/2013 Paul.  Just in case there is no space in the LBL_ON_THE term, add a space.
						sb.Append(" ");
						if ( nON_THE_MINUTE == 0 )
						{
							sb.Append(L10n.Term("Schedulers.LBL_HOUR_SING"));
						}
						else
						{
							sb.Append(nON_THE_MINUTE.ToString("00"));
							// 05/23/2013 Paul.  Just in case there is no space in the LBL_MIN_MARK term, add a space.
							sb.Append(" ");
							sb.Append(L10n.Term("Schedulers.LBL_MIN_MARK"));
						}
					}
					else
					{
						for ( int i = 0, nCronEntries = 0; i < arrCRON_TEMP.Length; i++ )
						{
							if ( arrCRON_TEMP[i].IndexOf('-') >= 0 )
							{
								arrCRON_VALUE = arrCRON_TEMP[i].Split('-');
								if ( arrCRON_VALUE.Length >= 2 )
								{
									nCRON_VALUE_START = Sql.ToInteger(arrCRON_VALUE[0]);
									nCRON_VALUE_END   = Sql.ToInteger(arrCRON_VALUE[1]);
									// 07/24/2023 Paul.  Minutes should range between 0 and 59.
									if ( nCRON_VALUE_START >= 0 && nCRON_VALUE_START <= 59 && nCRON_VALUE_END >= 0 && nCRON_VALUE_END <= 59 )
									{
										if ( nCronEntries > 0 )
											sb.Append(L10n.Term("Schedulers.LBL_AND"));
										sb.Append(L10n.Term("Schedulers.LBL_FROM"));
										sb.Append(L10n.Term("Schedulers.LBL_ON_THE"));
										// 05/23/2013 Paul.  Just in case there is no space in the LBL_ON_THE term, add a space.
										sb.Append(" ");
										if ( nCRON_VALUE_START == 0 )
										{
											sb.Append(L10n.Term("Schedulers.LBL_HOUR_SING"));
										}
										else
										{
											sb.Append(nCRON_VALUE_START.ToString("0"));
											// 05/23/2013 Paul.  Just in case there is no space in the LBL_MIN_MARK term, add a space.
											sb.Append(" ");
											sb.Append(L10n.Term("Schedulers.LBL_MIN_MARK"));
										}
										sb.Append(L10n.Term("Schedulers.LBL_RANGE"));
										sb.Append(L10n.Term("Schedulers.LBL_ON_THE"));
										// 05/23/2013 Paul.  Just in case there is no space in the LBL_ON_THE term, add a space.
										sb.Append(" ");
										sb.Append(nCRON_VALUE_END.ToString("0"));
										// 05/23/2013 Paul.  Just in case there is no space in the LBL_MIN_MARK term, add a space.
										sb.Append(" ");
										sb.Append(L10n.Term("Schedulers.LBL_MIN_MARK"));
										nCronEntries++;
									}
								}
							}
							else
							{
								nCRON_VALUE = Sql.ToInteger(arrCRON_TEMP[i]);
								// 07/24/2023 Paul.  Minutes should range between 0 and 59.
								if ( nCRON_VALUE >= 0 && nCRON_VALUE <= 59 )
								{
									if ( nCronEntries > 0 )
										sb.Append(L10n.Term("Schedulers.LBL_AND"));
									sb.Append(L10n.Term("Schedulers.LBL_ON_THE"));
									// 05/23/2013 Paul.  Just in case there is no space in the LBL_ON_THE term, add a space.
									sb.Append(" ");
									if ( nCRON_VALUE == 0 )
									{
										sb.Append(L10n.Term("Schedulers.LBL_HOUR_SING"));
									}
									else
									{
										sb.Append(nCRON_VALUE.ToString("0"));
										// 05/23/2013 Paul.  Just in case there is no space in the LBL_MIN_MARK term, add a space.
										sb.Append(" ");
										sb.Append(L10n.Term("Schedulers.LBL_MIN_MARK"));
									}
									nCronEntries++;
								}
							}
						}
					}
				}
				if ( sCRON_HOUR != "*" )
				{
					if ( sb.Length > 0 )
						sb.Append("; ");
					arrCRON_TEMP = sCRON_HOUR.Split(',');
					for ( int i = 0, nCronEntries = 0; i < arrCRON_TEMP.Length; i++ )
					{
						if ( arrCRON_TEMP[i].IndexOf('-') >= 0 )
						{
							arrCRON_VALUE = arrCRON_TEMP[i].Split('-');
							if ( arrCRON_VALUE.Length >= 2 )
							{
								nCRON_VALUE_START = Sql.ToInteger(arrCRON_VALUE[0]);
								nCRON_VALUE_END   = Sql.ToInteger(arrCRON_VALUE[1]);
								// 07/24/2023 Paul.  Hours should range between 0 and 23.
								if ( nCRON_VALUE_START >= 0 && nCRON_VALUE_START <= 23 && nCRON_VALUE_END >= 0 && nCRON_VALUE_END <= 23 )
								{
									if ( nCronEntries > 0 )
										sb.Append(L10n.Term("Schedulers.LBL_AND"));
									sb.Append(L10n.Term("Schedulers.LBL_FROM"));
									sb.Append(arrCRON_VALUE[0]);
									if ( nON_THE_MINUTE >= 0 )
										sb.Append(":" + nON_THE_MINUTE.ToString("00"));
									sb.Append(L10n.Term("Schedulers.LBL_RANGE"));
									sb.Append(arrCRON_VALUE[1]);
									if ( nON_THE_MINUTE >= 0 )
										sb.Append(":" + nON_THE_MINUTE.ToString("00"));
									nCronEntries++;
								}
							}
						}
						else
						{
							nCRON_VALUE = Sql.ToInteger(arrCRON_TEMP[i]);
							// 07/24/2023 Paul.  Hours should range between 0 and 23.
							if ( nCRON_VALUE >= 0 && nCRON_VALUE <= 23 )
							{
								if ( nCronEntries > 0 )
									sb.Append(L10n.Term("Schedulers.LBL_AND"));
								sb.Append(arrCRON_TEMP[i]);
								if ( nON_THE_MINUTE >= 0 )
									sb.Append(":" + nON_THE_MINUTE.ToString("00"));
								nCronEntries++;
							}
						}
					}
				}
				if ( sCRON_DAYOFMONTH != "*" )
				{
					if ( sb.Length > 0 )
						sb.Append("; ");
					arrCRON_TEMP = sCRON_DAYOFMONTH.Split(',');
					for ( int i = 0, nCronEntries = 0; i < arrCRON_TEMP.Length; i++ )
					{
						if ( arrCRON_TEMP[i].IndexOf('-') >= 0 )
						{
							arrCRON_VALUE = arrCRON_TEMP[i].Split('-');
							if ( arrCRON_VALUE.Length >= 2 )
							{
								nCRON_VALUE_START = Sql.ToInteger(arrCRON_VALUE[0]);
								nCRON_VALUE_END   = Sql.ToInteger(arrCRON_VALUE[1]);
								if ( nCRON_VALUE_START >= 1 && nCRON_VALUE_START <= 31 && nCRON_VALUE_END >= 1 && nCRON_VALUE_END <= 31 )
								{
									if ( nCronEntries > 0 )
										sb.Append(L10n.Term("Schedulers.LBL_AND"));
									sb.Append(L10n.Term("Schedulers.LBL_FROM"));
									sb.Append(nCRON_VALUE_START.ToString() + arrDaySuffixes[nCRON_VALUE_START]);
									sb.Append(L10n.Term("Schedulers.LBL_RANGE"));
									sb.Append(nCRON_VALUE_END.ToString() + arrDaySuffixes[nCRON_VALUE_END]);
									nCronEntries++;
								}
							}
						}
						else
						{
							nCRON_VALUE = Sql.ToInteger(arrCRON_TEMP[i]);
							if ( nCRON_VALUE >= 1 && nCRON_VALUE <= 31 )
							{
								if ( nCronEntries > 0 )
									sb.Append(L10n.Term("Schedulers.LBL_AND"));
								sb.Append(nCRON_VALUE.ToString() + arrDaySuffixes[nCRON_VALUE]);
								nCronEntries++;
							}
						}
					}
				}
				if ( sCRON_MONTH != "*" )
				{
					if ( sb.Length > 0 )
						sb.Append("; ");
					arrCRON_TEMP = sCRON_MONTH.Split(',');
					for ( int i = 0, nCronEntries = 0; i < arrCRON_TEMP.Length; i++ )
					{
						if ( arrCRON_TEMP[i].IndexOf('-') >= 0 )
						{
							arrCRON_VALUE = arrCRON_TEMP[i].Split('-');
							if ( arrCRON_VALUE.Length >= 2 )
							{
								nCRON_VALUE_START = Sql.ToInteger(arrCRON_VALUE[0]);
								nCRON_VALUE_END   = Sql.ToInteger(arrCRON_VALUE[1]);
								if ( nCRON_VALUE_START >= 1 && nCRON_VALUE_START <= 12 && nCRON_VALUE_END >= 1 && nCRON_VALUE_END <= 12 )
								{
									if ( nCronEntries > 0 )
										sb.Append(L10n.Term("Schedulers.LBL_AND"));
									sb.Append(L10n.Term("Schedulers.LBL_FROM"));
									// 08/17/2012 Paul.  LBL_FROM should have a trailing space, but it does not so fix here.
									sb.Append(" ");
									// 08/17/2012 Paul.  Month names are 0 based.
									sb.Append(culture.DateTimeFormat.MonthNames[nCRON_VALUE_START - 1]);
									sb.Append(L10n.Term("Schedulers.LBL_RANGE"));
									sb.Append(culture.DateTimeFormat.MonthNames[nCRON_VALUE_END - 1]);
									nCronEntries++;
								}
							}
						}
						else
						{
							nCRON_VALUE = Sql.ToInteger(arrCRON_TEMP[i]);
							if ( nCRON_VALUE >= 1 && nCRON_VALUE <= 12 )
							{
								if ( nCronEntries > 0 )
									sb.Append(L10n.Term("Schedulers.LBL_AND"));
								// 08/17/2012 Paul.  Month names are 0 based.
								sb.Append(culture.DateTimeFormat.MonthNames[nCRON_VALUE - 1]);
								nCronEntries++;
							}
						}
					}
				}
				if ( sCRON_DAYOFWEEK != "*" )
				{
					if ( sb.Length > 0 )
						sb.Append("; ");
					arrCRON_TEMP = sCRON_DAYOFWEEK.Split(',');
					for ( int i = 0, nCronEntries = 0; i < arrCRON_TEMP.Length; i++ )
					{
						if ( arrCRON_TEMP[i].IndexOf('-') >= 0 )
						{
							arrCRON_VALUE = arrCRON_TEMP[i].Split('-');
							if ( arrCRON_VALUE.Length >= 2 )
							{
								nCRON_VALUE_START = Sql.ToInteger(arrCRON_VALUE[0]);
								nCRON_VALUE_END   = Sql.ToInteger(arrCRON_VALUE[1]);
								if ( nCRON_VALUE_START >= 0 && nCRON_VALUE_START <= 6 && nCRON_VALUE_END >= 0 && nCRON_VALUE_END <= 6 )
								{
									if ( nCronEntries > 0 )
										sb.Append(L10n.Term("Schedulers.LBL_AND"));
									sb.Append(L10n.Term("Schedulers.LBL_FROM"));
									sb.Append(culture.DateTimeFormat.DayNames[nCRON_VALUE_START]);
									sb.Append(L10n.Term("Schedulers.LBL_RANGE"));
									sb.Append(culture.DateTimeFormat.DayNames[nCRON_VALUE_END]);
									nCronEntries++;
								}
							}
						}
						else
						{
							nCRON_VALUE = Sql.ToInteger(arrCRON_TEMP[i]);
							if ( nCRON_VALUE >= 0 && nCRON_VALUE <= 6 )
							{
								if ( nCronEntries > 0 )
									sb.Append(L10n.Term("Schedulers.LBL_AND"));
								sb.Append(culture.DateTimeFormat.DayNames[nCRON_VALUE]);
								nCronEntries++;
							}
						}
					}
				}
				return sb.ToString();
			}
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				return "<font class=error>" + ex.Message + "</font>";
			}
		}
		#endregion

		// =====================================================================================
		// RunJob
		// Dispatches a named scheduler job to the appropriate handler method.
		// 10/27/2008 Paul.  Pass the context instead of the Application so that more information
		//   will be available to the error handling.
		// .NET 10 Migration: Context parameter retained for signature compatibility; internally
		//   uses injected DI services (_memoryCache, _emailUtils, etc.) instead of Context.Application.
		//   The Context may be null when called from a background service with no active HTTP request.
		// =====================================================================================

		/// <summary>
		/// Executes a named scheduler job. All job implementations are preserved exactly from the source.
		/// </summary>
		/// <param name="Context">
		/// HTTP context passed for error reporting. May be null in background service context.
		/// In .NET 10, error logging uses IMemoryCache instead of Context.Application.
		/// </param>
		/// <param name="sJOB">
		/// The job function name (e.g. "function::BackupDatabase", "function::pruneDatabase").
		/// Must be prefixed with "function::" as stored in the vwSCHEDULERS view.
		/// </param>
		public void RunJob(HttpContext Context, string sJOB)
		{
			// .NET 10 Migration: DbProviderFactories.GetFactory(Context.Application) →
			// _dbProviderFactories.GetFactory(_memoryCache)
			DbProviderFactory dbf = _dbProviderFactories.GetFactory(_memoryCache);
			switch ( sJOB )
			{
				case "function::BackupDatabase":
				{
					// 01/28/2008 Paul.  Cannot perform a backup or restore operation within a transaction.
					// BACKUP DATABASE is terminating abnormally.
					using ( IDbConnection con = dbf.CreateConnection() )
					{
						con.Open();
						try
						{
							string sFILENAME = String.Empty;
							string sTYPE     = "FULL";
							using ( IDbCommand cmd = con.CreateCommand() )
							{
								cmd.CommandType = CommandType.StoredProcedure;
								cmd.CommandText = "spSqlBackupDatabase";
								// 02/09/2008 Paul.  A database backup can take a long time.  Don't timeout.
								cmd.CommandTimeout = 0;
								IDbDataParameter parFILENAME = Sql.AddParameter(cmd, "@FILENAME", sFILENAME  , 255);
								IDbDataParameter parTYPE     = Sql.AddParameter(cmd, "@TYPE"    , sTYPE      ,  20);
								parFILENAME.Direction = ParameterDirection.InputOutput;
								cmd.ExecuteNonQuery();
								sFILENAME = Sql.ToString(parFILENAME.Value);
							}
							// .NET 10 Migration: SplendidError.SystemMessage(Context, ...) →
							// SplendidError.SystemMessage(_memoryCache, ...)
							SplendidError.SystemMessage(_memoryCache, "Information", new StackTrace(true).GetFrame(0), "Database backup complete " + sFILENAME);
						}
						catch(Exception ex)
						{
							SplendidError.SystemMessage(_memoryCache, "Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
						}
					}
					break;
				}
				case "function::BackupTransactionLog":
				{
					// 01/28/2008 Paul.  Cannot perform a backup or restore operation within a transaction.
					// BACKUP DATABASE is terminating abnormally.
					using ( IDbConnection con = dbf.CreateConnection() )
					{
						con.Open();
						try
						{
							string sFILENAME = String.Empty;
							string sTYPE     = "LOG";
							using ( IDbCommand cmd = con.CreateCommand() )
							{
								cmd.CommandType = CommandType.StoredProcedure;
								cmd.CommandText = "spSqlBackupDatabase";
								// 02/09/2008 Paul.  A database backup can take a long time.  Don't timeout.
								cmd.CommandTimeout = 0;
								IDbDataParameter parFILENAME = Sql.AddParameter(cmd, "@FILENAME", sFILENAME  , 255);
								IDbDataParameter parTYPE     = Sql.AddParameter(cmd, "@TYPE"    , sTYPE      ,  20);
								parFILENAME.Direction = ParameterDirection.InputOutput;
								cmd.ExecuteNonQuery();
								sFILENAME = Sql.ToString(parFILENAME.Value);
							}
							SplendidError.SystemMessage(_memoryCache, "Information", new StackTrace(true).GetFrame(0), "Transaction Log backup complete " + sFILENAME);
						}
						catch(Exception ex)
						{
							SplendidError.SystemMessage(_memoryCache, "Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
						}
					}
					break;
				}
				case "function::runMassEmailCampaign":
				{
					// 12/30/2007 Paul.  Update the last run date before running so that the date marks the start of the run.
					// .NET 10 Migration: EmailUtils.SendQueued(Context, Guid.Empty, Guid.Empty, false) →
					// _emailUtils.SendQueued(Guid.Empty, Guid.Empty, false) (instance method, no Context param)
					_emailUtils.SendQueued(Guid.Empty, Guid.Empty, false);
					break;
				}
				case "function::pruneDatabase":
				{
					using ( IDbConnection con = dbf.CreateConnection() )
					{
						con.Open();
						// 10/07/2009 Paul.  We need to create our own global transaction ID to support auditing
						// and workflow on SQL Azure, PostgreSQL, Oracle, DB2 and MySQL.
						using ( IDbTransaction trn = Sql.BeginTransaction(con) )
						{
							try
							{
								// .NET 10 Migration: SqlProcs.spSqlPruneDatabase(trn) — static method preserved
								SqlProcs.spSqlPruneDatabase(trn);
								trn.Commit();
							}
							catch(Exception ex)
							{
								trn.Rollback();
								SplendidError.SystemMessage(_memoryCache, "Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
							}
						}
					}
					break;
				}
				// 02/26/2010 Paul.  Allow system log to be cleaned.
				case "function::CleanSystemLog":
				{
					using ( IDbConnection con = dbf.CreateConnection() )
					{
						con.Open();
						using ( IDbTransaction trn = Sql.BeginTransaction(con) )
						{
							try
							{
								// SqlProcs.spSYSTEM_LOG_Cleanup(trn);
								using ( IDbCommand cmd = con.CreateCommand() )
								{
									// 02/26/2010 Paul.  If the database is very old, then the first cleanup can take a long time.
									cmd.Transaction    = trn;
									cmd.CommandType    = CommandType.StoredProcedure;
									cmd.CommandText    = "spSYSTEM_LOG_Cleanup";
									cmd.CommandTimeout = 0;
									cmd.ExecuteNonQuery();
								}
								trn.Commit();
							}
							catch(Exception ex)
							{
								trn.Rollback();
								SplendidError.SystemMessage(_memoryCache, "Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
							}
						}
						// 09/22/2010 Paul.  We need to cleanup the WORKFLOW_EVENTS table on the Community Edition.
						using ( IDbTransaction trn = Sql.BeginTransaction(con) )
						{
							try
							{
								// SqlProcs.spWORKFLOW_EVENTS_ProcessAll(trn);
								using ( IDbCommand cmd = con.CreateCommand() )
								{
									cmd.Transaction    = trn;
									cmd.CommandType    = CommandType.StoredProcedure;
									cmd.CommandText    = "spWORKFLOW_EVENTS_ProcessAll";
									cmd.CommandTimeout = 0;
									cmd.ExecuteNonQuery();
								}
								trn.Commit();
							}
							catch(Exception ex)
							{
								trn.Rollback();
								SplendidError.SystemMessage(_memoryCache, "Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
							}
						}
					}
					break;
				}
				// 03/27/2010 Paul.  Allow system log to be cleaned.
				case "function::CleanSystemSyncLog":
				{
					using ( IDbConnection con = dbf.CreateConnection() )
					{
						con.Open();
						using ( IDbTransaction trn = Sql.BeginTransaction(con) )
						{
							try
							{
								// SqlProcs.spSYSTEM_SYNC_LOG_Cleanup(trn);
								using ( IDbCommand cmd = con.CreateCommand() )
								{
									// 02/26/2010 Paul.  If the database is very old, then the first cleanup can take a long time.
									cmd.Transaction    = trn;
									cmd.CommandType    = CommandType.StoredProcedure;
									cmd.CommandText    = "spSYSTEM_SYNC_LOG_Cleanup";
									cmd.CommandTimeout = 0;
									cmd.ExecuteNonQuery();
								}
								trn.Commit();
							}
							catch(Exception ex)
							{
								trn.Rollback();
								SplendidError.SystemMessage(_memoryCache, "Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
							}
						}
					}
					break;
				}
				case "function::pollMonitoredInboxes":
				{
					// .NET 10 Migration: EmailUtils.CheckMonitored(Context, Guid.Empty) →
					// _emailUtils.CheckMonitored(Guid.Empty) (instance method, no Context param)
					_emailUtils.CheckMonitored(Guid.Empty);
					break;
				}
				case "function::pollMonitoredInboxesForBouncedCampaignEmails":
				{
					// .NET 10 Migration: EmailUtils.CheckBounced(Context, Guid.Empty) →
					// _emailUtils.CheckBounced(Guid.Empty) (instance method, no Context param)
					_emailUtils.CheckBounced(Guid.Empty);
					break;
				}
				case "function::CheckVersion":
				{
					try
					{
						// .NET 10 Migration: Utils.CheckVersion(Context.Application) →
						// _utils.CheckVersion() (instance method, DI-injected services used internally)
						DataTable dtVersions = _utils.CheckVersion();

						DataView vwVersions = dtVersions.DefaultView;
						vwVersions.RowFilter = "New = '1'";
						if ( vwVersions.Count > 0 )
						{
							// .NET 10 Migration: Context.Application["available_version"] = ... →
							// _memoryCache.Set("available_version", ...)
							_memoryCache.Set("available_version"            , Sql.ToString(vwVersions[0]["Build"      ]));
							_memoryCache.Set("available_version_description", Sql.ToString(vwVersions[0]["Description"]));
						}
					}
					catch(Exception ex)
					{
						SplendidError.SystemMessage(_memoryCache, "Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
					}
					break;
				}
				case "function::pollOutboundEmails":
				{
					// 05/15/2008 Paul.  Check for outbound emails.
					// .NET 10 Migration: EmailUtils.SendOutbound(Context) →
					// _emailUtils.SendOutbound() (instance method, no Context param)
					_emailUtils.SendOutbound();
					break;
				}
				case "function::OfflineClientSync":
				{
					// .NET 10 Migration: Context.Application["SystemSync.LastBackgroundSync"] = DateTime.Now →
					// _memoryCache.Set("SystemSync.LastBackgroundSync", DateTime.Now)
					_memoryCache.Set("SystemSync.LastBackgroundSync", DateTime.Now);
					// 05/22/2011 Paul.  We need to catch any exceptions as a failure in a thread will abort the entire session.
					try
					{
						// 11/21/2009 Paul.  This is an Offline Client scheduled task. It cannot be configured
						// on the server as the SCHEDULES table is not sync'd.
						// .NET 10 Migration: SyncUtils.Retrieve/Sync/Send remain static; pass current HttpContext
						// from IHttpContextAccessor (may be null in background service context — acceptable since
						// OfflineClientSync is a client-side Offline Client feature, not a server-side job).
						HttpContext httpContext = _httpContextAccessor?.HttpContext;
						SyncUtils.Retrieve(httpContext, false, false);
						SyncUtils.Sync    (httpContext, false, false);
						SyncUtils.Send    (httpContext);
					}
					catch(Exception ex)
					{
						// .NET 10 Migration: SyncError.SystemMessage → SplendidError.SystemMessage
						// SyncError is not present in the migrated codebase; consolidated to SplendidError.
						SplendidError.SystemMessage(_memoryCache, "Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
					}
					break;
				}
				// 04/10/2018 Paul.  ModulesArchiveRules module to Professional.
				case "function::RunAllArchiveRules":
				{
					// 07/10/2018 Paul.  Don't run normal archive rules if external archive is enabled.
					// 10/27/2022 Paul.  Just now adding to Community.
					// .NET 10 Migration: Context.Application["ArchiveConnectionString"] →
					// _memoryCache.Get<object>("ArchiveConnectionString")
					if ( Sql.IsEmptyString(_memoryCache.Get<object>("ArchiveConnectionString")) )
					{
						using ( IDbConnection con = dbf.CreateConnection() )
						{
							con.Open();
							using ( IDbTransaction trn = Sql.BeginTransaction(con) )
							{
								try
								{
									using ( IDbCommand cmd = con.CreateCommand() )
									{
										cmd.Transaction    = trn;
										cmd.CommandType    = CommandType.StoredProcedure;
										cmd.CommandText    = "spMODULES_ARCHIVE_RULES_RunAll";
										cmd.CommandTimeout = 0;
										cmd.ExecuteNonQuery();
									}
									trn.Commit();
								}
								catch(Exception ex)
								{
									trn.Rollback();
									SplendidError.SystemMessage(_memoryCache, "Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
								}
							}
						}
					}
					else
					{
						SplendidError.SystemMessage(_memoryCache, "Error", new StackTrace(true).GetFrame(0), "SchedulerUtils.RunJobs: Rules cannot be run manually when External Archive is enabled.");
					}
					break;
				}
// 11/04/2021 Paul.  ArchiveExternalDB is not used in ReactOnlyUI.
#if !ReactOnlyUI
				// 04/10/2018 Paul.  Run External Archive.
				case "function::RunExternalArchive":
				{
					// .NET 10 Migration: ArchiveExternalDB.RunArchive was static in .NET Framework 4.8.
					// In .NET 10, ArchiveExternalDB is a DI-injected instance class.
					// Capture the injected instance in a local to avoid a captured-variable-in-lambda issue
					// with the outer 'Context' parameter (which may be null in background service context).
					// Use a Thread to preserve the original asynchronous non-blocking execution pattern.
					ArchiveExternalDB archiveDb = _archiveExternalDB;
					if ( archiveDb != null )
					{
						System.Threading.Thread t = new System.Threading.Thread(archiveDb.RunArchive);
						t.Start(Context);
					}
					break;
				}
#endif
			}
		}

		// =====================================================================================
		// OnTimer — Main scheduler timer callback
		// 10/27/2008 Paul.  Pass the context instead of the Application so that more information
		//   will be available to the error handling.
		// .NET 10 Migration:
		//   - Object sender was previously cast as HttpContext Context = sender as HttpContext;
		//   - In .NET 10, the sender from IHostedService is not an HttpContext.
		//   - The sender parameter is preserved for schema compliance; internally uses DI services.
		//   - HttpContext is obtained from IHttpContextAccessor for logging and passing to services.
		//   - A no-arg overload OnTimer() is provided for SchedulerHostedService callers.
		// =====================================================================================

		/// <summary>
		/// Main scheduler timer callback — schema-compliant Object-parameter overload.
		/// The sender argument is not used in the .NET 10 migration; DI services are used instead.
		/// </summary>
		/// <param name="sender">
		/// Preserved for schema compliance. In the original, this was an HttpContext cast.
		/// In .NET 10, this may be null or any object; the actual context comes from IHttpContextAccessor.
		/// </param>
		public void OnTimer(Object sender)
		{
			// .NET 10 Migration: HttpContext Context = sender as HttpContext; → not used
			// The DI-injected services are used instead of the HttpContext-as-sender pattern.
			// Delegate to the parameterless overload for all logic.
			OnTimerCore();
		}

		/// <summary>
		/// Main scheduler timer callback — no-arg overload for SchedulerHostedService.
		/// Called by SchedulerHostedService on the configured SCHEDULER_INTERVAL_MS interval.
		/// </summary>
		public void OnTimer()
		{
			OnTimerCore();
		}

		/// <summary>
		/// Core implementation of the main scheduler timer callback.
		/// Processes vwSYSTEM_EVENTS cache invalidation, workflow jobs, machine election,
		/// and all configured scheduler jobs from vwSCHEDULERS_Run.
		/// </summary>
		private void OnTimerCore()
		{
			// 12/22/2007 Paul.  In case the timer takes a long time, only allow one timer event to be processed.
			if ( !bInsideTimer )
			{
				bInsideTimer = true;
				try
				{
					// .NET 10 Migration: DbProviderFactories.GetFactory(Context.Application) →
					// _dbProviderFactories.GetFactory(_memoryCache)
					DbProviderFactory dbf = _dbProviderFactories.GetFactory(_memoryCache);
					using ( IDbConnection con = dbf.CreateConnection() )
					{
						con.Open();
						string sSQL;
						// .NET 10 Migration: Context.Application["SYSTEM_EVENTS.MaxDate"] →
						// _memoryCache.Get<object>("SYSTEM_EVENTS.MaxDate")
						DateTime dtLastUpdate = Sql.ToDateTime(_memoryCache.Get<object>("SYSTEM_EVENTS.MaxDate"));
						if ( dtLastUpdate == DateTime.MinValue )
						{
							dtLastUpdate = DateTime.Now;
							// 02/24/2009 Paul.  Update cache variable so that we will know when the last update ran.
							// .NET 10 Migration: Context.Application["SYSTEM_EVENTS.MaxDate"] = dtLastUpdate →
							// _memoryCache.Set("SYSTEM_EVENTS.MaxDate", dtLastUpdate)
							_memoryCache.Set("SYSTEM_EVENTS.MaxDate", dtLastUpdate);
						}

						// 08/20/2008 Paul.  We reload the system data if a system table or cached table changes.
						// The primary reason we do this is to support a load-balanced system where changes
						// on one server need to be replicated to the cache of the other servers.
						sSQL = "select TABLE_NAME                  " + ControlChars.CrLf
						     + "  from vwSYSTEM_EVENTS             " + ControlChars.CrLf
						     + " where DATE_ENTERED > @DATE_ENTERED" + ControlChars.CrLf
						     + " group by TABLE_NAME               " + ControlChars.CrLf
						     + " order by TABLE_NAME               " + ControlChars.CrLf;
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							cmd.CommandText = sSQL;
							Sql.AddParameter(cmd, "@DATE_ENTERED", dtLastUpdate);
							using ( DataTable dt = new DataTable() )
							{
								using ( DbDataAdapter da = dbf.CreateDataAdapter() )
								{
									((IDbDataAdapter)da).SelectCommand = cmd;
									da.Fill(dt);
									if ( dt.Rows.Count > 0 )
									{
										cmd.Parameters.Clear();
										sSQL = "select max(DATE_ENTERED)" + ControlChars.CrLf
										     + "  from vwSYSTEM_EVENTS  " + ControlChars.CrLf;
										cmd.CommandText = sSQL;
										dtLastUpdate = Sql.ToDateTime(cmd.ExecuteScalar());
										// .NET 10 Migration: Context.Application["SYSTEM_EVENTS.MaxDate"] →
										// _memoryCache.Set("SYSTEM_EVENTS.MaxDate", ...)
										_memoryCache.Set("SYSTEM_EVENTS.MaxDate", dtLastUpdate);

										StringBuilder sbTables = new StringBuilder();
										foreach ( DataRow row in dt.Rows )
										{
											if ( sbTables.Length > 0 )
												sbTables.Append(", ");
											sbTables.Append(Sql.ToString(row["TABLE_NAME"]));
										}
										// 03/02/2009 Paul.  We must pass the context to the error handler.
										// .NET 10 Migration: SplendidError.SystemMessage(Context, ...) →
										// SplendidError.SystemMessage(_memoryCache, ...)
										SplendidError.SystemMessage(_memoryCache, "Warning", new StackTrace(true).GetFrame(0), "System Events: " + sbTables.ToString());
										SplendidError.SystemMessage(_memoryCache, "Warning", new StackTrace(true).GetFrame(0), "System Events Last Update on " + dtLastUpdate.ToString());

										foreach ( DataRow row in dt.Rows )
										{
											string sTABLE_NAME = Sql.ToString(row["TABLE_NAME"]);
											// .NET 10 Migration: SplendidCache.ClearTable(sTABLE_NAME) →
											// _splendidCache.ClearTable(sTABLE_NAME)
											_splendidCache.ClearTable(sTABLE_NAME);
											// 10/26/2008 Paul.  IIS7 Integrated Pipeline does not allow HttpContext access inside Application_Start.
											// .NET 10 Migration: SplendidInit.InitTerminology(Context) →
											// _splendidInit.InitTerminology() (instance method, no Context param)
											if ( sTABLE_NAME.StartsWith("TERMINOLOGY") )
												_splendidInit.InitTerminology();
											else if ( sTABLE_NAME == "MODULES" || sTABLE_NAME.StartsWith("ACL_") )
												_splendidInit.InitModuleACL();
											else if ( sTABLE_NAME == "CONFIG" )
												_splendidInit.InitConfig();
											else if ( sTABLE_NAME == "TIMEZONES" )
												_splendidInit.InitTimeZones();
											else if ( sTABLE_NAME == "CURRENCIES" )
												_splendidInit.InitCurrencies();
										}
									}
								}
							}
						}
						// 10/13/2008 Paul.  Clear out old system events so that future queries are fast.
						// 10/07/2009 Paul.  We need to create our own global transaction ID to support auditing
						// and workflow on SQL Azure, PostgreSQL, Oracle, DB2 and MySQL.
						using ( IDbTransaction trn = Sql.BeginTransaction(con) )
						{
							try
							{
								using ( IDbCommand cmd = con.CreateCommand() )
								{
									cmd.Transaction = trn;
									cmd.CommandType = CommandType.StoredProcedure;
									cmd.CommandText = "spSYSTEM_EVENTS_ProcessAll";
									cmd.ExecuteNonQuery();
								}
								trn.Commit();
							}
							catch(Exception ex)
							{
								trn.Rollback();
								SplendidError.SystemMessage(_memoryCache, "Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
							}
						}
					}

					// 12/30/2007 Paul.  Workflow events always get processed.
					// 07/26/2008 Paul.  Provide a way to disable workflow.
					// .NET 10 Migration: Sql.ToBoolean(Context.Application["CONFIG.enable_workflow"]) →
					// Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.enable_workflow"))
					bool bEnableWorkflow = Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.enable_workflow"));
					if ( bEnableWorkflow )
					{
						// .NET 10 Migration: WorkflowUtils.Process(Context) →
						// _workflowUtils.Process(httpContext) where httpContext from IHttpContextAccessor
						// (may be null in background service; WorkflowUtils.Process accepts null Context)
						HttpContext httpContext = _httpContextAccessor?.HttpContext;
						_workflowUtils.Process(httpContext);
					}

					// 01/27/2009 Paul.  If multiple apps connect to the same database, make sure that only one is the job server.
					// This is primarily for load-balanced sites.
					// .NET 10 Migration: Sql.ToInteger(Context.Application["SplendidJobServer"]) →
					// Sql.ToInteger(_memoryCache.Get<object>("SplendidJobServer"))
					int nSplendidJobServer = Sql.ToInteger(_memoryCache.Get<object>("SplendidJobServer"));
					if ( nSplendidJobServer == 0 )
					{
						// .NET 10 Migration: System.Configuration.ConfigurationManager.AppSettings["SplendidJobServer"] →
						// _configuration["SplendidJobServer"] (AAP env var: SPLENDID_JOB_SERVER maps to Scheduler:JobServer)
						string sSplendidJobServer = _configuration["SplendidJobServer"] ?? _configuration["Scheduler:JobServer"] ?? String.Empty;
						// 09/17/2009 Paul.  If we are running in Azure, then assume that this is the only instance.
						string sMachineName = sSplendidJobServer;
						try
						{
							// 09/17/2009 Paul.  Azure does not support MachineName.  Just ignore the error.
							sMachineName = System.Environment.MachineName;
						}
						catch
						{
						}
						if ( Sql.IsEmptyString(sSplendidJobServer) || String.Compare(sMachineName, sSplendidJobServer, true) == 0 )
						{
							nSplendidJobServer = 1;
							SplendidError.SystemMessage(_memoryCache, "Warning", new StackTrace(true).GetFrame(0), sMachineName + " is a Splendid Job Server.");
						}
						else
						{
							nSplendidJobServer = -1;
							SplendidError.SystemMessage(_memoryCache, "Warning", new StackTrace(true).GetFrame(0), sMachineName + " is not a Splendid Job Server.");
						}
						// .NET 10 Migration: Context.Application["SplendidJobServer"] = nSplendidJobServer →
						// _memoryCache.Set("SplendidJobServer", nSplendidJobServer)
						_memoryCache.Set("SplendidJobServer", nSplendidJobServer);
					}
					if ( nSplendidJobServer > 0 )
					{
						using ( DataTable dt = new DataTable() )
						{
							// .NET 10 Migration: dbf declared above at the top of the try block — reuse it here.
							using ( IDbConnection con = dbf.CreateConnection() )
							{
								con.Open();
								string sSQL;
								sSQL = "select *               " + ControlChars.CrLf
								     + "  from vwSCHEDULERS_Run" + ControlChars.CrLf
								     + " order by NEXT_RUN     " + ControlChars.CrLf;
								using ( IDbCommand cmd = con.CreateCommand() )
								{
									cmd.CommandText = sSQL;
									// 01/01/2008 Paul.  The scheduler query should always be very fast.
									// In the off chance that there is a problem, abort after 15 seconds.
									cmd.CommandTimeout = 15;

									using ( DbDataAdapter da = dbf.CreateDataAdapter() )
									{
										((IDbDataAdapter)da).SelectCommand = cmd;
										da.Fill(dt);
									}
								}
							}
							// 05/14/2009 Paul.  Provide a way to track scheduler events.
							// .NET 10 Migration: Context.Application["CONFIG.suppress_scheduler_warning"] →
							// _memoryCache.Get<object>("CONFIG.suppress_scheduler_warning")
							if ( !Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.suppress_scheduler_warning")) )
							{
								SplendidError.SystemMessage(_memoryCache, "Warning", new StackTrace(true).GetFrame(0), "Scheduler Jobs to run: " + dt.Rows.Count.ToString());
							}
							// 01/13/2008 Paul.  Loop outside the connection so that only one connection will be used.
							foreach ( DataRow row in dt.Rows )
							{
								Guid     gID        = Sql.ToGuid    (row["ID"      ]);
								string   sJOB       = Sql.ToString  (row["JOB"     ]);
								// 01/31/2008 Paul.  Next run becomes last run.
								DateTime dtLAST_RUN = Sql.ToDateTime(row["NEXT_RUN"]);
								// 11/08/2022 Paul.  Separate Archive timer.
								// .NET 10 Migration: Context.Application["CONFIG.Archive.SeparateTimer"] →
								// _memoryCache.Get<object>("CONFIG.Archive.SeparateTimer")
								if ( Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.Archive.SeparateTimer")) )
								{
									if ( sJOB == "function::RunAllArchiveRules" || sJOB == "function::RunExternalArchive" )
									{
										break;
									}
								}
								// 11/02/2022 Paul.  Keep track of last job for verbose logging.
								sLastJob = sJOB;
								try
								{
									if ( !Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.suppress_scheduler_warning")) )
									{
										SplendidError.SystemMessage(_memoryCache, "Warning", new StackTrace(true).GetFrame(0), "Scheduler Job Start: " + sJOB + " at " + dtLAST_RUN.ToString());
									}
									// Pass null as Context since this is a background service with no active HTTP request.
									// RunJob uses DI-injected services internally so Context is only needed for legacy compat.
									RunJob(null, sJOB);
									if ( !Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.suppress_scheduler_warning")) )
									{
										SplendidError.SystemMessage(_memoryCache, "Warning", new StackTrace(true).GetFrame(0), "Scheduler Job End: " + sJOB + " at " + DateTime.Now.ToString());
									}
								}
								finally
								{
									using ( IDbConnection con = dbf.CreateConnection() )
									{
										con.Open();
										// 10/07/2009 Paul.  We need to create our own global transaction ID to support auditing
										// and workflow on SQL Azure, PostgreSQL, Oracle, DB2 and MySQL.
										using ( IDbTransaction trn = Sql.BeginTransaction(con) )
										{
											try
											{
												// 01/12/2008 Paul.  Make sure the Last Run value is updated after the operation.
												// .NET 10 Migration: SqlProcs.spSCHEDULERS_UpdateLastRun static method preserved
												SqlProcs.spSCHEDULERS_UpdateLastRun(gID, dtLAST_RUN, trn);
												trn.Commit();
											}
											catch(Exception ex)
											{
												trn.Rollback();
												SplendidError.SystemMessage(_memoryCache, "Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
											}
										}
									}
								}
							}
						}
					}
				}
				catch(Exception ex)
				{
					SplendidError.SystemMessage(_memoryCache, "Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
				}
				finally
				{
					bInsideTimer = false;
				}
			}
			// 11/02/2022 Paul.  Keep track of last job for verbose logging.
			// .NET 10 Migration: Context.Application["CONFIG.Scheduler.Verbose"] →
			// _memoryCache.Get<object>("CONFIG.Scheduler.Verbose")
			else if ( !Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.Scheduler.Verbose")) )
			{
				SplendidError.SystemMessage(_memoryCache, "Warning", new StackTrace(true).GetFrame(0), "Scheduler Busy: " + sLastJob);
			}
		}

		// =====================================================================================
		// OnArchiveTimer — Separate archive timer callback
		// 11/08/2022 Paul.  Separate Archive timer.
		// .NET 10 Migration: Same as OnTimer — Object sender is preserved for schema compliance;
		//   DI services are used internally. A no-arg overload is added for ArchiveHostedService.
		// =====================================================================================

		/// <summary>
		/// Archive timer callback — schema-compliant Object-parameter overload.
		/// Processes only RunAllArchiveRules and RunExternalArchive scheduler jobs.
		/// </summary>
		/// <param name="sender">
		/// Preserved for schema compliance. Not used in .NET 10 migration; DI services are used instead.
		/// </param>
		public void OnArchiveTimer(Object sender)
		{
			// .NET 10 Migration: sender as HttpContext not used; DI services replace all static access.
			OnArchiveTimerCore();
		}

		/// <summary>
		/// Archive timer callback — no-arg overload for ArchiveHostedService.
		/// Called by ArchiveHostedService on the configured ARCHIVE_INTERVAL_MS interval.
		/// </summary>
		public void OnArchiveTimer()
		{
			OnArchiveTimerCore();
		}

		/// <summary>
		/// Core implementation of the archive timer callback.
		/// Preserves machine election logic and archive job execution from the original.
		/// </summary>
		private void OnArchiveTimerCore()
		{
			if ( !bInsideArchiveTimer )
			{
				bInsideArchiveTimer = true;
				try
				{
					// .NET 10 Migration: Sql.ToInteger(Context.Application["SplendidJobServer"]) →
					// Sql.ToInteger(_memoryCache.Get<object>("SplendidJobServer"))
					int nSplendidJobServer = Sql.ToInteger(_memoryCache.Get<object>("SplendidJobServer"));
					if ( nSplendidJobServer == 0 )
					{
						// .NET 10 Migration: ConfigurationManager.AppSettings["SplendidJobServer"] →
						// _configuration["SplendidJobServer"] (also checks Scheduler:JobServer)
						string sSplendidJobServer = _configuration["SplendidJobServer"] ?? _configuration["Scheduler:JobServer"] ?? String.Empty;
						string sMachineName = sSplendidJobServer;
						try
						{
							// 09/17/2009 Paul.  Azure does not support MachineName.  Just ignore the error.
							sMachineName = System.Environment.MachineName;
						}
						catch
						{
						}
						if ( Sql.IsEmptyString(sSplendidJobServer) || String.Compare(sMachineName, sSplendidJobServer, true) == 0 )
						{
							nSplendidJobServer = 1;
							SplendidError.SystemMessage(_memoryCache, "Warning", new StackTrace(true).GetFrame(0), sMachineName + " is a Splendid Job Server.");
						}
						else
						{
							nSplendidJobServer = -1;
							SplendidError.SystemMessage(_memoryCache, "Warning", new StackTrace(true).GetFrame(0), sMachineName + " is not a Splendid Job Server.");
						}
						// .NET 10 Migration: Context.Application["SplendidJobServer"] = → _memoryCache.Set(...)
						_memoryCache.Set("SplendidJobServer", nSplendidJobServer);
					}
					if ( nSplendidJobServer > 0 )
					{
						using ( DataTable dt = new DataTable() )
						{
							DbProviderFactory dbf = _dbProviderFactories.GetFactory(_memoryCache);
							using ( IDbConnection con = dbf.CreateConnection() )
							{
								con.Open();
								string sSQL;
								sSQL = "select *               " + ControlChars.CrLf
								     + "  from vwSCHEDULERS_Run" + ControlChars.CrLf
								     + " where JOB in ('function::RunAllArchiveRules', 'function::RunExternalArchive')" + ControlChars.CrLf
								     + " order by NEXT_RUN     " + ControlChars.CrLf;
								using ( IDbCommand cmd = con.CreateCommand() )
								{
									cmd.CommandText = sSQL;
									cmd.CommandTimeout = 15;
									using ( DbDataAdapter da = dbf.CreateDataAdapter() )
									{
										((IDbDataAdapter)da).SelectCommand = cmd;
										da.Fill(dt);
									}
								}
							}
							// .NET 10 Migration: Context.Application["CONFIG.suppress_scheduler_warning"] →
							// _memoryCache.Get<object>("CONFIG.suppress_scheduler_warning")
							if ( !Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.suppress_scheduler_warning")) )
							{
								SplendidError.SystemMessage(_memoryCache, "Warning", new StackTrace(true).GetFrame(0), "Archive Jobs to run: " + dt.Rows.Count.ToString());
							}
							foreach ( DataRow row in dt.Rows )
							{
								Guid     gID        = Sql.ToGuid    (row["ID"      ]);
								string   sJOB       = Sql.ToString  (row["JOB"     ]);
								DateTime dtLAST_RUN = Sql.ToDateTime(row["NEXT_RUN"]);
								// 11/02/2022 Paul.  Keep track of last job for verbose logging.
								sLastJob = sJOB;
								try
								{
									if ( !Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.suppress_scheduler_warning")) )
									{
										SplendidError.SystemMessage(_memoryCache, "Warning", new StackTrace(true).GetFrame(0), "Archive Job Start: " + sJOB + " at " + dtLAST_RUN.ToString());
									}
									// Pass null as Context since this is a background service with no active HTTP request.
									RunJob(null, sJOB);
									if ( !Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.suppress_scheduler_warning")) )
									{
										SplendidError.SystemMessage(_memoryCache, "Warning", new StackTrace(true).GetFrame(0), "Archive Job End: " + sJOB + " at " + DateTime.Now.ToString());
									}
								}
								finally
								{
									DbProviderFactory dbfInner = _dbProviderFactories.GetFactory(_memoryCache);
									using ( IDbConnection con = dbfInner.CreateConnection() )
									{
										con.Open();
										using ( IDbTransaction trn = Sql.BeginTransaction(con) )
										{
											try
											{
												SqlProcs.spSCHEDULERS_UpdateLastRun(gID, dtLAST_RUN, trn);
												trn.Commit();
											}
											catch(Exception ex)
											{
												trn.Rollback();
												SplendidError.SystemMessage(_memoryCache, "Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
											}
										}
									}
								}
							}
						}
					}
				}
				catch(Exception ex)
				{
					SplendidError.SystemMessage(_memoryCache, "Error", new StackTrace(true).GetFrame(0), Utils.ExpandException(ex));
				}
				finally
				{
					bInsideArchiveTimer = false;
				}
			}
			// 11/02/2022 Paul.  Keep track of last job for verbose logging.
			// .NET 10 Migration: Context.Application["CONFIG.Scheduler.Verbose"] →
			// _memoryCache.Get<object>("CONFIG.Scheduler.Verbose")
			else if ( !Sql.ToBoolean(_memoryCache.Get<object>("CONFIG.Scheduler.Verbose")) )
			{
				SplendidError.SystemMessage(_memoryCache, "Warning", new StackTrace(true).GetFrame(0), "Archive Jobs Busy: " + sLastJob);
			}
		}

	}
}
