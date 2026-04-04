# =============================================================================
# SplendidCRM Common Module — Elastic Container Registry (ECR)
# =============================================================================
#
# Purpose:
#   Define 2 ECR repositories for the SplendidCRM backend and frontend Docker
#   images. Each repository has image scanning enabled on push, a lifecycle
#   policy that retains the 10 most recent images, and MUTABLE image tags to
#   support CI/CD retagging workflows.
#
# Resources Created:
#   1. aws_ecr_repository.backend           — Backend image repo
#   2. aws_ecr_lifecycle_policy.backend      — Retain last 10 backend images
#   3. aws_ecr_repository.frontend           — Frontend image repo
#   4. aws_ecr_lifecycle_policy.frontend     — Retain last 10 frontend images
#
# Naming Convention:
#   Repository names follow the ACME pattern: ${var.name_prefix}-{service}
#   Examples: splendidcrm-dev-backend, splendidcrm-dev-frontend
#
# Image Tag Strategy:
#   MUTABLE tags allow CI/CD pipelines to retag images (e.g., promote a
#   git-sha tag to "latest" or a release version). The same Docker image
#   deploys to Dev, Staging, and Production — behavioral differences are
#   determined entirely by injected configuration via ECS task definitions.
#
# Cross-File Dependencies:
#   READS FROM:
#     variables.tf → var.name_prefix
#
#   REFERENCED BY:
#     locals.tf    → aws_ecr_repository.backend.repository_url
#                    aws_ecr_repository.frontend.repository_url
#     iam.tf       → aws_ecr_repository.backend.arn
#                    aws_ecr_repository.frontend.arn
#     outputs.tf   → aws_ecr_repository.backend.repository_url
#                    aws_ecr_repository.frontend.repository_url
#
# ACME Compliance:
#   - No tfe.acme.com references (Guardrail G10)
#   - Uses standard aws_ecr_repository resource blocks for LocalStack
#     compatibility
#   - When deploying to real AWS with ACME private module registry access,
#     swap to tfe.acme.com/acme/ecr/aws per the mapping table in deployment
#     documentation
# =============================================================================

# -----------------------------------------------------------------------------
# Section 1: Backend ECR Repository
# -----------------------------------------------------------------------------
# Stores Docker images for the .NET 10 ASP.NET Core backend application.
# Images are built via multi-stage Dockerfile (SDK build → Alpine runtime)
# and pushed by the CI/CD pipeline defined in scripts/build-and-push.sh.
#
# Image scanning detects OS and application-level vulnerabilities on every
# push, providing security feedback before ECS deployment.
# -----------------------------------------------------------------------------

resource "aws_ecr_repository" "backend" {
  name                 = "${var.name_prefix}-backend"
  image_tag_mutability = "MUTABLE"
  force_delete         = true

  image_scanning_configuration {
    scan_on_push = true
  }

  tags = {
    Name = "${var.name_prefix}-backend"
  }
}

# Lifecycle policy: retain only the 10 most recent images (any tag status).
# Older images are automatically expired to control storage costs and prevent
# registry bloat from accumulated CI/CD builds.
resource "aws_ecr_lifecycle_policy" "backend" {
  repository = aws_ecr_repository.backend.name

  policy = jsonencode({
    rules = [
      {
        rulePriority = 1
        description  = "Keep last 10 images"
        selection = {
          tagStatus   = "any"
          countType   = "imageCountMoreThan"
          countNumber = 10
        }
        action = {
          type = "expire"
        }
      }
    ]
  })
}

# -----------------------------------------------------------------------------
# Section 2: Frontend ECR Repository
# -----------------------------------------------------------------------------
# Stores Docker images for the React 19 / Nginx frontend application.
# Images are built via multi-stage Dockerfile (Node 20 build → Nginx Alpine
# serve) with runtime config.json injection via docker-entrypoint.sh.
#
# Source maps are deleted during the Docker build (Guardrail G5) and
# additionally blocked by Nginx configuration to prevent client-side access
# to production source code.
# -----------------------------------------------------------------------------

resource "aws_ecr_repository" "frontend" {
  name                 = "${var.name_prefix}-frontend"
  image_tag_mutability = "MUTABLE"
  force_delete         = true

  image_scanning_configuration {
    scan_on_push = true
  }

  tags = {
    Name = "${var.name_prefix}-frontend"
  }
}

# Lifecycle policy: identical to backend — retain 10 most recent images.
resource "aws_ecr_lifecycle_policy" "frontend" {
  repository = aws_ecr_repository.frontend.name

  policy = jsonencode({
    rules = [
      {
        rulePriority = 1
        description  = "Keep last 10 images"
        selection = {
          tagStatus   = "any"
          countType   = "imageCountMoreThan"
          countNumber = 10
        }
        action = {
          type = "expire"
        }
      }
    ]
  })
}
