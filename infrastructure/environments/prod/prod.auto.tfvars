# -----------------------------------------------------------------------------
# SplendidCRM ECS Fargate — Production Environment Variable Values
# -----------------------------------------------------------------------------
# Production-specific Terraform variable values for the SplendidCRM ECS Fargate
# deployment. The .auto.tfvars suffix means Terraform automatically loads
# these values — no -var-file flag is needed.
#
# ⚠️  PRODUCTION ENVIRONMENT — changes to this file impact live users.
# All modifications must be reviewed and approved before apply.
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

# ACME naming convention: {app}-{env}
name_prefix = "splendidcrm-prod"

# Must match validation constraint in variables.tf (dev/staging/prod/localstack)
environment = "prod"

# AWS account ID — operator MUST fill before first apply.
# NEVER hardcode real account IDs in version control.
account_id = ""

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
container_port = 8080

# Docker image tag for ECR. Defaults to "latest" for initial setup; CI/CD
# pipelines MUST override with git SHA (e.g., "abc1234") or semantic version
# for production traceability and rollback capability.
image_tag = "latest"

# =============================================================================
# 3. ACME FinOps & Operations Tags
# =============================================================================

# ACME finops:portfolio tag — identifies the business portfolio for cost allocation
portfolio = "CRM"

# ACME finops:cost_center tag — identifies the department for chargeback reporting
cost_center = "IT"

# ACME ops:owner tag — operator MUST fill with the team distribution email
# before first apply (e.g., "crm-engineering@acme.com")
owner_email = ""

# =============================================================================
# 4. TLS / Certificate
# =============================================================================

# ACM certificate ARN for the HTTPS listener on the ALB.
# ⚠️  PRODUCTION: This MUST be set to a valid ACM certificate ARN before
# first apply. Production traffic requires TLS termination at the ALB.
certificate_arn = ""
