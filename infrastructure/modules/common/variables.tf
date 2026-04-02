# =============================================================================
# SplendidCRM Common Module — Input Variables
# =============================================================================
# Defines ALL module input variables passed from the environment layer
# (dev/staging/prod/localstack). These variables control naming, sizing,
# networking, and deployment configuration for all resources in the common
# module.
#
# Variable Cross-Reference (consumed by other files in this module):
#   ecr.tf            → name_prefix
#   security-groups.tf → name_prefix, vpc_id, vpc_cidr, container_port
#   alb.tf            → name_prefix, container_port, vpc_id, app_subnet_ids,
#                        certificate_arn
#   iam.tf            → name_prefix, account_id
#   ecs-fargate.tf    → name_prefix, environment, container_port, task_cpu,
#                        task_memory, task_ephemeral_gb, min_tasks, max_tasks,
#                        app_subnet_ids
#   kms.tf            → name_prefix, account_id
#   rds.tf            → name_prefix, environment, db_subnet_ids,
#                        rds_instance_class, rds_allocated_storage,
#                        rds_max_allocated_storage
#   secrets.tf        → name_prefix, environment
#   cloudwatch.tf     → name_prefix, log_retention_days
#   locals.tf         → name_prefix, environment, image_tag, portfolio,
#                        cost_center, owner_email
# =============================================================================

# -----------------------------------------------------------------------------
# Phase 1: Core Identity Variables
# -----------------------------------------------------------------------------
# These variables identify the deployment and are used by every resource in
# the module for naming and tagging.
# -----------------------------------------------------------------------------

variable "name_prefix" {
  type        = string
  description = "Naming prefix for all resources (e.g., splendidcrm-dev). Used in resource names, tags, and ARN construction across the entire module."

  validation {
    condition     = can(regex("^[a-z0-9][a-z0-9-]*[a-z0-9]$", var.name_prefix))
    error_message = "name_prefix must contain only lowercase alphanumeric characters and hyphens, and must start and end with an alphanumeric character."
  }
}

variable "environment" {
  type        = string
  description = "Deployment environment: dev, staging, prod, localstack. Controls environment-specific conditional logic in RDS (Multi-AZ, backup retention, deletion protection), ECS task definitions, and SSM parameter paths."

  validation {
    condition     = contains(["dev", "staging", "prod", "localstack"], var.environment)
    error_message = "Environment must be one of: dev, staging, prod, localstack."
  }
}

variable "account_id" {
  type        = string
  description = "AWS account ID for IAM role ARN construction. Used by iam.tf for assume-role ARNs and kms.tf for key policy principal ARNs."

  validation {
    condition     = can(regex("^[0-9]{12}$", var.account_id))
    error_message = "account_id must be a 12-digit AWS account ID."
  }
}

# -----------------------------------------------------------------------------
# Phase 2: Container Configuration Variables
# -----------------------------------------------------------------------------
# These variables control Docker image configuration and backend container
# port assignment. Port 8080 must be consistent across Dockerfile ENV,
# Dockerfile EXPOSE, ECS task definition containerPort, ALB target group port,
# and backend security group inbound rule (Guardrail G2).
# -----------------------------------------------------------------------------

variable "container_port" {
  type        = number
  description = "Backend container port (Kestrel). MUST be 8080 per Guardrail G2 — consistent across Dockerfile, ECS task definition, ALB target group, and security group."
  default     = 8080

  validation {
    condition     = var.container_port > 0 && var.container_port <= 65535
    error_message = "container_port must be a valid port number between 1 and 65535."
  }
}

variable "image_tag" {
  type        = string
  description = "Docker image tag for ECR (e.g., latest, v1.0.0, git-sha). The same image tag deploys to Dev, Staging, and Production — behavior is determined entirely by injected configuration."
  default     = "latest"

  validation {
    condition     = length(var.image_tag) > 0
    error_message = "image_tag must not be empty."
  }
}

# -----------------------------------------------------------------------------
# Phase 3: ECS Sizing Variables
# -----------------------------------------------------------------------------
# task_cpu and task_memory have NO defaults — they MUST be set per environment:
#   Dev:     512 CPU,  1024 MB,  21 GB ephemeral, 1-4  tasks
#   Staging: 1024 CPU, 2048 MB,  30 GB ephemeral, 2-6  tasks
#   Prod:    2048 CPU, 4096 MB,  50 GB ephemeral, 2-10 tasks
# Frontend tasks use the same sizing as backend tasks (uniform per ACME
# standard).
# -----------------------------------------------------------------------------

variable "task_cpu" {
  type        = number
  description = "ECS task CPU units (256, 512, 1024, 2048, 4096). Set per environment in locals.tf — no default to force explicit sizing."

  validation {
    condition     = contains([256, 512, 1024, 2048, 4096], var.task_cpu)
    error_message = "task_cpu must be one of: 256, 512, 1024, 2048, 4096."
  }
}

variable "task_memory" {
  type        = number
  description = "ECS task memory in MiB (512, 1024, 2048, 4096, 8192, 16384, 30720). Set per environment in locals.tf — no default to force explicit sizing."

  validation {
    condition     = var.task_memory >= 512 && var.task_memory <= 30720
    error_message = "task_memory must be between 512 and 30720 MiB."
  }
}

variable "task_ephemeral_gb" {
  type        = number
  description = "ECS task ephemeral storage in GB (21-200). Default 21 GB is the minimum Fargate allocation."
  default     = 21

  validation {
    condition     = var.task_ephemeral_gb >= 21 && var.task_ephemeral_gb <= 200
    error_message = "task_ephemeral_gb must be between 21 and 200 GB."
  }
}

variable "min_tasks" {
  type        = number
  description = "Minimum number of ECS tasks per service. Used as initial desired_count and auto-scaling lower bound."
  default     = 1

  validation {
    condition     = var.min_tasks >= 1
    error_message = "min_tasks must be at least 1."
  }
}

variable "max_tasks" {
  type        = number
  description = "Maximum number of ECS tasks per service (auto-scaling upper bound). Scaling triggers at 70% CPU/Memory utilization."
  default     = 4

  validation {
    condition     = var.max_tasks >= 1
    error_message = "max_tasks must be at least 1."
  }
}

# -----------------------------------------------------------------------------
# Phase 4: Networking Variables
# -----------------------------------------------------------------------------
# All networking variables are REQUIRED with no defaults — they must be
# provided by the environment layer from VPC/subnet data source lookups.
# VPC discovery uses tag-based data sources (tag: acme-{env}-vpc-use2).
# -----------------------------------------------------------------------------

variable "vpc_id" {
  type        = string
  description = "VPC ID for security groups and target groups. Discovered by environment layer via tag-based data source (e.g., acme-dev-vpc-use2)."

  validation {
    condition     = length(var.vpc_id) > 0
    error_message = "vpc_id must not be empty."
  }
}

variable "vpc_cidr" {
  type        = string
  description = "VPC CIDR block for ALB security group inbound rules. Only traffic from within the VPC CIDR is permitted to reach the internal ALB."

  validation {
    condition     = can(cidrhost(var.vpc_cidr, 0))
    error_message = "vpc_cidr must be a valid CIDR block (e.g., 10.0.0.0/16)."
  }
}

variable "app_subnet_ids" {
  type        = list(string)
  description = "Application subnet IDs for ECS tasks and ALB placement. Discovered by environment layer via tag-based data source (tag: *-app-*)."

  validation {
    condition     = length(var.app_subnet_ids) > 0
    error_message = "app_subnet_ids must contain at least one subnet ID."
  }
}

variable "db_subnet_ids" {
  type        = list(string)
  description = "Database subnet IDs for RDS instance placement. Private subnets only — RDS is not publicly accessible."

  validation {
    condition     = length(var.db_subnet_ids) > 0
    error_message = "db_subnet_ids must contain at least one subnet ID."
  }
}

# -----------------------------------------------------------------------------
# Phase 5: RDS Variables
# -----------------------------------------------------------------------------
# RDS SQL Server configuration with dev-suitable defaults. Production
# environments override via environment-specific .auto.tfvars files.
# SQL Server 2022 (engine version 16.00) matches the local Docker
# development environment (mcr.microsoft.com/mssql/server:2022-latest).
# -----------------------------------------------------------------------------

variable "rds_instance_class" {
  type        = string
  description = "RDS instance class (e.g., db.t3.medium for dev, db.r6i.xlarge for prod). Must support SQL Server Express or Standard edition."
  default     = "db.t3.medium"
}

variable "rds_allocated_storage" {
  type        = number
  description = "Initial RDS storage allocation in GB. Minimum 20 GB for SQL Server. Storage autoscaling expands up to rds_max_allocated_storage."
  default     = 20

  validation {
    condition     = var.rds_allocated_storage >= 20
    error_message = "rds_allocated_storage must be at least 20 GB for SQL Server."
  }
}

variable "rds_max_allocated_storage" {
  type        = number
  description = "Maximum RDS storage for autoscaling in GB. Set to 0 to disable autoscaling. Must be greater than rds_allocated_storage when enabled."
  default     = 100

  validation {
    condition     = var.rds_max_allocated_storage >= 0
    error_message = "rds_max_allocated_storage must be 0 (disabled) or a positive integer."
  }
}

# -----------------------------------------------------------------------------
# Phase 6: ACME Tag Variables
# -----------------------------------------------------------------------------
# Standard ACME tags applied to all resources via provider default_tags and
# resource-level tags. These support organizational cost tracking, ownership
# identification, and operational classification per ACME governance.
# -----------------------------------------------------------------------------

variable "portfolio" {
  type        = string
  description = "ACME finops:portfolio tag value. Identifies the business portfolio for cost allocation."
  default     = "CRM"
}

variable "cost_center" {
  type        = string
  description = "ACME finops:cost_center tag value. Identifies the cost center for financial reporting."
  default     = "IT"
}

variable "owner_email" {
  type        = string
  description = "ACME ops:owner tag value (email). Contact email for the infrastructure owner. Set per environment in .auto.tfvars."
  default     = ""
}

# -----------------------------------------------------------------------------
# Phase 7: Observability Variables
# -----------------------------------------------------------------------------
# CloudWatch log retention controls how long ECS container logs are preserved.
# Both backend and frontend task definitions stream to the same log group with
# different stream prefixes (backend, frontend).
# -----------------------------------------------------------------------------

variable "log_retention_days" {
  type        = number
  description = "CloudWatch log group retention period in days. Valid values: 1, 3, 5, 7, 14, 30, 60, 90, 120, 150, 180, 365, 400, 545, 731, 1096, 1827, 2192, 2557, 2922, 3288, 3653, or 0 (never expire)."
  default     = 30

  validation {
    condition     = contains([0, 1, 3, 5, 7, 14, 30, 60, 90, 120, 150, 180, 365, 400, 545, 731, 1096, 1827, 2192, 2557, 2922, 3288, 3653], var.log_retention_days)
    error_message = "log_retention_days must be a valid CloudWatch retention value: 0, 1, 3, 5, 7, 14, 30, 60, 90, 120, 150, 180, 365, 400, 545, 731, 1096, 1827, 2192, 2557, 2922, 3288, or 3653."
  }
}

# -----------------------------------------------------------------------------
# Phase 8: TLS/Certificate Variables
# -----------------------------------------------------------------------------
# HTTPS listener on the ALB is optional — controlled by providing an ACM
# certificate ARN. When empty, only the HTTP listener on port 80 is created.
# LocalStack testing uses HTTP only; real AWS environments should provide a
# certificate for HTTPS (port 443).
# -----------------------------------------------------------------------------

variable "certificate_arn" {
  type        = string
  description = "ACM certificate ARN for HTTPS listener on the ALB. Set to empty string to disable HTTPS listener (HTTP-only mode for LocalStack testing)."
  default     = ""
}
