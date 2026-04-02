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
6. **Docker Engine** (latest stable) — required for containerized deployment. Download [Docker](https://docs.docker.com/get-docker/)

### Frontend (React SPA)

7. **Node.js 20 LTS**. Download [Node.js 20 LTS](https://nodejs.org/en/download/)
8. **npm** (included with Node.js) — package manager for frontend dependencies

### IDE / Editor

Any IDE or text editor capable of working with .NET projects:
- **VS Code** with the C# Dev Kit extension
- **JetBrains Rider**
- **Visual Studio 2022+**
- Or simply the `dotnet` CLI from a terminal

### Infrastructure & Deployment Tools

9. **Terraform** >= 1.12.x — for infrastructure provisioning. Download [Terraform](https://www.terraform.io/downloads)
10. **AWS CLI v2** — for ECR authentication and resource verification. Install [AWS CLI](https://docs.aws.amazon.com/cli/latest/userguide/getting-started-install.html)
11. **LocalStack Pro** 4.14.0 — for local infrastructure validation. Install [LocalStack](https://docs.localstack.cloud/getting-started/installation/)

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
| **SplendidCRM.Core** | `src/SplendidCRM.Core/` | .NET 10 class library containing all extracted business logic (78 utility classes originally from `_code/`), data access, caching, security, and integration stubs. |
| **SplendidCRM.Web** | `src/SplendidCRM.Web/` | ASP.NET Core MVC web project providing REST API controllers, SOAP service, SignalR hubs, background services, authentication, authorization, and middleware. References `SplendidCRM.Core`. |

All NuGet package references are declared in the respective `.csproj` files and restored automatically by `dotnet restore`. Key packages include:

- `Microsoft.Data.SqlClient` 6.1.4 — SQL Server data access
- `SoapCore` 1.2.1.12 — SOAP middleware for ASP.NET Core
- `MailKit` 4.15.0 / `MimeKit` 4.15.0 — Email client
- `Newtonsoft.Json` 13.0.3 — JSON serialization (primary)
- `Twilio` 7.8.0 — Twilio SMS/Voice API
- `DocumentFormat.OpenXml` 3.3.0 — OpenXML document handling
- `AWSSDK.SecretsManager` / `AWSSDK.SimpleSystemsManagement` — AWS configuration providers

### React Build
We recommend that you use yarn to build the React files. We are currently using version 1.22, npm version 6.14 and node 16.20. These versions can be important as newer versions can have build failures. The first time you build, you will need to have yarn install all packages.

> yarn install

Then you can build the app.

> yarn build

The result will be the file React\dist\js\SteviaCRM.js

### SQL Build
The SQL Scripts folder contains all the code to create or update a database to the current level. The Build.bat file is designed to create a single Build.sql file that combines all the SQL code into a single Build.sql file. If this is the first time you are building the database, you will need to create the SQL database yourself and define a SQL user that has ownership access.
We have designed the SQL scripts to be run to upgrade any existing database to the current level. In addition, we designed the SQL scripts to be run over and over again, without any errors. We encourage you to continue this design. It includes data modifications that are designed to only be applied once. The basic logic is to check if the operation needs to occur before performing the action.

> if ( condition to test ) begin -- then
>	operation to perform
> end -- if;

If you are wondering why we use "begin -- then" and "end -- if;" instead of simply "begin" and "end", it is so that we can more easily convert the code to support the Oracle PL/SQL format.

## Docker

SplendidCRM is packaged as two Docker containers for deployment to AWS ECS Fargate or any container orchestrator.

### Docker Build

```bash
# Backend image (multi-stage: .NET 10 SDK → ASP.NET Alpine runtime, ≤500MB)
docker build -f Dockerfile.backend -t splendidcrm-backend:latest .

# Frontend image (multi-stage: Node 20 → Nginx Alpine, ≤100MB)
docker build -f Dockerfile.frontend -t splendidcrm-frontend:latest .
```

Both Dockerfiles use multi-stage builds to minimize image size and exclude build tools from the runtime image. The Docker build context must be the repository root.

### Docker Run (Local Development)

```bash
# Backend
docker run -d --name splendidcrm-backend \
  -p 8080:8080 \
  -e ConnectionStrings__SplendidCRM="Server=host.docker.internal;Database=SplendidCRM;User Id=sa;Password=YourPassword;TrustServerCertificate=True" \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e SPLENDID_JOB_SERVER=docker \
  -e SESSION_PROVIDER=SqlServer \
  -e SESSION_CONNECTION="Server=host.docker.internal;Database=SplendidSession;User Id=sa;Password=YourPassword;TrustServerCertificate=True" \
  -e AUTH_MODE=Forms \
  -e CORS_ORIGINS="" \
  splendidcrm-backend:latest

# Frontend
docker run -d --name splendidcrm-frontend \
  -p 3000:80 \
  -e API_BASE_URL="" \
  -e SIGNALR_URL="" \
  -e ENVIRONMENT=development \
  splendidcrm-frontend:latest
```

The backend container runs Kestrel on port 8080. The frontend container runs Nginx on port 80, with runtime `config.json` injection from environment variables via the `docker-entrypoint.sh` script.

### Local Validation

```bash
# Run all 12 Docker validation tests
scripts/validate-docker-local.sh
```

The validation suite tests image builds, image sizes, health checks, config injection, SPA fallback routing, source map blocking, and end-to-end login flow.

## Container Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     Internal ALB (HTTP/HTTPS)                   │
│                      Path-Based Routing Rules                   │
├──────────────────────────────┬──────────────────────────────────┤
│   Backend Paths              │   Frontend Default               │
│   /Rest.svc/*                │   /* (all other paths)           │
│   /Administration/Rest.svc/* │                                  │
│   /hubs/*                    │                                  │
│   /api/*                     │                                  │
│   /App_Themes/*              │                                  │
│   /Include/*                 │                                  │
├──────────────────────────────┼──────────────────────────────────┤
│   Backend Service            │   Frontend Service               │
│   ASP.NET 10 Alpine          │   Nginx Alpine                   │
│   Kestrel :8080              │   Port :80                       │
│   App_Themes + Include       │   React SPA dist/                │
│   static assets              │   Runtime config.json injection  │
└──────────────┬───────────────┴──────────────────────────────────┘
               │
               ▼
       ┌───────────────┐
       │ RDS SQL Server │
       │   Port 1433    │
       └───────────────┘
```

### Backend Container

- **Base image**: `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` with ICU and OpenSSL native dependencies
- **Port**: Kestrel listens on port 8080 (`ASPNETCORE_URLS=http://+:8080`)
- **Static assets**: `SplendidCRM/App_Themes/` and `SplendidCRM/Include/` are copied to `/SplendidCRM/` in the image for theme CSS, images, and shared JavaScript utilities
- **Health check**: `GET /api/health` returns `200 OK` with JSON status payload
- **Configuration**: All environment-specific values injected via ECS task definition environment variables and Secrets Manager references

### Frontend Container

- **Base image**: `nginx:alpine` serving the Vite-built React SPA from `/usr/share/nginx/html/`
- **Port**: Nginx listens on port 80
- **Runtime config injection**: `docker-entrypoint.sh` generates `/usr/share/nginx/html/config.json` from environment variables (`API_BASE_URL`, `SIGNALR_URL`, `ENVIRONMENT`) at container startup
- **SPA fallback**: Nginx `try_files` directive routes all non-file paths to `index.html` for client-side routing
- **Security**: Source maps deleted from image and blocked by Nginx (`*.map` → HTTP 404); security headers (`X-Content-Type-Options: nosniff`, `X-Frame-Options: SAMEORIGIN`) applied to all responses
- **Health check**: `GET /health` returns `200 OK`

### Same-Origin Cookie Architecture

Both backend and frontend services are deployed behind a single internal Application Load Balancer. This preserves the cookie-based session authentication by ensuring all requests share the same origin. The `API_BASE_URL` for the frontend is set to empty string (same-origin), eliminating cross-origin cookie issues.

## Infrastructure (Terraform)

All AWS infrastructure is provisioned via Terraform using standard `aws_*` resource blocks (LocalStack-compatible). For production deployments using ACME private Terraform modules, see the ACME module mapping documentation in the infrastructure module files.

### Directory Structure

```
infrastructure/
├── environments/
│   ├── dev/                    Dev environment (512 CPU, 1024MB, 1–4 tasks)
│   │   ├── versions.tf        Terraform Cloud backend + provider config
│   │   ├── variables.tf       Variable definitions
│   │   ├── dev.auto.tfvars    Dev-specific values
│   │   ├── data.tf            VPC/subnet data sources
│   │   ├── locals.tf          Dev sizing configuration
│   │   └── main.tf            Module instantiation
│   ├── staging/                Staging environment (1024 CPU, 2048MB, 2–6 tasks)
│   ├── prod/                   Production environment (2048 CPU, 4096MB, 2–10 tasks)
│   └── localstack/             LocalStack validation (local state, endpoint overrides)
└── modules/
    └── common/                 Shared resource definitions (14 files)
        ├── alb.tf              Internal ALB with 7 listener rules
        ├── cloudwatch.tf       CloudWatch log group and stream
        ├── data.tf             Common data sources (caller identity, region)
        ├── ecr.tf              2× ECR repositories (backend, frontend)
        ├── ecs-fargate.tf      ECS cluster, 2× task definitions, 2× services
        ├── iam.tf              3× IAM roles with least-privilege policies
        ├── kms.tf              KMS Customer Managed Key for secrets encryption
        ├── locals.tf           Local values for naming and computed references
        ├── main.tf             Module organization and provider requirements
        ├── outputs.tf          10 required outputs
        ├── rds.tf              RDS SQL Server instance
        ├── secrets.tf          6× Secrets Manager + 8× Parameter Store entries
        ├── security-groups.tf  4× security groups (ALB, Backend, Frontend, RDS)
        └── variables.tf        Module input variables
```

### LocalStack Validation

Validate all Terraform plans against LocalStack before targeting real AWS:

```bash
# Start LocalStack
localstack start -d

# Run Terraform against LocalStack
cd infrastructure/environments/localstack
terraform init
terraform plan
terraform apply -auto-approve

# Run infrastructure validation (19 tests)
scripts/validate-infra-localstack.sh
```

The LocalStack validation suite verifies resource creation (ECR, ECS, ALB, security groups, IAM, KMS, Secrets Manager, Parameter Store, RDS), idempotency (second `terraform plan` shows zero changes), and clean teardown (`terraform destroy` with zero orphans).

### AWS Deployment

```bash
# Initialize environment
cd infrastructure/environments/dev
terraform init
terraform plan -out=plan.out
terraform apply plan.out
```

Environment-specific configurations are isolated to `*.auto.tfvars` files. The same common module is used across all environments with different sizing and scaling parameters.

## Deployment

### First-Time Deployment Sequence

1. **Provision infrastructure** — `terraform apply` creates ECR repositories, ECS cluster (without running tasks), ALB, RDS, security groups, IAM roles, KMS key, Secrets Manager secrets, and Parameter Store parameters
2. **Build and push images** — `scripts/build-and-push.sh` builds Docker images, runs the 12-test local validation suite, and pushes to ECR
3. **Provision database schema** — `scripts/deploy-schema.sh` executes `Build.sql` (concatenated from `SQL Scripts Community/`) and the `SplendidSessions` DDL against the RDS instance
4. **Start services** — `terraform apply` with `image_tag` variable set updates ECS task definitions with image URIs and starts the services

### Subsequent Deployments

1. Build and push new image tags:

```bash
# Build, validate, and push to ECR
scripts/build-and-push.sh
```

2. Deploy with the new image tag:

```bash
cd infrastructure/environments/dev
terraform apply -var="image_tag=v1.2.3"
```

This triggers an ECS rolling deployment — new tasks are started with the updated image before old tasks are drained and stopped.

### Rollback

Rollback is performed by applying the previous `image_tag` value via Terraform:

```bash
cd infrastructure/environments/dev
terraform apply -var="image_tag=v1.2.2"
```

ECS deregisters the current tasks and launches the previous task definition revision. Target rollback time: under 5 minutes.

### Database Schema Provisioning

```bash
# Provision schema against RDS (or local SQL Server)
scripts/deploy-schema.sh
```

The schema deployment script:
- Creates the `SplendidCRM` database if it does not exist
- Executes `Build.sql` with a 600-second timeout (`sqlcmd -t 600`)
- Applies the `SplendidSessions` DDL for ASP.NET Core distributed session support
- Validates minimum object counts (≥218 tables, ≥583 views, ≥890 procedures)

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
│   └── Integrations/              Dormant integration stubs (17 subdirectories)
│
└── SplendidCRM.Web/               ASP.NET Core MVC — hosting
    ├── Program.cs                 Entry point, DI container, middleware pipeline
    ├── appsettings.json           Base configuration defaults
    ├── Controllers/
    │   ├── RestController.cs      Main REST API (82 endpoints)
    │   ├── AdminRestController.cs Admin REST API (58 endpoints)
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
    │   ├── ISugarSoapService.cs   SOAP service interface (41 methods)
    │   ├── SugarSoapService.cs    SOAP implementation
    │   └── DataCarriers.cs        SOAP DTOs (contact_detail, entry_value, etc.)
    ├── Hubs/
    │   ├── ChatManagerHub.cs      ASP.NET Core SignalR
    │   ├── TwilioManagerHub.cs    ASP.NET Core SignalR
    │   └── PhoneBurnerHub.cs      ASP.NET Core SignalR
    ├── SignalR/                    Chat, Twilio, PhoneBurner managers, SignalRUtils
    ├── Authentication/            Windows, Forms, SSO, DuoUniversal 2FA setup
    ├── Authorization/             4-tier ACL: Module → Team → Field → Record
    ├── Middleware/                 SPA redirect, cookie policy
    └── Configuration/             AWS Secrets Manager, Parameter Store providers
```

### Key Components

- **REST API** — All 82 main REST endpoints and 58 admin endpoints are served via ASP.NET Core Web API controllers. Routes are preserved at `/Rest.svc/{operation}` and `/Administration/Rest.svc/{operation}` for backward compatibility.
- **SOAP Service** — The 41-method SOAP interface is hosted via SoapCore middleware, preserving the `sugarsoap` XML namespace (`http://www.sugarcrm.com/sugarcrm`) and WSDL contract.
- **SignalR** — Real-time hubs for Chat, Twilio, and PhoneBurner are implemented using ASP.NET Core SignalR.
- **Background Services** — Four `IHostedService` implementations handle scheduled jobs, email polling, archive processing, and cache invalidation with configurable intervals and reentrancy guards.
- **Authentication** — Supports Windows (Negotiate/NTLM), Forms (cookie-based), and SSO (OIDC/SAML) authentication modes, selectable via the `AUTH_MODE` environment variable. Optional DuoUniversal 2FA integration is available.
- **Authorization** — A 4-tier ACL model enforces access control at the Module, Team, Field, and Record levels, replicating the original `Security.Filter` SQL predicate injection.
- **Distributed Session** — Session state is backed by Redis or SQL Server (configurable via `SESSION_PROVIDER`), replacing the legacy in-process session.
- **Health Check** — `GET /api/health` returns `200 OK` with a JSON status payload for use by load balancers and container orchestrators.

## Running the Application

Start the application from the repository root:

```bash
dotnet run --project src/SplendidCRM.Web
```

By default, Kestrel listens on `http://localhost:5000`. Override the listening URL with the `ASPNETCORE_URLS` environment variable:

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

The SOAP service WSDL contract is preserved with the original `sugarsoap` XML namespace (`http://www.sugarcrm.com/sugarcrm`). All 41 SOAP methods and data carriers (`contact_detail`, `entry_value`, `name_value`, `document_revision`, etc.) maintain byte-comparable serialization.

### JSON Serialization

API controller responses use **Newtonsoft.Json 13.x** as the primary JSON serializer, configured via `AddNewtonsoftJson()` in `Program.cs`. This preserves backward compatibility with the legacy .NET Framework 4.8 serialization behavior. Key serialization settings:

- **DateTime handling**: `DateTimeZoneHandling.Utc` — all `DateTime` values are serialized in UTC format
- **Null handling**: `NullValueHandling.Include` — properties with `null` values are included in the JSON output
- **Property naming**: Default camelCase convention (`CamelCasePropertyNamesContractResolver`)

> **Note for frontend developers (Prompt 2):** The serializer is Newtonsoft.Json, not System.Text.Json. If migrating API client code, ensure date parsing and null handling align with the Newtonsoft.Json defaults described above.

### SignalR Hub Endpoints

SignalR hubs have been migrated from OWIN-hosted Microsoft.AspNet.SignalR to ASP.NET Core SignalR. Hub endpoints are available at:
- `/hubs/chat` — ChatManager
- `/hubs/twilio` — TwilioManager
- `/hubs/phoneburner` — PhoneBurnerManager

> **Note for frontend developers (Prompt 2):** The SignalR wire protocol has been upgraded. Client libraries must use the ASP.NET Core SignalR JavaScript client (`@microsoft/signalr`) instead of the legacy jQuery SignalR client.

## License

SplendidCRM Community Edition is licensed under the [GNU Affero General Public License v3.0](LICENSE).
