# -----------------------------------------------------------------------------
# SplendidCRM ECS Fargate — LocalStack Validation Environment Variable Values
# -----------------------------------------------------------------------------
# LocalStack-specific Terraform variable values for the SplendidCRM ECS Fargate
# deployment. The .auto.tfvars suffix means Terraform automatically loads
# these values — no -var-file flag is needed.
#
# This environment is used exclusively for autonomous Terraform validation
# against LocalStack Pro. It validates resource creation, idempotency, and
# clean teardown before any real AWS deployment (dev/staging/prod).
#
# Values NOT set here (by design):
#   - ECS sizing (task_cpu, task_memory, etc.)    → locals.tf
#   - Network (vpc_id, vpc_cidr, subnet IDs)      → data.tf data sources
#   - RDS sizing (instance_class, storage)         → locals.tf
#   - Log retention                                → locals.tf
# -----------------------------------------------------------------------------

# =============================================================================
# 1. Core Identity
# =============================================================================

# ACME naming convention: {app}-{env_short}, using "local" for LocalStack
name_prefix = "splendidcrm-local"

# Must match validation constraint in variables.tf (dev/staging/prod/localstack)
environment = "localstack"

# Standard LocalStack account ID (12 zeros) — required by LocalStack for valid
# ARN construction. Real AWS account IDs are NEVER committed to version control.
account_id = "000000000000" # Standard LocalStack account ID

# =============================================================================
# 2. Container Configuration
# =============================================================================

# CRITICAL — Guardrail G2 (Port Consistency):
# This value MUST remain 8080 to maintain consistency across all five locations:
#   1. Dockerfile.backend  → ENV ASPNETCORE_URLS=http://+:8080 / EXPOSE 8080
#   2. ecs-fargate.tf      → containerPort 8080
#   3. alb.tf              → backend target group port 8080
#   4. security-groups.tf  → ALB → backend inbound rule port 8080
#   5. This file           → container_port = 8080
container_port = 8080 # G2: Must be 8080 across Dockerfile, ECS, ALB, SG

# Docker image tag for ECR. "latest" is appropriate for LocalStack validation;
# CI/CD pipelines override with git SHA or semantic version for traceability.
image_tag = "latest"

# =============================================================================
# 3. ACME FinOps & Operations Tags
# =============================================================================

# ACME finops:portfolio tag — identifies the business portfolio for cost allocation
portfolio = "CRM"

# ACME finops:cost_center tag — identifies the department for chargeback reporting
cost_center = "IT"

# ACME ops:owner tag — not required for LocalStack validation.
# Operator fills with team email for real AWS environments.
owner_email = ""

# =============================================================================
# 4. TLS / Certificate
# =============================================================================

# ACM certificate ARN for the HTTPS listener on the ALB.
# Empty string disables the HTTPS listener — LocalStack validation uses HTTP only.
certificate_arn = ""
