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
// .NET 10 Migration: SplendidCRM/_code/SqlBuild.cs → src/SplendidCRM.Core/SqlBuild.cs
// Changes applied:
//   - REMOVED: using System.Web; (HttpContext.Server.MapPath, HttpApplicationState)
//   - ADDED:   using Microsoft.AspNetCore.Http; using Microsoft.AspNetCore.Hosting;
//              using Microsoft.Extensions.Caching.Memory;
//   - REPLACED: Context.Server.MapPath("~/App_Data/Build.log") and Context.Server.MapPath("~/app_offline.htm")
//              → IWebHostEnvironment.ContentRootPath + Path.Combine
//              (IWebHostEnvironment obtained via Context.RequestServices service locator pattern)
//   - REPLACED: DbProviderFactories.GetFactory(Context.Application) [was static call on HttpApplicationState]
//              → DbProviderFactories instance obtained from Context.RequestServices,
//                then instance.GetFactory(IMemoryCache) with IMemoryCache from Context.RequestServices
//   - REPLACED: Microsoft.VisualBasic.Strings.Split → Strings.Split (from src/SplendidCRM.Core/VisualBasic.cs)
//   - REPLACED: Microsoft.VisualBasic.CompareMethod.Text → CompareMethod.Text (from VisualBasic.cs)
//   - REPLACED: Context.Response.Write(sOfflineHtml) 
//              → Context.Response.WriteAsync(sOfflineHtml).GetAwaiter().GetResult()
//   - REPLACED: Context.Response.Flush()
//              → Context.Response.Body.FlushAsync().GetAwaiter().GetResult()
//   - PRESERVED: All idempotent DDL build logic, progress tracking, error handling, app_offline.htm lifecycle
//   - PRESERVED: namespace SplendidCRM, public class SqlBuild, public nested class BuildState,
//              public static BuildDatabase(HttpContext), sProgressTemplate, sErrorTemplate constants
//   - PRESERVED: %0 placeholder in sErrorTemplate (original code behavior — String.Format does not replace %0,
//              resulting in literal %0 in error output; preserved as-is per minimal change clause)
//   - Minimal change clause: Only changes necessary for .NET Framework 4.8 → .NET 10 ASP.NET Core
#nullable disable
using System;
using System.IO;
using System.Text;
using System.Data;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	// 08/01/2015 Paul.  The Microsoft Web Platform Installer is unable to deploy due to a timeout when applying the Build.sql file. 
	// Increasing the timeout in the Manifest.xml does not solve the problem. 
	// 06/30/2018 Paul.  Move SqlBuild to separate file. 
	public class SqlBuild
	{
		// http://stackoverflow.com/questions/3773857/escape-curly-brace-in-string-format
		protected const string sProgressTemplate = @"
<html>
<head>
<style type=""text/css"">
.ProgressBarFrame {{ padding: 2px; border: 1px solid #cccccc; width: 60%; background-color: #ffffff; }}
.ProgressBar      {{ background-color: #000000; }}
.ProgressBar td   {{ color: #ffffff; font-size: 12px; font-style: normal; font-weight: normal; text-decoration: none; }}
.QuestionError    {{ color: #e00000; font-size: 11px; font-style: normal; font-weight: bold; text-decoration: none; background-color: inherit; }}
</style>
</head>
<script type=""text/javascript"">
setTimeout(function()
{{
	location.reload();
}}, 3000);
</script>
<body>
The SplendidCRM database is being built.
<div class=""ProgressBarFrame"" align=""left"">
	<table cellspacing=""0"" width=""100%"" class=""ProgressBar"" style=""width: {0}%;"">
		<tbody class=""ProgressBar"">
			<tr>
				<td align=""center"" style=""padding: 2px;"">{1}%</td>
			</tr>
		</tbody>
	</table>
</div>
<div class=""QuestionError"">{2}</div>
<pre>{3}</pre>
</body>
</html>";
		protected const string sErrorTemplate = @"<html>
<head>
<style type=""text/css"">
</style>
</head>
<body>
There were errors during the SplendidCRM database build process. 
To manually enable SplendidCRM, you will need to delete the app_offline.htm file at the root of the web site. 
<pre>%0</pre>
</body>
</html>";

		/// <summary>
		/// Nested class that executes the DDL batch statements from Build.sql against the database.
		/// Tracks progress by writing an app_offline.htm progress page and logs errors to Build.log.
		/// Migrated from SplendidCRM/_code/SqlBuild.cs BuildState inner class.
		/// </summary>
		public class BuildState
		{
			// .NET 10 Migration: HttpContext type unchanged — now from Microsoft.AspNetCore.Http
			// BEFORE: using System.Web; System.Web.HttpContext
			// AFTER:  using Microsoft.AspNetCore.Http; Microsoft.AspNetCore.Http.HttpContext
			private HttpContext Context;
			private string[]    arrSQL ;
			
			public BuildState(HttpContext Context, string[] arrSQL)
			{
				this.Context = Context;
				this.arrSQL  = arrSQL;
			}

			/// <summary>
			/// Resolves a virtual path (e.g. "~/App_Data/Build.log") to a physical file system path.
			/// 
			/// .NET 10 Migration: Replaces Context.Server.MapPath(virtualPath).
			/// BEFORE: string path = Context.Server.MapPath("~/App_Data/Build.log");
			///   — HttpServerUtility.MapPath maps IIS virtual paths to physical server paths.
			/// AFTER: IWebHostEnvironment.ContentRootPath + Path.Combine replaces the mapping.
			///   — IWebHostEnvironment obtained via Context.RequestServices service locator pattern.
			///   — Falls back to Directory.GetCurrentDirectory() when no IWebHostEnvironment is registered.
			/// </summary>
			/// <param name="virtualPath">Virtual path starting with "~/" (e.g. "~/App_Data/Build.log").</param>
			/// <returns>Physical file system path corresponding to the virtual path.</returns>
			private string MapPath(string virtualPath)
			{
				// .NET 10 Migration: Context.Server.MapPath → IWebHostEnvironment.ContentRootPath
				// IWebHostEnvironment is registered by the ASP.NET Core hosting infrastructure
				// and is always available when the application is properly configured.
				IWebHostEnvironment env = Context.RequestServices?.GetService(typeof(IWebHostEnvironment)) as IWebHostEnvironment;
				string sRoot = env?.ContentRootPath ?? Directory.GetCurrentDirectory();
				// Remove the "~/" prefix and convert forward slashes to OS path separator
				string sRelative = virtualPath.TrimStart('~').TrimStart('/');
				return Path.Combine(sRoot, sRelative.Replace('/', Path.DirectorySeparatorChar));
			}
			
			/// <summary>
			/// Executes the SQL DDL batch statements, updating the progress page and logging errors.
			/// Called synchronously from BuildDatabase after Build.sql has been split into GO-delimited batches.
			/// </summary>
			public void Start()
			{
				// .NET 10 Migration: Context.Server.MapPath → MapPath helper (IWebHostEnvironment-based)
				// BEFORE: string sBuildLogPath = Context.Server.MapPath("~/App_Data/Build.log");
				// AFTER:  string sBuildLogPath = MapPath("~/App_Data/Build.log");
				string sBuildLogPath = MapPath("~/App_Data/Build.log");
				try
				{
					// .NET 10 Migration: Context.Server.MapPath → MapPath helper
					// BEFORE: string sOfflinePath = Context.Server.MapPath("~/app_offline.htm");
					// AFTER:  string sOfflinePath = MapPath("~/app_offline.htm");
					string sOfflinePath = MapPath("~/app_offline.htm");
					try
					{
						Debug.WriteLine(DateTime.Now.ToString() + " Begin");
						File.AppendAllText(sBuildLogPath, DateTime.Now.ToString() + " Begin" + ControlChars.CrLf);
					}
					catch
					{
						// The App_Data folder may be read-only, so protect against exception. 
					}
					
					int nErrors = 0;
					StringBuilder sbLogText = new StringBuilder();
					// .NET 10 Migration: DbProviderFactories.GetFactory(Context.Application) [static, HttpApplicationState]
					//   → DbProviderFactories instance from Context.RequestServices + GetFactory(IMemoryCache)
					// BEFORE: DbProviderFactory dbf = DbProviderFactories.GetFactory(Context.Application);
					// AFTER:  IMemoryCache replaces HttpApplicationState; DbProviderFactories is DI-registered singleton.
					IMemoryCache memoryCache = Context.RequestServices?.GetService(typeof(IMemoryCache)) as IMemoryCache;
					DbProviderFactories dbpf = Context.RequestServices?.GetService(typeof(DbProviderFactories)) as DbProviderFactories;
					DbProviderFactory dbf = dbpf.GetFactory(memoryCache);
					using ( IDbConnection con = dbf.CreateConnection() )
					{
						con.Open();
						for ( int i = 0; i < arrSQL.Length; i++ )
						{
							string sSQL = arrSQL[i].Trim();
							if ( !String.IsNullOrEmpty(sSQL) )
							{
								int nProgress = (100 * i) / arrSQL.Length;
								try
								{
									// 08/02/2015 Paul.  Do not include the SQL as it would confuse users. 
									string sOfflineHtml = String.Format(sProgressTemplate, nProgress, nProgress, sbLogText.ToString(), String.Empty);
									try
									{
										File.WriteAllText(sOfflinePath, sOfflineHtml);
									}
									catch(Exception ex)
									{
										// There may be an exception if we try and write the file and IIS is trying to deliver the file. Just ignore. 
										Debug.WriteLine(ex.Message);
									}
#if DEBUG
									int nEndOfLine = sSQL.IndexOf(ControlChars.CrLf);
									string sFirstLine = (nEndOfLine > 0) ? sSQL.Substring(0, nEndOfLine) : sSQL;
									Debug.WriteLine(sFirstLine);
#endif
									using ( IDbCommand cmd = con.CreateCommand() )
									{
										cmd.CommandTimeout = 0;
										cmd.CommandText = sSQL;
										cmd.ExecuteNonQuery();
									}
								}
								catch(Exception ex)
								{
									nErrors++;
									string sThisError = i.ToString() + ": " + ex.Message + ControlChars.CrLf;
									sbLogText.Append(sThisError);
									try
									{
										File.AppendAllText(sBuildLogPath, DateTime.Now.ToString() + " - " + sThisError + sSQL + ControlChars.CrLf + ControlChars.CrLf);
									}
									catch
									{
										// The App_Data folder may be read-only, so protect against exception. 
									}
								}
							}
						}
					}
					try
					{
						Debug.WriteLine(DateTime.Now.ToString() + " End");
						File.AppendAllText(sBuildLogPath, DateTime.Now.ToString() + " End" + ControlChars.CrLf);
					}
					catch
					{
						// The App_Data folder may be read-only, so protect against exception. 
					}
					if ( nErrors > 0 )
					{
						SplendidError.SystemMessage(Context, "Error", new StackTrace(true).GetFrame(0), sbLogText.ToString());
						// Note: sErrorTemplate uses "%0" as placeholder, not "{0}". String.Format leaves %0 literal.
						// This is preserved as-is from the original per minimal change clause.
						string sOfflineHtml = String.Format(sErrorTemplate, sbLogText.ToString());
						File.WriteAllText(sOfflinePath, sOfflineHtml);
					}
					else
					{
						if ( File.Exists(sOfflinePath) )
							File.Delete(sOfflinePath);
					}
				}
				catch(Exception ex)
				{
					SplendidError.SystemMessage(Context, "Error", new StackTrace(true).GetFrame(0), ex);
					try
					{
						File.AppendAllText(sBuildLogPath, DateTime.Now.ToString() + " - " + ex.Message + ControlChars.CrLf + ControlChars.CrLf);
					}
					catch
					{
						// The App_Data folder may be read-only, so protect against exception. 
					}
				}
			}
		}

		/// <summary>
		/// Checks whether the database needs to be built by inspecting INFORMATION_SCHEMA.TABLES for the CONFIG table.
		/// If Build.sql is present and CONFIG does not exist, splits Build.sql on GO batch separators and
		/// executes all DDL batches, reporting progress via app_offline.htm.
		/// 
		/// .NET 10 Migration: HttpContext now from Microsoft.AspNetCore.Http.
		/// Context.Server.MapPath → IWebHostEnvironment.ContentRootPath + Path.Combine.
		/// Context.Application → IMemoryCache from Context.RequestServices.
		/// DbProviderFactories.GetFactory(Context.Application) → instance method GetFactory(IMemoryCache).
		/// Microsoft.VisualBasic.Strings.Split → Strings.Split (from src/SplendidCRM.Core/VisualBasic.cs).
		/// Context.Response.Write → Context.Response.WriteAsync (async, GetAwaiter().GetResult()).
		/// Context.Response.Flush → Context.Response.Body.FlushAsync (async, GetAwaiter().GetResult()).
		/// </summary>
		/// <param name="Context">
		/// The current HTTP context providing RequestServices (DI service locator for IWebHostEnvironment,
		/// IMemoryCache, DbProviderFactories) and Response for progress output.
		/// </param>
		public static void BuildDatabase(HttpContext Context)
		{
			// .NET 10 Migration: Context.Server.MapPath → IWebHostEnvironment.ContentRootPath + Path.Combine
			// BEFORE: string sBuildSqlPath = Context.Server.MapPath("~/App_Data/Build.sql");
			// AFTER:  IWebHostEnvironment obtained from Context.RequestServices service locator.
			//         Falls back to Directory.GetCurrentDirectory() when not configured.
			IWebHostEnvironment env = Context.RequestServices?.GetService(typeof(IWebHostEnvironment)) as IWebHostEnvironment;
			string sRoot = env?.ContentRootPath ?? Directory.GetCurrentDirectory();
			string sBuildSqlPath = Path.Combine(sRoot, "App_Data", "Build.sql");
			try
			{
				// 08/01/2015 Paul.  If Build.log exists, then we have already processed the build.sql file, so skip. 
				if ( File.Exists(sBuildSqlPath) )
				{
					// .NET 10 Migration: DbProviderFactories.GetFactory(Context.Application) [static, HttpApplicationState]
					//   → DbProviderFactories instance obtained from Context.RequestServices + GetFactory(IMemoryCache)
					// BEFORE: DbProviderFactory dbf = DbProviderFactories.GetFactory(Context.Application);
					// AFTER:  IMemoryCache replaces HttpApplicationState parameter. DbProviderFactories is DI singleton.
					IMemoryCache memoryCache = Context.RequestServices?.GetService(typeof(IMemoryCache)) as IMemoryCache;
					DbProviderFactories dbpf = Context.RequestServices?.GetService(typeof(DbProviderFactories)) as DbProviderFactories;
					DbProviderFactory dbf = dbpf.GetFactory(memoryCache);
					using ( IDbConnection con = dbf.CreateConnection() )
					{
						con.Open();
						using ( IDbCommand cmd = con.CreateCommand() )
						{
							string sSQL = "select count(*) from INFORMATION_SCHEMA.TABLES where TABLE_NAME = 'CONFIG'";
							cmd.CommandTimeout = 0;
							cmd.CommandText = sSQL;
							// Sql.ToInteger: converts ExecuteScalar result (boxed int) to int; returns 0 on null/DBNull
							int nTables = Sql.ToInteger(cmd.ExecuteScalar());
							if ( nTables == 0 )
							{
								// 08/12/2015 Paul.  Read the file after checking for a valid database. 
								string sBuildSQL = File.ReadAllText(sBuildSqlPath);
								if ( !String.IsNullOrEmpty(sBuildSQL) )
								{
									// Normalize GO batch separator to uppercase for case-insensitive split
									sBuildSQL = sBuildSQL.Replace(ControlChars.CrLf + "go" + ControlChars.CrLf, ControlChars.CrLf + "GO" + ControlChars.CrLf);
									sBuildSQL = sBuildSQL.Replace(ControlChars.CrLf + "Go" + ControlChars.CrLf, ControlChars.CrLf + "GO" + ControlChars.CrLf);
									// .NET 10 Migration: Microsoft.VisualBasic.Strings.Split → Strings.Split (SplendidCRM.VisualBasic)
									// .NET 10 Migration: Microsoft.VisualBasic.CompareMethod.Text → CompareMethod.Text (SplendidCRM.VisualBasic)
									// BEFORE: string[] arrSQL = Microsoft.VisualBasic.Strings.Split(sBuildSQL, ControlChars.CrLf + "GO" + ControlChars.CrLf, -1, Microsoft.VisualBasic.CompareMethod.Text);
									// AFTER:  Strings.Split and CompareMethod from src/SplendidCRM.Core/VisualBasic.cs
									string[] arrSQL = Strings.Split(sBuildSQL, ControlChars.CrLf + "GO" + ControlChars.CrLf, -1, CompareMethod.Text);
									if ( arrSQL.Length > 1 )
									{
										// .NET 10 Migration: Context.Server.MapPath → IWebHostEnvironment.ContentRootPath + Path.Combine
										// BEFORE: string sOfflinePath = Context.Server.MapPath("~/app_offline.htm");
										// AFTER:  sRoot already resolved from env.ContentRootPath above.
										string sOfflinePath = Path.Combine(sRoot, "app_offline.htm");
										try
										{
											string sOfflineHtml = String.Format(sProgressTemplate, 0, 0, String.Empty, String.Empty);
											File.WriteAllText(sOfflinePath, sOfflineHtml);
											// 08/01/2015 Paul.  Send content and flush so that the browser will refresh. 
											// .NET 10 Migration: Context.Response.Write → Context.Response.WriteAsync
											// BEFORE: Context.Response.Write(sOfflineHtml);
											// AFTER:  WriteAsync is the async equivalent; GetAwaiter().GetResult() preserves
											//         synchronous execution context while using the ASP.NET Core async API.
											Context.Response.WriteAsync(sOfflineHtml).GetAwaiter().GetResult();
											// .NET 10 Migration: Context.Response.Flush() → Context.Response.Body.FlushAsync()
											// BEFORE: Context.Response.Flush();
											// AFTER:  HttpResponse.Body.FlushAsync() is the ASP.NET Core equivalent.
											//         GetAwaiter().GetResult() preserves synchronous execution.
											Context.Response.Body.FlushAsync().GetAwaiter().GetResult();
										}
										catch(Exception ex)
										{
											// There may be an exception if we try and write the file and IIS is trying to deliver the file. Just ignore. 
											Debug.WriteLine(ex.Message);
										}
										BuildState build = new BuildState(Context, arrSQL);
										//System.Threading.Thread t = new System.Threading.Thread(build.Start);
										//t.Start();
										// 08/01/2015 Paul.  Can't use a thread as IIS will terminate it. 
										build.Start();
									}
								}
							}
						}
					}
				}
			}
			catch(Exception ex)
			{
				SplendidError.SystemMessage(Context, "Error", new StackTrace(true).GetFrame(0), ex);
			}
		}
	}
}
