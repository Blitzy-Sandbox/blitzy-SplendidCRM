# =============================================================================
# IAM Roles & Policies — SplendidCRM ECS Fargate Infrastructure Module
# =============================================================================
#
# Purpose:
#   Define 3 IAM roles with least-privilege policies for the ECS Fargate
#   deployment. Each role follows the principle of least privilege, granting
#   only the specific permissions required for its function:
#
#   1. ECS Execution Role (ecs_execution)
#      - Assumed by the ECS agent BEFORE containers start
#      - Permissions: ECR image pull, CloudWatch log stream creation,
#        Secrets Manager value retrieval (for valueFrom resolution), and
#        KMS Decrypt (for CMK-encrypted secrets)
#
#   2. Backend Task Role (backend_task)
#      - Assumed by the running backend application container
#      - Permissions: Secrets Manager reads (AwsSecretsManagerProvider.cs
#        runtime refresh), KMS Decrypt, SSM Parameter Store reads
#        (AwsParameterStoreProvider.cs), and ECS Exec for troubleshooting
#
#   3. Frontend Task Role (frontend_task)
#      - Assumed by the running frontend Nginx container
#      - Permissions: ECS Exec only (for troubleshooting)
#      - NO Secrets Manager, KMS, or SSM permissions (least-privilege:
#        Nginx has zero AWS SDK dependencies)
#
# Resources Created (9 total):
#   - aws_iam_role.ecs_execution                        — Execution role
#   - aws_iam_policy.ecs_execution                      — Execution policy
#   - aws_iam_role_policy_attachment.ecs_execution       — Attachment
#   - aws_iam_role.backend_task                          — Backend task role
#   - aws_iam_policy.backend_task                        — Backend task policy
#   - aws_iam_role_policy_attachment.backend_task         — Attachment
#   - aws_iam_role.frontend_task                         — Frontend task role
#   - aws_iam_policy.frontend_task                       — Frontend task policy
#   - aws_iam_role_policy_attachment.frontend_task        — Attachment
#
# Cross-File Dependencies:
#   READS FROM:
#     ecr.tf        → aws_ecr_repository.backend.arn,
#                      aws_ecr_repository.frontend.arn
#     cloudwatch.tf → aws_cloudwatch_log_group.application.arn
#     secrets.tf    → aws_secretsmanager_secret.db_connection.arn,
#                      aws_secretsmanager_secret.sso_client_id.arn,
#                      aws_secretsmanager_secret.sso_client_secret.arn,
#                      aws_secretsmanager_secret.duo_integration_key.arn,
#                      aws_secretsmanager_secret.duo_secret_key.arn,
#                      aws_secretsmanager_secret.smtp_credentials.arn
#     kms.tf        → aws_kms_key.secrets.arn
#     data.tf       → data.aws_region.current.region,
#                      data.aws_caller_identity.current.account_id
#     variables.tf  → var.name_prefix
#
#   REFERENCED BY:
#     ecs-fargate.tf → aws_iam_role.ecs_execution.arn  (execution_role_arn)
#                      aws_iam_role.backend_task.arn    (task_role_arn backend)
#                      aws_iam_role.frontend_task.arn   (task_role_arn frontend)
#     kms.tf         → aws_iam_role.ecs_execution.arn  (key policy decrypt)
#                      aws_iam_role.backend_task.arn    (key policy decrypt)
#
# Application Context:
#   - AwsSecretsManagerProvider.cs uses the backend task role to call
#     secretsmanager:GetSecretValue at runtime with a 5-minute refresh
#     interval for secret rotation support (line 200: AmazonSecretsManagerClient
#     with default credential chain resolves to task role in ECS).
#   - AwsParameterStoreProvider.cs uses the backend task role to call
#     ssm:GetParametersByPath with basePath "/splendidcrm/{environment}/"
#     at startup (line 220: GetParametersByPathRequest with Recursive=true).
#   - The ECS execution role resolves valueFrom secret references in the
#     task definition BEFORE the application container starts. The ECS agent
#     calls secretsmanager:GetSecretValue and kms:Decrypt using the execution
#     role, then injects the resolved values as environment variables.
#   - Frontend Nginx container has zero AWS SDK dependencies — no AWS
#     permissions needed beyond ECS Exec for troubleshooting.
#
# Security Design:
#   - All policies use specific resource ARNs, not wildcards (except where
#     required: ecr:GetAuthorizationToken and ssmmessages:* on Resource "*")
#   - Secret ARN references point to actual Terraform resources (G7 compliance)
#   - SSM parameter path scoped to /splendidcrm/* (AAP §0.8.1)
#   - KMS Decrypt scoped to specific CMK ARN, not wildcard
#
# ACME Compliance:
#   - No tfe.acme.com references (Guardrail G10)
#   - Uses standard aws_iam_role / aws_iam_policy resource blocks for
#     LocalStack compatibility
#   - ACME module swap: aws_iam_role → tfe.acme.com/acme/iam/aws
# =============================================================================


# =============================================================================
# SECTION 1: ECS EXECUTION ROLE
# =============================================================================
# The ECS execution role is assumed by the ECS agent (not the application
# container) during task launch. It provides permissions for:
#   - Pulling Docker images from ECR (backend and frontend repositories)
#   - Creating log streams and writing log events to CloudWatch
#   - Retrieving secret values from Secrets Manager (for valueFrom resolution
#     in the ECS task definition per Guardrail G7)
#   - Decrypting secrets encrypted with the KMS Customer Managed Key
#
# This role is referenced by both backend and frontend task definitions
# via execution_role_arn in ecs-fargate.tf.
# =============================================================================

# -----------------------------------------------------------------------------
# 1.1: ECS Execution IAM Role
# -----------------------------------------------------------------------------
# Trust policy allows the ECS Tasks service principal to assume this role.
# This is the standard trust policy for ECS Fargate execution roles.
# -----------------------------------------------------------------------------
resource "aws_iam_role" "ecs_execution" {
  name = "${var.name_prefix}-ecs-execution-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "AllowECSTasksAssume"
        Effect = "Allow"
        Principal = {
          Service = "ecs-tasks.amazonaws.com"
        }
        Action = "sts:AssumeRole"
      }
    ]
  })

  tags = {
    Name = "${var.name_prefix}-ecs-execution-role"
  }
}

# -----------------------------------------------------------------------------
# 1.2: ECS Execution IAM Policy
# -----------------------------------------------------------------------------
# Least-privilege policy with 4 permission groups:
#   1. ECR Pull — scoped to specific repository ARNs + auth token (global)
#   2. CloudWatch Logs — scoped to the application log group ARN
#   3. Secrets Manager — scoped to all 6 specific secret ARNs
#   4. KMS Decrypt — scoped to the CMK ARN
#
# Each statement uses the minimum set of actions required for its function.
# Resource ARNs reference actual Terraform resources to ensure consistency
# and prevent drift between IAM permissions and actual resources.
# -----------------------------------------------------------------------------
resource "aws_iam_policy" "ecs_execution" {
  name        = "${var.name_prefix}-ecs-execution-policy"
  description = "ECS execution role policy for ${var.name_prefix}: ECR pull, CloudWatch logs, Secrets Manager read, KMS decrypt"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      # -----------------------------------------------------------------
      # Statement 1: ECR Image Pull (scoped to specific repositories)
      # -----------------------------------------------------------------
      # Grants permission to download Docker image layers from the backend
      # and frontend ECR repositories. These three actions are the minimum
      # required for the ECS agent to pull container images:
      #   - GetDownloadUrlForLayer: Get pre-signed URL for layer download
      #   - BatchGetImage: Retrieve image manifest and layer metadata
      #   - BatchCheckLayerAvailability: Verify layer existence before pull
      # -----------------------------------------------------------------
      {
        Sid    = "ECRImagePull"
        Effect = "Allow"
        Action = [
          "ecr:GetDownloadUrlForLayer",
          "ecr:BatchGetImage",
          "ecr:BatchCheckLayerAvailability"
        ]
        Resource = [
          aws_ecr_repository.backend.arn,
          aws_ecr_repository.frontend.arn
        ]
      },
      # -----------------------------------------------------------------
      # Statement 2: ECR Authorization Token (global — not scopeable)
      # -----------------------------------------------------------------
      # ecr:GetAuthorizationToken MUST be granted on Resource "*" because
      # the authorization token API operates at the registry level, not
      # the repository level. This is an AWS API limitation documented in
      # the ECR IAM reference. The token itself is scoped to the account
      # registry, not all of ECR.
      # -----------------------------------------------------------------
      {
        Sid    = "ECRAuthToken"
        Effect = "Allow"
        Action = [
          "ecr:GetAuthorizationToken"
        ]
        Resource = "*"
      },
      # -----------------------------------------------------------------
      # Statement 3: CloudWatch Logs (scoped to application log group)
      # -----------------------------------------------------------------
      # Grants permission to create log streams and write log events to
      # the centralized application log group defined in cloudwatch.tf.
      # The ":*" suffix on the log group ARN is required by the CloudWatch
      # Logs API to match log streams within the group.
      #
      # Both backend and frontend task definitions use the awslogs driver
      # with different stream prefixes (backend/frontend), both targeting
      # this same log group.
      # -----------------------------------------------------------------
      {
        Sid    = "CloudWatchLogs"
        Effect = "Allow"
        Action = [
          "logs:CreateLogStream",
          "logs:PutLogEvents"
        ]
        Resource = [
          "${aws_cloudwatch_log_group.application.arn}:*"
        ]
      },
      # -----------------------------------------------------------------
      # Statement 4: Secrets Manager (scoped to all 6 specific secrets)
      # -----------------------------------------------------------------
      # Grants secretsmanager:GetSecretValue on all 6 secrets defined in
      # secrets.tf. The ECS agent uses this permission to resolve the
      # valueFrom references in the backend task definition before the
      # application container starts (Guardrail G7: full ARN format).
      #
      # The 7 secret references in the backend task definition map to
      # these 6 secrets (db_connection is referenced twice: once for
      # ConnectionStrings__SplendidCRM and once for Session__ConnectionString):
      #   1. db_connection       → ConnectionStrings__SplendidCRM
      #   2. db_connection       → Session__ConnectionString
      #   3. sso_client_id       → SSO__ClientId
      #   4. sso_client_secret   → SSO__ClientSecret
      #   5. duo_integration_key → Duo__IntegrationKey
      #   6. duo_secret_key      → Duo__SecretKey
      #   7. smtp_credentials    → Smtp__Credentials
      # -----------------------------------------------------------------
      {
        Sid    = "SecretsManagerRead"
        Effect = "Allow"
        Action = [
          "secretsmanager:GetSecretValue"
        ]
        Resource = [
          aws_secretsmanager_secret.db_connection.arn,
          aws_secretsmanager_secret.sso_client_id.arn,
          aws_secretsmanager_secret.sso_client_secret.arn,
          aws_secretsmanager_secret.duo_integration_key.arn,
          aws_secretsmanager_secret.duo_secret_key.arn,
          aws_secretsmanager_secret.smtp_credentials.arn
        ]
      },
      # -----------------------------------------------------------------
      # Statement 5: KMS Decrypt (scoped to the specific CMK ARN)
      # -----------------------------------------------------------------
      # Grants kms:Decrypt on the Customer Managed Key used to encrypt
      # all Secrets Manager secrets. Without this permission, the ECS
      # agent cannot unwrap the encrypted secret values and container
      # startup fails silently (G7 compliance).
      #
      # This is scoped to the specific CMK ARN defined in kms.tf — NOT
      # a wildcard. The CMK is identified by aws_kms_key.secrets.arn.
      # -----------------------------------------------------------------
      {
        Sid    = "KMSDecrypt"
        Effect = "Allow"
        Action = [
          "kms:Decrypt"
        ]
        Resource = [
          aws_kms_key.secrets.arn
        ]
      }
    ]
  })

  tags = {
    Name = "${var.name_prefix}-ecs-execution-policy"
  }
}

# -----------------------------------------------------------------------------
# 1.3: ECS Execution Policy Attachment
# -----------------------------------------------------------------------------
# Attaches the execution policy to the execution role. This is the only
# policy attached to the execution role — no AWS managed policies are used
# to maintain strict least-privilege control.
# -----------------------------------------------------------------------------
resource "aws_iam_role_policy_attachment" "ecs_execution" {
  role       = aws_iam_role.ecs_execution.name
  policy_arn = aws_iam_policy.ecs_execution.arn
}


# =============================================================================
# SECTION 2: BACKEND TASK ROLE
# =============================================================================
# The backend task role is assumed by the running .NET 10 application container.
# It provides runtime permissions for the application's AWS integrations:
#   - Secrets Manager reads (AwsSecretsManagerProvider.cs — 5-minute refresh)
#   - KMS Decrypt (for CMK-encrypted secrets accessed at runtime)
#   - SSM Parameter Store reads (AwsParameterStoreProvider.cs — startup load)
#   - ECS Exec (ssmmessages) for interactive troubleshooting
#
# Application context:
#   - AwsSecretsManagerProvider.cs creates an AmazonSecretsManagerClient using
#     the default credential chain (line 200), which resolves to this task role
#     in ECS. It calls GetSecretValueAsync to retrieve secrets.
#   - AwsParameterStoreProvider.cs creates an AmazonSimpleSystemsManagementClient
#     (line 214) and calls GetParametersByPathAsync with the base path
#     "/splendidcrm/{environment}/config/" to load all parameters recursively.
#   - If kms:Decrypt is missing, AwsSecretsManagerProvider.cs catches
#     DecryptionFailureException (line 159) and logs the diagnostic message:
#     "Verify that the ECS Task Role has kms:Decrypt permission on the CMK."
# =============================================================================

# -----------------------------------------------------------------------------
# 2.1: Backend Task IAM Role
# -----------------------------------------------------------------------------
# Same ECS tasks trust policy as the execution role. The distinction is that
# this role is assigned to task_role_arn (application container credentials),
# while the execution role is assigned to execution_role_arn (ECS agent
# credentials) in the task definition.
# -----------------------------------------------------------------------------
resource "aws_iam_role" "backend_task" {
  name = "${var.name_prefix}-backend-task-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "AllowECSTasksAssume"
        Effect = "Allow"
        Principal = {
          Service = "ecs-tasks.amazonaws.com"
        }
        Action = "sts:AssumeRole"
      }
    ]
  })

  tags = {
    Name = "${var.name_prefix}-backend-task-role"
  }
}

# -----------------------------------------------------------------------------
# 2.2: Backend Task IAM Policy
# -----------------------------------------------------------------------------
# Least-privilege policy with 4 permission groups:
#   1. Secrets Manager — runtime reads for AwsSecretsManagerProvider.cs
#   2. KMS Decrypt — decrypt CMK-encrypted secrets at runtime
#   3. SSM Parameter Store — runtime reads for AwsParameterStoreProvider.cs
#   4. ECS Exec — ssmmessages for interactive troubleshooting
#
# The Secrets Manager and KMS permissions overlap with the execution role
# by design: the execution role resolves secrets at task launch, while the
# backend task role reads secrets at runtime (with 5-minute refresh for
# secret rotation). Both need the same permissions but serve different
# lifecycle phases.
# -----------------------------------------------------------------------------
resource "aws_iam_policy" "backend_task" {
  name        = "${var.name_prefix}-backend-task-policy"
  description = "Backend task role policy for ${var.name_prefix}: Secrets Manager read, KMS decrypt, SSM Parameter Store read, ECS Exec"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      # -----------------------------------------------------------------
      # Statement 1: Secrets Manager (scoped to all 6 specific secrets)
      # -----------------------------------------------------------------
      # Runtime permission for AwsSecretsManagerProvider.cs to read secrets.
      # The provider calls GetSecretValueAsync (line 210) using the default
      # credential chain, which resolves to this task role in ECS.
      #
      # The 5-minute reload interval (ReloadInterval in Program.cs) means
      # this permission is exercised periodically, not just at startup.
      # This supports AWS Secrets Manager automatic rotation without
      # requiring container restarts.
      # -----------------------------------------------------------------
      {
        Sid    = "SecretsManagerRead"
        Effect = "Allow"
        Action = [
          "secretsmanager:GetSecretValue"
        ]
        Resource = [
          aws_secretsmanager_secret.db_connection.arn,
          aws_secretsmanager_secret.sso_client_id.arn,
          aws_secretsmanager_secret.sso_client_secret.arn,
          aws_secretsmanager_secret.duo_integration_key.arn,
          aws_secretsmanager_secret.duo_secret_key.arn,
          aws_secretsmanager_secret.smtp_credentials.arn
        ]
      },
      # -----------------------------------------------------------------
      # Statement 2: KMS Decrypt (scoped to the specific CMK ARN)
      # -----------------------------------------------------------------
      # Required for decrypting secrets at runtime. Without this permission,
      # AwsSecretsManagerProvider.cs throws DecryptionFailureException
      # (caught at line 159 of AwsSecretsManagerProvider.cs).
      #
      # Scoped to the specific CMK ARN — NOT a wildcard.
      # -----------------------------------------------------------------
      {
        Sid    = "KMSDecrypt"
        Effect = "Allow"
        Action = [
          "kms:Decrypt"
        ]
        Resource = [
          aws_kms_key.secrets.arn
        ]
      },
      # -----------------------------------------------------------------
      # Statement 3: SSM Parameter Store (scoped to /splendidcrm/* path)
      # -----------------------------------------------------------------
      # Runtime permission for AwsParameterStoreProvider.cs to read
      # non-secret configuration parameters. The provider uses
      # GetParametersByPathAsync (line 220-230) with:
      #   Path       = "/splendidcrm/{environment}/config/"
      #   Recursive  = true
      #   WithDecryption = true (for SecureString parameters)
      #
      # The ARN pattern /splendidcrm/* grants access to all parameters
      # under the /splendidcrm/ prefix across all environments. This is
      # intentionally broader than a single environment to support
      # cross-environment parameter reads if needed, while still being
      # scoped to the SplendidCRM application namespace.
      #
      # Three SSM actions are required:
      #   - GetParameter: Single parameter retrieval
      #   - GetParameters: Batch parameter retrieval by name
      #   - GetParametersByPath: Recursive path-based retrieval (primary)
      # -----------------------------------------------------------------
      {
        Sid    = "SSMParameterStoreRead"
        Effect = "Allow"
        Action = [
          "ssm:GetParameter",
          "ssm:GetParameters",
          "ssm:GetParametersByPath"
        ]
        Resource = [
          "arn:aws:ssm:${data.aws_region.current.region}:${data.aws_caller_identity.current.account_id}:parameter/splendidcrm/*"
        ]
      },
      # -----------------------------------------------------------------
      # Statement 4: ECS Exec (ssmmessages for troubleshooting)
      # -----------------------------------------------------------------
      # Grants the 4 ssmmessages actions required for ECS Exec interactive
      # sessions (aws ecs execute-command). This enables operators to
      # troubleshoot the running backend container by opening a shell:
      #   aws ecs execute-command --cluster <cluster> --task <task-id> \
      #     --container backend --interactive --command "/bin/sh"
      #
      # These actions must be on Resource "*" because SSM Messages
      # channels are not ARN-addressable — they are session-based.
      # -----------------------------------------------------------------
      {
        Sid    = "ECSExec"
        Effect = "Allow"
        Action = [
          "ssmmessages:CreateControlChannel",
          "ssmmessages:CreateDataChannel",
          "ssmmessages:OpenControlChannel",
          "ssmmessages:OpenDataChannel"
        ]
        Resource = "*"
      }
    ]
  })

  tags = {
    Name = "${var.name_prefix}-backend-task-policy"
  }
}

# -----------------------------------------------------------------------------
# 2.3: Backend Task Policy Attachment
# -----------------------------------------------------------------------------
resource "aws_iam_role_policy_attachment" "backend_task" {
  role       = aws_iam_role.backend_task.name
  policy_arn = aws_iam_policy.backend_task.arn
}


# =============================================================================
# SECTION 3: FRONTEND TASK ROLE
# =============================================================================
# The frontend task role is assumed by the running Nginx container. It has
# minimal permissions — only ECS Exec for troubleshooting. The frontend
# container:
#   - Serves static files (React 19 SPA build output) via Nginx
#   - Injects runtime config.json via docker-entrypoint.sh (from env vars)
#   - Has ZERO AWS SDK dependencies
#   - Makes NO AWS API calls at runtime
#
# Per AAP §0.8.1 (least-privilege), this role intentionally has:
#   - NO Secrets Manager permissions
#   - NO KMS permissions
#   - NO SSM Parameter Store permissions
#   - NO ECR permissions (handled by the execution role)
#   - NO CloudWatch Logs permissions (handled by the execution role)
#   - ONLY ECS Exec (ssmmessages) for operator troubleshooting
# =============================================================================

# -----------------------------------------------------------------------------
# 3.1: Frontend Task IAM Role
# -----------------------------------------------------------------------------
resource "aws_iam_role" "frontend_task" {
  name = "${var.name_prefix}-frontend-task-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "AllowECSTasksAssume"
        Effect = "Allow"
        Principal = {
          Service = "ecs-tasks.amazonaws.com"
        }
        Action = "sts:AssumeRole"
      }
    ]
  })

  tags = {
    Name = "${var.name_prefix}-frontend-task-role"
  }
}

# -----------------------------------------------------------------------------
# 3.2: Frontend Task IAM Policy (Minimal — ECS Exec Only)
# -----------------------------------------------------------------------------
# This is the most restrictive task role policy — only ECS Exec permissions
# for troubleshooting. No Secrets Manager, KMS, SSM, or any other AWS
# service permissions are granted.
# -----------------------------------------------------------------------------
resource "aws_iam_policy" "frontend_task" {
  name        = "${var.name_prefix}-frontend-task-policy"
  description = "Frontend task role policy for ${var.name_prefix}: ECS Exec only (minimal permissions)"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      # -----------------------------------------------------------------
      # Statement 1: ECS Exec (ssmmessages for troubleshooting)
      # -----------------------------------------------------------------
      # Same ECS Exec permissions as the backend task role. Enables
      # operators to troubleshoot the frontend Nginx container:
      #   aws ecs execute-command --cluster <cluster> --task <task-id> \
      #     --container frontend --interactive --command "/bin/sh"
      #
      # This is the ONLY permission granted to the frontend task role.
      # -----------------------------------------------------------------
      {
        Sid    = "ECSExec"
        Effect = "Allow"
        Action = [
          "ssmmessages:CreateControlChannel",
          "ssmmessages:CreateDataChannel",
          "ssmmessages:OpenControlChannel",
          "ssmmessages:OpenDataChannel"
        ]
        Resource = "*"
      }
    ]
  })

  tags = {
    Name = "${var.name_prefix}-frontend-task-policy"
  }
}

# -----------------------------------------------------------------------------
# 3.3: Frontend Task Policy Attachment
# -----------------------------------------------------------------------------
resource "aws_iam_role_policy_attachment" "frontend_task" {
  role       = aws_iam_role.frontend_task.name
  policy_arn = aws_iam_policy.frontend_task.arn
}
