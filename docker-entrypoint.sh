#!/bin/sh
# =============================================================================
# docker-entrypoint.sh — Runtime config.json Generation for Frontend Container
# =============================================================================
#
# This script is the ENTRYPOINT for the SplendidCRM frontend Docker container.
# It generates /usr/share/nginx/html/config.json from environment variables
# at container startup, then starts Nginx in the foreground.
#
# The same Docker image deploys to every environment (dev, staging, production).
# All behavioral differences are injected via ECS task definition environment
# variables — no image rebuild is required for configuration changes.
#
# config.json is consumed by config-loader.js (loaded synchronously via XHR
# in index.html before React initialization) and populates
# window.__SPLENDID_CONFIG__ which is read by src/config.ts.
#
# Environment Variables (injected by ECS task definition or docker run -e):
#   API_BASE_URL  — Backend API origin. Empty string = same-origin (ALB).
#                   CRITICAL (G8): Must be empty when both frontend and backend
#                   are behind the same ALB to preserve cookie-based session
#                   authentication.
#   SIGNALR_URL   — SignalR hub base URL. Empty string = falls back to
#                   API_BASE_URL in the frontend config.ts module.
#   ENVIRONMENT   — Environment identifier shown in UI/logs. One of:
#                   "development", "staging", "production".
#
# Constraints:
#   - Alpine ash compatible: no bashisms (no [[ ]], no arrays, no process
#     substitution). Uses only POSIX sh features.
#   - config.json MUST be fully written before Nginx starts serving requests.
#     The sequential cat → exec nginx ensures this ordering.
#   - The Dockerfile.frontend MUST chmod +x this script and use:
#       ENTRYPOINT ["./docker-entrypoint.sh"]
#     ENTRYPOINT (not CMD) ensures the script always runs, even if CMD is
#     overridden at runtime.
#
# Reference:
#   - nginx.conf: defines document root /usr/share/nginx/html and
#     config.json no-cache location block
#   - SplendidCRM/React/public/config-loader.js: synchronous XHR fetch
#   - SplendidCRM/React/src/config.ts: AppConfig interface (3 fields)
#   - SplendidCRM/React/public/config.json: development default template
# =============================================================================

set -e

# ---------------------------------------------------------------------------
# Phase 1: Read environment variables with POSIX-compatible defaults
# ---------------------------------------------------------------------------
# ${VAR:-default} is POSIX sh compatible (works in Alpine ash, dash, bash).
#
# API_BASE_URL defaults to empty string — this means same-origin requests.
# When both frontend and backend are behind the same ALB, the browser sends
# API requests to the same hostname, preserving cookie-based authentication
# without CORS preflight requests (Guardrail G8).
#
# SIGNALR_URL defaults to empty string — the frontend config.ts falls back
# to API_BASE_URL when SIGNALR_URL is empty (see config.ts line 117).
#
# ENVIRONMENT defaults to "production" — the safe default ensures that if
# this variable is accidentally omitted from an ECS task definition, the
# container behaves conservatively (production-mode logging, no debug
# features). In practice, ECS always injects ENVIRONMENT explicitly via
# the task definition environment block, so this default only applies to
# local Docker runs where -e ENVIRONMENT is not specified.
# ---------------------------------------------------------------------------
API_BASE_URL="${API_BASE_URL:-}"
SIGNALR_URL="${SIGNALR_URL:-}"
ENVIRONMENT="${ENVIRONMENT:-production}"

# ---------------------------------------------------------------------------
# Phase 1b: JSON string escaping function
# ---------------------------------------------------------------------------
# Escape special characters in environment variable values so the generated
# config.json is always valid JSON regardless of input content.
#
# Handles the JSON special characters defined in RFC 8259 §7:
#   - Backslash (\)    → \\   (must be first to avoid double-escaping)
#   - Double quote (")  → \"
#   - Forward slash (/) → \/   (optional per RFC, but defense-in-depth for
#                                <script> tag injection in HTML contexts)
#   - Tab               → \t
#   - Carriage return    → \r
#
# Uses sed for POSIX sh compatibility (Alpine ash, dash, bash). The printf
# '%s' ensures no trailing newline is added to the value.
#
# NOTE: Newlines within environment variable values are extremely rare in
# Docker/ECS contexts (URL values, hostnames, environment names). If
# multi-line values are ever needed, this function handles them via sed's
# line-by-line processing — each line's special chars are escaped correctly.
# ---------------------------------------------------------------------------
json_escape() {
  printf '%s' "$1" | sed \
    -e 's/\\/\\\\/g' \
    -e 's/"/\\"/g' \
    -e 's/\//\\\//g' \
    -e 's/	/\\t/g'
}

# ---------------------------------------------------------------------------
# Phase 2: Generate config.json at the Nginx document root
# ---------------------------------------------------------------------------
# The JSON structure MUST exactly match the AppConfig interface in config.ts:
#   - API_BASE_URL (string): Backend API base URL
#   - SIGNALR_URL  (string): SignalR hub base URL
#   - ENVIRONMENT  (string): Environment identifier
#
# Written to /usr/share/nginx/html/config.json — the same directory where
# Vite's dist/ output is served by Nginx (see nginx.conf root directive).
#
# The nginx.conf config.json location block serves this file with no-cache
# headers (Cache-Control: no-cache, no-store, must-revalidate) so browsers
# always fetch the latest configuration on page reload.
#
# All values are passed through json_escape() to ensure the output is always
# valid JSON, even if environment variables contain double quotes, backslashes,
# or other JSON-special characters. printf '%s' is used (instead of a heredoc)
# to avoid shell expansion issues with special characters in escaped values.
# ---------------------------------------------------------------------------
printf '{\n  "API_BASE_URL": "%s",\n  "SIGNALR_URL": "%s",\n  "ENVIRONMENT": "%s"\n}\n' \
  "$(json_escape "$API_BASE_URL")" \
  "$(json_escape "$SIGNALR_URL")" \
  "$(json_escape "$ENVIRONMENT")" \
  > /usr/share/nginx/html/config.json

# ---------------------------------------------------------------------------
# Phase 3: Log configuration for CloudWatch troubleshooting
# ---------------------------------------------------------------------------
# Printed to stdout so Docker/ECS captures it in CloudWatch Logs.
# Intentionally logs the full config.json content — these are non-sensitive
# values (URLs and environment name). Secrets (connection strings, API keys)
# are handled by the backend container via AWS Secrets Manager, not here.
# ---------------------------------------------------------------------------
echo "[docker-entrypoint] Generated /usr/share/nginx/html/config.json:"
echo "  API_BASE_URL=${API_BASE_URL}"
echo "  SIGNALR_URL=${SIGNALR_URL}"
echo "  ENVIRONMENT=${ENVIRONMENT}"

# ---------------------------------------------------------------------------
# Phase 4: Start Nginx in the foreground
# ---------------------------------------------------------------------------
# exec replaces the current shell process with Nginx. This is critical for
# Docker containers because:
#   1. Nginx becomes PID 1, receiving SIGTERM directly from Docker/ECS for
#      graceful shutdown (without exec, the shell would receive SIGTERM and
#      Nginx would be orphaned).
#   2. Docker health checks and ECS task lifecycle management target PID 1.
#
# "daemon off" keeps Nginx in the foreground — Docker containers must have
# a foreground process or they exit immediately. The semicolon after
# "daemon off" is required Nginx configuration syntax.
# ---------------------------------------------------------------------------
exec nginx -g 'daemon off;'
