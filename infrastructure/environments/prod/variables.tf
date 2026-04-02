# -----------------------------------------------------------------------------
# SplendidCRM ECS Fargate — Prod Environment Variables
# -----------------------------------------------------------------------------
# Defines ALL Terraform input variables for the prod environment root module.
# These variables are populated by prod.auto.tfvars and consumed by main.tf
# when passing values to the common module.
#
# This file defines the INTERFACE CONTRACT between the operator (via tfvars)
# and the infrastructure module. The same variable definitions are used across
# all environments (dev/staging/prod/localstack) — only .auto.tfvars values differ.
#
# Variable categories:
#   1. Core Identity    — name_prefix, environment, account_id
#   2. Container Config — container_port, image_tag
#   3. ACME Tags        — portfolio, cost_center, owner_email
#   4. TLS/Certificate  — certificate_arn
#
# NOT defined here (by design):
#   - ECS sizing (task_cpu, task_memory, etc.) → locals.tf
#   - Network (vpc_id, vpc_cidr, subnet IDs) → data.tf data sources
#   - RDS sizing → locals.tf
# -----------------------------------------------------------------------------

# =============================================================================
# 1. Core Identity Variables
# =============================================================================

variable "name_prefix" {
  type        = string
  description = "Naming prefix for all resources (e.g., splendidcrm-prod). Used in resource names, tags, and log group paths following ACME naming convention: {app}-{env}."
}

variable "environment" {
  type        = string
  description = "Deployment environment. Controls environment-specific behavior in the common module including tagging, sizing references, and configuration injection."

  validation {
    condition     = contains(["dev", "staging", "prod", "localstack"], var.environment)
    error_message = "Environment must be one of: dev, staging, prod, localstack."
  }
}

variable "account_id" {
  type        = string
  description = "AWS account ID for IAM role ARN construction. Used to build assume_role ARNs and scope IAM policies. Must not be hardcoded — set via tfvars per environment."
}

# =============================================================================
# 2. Container Configuration Variables
# =============================================================================

variable "container_port" {
  type        = number
  description = "Backend container port (Kestrel). CRITICAL G2: This value MUST be 8080 to maintain port consistency across Dockerfile ENV/EXPOSE, ECS task definition containerPort, ALB backend target group port, and backend security group inbound rule."
  default     = 8080
}

variable "image_tag" {
  type        = string
  description = "Docker image tag for ECR. Defaults to 'latest' for development; CI/CD pipelines override with git SHA or semantic version for traceability."
  default     = "latest"
}

# =============================================================================
# 3. ACME Tag Variables
# =============================================================================

variable "portfolio" {
  type        = string
  description = "ACME finops:portfolio tag value. Used in provider default_tags for cost allocation and budget tracking across the ACME organization."
  default     = "CRM"
}

variable "cost_center" {
  type        = string
  description = "ACME finops:cost_center tag value. Used in provider default_tags for departmental cost allocation and chargeback reporting."
  default     = "IT"
}

variable "owner_email" {
  type        = string
  description = "ACME ops:owner tag value (team email). Used in provider default_tags for ownership attribution and operational contact routing."
  default     = ""
}

# =============================================================================
# 4. TLS/Certificate Variable
# =============================================================================

variable "certificate_arn" {
  type        = string
  description = "ACM certificate ARN for the HTTPS listener on the ALB. When empty, the HTTPS listener is not created and the ALB serves HTTP only. Set to a valid ACM certificate ARN to enable TLS termination at the load balancer."
  default     = ""
}
