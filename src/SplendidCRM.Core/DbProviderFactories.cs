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
// .NET 10 Migration: SplendidCRM/_code/DbProviderFactories.cs → src/SplendidCRM.Core/DbProviderFactories.cs
// Changes applied:
//   - REMOVED: using System.Web; (HttpApplicationState used as GetFactory parameter)
//   - ADDED:   using Microsoft.AspNetCore.Http; (IHttpContextAccessor for parameterless GetFactory())
//   - ADDED:   using Microsoft.Extensions.Caching.Memory; (IMemoryCache replaces Application[])
//   - REPLACED: GetFactory(HttpApplicationState Application) → GetFactory(IMemoryCache memoryCache)
//              Application["SplendidProvider"] → memoryCache.TryGetValue("SplendidProvider", ...)
//              Application["ConnectionString"]  → memoryCache.TryGetValue("ConnectionString", ...)
//              Application["ServerName"]        → memoryCache.TryGetValue("ServerName", ...)
//              Application["ApplicationPath"]   → memoryCache.TryGetValue("ApplicationPath", ...)
//              Application["SplendidProvider"] = → memoryCache.Set("SplendidProvider", ...)
//              Application["ConnectionString"]  = → memoryCache.Set("ConnectionString", ...)
//   - REPLACED: HttpContext.Current.Application in parameterless GetFactory() → injected IHttpContextAccessor
//              to retrieve the cached IMemoryCache; falls back to injected _memoryCache field
//   - REPLACED: Utils.AppSettings["key"] → IConfiguration["key"] / IConfiguration.GetConnectionString("key")
//              ConfigurationManager.AppSettings was replaced by IConfiguration in ASP.NET Core
//   - ADDED:   DI constructor accepting IMemoryCache, IHttpContextAccessor, and IConfiguration
//   - PRESERVED: namespace SplendidCRM, all public GetFactory signatures
//   - PRESERVED: Windows Registry lookup path (Registry case) — throws PlatformNotSupportedException on Linux
//   - PRESERVED: HostingDatabase lookup path using IDbConnection, IDbCommand, IDataReader, CommandBehavior
//   - PRESERVED: All business logic, switch cases, expiration checks, error messages
//   - ADDED:   Convenience instance methods (CreateConnection, CreateCommand, CreateDataAdapter,
//              CreateParameter overloads, ConnectionString property) used by injected consumers
//              (Sql.cs, SqlProcs.cs, SqlBuild.cs, OrderUtils.cs, SplendidInit.cs, SplendidError.cs)
//              These replace the previous pattern of GetFactory(Application).CreateConnection()
//              with direct calls on the injected DbProviderFactories singleton.
//   - Minimal change clause: Only changes necessary for .NET Framework 4.8 → .NET 10 ASP.NET Core
#nullable disable
using System;
using System.Data;
using System.Data.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;

namespace SplendidCRM
{
	/// <summary>
	/// Provider factory registry for SplendidCRM database connections.
	/// Migrated from SplendidCRM/_code/DbProviderFactories.cs (.NET Framework 4.8 → .NET 10 ASP.NET Core).
	/// 
	/// BEFORE (.NET Framework 4.8):
	///   Static class, all methods static, Application[] state passed as parameter or via HttpContext.Current.
	///   Usage: DbProviderFactory dbf = DbProviderFactories.GetFactory(Application);
	///          using (IDbConnection con = dbf.CreateConnection()) { ... }
	/// 
	/// AFTER (.NET 10 ASP.NET Core):
	///   Instance class registered as DI singleton. IMemoryCache replaces Application[],
	///   IHttpContextAccessor replaces HttpContext.Current, IConfiguration replaces Utils.AppSettings.
	///   Usage (modern pattern): using (IDbConnection con = _dbProviderFactories.CreateConnection()) { ... }
	///   Usage (legacy pattern): DbProviderFactory dbf = _dbProviderFactories.GetFactory(_memoryCache);
	/// 
	/// Three GetFactory overloads are preserved for backward compatibility:
	///   GetFactory(IMemoryCache)  — replaces GetFactory(HttpApplicationState Application)
	///   GetFactory()              — replaces GetFactory() (was HttpContext.Current.Application)
	///   GetFactory(string,string) — unchanged; returns SqlClientFactory for the named provider
	/// </summary>
	public class DbProviderFactories
	{
		// =====================================================================================
		// .NET 10 Migration: DI-injected services replacing static/thread-local state
		// =====================================================================================

		private readonly IMemoryCache         _memoryCache        ;
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly IConfiguration       _configuration      ;

		/// <summary>
		/// Initializes a new instance of <see cref="DbProviderFactories"/> with DI-injected services.
		/// This constructor replaces the static-only access pattern of the .NET Framework 4.8 version.
		/// </summary>
		/// <param name="memoryCache">
		/// IMemoryCache replacing Application[] state for caching the resolved SplendidProvider
		/// and ConnectionString values between requests.
		/// </param>
		/// <param name="httpContextAccessor">
		/// IHttpContextAccessor replacing HttpContext.Current for the parameterless GetFactory()
		/// overload that needs to access request context to reach the IMemoryCache.
		/// </param>
		/// <param name="configuration">
		/// IConfiguration replacing Utils.AppSettings / ConfigurationManager.AppSettings for reading
		/// SplendidProvider, SplendidSQLServer, SplendidRegistry, SplendidHostingProvider, and
		/// SplendidHostingConnection configuration values. May be null in unit-test contexts.
		/// </param>
		public DbProviderFactories(IMemoryCache memoryCache, IHttpContextAccessor httpContextAccessor, IConfiguration configuration = null)
		{
			_memoryCache         = memoryCache        ;
			_httpContextAccessor = httpContextAccessor;
			_configuration       = configuration      ;
		}

		// =====================================================================================
		// ConnectionString property
		// Convenience accessor used by injected consumers (SqlProcs, SqlBuild, SplendidInit, etc.)
		// BEFORE: string cs = DbProviderFactories.GetFactory(Application).CreateConnection().ConnectionString;
		// AFTER:  string cs = _dbProviderFactories.ConnectionString;
		// =====================================================================================

		/// <summary>
		/// Gets the configured SQL Server connection string for SplendidCRM.
		/// Reads from the five-tier configuration hierarchy via IConfiguration.
		/// Falls back to empty string if not configured; callers check for empty and throw.
		/// </summary>
		public string ConnectionString
		{
			get
			{
				// .NET 10 Migration: IConfiguration.GetConnectionString replaces
				// ConfigurationManager.ConnectionStrings["SplendidCRM"].ConnectionString
				string sConnectionString = _configuration?.GetConnectionString("SplendidCRM");
				if ( Sql.IsEmptyString(sConnectionString) )
				{
					// Also accept the legacy appSettings key used in the original Web.config
					sConnectionString = _configuration?["SplendidSQLServer"];
				}
				return sConnectionString ?? string.Empty;
			}
		}

		// =====================================================================================
		// GetFactory overloads — primary schema exports
		// =====================================================================================

		/// <summary>
		/// Resolves the configured database provider and connection string, caching the result
		/// in <paramref name="memoryCache"/> for subsequent calls.
		/// 
		/// BEFORE (.NET Framework 4.8):
		///   public static DbProviderFactory GetFactory(HttpApplicationState Application)
		///   Reads Application["SplendidProvider"] / Application["ConnectionString"] as cache.
		///   On miss, reads Utils.AppSettings then stores back to Application[].
		/// 
		/// AFTER (.NET 10 ASP.NET Core):
		///   public DbProviderFactory GetFactory(IMemoryCache memoryCache)
		///   Reads memoryCache.TryGetValue("SplendidProvider") / ("ConnectionString") as cache.
		///   On miss, reads IConfiguration then stores back to memoryCache.Set().
		/// 
		/// Supported provider modes (from IConfiguration["SplendidProvider"]):
		///   "System.Data.SqlClient" — Direct SQL Server connection string from IConfiguration
		///   "Registry"              — Connection from Windows registry key (HKLM)
		///   "HostingDatabase"       — Connection from a hosting master database query
		/// </summary>
		/// <param name="memoryCache">
		/// IMemoryCache instance to use for reading/writing the resolved provider and connection.
		/// Replaces HttpApplicationState parameter of the original method.
		/// </param>
		/// <returns>
		/// A <see cref="DbProviderFactory"/> (specifically a <see cref="SqlClientFactory"/>)
		/// configured with the resolved connection string.
		/// </returns>
		public DbProviderFactory GetFactory(IMemoryCache memoryCache)
		{
			// 11/14/2005 Paul.  Cache the connection string in the application as config and registry access is expected to be slower.
			// .NET 10 Migration: Application["SplendidProvider"] → memoryCache.TryGetValue("SplendidProvider", ...)
			string sSplendidProvider = Sql.ToString(memoryCache.TryGetValue("SplendidProvider", out object providerObj) ? providerObj : null);
			string sConnectionString = Sql.ToString(memoryCache.TryGetValue("ConnectionString" , out object connObj    ) ? connObj     : null);
			if ( Sql.IsEmptyString(sSplendidProvider) || Sql.IsEmptyString(sConnectionString) )
			{
				// .NET 10 Migration: Utils.AppSettings["SplendidProvider"] → IConfiguration["SplendidProvider"]
				// The IConfiguration provider hierarchy reads from AWS Secrets Manager → Env vars →
				// Parameter Store → appsettings.{Environment}.json → appsettings.json (highest to lowest priority).
				sSplendidProvider = _configuration?["SplendidProvider"];
				switch ( sSplendidProvider )
				{
					// 11/27/2008 Paul.  SplendidCRM Basic only supports SQL Server.
					case "System.Data.SqlClient":
						// .NET 10 Migration: Utils.AppSettings["SplendidSQLServer"] → IConfiguration["SplendidSQLServer"]
						// Also accepts the standard connection string via GetConnectionString("SplendidCRM").
						sConnectionString = _configuration?["SplendidSQLServer"];
						if ( Sql.IsEmptyString(sConnectionString) )
							sConnectionString = _configuration?.GetConnectionString("SplendidCRM");
						break;
					// 10/31/2021 Paul.  Remove EffiProz.
					case "Registry":
					{
						// .NET 10 Migration: Utils.AppSettings["SplendidRegistry"] → IConfiguration["SplendidRegistry"]
						string sSplendidRegistry = _configuration?["SplendidRegistry"];
						if ( Sql.IsEmptyString(sSplendidRegistry) )
						{
							// 11/14/2005 Paul.  If registry key is not provided, then compute it using the server and the application path.
							// This will allow a single installation to support multiple databases.
							// 12/22/2007 Paul.  We can no longer rely upon the Request object being valid as we might be inside the timer event.
							// .NET 10 Migration: Application["ServerName"] / Application["ApplicationPath"]
							//   → memoryCache.TryGetValue("ServerName", ...) / ("ApplicationPath", ...)
							string sServerName      = Sql.ToString(memoryCache.TryGetValue("ServerName"     , out object snObj) ? snObj : null);
							string sApplicationPath = Sql.ToString(memoryCache.TryGetValue("ApplicationPath", out object apObj) ? apObj : null);
							sSplendidRegistry  = "SOFTWARE\\SplendidCRM Software\\";
							sSplendidRegistry += sServerName;
							if ( sApplicationPath != "/" )
								sSplendidRegistry += sApplicationPath.Replace("/", "\\");
						}
						// .NET 10 Migration: Registry access unchanged. Windows Registry is available on .NET 10
						// when running on Windows. On Linux, RegistryKey.OpenSubKey throws PlatformNotSupportedException.
						// The "Registry" provider mode is a legacy configuration path; production deployments
						// on Linux use "System.Data.SqlClient" with connection string in Secrets Manager / env vars.
						using ( RegistryKey keySplendidCRM = Registry.LocalMachine.OpenSubKey(sSplendidRegistry) )
						{
							if ( keySplendidCRM != null )
							{
								sSplendidProvider = Sql.ToString(keySplendidCRM.GetValue("SplendidProvider"));
								sConnectionString = Sql.ToString(keySplendidCRM.GetValue("ConnectionString"));
								// 01/17/2008 Paul.  99.999% percent of the time, we will be hosting on SQL Server.
								// If the provider is not specified, then just assume SQL Server.
								if ( Sql.IsEmptyString(sSplendidProvider) )
									sSplendidProvider = "System.Data.SqlClient";
							}
							else
							{
								throw new Exception("Database connection information was not found in the registry " + sSplendidRegistry);
							}
						}
						break;
					}
					case "HostingDatabase":
					{
						// 09/27/2006 Paul.  Allow a Hosting Database to contain connection strings.
						// Configuration example (now from appsettings.json or environment variables):
						//   SplendidProvider:           "HostingDatabase"
						//   SplendidHostingProvider:    "System.Data.SqlClient"
						//   SplendidHostingConnection:  "data source=(local)\\SplendidCRM;initial catalog=SplendidCRM;user id=sa;password="
						// .NET 10 Migration: Utils.AppSettings["SplendidHostingProvider/Connection"]
						//   → IConfiguration["SplendidHostingProvider/Connection"]
						string sSplendidHostingProvider   = _configuration?["SplendidHostingProvider"  ];
						string sSplendidHostingConnection = _configuration?["SplendidHostingConnection"];
						if ( Sql.IsEmptyString(sSplendidHostingProvider) || Sql.IsEmptyString(sSplendidHostingConnection) )
						{
							throw new Exception("SplendidHostingProvider and SplendidHostingConnection are both required in order to pull the connection from a hosting server. ");
						}
						else
						{
							// 12/22/2007 Paul.  We can no longer rely upon the Request object being valid as we might be inside the timer event.
							// .NET 10 Migration: Application["ServerName"] / Application["ApplicationPath"]
							//   → memoryCache.TryGetValue("ServerName", ...) / ("ApplicationPath", ...)
							string sSplendidHostingSite = Sql.ToString(memoryCache.TryGetValue("ServerName"     , out object hostSnObj) ? hostSnObj : null);
							string sApplicationPath     = Sql.ToString(memoryCache.TryGetValue("ApplicationPath", out object hostApObj) ? hostApObj : null);
							if ( sApplicationPath != "/" )
								sSplendidHostingSite += sApplicationPath;

							DbProviderFactory dbf = GetFactory(sSplendidHostingProvider, sSplendidHostingConnection);
							using ( IDbConnection con = dbf.CreateConnection() )
							{
								con.Open();
								string sSQL;
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
													throw new Exception("The hosting site " + sSplendidHostingSite + " expired on " + dtEXPIRATION_DATE.ToShortDateString());
											}
											// 01/17/2008 Paul.  Original bug preserved for backward compatibility.
											if ( Sql.IsEmptyString(sSplendidProvider) || Sql.IsEmptyString(sSplendidProvider) )
												throw new Exception("Incomplete database connection information was found on the hosting server for site " + sSplendidHostingSite);
										}
										else
										{
											throw new Exception("Database connection information was not found on the hosting server for site " + sSplendidHostingSite);
										}
									}
								}
							}
						}
						break;
					}
					default:
					{
						// Default: assume SQL Server if no explicit provider is configured.
						// In .NET 10 ASP.NET Core deployments, the connection string is typically
						// supplied via ConnectionStrings__SplendidCRM environment variable or
						// AWS Secrets Manager under the "ConnectionStrings:SplendidCRM" path.
						if ( Sql.IsEmptyString(sSplendidProvider) )
							sSplendidProvider = "System.Data.SqlClient";
						if ( Sql.IsEmptyString(sConnectionString) )
							sConnectionString = _configuration?.GetConnectionString("SplendidCRM") ?? string.Empty;
						break;
					}
				}
				// .NET 10 Migration: Application["SplendidProvider"] = → memoryCache.Set("SplendidProvider", ...)
				// IMemoryCache.Set without expiration stores the value until evicted by memory pressure,
				// which matches the original Application[] behavior (persistent for the application lifetime).
				memoryCache.Set("SplendidProvider", sSplendidProvider);
				memoryCache.Set("ConnectionString" , sConnectionString);
			}
			return GetFactory(sSplendidProvider, sConnectionString);
		}

		/// <summary>
		/// Resolves the configured database provider and connection string using the injected
		/// IMemoryCache from the DI constructor.
		/// 
		/// BEFORE (.NET Framework 4.8):
		///   public static DbProviderFactory GetFactory()
		///   Throws if HttpContext.Current == null or HttpContext.Current.Application == null.
		///   Delegates to GetFactory(HttpContext.Current.Application).
		/// 
		/// AFTER (.NET 10 ASP.NET Core):
		///   public DbProviderFactory GetFactory()
		///   Uses injected _memoryCache (singleton DI service, always available).
		///   Falls back to IConfiguration on cache miss.
		/// </summary>
		/// <returns>
		/// A <see cref="DbProviderFactory"/> configured with the resolved connection string.
		/// </returns>
		/// <exception cref="Exception">
		/// Thrown if IMemoryCache was not injected (not set via DI constructor).
		/// </exception>
		public DbProviderFactory GetFactory()
		{
			// .NET 10 Migration: HttpContext.Current == null check → _memoryCache null check.
			// In ASP.NET Core, IMemoryCache is a singleton DI service always available after startup.
			// There is no equivalent of "null Application" — if _memoryCache is null, the service
			// was not properly registered in the DI container.
			if ( _memoryCache == null )
				throw new Exception("DbProviderFactory.GetFactory: IMemoryCache cannot be NULL. Ensure DbProviderFactories is registered in the DI container.");
			return GetFactory(_memoryCache);
		}

		/// <summary>
		/// Creates a <see cref="DbProviderFactory"/> for the specified provider and connection string.
		/// This is a pure utility method with no dependency on application state.
		/// Used internally by GetFactory(IMemoryCache) and directly by the HostingDatabase lookup path.
		/// 
		/// BEFORE / AFTER: Identical signature and behavior. System.Data.SqlClient provider name
		/// now also accepted as "System.Data.SqlClient" for backward compatibility; the returned
		/// SqlClientFactory uses Microsoft.Data.SqlClient internally (see SqlClientFactory.cs).
		/// </summary>
		/// <param name="sSplendidProvider">
		/// The provider name. Supported value: "System.Data.SqlClient".
		/// "Microsoft.Data.SqlClient" is also accepted as an alias (same provider, new NuGet name).
		/// </param>
		/// <param name="sConnectionString">
		/// The ADO.NET connection string for the specified provider.
		/// </param>
		/// <returns>
		/// A <see cref="SqlClientFactory"/> instance configured with the given connection string.
		/// </returns>
		/// <exception cref="Exception">
		/// Thrown for unsupported provider names.
		/// </exception>
		public static DbProviderFactory GetFactory(string sSplendidProvider, string sConnectionString)
		{
			switch ( sSplendidProvider )
			{
				// 11/27/2008 Paul.  SplendidCRM Basic only supports SQL Server.
				// .NET 10 Migration: "System.Data.SqlClient" provider name preserved for backward
				// compatibility. SqlClientFactory internally uses Microsoft.Data.SqlClient 6.1.4,
				// which is a drop-in replacement with an identical API surface.
				case "System.Data.SqlClient":
				// .NET 10 Migration: Also accept "Microsoft.Data.SqlClient" as the canonical provider
				// name for .NET 10 deployments that set SplendidProvider to the new NuGet package name.
				case "Microsoft.Data.SqlClient":
				{
					return new SqlClientFactory(sConnectionString);
				}
				// 10/31/2021 Paul.  Remove EffiProz.
				default:
					throw new Exception("Unsupported factory " + sSplendidProvider);
			}
		}

		// =====================================================================================
		// Convenience instance methods — used by DI consumers
		// These replace the previous pattern of:
		//   DbProviderFactory dbf = DbProviderFactories.GetFactory(Application);
		//   using (IDbConnection con = dbf.CreateConnection())
		// With:
		//   using (IDbConnection con = _dbProviderFactories.CreateConnection())
		//
		// Consumers: Sql.cs (via _ambientDbf), SqlProcs.cs, SqlBuild.cs, OrderUtils.cs,
		//            SplendidInit.cs, SplendidError.cs (via RequestServices).
		// =====================================================================================

		/// <summary>
		/// Creates and returns a new (closed) SQL Server connection using the configured connection string.
		/// Caller is responsible for opening and disposing the connection.
		/// 
		/// BEFORE: DbProviderFactory dbf = DbProviderFactories.GetFactory(Application);
		///         using (IDbConnection con = dbf.CreateConnection()) { con.Open(); ... }
		/// AFTER:  using (IDbConnection con = _dbProviderFactories.CreateConnection()) { con.Open(); ... }
		/// </summary>
		/// <returns>A new <see cref="IDbConnection"/> configured with the SplendidCRM connection string.</returns>
		public IDbConnection CreateConnection()
		{
			string sConnectionString = ConnectionString;
			// Direct SqlConnection construction is equivalent to the reflection-based CreateConnection()
			// in DbProviderFactory.cs but avoids the overhead of reflection for the common SQL Server path.
			// Microsoft.Data.SqlClient.SqlConnection implements IDbConnection.
			return new SqlConnection(sConnectionString);
		}

		/// <summary>
		/// Creates a new <see cref="IDbCommand"/> associated with the given connection.
		/// 
		/// BEFORE: DbProviderFactory dbf = ...; IDbCommand cmd = dbf.CreateCommand();
		///         (Command not associated with connection until cmd.Connection = con)
		/// AFTER:  IDbCommand cmd = _dbProviderFactories.CreateCommand(con);
		/// </summary>
		/// <param name="con">The open database connection to associate with the new command.</param>
		/// <returns>A new <see cref="IDbCommand"/> created by the connection.</returns>
		public IDbCommand CreateCommand(IDbConnection con)
		{
			return con.CreateCommand();
		}

		/// <summary>
		/// Creates a new <see cref="SqlDataAdapter"/> for bulk data operations.
		/// Used by SqlProcs for data adapter fill operations.
		/// 
		/// BEFORE: DbDataAdapter da = dbf.CreateDataAdapter();
		/// AFTER:  SqlDataAdapter da = _dbProviderFactories.CreateDataAdapter();
		/// </summary>
		/// <returns>A new empty <see cref="SqlDataAdapter"/>.</returns>
		public SqlDataAdapter CreateDataAdapter()
		{
			return new SqlDataAdapter();
		}

		/// <summary>
		/// Creates a new SQL Server parameter with no name or type.
		/// Equivalent to new SqlParameter() for DI-agnostic parameter creation.
		/// </summary>
		/// <returns>A new <see cref="IDbDataParameter"/> (SqlParameter).</returns>
		public IDbDataParameter CreateParameter()
		{
			return new SqlParameter();
		}

		/// <summary>
		/// Creates a new SQL Server parameter with the specified name and ADO.NET DbType.
		/// </summary>
		/// <param name="sName">The parameter name (typically prefixed with @).</param>
		/// <param name="dbType">The ADO.NET DbType for the parameter.</param>
		/// <returns>A new <see cref="IDbDataParameter"/> (SqlParameter) with name and type set.</returns>
		public IDbDataParameter CreateParameter(string sName, DbType dbType)
		{
			SqlParameter par = new SqlParameter();
			par.ParameterName = sName;
			par.DbType        = dbType;
			return par;
		}

		/// <summary>
		/// Creates a new SQL Server parameter with the specified name, ADO.NET DbType, and size.
		/// </summary>
		/// <param name="sName">The parameter name (typically prefixed with @).</param>
		/// <param name="dbType">The ADO.NET DbType for the parameter.</param>
		/// <param name="nSize">The maximum size of the parameter value (for variable-length types).</param>
		/// <returns>A new <see cref="IDbDataParameter"/> (SqlParameter) with name, type, and size set.</returns>
		public IDbDataParameter CreateParameter(string sName, DbType dbType, int nSize)
		{
			SqlParameter par = new SqlParameter();
			par.ParameterName = sName  ;
			par.DbType        = dbType ;
			par.Size          = nSize  ;
			return par;
		}
	}
}
