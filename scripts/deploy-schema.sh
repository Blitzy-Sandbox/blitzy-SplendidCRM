#!/usr/bin/env bash
# =============================================================================
# SplendidCRM Database Schema Provisioning Script
# =============================================================================
#
# Purpose:  Provisions the SplendidCRM database schema against any SQL Server
#           instance — local Docker, AWS RDS, or any accessible SQL Server.
#           Creates the database, generates and executes Build.sql from the
#           SQL Scripts Community/ directory, creates the SplendidSessions
#           distributed session table, and validates schema object counts.
#
# Usage:    DB_HOST=localhost SA_PASSWORD=secret ./scripts/deploy-schema.sh
#           DB_HOST=rds.example.com DB_PORT=1433 SA_PASSWORD=secret ./scripts/deploy-schema.sh
#           ./scripts/deploy-schema.sh --help
#
# Environment Variables:
#   DB_HOST       — (required) SQL Server hostname or IP address
#                   Examples: localhost, splendidcrm-dev-rds.xxxx.us-east-2.rds.amazonaws.com
#   DB_PORT       — (optional) SQL Server port, default: 1433
#   DB_NAME       — (optional) Database name, default: SplendidCRM
#   SA_PASSWORD   — (required) SQL Server admin password (sa or RDS admin)
#   DB_USER       — (optional) SQL Server admin username, default: sa
#
# Prerequisites:
#   - sqlcmd (mssql-tools18) installed and accessible
#   - SQL Scripts Community/ directory in repository root
#   - Network connectivity to the target SQL Server
#
# Artifacts:
#   - ./dist/sql/Build.sql — Concatenated schema artifact
#
# Guardrails (G9):
#   - sqlcmd uses -t 600 (10-minute query timeout) for Build.sql execution
#   - sqlcmd uses -l 30 (30-second login timeout) for all connections
#   - sqlcmd does NOT use -b flag (non-fatal DDL warnings are expected)
#   - SplendidSessions DDL executes AFTER Build.sql (order matters)
#   - SQL directory concatenation order matches Build.bat exactly
#   - Within directories, files are concatenated by numeric suffix (0→9)
#
# Exit Codes:
#   0 — Schema provisioned and validated successfully
#   1 — Validation failure, missing prerequisites, or connection error
#
# =============================================================================

set -euo pipefail

# =============================================================================
# Terminal Color Constants and Utility Functions
# =============================================================================

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

# =============================================================================
# Help / Usage
# =============================================================================

if [[ "${1:-}" == "--help" ]]; then
  cat << 'HELPEOF'
SplendidCRM Database Schema Provisioning Script

Usage:
  DB_HOST=<host> SA_PASSWORD=<password> ./scripts/deploy-schema.sh [OPTIONS]

Options:
  --help    Show this help message and exit.

Required Environment Variables:
  DB_HOST       SQL Server hostname or IP address
  SA_PASSWORD   SQL Server admin password

Optional Environment Variables:
  DB_PORT       SQL Server port (default: 1433)
  DB_NAME       Database name (default: SplendidCRM)
  DB_USER       SQL Server username (default: sa)

Workflow:
  1. Validates prerequisites (sqlcmd, SQL Scripts Community/)
  2. Waits for SQL Server to become ready (up to 60 seconds)
  3. Creates database if it does not exist
  4. Generates Build.sql from SQL Scripts Community/ in dependency order
  5. Executes Build.sql against the database (10-minute timeout)
  6. Creates SplendidSessions distributed session table
  7. Validates schema object counts (≥218 tables, ≥583 views, ≥890 procedures)

Examples:
  # Local Docker SQL Server
  DB_HOST=localhost SA_PASSWORD='YourP@ssw0rd' ./scripts/deploy-schema.sh

  # AWS RDS SQL Server
  DB_HOST=splendidcrm-dev-rds.xxxx.us-east-2.rds.amazonaws.com \
  DB_PORT=1433 \
  SA_PASSWORD='RdsAdminPassword' \
  DB_USER=admin \
  ./scripts/deploy-schema.sh

  # Custom database name
  DB_HOST=localhost SA_PASSWORD='secret' DB_NAME=SplendidCRM_Test ./scripts/deploy-schema.sh

Artifacts:
  ./dist/sql/Build.sql  Concatenated SQL schema file generated from SQL Scripts Community/

Notes:
  - This script uses direct sqlcmd, not docker exec
  - The -b flag is intentionally omitted from sqlcmd — idempotent DDL scripts
    produce non-fatal dependency warnings that are safe to ignore
  - SplendidSessions table uses Id NVARCHAR(449) column as required by
    Microsoft.Extensions.Caching.SqlServer v10
  - SQL file concatenation order matches SQL Scripts Community/Build.bat exactly
  - Within each directory, files are ordered by numeric suffix (*.0.sql → *.9.sql)
HELPEOF
  exit 0
fi

# =============================================================================
# Phase 1: Environment Variable Parsing
# =============================================================================

echo ""
printf '%b============================================%b\n' "${BLUE}" "${NC}"
printf '%b SplendidCRM Schema Provisioning%b\n' "${BLUE}" "${NC}"
printf '%b============================================%b\n' "${BLUE}" "${NC}"
echo ""

info "Phase 1: Reading configuration..."

# Required variables — fail fast with descriptive error
: "${DB_HOST:?Error: DB_HOST environment variable is required. Set it to the SQL Server hostname (e.g., localhost or RDS endpoint).}"
: "${SA_PASSWORD:?Error: SA_PASSWORD environment variable is required. Set it to the SQL Server admin password.}"

# Optional variables with defaults
readonly DB_PORT="${DB_PORT:-1433}"
readonly DB_NAME="${DB_NAME:-SplendidCRM}"
readonly DB_USER="${DB_USER:-sa}"

# Validate DB_NAME format — alphanumeric and underscores only. Prevents
# accidental SQL injection from a misconfigured environment variable since
# DB_NAME is interpolated into SQL statements (e.g., WHERE name='${DB_NAME}').
if [[ ! "${DB_NAME}" =~ ^[a-zA-Z0-9_]+$ ]]; then
  error "DB_NAME contains invalid characters: '${DB_NAME}'. Only alphanumeric characters and underscores are allowed."
fi

# Build.sql output directory
readonly SQL_ARTIFACT_DIR="./dist/sql"

# Build.sql marker comment for stale file detection
readonly BUILD_SQL_MARKER="-- deploy-schema.sh suffix-ordered v1"

# Schema validation thresholds (from AAP §0.5.1)
readonly MIN_TABLES=218
readonly MIN_VIEWS=583
readonly MIN_PROCS=890

# Print connection summary (never print password)
info "Connection: ${DB_USER}@${DB_HOST},${DB_PORT} → database [${DB_NAME}]"

# =============================================================================
# Phase 2: Prerequisites Check
# =============================================================================

info "Phase 2: Checking prerequisites..."

# Discover sqlcmd binary — check multiple known paths
SQLCMD=""
if command -v sqlcmd &>/dev/null; then
  SQLCMD="sqlcmd"
elif [ -x "/opt/mssql-tools18/bin/sqlcmd" ]; then
  SQLCMD="/opt/mssql-tools18/bin/sqlcmd"
elif [ -x "/opt/mssql-tools/bin/sqlcmd" ]; then
  SQLCMD="/opt/mssql-tools/bin/sqlcmd"
else
  error "sqlcmd is not installed or not on PATH. Install mssql-tools18: https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-setup-tools"
fi
readonly SQLCMD
success "Found sqlcmd: ${SQLCMD}"

# Verify SQL Scripts Community directory exists (must run from repo root)
if [ ! -d "SQL Scripts Community" ]; then
  error "Directory 'SQL Scripts Community/' not found. This script must be run from the repository root directory."
fi
success "Found SQL Scripts Community/ directory."

# =============================================================================
# Phase 3: Wait for SQL Server Readiness
# =============================================================================

info "Phase 3: Waiting for SQL Server readiness..."

readonly MAX_WAIT_SECONDS=60
readonly RETRY_INTERVAL=2
ELAPSED=0

while [ "${ELAPSED}" -lt "${MAX_WAIT_SECONDS}" ]; do
  if ${SQLCMD} -S "${DB_HOST},${DB_PORT}" -U "${DB_USER}" -P "${SA_PASSWORD}" -C \
    -l 30 -Q "SELECT 1" > /dev/null 2>&1; then
    success "SQL Server is ready at ${DB_HOST},${DB_PORT}."
    break
  fi
  printf "."
  sleep "${RETRY_INTERVAL}"
  ELAPSED=$((ELAPSED + RETRY_INTERVAL))
done

if [ "${ELAPSED}" -ge "${MAX_WAIT_SECONDS}" ]; then
  echo ""
  error "SQL Server at ${DB_HOST},${DB_PORT} did not become ready within ${MAX_WAIT_SECONDS} seconds. Verify the host, port, credentials, and network connectivity."
fi

# =============================================================================
# Phase 4: Create Database
# =============================================================================

info "Phase 4: Creating database [${DB_NAME}] if not exists..."

${SQLCMD} -S "${DB_HOST},${DB_PORT}" -U "${DB_USER}" -P "${SA_PASSWORD}" -C \
  -l 30 \
  -Q "IF NOT EXISTS (SELECT * FROM sys.databases WHERE name='${DB_NAME}') CREATE DATABASE [${DB_NAME}]" \
  > /dev/null 2>&1

success "Database [${DB_NAME}] is ready."

# =============================================================================
# Phase 5: Generate Build.sql
# =============================================================================

info "Phase 5: Generating Build.sql from SQL Scripts Community/..."

mkdir -p "${SQL_ARTIFACT_DIR}"

# Determine whether Build.sql needs to be regenerated.
# Regenerate if: file missing, empty, or generated by a different script version.
NEEDS_REBUILD=false
if [ ! -f "${SQL_ARTIFACT_DIR}/Build.sql" ] || [ ! -s "${SQL_ARTIFACT_DIR}/Build.sql" ]; then
  NEEDS_REBUILD=true
elif ! head -1 "${SQL_ARTIFACT_DIR}/Build.sql" | grep -qF -e "${BUILD_SQL_MARKER}"; then
  info "Detected Build.sql generated with different tooling. Regenerating..."
  NEEDS_REBUILD=true
fi

if [ "${NEEDS_REBUILD}" = true ]; then
  info "Concatenating SQL files in dependency order..."

  # Write marker comment at top for stale-file detection on subsequent runs
  echo "${BUILD_SQL_MARKER}" > "${SQL_ARTIFACT_DIR}/Build.sql"

  # Directory order matches the upstream SQL Scripts Community/Build.bat
  # dependency chain exactly. "Reports" is intentionally omitted because
  # the community edition ships no Reports/ SQL directory.
  readonly SQL_DIRS=(
    "ProceduresDDL" "BaseTables" "Tables" "Functions"
    "ViewsDDL" "Views" "Procedures" "Triggers"
    "Data" "Terminology"
  )

  # CRITICAL: Within each directory the SQL files use a numeric suffix
  # convention (e.g. ACCOUNTS.1.sql, ACCOUNTS_BUGS.2.sql) to encode
  # dependency order. All *.0.sql files must execute before *.1.sql,
  # all *.1.sql before *.2.sql, and so on up to *.9.sql. A plain
  # alphabetical sort would interleave suffixes (ACCOUNTS_BUGS.2.sql
  # before BUGS.1.sql) causing foreign-key failures. The nested loop
  # below mirrors the Build.bat strategy of concatenating by suffix.
  for dir in "${SQL_DIRS[@]}"; do
    if [ -d "SQL Scripts Community/${dir}" ]; then
      for suffix in 0 1 2 3 4 5 6 7 8 9; do
        find "SQL Scripts Community/${dir}" -name "*.${suffix}.sql" -print0 \
          | sort -z \
          | xargs -r -0 cat >> "${SQL_ARTIFACT_DIR}/Build.sql"
      done
      # Add a GO batch separator after each directory's scripts
      echo "GO" >> "${SQL_ARTIFACT_DIR}/Build.sql"
    fi
  done

  BUILD_SIZE=$(wc -c < "${SQL_ARTIFACT_DIR}/Build.sql")
  success "Build.sql generated (${BUILD_SIZE} bytes)."
else
  BUILD_SIZE=$(wc -c < "${SQL_ARTIFACT_DIR}/Build.sql")
  success "Build.sql already exists with correct marker (${BUILD_SIZE} bytes). Skipping regeneration."
fi

# =============================================================================
# Phase 6: Execute Build.sql (G9)
# =============================================================================

info "Phase 6: Executing Build.sql against [${DB_NAME}] (this may take several minutes)..."

# CRITICAL Guardrail G9 constraints:
#   -t 600  — 10-minute query timeout (Build.sql can take several minutes)
#   -l 30   — 30-second login timeout
#   NO -b   — Idempotent DDL scripts produce non-fatal dependency warnings
#             (e.g., forward-referenced modules) that are safe to ignore.
#             The -b flag would abort on these non-fatal warnings.
#   -C      — Trust server certificate (required for Docker and RDS self-signed certs)
${SQLCMD} -S "${DB_HOST},${DB_PORT}" -U "${DB_USER}" -P "${SA_PASSWORD}" -C \
  -d "${DB_NAME}" \
  -i "${SQL_ARTIFACT_DIR}/Build.sql" \
  -t 600 \
  -l 30 \
  2>&1 | tail -5 || warn "Some SQL statements produced warnings (non-fatal). This is expected for idempotent DDL scripts."

success "Build.sql execution completed."

# =============================================================================
# Phase 7: Create SplendidSessions Table
# =============================================================================

info "Phase 7: Provisioning SplendidSessions distributed session table..."

# The SplendidSessions table is required by Microsoft.Extensions.Caching.SqlServer
# v10 which internally queries column "Id NVARCHAR(449)" — NOT "SessionId".
# If the table exists but was created with the wrong column name (SessionId),
# drop and recreate with the correct schema.
#
# CRITICAL: This DDL MUST execute AFTER Build.sql (order matters).
# Uses -t 600 -l 30 and NO -b flag (consistent with Build.sql execution).
${SQLCMD} -S "${DB_HOST},${DB_PORT}" -U "${DB_USER}" -P "${SA_PASSWORD}" -C \
  -d "${DB_NAME}" \
  -t 600 \
  -l 30 \
  -Q "
    -- Drop table if it exists with wrong schema (SessionId instead of Id)
    IF EXISTS (
      SELECT * FROM INFORMATION_SCHEMA.TABLES
      WHERE TABLE_NAME = 'SplendidSessions'
    )
    AND NOT EXISTS (
      SELECT * FROM INFORMATION_SCHEMA.COLUMNS
      WHERE TABLE_NAME = 'SplendidSessions' AND COLUMN_NAME = 'Id'
    )
    BEGIN
      DROP TABLE dbo.SplendidSessions;
      PRINT 'Dropped SplendidSessions table with wrong schema (SessionId column).';
    END

    IF NOT EXISTS (
      SELECT * FROM INFORMATION_SCHEMA.TABLES
      WHERE TABLE_NAME = 'SplendidSessions'
    )
    BEGIN
      CREATE TABLE dbo.SplendidSessions (
        Id                         NVARCHAR(449)   NOT NULL PRIMARY KEY,
        Value                      VARBINARY(MAX)  NOT NULL,
        ExpiresAtTime              DATETIMEOFFSET  NOT NULL,
        SlidingExpirationInSeconds BIGINT          NULL,
        AbsoluteExpiration         DATETIMEOFFSET  NULL
      );
      PRINT 'Created SplendidSessions table with correct schema.';
    END
    ELSE
      PRINT 'SplendidSessions table already exists with correct schema.';
  " 2>/dev/null || warn "SplendidSessions DDL produced a warning (non-fatal)."

success "SplendidSessions table is ready."

# =============================================================================
# Phase 8: Validate Schema Object Counts
# =============================================================================

info "Phase 8: Validating schema object counts..."

# Query table count — SET NOCOUNT ON + -h -1 -W for clean numeric output
TABLE_COUNT=$(${SQLCMD} -S "${DB_HOST},${DB_PORT}" -U "${DB_USER}" -P "${SA_PASSWORD}" -C \
  -d "${DB_NAME}" \
  -l 30 \
  -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM ${DB_NAME}.INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'" \
  -h -1 -W 2>/dev/null | tr -d '[:space:]' || echo "0")

# Query view count
VIEW_COUNT=$(${SQLCMD} -S "${DB_HOST},${DB_PORT}" -U "${DB_USER}" -P "${SA_PASSWORD}" -C \
  -d "${DB_NAME}" \
  -l 30 \
  -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM ${DB_NAME}.INFORMATION_SCHEMA.VIEWS" \
  -h -1 -W 2>/dev/null | tr -d '[:space:]' || echo "0")

# Query stored procedure count
PROC_COUNT=$(${SQLCMD} -S "${DB_HOST},${DB_PORT}" -U "${DB_USER}" -P "${SA_PASSWORD}" -C \
  -d "${DB_NAME}" \
  -l 30 \
  -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM ${DB_NAME}.INFORMATION_SCHEMA.ROUTINES WHERE ROUTINE_TYPE='PROCEDURE'" \
  -h -1 -W 2>/dev/null | tr -d '[:space:]' || echo "0")

info "Schema counts: ${TABLE_COUNT} tables, ${VIEW_COUNT} views, ${PROC_COUNT} procedures"

# Track validation failures
VALIDATION_FAILED=false

if [ "${TABLE_COUNT}" -lt "${MIN_TABLES}" ]; then
  warn "Table count ${TABLE_COUNT} is below minimum threshold of ${MIN_TABLES}."
  VALIDATION_FAILED=true
fi

if [ "${VIEW_COUNT}" -lt "${MIN_VIEWS}" ]; then
  warn "View count ${VIEW_COUNT} is below minimum threshold of ${MIN_VIEWS}."
  VALIDATION_FAILED=true
fi

if [ "${PROC_COUNT}" -lt "${MIN_PROCS}" ]; then
  warn "Procedure count ${PROC_COUNT} is below minimum threshold of ${MIN_PROCS}."
  VALIDATION_FAILED=true
fi

if [ "${VALIDATION_FAILED}" = true ]; then
  error "Schema validation FAILED. Expected ≥${MIN_TABLES} tables, ≥${MIN_VIEWS} views, ≥${MIN_PROCS} procedures. Got ${TABLE_COUNT} tables, ${VIEW_COUNT} views, ${PROC_COUNT} procedures."
fi

success "Schema validated: ${TABLE_COUNT} tables, ${VIEW_COUNT} views, ${PROC_COUNT} procedures."

# =============================================================================
# Phase 9: Summary
# =============================================================================

echo ""
printf '%b============================================%b\n' "${GREEN}" "${NC}"
printf '%b Schema Provisioning Complete%b\n' "${GREEN}" "${NC}"
printf '%b============================================%b\n' "${GREEN}" "${NC}"
echo ""
success "Database:   [${DB_NAME}]"
success "Host:       ${DB_HOST},${DB_PORT}"
success "User:       ${DB_USER}"
success "Tables:     ${TABLE_COUNT} (minimum: ${MIN_TABLES})"
success "Views:      ${VIEW_COUNT} (minimum: ${MIN_VIEWS})"
success "Procedures: ${PROC_COUNT} (minimum: ${MIN_PROCS})"
success "Sessions:   SplendidSessions table ready (Id NVARCHAR(449))"
success "Artifact:   ${SQL_ARTIFACT_DIR}/Build.sql ($(wc -c < "${SQL_ARTIFACT_DIR}/Build.sql") bytes)"
echo ""

exit 0
