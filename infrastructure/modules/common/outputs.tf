# =============================================================================
# SplendidCRM Common Module — Terraform Outputs
# =============================================================================
#
# Purpose:
#   Define the 10 required Terraform outputs that expose critical resource
#   identifiers, endpoints, and ARNs to the calling environment layer
#   (dev/staging/prod/localstack). These outputs are consumed by:
#     - CI/CD scripts (build-and-push.sh) for ECR repository URLs
#     - Deployment orchestration (deploy-schema.sh) for RDS endpoint
#     - Operational dashboards and monitoring for cluster/log identifiers
#     - Cross-stack references when composing with other Terraform modules
#
# Outputs Defined (10 total):
#   1. backend_ecr_repository_url   — ECR URL for backend image push/pull
#   2. frontend_ecr_repository_url  — ECR URL for frontend image push/pull
#   3. ecs_cluster_name             — ECS cluster name for service deployment
#   4. alb_dns_name                 — ALB DNS name for application access
#   5. cloudwatch_log_group_name    — CloudWatch log group for log streaming
#   6. cloudwatch_log_stream_name   — CloudWatch log stream name
#   7. alb_security_group_id        — ALB security group ID
#   8. backend_security_group_id    — Backend ECS service security group ID
#   9. frontend_security_group_id   — Frontend ECS service security group ID
#  10. rds_endpoint                 — RDS SQL Server endpoint (sensitive)
#
# Resource Cross-References (reads from sibling .tf files):
#   ecr.tf             → aws_ecr_repository.backend.repository_url
#                        aws_ecr_repository.frontend.repository_url
#   ecs-fargate.tf     → aws_ecs_cluster.main.name
#   alb.tf             → aws_lb.main.dns_name
#   cloudwatch.tf      → aws_cloudwatch_log_group.application.name
#                        aws_cloudwatch_log_stream.ecs.name
#   security-groups.tf → aws_security_group.alb.id
#                        aws_security_group.backend.id
#                        aws_security_group.frontend.id
#   rds.tf             → aws_db_instance.main.endpoint
#
# Sensitivity:
#   - rds_endpoint is marked sensitive (contains database host information)
#   - ECR URLs are NOT sensitive (required in CI/CD pipeline stdout)
#   - Security group IDs are NOT sensitive (informational for operators)
#
# ACME Compliance:
#   - No tfe.acme.com references (Guardrail G10)
#   - All outputs reference standard aws_* resource attributes
#   - No hardcoded values — every output is a dynamic resource reference
# =============================================================================

# -----------------------------------------------------------------------------
# Output 1: Backend ECR Repository URL
# -----------------------------------------------------------------------------
# Full ECR repository URL for the backend Docker image. Used by CI/CD scripts
# (build-and-push.sh) to tag and push backend images after local validation.
# Format: <account_id>.dkr.ecr.<region>.amazonaws.com/<name_prefix>-backend
# -----------------------------------------------------------------------------
output "backend_ecr_repository_url" {
  description = "Full ECR repository URL for the SplendidCRM backend Docker image. Used by CI/CD scripts to tag and push images."
  value       = aws_ecr_repository.backend.repository_url
}

# -----------------------------------------------------------------------------
# Output 2: Frontend ECR Repository URL
# -----------------------------------------------------------------------------
# Full ECR repository URL for the frontend Docker image. Used by CI/CD scripts
# (build-and-push.sh) to tag and push frontend images after local validation.
# Format: <account_id>.dkr.ecr.<region>.amazonaws.com/<name_prefix>-frontend
# -----------------------------------------------------------------------------
output "frontend_ecr_repository_url" {
  description = "Full ECR repository URL for the SplendidCRM frontend Docker image. Used by CI/CD scripts to tag and push images."
  value       = aws_ecr_repository.frontend.repository_url
}

# -----------------------------------------------------------------------------
# Output 3: ECS Cluster Name
# -----------------------------------------------------------------------------
# The name of the ECS Fargate cluster hosting both backend and frontend
# services. Used by deployment scripts for service updates and by operators
# for ECS Exec troubleshooting sessions.
# -----------------------------------------------------------------------------
output "ecs_cluster_name" {
  description = "Name of the ECS Fargate cluster hosting SplendidCRM backend and frontend services."
  value       = aws_ecs_cluster.main.name
}

# -----------------------------------------------------------------------------
# Output 4: ALB DNS Name
# -----------------------------------------------------------------------------
# The DNS name of the internal Application Load Balancer. This is the single
# entry point for all SplendidCRM traffic, implementing path-based routing
# between backend and frontend services. Used for application access and
# health verification after deployment.
# -----------------------------------------------------------------------------
output "alb_dns_name" {
  description = "DNS name of the internal ALB serving SplendidCRM. Use this URL to access the application after deployment."
  value       = aws_lb.main.dns_name
}

# -----------------------------------------------------------------------------
# Output 5: CloudWatch Log Group Name
# -----------------------------------------------------------------------------
# The name of the CloudWatch log group where both backend and frontend ECS
# tasks stream their container logs via the awslogs driver. Used for log
# queries, alarm configuration, and operational monitoring.
# -----------------------------------------------------------------------------
output "cloudwatch_log_group_name" {
  description = "CloudWatch log group name for SplendidCRM ECS container logs. Use for log queries and alarm configuration."
  value       = aws_cloudwatch_log_group.application.name
}

# -----------------------------------------------------------------------------
# Output 6: CloudWatch Log Stream Name
# -----------------------------------------------------------------------------
# The name of the pre-created CloudWatch log stream for ECS container logs.
# Individual container streams are auto-created by the awslogs driver using
# the pattern: {stream-prefix}/{container-name}/{task-id}.
# -----------------------------------------------------------------------------
output "cloudwatch_log_stream_name" {
  description = "CloudWatch log stream name for SplendidCRM ECS logs."
  value       = aws_cloudwatch_log_stream.ecs.name
}

# -----------------------------------------------------------------------------
# Output 7: ALB Security Group ID
# -----------------------------------------------------------------------------
# The security group ID attached to the internal ALB. Allows inbound HTTP/HTTPS
# from VPC CIDR and outbound to backend (8080) and frontend (80) target groups.
# Useful for network debugging and additional security group rule configuration.
# -----------------------------------------------------------------------------
output "alb_security_group_id" {
  description = "Security group ID for the SplendidCRM internal ALB. Allows VPC CIDR inbound on ports 80/443."
  value       = aws_security_group.alb.id
}

# -----------------------------------------------------------------------------
# Output 8: Backend Security Group ID
# -----------------------------------------------------------------------------
# The security group ID for the backend ECS Fargate tasks. Allows inbound
# from ALB on port 8080 (G2) and outbound to RDS on port 1433 plus HTTPS
# for AWS API access (Secrets Manager, Parameter Store, KMS).
# -----------------------------------------------------------------------------
output "backend_security_group_id" {
  description = "Security group ID for backend ECS tasks. Allows ALB inbound on port 8080 and RDS/AWS API outbound."
  value       = aws_security_group.backend.id
}

# -----------------------------------------------------------------------------
# Output 9: Frontend Security Group ID
# -----------------------------------------------------------------------------
# The security group ID for the frontend ECS Fargate tasks. Allows inbound
# from ALB on port 80. The frontend Nginx container has no direct access to
# RDS or Secrets Manager (least-privilege isolation).
# -----------------------------------------------------------------------------
output "frontend_security_group_id" {
  description = "Security group ID for frontend ECS tasks. Allows ALB inbound on port 80 only."
  value       = aws_security_group.frontend.id
}

# -----------------------------------------------------------------------------
# Output 10: RDS SQL Server Endpoint
# -----------------------------------------------------------------------------
# The RDS SQL Server instance endpoint in host:port format. Used by the
# deploy-schema.sh script for database provisioning (Build.sql +
# SplendidSessions DDL) and for constructing the connection string injected
# into the backend ECS task definition via Secrets Manager.
#
# SENSITIVE: Marked sensitive because it contains the database host address,
# which is an internal infrastructure detail that should not appear in
# Terraform plan output or CI/CD logs.
# -----------------------------------------------------------------------------
output "rds_endpoint" {
  description = "RDS SQL Server endpoint (host:port) for database connectivity. Used by deploy-schema.sh and connection string construction."
  value       = aws_db_instance.main.endpoint
  sensitive   = true
}
