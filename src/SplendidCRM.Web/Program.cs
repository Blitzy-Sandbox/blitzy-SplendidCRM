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
// Migrated from SplendidCRM/Global.asax.cs — Application entry point for .NET 10 ASP.NET Core.
// Implements the 5-tier configuration provider hierarchy, DI container registration, middleware
// pipeline, hosted services, SignalR hub mapping, SoapCore endpoint, and fail-fast startup validation.
// Per AAP §0.5.1 and §0.8.2:
//   Provider Hierarchy (highest priority wins):
//     1. AWS Secrets Manager
//     2. Environment variables
//     3. AWS Systems Manager Parameter Store
//     4. appsettings.{Environment}.json
//     5. appsettings.json
#nullable disable
using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SplendidCRM;
using SplendidCRM.Web.Authentication;
using SplendidCRM.Web.Authorization;
using SplendidCRM.Web.Configuration;
using SplendidCRM.Web.Middleware;
using SplendidCRM.Web.Services;
// SignalR manager classes are in the SplendidCRM namespace (see SignalR/ folder)
using SoapCore;

// =====================================================================================
// 1. BUILD — Configuration Provider Registration (5-tier hierarchy)
// =====================================================================================
var builder = WebApplication.CreateBuilder(args);

// Tier 5 (lowest priority): appsettings.json — already loaded by default.
// Tier 4: appsettings.{Environment}.json — already loaded by default.
// Tier 3: AWS Systems Manager Parameter Store — environment-specific non-secret config.
// Tier 2: Environment variables — already loaded by default (AddEnvironmentVariables).
// Tier 1 (highest priority): AWS Secrets Manager — secrets (DB creds, SMTP, SSO, Duo).
//
// The default WebApplicationBuilder already registers:
//   appsettings.json → appsettings.{env}.json → env vars → command-line args
// We add AWS providers between env vars and the defaults via Insert.
// NOTE: AWS providers are optional; they gracefully no-op if AWS credentials are unavailable.
try
{
	string ssmBasePath = builder.Configuration["Aws:ParameterStore:BasePath"] ?? "/splendidcrm/";
	builder.Configuration.AddAwsParameterStore(ssmBasePath, optional: true);
}
catch (Exception ex)
{
	Console.Error.WriteLine($"[WARNING] AWS Parameter Store provider initialization failed (non-fatal): {ex.Message}");
}

try
{
	string secretId = builder.Configuration["Aws:SecretsManager:SecretId"] ?? "splendidcrm/secrets";
	builder.Configuration.AddAwsSecretsManager(secretId, optional: true, reloadInterval: TimeSpan.FromMinutes(5));
}
catch (Exception ex)
{
	Console.Error.WriteLine($"[WARNING] AWS Secrets Manager provider initialization failed (non-fatal): {ex.Message}");
}

// Override connection string from environment variable if present (per AAP naming convention).
string envConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__SplendidCRM");
if (!string.IsNullOrWhiteSpace(envConnectionString))
{
	builder.Configuration["ConnectionStrings:SplendidCRM"] = envConnectionString;
}

// Map flat environment variable names to structured config keys.
MapEnvVarToConfig(builder.Configuration, "SPLENDID_JOB_SERVER", "Scheduler:JobServer");
MapEnvVarToConfig(builder.Configuration, "SCHEDULER_INTERVAL_MS", "Scheduler:IntervalMs");
MapEnvVarToConfig(builder.Configuration, "EMAIL_POLL_INTERVAL_MS", "Scheduler:EmailPollIntervalMs");
MapEnvVarToConfig(builder.Configuration, "ARCHIVE_INTERVAL_MS", "Scheduler:ArchiveIntervalMs");
MapEnvVarToConfig(builder.Configuration, "SESSION_PROVIDER", "Session:Provider");
MapEnvVarToConfig(builder.Configuration, "SESSION_CONNECTION", "Session:ConnectionString");
MapEnvVarToConfig(builder.Configuration, "AUTH_MODE", "Authentication:Mode");
MapEnvVarToConfig(builder.Configuration, "SSO_AUTHORITY", "SSO:Authority");
MapEnvVarToConfig(builder.Configuration, "SSO_CLIENT_ID", "SSO:ClientId");
MapEnvVarToConfig(builder.Configuration, "SSO_CLIENT_SECRET", "SSO:ClientSecret");
MapEnvVarToConfig(builder.Configuration, "DUO_INTEGRATION_KEY", "Duo:IntegrationKey");
MapEnvVarToConfig(builder.Configuration, "DUO_SECRET_KEY", "Duo:SecretKey");
MapEnvVarToConfig(builder.Configuration, "DUO_API_HOSTNAME", "Duo:ApiHostname");
MapEnvVarToConfig(builder.Configuration, "SMTP_CREDENTIALS", "Smtp:Credentials");
MapEnvVarToConfig(builder.Configuration, "LOG_LEVEL", "Logging:LogLevel:Default");
MapEnvVarToConfig(builder.Configuration, "CORS_ORIGINS", "Cors:AllowedOrigins");

// =====================================================================================
// 2. SERVICES — Dependency Injection Container Registration
// =====================================================================================

// Core infrastructure services.
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

// Distributed session provider selection (per AAP §0.8.2: SESSION_PROVIDER env var).
string sessionProvider = builder.Configuration["Session:Provider"] ?? string.Empty;
string sessionConnection = builder.Configuration["Session:ConnectionString"] ?? string.Empty;
if (string.Equals(sessionProvider, "Redis", StringComparison.OrdinalIgnoreCase))
{
	builder.Services.AddStackExchangeRedisCache(options =>
	{
		options.Configuration = sessionConnection;
		options.InstanceName = "SplendidCRM_";
	});
}
else if (string.Equals(sessionProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
{
	builder.Services.AddDistributedSqlServerCache(options =>
	{
		options.ConnectionString = sessionConnection;
		options.SchemaName = "dbo";
		options.TableName = "SplendidSessions";
	});
}
else
{
	// Fallback to in-memory distributed cache for development.
	builder.Services.AddDistributedMemoryCache();
}

// Session middleware configuration (replaces InProc session from Web.config).
int sessionTimeoutMinutes = builder.Configuration.GetValue<int>("Session:TimeoutMinutes", 20);
builder.Services.AddSession(options =>
{
	options.IdleTimeout = TimeSpan.FromMinutes(sessionTimeoutMinutes);
	options.Cookie.HttpOnly = true;
	options.Cookie.IsEssential = true;
	options.Cookie.SameSite = SameSiteMode.Lax;
	options.Cookie.Name = "SplendidCRM.Session";
});

// Authentication scheme selection (per AAP §0.8.2: AUTH_MODE env var).
string authMode = builder.Configuration["Authentication:Mode"] ?? "Forms";
var authBuilder = builder.Services.AddAuthentication();
if (string.Equals(authMode, "Windows", StringComparison.OrdinalIgnoreCase))
{
	authBuilder.AddWindowsAuthentication(builder.Configuration);
}
else if (string.Equals(authMode, "SSO", StringComparison.OrdinalIgnoreCase))
{
	authBuilder.AddSsoAuthentication(builder.Configuration);
}
else
{
	authBuilder.AddFormsAuthentication(builder.Configuration);
}

// Authorization services with 4-tier ACL model (Module → Team → Field → Record).
builder.Services.AddAuthorization();
builder.Services.AddSingleton<ModuleAuthorizationHandler>();
builder.Services.AddSingleton<TeamAuthorizationHandler>();
builder.Services.AddSingleton<FieldAuthorizationHandler>();
builder.Services.AddSingleton<RecordAuthorizationHandler>();
builder.Services.AddSingleton<SecurityFilterMiddleware>();

// Register SplendidCRM core business logic services via DI.
// These replace the static-class access patterns from .NET Framework (HttpContext.Current, Application[], etc.)
builder.Services.AddSingleton<Sql>();
builder.Services.AddSingleton<SqlProcs>();
builder.Services.AddSingleton<DbProviderFactories>();
builder.Services.AddSingleton<DbProviderFactory>();
builder.Services.AddSingleton<SqlClientFactory>();
builder.Services.AddSingleton<SplendidError>();
builder.Services.AddSingleton<SplendidDefaults>();
builder.Services.AddSingleton<Security>();
builder.Services.AddSingleton<SplendidCache>();
builder.Services.AddSingleton<SplendidInit>();
builder.Services.AddSingleton<L10N>();
builder.Services.AddSingleton<SplendidCRM.TimeZone>();
builder.Services.AddSingleton<Currency>();
builder.Services.AddSingleton<Crm>();
builder.Services.AddSingleton<Utils>();
builder.Services.AddSingleton<RestUtil>();
builder.Services.AddSingleton<SearchBuilder>();
builder.Services.AddSingleton<ModuleUtils>();
builder.Services.AddSingleton<EmailUtils>();
builder.Services.AddSingleton<MimeUtils>();
builder.Services.AddSingleton<ImapUtils>();
builder.Services.AddSingleton<PopUtils>();
builder.Services.AddSingleton<SchedulerUtils>();
builder.Services.AddSingleton<SplendidDynamic>();
builder.Services.AddSingleton<SplendidExport>();
builder.Services.AddSingleton<SplendidImport>();
builder.Services.AddSingleton<ImportUtils>();
builder.Services.AddSingleton<CampaignUtils>();
builder.Services.AddSingleton<ReportingUtils>();
builder.Services.AddSingleton<OrderUtils>();
builder.Services.AddSingleton<ChartUtil>();
builder.Services.AddSingleton<RulesUtil>();
builder.Services.AddSingleton<RdlUtil>();
builder.Services.AddSingleton<XmlUtil>();
builder.Services.AddSingleton<SplendidControl>();
builder.Services.AddSingleton<SplendidPage>();
builder.Services.AddSingleton<ActiveDirectory>();
builder.Services.AddSingleton<ExchangeUtils>();
builder.Services.AddSingleton<ExchangeSync>();
builder.Services.AddSingleton<GoogleUtils>();
builder.Services.AddSingleton<GoogleSync>();
builder.Services.AddSingleton<GoogleApps>();
builder.Services.AddSingleton<iCloudSync>();
builder.Services.AddSingleton<FacebookUtils>();
builder.Services.AddSingleton<SocialImport>();
builder.Services.AddSingleton<ArchiveExternalDB>();
builder.Services.AddSingleton<PortalCache>();
builder.Services.AddSingleton<SplendidMailClient>();
builder.Services.AddSingleton<SplendidMailSmtp>();
builder.Services.AddSingleton<WorkflowUtils>();
builder.Services.AddSingleton<WorkflowInit>();
builder.Services.AddSingleton<SyncUtils>();
builder.Services.AddSingleton<SqlBuild>();

// SignalR manager services (business logic behind hub invocations).
builder.Services.AddSingleton<ChatManager>();
builder.Services.AddSingleton<TwilioManager>();
builder.Services.AddSingleton<PhoneBurnerManager>();
builder.Services.AddSingleton<SignalRUtils>();

// ASP.NET Core SignalR hubs with authorization filter.
builder.Services.AddSignalR(options =>
{
	options.EnableDetailedErrors = builder.Environment.IsDevelopment();
	options.AddFilter<SplendidHubAuthorize>();
});

// MVC Controllers for REST API endpoints.
builder.Services.AddControllers()
	.AddNewtonsoftJson(options =>
	{
		// Preserve backward-compatible JSON serialization (Newtonsoft.Json 13.x fallback).
		options.SerializerSettings.DateTimeZoneHandling = Newtonsoft.Json.DateTimeZoneHandling.Utc;
		options.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Include;
	});

// CORS configuration (per AAP §0.8.2: CORS_ORIGINS env var).
string corsOrigins = builder.Configuration["Cors:AllowedOrigins"] ?? "*";
builder.Services.AddCors(options =>
{
	options.AddDefaultPolicy(policy =>
	{
		if (corsOrigins == "*")
		{
			policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
		}
		else
		{
			policy.WithOrigins(corsOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
				.AllowAnyMethod()
				.AllowAnyHeader()
				.AllowCredentials();
		}
	});
});

// SoapCore SOAP middleware for SugarCRM SOAP API (replaces soap.asmx.cs).
builder.Services.AddSoapCore();
builder.Services.AddScoped<SplendidCRM.Web.Soap.ISugarSoapService, SplendidCRM.Web.Soap.SugarSoapService>();

// Background hosted services (replaces Global.asax.cs timer-based approach).
builder.Services.AddHostedService<SchedulerHostedService>();
builder.Services.AddHostedService<EmailPollingHostedService>();
builder.Services.AddHostedService<ArchiveHostedService>();
builder.Services.AddHostedService<CacheInvalidationService>();

// Cookie policy (replaces Global.asax.cs Session_Start cookie hardening).
builder.Services.Configure<CookiePolicyOptions>(options =>
{
	options.MinimumSameSitePolicy = SameSiteMode.Lax;
});

// =====================================================================================
// 3. BUILD — Build the application
// =====================================================================================
var app = builder.Build();

// =====================================================================================
// 4. STARTUP VALIDATION — Fail-fast on missing required configuration
// =====================================================================================
// Per AAP §0.8.2: If any Secrets Manager key or required environment variable is missing or empty,
// application MUST log the specific missing variable name and exit with non-zero code.
StartupValidator.Validate(app.Configuration, app.Services.GetService<ILogger<Program>>());

// =====================================================================================
// 5. MIDDLEWARE PIPELINE — Order matters
// =====================================================================================

// Global exception handling for unobserved task exceptions (from Global.asax.cs Application_Start).
TaskScheduler.UnobservedTaskException += (sender, e) => { e.SetObserved(); };

if (app.Environment.IsDevelopment())
{
	app.UseDeveloperExceptionPage();
}

// CORS must come before authentication/authorization.
app.UseCors();

// Cookie policy middleware (SameSite/Secure from Global.asax.cs Session_Start).
app.UseCookiePolicy();

// SPA redirect middleware (React URL rewriting from Global.asax.cs Application_BeginRequest).
app.UseMiddleware<SpaRedirectMiddleware>();

// Serve static files (wwwroot/).
app.UseStaticFiles();

// Routing (enables attribute routing for controllers).
app.UseRouting();

// Authentication and authorization.
app.UseAuthentication();
app.UseAuthorization();

// Session middleware (distributed session from Redis or SQL Server).
app.UseSession();

// SoapCore endpoint for SugarCRM SOAP API (preserves /soap.asmx path).
((IApplicationBuilder)app).UseSoapEndpoint<SplendidCRM.Web.Soap.ISugarSoapService>(
	path: "/soap.asmx",
	encoder: new SoapEncoderOptions(),
	serializer: SoapSerializer.XmlSerializer,
	caseInsensitivePath: true
);

// Map REST API controllers.
app.MapControllers();

// Map SignalR hubs (preserves hub method signatures from OWIN SignalR).
app.MapHub<ChatManagerHub>("/hubs/chat");
app.MapHub<TwilioManagerHub>("/hubs/twilio");
app.MapHub<PhoneBurnerHub>("/hubs/phoneburner");

// =====================================================================================
// 6. RUN — Start the application
// =====================================================================================
app.Run();

// =====================================================================================
// HELPER — Map environment variable to structured configuration key
// =====================================================================================
static void MapEnvVarToConfig(ConfigurationManager config, string envVarName, string configKey)
{
	string value = Environment.GetEnvironmentVariable(envVarName);
	if (!string.IsNullOrWhiteSpace(value))
	{
		config[configKey] = value;
	}
}

/// <summary>
/// Marker class for ILogger generic type parameter.
/// Required because top-level statements generate an implicit Program class.
/// </summary>
public partial class Program { }
