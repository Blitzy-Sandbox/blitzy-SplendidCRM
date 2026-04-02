# =============================================================================
# SplendidCRM Common Module — Local Values
# =============================================================================
#
# Purpose:
#   Define local values for naming conventions, computed Docker image URIs
#   (ECR repository URL + image tag), and ACME-standard resource tags used
#   throughout the module. This file centralizes all computed values to
#   avoid repetition, ensure naming consistency, and maintain a single
#   source of truth for cross-resource references.
#
# Sections:
#   1. Computed Image URIs  — Docker image references for ECS task definitions
#   2. Common Tags          — ACME-standard tags applied to all resources
#   3. Naming Conventions   — Pass-through and computed identity values
#
# Cross-File Dependencies:
#   READS FROM:
#     ecr.tf       → aws_ecr_repository.backend.repository_url
#                    aws_ecr_repository.frontend.repository_url
#     variables.tf → var.image_tag, var.name_prefix, var.environment,
#                    var.portfolio, var.cost_center, var.owner_email
#     data.tf      → data.aws_caller_identity.current.account_id
#                    data.aws_region.current.region
#
#   REFERENCED BY:
#     ecs-fargate.tf → local.backend_image, local.frontend_image,
#                      local.common_tags
#     alb.tf         → local.common_tags
#     rds.tf         → local.common_tags
#     kms.tf         → local.common_tags
#     secrets.tf     → local.common_tags
#     cloudwatch.tf  → local.common_tags
#     iam.tf         → local.common_tags
#
# ACME Compliance:
#   - No tfe.acme.com references (Guardrail G10)
#   - Uses standard aws_* resource attributes for LocalStack compatibility
#   - ACME tag keys follow organizational governance naming conventions
# =============================================================================

locals {
  # ---------------------------------------------------------------------------
  # Section 1: Computed Docker Image URIs
  # ---------------------------------------------------------------------------
  # These locals compose the full Docker image URI from the ECR repository URL
  # (created in ecr.tf) and the image tag variable. ECS task definitions in
  # ecs-fargate.tf reference these locals for the container image attribute.
  #
  # The same image tag deploys to Dev, Staging, and Production — behavioral
  # differences are determined entirely by injected configuration (environment
  # variables and Secrets Manager references in the ECS task definition).
  #
  # Format: <account_id>.dkr.ecr.<region>.amazonaws.com/<repo-name>:<tag>
  # Example: 123456789012.dkr.ecr.us-east-2.amazonaws.com/splendidcrm-dev-backend:v1.0.0
  # ---------------------------------------------------------------------------

  backend_image  = "${aws_ecr_repository.backend.repository_url}:${var.image_tag}"
  frontend_image = "${aws_ecr_repository.frontend.repository_url}:${var.image_tag}"

  # ---------------------------------------------------------------------------
  # Section 2: ACME-Standard Common Tags
  # ---------------------------------------------------------------------------
  # All AWS resources in this module receive these tags for organizational
  # governance, cost tracking, and operational ownership identification.
  # These tags are applied via resource-level `tags` attributes and/or the
  # AWS provider `default_tags` block in the environment layer.
  #
  # ACME Tag Taxonomy:
  #   admin:*    — Administrative classification (environment lifecycle)
  #   finops:*   — Financial operations (cost allocation and reporting)
  #   managed_by — Infrastructure management tool identifier
  #   ops:*      — Operational metadata (ownership, application, component)
  # ---------------------------------------------------------------------------

  common_tags = {
    "admin:environment"  = var.environment
    "finops:portfolio"   = var.portfolio
    "finops:cost_center" = var.cost_center
    "managed_by"         = "terraform"
    "ops:owner"          = var.owner_email
    "ops:application"    = "splendidcrm"
    "ops:component"      = var.name_prefix
  }

  # ---------------------------------------------------------------------------
  # Section 3: Naming Conventions and Identity Values
  # ---------------------------------------------------------------------------
  # Centralized identity values used by multiple resources across the module.
  # The name_prefix pass-through ensures all files reference the same local
  # for naming consistency. Account ID and region are resolved from data
  # sources (data.tf) to avoid hardcoding.
  # ---------------------------------------------------------------------------

  name_prefix = var.name_prefix
  account_id  = data.aws_caller_identity.current.account_id
  region      = data.aws_region.current.region
}
