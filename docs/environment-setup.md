# SplendidCRM Full-Stack Environment Setup Guide

This guide covers setting up the modernized SplendidCRM development environment. The stack consists of:

- **Backend:** ASP.NET Core 10 running on Kestrel (port 5000) — migrated from ASP.NET Framework 4.8 / IIS in Prompt 1
- **Frontend:** React 19 + Vite 6.x standalone SPA — migrated from React 18 / Webpack 5 in Prompt 2
- **Database:** SQL Server Express 2022 (or full SQL Server)

> **Note:** Containerization (Docker images, AWS ECS/Fargate deployment, Nginx reverse proxy, CI/CD pipelines) is covered separately in Prompt 3.

---

## Prerequisites

Ensure the following tools are installed before proceeding:

| Requirement | Version | Notes |
|---|---|---|
| Node.js | 20 LTS (20.x) | Ships with npm 10.x |
| .NET SDK | 10.0 | For the ASP.NET Core backend |
| SQL Server | Express 2022 or compatible | Docker-hosted recommended on Linux/macOS |
| npm | 10.x (ships with Node.js) | **Do NOT use Yarn** — project migrated from Yarn 1.22 to npm |
| Git | 2.x+ | Source control |
| OS | Linux or macOS (primary); Windows with WSL2 also supported | Linux-first development workflow |

---

## 1. Node.js 20 LTS Installation

### Linux (Ubuntu/Debian)

```bash
curl -fsSL https://deb.nodesource.com/setup_20.x | sudo -E bash -
sudo apt-get install -y nodejs
```

### macOS (Homebrew)

```bash
brew install node@20
```

### Windows

Download the LTS installer from [https://nodejs.org/](https://nodejs.org/) or use `nvm-windows`:

```powershell
nvm install 20
nvm use 20
```

### Verification

```bash
node --version   # Expected: v20.x.x
npm --version    # Expected: 10.x.x or 11.x.x
```

> **Important:** This project has migrated from **Yarn 1.22** to **npm**. Do **NOT** use `yarn` commands anywhere in this project. Use `npm` exclusively. The lockfile is `package-lock.json` (not `yarn.lock`).

---

## 2. .NET 10 SDK Setup

### Linux (Ubuntu/Debian) — Script Installation

```bash
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 10.0
```

After installation, add to your shell profile (e.g., `~/.bashrc` or `~/.zshrc`):

```bash
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools"
```

### Linux (Ubuntu/Debian) — Package Manager

```bash
# Add Microsoft package repository
wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

sudo apt-get update
sudo apt-get install -y dotnet-sdk-10.0
```

### macOS

Download the installer from [https://dotnet.microsoft.com/download/dotnet/10.0](https://dotnet.microsoft.com/download/dotnet/10.0).

### Windows

Download the installer from the same URL above, or use `winget`:

```powershell
winget install Microsoft.DotNet.SDK.10
```

### Verification

```bash
dotnet --version   # Expected: 10.0.x
```

> **Note:** The backend runs on **Kestrel** (not IIS). No IIS setup is needed for development. This is a change from the legacy setup which required Windows and IIS.

---

## 3. SQL Server Express Setup

### Linux / macOS (Docker — Recommended)

```bash
# Pull the SQL Server 2022 image
docker pull mcr.microsoft.com/mssql/server:2022-latest

# Start SQL Server container
docker run \
  -e "ACCEPT_EULA=Y" \
  -e "MSSQL_SA_PASSWORD=YourStrong!Password" \
  -e "MSSQL_PID=Express" \
  -p 1433:1433 \
  --name splendid-sql \
  -v mssql_data:/var/opt/mssql \
  -d mcr.microsoft.com/mssql/server:2022-latest
```

Wait for SQL Server to become ready:

```bash
# Check container is running
docker ps | grep splendid-sql

# Test connectivity (requires sqlcmd or mssql-tools)
docker exec splendid-sql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "YourStrong!Password" -C -Q "SELECT 1"
```

### Windows

Download SQL Server Express 2022 from [Microsoft's download page](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) and run the installer.

### Database Initialization

The database schema is created from SQL scripts in the repository:

```bash
# Create the combined Build.sql from individual script directories
mkdir -p dist/sql

# Concatenate scripts in dependency order
for dir in ProceduresDDL BaseTables Tables Functions ViewsDDL Views Procedures Triggers Data Reports Terminology; do
  find "SQL Scripts Community/$dir" -name "*.sql" -print0 | sort -z | xargs -r -0 cat >> dist/sql/Build.sql
  echo "GO" >> dist/sql/Build.sql
done
```

Then execute `dist/sql/Build.sql` against your SQL Server instance to create the `SplendidCRM` database and all schema objects.

### Connection String

The standard connection string format is:

```
Server=localhost,1433;Database=SplendidCRM;User Id=sa;Password=YourStrong!Password;TrustServerCertificate=true;
```

Set this as an environment variable for the backend:

```bash
export ConnectionStrings__SplendidCRM="Server=localhost,1433;Database=SplendidCRM;User Id=sa;Password=YourStrong!Password;TrustServerCertificate=true;"
```

---

## 4. Backend Setup (ASP.NET Core 10)

```bash
# Navigate to the backend project
cd SplendidCRM

# Restore NuGet packages
dotnet restore

# Build the backend
dotnet build

# Set the database connection string (if not already set)
export ConnectionStrings__SplendidCRM="Server=localhost,1433;Database=SplendidCRM;User Id=sa;Password=YourStrong!Password;TrustServerCertificate=true;"

# Run the backend (listens on port 5000)
dotnet run --urls "http://localhost:5000"
```

### Verify Backend Health

In a separate terminal:

```bash
curl http://localhost:5000/api/health
# Expected: HTTP 200
```

> **Note:** The backend must be running for the frontend Vite dev server proxy to work. Start the backend before starting the frontend dev server.

### Backend API Endpoints

| Endpoint Pattern | Description |
|---|---|
| `/Rest.svc/*` | CRM REST API (152 endpoints) |
| `/Administration/Rest.svc/*` | Admin REST API (65 endpoints) |
| `/hubs/chat` | SignalR ChatHub |
| `/hubs/twilio` | SignalR TwilioHub |
| `/hubs/phoneburner` | SignalR PhoneBurnerHub |
| `/api/health` | Health check endpoint |

---

## 5. Frontend Workspace Setup (React 19 + Vite)

```bash
# Navigate to the React workspace
cd SplendidCRM/React

# Install all dependencies via npm (NOT yarn)
npm install
```

### Key Migration Notes

| Aspect | Old (Pre-Migration) | New (Current) |
|---|---|---|
| Package Manager | Yarn 1.22 (`yarn.lock`) | npm (`package-lock.json`) |
| Build Tool | Webpack 5.90.2 (6 config files in `configs/webpack/`) | Vite 6.x (single `vite.config.ts`) |
| Build Config | `configs/webpack/common.js`, `dev_local.js`, `dev_remote.js`, `mobile.js`, `prod.js`, `prod_minimize.js` | `vite.config.ts` at project root |
| TypeScript | 5.3.3 — `target: ES5`, `module: CommonJS` | 5.8.x — `target: ES2015`, `module: ESNext`, `moduleResolution: bundler` |
| CSS Compiler | node-sass 9.0.0 (native, deprecated) | sass (Dart Sass, pure JS) |

### Important Configuration Details

- **MobX Decorators:** Supported via `experimentalDecorators: true` in `tsconfig.json` and Babel decorator plugins (`@babel/plugin-proposal-decorators` with `{ legacy: true }`) in the Vite config.
- **CKEditor Custom Build:** Located at `ckeditor5-custom-build/` with pre-compiled output in `build/ckeditor.js`. Referenced as a local file dependency (`"ckeditor5-custom-build": "file:./ckeditor5-custom-build"`). Does not need rebuilding.
- **`@babel/standalone`:** This is a **production dependency** (not devDependency) used for runtime in-browser TSX compilation by the Dynamic Layout system. It is included in Vite's `optimizeDeps.include` to ensure proper pre-bundling.
- **`.npmrc`:** Contains `legacy-peer-deps=true` to handle peer dependency conflicts from older BPMN packages.

---

## 6. Development Workflow

### Starting the Dev Server

```bash
cd SplendidCRM/React

# Start Vite dev server (with API proxy to backend)
npm run dev
```

The Vite dev server:

- Runs on **`http://localhost:5173`** (Vite's default port)
- Provides **Hot Module Replacement (HMR)** for instant updates without full page reloads
- **Proxies API calls** to the ASP.NET Core backend at `http://localhost:5000`
- Serves `public/config.json` for runtime configuration

### Vite Dev Proxy Configuration

The proxy rules configured in `vite.config.ts` route API requests to the backend:

| URL Pattern | Target | Description |
|---|---|---|
| `/Rest.svc/**` | `http://localhost:5000` | CRM REST API — 152 endpoints |
| `/Administration/Rest.svc/**` | `http://localhost:5000` | Admin API — 65 endpoints |
| `/hubs/**` | `http://localhost:5000` (WebSocket) | SignalR hub connections (`ws: true`) |
| `/api/**` | `http://localhost:5000` | Health check and utility endpoints |

> **Production Note:** In production, there is no proxy. The frontend reads `API_BASE_URL` from `/config.json` and makes direct cross-origin requests with `credentials: 'include'`. The backend must have CORS configured to allow the frontend origin.

### Type Checking

```bash
# Run TypeScript type checking (separate from Vite build)
npm run typecheck
```

Vite does **NOT** type-check during builds. This is a change from the old Webpack setup which used `fork-ts-checker-webpack-plugin` for in-build type checking. Type checking is now a separate step via `tsc --noEmit`.

---

## 7. Production Build

```bash
cd SplendidCRM/React

# Build for production
npm run build
```

### Build Output

Build artifacts are written to `SplendidCRM/React/dist/`:

| Output | Description |
|---|---|
| `dist/index.html` | Entry point with auto-injected `<script type="module">` tags |
| `dist/assets/index-[hash].js` | Application entry chunk |
| `dist/assets/vendor-[hash].js` | Vendor/library chunk |
| `dist/assets/index-[hash].css` | Compiled and bundled stylesheets |
| `dist/assets/*` | Additional chunks, fonts, and images |

Source maps are generated alongside each chunk for production debugging.

### Key Difference from Old Build

| Old Build (Webpack) | New Build (Vite) |
|---|---|
| `yarn build` | `npm run build` |
| Single `dist/js/SteviaCRM.js` bundle | Chunked `dist/` output with hashed filenames |
| `index.html.ejs` Webpack template | Root `index.html` as Vite entry point |
| `SteviaCRM.js` loaded via `<script>` tag | ES modules loaded via `<script type="module">` |

### Preview Production Build Locally

```bash
# Serve the production build for local testing
npm run preview
```

This starts a static file server serving the `dist/` directory, useful for verifying the production build before deployment.

---

## 8. Runtime Configuration

### Overview

The build output contains **NO environment-specific values**. The same build artifact works in any environment (development, staging, production) without rebuilding. Configuration is injected at runtime via `/config.json`.

### Configuration File: `public/config.json`

During development, this file lives at `SplendidCRM/React/public/config.json` and is served by Vite's dev server:

```json
{
  "API_BASE_URL": "http://localhost:5000",
  "SIGNALR_URL": "",
  "ENVIRONMENT": "development"
}
```

### Field Reference

| Field | Type | Default | Description |
|---|---|---|---|
| `API_BASE_URL` | string | `"http://localhost:5000"` | Full URL to the ASP.NET Core backend. Used as prefix for all REST API calls and SignalR hub connections. |
| `SIGNALR_URL` | string | `""` (empty) | Optional separate URL for SignalR hub connections. When empty, falls back to `API_BASE_URL`. Useful when SignalR is hosted on a different server or port. |
| `ENVIRONMENT` | string | `"development"` | Environment identifier (development, staging, production). Used for conditional logging and feature flags. |

### How It Works

1. The frontend app loads `/config.json` **before** React initialization
2. The configuration is stored in a typed singleton (`src/config.ts`)
3. All HTTP requests use `config.API_BASE_URL` as the base URL prefix
4. SignalR connections use `config.SIGNALR_URL` (or `config.API_BASE_URL` as fallback)

This replaces the old same-origin assumption where the React app was served from the same origin as the ASP.NET backend and all API calls used relative URLs.

### Production Configuration

In production (Prompt 3), `config.json` is written by the Docker container's entrypoint script from environment variables. The same Docker image is used across all environments — only the `config.json` contents change.

---

## 9. SignalR Hub Endpoints

The legacy jQuery-based SignalR client (`signalr` 2.4.3) and its single `/signalr` endpoint have been completely removed. The application now uses `@microsoft/signalr` 10.0.0 with discrete ASP.NET Core SignalR hub endpoints.

### Hub Endpoint Reference

| Hub | Endpoint Path | Full URL | Purpose |
|---|---|---|---|
| ChatHub | `/hubs/chat` | `{API_BASE_URL}/hubs/chat` | Real-time chat messaging |
| TwilioHub | `/hubs/twilio` | `{API_BASE_URL}/hubs/twilio` | Twilio telephony integration |
| PhoneBurnerHub | `/hubs/phoneburner` | `{API_BASE_URL}/hubs/phoneburner` | PhoneBurner dialer integration |

### URL Construction

Hub URLs are constructed at runtime:

- **Primary:** `{config.API_BASE_URL}/hubs/{hubname}`
- **Override:** `{config.SIGNALR_URL}/hubs/{hubname}` (when `SIGNALR_URL` is set)

### Migration from Legacy SignalR

| Aspect | Before | After |
|---|---|---|
| Client Library | Dual: `@microsoft/signalr` 8.0.0 + legacy `signalr` 2.4.3 | `@microsoft/signalr` 10.0.0 only |
| Hub Endpoint | Single `/signalr` with hub multiplexing | Discrete `/hubs/chat`, `/hubs/twilio`, `/hubs/phoneburner` |
| Transport | OWIN SignalR (jQuery-based) | ASP.NET Core SignalR (modern WebSocket) |
| Configuration | Hardcoded paths | Runtime config via `/config.json` |

---

## 10. Architecture Changes Summary

| Aspect | Before (Pre-Migration) | After (Prompt 2 Migration) |
|---|---|---|
| React Version | 18.2.0 | 19.1.0 |
| Build Tool | Webpack 5.90.2 (6 config files) | Vite 6.x (single `vite.config.ts`) |
| Package Manager | Yarn 1.22 | npm (LTS) |
| Node.js | 16.20 | 20 LTS |
| TypeScript | 5.3.3 (`target: ES5` / `module: CommonJS`) | 5.8.x (`target: ES2015` / `module: ESNext` / `moduleResolution: bundler`) |
| Module System | CommonJS (`require()` / `module.exports`) | ESM (`import` / `export`) |
| CSS Compiler | node-sass 9.0.0 (native C++, deprecated) | sass (Dart Sass, pure JavaScript) |
| SignalR Client | Dual: `@microsoft/signalr` 8.0.0 + legacy `signalr` 2.4.3 | `@microsoft/signalr` 10.0.0 only |
| Routing | `react-router-dom` 6.22.1 | `react-router` 7.x (consolidated package) |
| API Communication | Same-origin (implicit relative URLs) | Cross-origin via `API_BASE_URL` from `/config.json` |
| Build Output | Single `SteviaCRM.js` bundle | Chunked `dist/` with `index.html` entry point |
| Entry Point | Webpack `index.html.ejs` template | Root `index.html` (Vite entry) |
| Dev Server Port | 3000 (webpack-dev-server) | 5173 (Vite) |
| Dev Proxy Target | `http://localhost:80` (`/SplendidCRM/**`) | `http://localhost:5000` (`/Rest.svc`, `/hubs`, `/api`) |
| Type Checking | In-build via `fork-ts-checker-webpack-plugin` | Separate step via `tsc --noEmit` |
| Asset Handling | `file-loader`, `url-loader`, `svg-inline-loader` | Vite built-in asset handling |
| State Management | MobX 6.12.0 with decorators | MobX 6.15.0 with decorators (preserved) |

---

## 11. Handoff Notes for Prompt 3 (Containerization)

This section documents all information needed by the containerization team (Prompt 3).

### Frontend Build

| Item | Value |
|---|---|
| Build Command | `cd SplendidCRM/React && npm install && npm run build` |
| Build Output Directory | `SplendidCRM/React/dist/` |
| Node.js Version | 20 LTS |
| Build-Time Env Vars | **None** — all config is runtime-injected |

### Nginx Configuration Requirements

```nginx
server {
    listen 80;
    root /usr/share/nginx/html;
    index index.html;

    # SPA fallback — all routes serve index.html
    location / {
        try_files $uri $uri/ /index.html;
    }

    # Cache hashed assets aggressively
    location /assets/ {
        expires 1y;
        add_header Cache-Control "public, max-age=31536000, immutable";
    }

    # Do not cache config.json (runtime config)
    location = /config.json {
        expires -1;
        add_header Cache-Control "no-cache, no-store, must-revalidate";
    }
}
```

- Serve static files from `dist/`
- SPA fallback: `try_files $uri $uri/ /index.html`
- Cache headers for hashed assets: `Cache-Control: public, max-age=31536000, immutable`
- No proxy pass needed — the frontend is a standalone SPA that makes direct API calls

### Runtime Config Injection

The container entrypoint script must write environment variables to `/usr/share/nginx/html/config.json` before starting Nginx:

```bash
#!/bin/sh
cat > /usr/share/nginx/html/config.json <<EOF
{
  "API_BASE_URL": "${API_BASE_URL}",
  "SIGNALR_URL": "${SIGNALR_URL:-}",
  "ENVIRONMENT": "${ENVIRONMENT:-production}"
}
EOF

exec nginx -g 'daemon off;'
```

### Config Schema

```json
{
  "API_BASE_URL": "string (required) — Full URL to backend, e.g., http://internal-alb-dns:5000",
  "SIGNALR_URL": "string (optional) — Separate SignalR URL, defaults to API_BASE_URL when empty",
  "ENVIRONMENT": "string (optional) — Environment name, defaults to 'production'"
}
```

### CORS Requirement

The backend's `CORS_ORIGINS` environment variable **must** include the frontend's origin URL for cross-origin API calls to succeed. For example, if the frontend is served at `https://crm.example.com`, the backend needs:

```bash
CORS_ORIGINS=https://crm.example.com
```

### Health Check

```
GET {API_BASE_URL}/api/health → HTTP 200
```

### Key Principle

**Zero build-time environment variables.** The same Docker image works for all environments. Only the runtime `config.json` contents differ between dev, staging, and production.

---

## 12. Troubleshooting

### 1. `npm install` fails with `node-sass` errors

The project has migrated from `node-sass` 9.0.0 (native C++ bindings, deprecated) to `sass` (Dart Sass, pure JavaScript). If you see `node-sass` build errors, ensure `package.json` has been updated with the migrated dependencies. The `sass` package requires no native compilation.

### 2. `require is not defined` in browser console

All CommonJS `require()` calls (44 files) have been converted to ESM `import` statements as part of the migration. If this error appears:

- Check that the specific file has been converted from `require()` to `import`
- Verify `tsconfig.json` has `"module": "ESNext"` (not `"CommonJS"`)
- Check that Vite's `optimizeDeps` is configured to pre-bundle any CJS-only third-party dependencies

### 3. MobX decorators not working (silent failures at runtime)

MobX decorator support requires **both** of the following:

1. `tsconfig.json` must have `"experimentalDecorators": true`
2. `vite.config.ts` must include Babel plugins in the React plugin configuration:
   ```typescript
   react({
     babel: {
       plugins: [
         ['@babel/plugin-proposal-decorators', { legacy: true }],
         ['@babel/plugin-proposal-class-properties', { loose: true }]
       ]
     }
   })
   ```

### 4. API calls returning 404 or CORS errors in development

- Ensure the ASP.NET Core backend is running on port 5000: `curl http://localhost:5000/api/health`
- Verify the Vite dev server proxy is configured in `vite.config.ts` for `/Rest.svc`, `/Administration/Rest.svc`, `/hubs`, and `/api`
- Check that you are accessing the app via `http://localhost:5173` (Vite dev server), not directly hitting the backend

### 5. SignalR connection fails

- Verify hub endpoints are the **new** discrete paths: `/hubs/chat`, `/hubs/twilio`, `/hubs/phoneburner`
- The legacy `/signalr` endpoint no longer exists
- In development, the Vite proxy forwards `/hubs/**` to the backend with WebSocket support (`ws: true`)
- In production, ensure `config.json` has the correct `API_BASE_URL` pointing to the backend

### 6. `@babel/standalone` not available at runtime

- Ensure `@babel/standalone` is listed as a **production dependency** (in `dependencies`, not `devDependencies`) in `package.json`
- Ensure it is included in Vite's `optimizeDeps.include` array in `vite.config.ts`
- This package is required at runtime for the Dynamic Layout system's in-browser TSX compilation

### 7. CKEditor not loading

- The CKEditor 5 custom build is at `ckeditor5-custom-build/` with pre-compiled output in `build/ckeditor.js`
- It is referenced as a local file dependency: `"ckeditor5-custom-build": "file:./ckeditor5-custom-build"`
- Run `cd ckeditor5-custom-build && npm install --ignore-scripts` if the CKEditor dependency fails to resolve

### 8. Build fails on Linux with Windows-specific errors

- The frontend build is designed to run on **Linux** with zero Windows dependencies
- Ensure `node-sass` has been replaced with `sass` (Dart Sass) — `node-sass` requires native C++ compilation that can fail on Linux
- File path case-sensitivity: Linux is case-sensitive while Windows is not. Import paths must match actual filenames exactly

### 9. Peer dependency warnings during `npm install`

- The `.npmrc` file contains `legacy-peer-deps=true` to handle peer dependency conflicts from older BPMN packages (`bpmn-js` 1.3.3 and `bpmn-js-properties-panel` 0.13.1)
- These warnings are expected and do not affect functionality
