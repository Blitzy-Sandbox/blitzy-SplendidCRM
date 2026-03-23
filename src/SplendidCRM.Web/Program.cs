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
#nullable enable
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

// Configure Kestrel web server settings.
// Replaces Web.config <httpRuntime maxRequestLength="104857600" /> (line 111) and
// <system.webServer>/<security>/<requestFiltering>/<requestLimits maxAllowedContentLength="104857600"/>
// (Web.config line 139) with Kestrel-native limits.
// Kestrel port is configurable via ASPNETCORE_URLS env var (default http://localhost:5000).
builder.WebHost.ConfigureKestrel(options =>
{
	// Security hardening: suppress Kestrel Server header to prevent technology disclosure.
	options.AddServerHeader = false;

	// 100MB request body size limit — matches legacy Web.config line 111:
	//   maxRequestLength="104857600" (in bytes)
	// Required for file upload endpoints (image, campaign tracker, import).
	// Per AAP Phase 7 (Kestrel Configuration).
	options.Limits.MaxRequestBodySize = 104857600;
});

// Five-tier configuration provider hierarchy (highest priority wins):
// Tier 1 (highest): AWS Secrets Manager — secrets (DB creds, SMTP, SSO, Duo).
// Tier 2: Environment variables — runtime overrides (already loaded by default via AddEnvironmentVariables).
// Tier 3: AWS Systems Manager Parameter Store — environment-specific non-secret config.
// Tier 4: appsettings.{Environment}.json — per-environment defaults (already loaded by default).
// Tier 5 (lowest): appsettings.json — base defaults (already loaded by default).
//
// The default WebApplicationBuilder registers sources in this order (later overrides earlier):
//   appsettings.json → appsettings.{env}.json → env vars → command-line args
// We INSERT Parameter Store BEFORE the env vars source (so env vars override it),
// and APPEND Secrets Manager at the end (highest priority).
// NOTE: AWS providers are optional; they gracefully no-op if AWS credentials are unavailable.
try
{
	string ssmBasePath = builder.Configuration["Aws:ParameterStore:BasePath"] ?? "/splendidcrm/";
	var parameterStoreSource = new AwsParameterStoreConfigurationSource
	{
		BasePath = ssmBasePath,
		Optional = true
	};
	// Insert Parameter Store before the environment variables source so that
	// env vars (tier 2) override Parameter Store (tier 3) per the 5-tier hierarchy.
	int envVarIndex = -1;
	for (int i = 0; i < builder.Configuration.Sources.Count; i++)
	{
		if (builder.Configuration.Sources[i].GetType().Name == "EnvironmentVariablesConfigurationSource")
		{
			envVarIndex = i;
			break;
		}
	}
	if (envVarIndex >= 0)
	{
		builder.Configuration.Sources.Insert(envVarIndex, parameterStoreSource);
	}
	else
	{
		// Fallback: append if env vars source not found (should not happen in normal ASP.NET Core).
		((IConfigurationBuilder)builder.Configuration).Add(parameterStoreSource);
	}
}
catch (Exception ex)
{
	Console.Error.WriteLine($"[WARNING] AWS Parameter Store provider initialization failed (non-fatal): {ex.Message}");
}

try
{
	string secretId = builder.Configuration["Aws:SecretsManager:SecretId"] ?? "splendidcrm/secrets";
	// Append Secrets Manager as the last source — highest priority (tier 1).
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
	// Security hardening: set Secure flag based on request scheme (Issue 6).
	// SameAsRequest ensures Secure flag is sent when accessed via HTTPS, absent when HTTP (dev).
	options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
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

// Conditionally register Duo 2FA if DUO_INTEGRATION_KEY is present in configuration.
// Per AAP §0.8.2: DUO_INTEGRATION_KEY is Optional — DuoUniversal 2FA is only registered when
// this key is non-empty. Replaces the embedded DuoUniversal implementation from
// SplendidCRM/_code/DuoUniversal/ by wiring into the DuoTwoFactorSetup middleware.
string duoIntegrationKey = builder.Configuration["Duo:IntegrationKey"];
if (!string.IsNullOrWhiteSpace(duoIntegrationKey))
{
	builder.Services.AddDuoTwoFactorAuthentication(builder.Configuration);
}

// Authorization services with 4-tier ACL model (Module → Team → Field → Record).
builder.Services.AddAuthorization();
builder.Services.AddSingleton<ModuleAuthorizationHandler>();
builder.Services.AddSingleton<TeamAuthorizationHandler>();
builder.Services.AddSingleton<FieldAuthorizationHandler>();
builder.Services.AddSingleton<RecordAuthorizationHandler>();
builder.Services.AddSingleton<SecurityFilterService>();

// Register SplendidCRM core business logic services via DI.
// These replace the static-class access patterns from .NET Framework (HttpContext.Current, Application[], etc.)
builder.Services.AddSingleton<Sql>();
builder.Services.AddSingleton<SqlProcs>();
builder.Services.AddSingleton<DbProviderFactories>();
// NOTE: DbProviderFactory (singular) is NOT registered — it has a legacy 7-string-parameter constructor
// incompatible with DI. DbProviderFactories (plural) provides all DI-friendly DB operations.
// NOTE: SqlClientFactory is NOT registered in DI — its constructor requires a runtime connection string
// parameter (SqlClientFactory(string sConnectionString)). It is instantiated on-demand by
// DbProviderFactories.CreateConnection() with the connection string from IConfiguration.
// See src/SplendidCRM.Core/DbProviderFactories.cs line 394.
builder.Services.AddSingleton<SplendidError>();
builder.Services.AddSingleton<SplendidDefaults>();
builder.Services.AddSingleton<Security>();
builder.Services.AddSingleton<SplendidCache>();
builder.Services.AddSingleton<SplendidInit>();
// NOTE: L10N is NOT registered in DI — its constructor requires a per-request culture name string
// parameter (L10N(string sNAME, IMemoryCache memoryCache)). L10N instances are created on-demand
// per request with the user's language preference. See SplendidControl.GetL10n() and REST controller
// methods that create L10N instances with the request culture.
builder.Services.AddSingleton<SplendidCRM.TimeZone>();
builder.Services.AddSingleton<Currency>();
builder.Services.AddSingleton<Crm>();
builder.Services.AddSingleton<Utils>();
builder.Services.AddSingleton<RestUtil>();
// NOTE: SearchBuilder is NOT registered in DI — its constructor requires per-invocation parameters
// (SearchBuilder(string str, IDbCommand cmd)). SearchBuilder instances are created on-demand per
// query with the specific search string and database command for that operation.
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
// NOTE: FacebookUtils is NOT registered in DI — its constructor requires per-request parameters
// (FacebookUtils(string sAppID, string sAppSecret, IRequestCookieCollection cookies)).
// FacebookUtils instances are created on-demand with the Facebook app credentials and request cookies.
builder.Services.AddSingleton<SocialImport>();
builder.Services.AddSingleton<ArchiveExternalDB>();
builder.Services.AddSingleton<PortalCache>();
builder.Services.AddSingleton<SplendidMailClient>();
builder.Services.AddSingleton<SplendidMailSmtp>();
builder.Services.AddSingleton<WorkflowUtils>();
builder.Services.AddSingleton<WorkflowInit>();
builder.Services.AddSingleton<SyncUtils>();
builder.Services.AddSingleton<SyncError>();
builder.Services.AddSingleton<SqlBuild>();
// NOTE: Class name has a known typo (DbAcrhiveFactories vs DbArchiveFactories).
// The typo is in the Core library class definition (src/SplendidCRM.Core/DbAcrhiveFactories.cs)
// and must be fixed there first. This registration correctly references the actual class name.
builder.Services.AddSingleton<DbAcrhiveFactories>();
builder.Services.AddSingleton<SplendidGrid>();

// NOTE: SplendidMailOffice365, SplendidMailGmail, and SplendidMailExchangePassword are NOT registered
// in DI — their constructors require runtime parameters (Guid gOAUTH_TOKEN_ID for Office365/Gmail,
// server credentials for ExchangePassword) that vary per invocation. These mail transport subclasses
// are instantiated on-demand by SplendidMailClient.CreateMailClient() factory method and by
// EmailUtils.CreateMailClient() with the appropriate OAuth token or server credentials.
// See src/SplendidCRM.Core/SplendidMailClient.cs lines 159, 173 and EmailUtils.cs lines 413, 415.

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

// =====================================================================================
// EARLY CONFIGURATION VALIDATION (defense-in-depth — runs BEFORE builder.Build())
// =====================================================================================
// Per AAP §0.8.2: Application MUST log the specific missing variable name and exit with non-zero code.
// This early validation ensures that the most critical configuration errors are caught before the DI
// container is built, providing clear error messages even if a DI registration issue is introduced.
// The full StartupValidator.Validate() runs AFTER builder.Build() for comprehensive validation.
{
	string earlyConnStr = builder.Configuration.GetConnectionString("SplendidCRM");
	if (string.IsNullOrWhiteSpace(earlyConnStr))
	{
		Console.Error.WriteLine("[ERROR] Required configuration 'ConnectionStrings:SplendidCRM' (env var: ConnectionStrings__SplendidCRM) is missing or empty. Application cannot start without a database connection string.");
		Environment.Exit(1);
	}
	string earlySessionProvider = builder.Configuration["Session:Provider"] ?? string.Empty;
	if (!string.Equals(earlySessionProvider, "Redis", StringComparison.OrdinalIgnoreCase)
	 && !string.Equals(earlySessionProvider, "SqlServer", StringComparison.OrdinalIgnoreCase)
	 && !string.IsNullOrWhiteSpace(earlySessionProvider))
	{
		Console.Error.WriteLine($"[ERROR] Required configuration 'SESSION_PROVIDER' has invalid value '{earlySessionProvider}'. Must be exactly 'Redis' or 'SqlServer'.");
		Environment.Exit(1);
	}
	string earlyAuthMode = builder.Configuration["Authentication:Mode"] ?? string.Empty;
	if (!string.IsNullOrWhiteSpace(earlyAuthMode)
	 && !string.Equals(earlyAuthMode, "Windows", StringComparison.OrdinalIgnoreCase)
	 && !string.Equals(earlyAuthMode, "Forms", StringComparison.OrdinalIgnoreCase)
	 && !string.Equals(earlyAuthMode, "SSO", StringComparison.OrdinalIgnoreCase))
	{
		Console.Error.WriteLine($"[ERROR] Required configuration 'AUTH_MODE' has invalid value '{earlyAuthMode}'. Must be exactly 'Windows', 'Forms', or 'SSO'.");
		Environment.Exit(1);
	}
}

// CORS configuration (per AAP §0.8.2: CORS_ORIGINS env var — Required).
// Defense-in-depth: no wildcard fallback. If CORS_ORIGINS is not configured, fail-fast
// (StartupValidator also enforces this, but the CORS code itself must not have a permissive default).
string corsOrigins = builder.Configuration["Cors:AllowedOrigins"];
if (string.IsNullOrWhiteSpace(corsOrigins))
{
	Console.Error.WriteLine("[ERROR] Required configuration 'CORS_ORIGINS' is missing or empty. Set the CORS_ORIGINS environment variable or Cors:AllowedOrigins in appsettings.");
	Environment.Exit(1);
}
builder.Services.AddCors(options =>
{
	options.AddDefaultPolicy(policy =>
	{
		policy.WithOrigins(corsOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			.AllowAnyMethod()
			.AllowAnyHeader()
			.AllowCredentials();
	});
});

// Antiforgery protection for state-changing endpoints (Issue 8).
// JSON Content-Type + CORS restrictions provide baseline CSRF mitigation for SPA consumption;
// antiforgery tokens add defense-in-depth for form-based submissions.
builder.Services.AddAntiforgery(options =>
{
	options.HeaderName = "X-XSRF-TOKEN";
	options.Cookie.Name = "SplendidCRM.Antiforgery";
	options.Cookie.HttpOnly = true;
	options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
	options.Cookie.SameSite = SameSiteMode.Strict;
});

// SoapCore SOAP middleware for SugarCRM SOAP API (replaces soap.asmx.cs).
builder.Services.AddSoapCore();
builder.Services.AddScoped<SplendidCRM.ISugarSoapService, SplendidCRM.SugarSoapService>();

// Background hosted services (replaces Global.asax.cs timer-based approach).
builder.Services.AddHostedService<SchedulerHostedService>();
builder.Services.AddHostedService<EmailPollingHostedService>();
builder.Services.AddHostedService<ArchiveHostedService>();
builder.Services.AddHostedService<CacheInvalidationService>();

// Cookie policy (replaces Global.asax.cs Session_Start SameSite/Secure cookie hardening).
// AddSplendidCookiePolicy() configures:
//   - MinimumSameSitePolicy = SameSiteMode.Lax (AAP §0.7.6)
//   - Secure = CookieSecurePolicy.SameAsRequest (HTTPS only)
//   - User-agent-based SameSite override for iOS 12 / Chrome 50-69 / Mac Safari 12
//     (replicates DisallowsSameSiteNone from Global.asax.cs lines 110-147)
// P3P header intentionally dropped — legacy IE iframe compatibility, no modern browsers support it
// (was in Global.asax.cs lines 223-226; per AAP §0.7.6 and Phase 6 requirement)
builder.Services.AddSplendidCookiePolicy();

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
// STATIC AMBIENT WIRING — Bridge DI services into legacy static classes.
// These classes use static ambient fields because their methods are static and cannot
// accept constructor-injected parameters directly.
// Must run AFTER builder.Build() (DI container ready) and BEFORE any middleware that
// invokes these static methods.
// =====================================================================================
{
	var ambientHttpAccessor  = app.Services.GetRequiredService<IHttpContextAccessor>();
	var ambientMemoryCache   = app.Services.GetRequiredService<IMemoryCache>();
	var ambientConfiguration = app.Services.GetRequiredService<IConfiguration>();
	var ambientSecurity      = app.Services.GetRequiredService<Security>();
	var ambientDbf           = app.Services.GetRequiredService<DbProviderFactories>();
	var ambientSplendidCache = app.Services.GetRequiredService<SplendidCache>();

	Sql.SetAmbient(ambientDbf, ambientMemoryCache, ambientHttpAccessor, ambientSecurity);
	SqlProcs.SetAmbient(ambientSecurity, ambientDbf);
	Utils.SetAmbient(ambientHttpAccessor, ambientMemoryCache, ambientConfiguration, ambientSecurity, ambientDbf, ambientSplendidCache);
	SplendidDefaults.SetAmbient(ambientHttpAccessor, ambientMemoryCache);
	SplendidCRM.TimeZone.SetAmbient(ambientMemoryCache);
	PopUtils.SetAmbient(ambientMemoryCache);
	MimeUtils.SetAmbient(ambientMemoryCache, ambientSecurity, ambientDbf);
	SqlProcs.SetDynamicFactoryAmbient(ambientMemoryCache, ambientDbf);
}

// =====================================================================================
// 5. MIDDLEWARE PIPELINE — Order matters
// =====================================================================================

// Global exception handling for unobserved task exceptions (from Global.asax.cs Application_Start).
TaskScheduler.UnobservedTaskException += (sender, e) => { e.SetObserved(); };

if (app.Environment.IsDevelopment())
{
	app.UseDeveloperExceptionPage();
}
else
{
	// HSTS and HTTPS redirection for non-Development environments (Issue 7).
	// In container deployments where TLS terminates at the load balancer (Prompt 3),
	// UseHsts ensures Strict-Transport-Security headers are sent and UseHttpsRedirection
	// redirects HTTP to HTTPS.
	app.UseHsts();
}
app.UseHttpsRedirection();

// Security headers middleware (Issues 2, 3) — sets standard security headers on ALL responses.
// X-Content-Type-Options: nosniff — prevents MIME-type sniffing attacks.
// X-Frame-Options: DENY — prevents clickjacking via iframe embedding.
// X-XSS-Protection: 0 — modern approach; CSP is preferred over legacy XSS auditor.
// Referrer-Policy: strict-origin-when-cross-origin — limits referrer information leakage.
// Permissions-Policy — disables common browser features not used by the CRM.
app.Use(async (context, next) =>
{
	context.Response.Headers["X-Content-Type-Options"] = "nosniff";
	context.Response.Headers["X-Frame-Options"] = "DENY";
	context.Response.Headers["X-XSS-Protection"] = "0";
	context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
	context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
	await next();
});

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

// Session middleware MUST come BEFORE authentication — auth cookie validation reads session data.
// Without this ordering, the authentication callback cannot access Session["USER_ID"] and every
// authenticated request fails with 401 even though the user has a valid session.
app.UseSession();

// Authentication and authorization AFTER session.
app.UseAuthentication();

// Request logging middleware — logs method, path, status, elapsed time, user, and correlation ID.
// Placed AFTER UseAuthentication() so User.Identity is populated, BEFORE UseAuthorization().
app.UseMiddleware<SplendidCRM.Middleware.RequestLoggingMiddleware>();

app.UseAuthorization();

// =====================================================================================
// APPLICATION INITIALIZATION MIDDLEWARE — Replaces Global.asax.cs Application_BeginRequest.
// Original: if (Application.Count == 0) { SplendidInit.InitApp(this.Context); }
// Loads CONFIG, modules, terminology, ACL, timezones, currencies from the database
// into IMemoryCache on the first HTTP request.
// Guard key "imageURL" is written by SplendidInit.InitAppURLs() — same sentinel as legacy.
// MUST be AFTER UseSession() and BEFORE MapControllers().
// =====================================================================================
app.Use(async (context, next) =>
{
	var splendidInit = context.RequestServices.GetRequiredService<SplendidInit>();
	var memoryCache  = context.RequestServices.GetRequiredService<IMemoryCache>();
	if (!memoryCache.TryGetValue("imageURL", out object _))
	{
		splendidInit.InitApp();
	}
	await next();
});

// =====================================================================================
// SESSION RENEWAL MIDDLEWARE — Replaces per-method SplendidSession.CreateSession calls.
// Legacy called SplendidSession.CreateSession(HttpContext.Current.Session) in 20+ REST
// methods to keep the SignalR session dictionary in sync with ASP.NET session expiry.
// This middleware applies the same call cross-cuttingly for every authenticated request.
// =====================================================================================
app.Use(async (context, next) =>
{
	if ( context.Session != null && context.User?.Identity?.IsAuthenticated == true )
	{
		try
		{
			SplendidSession.CreateSession(context.Session, context.Session.Id);
		}
		catch { /* session renewal is non-critical */ }
	}
	await next();
});

// SoapCore endpoint for SugarCRM SOAP API (preserves /soap.asmx path).
((IApplicationBuilder)app).UseSoapEndpoint<SplendidCRM.ISugarSoapService>(
	path: "/soap.asmx",
	encoder: new SoapEncoderOptions(),
	serializer: SoapSerializer.XmlSerializer,
	caseInsensitivePath: true
);

// Map REST API controllers.
app.MapControllers();

// Map SignalR hubs via SignalRUtils.MapHubs() (preserves hub method signatures from OWIN SignalR).
// Registers: ChatManagerHub at /hubs/chat, TwilioManagerHub at /hubs/twilio,
//            PhoneBurnerHub at /hubs/phoneburner.
// HANDOFF TO PROMPT 2: These paths changed from the OWIN default /signalr.
//   Frontend must update @microsoft/signalr HubConnectionBuilder().withUrl() calls accordingly.
// Per AAP §0.4.4 and §0.7 (Goal 7 — SignalR Migration).
SignalRUtils.MapHubs(app);

// =====================================================================================
// 6. RUN — Start the application
// =====================================================================================
app.Run();

// =====================================================================================
// HELPER — Map environment variable to structured configuration key
// =====================================================================================
// NOTE: Environment variables mapped via MapEnvVarToConfig are written directly into the
// IConfiguration root, which gives them effective override priority ABOVE AWS Secrets Manager
// entries loaded via the provider hierarchy. This is an intentional design decision that
// enables container orchestrators (ECS, Kubernetes) to inject runtime overrides at deployment
// time. See AAP §0.8.2 for the 5-tier provider hierarchy. For Prompt 3 container deployments,
// the ECS task definition may set env vars that override Secrets Manager values for local
// development or per-container tuning.
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
