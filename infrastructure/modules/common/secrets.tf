# =============================================================================
# Secrets Manager & SSM Parameter Store — SplendidCRM Infrastructure Module
# =============================================================================
#
# Purpose:
#   Define 6 Secrets Manager secrets (all encrypted with KMS Customer Managed
#   Key) and 8 SSM Parameter Store parameters for non-secret configuration
#   values. These resources correspond to the configuration keys validated by
#   StartupValidator.cs and consumed by AwsSecretsManagerProvider.cs and
#   AwsParameterStoreProvider.cs at application startup.
#
# Resources Created:
#   Secrets Manager (6 secrets, all CMK-encrypted):
#     - aws_secretsmanager_secret.db_connection       — SQL Server connection string
#     - aws_secretsmanager_secret.sso_client_id       — SSO/OIDC Client ID
#     - aws_secretsmanager_secret.sso_client_secret   — SSO/OIDC Client Secret
#     - aws_secretsmanager_secret.duo_integration_key — Duo 2FA integration key
#     - aws_secretsmanager_secret.duo_secret_key      — Duo 2FA secret key
#     - aws_secretsmanager_secret.smtp_credentials    — SMTP server credentials
#
#   SSM Parameter Store (8 parameters):
#     - aws_ssm_parameter.session_provider     — Session backend (SqlServer/Redis/InMemory)
#     - aws_ssm_parameter.auth_mode            — Auth mode (Forms/Windows/SSO)
#     - aws_ssm_parameter.scheduler_interval   — Scheduler interval (ms)
#     - aws_ssm_parameter.email_poll_interval  — Email poll interval (ms)
#     - aws_ssm_parameter.archive_interval     — Archive interval (ms)
#     - aws_ssm_parameter.log_level            — Application log level
#     - aws_ssm_parameter.duo_api_hostname     — Duo 2FA API hostname
#     - aws_ssm_parameter.cors_origins         — CORS allowed origins
#
# IMPORTANT:
#   - All 6 secrets use aws_kms_key.secrets.arn (CMK) — NOT the AWS default
#     key (aws/secretsmanager). This is a non-negotiable security requirement.
#   - No aws_secretsmanager_secret_version resources are created. Secret
#     values are populated manually by operators and never stored in
#     Terraform state.
#   - SSO and Duo secrets are always created but only populated when the
#     corresponding authentication mode is enabled (AUTH_MODE=SSO for SSO
#     secrets, Duo configuration for Duo secrets).
#
# Cross-File References:
#   Consumes:
#     - kms.tf       → aws_kms_key.secrets.arn  (CMK for secret encryption)
#     - variables.tf → var.name_prefix, var.environment
#   Referenced BY:
#     - ecs-fargate.tf → aws_secretsmanager_secret.*.arn  (valueFrom in task defs, G7)
#     - iam.tf         → aws_secretsmanager_secret.*.arn  (secretsmanager:GetSecretValue)
#                      → SSM parameter path prefix /splendidcrm/* (ssm:GetParameter*)
#
# Application Context:
#   - StartupValidator.cs validates 7 required config keys at startup:
#     ConnectionStrings:SplendidCRM, ASPNETCORE_ENVIRONMENT, SPLENDID_JOB_SERVER,
#     SESSION_PROVIDER, SESSION_CONNECTION, AUTH_MODE, CORS_ORIGINS
#   - AwsSecretsManagerProvider.cs reads secrets by name at startup with
#     5-minute cache refresh for secret rotation support
#   - AwsParameterStoreProvider.cs reads SSM parameters by path prefix
#     (/splendidcrm/{environment}/) at startup
#
# Naming Conventions:
#   Secrets:    ${var.name_prefix}/<secret-name>   (e.g., splendidcrm-dev/db-connection)
#   Parameters: /splendidcrm/${var.environment}/<param-name>
#
# ACME Compliance:
#   - No tfe.acme.com references (Guardrail G10)
#   - Uses standard aws_secretsmanager_secret and aws_ssm_parameter resources
#     for LocalStack compatibility
#   - ACME module swap: manual mapping to ACME private modules when registry
#     access is available
# =============================================================================


# =============================================================================
# SECTION 1: SECRETS MANAGER SECRETS
# =============================================================================
# All 6 secrets are encrypted with the KMS Customer Managed Key defined in
# kms.tf. The ECS execution role (iam.tf) resolves these at container startup
# via the valueFrom mechanism (Guardrail G7: full ARN references).
# The backend task role reads them at runtime via AwsSecretsManagerProvider.cs.
# =============================================================================

# -----------------------------------------------------------------------------
# Secret 1: Database Connection String
# -----------------------------------------------------------------------------
# The primary SQL Server connection string for SplendidCRM. This is the
# REQUIRED configuration key validated by StartupValidator.cs (line 104):
#   configuration.GetConnectionString("SplendidCRM")
#
# The ECS task definition references this secret twice:
#   1. ConnectionStrings__SplendidCRM → for the primary database connection
#   2. Session__ConnectionString      → for SQL Server distributed session
#
# Connection string format:
#   Server=<rds-endpoint>;Database=SplendidCRM;User Id=<user>;
#   Password=<pass>;TrustServerCertificate=True;
# -----------------------------------------------------------------------------
resource "aws_secretsmanager_secret" "db_connection" {
  name                    = "${var.name_prefix}/db-connection"
  description             = "SQL Server connection string for SplendidCRM"
  kms_key_id              = aws_kms_key.secrets.arn
  recovery_window_in_days = 7

  tags = {
    Name = "${var.name_prefix}-db-connection"
  }
}

# -----------------------------------------------------------------------------
# Secret 2: SSO/OIDC Client ID
# -----------------------------------------------------------------------------
# The OIDC Client ID for SSO authentication. This is conditionally required
# by StartupValidator.cs (line 180) when AUTH_MODE=SSO:
#   configuration["SSO_CLIENT_ID"]
#
# Always created but only populated by operators when AUTH_MODE is set to SSO.
# When AUTH_MODE=Forms (default), this secret remains empty.
# Maps to appsettings.json key: SSO.ClientId
# -----------------------------------------------------------------------------
resource "aws_secretsmanager_secret" "sso_client_id" {
  name                    = "${var.name_prefix}/sso-client-id"
  description             = "SSO/OIDC Client ID for SplendidCRM authentication"
  kms_key_id              = aws_kms_key.secrets.arn
  recovery_window_in_days = 7

  tags = {
    Name = "${var.name_prefix}-sso-client-id"
  }
}

# -----------------------------------------------------------------------------
# Secret 3: SSO/OIDC Client Secret
# -----------------------------------------------------------------------------
# The OIDC Client Secret for SSO authentication. This is conditionally
# required by StartupValidator.cs (line 187) when AUTH_MODE=SSO:
#   configuration["SSO_CLIENT_SECRET"]
#
# Always created but only populated by operators when AUTH_MODE is set to SSO.
# Maps to appsettings.json key: SSO.ClientSecret
# -----------------------------------------------------------------------------
resource "aws_secretsmanager_secret" "sso_client_secret" {
  name                    = "${var.name_prefix}/sso-client-secret"
  description             = "SSO/OIDC Client Secret for SplendidCRM authentication"
  kms_key_id              = aws_kms_key.secrets.arn
  recovery_window_in_days = 7

  tags = {
    Name = "${var.name_prefix}-sso-client-secret"
  }
}

# -----------------------------------------------------------------------------
# Secret 4: Duo Security Integration Key
# -----------------------------------------------------------------------------
# The Duo Security integration key for two-factor authentication. This is
# optional per StartupValidator.cs (line 220):
#   configuration["DUO_INTEGRATION_KEY"]
#
# All three Duo values (integration key, secret key, API hostname) must be
# provided together for Duo 2FA to function. Partial configuration generates
# a warning at startup (StartupValidator.cs line 237).
# Maps to appsettings.json key: Duo.IntegrationKey
# -----------------------------------------------------------------------------
resource "aws_secretsmanager_secret" "duo_integration_key" {
  name                    = "${var.name_prefix}/duo-integration-key"
  description             = "Duo Security integration key for 2FA"
  kms_key_id              = aws_kms_key.secrets.arn
  recovery_window_in_days = 7

  tags = {
    Name = "${var.name_prefix}-duo-integration-key"
  }
}

# -----------------------------------------------------------------------------
# Secret 5: Duo Security Secret Key
# -----------------------------------------------------------------------------
# The Duo Security secret key for two-factor authentication. This is
# optional per StartupValidator.cs (line 222):
#   configuration["DUO_SECRET_KEY"]
#
# Must be provided together with DUO_INTEGRATION_KEY and DUO_API_HOSTNAME.
# Maps to appsettings.json key: Duo.SecretKey
# -----------------------------------------------------------------------------
resource "aws_secretsmanager_secret" "duo_secret_key" {
  name                    = "${var.name_prefix}/duo-secret-key"
  description             = "Duo Security secret key for 2FA"
  kms_key_id              = aws_kms_key.secrets.arn
  recovery_window_in_days = 7

  tags = {
    Name = "${var.name_prefix}-duo-secret-key"
  }
}

# -----------------------------------------------------------------------------
# Secret 6: SMTP Server Credentials
# -----------------------------------------------------------------------------
# SMTP server credentials for email sending functionality. This is optional
# per StartupValidator.cs (line 245):
#   configuration["SMTP_CREDENTIALS"]
#
# When empty, email sending functionality is unavailable and a warning is
# logged at startup.
# Maps to appsettings.json key: Smtp.Credentials
# -----------------------------------------------------------------------------
resource "aws_secretsmanager_secret" "smtp_credentials" {
  name                    = "${var.name_prefix}/smtp-credentials"
  description             = "SMTP server credentials for email functionality"
  kms_key_id              = aws_kms_key.secrets.arn
  recovery_window_in_days = 7

  tags = {
    Name = "${var.name_prefix}-smtp-credentials"
  }
}


# =============================================================================
# SECTION 2: SSM PARAMETER STORE PARAMETERS
# =============================================================================
# These 8 parameters store non-secret, environment-specific configuration
# values. They are read by AwsParameterStoreProvider.cs at startup using
# the path prefix /splendidcrm/{environment}/ with the AWS SDK default
# credential chain (IAM task role in ECS).
#
# The backend task role (iam.tf) is granted ssm:GetParameter,
# ssm:GetParameters, and ssm:GetParametersByPath on the ARN pattern:
#   arn:aws:ssm:{region}:{account}:parameter/splendidcrm/*
#
# Parameter naming convention:
#   /splendidcrm/{environment}/{parameter-name}
#   Example: /splendidcrm/dev/session-provider
# =============================================================================

# -----------------------------------------------------------------------------
# Parameter 1: Session Storage Provider
# -----------------------------------------------------------------------------
# Controls which distributed session backend is used by ASP.NET Core.
# Validated by StartupValidator.cs (line 128) as REQUIRED:
#   configuration["SESSION_PROVIDER"]
# Must be exactly "Redis" or "SqlServer" (case-insensitive validation).
#
# Default: "SqlServer" — uses Microsoft.Extensions.Caching.SqlServer with
# the SplendidSessions table created by deploy-schema.sh.
# Maps to appsettings.json key: Session.Provider
# -----------------------------------------------------------------------------
resource "aws_ssm_parameter" "session_provider" {
  name        = "/splendidcrm/${var.environment}/session-provider"
  type        = "String"
  value       = "SqlServer"
  description = "Session storage provider: SqlServer, Redis, or InMemory"

  tags = {
    Name = "${var.name_prefix}-ssm-session-provider"
  }
}

# -----------------------------------------------------------------------------
# Parameter 2: Authentication Mode
# -----------------------------------------------------------------------------
# Controls the authentication scheme used by the application.
# Validated by StartupValidator.cs (line 148) as REQUIRED:
#   configuration["AUTH_MODE"]
# Must be exactly "Windows", "Forms", or "SSO" (case-insensitive).
#
# Default: "Forms" — standard username/password authentication.
# When set to "SSO", the SSO_CLIENT_ID and SSO_CLIENT_SECRET secrets
# become conditionally required.
# Maps to appsettings.json key: Authentication.Mode
# -----------------------------------------------------------------------------
resource "aws_ssm_parameter" "auth_mode" {
  name        = "/splendidcrm/${var.environment}/auth-mode"
  type        = "String"
  value       = "Forms"
  description = "Authentication mode: Forms, Windows, or SSO"

  tags = {
    Name = "${var.name_prefix}-ssm-auth-mode"
  }
}

# -----------------------------------------------------------------------------
# Parameter 3: Scheduler Job Check Interval
# -----------------------------------------------------------------------------
# Controls how often the Scheduler hosted service checks for pending jobs.
# Optional with default per StartupValidator.cs (line 199):
#   configuration["SCHEDULER_INTERVAL_MS"]
# Default: 60000 milliseconds (60 seconds).
# Maps to appsettings.json key: Scheduler.IntervalMs
# -----------------------------------------------------------------------------
resource "aws_ssm_parameter" "scheduler_interval" {
  name        = "/splendidcrm/${var.environment}/scheduler-interval-ms"
  type        = "String"
  value       = "60000"
  description = "Scheduler job check interval in milliseconds"

  tags = {
    Name = "${var.name_prefix}-ssm-scheduler-interval"
  }
}

# -----------------------------------------------------------------------------
# Parameter 4: Email Poll Interval
# -----------------------------------------------------------------------------
# Controls how often the email hosted service polls for inbound messages.
# Optional with default per StartupValidator.cs (line 206):
#   configuration["EMAIL_POLL_INTERVAL_MS"]
# Default: 60000 milliseconds (60 seconds).
# Maps to appsettings.json key: Scheduler.EmailPollIntervalMs
# -----------------------------------------------------------------------------
resource "aws_ssm_parameter" "email_poll_interval" {
  name        = "/splendidcrm/${var.environment}/email-poll-interval-ms"
  type        = "String"
  value       = "60000"
  description = "Email poll interval in milliseconds"

  tags = {
    Name = "${var.name_prefix}-ssm-email-poll-interval"
  }
}

# -----------------------------------------------------------------------------
# Parameter 5: Archive Job Interval
# -----------------------------------------------------------------------------
# Controls how often the archive hosted service processes archival tasks.
# Optional with default per StartupValidator.cs (line 213):
#   configuration["ARCHIVE_INTERVAL_MS"]
# Default: 300000 milliseconds (5 minutes).
# Maps to appsettings.json key: Scheduler.ArchiveIntervalMs
# -----------------------------------------------------------------------------
resource "aws_ssm_parameter" "archive_interval" {
  name        = "/splendidcrm/${var.environment}/archive-interval-ms"
  type        = "String"
  value       = "300000"
  description = "Archive job interval in milliseconds"

  tags = {
    Name = "${var.name_prefix}-ssm-archive-interval"
  }
}

# -----------------------------------------------------------------------------
# Parameter 6: Application Log Level
# -----------------------------------------------------------------------------
# Default log level for the application. Can be overridden per environment
# to control verbosity. Not explicitly validated by StartupValidator.cs
# but consumed by the logging configuration in Program.cs.
#
# Valid values: Trace, Debug, Information, Warning, Error, Critical, None
# Default: "Information" — standard operational logging level.
# Maps to appsettings.json key: Logging.LogLevel.Default
# -----------------------------------------------------------------------------
resource "aws_ssm_parameter" "log_level" {
  name        = "/splendidcrm/${var.environment}/log-level"
  type        = "String"
  value       = "Information"
  description = "Default log level for the application"

  tags = {
    Name = "${var.name_prefix}-ssm-log-level"
  }
}

# -----------------------------------------------------------------------------
# Parameter 7: Duo Security API Hostname
# -----------------------------------------------------------------------------
# The Duo Security API hostname for two-factor authentication. This is
# optional per StartupValidator.cs (line 224):
#   configuration["DUO_API_HOSTNAME"]
#
# Must be provided together with DUO_INTEGRATION_KEY and DUO_SECRET_KEY
# secrets for Duo 2FA to function. Stored as a parameter (not a secret)
# because the API hostname is not a credential.
# Default: empty string (Duo 2FA disabled).
# Maps to appsettings.json key: Duo.ApiHostname
# -----------------------------------------------------------------------------
resource "aws_ssm_parameter" "duo_api_hostname" {
  name        = "/splendidcrm/${var.environment}/duo-api-hostname"
  type        = "String"
  value       = ""
  description = "Duo Security API hostname"

  tags = {
    Name = "${var.name_prefix}-ssm-duo-api-hostname"
  }
}

# -----------------------------------------------------------------------------
# Parameter 8: CORS Allowed Origins
# -----------------------------------------------------------------------------
# Comma-separated list of allowed origins for CORS policy. Validated by
# StartupValidator.cs (line 160) as REQUIRED:
#   configuration["CORS_ORIGINS"]
#
# For same-origin ALB deployments (Guardrail G8), this should be empty
# string — the frontend and backend share the same ALB DNS name, so
# CORS is not needed. API_BASE_URL in the frontend is also empty for
# same-origin cookie architecture.
# Default: empty string (same-origin ALB deployment).
# Maps to appsettings.json key: Cors.AllowedOrigins
# -----------------------------------------------------------------------------
resource "aws_ssm_parameter" "cors_origins" {
  name        = "/splendidcrm/${var.environment}/cors-origins"
  type        = "String"
  value       = ""
  description = "CORS allowed origins (empty for same-origin ALB)"

  tags = {
    Name = "${var.name_prefix}-ssm-cors-origins"
  }
}
