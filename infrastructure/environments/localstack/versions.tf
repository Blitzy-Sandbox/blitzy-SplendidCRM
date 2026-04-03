# -----------------------------------------------------------------------------
# Terraform Local State Backend & Provider Configuration — LocalStack Environment
# -----------------------------------------------------------------------------
# This file configures a LOCAL state backend (no Terraform Cloud dependency)
# and the AWS provider with endpoint overrides pointing all AWS services to
# LocalStack at http://localhost:4566.
#
# CRITICAL DIFFERENCES FROM DEV:
#   - Backend: local state file (NOT Terraform Cloud at tfe.acme.com)
#   - Auth: static test/test credentials (NOT assume_role)
#   - Endpoints: ALL AWS services → http://localhost:4566 (NOT default AWS)
#   - Skip flags: credentials, metadata, account_id checks all skipped
#   - State: stored on disk as terraform.tfstate (NOT TFE Cloud managed)
#
# This is the ONLY environment that does NOT use TFE Cloud backend.
# Per AAP §0.8.3: All autonomous Terraform operations must run from
# environments/localstack/ — never from dev/staging/prod (which require
# tfe.acme.com access).
#
# ACME Standards Preserved:
#   - Required Terraform version: >= 1.12.0
#   - AWS provider version: >= 6.0.0
#   - Region: us-east-2 (matches VPC tag acme-dev-vpc-use2)
#   - Default tags: 14 mandatory ACME tags for tag propagation validation
# -----------------------------------------------------------------------------

terraform {
  # ---------------------------------------------------------------------------
  # Local State Backend
  # ---------------------------------------------------------------------------
  # LocalStack validation uses local state — no Terraform Cloud dependency.
  # State is stored in the current directory as terraform.tfstate.
  # This eliminates the requirement for tfe.acme.com connectivity during
  # autonomous infrastructure validation.
  # ---------------------------------------------------------------------------
  backend "local" {
    path = "terraform.tfstate"
  }

  # Terraform CLI version constraint — same as dev for consistency
  required_version = ">= 1.12.0"

  # ---------------------------------------------------------------------------
  # Required Providers
  # ---------------------------------------------------------------------------
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = ">= 6.0.0"
    }
  }
}

# -----------------------------------------------------------------------------
# AWS Provider Configuration — LocalStack Endpoint Overrides
# -----------------------------------------------------------------------------
# The provider uses static test/test credentials and overrides ALL AWS service
# endpoints to point to LocalStack at http://localhost:4566. Skip flags disable
# credential validation, metadata API checks, and account ID requests that
# would fail against LocalStack.
#
# NO assume_role block — LocalStack does not support cross-account IAM role
# assumption. Authentication is handled by the static access_key/secret_key.
#
# ALL 13 service endpoints are overridden to ensure every resource type in the
# common module (ECR, ECS, ALB, RDS, IAM, KMS, Secrets Manager, SSM, etc.)
# communicates with LocalStack instead of real AWS.
#
# Default tags maintain the 14 ACME-standard tags to validate that tag
# propagation works correctly through LocalStack.
# -----------------------------------------------------------------------------
provider "aws" {
  region                      = "us-east-2"
  access_key                  = "test"
  secret_key                  = "test"
  skip_credentials_validation = true
  skip_metadata_api_check     = true
  skip_requesting_account_id  = true

  # ---------------------------------------------------------------------------
  # LocalStack Endpoint Overrides
  # ---------------------------------------------------------------------------
  # Every AWS service used by the common module must have its endpoint
  # redirected to LocalStack. Missing an endpoint causes Terraform to call
  # real AWS APIs, which will fail with invalid credentials.
  # ---------------------------------------------------------------------------
  endpoints {
    acm                    = "http://localhost:4566"
    cloudwatch             = "http://localhost:4566"
    cloudwatchlogs         = "http://localhost:4566"
    ec2                    = "http://localhost:4566"
    ecr                    = "http://localhost:4566"
    ecs                    = "http://localhost:4566"
    elasticloadbalancingv2 = "http://localhost:4566"
    iam                    = "http://localhost:4566"
    kms                    = "http://localhost:4566"
    rds                    = "http://localhost:4566"
    secretsmanager         = "http://localhost:4566"
    ssm                    = "http://localhost:4566"
    sts                    = "http://localhost:4566"
  }

  # ---------------------------------------------------------------------------
  # ACME Default Tags — 14 Mandatory Tags
  # ---------------------------------------------------------------------------
  # Same 14 ACME-standard tags as dev/staging/prod environments to validate
  # that tag propagation works correctly through LocalStack. Variable
  # references resolve from localstack.auto.tfvars values.
  # ---------------------------------------------------------------------------
  default_tags {
    tags = {
      # Admin tags — environment classification
      "admin:environment" = var.environment

      # FinOps tags — cost allocation and budget tracking
      "finops:portfolio"   = var.portfolio
      "finops:costcenter"  = var.cost_center
      "finops:owner"       = var.owner_email
      "finops:application" = "splendidcrm"

      # Global managed_by tag — IaC provenance indicator (AFT = Account Factory for Terraform)
      "managed_by" = "AFT"

      # Ops tags — backup and DR schedule placeholders (required by ACME Sentinel)
      "ops:backupschedule1" = "none"
      "ops:backupschedule2" = "none"
      "ops:backupschedule3" = "none"
      "ops:backupschedule4" = "none"
      "ops:drschedule1"     = "none"
      "ops:drschedule2"     = "none"
      "ops:drschedule3"     = "none"
      "ops:drschedule4"     = "none"
    }
  }
}
