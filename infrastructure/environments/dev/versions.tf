# -----------------------------------------------------------------------------
# Terraform Cloud Backend & Provider Configuration — Dev Environment
# -----------------------------------------------------------------------------
# This file configures the ACME enterprise Terraform Cloud (TFE) backend,
# required Terraform and provider versions, and the AWS provider with
# cross-account assume_role access and 14 ACME-standard default tags.
#
# ACME Standards:
#   - Backend: Terraform Cloud at tfe.acme.com (NOT S3 or local)
#   - Workspace naming: {app}-{env} → splendidcrm-dev
#   - Assume role: acme-tfe-assume-role for TFE→AWS cross-account access
#   - Default tags: 14 mandatory tags for cost allocation and compliance
#   - Region: us-east-2 (matches VPC tag acme-dev-vpc-use2)
# -----------------------------------------------------------------------------

terraform {
  # ---------------------------------------------------------------------------
  # ACME Terraform Cloud Backend
  # ---------------------------------------------------------------------------
  # All ACME infrastructure state is managed by Terraform Enterprise at
  # tfe.acme.com. This ensures state locking, audit trail, and policy
  # enforcement across all environments. The workspace name follows the
  # ACME convention: {application}-{environment}.
  # ---------------------------------------------------------------------------
  cloud {
    hostname     = "tfe.acme.com"
    organization = "acme"

    workspaces {
      name = "splendidcrm-dev"
    }
  }

  # Terraform CLI version constraint — requires 1.12.x or newer for cloud
  # block support and latest provider compatibility
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
# AWS Provider Configuration
# -----------------------------------------------------------------------------
# The provider uses cross-account assume_role via the standard ACME TFE role.
# The role ARN is constructed dynamically from var.account_id to support
# multiple AWS accounts without hardcoded values. Region is us-east-2 to
# match the ACME development VPC (acme-dev-vpc-use2).
#
# Default tags are applied automatically to ALL resources created by this
# provider, ensuring ACME compliance for cost allocation, ownership tracking,
# and operational reporting.
# -----------------------------------------------------------------------------
provider "aws" {
  region = "us-east-2"

  assume_role {
    role_arn = "arn:aws:iam::${var.account_id}:role/acme-tfe-assume-role"
  }

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
