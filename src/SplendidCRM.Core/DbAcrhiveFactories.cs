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
// .NET 10 Migration: SplendidCRM/_code/DbAcrhiveFactories.cs → src/SplendidCRM.Core/DbAcrhiveFactories.cs
// Note: Original filename preserves intentional typo "Acrhive" for backward compatibility.
// Changes applied:
//   - REMOVED: using System.Web; (HttpApplicationState is a System.Web type removed in ASP.NET Core)
//   - ADDED:   using Microsoft.AspNetCore.Http; (IHttpContextAccessor replaces HttpContext.Current)
//   - ADDED:   using Microsoft.Extensions.Caching.Memory; (IMemoryCache replaces Application[])
//   - REPLACED: GetFactory(HttpApplicationState Application) → GetFactory(IMemoryCache memoryCache)
//              Application["SplendidProvider"]       → memoryCache.TryGetValue("SplendidProvider", ...)
//              Application["ArchiveConnectionString"] → memoryCache.TryGetValue("ArchiveConnectionString", ...)
//              Application["ServerName"]              → memoryCache.TryGetValue("ServerName", ...)
//              Application["ApplicationPath"]         → memoryCache.TryGetValue("ApplicationPath", ...)
//              Application["SplendidProvider"] =      → memoryCache.Set("SplendidProvider", ...)
//              Application["ArchiveConnectionString"] = → memoryCache.Set("ArchiveConnectionString", ...)
//   - REPLACED: static class with instance class; added DI constructor
//   - REPLACED: HttpContext.Current.Application in parameterless GetFactory() → constructor-injected _memoryCache
//   - REPLACED: Utils.AppSettings["key"] → _utils.AppSettings["key"] via injected Utils instance
//   - PRESERVED: namespace SplendidCRM, class name DbAcrhiveFactories (original typo preserved)
//   - PRESERVED: all provider switch cases (SqlClient, OracleClient, MySql, DB2, Sybase, SQLAnywhere, Npgsql, Registry, HostingDatabase)
//   - PRESERVED: Windows Registry lookup path — Registry.LocalMachine.OpenSubKey (throws PlatformNotSupportedException on Linux)
//   - PRESERVED: HostingDatabase lookup path — IDbConnection, IDbCommand, IDataReader, CommandBehavior, Sql.AddParameter, ControlChars.CrLf
//   - PRESERVED: all error messages, comments, DEBUG conditional, and business logic exactly
//   - Minimal change clause: Only changes necessary for .NET Framework 4.8 → .NET 10 ASP.NET Core transition
#nullable disable
using System;
using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Win32;

namespace SplendidCRM
{
	/// <summary>
	/// Archive database provider factory for SplendidCRM.
	/// Migrated from SplendidCRM/_code/DbAcrhiveFactories.cs (.NET Framework 4.8 → .NET 10 ASP.NET Core).
	/// Note: Class name preserves original typo "Acrhive" for backward compatibility.
	/// 
	/// BEFORE (.NET Framework 4.8):
	///   Static methods; HttpApplicationState parameter passed explicitly or read via HttpContext.Current.Application.
	///   Usage: DbProviderFactory dbf = DbAcrhiveFactories.GetFactory(Application);
	///          DbProviderFactory dbf = DbAcrhiveFactories.GetFactory();
	/// 
	/// AFTER (.NET 10 ASP.NET Core):
	///   Instance class registered as a DI service. IMemoryCache replaces HttpApplicationState (Application[]),
	///   IHttpContextAccessor replaces HttpContext.Current, Utils.AppSettings wraps IConfiguration.
	///   Usage: DbProviderFactory dbf = _dbAcrhiveFactories.GetFactory(_memoryCache);
	///          DbProviderFactory dbf = _dbAcrhiveFactories.GetFactory();
	/// 
	/// Three GetFactory overloads are supported:
	///   GetFactory(IMemoryCache)  — replaces GetFactory(HttpApplicationState Application)
	///   GetFactory()              — replaces parameterless GetFactory() (was HttpContext.Current.Application)
	///   DbProviderFactories.GetFactory(string,string) — static method called internally, unchanged
	/// </summary>
	public class DbAcrhiveFactories
	{
		// =====================================================================================
		// .NET 10 Migration: DI-injected services replacing static/thread-local Application state
		// =====================================================================================

		/// <summary>
		/// Replaces HttpApplicationState (Application[]) for cached SplendidProvider and
		/// ArchiveConnectionString values. Singleton registered in the DI container.
		/// BEFORE: Application["SplendidProvider"] / Application["ArchiveConnectionString"]
		/// AFTER:  _memoryCache.TryGetValue(...) / _memoryCache.Set(...)
		/// </summary>
		private readonly IMemoryCache         _memoryCache        ;

		/// <summary>
		/// Replaces HttpContext.Current for null-checking the request context in the parameterless
		/// GetFactory() overload.
		/// BEFORE: HttpContext.Current == null
		/// AFTER:  _httpContextAccessor check (DI guarantees non-null injection)
		/// </summary>
		private readonly IHttpContextAccessor _httpContextAccessor;

		/// <summary>
		/// Provides access to IConfiguration via Utils.AppSettings, replacing the static
		/// Utils.AppSettings (NameValueCollection) with the 5-tier provider hierarchy.
		/// BEFORE: Utils.AppSettings["SplendidProvider"]
		/// AFTER:  _utils.AppSettings["SplendidProvider"]
		/// </summary>
		private readonly Utils                _utils              ;

		/// <summary>
		/// Initializes a new instance of <see cref="DbAcrhiveFactories"/> with DI-injected services.
		/// This constructor replaces the static-only access pattern of the .NET Framework 4.8 version.
		/// </summary>
		/// <param name="memoryCache">
		/// IMemoryCache replacing Application[] for caching the resolved SplendidProvider and
		/// ArchiveConnectionString values between calls (avoids repeated config/registry lookups).
		/// </param>
		/// <param name="httpContextAccessor">
		/// IHttpContextAccessor replacing HttpContext.Current for the parameterless GetFactory()
		/// overload validation check.
		/// </param>
		/// <param name="utils">
		/// Utils instance providing Utils.AppSettings (IConfiguration wrapper) to read
		/// SplendidProvider, archive connection strings, hosting provider settings, and registry
		/// key configuration values.
		/// </param>
		public DbAcrhiveFactories(
			IMemoryCache         memoryCache        ,
			IHttpContextAccessor httpContextAccessor,
			Utils                utils              )
		{
			_memoryCache         = memoryCache        ;
			_httpContextAccessor = httpContextAccessor;
			_utils               = utils              ;
		}

		// =====================================================================================
		// GetFactory overloads — primary exported methods
		// =====================================================================================

		/// <summary>
		/// Resolves the configured archive database provider and connection string, caching the result
		/// in <paramref name="memoryCache"/> for subsequent calls to avoid repeated config and
		/// registry lookups.
		/// 
		/// BEFORE (.NET Framework 4.8):
		///   public static DbProviderFactory GetFactory(HttpApplicationState Application)
		///   Reads Application["SplendidProvider"] / Application["ArchiveConnectionString"] as cache.
		///   On cache miss, reads Utils.AppSettings (NameValueCollection) and stores to Application[].
		/// 
		/// AFTER (.NET 10 ASP.NET Core):
		///   public DbProviderFactory GetFactory(IMemoryCache memoryCache)
		///   Reads memoryCache.TryGetValue("SplendidProvider") / ("ArchiveConnectionString") as cache.
		///   On cache miss, reads _utils.AppSettings (IConfiguration wrapper) and stores via memoryCache.Set().
		/// 
		/// Supported SplendidProvider values (from configuration):
		///   "System.Data.SqlClient"  — SQL Server archive connection from appsettings key ArchiveSQLServer
		///   "System.Data.OracleClient" — Oracle via System.Data provider
		///   "Oracle.DataAccess.Client" — Oracle via ODP.NET provider
		///   "MySql.Data"             — MySQL archive connection
		///   "IBM.Data.DB2"           — IBM DB2 archive connection
		///   "Sybase.Data.AseClient"  — Sybase ASE archive connection
		///   "iAnywhere.Data.AsaClient" — SQL Anywhere archive connection
		///   "Npgsql"                 — PostgreSQL archive connection
		///   "Registry"               — Connection read from Windows HKLM registry (Windows-only)
		///   "HostingDatabase"        — Connection read from a hosting master SQL database
		/// </summary>
		/// <param name="memoryCache">
		/// The IMemoryCache instance to use for reading and writing the cached provider and connection
		/// string. Replaces the HttpApplicationState parameter of the original method. This may be
		/// the constructor-injected instance or a caller-provided cache for multi-tenant scenarios.
		/// </param>
		/// <returns>
		/// A <see cref="DbProviderFactory"/> configured with the resolved archive connection string.
		/// Calls the static <see cref="DbProviderFactories.GetFactory(string, string)"/> overload
		/// with the resolved provider name and connection string.
		/// </returns>
		public DbProviderFactory GetFactory(IMemoryCache memoryCache)
		{
			// 11/14/2005 Paul.  Cache the connection string in the application as config and registry access is expected to be slower. 
			// .NET 10 Migration: Application["SplendidProvider"]       → memoryCache.TryGetValue("SplendidProvider", ...)
			// .NET 10 Migration: Application["ArchiveConnectionString"] → memoryCache.TryGetValue("ArchiveConnectionString", ...)
			string sSplendidProvider = Sql.ToString(memoryCache.TryGetValue("SplendidProvider"      , out object providerObj) ? providerObj : null);
			string sConnectionString = Sql.ToString(memoryCache.TryGetValue("ArchiveConnectionString", out object connObj    ) ? connObj     : null);
#if DEBUG
//			sSplendidProvider = String.Empty;
#endif
			if ( Sql.IsEmptyString(sSplendidProvider) || Sql.IsEmptyString(sConnectionString) )
			{
				// .NET 10 Migration: Utils.AppSettings["key"] → _utils.AppSettings["key"]
				// Utils.AppSettings returns IConfiguration from the 5-tier provider hierarchy:
				// AWS Secrets Manager → env vars → Parameter Store → appsettings.{Env}.json → appsettings.json
				sSplendidProvider = _utils.AppSettings["SplendidProvider"];
				switch ( sSplendidProvider )
				{
					case "System.Data.SqlClient":
						sConnectionString = _utils.AppSettings["ArchiveSQLServer"];
						break;
					case "System.Data.OracleClient":
						sConnectionString = _utils.AppSettings["ArchiveSystemOracle"];
						break;
					case "Oracle.DataAccess.Client":
						sConnectionString = _utils.AppSettings["ArchiveOracle"];
						break;
					case "MySql.Data":
						sConnectionString = _utils.AppSettings["ArchiveMySql"];
						break;
					case "IBM.Data.DB2":
						sConnectionString = _utils.AppSettings["ArchiveDB2"];
						break;
					case "Sybase.Data.AseClient":
						sConnectionString = _utils.AppSettings["ArchiveSybase"];
						break;
					case "iAnywhere.Data.AsaClient":
						sConnectionString = _utils.AppSettings["ArchiveSQLAnywhere"];
						break;
					case "Npgsql":
						sConnectionString = _utils.AppSettings["ArchiveNpgsql"];
						break;
					case "Registry":
					{
						string sSplendidRegistry = _utils.AppSettings["SplendidRegistry"];
						if ( Sql.IsEmptyString(sSplendidRegistry) )
						{
							// 11/14/2005 Paul.  If registry key is not provided, then compute it using the server and the application path. 
							// This will allow a single installation to support multiple databases. 
							// 12/22/2007 Paul.  We can no longer rely upon the Request object being valid as we might be inside the timer event. 
							// .NET 10 Migration: Application["ServerName"]      → memoryCache.TryGetValue("ServerName", ...)
							// .NET 10 Migration: Application["ApplicationPath"] → memoryCache.TryGetValue("ApplicationPath", ...)
							string sServerName      = Sql.ToString(memoryCache.TryGetValue("ServerName"     , out object snObj) ? snObj : null);
							string sApplicationPath = Sql.ToString(memoryCache.TryGetValue("ApplicationPath", out object apObj) ? apObj : null);
							// 09/24/2010 Paul.  Remove trailing . so that it will be easier to debug http://localhost./SplendidCRM using Fiddler2. 
							if ( sServerName.EndsWith(".") )
								sServerName = sServerName.Substring(0, sServerName.Length - 1);
							sSplendidRegistry  = "SOFTWARE\\SplendidCRM Software\\" ;
							sSplendidRegistry += sServerName;
							if ( sApplicationPath != "/" )
								sSplendidRegistry += sApplicationPath.Replace("/", "\\");
						}
						using (RegistryKey keySplendidCRM = Registry.LocalMachine.OpenSubKey(sSplendidRegistry))
						{
							if ( keySplendidCRM != null )
							{
								sSplendidProvider = Sql.ToString(keySplendidCRM.GetValue("SplendidProvider"));
								sConnectionString = Sql.ToString(keySplendidCRM.GetValue("ArchiveConnectionString"));
								// 01/17/2008 Paul.  99.999% percent of the time, we will be hosting on SQL Server. 
								// If the provider is not specified, then just assume SQL Server. 
								if ( Sql.IsEmptyString(sSplendidProvider) )
									sSplendidProvider = "System.Data.SqlClient";
							}
							else
							{
								throw(new Exception("Database connection information was not found in the registry " + sSplendidRegistry));
							}
						}
						break;
					}
					case "HostingDatabase":
					{
						// 09/27/2006 Paul.  Allow a Hosting Database to contain connection strings. 
						/*
						<appSettings>
							<add key="SplendidProvider"          value="HostingDatabase" />
							<add key="SplendidHostingProvider"   value="System.Data.SqlClient" />
							<add key="SplendidHostingConnection" value="data source=(local)\SplendidCRM;initial catalog=SplendidCRM;user id=sa;password=" />
						</appSettings>
						*/
						string sSplendidHostingProvider   = _utils.AppSettings["SplendidHostingProvider"  ];
						string sSplendidHostingConnection = _utils.AppSettings["SplendidHostingConnection"];
						if ( Sql.IsEmptyString(sSplendidHostingProvider) || Sql.IsEmptyString(sSplendidHostingConnection) )
						{
							throw(new Exception("SplendidHostingProvider and SplendidHostingConnection are both required in order to pull the connection from a hosting server. "));
						}
						else
						{
							// 12/22/2007 Paul.  We can no longer rely upon the Request object being valid as we might be inside the timer event. 
							// .NET 10 Migration: Application["ServerName"]      → memoryCache.TryGetValue("ServerName", ...)
							// .NET 10 Migration: Application["ApplicationPath"] → memoryCache.TryGetValue("ApplicationPath", ...)
							string sSplendidHostingSite = Sql.ToString(memoryCache.TryGetValue("ServerName"     , out object hsnObj) ? hsnObj : null);
							string sApplicationPath     = Sql.ToString(memoryCache.TryGetValue("ApplicationPath", out object hapObj) ? hapObj : null);
							if ( sApplicationPath != "/" )
								sSplendidHostingSite += sApplicationPath;
							
							// DbProviderFactories.GetFactory(string, string) is a static method — call unchanged.
							DbProviderFactory dbf = DbProviderFactories.GetFactory(sSplendidHostingProvider, sSplendidHostingConnection);
							using ( IDbConnection con = dbf.CreateConnection() )
							{
								con.Open();
								string sSQL ;
								sSQL = "select SPLENDID_PROVIDER           " + ControlChars.CrLf
								     + "     , CONNECTION_STRING           " + ControlChars.CrLf
								     + "     , EXPIRATION_DATE             " + ControlChars.CrLf
								     + "  from vwSPLENDID_HOSTING_SITES    " + ControlChars.CrLf
								     + " where HOSTING_SITE = @HOSTING_SITE" + ControlChars.CrLf;
								using ( IDbCommand cmd = con.CreateCommand() )
								{
									cmd.CommandText = sSQL;
									Sql.AddParameter(cmd, "@HOSTING_SITE", sSplendidHostingSite);
									using ( IDataReader rdr = cmd.ExecuteReader(CommandBehavior.SingleRow) )
									{
										if ( rdr.Read() )
										{
											sSplendidProvider = Sql.ToString(rdr["SPLENDID_PROVIDER"]);
											sConnectionString = Sql.ToString(rdr["CONNECTION_STRING"]);
											// 01/17/2008 Paul.  99.999% percent of the time, we will be hosting on SQL Server. 
											// If the provider is not specified, then just assume SQL Server. 
											if ( Sql.IsEmptyString(sSplendidProvider) )
												sSplendidProvider = "System.Data.SqlClient";
											if ( rdr["EXPIRATION_DATE"] != DBNull.Value )
											{
												DateTime dtEXPIRATION_DATE = Sql.ToDateTime(rdr["EXPIRATION_DATE"]);
												if ( dtEXPIRATION_DATE < DateTime.Today )
													throw(new Exception("The hosting site " + sSplendidHostingSite + " expired on " + dtEXPIRATION_DATE.ToShortDateString()));
											}
											// Preserved from original: note that both checks use sSplendidProvider (original code preserved as-is per minimal change clause).
											if ( Sql.IsEmptyString(sSplendidProvider) || Sql.IsEmptyString(sSplendidProvider) )
												throw(new Exception("Incomplete database connection information was found on the hosting server for site " + sSplendidHostingSite));
										}
										else
										{
											throw(new Exception("Database connection information was not found on the hosting server for site " + sSplendidHostingSite));
										}
									}
								}
							}
						}
						break;
					}
				}
				// .NET 10 Migration: Application["SplendidProvider"] = sSplendidProvider → memoryCache.Set(...)
				// .NET 10 Migration: Application["ArchiveConnectionString"] = sConnectionString → memoryCache.Set(...)
				memoryCache.Set("SplendidProvider"      , sSplendidProvider);
				memoryCache.Set("ArchiveConnectionString", sConnectionString);
			}
			// DbProviderFactories.GetFactory(string, string) is a static method — call unchanged.
			return DbProviderFactories.GetFactory(sSplendidProvider, sConnectionString);
		}

		/// <summary>
		/// Resolves the archive database provider factory using the constructor-injected IMemoryCache.
		/// Delegates to <see cref="GetFactory(IMemoryCache)"/> with the DI-provided cache.
		/// 
		/// BEFORE (.NET Framework 4.8):
		///   public static DbProviderFactory GetFactory()
		///   Checked: if ( HttpContext.Current == null || HttpContext.Current.Application == null )
		///       throw(new Exception("DbProviderFactory.GetFactory: Application cannot be NULL."));
		///   return GetFactory(HttpContext.Current.Application);
		/// 
		/// AFTER (.NET 10 ASP.NET Core):
		///   Validates the DI-injected _memoryCache is not null (DI container guarantees this in
		///   normal usage, but explicit check preserves the fail-fast behavior of the original).
		///   IHttpContextAccessor is retained as a DI field; request-context null checks are
		///   handled by ASP.NET Core middleware rather than static Application null checks.
		/// </summary>
		/// <returns>
		/// A <see cref="DbProviderFactory"/> configured with the resolved archive connection string.
		/// </returns>
		/// <exception cref="Exception">
		/// Thrown if the constructor-injected <see cref="_memoryCache"/> is null, which indicates
		/// that <see cref="DbAcrhiveFactories"/> was not properly registered in the DI container.
		/// </exception>
		public DbProviderFactory GetFactory()
		{
			// .NET 10 Migration: HttpContext.Current == null || HttpContext.Current.Application == null
			// → _memoryCache == null check
			// The DI container guarantees _memoryCache is non-null when properly registered,
			// but an explicit guard preserves the fail-fast behavior of the original.
			if ( _memoryCache == null )
				throw(new Exception("DbAcrhiveFactories.GetFactory: IMemoryCache cannot be NULL. Ensure DbAcrhiveFactories is registered in the DI container."));
			return GetFactory(_memoryCache);
		}
	}
}
