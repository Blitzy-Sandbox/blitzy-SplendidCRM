/**********************************************************************************************************************
 * SplendidCRM is a Customer Relationship Management program created by SplendidCRM Software, Inc. 
 * Copyright (C) 2005-2023 SplendidCRM Software, Inc. All rights reserved.
 *********************************************************************************************************************/
#nullable disable
using System;
using System.Data;
using System.Threading;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;

namespace SplendidCRM
{
	/// <summary>
	/// Application bootstrap orchestrator.
	/// Migrated from SplendidCRM/_code/SplendidInit.cs (~900 lines) for .NET 10 ASP.NET Core.
	/// Replaces Application[] lock with thread-safe singleton initialization using SemaphoreSlim.
	/// </summary>
	public class SplendidInit
	{
		private readonly IMemoryCache        _memoryCache       ;
		private readonly IConfiguration      _configuration     ;
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly DbProviderFactories _dbProviderFactories;
		private readonly SplendidCache       _splendidCache     ;
		private readonly Security            _security          ;
		private readonly SplendidError       _splendidError     ;

		private static readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);
		private static bool _bInitialized = false;

		public SplendidInit(
			IMemoryCache memoryCache,
			IConfiguration configuration,
			IHttpContextAccessor httpContextAccessor,
			DbProviderFactories dbProviderFactories,
			SplendidCache splendidCache,
			Security security,
			SplendidError splendidError)
		{
			_memoryCache        = memoryCache       ;
			_configuration      = configuration     ;
			_httpContextAccessor = httpContextAccessor;
			_dbProviderFactories = dbProviderFactories;
			_splendidCache      = splendidCache     ;
			_security           = security          ;
			_splendidError      = splendidError     ;
		}

		/// <summary>
		/// One-time application initialization. Thread-safe via SemaphoreSlim.
		/// Replaces Application_BeginRequest's Application.Count == 0 check from Global.asax.cs.
		/// </summary>
		public void InitApp()
		{
			if (_bInitialized)
				return;
			_initLock.Wait();
			try
			{
				if (_bInitialized)
					return;
				try
				{
					string sConnectionString = _dbProviderFactories.ConnectionString;
					if (!Sql.IsEmptyString(sConnectionString))
					{
						_splendidCache.LoadConfig();
						_splendidCache.LoadTerminology(SplendidDefaults.Culture());
						SplendidError.SystemError(new StackTrace(true).GetFrame(0), "SplendidInit.InitApp: Application initialized successfully.");
					}
					else
					{
						SplendidError.SystemError(new StackTrace(true).GetFrame(0), "SplendidInit.InitApp: Connection string is empty. Application is in limited mode.");
					}
				}
				catch (Exception ex)
				{
					SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
				}
				_bInitialized = true;
			}
			finally
			{
				_initLock.Release();
			}
		}

		/// <summary>
		/// Initializes a user session with default settings.
		/// Replaces Global.asax.cs Session_Start initialization.
		/// </summary>
		public void InitSession()
		{
			var session = _httpContextAccessor?.HttpContext?.Session;
			if (session == null)
				return;
			try
			{
				string sDefaultCulture  = _splendidCache.Config("default_language" );
				string sDefaultTheme    = _splendidCache.Config("default_theme"    );
				string sDefaultDateFmt  = _splendidCache.Config("default_date_format");
				string sDefaultTimeFmt  = _splendidCache.Config("default_time_format");
				if (Sql.IsEmptyString(sDefaultCulture )) sDefaultCulture  = SplendidDefaults.Culture();
				if (Sql.IsEmptyString(sDefaultTheme   )) sDefaultTheme    = SplendidDefaults.Theme();
				if (Sql.IsEmptyString(sDefaultDateFmt )) sDefaultDateFmt  = SplendidDefaults.DateFormat();
				if (Sql.IsEmptyString(sDefaultTimeFmt )) sDefaultTimeFmt  = SplendidDefaults.TimeFormat();
				session.SetString("USER_SETTINGS_CULTURE"   , sDefaultCulture );
				session.SetString("USER_SETTINGS_THEME"     , sDefaultTheme   );
				session.SetString("USER_SETTINGS_DATEFORMAT", sDefaultDateFmt );
				session.SetString("USER_SETTINGS_TIMEFORMAT", sDefaultTimeFmt );
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
		}

		/// <summary>
		/// Logs in a user by setting all session variables.
		/// </summary>
		public void LoginUser(DataRow rowUser)
		{
			if (rowUser == null)
				return;
			_security.USER_ID     = Sql.ToGuid   (rowUser["ID"        ]);
			_security.USER_NAME   = Sql.ToString  (rowUser["USER_NAME" ]);
			_security.FULL_NAME   = Sql.ToString  (rowUser["FULL_NAME" ]);
			_security.TEAM_ID     = Sql.ToGuid   (rowUser["TEAM_ID"   ]);
			_security.IS_ADMIN    = Sql.ToBoolean (rowUser["IS_ADMIN"  ]);
		}

		/// <summary>
		/// Application shutdown cleanup.
		/// Replaces Global.asax.cs Application_End.
		/// </summary>
		public static void StopApp()
		{
			_bInitialized = false;
		}
	}
}
