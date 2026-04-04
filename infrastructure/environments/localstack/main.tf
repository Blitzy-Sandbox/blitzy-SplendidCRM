# =============================================================================
# SplendidCRM ECS Fargate — LocalStack Validation Environment Root Module
# =============================================================================
#
# Purpose:
#   Root module instantiation file for the LOCALSTACK validation environment.
#   This file calls the shared common module (../../modules/common) and passes
#   all 22 required variables sourced from variables.tf, data.tf, and locals.tf.
#
# Architecture:
#   infrastructure/environments/localstack/
#     ├── versions.tf              — Local state backend + LocalStack provider
#     ├── variables.tf             — Input variable definitions (populated by .auto.tfvars)
#     ├── data.tf                  — VPC and subnet tag-based data source lookups
#     ├── locals.tf                — Dev-equivalent sizing constants (ECS, RDS, observability)
#     ├── main.tf                  — THIS FILE: module instantiation + output passthrough
#     └── localstack.auto.tfvars   — LocalStack-specific variable values
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
#
# LocalStack Context:
#   This environment uses the SAME module interface as dev/staging/prod.
#   The only differences are in versions.tf (local state backend, LocalStack
#   endpoints) and localstack.auto.tfvars (splendidcrm-local prefix,
#   000000000000 account ID). This validates the common module works correctly
#   before deploying to real AWS environments.
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
  # Identity & Configuration Variables (from variables.tf → localstack.auto.tfvars)
  # ---------------------------------------------------------------------------
  # These identify the deployment and control naming, tagging, and image
  # selection across all resources in the common module.
  #   name_prefix → "splendidcrm-local" (LocalStack validation)
  #   environment → "localstack"
  #   account_id  → "000000000000" (standard LocalStack account ID)
  # ---------------------------------------------------------------------------
  name_prefix    = var.name_prefix
  environment    = var.environment
  account_id     = var.account_id
  container_port = var.container_port # G2: Must be 8080 — flows to ECS, ALB, SG
  image_tag      = var.image_tag

  # ---------------------------------------------------------------------------
  # ECS Fargate Task Sizing (from locals.tf — Dev-Equivalent Tier)
  # ---------------------------------------------------------------------------
  # Dev-equivalent sizing: 512 CPU, 1024 MB memory, 21 GB ephemeral, 1-4 tasks
  # These values are uniform for both backend and frontend tasks per ACME
  # standard. LocalStack uses dev-equivalent sizing since it is for
  # infrastructure validation only, not production workloads.
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
  # For LocalStack, VPC and subnets must be pre-created with matching tags
  # by the validation setup script before terraform apply.
  #   - VPC: tag "acme-dev-vpc-use2"
  #   - App subnets: tag wildcard "*-app-*" (ECS tasks + ALB)
  #   - DB subnets: tag wildcard "*-db-*" (RDS private isolation)
  # ---------------------------------------------------------------------------
  vpc_id         = data.aws_vpc.main.id
  vpc_cidr       = data.aws_vpc.main.cidr_block
  app_subnet_ids = data.aws_subnets.app.ids
  db_subnet_ids  = data.aws_subnets.db.ids

  # ---------------------------------------------------------------------------
  # RDS SQL Server Sizing (from locals.tf — Dev-Equivalent Tier)
  # ---------------------------------------------------------------------------
  # Dev-equivalent instance sizing for LocalStack validation:
  #   - db.t3.medium: 2 vCPU, 4 GB RAM — cost-effective for validation
  #   - 20 GB initial storage with autoscaling up to 100 GB
  # ---------------------------------------------------------------------------
  rds_instance_class        = local.rds_instance_class
  rds_allocated_storage     = local.rds_allocated_storage
  rds_max_allocated_storage = local.rds_max_allocated_storage

  # ---------------------------------------------------------------------------
  # ACME Tag Variables (from variables.tf → localstack.auto.tfvars)
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
  # 30-day CloudWatch log retention for LocalStack — same as dev to validate
  # log group creation with the expected retention policy.
  # ---------------------------------------------------------------------------
  log_retention_days = local.log_retention_days

  # ---------------------------------------------------------------------------
  # TLS/Certificate Configuration (from variables.tf → localstack.auto.tfvars)
  # ---------------------------------------------------------------------------
  # Empty for LocalStack — the ALB serves HTTP only (port 80). LocalStack
  # does not require TLS termination for infrastructure validation. Set to a
  # valid ACM certificate ARN in real AWS environments (dev/staging/prod).
  # ---------------------------------------------------------------------------
  certificate_arn = var.certificate_arn

  # ---------------------------------------------------------------------------
  # Monitoring / Alerting Configuration
  # ---------------------------------------------------------------------------
  # Empty string disables alarm actions — alarms still change state but send
  # no notifications. LocalStack does not support SNS delivery; this ensures
  # alarm resources are created for validation without delivery failures.
  # ---------------------------------------------------------------------------
  alarm_sns_arn = ""
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
#   - LocalStack validation scripts → verify resource creation
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
