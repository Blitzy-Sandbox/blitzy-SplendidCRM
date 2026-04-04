# =============================================================================
# KMS Customer Managed Key — SplendidCRM Infrastructure Module
# =============================================================================
#
# Purpose:
#   Define the KMS Customer Managed Key (CMK) used to encrypt all Secrets
#   Manager secrets for SplendidCRM. This key ensures that secrets are
#   encrypted with a customer-controlled key rather than the AWS default
#   key (aws/secretsmanager), enabling granular access control via key
#   policy and supporting automatic annual key rotation.
#
# Resources Created:
#   - aws_kms_key.secrets          — The CMK with key policy and auto-rotation
#   - aws_kms_alias.secrets        — Human-readable alias for the CMK
#
# Key Policy Principals:
#   1. Root account          — Full KMS access (required for key administration)
#   2. Terraform assume role — Key administration during provisioning
#   3. ECS execution role    — Decrypt at container startup (Secrets Manager
#                              valueFrom resolution by the ECS agent)
#   4. Backend task role     — Decrypt at runtime (AwsSecretsManagerProvider.cs
#                              reads secrets with 5-minute refresh cycle)
#
# Cross-File References:
#   Consumes:
#     - data.tf       → data.aws_caller_identity.current.account_id
#     - variables.tf  → var.name_prefix, var.account_id
#     - iam.tf        → aws_iam_role.ecs_execution.arn,
#                        aws_iam_role.backend_task.arn
#   Referenced BY:
#     - secrets.tf    → aws_kms_key.secrets.arn  (kms_key_id on each secret)
#     - iam.tf        → aws_kms_key.secrets.arn  (kms:Decrypt in IAM policy)
#     - rds.tf        → aws_kms_key.secrets.arn  (storage encryption)
#
# Security Requirements (AAP):
#   - All 6 Secrets Manager secrets MUST use this CMK, NOT aws/secretsmanager
#   - enable_key_rotation MUST be true for automatic annual rotation
#   - Key policy follows least-privilege: only ECS roles get Decrypt
#   - deletion_window_in_days = 30 for safe recovery period
#
# ACME Compliance:
#   - No tfe.acme.com references (Guardrail G10)
#   - Uses standard aws_kms_key resource for LocalStack compatibility
#   - ACME module swap: aws_kms_key → tfe.acme.com/acme/kms/aws (if available)
# =============================================================================

# -----------------------------------------------------------------------------
# KMS Customer Managed Key
# -----------------------------------------------------------------------------
# Creates the symmetric encryption key used by all Secrets Manager secrets
# and RDS storage encryption. The key policy defines four principal groups
# with progressively restricted permissions:
#
#   1. Root account       → kms:* (full access, required per AWS best practice)
#   2. Terraform role     → Administrative actions (create, describe, enable,
#                           list, put, update, revoke, disable, get, delete,
#                           tag, schedule/cancel deletion)
#   3. ECS execution role → kms:Decrypt + kms:DescribeKey (resolve Secrets
#                           Manager valueFrom references at container startup)
#   4. Backend task role  → kms:Decrypt + kms:DescribeKey (runtime secret
#                           reads by AwsSecretsManagerProvider.cs)
#
# Terraform handles the circular dependency between this key and the IAM
# roles defined in iam.tf: the key policy references role ARNs, while
# the IAM policies reference this key's ARN. Terraform's dependency graph
# resolution manages the creation order automatically.
# -----------------------------------------------------------------------------
resource "aws_kms_key" "secrets" {
  description             = "CMK for encrypting SplendidCRM Secrets Manager secrets"
  deletion_window_in_days = 30
  enable_key_rotation     = true
  is_enabled              = true

  # Key policy granting access to root account, Terraform role, and ECS roles.
  # The policy document uses jsonencode() for type-safe HCL-native JSON
  # generation, avoiding raw heredoc JSON strings that are prone to syntax
  # errors and harder to maintain.
  policy = jsonencode({
    Version = "2012-10-17"
    Id      = "${var.name_prefix}-secrets-cmk-policy"
    Statement = [
      # -----------------------------------------------------------------
      # Statement 1: Root Account Full Access
      # -----------------------------------------------------------------
      # Required by AWS KMS best practice: the root account must always
      # have full access to prevent key lockout. Without this statement,
      # if all other principals are deleted, the key becomes permanently
      # inaccessible and all encrypted data is lost.
      # -----------------------------------------------------------------
      {
        Sid    = "EnableRootAccountFullAccess"
        Effect = "Allow"
        Principal = {
          AWS = "arn:aws:iam::${data.aws_caller_identity.current.account_id}:root"
        }
        Action   = "kms:*"
        Resource = "*"
      },

      # -----------------------------------------------------------------
      # Statement 2: Terraform Role Key Administration
      # -----------------------------------------------------------------
      # Grants the ACME Terraform Enterprise assume role administrative
      # permissions on this key. This allows Terraform to manage the key
      # lifecycle (create, update, describe, enable/disable, schedule
      # deletion, manage tags) during infrastructure provisioning.
      #
      # Note: Uses var.account_id (from environment layer) for the role
      # ARN construction. This is the same account ID used by the
      # provider assume_role configuration in versions.tf.
      # -----------------------------------------------------------------
      {
        Sid    = "AllowTerraformKeyAdministration"
        Effect = "Allow"
        Principal = {
          AWS = "arn:aws:iam::${var.account_id}:role/acme-tfe-assume-role"
        }
        Action = [
          "kms:Create*",
          "kms:Describe*",
          "kms:Enable*",
          "kms:List*",
          "kms:Put*",
          "kms:Update*",
          "kms:Revoke*",
          "kms:Disable*",
          "kms:Get*",
          "kms:Delete*",
          "kms:TagResource",
          "kms:UntagResource",
          "kms:ScheduleKeyDeletion",
          "kms:CancelKeyDeletion"
        ]
        Resource = "*"
      },

      # -----------------------------------------------------------------
      # Statement 3: ECS Execution Role Decrypt
      # -----------------------------------------------------------------
      # The ECS execution role resolves Secrets Manager valueFrom
      # references in the ECS task definition BEFORE the application
      # container starts. The ECS agent calls secretsmanager:GetSecretValue
      # and needs kms:Decrypt to unwrap the encrypted secret value.
      #
      # This is required for the 7 secret references in the backend task
      # definition (ConnectionStrings__SplendidCRM, SSO__ClientId,
      # SSO__ClientSecret, Duo__IntegrationKey, Duo__SecretKey,
      # Smtp__Credentials, Session__ConnectionString) defined in
      # ecs-fargate.tf using Guardrail G7 (full ARN format).
      # -----------------------------------------------------------------
      {
        Sid    = "AllowECSExecutionRoleDecrypt"
        Effect = "Allow"
        Principal = {
          AWS = aws_iam_role.ecs_execution.arn
        }
        Action = [
          "kms:Decrypt",
          "kms:DescribeKey"
        ]
        Resource = "*"
      },

      # -----------------------------------------------------------------
      # Statement 4: Backend Task Role Decrypt
      # -----------------------------------------------------------------
      # The backend ECS task role is assumed by the running application
      # container. AwsSecretsManagerProvider.cs uses the default AWS
      # credential chain (which resolves to this task role in ECS) to
      # call secretsmanager:GetSecretValue at runtime with a 5-minute
      # refresh interval for secret rotation support.
      #
      # If this permission is missing, the provider throws
      # DecryptionFailureException (caught at line 159 of
      # AwsSecretsManagerProvider.cs) with the message:
      #   "Verify that the ECS Task Role has kms:Decrypt permission
      #    on the CMK."
      # -----------------------------------------------------------------
      {
        Sid    = "AllowBackendTaskRoleDecrypt"
        Effect = "Allow"
        Principal = {
          AWS = aws_iam_role.backend_task.arn
        }
        Action = [
          "kms:Decrypt",
          "kms:DescribeKey"
        ]
        Resource = "*"
      }
    ]
  })

  tags = {
    Name = "${var.name_prefix}-secrets-cmk"
  }
}

# -----------------------------------------------------------------------------
# KMS Key Alias
# -----------------------------------------------------------------------------
# Creates a human-readable alias for the CMK following the ACME naming
# convention: alias/{name_prefix}-secrets. This alias can be used in AWS
# Console and CLI commands as an alternative to the full key ARN or key ID.
#
# Examples:
#   - Dev:        alias/splendidcrm-dev-secrets
#   - Staging:    alias/splendidcrm-staging-secrets
#   - Production: alias/splendidcrm-prod-secrets
#   - LocalStack: alias/splendidcrm-local-secrets
# -----------------------------------------------------------------------------
resource "aws_kms_alias" "secrets" {
  name          = "alias/${var.name_prefix}-secrets"
  target_key_id = aws_kms_key.secrets.key_id
}
