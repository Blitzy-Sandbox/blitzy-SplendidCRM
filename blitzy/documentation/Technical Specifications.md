# Technical Specification

# 0. Agent Action Plan

## 0.1 Intent Clarification

### 0.1.1 Core Refactoring Objective

Based on the prompt, the Blitzy platform understands that the refactoring objective is to perform a **technology stack migration** of the SplendidCRM Community Edition v15.2 backend from the legacy .NET Framework 4.8 / ASP.NET WebForms / WCF / IIS platform to a modern .NET 10 ASP.NET Core MVC platform, while simultaneously decoupling all build and runtime dependencies from Windows-only / Visual Studio-only / IIS-only toolchains.

- **Refactoring type:** Tech stack migration (framework-level replatforming)
- **Target repository:** Same repository — in-place migration of backend code
- **Scope boundary:** This is Prompt 1 of 3 in a phased SplendidCRM modernization series. This prompt covers backend modernization and toolchain decoupling ONLY. Frontend modernization (Prompt 2) and containerization/AWS infrastructure (Prompt 3) are explicitly excluded.

**Refactoring Goals with Enhanced Clarity:**

- **Goal 1 — Business Logic Extraction:** Extract all 74 root-level C# utility classes from `SplendidCRM/_code/` (including `Security.cs`, `SplendidCache.cs`, `SplendidInit.cs`, `SchedulerUtils.cs`, `RestUtil.cs`, `SearchBuilder.cs`, `EmailUtils.cs`, `Sql.cs`, and 60+ others) into a standalone .NET 10 class library project, preserving MVC pattern separation of Model and Controller logic.
- **Goal 2 — REST API Conversion:** Convert the monolithic WCF REST surface in `Rest.svc.cs` (8,369 lines, 152 endpoint operations) to ASP.NET Core Web API controllers with identical route paths, HTTP methods, request/response JSON schemas, and full OData-style query support (`$filter`, `$select`, `$orderby`, `$groupby`).
- **Goal 3 — SOAP API Preservation:** Convert the WCF SOAP surface in `soap.asmx.cs` (4,641 lines, 84 SOAP methods) to SoapCore middleware preserving the `sugarsoap` namespace (`http://www.sugarcrm.com/sugarcrm`), WSDL contract, and all data carriers (`contact_detail`, `entry_value`, `name_value`, `document_revision`, etc.).
- **Goal 4 — Admin API Conversion:** Convert `Administration/Rest.svc.cs` (6,473 lines, 65 endpoint operations) and `Administration/Impersonation.svc.cs` to ASP.NET Core admin API controllers.
- **Goal 5 — DLL-to-NuGet Modernization:** Replace all 37 manually managed DLL references (sourced from `BackupBin2012/`, `BackupBin2022/`, `BackupBin2025/`) with NuGet package references.
- **Goal 6 — Application Lifecycle Migration:** Convert `Global.asax.cs` (400 lines) lifecycle management (`Application_Start`, `Session_Start`, timer initialization, `Application_End`) to `Program.cs` + three `IHostedService` implementations with configurable intervals and reentrancy guards.
- **Goal 7 — SignalR Migration:** Migrate OWIN-hosted Microsoft.AspNet.SignalR 1.2.2 (10 files in `_code/SignalR/`) to ASP.NET Core SignalR, preserving hub method signatures for ChatManager, TwilioManager, and PhoneBurnerManager.
- **Goal 8 — Distributed Session:** Migrate InProc session (20-minute timeout, configured in `Web.config`) to distributed session backed by Redis or SQL Server, selectable via `SESSION_PROVIDER` environment variable.
- **Goal 9 — Configuration Externalization:** Externalize all environment-specific configuration from `Web.config` (196 lines) to a five-tier provider hierarchy: AWS Secrets Manager → Environment variables → AWS Systems Manager Parameter Store → `appsettings.{Environment}.json` → `appsettings.json`, with mandatory startup validation and fail-fast behavior.
- **Goal 10 — Platform Independence:** Eliminate all Windows-only, IIS-only, and Visual Studio-only build dependencies so the backend builds and runs via `dotnet restore && dotnet build && dotnet run` on Linux with zero Windows dependencies.

**Implicit Requirements Surfaced:**

- Replace all `HttpContext.Current` static access (found in 31+ files across `_code/`, `Rest.svc.cs`, `soap.asmx.cs`, `Administration/Rest.svc.cs`) with `IHttpContextAccessor` dependency injection
- Replace all `Application[]` state access (found in 36 files) with `IMemoryCache` or scoped service injection
- Replace all `HttpRuntime.Cache` usage (5 files, primarily `SplendidCache.cs`) with `IMemoryCache`
- Replace all `Session[]` direct access (20 files) with distributed session-compatible patterns via `IHttpContextAccessor`
- Remove all `System.Web` namespace dependencies (65 files) in favor of `Microsoft.AspNetCore.*` equivalents
- Eliminate WCF `system.serviceModel` configuration and assembly binding redirects entirely — NuGet dependency resolution replaces both
- Preserve the P3P header intentional removal as a documented change (legacy IE iframe compatibility)
- Maintain MD5 password hashing as-is for SugarCRM backward compatibility, documenting it as technical debt

### 0.1.2 Technical Interpretation

This refactoring translates to the following technical transformation strategy:

**Current Architecture → Target Architecture Mapping:**

| Layer | Current (.NET Framework 4.8) | Target (.NET 10 ASP.NET Core) |
|---|---|---|
| Runtime | .NET Framework 4.8 (Windows-only) | .NET 10 LTS (cross-platform) |
| Web Framework | ASP.NET WebForms + WCF | ASP.NET Core MVC |
| REST API Surface | WCF REST (`Rest.svc.cs`, `[ServiceContract]`, `[WebInvoke]`) | ASP.NET Core Web API Controllers (`[ApiController]`, `[HttpPost]`) |
| SOAP API Surface | ASMX Web Service (`soap.asmx.cs`, `[SoapRpcService]`) | SoapCore middleware 1.2.1.12 |
| Admin API Surface | WCF REST (`Administration/Rest.svc.cs`) | ASP.NET Core Admin Controllers |
| Application Lifecycle | `Global.asax.cs` (`HttpApplication` events + timer-based) | `Program.cs` + 3× `IHostedService` |
| Authentication | `Web.config <authentication mode="Windows"/>` + custom `Security.cs` | ASP.NET Core authentication schemes (Negotiate, Cookie, OIDC/SAML) + custom `IAuthorizationHandler` |
| Session Management | InProc `SessionState` (20 min, `Web.config`) | Distributed cache (Redis or SQL Server via `SESSION_PROVIDER`) |
| Caching | `HttpRuntime.Cache` + `Application[]` + static dictionaries | `IMemoryCache` with equivalent keys and invalidation |
| Configuration | `Web.config` (appSettings, connectionStrings, serviceModel) | AWS Secrets Manager + Env vars + Parameter Store + `appsettings.json` |
| Data Access | `System.Data.SqlClient` (embedded in .NET Framework) | `Microsoft.Data.SqlClient` 6.1.4 NuGet |
| Real-Time | OWIN SignalR 1.2.2 (`Microsoft.AspNet.SignalR`) | ASP.NET Core SignalR (`Microsoft.AspNetCore.SignalR`) |
| JSON Serialization | Newtonsoft.Json 13.0 (binding redirect from 6.0) | System.Text.Json (primary) + Newtonsoft.Json 13.x fallback |
| TLS Enforcement | `ServicePointManager.SecurityProtocol` in `Global.asax.cs` | Kestrel HTTPS configuration |
| Dependency Management | 37 manual DLL references from `BackupBin*` folders | NuGet `<PackageReference>` in `.csproj` |
| Build System | Visual Studio 2017 MSBuild (`.csproj` with GUID project types) | `dotnet` CLI SDK-style projects |
| Hosting | IIS integrated pipeline | Kestrel (self-hosted, Linux-compatible) |

**Transformation Rules:**

- Every `[ServiceContract]` + `[WebInvoke]` WCF endpoint → equivalent `[ApiController]` + `[HttpPost]`/`[HttpGet]` attribute routing
- Every `[SoapRpcMethod]` + `[WebMethod]` SOAP operation → SoapCore `[ServiceContract]` + `[OperationContract]` interface method
- Every `HttpContext.Current.Session["key"]` → `IHttpContextAccessor.HttpContext.Session.GetString("key")` (or typed equivalent)
- Every `HttpContext.Current.Application["key"]` → `IMemoryCache.Get<T>("key")` via DI
- Every `using System.Data.SqlClient` → `using Microsoft.Data.SqlClient`
- Every `using System.Web` → replaced by appropriate `Microsoft.AspNetCore.*` equivalent
- Every `Web.config <appSettings>` value → `IConfiguration["key"]` sourced from provider hierarchy
- Timer-based `System.Timers.Timer` patterns → `IHostedService` with `PeriodicTimer` or `Task.Delay` loops

## 0.2 Source Analysis

### 0.2.1 Comprehensive Source File Discovery

The following search patterns and manual inspection identified ALL files requiring migration as part of this backend modernization:

**WCF / SOAP Service Files (3 files — direct conversion required):**

| File | Lines | Endpoints | Migration Target |
|---|---|---|---|
| `SplendidCRM/Rest.svc.cs` | 8,369 | 152 WCF operations | ASP.NET Core Web API Controllers |
| `SplendidCRM/Administration/Rest.svc.cs` | 6,473 | 65 WCF operations | ASP.NET Core Admin Controllers |
| `SplendidCRM/soap.asmx.cs` | 4,641 | 84 SOAP methods | SoapCore middleware |

**WCF Admin Impersonation Service (1 file):**

| File | Migration Target |
|---|---|
| `SplendidCRM/Administration/Impersonation.svc.cs` | ASP.NET Core controller action |

**Application Lifecycle (1 file — decomposition required):**

| File | Lines | Migration Target |
|---|---|---|
| `SplendidCRM/Global.asax.cs` | 400 | `Program.cs` + 3× `IHostedService` |

**Core Business Logic — `SplendidCRM/_code/` Root Files (74 files):**

| File | Lines | Role |
|---|---|---|
| `Security.cs` | 1,388 | Authentication, ACL enforcement, MD5 hashing, encryption |
| `SplendidCache.cs` | 11,582 | Metadata caching hub, React dictionary helpers |
| `SplendidInit.cs` | ~900 | Application bootstrap orchestrator |
| `SchedulerUtils.cs` | ~600 | Cron job scheduling, reentrancy guards, timer callbacks |
| `RestUtil.cs` | ~800 | REST serialization (DataTable→JSON), timezone math |
| `SearchBuilder.cs` | ~500 | Provider-aware WHERE clause generation |
| `EmailUtils.cs` | ~700 | Email sending, polling, campaign processing |
| `MimeUtils.cs` | ~400 | MIME message construction |
| `Sql.cs` | ~300 | SQL helper utilities, parameterized query builders |
| `SqlBuild.cs` | ~300 | Database schema builder (idempotent DDL) |
| `DbProviderFactory.cs` | ~100 | Provider-agnostic DB factory |
| `SqlClientFactory.cs` | ~50 | SqlClient-specific factory |
| `SplendidError.cs` | ~200 | Error logging/handling |
| `SplendidDynamic.cs` | ~400 | Dynamic view/edit/list rendering support |
| `SplendidExport.cs` | ~300 | Data export (CSV, Excel, XML) |
| `SplendidImport.cs` | ~400 | Data import processing |
| `SplendidControl.cs` | ~500 | Base WebForms control (migration adapter) |
| `SplendidPage.cs` | ~500 | Base WebForms page (migration adapter) |
| `SplendidCRM.cs` | ~200 | Core CRM utility class |
| `SplendidDefaults.cs` | ~100 | Default configuration values |
| `Utils.cs` | ~400 | General utility methods |
| `L10N.cs` | ~300 | Localization/internationalization |
| `Currency.cs` | ~200 | Currency formatting/conversion |
| `TimeZone.cs` | ~300 | Timezone handling |
| `GoogleApps.cs` | ~200 | Google integration utilities |
| `GoogleSync.cs` | ~300 | Google calendar/contact sync |
| `GoogleUtils.cs` | ~200 | Google API helpers |
| `ExchangeSync.cs` | ~300 | Exchange sync utilities |
| `ExchangeUtils.cs` | ~400 | Exchange API helpers |
| `iCloudSync.cs` | ~200 | iCloud sync utilities |
| `ActiveDirectory.cs` | ~300 | AD integration |
| `FacebookUtils.cs` | ~200 | Facebook integration |
| `SocialImport.cs` | ~200 | Social media import |
| `CampaignUtils.cs` | ~300 | Campaign management |
| `ModuleUtils.cs` | ~500 | Module metadata and routing |
| `ReportingUtils.cs` | ~400 | Report generation |
| `AuditView.cs` | ~200 | Audit trail display |
| Remaining ~37 files | ~100-400 each | Various CRM utilities (ACL, workflow, relationships, etc.) |

**Integration Subdirectories — 16 Dormant Stubs (358 files total):**

| Subdirectory | Files | Purpose |
|---|---|---|
| `SplendidCRM/_code/Spring.Social.Facebook/` | 109 | Facebook API integration |
| `SplendidCRM/_code/Spring.Social.Twitter/` | 77 | Twitter API integration |
| `SplendidCRM/_code/Spring.Social.Salesforce/` | 53 | Salesforce sync integration |
| `SplendidCRM/_code/Spring.Social.LinkedIn/` | 43 | LinkedIn integration |
| `SplendidCRM/_code/Spring.Social.Office365/` | 43 | Office 365 integration |
| `SplendidCRM/_code/Spring.Social.HubSpot/` | 3 | HubSpot integration |
| `SplendidCRM/_code/Spring.Social.PhoneBurner/` | 2 | PhoneBurner integration |
| `SplendidCRM/_code/Spring.Social.QuickBooks/` | 4 | QuickBooks integration |
| `SplendidCRM/_code/PayPal/` | 4 | PayPal payment processing |
| `SplendidCRM/_code/QuickBooks/` | 2 | QuickBooks (separate) |
| `SplendidCRM/_code/Excel/` | 3 | Excel import/export |
| `SplendidCRM/_code/OpenXML/` | 3 | OpenXML document handling |
| `SplendidCRM/_code/FileBrowser/` | 3 | File management UI |
| `SplendidCRM/_code/Workflow/` | 1 | Workflow engine v1 |
| `SplendidCRM/_code/Workflow4/` | 1 | Workflow engine v4 |
| `SplendidCRM/_code/mono/` | 1 | Mono compatibility layer |

**Active Integration Subdirectories — 2 (migrated as active components, not stubs):**

| Subdirectory | Files | Purpose |
|---|---|---|
| `SplendidCRM/_code/DuoUniversal/` | 7 | DuoUniversal 2FA (active authentication) |
| `SplendidCRM/_code/SignalR/` | 10 | Real-time hubs: Chat, Twilio, PhoneBurner |

**Configuration Files (2 files — replaced/eliminated):**

| File | Lines | Migration Target |
|---|---|---|
| `SplendidCRM/Web.config` | 196 | `appsettings.json` + env vars + Secrets Manager + Parameter Store |
| `SplendidCRM/SplendidCRM7_VS2017.csproj` | 250+ | New SDK-style `.csproj` with `<PackageReference>` entries |

**Root-Level Application Files (8 additional .cs files):**

| File | Purpose |
|---|---|
| `SplendidCRM/AssemblyInfo.cs` | Assembly metadata — replace with `<PropertyGroup>` attributes |
| `SplendidCRM/default.aspx.cs` | Default page — React SPA redirect |
| `SplendidCRM/SystemCheck.aspx.cs` | System diagnostics — convert to health check endpoint |
| `SplendidCRM/TwiML.aspx.cs` | Twilio webhook handler — convert to controller |
| `SplendidCRM/campaign_trackerv2.aspx.cs` | Campaign tracking — convert to controller |
| `SplendidCRM/image.aspx.cs` | Image serving — convert to controller/middleware |
| `SplendidCRM/RemoveMe.aspx.cs` | Unsubscribe handler — convert to controller |
| `SplendidCRM/Administration/default.aspx.cs` | Admin default page |

### 0.2.2 Current Structure Mapping

```
Current:
SplendidCRM/
├── Global.asax.cs                          (400 lines — lifecycle → Program.cs + IHostedService)
├── Rest.svc.cs                             (8,369 lines — WCF REST → Web API Controllers)
├── soap.asmx.cs                            (4,641 lines — SOAP → SoapCore middleware)
├── Web.config                              (196 lines — eliminated/externalized)
├── SplendidCRM7_VS2017.csproj              (250+ lines — replaced with SDK-style)
├── AssemblyInfo.cs                         (assembly metadata)
├── default.aspx.cs                         (SPA redirect)
├── SystemCheck.aspx.cs                     (health check source)
├── TwiML.aspx.cs                           (Twilio webhook)
├── campaign_trackerv2.aspx.cs              (campaign tracking)
├── image.aspx.cs                           (image serving)
├── RemoveMe.aspx.cs                        (unsubscribe handler)
├── _code/                                  (443 .cs files total)
│   ├── Security.cs                         (1,388 lines — auth/ACL)
│   ├── SplendidCache.cs                    (11,582 lines — caching hub)
│   ├── SplendidInit.cs                     (~900 lines — app bootstrap)
│   ├── SchedulerUtils.cs                   (~600 lines — scheduler)
│   ├── RestUtil.cs                         (~800 lines — REST serialization)
│   ├── SearchBuilder.cs                    (~500 lines — SQL WHERE builder)
│   ├── EmailUtils.cs                       (~700 lines — email operations)
│   ├── DbProviderFactory.cs                (DB provider factory)
│   ├── SqlClientFactory.cs                 (SqlClient factory)
│   ├── [66 more root .cs files]            (various CRM utilities)
│   ├── DuoUniversal/                       (7 files — active 2FA)
│   ├── SignalR/                             (10 files — active real-time)
│   ├── Spring.Social.Facebook/             (109 files — stub)
│   ├── Spring.Social.Twitter/              (77 files — stub)
│   ├── Spring.Social.Salesforce/           (53 files — stub)
│   ├── Spring.Social.LinkedIn/             (43 files — stub)
│   ├── Spring.Social.Office365/            (43 files — stub)
│   ├── Spring.Social.HubSpot/             (3 files — stub)
│   ├── Spring.Social.PhoneBurner/          (2 files — stub)
│   ├── Spring.Social.QuickBooks/           (4 files — stub)
│   ├── PayPal/                             (4 files — stub)
│   ├── QuickBooks/                         (2 files — stub)
│   ├── Excel/                              (3 files — stub)
│   ├── OpenXML/                            (3 files — stub)
│   ├── FileBrowser/                        (3 files — stub)
│   ├── Workflow/                           (1 file — stub)
│   ├── Workflow4/                          (1 file — stub)
│   └── mono/                               (1 file — stub)
├── Administration/
│   ├── Rest.svc.cs                         (6,473 lines — WCF REST → Admin Controllers)
│   ├── Impersonation.svc.cs                (WCF → Admin Controller)
│   ├── default.aspx.cs                     (admin page)
│   └── [9 more .ascx.cs view code-behinds]
├── React/                                  (EXCLUDED — Prompt 2)
├── Angular/                                (EXCLUDED — out of scope entirely)
├── html5/                                  (EXCLUDED — out of scope entirely)
└── [60+ module folders]/                   (WebForms code-behinds, ~1,064 .cs files)
```

**Quantitative Summary:**

| Category | File Count | Line Estimate |
|---|---|---|
| WCF/SOAP Services | 4 | ~20,000 |
| Application Lifecycle | 1 | ~400 |
| Core Business Logic (_code root) | 74 | ~25,000 |
| Integration Stubs (16 subdirs) | 358 | ~30,000 |
| Active Integrations (DuoUniversal + SignalR) | 17 | ~3,000 |
| Configuration/Project Files | 2 | ~450 |
| Root Application Files | 8 | ~1,500 |
| Administration Root Files | 12 | ~13,500 |
| **Total In-Scope Backend Files** | **~476** | **~94,000** |

## 0.3 Scope Boundaries

### 0.3.1 Exhaustively In Scope

**Source Transformations (backend C# code):**

- `SplendidCRM/Rest.svc.cs` — WCF REST → ASP.NET Core Web API controllers
- `SplendidCRM/soap.asmx.cs` — ASMX SOAP → SoapCore middleware
- `SplendidCRM/Administration/Rest.svc.cs` — WCF REST → ASP.NET Core admin controllers
- `SplendidCRM/Administration/Impersonation.svc.cs` — WCF → ASP.NET Core controller
- `SplendidCRM/Global.asax.cs` — Lifecycle → `Program.cs` + `IHostedService`
- `SplendidCRM/_code/*.cs` — All 74 root business logic files → .NET 10 class library
- `SplendidCRM/_code/DuoUniversal/*.cs` — Active 2FA integration → .NET 10
- `SplendidCRM/_code/SignalR/*.cs` — OWIN SignalR → ASP.NET Core SignalR
- `SplendidCRM/_code/Spring.Social.*/**/*.cs` — 8 integration stub dirs → .NET 10 compilation
- `SplendidCRM/_code/PayPal/*.cs` — Payment stub → .NET 10 compilation
- `SplendidCRM/_code/QuickBooks/*.cs` — Accounting stub → .NET 10 compilation
- `SplendidCRM/_code/Excel/*.cs` — Excel export stub → .NET 10 compilation
- `SplendidCRM/_code/OpenXML/*.cs` — Document stub → .NET 10 compilation
- `SplendidCRM/_code/FileBrowser/*.cs` — File management stub → .NET 10 compilation
- `SplendidCRM/_code/Workflow/*.cs` — Workflow v1 stub → .NET 10 compilation
- `SplendidCRM/_code/Workflow4/*.cs` — Workflow v4 stub → .NET 10 compilation
- `SplendidCRM/_code/mono/*.cs` — Mono compat stub → .NET 10 compilation
- `SplendidCRM/AssemblyInfo.cs` — Replace with `<PropertyGroup>` in SDK-style csproj
- `SplendidCRM/SystemCheck.aspx.cs` — Convert to health check endpoint
- `SplendidCRM/TwiML.aspx.cs` — Convert to ASP.NET Core controller
- `SplendidCRM/campaign_trackerv2.aspx.cs` — Convert to ASP.NET Core controller
- `SplendidCRM/image.aspx.cs` — Convert to ASP.NET Core controller/middleware
- `SplendidCRM/RemoveMe.aspx.cs` — Convert to ASP.NET Core controller
- `SplendidCRM/default.aspx.cs` — Convert to SPA redirect middleware

**Configuration Transformations:**

- `SplendidCRM/Web.config` — Decompose into externalized configuration providers
- `SplendidCRM/SplendidCRM7_VS2017.csproj` — Replace with SDK-style `.csproj` files

**Dependency Modernization:**

- `BackupBin2012/*.dll` — 22 DLLs → NuGet packages
- `BackupBin2022/*.dll` — 2 DLLs → NuGet packages
- `BackupBin2025/*.dll` — 13 DLLs → NuGet packages or framework-included

**New Files to Create:**

- `SplendidCRM.sln` — New .NET 10 solution file
- `src/SplendidCRM.Core/SplendidCRM.Core.csproj` — Class library project
- `src/SplendidCRM.Web/SplendidCRM.Web.csproj` — ASP.NET Core MVC web project
- `src/SplendidCRM.Web/Program.cs` — Application entry point with provider registration and startup validation
- `src/SplendidCRM.Web/appsettings.json` — Base configuration defaults
- `src/SplendidCRM.Web/appsettings.Development.json` — Development overrides
- `src/SplendidCRM.Web/appsettings.Staging.json` — Staging overrides
- `src/SplendidCRM.Web/appsettings.Production.json` — Production overrides
- `src/SplendidCRM.Web/Controllers/RestController.cs` — Primary REST API controller(s)
- `src/SplendidCRM.Web/Controllers/AdminRestController.cs` — Admin REST API controller(s)
- `src/SplendidCRM.Web/Controllers/ImpersonationController.cs` — Admin impersonation
- `src/SplendidCRM.Web/Services/SchedulerHostedService.cs` — Scheduler background service
- `src/SplendidCRM.Web/Services/EmailPollingHostedService.cs` — Email polling background service
- `src/SplendidCRM.Web/Services/ArchiveHostedService.cs` — Archive background service
- `src/SplendidCRM.Web/Middleware/SoapServiceMiddleware.cs` — SoapCore registration
- `src/SplendidCRM.Web/Hubs/ChatManagerHub.cs` — ASP.NET Core SignalR hub
- `src/SplendidCRM.Web/Hubs/TwilioManagerHub.cs` — ASP.NET Core SignalR hub
- `src/SplendidCRM.Web/Authorization/ModuleAuthorizationHandler.cs` — 4-tier ACL handler
- Health check endpoint at `/api/health`

**Import Corrections:**

- Every file containing `using System.Web` (65 files in `_code/`)
- Every file containing `using System.Data.SqlClient` (5 files)
- Every file containing `HttpContext.Current` (31 files)
- Every file containing `Application[` (36 files)
- Every file containing `HttpRuntime.Cache` (5 files)
- Every file containing `Session[` (20 files)
- Every file referencing WCF namespaces (`System.ServiceModel`, `System.ServiceModel.Web`, `System.ServiceModel.Activation`)

**Documentation Updates:**

- `README.md` — Update build instructions, runtime requirements, architecture description

### 0.3.2 Explicitly Out of Scope

The following items are explicitly excluded from this migration effort per user directives:

**Frontend Code (Prompt 2):**
- `SplendidCRM/React/` — All React SPA code, TypeScript, npm, Webpack — handled by Prompt 2
- `SplendidCRM/Angular/` — Entire Angular client — excluded from modernization entirely
- `SplendidCRM/html5/` — Entire HTML5 legacy client — excluded from modernization entirely
- `SplendidCRM/Include/` — Shared JavaScript utility scripts

**SQL Server Schema (Zero DDL Changes):**
- `SQL Scripts Community/` — All SQL build pipeline content and `Build.sql` artifact
- All database tables, views, stored procedures, functions, and triggers — zero modifications

**Infrastructure (Prompt 3):**
- Dockerfiles and container configuration
- AWS infrastructure: Terraform, ECR, ECS, ALB, RDS
- CI/CD pipeline definitions

**Feature Additions:**
- No new CRM features or module additions
- No Enterprise Edition integration connector activation
- No code optimization or improvement beyond what the framework migration requires

**Security Exceptions:**
- MD5 password hashing — preserve as-is, document as known technical debt
- Address security vulnerabilities ONLY if critical (CVSS > 7.0) or introduced by the migration itself

**WebForms Module Code-Behinds (~1,064 files):**
- `SplendidCRM/Accounts/*.aspx.cs`, `SplendidCRM/Contacts/*.aspx.cs`, etc. — WebForms pages are NOT in scope for this backend migration; they are legacy presentation layer artifacts. Only the backend business logic they depend on (in `_code/`) is migrated.
- `SplendidCRM/_controls/*.cs` (44 files) — WebForms user controls
- `SplendidCRM/_devtools/*.cs` (8 files) — Developer tools
- `SplendidCRM/Administration/*.ascx.cs` (9 view code-behinds) — Admin WebForms views

**Documentation Build Pipeline:**
- `SQL Scripts Community/Build.bat` and `Build.sql` artifact — unchanged

## 0.4 Target Design

### 0.4.1 Refactored Structure Planning

The target structure follows a two-project .NET 10 solution: a class library for extracted business logic and an ASP.NET Core MVC web project for hosting, API controllers, middleware, and background services.

```
Target:
SplendidCRM.sln
src/
├── SplendidCRM.Core/                               (Class Library — Business Logic)
│   ├── SplendidCRM.Core.csproj                     (SDK-style, net10.0, NuGet refs)
│   ├── Security.cs                                 (Auth/ACL → IHttpContextAccessor DI)
│   ├── SplendidCache.cs                            (→ IMemoryCache injection)
│   ├── SplendidInit.cs                             (App bootstrap → service methods)
│   ├── SchedulerUtils.cs                           (Job execution logic)
│   ├── RestUtil.cs                                 (REST serialization)
│   ├── SearchBuilder.cs                            (SQL WHERE builder)
│   ├── EmailUtils.cs                               (Email operations)
│   ├── MimeUtils.cs                                (MIME construction)
│   ├── Sql.cs                                      (SQL helpers)
│   ├── SqlBuild.cs                                 (Schema builder)
│   ├── DbProviderFactory.cs                        (→ Microsoft.Data.SqlClient)
│   ├── SqlClientFactory.cs                         (→ Microsoft.Data.SqlClient)
│   ├── SplendidError.cs                            (Error logging)
│   ├── SplendidDynamic.cs                          (Dynamic rendering support)
│   ├── SplendidExport.cs                           (Data export)
│   ├── SplendidImport.cs                           (Data import)
│   ├── SplendidControl.cs                          (Base control adapter)
│   ├── SplendidPage.cs                             (Base page adapter)
│   ├── SplendidCRM.cs                              (Core utility)
│   ├── SplendidDefaults.cs                         (Defaults)
│   ├── Utils.cs                                    (General utilities)
│   ├── L10N.cs                                     (Localization)
│   ├── Currency.cs                                 (Currency handling)
│   ├── TimeZone.cs                                 (Timezone handling)
│   ├── CampaignUtils.cs                            (Campaign logic)
│   ├── ModuleUtils.cs                              (Module metadata)
│   ├── ReportingUtils.cs                           (Reporting)
│   ├── AuditView.cs                                (Audit trail)
│   ├── ActiveDirectory.cs                          (AD integration)
│   ├── ExchangeSync.cs                             (Exchange sync)
│   ├── ExchangeUtils.cs                            (Exchange helpers)
│   ├── GoogleApps.cs                               (Google integration)
│   ├── GoogleSync.cs                               (Google sync)
│   ├── GoogleUtils.cs                              (Google helpers)
│   ├── iCloudSync.cs                               (iCloud sync)
│   ├── FacebookUtils.cs                            (Facebook integration)
│   ├── SocialImport.cs                             (Social media import)
│   ├── [remaining ~37 _code root files]            (All other utilities)
│   ├── DuoUniversal/                               (7 files — active 2FA)
│   │   ├── Client.cs
│   │   ├── ClientBuilder.cs
│   │   ├── CertificatePinnerFactory.cs
│   │   ├── DuoException.cs
│   │   ├── JwtUtils.cs
│   │   ├── Labels.cs
│   │   ├── Models.cs
│   │   └── Utils.cs
│   └── Integrations/                               (16 dormant stub subdirs)
│       ├── Spring.Social.Facebook/                 (109 files)
│       ├── Spring.Social.Twitter/                  (77 files)
│       ├── Spring.Social.Salesforce/               (53 files)
│       ├── Spring.Social.LinkedIn/                 (43 files)
│       ├── Spring.Social.Office365/                (43 files)
│       ├── Spring.Social.HubSpot/                  (3 files)
│       ├── Spring.Social.PhoneBurner/              (2 files)
│       ├── Spring.Social.QuickBooks/               (4 files)
│       ├── PayPal/                                 (4 files)
│       ├── QuickBooks/                             (2 files)
│       ├── Excel/                                  (3 files)
│       ├── OpenXML/                                (3 files)
│       ├── FileBrowser/                            (3 files)
│       ├── Workflow/                               (1 file)
│       ├── Workflow4/                              (1 file)
│       └── mono/                                   (1 file)
│
├── SplendidCRM.Web/                                (ASP.NET Core MVC — Hosting)
│   ├── SplendidCRM.Web.csproj                      (SDK-style, net10.0, refs SplendidCRM.Core)
│   ├── Program.cs                                  (Entry point: config providers, middleware, validation)
│   ├── appsettings.json                            (Base defaults)
│   ├── appsettings.Development.json                (Dev overrides)
│   ├── appsettings.Staging.json                    (Staging overrides)
│   ├── appsettings.Production.json                 (Prod overrides)
│   ├── Controllers/
│   │   ├── RestController.cs                       (Main REST API — from Rest.svc.cs)
│   │   ├── AdminRestController.cs                  (Admin REST — from Administration/Rest.svc.cs)
│   │   ├── ImpersonationController.cs              (From Impersonation.svc.cs)
│   │   ├── CampaignTrackerController.cs            (From campaign_trackerv2.aspx.cs)
│   │   ├── ImageController.cs                      (From image.aspx.cs)
│   │   ├── UnsubscribeController.cs                (From RemoveMe.aspx.cs)
│   │   ├── TwiMLController.cs                      (From TwiML.aspx.cs)
│   │   └── HealthCheckController.cs                (New — /api/health)
│   ├── Services/
│   │   ├── SchedulerHostedService.cs               (IHostedService — from Global.asax.cs OnTimer)
│   │   ├── EmailPollingHostedService.cs            (IHostedService — from Global.asax.cs email timer)
│   │   ├── ArchiveHostedService.cs                 (IHostedService — from Global.asax.cs OnArchiveTimer)
│   │   └── CacheInvalidationService.cs             (vwSYSTEM_EVENTS monitoring)
│   ├── Soap/
│   │   ├── ISugarSoapService.cs                    (Service interface from soap.asmx.cs)
│   │   ├── SugarSoapService.cs                     (Implementation)
│   │   └── DataCarriers.cs                         (contact_detail, entry_value, name_value DTOs)
│   ├── Hubs/
│   │   ├── ChatManagerHub.cs                       (ASP.NET Core SignalR)
│   │   ├── TwilioManagerHub.cs                     (ASP.NET Core SignalR)
│   │   └── PhoneBurnerHub.cs                       (ASP.NET Core SignalR)
│   ├── SignalR/
│   │   ├── ChatManager.cs                          (Chat business logic)
│   │   ├── TwilioManager.cs                        (Twilio business logic)
│   │   ├── PhoneBurnerManager.cs                   (PhoneBurner logic)
│   │   ├── SignalRUtils.cs                         (SignalR utilities)
│   │   └── SplendidHubAuthorize.cs                 (Hub authorization filter)
│   ├── Authentication/
│   │   ├── WindowsAuthenticationSetup.cs           (Negotiate/NTLM scheme)
│   │   ├── FormsAuthenticationSetup.cs             (Cookie auth with custom login)
│   │   ├── SsoAuthenticationSetup.cs               (OIDC/SAML middleware)
│   │   └── DuoTwoFactorSetup.cs                   (DuoUniversal 2FA integration)
│   ├── Authorization/
│   │   ├── ModuleAuthorizationHandler.cs           (Module-level ACL)
│   │   ├── TeamAuthorizationHandler.cs             (Team-level ACL)
│   │   ├── FieldAuthorizationHandler.cs            (Field-level ACL)
│   │   ├── RecordAuthorizationHandler.cs           (Record-level ACL)
│   │   └── SecurityFilterMiddleware.cs             (Security.Filter SQL predicate injection)
│   ├── Middleware/
│   │   ├── SpaRedirectMiddleware.cs                (React SPA URL rewriting — from Global.asax.cs)
│   │   └── CookiePolicySetup.cs                    (SameSite/Secure — from Global.asax.cs)
│   └── Configuration/
│       ├── AwsSecretsManagerProvider.cs            (AWS Secrets Manager config provider)
│       ├── AwsParameterStoreProvider.cs            (AWS SSM Parameter Store provider)
│       └── StartupValidator.cs                     (Required config validation + fail-fast)
│
└── README.md                                       (Updated build/run instructions)
```

### 0.4.2 Web Search Research Conducted

- **.NET 10 LTS release:** Released November 11, 2025 with Long-Term Support until November 2028. Ships with C# 14, ASP.NET Core 10, and full cross-platform support on Linux. Default container images now use Ubuntu.
- **SoapCore NuGet:** Latest stable version 1.2.1.12 (December 11, 2025). Supports ASP.NET Core 3.1+ with endpoint routing. Compatible with `[ServiceContract]` and `[OperationContract]` attributes and `XmlSerializer` serialization.
- **Microsoft.Data.SqlClient:** Latest stable version 6.1.4 — direct drop-in replacement for `System.Data.SqlClient` with identical `SqlConnection`, `SqlCommand`, `SqlDataReader`, `SqlDataAdapter` API surface. Supports .NET 8+ and .NET Framework 4.6.2+.
- **MailKit:** Latest stable version 4.15.0 — cross-platform .NET mail client supporting SMTP, IMAP, POP3 with full async support.
- **ASP.NET Core SignalR:** Ships as part of the .NET 10 framework; replaces `Microsoft.AspNet.SignalR` 1.2.2 with `Microsoft.AspNetCore.SignalR`.
- **ASP.NET Core Identity + OIDC:** Built into .NET 10 framework; supports Negotiate (Windows Auth), Cookie, OpenID Connect, and SAML authentication schemes.

### 0.4.3 Design Pattern Applications

- **Service layer pattern** — Business logic classes from `_code/` are wrapped as injectable services via DI, replacing static class access with constructor injection
- **Dependency injection** — All `HttpContext.Current`, `Application[]`, `HttpRuntime.Cache`, and `Session[]` static access patterns replaced with `IHttpContextAccessor`, `IMemoryCache`, and `IDistributedCache` injected via constructor
- **Repository pattern (preserved)** — Existing `DbProviderFactory`/`SqlClientFactory` data access pattern maintained with `Microsoft.Data.SqlClient` swap
- **Background service pattern** — Three `IHostedService` implementations replace timer-based approach in `Global.asax.cs`, each with `SemaphoreSlim(1,1)` reentrancy guards matching existing `bInsideTimer`/`bInsideArchiveTimer` patterns
- **Middleware pipeline pattern** — SPA redirect, cookie policy, CORS, and SoapCore registered as ASP.NET Core middleware replacing IIS pipeline hooks
- **Configuration provider pattern** — Five-tier hierarchical configuration with AWS Secrets Manager at top priority, cascading through env vars, Parameter Store, and JSON files
- **Authorization handler pattern** — Four custom `IAuthorizationHandler` implementations replicating the 4-tier ACL model (Module → Team → Field → Record) from `Security.cs`

### 0.4.4 Handoff Documentation

**Handoff to Prompt 2 (Frontend Modernization):**
- REST API base path: `/Rest.svc/` compatibility route maintained (or `/api/` with redirect)
- SignalR hub negotiation endpoint: `/hubs/chat`, `/hubs/twilio` (document any path changes from OWIN SignalR `/signalr` default)
- JSON serialization: System.Text.Json primary with Newtonsoft.Json 13.x fallback; document any edge-case serialization differences (e.g., `DateTime` format, `null` handling, camelCase defaults)
- CORS configuration: `CORS_ORIGINS` env var defines allowed origins
- Health check endpoint: `GET /api/health` returning `200 OK` with JSON status

**Handoff to Prompt 3 (Containerization & AWS):**
- `dotnet publish` output: `src/SplendidCRM.Web/bin/Release/net10.0/publish/`
- Required environment variables: 18 variables documented in Configuration Externalization section
- Health check: `GET /api/health` → `200 OK` `{"status":"Healthy"}`
- Kestrel port: configurable via `ASPNETCORE_URLS` (default `http://+:5000`)
- Volume mounts: temp file directory for campaign tracker, export files
- IAM requirement: ECS Task Role requires `kms:Decrypt` on CMK + `secretsmanager:GetSecretValue`

## 0.5 Transformation Mapping

### 0.5.1 File-by-File Transformation Plan

The entire refactor is executed in **ONE phase**. Every target file is mapped to a source file. Transformation modes:
- **UPDATE** — Modify an existing file for .NET 10 compatibility
- **CREATE** — Create a new file (new infrastructure, decomposed from existing)
- **REFERENCE** — Use an existing file as a pattern reference

**Solution and Project Files:**

| Target File | Transformation | Source File | Key Changes |
|---|---|---|---|
| `SplendidCRM.sln` | CREATE | `SplendidCRM/SplendidCRM7_VS2017.csproj` | New .NET 10 solution with two projects |
| `src/SplendidCRM.Core/SplendidCRM.Core.csproj` | CREATE | `SplendidCRM/SplendidCRM7_VS2017.csproj` | SDK-style class library, net10.0 TFM, NuGet PackageReferences |
| `src/SplendidCRM.Web/SplendidCRM.Web.csproj` | CREATE | `SplendidCRM/SplendidCRM7_VS2017.csproj` | SDK-style web project, net10.0 TFM, ProjectReference to Core |

**Application Entry Point and Configuration:**

| Target File | Transformation | Source File | Key Changes |
|---|---|---|---|
| `src/SplendidCRM.Web/Program.cs` | CREATE | `SplendidCRM/Global.asax.cs` | 5-tier config provider registration, middleware pipeline, DI container, startup validation, fail-fast on missing config |
| `src/SplendidCRM.Web/appsettings.json` | CREATE | `SplendidCRM/Web.config` | Base defaults extracted from Web.config appSettings, connectionStrings placeholder |
| `src/SplendidCRM.Web/appsettings.Development.json` | CREATE | `SplendidCRM/Web.config` | Development-specific overrides |
| `src/SplendidCRM.Web/appsettings.Staging.json` | CREATE | `SplendidCRM/Web.config` | Staging-specific overrides |
| `src/SplendidCRM.Web/appsettings.Production.json` | CREATE | `SplendidCRM/Web.config` | Production-specific overrides |
| `src/SplendidCRM.Web/Configuration/AwsSecretsManagerProvider.cs` | CREATE | `SplendidCRM/Web.config` | AWS Secrets Manager IConfigurationProvider for DB creds, SMTP, SSO secrets, Duo keys |
| `src/SplendidCRM.Web/Configuration/AwsParameterStoreProvider.cs` | CREATE | `SplendidCRM/Web.config` | AWS SSM Parameter Store IConfigurationProvider for non-secret env config |
| `src/SplendidCRM.Web/Configuration/StartupValidator.cs` | CREATE | `SplendidCRM/Global.asax.cs` | Validate required config at startup, fail-fast with descriptive error |

**REST API Controllers (from WCF):**

| Target File | Transformation | Source File | Key Changes |
|---|---|---|---|
| `src/SplendidCRM.Web/Controllers/RestController.cs` | CREATE | `SplendidCRM/Rest.svc.cs` | Convert 152 WCF `[WebInvoke]` operations to `[HttpPost]`/`[HttpGet]` controller actions; preserve exact route paths `/Rest.svc/{operation}` via attribute routing |
| `src/SplendidCRM.Web/Controllers/AdminRestController.cs` | CREATE | `SplendidCRM/Administration/Rest.svc.cs` | Convert 65 WCF operations to admin controller actions; preserve routes |
| `src/SplendidCRM.Web/Controllers/ImpersonationController.cs` | CREATE | `SplendidCRM/Administration/Impersonation.svc.cs` | Convert WCF impersonation service to controller |
| `src/SplendidCRM.Web/Controllers/HealthCheckController.cs` | CREATE | `SplendidCRM/SystemCheck.aspx.cs` | New `/api/health` endpoint; reference SystemCheck for diagnostic patterns |
| `src/SplendidCRM.Web/Controllers/CampaignTrackerController.cs` | CREATE | `SplendidCRM/campaign_trackerv2.aspx.cs` | Convert ASPX campaign tracker to controller |
| `src/SplendidCRM.Web/Controllers/ImageController.cs` | CREATE | `SplendidCRM/image.aspx.cs` | Convert ASPX image serving to controller |
| `src/SplendidCRM.Web/Controllers/UnsubscribeController.cs` | CREATE | `SplendidCRM/RemoveMe.aspx.cs` | Convert ASPX unsubscribe to controller |
| `src/SplendidCRM.Web/Controllers/TwiMLController.cs` | CREATE | `SplendidCRM/TwiML.aspx.cs` | Convert Twilio webhook handler to controller |

**SOAP Service (SoapCore):**

| Target File | Transformation | Source File | Key Changes |
|---|---|---|---|
| `src/SplendidCRM.Web/Soap/ISugarSoapService.cs` | CREATE | `SplendidCRM/soap.asmx.cs` | Extract `[ServiceContract]` interface with `[OperationContract]` methods from 84 SOAP methods; preserve `sugarsoap` namespace |
| `src/SplendidCRM.Web/Soap/SugarSoapService.cs` | CREATE | `SplendidCRM/soap.asmx.cs` | Implementation class; migrate all SOAP method bodies |
| `src/SplendidCRM.Web/Soap/DataCarriers.cs` | CREATE | `SplendidCRM/soap.asmx.cs` | Extract serializable DTOs: `contact_detail`, `entry_value`, `name_value`, `document_revision`, etc. with identical XML serialization attributes |

**Background Services (from Global.asax.cs timers):**

| Target File | Transformation | Source File | Key Changes |
|---|---|---|---|
| `src/SplendidCRM.Web/Services/SchedulerHostedService.cs` | CREATE | `SplendidCRM/Global.asax.cs` | `IHostedService` wrapping `SchedulerUtils.OnTimer`; interval from `SCHEDULER_INTERVAL_MS` env var; `SemaphoreSlim(1,1)` reentrancy guard replacing `bInsideTimer` |
| `src/SplendidCRM.Web/Services/EmailPollingHostedService.cs` | CREATE | `SplendidCRM/Global.asax.cs` | `IHostedService` for email polling; interval from `EMAIL_POLL_INTERVAL_MS`; reentrancy guard |
| `src/SplendidCRM.Web/Services/ArchiveHostedService.cs` | CREATE | `SplendidCRM/Global.asax.cs` | `IHostedService` wrapping `SchedulerUtils.OnArchiveTimer`; interval from `ARCHIVE_INTERVAL_MS`; `SemaphoreSlim(1,1)` replacing `bInsideArchiveTimer` |
| `src/SplendidCRM.Web/Services/CacheInvalidationService.cs` | CREATE | `SplendidCRM/_code/SchedulerUtils.cs` | Background monitoring of `vwSYSTEM_EVENTS` for cache invalidation |

**SignalR Hubs (from OWIN SignalR):**

| Target File | Transformation | Source File | Key Changes |
|---|---|---|---|
| `src/SplendidCRM.Web/Hubs/ChatManagerHub.cs` | CREATE | `SplendidCRM/_code/SignalR/ChatManagerHub.cs` | Rewrite as ASP.NET Core `Hub<T>`; preserve method signatures |
| `src/SplendidCRM.Web/Hubs/TwilioManagerHub.cs` | CREATE | `SplendidCRM/_code/SignalR/TwilioManagerHub.cs` | Rewrite as ASP.NET Core `Hub<T>`; preserve method signatures |
| `src/SplendidCRM.Web/Hubs/PhoneBurnerHub.cs` | CREATE | `SplendidCRM/_code/SignalR/PhoneBurnerManager.cs` | Rewrite as ASP.NET Core `Hub<T>` |
| `src/SplendidCRM.Web/SignalR/ChatManager.cs` | UPDATE | `SplendidCRM/_code/SignalR/ChatManager.cs` | Replace OWIN hub context with ASP.NET Core `IHubContext<T>` |
| `src/SplendidCRM.Web/SignalR/TwilioManager.cs` | UPDATE | `SplendidCRM/_code/SignalR/TwilioManager.cs` | Replace OWIN hub context with ASP.NET Core `IHubContext<T>` |
| `src/SplendidCRM.Web/SignalR/PhoneBurnerManager.cs` | UPDATE | `SplendidCRM/_code/SignalR/PhoneBurnerManager.cs` | Replace OWIN hub context |
| `src/SplendidCRM.Web/SignalR/SignalRUtils.cs` | UPDATE | `SplendidCRM/_code/SignalR/SignalRUtils.cs` | Remove OWIN startup; replace with `MapHub<T>` registration |
| `src/SplendidCRM.Web/SignalR/SplendidHubAuthorize.cs` | UPDATE | `SplendidCRM/_code/SignalR/SplendidHubAuthorize.cs` | Convert to ASP.NET Core `IHubFilter` or authorization requirement |

**Authentication and Authorization:**

| Target File | Transformation | Source File | Key Changes |
|---|---|---|---|
| `src/SplendidCRM.Web/Authentication/WindowsAuthenticationSetup.cs` | CREATE | `SplendidCRM/_code/Security.cs` | Configure Negotiate/NTLM scheme from `AUTH_MODE` env var |
| `src/SplendidCRM.Web/Authentication/FormsAuthenticationSetup.cs` | CREATE | `SplendidCRM/_code/Security.cs` | Cookie auth with custom login endpoint |
| `src/SplendidCRM.Web/Authentication/SsoAuthenticationSetup.cs` | CREATE | `SplendidCRM/_code/Security.cs` | OIDC/SAML middleware from `SSO_AUTHORITY`, `SSO_CLIENT_ID`, `SSO_CLIENT_SECRET` |
| `src/SplendidCRM.Web/Authentication/DuoTwoFactorSetup.cs` | CREATE | `SplendidCRM/_code/DuoUniversal/Client.cs` | DuoUniversal 2FA integration from `DUO_INTEGRATION_KEY`, `DUO_SECRET_KEY`, `DUO_API_HOSTNAME` |
| `src/SplendidCRM.Web/Authorization/ModuleAuthorizationHandler.cs` | CREATE | `SplendidCRM/_code/Security.cs` | Module-level ACL from `Security.GetUserAccess` |
| `src/SplendidCRM.Web/Authorization/TeamAuthorizationHandler.cs` | CREATE | `SplendidCRM/_code/Security.cs` | Team-level ACL from team hierarchy filters |
| `src/SplendidCRM.Web/Authorization/FieldAuthorizationHandler.cs` | CREATE | `SplendidCRM/_code/Security.cs` | Field-level ACL from `ACL_FIELD_ACCESS` |
| `src/SplendidCRM.Web/Authorization/RecordAuthorizationHandler.cs` | CREATE | `SplendidCRM/_code/Security.cs` | Record-level security filters |
| `src/SplendidCRM.Web/Authorization/SecurityFilterMiddleware.cs` | CREATE | `SplendidCRM/_code/Security.cs` | `Security.Filter` SQL predicate injection middleware |

**Middleware:**

| Target File | Transformation | Source File | Key Changes |
|---|---|---|---|
| `src/SplendidCRM.Web/Middleware/SpaRedirectMiddleware.cs` | CREATE | `SplendidCRM/Global.asax.cs` | React SPA URL rewriting from `Application_BeginRequest` |
| `src/SplendidCRM.Web/Middleware/CookiePolicySetup.cs` | CREATE | `SplendidCRM/Global.asax.cs` | SameSite/Secure cookie settings from `Session_Start` |

**Core Business Logic (Class Library):**

| Target File | Transformation | Source File | Key Changes |
|---|---|---|---|
| `src/SplendidCRM.Core/Security.cs` | UPDATE | `SplendidCRM/_code/Security.cs` | Replace `HttpContext.Current` → `IHttpContextAccessor`; replace `Session[]` → distributed session; preserve MD5 hashing with tech debt comment |
| `src/SplendidCRM.Core/SplendidCache.cs` | UPDATE | `SplendidCRM/_code/SplendidCache.cs` | Replace `HttpRuntime.Cache` + `Application[]` → `IMemoryCache`; preserve all cache keys, invalidation logic, React getters |
| `src/SplendidCRM.Core/SplendidInit.cs` | UPDATE | `SplendidCRM/_code/SplendidInit.cs` | Replace `Application` lock → DI-scoped initialization; replace `HttpContext.Current` → `IHttpContextAccessor` |
| `src/SplendidCRM.Core/SchedulerUtils.cs` | UPDATE | `SplendidCRM/_code/SchedulerUtils.cs` | Replace `HttpContext.Current` and `Application[]` → injected services; preserve `OnTimer`/`OnArchiveTimer` logic |
| `src/SplendidCRM.Core/RestUtil.cs` | UPDATE | `SplendidCRM/_code/RestUtil.cs` | Replace `HttpContext.Current` → `IHttpContextAccessor`; ensure byte-identical JSON responses |
| `src/SplendidCRM.Core/SearchBuilder.cs` | UPDATE | `SplendidCRM/_code/SearchBuilder.cs` | Replace `System.Web` dependencies |
| `src/SplendidCRM.Core/EmailUtils.cs` | UPDATE | `SplendidCRM/_code/EmailUtils.cs` | Replace `System.Web` → ASP.NET Core; replace `System.Data.SqlClient` → `Microsoft.Data.SqlClient` |
| `src/SplendidCRM.Core/MimeUtils.cs` | UPDATE | `SplendidCRM/_code/MimeUtils.cs` | Replace `System.Web` dependencies |
| `src/SplendidCRM.Core/Sql.cs` | UPDATE | `SplendidCRM/_code/Sql.cs` | Replace `System.Data.SqlClient` → `Microsoft.Data.SqlClient` |
| `src/SplendidCRM.Core/SqlBuild.cs` | UPDATE | `SplendidCRM/_code/SqlBuild.cs` | Replace `System.Data.SqlClient` → `Microsoft.Data.SqlClient` |
| `src/SplendidCRM.Core/DbProviderFactory.cs` | UPDATE | `SplendidCRM/_code/DbProviderFactory.cs` | Replace `System.Data.SqlClient` → `Microsoft.Data.SqlClient` |
| `src/SplendidCRM.Core/SqlClientFactory.cs` | UPDATE | `SplendidCRM/_code/SqlClientFactory.cs` | Replace `System.Data.SqlClient` → `Microsoft.Data.SqlClient` |
| `src/SplendidCRM.Core/*.cs` (remaining ~60 files) | UPDATE | `SplendidCRM/_code/*.cs` | Replace `System.Web` → `Microsoft.AspNetCore.*`; replace `HttpContext.Current` → `IHttpContextAccessor`; replace `Application[]` → `IMemoryCache` |

**DuoUniversal (Active 2FA — Class Library):**

| Target File | Transformation | Source File | Key Changes |
|---|---|---|---|
| `src/SplendidCRM.Core/DuoUniversal/*.cs` (7 files) | UPDATE | `SplendidCRM/_code/DuoUniversal/*.cs` | Replace any `System.Web` refs; update to .NET 10 crypto APIs if needed; preserve HMAC SHA-512, certificate pinning |

**Integration Stubs (16 subdirs — compile only):**

| Target File | Transformation | Source File | Key Changes |
|---|---|---|---|
| `src/SplendidCRM.Core/Integrations/Spring.Social.Facebook/**/*.cs` | UPDATE | `SplendidCRM/_code/Spring.Social.Facebook/**/*.cs` | Replace `System.Web` and `Spring.Rest`/`Spring.Social.Core` with .NET 10 compatible stubs; must compile |
| `src/SplendidCRM.Core/Integrations/Spring.Social.Twitter/**/*.cs` | UPDATE | `SplendidCRM/_code/Spring.Social.Twitter/**/*.cs` | Same pattern as above |
| `src/SplendidCRM.Core/Integrations/Spring.Social.Salesforce/**/*.cs` | UPDATE | `SplendidCRM/_code/Spring.Social.Salesforce/**/*.cs` | Same pattern |
| `src/SplendidCRM.Core/Integrations/Spring.Social.LinkedIn/**/*.cs` | UPDATE | `SplendidCRM/_code/Spring.Social.LinkedIn/**/*.cs` | Same pattern |
| `src/SplendidCRM.Core/Integrations/Spring.Social.Office365/**/*.cs` | UPDATE | `SplendidCRM/_code/Spring.Social.Office365/**/*.cs` | Same pattern |
| `src/SplendidCRM.Core/Integrations/Spring.Social.HubSpot/**/*.cs` | UPDATE | `SplendidCRM/_code/Spring.Social.HubSpot/**/*.cs` | Same pattern |
| `src/SplendidCRM.Core/Integrations/Spring.Social.PhoneBurner/**/*.cs` | UPDATE | `SplendidCRM/_code/Spring.Social.PhoneBurner/**/*.cs` | Same pattern |
| `src/SplendidCRM.Core/Integrations/Spring.Social.QuickBooks/**/*.cs` | UPDATE | `SplendidCRM/_code/Spring.Social.QuickBooks/**/*.cs` | Same pattern |
| `src/SplendidCRM.Core/Integrations/PayPal/**/*.cs` | UPDATE | `SplendidCRM/_code/PayPal/**/*.cs` | Replace .NET Framework refs; must compile |
| `src/SplendidCRM.Core/Integrations/QuickBooks/**/*.cs` | UPDATE | `SplendidCRM/_code/QuickBooks/**/*.cs` | Same pattern |
| `src/SplendidCRM.Core/Integrations/Excel/**/*.cs` | UPDATE | `SplendidCRM/_code/Excel/**/*.cs` | Same pattern |
| `src/SplendidCRM.Core/Integrations/OpenXML/**/*.cs` | UPDATE | `SplendidCRM/_code/OpenXML/**/*.cs` | Same pattern |
| `src/SplendidCRM.Core/Integrations/FileBrowser/**/*.cs` | UPDATE | `SplendidCRM/_code/FileBrowser/**/*.cs` | Same pattern |
| `src/SplendidCRM.Core/Integrations/Workflow/**/*.cs` | UPDATE | `SplendidCRM/_code/Workflow/**/*.cs` | Same pattern |
| `src/SplendidCRM.Core/Integrations/Workflow4/**/*.cs` | UPDATE | `SplendidCRM/_code/Workflow4/**/*.cs` | Same pattern |
| `src/SplendidCRM.Core/Integrations/mono/**/*.cs` | UPDATE | `SplendidCRM/_code/mono/**/*.cs` | Same pattern |

**Documentation:**

| Target File | Transformation | Source File | Key Changes |
|---|---|---|---|
| `README.md` | UPDATE | `README.md` | Update build/run instructions for `dotnet` CLI; update architecture description; document .NET 10 requirements |

### 0.5.2 Cross-File Dependencies

**Import Statement Updates:**

- FROM: `using System.Web;` → TO: `using Microsoft.AspNetCore.Http;` (and related ASP.NET Core namespaces)
- FROM: `using System.Web.SessionState;` → TO: removed (ASP.NET Core session via `ISession`)
- FROM: `using System.Data.SqlClient;` → TO: `using Microsoft.Data.SqlClient;`
- FROM: `using System.ServiceModel;` / `using System.ServiceModel.Web;` / `using System.ServiceModel.Activation;` → TO: removed (replaced by `[ApiController]` attributes)
- FROM: `using System.Web.Optimization;` → TO: removed (bundling handled by frontend toolchain)
- FROM: `using Microsoft.AspNet.SignalR;` → TO: `using Microsoft.AspNetCore.SignalR;`
- FROM: `using Microsoft.Owin;` / `using Microsoft.Owin.Security;` → TO: removed (replaced by ASP.NET Core middleware)

**Configuration Reference Updates:**

- FROM: `ConfigurationManager.AppSettings["key"]` → TO: `IConfiguration["key"]`
- FROM: `ConfigurationManager.ConnectionStrings["SplendidCRM"]` → TO: `IConfiguration.GetConnectionString("SplendidCRM")`
- FROM: `WebConfigurationManager.*` → TO: `IConfiguration` injected via DI

**Static Access Pattern Updates:**

- FROM: `HttpContext.Current.Session["key"]` → TO: `_httpContextAccessor.HttpContext.Session.GetString("key")`
- FROM: `HttpContext.Current.Application["key"]` → TO: `_memoryCache.Get<T>("key")`
- FROM: `HttpRuntime.Cache["key"]` → TO: `_memoryCache.Get<T>("key")`
- FROM: `HttpContext.Current.Request.*` → TO: `_httpContextAccessor.HttpContext.Request.*`
- FROM: `HttpContext.Current.Response.*` → TO: `_httpContextAccessor.HttpContext.Response.*`

### 0.5.3 Wildcard Patterns

All wildcard patterns use trailing patterns only:

- `src/SplendidCRM.Core/*.cs` — All core business logic files (UPDATE)
- `src/SplendidCRM.Core/DuoUniversal/*.cs` — All DuoUniversal files (UPDATE)
- `src/SplendidCRM.Core/Integrations/Spring.Social.*/**/*.cs` — All 8 Spring.Social stub directories (UPDATE)
- `src/SplendidCRM.Core/Integrations/PayPal/*.cs` — PayPal stubs (UPDATE)
- `src/SplendidCRM.Core/Integrations/QuickBooks/*.cs` — QuickBooks stubs (UPDATE)
- `src/SplendidCRM.Core/Integrations/Excel/*.cs` — Excel stubs (UPDATE)
- `src/SplendidCRM.Core/Integrations/OpenXML/*.cs` — OpenXML stubs (UPDATE)
- `src/SplendidCRM.Core/Integrations/FileBrowser/*.cs` — FileBrowser stubs (UPDATE)
- `src/SplendidCRM.Core/Integrations/Workflow/*.cs` — Workflow stubs (UPDATE)
- `src/SplendidCRM.Core/Integrations/Workflow4/*.cs` — Workflow4 stubs (UPDATE)
- `src/SplendidCRM.Core/Integrations/mono/*.cs` — Mono compat stubs (UPDATE)
- `src/SplendidCRM.Web/Controllers/*.cs` — All API controllers (CREATE)
- `src/SplendidCRM.Web/Services/*.cs` — All hosted services (CREATE)
- `src/SplendidCRM.Web/Soap/*.cs` — All SOAP service files (CREATE)
- `src/SplendidCRM.Web/Hubs/*.cs` — All SignalR hubs (CREATE)
- `src/SplendidCRM.Web/SignalR/*.cs` — All SignalR manager files (UPDATE)
- `src/SplendidCRM.Web/Authentication/*.cs` — All auth setup files (CREATE)
- `src/SplendidCRM.Web/Authorization/*.cs` — All authorization handlers (CREATE)
- `src/SplendidCRM.Web/Middleware/*.cs` — All middleware files (CREATE)
- `src/SplendidCRM.Web/Configuration/*.cs` — All config providers (CREATE)
- `src/SplendidCRM.Web/appsettings*.json` — All JSON config files (CREATE)

### 0.5.4 One-Phase Execution

The entire refactor is executed by Blitzy in **ONE phase**. All files listed in sections 0.5.1 through 0.5.3 are included in a single transformation pass. There is no phased rollout, no incremental migration, and no coexistence period. The source codebase transitions from .NET Framework 4.8 to .NET 10 ASP.NET Core in a single atomic operation.

## 0.6 Dependency Inventory

### 0.6.1 Key Private and Public Packages

The following table maps every manually managed DLL reference in the current codebase to its NuGet equivalent or framework-included replacement. All versions are verified from NuGet.org or the .NET 10 framework manifest.

**Third-Party DLL → NuGet Package Mappings (from BackupBin2012/):**

| Registry | Current DLL | NuGet Package | Version | Purpose | Action |
|---|---|---|---|---|---|
| NuGet | `AjaxControlToolkit.dll` (v3.0) | — | — | WebForms toolkit | **REMOVE** — WebForms only, not used in backend API |
| NuGet | `Antlr3.Runtime.dll` | — | — | Parser generator runtime | **REMOVE** — dependency of WebGrease/bundling |
| NuGet | `BouncyCastle.Crypto.dll` | `BouncyCastle.Cryptography` | 2.5.1 | Cryptographic operations (MailKit dependency) | Replace with NuGet |
| NuGet | `CKEditor.NET.dll` (v3.6.6.2) | — | — | Rich text editor server control | **REMOVE** — WebForms only |
| NuGet | `Common.Logging.dll` (v2.0) | — | — | Logging abstraction | **REMOVE** — replace with `Microsoft.Extensions.Logging` |
| NuGet | `DocumentFormat.OpenXml.dll` (v2.0) | `DocumentFormat.OpenXml` | 3.3.0 | OpenXML document manipulation | Replace with NuGet |
| NuGet | `ICSharpCode.SharpZLib.dll` (v0.84) | `SharpZipLib` | 1.4.2 | ZIP/compression | Replace with NuGet |
| NuGet | `MailKit.dll` | `MailKit` | 4.15.0 | SMTP/IMAP/POP3 email client | Replace with NuGet |
| NuGet | `Microsoft.AspNet.SignalR.Core.dll` (v1.2.2) | `Microsoft.AspNetCore.SignalR` | Framework-included | Real-time hubs | **REMOVE** DLL — use framework package |
| NuGet | `Microsoft.AspNet.SignalR.SystemWeb.dll` (v1.2.2) | — | — | SignalR IIS hosting | **REMOVE** — ASP.NET Core SignalR is self-hosted |
| NuGet | `Microsoft.Owin.dll` | — | — | OWIN middleware | **REMOVE** — replaced by ASP.NET Core middleware |
| NuGet | `Microsoft.Owin.Host.SystemWeb.dll` (v1.0) | — | — | OWIN-IIS bridge | **REMOVE** — Kestrel hosting |
| NuGet | `Microsoft.Owin.Security.dll` | — | — | OWIN security | **REMOVE** — ASP.NET Core Identity |
| NuGet | `Microsoft.Web.Infrastructure.dll` (v1.0) | — | — | Web infrastructure | **REMOVE** — .NET Framework only |
| NuGet | `MimeKit.dll` | `MimeKit` | 4.15.0 | MIME message library | Replace with NuGet |
| NuGet | `Owin.dll` (v1.0) | — | — | OWIN interface | **REMOVE** — no OWIN in ASP.NET Core |
| NuGet | `RestSharp.dll` (v104.2) | `RestSharp` | 112.1.0 | REST HTTP client (integration stubs) | Replace with NuGet |
| NuGet | `Spring.Rest.dll` (v1.1) | — | — | Spring REST framework | **REMOVE** — replace with `HttpClient` in stubs |
| NuGet | `Spring.Social.Core.dll` (v1.0) | — | — | Spring Social framework | **REMOVE** — replace with stub interfaces |
| NuGet | `TweetinCore.dll` | — | — | Twitter client library | **REMOVE** — integration stub; replace with stub interface |
| NuGet | `WebGrease.dll` | — | — | CSS/JS minification | **REMOVE** — frontend bundling out of scope |
| NuGet | `System.Web.Optimization.dll` | — | — | Bundling/minification | **REMOVE** — frontend toolchain handles this |

**Third-Party DLL → NuGet Package Mappings (from BackupBin2022/):**

| Registry | Current DLL | NuGet Package | Version | Purpose | Action |
|---|---|---|---|---|---|
| NuGet | `Newtonsoft.Json.dll` (redirect → v13.0) | `Newtonsoft.Json` | 13.0.3 | JSON serialization fallback | Replace with NuGet |
| NuGet | `Twilio.dll` (v6.0.1) | `Twilio` | 7.7.1 | Twilio SMS/Voice API | Replace with NuGet |

**Third-Party DLL → NuGet/Framework Mappings (from BackupBin2025/):**

| Registry | Current DLL | NuGet Package | Version | Purpose | Action |
|---|---|---|---|---|---|
| NuGet | `Microsoft.IdentityModel.Abstractions.dll` (v6.34) | `Microsoft.IdentityModel.Abstractions` | 8.7.0 | Identity model base | Replace with NuGet |
| NuGet | `Microsoft.IdentityModel.JsonWebTokens.dll` (v6.34) | `Microsoft.IdentityModel.JsonWebTokens` | 8.7.0 | JWT handling | Replace with NuGet |
| NuGet | `Microsoft.IdentityModel.Logging.dll` (v6.34) | `Microsoft.IdentityModel.Logging` | 8.7.0 | Identity logging | Replace with NuGet |
| NuGet | `Microsoft.IdentityModel.Tokens.dll` (v6.34) | `Microsoft.IdentityModel.Tokens` | 8.7.0 | Token validation | Replace with NuGet |
| NuGet | `Microsoft.Bcl.AsyncInterfaces.dll` (v5.0) | — | — | Async interfaces | **REMOVE** — included in .NET 10 |
| Framework | `System.Buffers.dll` (v4.0.3) | — | — | Buffer primitives | **REMOVE** — included in .NET 10 |
| Framework | `System.Memory.dll` (v4.0.1.1) | — | — | Memory/Span | **REMOVE** — included in .NET 10 |
| Framework | `System.Net.Http.Json.dll` (v5.0) | — | — | HTTP JSON extensions | **REMOVE** — included in .NET 10 |
| Framework | `System.Numerics.Vectors.dll` (v4.0) | — | — | SIMD vectors | **REMOVE** — included in .NET 10 |
| Framework | `System.Runtime.CompilerServices.Unsafe.dll` (v5.0) | — | — | Unsafe utilities | **REMOVE** — included in .NET 10 |
| Framework | `System.Text.Encodings.Web.dll` (v5.0) | — | — | HTML/URL encoding | **REMOVE** — included in .NET 10 |
| Framework | `System.Text.Json.dll` (v5.0) | — | — | JSON serialization | **REMOVE** — included in .NET 10 (System.Text.Json 10.0) |
| Framework | `System.Threading.Tasks.Extensions.dll` (v4.2) | — | — | ValueTask | **REMOVE** — included in .NET 10 |

**New NuGet Packages Required:**

| Registry | Package | Version | Purpose |
|---|---|---|---|
| NuGet | `SoapCore` | 1.2.1.12 | SOAP middleware for ASP.NET Core (replacing soap.asmx.cs) |
| NuGet | `Microsoft.Data.SqlClient` | 6.1.4 | SQL Server data access (replacing System.Data.SqlClient) |
| NuGet | `Microsoft.Extensions.Caching.StackExchangeRedis` | 10.0.0 | Redis distributed session option |
| NuGet | `Microsoft.Extensions.Caching.SqlServer` | 10.0.0 | SQL Server distributed session option |
| NuGet | `AWSSDK.SecretsManager` | 3.7.405.5 | AWS Secrets Manager config provider |
| NuGet | `AWSSDK.SSM` | 3.7.405.5 | AWS Systems Manager Parameter Store config provider |
| NuGet | `DuoUniversal` | 1.3.0 | DuoUniversal 2FA (replacing embedded implementation) |
| NuGet | `Microsoft.AspNetCore.Authentication.Negotiate` | 10.0.0 | Windows Authentication (Negotiate/NTLM) |
| NuGet | `Microsoft.AspNetCore.Authentication.OpenIdConnect` | 10.0.0 | OIDC SSO middleware |

### 0.6.2 Dependency Updates

**Import Refactoring — Files Requiring Import Updates:**

| File Pattern | Import Change | Count |
|---|---|---|
| `src/SplendidCRM.Core/*.cs` | `System.Web` → `Microsoft.AspNetCore.Http` | 65 files |
| `src/SplendidCRM.Core/*.cs` | `System.Data.SqlClient` → `Microsoft.Data.SqlClient` | 5 files |
| `src/SplendidCRM.Core/*.cs` | `HttpContext.Current` → `IHttpContextAccessor` injection | 31 files |
| `src/SplendidCRM.Core/*.cs` | `Application[` → `IMemoryCache` injection | 36 files |
| `src/SplendidCRM.Core/*.cs` | `HttpRuntime.Cache` → `IMemoryCache` injection | 5 files |
| `src/SplendidCRM.Core/*.cs` | `Session[` → distributed session via `IHttpContextAccessor` | 20 files |
| `src/SplendidCRM.Web/SignalR/*.cs` | `Microsoft.AspNet.SignalR` → `Microsoft.AspNetCore.SignalR` | 10 files |
| `src/SplendidCRM.Core/Integrations/Spring.Social.*/**/*.cs` | `Spring.Rest`/`Spring.Social.Core` → stub interfaces | 334 files |

**Import Transformation Rules:**

- Old: `using System.Web;` → New: `using Microsoft.AspNetCore.Http;`
- Old: `using System.Web.SessionState;` → New: removed
- Old: `using System.Web.Caching;` → New: `using Microsoft.Extensions.Caching.Memory;`
- Old: `using System.Web.Configuration;` → New: `using Microsoft.Extensions.Configuration;`
- Old: `using System.Data.SqlClient;` → New: `using Microsoft.Data.SqlClient;`
- Old: `using System.ServiceModel;` → New: removed (WCF endpoints converted)
- Old: `using System.ServiceModel.Web;` → New: removed
- Old: `using System.ServiceModel.Activation;` → New: removed
- Old: `using Microsoft.AspNet.SignalR;` → New: `using Microsoft.AspNetCore.SignalR;`
- Old: `using Microsoft.Owin;` → New: removed
- Old: `using Spring.Rest.Client;` → New: `using System.Net.Http;` (stubs)
- Old: `using Spring.Social.OAuth2;` → New: stub interface or removed
- Apply to: All files matching the patterns in the table above

**External Reference Updates:**

| File Pattern | Update Description |
|---|---|
| `src/SplendidCRM.Web/appsettings*.json` | All configuration previously in `Web.config` |
| `src/SplendidCRM.Core/SplendidCRM.Core.csproj` | NuGet `<PackageReference>` entries replacing all manual DLLs |
| `src/SplendidCRM.Web/SplendidCRM.Web.csproj` | NuGet references + `<ProjectReference>` to Core |
| `README.md` | Updated build instructions, runtime requirements, dependency list |

**Eliminated Configuration Files:**

| File | Status |
|---|---|
| `SplendidCRM/Web.config` | **ELIMINATED** — all content migrated to `appsettings*.json` + env vars + Secrets Manager + Parameter Store |
| `SplendidCRM/SplendidCRM7_VS2017.csproj` | **REPLACED** — by two SDK-style `.csproj` files |
| All `<assemblyBinding>` redirects | **ELIMINATED** — NuGet dependency resolution handles versioning |
| All `<system.serviceModel>` WCF config | **ELIMINATED** — ASP.NET Core middleware pipeline |
| All `<system.webServer>` IIS config | **ELIMINATED** — Kestrel self-hosting |

## 0.7 Special Analysis

### 0.7.1 Cross-Cutting Concern: HttpContext.Current Replacement

The most pervasive migration challenge is the replacement of `HttpContext.Current` static access, which is used throughout the codebase as the primary mechanism for accessing session state, request/response data, and application state. This pattern does not exist in ASP.NET Core.

**Impact Analysis:**

| Access Pattern | File Count | Replacement Strategy |
|---|---|---|
| `HttpContext.Current.Session["key"]` | 20 files | Constructor-injected `IHttpContextAccessor` → `HttpContext.Session` |
| `HttpContext.Current.Application["key"]` | 36 files | Constructor-injected `IMemoryCache` with equivalent cache keys |
| `HttpContext.Current.Request.*` | 15+ files | Constructor-injected `IHttpContextAccessor` → `HttpContext.Request` |
| `HttpContext.Current.Response.*` | 10+ files | Controller action `HttpContext.Response` or `IHttpContextAccessor` |
| `HttpContext.Current.Server.MapPath()` | 5+ files | `IWebHostEnvironment.ContentRootPath` / `WebRootPath` |

**Critical files requiring deep refactoring:**

- `Security.cs` (1,388 lines) — Session-backed static properties (`USER_ID`, `USER_LOGIN_ID`, `TEAM_ID`, `FULL_NAME`, `IS_ADMIN`, etc.) all read from `HttpContext.Current.Session`. Each property getter must be refactored to receive `IHttpContextAccessor` via constructor injection while preserving the identical property contract.
- `SplendidCache.cs` (11,582 lines) — Uses `HttpRuntime.Cache`, `Application[]`, and `Session[]` extensively. Every cache retrieval method must be converted to `IMemoryCache` injection while preserving identical cache keys and return types.
- `SplendidInit.cs` (~900 lines) — `InitApp` uses `Application` lock and `Application[]` for one-time initialization. Must be converted to a thread-safe singleton initialization pattern using `SemaphoreSlim` or `Lazy<T>`.
- `RestUtil.cs` (~800 lines) — REST serialization helper uses `HttpContext.Current` for timezone resolution and ACL-aware table selection. Must receive context via DI.

**Transformation Pattern:**

The fundamental transformation converts static-access classes to DI-friendly services:

```csharp
// BEFORE (.NET Framework 4.8)
public static Guid USER_ID { get { return Sql.ToGuid(HttpContext.Current.Session["USER_ID"]); } }
// AFTER (.NET 10 ASP.NET Core)
public Guid USER_ID { get { return Sql.ToGuid(_httpContextAccessor.HttpContext?.Session.GetString("USER_ID")); } }
```

### 0.7.2 Cross-Cutting Concern: Application State to IMemoryCache Migration

The `Application[]` dictionary (ASP.NET global state) is used in 36 files as a shared in-memory store. ASP.NET Core does not provide an `Application` object.

**Migration Strategy:**

- All `Application["key"]` reads → `IMemoryCache.TryGetValue<T>("key", out var value)`
- All `Application["key"] = value` writes → `IMemoryCache.Set("key", value, cacheOptions)`
- The `SplendidCache.cs` class becomes the primary consumer, wrapping `IMemoryCache` with the same key structure
- Cache invalidation via `vwSYSTEM_EVENTS` monitoring is preserved in `CacheInvalidationService.cs` (a background service that periodically queries the SQL view and evicts stale cache entries)

**Key Cache Key Families (must be preserved identically):**

| Cache Key Pattern | Source Method | Purpose |
|---|---|---|
| `vwMODULES_*` | `SplendidCache.Modules()` | Module metadata |
| `vwTERMINOLOGY_*` | `SplendidCache.Terminology()` | Localization strings |
| `vwGRIDVIEWS_*` / `vwDETAILVIEWS_*` / `vwEDITVIEWS_*` | `SplendidCache.GridViews()` etc. | Dynamic layout metadata |
| `vwDYNAMIC_BUTTONS_*` | `SplendidCache.DynamicButtons()` | Button configurations |
| `CONFIG_*` | `SplendidCache.Config()` | System configuration |
| `vwTIMEZONES` | `SplendidCache.TimeZones()` | Timezone list |
| `vwCURRENCIES` | `SplendidCache.Currencies()` | Currency list |

### 0.7.3 Cross-Cutting Concern: WCF-to-Web-API Endpoint Mapping

The monolithic `Rest.svc.cs` (8,369 lines) contains 152 WCF operations decorated with `[WebInvoke]` attributes. Each must be mapped to an equivalent ASP.NET Core controller action.

**WCF Pattern → ASP.NET Core Pattern:**

| WCF Attribute | ASP.NET Core Equivalent |
|---|---|
| `[ServiceContract]` | `[ApiController]` on class |
| `[AspNetCompatibilityRequirements]` | Removed — not needed |
| `[WebInvoke(Method="POST", BodyStyle=WebMessageBodyStyle.WrappedRequest, ...)]` | `[HttpPost("Rest.svc/{OperationName}")]` |
| `[WebInvoke(Method="GET", ...)]` | `[HttpGet("Rest.svc/{OperationName}")]` |
| `[OperationContract]` | Method is public on controller |
| `WebOperationContext.Current.OutgoingResponse` | `HttpContext.Response` |
| `WebOperationContext.Current.IncomingRequest` | `HttpContext.Request` |

**Route Preservation Strategy:**

The current WCF REST URL pattern is `/Rest.svc/{Operation}`. To maintain backward compatibility with the React SPA and any external integrations, the ASP.NET Core controllers must register compatibility routes:

- `[Route("Rest.svc")]` on the main REST controller
- `[Route("Administration/Rest.svc")]` on the admin REST controller
- Individual action methods: `[HttpPost("{Operation}")]` where `{Operation}` matches the WCF operation name exactly

**OData-Style Query Parameters:**

The existing `Rest.svc.cs` implements custom OData-style query parameter parsing for `$filter`, `$select`, `$orderby`, and `$groupby`. This is NOT standard OData — it is custom parsing logic in `SearchBuilder.cs` and `RestUtil.cs`. The migration must preserve this custom parsing exactly, not introduce Microsoft OData middleware.

### 0.7.4 Cross-Cutting Concern: Spring.Social Dependency Removal

The 8 Spring.Social integration directories (334 files) depend on `Spring.Rest.dll` and `Spring.Social.Core.dll` — both are discontinued .NET libraries with no .NET Core/.NET 10 equivalent. These DLLs cannot be replaced with NuGet packages.

**Removal Strategy:**

- Create minimal stub interfaces that satisfy the compilation requirements:
  - `IRestOperations` (REST client interface used by Spring.Social providers)
  - `IOAuth2Operations` (OAuth 2.0 flow interface)
  - `IApiBinding` (base API binding interface)
- Replace `using Spring.Rest.Client;` with `using System.Net.Http;` where possible
- Replace `using Spring.Social.OAuth2;` with stub interface references
- All 334 files must compile but are NOT expected to execute — they are dormant Enterprise Edition stubs
- The stubs preserve public class signatures and interface contracts for future Enterprise Edition activation

### 0.7.5 Cross-Cutting Concern: Session Serialization Compatibility

The migration from InProc session to distributed session (Redis or SQL Server) requires all session data to be serializable. InProc session can store any .NET object reference; distributed session requires explicit serialization.

**Session Usage Audit:**

| Session Key Pattern | Data Type | Serialization Impact |
|---|---|---|
| `USER_ID` | `Guid` (stored as string) | Compatible — `GetString`/`SetString` |
| `USER_LOGIN_ID` | `Guid` | Compatible |
| `TEAM_ID` | `Guid` | Compatible |
| `FULL_NAME` | `string` | Compatible |
| `IS_ADMIN` | `bool` (stored as string) | Compatible |
| `IS_ADMIN_DELEGATE` | `bool` | Compatible |
| `USER_EXTENSION` | `string` | Compatible |
| Module ACL data | `DataTable` | **REQUIRES serialization adapter** — must convert to JSON/binary |
| Field ACL data | `DataTable` | **REQUIRES serialization adapter** |

The `Security.cs` class stores ACL data as `DataTable` objects in session. Distributed session requires these to be serialized. The migration must implement a session serialization adapter that converts `DataTable` ACL structures to a serializable format (JSON strings) on write and deserializes on read, while maintaining the identical API contract visible to consuming code.

### 0.7.6 Cross-Cutting Concern: TLS and Cookie Security Migration

**TLS Enforcement:**

- Current: `ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;` in `Global.asax.cs Application_Start`
- Target: Kestrel HTTPS configuration in `Program.cs` enforcing TLS 1.2+ at the transport level
- `ServicePointManager` is not available/needed in ASP.NET Core; Kestrel handles TLS natively

**Cookie Security:**

- Current: Manual `SameSite` and `Secure` flag hardening in `Global.asax.cs Session_Start` via cookie iteration
- Target: `CookiePolicyOptions` middleware in `Program.cs`:

```csharp
builder.Services.Configure<CookiePolicyOptions>(o => {
    o.MinimumSameSitePolicy = SameSiteMode.Lax;
});
```

**P3P Header:**

- Current: `Response.AddHeader("p3p", ...)` in `Global.asax.cs Application_BeginRequest`
- Target: Intentionally dropped. Document as removed — P3P was legacy IE iframe compatibility, no modern browsers support it

## 0.8 Refactoring Rules

### 0.8.1 Refactoring-Specific Rules

The following rules are explicitly emphasized by the user and MUST be observed throughout all migration work:

**Minimal Change Clause:**
- Make ONLY the changes necessary for the .NET Framework 4.8 → .NET 10 ASP.NET Core transition
- Preserve all business logic, validation rules, and data processing exactly as-is
- Do NOT optimize, refactor, or "improve" code patterns beyond what the framework migration requires
- Do NOT add features, modules, or capabilities
- Do NOT modify SQL Server schema, stored procedures, views, functions, or triggers
- Preserve original code structure and organization where possible; isolate new implementations in dedicated files when framework patterns diverge
- When multiple migration approaches exist, choose the one requiring the least modification to existing logic

**Immutable Interfaces (contract preservation):**
- REST API endpoint paths, HTTP methods, request/response JSON schemas, OData query parameters (`$filter`, `$select`, `$orderby`, `$groupby`) — 100% response parity
- SOAP WSDL contract, `sugarsoap` namespace, all data carriers — zero contract changes, WSDL byte-comparable
- SQL Server stored procedure signatures and view definitions — zero DDL changes
- SignalR hub method signatures — wire protocol may upgrade but method names and parameters preserved
- 4-tier ACL model: Module → Team → Field → Record — `Security.Filter` produces identical SQL predicates
- Cookie/session token names and authentication flow sequences visible to external clients
- Scheduler job names and behavior: `CleanSystemLog`, `pruneDatabase`, `BackupDatabase`, `BackupTransactionLog`, `CheckVersion`, `RunAllArchiveRules`, `RunExternalArchive`

**Security Preservation:**
- MD5 password hashing MUST be preserved as-is for SugarCRM backward compatibility
- Add inline comment: `// TECHNICAL DEBT: MD5 hash preserved for SugarCRM backward compatibility. Do not modify.`
- Address security vulnerabilities ONLY if critical (CVSS > 7.0) or introduced by the migration itself

**Integration Stubs:**
- All 16 integration subdirectories MUST compile on .NET 10 but MUST NOT be activated or tested beyond compilation
- Preserve all public interfaces and class signatures for Enterprise Edition upgrade path

**Documentation Requirements:**
- Document all technology-specific changes with clear comments referencing the migration
- P3P header intentionally dropped — document as removed (legacy IE iframe compatibility)
- Document any SignalR client-facing endpoint path changes for Prompt 2

### 0.8.2 Special Instructions and Constraints

**Configuration Externalization:**
- Zero configuration values that vary per environment MUST be hardcoded in source
- Application MUST validate all required configuration at startup in `Program.cs`
- If any Secrets Manager key or required environment variable is missing or empty, application MUST log the specific missing variable name and exit with non-zero code
- Application MUST NOT start with null or empty connection strings

**Provider Hierarchy (highest priority wins):**
1. AWS Secrets Manager — secrets
2. Environment variables — runtime overrides
3. AWS Systems Manager Parameter Store — environment-specific non-secret config
4. `appsettings.{Environment}.json` — environment defaults
5. `appsettings.json` — base defaults

**Required Environment Variables (18 total):**

| Variable | Source | Required |
|---|---|---|
| `ConnectionStrings__SplendidCRM` | Secrets Manager | Yes — fail-fast if missing |
| `ASPNETCORE_ENVIRONMENT` | Env var | Yes |
| `SPLENDID_JOB_SERVER` | Env var | Yes — scheduler job election |
| `SCHEDULER_INTERVAL_MS` | Parameter Store | Default: 60000 |
| `EMAIL_POLL_INTERVAL_MS` | Parameter Store | Default: 60000 |
| `ARCHIVE_INTERVAL_MS` | Parameter Store | Default: 300000 |
| `SESSION_PROVIDER` | Parameter Store | Required: `Redis` or `SqlServer` |
| `SESSION_CONNECTION` | Secrets Manager | Yes — fail-fast if missing |
| `AUTH_MODE` | Parameter Store | Required: `Windows` / `Forms` / `SSO` |
| `SSO_AUTHORITY` | Parameter Store | Required if AUTH_MODE=SSO |
| `SSO_CLIENT_ID` | Secrets Manager | Required if AUTH_MODE=SSO |
| `SSO_CLIENT_SECRET` | Secrets Manager | Required if AUTH_MODE=SSO |
| `DUO_INTEGRATION_KEY` | Secrets Manager | Optional — 2FA |
| `DUO_SECRET_KEY` | Secrets Manager | Optional — 2FA |
| `DUO_API_HOSTNAME` | Parameter Store | Optional — 2FA |
| `SMTP_CREDENTIALS` | Secrets Manager | Optional — email sending |
| `LOG_LEVEL` | Env var | Default: Information |
| `CORS_ORIGINS` | Parameter Store | Required — allowed API origins |

**Performance Constraint:**
- All REST endpoints must respond with ≤10% latency variance at 95th percentile versus .NET Framework 4.8 baseline

**Build Constraint:**
- Backend MUST build and run via `dotnet restore && dotnet build && dotnet run` on Linux with zero Windows dependencies
- Zero reliance on Visual Studio, MSBuild from Windows SDK, or IIS

**Scheduler Constraint:**
- All 7 scheduler jobs must execute on configured intervals with reentrancy guards preventing concurrent execution
- Machine-name-based job election using `SPLENDID_JOB_SERVER` env var must be preserved

### 0.8.3 Validation Criteria

| Test Type | Scope | Pass Criteria |
|---|---|---|
| API Contract | All REST endpoints | 100% response parity with .NET Framework baseline |
| SOAP Contract | Full WSDL + data carriers | Zero contract changes, WSDL byte-comparable |
| Auth Flows | Windows, Forms, SSO, DuoUniversal 2FA | All mechanisms authenticate and authorize identically |
| ACL Enforcement | Module, Team, Field, Record level | `Security.Filter` produces identical SQL predicates |
| Scheduler | All 7 named jobs + 3 hosted services | Execute on intervals, reentrancy guards prevent overlap |
| Cache Parity | All SplendidCache metadata queries | `IMemoryCache` returns identical payloads |
| Data Access | All stored procedure calls | `Microsoft.Data.SqlClient` produces identical results |
| Configuration | Missing secrets, missing env vars | Fail-fast on missing config; correct override hierarchy |
| Build | `dotnet restore && dotnet build && dotnet publish` | Zero Windows/VS dependencies, clean Linux build |
| Performance | Key API endpoints under load | ≤10% latency variance at P95 versus baseline |

## 0.9 References

### 0.9.1 Repository Files and Folders Searched

The following files and folders were inspected during codebase analysis to derive the conclusions in this Agent Action Plan:

**Root-Level Exploration:**

| Path | Type | Purpose |
|---|---|---|
| `/` (repository root) | Folder | Identified top-level structure: LICENSE, README.md, BackupBin2012/, SQL Scripts Community/, SplendidCRM/ |
| `BackupBin2012/` | Folder | Documented 22 third-party DLL XML IntelliSense files; confirmed DLL dependency inventory |
| `SplendidCRM/` | Folder | Main application directory — identified ~60+ module folders, _code/, Administration/, React/, Angular/, html5/ |

**Core Backend Files (read in full):**

| File Path | Lines | Analysis Purpose |
|---|---|---|
| `SplendidCRM/Global.asax.cs` | 400 | Application lifecycle analysis — timer setup, session handling, TLS enforcement, P3P headers, React URL rewriting |
| `SplendidCRM/Web.config` | 196 | Configuration audit — appSettings, connectionStrings, authentication mode, session state, WCF serviceModel, binding redirects |
| `SplendidCRM/SplendidCRM7_VS2017.csproj` | 250+ | DLL reference inventory, framework version, project type, compile items |
| `SplendidCRM/Rest.svc.cs` | 120 (head) | WCF REST service contract, endpoint structure, operation patterns |
| `SplendidCRM/soap.asmx.cs` | 120 (head) | SOAP service contract, DTO definitions, namespace declaration |

**Core Business Logic (summaries retrieved):**

| File Path | Purpose |
|---|---|
| `SplendidCRM/_code/Security.cs` | Security façade — session-backed properties, MD5 hashing, ACL model, Filter() overloads |
| `SplendidCRM/_code/SplendidCache.cs` | Caching hub — HttpRuntime.Cache, React helpers, cache invalidation |
| `SplendidCRM/_code/SplendidInit.cs` | Application bootstrap — InitApp, InitSession, LoginUser |
| `SplendidCRM/_code/SchedulerUtils.cs` | Scheduler — cron parsing, OnTimer, OnArchiveTimer, reentrancy guards |

**Folder Structure Analysis:**

| Folder Path | Children Found | Analysis Purpose |
|---|---|---|
| `SplendidCRM/_code/` | 74 root .cs files + 18 subdirs | Core business logic inventory |
| `SplendidCRM/_code/SignalR/` | 10 .cs files | SignalR hub and manager inventory |
| `SplendidCRM/_code/DuoUniversal/` | 7 .cs files | DuoUniversal 2FA component inventory |
| `SplendidCRM/Administration/` | 13 root .cs files + 47+ subdirs | Admin REST and WCF service discovery |

**Quantitative Analyses (bash commands):**

| Analysis | Command | Result |
|---|---|---|
| Integration subdirectories | `ls -d SplendidCRM/_code/Spring.Social.* ...` | 18 total (16 stubs + 2 active) |
| Integration root files | `ls SplendidCRM/_code/{ExchangeSync,...}.cs` | 9 integration utility files |
| System.Web usage | `grep -rl "System\.Web" SplendidCRM/_code/*.cs` | 65 files |
| HttpContext.Current usage | `grep -rl "HttpContext\.Current" ...` | 31 files |
| System.Data.SqlClient usage | `grep -rl "System\.Data\.SqlClient" ...` | 5 files |
| Application[] state usage | `grep -rl "Application\[" ...` | 36 files |
| HttpRuntime.Cache usage | `grep -rl "HttpRuntime\.Cache" ...` | 5 files |
| Session[] usage | `grep -rl "Session\[" ...` | 20 files |
| WCF attribute locations | `grep -rl "ServiceContract\|WebInvoke" ...` | 3 files (Rest.svc.cs, Admin/Rest.svc.cs, Admin/Impersonation.svc.cs) |
| REST endpoint count | `grep -c "WebInvoke\|OperationContract" Rest.svc.cs` | 152 main + 65 admin = 217 total |
| SOAP method count | `grep -c "WebMethod\|SoapRpcMethod" soap.asmx.cs` | 84 methods |
| HintPath DLLs | `grep -oP "HintPath>..." SplendidCRM7_VS2017.csproj` | 37 DLL references across BackupBin2012/2022/2025 |
| _controls file count | `ls SplendidCRM/_controls/*.cs \| wc -l` | 44 files |
| _devtools file count | `ls SplendidCRM/_devtools/*.cs \| wc -l` | 8 files |
| Total .cs in _code | `find SplendidCRM/_code -name "*.cs" \| wc -l` | 443 files |
| Admin .cs root files | `ls SplendidCRM/Administration/*.cs` | 12 files |
| Root .cs files | `ls SplendidCRM/*.cs` | 10 files |

**Tech Spec Sections Retrieved:**

| Section | Purpose |
|---|---|
| 3.1 PROGRAMMING LANGUAGES | Confirmed .NET Framework 4.8 target, C# backend, TypeScript frontend, T-SQL database |

### 0.9.2 Web Research Conducted

| Search Query | Key Finding |
|---|---|
| `.NET 10 release date LTS 2025` | .NET 10 released November 11, 2025 as LTS; supported until November 2028; ships with C# 14 and ASP.NET Core 10 |
| `SoapCore NuGet latest version 2025` | SoapCore 1.2.1.12 (December 11, 2025) — SOAP middleware for ASP.NET Core; supports endpoint routing; `[ServiceContract]`/`[OperationContract]` attributes compatible |
| `Microsoft.Data.SqlClient NuGet latest version 2025` | Microsoft.Data.SqlClient 6.1.4 — drop-in replacement for System.Data.SqlClient; identical API surface (SqlConnection, SqlCommand, SqlDataReader, SqlDataAdapter) |
| `MailKit NuGet latest version 2025` | MailKit 4.15.0 — cross-platform .NET mail client; SMTP/IMAP/POP3 with full async support |

### 0.9.3 Attachments and External References

- **No Figma URLs provided** — This is a backend-only migration with no UI design components
- **No file attachments provided** — All analysis derived from repository inspection and web research
- **User Prompt:** Prompt 1 of 3 in SplendidCRM modernization series
  - Prompt 1 (this): Backend modernization and toolchain decoupling
  - Prompt 2 (future): Frontend modernization — React 19 + Vite
  - Prompt 3 (future): Containerization, AWS infrastructure, and deployment

