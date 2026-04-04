# =============================================================================
# SplendidCRM Common Module — CloudWatch Observability
# =============================================================================
# Defines the centralized CloudWatch log group and log stream for ECS Fargate
# container log aggregation. Both backend and frontend ECS tasks send their
# stdout/stderr output to this log group via the `awslogs` log driver
# configured in their respective task definitions (ecs-fargate.tf).
#
# Resource Cross-References:
#   ecs-fargate.tf  → aws_cloudwatch_log_group.application.name  (logConfiguration awslogs-group)
#   iam.tf          → aws_cloudwatch_log_group.application.arn    (logs:CreateLogStream, logs:PutLogEvents)
#   outputs.tf      → aws_cloudwatch_log_group.application.name   (cloudwatch_log_group_name output)
#   outputs.tf      → aws_cloudwatch_log_stream.ecs.name          (cloudwatch_log_stream_name output)
#
# Variable Dependencies (from variables.tf):
#   var.name_prefix       → Naming prefix for uniqueness (e.g., splendidcrm-dev)
#   var.log_retention_days → Retention period in days (default 30; e.g., 30 dev, 90 prod)
#
# ACME Strategy Note:
#   This file uses standard aws_* resource blocks for LocalStack compatibility.
#   When deploying to real AWS with ACME private module registry access, swap
#   resource blocks to ACME module sources per the mapping table in deployment
#   documentation. No tfe.acme.com references are included (Guardrail G10).
# =============================================================================

# -----------------------------------------------------------------------------
# CloudWatch Log Group
# -----------------------------------------------------------------------------
# Centralized log group where both backend (Kestrel/.NET 10) and frontend
# (Nginx) ECS tasks stream their container logs. The ECS task definitions in
# ecs-fargate.tf configure the awslogs driver with different stream prefixes:
#   - Backend tasks use stream prefix "backend"
#   - Frontend tasks use stream prefix "frontend"
#
# Log group naming follows the AAP convention: /application_logs/{name_prefix}
# This allows per-environment isolation (e.g., /application_logs/splendidcrm-dev)
# while maintaining a consistent organizational prefix for CloudWatch Insights
# queries and alarm configurations.
#
# Retention is variable-driven to support per-environment policies:
#   - Dev:     30 days  (cost optimization)
#   - Staging: 90 days  (extended debugging)
#   - Prod:    365 days (compliance/audit requirements)
# -----------------------------------------------------------------------------

resource "aws_cloudwatch_log_group" "application" {
  name              = "/application_logs/${var.name_prefix}"
  retention_in_days = var.log_retention_days

  tags = {
    Name = "${var.name_prefix}-log-group"
  }
}

# -----------------------------------------------------------------------------
# CloudWatch Log Stream
# -----------------------------------------------------------------------------
# Pre-created log stream for ECS container logs. While the awslogs driver can
# auto-create streams using the stream prefix pattern, pre-creating a named
# stream ensures the IAM execution role's logs:CreateLogStream permission is
# validated at Terraform apply time rather than at first container startup.
#
# Stream naming follows the AAP convention: {name_prefix}-ecs-logs
# Individual container log streams are auto-created by the ECS awslogs driver
# using the pattern: {stream-prefix}/{container-name}/{task-id}
# -----------------------------------------------------------------------------

resource "aws_cloudwatch_log_stream" "ecs" {
  name           = "${var.name_prefix}-ecs-logs"
  log_group_name = aws_cloudwatch_log_group.application.name
}
