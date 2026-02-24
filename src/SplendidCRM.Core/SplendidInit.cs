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
// .NET 10 Migration: SplendidCRM/_code/SplendidInit.cs → src/SplendidCRM.Core/SplendidInit.cs
// Changes applied:
//   - REMOVED: using System.Web; using System.Web.SessionState; using System.CodeDom.Compiler;
//   - ADDED:   using Microsoft.AspNetCore.Http; using Microsoft.Extensions.Caching.Memory; using Microsoft.Extensions.Configuration;
//   - REMOVED: HttpApplicationState Application → replaced by IMemoryCache (_memoryCache)
//   - REMOVED: HttpContext.Current → replaced by IHttpContextAccessor (_httpContextAccessor)
//   - REMOVED: HttpSessionState Session → replaced by ISession via IHttpContextAccessor
//   - REMOVED: HttpRuntime.Cache → replaced by IMemoryCache
//   - REMOVED: ConfigurationManager.AppSettings → replaced by IConfiguration
//   - REMOVED: Application.Lock()/UnLock() → replaced by SemaphoreSlim(1,1) reentrancy guard
//   - REMOVED: Application.Clear() → removed; SemaphoreSlim + _bInitialized handles reinit
//   - REPLACED: All static methods → instance methods consuming DI-injected services
//   - REPLACED: Security.* static property access → _security.* instance property access
//   - REPLACED: Security.SetModuleAccess(Application,...) → _security.SetModuleAccess(...)
//   - REPLACED: DbProviderFactories.GetFactory(Application) → _dbProviderFactories.GetFactory(_memoryCache)
//   - REPLACED: L10N.SetTerm(Application,...) → L10N.SetTerm(_memoryCache,...)
//   - REPLACED: Currency(Application,...) → Currency(_memoryCache,...)
//   - REPLACED: OrderUtils.GetCurrencyConversionRate(Application,...) → _orderUtils.GetCurrencyConversionRate(_memoryCache,...)
//   - REPLACED: Crm.Password.ExpirationDays(Application) → Crm.Password.ExpirationDays(_memoryCache)
//   - REPLACED: Context.Request.Browser.Browser/.IsMobileDevice → User-Agent parsing (Browser object unavailable in ASP.NET Core)
//   - REPLACED: Server.GetLastError() → IExceptionHandlerFeature (in Application_OnError)
//   - REPLACED: Server.UrlEncode() → Uri.EscapeDataString()
//   - REPLACED: Context.Request.UserHostName → context.Connection.RemoteIpAddress
//   - REPLACED: Context.Request.Url.Host → context.Request.Host.Host
//   - REPLACED: Context.Request.AppRelativeCurrentExecutionFilePath → context.Request.Path.Value
//   - REPLACED: Session["key"] = value → session.SetString("key", value.ToString()) [strings only for distributed session]
//   - REPLACED: Session["key"] (object) → session.GetString("key") via Sql.ToType() helpers
//   - REPLACED: Session.SessionID → session.Id
//   - PRESERVED: namespace SplendidCRM, static bUseSQLServerToken, static bEnableACLFieldSecurity
//   - PRESERVED: All SQL queries, stored procedure calls, business logic
//   - NOTE: ReactOnlyUI preprocessor directive → treated as #else (all modules Exist = true)
//   - NOTE: Utils.IsOfflineClient → not applicable in ASP.NET Core; treated as false
#nullable disable
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace SplendidCRM
{
	/// <summary>
	/// Application bootstrap orchestrator.
	/// Migrated from SplendidCRM/_code/SplendidInit.cs (~2443 lines) for .NET 10 ASP.NET Core.
	///
	/// Replaces:
	///   - Application.Lock()/UnLock() → SemaphoreSlim(1,1) reentrancy guard
	///   - HttpContext.Current           → IHttpContextAccessor DI injection
	///   - HttpApplicationState          → IMemoryCache DI injection
	///   - HttpSessionState              → ISession (via IHttpContextAccessor, distributed session compatible)
	///   - HttpRuntime.Cache             → IMemoryCache
	///   - ConfigurationManager          → IConfiguration
	///
	/// Register this class as a SCOPED service in the DI container so that each request
	/// gets the current IHttpContextAccessor-bound session data.
	/// </summary>
	public class SplendidInit
	{
		// 10/24/2009 Paul.  As a performance optimization, we need a way to avoid calling spSYSTEM_TRANSACTIONS_Create for every transaction.
		public static bool bUseSQLServerToken      = false;
		public static bool bEnableACLFieldSecurity = false;

		// .NET 10 Migration: Application.Lock()/UnLock() replaced with SemaphoreSlim(1,1) reentrancy guard.
		// Static so that it survives DI scoped lifetime resets.
		private static readonly SemaphoreSlim  _initLock       = new SemaphoreSlim(1, 1);
		private static volatile bool           _bInitialized   = false;

		private readonly IMemoryCache          _memoryCache        ;
		private readonly IConfiguration        _configuration      ;
		private readonly IHttpContextAccessor  _httpContextAccessor;
		private readonly DbProviderFactories   _dbProviderFactories;
		private readonly Security              _security           ;
		private readonly OrderUtils            _orderUtils         ;

		public SplendidInit(
			IMemoryCache         memoryCache        ,
			IConfiguration       configuration      ,
			IHttpContextAccessor httpContextAccessor,
			DbProviderFactories  dbProviderFactories,
			Security             security           ,
			OrderUtils           orderUtils         )
		{
			_memoryCache         = memoryCache        ;
			_configuration       = configuration      ;
			_httpContextAccessor = httpContextAccessor;
			_dbProviderFactories = dbProviderFactories;
			_security            = security           ;
			_orderUtils          = orderUtils         ;
		}

		// =====================================================================================
		// Private helper: InitAppURLs
		// =====================================================================================
		/// <summary>
		/// Initialises Application URL cache entries from the current HttpContext request.
		/// .NET 10 Migration: Application[] → IMemoryCache; Request.ServerVariables → Request properties.
		/// </summary>
		private void InitAppURLs()
		{
			// Guard: skip if already initialised for this request.
			if (!Sql.IsEmptyString(Sql.ToString(_memoryCache.Get("imageURL"))))
				return;

			var context = _httpContextAccessor?.HttpContext;
			if (context == null)
				return;

			try
			{
				Assembly asm = Assembly.GetExecutingAssembly();
				var request  = context.Request;

				// .NET 10 Migration: Request.ServerVariables["SERVER_NAME"] → Request.Host.Host
				string sServerName      = request.Host.Host;
				// .NET 10 Migration: Request.ServerVariables["LOCAL_ADDR"] → Connection.LocalIpAddress
				string sServerIPAddress = context.Connection?.LocalIpAddress?.ToString() ?? String.Empty;
				// .NET 10 Migration: Request.ApplicationPath → Request.PathBase (may be empty for root-mounted apps)
				string sApplicationPath = request.PathBase.HasValue ? request.PathBase.ToString() : "/";
				if (Sql.IsEmptyString(sApplicationPath))
					sApplicationPath = "/";

				// Store assembly version and framework info.
				_memoryCache.Set("SplendidVersion"    , asm.GetName().Version?.ToString() ?? String.Empty);
				// .NET 10 Migration: asm.ImageRuntimeVersion is obsolete in .NET Core; use FrameworkDescription.
				_memoryCache.Set("ImageRuntimeVersion", System.Runtime.InteropServices.RuntimeEnvironment.GetSystemVersion());
				try
				{
					var attr = asm.GetCustomAttribute<TargetFrameworkAttribute>();
					_memoryCache.Set("TargetFramework", attr?.FrameworkDisplayName ?? String.Empty);
				}
				catch {}

				// .NET 10 Migration: Request.Url.Scheme → Request.Scheme
				_memoryCache.Set("ServerScheme"    , request.Scheme);
				_memoryCache.Set("ServerName"      , sServerName);
				_memoryCache.Set("ServerIPAddress" , sServerIPAddress);
				// .NET 10 Migration: Request.Url.IsDefaultPort / Request.Url.Port → Request.Host.Port
				bool bIsDefaultPort =
					!request.Host.Port.HasValue ||
					(request.Scheme == "http"  && request.Host.Port == 80 ) ||
					(request.Scheme == "https" && request.Host.Port == 443);
				_memoryCache.Set("ServerPort"      , bIsDefaultPort ? String.Empty : ":" + request.Host.Port.Value.ToString());
				_memoryCache.Set("ApplicationPath" , sApplicationPath);
				if (!sApplicationPath.EndsWith("/"))
					sApplicationPath += "/";
				_memoryCache.Set("rootURL"   , sApplicationPath);
				// 07/28/2006 Paul.  Mono requires case-significant paths.
				_memoryCache.Set("imageURL"  , sApplicationPath + "Include/images/");
				_memoryCache.Set("scriptURL" , sApplicationPath + "Include/javascript/");
				_memoryCache.Set("chartURL"  , sApplicationPath + "Include/charts/");
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
		}

		// =====================================================================================
		// InitTerminology
		// =====================================================================================
		/// <summary>
		/// Loads all terminology from vwTERMINOLOGY_Active and vwTERMINOLOGY_ALIASES into IMemoryCache.
		/// .NET 10 Migration: Application[] → IMemoryCache; DbProviderFactories(Application) → _dbProviderFactories.GetFactory(_memoryCache)
		/// </summary>
		public void InitTerminology()
		{
			try
			{
				// 12/03/2008 Paul.  This function can be called from a scheduled task.
				DbProviderFactory dbf = _dbProviderFactories.GetFactory(_memoryCache);
				using (IDbConnection con = dbf.CreateConnection())
				{
					con.Open();
					string sSQL;
					// 05/20/2008 Paul.  Only load terminology from Active languages.
					sSQL = "select NAME                " + ControlChars.CrLf
					     + "     , LANG                " + ControlChars.CrLf
					     + "     , MODULE_NAME         " + ControlChars.CrLf
					     + "     , DISPLAY_NAME        " + ControlChars.CrLf
					     + "  from vwTERMINOLOGY_Active" + ControlChars.CrLf
					     + " where LIST_NAME is null   " + ControlChars.CrLf;
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						using (IDataReader rdr = cmd.ExecuteReader())
						{
							while (rdr.Read())
							{
								string sLANG        = Sql.ToString(rdr["LANG"        ]);
								string sMODULE_NAME = Sql.ToString(rdr["MODULE_NAME" ]);
								string sNAME        = Sql.ToString(rdr["NAME"        ]);
								string sDISP        = Sql.ToString(rdr["DISPLAY_NAME"]);
								// .NET 10 Migration: L10N.SetTerm(Application,...) → L10N.SetTerm(_memoryCache,...)
								L10N.SetTerm(_memoryCache, sLANG, sMODULE_NAME, sNAME, sDISP);
							}
						}
					}
					sSQL = "select NAME                 " + ControlChars.CrLf
					     + "     , LANG                 " + ControlChars.CrLf
					     + "     , MODULE_NAME          " + ControlChars.CrLf
					     + "     , LIST_NAME            " + ControlChars.CrLf
					     + "     , DISPLAY_NAME         " + ControlChars.CrLf
					     + "  from vwTERMINOLOGY_Active " + ControlChars.CrLf
					     + " where LIST_NAME is not null" + ControlChars.CrLf;
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						using (IDataReader rdr = cmd.ExecuteReader())
						{
							while (rdr.Read())
							{
								string sLANG        = Sql.ToString(rdr["LANG"        ]);
								string sMODULE_NAME = Sql.ToString(rdr["MODULE_NAME" ]);
								string sNAME        = Sql.ToString(rdr["NAME"        ]);
								string sLIST_NAME   = Sql.ToString(rdr["LIST_NAME"   ]);
								string sDISP        = Sql.ToString(rdr["DISPLAY_NAME"]);
								L10N.SetTerm(_memoryCache, sLANG, sMODULE_NAME, sLIST_NAME, sNAME, sDISP);
							}
						}
					}
					sSQL = "select ALIAS_NAME           " + ControlChars.CrLf
					     + "     , ALIAS_MODULE_NAME    " + ControlChars.CrLf
					     + "     , ALIAS_LIST_NAME      " + ControlChars.CrLf
					     + "     , NAME                 " + ControlChars.CrLf
					     + "     , MODULE_NAME          " + ControlChars.CrLf
					     + "     , LIST_NAME            " + ControlChars.CrLf
					     + "  from vwTERMINOLOGY_ALIASES" + ControlChars.CrLf;
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						using (IDataReader rdr = cmd.ExecuteReader())
						{
							while (rdr.Read())
							{
								string sALIAS_NAME        = Sql.ToString(rdr["ALIAS_NAME"       ]);
								string sALIAS_MODULE_NAME = Sql.ToString(rdr["ALIAS_MODULE_NAME"]);
								string sALIAS_LIST_NAME   = Sql.ToString(rdr["ALIAS_LIST_NAME"  ]);
								string sNAME              = Sql.ToString(rdr["NAME"             ]);
								string sMODULE_NAME       = Sql.ToString(rdr["MODULE_NAME"      ]);
								string sLIST_NAME         = Sql.ToString(rdr["LIST_NAME"        ]);
								L10N.SetAlias(_memoryCache, sALIAS_MODULE_NAME, sALIAS_LIST_NAME, sALIAS_NAME, sMODULE_NAME, sLIST_NAME, sNAME);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemMessage(_httpContextAccessor?.HttpContext, "Error", new StackTrace(true).GetFrame(0), ex);
			}
		}

		// =====================================================================================
		// InitModules
		// =====================================================================================
		/// <summary>
		/// Loads module metadata from vwMODULES_AppVars into IMemoryCache.
		/// .NET 10 Migration: Application["Modules.*"] → _memoryCache.Set("Modules.*", ...)
		/// ReactOnlyUI: all modules treated as Exists=true (no Server.MapPath() available)
		/// </summary>
		public void InitModules()
		{
			try
			{
				DbProviderFactory dbf = _dbProviderFactories.GetFactory(_memoryCache);
				using (IDbConnection con = dbf.CreateConnection())
				{
					con.Open();
					string sSQL;
					sSQL = "select *                " + ControlChars.CrLf
					     + "  from vwMODULES_AppVars" + ControlChars.CrLf
					     + " order by MODULE_NAME   " + ControlChars.CrLf;
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText    = sSQL;
						cmd.CommandTimeout = 0;
						using (IDataReader rdr = cmd.ExecuteReader())
						{
							Regex r = new Regex(@"[^A-Za-z0-9_]");
							while (rdr.Read())
							{
								string sMODULE_NAME  = Sql.ToString (rdr["MODULE_NAME" ]);
								string sTABLE_NAME   = Sql.ToString (rdr["TABLE_NAME"  ]);
								string sRELATIVE_PATH= Sql.ToString (rdr["RELATIVE_PATH"]);
								string sDISPLAY_NAME = Sql.ToString (rdr["DISPLAY_NAME"]);
								bool   bIS_AUDITED   = Sql.ToBoolean(rdr["IS_AUDITED"  ]);
								bool   bIS_TEAMED    = Sql.ToBoolean(rdr["IS_TEAMED"   ]);
								bool   bIS_ASSIGNED  = Sql.ToBoolean(rdr["IS_ASSIGNED" ]);
								bool   bCUSTOM_PAGING          = false;
								bool   bMASS_UPDATE_ENABLED    = true ;
								bool   bDEFAULT_SEARCH_ENABLED = true ;
								bool   bEXCHANGE_SYNC          = false;
								bool   bEXCHANGE_FOLDERS       = false;
								bool   bEXCHANGE_CREATE_PARENT = false;
								bool   bIS_ADMIN               = false;
								bool   bREST_ENABLED           = false;
								bool   bDUPLICATE_CHECKING_ENABLED    = false;
								bool   bSTREAM_ENABLED                = false;
								bool   bRECORD_LEVEL_SECURITY_ENABLED = false;
								bool   bHAS_CUSTOM                    = false;
								try { bCUSTOM_PAGING              = Sql.ToBoolean(rdr["CUSTOM_PAGING"      ]); } catch {}
								try { bMASS_UPDATE_ENABLED        = Sql.ToBoolean(rdr["MASS_UPDATE_ENABLED"]); } catch {}
								try { bDEFAULT_SEARCH_ENABLED     = Sql.ToBoolean(rdr["DEFAULT_SEARCH_ENABLED"]); } catch {}
								try { bEXCHANGE_SYNC              = Sql.ToBoolean(rdr["EXCHANGE_SYNC"      ]); } catch {}
								try { bEXCHANGE_FOLDERS           = Sql.ToBoolean(rdr["EXCHANGE_FOLDERS"   ]); } catch {}
								try { bEXCHANGE_CREATE_PARENT     = Sql.ToBoolean(rdr["EXCHANGE_CREATE_PARENT"]); } catch {}
								try { bIS_ADMIN                   = Sql.ToBoolean(rdr["IS_ADMIN"           ]); } catch {}
								try { bREST_ENABLED               = Sql.ToBoolean(rdr["REST_ENABLED"       ]); } catch {}
								try { bDUPLICATE_CHECKING_ENABLED = Sql.ToBoolean(rdr["DUPLICATE_CHECHING_ENABLED"]); } catch {}
								try { bSTREAM_ENABLED             = Sql.ToBoolean(rdr["STREAM_ENABLED"     ]); } catch {}
								try { bRECORD_LEVEL_SECURITY_ENABLED = Sql.ToBoolean(rdr["RECORD_LEVEL_SECURITY_ENABLED"]); } catch {}
								try { bHAS_CUSTOM                 = Sql.ToBoolean(rdr["HAS_CUSTOM"         ]); } catch {}

								// 11/03/2009 Paul.  As extra precaution, make sure that the table name is valid.
								sTABLE_NAME = r.Replace(sTABLE_NAME, "");
								_memoryCache.Set("Modules." + sMODULE_NAME + ".TableName"    , sTABLE_NAME         );
								_memoryCache.Set("Modules." + sMODULE_NAME + ".RelativePath" , sRELATIVE_PATH      );
								_memoryCache.Set("Modules." + sMODULE_NAME + ".DisplayName"  , sDISPLAY_NAME       );
								_memoryCache.Set("Modules." + sMODULE_NAME + ".Audited"      , bIS_AUDITED         );
								_memoryCache.Set("Modules." + sMODULE_NAME + ".Teamed"       , bIS_TEAMED          );
								_memoryCache.Set("Modules." + sMODULE_NAME + ".Assigned"     , bIS_ASSIGNED        );
								_memoryCache.Set("Modules." + sMODULE_NAME + ".CustomPaging" , bCUSTOM_PAGING      );
								_memoryCache.Set("Modules." + sMODULE_NAME + ".IsAdmin"      , bIS_ADMIN           );
								_memoryCache.Set("Modules." + sMODULE_NAME + ".MassUpdate"   , bMASS_UPDATE_ENABLED);
								_memoryCache.Set("Modules." + sMODULE_NAME + ".DefaultSearch", bDEFAULT_SEARCH_ENABLED);
								_memoryCache.Set("Modules." + sMODULE_NAME + ".ExchangeSync" , bEXCHANGE_SYNC     );
								_memoryCache.Set("Modules." + sMODULE_NAME + ".ExchangeFolders", bEXCHANGE_FOLDERS );
								_memoryCache.Set("Modules." + sMODULE_NAME + ".ExchangeCreateParent", bEXCHANGE_CREATE_PARENT);
								// .NET 10 Migration: ReactOnlyUI treated as true → all modules Exist = true.
								// Context.Server.MapPath() is not available in ASP.NET Core.
								_memoryCache.Set("Modules." + sMODULE_NAME + ".Exists"       , true                );
								_memoryCache.Set("Modules." + sMODULE_NAME + ".Valid"        , true                );
								if (!Sql.IsEmptyString(sTABLE_NAME))
									_memoryCache.Set("Modules." + sTABLE_NAME + ".ModuleName", sMODULE_NAME);
								_memoryCache.Set("Modules." + sMODULE_NAME + ".RestEnabled"  , bREST_ENABLED       );
								_memoryCache.Set("Modules." + sMODULE_NAME + ".DuplicateCheckingEnabled", bDUPLICATE_CHECKING_ENABLED);
								_memoryCache.Set("Modules." + sMODULE_NAME + ".StreamEnabled", bSTREAM_ENABLED     );
								_memoryCache.Set("Modules." + sMODULE_NAME + ".RecordLevelSecurity", bRECORD_LEVEL_SECURITY_ENABLED);
								_memoryCache.Set("Modules." + sMODULE_NAME + ".HasCustomTable", bHAS_CUSTOM        );
							}
						}
					}
					// 12/13/2017 Paul.  Move archive flag to app so as to prevent catastrophic failure.
					try
					{
						string sARCHIVE_DATABASE = Sql.ToString(_memoryCache.Get("CONFIG.Archive.Database"));
						if (!Sql.IsEmptyString(sARCHIVE_DATABASE))
							sARCHIVE_DATABASE = "[" + sARCHIVE_DATABASE + "].";
						sSQL = "select MODULE_NAME      " + ControlChars.CrLf
						     + "     , (select count(*) from " + sARCHIVE_DATABASE + "INFORMATION_SCHEMA.TABLES where TABLES.TABLE_NAME = vwMODULES_AppVars.TABLE_NAME + '_ARCHIVE') as ARCHIVED_ENBLED" + ControlChars.CrLf
						     + "  from vwMODULES_AppVars" + ControlChars.CrLf
						     + " order by MODULE_NAME   " + ControlChars.CrLf;
						using (IDbCommand cmd = con.CreateCommand())
						{
							cmd.CommandText    = sSQL;
							cmd.CommandTimeout = 0;
							using (IDataReader rdr = cmd.ExecuteReader())
							{
								while (rdr.Read())
								{
									string sMODULE_NAME      = Sql.ToString (rdr["MODULE_NAME"    ]);
									bool   bARCHIVED_ENABLED = Sql.ToBoolean(rdr["ARCHIVED_ENBLED"]);
									_memoryCache.Set("Modules." + sMODULE_NAME + ".ArchiveEnabled", bARCHIVED_ENABLED);
								}
							}
						}
					}
					catch (Exception ex)
					{
						SplendidError.SystemMessage(_httpContextAccessor?.HttpContext, "Error", new StackTrace(true).GetFrame(0), ex);
					}
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = "select count(*) from vwSqlColumns where ObjectName = 'ACCOUNTS'";
						int nColumns = Sql.ToInteger(cmd.ExecuteScalar());
						if (nColumns == 0)
							SplendidError.SystemError(new StackTrace(true).GetFrame(0), "The connection string does not provide sufficient access to base tables.  Please make sure that the SQL user has db_owner access to the database.");
					}
					sSQL = "select max(DATE_MODIFIED_UTC)" + ControlChars.CrLf
					     + "  from vwMODULES             " + ControlChars.CrLf;
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						DateTime dtLastModified = Sql.ToDateTime(cmd.ExecuteScalar());
						_memoryCache.Set("Modules.LastModified", dtLastModified.ToString());
					}
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemMessage(_httpContextAccessor?.HttpContext, "Error", new StackTrace(true).GetFrame(0), ex);
			}
		}

		// =====================================================================================
		// InitModuleACL
		// =====================================================================================
		/// <summary>
		/// Loads default module ACL rights from vwACL_ACCESS_ByModule.
		/// .NET 10 Migration: Security.SetModuleAccess(Application,...) → _security.SetModuleAccess(...)
		/// </summary>
		public void InitModuleACL()
		{
			try
			{
				DbProviderFactory dbf = _dbProviderFactories.GetFactory(_memoryCache);
				using (IDbConnection con = dbf.CreateConnection())
				{
					con.Open();
					string sSQL;
					sSQL = "select MODULE_NAME          " + ControlChars.CrLf
					     + "     , ACLACCESS_ADMIN      " + ControlChars.CrLf
					     + "     , ACLACCESS_ACCESS     " + ControlChars.CrLf
					     + "     , ACLACCESS_VIEW       " + ControlChars.CrLf
					     + "     , ACLACCESS_LIST       " + ControlChars.CrLf
					     + "     , ACLACCESS_EDIT       " + ControlChars.CrLf
					     + "     , ACLACCESS_DELETE     " + ControlChars.CrLf
					     + "     , ACLACCESS_IMPORT     " + ControlChars.CrLf
					     + "     , ACLACCESS_EXPORT     " + ControlChars.CrLf
					     + "     , ACLACCESS_ARCHIVE    " + ControlChars.CrLf
					     + "     , IS_ADMIN             " + ControlChars.CrLf
					     + "  from vwACL_ACCESS_ByModule" + ControlChars.CrLf;
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						using (IDataReader rdr = cmd.ExecuteReader())
						{
							while (rdr.Read())
							{
								string sMODULE_NAME = Sql.ToString(rdr["MODULE_NAME"]);
								_security.SetModuleAccess(sMODULE_NAME, "admin"  , Sql.ToInteger(rdr["ACLACCESS_ADMIN"  ]));
								_security.SetModuleAccess(sMODULE_NAME, "access" , Sql.ToInteger(rdr["ACLACCESS_ACCESS" ]));
								_security.SetModuleAccess(sMODULE_NAME, "view"   , Sql.ToInteger(rdr["ACLACCESS_VIEW"   ]));
								_security.SetModuleAccess(sMODULE_NAME, "list"   , Sql.ToInteger(rdr["ACLACCESS_LIST"   ]));
								_security.SetModuleAccess(sMODULE_NAME, "edit"   , Sql.ToInteger(rdr["ACLACCESS_EDIT"   ]));
								_security.SetModuleAccess(sMODULE_NAME, "delete" , Sql.ToInteger(rdr["ACLACCESS_DELETE" ]));
								_security.SetModuleAccess(sMODULE_NAME, "import" , Sql.ToInteger(rdr["ACLACCESS_IMPORT" ]));
								_security.SetModuleAccess(sMODULE_NAME, "export" , Sql.ToInteger(rdr["ACLACCESS_EXPORT" ]));
								_security.SetModuleAccess(sMODULE_NAME, "archive", Sql.ToInteger(rdr["ACLACCESS_ARCHIVE"]));
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemMessage(_httpContextAccessor?.HttpContext, "Error", new StackTrace(true).GetFrame(0), ex);
			}
		}

		// =====================================================================================
		// InitConfig
		// =====================================================================================
		/// <summary>
		/// Loads system configuration from vwCONFIG into IMemoryCache with "CONFIG." prefix.
		/// .NET 10 Migration: Application["CONFIG.key"] → _memoryCache.Set("CONFIG.key", value)
		/// </summary>
		public void InitConfig()
		{
			try
			{
				DbProviderFactory dbf = _dbProviderFactories.GetFactory(_memoryCache);
				using (IDbConnection con = dbf.CreateConnection())
				{
					con.Open();
					string sSQL;
					sSQL = "select NAME    " + ControlChars.CrLf
					     + "     , VALUE   " + ControlChars.CrLf
					     + "  from vwCONFIG" + ControlChars.CrLf;
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						using (IDataReader rdr = cmd.ExecuteReader())
						{
							while (rdr.Read())
							{
								string sNAME  = Sql.ToString(rdr["NAME" ]);
								string sVALUE = Sql.ToString(rdr["VALUE"]);
								_memoryCache.Set("CONFIG." + sNAME, sVALUE);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemMessage(_httpContextAccessor?.HttpContext, "Error", new StackTrace(true).GetFrame(0), ex);
			}
		}

		// =====================================================================================
		// InitFieldValidators
		// =====================================================================================
		/// <summary>
		/// Loads field validators from vwFIELD_VALIDATORS into IMemoryCache.
		/// </summary>
		public void InitFieldValidators()
		{
			try
			{
				DbProviderFactory dbf = _dbProviderFactories.GetFactory(_memoryCache);
				using (IDbConnection con = dbf.CreateConnection())
				{
					con.Open();
					string sSQL;
					sSQL = "select *                " + ControlChars.CrLf
					     + "  from vwFIELD_VALIDATORS" + ControlChars.CrLf;
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						DataTable dt = new DataTable();
						dt.Load(cmd.ExecuteReader());
						// Group by FIELD_VALIDATOR_ID and cache each group as a DataTable.
						Hashtable hashValidators = new Hashtable();
						foreach (DataRow row in dt.Rows)
						{
							Guid   gFIELD_VALIDATOR_ID = Sql.ToGuid   (row["FIELD_VALIDATOR_ID"]);
							string sFIELD_VALIDATOR_ID = gFIELD_VALIDATOR_ID.ToString().ToUpper();
							if (!hashValidators.Contains(sFIELD_VALIDATOR_ID))
							{
								DataTable dtValidators = dt.Clone();
								hashValidators.Add(sFIELD_VALIDATOR_ID, dtValidators);
							}
							DataTable dtTarget = hashValidators[sFIELD_VALIDATOR_ID] as DataTable;
							DataRow rowNew = dtTarget.NewRow();
							rowNew.ItemArray = row.ItemArray;
							dtTarget.Rows.Add(rowNew);
						}
						foreach (DictionaryEntry entry in hashValidators)
						{
							_memoryCache.Set("FIELD_VALIDATORS." + entry.Key.ToString(), entry.Value);
						}
					}
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemMessage(_httpContextAccessor?.HttpContext, "Error", new StackTrace(true).GetFrame(0), ex);
			}
		}

		// =====================================================================================
		// InitTimeZones
		// =====================================================================================
		/// <summary>
		/// Creates TimeZone objects from vwTIMEZONES and stores in IMemoryCache.
		/// .NET 10 Migration: Application["TIMEZONE.{id}"] → _memoryCache.Set("TIMEZONE.{id}", ...)
		/// </summary>
		public void InitTimeZones()
		{
			try
			{
				DbProviderFactory dbf = _dbProviderFactories.GetFactory(_memoryCache);
				using (IDbConnection con = dbf.CreateConnection())
				{
					con.Open();
					string sSQL;
					sSQL = "select *          " + ControlChars.CrLf
					     + "  from vwTIMEZONES" + ControlChars.CrLf;
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						using (IDataReader rdr = cmd.ExecuteReader())
						{
							while (rdr.Read())
							{
								string sTZID            = String.Empty;
								string sLINKED_TIMEZONE = String.Empty;
								try { sTZID            = Sql.ToString(rdr["TZID"            ]); } catch {}
								try { sLINKED_TIMEZONE = Sql.ToString(rdr["LINKED_TIMEZONE" ]); } catch {}

								// .NET 10 Migration: Application["CONFIG.GMT_Storage"] → _memoryCache.Get("CONFIG.GMT_Storage")
								TimeZone oTimeZone = new TimeZone
									( Sql.ToGuid   (rdr["ID"                   ])
									, Sql.ToString (rdr["NAME"                 ])
									, Sql.ToString (rdr["STANDARD_NAME"        ])
									, Sql.ToString (rdr["STANDARD_ABBREVIATION"])
									, Sql.ToString (rdr["DAYLIGHT_NAME"        ])
									, Sql.ToString (rdr["DAYLIGHT_ABBREVIATION"])
									, Sql.ToInteger(rdr["BIAS"                 ])
									, Sql.ToInteger(rdr["STANDARD_BIAS"        ])
									, Sql.ToInteger(rdr["DAYLIGHT_BIAS"        ])
									, Sql.ToInteger(rdr["STANDARD_YEAR"        ])
									, Sql.ToInteger(rdr["STANDARD_MONTH"       ])
									, Sql.ToInteger(rdr["STANDARD_WEEK"        ])
									, Sql.ToInteger(rdr["STANDARD_DAYOFWEEK"   ])
									, Sql.ToInteger(rdr["STANDARD_HOUR"        ])
									, Sql.ToInteger(rdr["STANDARD_MINUTE"      ])
									, Sql.ToInteger(rdr["DAYLIGHT_YEAR"        ])
									, Sql.ToInteger(rdr["DAYLIGHT_MONTH"       ])
									, Sql.ToInteger(rdr["DAYLIGHT_WEEK"        ])
									, Sql.ToInteger(rdr["DAYLIGHT_DAYOFWEEK"   ])
									, Sql.ToInteger(rdr["DAYLIGHT_HOUR"        ])
									, Sql.ToInteger(rdr["DAYLIGHT_MINUTE"      ])
									, Sql.ToBoolean(_memoryCache.Get("CONFIG.GMT_Storage"))
									, sTZID
									);
								_memoryCache.Set("TIMEZONE." + oTimeZone.ID.ToString(), oTimeZone);
								if (!Sql.IsEmptyString(sTZID))
									_memoryCache.Set("TIMEZONE.TZID." + oTimeZone.TZID, oTimeZone);
								if (!Sql.IsEmptyString(sLINKED_TIMEZONE))
									_memoryCache.Set("TIMEZONE.TZID." + sLINKED_TIMEZONE, oTimeZone);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemMessage(_httpContextAccessor?.HttpContext, "Error", new StackTrace(true).GetFrame(0), ex);
			}
		}

		// =====================================================================================
		// InitCurrencies — continuation
		// =====================================================================================
		/// <summary>
		/// Creates Currency objects from vwCURRENCIES and stores in IMemoryCache.
		/// .NET 10 Migration: Currency(Application,...) → Currency(_memoryCache,...);
		///                    Application["CURRENCY.*"] → _memoryCache.Set("CURRENCY.*",...)
		/// </summary>
		public void InitCurrencies()
		{
			try
			{
				DbProviderFactory dbf = _dbProviderFactories.GetFactory(_memoryCache);
				using (IDbConnection con = dbf.CreateConnection())
				{
					con.Open();
					string sSQL;
					sSQL = "select *           " + ControlChars.CrLf
					     + "  from vwCURRENCIES" + ControlChars.CrLf;
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						using (IDataReader rdr = cmd.ExecuteReader())
						{
							while (rdr.Read())
							{
								// .NET 10 Migration: Currency(Application,...) → Currency(_memoryCache,...)
								Currency C10n = new Currency
									( _memoryCache
									, Sql.ToGuid  (rdr["ID"             ])
									, Sql.ToString(rdr["NAME"           ])
									, Sql.ToString(rdr["SYMBOL"         ])
									, Sql.ToString(rdr["ISO4217"        ])
									, Sql.ToFloat (rdr["CONVERSION_RATE"])
									);
								_memoryCache.Set("CURRENCY." + C10n.ID.ToString(), C10n);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemMessage(_httpContextAccessor?.HttpContext, "Error", new StackTrace(true).GetFrame(0), ex);
			}
		}

		// =====================================================================================
		// InitApp
		// =====================================================================================
		/// <summary>
		/// One-time application initialization. Thread-safe via SemaphoreSlim.
		/// .NET 10 Migration: Application.Lock()/UnLock() → SemaphoreSlim(1,1)
		///                    Application.Clear() → IMemoryCache.Clear() (MemoryCache)
		///                    ConfigurationManager.AppSettings → IConfiguration sections
		///                    HttpRuntime.Cache → IMemoryCache
		///                    Application["CONFIG.*"] → _memoryCache.Set("CONFIG.*",...)
		/// </summary>
		public void InitApp()
		{
			_initLock.Wait();
			try
			{
				bool bFirstInit = !_bInitialized;
				_bInitialized = false;

				// .NET 10 Migration: Application.Clear() replaced by MemoryCache.Clear().
				if (_memoryCache is MemoryCache mc)
				{
					mc.Clear();
				}

				InitAppURLs();

				// Build database before logging to ensure ServerName is available.
				// .NET 10 Migration: SqlBuild.BuildDatabase(Context) – static method preserving HttpContext signature.
				SqlBuild.BuildDatabase(_httpContextAccessor?.HttpContext);

				string sSplendidVersion = Sql.ToString(_memoryCache.Get("SplendidVersion"));
				if (bFirstInit)
					SplendidError.SystemMessage(_httpContextAccessor?.HttpContext, "Warning", new System.Diagnostics.StackTrace(true).GetFrame(0), "Application start. Version " + sSplendidVersion);
				else
					SplendidError.SystemMessage(_httpContextAccessor?.HttpContext, "Warning", new System.Diagnostics.StackTrace(true).GetFrame(0), "Application restart. Version " + sSplendidVersion);

				// .NET 10 Migration: Application.Clear() of session shortcut keys via ISession.
				var session = _httpContextAccessor?.HttpContext?.Session;
				if (session != null)
				{
					List<string> keysToRemove = new List<string>();
					foreach (string sKey in session.Keys)
					{
						if (sKey.StartsWith("vwSHORTCUTS_Menu_ByUser") || sKey.StartsWith("vwMODULES_TabMenu_ByUser"))
							keysToRemove.Add(sKey);
					}
					foreach (string sKey in keysToRemove)
						session.Remove(sKey);
				}

				// .NET 10 Migration: ConfigurationManager.AppSettings → IConfiguration "AppSettings" section.
				var appSettingsSection = _configuration?.GetSection("AppSettings");
				if (appSettingsSection != null)
				{
					foreach (var child in appSettingsSection.GetChildren())
					{
						if (!Sql.IsEmptyString(child.Value))
							_memoryCache.Set("CONFIG." + child.Key, child.Value);
					}
				}
				var configSection = _configuration?.GetSection("CONFIG");
				if (configSection != null)
				{
					foreach (var child in configSection.GetChildren())
					{
						if (!Sql.IsEmptyString(child.Value))
							_memoryCache.Set("CONFIG." + child.Key, child.Value);
					}
				}

				DbProviderFactory dbf = _dbProviderFactories.GetFactory(_memoryCache);
				using (IDbConnection con = dbf.CreateConnection())
				{
					con.Open();
					bUseSQLServerToken      = false;
					bEnableACLFieldSecurity = false;
					if (Sql.IsSQLServer(con))
					{
						using (IDbCommand cmd = con.CreateCommand())
						{
							cmd.CommandText = "select @@VERSION";
							string sSqlVersion = Sql.ToString(cmd.ExecuteScalar());
							bool bSQLAzure = false;
							if (sSqlVersion.StartsWith("Microsoft SQL Azure") || (sSqlVersion.IndexOf("SQL Server") > 0 && sSqlVersion.IndexOf("CloudDB") > 0))
							{
								bSQLAzure = true;
								SplendidError.SystemMessage(_httpContextAccessor?.HttpContext, "Warning", new System.Diagnostics.StackTrace(true).GetFrame(0), "Connected to Microsoft SQL Azure.");
							}
							if (!bSQLAzure)
							{
								try
								{
									cmd.CommandText = "select object_definition(object_id(N'spSqlGetTransactionToken'))";
									sSqlVersion = Sql.ToString(cmd.ExecuteScalar());
									if (sSqlVersion != null && sSqlVersion.Contains("exec dbo.spSYSTEM_TRANSACTIONS_Create @ID out, null;"))
									{
										bSQLAzure = true;
										SplendidError.SystemMessage(_httpContextAccessor?.HttpContext, "Warning", new System.Diagnostics.StackTrace(true).GetFrame(0), "Using SYSTEM_TRANSACTIONS table.");
									}
								}
								catch (Exception exVer)
								{
									SplendidError.SystemMessage(_httpContextAccessor?.HttpContext, "Error", new System.Diagnostics.StackTrace(true).GetFrame(0), exVer);
								}
							}
							bUseSQLServerToken = !bSQLAzure;
						}
					}
					else if (Sql.IsEffiProz(con))
					{
						bUseSQLServerToken = true;
					}
					using (IDbCommand cmd = con.CreateCommand())
					{
						try
						{
							cmd.CommandText = "select count(*) from vwSqlViews where VIEW_NAME = upper('vwACL_FIELD_ACCESS_ByUserAlias')";
							bEnableACLFieldSecurity = Sql.ToBoolean(cmd.ExecuteScalar());
						}
						catch
						{
							bEnableACLFieldSecurity = false;
						}
					}
				}

				InitTerminology();
				InitConfig     ();
				InitModules    ();
				InitFieldValidators();
				InitTimeZones  ();
				InitCurrencies ();
				InitModuleACL  ();

				// .NET 10 Migration: Linux platform detection via Environment.OSVersion.Platform.
				int nPlatform = (int) Environment.OSVersion.Platform;
				if (nPlatform == 4 || nPlatform == 128)
				{
					SplendidError.SystemMessage(_httpContextAccessor?.HttpContext, "Warning", new System.Diagnostics.StackTrace(true).GetFrame(0), "Silverlight is disabled on Mono.");
					_memoryCache.Set("CONFIG.enable_silverlight", "false");
					_memoryCache.Set("CONFIG.enable_flash"      , "true" );
				}

				// .NET 10 Migration: System.NET45 flag – always true in .NET 10.
				_memoryCache.Set("System.NET45", true);

				using (IDbConnection con = dbf.CreateConnection())
				{
					con.Open();
					string sSQL = "select max(DATE_ENTERED)" + ControlChars.CrLf
					            + "  from vwSYSTEM_EVENTS  " + ControlChars.CrLf;
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						DateTime dtLastUpdate = Sql.ToDateTime(cmd.ExecuteScalar());
						if (dtLastUpdate == DateTime.MinValue)
							dtLastUpdate = DateTime.Now;
						_memoryCache.Set("SYSTEM_EVENTS.MaxDate", dtLastUpdate);
						SplendidError.SystemMessage(_httpContextAccessor?.HttpContext, "Warning", new System.Diagnostics.StackTrace(true).GetFrame(0), "System Events Last Update on " + dtLastUpdate.ToString());
					}
				}
				_bInitialized = true;
			}
			catch (Exception ex)
			{
				SplendidError.SystemMessage(_httpContextAccessor?.HttpContext, "Error", new System.Diagnostics.StackTrace(true).GetFrame(0), ex);
			}
			finally
			{
				_initLock.Release();
			}
		}

		// =====================================================================================
		// StopApp
		// =====================================================================================
		/// <summary>
		/// Application shutdown handler: logs the stop event and resets initialization flag.
		/// .NET 10 Migration: DbProviderFactories.GetFactory(Application) → _dbProviderFactories.GetFactory(_memoryCache)
		/// </summary>
		public void StopApp()
		{
			try
			{
				Guid   gUSER_ID          = Guid.Empty;
				string sUSER_NAME        = String.Empty;
				string sMACHINE          = String.Empty;
				string sASPNET_SESSIONID = String.Empty;
				string sREMOTE_HOST      = String.Empty;
				string sSERVER_HOST      = String.Empty;
				string sTARGET           = String.Empty;
				string sRELATIVE_PATH    = String.Empty;
				string sPARAMETERS       = String.Empty;
				string sFILE_NAME        = String.Empty;
				string sMETHOD           = String.Empty;
				string sERROR_TYPE       = "Warning";
				string sMESSAGE          = "Application stop.";
				Int32  nLINE_NUMBER      = 0;
				try { sMACHINE = System.Environment.MachineName; } catch {}

				System.Diagnostics.StackFrame stack = new System.Diagnostics.StackTrace(true).GetFrame(0);
				if (stack != null)
				{
					sFILE_NAME   = stack.GetFileName();
					sMETHOD      = stack.GetMethod().ToString();
					nLINE_NUMBER = stack.GetFileLineNumber();
				}
				try
				{
					DbProviderFactory dbf = _dbProviderFactories.GetFactory(_memoryCache);
					using (IDbConnection con = dbf.CreateConnection())
					{
						con.Open();
						using (IDbTransaction trn = Sql.BeginTransaction(con))
						{
							try
							{
								SqlProcs.spSYSTEM_LOG_InsertOnly(gUSER_ID, sUSER_NAME, sMACHINE, sASPNET_SESSIONID, sREMOTE_HOST, sSERVER_HOST, sTARGET, sRELATIVE_PATH, sPARAMETERS, sERROR_TYPE, sFILE_NAME, sMETHOD, nLINE_NUMBER, sMESSAGE, trn);
								trn.Commit();
							}
							catch
							{
								trn.Rollback();
							}
						}
						if (Sql.IsEffiProz(con))
						{
							using (IDbCommand cmd = con.CreateCommand())
							{
								cmd.CommandText = "SHUTDOWN";
								cmd.ExecuteNonQuery();
							}
						}
					}
				}
				catch {}
			}
			catch {}
			finally
			{
				_bInitialized = false;
			}
		}

		// =====================================================================================
		// InitUserPreferences
		// =====================================================================================
		/// <summary>
		/// Parses an XML user preferences string and populates missing defaults from
		/// IMemoryCache CONFIG.* entries (replacing Application[] access).
		/// </summary>
		public XmlDocument InitUserPreferences(string sUSER_PREFERENCES)
		{
			XmlDocument xml = null;
			try
			{
				xml = new XmlDocument();
				xml.XmlResolver = null;
				if (!Sql.IsEmptyString(sUSER_PREFERENCES))
				{
					if (!sUSER_PREFERENCES.StartsWith("<?xml ") && !sUSER_PREFERENCES.StartsWith("<xml>"))
					{
						sUSER_PREFERENCES = XmlUtil.ConvertFromPHP(sUSER_PREFERENCES);
					}
					xml.LoadXml(sUSER_PREFERENCES);
				}
				else
				{
					xml.AppendChild(xml.CreateProcessingInstruction("xml", "version=\"1.0\" encoding=\"UTF-8\""));
					xml.AppendChild(xml.CreateElement("USER_PREFERENCE"));
				}

				string sCulture    = L10N.NormalizeCulture(XmlUtil.SelectSingleNode(xml, "culture"));
				string sTheme      = XmlUtil.SelectSingleNode(xml, "theme"      );
				string sDateFormat = XmlUtil.SelectSingleNode(xml, "dateformat" );
				string sTimeFormat = XmlUtil.SelectSingleNode(xml, "timeformat" );
				string sTimeZone   = XmlUtil.SelectSingleNode(xml, "timezone"   );
				string sCurrencyID = XmlUtil.SelectSingleNode(xml, "currency_id");

				if (Sql.IsEmptyString(sCulture   )) XmlUtil.SetSingleNode(xml, "culture"   , SplendidDefaults.Culture()    );
				if (Sql.IsEmptyString(sTheme      )) XmlUtil.SetSingleNode(xml, "theme"      , SplendidDefaults.Theme()      );
				if (Sql.IsEmptyString(sDateFormat )) XmlUtil.SetSingleNode(xml, "dateformat" , SplendidDefaults.DateFormat() );
				if (SplendidDefaults.IsValidDateFormat(sDateFormat))
					XmlUtil.SetSingleNode(xml, "dateformat", SplendidDefaults.DateFormat(sDateFormat));
				if (Sql.IsEmptyString(sTimeFormat )) XmlUtil.SetSingleNode(xml, "timeformat" , SplendidDefaults.TimeFormat() );
				if (Sql.IsEmptyString(sCurrencyID )) XmlUtil.SetSingleNode(xml, "currency_id", SplendidDefaults.CurrencyID() );
				if (Sql.IsEmptyString(sTimeZone) && !Sql.IsEmptyString(XmlUtil.SelectSingleNode(xml, "timez")))
				{
					int nTimez = Sql.ToInteger(XmlUtil.SelectSingleNode(xml, "timez"));
					sTimeZone = SplendidDefaults.TimeZone(nTimez);
					XmlUtil.SetSingleNode(xml, "timezone", sTimeZone);
				}
				if (Sql.IsEmptyString(sTimeZone))
				{
					sTimeZone = SplendidDefaults.TimeZone();
					XmlUtil.SetSingleNode(xml, "timezone", sTimeZone);
				}
				// .NET 10 Migration: Application["CONFIG.*"] → _memoryCache.Get("CONFIG.*")
				string sSaveQuery = XmlUtil.SelectSingleNode(xml, "save_query");
				if (Sql.IsEmptyString(sSaveQuery))
					XmlUtil.SetSingleNode(xml, "save_query", Sql.ToBoolean(_memoryCache.Get("CONFIG.save_query")).ToString());
				string sGroupTabs = XmlUtil.SelectSingleNode(xml, "group_tabs");
				if (Sql.IsEmptyString(sGroupTabs))
					XmlUtil.SetSingleNode(xml, "group_tabs", Sql.ToBoolean(_memoryCache.Get("CONFIG.default_group_tabs")).ToString());
				string sSubPanelTabs = XmlUtil.SelectSingleNode(xml, "subpanel_tabs");
				if (Sql.IsEmptyString(sSubPanelTabs))
					XmlUtil.SetSingleNode(xml, "subpanel_tabs", Sql.ToBoolean(_memoryCache.Get("CONFIG.default_subpanel_tabs")).ToString());
			}
			catch (Exception ex)
			{
				SplendidError.SystemError(new System.Diagnostics.StackTrace(true).GetFrame(0), ex);
			}
			return xml;
		}

		// =====================================================================================
		// LoadUserPreferences (3-param overload)
		// =====================================================================================
		/// <summary>
		/// Convenience overload: uses ambient IHttpContextAccessor context.
		/// </summary>
		public void LoadUserPreferences(Guid gID, string sTheme, string sCulture)
		{
			LoadUserPreferences(_httpContextAccessor?.HttpContext, gID, sTheme, sCulture);
		}

		// =====================================================================================
		// LoadUserPreferences (4-param overload)
		// =====================================================================================
		/// <summary>
		/// Loads per-user preferences from vwUSERS into the supplied HttpContext session.
		/// .NET 10 Migration: HttpApplicationState → _memoryCache; HttpSessionState → ISession;
		///                    L10N.AlternateLanguage(Application,...) → L10N.AlternateLanguage(_memoryCache,...);
		///                    Currency.CreateCurrency(Application,...) → Currency.CreateCurrency(_memoryCache,...);
		///                    OrderUtils.GetCurrencyConversionRate(Application,...) → _orderUtils.GetCurrencyConversionRate(_memoryCache,...);
		/// </summary>
		public void LoadUserPreferences(HttpContext context, Guid gID, string sTheme, string sCulture)
		{
			ISession session = context?.Session ?? _httpContextAccessor?.HttpContext?.Session;
			string sApplicationPath = Sql.ToString(_memoryCache.Get("rootURL"));

			if (!Sql.IsEmptyGuid(gID))
			{
				DbProviderFactory dbf = _dbProviderFactories.GetFactory(_memoryCache);
				using (IDbConnection con = dbf.CreateConnection())
				{
					string sSQL = "select *       " + ControlChars.CrLf
					            + "  from vwUSERS " + ControlChars.CrLf
					            + " where ID = @ID" + ControlChars.CrLf;
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@ID", gID);
						con.Open();
						using (IDataReader rdr = cmd.ExecuteReader(CommandBehavior.SingleRow))
						{
							if (rdr.Read())
							{
								_security.EMAIL1    = Sql.ToString(rdr["EMAIL1"   ]);
								_security.FULL_NAME = Sql.ToString(rdr["FULL_NAME"]);
								try
								{
									if (session != null)
									{
										// .NET 10 Migration: L10N.AlternateLanguage(Application,...) → L10N.AlternateLanguage(_memoryCache,...)
										string sUserCulture = L10N.AlternateLanguage(_memoryCache, Sql.IsEmptyString(sCulture) ? Sql.ToString(rdr["LANG"]) : sCulture);
										string sUserTheme   = Sql.IsEmptyString(sTheme) ? Sql.ToString(rdr["THEME"]) : sTheme;
										session.SetString("USER_SETTINGS/CULTURE"          , sUserCulture);
										session.SetString("USER_SETTINGS/THEME"            , sUserTheme  );
										session.SetString("USER_SETTINGS/DEFAULT_THEME"    , sUserTheme  );
										session.SetString("themeURL"                       , sApplicationPath + "App_Themes/" + sUserTheme + "/");
										session.SetString("USER_SETTINGS/DATEFORMAT"       , Sql.ToString (rdr["DATE_FORMAT"   ]));
										session.SetString("USER_SETTINGS/TIMEFORMAT"       , Sql.ToString (rdr["TIME_FORMAT"   ]));
										session.SetString("USER_SETTINGS/SAVE_QUERY"       , Sql.ToBoolean(rdr["SAVE_QUERY"    ]).ToString());
										session.SetString("USER_SETTINGS/GROUP_TABS"       , Sql.ToBoolean(rdr["GROUP_TABS"    ]).ToString());
										session.SetString("USER_SETTINGS/SUBPANEL_TABS"    , Sql.ToBoolean(rdr["SUBPANEL_TABS" ]).ToString());
										session.SetString("USER_SETTINGS/TIMEZONE"         , Sql.ToGuid   (rdr["TIMEZONE_ID"   ]).ToString());
										session.SetString("USER_SETTINGS/TIMEZONE/ORIGINAL", Sql.ToString (rdr["TIMEZONE_ID"   ]));
										session.SetString("USER_SETTINGS/CURRENCY"         , Sql.ToString (rdr["CURRENCY_ID"   ]));
									}
								}
								catch (Exception ex)
								{
									SplendidError.SystemError(new System.Diagnostics.StackTrace(true).GetFrame(0), ex);
								}
								try
								{
									if (session != null)
									{
										session.SetString("PHONE_WORK"  , Sql.ToString(rdr["PHONE_WORK"  ]));
										session.SetString("EXTENSION"   , Sql.ToString(rdr["EXTENSION"   ]));
										session.SetString("PHONE_MOBILE", Sql.ToString(rdr["PHONE_MOBILE"]));
										session.SetString("SMS_OPT_IN"  , Sql.ToString(rdr["SMS_OPT_IN"  ]));
									}
								}
								catch (Exception ex)
								{
									SplendidError.SystemError(new System.Diagnostics.StackTrace(true).GetFrame(0), ex);
								}
								try
								{
									_security.PICTURE        = Sql.ToString(rdr["PICTURE"       ]);
									_security.EXCHANGE_ALIAS = Sql.ToString(rdr["EXCHANGE_ALIAS"]);
									_security.EXCHANGE_EMAIL = Sql.ToString(rdr["EXCHANGE_EMAIL"]);
									_security.MAIL_SMTPUSER  = Sql.ToString(rdr["MAIL_SMTPUSER" ]);
									_security.MAIL_SMTPPASS  = Sql.ToString(rdr["MAIL_SMTPPASS" ]);
								}
								catch (Exception ex)
								{
									SplendidError.SystemError(new System.Diagnostics.StackTrace(true).GetFrame(0), ex);
								}
							}
						}
					}
				}
			}

			if (session == null) return;

			// Apply defaults for any preferences still missing.
			if (Sql.IsEmptyString(session.GetString("USER_SETTINGS/CULTURE")))
				session.SetString("USER_SETTINGS/CULTURE", SplendidDefaults.Culture());
			if (Sql.IsEmptyString(session.GetString("USER_SETTINGS/THEME")))
			{
				string sDefaultTheme = SplendidDefaults.Theme();
				session.SetString("USER_SETTINGS/THEME"        , sDefaultTheme);
				session.SetString("USER_SETTINGS/DEFAULT_THEME", sDefaultTheme);
				session.SetString("themeURL"                   , sApplicationPath + "App_Themes/" + sDefaultTheme + "/");
			}
			if (Sql.IsEmptyString(session.GetString("USER_SETTINGS/DATEFORMAT")))
				session.SetString("USER_SETTINGS/DATEFORMAT", SplendidDefaults.DateFormat());
			if (Sql.IsEmptyString(session.GetString("USER_SETTINGS/TIMEFORMAT")))
				session.SetString("USER_SETTINGS/TIMEFORMAT", SplendidDefaults.TimeFormat());
			if (Sql.IsEmptyString(session.GetString("USER_SETTINGS/CURRENCY")))
				session.SetString("USER_SETTINGS/CURRENCY", SplendidDefaults.CurrencyID().ToString());
			if (Sql.IsEmptyString(session.GetString("USER_SETTINGS/TIMEZONE")))
				session.SetString("USER_SETTINGS/TIMEZONE", SplendidDefaults.TimeZone());
			if (Sql.IsEmptyString(session.GetString("USER_SETTINGS/SAVE_QUERY")))
				session.SetString("USER_SETTINGS/SAVE_QUERY", Sql.ToBoolean(_memoryCache.Get("CONFIG.save_query")).ToString());
			if (Sql.IsEmptyString(session.GetString("USER_SETTINGS/GROUP_TABS")))
				session.SetString("USER_SETTINGS/GROUP_TABS", Sql.ToBoolean(_memoryCache.Get("CONFIG.default_group_tabs")).ToString());
			if (Sql.IsEmptyString(session.GetString("USER_SETTINGS/SUBPANEL_TABS")))
				session.SetString("USER_SETTINGS/SUBPANEL_TABS", Sql.ToBoolean(_memoryCache.Get("CONFIG.default_subpanel_tabs")).ToString());

			// .NET 10 Migration: CurrencyLayer API rate check.
			// Currency.CreateCurrency(_memoryCache,...) → static factory; _orderUtils.GetCurrencyConversionRate(...)
			if (!Sql.IsEmptyString(_memoryCache.Get("CONFIG.CurrencyLayer.AccessKey")))
			{
				Guid gCURRENCY_ID = Sql.ToGuid(session.GetString("USER_SETTINGS/CURRENCY"));
				Currency C10n = Currency.CreateCurrency(_memoryCache, gCURRENCY_ID);
				if (C10n != null)
				{
					StringBuilder sbErrors = new StringBuilder();
					float dRate = _orderUtils.GetCurrencyConversionRate(_memoryCache, C10n.ISO4217, String.Empty, sbErrors);
					if (sbErrors.Length == 0)
						C10n.CONVERSION_RATE = dRate;
				}
			}
		}

		// =====================================================================================
		// ClearUserACL
		// =====================================================================================
		/// <summary>
		/// Removes ACLACCESS_ and ACLFIELD_ session keys to force ACL re-evaluation.
		/// .NET 10 Migration: HttpContext.Current.Session → ISession via _httpContextAccessor.
		/// </summary>
		public void ClearUserACL()
		{
			var session = _httpContextAccessor?.HttpContext?.Session;
			if (session == null) return;
			List<string> keysToRemove = new List<string>();
			foreach (string sKey in session.Keys)
			{
				if (sKey.StartsWith("ACLACCESS_") || sKey.StartsWith("ACLFIELD_"))
					keysToRemove.Add(sKey);
			}
			foreach (string sKey in keysToRemove)
				session.Remove(sKey);
		}

		// =====================================================================================
		// LoadPortalACL
		// =====================================================================================
		/// <summary>
		/// Loads portal-role ACL from vwACL_ACCESS_ByRole (fixed ROLE_ID) into session.
		/// .NET 10 Migration: Security.SetUserAccess → _security.SetUserAccess (instance).
		/// </summary>
		public void LoadPortalACL()
		{
			DbProviderFactory dbf = _dbProviderFactories.GetFactory(_memoryCache);
			using (IDbConnection con = dbf.CreateConnection())
			{
				con.Open();
				Guid gROLE_ID = new Guid("5B99F57A-3F86-4B44-9324-80E777D0EE04");
				string sSQL = "select MODULE_NAME          " + ControlChars.CrLf
				            + "     , ACLACCESS_ADMIN      " + ControlChars.CrLf
				            + "     , ACLACCESS_ACCESS     " + ControlChars.CrLf
				            + "     , ACLACCESS_VIEW       " + ControlChars.CrLf
				            + "     , ACLACCESS_LIST       " + ControlChars.CrLf
				            + "     , ACLACCESS_EDIT       " + ControlChars.CrLf
				            + "     , ACLACCESS_DELETE     " + ControlChars.CrLf
				            + "     , ACLACCESS_IMPORT     " + ControlChars.CrLf
				            + "     , ACLACCESS_EXPORT     " + ControlChars.CrLf
				            + "     , ACLACCESS_ARCHIVE    " + ControlChars.CrLf
				            + "     , IS_ADMIN             " + ControlChars.CrLf
				            + "  from vwACL_ACCESS_ByRole  " + ControlChars.CrLf
				            + " where ROLE_ID = @ROLE_ID   " + ControlChars.CrLf;
				using (IDbCommand cmd = con.CreateCommand())
				{
					cmd.CommandText = sSQL;
					Sql.AddParameter(cmd, "@ROLE_ID", gROLE_ID);
					using (IDataReader rdr = cmd.ExecuteReader())
					{
						while (rdr.Read())
						{
							string sMODULE_NAME = Sql.ToString(rdr["MODULE_NAME"]);
							_security.SetUserAccess(sMODULE_NAME, "admin"  , Sql.ToInteger(rdr["ACLACCESS_ADMIN"  ]));
							_security.SetUserAccess(sMODULE_NAME, "access" , Sql.ToInteger(rdr["ACLACCESS_ACCESS" ]));
							_security.SetUserAccess(sMODULE_NAME, "view"   , Sql.ToInteger(rdr["ACLACCESS_VIEW"   ]));
							_security.SetUserAccess(sMODULE_NAME, "list"   , Sql.ToInteger(rdr["ACLACCESS_LIST"   ]));
							_security.SetUserAccess(sMODULE_NAME, "edit"   , Sql.ToInteger(rdr["ACLACCESS_EDIT"   ]));
							_security.SetUserAccess(sMODULE_NAME, "delete" , Sql.ToInteger(rdr["ACLACCESS_DELETE" ]));
							_security.SetUserAccess(sMODULE_NAME, "import" , Sql.ToInteger(rdr["ACLACCESS_IMPORT" ]));
							_security.SetUserAccess(sMODULE_NAME, "export" , Sql.ToInteger(rdr["ACLACCESS_EXPORT" ]));
							_security.SetUserAccess(sMODULE_NAME, "archive", Sql.ToInteger(rdr["ACLACCESS_ARCHIVE"]));
						}
					}
				}
				if (bEnableACLFieldSecurity)
				{
					sSQL = "select MODULE_NAME                   " + ControlChars.CrLf
					     + "     , FIELD_NAME                    " + ControlChars.CrLf
					     + "     , ACLACCESS                     " + ControlChars.CrLf
					     + "  from vwACL_FIELD_ACCESS_ByRole     " + ControlChars.CrLf
					     + " where ROLE_ID = @ROLE_ID            " + ControlChars.CrLf
					     + "   and FIELD_NAME is not null        " + ControlChars.CrLf;
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@ROLE_ID", gROLE_ID);
						using (IDataReader rdr = cmd.ExecuteReader())
						{
							while (rdr.Read())
							{
								string sMODULE_NAME = Sql.ToString (rdr["MODULE_NAME"]);
								string sFIELD_NAME  = Sql.ToString (rdr["FIELD_NAME" ]);
								int    nACLACCESS   = Sql.ToInteger(rdr["ACLACCESS"  ]);
								_security.SetUserFieldSecurity(sMODULE_NAME, sFIELD_NAME, nACLACCESS);
							}
						}
					}
				}
			}
		}

		// =====================================================================================
		// LoadUserACL
		// =====================================================================================
		/// <summary>
		/// Loads per-user ACL overrides from vwACL_ACCESS_ByUser into the current session.
		/// .NET 10 Migration: Security.SetUserAccess → _security.SetUserAccess (instance).
		/// </summary>
		public void LoadUserACL(Guid gUSER_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories.GetFactory(_memoryCache);
			using (IDbConnection con = dbf.CreateConnection())
			{
				con.Open();
				string sSQL = "select MODULE_NAME          " + ControlChars.CrLf
				            + "     , ACLACCESS_ADMIN      " + ControlChars.CrLf
				            + "     , ACLACCESS_ACCESS     " + ControlChars.CrLf
				            + "     , ACLACCESS_VIEW       " + ControlChars.CrLf
				            + "     , ACLACCESS_LIST       " + ControlChars.CrLf
				            + "     , ACLACCESS_EDIT       " + ControlChars.CrLf
				            + "     , ACLACCESS_DELETE     " + ControlChars.CrLf
				            + "     , ACLACCESS_IMPORT     " + ControlChars.CrLf
				            + "     , ACLACCESS_EXPORT     " + ControlChars.CrLf
				            + "     , ACLACCESS_ARCHIVE    " + ControlChars.CrLf
				            + "     , IS_ADMIN             " + ControlChars.CrLf
				            + "  from vwACL_ACCESS_ByUser  " + ControlChars.CrLf
				            + " where USER_ID = @USER_ID   " + ControlChars.CrLf;
				using (IDbCommand cmd = con.CreateCommand())
				{
					cmd.CommandText = sSQL;
					Sql.AddParameter(cmd, "@USER_ID", gUSER_ID);
					using (IDataReader rdr = cmd.ExecuteReader())
					{
						while (rdr.Read())
						{
							string sMODULE_NAME = Sql.ToString(rdr["MODULE_NAME"]);
							_security.SetUserAccess(sMODULE_NAME, "admin"  , Sql.ToInteger(rdr["ACLACCESS_ADMIN"  ]));
							_security.SetUserAccess(sMODULE_NAME, "access" , Sql.ToInteger(rdr["ACLACCESS_ACCESS" ]));
							_security.SetUserAccess(sMODULE_NAME, "view"   , Sql.ToInteger(rdr["ACLACCESS_VIEW"   ]));
							_security.SetUserAccess(sMODULE_NAME, "list"   , Sql.ToInteger(rdr["ACLACCESS_LIST"   ]));
							_security.SetUserAccess(sMODULE_NAME, "edit"   , Sql.ToInteger(rdr["ACLACCESS_EDIT"   ]));
							_security.SetUserAccess(sMODULE_NAME, "delete" , Sql.ToInteger(rdr["ACLACCESS_DELETE" ]));
							_security.SetUserAccess(sMODULE_NAME, "import" , Sql.ToInteger(rdr["ACLACCESS_IMPORT" ]));
							_security.SetUserAccess(sMODULE_NAME, "export" , Sql.ToInteger(rdr["ACLACCESS_EXPORT" ]));
							_security.SetUserAccess(sMODULE_NAME, "archive", Sql.ToInteger(rdr["ACLACCESS_ARCHIVE"]));
						}
					}
				}
				if (bEnableACLFieldSecurity)
				{
					sSQL = "select MODULE_NAME                   " + ControlChars.CrLf
					     + "     , FIELD_NAME                    " + ControlChars.CrLf
					     + "     , ACLACCESS                     " + ControlChars.CrLf
					     + "  from vwACL_FIELD_ACCESS_ByUserAlias" + ControlChars.CrLf
					     + " where USER_ID = @USER_ID            " + ControlChars.CrLf;
					using (IDbCommand cmd = con.CreateCommand())
					{
						cmd.CommandText = sSQL;
						Sql.AddParameter(cmd, "@USER_ID", gUSER_ID);
						using (IDataReader rdr = cmd.ExecuteReader())
						{
							while (rdr.Read())
							{
								string sMODULE_NAME = Sql.ToString (rdr["MODULE_NAME"]);
								string sFIELD_NAME  = Sql.ToString (rdr["FIELD_NAME" ]);
								int    nACLACCESS   = Sql.ToInteger(rdr["ACLACCESS"  ]);
								_security.SetUserFieldSecurity(sMODULE_NAME, sFIELD_NAME, nACLACCESS);
							}
						}
					}
				}
			}
		}

		// =====================================================================================
		// LoadACLRoles
		// =====================================================================================
		/// <summary>
		/// Loads ACL role assignments for the user from vwACL_ROLES_USERS.
		/// .NET 10 Migration: Security.SetACLRoleAccess → _security.SetACLRoleAccess (instance).
		/// </summary>
		public void LoadACLRoles(Guid gUSER_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories.GetFactory(_memoryCache);
			using (IDbConnection con = dbf.CreateConnection())
			{
				con.Open();
				string sSQL = "select ROLE_NAME        " + ControlChars.CrLf
				            + "  from vwACL_ROLES_USERS" + ControlChars.CrLf
				            + " where USER_ID = @USER_ID" + ControlChars.CrLf;
				using (IDbCommand cmd = con.CreateCommand())
				{
					cmd.CommandText = sSQL;
					Sql.AddParameter(cmd, "@USER_ID", gUSER_ID);
					using (IDataReader rdr = cmd.ExecuteReader())
					{
						while (rdr.Read())
						{
							string sROLE_NAME = Sql.ToString(rdr["ROLE_NAME"]);
							_security.SetACLRoleAccess(sROLE_NAME);
						}
					}
				}
			}
		}

		// =====================================================================================
		// LoadTeams
		// =====================================================================================
		/// <summary>
		/// Loads team memberships for the user from vwTEAM_MEMBERSHIPS_List.
		/// .NET 10 Migration: Security.SetTeamAccess → _security.SetTeamAccess (instance).
		/// </summary>
		public void LoadTeams(Guid gUSER_ID)
		{
			DbProviderFactory dbf = _dbProviderFactories.GetFactory(_memoryCache);
			using (IDbConnection con = dbf.CreateConnection())
			{
				con.Open();
				string sSQL = "select TEAM_NAME                " + ControlChars.CrLf
				            + "  from vwTEAM_MEMBERSHIPS_List  " + ControlChars.CrLf
				            + " where USER_ID = @USER_ID       " + ControlChars.CrLf;
				using (IDbCommand cmd = con.CreateCommand())
				{
					cmd.CommandText = sSQL;
					Sql.AddParameter(cmd, "@USER_ID", gUSER_ID);
					using (IDataReader rdr = cmd.ExecuteReader())
					{
						while (rdr.Read())
						{
							string sTEAM_NAME = Sql.ToString(rdr["TEAM_NAME"]);
							_security.SetTeamAccess(sTEAM_NAME);
						}
					}
				}
			}
		}

		// =====================================================================================
		// LoginTracking, LoginFailures, InvalidIPAddress
		// =====================================================================================
		/// <summary>
		/// Tracks login success/failure for a user name in IMemoryCache.
		/// .NET 10 Migration: Application["Users.LoginFailures.*"] → _memoryCache.
		/// </summary>
		public void LoginTracking(string sUSER_NAME, bool bValidUser)
		{
			if (bValidUser)
			{
				_memoryCache.Remove("Users.LoginFailures." + sUSER_NAME);
			}
			else
			{
				int nLoginFailures = Sql.ToInteger(_memoryCache.Get("Users.LoginFailures." + sUSER_NAME));
				_memoryCache.Set("Users.LoginFailures." + sUSER_NAME, nLoginFailures + 1);
			}
		}

		/// <summary>Returns the number of consecutive login failures for a user.</summary>
		public int LoginFailures(string sUSER_NAME)
		{
			return Sql.ToInteger(_memoryCache.Get("Users.LoginFailures." + sUSER_NAME));
		}

		/// <summary>
		/// Returns true when sUserHostAddress is NOT in the permitted IP list stored in
		/// CONFIG.Authentication.IPAddresses.
		/// .NET 10 Migration: Application["CONFIG.Authentication.IPAddresses"] → _memoryCache.
		/// </summary>
		public bool InvalidIPAddress(string sUserHostAddress)
		{
			string sIPAddresses = Sql.ToString(_memoryCache.Get("CONFIG.Authentication.IPAddresses"));
			sIPAddresses = sIPAddresses.Replace(",", " ").Trim();
			if (!Sql.IsEmptyString(sIPAddresses))
			{
				foreach (string sValidIP in sIPAddresses.Split(' '))
				{
					if (sUserHostAddress == sValidIP)
						return false;
				}
				return true;
			}
			return false;
		}

		// =====================================================================================
		// VerifyUser
		// =====================================================================================
		/// <summary>
		/// Verifies credentials without creating a full session (used by DuoUniversal).
		/// Returns the user's Guid if valid, Guid.Empty otherwise.
		/// </summary>
		public Guid VerifyUser(string sUSER_NAME, string sPASSWORD, string sUSER_DOMAIN)
		{
			Guid gUSER_ID = Guid.Empty;
			DbProviderFactory dbf = _dbProviderFactories.GetFactory(_memoryCache);
			using (IDbConnection con = dbf.CreateConnection())
			{
				con.Open();
				string sSQL = "select *            " + ControlChars.CrLf
				            + "  from vwUSERS_Login" + ControlChars.CrLf;
				if (Sql.IsOracle(con) || Sql.IsDB2(con) || Sql.IsPostgreSQL(con))
					sSQL += " where lower(USER_NAME) = @USER_NAME" + ControlChars.CrLf;
				else
					sSQL += " where USER_NAME = @USER_NAME" + ControlChars.CrLf;
				using (IDbCommand cmd = con.CreateCommand())
				{
					cmd.CommandText    = sSQL;
					cmd.CommandTimeout = 0;
					Sql.AddParameter(cmd, "@USER_NAME", sUSER_NAME.ToLower());
					if (Sql.IsEmptyString(sUSER_DOMAIN))
					{
						if (!Sql.IsEmptyString(sPASSWORD))
						{
							string sUSER_HASH = Security.HashPassword(sPASSWORD);
							cmd.CommandText += "   and USER_HASH = @USER_HASH" + ControlChars.CrLf;
							Sql.AddParameter(cmd, "@USER_HASH", sUSER_HASH);
						}
						else
						{
							cmd.CommandText += "   and (USER_HASH = '' or USER_HASH is null)" + ControlChars.CrLf;
						}
					}
					using (IDataReader rdr = cmd.ExecuteReader())
					{
						if (rdr.Read())
							gUSER_ID = Sql.ToGuid(rdr["ID"]);
					}
				}
			}
			return gUSER_ID;
		}

		// =====================================================================================
		// LoginUser (primary string overload)
		// =====================================================================================
		/// <summary>
		/// Full login: authenticates by username/password or Windows domain credentials.
		/// .NET 10 Migration: HttpContext.Current.* → _httpContextAccessor.HttpContext.*;
		///                    Session["key"] = value → session.SetString(...);
		///                    Request.UserHostName → context.Connection.RemoteIpAddress;
		///                    Session.SessionID → session.Id.
		/// </summary>
		public bool LoginUser(string sUSER_NAME, string sPASSWORD, string sTHEME, string sLANGUAGE, string sUSER_DOMAIN, bool bIS_ADMIN)
		{
			var context = _httpContextAccessor?.HttpContext;
			var session = context?.Session;

			// .NET 10 Migration: Request.UserHostName → context.Connection.RemoteIpAddress
			string sRemoteHost   = context?.Connection?.RemoteIpAddress?.ToString() ?? String.Empty;
			// .NET 10 Migration: Request.Url.Host → context.Request.Host.Host
			string sServerHost   = context?.Request?.Host.Host ?? String.Empty;
			// .NET 10 Migration: Request.Path → context.Request.Path.Value
			string sTargetPath   = context?.Request?.Path.Value ?? String.Empty;
			string sRelativePath = sTargetPath;
			// .NET 10 Migration: Request.UserAgent → context.Request.Headers["User-Agent"]
			string sUserAgent    = context?.Request?.Headers["User-Agent"].ToString() ?? String.Empty;
			// .NET 10 Migration: Session.SessionID → session.Id
			string sSessionID    = session?.Id ?? String.Empty;

			bool bValidUser = false;
			DbProviderFactory dbf = _dbProviderFactories.GetFactory(_memoryCache);
			using (IDbConnection con = dbf.CreateConnection())
			{
				con.Open();
				string sSQL = "select *            " + ControlChars.CrLf
				            + "  from vwUSERS_Login" + ControlChars.CrLf;
				if (Sql.IsOracle(con) || Sql.IsDB2(con) || Sql.IsPostgreSQL(con))
					sSQL += " where lower(USER_NAME) = @USER_NAME" + ControlChars.CrLf;
				else
					sSQL += " where USER_NAME = @USER_NAME" + ControlChars.CrLf;
				using (IDbCommand cmd = con.CreateCommand())
				{
					cmd.CommandText    = sSQL;
					cmd.CommandTimeout = 0;
					Sql.AddParameter(cmd, "@USER_NAME", sUSER_NAME.ToLower());
					string sLOGIN_TYPE = "Windows";
					if (Sql.IsEmptyString(sUSER_DOMAIN))
					{
						sLOGIN_TYPE = "Anonymous";
						if (!Sql.IsEmptyString(sPASSWORD))
						{
							string sUSER_HASH = Security.HashPassword(sPASSWORD);
							cmd.CommandText += "   and USER_HASH = @USER_HASH" + ControlChars.CrLf;
							Sql.AddParameter(cmd, "@USER_HASH", sUSER_HASH);
						}
						else
						{
							cmd.CommandText += "   and (USER_HASH = '' or USER_HASH is null)" + ControlChars.CrLf;
						}
					}
					using (IDataReader rdr = cmd.ExecuteReader())
					{
						Guid gUSER_LOGIN_ID = Guid.Empty;
						if (rdr.Read())
						{
							_security.Clear();
							_security.USER_ID     = Sql.ToGuid   (rdr["ID"         ]);
							_security.USER_NAME   = Sql.ToString (rdr["USER_NAME"  ]);
							_security.FULL_NAME   = Sql.ToString (rdr["FULL_NAME"  ]);
							_security.IS_ADMIN    = Sql.ToBoolean(rdr["IS_ADMIN"   ]);
							_security.PORTAL_ONLY = Sql.ToBoolean(rdr["PORTAL_ONLY"]);
							try
							{
								_security.TEAM_ID   = Sql.ToGuid  (rdr["TEAM_ID"  ]);
								_security.TEAM_NAME = Sql.ToString(rdr["TEAM_NAME"]);
							}
							catch (Exception ex)
							{
								SplendidError.SystemMessage(context, "Error", new System.Diagnostics.StackTrace(true).GetFrame(0), "Failed to read TEAM_ID. " + ex.Message);
							}
							try
							{
								_security.IS_ADMIN_DELEGATE = Sql.ToBoolean(rdr["IS_ADMIN_DELEGATE"]);
							}
							catch (Exception ex)
							{
								SplendidError.SystemMessage(context, "Error", new System.Diagnostics.StackTrace(true).GetFrame(0), "Failed to read IS_ADMIN_DELEGATE. " + ex.Message);
							}
							try
							{
								_security.PRIMARY_ROLE_ID   = Sql.ToGuid  (rdr["PRIMARY_ROLE_ID"  ]);
								_security.PRIMARY_ROLE_NAME = Sql.ToString(rdr["PRIMARY_ROLE_NAME"]);
							}
							catch (Exception ex)
							{
								SplendidError.SystemMessage(context, "Error", new System.Diagnostics.StackTrace(true).GetFrame(0), "Failed to read PRIMARY_ROLE_NAME. " + ex.Message);
							}
							try
							{
								// 02/22/2011 Paul.  Password expiration only applies to Anonymous Authentication.
								if (sLOGIN_TYPE == "Anonymous" && session != null)
								{
									bool     bSYSTEM_GENERATED_PASSWORD = Sql.ToBoolean (rdr["SYSTEM_GENERATED_PASSWORD"]);
									DateTime dtPWD_LAST_CHANGED         = Sql.ToDateTime(rdr["PWD_LAST_CHANGED"         ]);
									// .NET 10 Migration: Crm.Password.ExpirationDays(_memoryCache)
									int nExpirationDays = Crm.Password.ExpirationDays(_memoryCache);
									if (nExpirationDays > 0)
									{
										if (dtPWD_LAST_CHANGED == DateTime.MinValue || dtPWD_LAST_CHANGED.AddDays(nExpirationDays) < DateTime.Now)
											bSYSTEM_GENERATED_PASSWORD = true;
									}
									session.SetString("SYSTEM_GENERATED_PASSWORD", bSYSTEM_GENERATED_PASSWORD.ToString());
								}
							}
							catch (Exception ex)
							{
								SplendidError.SystemMessage(context, "Error", new System.Diagnostics.StackTrace(true).GetFrame(0), "Failed to read SYSTEM_GENERATED_PASSWORD. " + ex.Message);
							}

							Guid gUSER_ID = Sql.ToGuid(rdr["ID"]);
							if (!Sql.IsEffiProz(con))
								SqlProcs.spUSERS_LOGINS_InsertOnly(ref gUSER_LOGIN_ID, gUSER_ID, sUSER_NAME, sLOGIN_TYPE, "Succeeded", sSessionID, sRemoteHost, sServerHost, sTargetPath, sRelativePath, sUserAgent);
							_security.USER_LOGIN_ID = gUSER_LOGIN_ID;
							LoginTracking(sUSER_NAME, true);

							LoadUserPreferences(context, gUSER_ID, sTHEME, String.Empty);
							LoadUserACL(gUSER_ID);
							LoadACLRoles(gUSER_ID);
							LoadTeams(gUSER_ID);
							bValidUser = true;
							SplendidError.SystemMessage(context, "Warning", new System.Diagnostics.StackTrace(true).GetFrame(0), "User login.");
						}
						else if (_security.IsWindowsAuthentication())
						{
							// NTLM / Windows-integrated user auto-create.
							rdr.Close();
							if (!bIS_ADMIN)
							{
								cmd.Parameters.Clear();
								cmd.CommandText = "select count(*) from vwUSERS where ID > '00000000-0000-0000-0000-0000000000ff'";
								int nUsers = Sql.ToInteger(cmd.ExecuteScalar());
								if (nUsers == 0)
									bIS_ADMIN = true;
							}
							Guid gUSER_ID = Guid.Empty;
							SqlProcs.spUSERS_InsertNTLM(ref gUSER_ID, sUSER_DOMAIN, sUSER_NAME, bIS_ADMIN);

							_security.Clear();
							_security.USER_ID     = gUSER_ID  ;
							_security.USER_NAME   = sUSER_NAME;
							_security.IS_ADMIN    = bIS_ADMIN ;
							_security.PORTAL_ONLY = false     ;

							if (!Sql.IsEffiProz(con))
								SqlProcs.spUSERS_LOGINS_InsertOnly(ref gUSER_LOGIN_ID, gUSER_ID, sUSER_NAME, sLOGIN_TYPE, "Succeeded", sSessionID, sRemoteHost, sServerHost, sTargetPath, sRelativePath, sUserAgent);
							_security.USER_LOGIN_ID = gUSER_LOGIN_ID;
							LoginTracking(sUSER_NAME, true);

							// Retrieve TEAM_ID / TEAM_NAME for the newly created NTLM user.
							cmd.Parameters.Clear();
							cmd.CommandText = "select TEAM_ID      " + ControlChars.CrLf
							                + "     , TEAM_NAME    " + ControlChars.CrLf
							                + "  from vwUSERS_Login" + ControlChars.CrLf
							                + " where ID = @ID     " + ControlChars.CrLf;
							Sql.AddParameter(cmd, "@ID", _security.USER_ID);
							using (IDataReader rdrTeam = cmd.ExecuteReader())
							{
								if (rdrTeam.Read())
								{
									try
									{
										_security.TEAM_ID   = Sql.ToGuid  (rdrTeam["TEAM_ID"  ]);
										_security.TEAM_NAME = Sql.ToString(rdrTeam["TEAM_NAME"]);
									}
									catch (Exception ex)
									{
										SplendidError.SystemMessage(context, "Error", new System.Diagnostics.StackTrace(true).GetFrame(0), "Failed to read TEAM_ID. " + ex.Message);
									}
								}
							}

							LoadUserPreferences(context, gUSER_ID, String.Empty, String.Empty);
							LoadUserACL(gUSER_ID);
							LoadACLRoles(gUSER_ID);
							LoadTeams(gUSER_ID);
							bValidUser = true;
							SplendidError.SystemMessage(context, "Warning", new System.Diagnostics.StackTrace(true).GetFrame(0), "User login.");
						}
						else
						{
							if (!Sql.IsEffiProz(con))
								SqlProcs.spUSERS_LOGINS_InsertOnly(ref gUSER_LOGIN_ID, Guid.Empty, sUSER_NAME, sLOGIN_TYPE, "Failed", sSessionID, sRemoteHost, sServerHost, sTargetPath, sRelativePath, sUserAgent);
							LoginTracking(sUSER_NAME, false);
							LoadUserPreferences(context, Guid.Empty, String.Empty, String.Empty);
						}
					}
				}
			}
			return bValidUser;
		}

		/// <summary>Simplified overload: calls full LoginUser with empty domain and false IS_ADMIN.</summary>
		public bool LoginUser(string sUSER_NAME, string sPASSWORD, string sTHEME, string sLANGUAGE)
		{
			return LoginUser(sUSER_NAME, sPASSWORD, sTHEME, sLANGUAGE, String.Empty, false);
		}

		/// <summary>
		/// Convenience overload: sets the Security context from a pre-fetched DataRow (e.g. from
		/// RestController or ImpersonationController) and loads ACL / preferences.
		/// The DataRow is expected to contain columns from vwUSERS_Login.
		/// .NET 10 Migration: new overload added to support callers that already have a DataRow.
		/// </summary>
		public void LoginUser(DataRow row)
		{
			if (row == null) throw new ArgumentNullException("row");
			var context = _httpContextAccessor?.HttpContext;
			var session = context?.Session;
			string sSessionID    = session?.Id ?? String.Empty;
			string sRemoteHost   = context?.Connection?.RemoteIpAddress?.ToString() ?? String.Empty;
			string sServerHost   = context?.Request?.Host.Host ?? String.Empty;
			string sTargetPath   = context?.Request?.Path.Value ?? String.Empty;
			string sRelativePath = sTargetPath;
			string sUserAgent    = context?.Request?.Headers["User-Agent"].ToString() ?? String.Empty;

			_security.Clear();
			_security.USER_ID     = Sql.ToGuid   (row["ID"         ]);
			_security.USER_NAME   = Sql.ToString (row["USER_NAME"  ]);
			_security.FULL_NAME   = Sql.ToString (row["FULL_NAME"  ]);
			_security.IS_ADMIN    = Sql.ToBoolean(row["IS_ADMIN"   ]);
			_security.PORTAL_ONLY = Sql.ToBoolean(row["PORTAL_ONLY"]);
			try
			{
				_security.TEAM_ID   = Sql.ToGuid  (row["TEAM_ID"  ]);
				_security.TEAM_NAME = Sql.ToString(row["TEAM_NAME"]);
			}
			catch {}
			try { _security.IS_ADMIN_DELEGATE = Sql.ToBoolean(row["IS_ADMIN_DELEGATE"]); } catch {}
			try
			{
				_security.PRIMARY_ROLE_ID   = Sql.ToGuid  (row["PRIMARY_ROLE_ID"  ]);
				_security.PRIMARY_ROLE_NAME = Sql.ToString(row["PRIMARY_ROLE_NAME"]);
			}
			catch {}

			DbProviderFactory dbf = _dbProviderFactories.GetFactory(_memoryCache);
			using (IDbConnection con = dbf.CreateConnection())
			{
				con.Open();
				if (!Sql.IsEffiProz(con))
				{
					Guid gUSER_LOGIN_ID = Guid.Empty;
					SqlProcs.spUSERS_LOGINS_InsertOnly(ref gUSER_LOGIN_ID, _security.USER_ID, _security.USER_NAME, "Anonymous", "Succeeded", sSessionID, sRemoteHost, sServerHost, sTargetPath, sRelativePath, sUserAgent);
					_security.USER_LOGIN_ID = gUSER_LOGIN_ID;
				}
			}

			LoginTracking(_security.USER_NAME, true);
			LoadUserPreferences(context, _security.USER_ID, String.Empty, String.Empty);
			LoadUserACL(_security.USER_ID);
			LoadACLRoles(_security.USER_ID);
			LoadTeams(_security.USER_ID);
			SplendidError.SystemMessage(context, "Warning", new System.Diagnostics.StackTrace(true).GetFrame(0), "User login.");
		}

		// =====================================================================================
		// LoginUser (Guid overload — impersonation)
		// =====================================================================================
		/// <summary>
		/// Logs in a user by their Guid ID. Used for admin impersonation flows.
		/// .NET 10 Migration: same session/context patterns as the string overload.
		/// </summary>
		public void LoginUser(Guid gUSER_ID, string sLOGIN_TYPE)
		{
			var context = _httpContextAccessor?.HttpContext;
			var session = context?.Session;

			string sRemoteHost   = context?.Connection?.RemoteIpAddress?.ToString() ?? String.Empty;
			string sServerHost   = context?.Request?.Host.Host ?? String.Empty;
			string sTargetPath   = context?.Request?.Path.Value ?? String.Empty;
			string sRelativePath = sTargetPath;
			string sUserAgent    = context?.Request?.Headers["User-Agent"].ToString() ?? String.Empty;
			string sSessionID    = session?.Id ?? String.Empty;

			DbProviderFactory dbf = _dbProviderFactories.GetFactory(_memoryCache);
			using (IDbConnection con = dbf.CreateConnection())
			{
				con.Open();
				string sSQL = "select *            " + ControlChars.CrLf
				            + "  from vwUSERS_Login" + ControlChars.CrLf
				            + " where ID = @ID     " + ControlChars.CrLf;
				using (IDbCommand cmd = con.CreateCommand())
				{
					cmd.CommandText    = sSQL;
					cmd.CommandTimeout = 0;
					Sql.AddParameter(cmd, "@ID", gUSER_ID);
					using (IDataReader rdr = cmd.ExecuteReader())
					{
						Guid gUSER_LOGIN_ID = Guid.Empty;
						if (rdr.Read())
						{
							string sUSER_NAME = Sql.ToString(rdr["USER_NAME"]);
							_security.Clear();
							_security.USER_ID     = Sql.ToGuid   (rdr["ID"         ]);
							_security.USER_NAME   = sUSER_NAME                      ;
							_security.FULL_NAME   = Sql.ToString (rdr["FULL_NAME"  ]);
							_security.IS_ADMIN    = Sql.ToBoolean(rdr["IS_ADMIN"   ]);
							_security.PORTAL_ONLY = Sql.ToBoolean(rdr["PORTAL_ONLY"]);
							try
							{
								_security.TEAM_ID   = Sql.ToGuid  (rdr["TEAM_ID"  ]);
								_security.TEAM_NAME = Sql.ToString(rdr["TEAM_NAME"]);
							}
							catch {}
							try
							{
								_security.IS_ADMIN_DELEGATE = Sql.ToBoolean(rdr["IS_ADMIN_DELEGATE"]);
							}
							catch {}
							try
							{
								_security.PRIMARY_ROLE_ID   = Sql.ToGuid  (rdr["PRIMARY_ROLE_ID"  ]);
								_security.PRIMARY_ROLE_NAME = Sql.ToString(rdr["PRIMARY_ROLE_NAME"]);
							}
							catch {}

							if (!Sql.IsEffiProz(con))
								SqlProcs.spUSERS_LOGINS_InsertOnly(ref gUSER_LOGIN_ID, gUSER_ID, sUSER_NAME, sLOGIN_TYPE, "Succeeded", sSessionID, sRemoteHost, sServerHost, sTargetPath, sRelativePath, sUserAgent);
							_security.USER_LOGIN_ID = gUSER_LOGIN_ID;
							LoginTracking(sUSER_NAME, true);

							LoadUserPreferences(context, gUSER_ID, String.Empty, String.Empty);
							LoadUserACL(gUSER_ID);
							LoadACLRoles(gUSER_ID);
							LoadTeams(gUSER_ID);
							SplendidError.SystemMessage(context, "Warning", new System.Diagnostics.StackTrace(true).GetFrame(0), "User login.");
						}
						else
						{
							throw new Exception("Users.ERR_INVALID_USER");
						}
					}
				}
			}
		}

		// =====================================================================================
		// FacebookLoginUser
		// =====================================================================================
		/// <summary>Authenticates a user by Facebook ID; delegates to LoginUser(Guid, string).</summary>
		public bool FacebookLoginUser(string sFACEBOOK_ID)
		{
			bool bValidUser = false;
			DbProviderFactory dbf = _dbProviderFactories.GetFactory(_memoryCache);
			using (IDbConnection con = dbf.CreateConnection())
			{
				con.Open();
				string sSQL = "select ID                        " + ControlChars.CrLf
				            + "  from vwUSERS_Login             " + ControlChars.CrLf
				            + " where FACEBOOK_ID = @FACEBOOK_ID" + ControlChars.CrLf;
				using (IDbCommand cmd = con.CreateCommand())
				{
					cmd.CommandText    = sSQL;
					cmd.CommandTimeout = 0;
					Sql.AddParameter(cmd, "@FACEBOOK_ID", sFACEBOOK_ID.ToLower());
					Guid gUSER_ID = Sql.ToGuid(cmd.ExecuteScalar());
					if (!Sql.IsEmptyGuid(gUSER_ID))
					{
						LoginUser(gUSER_ID, "facebook");
						bValidUser = true;
					}
				}
			}
			return bValidUser;
		}

		// =====================================================================================
		// LoginPortalUser
		// =====================================================================================
		/// <summary>Convenience overload without Facebook-login flag.</summary>
		public bool LoginPortalUser(string sUSER_NAME, string sPASSWORD, string sTHEME, string sLANGUAGE)
		{
			return LoginPortalUser(sUSER_NAME, sPASSWORD, sTHEME, sLANGUAGE, false);
		}

		/// <summary>
		/// Authenticates a contact (portal) user against vwCONTACTS_PortalLogin.
		/// .NET 10 Migration: same session/context patterns as LoginUser string overload.
		/// </summary>
		public bool LoginPortalUser(string sUSER_NAME, string sPASSWORD, string sTHEME, string sLANGUAGE, bool bFacebookLogin)
		{
			var context = _httpContextAccessor?.HttpContext;
			var session = context?.Session;

			string sRemoteHost   = context?.Connection?.RemoteIpAddress?.ToString() ?? String.Empty;
			string sServerHost   = context?.Request?.Host.Host ?? String.Empty;
			string sTargetPath   = context?.Request?.Path.Value ?? String.Empty;
			string sRelativePath = sTargetPath;
			string sUserAgent    = context?.Request?.Headers["User-Agent"].ToString() ?? String.Empty;
			string sSessionID    = session?.Id ?? String.Empty;

			bool bValidUser = false;
			DbProviderFactory dbf = _dbProviderFactories.GetFactory(_memoryCache);
			using (IDbConnection con = dbf.CreateConnection())
			{
				con.Open();
				string sSQL = "select ID                                   " + ControlChars.CrLf
				            + "     , PORTAL_NAME                          " + ControlChars.CrLf
				            + "     , FULL_NAME                            " + ControlChars.CrLf
				            + "     , TEAM_ID                              " + ControlChars.CrLf
				            + "     , TEAM_NAME                            " + ControlChars.CrLf
				            + "  from vwCONTACTS_PortalLogin               " + ControlChars.CrLf
				            + " where lower(PORTAL_NAME) = @PORTAL_NAME    " + ControlChars.CrLf;
				using (IDbCommand cmd = con.CreateCommand())
				{
					cmd.CommandText    = sSQL;
					cmd.CommandTimeout = 0;
					Sql.AddParameter(cmd, "@PORTAL_NAME", sUSER_NAME.ToLower());

					string sLOGIN_TYPE = "Anonymous";
					if (!bFacebookLogin)
					{
						cmd.CommandText += "   and PORTAL_PASSWORD = @PORTAL_PASSWORD" + ControlChars.CrLf;
						Sql.AddParameter(cmd, "@PORTAL_PASSWORD", Security.HashPassword(sPASSWORD));
					}
					using (IDataReader rdr = cmd.ExecuteReader())
					{
						Guid gUSER_LOGIN_ID = Guid.Empty;
						if (rdr.Read())
						{
							_security.Clear();
							Guid gUSER_ID_PORTAL = Sql.ToGuid  (rdr["ID"         ]);
							_security.USER_ID     = gUSER_ID_PORTAL;
							_security.USER_NAME   = Sql.ToString(rdr["PORTAL_NAME"]);
							_security.FULL_NAME   = Sql.ToString(rdr["FULL_NAME"  ]);
							_security.IS_ADMIN    = false;
							_security.PORTAL_ONLY = true;
							_security.TEAM_ID     = Sql.ToGuid  (rdr["TEAM_ID"   ]);
							_security.TEAM_NAME   = Sql.ToString(rdr["TEAM_NAME" ]);

							if (!Sql.IsEffiProz(con))
								SqlProcs.spUSERS_LOGINS_InsertOnly(ref gUSER_LOGIN_ID, gUSER_ID_PORTAL, sUSER_NAME, sLOGIN_TYPE, "Succeeded", sSessionID, sRemoteHost, sServerHost, sTargetPath, sRelativePath, sUserAgent);
							_security.USER_LOGIN_ID = gUSER_LOGIN_ID;
							LoginTracking(sUSER_NAME, true);

							LoadUserPreferences(context, gUSER_ID_PORTAL, sTHEME, String.Empty);
							LoadUserACL(gUSER_ID_PORTAL);
							LoadPortalACL();
							LoadACLRoles(gUSER_ID_PORTAL);
							LoadTeams(gUSER_ID_PORTAL);
							bValidUser = true;
							SplendidError.SystemMessage(context, "Warning", new System.Diagnostics.StackTrace(true).GetFrame(0), "User login.");
						}
						else
						{
							if (!Sql.IsEffiProz(con))
								SqlProcs.spUSERS_LOGINS_InsertOnly(ref gUSER_LOGIN_ID, Guid.Empty, sUSER_NAME, sLOGIN_TYPE, "Failed", sSessionID, sRemoteHost, sServerHost, sTargetPath, sRelativePath, sUserAgent);
							LoginTracking(sUSER_NAME, false);
							LoadUserPreferences(context, Guid.Empty, String.Empty, String.Empty);
						}
					}
				}
			}
			return bValidUser;
		}

		// =====================================================================================
		// ChangeTheme
		// =====================================================================================
		/// <summary>
		/// Updates the current session's theme and culture.
		/// .NET 10 Migration: HttpContext.Current.Session → _httpContextAccessor.HttpContext.Session;
		///                    L10N.AlternateLanguage(Application,...) → L10N.AlternateLanguage(_memoryCache,...).
		/// </summary>
		public void ChangeTheme(string sTHEME, string sLANGUAGE)
		{
			var session = _httpContextAccessor?.HttpContext?.Session;
			if (session == null) return;
			if (!Sql.IsEmptyString(sTHEME))
			{
				string sApplicationPath = Sql.ToString(_memoryCache.Get("rootURL"));
				session.SetString("USER_SETTINGS/THEME", sTHEME);
				session.SetString("themeURL"           , sApplicationPath + "App_Themes/" + sTHEME + "/");
			}
			if (!Sql.IsEmptyString(sLANGUAGE))
				// .NET 10 Migration: L10N.AlternateLanguage(Application,...) → L10N.AlternateLanguage(_memoryCache,...)
				session.SetString("USER_SETTINGS/CULTURE", L10N.AlternateLanguage(_memoryCache, sLANGUAGE));
		}

		// =====================================================================================
		// InitSession
		// =====================================================================================
		/// <summary>
		/// Session-start handler: detects browser/device from User-Agent string,
		/// sets default theme/culture, and performs Windows Authentication auto-login.
		///
		/// .NET 10 Migration:
		///   Context.Request.Browser.Browser/.IsMobileDevice/.Version → User-Agent string parsing
		///   (ASP.NET Core does not provide a Browser object; UA detection replaced by string checks)
		///   Context.Request.Browser["supportsPopups"]/["AjaxAutoComplete"] → removed
		///   Session["key"] = value → session.SetString(...)
		///   Context.Request.AppRelativeCurrentExecutionFilePath → context.Request.Path.Value
		///   Context.Request["Redirect"] → context.Request.Query["Redirect"]
		///   Utils.IsOfflineClient → false (not applicable in ASP.NET Core)
		/// </summary>
		public void InitSession()
		{
			InitAppURLs();
			var context = _httpContextAccessor?.HttpContext;
			if (context == null) return;

			try
			{
				var session = context.Session;
				string sApplicationPath = Sql.ToString(_memoryCache.Get("rootURL"));

				// ---- Browser / device detection via User-Agent ----
				// .NET 10 Migration: Context.Request.Browser is not available in ASP.NET Core.
				//                    User-Agent header parsed manually; defaults are set below and overridden per UA.
				string sUserAgent = context.Request.Headers["User-Agent"].ToString() ?? String.Empty;
				session.SetString("Browser"            , "Unknown");
				session.SetString("IsMobileDevice"     , "False"  );
				session.SetString("SupportsPopups"     , "True"   );
				session.SetString("AllowAutoComplete"  , "True"   );
				session.SetString("SupportsSpeech"     , "False"  );
				session.SetString("SupportsHandwriting", "False"  );
				session.SetString("SupportsTouch"      , "False"  );
				session.SetString("SupportsDraggable"  , "True"   );

				if (sUserAgent.Contains("Android"))
				{
					if (sUserAgent.Contains("Mobile"))
					{
						session.SetString("Browser"      , "Android Mobile");
						session.SetString("IsMobileDevice", "True"          );
					}
					else
					{
						session.SetString("Browser"      , "Android Tablet");
						session.SetString("IsMobileDevice", "False"         );
					}
					session.SetString("SupportsSpeech"     , "True");
					session.SetString("SupportsHandwriting", "True");
					session.SetString("SupportsTouch"      , "True");
				}
				else if (sUserAgent.Contains("BlackBerry"))
				{
					session.SetString("Browser"          , "BlackBerry");
					session.SetString("IsMobileDevice"   , "True"      );
					session.SetString("SupportsPopups"   , "False"     );
					session.SetString("AllowAutoComplete", "False"     );
				}
				else if (sUserAgent.Contains("iPad"))
				{
					session.SetString("Browser"            , "iPad" );
					session.SetString("IsMobileDevice"     , "False");
					session.SetString("SupportsSpeech"     , "True" );
					session.SetString("SupportsHandwriting", "True" );
					session.SetString("SupportsTouch"      , "True" );
				}
				else if (sUserAgent.Contains("iPhone"))
				{
					session.SetString("Browser"            , "iPhone");
					session.SetString("IsMobileDevice"     , "True"  );
					session.SetString("SupportsSpeech"     , "True"  );
					session.SetString("SupportsHandwriting", "True"  );
					session.SetString("SupportsTouch"      , "True"  );
				}
				else if (sUserAgent.Contains("iPod"))
				{
					session.SetString("Browser"            , "iPod" );
					session.SetString("IsMobileDevice"     , "True" );
					session.SetString("SupportsSpeech"     , "True" );
					session.SetString("SupportsHandwriting", "True" );
					session.SetString("SupportsTouch"      , "True" );
				}
				else if (sUserAgent.Contains("Opera Mini"))
				{
					session.SetString("Browser"          , "Opera Mini");
					session.SetString("IsMobileDevice"   , "True"      );
					session.SetString("SupportsPopups"   , "False"     );
					session.SetString("AllowAutoComplete", "False"     );
				}
				else if (sUserAgent.Contains("Palm"))
				{
					session.SetString("Browser"          , "Palm" );
					session.SetString("IsMobileDevice"   , "True" );
					session.SetString("SupportsPopups"   , "False");
					session.SetString("AllowAutoComplete", "False");
				}
				else if (sUserAgent.Contains("Chrome"))
				{
					session.SetString("SupportsSpeech"     , "True");
					session.SetString("SupportsHandwriting", "True");
				}
				else if (sUserAgent.Contains("Touch"))
				{
					session.SetString("SupportsTouch", "True");
				}
				// .NET 10 Migration: IE version check not applicable — IE is deprecated.
				// Context.Request.Browser.Browser / .Version not available in ASP.NET Core.

				string sTheme = SplendidDefaults.Theme();
				session.SetString("USER_SETTINGS/THEME"  , sTheme);
				session.SetString("USER_SETTINGS/CULTURE", SplendidDefaults.Culture());
				session.SetString("themeURL"             , sApplicationPath + "App_Themes/" + sTheme + "/");

				// .NET 10 Migration: Security.IsWindowsAuthentication() → _security.IsWindowsAuthentication()
				if (_security.IsWindowsAuthentication())
				{
					// .NET 10 Migration: Context.User.Identity.Name → context.User.Identity.Name (identical)
					string sIdentityName = context.User?.Identity?.Name ?? String.Empty;
					string[] arrUserName = sIdentityName.Split('\\');
					string sUSER_DOMAIN = String.Empty;
					string sUSER_NAME   = String.Empty;
					string sMACHINE     = String.Empty;
					try { sMACHINE = System.Environment.MachineName; } catch {}

					if (arrUserName.Length > 1)
					{
						sUSER_DOMAIN = arrUserName[0];
						sUSER_NAME   = arrUserName[1];
					}
					else
					{
						sUSER_DOMAIN = sMACHINE;
						sUSER_NAME   = arrUserName[0];
					}
					bool bIS_ADMIN = false;
					try
					{
						// .NET 10 Migration: Context.User.IsInRole(…) → context.User.IsInRole(…) (identical)
						bIS_ADMIN = context.User.IsInRole("BUILTIN\\Administrators")
						              || (!Sql.IsEmptyString(sUSER_DOMAIN) && context.User.IsInRole(sUSER_DOMAIN + "\\SplendidCRM Administrators"))
						              || (!Sql.IsEmptyString(sMACHINE    ) && context.User.IsInRole(sMACHINE     + "\\SplendidCRM Administrators"))
						              || (!Sql.IsEmptyString(sUSER_DOMAIN) && context.User.IsInRole(sUSER_DOMAIN + "\\Domain Admins"));
					}
					catch (Exception ex)
					{
						SplendidError.SystemMessage(context, "Warning", new System.Diagnostics.StackTrace(true).GetFrame(0), ex);
					}

					LoginUser(sUSER_NAME, String.Empty, String.Empty, String.Empty, sUSER_DOMAIN, bIS_ADMIN);

					// .NET 10 Migration: Context.Request["Redirect"] → context.Request.Query["Redirect"]
					string sRedirect = Sql.ToString(context.Request.Query["Redirect"].ToString());
					if (!Sql.IsEmptyString(sRedirect) && sRedirect.StartsWith("~/"))
					{
						sRedirect = sApplicationPath.TrimEnd('/') + sRedirect.Substring(1);
						context.Response.Redirect(sRedirect);
					}
					else if (bIS_ADMIN && Sql.IsEmptyString(_memoryCache.Get("CONFIG.Configurator.LastRun")))
					{
						// .NET 10 Migration: ReactOnlyUI mode – Configurator redirect preserved as root-relative URL.
						context.Response.Redirect(sApplicationPath.TrimEnd('/') + "/Administration/Configurator/");
					}
					// .NET 10 Migration: ReactOnlyUI – User Wizard redirect handled by the React SPA.
				}
				else
				{
					// 11/22/2005 Paul.  Assume portal user for unauthenticated sessions.
					_security.PORTAL_ONLY = true;
					LoadUserPreferences(context, Guid.Empty, String.Empty, String.Empty);
				}
			}
			catch (Exception ex)
			{
				SplendidError.SystemMessage(context, "Error", new System.Diagnostics.StackTrace(true).GetFrame(0), ex);
			}
		}

		// =====================================================================================
		// Application_OnError
		// =====================================================================================
		/// <summary>
		/// Unhandled application error handler.
		/// .NET 10 Migration:
		///   Server.GetLastError() → IExceptionHandlerFeature.Error (from DI / context features)
		///   HttpException (System.Web) → not available; handled via generic Exception
		///   HttpCompileException / CompilerErrorCollection → removed (WebForms compilation not applicable)
		///   Server.ClearError() → not needed; ASP.NET Core middleware pipeline clears errors
		///   Server.UrlEncode() → Uri.EscapeDataString()
		/// </summary>
		public void Application_OnError()
		{
			var context = _httpContextAccessor?.HttpContext;
			if (context == null) return;

			try
			{
				// .NET 10 Migration: Server.GetLastError() → IExceptionHandlerFeature.Error
				Exception ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;
				if (ex != null)
				{
					// Unwrap any inner exception.
					while (ex.InnerException != null)
						ex = ex.InnerException;

					string        sException = ex.GetType().Name;
					StringBuilder sbMessage  = new StringBuilder(ex.Message);

					if (ex is FileNotFoundException)
					{
						sbMessage = new StringBuilder("File Not Found");
					}
					// .NET 10 Migration: typeof(HttpException) not available in ASP.NET Core.
					// HTTP 403/404 status codes are handled by the ASP.NET Core pipeline directly.
					// typeof(HttpCompileException) removed — WebForms compilation is not applicable.

					SplendidError.SystemMessage(context, "Error", new System.Diagnostics.StackTrace(true).GetFrame(0), sbMessage.ToString());

					// .NET 10 Migration: Server.ClearError() not applicable in ASP.NET Core.
					// .NET 10 Migration: Server.UrlEncode() → Uri.EscapeDataString()
					string sApplicationPath = Sql.ToString(_memoryCache.Get("rootURL")).TrimEnd('/');
					string sQueryString = String.Format("aspxerrorpath={0}&Exception={1}&Message={2}",
						Uri.EscapeDataString(context.Request.Path.Value ?? String.Empty),
						Uri.EscapeDataString(sException),
						Uri.EscapeDataString(sbMessage.ToString()));
					context.Response.Redirect(sApplicationPath + "/Home/ServerError.aspx?" + sQueryString);
				}
			}
			catch { /* Swallow secondary errors in the error handler. */ }
		}
	}
}
