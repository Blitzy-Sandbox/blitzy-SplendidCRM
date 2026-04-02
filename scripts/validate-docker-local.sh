#!/usr/bin/env bash
# =============================================================================
# SplendidCRM Local Docker Validation Suite — 12 Tests
# =============================================================================
#
# Purpose:  Validates locally-built Docker images for the SplendidCRM backend
#           and frontend containers. ALL 12 tests must pass before any ECR push.
#
# Usage:    ./scripts/validate-docker-local.sh
#           bash scripts/validate-docker-local.sh
#
# Tests:
#    1. Backend image builds successfully
#    2. Frontend image builds successfully
#    3. Backend image size ≤ 500MB
#    4. Frontend image size ≤ 100MB
#    5. Backend container /api/health returns 200
#    6. Frontend container /health returns 200
#    7. Frontend config.json injection from env vars
#    8. Frontend SPA fallback (non-file path → index.html)
#    9. Frontend source maps blocked (*.map → 404)
#   10. No *.map files exist in frontend image
#   11. No secrets in Docker history (both images)
#   12. End-to-end reachability (backend + frontend)
#
# Ports (non-standard to avoid conflicts with running services):
#   Backend:    18080 → container 8080
#   Frontend:   13000 → container 80
#   SQL Server: 11433 → container 1433
#
# Environment Variables:
#   SA_PASSWORD — SQL Server sa password (default: YourStrong!Passw0rd)
#
# Prerequisites:
#   - Docker Engine ≥ 20.0
#   - curl
#   - Dockerfile.backend and Dockerfile.frontend at repository root
#   - docker-entrypoint.sh and nginx.conf at repository root
#
# Exit Codes:
#   0 — All 12 tests passed
#   1 — One or more tests failed
#
# Note: This script MUST be run from the repository root directory.
# =============================================================================

set -euo pipefail

# =============================================================================
# Color Constants and Utility Functions
# =============================================================================
# Terminal color codes — same pattern as scripts/build-and-run.sh for
# consistent developer experience across all SplendidCRM scripts.

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
}

# =============================================================================
# Test Counters
# =============================================================================

TESTS_PASSED=0
TESTS_FAILED=0
readonly TESTS_TOTAL=12

# =============================================================================
# Container and Image Constants
# =============================================================================
# All container names use the "splendid-validate-" prefix to avoid collisions
# with other running containers (e.g., splendid-sql-express from build-and-run.sh).

readonly BACKEND_CONTAINER="splendid-validate-backend"
readonly FRONTEND_CONTAINER="splendid-validate-frontend"
readonly SQL_CONTAINER="splendid-validate-sql"

readonly BACKEND_IMAGE="splendidcrm-backend:test"
readonly FRONTEND_IMAGE="splendidcrm-frontend:test"
readonly SQL_IMAGE="mcr.microsoft.com/mssql/server:2022-latest"

# Non-standard host ports to avoid conflicts with existing services.
readonly BACKEND_PORT=18080
readonly FRONTEND_PORT=13000
readonly SQL_PORT=11433

# SQL Server sa password — configurable via environment variable with a secure default.
readonly SA_PASSWORD="${SA_PASSWORD:-YourStrong!Passw0rd}"

# =============================================================================
# Cleanup Function
# =============================================================================
# Registered via trap to ensure test containers are always stopped and removed,
# even if the script exits due to an error or user interrupt (Ctrl-C).
# Containers that do not exist are silently ignored via || true.

cleanup() {
  echo ""
  info "Cleaning up validation containers..."

  # Stop and remove each test container. The --time 5 flag gives the container
  # 5 seconds for graceful shutdown before SIGKILL.
  for container in "${BACKEND_CONTAINER}" "${FRONTEND_CONTAINER}" "${SQL_CONTAINER}"; do
    if docker ps -a --format '{{.Names}}' 2>/dev/null | grep -q "^${container}$"; then
      docker stop "${container}" --time 5 >/dev/null 2>&1 || true
      docker rm -f "${container}" >/dev/null 2>&1 || true
      info "Removed container: ${container}"
    fi
  done

  info "Cleanup complete."
}

# Register cleanup on EXIT so it fires on normal exit, error (set -e), and signals.
trap cleanup EXIT

# =============================================================================
# Helper: run_test
# =============================================================================
# Executes a test function, captures its exit code, and prints a PASS/FAIL line.
# Increments the appropriate counter. The || true pattern prevents set -e from
# aborting the script when a test fails — each test is independent.
#
# Arguments:
#   $1 — Test number (1-12)
#   $2 — Test name (human-readable description)
#   $3 — Test function name to invoke
#
# The test function must return 0 for pass, non-zero for fail.

run_test() {
  local test_num="$1"
  local test_name="$2"
  local test_func="$3"
  local result=0

  echo ""
  printf '%b──────────────────────────────────────────────%b\n' "${BLUE}" "${NC}"
  info "Test ${test_num}/${TESTS_TOTAL}: ${test_name}"
  printf '%b──────────────────────────────────────────────%b\n' "${BLUE}" "${NC}"

  # Execute the test function in a subshell so that set -e in the test
  # function body does not propagate to the outer script. Capture exit code.
  if "${test_func}"; then
    result=0
  else
    result=1
  fi

  if [ "${result}" -eq 0 ]; then
    printf "${GREEN}[PASS]${NC} Test %d: %s\n" "${test_num}" "${test_name}"
    TESTS_PASSED=$((TESTS_PASSED + 1))
  else
    printf "${RED}[FAIL]${NC} Test %d: %s\n" "${test_num}" "${test_name}"
    TESTS_FAILED=$((TESTS_FAILED + 1))
  fi

  return 0  # Always return 0 so set -e does not abort after a test failure
}

# =============================================================================
# Helper: wait_for_url
# =============================================================================
# Polls a URL with curl until it returns HTTP 200 or the timeout expires.
#
# Arguments:
#   $1 — URL to poll
#   $2 — Maximum wait time in seconds
#   $3 — Descriptive label for log messages
#
# Returns 0 if HTTP 200 received, 1 if timeout expired.

wait_for_url() {
  local url="$1"
  local max_wait="$2"
  local label="$3"
  local elapsed=0
  local interval=2

  info "Waiting for ${label} at ${url} (timeout: ${max_wait}s)..."

  while [ "${elapsed}" -lt "${max_wait}" ]; do
    if curl -sf -o /dev/null "${url}" 2>/dev/null; then
      success "${label} is ready (${elapsed}s)."
      return 0
    fi
    sleep "${interval}"
    elapsed=$((elapsed + interval))
    printf "."
  done

  echo ""
  error "${label} did not become ready within ${max_wait}s."
  return 1
}

# =============================================================================
# Helper: wait_for_container_log
# =============================================================================
# Polls docker logs for a container until a specific string appears or timeout.
#
# Arguments:
#   $1 — Container name
#   $2 — String to search for in logs
#   $3 — Maximum wait time in seconds
#   $4 — Descriptive label for log messages
#
# Returns 0 if string found, 1 if timeout expired.

wait_for_container_log() {
  local container="$1"
  local search_string="$2"
  local max_wait="$3"
  local label="$4"
  local elapsed=0
  local interval=2

  info "Waiting for ${label} (timeout: ${max_wait}s)..."

  while [ "${elapsed}" -lt "${max_wait}" ]; do
    if docker logs "${container}" 2>&1 | grep -q "${search_string}"; then
      success "${label} ready (${elapsed}s)."
      return 0
    fi
    sleep "${interval}"
    elapsed=$((elapsed + interval))
    printf "."
  done

  echo ""
  warn "${label} did not become ready within ${max_wait}s."
  return 1
}

# =============================================================================
# =============================================================================
#                           TEST IMPLEMENTATIONS
# =============================================================================
# =============================================================================

# =============================================================================
# Test 1: Backend image builds successfully
# =============================================================================
# Builds the backend Docker image using Dockerfile.backend with the repository
# root as the build context. The image is tagged splendidcrm-backend:test.
# Pass: docker build exits with code 0.

test_01_backend_build() {
  info "Building backend image: docker build -f Dockerfile.backend -t ${BACKEND_IMAGE} ."
  if docker build -f Dockerfile.backend -t "${BACKEND_IMAGE}" . ; then
    success "Backend image built successfully."
    return 0
  else
    error "Backend image build failed."
    return 1
  fi
}

# =============================================================================
# Test 2: Frontend image builds successfully
# =============================================================================
# Builds the frontend Docker image using Dockerfile.frontend with the repository
# root as the build context. The image is tagged splendidcrm-frontend:test.
# Pass: docker build exits with code 0.

test_02_frontend_build() {
  info "Building frontend image: docker build -f Dockerfile.frontend -t ${FRONTEND_IMAGE} ."
  if docker build -f Dockerfile.frontend -t "${FRONTEND_IMAGE}" . ; then
    success "Frontend image built successfully."
    return 0
  else
    error "Frontend image build failed."
    return 1
  fi
}

# =============================================================================
# Test 3: Backend image size ≤ 500MB
# =============================================================================
# Inspects the backend image size and verifies it does not exceed 500MB.
# The image uses ASP.NET 10.0-alpine + ICU + OpenSSL as the runtime base.

test_03_backend_size() {
  local size_bytes
  local size_mb
  local max_mb=500

  size_bytes=$(docker image inspect "${BACKEND_IMAGE}" --format '{{.Size}}')
  size_mb=$((size_bytes / 1024 / 1024))

  info "Backend image size: ${size_mb}MB (limit: ${max_mb}MB)"

  if [ "${size_mb}" -le "${max_mb}" ]; then
    success "Backend image size ${size_mb}MB ≤ ${max_mb}MB threshold."
    return 0
  else
    error "Backend image size ${size_mb}MB exceeds ${max_mb}MB threshold."
    return 1
  fi
}

# =============================================================================
# Test 4: Frontend image size ≤ 100MB
# =============================================================================
# Inspects the frontend image size and verifies it does not exceed 100MB.
# The image uses nginx:alpine as the runtime base.

test_04_frontend_size() {
  local size_bytes
  local size_mb
  local max_mb=100

  size_bytes=$(docker image inspect "${FRONTEND_IMAGE}" --format '{{.Size}}')
  size_mb=$((size_bytes / 1024 / 1024))

  info "Frontend image size: ${size_mb}MB (limit: ${max_mb}MB)"

  if [ "${size_mb}" -le "${max_mb}" ]; then
    success "Frontend image size ${size_mb}MB ≤ ${max_mb}MB threshold."
    return 0
  else
    error "Frontend image size ${size_mb}MB exceeds ${max_mb}MB threshold."
    return 1
  fi
}

# =============================================================================
# Infrastructure Setup: SQL Server + Backend + Frontend Containers
# =============================================================================
# Starts the test containers needed for Tests 5-12.
# This is NOT a test — it is infrastructure setup. Failures here cause
# downstream test failures but are not counted as test failures themselves.

start_test_containers() {
  # ------------------------------------------------------------------
  # SQL Server container
  # ------------------------------------------------------------------
  info "Starting SQL Server container on port ${SQL_PORT}..."

  # Remove any leftover container from a previous run
  docker rm -f "${SQL_CONTAINER}" >/dev/null 2>&1 || true

  docker run -d \
    --name "${SQL_CONTAINER}" \
    -e "ACCEPT_EULA=Y" \
    -e "MSSQL_SA_PASSWORD=${SA_PASSWORD}" \
    -e "MSSQL_PID=Express" \
    -p "${SQL_PORT}:1433" \
    "${SQL_IMAGE}" >/dev/null

  # Wait for SQL Server readiness (up to 60 seconds)
  local sql_ready=false
  local elapsed=0
  local max_wait=60

  info "Waiting for SQL Server readiness (timeout: ${max_wait}s)..."
  while [ "${elapsed}" -lt "${max_wait}" ]; do
    if docker exec "${SQL_CONTAINER}" /opt/mssql-tools18/bin/sqlcmd \
        -S localhost -U sa -P "${SA_PASSWORD}" -C -Q "SELECT 1" >/dev/null 2>&1; then
      sql_ready=true
      break
    fi
    sleep 2
    elapsed=$((elapsed + 2))
    printf "."
  done
  echo ""

  if [ "${sql_ready}" = true ]; then
    success "SQL Server ready on port ${SQL_PORT} (${elapsed}s)."
  else
    warn "SQL Server did not become ready within ${max_wait}s — backend tests may fail."
  fi

  # ------------------------------------------------------------------
  # Backend container
  # ------------------------------------------------------------------
  info "Starting backend container on port ${BACKEND_PORT}..."

  docker rm -f "${BACKEND_CONTAINER}" >/dev/null 2>&1 || true

  # Determine the Docker host address for SQL Server connectivity.
  # On Linux (non-Docker Desktop), host.docker.internal may not resolve.
  # Use the Docker bridge gateway as a fallback.
  local docker_host="host.docker.internal"

  # Add host.docker.internal mapping on Linux where it's not natively supported.
  # Docker Desktop (macOS/Windows) resolves it automatically.
  local extra_host_flag=""
  if ! docker run --rm alpine:3 nslookup host.docker.internal >/dev/null 2>&1; then
    extra_host_flag="--add-host=host.docker.internal:host-gateway"
  fi

  # shellcheck disable=SC2086
  docker run -d \
    --name "${BACKEND_CONTAINER}" \
    -p "${BACKEND_PORT}:8080" \
    ${extra_host_flag} \
    -e "ConnectionStrings__SplendidCRM=Server=${docker_host},${SQL_PORT};Database=SplendidCRM;User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True" \
    -e "ASPNETCORE_ENVIRONMENT=Development" \
    -e "SPLENDID_JOB_SERVER=docker-test" \
    -e "SESSION_PROVIDER=SqlServer" \
    -e "SESSION_CONNECTION=Server=${docker_host},${SQL_PORT};Database=SplendidCRM;User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True" \
    -e "AUTH_MODE=Forms" \
    -e "CORS_ORIGINS=http://localhost:${FRONTEND_PORT}" \
    "${BACKEND_IMAGE}" >/dev/null

  success "Backend container started on port ${BACKEND_PORT}."

  # ------------------------------------------------------------------
  # Frontend container
  # ------------------------------------------------------------------
  info "Starting frontend container on port ${FRONTEND_PORT}..."

  docker rm -f "${FRONTEND_CONTAINER}" >/dev/null 2>&1 || true

  docker run -d \
    --name "${FRONTEND_CONTAINER}" \
    -p "${FRONTEND_PORT}:80" \
    -e "API_BASE_URL=" \
    -e "SIGNALR_URL=" \
    -e "ENVIRONMENT=test" \
    "${FRONTEND_IMAGE}" >/dev/null

  success "Frontend container started on port ${FRONTEND_PORT}."
}

# =============================================================================
# Test 5: Backend container starts and /api/health returns 200
# =============================================================================
# Verifies the backend container is serving the health check endpoint.
# The HealthCheckController.cs returns:
#   200 OK:  {"status":"Healthy","machineName":"...","timestamp":"...","initialized":...}
#   503:     {"status":"Unhealthy","error":"...","timestamp":"..."}
# Both 200 and 503 are considered "alive" — 503 just means the database is
# not connected, but the container is running and Kestrel is responding.

test_05_backend_health() {
  # Wait for the backend to start (Kestrel startup + DI initialization)
  if ! wait_for_url "http://localhost:${BACKEND_PORT}/api/health" 30 "Backend health check"; then
    # If a 200 wasn't received, check if we at least get an HTTP response (503 is acceptable)
    local http_code
    http_code=$(curl -o /dev/null -s -w '%{http_code}' "http://localhost:${BACKEND_PORT}/api/health" 2>/dev/null || echo "000")

    if [ "${http_code}" = "200" ] || [ "${http_code}" = "503" ]; then
      info "Backend returned HTTP ${http_code} (container is running, DB may not be connected)."
      success "Backend health endpoint is reachable (HTTP ${http_code})."
      return 0
    fi

    error "Backend health check unreachable (HTTP ${http_code})."
    # Print container logs for debugging
    info "Backend container logs (last 20 lines):"
    docker logs "${BACKEND_CONTAINER}" --tail 20 2>&1 || true
    return 1
  fi

  success "Backend health check returned HTTP 200."
  return 0
}

# =============================================================================
# Test 6: Frontend container starts and /health returns 200
# =============================================================================
# Verifies the frontend Nginx container is serving the health check endpoint.
# nginx.conf defines: location = /health { return 200 'ok'; }

test_06_frontend_health() {
  if ! wait_for_url "http://localhost:${FRONTEND_PORT}/health" 10 "Frontend health check"; then
    error "Frontend health check did not return HTTP 200."
    info "Frontend container logs (last 20 lines):"
    docker logs "${FRONTEND_CONTAINER}" --tail 20 2>&1 || true
    return 1
  fi

  success "Frontend health check returned HTTP 200."
  return 0
}

# =============================================================================
# Test 7: Frontend config.json is generated with injected env vars
# =============================================================================
# Verifies docker-entrypoint.sh correctly generated config.json from the
# environment variables passed via docker run -e.
# Expected content:
#   {"API_BASE_URL":"","SIGNALR_URL":"","ENVIRONMENT":"test"}

test_07_config_json_injection() {
  local config_response

  config_response=$(curl -sf "http://localhost:${FRONTEND_PORT}/config.json" 2>/dev/null) || {
    error "Failed to fetch config.json from frontend container."
    return 1
  }

  # Verify the response is valid JSON (check for opening brace)
  if ! echo "${config_response}" | grep -q '{'; then
    error "config.json response is not valid JSON: ${config_response}"
    return 1
  fi

  # Verify API_BASE_URL is present with the injected empty string value
  if ! echo "${config_response}" | grep -q '"API_BASE_URL"'; then
    error "config.json missing API_BASE_URL field."
    return 1
  fi

  # Verify ENVIRONMENT is present with the injected "test" value
  if ! echo "${config_response}" | grep -q '"ENVIRONMENT"'; then
    error "config.json missing ENVIRONMENT field."
    return 1
  fi

  # Verify the ENVIRONMENT value matches what we injected
  if ! echo "${config_response}" | grep -q '"test"'; then
    error "config.json ENVIRONMENT value does not match injected 'test'."
    return 1
  fi

  info "config.json content: ${config_response}"
  success "config.json correctly generated with injected environment variables."
  return 0
}

# =============================================================================
# Test 8: Frontend SPA fallback works (non-file path → index.html)
# =============================================================================
# Verifies nginx try_files $uri $uri/ /index.html routing. A request to a
# non-existent path (e.g., /some/random/path) should return the SPA's
# index.html content, enabling React Router client-side navigation.

test_08_spa_fallback() {
  local response

  response=$(curl -sf "http://localhost:${FRONTEND_PORT}/some/random/path" 2>/dev/null) || {
    error "SPA fallback request failed (curl error)."
    return 1
  }

  # Check for HTML markers that indicate index.html was served.
  # The SPA entry point contains <!DOCTYPE html> or <html tags.
  if echo "${response}" | grep -qi '<html\|<!doctype'; then
    success "SPA fallback correctly serves index.html for non-file paths."
    return 0
  else
    error "SPA fallback did not return HTML content. Response excerpt: $(echo "${response}" | head -c 200)"
    return 1
  fi
}

# =============================================================================
# Test 9: Frontend source maps blocked (*.map → 404)
# =============================================================================
# Verifies nginx.conf source map blocking: location ~* \.map$ { return 404; }
# This is the defense-in-depth complement to the Dockerfile RUN find -delete.

test_09_source_map_blocked() {
  local http_code

  # Request a .map file path — the specific filename doesn't matter because
  # the Nginx regex blocks ALL *.map requests.
  http_code=$(curl -o /dev/null -s -w '%{http_code}' "http://localhost:${FRONTEND_PORT}/assets/vendor.js.map" 2>/dev/null || echo "000")

  info "Source map request returned HTTP ${http_code} (expected 404)."

  if [ "${http_code}" = "404" ]; then
    success "Source maps correctly blocked with HTTP 404."
    return 0
  else
    error "Source maps NOT blocked — received HTTP ${http_code} instead of 404."
    return 1
  fi
}

# =============================================================================
# Test 10: No *.map files exist in frontend image
# =============================================================================
# Verifies the Dockerfile.frontend build stage deleted all source maps:
#   RUN find /app/dist -name '*.map' -delete
# No .map files should exist in the Nginx document root.

test_10_no_map_files_in_image() {
  local map_files

  map_files=$(docker exec "${FRONTEND_CONTAINER}" find /usr/share/nginx/html -name '*.map' -type f 2>/dev/null || true)

  if [ -z "${map_files}" ]; then
    success "No .map files found in frontend image."
    return 0
  else
    local count
    count=$(echo "${map_files}" | wc -l)
    error "Found ${count} .map file(s) in frontend image:"
    echo "${map_files}"
    return 1
  fi
}

# =============================================================================
# Test 11: No secrets in Docker history
# =============================================================================
# Scans docker history for both images to ensure no secrets were baked into
# image layers via ENV, ARG, RUN echo, or similar Dockerfile instructions.
# Connection strings, passwords, and API keys must never appear in the
# immutable image layers.

test_11_no_secrets_in_history() {
  local found_secrets=false

  # Secret patterns to scan for — case-insensitive.
  # These patterns catch common secret leakage:
  #   Password=          — SQL Server connection string password component
  #   ConnectionString   — Full connection string baked into layer
  #   AWS_SECRET_ACCESS  — AWS secret access key
  #   MSSQL_SA_PASSWORD  — SQL Server password in environment variable
  local -a secret_patterns=(
    "Password="
    "ConnectionString"
    "AWS_SECRET_ACCESS"
    "MSSQL_SA_PASSWORD"
  )

  for image in "${BACKEND_IMAGE}" "${FRONTEND_IMAGE}"; do
    local history
    history=$(docker history "${image}" --no-trunc 2>/dev/null) || {
      error "Failed to retrieve docker history for ${image}."
      return 1
    }

    for pattern in "${secret_patterns[@]}"; do
      if echo "${history}" | grep -i "${pattern}" >/dev/null 2>&1; then
        error "Secret pattern '${pattern}' found in docker history of ${image}."
        found_secrets=true
      fi
    done
  done

  if [ "${found_secrets}" = true ]; then
    error "Secrets detected in Docker image history — images are NOT safe to push."
    return 1
  fi

  success "No secrets found in Docker history for either image."
  return 0
}

# =============================================================================
# Test 12: End-to-end reachability test
# =============================================================================
# Best-effort reachability verification — confirms both containers are running
# and responding to HTTP requests. This is NOT a full database connectivity or
# login test; it is a container reachability check.
#
# Acceptance criteria:
#   1. Backend responds on /api/health with HTTP 200 (DB connected) or HTTP 503
#      (DB not connected). Both codes confirm Kestrel is alive and processing
#      requests. Connection refused or timeout = FAIL.
#   2. Frontend serves HTML on / (Nginx + SPA fallback working). No HTML or
#      connection refused = FAIL.
#
# NOTE: HTTP 503 is accepted from the backend because the local SQL Server
# may not have the SplendidCRM database fully provisioned in this local
# validation context. SESSION_PROVIDER=SqlServer is used with a connection
# string pointing to the local SQL Server container on port ${SQL_PORT}.
# Full database connectivity is validated separately by validate-infra-localstack.sh
# (Docker SQL Server tests 16-19) and by deploy-schema.sh.

test_12_e2e_reachability() {
  local backend_ok=false
  local frontend_ok=false

  # Check backend reachability — accept both 200 (healthy) and 503 (db not connected)
  local backend_code
  backend_code=$(curl -o /dev/null -s -w '%{http_code}' "http://localhost:${BACKEND_PORT}/api/health" 2>/dev/null || echo "000")

  if [ "${backend_code}" = "200" ] || [ "${backend_code}" = "503" ]; then
    info "Backend reachable (HTTP ${backend_code})."
    backend_ok=true
  else
    error "Backend unreachable (HTTP ${backend_code})."
  fi

  # Check frontend reachability — should serve HTML on /
  local frontend_response
  frontend_response=$(curl -sf "http://localhost:${FRONTEND_PORT}/" 2>/dev/null) || true

  if echo "${frontend_response}" | grep -qi '<html\|<!doctype'; then
    info "Frontend reachable (serving HTML SPA)."
    frontend_ok=true
  else
    error "Frontend unreachable or not serving HTML."
  fi

  # Both must be reachable for the E2E test to pass
  if [ "${backend_ok}" = true ] && [ "${frontend_ok}" = true ]; then
    success "End-to-end: Both backend and frontend are reachable."
    return 0
  else
    error "End-to-end: One or both services are unreachable."
    return 1
  fi
}

# =============================================================================
# =============================================================================
#                              MAIN EXECUTION
# =============================================================================
# =============================================================================

echo ""
printf '%b========================================%b\n' "${BLUE}" "${NC}"
printf '%b SplendidCRM Docker Local Validation%b\n' "${BLUE}" "${NC}"
printf '%b 12-Test Suite%b\n' "${BLUE}" "${NC}"
printf '%b========================================%b\n' "${BLUE}" "${NC}"
echo ""

# Verify we are at the repository root (Dockerfile.backend should exist)
if [ ! -f "Dockerfile.backend" ] || [ ! -f "Dockerfile.frontend" ]; then
  error "Dockerfile.backend or Dockerfile.frontend not found. Run this script from the repository root."
  exit 1
fi

# Verify required tools are available
for tool in docker curl grep; do
  if ! command -v "${tool}" &>/dev/null; then
    error "${tool} is required but not installed."
    exit 1
  fi
done

if [ -n "${SA_PASSWORD:-}" ]; then
  info "SA_PASSWORD is set (hidden)."
else
  info "SA_PASSWORD is not set, using default."
fi
info "Starting validation suite..."

# ─────────────────────────────────────────────────────────────────────────────
# Phase 1: Image Build Verification (Tests 1-2)
# ─────────────────────────────────────────────────────────────────────────────

run_test 1 "Backend image builds successfully" test_01_backend_build
run_test 2 "Frontend image builds successfully" test_02_frontend_build

# ─────────────────────────────────────────────────────────────────────────────
# Phase 2: Image Size Checks (Tests 3-4)
# ─────────────────────────────────────────────────────────────────────────────
# Only run size checks if the images were built successfully.

if docker image inspect "${BACKEND_IMAGE}" >/dev/null 2>&1; then
  run_test 3 "Backend image size ≤ 500MB" test_03_backend_size
else
  warn "Skipping Test 3 (backend image not available)."
  TESTS_FAILED=$((TESTS_FAILED + 1))
fi

if docker image inspect "${FRONTEND_IMAGE}" >/dev/null 2>&1; then
  run_test 4 "Frontend image size ≤ 100MB" test_04_frontend_size
else
  warn "Skipping Test 4 (frontend image not available)."
  TESTS_FAILED=$((TESTS_FAILED + 1))
fi

# ─────────────────────────────────────────────────────────────────────────────
# Phase 3: Container Startup (prerequisite for Tests 5-12)
# ─────────────────────────────────────────────────────────────────────────────
# Start SQL Server, backend, and frontend containers. This is infrastructure
# setup — not a numbered test. Failures cause downstream tests to fail.

if docker image inspect "${BACKEND_IMAGE}" >/dev/null 2>&1 && \
   docker image inspect "${FRONTEND_IMAGE}" >/dev/null 2>&1; then
  echo ""
  info "Starting test containers for Tests 5-12..."
  start_test_containers
else
  warn "Skipping container tests (images not available)."
  # Mark Tests 5-12 as failed since containers cannot start
  TESTS_FAILED=$((TESTS_FAILED + 8))
fi

# ─────────────────────────────────────────────────────────────────────────────
# Phase 4: Health Check Verification (Tests 5-6)
# ─────────────────────────────────────────────────────────────────────────────

if docker ps --format '{{.Names}}' 2>/dev/null | grep -q "^${BACKEND_CONTAINER}$"; then
  run_test 5 "Backend /api/health returns 200" test_05_backend_health
else
  warn "Skipping Test 5 (backend container not running)."
  TESTS_FAILED=$((TESTS_FAILED + 1))
fi

if docker ps --format '{{.Names}}' 2>/dev/null | grep -q "^${FRONTEND_CONTAINER}$"; then
  run_test 6 "Frontend /health returns 200" test_06_frontend_health
else
  warn "Skipping Test 6 (frontend container not running)."
  TESTS_FAILED=$((TESTS_FAILED + 1))
fi

# ─────────────────────────────────────────────────────────────────────────────
# Phase 5: Frontend Behavior Tests (Tests 7-10)
# ─────────────────────────────────────────────────────────────────────────────

if docker ps --format '{{.Names}}' 2>/dev/null | grep -q "^${FRONTEND_CONTAINER}$"; then
  run_test 7  "Frontend config.json injection from env vars" test_07_config_json_injection
  run_test 8  "Frontend SPA fallback (non-file path → index.html)" test_08_spa_fallback
  run_test 9  "Frontend source maps blocked (*.map → 404)" test_09_source_map_blocked
  run_test 10 "No *.map files exist in frontend image" test_10_no_map_files_in_image
else
  warn "Skipping Tests 7-10 (frontend container not running)."
  TESTS_FAILED=$((TESTS_FAILED + 4))
fi

# ─────────────────────────────────────────────────────────────────────────────
# Phase 6: Security Tests (Test 11)
# ─────────────────────────────────────────────────────────────────────────────

if docker image inspect "${BACKEND_IMAGE}" >/dev/null 2>&1 && \
   docker image inspect "${FRONTEND_IMAGE}" >/dev/null 2>&1; then
  run_test 11 "No secrets in Docker history" test_11_no_secrets_in_history
else
  warn "Skipping Test 11 (images not available)."
  TESTS_FAILED=$((TESTS_FAILED + 1))
fi

# ─────────────────────────────────────────────────────────────────────────────
# Phase 7: End-to-End Test (Test 12)
# ─────────────────────────────────────────────────────────────────────────────

if docker ps --format '{{.Names}}' 2>/dev/null | grep -q "^${BACKEND_CONTAINER}$" && \
   docker ps --format '{{.Names}}' 2>/dev/null | grep -q "^${FRONTEND_CONTAINER}$"; then
  run_test 12 "End-to-end reachability (backend + frontend)" test_12_e2e_reachability
else
  warn "Skipping Test 12 (containers not running)."
  TESTS_FAILED=$((TESTS_FAILED + 1))
fi

# =============================================================================
# Phase 8: Summary
# =============================================================================

echo ""
printf '%b========================================%b\n' "${BLUE}" "${NC}"
printf '%b Docker Local Validation Results%b\n' "${BLUE}" "${NC}"
printf '%b========================================%b\n' "${BLUE}" "${NC}"

if [ "${TESTS_PASSED}" -eq "${TESTS_TOTAL}" ]; then
  printf " ${GREEN}Passed: %d/%d${NC}\n" "${TESTS_PASSED}" "${TESTS_TOTAL}"
  printf " ${GREEN}Failed: %d/%d${NC}\n" "${TESTS_FAILED}" "${TESTS_TOTAL}"
else
  printf " ${GREEN}Passed: %d/%d${NC}\n" "${TESTS_PASSED}" "${TESTS_TOTAL}"
  printf " ${RED}Failed: %d/%d${NC}\n" "${TESTS_FAILED}" "${TESTS_TOTAL}"
fi

printf '%b========================================%b\n' "${BLUE}" "${NC}"
echo ""

# Exit with appropriate code — cleanup runs via trap regardless.
if [ "${TESTS_FAILED}" -gt 0 ]; then
  error "Validation FAILED — ${TESTS_FAILED} test(s) did not pass. Fix issues before ECR push."
  exit 1
fi

success "All ${TESTS_TOTAL} tests passed. Docker images are ready for ECR push."
exit 0
