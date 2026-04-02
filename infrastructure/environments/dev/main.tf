# =============================================================================
# SplendidCRM ECS Fargate — Dev Environment Root Module
# =============================================================================
#
# Purpose:
#   Root module instantiation file for the DEV environment. This file calls
#   the shared common module (../../modules/common) and passes all 22 required
#   variables sourced from variables.tf, data.tf, and locals.tf.
#
# Architecture:
#   infrastructure/environments/dev/
#     ├── versions.tf     — Terraform Cloud backend + AWS provider config
#     ├── variables.tf    — Input variable definitions (populated by .auto.tfvars)
#     ├── data.tf         — VPC and subnet tag-based data source lookups
#     ├── locals.tf       — Dev-specific sizing constants (ECS, RDS, observability)
#     ├── main.tf         — THIS FILE: module instantiation + output passthrough
#     └── dev.auto.tfvars — Dev-specific variable values
#
# Variable Source Mapping (22 total):
#   var.*   → variables.tf  (9 variables from tfvars)
#   local.* → locals.tf     (9 sizing constants)
#   data.*  → data.tf       (4 networking values from 3 data sources)
#
# Cross-File Dependencies:
#   - Port 8080 flows through var.container_port → module.common.container_port
#     to maintain Guardrail G2 consistency across Dockerfile, ECS, ALB, and SG
#   - Secrets Manager ARNs (G7) are handled inside the common module
#   - Same-origin cookie architecture (G8) is enforced by the single ALB
#
# ACME Compliance:
#   - No tfe.acme.com module sources (G10) — uses local relative path only
#   - No provider blocks — provider configuration is in versions.tf
#   - No hardcoded values — every value comes from var.*, local.*, or data.*
#   - All 10 required outputs are passed through for CI/CD and operational use
# =============================================================================

# -----------------------------------------------------------------------------
# Common Module Instantiation
# -----------------------------------------------------------------------------
# Calls the shared module that provisions ALL AWS resources:
#   - ECR repositories (backend + frontend)
#   - Security groups (ALB, backend, frontend, RDS)
#   - Internal ALB with 7 path-based listener rules
#   - IAM roles (execution, backend task, frontend task)
#   - ECS Fargate cluster, task definitions, and services
#   - KMS Customer Managed Key for Secrets Manager encryption
#   - RDS SQL Server instance
#   - Secrets Manager (6 secrets) + Parameter Store (8 parameters)
#   - CloudWatch log group and stream
#
# The module source is a relative path — NOT a remote Terraform registry
# reference. This ensures LocalStack compatibility and avoids dependency
# on tfe.acme.com during autonomous development (Guardrail G10).
# -----------------------------------------------------------------------------
module "common" {
  source = "../../modules/common"

  # ---------------------------------------------------------------------------
  # Identity & Configuration Variables (from variables.tf → dev.auto.tfvars)
  # ---------------------------------------------------------------------------
  # These identify the deployment and control naming, tagging, and image
  # selection across all resources in the common module.
  # ---------------------------------------------------------------------------
  name_prefix    = var.name_prefix
  environment    = var.environment
  account_id     = var.account_id
  container_port = var.container_port # G2: Must be 8080 — flows to ECS, ALB, SG
  image_tag      = var.image_tag

  # ---------------------------------------------------------------------------
  # ECS Fargate Task Sizing (from locals.tf — Dev Tier)
  # ---------------------------------------------------------------------------
  # Dev sizing: 512 CPU, 1024 MB memory, 21 GB ephemeral, 1-4 tasks
  # These values are uniform for both backend and frontend tasks per ACME
  # standard. Production environments use larger values defined in their
  # respective locals.tf files.
  # ---------------------------------------------------------------------------
  task_cpu          = local.task_cpu
  task_memory       = local.task_memory
  task_ephemeral_gb = local.task_ephemeral_gb
  min_tasks         = local.min_tasks
  max_tasks         = local.max_tasks

  # ---------------------------------------------------------------------------
  # Networking Variables (from data.tf — tag-based VPC/subnet discovery)
  # ---------------------------------------------------------------------------
  # VPC and subnets are discovered dynamically via AWS Name tags per ACME
  # standards. No hardcoded VPC or subnet IDs are used anywhere.
  #   - VPC: tag "acme-dev-vpc-use2"
  #   - App subnets: tag wildcard "*-app-*" (ECS tasks + ALB)
  #   - DB subnets: tag wildcard "*-db-*" (RDS private isolation)
  # ---------------------------------------------------------------------------
  vpc_id         = data.aws_vpc.main.id
  vpc_cidr       = data.aws_vpc.main.cidr_block
  app_subnet_ids = data.aws_subnets.app.ids
  db_subnet_ids  = data.aws_subnets.db.ids

  # ---------------------------------------------------------------------------
  # RDS SQL Server Sizing (from locals.tf — Dev Tier)
  # ---------------------------------------------------------------------------
  # Cost-effective instance sizing for development workloads:
  #   - db.t3.medium: 2 vCPU, 4 GB RAM — sufficient for dev SQL Server
  #   - 20 GB initial storage with autoscaling up to 100 GB
  # ---------------------------------------------------------------------------
  rds_instance_class        = local.rds_instance_class
  rds_allocated_storage     = local.rds_allocated_storage
  rds_max_allocated_storage = local.rds_max_allocated_storage

  # ---------------------------------------------------------------------------
  # ACME Tag Variables (from variables.tf → dev.auto.tfvars)
  # ---------------------------------------------------------------------------
  # Applied to resources via the common module's resource-level tags,
  # supplementing the provider-level default_tags in versions.tf.
  # ---------------------------------------------------------------------------
  portfolio   = var.portfolio
  cost_center = var.cost_center
  owner_email = var.owner_email

  # ---------------------------------------------------------------------------
  # Observability Configuration (from locals.tf)
  # ---------------------------------------------------------------------------
  # 30-day CloudWatch log retention for dev — shorter than staging (90) and
  # production (365) to minimize storage costs during development.
  # ---------------------------------------------------------------------------
  log_retention_days = local.log_retention_days

  # ---------------------------------------------------------------------------
  # TLS/Certificate Configuration (from variables.tf → dev.auto.tfvars)
  # ---------------------------------------------------------------------------
  # When empty, the ALB serves HTTP only (port 80). Set to a valid ACM
  # certificate ARN to enable HTTPS (port 443) with TLS termination at the
  # ALB. LocalStack testing uses HTTP only; real AWS environments should
  # provide a certificate.
  # ---------------------------------------------------------------------------
  certificate_arn = var.certificate_arn
}

# =============================================================================
# Output Passthrough — Expose Common Module Outputs at Root Level
# =============================================================================
# These 10 outputs pass through the common module's outputs to the root level,
# making them available to:
#   - CI/CD scripts (build-and-push.sh) → ECR repository URLs
#   - Deployment scripts (deploy-schema.sh) → RDS endpoint
#   - Operational monitoring → cluster name, log group, ALB DNS
#   - Network debugging → security group IDs
#   - Terraform Cloud workspace outputs → visible in TFE UI
#
# Output naming matches the common module exactly for consistency across
# all environments (dev/staging/prod/localstack).
# =============================================================================

# -----------------------------------------------------------------------------
# ECR Repository URLs — used by build-and-push.sh for image push
# -----------------------------------------------------------------------------
output "backend_ecr_repository_url" {
  description = "Full ECR repository URL for the SplendidCRM backend Docker image. Used by CI/CD scripts to tag and push images."
  value       = module.common.backend_ecr_repository_url
}

output "frontend_ecr_repository_url" {
  description = "Full ECR repository URL for the SplendidCRM frontend Docker image. Used by CI/CD scripts to tag and push images."
  value       = module.common.frontend_ecr_repository_url
}

# -----------------------------------------------------------------------------
# ECS Cluster — used for service deployment and ECS Exec troubleshooting
# -----------------------------------------------------------------------------
output "ecs_cluster_name" {
  description = "Name of the ECS Fargate cluster hosting SplendidCRM backend and frontend services."
  value       = module.common.ecs_cluster_name
}

# -----------------------------------------------------------------------------
# ALB DNS — single entry point for all SplendidCRM traffic
# -----------------------------------------------------------------------------
output "alb_dns_name" {
  description = "DNS name of the internal ALB serving SplendidCRM. Use this URL to access the application after deployment."
  value       = module.common.alb_dns_name
}

# -----------------------------------------------------------------------------
# CloudWatch Observability — log group and stream for ECS container logs
# -----------------------------------------------------------------------------
output "cloudwatch_log_group_name" {
  description = "CloudWatch log group name for SplendidCRM ECS container logs. Use for log queries and alarm configuration."
  value       = module.common.cloudwatch_log_group_name
}

output "cloudwatch_log_stream_name" {
  description = "CloudWatch log stream name for SplendidCRM ECS logs."
  value       = module.common.cloudwatch_log_stream_name
}

# -----------------------------------------------------------------------------
# Security Group IDs — used for network debugging and cross-stack references
# -----------------------------------------------------------------------------
output "alb_security_group_id" {
  description = "Security group ID for the SplendidCRM internal ALB. Allows VPC CIDR inbound on ports 80/443."
  value       = module.common.alb_security_group_id
}

output "backend_security_group_id" {
  description = "Security group ID for backend ECS tasks. Allows ALB inbound on port 8080 and RDS/AWS API outbound."
  value       = module.common.backend_security_group_id
}

output "frontend_security_group_id" {
  description = "Security group ID for frontend ECS tasks. Allows ALB inbound on port 80 only."
  value       = module.common.frontend_security_group_id
}

# -----------------------------------------------------------------------------
# RDS Endpoint — SENSITIVE: used by deploy-schema.sh for DB provisioning
# -----------------------------------------------------------------------------
output "rds_endpoint" {
  description = "RDS SQL Server endpoint (host:port) for database connectivity. Used by deploy-schema.sh and connection string construction."
  value       = module.common.rds_endpoint
  sensitive   = true
}
