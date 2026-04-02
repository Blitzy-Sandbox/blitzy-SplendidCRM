# =============================================================================
# Common Data Sources — SplendidCRM Infrastructure Module
# =============================================================================
#
# Purpose:
#   Define shared AWS data sources that provide dynamic account and region
#   information throughout the module. These data sources eliminate hardcoded
#   AWS account IDs and region names, ensuring the module is portable across
#   AWS accounts and regions.
#
# Data Sources:
#   - aws_caller_identity.current — Provides the AWS account ID, ARN, and
#     user ID of the entity executing Terraform (or the assumed role).
#   - aws_region.current — Provides the name of the AWS region configured
#     in the provider (e.g., "us-east-2").
#
# Cross-File References:
#   - kms.tf         → data.aws_caller_identity.current.account_id
#                       (KMS key policy root account ARN construction)
#   - iam.tf         → data.aws_region.current.name,
#                       data.aws_caller_identity.current.account_id
#                       (SSM Parameter Store ARN construction for IAM policies)
#   - locals.tf      → data.aws_caller_identity.current.account_id,
#                       data.aws_region.current.name
#                       (Centralized computed values for naming and ARNs)
#   - ecs-fargate.tf → data.aws_region.current.name
#                       (CloudWatch awslogs-region in task definitions)
#
# ACME Compliance:
#   - No tfe.acme.com references (G10)
#   - No hardcoded account IDs or region names
#   - Uses standard aws_* data sources for LocalStack compatibility
# =============================================================================

# -----------------------------------------------------------------------------
# AWS Caller Identity
# -----------------------------------------------------------------------------
# Retrieves the effective AWS account ID, ARN, and user ID for the credentials
# being used to run Terraform. In ACME environments, this reflects the assumed
# role (acme-tfe-assume-role) configured in the provider block.
#
# Attributes exposed:
#   - account_id : The AWS account ID (e.g., "123456789012")
#   - arn        : The ARN of the calling entity
#   - user_id    : The unique identifier of the calling entity
# -----------------------------------------------------------------------------
data "aws_caller_identity" "current" {}

# -----------------------------------------------------------------------------
# AWS Region
# -----------------------------------------------------------------------------
# Retrieves the name of the AWS region configured in the provider. This ensures
# all region-specific ARN constructions (e.g., SSM parameter ARNs, CloudWatch
# log configurations) dynamically resolve to the correct region without
# hardcoding.
#
# Attributes exposed:
#   - name        : The region name (e.g., "us-east-2")
#   - description : Human-readable region description
# -----------------------------------------------------------------------------
data "aws_region" "current" {}
