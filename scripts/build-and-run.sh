#!/usr/bin/env bash
# =============================================================================
# SplendidCRM Full-Stack Build & Run Script
# =============================================================================
#
# Purpose:  Automated local development setup for SplendidCRM.
#           Checks prerequisites, installs dependencies, builds the React 19 /
#           Vite frontend, builds the ASP.NET Core 10 backend, starts the
#           backend server, and verifies health.
#
# Usage:    ./scripts/build-and-run.sh [OPTIONS]
#           bash scripts/build-and-run.sh [OPTIONS]
#
# Options:
#   --frontend-only   Build only the React frontend; skip backend build/start.
#   --skip-install    Skip `npm install` (use when dependencies are already
#                     installed).
#   --help            Print this usage information and exit.
#
# Prerequisites:
#   - Node.js 20 LTS or higher
#   - npm (bundled with Node.js)
#   - .NET 10 SDK or higher (unless --frontend-only is used)
#   - SQL Server accessible on localhost:1433 (optional; warning only)
#
# Note:     This script MUST be run from the repository root directory
#           (the directory that contains the SplendidCRM/ folder).
#
# Architecture:
#   Frontend — React 19 + Vite 6.x  → builds to SplendidCRM/React/dist/
#   Backend  — ASP.NET Core 10      → runs on http://localhost:5000
#   Dev Server — Vite dev server     → http://localhost:3000 (proxies API)
#   Runtime Config — SplendidCRM/React/public/config.json (API_BASE_URL)
#
# =============================================================================

set -euo pipefail

# =============================================================================
# Color Constants and Utility Functions
# =============================================================================

# Terminal color codes for readable output.
# These are ANSI escape sequences supported by all modern terminals.
readonly RED='\033[0;31m'
readonly GREEN='\033[0;32m'
readonly YELLOW='\033[0;33m'
readonly BLUE='\033[0;34m'
readonly NC='\033[0m' # No Color — resets terminal color

# info: Prints a blue informational message to stdout.
# Usage: info "Installing dependencies..."
info() {
  printf "${BLUE}[INFO]${NC} %s\n" "$1"
}

# success: Prints a green success message to stdout.
# Usage: success "Build complete!"
success() {
  printf "${GREEN}[OK]${NC}   %s\n" "$1"
}

# warn: Prints a yellow warning message to stderr.
# Warnings are non-fatal and do not stop script execution.
# Usage: warn "SQL Server not detected."
warn() {
  printf "${YELLOW}[WARN]${NC} %s\n" "$1" >&2
}

# error: Prints a red error message to stderr and exits with code 1.
# This function NEVER returns — it terminates the script immediately.
# Usage: error "Node.js 20 is required."
error() {
  printf "${RED}[ERROR]${NC} %s\n" "$1" >&2
  exit 1
}

# check_command: Verifies that a command-line tool is available on PATH.
# If the command is not found, prints an error and exits.
# Usage: check_command "node" "Node.js"
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

# Default flag values — both false means full-stack build.
FRONTEND_ONLY=false
SKIP_INSTALL=false

# Parse command-line arguments.
# Supports: --frontend-only, --skip-install, --help
# Unknown arguments trigger an error with usage hint.
for arg in "$@"; do
  case "$arg" in
    --frontend-only)
      FRONTEND_ONLY=true
      ;;
    --skip-install)
      SKIP_INSTALL=true
      ;;
    --help)
      # Print the header comment block as usage information and exit cleanly.
      cat << 'HELPEOF'
SplendidCRM Full-Stack Build & Run Script

Usage: ./scripts/build-and-run.sh [OPTIONS]
       bash scripts/build-and-run.sh [OPTIONS]

Options:
  --frontend-only   Build only the React 19 frontend; skip backend
                    build, start, and health check. Useful when you only
                    need to rebuild the Vite production bundle.

  --skip-install    Skip the `npm install` step. Use this when frontend
                    dependencies are already installed to save time.

  --help            Show this help message and exit.

Prerequisites:
  - Node.js 20 LTS or higher    (required)
  - npm                          (required, bundled with Node.js)
  - .NET 10 SDK or higher        (required for backend; skipped with --frontend-only)
  - SQL Server on localhost:1433  (optional; warning only if unreachable)

This script must be run from the repository root directory (the directory
that contains the SplendidCRM/ folder).

Workflow:
  1. Checks all prerequisites (Node 20+, npm, .NET 10 SDK)
  2. Installs frontend dependencies (npm install)
  3. Builds frontend with Vite (npm run build)
  4. Restores and builds backend (dotnet restore && dotnet build)
  5. Starts backend on http://localhost:5000
  6. Verifies backend health via GET /api/health
  7. Prints summary with next steps

Runtime Configuration:
  The built frontend reads API_BASE_URL from SplendidCRM/React/public/config.json
  at startup. No build-time environment variables are baked into the bundle.
  Edit config.json to point to a different backend server.

Examples:
  # Full-stack build and run
  ./scripts/build-and-run.sh

  # Frontend only (skip backend)
  ./scripts/build-and-run.sh --frontend-only

  # Full-stack, skip npm install (deps already present)
  ./scripts/build-and-run.sh --skip-install

  # Combine flags
  ./scripts/build-and-run.sh --frontend-only --skip-install
HELPEOF
      exit 0
      ;;
    *)
      error "Unknown option: ${arg}. Use --help for usage information."
      ;;
  esac
done

# =============================================================================
# Cleanup Function
# =============================================================================

# BACKEND_PID is set later when the backend is started in background.
# It is intentionally unset here so cleanup() can check safely.
BACKEND_PID=""

# cleanup: Stops the backend server if it was started by this script.
# This function is NOT wired to the EXIT trap by default because the user
# expects the backend to remain running after the script completes.
# It is available for manual invocation or can be wired to a signal trap
# for error scenarios.
# shellcheck disable=SC2317  # Function is reachable via trap (ERR/INT/TERM)
cleanup() {
  if [ -n "${BACKEND_PID:-}" ]; then
    info "Stopping backend server (PID: ${BACKEND_PID})..."
    kill "${BACKEND_PID}" 2>/dev/null || true
    wait "${BACKEND_PID}" 2>/dev/null || true
    BACKEND_PID=""
  fi
}

# Wire cleanup to ERR signal so the backend is killed if the script fails
# after starting it. INT and TERM are also handled for Ctrl-C graceful exit.
trap cleanup ERR INT TERM

# =============================================================================
# Phase 1: Prerequisite Checks
# =============================================================================

echo ""
printf '%b============================================%b\n' "${BLUE}" "${NC}"
printf '%b SplendidCRM Full-Stack Build & Run%b\n' "${BLUE}" "${NC}"
printf '%b============================================%b\n' "${BLUE}" "${NC}"
echo ""

info "Checking prerequisites..."

# ---- Node.js 20 LTS ----
check_command "node" "Node.js"
NODE_VERSION_RAW=$(node --version)
# Extract major version number from "vXX.YY.ZZ" format.
NODE_MAJOR=$(echo "${NODE_VERSION_RAW}" | sed -E 's/^v([0-9]+)\..*/\1/')
if [ "${NODE_MAJOR}" -lt 20 ]; then
  error "Node.js 20 LTS or higher is required. Found: ${NODE_VERSION_RAW}"
fi
success "Node.js ${NODE_VERSION_RAW} detected (>= 20 required)"

# ---- npm ----
check_command "npm" "npm"
NPM_VERSION_RAW=$(npm --version)
success "npm v${NPM_VERSION_RAW} detected"

# ---- .NET 10 SDK (skip check if --frontend-only) ----
if [ "${FRONTEND_ONLY}" = false ]; then
  check_command "dotnet" ".NET SDK"
  DOTNET_VERSION_RAW=$(dotnet --version)
  # Extract major version number from "XX.YY.ZZZ" format.
  DOTNET_MAJOR=$(echo "${DOTNET_VERSION_RAW}" | sed -E 's/^([0-9]+)\..*/\1/')
  if [ "${DOTNET_MAJOR}" -lt 10 ]; then
    error ".NET 10 SDK or higher is required. Found: ${DOTNET_VERSION_RAW}"
  fi
  success ".NET SDK ${DOTNET_VERSION_RAW} detected (>= 10 required)"
fi

# ---- SQL Server Connectivity (optional — warning only) ----
if [ "${FRONTEND_ONLY}" = false ]; then
  SQL_DETECTED=false
  if command -v nc &>/dev/null; then
    if nc -z localhost 1433 2>/dev/null; then
      SQL_DETECTED=true
    fi
  elif command -v ss &>/dev/null; then
    if ss -tln 2>/dev/null | grep -q ':1433'; then
      SQL_DETECTED=true
    fi
  fi

  if [ "${SQL_DETECTED}" = true ]; then
    success "SQL Server detected on localhost:1433"
  else
    warn "SQL Server not detected on localhost:1433. Ensure it is running before starting the backend."
    warn "The backend requires a SQL Server connection to function properly."
  fi
fi

echo ""
success "All prerequisites satisfied."
echo ""

# =============================================================================
# Phase 2: Frontend Build (React 19 + Vite)
# =============================================================================

printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"
info "Building Frontend (React 19 + Vite)"
printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"

FRONTEND_DIR="SplendidCRM/React"
if [ ! -d "${FRONTEND_DIR}" ]; then
  error "Frontend directory not found: ${FRONTEND_DIR}. Are you running from the repository root?"
fi

# Save original directory for reliable navigation back.
REPO_ROOT=$(pwd)

cd "${FRONTEND_DIR}"

# ---- npm install (unless --skip-install) ----
if [ "${SKIP_INSTALL}" = false ]; then
  info "Installing frontend dependencies..."
  if npm install; then
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
info "Building frontend with Vite..."
if npm run build; then
  success "Frontend build complete. Output in ${FRONTEND_DIR}/dist/"
else
  error "Frontend build failed. Check Vite output above."
fi

cd "${REPO_ROOT}"

# =============================================================================
# Phase 3: Backend Build (ASP.NET Core 10) — skipped with --frontend-only
# =============================================================================

if [ "${FRONTEND_ONLY}" = true ]; then
  echo ""
  info "Skipping backend build and start (--frontend-only flag set)."
  echo ""

  # Print frontend-only summary and exit.
  printf '%b============================================%b\n' "${GREEN}" "${NC}"
  printf '%b SplendidCRM Frontend Build Complete!%b\n' "${GREEN}" "${NC}"
  printf '%b============================================%b\n' "${GREEN}" "${NC}"
  echo ""
  echo "  Frontend:  Built -> ${FRONTEND_DIR}/dist/"
  echo ""
  echo "  Next Steps:"
  echo "  1. Start Vite dev server:"
  echo "     cd ${FRONTEND_DIR} && npm run dev"
  echo ""
  echo "  2. Open browser to http://localhost:3000"
  echo "     (Vite dev server with API proxy to backend on :5000)"
  echo ""
  echo "  3. Or serve the production build:"
  echo "     cd ${FRONTEND_DIR} && npm run preview"
  echo ""
  echo "  Runtime Configuration:"
  echo "  - Edit ${FRONTEND_DIR}/public/config.json to change API_BASE_URL"
  echo "  - Default: http://localhost:5000"
  echo ""
  printf '%b============================================%b\n' "${GREEN}" "${NC}"
  exit 0
fi

echo ""
printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"
info "Building Backend (ASP.NET Core 10)"
printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"

BACKEND_DIR="SplendidCRM"
if [ ! -d "${BACKEND_DIR}" ]; then
  error "Backend directory not found: ${BACKEND_DIR}. Are you running from the repository root?"
fi

cd "${BACKEND_DIR}"

# ---- dotnet restore ----
info "Restoring backend NuGet packages..."
if dotnet restore; then
  success "Backend NuGet packages restored."
else
  warn "dotnet restore failed. The backend may not have a .csproj or packages may be unavailable."
  warn "Continuing — the build step may still succeed if packages are cached."
fi

# ---- dotnet build ----
info "Building backend..."
if dotnet build --configuration Release --no-restore; then
  success "Backend build complete."
else
  warn "Backend build failed. Check dotnet output above."
  warn "Continuing to attempt backend startup — a previous build may still be runnable."
fi

cd "${REPO_ROOT}"

# =============================================================================
# Phase 4: Backend Start (Background)
# =============================================================================

echo ""
printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"
info "Starting Backend Server"
printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"

# Check for ConnectionStrings__SplendidCRM environment variable.
# This is the connection string the backend uses to connect to SQL Server.
if [ -z "${ConnectionStrings__SplendidCRM:-}" ]; then
  warn "ConnectionStrings__SplendidCRM environment variable is not set."
  warn "The backend will use its default connection string (if configured in appsettings.json)."
  warn "To set it, run:"
  warn '  export ConnectionStrings__SplendidCRM="Server=localhost;Database=SplendidCRM;User Id=sa;Password=YourPassword;TrustServerCertificate=true;"'
else
  success "ConnectionStrings__SplendidCRM is set."
fi

# Start the ASP.NET Core backend in the background.
# The backend listens on port 5000 (Kestrel), matching the Vite dev proxy target.
cd "${BACKEND_DIR}"
info "Starting backend on http://localhost:5000 ..."

dotnet run --urls "http://localhost:5000" &
BACKEND_PID=$!

cd "${REPO_ROOT}"

info "Backend starting on http://localhost:5000 (PID: ${BACKEND_PID})"

# =============================================================================
# Phase 5: Health Check Verification
# =============================================================================

echo ""
printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"
info "Verifying Backend Health"
printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"

# Wait for the backend to respond to health check requests.
# The ASP.NET Core backend exposes GET /api/health → HTTP 200 when ready.
# Maximum wait: 30 seconds, polling every 2 seconds.
HEALTH_URL="http://localhost:5000/api/health"
MAX_WAIT=30
ELAPSED=0

info "Waiting for backend health check at ${HEALTH_URL} ..."

while [ "${ELAPSED}" -lt "${MAX_WAIT}" ]; do
  if curl -sf "${HEALTH_URL}" >/dev/null 2>&1; then
    echo ""
    success "Backend health check passed! (HTTP 200 from ${HEALTH_URL})"
    break
  fi

  # Check if the backend process is still alive.
  if ! kill -0 "${BACKEND_PID}" 2>/dev/null; then
    echo ""
    warn "Backend process (PID: ${BACKEND_PID}) is no longer running."
    warn "Check backend logs for errors. The backend may have crashed during startup."
    BACKEND_PID=""
    break
  fi

  printf "."
  sleep 2
  ELAPSED=$((ELAPSED + 2))
done

if [ "${ELAPSED}" -ge "${MAX_WAIT}" ]; then
  echo ""
  warn "Backend health check did not respond within ${MAX_WAIT}s. It may still be starting."
  warn "You can manually verify: curl -sf ${HEALTH_URL}"
fi

# =============================================================================
# Phase 6: Summary and Next Steps
# =============================================================================

echo ""
printf '%b============================================%b\n' "${GREEN}" "${NC}"
printf '%b SplendidCRM Full-Stack Build Complete!%b\n' "${GREEN}" "${NC}"
printf '%b============================================%b\n' "${GREEN}" "${NC}"
echo ""
echo "  Frontend:  Built -> ${FRONTEND_DIR}/dist/"

if [ -n "${BACKEND_PID}" ]; then
  echo "  Backend:   Running on http://localhost:5000 (PID: ${BACKEND_PID})"
else
  echo "  Backend:   Not running (see warnings above)"
fi

echo ""
echo "  Next Steps:"
echo "  1. Start Vite dev server:"
echo "     cd ${FRONTEND_DIR} && npm run dev"
echo ""
echo "  2. Open browser to http://localhost:3000"
echo "     (Vite dev server with API proxy to backend on :5000)"
echo ""
echo "  3. Or serve the production build:"
echo "     cd ${FRONTEND_DIR} && npm run preview"
echo ""
echo "  Runtime Configuration:"
echo "  - Edit ${FRONTEND_DIR}/public/config.json to change API_BASE_URL"
echo "  - Default: http://localhost:5000"
echo "  - The same build artifact works with any API_BASE_URL value"
echo "  - No rebuild needed when changing the backend URL"
echo ""

if [ -n "${BACKEND_PID}" ]; then
  echo "  To stop the backend:"
  echo "    kill ${BACKEND_PID}"
  echo ""
fi

echo "  SignalR Hub Endpoints (configured in backend):"
echo "    - http://localhost:5000/hubs/chat"
echo "    - http://localhost:5000/hubs/twilio"
echo "    - http://localhost:5000/hubs/phoneburner"
echo ""
printf '%b============================================%b\n' "${GREEN}" "${NC}"

exit 0
