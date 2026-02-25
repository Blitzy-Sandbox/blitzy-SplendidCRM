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
// .NET 10 Migration: SplendidCRM/_code/SignalR/SignalRUtils.cs → src/SplendidCRM.Web/SignalR/SignalRUtils.cs
// Changes applied per AAP Section 0.5.2:
//   - REMOVED: using System.Web;          — not available in .NET 10
//   - REMOVED: using System.Web.UI;       — WebForms ScriptManager not available in .NET 10
//   - REMOVED: using Microsoft.Owin;      — OWIN replaced by ASP.NET Core middleware
//   - REMOVED: using Owin;                — OWIN replaced by ASP.NET Core middleware
//   - REMOVED: [assembly: OwinStartup(typeof(SplendidCRM.SignalRUtils))] — OWIN startup registration
//              replaced by MapHub<T>() calls in Program.cs
//   - REMOVED: Configuration(IAppBuilder app) method — OWIN startup entry point
//              In ASP.NET Core, hub mapping is done in Program.cs via app.MapHub<T>(path)
//   - REMOVED: RegisterSignalR(ScriptManager mgrAjax) method — WebForms ScriptManager injection
//              In ASP.NET Core, SignalR client scripts are managed by the frontend build (npm @microsoft/signalr)
//   - ADDED:   using Microsoft.AspNetCore.Routing; — IEndpointRouteBuilder for MapHubs() parameter
//   - ADDED:   using Microsoft.AspNetCore.SignalR; — MapHub<THub>() extension method
//   - ADDED:   static MapHubs(IEndpointRouteBuilder endpoints) — replaces OWIN app.MapSignalR()
//              Registers ChatManagerHub at /hubs/chat, TwilioManagerHub at /hubs/twilio,
//              PhoneBurnerHub at /hubs/phoneburner
//   - PRESERVED: License header (lines 1-21 exactly as-is)
//   - PRESERVED: InitApp() with commented-out scale-out options, updated for ASP.NET Core equivalents
//   - PRESERVED: All Paul's date-stamped comments
//   - Minimal change clause applied per AAP 0.8.1
//
// HANDOFF TO PROMPT 2 (Frontend Modernization):
//   SignalR client-facing endpoint paths have changed from the OWIN default:
//   OLD default: /signalr (OWIN app.MapSignalR() single default route)
//   NEW paths:
//     Chat hub:        /hubs/chat
//     Twilio hub:      /hubs/twilio
//     PhoneBurner hub: /hubs/phoneburner
//   Frontend must update @microsoft/signalr HubConnectionBuilder().withUrl() calls accordingly.
//   CORS for SignalR is configured via CORS_ORIGINS env var in Program.cs.
#nullable disable
using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;

namespace SplendidCRM
{
	/// <summary>
	/// Summary description for SignalRUtils.
	/// Provides ASP.NET Core SignalR hub registration and initialization utilities.
	/// Migrated from OWIN-based SignalR 2.4.1 startup to ASP.NET Core SignalR endpoint routing.
	/// </summary>
	// 09/14/2020 Paul.  Convert to SignalR 2.4.1
	// https://docs.microsoft.com/en-us/aspnet/signalr/overview/releases/upgrading-signalr-1x-projects-to-20
	// .NET 10 Migration: OWIN startup replaced by MapHubs() + ASP.NET Core endpoint routing in Program.cs
	public class SignalRUtils
	{
		/// <summary>
		/// Registers all ASP.NET Core SignalR hubs with the endpoint routing middleware.
		/// Called from Program.cs within the app.UseEndpoints() / app.MapGroup() block.
		/// Replaces OWIN app.MapSignalR() which registered all hubs at the single default /signalr route.
		/// 
		/// Hub endpoint paths (CHANGED from OWIN default — see Prompt 2 handoff notes above):
		///   /hubs/chat        — ChatManagerHub  (was /signalr under OWIN)
		///   /hubs/twilio      — TwilioManagerHub (was /signalr under OWIN)
		///   /hubs/phoneburner — PhoneBurnerHub   (was /signalr under OWIN)
		/// </summary>
		/// <param name="endpoints">The ASP.NET Core endpoint route builder from Program.cs.</param>
		public static void MapHubs(IEndpointRouteBuilder endpoints)
		{
			// Map ChatManagerHub to /hubs/chat
			// Migrated from: OWIN app.MapSignalR() → ChatManagerHub at /signalr (default OWIN route)
			// New path documented for Prompt 2 frontend handoff (AAP 0.4.4)
			endpoints.MapHub<ChatManagerHub>("/hubs/chat");

			// Map TwilioManagerHub to /hubs/twilio
			// Migrated from: OWIN app.MapSignalR() → TwilioManagerHub at /signalr (default OWIN route)
			// New path documented for Prompt 2 frontend handoff (AAP 0.4.4)
			endpoints.MapHub<TwilioManagerHub>("/hubs/twilio");

			// Map PhoneBurnerHub to /hubs/phoneburner
			// Migrated from: OWIN app.MapSignalR() → PhoneBurnerManager at /signalr (default OWIN route)
			// New path documented for Prompt 2 frontend handoff (AAP 0.4.4)
			endpoints.MapHub<PhoneBurnerHub>("/hubs/phoneburner");
		}

		/// <summary>
		/// Initializes SignalR application-level configuration.
		/// Called during application startup from Program.cs or SchedulerHostedService.
		/// 
		/// In SignalR 1.x / 2.x (OWIN), this method configured scale-out backplane providers
		/// (SQL Server, Redis, Azure Service Bus) via GlobalHost.DependencyResolver.
		/// In ASP.NET Core SignalR, scale-out is configured via ISignalRServerBuilder extensions
		/// (AddStackExchangeRedis / AddSqlServer) registered in Program.cs builder.Services.AddSignalR().
		/// The commented-out options below are preserved as documentation of available scale-out paths.
		/// </summary>
		public static void InitApp()
		{
			// ASP.NET Core SignalR scale-out options (replaces GlobalHost.DependencyResolver in SignalR 1.x/2.x):
			// In Program.cs, configure via:
			//   builder.Services.AddSignalR().AddStackExchangeRedis("127.0.0.1:6379", options => { options.Configuration.ChannelPrefix = RedisChannel.Literal("SignalRSamples"); });
			// or:
			//   builder.Services.AddSignalR().AddSqlServer(connectionString, options => { options.SchemaName = "SignalRSamples"; });
			// or:
			//   builder.Services.AddSignalR().AddAzureSignalR("connection string");

			// Uncomment the following line to enable scale-out using SQL Server
			// (ASP.NET Core equivalent of: dependencyResolver.UseSqlServer(connStr))
			//builder.Services.AddSignalR().AddSqlServer(System.Configuration.ConfigurationManager.ConnectionStrings["SignalRSamples"].ConnectionString);

			// Uncomment the following line to enable scale-out using Redis
			// (ASP.NET Core equivalent of: dependencyResolver.UseRedis(config))
			//builder.Services.AddSignalR().AddStackExchangeRedis("127.0.0.1", 6379, options => { options.Configuration.ChannelPrefix = RedisChannel.Literal("SignalRSamples"); });

			// Uncomment the following line to enable scale-out using service bus
			// (ASP.NET Core equivalent of: dependencyResolver.UseServiceBus(...))
			//builder.Services.AddSignalR().AddAzureSignalR("connection string");

			// ASP.NET Core SignalR hub pipeline modules are added via IHubFilter:
			// (ASP.NET Core equivalent of: hubPipeline.AddModule(new SplendidPipelineModule()))
			//builder.Services.AddSignalR(options => { options.AddFilter<SplendidHubFilter>(); });

			// Hub registration is now done via MapHub<T>() in MapHubs() above.
			// (ASP.NET Core equivalent of: RouteTable.Routes.MapHubs("/signalr", new HubConfiguration() { EnableDetailedErrors = true }))
			try
			{
				// 12/02/2014 Paul.  Enable Cross Domain for the Mobile Client.
				// 09/14/2020 Paul.  Convert to SignalR 2.4.1
				// .NET 10 Migration: Cross-domain/CORS for SignalR is now configured in Program.cs via
				//   builder.Services.AddCors() and app.UseCors() — see Program.cs CORS_ORIGINS env var handling.
				// (ASP.NET Core equivalent of: HubConfiguration config = new HubConfiguration(); config.EnableCrossDomain = true;)
				//   app.UseCors(policy => policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials());
			}
			catch(Exception ex)
			{
				Debug.WriteLine(ex.Message);
			}
		}
	}
}
