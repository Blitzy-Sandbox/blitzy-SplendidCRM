# SplendidCRM Community Edition

## Overview

SplendidCRM Community Edition is an open-source Customer Relationship Management platform built on **.NET 10 ASP.NET Core MVC** with a **React** single-page application frontend and a **SQL Server** database backend.

This version has been modernized from the original .NET Framework 4.8 / ASP.NET WebForms / WCF / IIS platform to a cross-platform .NET 10 ASP.NET Core architecture. The backend builds and runs on **Linux, macOS, and Windows** using the standard `dotnet` CLI — no Windows-only toolchains, Visual Studio, or IIS dependencies are required.

Licensed under the [GNU Affero General Public License v3.0](LICENSE).

## Minimum Requirements

### Backend

1. **.NET 10 SDK** (LTS, released November 2025). Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0)
2. **SQL Server Express 2008 or higher**. Download [SQL Server Express 2019](https://www.microsoft.com/en-us/download/details.aspx?id=101064)
3. **SQL Server Management Studio** (for database management). Download [SSMS](https://docs.microsoft.com/en-us/sql/ssms/download-sql-server-management-studio-ssms)
4. **Distributed session store** — Redis or SQL Server, configurable via the `SESSION_PROVIDER` environment variable
5. **AWS credentials** (optional) — required only if using AWS Secrets Manager or AWS Systems Manager Parameter Store for configuration

### Frontend (React SPA)

6. **Node.js** version 16.20. Download [Node 16.20](https://nodejs.org/en/download/)
7. **Yarn** version 1.22. Install via npm: `npm install --global yarn`

### IDE / Editor

Any IDE or text editor capable of working with .NET projects:
- **VS Code** with the C# Dev Kit extension
- **JetBrains Rider**
- **Visual Studio 2022+**
- Or simply the `dotnet` CLI from a terminal

## Using the Installer

> **Note:** The installer described below applies to the **legacy .NET Framework 4.8** version of SplendidCRM. For the modernized .NET 10 version, see the [Building Yourself](#building-yourself) and [Running the Application](#running-the-application) sections.

The goal of the installer is to do everything necessary to get the system running on whatever version of windows you are running. We typically include SQL Server Express with the installer to save you that step, but if you already have SQL Server installed on your network, you can use the smaller Upgrade download. The app will do the following:
1. Install all files for the app. This action is performed by the typical InstallShield app.
2. Run SplendidCRM Configuration Wizard to configure Windows. This is where the real work is done.
3. Install IIS if not already installed.
4. Add IIS features that are required for the app.
5. Add SplendidApp application to IIS.
6. Connect to the database. SQL Server can be remote or local, all that is important is that you can connect. The installer includes an initialized database, which will be attached if an existing CRM database is not detected.
7. Create or update all tables, functions, views, procedures and/or data to run the app.
![InstallShield installer](https://www.splendidcrm.com/portals/0/SplendidCRM/Installation_InstallShield.gif "InstallShield installer")
![SplendidCRM Configuration Wizard](https://www.splendidcrm.com/portals/0/SplendidCRM/Installation_Wizard.gif "SplendidCRM Configuration Wizard")

## Building Yourself

The project is organized as a .NET 10 solution (`SplendidCRM.sln`) with two SDK-style projects. All backend dependencies are managed via **NuGet** — there are no manually managed DLL references. The backend builds and runs on **Linux, macOS, and Windows**.

### Backend Build

Build the backend from the repository root:

```bash
dotnet restore SplendidCRM.sln
dotnet build SplendidCRM.sln
```

Or as a single command:

```bash
dotnet restore && dotnet build
```

#### Solution Structure

| Project | Path | Description |
|---|---|---|
| **SplendidCRM.Core** | `src/SplendidCRM.Core/` | .NET 10 class library containing all extracted business logic (74 utility classes originally from `_code/`), data access, caching, security, and integration stubs. |
| **SplendidCRM.Web** | `src/SplendidCRM.Web/` | ASP.NET Core MVC web project providing REST API controllers, SOAP service, SignalR hubs, background services, authentication, authorization, and middleware. References `SplendidCRM.Core`. |

All NuGet package references are declared in the respective `.csproj` files and restored automatically by `dotnet restore`. Key packages include:

- `Microsoft.Data.SqlClient` 6.1.4 — SQL Server data access
- `SoapCore` 1.2.1.12 — SOAP middleware for ASP.NET Core
- `MailKit` 4.15.0 / `MimeKit` 4.15.0 — Email client
- `Newtonsoft.Json` 13.0.3 — JSON serialization (fallback)
- `Twilio` 7.8.0 — Twilio SMS/Voice API
- `DocumentFormat.OpenXml` 3.3.0 — OpenXML document handling
- `AWSSDK.SecretsManager` / `AWSSDK.SimpleSystemsManagement` — AWS configuration providers

### React Build
We recommend that you use yarn to bulid the React files. We are currently using version 1.22, npm version 6.14 adn node 16.20. These versions can be important as newer versions can have build failures. The first time you build, you will need to have yarn install all packages.

> yarn install

Then you can build the app.

> yarn build

The result will be the file React\dist\js\SteviaCRM.js

### SQL Build
The SQL Scripts folder contains all the code to create or update a database to the current level. The Build.bat file is designed to create a single Build.sql file that combines all the SQL code into a single Build.sql file. If this is the first time you are building the database, you will need to create the SQL database yourself and define a SQL user that has ownership access.
We have designed the SQL scripts to be run to upgrade any existing database to the current level. In addition, we designed the SQL scripts to be run over and over again, without any errors. We encourage you to continue this design. It includes data modifications that are designed to only be applied once. The basic logic is to check if the operation needs to occur before performing the acction.

> if ( condition to test ) begin -- then
>	operation to perform
> end -- if;

If you are wondering why we use "begin -- then" and "end -- if;" instead of simply "begin" and "end", it is so that we can more easily convert the code to support the Oracle PL/SQL format.

## Configuration

SplendidCRM uses a **five-tier configuration provider hierarchy**. Values from higher-priority sources override those from lower-priority sources:

| Priority | Source | Purpose |
|---|---|---|
| 1 (highest) | **AWS Secrets Manager** | Database credentials, SMTP credentials, SSO secrets, Duo keys |
| 2 | **Environment variables** | Runtime overrides, deployment-specific settings |
| 3 | **AWS Systems Manager Parameter Store** | Environment-specific non-secret configuration |
| 4 | `appsettings.{Environment}.json` | Per-environment defaults (Development, Staging, Production) |
| 5 (lowest) | `appsettings.json` | Base defaults |

AWS Secrets Manager and Parameter Store providers are **optional**. If AWS credentials are not available, the application falls back to environment variables and JSON configuration files.

### Required Environment Variables

The application performs **startup validation** and will **fail fast** with a descriptive error message if any required configuration value is missing or empty.

| Variable | Source | Required | Description |
|---|---|---|---|
| `ConnectionStrings__SplendidCRM` | Secrets Manager / Env | **Yes** (fail-fast) | SQL Server connection string |
| `ASPNETCORE_ENVIRONMENT` | Env | **Yes** | Runtime environment (`Development`, `Staging`, `Production`) |
| `SPLENDID_JOB_SERVER` | Env | **Yes** | Machine name for scheduler job election |
| `SESSION_PROVIDER` | Parameter Store / Env | **Yes** | Distributed session backend: `Redis` or `SqlServer` |
| `SESSION_CONNECTION` | Secrets Manager / Env | **Yes** (fail-fast) | Connection string for the session store |
| `AUTH_MODE` | Parameter Store / Env | **Yes** | Authentication mode: `Windows`, `Forms`, or `SSO` |
| `SSO_AUTHORITY` | Parameter Store / Env | Conditional | OIDC/SAML authority URL (required if `AUTH_MODE=SSO`) |
| `SSO_CLIENT_ID` | Secrets Manager / Env | Conditional | OIDC client ID (required if `AUTH_MODE=SSO`) |
| `SSO_CLIENT_SECRET` | Secrets Manager / Env | Conditional | OIDC client secret (required if `AUTH_MODE=SSO`) |
| `DUO_INTEGRATION_KEY` | Secrets Manager / Env | Optional | DuoUniversal 2FA integration key |
| `DUO_SECRET_KEY` | Secrets Manager / Env | Optional | DuoUniversal 2FA secret key |
| `DUO_API_HOSTNAME` | Parameter Store / Env | Optional | DuoUniversal 2FA API hostname |
| `SMTP_CREDENTIALS` | Secrets Manager / Env | Optional | SMTP credentials for email sending |
| `SCHEDULER_INTERVAL_MS` | Parameter Store / Env | Optional | Scheduler timer interval in milliseconds (default: `60000`) |
| `EMAIL_POLL_INTERVAL_MS` | Parameter Store / Env | Optional | Email polling interval in milliseconds (default: `60000`) |
| `ARCHIVE_INTERVAL_MS` | Parameter Store / Env | Optional | Archive timer interval in milliseconds (default: `300000`) |
| `LOG_LEVEL` | Env | Optional | Logging level (default: `Information`) |
| `CORS_ORIGINS` | Parameter Store / Env | **Yes** | Comma-separated list of allowed CORS origins |

## Architecture

### Solution Structure

```
SplendidCRM.sln
src/
├── SplendidCRM.Core/              Class library — business logic
│   ├── Security.cs                Authentication, ACL enforcement, encryption
│   ├── SplendidCache.cs           Metadata caching (IMemoryCache)
│   ├── SplendidInit.cs            Application bootstrap
│   ├── SchedulerUtils.cs          Cron job execution logic
│   ├── RestUtil.cs                REST serialization, timezone math
│   ├── SearchBuilder.cs           SQL WHERE clause builder
│   ├── EmailUtils.cs              Email sending and polling
│   ├── Sql.cs                     SQL helper utilities
│   ├── [60+ more utility classes]
│   ├── DuoUniversal/              DuoUniversal 2FA integration
│   └── Integrations/              Dormant integration stubs (16 subdirectories)
│
└── SplendidCRM.Web/               ASP.NET Core MVC — hosting
    ├── Program.cs                 Entry point, DI container, middleware pipeline
    ├── appsettings.json           Base configuration defaults
    ├── Controllers/
    │   ├── RestController.cs      Main REST API (152 endpoints)
    │   ├── AdminRestController.cs Admin REST API (65 endpoints)
    │   ├── ImpersonationController.cs
    │   ├── HealthCheckController.cs   GET /api/health
    │   ├── CampaignTrackerController.cs
    │   ├── ImageController.cs
    │   ├── UnsubscribeController.cs
    │   └── TwiMLController.cs
    ├── Services/
    │   ├── SchedulerHostedService.cs      IHostedService — job scheduler
    │   ├── EmailPollingHostedService.cs   IHostedService — email polling
    │   ├── ArchiveHostedService.cs        IHostedService — archive processing
    │   └── CacheInvalidationService.cs    Cache invalidation monitor
    ├── Soap/
    │   ├── ISugarSoapService.cs   SOAP service interface (84 methods)
    │   ├── SugarSoapService.cs    SOAP implementation
    │   └── DataCarriers.cs        SOAP DTOs (contact_detail, entry_value, etc.)
    ├── Hubs/
    │   ├── ChatManagerHub.cs      ASP.NET Core SignalR
    │   ├── TwilioManagerHub.cs    ASP.NET Core SignalR
    │   └── PhoneBurnerHub.cs      ASP.NET Core SignalR
    ├── Authentication/            Windows, Forms, SSO, DuoUniversal 2FA setup
    ├── Authorization/             4-tier ACL: Module → Team → Field → Record
    ├── Middleware/                 SPA redirect, cookie policy
    └── Configuration/             AWS Secrets Manager, Parameter Store providers
```

### Key Components

- **REST API** — All 152 main REST endpoints and 65 admin endpoints are served via ASP.NET Core Web API controllers. Routes are preserved at `/Rest.svc/{operation}` and `/Administration/Rest.svc/{operation}` for backward compatibility.
- **SOAP Service** — The 84-method SOAP interface is hosted via SoapCore middleware, preserving the `sugarsoap` XML namespace (`http://www.sugarcrm.com/sugarcrm`) and WSDL contract.
- **SignalR** — Real-time hubs for Chat, Twilio, and PhoneBurner are implemented using ASP.NET Core SignalR.
- **Background Services** — Three `IHostedService` implementations handle scheduled jobs, email polling, and archive processing with configurable intervals and reentrancy guards.
- **Authentication** — Supports Windows (Negotiate/NTLM), Forms (cookie-based), and SSO (OIDC/SAML) authentication modes, selectable via the `AUTH_MODE` environment variable. Optional DuoUniversal 2FA integration is available.
- **Authorization** — A 4-tier ACL model enforces access control at the Module, Team, Field, and Record levels, replicating the original `Security.Filter` SQL predicate injection.
- **Distributed Session** — Session state is backed by Redis or SQL Server (configurable via `SESSION_PROVIDER`), replacing the legacy in-process session.
- **Health Check** — `GET /api/health` returns `200 OK` with a JSON status payload for use by load balancers and container orchestrators.

## Running the Application

Start the application from the repository root:

```bash
dotnet run --project src/SplendidCRM.Web
```

By default, Kestrel listens on `http://+:5000`. Override the listening URL with the `ASPNETCORE_URLS` environment variable:

```bash
ASPNETCORE_URLS="http://+:8080" dotnet run --project src/SplendidCRM.Web
```

### Quick Start (Development)

```bash
# 1. Set required environment variables
export ConnectionStrings__SplendidCRM="Server=localhost;Database=SplendidCRM;User Id=sa;Password=YourPassword;TrustServerCertificate=True"
export ASPNETCORE_ENVIRONMENT=Development
export SPLENDID_JOB_SERVER=$(hostname)
export SESSION_PROVIDER=SqlServer
export SESSION_CONNECTION="Server=localhost;Database=SplendidSession;User Id=sa;Password=YourPassword;TrustServerCertificate=True"
export AUTH_MODE=Forms
export CORS_ORIGINS="http://localhost:3000"

# 2. Restore and build
dotnet restore SplendidCRM.sln
dotnet build SplendidCRM.sln

# 3. Run
dotnet run --project src/SplendidCRM.Web
```

The application is fully self-hosted via Kestrel — **no IIS installation is required**. It runs on Linux, macOS, and Windows.

### Publish for Production

```bash
dotnet publish src/SplendidCRM.Web -c Release -o ./publish
```

The published output at `./publish` can be deployed to any server with the .NET 10 runtime installed, or packaged into a container image.

## Migration Notes

This section documents notable changes introduced by the .NET Framework 4.8 → .NET 10 ASP.NET Core migration.

### P3P Header Removed

The legacy `P3P` HTTP response header has been intentionally dropped. P3P was used for Internet Explorer iframe cookie compatibility and is not supported by any modern browser.

### MD5 Password Hashing Preserved

MD5 password hashing is preserved as-is for **SugarCRM backward compatibility**. This is documented as technical debt. Do not modify the hashing algorithm without a coordinated data migration plan.

### REST API Route Compatibility

All REST API routes are preserved at their original paths (`/Rest.svc/{operation}` and `/Administration/Rest.svc/{operation}`) to maintain backward compatibility with existing clients and the React SPA frontend.

### SOAP WSDL Contract Preserved

The SOAP service WSDL contract is preserved with the original `sugarsoap` XML namespace (`http://www.sugarcrm.com/sugarcrm`). All 84 SOAP methods and data carriers (`contact_detail`, `entry_value`, `name_value`, `document_revision`, etc.) maintain byte-comparable serialization.

### SignalR Hub Endpoints

SignalR hubs have been migrated from OWIN-hosted Microsoft.AspNet.SignalR to ASP.NET Core SignalR. Hub endpoints are available at:
- `/hubs/chat` — ChatManager
- `/hubs/twilio` — TwilioManager
- `/hubs/phoneburner` — PhoneBurnerManager

> **Note for frontend developers (Prompt 2):** The SignalR wire protocol has been upgraded. Client libraries must use the ASP.NET Core SignalR JavaScript client (`@microsoft/signalr`) instead of the legacy jQuery SignalR client.

## License

SplendidCRM Community Edition is licensed under the [GNU Affero General Public License v3.0](LICENSE).
