#!/usr/bin/env bash
# =============================================================================
# SplendidCRM Infrastructure Validation Suite — LocalStack + Docker SQL Server
# =============================================================================
#
# Purpose:  Validates the entire Terraform infrastructure against LocalStack Pro
#           and tests database schema provisioning against a Docker SQL Server
#           container. Runs 19 tests: 15 LocalStack resource verification tests
#           and 4 Docker SQL Server schema/connectivity tests.
#
# Usage:    ./scripts/validate-infra-localstack.sh
#           SA_PASSWORD='YourP@ssw0rd' ./scripts/validate-infra-localstack.sh
#
# Prerequisites:
#   - Terraform >= 1.12.0
#   - AWS CLI (v1 or v2)
#   - Docker Engine (running)
#   - curl >= 7.0
#   - jq >= 1.6
#   - sqlcmd (mssql-tools18) — required for Docker SQL Server tests
#   - LocalStack Pro running on http://localhost:4566
#
# Test Categories:
#   Tests  1-15:  LocalStack AWS resource verification (via AWS CLI)
#   Tests 16-19:  Docker SQL Server schema provisioning and validation
#
# Additional Validation Phases (not counted in test total):
#   - Terraform idempotency check (terraform plan -detailed-exitcode)
#   - Terraform destroy verification (clean teardown)
#
# Exit Codes:
#   0 — All 19 tests passed
#   1 — One or more tests failed, or a prerequisite check failed
#
# Environment Variables:
#   SA_PASSWORD   — (required for Docker SQL tests) SQL Server admin password
#   SQL_PASSWORD  — (fallback) Used if SA_PASSWORD is not set
#
# Architecture:
#   Phase 1: Prerequisites check
#   Phase 2: LocalStack VPC/subnet pre-creation (tag-based data source targets)
#   Phase 3: Terraform init → plan → apply (against LocalStack)
#   Phase 4: 15 AWS resource verification tests via AWS CLI
#   Phase 5: Terraform idempotency verification (plan -detailed-exitcode)
#   Phase 6: Terraform destroy verification (clean teardown)
#   Phase 7: 4 Docker SQL Server schema provisioning + validation tests
#   Phase 8: Summary report and cleanup
#
# =============================================================================

set -euo pipefail

# =============================================================================
# Terminal Color Constants and Utility Functions
# =============================================================================
# Matches the color-coded output convention from scripts/build-and-run.sh

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

# error_exit: Fatal error that terminates the script.
# Unlike build-and-run.sh's error(), this is named error_exit to distinguish
# from test failures (which should NOT terminate the script).
error_exit() {
  printf "${RED}[FAIL]${NC} %s\n" "$1" >&2
  exit 1
}

# =============================================================================
# Test Counter Variables and Helper Functions
# =============================================================================

TESTS_PASSED=0
TESTS_FAILED=0
readonly TESTS_TOTAL=19

# test_passed: Record a passing test with formatted output.
#   $1 — test number (integer)
#   $2 — test description (string)
test_passed() {
  TESTS_PASSED=$((TESTS_PASSED + 1))
  printf "${GREEN}[PASS]${NC} Test %02d: %s\n" "$1" "$2"
}

# test_failed: Record a failing test with formatted output.
#   $1 — test number (integer)
#   $2 — test description (string)
#   $3 — (optional) failure detail message
test_failed() {
  TESTS_FAILED=$((TESTS_FAILED + 1))
  printf "${RED}[FAIL]${NC} Test %02d: %s\n" "$1" "$2"
  if [ -n "${3:-}" ]; then
    printf "         Details: %s\n" "$3"
  fi
}

# phase_status: Print a non-test validation phase result (idempotency/destroy).
#   $1 — "PASS" or "FAIL"
#   $2 — phase description
#   $3 — (optional) detail message
phase_status() {
  local status="$1"
  local desc="$2"
  local detail="${3:-}"
  if [ "${status}" = "PASS" ]; then
    printf "${GREEN}[PASS]${NC} Phase: %s\n" "${desc}"
  else
    printf "${RED}[FAIL]${NC} Phase: %s\n" "${desc}"
    if [ -n "${detail}" ]; then
      printf "         Details: %s\n" "${detail}"
    fi
  fi
}

# =============================================================================
# Script Configuration
# =============================================================================

# Resolve script and repository root directories
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

# LocalStack connection configuration
readonly LOCALSTACK_ENDPOINT="http://localhost:4566"
readonly AWS_REGION="us-east-2"

# Resource naming — must match infrastructure/environments/localstack/localstack.auto.tfvars
readonly NAME_PREFIX="splendidcrm-local"

# Terraform directory — per AAP §0.8.3: ALL autonomous Terraform operations
# MUST run from environments/localstack/ — NEVER from dev/staging/prod
readonly TF_DIR="${REPO_ROOT}/infrastructure/environments/localstack"

# Docker SQL Server validation configuration
readonly SQL_VALIDATION_CONTAINER="splendid-sql-validation"
readonly SQL_VALIDATION_DB="SplendidCRM_ValidationTest"
SQL_VALIDATION_PORT=14330  # Non-readonly: may be updated if reusing existing container
SQL_STARTED_BY_SCRIPT=false

# SA_PASSWORD resolution: check SA_PASSWORD, fall back to SQL_PASSWORD
if [ -n "${SA_PASSWORD:-}" ]; then
  : # SA_PASSWORD already set
elif [ -n "${SQL_PASSWORD:-}" ]; then
  SA_PASSWORD="${SQL_PASSWORD}"
else
  SA_PASSWORD=""
fi

# =============================================================================
# awslocal: Helper function to invoke AWS CLI against LocalStack
# =============================================================================
# All AWS CLI calls MUST use --endpoint-url and --region to target LocalStack.
# This helper eliminates repetition and ensures consistency.
awslocal() {
  aws --endpoint-url "${LOCALSTACK_ENDPOINT}" --region "${AWS_REGION}" --output json "$@"
}

# =============================================================================
# Cleanup Function — Trap on EXIT
# =============================================================================
# Automatically cleans up resources created by this script on exit, regardless
# of whether the script succeeded or failed. Removes:
#   - Validation SQL Server Docker container (if started by this script)
#   - Terraform plan files (tfplan)
cleanup() {
  echo ""
  info "Running cleanup..."

  # Remove validation SQL Server container only if this script started it
  if [ "${SQL_STARTED_BY_SCRIPT}" = true ]; then
    if docker ps -a --format '{{.Names}}' 2>/dev/null | grep -q "^${SQL_VALIDATION_CONTAINER}$"; then
      info "Stopping and removing validation SQL Server container..."
      docker stop "${SQL_VALIDATION_CONTAINER}" > /dev/null 2>&1 || true
      docker rm -f "${SQL_VALIDATION_CONTAINER}" > /dev/null 2>&1 || true
      success "Validation SQL Server container removed."
    fi
  fi

  # Remove terraform plan file
  if [ -f "${TF_DIR}/tfplan" ]; then
    rm -f "${TF_DIR}/tfplan"
    info "Removed terraform plan file."
  fi

  info "Cleanup complete."
}

trap cleanup EXIT

# =============================================================================
# Usage / Help
# =============================================================================
# Provides interactive --help support for consistency with deploy-schema.sh and
# build-and-push.sh. Mirrors the header documentation in a user-friendly format.

usage() {
  cat << 'HELPEOF'
Usage: ./scripts/validate-infra-localstack.sh [--help]

Validates the entire Terraform infrastructure against LocalStack Pro and tests
database schema provisioning against a Docker SQL Server container.

Test Suite (19 tests):
  Tests  1-15   LocalStack AWS resource verification (via AWS CLI)
  Tests 16-19   Docker SQL Server schema provisioning and validation

Additional Validation Phases (not counted in test total):
  - Terraform idempotency check (terraform plan -detailed-exitcode)
  - Terraform destroy verification (clean teardown)

Prerequisites:
  - Terraform >= 1.12.0           - AWS CLI (v1 or v2)
  - Docker Engine (running)       - curl >= 7.0
  - jq >= 1.6                     - sqlcmd (mssql-tools18)
  - LocalStack Pro running on http://localhost:4566

Environment Variables:
  SA_PASSWORD   (required for Docker SQL tests) SQL Server admin password
  SQL_PASSWORD  (fallback) Used if SA_PASSWORD is not set

Exit Codes:
  0 — All 19 tests passed
  1 — One or more tests failed, or a prerequisite check failed

Examples:
  # Run full validation suite
  ./scripts/validate-infra-localstack.sh

  # Run with explicit SA_PASSWORD
  SA_PASSWORD='YourP@ssw0rd' ./scripts/validate-infra-localstack.sh
HELPEOF
}

# Parse command-line arguments (--help) BEFORE any heavy operations, so help
# works even when prerequisites like LocalStack are not running.
for arg in "$@"; do
  case "$arg" in
    --help)
      usage
      exit 0
      ;;
    *)
      error "Unknown option: ${arg}. Use --help for usage information."
      ;;
  esac
done

# Navigate to repository root for consistent relative path resolution
cd "${REPO_ROOT}"

# =============================================================================
# Script Banner
# =============================================================================

echo ""
printf '%b============================================%b\n' "${BLUE}" "${NC}"
printf '%b SplendidCRM Infrastructure Validation%b\n' "${BLUE}" "${NC}"
printf '%b LocalStack + Docker SQL Server%b\n' "${BLUE}" "${NC}"
printf '%b============================================%b\n' "${BLUE}" "${NC}"
echo ""
info "Test suite: ${TESTS_TOTAL} tests (15 LocalStack + 4 Docker SQL Server)"
info "LocalStack endpoint: ${LOCALSTACK_ENDPOINT}"
info "AWS region: ${AWS_REGION}"
info "Name prefix: ${NAME_PREFIX}"
echo ""

# =============================================================================
# Phase 1: Prerequisites Check
# =============================================================================

printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"
info "Phase 1: Checking prerequisites..."
printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"

# --- Terraform ---
if ! command -v terraform &>/dev/null; then
  error_exit "Terraform is not installed or not on PATH. Install Terraform >= 1.12.0."
fi
TF_VERSION_RAW=$(terraform version -json 2>/dev/null | jq -r '.terraform_version' 2>/dev/null || terraform version | head -1 | sed 's/[^0-9.]//g')
TF_MAJOR=$(echo "${TF_VERSION_RAW}" | cut -d. -f1)
TF_MINOR=$(echo "${TF_VERSION_RAW}" | cut -d. -f2)
if [ "${TF_MAJOR}" -lt 1 ] || { [ "${TF_MAJOR}" -eq 1 ] && [ "${TF_MINOR}" -lt 12 ]; }; then
  error_exit "Terraform >= 1.12.0 required. Found: ${TF_VERSION_RAW}"
fi
success "Terraform ${TF_VERSION_RAW} detected (>= 1.12.0 required)"

# --- AWS CLI ---
if ! command -v aws &>/dev/null; then
  error_exit "AWS CLI is not installed or not on PATH. Install AWS CLI v1 or v2."
fi
AWS_VERSION_RAW=$(aws --version 2>&1 | head -1)
success "AWS CLI detected: ${AWS_VERSION_RAW}"

# --- Docker ---
if ! command -v docker &>/dev/null; then
  error_exit "Docker is not installed or not on PATH."
fi
if ! docker info &>/dev/null; then
  if ! sudo docker info &>/dev/null; then
    error_exit "Docker daemon is not running. Start Docker and try again."
  fi
fi
DOCKER_VERSION_RAW=$(docker --version | sed -E 's/.*version ([0-9.]+).*/\1/')
success "Docker ${DOCKER_VERSION_RAW} detected and running"

# --- curl ---
if ! command -v curl &>/dev/null; then
  error_exit "curl is not installed or not on PATH."
fi
success "curl detected"

# --- jq ---
if ! command -v jq &>/dev/null; then
  error_exit "jq is not installed or not on PATH. Install jq >= 1.6."
fi
success "jq detected: $(jq --version 2>&1)"

# --- sqlcmd (for Docker SQL Server tests) ---
SQLCMD_PATH=""
if command -v sqlcmd &>/dev/null; then
  SQLCMD_PATH="sqlcmd"
elif [ -x "/opt/mssql-tools18/bin/sqlcmd" ]; then
  SQLCMD_PATH="/opt/mssql-tools18/bin/sqlcmd"
elif [ -x "/opt/mssql-tools/bin/sqlcmd" ]; then
  SQLCMD_PATH="/opt/mssql-tools/bin/sqlcmd"
fi

SQLCMD_AVAILABLE=true
if [ -z "${SQLCMD_PATH}" ]; then
  warn "sqlcmd not found — Docker SQL Server tests (16-19) will be skipped."
  SQLCMD_AVAILABLE=false
else
  success "sqlcmd detected: ${SQLCMD_PATH}"
fi

# --- SA_PASSWORD (for Docker SQL Server tests) ---
if [ -z "${SA_PASSWORD}" ]; then
  warn "SA_PASSWORD is not set — Docker SQL Server tests (16-19) will be skipped."
  SQLCMD_AVAILABLE=false
fi

# --- deploy-schema.sh (for Docker SQL Server test 16) ---
DEPLOY_SCHEMA_PATH="${REPO_ROOT}/scripts/deploy-schema.sh"
if [ ! -f "${DEPLOY_SCHEMA_PATH}" ]; then
  warn "scripts/deploy-schema.sh not found — Docker SQL Server tests (16-19) will be skipped."
  SQLCMD_AVAILABLE=false
else
  success "deploy-schema.sh found"
fi

# --- LocalStack Pro health check ---
if ! curl -sf "${LOCALSTACK_ENDPOINT}/_localstack/health" > /dev/null 2>&1; then
  error_exit "LocalStack Pro is not running at ${LOCALSTACK_ENDPOINT}. Start LocalStack and try again."
fi
success "LocalStack Pro is running and healthy at ${LOCALSTACK_ENDPOINT}"

# --- Terraform directory ---
if [ ! -d "${TF_DIR}" ]; then
  error_exit "Terraform directory not found: ${TF_DIR}"
fi
if [ ! -f "${TF_DIR}/versions.tf" ]; then
  error_exit "versions.tf not found in ${TF_DIR}"
fi
if [ ! -f "${TF_DIR}/main.tf" ]; then
  error_exit "main.tf not found in ${TF_DIR}"
fi
success "Terraform LocalStack environment directory validated"

echo ""
success "All prerequisites satisfied."
echo ""

# =============================================================================
# Phase 2: LocalStack VPC and Subnet Pre-Creation
# =============================================================================
# The Terraform data sources in data.tf use tag-based lookups to discover:
#   - VPC with tag:Name = "acme-dev-vpc-use2"
#   - App subnets with tag:Name matching "*-app-*" in that VPC
#   - DB subnets with tag:Name matching "*-db-*" in that VPC
#
# These resources must exist in LocalStack BEFORE terraform plan/apply.
# This phase creates them idempotently.

printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"
info "Phase 2: Setting up VPC and subnets in LocalStack..."
printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"

# Check if VPC already exists
EXISTING_VPC_ID=$(awslocal ec2 describe-vpcs \
  --filters "Name=tag:Name,Values=acme-dev-vpc-use2" \
  --query 'Vpcs[0].VpcId' --output text 2>/dev/null) || true

if [ "${EXISTING_VPC_ID}" != "None" ] && [ -n "${EXISTING_VPC_ID}" ] && [ "${EXISTING_VPC_ID}" != "null" ]; then
  VPC_ID="${EXISTING_VPC_ID}"
  info "VPC already exists: ${VPC_ID}"
else
  info "Creating VPC (10.0.0.0/16) with tag acme-dev-vpc-use2..."
  VPC_ID=$(awslocal ec2 create-vpc \
    --cidr-block 10.0.0.0/16 \
    --query 'Vpc.VpcId' --output text)

  awslocal ec2 create-tags \
    --resources "${VPC_ID}" \
    --tags Key=Name,Value=acme-dev-vpc-use2 > /dev/null
  success "VPC created: ${VPC_ID}"
fi

# Check if app subnets already exist
EXISTING_APP_SUBNETS=$(awslocal ec2 describe-subnets \
  --filters "Name=vpc-id,Values=${VPC_ID}" "Name=tag:Name,Values=*-app-*" \
  --query 'Subnets[].SubnetId' --output text 2>/dev/null) || true

if [ -n "${EXISTING_APP_SUBNETS}" ] && [ "${EXISTING_APP_SUBNETS}" != "None" ]; then
  info "App subnets already exist: ${EXISTING_APP_SUBNETS}"
else
  info "Creating app subnets (required for ALB and ECS services)..."
  APP_SUBNET_1=$(awslocal ec2 create-subnet \
    --vpc-id "${VPC_ID}" \
    --cidr-block 10.0.1.0/24 \
    --availability-zone "${AWS_REGION}a" \
    --query 'Subnet.SubnetId' --output text)
  awslocal ec2 create-tags \
    --resources "${APP_SUBNET_1}" \
    --tags Key=Name,Value=acme-dev-app-use2a > /dev/null

  APP_SUBNET_2=$(awslocal ec2 create-subnet \
    --vpc-id "${VPC_ID}" \
    --cidr-block 10.0.2.0/24 \
    --availability-zone "${AWS_REGION}b" \
    --query 'Subnet.SubnetId' --output text)
  awslocal ec2 create-tags \
    --resources "${APP_SUBNET_2}" \
    --tags Key=Name,Value=acme-dev-app-use2b > /dev/null

  success "App subnets created: ${APP_SUBNET_1}, ${APP_SUBNET_2}"
fi

# Check if DB subnets already exist
EXISTING_DB_SUBNETS=$(awslocal ec2 describe-subnets \
  --filters "Name=vpc-id,Values=${VPC_ID}" "Name=tag:Name,Values=*-db-*" \
  --query 'Subnets[].SubnetId' --output text 2>/dev/null) || true

if [ -n "${EXISTING_DB_SUBNETS}" ] && [ "${EXISTING_DB_SUBNETS}" != "None" ]; then
  info "DB subnets already exist: ${EXISTING_DB_SUBNETS}"
else
  info "Creating DB subnets (required for RDS subnet group)..."
  DB_SUBNET_1=$(awslocal ec2 create-subnet \
    --vpc-id "${VPC_ID}" \
    --cidr-block 10.0.3.0/24 \
    --availability-zone "${AWS_REGION}a" \
    --query 'Subnet.SubnetId' --output text)
  awslocal ec2 create-tags \
    --resources "${DB_SUBNET_1}" \
    --tags Key=Name,Value=acme-dev-db-use2a > /dev/null

  DB_SUBNET_2=$(awslocal ec2 create-subnet \
    --vpc-id "${VPC_ID}" \
    --cidr-block 10.0.4.0/24 \
    --availability-zone "${AWS_REGION}b" \
    --query 'Subnet.SubnetId' --output text)
  awslocal ec2 create-tags \
    --resources "${DB_SUBNET_2}" \
    --tags Key=Name,Value=acme-dev-db-use2b > /dev/null

  success "DB subnets created: ${DB_SUBNET_1}, ${DB_SUBNET_2}"
fi

echo ""
success "LocalStack VPC and subnets ready."
echo ""

# =============================================================================
# Phase 3: Terraform Init, Plan, and Apply (against LocalStack)
# =============================================================================
# All Terraform operations MUST target infrastructure/environments/localstack/
# per AAP §0.8.3. The localstack environment uses local state backend and
# LocalStack endpoint overrides defined in versions.tf.

printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"
info "Phase 3: Running Terraform against LocalStack..."
printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"

pushd "${TF_DIR}" > /dev/null

# Clean stale state if needed (previous failed runs)
if [ -f ".terraform.lock.hcl" ] && [ ! -d ".terraform" ]; then
  rm -f .terraform.lock.hcl
fi

# --- Terraform Init ---
info "Running: terraform init -input=false"
if terraform init -input=false -no-color > /tmp/tf_init_output.txt 2>&1; then
  success "Terraform init succeeded."
else
  cat /tmp/tf_init_output.txt >&2
  rm -f /tmp/tf_init_output.txt
  popd > /dev/null
  error_exit "Terraform init failed. Check module configuration and try again."
fi
rm -f /tmp/tf_init_output.txt

# --- Terraform Plan ---
info "Running: terraform plan -input=false -out=tfplan"
if terraform plan -input=false -out=tfplan -no-color > /tmp/tf_plan_output.txt 2>&1; then
  success "Terraform plan succeeded — plan file saved."
else
  cat /tmp/tf_plan_output.txt >&2
  rm -f /tmp/tf_plan_output.txt
  popd > /dev/null
  error_exit "Terraform plan failed. Check Terraform configuration and try again."
fi
rm -f /tmp/tf_plan_output.txt

# --- Terraform Apply ---
info "Running: terraform apply -input=false -auto-approve tfplan"
if terraform apply -input=false -auto-approve tfplan -no-color > /tmp/tf_apply_output.txt 2>&1; then
  success "Terraform apply succeeded — all resources created in LocalStack."
else
  cat /tmp/tf_apply_output.txt >&2
  rm -f /tmp/tf_apply_output.txt
  popd > /dev/null
  error_exit "Terraform apply failed. Check LocalStack service compatibility."
fi
rm -f /tmp/tf_apply_output.txt

popd > /dev/null

echo ""
success "Terraform infrastructure deployed to LocalStack."
echo ""

# =============================================================================
# Phase 4: LocalStack Resource Verification Tests (Tests 1-15)
# =============================================================================
# Each test uses the AWS CLI with --endpoint-url http://localhost:4566 --region
# us-east-2 to verify that Terraform created the expected AWS resources in
# LocalStack. Tests use the naming convention from localstack.auto.tfvars:
#   name_prefix = "splendidcrm-local"

printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"
info "Phase 4: Running 15 LocalStack resource verification tests..."
printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"
echo ""

# ---- Test 01: ECR Repositories (2 repos) ----
info "Test 01: Checking ECR repositories..."
ECR_REPOS=""
ECR_REPOS=$(awslocal ecr describe-repositories \
  --query 'repositories[].repositoryName' --output text 2>/dev/null) || true

ECR_HAS_BACKEND=false
ECR_HAS_FRONTEND=false
if echo "${ECR_REPOS}" | grep -q "${NAME_PREFIX}-backend"; then
  ECR_HAS_BACKEND=true
fi
if echo "${ECR_REPOS}" | grep -q "${NAME_PREFIX}-frontend"; then
  ECR_HAS_FRONTEND=true
fi

if [ "${ECR_HAS_BACKEND}" = true ] && [ "${ECR_HAS_FRONTEND}" = true ]; then
  test_passed 1 "ECR repositories (${NAME_PREFIX}-backend + ${NAME_PREFIX}-frontend)"
else
  MISSING=""
  [ "${ECR_HAS_BACKEND}" = false ] && MISSING="${MISSING} ${NAME_PREFIX}-backend"
  [ "${ECR_HAS_FRONTEND}" = false ] && MISSING="${MISSING} ${NAME_PREFIX}-frontend"
  test_failed 1 "ECR repositories" "Missing repos:${MISSING}"
fi

# ---- Test 02: ECS Cluster ----
info "Test 02: Checking ECS cluster..."
CLUSTER_STATUS=""
CLUSTER_STATUS=$(awslocal ecs describe-clusters \
  --clusters "${NAME_PREFIX}-cluster" \
  --query 'clusters[0].status' --output text 2>/dev/null) || true

if [ "${CLUSTER_STATUS}" = "ACTIVE" ]; then
  test_passed 2 "ECS cluster (${NAME_PREFIX}-cluster) — status: ACTIVE"
else
  test_failed 2 "ECS cluster (${NAME_PREFIX}-cluster)" "Expected ACTIVE, got: ${CLUSTER_STATUS:-<not found>}"
fi

# ---- Test 03: ECS Task Definitions (2 task defs) ----
info "Test 03: Checking ECS task definitions..."
TASK_DEFS=""
TASK_DEFS=$(awslocal ecs list-task-definitions \
  --query 'taskDefinitionArns[]' --output text 2>/dev/null) || true

TD_HAS_BACKEND=false
TD_HAS_FRONTEND=false
if echo "${TASK_DEFS}" | grep -q "${NAME_PREFIX}-backend-task"; then
  TD_HAS_BACKEND=true
fi
if echo "${TASK_DEFS}" | grep -q "${NAME_PREFIX}-frontend-task"; then
  TD_HAS_FRONTEND=true
fi

if [ "${TD_HAS_BACKEND}" = true ] && [ "${TD_HAS_FRONTEND}" = true ]; then
  test_passed 3 "ECS task definitions (backend-task + frontend-task)"
else
  MISSING=""
  [ "${TD_HAS_BACKEND}" = false ] && MISSING="${MISSING} ${NAME_PREFIX}-backend-task"
  [ "${TD_HAS_FRONTEND}" = false ] && MISSING="${MISSING} ${NAME_PREFIX}-frontend-task"
  test_failed 3 "ECS task definitions" "Missing task defs:${MISSING}"
fi

# ---- Test 04: ECS Services (2 services) ----
info "Test 04: Checking ECS services..."
ECS_SERVICES=""
ECS_SERVICES=$(awslocal ecs list-services \
  --cluster "${NAME_PREFIX}-cluster" \
  --query 'serviceArns[]' --output text 2>/dev/null) || true

SVC_HAS_BACKEND=false
SVC_HAS_FRONTEND=false
if echo "${ECS_SERVICES}" | grep -q "${NAME_PREFIX}-backend-svc"; then
  SVC_HAS_BACKEND=true
fi
if echo "${ECS_SERVICES}" | grep -q "${NAME_PREFIX}-frontend-svc"; then
  SVC_HAS_FRONTEND=true
fi

if [ "${SVC_HAS_BACKEND}" = true ] && [ "${SVC_HAS_FRONTEND}" = true ]; then
  test_passed 4 "ECS services (backend-svc + frontend-svc)"
else
  MISSING=""
  [ "${SVC_HAS_BACKEND}" = false ] && MISSING="${MISSING} ${NAME_PREFIX}-backend-svc"
  [ "${SVC_HAS_FRONTEND}" = false ] && MISSING="${MISSING} ${NAME_PREFIX}-frontend-svc"
  test_failed 4 "ECS services" "Missing services:${MISSING}"
fi

# ---- Test 05: Application Load Balancer ----
info "Test 05: Checking ALB..."
ALB_NAMES=""
ALB_NAMES=$(awslocal elbv2 describe-load-balancers \
  --query 'LoadBalancers[].LoadBalancerName' --output text 2>/dev/null) || true

if echo "${ALB_NAMES}" | grep -q "${NAME_PREFIX}"; then
  test_passed 5 "Application Load Balancer (${NAME_PREFIX}-alb)"
else
  test_failed 5 "Application Load Balancer" "No ALB found matching '${NAME_PREFIX}'"
fi

# ---- Test 06: Target Groups (2 TGs: backend :8080, frontend :80) ----
info "Test 06: Checking target groups..."
TG_OUTPUT=""
TG_OUTPUT=$(awslocal elbv2 describe-target-groups 2>/dev/null) || true

TG_HAS_BACKEND=false
TG_HAS_FRONTEND=false
if echo "${TG_OUTPUT}" | jq -r '.TargetGroups[] | "\(.TargetGroupName) \(.Port)"' 2>/dev/null | grep -q "${NAME_PREFIX}.*8080"; then
  TG_HAS_BACKEND=true
fi
if echo "${TG_OUTPUT}" | jq -r '.TargetGroups[] | "\(.TargetGroupName) \(.Port)"' 2>/dev/null | grep -q "${NAME_PREFIX}.*80\b"; then
  TG_HAS_FRONTEND=true
fi
# Also try checking by port directly in case name matching is partial
if [ "${TG_HAS_BACKEND}" = false ]; then
  if echo "${TG_OUTPUT}" | jq -e '.TargetGroups[] | select(.Port == 8080)' > /dev/null 2>&1; then
    TG_HAS_BACKEND=true
  fi
fi
if [ "${TG_HAS_FRONTEND}" = false ]; then
  if echo "${TG_OUTPUT}" | jq -e '.TargetGroups[] | select(.Port == 80)' > /dev/null 2>&1; then
    TG_HAS_FRONTEND=true
  fi
fi

if [ "${TG_HAS_BACKEND}" = true ] && [ "${TG_HAS_FRONTEND}" = true ]; then
  test_passed 6 "Target groups (backend :8080 + frontend :80)"
else
  MISSING=""
  [ "${TG_HAS_BACKEND}" = false ] && MISSING="${MISSING} backend(:8080)"
  [ "${TG_HAS_FRONTEND}" = false ] && MISSING="${MISSING} frontend(:80)"
  test_failed 6 "Target groups" "Missing target groups:${MISSING}"
fi

# ---- Test 07: ALB Listener Rules (at least 7: 6 path-based + 1 default) ----
info "Test 07: Checking ALB listener rules..."
RULE_COUNT=0

# Get ALB ARN
ALB_ARN=$(awslocal elbv2 describe-load-balancers \
  --query 'LoadBalancers[0].LoadBalancerArn' --output text 2>/dev/null) || true

if [ -n "${ALB_ARN}" ] && [ "${ALB_ARN}" != "None" ] && [ "${ALB_ARN}" != "null" ]; then
  # Get listener ARN (HTTP listener on port 80)
  LISTENER_ARN=$(awslocal elbv2 describe-listeners \
    --load-balancer-arn "${ALB_ARN}" \
    --query 'Listeners[0].ListenerArn' --output text 2>/dev/null) || true

  if [ -n "${LISTENER_ARN}" ] && [ "${LISTENER_ARN}" != "None" ] && [ "${LISTENER_ARN}" != "null" ]; then
    RULES_OUTPUT=$(awslocal elbv2 describe-rules \
      --listener-arn "${LISTENER_ARN}" 2>/dev/null) || true
    RULE_COUNT=$(echo "${RULES_OUTPUT}" | jq '.Rules | length' 2>/dev/null) || true
    RULE_COUNT=${RULE_COUNT:-0}
  fi
fi

if [ "${RULE_COUNT}" -ge 7 ] 2>/dev/null; then
  test_passed 7 "ALB listener rules (${RULE_COUNT} rules, >= 7 required)"
else
  test_failed 7 "ALB listener rules" "Found ${RULE_COUNT} rules, expected >= 7 (6 path-based + 1 default)"
fi

# ---- Test 08: Security Groups (4 SGs) ----
info "Test 08: Checking security groups..."
SG_OUTPUT=""
SG_OUTPUT=$(awslocal ec2 describe-security-groups 2>/dev/null) || true
SG_NAMES=$(echo "${SG_OUTPUT}" | jq -r '.SecurityGroups[].GroupName' 2>/dev/null) || true

SG_EXPECTED=("${NAME_PREFIX}-alb-sg" "${NAME_PREFIX}-backend-sg" "${NAME_PREFIX}-frontend-sg" "${NAME_PREFIX}-rds-sg")
SG_FOUND_COUNT=0
SG_MISSING=""

for sg_name in "${SG_EXPECTED[@]}"; do
  if echo "${SG_NAMES}" | grep -q "^${sg_name}$"; then
    SG_FOUND_COUNT=$((SG_FOUND_COUNT + 1))
  else
    SG_MISSING="${SG_MISSING} ${sg_name}"
  fi
done

if [ "${SG_FOUND_COUNT}" -ge 4 ]; then
  test_passed 8 "Security groups (${SG_FOUND_COUNT}/4: alb-sg, backend-sg, frontend-sg, rds-sg)"
else
  test_failed 8 "Security groups" "Found ${SG_FOUND_COUNT}/4. Missing:${SG_MISSING}"
fi

# ---- Test 09: IAM Roles (3 roles) ----
info "Test 09: Checking IAM roles..."
IAM_ROLES=""
IAM_ROLES=$(awslocal iam list-roles \
  --query 'Roles[].RoleName' --output text 2>/dev/null) || true

ROLE_EXPECTED=("${NAME_PREFIX}-ecs-execution-role" "${NAME_PREFIX}-backend-task-role" "${NAME_PREFIX}-frontend-task-role")
ROLE_FOUND_COUNT=0
ROLE_MISSING=""

for role_name in "${ROLE_EXPECTED[@]}"; do
  if echo "${IAM_ROLES}" | grep -q "${role_name}"; then
    ROLE_FOUND_COUNT=$((ROLE_FOUND_COUNT + 1))
  else
    ROLE_MISSING="${ROLE_MISSING} ${role_name}"
  fi
done

if [ "${ROLE_FOUND_COUNT}" -ge 3 ]; then
  test_passed 9 "IAM roles (${ROLE_FOUND_COUNT}/3: execution, backend-task, frontend-task)"
else
  test_failed 9 "IAM roles" "Found ${ROLE_FOUND_COUNT}/3. Missing:${ROLE_MISSING}"
fi

# ---- Test 10: KMS Key (alias/splendidcrm-local-secrets) ----
info "Test 10: Checking KMS key alias..."
KMS_ALIASES=""
KMS_ALIASES=$(awslocal kms list-aliases \
  --query 'Aliases[].AliasName' --output text 2>/dev/null) || true

if echo "${KMS_ALIASES}" | grep -q "alias/${NAME_PREFIX}-secrets"; then
  test_passed 10 "KMS key alias (alias/${NAME_PREFIX}-secrets)"
else
  test_failed 10 "KMS key alias" "Expected alias/${NAME_PREFIX}-secrets not found"
fi

# ---- Test 11: Secrets Manager Secrets (6 secrets) ----
info "Test 11: Checking Secrets Manager secrets..."
SECRETS_OUTPUT=""
SECRETS_OUTPUT=$(awslocal secretsmanager list-secrets 2>/dev/null) || true
SECRET_NAMES=$(echo "${SECRETS_OUTPUT}" | jq -r '.SecretList[].Name' 2>/dev/null) || true

# Count secrets matching the name_prefix pattern (splendidcrm-local/*)
SECRET_MATCH_COUNT=0
if [ -n "${SECRET_NAMES}" ]; then
  SECRET_MATCH_COUNT=$(echo "${SECRET_NAMES}" | grep -c "^${NAME_PREFIX}/" 2>/dev/null) || true
fi

if [ "${SECRET_MATCH_COUNT}" -ge 6 ]; then
  test_passed 11 "Secrets Manager secrets (${SECRET_MATCH_COUNT} secrets matching ${NAME_PREFIX}/*)"
else
  test_failed 11 "Secrets Manager secrets" "Found ${SECRET_MATCH_COUNT} secrets matching ${NAME_PREFIX}/*, expected >= 6"
fi

# ---- Test 12: Parameter Store Parameters (8 parameters) ----
info "Test 12: Checking SSM Parameter Store parameters..."
SSM_OUTPUT=""
SSM_OUTPUT=$(awslocal ssm describe-parameters 2>/dev/null) || true
PARAM_NAMES=$(echo "${SSM_OUTPUT}" | jq -r '.Parameters[].Name' 2>/dev/null) || true

# Count parameters matching /splendidcrm/* pattern
PARAM_MATCH_COUNT=0
if [ -n "${PARAM_NAMES}" ]; then
  PARAM_MATCH_COUNT=$(echo "${PARAM_NAMES}" | grep -c "^/splendidcrm/" 2>/dev/null) || true
fi

if [ "${PARAM_MATCH_COUNT}" -ge 8 ]; then
  test_passed 12 "SSM parameters (${PARAM_MATCH_COUNT} parameters matching /splendidcrm/*)"
else
  test_failed 12 "SSM parameters" "Found ${PARAM_MATCH_COUNT} parameters matching /splendidcrm/*, expected >= 8"
fi

# ---- Test 13: RDS Instance ----
info "Test 13: Checking RDS instance..."
RDS_OUTPUT=""
RDS_OUTPUT=$(awslocal rds describe-db-instances 2>/dev/null) || true
RDS_IDENTIFIERS=$(echo "${RDS_OUTPUT}" | jq -r '.DBInstances[].DBInstanceIdentifier' 2>/dev/null) || true

RDS_FOUND=false
if [ -n "${RDS_IDENTIFIERS}" ]; then
  if echo "${RDS_IDENTIFIERS}" | grep -q "${NAME_PREFIX}"; then
    RDS_FOUND=true
  fi
fi

if [ "${RDS_FOUND}" = true ]; then
  RDS_STATUS=$(echo "${RDS_OUTPUT}" | jq -r ".DBInstances[] | select(.DBInstanceIdentifier | contains(\"${NAME_PREFIX}\")) | .DBInstanceStatus" 2>/dev/null) || true
  test_passed 13 "RDS instance (${NAME_PREFIX}-sqlserver) — status: ${RDS_STATUS:-unknown}"
else
  test_failed 13 "RDS instance" "No RDS instance found matching '${NAME_PREFIX}'"
fi

# ---- Test 14: CloudWatch Log Group ----
info "Test 14: Checking CloudWatch log group..."
LOG_GROUPS=""
LOG_GROUPS=$(awslocal logs describe-log-groups \
  --log-group-name-prefix "/application_logs" \
  --query 'logGroups[].logGroupName' --output text 2>/dev/null) || true

if echo "${LOG_GROUPS}" | grep -q "/application_logs/${NAME_PREFIX}"; then
  test_passed 14 "CloudWatch log group (/application_logs/${NAME_PREFIX})"
else
  test_failed 14 "CloudWatch log group" "Expected /application_logs/${NAME_PREFIX} not found"
fi

# ---- Test 15: CloudWatch Log Stream ----
info "Test 15: Checking CloudWatch log stream..."
LOG_STREAMS=""
LOG_STREAMS=$(awslocal logs describe-log-streams \
  --log-group-name "/application_logs/${NAME_PREFIX}" \
  --query 'logStreams[].logStreamName' --output text 2>/dev/null) || true

if echo "${LOG_STREAMS}" | grep -q "${NAME_PREFIX}-ecs-logs"; then
  test_passed 15 "CloudWatch log stream (${NAME_PREFIX}-ecs-logs)"
else
  test_failed 15 "CloudWatch log stream" "Expected ${NAME_PREFIX}-ecs-logs not found in /application_logs/${NAME_PREFIX}"
fi

echo ""
info "LocalStack resource verification complete: ${TESTS_PASSED} passed, ${TESTS_FAILED} failed out of 15."
echo ""

# =============================================================================
# Phase 5: Terraform Idempotency Check (not counted as a test)
# =============================================================================
# Verifies that a second terraform plan produces no changes. This ensures the
# infrastructure definition is idempotent — running apply twice creates the
# same state. Uses -detailed-exitcode:
#   Exit 0 = no changes (PASS)
#   Exit 1 = error
#   Exit 2 = changes detected (FAIL — not idempotent)

printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"
info "Phase 5: Terraform idempotency check..."
printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"

pushd "${TF_DIR}" > /dev/null

IDEMPOTENT_EXIT=0
set +e
terraform plan -input=false -detailed-exitcode -no-color > /tmp/tf_idempotent_output.txt 2>&1
IDEMPOTENT_EXIT=$?
set -e

if [ "${IDEMPOTENT_EXIT}" -eq 0 ]; then
  phase_status "PASS" "Terraform idempotency — no changes detected (exit code 0)"
elif [ "${IDEMPOTENT_EXIT}" -eq 2 ]; then
  phase_status "FAIL" "Terraform idempotency — changes detected (exit code 2)" \
    "Infrastructure is NOT idempotent. Review plan output for drift."
  warn "Idempotency plan output (first 20 lines):"
  head -20 /tmp/tf_idempotent_output.txt >&2
else
  phase_status "FAIL" "Terraform idempotency — plan error (exit code ${IDEMPOTENT_EXIT})" \
    "Terraform plan returned an unexpected error."
fi
rm -f /tmp/tf_idempotent_output.txt

popd > /dev/null
echo ""

# =============================================================================
# Phase 6: Terraform Destroy Verification (not counted as a test)
# =============================================================================
# Verifies that terraform destroy cleanly removes all resources without errors.
# This ensures the infrastructure can be fully torn down and reproduced.

printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"
info "Phase 6: Terraform destroy verification..."
printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"

pushd "${TF_DIR}" > /dev/null

DESTROY_EXIT=0
set +e
terraform destroy -input=false -auto-approve -no-color > /tmp/tf_destroy_output.txt 2>&1
DESTROY_EXIT=$?
set -e

if [ "${DESTROY_EXIT}" -eq 0 ]; then
  phase_status "PASS" "Terraform destroy — all resources removed successfully (exit code 0)"
else
  phase_status "FAIL" "Terraform destroy — failed (exit code ${DESTROY_EXIT})" \
    "Terraform destroy did not complete cleanly."
  warn "Destroy output (first 20 lines):"
  head -20 /tmp/tf_destroy_output.txt >&2
fi
rm -f /tmp/tf_destroy_output.txt

popd > /dev/null
echo ""

# =============================================================================
# Phase 7: Docker SQL Server Tests (Tests 16-19)
# =============================================================================
# Tests database schema provisioning and validation against a Docker SQL Server
# container. Uses scripts/deploy-schema.sh to create the database and execute
# Build.sql + SplendidSessions DDL, then validates schema object counts:
#   Test 16: Schema provisioning (deploy-schema.sh completes successfully)
#   Test 17: Table count >= 218
#   Test 18: View count >= 583
#   Test 19: Procedure count >= 890
#
# These tests require: Docker, sqlcmd, SA_PASSWORD, deploy-schema.sh
# If any prerequisite is missing, all 4 tests are marked as FAILED.

printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"
info "Phase 7: Docker SQL Server schema validation tests..."
printf '%b--------------------------------------------%b\n' "${BLUE}" "${NC}"

if [ "${SQLCMD_AVAILABLE}" = false ]; then
  warn "Skipping Docker SQL Server tests — prerequisites not met."
  warn "Ensure sqlcmd, SA_PASSWORD, and deploy-schema.sh are available."
  test_failed 16 "Docker SQL: Schema provisioning" "Prerequisites not met (sqlcmd/SA_PASSWORD/deploy-schema.sh missing)"
  test_failed 17 "Docker SQL: Table count >= 218" "Skipped — schema provisioning prerequisites not met"
  test_failed 18 "Docker SQL: View count >= 583" "Skipped — schema provisioning prerequisites not met"
  test_failed 19 "Docker SQL: Procedure count >= 890" "Skipped — schema provisioning prerequisites not met"
else
  # --- Determine SQL Server connectivity ---
  # Strategy: reuse existing container on port 1433 if available, otherwise start a new one
  SQL_HOST="localhost"
  SQL_PORT=1433
  SQL_USER="sa"

  # Check if the existing splendid-sql-express container is running
  if docker ps --format '{{.Names}}' 2>/dev/null | grep -q "^splendid-sql-express$"; then
    info "Reusing existing SQL Server container (splendid-sql-express) on port 1433."
  else
    # Check if port 1433 is available
    if lsof -i ":1433" &>/dev/null 2>&1 || ss -tlnp 2>/dev/null | grep -q ":1433 "; then
      SQL_PORT=${SQL_VALIDATION_PORT}
      info "Port 1433 is in use — will start validation container on port ${SQL_PORT}."
    fi

    info "Starting validation SQL Server container (${SQL_VALIDATION_CONTAINER}) on port ${SQL_PORT}..."
    # Remove any stale container with the same name
    docker rm -f "${SQL_VALIDATION_CONTAINER}" > /dev/null 2>&1 || true

    if docker run -d \
      --name "${SQL_VALIDATION_CONTAINER}" \
      -e "ACCEPT_EULA=Y" \
      -e "MSSQL_SA_PASSWORD=${SA_PASSWORD}" \
      -e "MSSQL_PID=Express" \
      -p "${SQL_PORT}:1433" \
      mcr.microsoft.com/mssql/server:2022-latest > /dev/null 2>&1; then
      SQL_STARTED_BY_SCRIPT=true
      success "Validation SQL Server container started on port ${SQL_PORT}."
    else
      warn "Failed to start validation SQL Server container."
      test_failed 16 "Docker SQL: Schema provisioning" "Could not start SQL Server container"
      test_failed 17 "Docker SQL: Table count >= 218" "Skipped — SQL Server container not available"
      test_failed 18 "Docker SQL: View count >= 583" "Skipped — SQL Server container not available"
      test_failed 19 "Docker SQL: Procedure count >= 890" "Skipped — SQL Server container not available"
      SQL_PORT=0  # Signal to skip
    fi
  fi

  if [ "${SQL_PORT}" -ne 0 ]; then
    # Wait for SQL Server readiness (up to 60 seconds)
    info "Waiting for SQL Server readiness on port ${SQL_PORT}..."
    SQL_READY=false
    for _attempt in $(seq 1 30); do
      if ${SQLCMD_PATH} -S "${SQL_HOST},${SQL_PORT}" -U "${SQL_USER}" -P "${SA_PASSWORD}" \
         -C -Q "SELECT 1" > /dev/null 2>&1; then
        SQL_READY=true
        break
      fi
      sleep 2
    done

    if [ "${SQL_READY}" = false ]; then
      warn "SQL Server did not become ready within 60 seconds."
      test_failed 16 "Docker SQL: Schema provisioning" "SQL Server readiness timeout on port ${SQL_PORT}"
      test_failed 17 "Docker SQL: Table count >= 218" "Skipped — SQL Server not ready"
      test_failed 18 "Docker SQL: View count >= 583" "Skipped — SQL Server not ready"
      test_failed 19 "Docker SQL: Procedure count >= 890" "Skipped — SQL Server not ready"
    else
      success "SQL Server is ready on port ${SQL_PORT}."

      # ---- Test 16: Schema Provisioning (deploy-schema.sh) ----
      info "Test 16: Running schema provisioning (deploy-schema.sh)..."
      DEPLOY_EXIT=0
      set +e
      DB_HOST="${SQL_HOST}" \
      DB_PORT="${SQL_PORT}" \
      DB_NAME="${SQL_VALIDATION_DB}" \
      SA_PASSWORD="${SA_PASSWORD}" \
      DB_USER="${SQL_USER}" \
        bash "${DEPLOY_SCHEMA_PATH}" > /tmp/deploy_schema_output.txt 2>&1
      DEPLOY_EXIT=$?
      set -e

      if [ "${DEPLOY_EXIT}" -eq 0 ]; then
        test_passed 16 "Docker SQL: Schema provisioning (deploy-schema.sh)"
      else
        test_failed 16 "Docker SQL: Schema provisioning" "deploy-schema.sh exited with code ${DEPLOY_EXIT}"
        warn "Deploy output (last 20 lines):"
        tail -20 /tmp/deploy_schema_output.txt >&2 2>/dev/null || true
      fi
      rm -f /tmp/deploy_schema_output.txt

      # ---- Test 17: Table Count >= 218 ----
      info "Test 17: Validating table count >= 218..."
      TABLE_COUNT=0
      TABLE_COUNT_RAW=""
      set +e
      TABLE_COUNT_RAW=$(${SQLCMD_PATH} \
        -S "${SQL_HOST},${SQL_PORT}" -U "${SQL_USER}" -P "${SA_PASSWORD}" -C \
        -d "${SQL_VALIDATION_DB}" \
        -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'" \
        -h -1 -W 2>/dev/null)
      set -e
      TABLE_COUNT=$(echo "${TABLE_COUNT_RAW}" | tr -d '[:space:]')

      if [ -n "${TABLE_COUNT}" ] && [ "${TABLE_COUNT}" -ge 218 ] 2>/dev/null; then
        test_passed 17 "Docker SQL: Table count (${TABLE_COUNT} >= 218)"
      else
        test_failed 17 "Docker SQL: Table count >= 218" "Found ${TABLE_COUNT:-<error>} tables, expected >= 218"
      fi

      # ---- Test 18: View Count >= 583 ----
      info "Test 18: Validating view count >= 583..."
      VIEW_COUNT=0
      VIEW_COUNT_RAW=""
      set +e
      VIEW_COUNT_RAW=$(${SQLCMD_PATH} \
        -S "${SQL_HOST},${SQL_PORT}" -U "${SQL_USER}" -P "${SA_PASSWORD}" -C \
        -d "${SQL_VALIDATION_DB}" \
        -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM INFORMATION_SCHEMA.VIEWS" \
        -h -1 -W 2>/dev/null)
      set -e
      VIEW_COUNT=$(echo "${VIEW_COUNT_RAW}" | tr -d '[:space:]')

      if [ -n "${VIEW_COUNT}" ] && [ "${VIEW_COUNT}" -ge 583 ] 2>/dev/null; then
        test_passed 18 "Docker SQL: View count (${VIEW_COUNT} >= 583)"
      else
        test_failed 18 "Docker SQL: View count >= 583" "Found ${VIEW_COUNT:-<error>} views, expected >= 583"
      fi

      # ---- Test 19: Procedure Count >= 890 ----
      info "Test 19: Validating procedure count >= 890..."
      PROC_COUNT=0
      PROC_COUNT_RAW=""
      set +e
      PROC_COUNT_RAW=$(${SQLCMD_PATH} \
        -S "${SQL_HOST},${SQL_PORT}" -U "${SQL_USER}" -P "${SA_PASSWORD}" -C \
        -d "${SQL_VALIDATION_DB}" \
        -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM INFORMATION_SCHEMA.ROUTINES WHERE ROUTINE_TYPE='PROCEDURE'" \
        -h -1 -W 2>/dev/null)
      set -e
      PROC_COUNT=$(echo "${PROC_COUNT_RAW}" | tr -d '[:space:]')

      if [ -n "${PROC_COUNT}" ] && [ "${PROC_COUNT}" -ge 890 ] 2>/dev/null; then
        test_passed 19 "Docker SQL: Procedure count (${PROC_COUNT} >= 890)"
      else
        test_failed 19 "Docker SQL: Procedure count >= 890" "Found ${PROC_COUNT:-<error>} procedures, expected >= 890"
      fi
    fi
  fi
fi

echo ""
info "Docker SQL Server tests complete."
echo ""

# =============================================================================
# Phase 8: Summary Report
# =============================================================================

printf '%b============================================%b\n' "${BLUE}" "${NC}"
printf '%b    INFRASTRUCTURE VALIDATION SUMMARY%b\n' "${BLUE}" "${NC}"
printf '%b============================================%b\n' "${BLUE}" "${NC}"
echo ""
printf "  Tests Passed : %b%d%b / %d\n" "${GREEN}" "${TESTS_PASSED}" "${NC}" "${TESTS_TOTAL}"
printf "  Tests Failed : %b%d%b / %d\n" "${RED}" "${TESTS_FAILED}" "${NC}" "${TESTS_TOTAL}"

# Calculate test coverage percentage
if [ "${TESTS_TOTAL}" -gt 0 ]; then
  PASS_PCT=$(( (TESTS_PASSED * 100) / TESTS_TOTAL ))
  printf "  Pass Rate    : %d%%\n" "${PASS_PCT}"
fi

echo ""

if [ "${TESTS_FAILED}" -eq 0 ] && [ "${TESTS_PASSED}" -eq "${TESTS_TOTAL}" ]; then
  printf '%b  ✓ ALL %d TESTS PASSED — Infrastructure validated successfully.%b\n' "${GREEN}" "${TESTS_TOTAL}" "${NC}"
  echo ""
  printf '%b============================================%b\n' "${BLUE}" "${NC}"
  echo ""
  exit 0
else
  printf '%b  ✗ %d TEST(S) FAILED — Review failures above and fix issues.%b\n' "${RED}" "${TESTS_FAILED}" "${NC}"
  echo ""
  printf '%b============================================%b\n' "${BLUE}" "${NC}"
  echo ""
  exit 1
fi
