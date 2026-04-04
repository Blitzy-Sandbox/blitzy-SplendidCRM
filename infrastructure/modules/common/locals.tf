# =============================================================================
# SplendidCRM Common Module — Local Values
# =============================================================================
#
# Purpose:
#   Define local values for computed Docker image URIs (ECR repository URL +
#   image tag) used by ECS task definitions. This file centralizes computed
#   values to avoid repetition and maintain a single source of truth for
#   cross-resource references.
#
# Cross-File Dependencies:
#   READS FROM:
#     ecr.tf       → aws_ecr_repository.backend.repository_url
#                    aws_ecr_repository.frontend.repository_url
#     variables.tf → var.image_tag
#
#   REFERENCED BY:
#     ecs-fargate.tf → local.backend_image, local.frontend_image
#
# ACME Compliance:
#   - No tfe.acme.com references (Guardrail G10)
#   - Uses standard aws_* resource attributes for LocalStack compatibility
# =============================================================================

locals {
  # ---------------------------------------------------------------------------
  # Computed Docker Image URIs
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
}
