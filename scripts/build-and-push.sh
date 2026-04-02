#!/usr/bin/env bash
# =============================================================================
# SplendidCRM CI/CD — Docker Image Build, Validate, and Push to ECR
# =============================================================================
#
# Purpose:  Builds both Docker images (backend and frontend), runs the full
#           local validation suite (all 12 tests), then authenticates with
#           AWS ECR, tags, and pushes both images. This is the primary CI/CD
#           image delivery script for the SplendidCRM containerization pipeline.
#
# Usage:    IMAGE_TAG=v1.0.0 AWS_ACCOUNT_ID=123456789012 ./scripts/build-and-push.sh
#           IMAGE_TAG=abc123 AWS_ACCOUNT_ID=123456789012 AWS_REGION=us-west-2 bash scripts/build-and-push.sh
#           ./scripts/build-and-push.sh --help
#
# Required Environment Variables:
#   IMAGE_TAG       — Docker image version tag (e.g., v1.0.0, latest, commit SHA).
#                     Applied to both backend and frontend images.
#   AWS_ACCOUNT_ID  — AWS account ID for constructing ECR registry URIs
#                     (e.g., 123456789012).
#
# Optional Environment Variables:
#   AWS_REGION      — AWS region for ECR (default: us-east-2, ACME standard).
#
# Options:
#   --help          — Print this usage information and exit.
#
# Phase Sequence:
#   1. Script header and utilities
#   2. Parameter parsing and validation
#   3. Prerequisites check (docker, aws, Dockerfiles)
#   4. Docker image build (backend + frontend)
#   5. Local validation (scripts/validate-docker-local.sh — all 12 tests MUST pass)
#   6. AWS ECR login
#   7. Tag and push to ECR
#   8. Verification (aws ecr describe-images)
#   9. Summary and exit
#
# ECR Repository Names (must match Terraform resource names):
#   splendidcrm-backend   — ASP.NET Core 10 backend (Kestrel, port 8080)
#   splendidcrm-frontend  — React 19 / Nginx frontend (port 80)
#
# Exit Codes:
#   0 — All phases completed successfully: build, validation, push, verify.
#   1 — Any phase failed (build error, validation failure, ECR push error, etc.).
#       set -euo pipefail ensures immediate abort on any command failure.
#
# Prerequisites:
#   - Docker Engine >= 20.0 (daemon must be running)
#   - AWS CLI v2 (for ECR authentication and verification)
#   - Dockerfile.backend and Dockerfile.frontend at repository root
#   - scripts/validate-docker-local.sh (12-test local validation suite)
#
# CRITICAL Constraints:
#   - Local validation is MANDATORY before push — no skip flag exists.
#   - All 12 local Docker validation tests must pass before any ECR push.
#   - IMAGE_TAG and AWS_ACCOUNT_ID are REQUIRED — no defaults provided.
#   - AWS_REGION defaults to us-east-2 (ACME standard region).
#   - ECR repository names match Terraform: splendidcrm-backend, splendidcrm-frontend.
#   - Images remain locally after push for debugging — no cleanup performed.
#
# Note: This script MUST be run from the repository root directory.
# =============================================================================

set -euo pipefail

# =============================================================================
# Color Constants and Utility Functions
# =============================================================================
# Terminal color codes for readable output — same pattern as build-and-run.sh
# for consistent developer experience across all SplendidCRM scripts.

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
# Phase 1: Usage / Help
# =============================================================================
# Display comprehensive usage information including all parameters, environment
# variables, and the phase sequence. Accessible via --help flag.

usage() {
  cat << 'HELPEOF'
SplendidCRM CI/CD — Docker Image Build, Validate, and Push to ECR

Usage:
  IMAGE_TAG=v1.0.0 AWS_ACCOUNT_ID=123456789012 ./scripts/build-and-push.sh
  IMAGE_TAG=abc123 AWS_ACCOUNT_ID=123456789012 AWS_REGION=us-west-2 bash scripts/build-and-push.sh
  ./scripts/build-and-push.sh --help

Required Environment Variables:
  IMAGE_TAG       Docker image version tag (e.g., v1.0.0, latest, commit SHA).
                  Applied to both backend and frontend images.

  AWS_ACCOUNT_ID  AWS account ID for constructing ECR registry URIs
                  (e.g., 123456789012).

Optional Environment Variables:
  AWS_REGION      AWS region for ECR (default: us-east-2, ACME standard).

Options:
  --help          Print this usage information and exit.

Phase Sequence:
  1. Validate parameters (IMAGE_TAG, AWS_ACCOUNT_ID)
  2. Check prerequisites (docker daemon, aws CLI, Dockerfiles)
  3. Build backend Docker image (Dockerfile.backend)
  4. Build frontend Docker image (Dockerfile.frontend)
  5. Run local validation suite (scripts/validate-docker-local.sh — 12 tests)
  6. Authenticate with AWS ECR
  7. Tag and push both images to ECR
  8. Verify pushed images exist in ECR
  9. Print summary with full ECR URIs

ECR Repository Names:
  splendidcrm-backend   ASP.NET Core 10 backend (Kestrel, port 8080)
  splendidcrm-frontend  React 19 / Nginx frontend (port 80)

CRITICAL:
  - ALL 12 local validation tests must pass before any ECR push.
  - Local validation is mandatory — there is no --skip-validation flag.
  - Images remain locally after push for debugging.

Examples:
  # Build, validate, and push with a semantic version tag
  IMAGE_TAG=v1.0.0 AWS_ACCOUNT_ID=123456789012 ./scripts/build-and-push.sh

  # Build, validate, and push with a commit SHA tag
  IMAGE_TAG=$(git rev-parse --short HEAD) AWS_ACCOUNT_ID=123456789012 ./scripts/build-and-push.sh

  # Use a different AWS region
  IMAGE_TAG=latest AWS_ACCOUNT_ID=123456789012 AWS_REGION=us-west-2 ./scripts/build-and-push.sh
HELPEOF
}

# =============================================================================
# Phase 2: Argument Parsing and Parameter Validation
# =============================================================================
# Parse command-line arguments (--help) BEFORE validating environment variables,
# so --help works even when IMAGE_TAG or AWS_ACCOUNT_ID are not set.

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

# Validate required environment variables using bash parameter expansion.
# ${VAR:?message} exits with code 1 and prints message if VAR is unset or empty.
readonly IMAGE_TAG="${IMAGE_TAG:?ERROR: IMAGE_TAG environment variable is required. Set it to the Docker image version tag (e.g., v1.0.0, latest, commit SHA).}"
readonly AWS_ACCOUNT_ID="${AWS_ACCOUNT_ID:?ERROR: AWS_ACCOUNT_ID environment variable is required. Set it to the 12-digit AWS account ID for ECR.}"

# Optional: AWS_REGION defaults to us-east-2 (ACME standard region).
readonly AWS_REGION="${AWS_REGION:-us-east-2}"

# Construct the ECR registry URL from account ID and region.
# Format: <account-id>.dkr.ecr.<region>.amazonaws.com
readonly ECR_REGISTRY="${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com"

# ECR repository names — must match Terraform resource names in
# infrastructure/modules/common/ecr.tf to ensure tag/push targets the
# correct repositories provisioned by Terraform.
readonly BACKEND_REPO="splendidcrm-backend"
readonly FRONTEND_REPO="splendidcrm-frontend"

# Local image names (local tag applied during docker build)
readonly BACKEND_LOCAL_TAG="${BACKEND_REPO}:${IMAGE_TAG}"
readonly FRONTEND_LOCAL_TAG="${FRONTEND_REPO}:${IMAGE_TAG}"

# Full ECR image URIs (used for docker tag and docker push)
readonly BACKEND_ECR_URI="${ECR_REGISTRY}/${BACKEND_REPO}:${IMAGE_TAG}"
readonly FRONTEND_ECR_URI="${ECR_REGISTRY}/${FRONTEND_REPO}:${IMAGE_TAG}"

# ECR image URIs with "latest" tag — pushed alongside IMAGE_TAG per CI/CD
# convention. The latest tag provides a stable reference for manual pulls
# and quick rollback identification while IMAGE_TAG provides immutable
# version-specific references for ECS task definitions.
readonly BACKEND_ECR_LATEST="${ECR_REGISTRY}/${BACKEND_REPO}:latest"
readonly FRONTEND_ECR_LATEST="${ECR_REGISTRY}/${FRONTEND_REPO}:latest"

# =============================================================================
# Script Banner
# =============================================================================

echo ""
printf '%b================================================%b\n' "${BLUE}" "${NC}"
printf '%b SplendidCRM CI/CD — Build, Validate & Push%b\n' "${BLUE}" "${NC}"
printf '%b================================================%b\n' "${BLUE}" "${NC}"
echo ""

info "Configuration:"
info "  IMAGE_TAG:      ${IMAGE_TAG}"
info "  AWS_ACCOUNT_ID: ${AWS_ACCOUNT_ID}"
info "  AWS_REGION:     ${AWS_REGION}"
info "  ECR_REGISTRY:   ${ECR_REGISTRY}"
info "  Backend image:  ${BACKEND_ECR_URI}"
info "  Frontend image: ${FRONTEND_ECR_URI}"
echo ""

# =============================================================================
# Phase 3: Prerequisites Check
# =============================================================================
# Verify all required tools are installed and the Docker daemon is running
# before attempting any build or push operations.

info "Phase 3: Checking prerequisites..."

# ---- Docker CLI ----
if ! command -v docker &>/dev/null; then
  error "Docker is not installed or not on PATH. Install Docker Engine >= 20.0 and try again."
fi

# ---- Docker Daemon ----
# Verify the Docker daemon is running by executing 'docker info'. If the daemon
# is not running, 'docker info' exits with a non-zero code. The >/dev/null
# redirects suppress the verbose system information output.
if ! docker info >/dev/null 2>&1; then
  error "Docker daemon is not running. Start the Docker daemon and try again."
fi
success "Docker daemon is running."

# ---- AWS CLI ----
if ! command -v aws &>/dev/null; then
  error "AWS CLI is not installed or not on PATH. Install AWS CLI v2 and try again."
fi

# Verify AWS CLI version (v2 required for ecr get-login-password).
# aws --version outputs: "aws-cli/2.x.y Python/3.x.y ..." or "aws-cli/1.x.y ...".
# We check for major version 2 or higher.
AWS_CLI_VERSION_OUTPUT=$(aws --version 2>&1)
if ! echo "${AWS_CLI_VERSION_OUTPUT}" | grep -qE 'aws-cli/[2-9]'; then
  warn "AWS CLI v2 recommended. Detected: ${AWS_CLI_VERSION_OUTPUT}"
  info "Continuing — aws ecr get-login-password may not be available in v1."
fi
success "AWS CLI is available."

# ---- Dockerfile Existence ----
# Both Dockerfiles must exist at the repository root (the script's build context).
if [ ! -f "Dockerfile.backend" ]; then
  error "Dockerfile.backend not found in repository root. Ensure you are running from the repo root."
fi

if [ ! -f "Dockerfile.frontend" ]; then
  error "Dockerfile.frontend not found in repository root. Ensure you are running from the repo root."
fi
success "Dockerfile.backend and Dockerfile.frontend found."

# ---- Validation Script Existence ----
if [ ! -f "scripts/validate-docker-local.sh" ]; then
  error "scripts/validate-docker-local.sh not found. This script is required for mandatory pre-push validation."
fi
success "scripts/validate-docker-local.sh found."

success "All prerequisites satisfied."
echo ""

# =============================================================================
# Phase 4: Docker Image Build
# =============================================================================
# Build both images using the repository root as the Docker build context.
# Each image is tagged with the provided IMAGE_TAG for consistent versioning.
# Build failures are fatal — set -e aborts the script immediately.

info "Phase 4: Building Docker images..."
echo ""

# ---- Backend Image Build ----
info "Building backend image: docker build -f Dockerfile.backend -t ${BACKEND_LOCAL_TAG} ."
printf '%b──────────────────────────────────────────────%b\n' "${BLUE}" "${NC}"

docker build -f Dockerfile.backend -t "${BACKEND_LOCAL_TAG}" .

printf '%b──────────────────────────────────────────────%b\n' "${BLUE}" "${NC}"
success "Backend image built successfully: ${BACKEND_LOCAL_TAG}"
echo ""

# ---- Frontend Image Build ----
info "Building frontend image: docker build -f Dockerfile.frontend -t ${FRONTEND_LOCAL_TAG} ."
printf '%b──────────────────────────────────────────────%b\n' "${BLUE}" "${NC}"

docker build -f Dockerfile.frontend -t "${FRONTEND_LOCAL_TAG}" .

printf '%b──────────────────────────────────────────────%b\n' "${BLUE}" "${NC}"
success "Frontend image built successfully: ${FRONTEND_LOCAL_TAG}"
echo ""

# ---- Print Image Sizes ----
# Retrieve image sizes in bytes via docker image inspect and convert to
# human-readable megabytes for quick visual verification against size targets
# (backend ≤ 500MB, frontend ≤ 100MB).
BACKEND_SIZE_BYTES=$(docker image inspect "${BACKEND_LOCAL_TAG}" --format '{{.Size}}')
FRONTEND_SIZE_BYTES=$(docker image inspect "${FRONTEND_LOCAL_TAG}" --format '{{.Size}}')

BACKEND_SIZE_MB=$((BACKEND_SIZE_BYTES / 1024 / 1024))
FRONTEND_SIZE_MB=$((FRONTEND_SIZE_BYTES / 1024 / 1024))

info "Image sizes:"
info "  Backend:  ${BACKEND_SIZE_MB}MB (target: ≤500MB)"
info "  Frontend: ${FRONTEND_SIZE_MB}MB (target: ≤100MB)"

success "Both Docker images built successfully."
echo ""

# =============================================================================
# Phase 5: Local Validation (MANDATORY before push)
# =============================================================================
# Run the full 12-test local Docker validation suite. ALL tests must pass
# before any ECR push is permitted. This is a non-negotiable quality gate
# per AAP §0.8.2 — there is no --skip-validation flag.
#
# The validate-docker-local.sh script:
#   - Exit 0: All 12 tests passed → proceed to ECR push
#   - Exit 1: One or more tests failed → abort (do NOT push to ECR)

info "Phase 5: Running local Docker validation suite (12 tests)..."
printf '%b══════════════════════════════════════════════%b\n' "${BLUE}" "${NC}"

if bash scripts/validate-docker-local.sh; then
  printf '%b══════════════════════════════════════════════%b\n' "${BLUE}" "${NC}"
  success "All local validation tests passed. Proceeding to ECR push."
else
  printf '%b══════════════════════════════════════════════%b\n' "${BLUE}" "${NC}"
  error "Local validation FAILED. One or more of the 12 tests did not pass. Fix issues before pushing to ECR."
fi
echo ""

# =============================================================================
# Phase 6: AWS ECR Login
# =============================================================================
# Authenticate Docker with the ECR registry using the aws ecr get-login-password
# command. This retrieves a temporary authentication token valid for 12 hours
# and pipes it to docker login. The --username AWS is required for ECR.

info "Phase 6: Authenticating with AWS ECR..."
info "  Registry: ${ECR_REGISTRY}"

if aws ecr get-login-password --region "${AWS_REGION}" | docker login --username AWS --password-stdin "${ECR_REGISTRY}"; then
  success "ECR authentication successful."
else
  error "ECR authentication failed. Verify AWS credentials and account ID."
fi
echo ""

# =============================================================================
# Phase 7: Tag and Push
# =============================================================================
# Tag both locally-built images with their full ECR URIs, then push to ECR.
# ECR repository names must match Terraform-provisioned resources:
#   splendidcrm-backend  → infrastructure/modules/common/ecr.tf
#   splendidcrm-frontend → infrastructure/modules/common/ecr.tf

info "Phase 7: Tagging and pushing images to ECR..."
echo ""

# ---- Backend: Tag and Push (IMAGE_TAG + latest) ----
info "Tagging backend: ${BACKEND_LOCAL_TAG} → ${BACKEND_ECR_URI}"
docker tag "${BACKEND_LOCAL_TAG}" "${BACKEND_ECR_URI}"
info "Tagging backend: ${BACKEND_LOCAL_TAG} → ${BACKEND_ECR_LATEST}"
docker tag "${BACKEND_LOCAL_TAG}" "${BACKEND_ECR_LATEST}"
success "Backend image tagged (${IMAGE_TAG} + latest)."

info "Pushing backend: ${BACKEND_ECR_URI}"
docker push "${BACKEND_ECR_URI}"
info "Pushing backend: ${BACKEND_ECR_LATEST}"
docker push "${BACKEND_ECR_LATEST}"
success "Backend image pushed to ECR (${IMAGE_TAG} + latest)."
echo ""

# ---- Frontend: Tag and Push (IMAGE_TAG + latest) ----
info "Tagging frontend: ${FRONTEND_LOCAL_TAG} → ${FRONTEND_ECR_URI}"
docker tag "${FRONTEND_LOCAL_TAG}" "${FRONTEND_ECR_URI}"
info "Tagging frontend: ${FRONTEND_LOCAL_TAG} → ${FRONTEND_ECR_LATEST}"
docker tag "${FRONTEND_LOCAL_TAG}" "${FRONTEND_ECR_LATEST}"
success "Frontend image tagged (${IMAGE_TAG} + latest)."

info "Pushing frontend: ${FRONTEND_ECR_URI}"
docker push "${FRONTEND_ECR_URI}"
info "Pushing frontend: ${FRONTEND_ECR_LATEST}"
docker push "${FRONTEND_ECR_LATEST}"
success "Frontend image pushed to ECR (${IMAGE_TAG} + latest)."
echo ""

success "Both images pushed to ECR (${IMAGE_TAG} + latest)."
echo ""

# =============================================================================
# Phase 8: Verification
# =============================================================================
# Verify that pushed images exist in ECR by querying the repository metadata.
# aws ecr describe-images returns image details if the tag exists, or exits
# with a non-zero code if the image is not found.

info "Phase 8: Verifying pushed images in ECR..."

# ---- Backend Verification (IMAGE_TAG) ----
info "Verifying backend image in ECR: ${BACKEND_REPO}:${IMAGE_TAG}"
if aws ecr describe-images \
    --repository-name "${BACKEND_REPO}" \
    --image-ids imageTag="${IMAGE_TAG}" \
    --region "${AWS_REGION}" >/dev/null 2>&1; then
  success "Backend image verified in ECR: ${BACKEND_ECR_URI}"
else
  error "Backend image NOT found in ECR after push. Verify repository '${BACKEND_REPO}' exists and push succeeded."
fi

# ---- Backend Verification (latest) ----
info "Verifying backend image in ECR: ${BACKEND_REPO}:latest"
if aws ecr describe-images \
    --repository-name "${BACKEND_REPO}" \
    --image-ids imageTag="latest" \
    --region "${AWS_REGION}" >/dev/null 2>&1; then
  success "Backend latest tag verified in ECR: ${BACKEND_ECR_LATEST}"
else
  error "Backend 'latest' tag NOT found in ECR after push. Verify repository '${BACKEND_REPO}' exists and push succeeded."
fi

# ---- Frontend Verification (IMAGE_TAG) ----
info "Verifying frontend image in ECR: ${FRONTEND_REPO}:${IMAGE_TAG}"
if aws ecr describe-images \
    --repository-name "${FRONTEND_REPO}" \
    --image-ids imageTag="${IMAGE_TAG}" \
    --region "${AWS_REGION}" >/dev/null 2>&1; then
  success "Frontend image verified in ECR: ${FRONTEND_ECR_URI}"
else
  error "Frontend image NOT found in ECR after push. Verify repository '${FRONTEND_REPO}' exists and push succeeded."
fi

# ---- Frontend Verification (latest) ----
info "Verifying frontend image in ECR: ${FRONTEND_REPO}:latest"
if aws ecr describe-images \
    --repository-name "${FRONTEND_REPO}" \
    --image-ids imageTag="latest" \
    --region "${AWS_REGION}" >/dev/null 2>&1; then
  success "Frontend latest tag verified in ECR: ${FRONTEND_ECR_LATEST}"
else
  error "Frontend 'latest' tag NOT found in ECR after push. Verify repository '${FRONTEND_REPO}' exists and push succeeded."
fi

echo ""
success "Both images verified in ECR (${IMAGE_TAG} + latest)."
echo ""

# =============================================================================
# Phase 9: Summary and Exit
# =============================================================================
# Print a final summary with the full ECR URIs for both images. These URIs
# are used in ECS task definitions and CI/CD pipelines to deploy containers.

printf '%b================================================%b\n' "${GREEN}" "${NC}"
printf '%b  BUILD, VALIDATE & PUSH COMPLETE%b\n' "${GREEN}" "${NC}"
printf '%b================================================%b\n' "${GREEN}" "${NC}"
echo ""
info "Pushed images:"
info "  Backend:  ${BACKEND_ECR_URI}"
info "  Backend:  ${BACKEND_ECR_LATEST}"
info "  Frontend: ${FRONTEND_ECR_URI}"
info "  Frontend: ${FRONTEND_ECR_LATEST}"
echo ""
info "Image sizes:"
info "  Backend:  ${BACKEND_SIZE_MB}MB"
info "  Frontend: ${FRONTEND_SIZE_MB}MB"
echo ""
info "Next steps:"
info "  1. Update ECS task definitions with image tag '${IMAGE_TAG}'"
info "  2. Deploy via: terraform apply -var=\"image_tag=${IMAGE_TAG}\""
info "  3. Monitor deployment in CloudWatch log group /application_logs"
echo ""
success "CI/CD pipeline completed successfully."
exit 0
