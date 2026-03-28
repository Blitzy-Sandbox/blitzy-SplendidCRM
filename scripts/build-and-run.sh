#!/usr/bin/env bash
# =============================================================================
# SplendidCRM Full-Stack Build & Run Script
# =============================================================================
#
# Purpose:  Automated local development setup for SplendidCRM.
#           Starts SQL Server (Docker), provisions database schema, builds and
#           starts the ASP.NET Core 10 backend, builds and starts the React 19
#           / Vite frontend dev server, and verifies all services are healthy.
#
# Usage:    ./scripts/build-and-run.sh [OPTIONS]
#           bash scripts/build-and-run.sh [OPTIONS]
#
# Options:
#   --frontend-only   Build and start only the React frontend; skip SQL Server,
#                     schema provisioning, and backend build/start.
#   --skip-install    Skip `npm install` (use when dependencies are already
#                     installed).
#   --skip-sql        Skip SQL Server Docker startup and schema provisioning
#                     (use when SQL Server is already running and provisioned).
#   --no-dev-server   Build frontend only; do NOT start Vite dev server.
#   --help            Print this usage information and exit.
#
# Prerequisites:
#   - Node.js 20 LTS or higher
#   - npm (bundled with Node.js)
#   - Docker Engine (for SQL Server container)
#   - .NET 10 SDK or higher (unless --frontend-only is used)
#
# Note:     This script MUST be run from the repository root directory
#           (the directory that contains the SplendidCRM/ folder).
#
# Architecture:
#   SQL Server — Docker container (mcr.microsoft.com/mssql/server:2022-latest)
#                Port 1433
#   Backend    — ASP.NET Core 10 (Kestrel) → http://localhost:5000
#   Frontend   — Vite dev server            → http://localhost:3000 (proxies API)
#   Runtime Config — SplendidCRM/React/public/config.json (API_BASE_URL)
#
# Environment Variables (auto-set by this script if not already exported):
#   ConnectionStrings__SplendidCRM  — SQL Server connection string
#   ASPNETCORE_ENVIRONMENT          — Development
#   ASPNETCORE_URLS                 — http://0.0.0.0:5000
#   CORS_ORIGINS                    — http://localhost:3000
#   SESSION_PROVIDER                — SqlServer
#   AUTH_MODE                       — Forms
#   SPLENDID_JOB_SERVER             — $(hostname)
#
# =============================================================================

set -euo pipefail

# =============================================================================
# Color Constants and Utility Functions
# =============================================================================

# Terminal color codes for readable output.
readonly RED='\033[0;31m'
readonly GREEN='\033[0;32m'
readonly YELLOW='\033[0;33m'
readonly BLUE='\033[0;34m'
readonly NC='\033[0m' # No Color — resets terminal color

info() {
  printf "${BLUE}[INFO]${NC} %s\n" "$1"
}

success() {
  printf "${GREEN}[ OK ]${NC} %s\n" "$1"
}

warn() {
  printf "${YELLOW}[WARN]${NC} %s\n" "$1" >&2
}

error() {
  printf "${RED}[FAIL]${NC} %s\n" "$1" >&2
  exit 1
}

check_command() {
  local cmd="$1"
  local display_name="$2"
  if ! command -v "$cmd" &>/dev/null; then
    error "${display_name} is not installed or not on PATH. Please install ${display_name} and try again."
  fi
}

# =============================================================================
# Script Options Parsing
# =============================================================================

FRONTEND_ONLY=false
SKIP_INSTALL=false
SKIP_SQL=false
NO_DEV_SERVER=false

for arg in "$@"; do
  case "$arg" in
    --frontend-only)
      FRONTEND_ONLY=true
      ;;
    --skip-install)
      SKIP_INSTALL=true
      ;;
    --skip-sql)
      SKIP_SQL=true
      ;;
    --no-dev-server)
      NO_DEV_SERVER=true
      ;;
    --help)
      cat << 'HELPEOF'
SplendidCRM Full-Stack Build & Run Script

Usage: ./scripts/build-and-run.sh [OPTIONS]
       bash scripts/build-and-run.sh [OPTIONS]

Options:
  --frontend-only   Build and start only the React 19 frontend; skip SQL
                    Server, schema provisioning, and backend build/start.

  --skip-install    Skip the `npm install` step. Use this when frontend
                    dependencies are already installed to save time.

  --skip-sql        Skip SQL Server Docker startup and schema provisioning.
                    Use this when SQL Server is already running and the
                    SplendidCRM database has been provisioned.

  --no-dev-server   Build the frontend only; do NOT start the Vite dev
                    server. Useful for CI/CD or when you want to serve the
                    production build with `npm run preview`.

  --help            Show this help message and exit.

Prerequisites:
  - Node.js 20 LTS or higher    (required)
  - npm                          (required, bundled with Node.js)
  - Docker Engine                (required for SQL Server container)
  - .NET 10 SDK or higher        (required for backend; skipped with --frontend-only)

This script must be run from the repository root directory (the directory
that contains the SplendidCRM/ folder).

Workflow:
  1. Checks all prerequisites (Node 20+, npm, Docker, .NET 10 SDK)
  2. Starts SQL Server Docker container on port 1433 (if not already running)
  3. Provisions SplendidCRM database from SQL Scripts Community/Build.sql
  4. Installs frontend dependencies (npm install)
  5. Builds frontend with Vite (npm run build)
  6. Restores and builds backend (dotnet build --configuration Release)
  7. Starts backend on http://localhost:5000 with required env vars
  8. Starts Vite dev server on http://localhost:3000
  9. Verifies health of all three services
  10. Prints summary with status indicators

Runtime Configuration:
  The built frontend reads API_BASE_URL from SplendidCRM/React/public/config.json
  at startup. No build-time environment variables are baked into the bundle.

Graceful Shutdown:
  Press Ctrl-C to stop all services. The script traps SIGINT/SIGTERM and
  will stop the Vite dev server, backend, and (optionally) the SQL Server
  Docker container.

Examples:
  # Full-stack: SQL Server + backend + frontend dev server
  ./scripts/build-and-run.sh

  # Frontend only (skip SQL/backend)
  ./scripts/build-and-run.sh --frontend-only

  # Full-stack, SQL already running
  ./scripts/build-and-run.sh --skip-sql

  # Full-stack, deps already installed
  ./scripts/build-and-run.sh --skip-install

  # Build only, no dev server
  ./scripts/build-and-run.sh --no-dev-server
HELPEOF
      exit 0
      ;;
    *)
      error "Unknown option: ${arg}. Use --help for usage information."
      ;;
  esac
done

# =============================================================================
# Cleanup / Graceful Shutdown
# =============================================================================

# PIDs are set later when services are started in background.
BACKEND_PID=""
FRONTEND_PID=""

# cleanup: Stops the frontend dev server and backend if started by this script.
# Called on ERR, INT (Ctrl-C), and TERM signals for graceful shutdown.
cleanup() {
  echo ""
  info "Shutting down services..."

  # Stop Vite dev server
  if [ -n "${FRONTEND_PID:-}" ]; then
    info "Stopping Vite dev server (PID: ${FRONTEND_PID})..."
    kill "${FRONTEND_PID}" 2>/dev/null || true
    wait "${FRONTEND_PID}" 2>/dev/null || true
    FRONTEND_PID=""
    success "Vite dev server stopped."
  fi

  # Stop .NET backend
  if [ -n "${BACKEND_PID:-}" ]; then
    info "Stopping backend server (PID: ${BACKEND_PID})..."
    kill "${BACKEND_PID}" 2>/dev/null || true
    wait "${BACKEND_PID}" 2>/dev/null || true
    BACKEND_PID=""
    success "Backend server stopped."
  fi

  # SQL Server Docker container is intentionally NOT stopped by default.
  # Database data persists in the Docker volume. To stop it manually:
  #   docker stop splendid-sql-express
  info "SQL Server Docker container left running (data persists in volume)."
  info "To stop SQL Server: docker stop splendid-sql-express"
  echo ""
}

trap cleanup ERR INT TERM

# =============================================================================
# Phase 1: Prerequisite Checks
# =============================================================================

echo ""
printf '%b============================================%b\n' "${BLUE}" "${NC}"
printf '%b SplendidCRM Full-Stack Build & Run%b\n' "${BLUE}" "${NC}"
printf '%b============================================%b\n' "${BLUE}" "${NC}"
echo ""

info "Phase 1: Checking prerequisites..."

# ---- Node.js 20 LTS ----
check_command "node" "Node.js"
NODE_VERSION_RAW=$(node --version)
NODE_MAJOR=$(echo "${NODE_VERSION_RAW}" | sed -E 's/^v([0-9]+)\..*/\1/')
if [ "${NODE_MAJOR}" -lt 20 ]; then
  error "Node.js 20 LTS or higher is required. Found: ${NODE_VERSION_RAW}"
fi
success "Node.js ${NODE_VERSION_RAW} detected (>= 20 required)"

# ---- npm ----
check_command "npm" "npm"
NPM_VERSION_RAW=$(npm --version)
success "npm v${NPM_VERSION_RAW} detected"

# ---- Docker ----
if [ "${FRONTEND_ONLY}" = false ] && [ "${SKIP_SQL}" = false ]; then
  check_command "docker" "Docker"
  # Verify Docker daemon is running
  if ! docker info &>/dev/null; then
    # Try with sudo
    if sudo docker info &>/dev/null; then
      DOCKER_CMD="sudo docker"
    else
      error "Docker daemon is not running. Start Docker and try again."
    fi
  else
    DOCKER_CMD="docker"
  fi
  DOCKER_VERSION_RAW=$(${DOCKER_CMD} --version | sed -E 's/.*version ([0-9.]+).*/\1/')
  success "Docker ${DOCKER_VERSION_RAW} detected and running"
fi

# ---- .NET 10 SDK (skip check if --frontend-only) ----
if [ "${FRONTEND_ONLY}" = false ]; then
  check_command "dotnet" ".NET SDK"
  DOTNET_VERSION_RAW=$(dotnet --version)
  DOTNET_MAJOR=$(echo "${DOTNET_VERSION_RAW}" | sed -E 's/^([0-9]+)\..*/\1/')
  if [ "${DOTNET_MAJOR}" -lt 10 ]; then
    error ".NET 10 SDK or higher is required. Found: ${DOTNET_VERSION_RAW}"
  fi
  success ".NET SDK ${DOTNET_VERSION_RAW} detected (>= 10 required)"
fi

echo ""
success "All prerequisites satisfied."
echo ""

# =============================================================================
# Phase 2: SQL Server Docker Startup & Schema Provisioning
# =============================================================================

if [ "${FRONTEND_ONLY}" = false ] && [ "${SKIP_SQL}" = false ]; then
  printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"
  info "Phase 2: SQL Server Startup & Schema Provisioning"
  printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"

  SQL_CONTAINER="splendid-sql-express"
  SQL_IMAGE="mcr.microsoft.com/mssql/server:2022-latest"
  SQL_PORT=1433

  # Determine SQL password from environment or use default for local dev
  if [ -n "${SQL_PASSWORD:-}" ]; then
    SA_PASSWORD="${SQL_PASSWORD}"
  elif [ -n "${ConnectionStrings__SplendidCRM:-}" ]; then
    # Try to extract password from connection string
    SA_PASSWORD=$(echo "${ConnectionStrings__SplendidCRM}" | grep -oP 'Password=\K[^;]+' || echo "")
    if [ -z "${SA_PASSWORD}" ]; then
      error "Cannot extract SQL password from ConnectionStrings__SplendidCRM. Set SQL_PASSWORD env var."
    fi
  else
    error "SQL_PASSWORD or ConnectionStrings__SplendidCRM must be set. Example: export SQL_PASSWORD='YourStrong!Passw0rd'"
  fi

  # Check if SQL Server container is already running
  SQL_RUNNING=false
  if ${DOCKER_CMD} ps --format '{{.Names}}' 2>/dev/null | grep -q "^${SQL_CONTAINER}$"; then
    SQL_RUNNING=true
    success "SQL Server container '${SQL_CONTAINER}' is already running."
  fi

  if [ "${SQL_RUNNING}" = false ]; then
    info "Starting SQL Server Docker container..."

    # Remove existing stopped container if present
    ${DOCKER_CMD} rm -f "${SQL_CONTAINER}" 2>/dev/null || true

    # Start SQL Server Express in Docker
    ${DOCKER_CMD} run \
      -e "ACCEPT_EULA=Y" \
      -e "MSSQL_SA_PASSWORD=${SA_PASSWORD}" \
      -e "MSSQL_PID=Express" \
      -p "${SQL_PORT}:1433" \
      --name "${SQL_CONTAINER}" \
      -v mssql_data:/var/opt/mssql \
      -d "${SQL_IMAGE}"

    success "SQL Server container started on port ${SQL_PORT}."
  fi

  # Wait for SQL Server to accept connections (timeout 60 seconds)
  info "Waiting for SQL Server to accept connections..."
  SQL_READY=false
  SQL_MAX_WAIT=60
  SQL_ELAPSED=0

  while [ "${SQL_ELAPSED}" -lt "${SQL_MAX_WAIT}" ]; do
    if ${DOCKER_CMD} exec "${SQL_CONTAINER}" /opt/mssql-tools18/bin/sqlcmd \
        -S localhost -U sa -P "${SA_PASSWORD}" -C -Q "SELECT 1" >/dev/null 2>&1; then
      SQL_READY=true
      break
    fi
    printf "."
    sleep 2
    SQL_ELAPSED=$((SQL_ELAPSED + 2))
  done
  echo ""

  if [ "${SQL_READY}" = false ]; then
    error "SQL Server did not become ready within ${SQL_MAX_WAIT}s. Check container logs: docker logs ${SQL_CONTAINER}"
  fi
  success "SQL Server is ready and accepting connections."

  # ---- Schema Provisioning ----
  # Check if SplendidCRM database already exists with expected objects.
  # We verify both table and view counts to guard against partial provisioning.
  DB_EXISTS=false
  TABLE_COUNT=$(${DOCKER_CMD} exec "${SQL_CONTAINER}" /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "${SA_PASSWORD}" -C \
    -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM SplendidCRM.INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'" \
    -h -1 -W 2>/dev/null | tr -d '[:space:]' || echo "0")
  VIEW_COUNT=$(${DOCKER_CMD} exec "${SQL_CONTAINER}" /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "${SA_PASSWORD}" -C \
    -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM SplendidCRM.INFORMATION_SCHEMA.VIEWS" \
    -h -1 -W 2>/dev/null | tr -d '[:space:]' || echo "0")

  if [ "${TABLE_COUNT}" -gt 300 ] && [ "${VIEW_COUNT}" -gt 500 ]; then
    DB_EXISTS=true
    success "SplendidCRM database exists with ${TABLE_COUNT} tables and ${VIEW_COUNT} views. Skipping schema provisioning."
  fi

  if [ "${DB_EXISTS}" = false ]; then
    info "Provisioning SplendidCRM database schema..."

    # Create database if it does not exist
    ${DOCKER_CMD} exec "${SQL_CONTAINER}" /opt/mssql-tools18/bin/sqlcmd \
      -S localhost -U sa -P "${SA_PASSWORD}" -C \
      -Q "IF NOT EXISTS (SELECT * FROM sys.databases WHERE name='SplendidCRM') CREATE DATABASE SplendidCRM"

    # Build consolidated SQL script
    SQL_ARTIFACT_DIR="./dist/sql"
    mkdir -p "${SQL_ARTIFACT_DIR}"

    # Regenerate Build.sql if it does not exist, is empty, or was generated
    # by an older version of this script (prior to the suffix-ordered fix).
    # The marker comment below is written at the top of every correctly
    # generated Build.sql so we can detect stale files.
    BUILD_SQL_MARKER="-- build-and-run.sh suffix-ordered v2"
    NEEDS_REBUILD=false
    if [ ! -f "${SQL_ARTIFACT_DIR}/Build.sql" ] || [ ! -s "${SQL_ARTIFACT_DIR}/Build.sql" ]; then
      NEEDS_REBUILD=true
    elif ! head -1 "${SQL_ARTIFACT_DIR}/Build.sql" | grep -qF "${BUILD_SQL_MARKER}"; then
      info "Detected Build.sql generated with old ordering. Regenerating..."
      NEEDS_REBUILD=true
    fi

    if [ "${NEEDS_REBUILD}" = true ]; then
      info "Generating Build.sql from SQL Scripts Community/..."

      echo "${BUILD_SQL_MARKER}" > "${SQL_ARTIFACT_DIR}/Build.sql"

      # Directory order matches the upstream Build.bat dependency chain.
      # "Reports" is intentionally omitted — the community edition ships no
      # Reports/ SQL directory, and its absence is harmless.
      SQL_DIRS=(
        "ProceduresDDL" "BaseTables" "Tables" "Functions"
        "ViewsDDL" "Views" "Procedures" "Triggers"
        "Data" "Terminology"
      )

      # CRITICAL: Within each directory the SQL files use a numeric suffix
      # convention (e.g. ACCOUNTS.1.sql, ACCOUNTS_BUGS.2.sql) to encode
      # dependency order. All *.0.sql files must execute before *.1.sql,
      # all *.1.sql before *.2.sql, and so on up to *.9.sql.  A plain
      # alphabetical sort would interleave suffixes (ACCOUNTS_BUGS.2.sql
      # before BUGS.1.sql) causing foreign-key failures.  The nested loop
      # below mirrors the Build.bat strategy of concatenating by suffix.
      for dir in "${SQL_DIRS[@]}"; do
        if [ -d "SQL Scripts Community/${dir}" ]; then
          for suffix in 0 1 2 3 4 5 6 7 8 9; do
            find "SQL Scripts Community/${dir}" -name "*.${suffix}.sql" -print0 \
              | sort -z \
              | xargs -r -0 cat >> "${SQL_ARTIFACT_DIR}/Build.sql"
          done
          echo "GO" >> "${SQL_ARTIFACT_DIR}/Build.sql"
        fi
      done

      success "Build.sql generated ($(wc -c < "${SQL_ARTIFACT_DIR}/Build.sql") bytes)."
    else
      success "Build.sql already exists ($(wc -c < "${SQL_ARTIFACT_DIR}/Build.sql") bytes)."
    fi

    # Execute Build.sql against SQL Server
    info "Executing Build.sql (this may take several minutes)..."
    ${DOCKER_CMD} cp "${SQL_ARTIFACT_DIR}/Build.sql" "${SQL_CONTAINER}:/tmp/Build.sql"
    # Note: -b is intentionally omitted so that non-fatal dependency
    # warnings (e.g. forward-referenced modules) do not abort the run.
    ${DOCKER_CMD} exec "${SQL_CONTAINER}" /opt/mssql-tools18/bin/sqlcmd \
      -S localhost -U sa -P "${SA_PASSWORD}" -C \
      -d SplendidCRM \
      -i /tmp/Build.sql \
      2>&1 | tail -5 || warn "Some SQL statements produced warnings (non-fatal)."

    # Verify provisioning — check tables, views, stored procedures and functions
    FINAL_TABLE_COUNT=$(${DOCKER_CMD} exec "${SQL_CONTAINER}" /opt/mssql-tools18/bin/sqlcmd \
      -S localhost -U sa -P "${SA_PASSWORD}" -C \
      -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM SplendidCRM.INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'" \
      -h -1 -W 2>/dev/null | tr -d '[:space:]' || echo "0")
    FINAL_VIEW_COUNT=$(${DOCKER_CMD} exec "${SQL_CONTAINER}" /opt/mssql-tools18/bin/sqlcmd \
      -S localhost -U sa -P "${SA_PASSWORD}" -C \
      -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM SplendidCRM.INFORMATION_SCHEMA.VIEWS" \
      -h -1 -W 2>/dev/null | tr -d '[:space:]' || echo "0")
    FINAL_PROC_COUNT=$(${DOCKER_CMD} exec "${SQL_CONTAINER}" /opt/mssql-tools18/bin/sqlcmd \
      -S localhost -U sa -P "${SA_PASSWORD}" -C \
      -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM SplendidCRM.INFORMATION_SCHEMA.ROUTINES WHERE ROUTINE_TYPE='PROCEDURE'" \
      -h -1 -W 2>/dev/null | tr -d '[:space:]' || echo "0")
    FINAL_FUNC_COUNT=$(${DOCKER_CMD} exec "${SQL_CONTAINER}" /opt/mssql-tools18/bin/sqlcmd \
      -S localhost -U sa -P "${SA_PASSWORD}" -C \
      -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM SplendidCRM.INFORMATION_SCHEMA.ROUTINES WHERE ROUTINE_TYPE='FUNCTION'" \
      -h -1 -W 2>/dev/null | tr -d '[:space:]' || echo "0")
    success "Schema provisioned: ${FINAL_TABLE_COUNT} tables, ${FINAL_VIEW_COUNT} views, ${FINAL_PROC_COUNT} procedures, ${FINAL_FUNC_COUNT} functions."

    # Sanity-check: the community edition should produce at least 300 tables and 500 views.
    if [ "${FINAL_TABLE_COUNT}" -lt 300 ] || [ "${FINAL_VIEW_COUNT}" -lt 500 ]; then
      warn "Schema object counts are lower than expected. Some SQL scripts may have failed."
      warn "Expected ≥300 tables and ≥500 views; got ${FINAL_TABLE_COUNT} tables and ${FINAL_VIEW_COUNT} views."
    fi
  fi

  # Ensure SplendidSessions table exists (required for session management)
  ${DOCKER_CMD} exec "${SQL_CONTAINER}" /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "${SA_PASSWORD}" -C \
    -d SplendidCRM \
    -Q "
      IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='SplendidSessions')
      BEGIN
        CREATE TABLE dbo.SplendidSessions (
          SessionId    NVARCHAR(88) NOT NULL PRIMARY KEY,
          Value        VARBINARY(MAX) NOT NULL,
          ExpiresAtTime DATETIMEOFFSET NOT NULL,
          SlidingExpirationInSeconds BIGINT NULL,
          AbsoluteExpiration DATETIMEOFFSET NULL
        );
        PRINT 'Created SplendidSessions table.';
      END
      ELSE
        PRINT 'SplendidSessions table already exists.';
    " 2>/dev/null || warn "SplendidSessions table check produced a warning."

  # Set connection string for the backend
  export ConnectionStrings__SplendidCRM="Server=localhost,${SQL_PORT};Database=SplendidCRM;User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True;"
  success "ConnectionStrings__SplendidCRM set for localhost:${SQL_PORT}."

  echo ""

elif [ "${FRONTEND_ONLY}" = false ] && [ "${SKIP_SQL}" = true ]; then
  info "Skipping SQL Server startup (--skip-sql flag set)."
  # Verify SQL is reachable
  SQL_DETECTED=false
  if command -v nc &>/dev/null; then
    if nc -z localhost 1433 2>/dev/null; then SQL_DETECTED=true; fi
  elif [ -f /proc/net/tcp ]; then
    if grep -q ":0599" /proc/net/tcp 2>/dev/null; then SQL_DETECTED=true; fi
  fi
  if [ "${SQL_DETECTED}" = true ]; then
    success "SQL Server detected on localhost:1433."
  else
    warn "SQL Server not detected on localhost:1433. Backend may fail to start."
  fi
  echo ""
fi

# Save repository root for reliable navigation.
REPO_ROOT=$(pwd)

# =============================================================================
# Phase 3: Frontend Build (React 19 + Vite)
# =============================================================================

printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"
info "Phase 3: Building Frontend (React 19 + Vite)"
printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"

FRONTEND_DIR="SplendidCRM/React"
if [ ! -d "${FRONTEND_DIR}" ]; then
  error "Frontend directory not found: ${FRONTEND_DIR}. Are you running from the repository root?"
fi

cd "${FRONTEND_DIR}"

# ---- npm install (unless --skip-install) ----
if [ "${SKIP_INSTALL}" = false ]; then
  info "Installing frontend dependencies..."
  if CI=true npm install; then
    success "Frontend dependencies installed."
  else
    error "Failed to install frontend dependencies. Check npm output above."
  fi
else
  info "Skipping npm install (--skip-install flag set)."
  if [ ! -d "node_modules" ]; then
    warn "node_modules/ not found! Consider running without --skip-install."
  fi
fi

# ---- npm run build (vite build) ----
# Guard against JavaScript heap-out-of-memory on constrained machines.
# Vite + Rollup bundling the full 763-file SPA can peak above the default
# Node.js heap limit.  Setting --max-old-space-size=4096 (4 GB) provides
# comfortable headroom without requiring the caller to export NODE_OPTIONS.
export NODE_OPTIONS="${NODE_OPTIONS:---max-old-space-size=4096}"
info "Building frontend with Vite (NODE_OPTIONS=${NODE_OPTIONS})..."
if npm run build; then
  success "Frontend build complete. Output: ${FRONTEND_DIR}/dist/"
else
  error "Frontend build failed. Check Vite output above."
fi

cd "${REPO_ROOT}"

# =============================================================================
# Phase 4: Backend Build (ASP.NET Core 10) — skipped with --frontend-only
# =============================================================================

if [ "${FRONTEND_ONLY}" = true ]; then
  echo ""
  info "Skipping backend build and start (--frontend-only flag set)."

  if [ "${NO_DEV_SERVER}" = false ]; then
    # Start Vite dev server even in frontend-only mode
    info "Starting Vite dev server on http://localhost:3000 ..."
    cd "${FRONTEND_DIR}"
    npx vite --port 3000 --host &
    FRONTEND_PID=$!
    cd "${REPO_ROOT}"
    sleep 3

    printf '%b============================================%b\n' "${GREEN}" "${NC}"
    printf '%b SplendidCRM Frontend Ready!%b\n' "${GREEN}" "${NC}"
    printf '%b============================================%b\n' "${GREEN}" "${NC}"
    echo ""
    echo "  Frontend:  http://localhost:3000 (PID: ${FRONTEND_PID})"
    echo "  Build:     ${FRONTEND_DIR}/dist/"
    echo "  Config:    ${FRONTEND_DIR}/public/config.json"
    echo ""
    echo "  Press Ctrl-C to stop."
    printf '%b============================================%b\n' "${GREEN}" "${NC}"

    # Wait for Ctrl-C
    wait "${FRONTEND_PID}" 2>/dev/null || true
  else
    printf '%b============================================%b\n' "${GREEN}" "${NC}"
    printf '%b SplendidCRM Frontend Build Complete!%b\n' "${GREEN}" "${NC}"
    printf '%b============================================%b\n' "${GREEN}" "${NC}"
    echo ""
    echo "  Build:   ${FRONTEND_DIR}/dist/"
    echo "  Config:  ${FRONTEND_DIR}/public/config.json"
    echo ""
    echo "  To start dev server: cd ${FRONTEND_DIR} && npm run dev"
    echo "  To preview build:    cd ${FRONTEND_DIR} && npm run preview"
    printf '%b============================================%b\n' "${GREEN}" "${NC}"
  fi

  exit 0
fi

echo ""
printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"
info "Phase 4: Building Backend (ASP.NET Core 10)"
printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"

BACKEND_PROJECT="src/SplendidCRM.Web"
if [ ! -d "${BACKEND_PROJECT}" ]; then
  # Fallback to SplendidCRM directory layout
  BACKEND_PROJECT="SplendidCRM"
fi

cd "${BACKEND_PROJECT}"

# ---- dotnet restore ----
info "Restoring backend NuGet packages..."
if dotnet restore; then
  success "Backend NuGet packages restored."
else
  warn "dotnet restore encountered issues. Build may still succeed with cached packages."
fi

# ---- dotnet build ----
info "Building backend (Release configuration)..."
if dotnet build --configuration Release --no-restore; then
  success "Backend build complete."
else
  warn "Backend build encountered warnings. Attempting to continue..."
fi

cd "${REPO_ROOT}"

# =============================================================================
# Phase 5: Start Backend (Background)
# =============================================================================

echo ""
printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"
info "Phase 5: Starting Backend Server"
printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"

# Set required environment variables for the backend
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://0.0.0.0:5000}"
export CORS_ORIGINS="${CORS_ORIGINS:-http://localhost:3000}"
export SESSION_PROVIDER="${SESSION_PROVIDER:-SqlServer}"
export AUTH_MODE="${AUTH_MODE:-Forms}"
export SPLENDID_JOB_SERVER="${SPLENDID_JOB_SERVER:-$(hostname)}"

# SESSION_CONNECTION defaults to the same connection string as the main DB
if [ -z "${SESSION_CONNECTION:-}" ] && [ -n "${ConnectionStrings__SplendidCRM:-}" ]; then
  export SESSION_CONNECTION="${ConnectionStrings__SplendidCRM}"
fi

if [ -z "${ConnectionStrings__SplendidCRM:-}" ]; then
  warn "ConnectionStrings__SplendidCRM is not set. Backend may fail to connect to SQL Server."
  warn "Set it via: export ConnectionStrings__SplendidCRM=\"Server=localhost;Database=SplendidCRM;User Id=sa;Password=YourPassword;TrustServerCertificate=True;\""
fi

success "Backend environment configured:"
info "  ASPNETCORE_ENVIRONMENT = ${ASPNETCORE_ENVIRONMENT}"
info "  ASPNETCORE_URLS        = ${ASPNETCORE_URLS}"
info "  CORS_ORIGINS           = ${CORS_ORIGINS}"
info "  SESSION_PROVIDER       = ${SESSION_PROVIDER}"
info "  AUTH_MODE              = ${AUTH_MODE}"

# Start backend in background
cd "${BACKEND_PROJECT}"
info "Starting backend on http://localhost:5000 ..."
dotnet run --configuration Release --no-build --urls "${ASPNETCORE_URLS}" > /tmp/splendid_backend.log 2>&1 &
BACKEND_PID=$!
cd "${REPO_ROOT}"

info "Backend starting (PID: ${BACKEND_PID}, logs: /tmp/splendid_backend.log)"

# Wait for backend health check (timeout 30 seconds)
HEALTH_URL="http://localhost:5000/api/health"
MAX_WAIT=30
ELAPSED=0

info "Waiting for backend health check at ${HEALTH_URL} ..."

while [ "${ELAPSED}" -lt "${MAX_WAIT}" ]; do
  if curl -sf "${HEALTH_URL}" >/dev/null 2>&1; then
    echo ""
    success "Backend health check passed! (HTTP 200)"
    break
  fi

  # Check if backend process is still alive
  if ! kill -0 "${BACKEND_PID}" 2>/dev/null; then
    echo ""
    warn "Backend process (PID: ${BACKEND_PID}) exited unexpectedly."
    warn "Check logs: tail -50 /tmp/splendid_backend.log"
    BACKEND_PID=""
    break
  fi

  printf "."
  sleep 2
  ELAPSED=$((ELAPSED + 2))
done

if [ "${ELAPSED}" -ge "${MAX_WAIT}" ]; then
  echo ""
  warn "Backend health check did not respond within ${MAX_WAIT}s."
  warn "Check logs: tail -50 /tmp/splendid_backend.log"
fi

echo ""

# =============================================================================
# Phase 6: Start Frontend Dev Server (Background)
# =============================================================================

if [ "${NO_DEV_SERVER}" = false ]; then
  printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"
  info "Phase 6: Starting Frontend Dev Server"
  printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"

  cd "${FRONTEND_DIR}"
  npx vite --port 3000 --host > /tmp/splendid_frontend.log 2>&1 &
  FRONTEND_PID=$!
  cd "${REPO_ROOT}"

  info "Vite dev server starting (PID: ${FRONTEND_PID}, logs: /tmp/splendid_frontend.log)"

  # Wait for frontend to respond (timeout 15 seconds)
  FE_URL="http://localhost:3000"
  FE_MAX_WAIT=15
  FE_ELAPSED=0

  info "Waiting for frontend at ${FE_URL} ..."

  while [ "${FE_ELAPSED}" -lt "${FE_MAX_WAIT}" ]; do
    if curl -sf "${FE_URL}" >/dev/null 2>&1; then
      echo ""
      success "Frontend dev server is ready! (HTTP 200)"
      break
    fi

    # Check if frontend process is still alive
    if ! kill -0 "${FRONTEND_PID}" 2>/dev/null; then
      echo ""
      warn "Frontend process (PID: ${FRONTEND_PID}) exited unexpectedly."
      warn "Check logs: tail -50 /tmp/splendid_frontend.log"
      FRONTEND_PID=""
      break
    fi

    printf "."
    sleep 1
    FE_ELAPSED=$((FE_ELAPSED + 1))
  done

  if [ "${FE_ELAPSED}" -ge "${FE_MAX_WAIT}" ]; then
    echo ""
    warn "Frontend did not respond within ${FE_MAX_WAIT}s."
    warn "Check logs: tail -50 /tmp/splendid_frontend.log"
  fi

  echo ""
fi

# =============================================================================
# Phase 7: Health Verification Summary (All Services)
# =============================================================================

printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"
info "Phase 7: Service Health Verification"
printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"

ALL_HEALTHY=true

# ---- SQL Server ----
if [ "${SKIP_SQL}" = false ]; then
  if ${DOCKER_CMD} ps --format '{{.Names}}' 2>/dev/null | grep -q "^${SQL_CONTAINER}$"; then
    success "[SQL Server]  Running on localhost:1433 (container: ${SQL_CONTAINER})"
  else
    printf "${RED}[FAIL]${NC} [SQL Server]  Not running\n"
    ALL_HEALTHY=false
  fi
else
  info "[SQL Server]  Skipped (--skip-sql)"
fi

# ---- Backend ----
if [ -n "${BACKEND_PID}" ] && kill -0 "${BACKEND_PID}" 2>/dev/null; then
  if curl -sf "http://localhost:5000/api/health" >/dev/null 2>&1; then
    success "[Backend]     Running on http://localhost:5000 (PID: ${BACKEND_PID})"
  else
    printf "${YELLOW}[WARN]${NC} [Backend]     Process running but health check failed (PID: ${BACKEND_PID})\n"
    ALL_HEALTHY=false
  fi
else
  printf "${RED}[FAIL]${NC} [Backend]     Not running\n"
  ALL_HEALTHY=false
fi

# ---- Frontend ----
if [ "${NO_DEV_SERVER}" = false ]; then
  if [ -n "${FRONTEND_PID}" ] && kill -0 "${FRONTEND_PID}" 2>/dev/null; then
    if curl -sf "http://localhost:3000" >/dev/null 2>&1; then
      success "[Frontend]    Running on http://localhost:3000 (PID: ${FRONTEND_PID})"
    else
      printf "${YELLOW}[WARN]${NC} [Frontend]    Process running but not responding (PID: ${FRONTEND_PID})\n"
      ALL_HEALTHY=false
    fi
  else
    printf "${RED}[FAIL]${NC} [Frontend]    Not running\n"
    ALL_HEALTHY=false
  fi
else
  info "[Frontend]    Dev server not started (--no-dev-server)"
fi

echo ""

# =============================================================================
# Phase 8: Final Summary
# =============================================================================

if [ "${ALL_HEALTHY}" = true ]; then
  printf '%b============================================%b\n' "${GREEN}" "${NC}"
  printf '%b SplendidCRM Full Stack Running!%b\n' "${GREEN}" "${NC}"
  printf '%b============================================%b\n' "${GREEN}" "${NC}"
else
  printf '%b============================================%b\n' "${YELLOW}" "${NC}"
  printf '%b SplendidCRM Started (with warnings)%b\n' "${YELLOW}" "${NC}"
  printf '%b============================================%b\n' "${YELLOW}" "${NC}"
fi

echo ""
echo "  Services:"
[ "${SKIP_SQL}" = false ] && echo "    SQL Server:  localhost:1433 (Docker: ${SQL_CONTAINER})"
echo "    Backend:     http://localhost:5000"
[ "${NO_DEV_SERVER}" = false ] && echo "    Frontend:    http://localhost:3000"
echo "    Build:       ${FRONTEND_DIR}/dist/"
echo ""
echo "  SignalR Hubs:"
echo "    http://localhost:5000/hubs/chat"
echo "    http://localhost:5000/hubs/twilio"
echo "    http://localhost:5000/hubs/phoneburner"
echo ""
echo "  Runtime Config: ${FRONTEND_DIR}/public/config.json"
echo "  Backend Logs:   /tmp/splendid_backend.log"
[ "${NO_DEV_SERVER}" = false ] && echo "  Frontend Logs:  /tmp/splendid_frontend.log"
echo ""
echo "  Press Ctrl-C to stop all services."
printf '%b============================================%b\n' "${GREEN}" "${NC}"

# Keep the script alive so services stay running.
# Ctrl-C triggers the trap → cleanup() → graceful shutdown.
if [ "${NO_DEV_SERVER}" = false ] && [ -n "${FRONTEND_PID}" ]; then
  wait "${FRONTEND_PID}" 2>/dev/null || true
elif [ -n "${BACKEND_PID}" ]; then
  wait "${BACKEND_PID}" 2>/dev/null || true
fi

exit 0
