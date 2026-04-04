# =============================================================================
# CloudWatch Monitoring Alarms — SplendidCRM ECS Fargate Infrastructure
# =============================================================================
#
# This file defines 9 CloudWatch metric alarms for operational monitoring of
# the SplendidCRM ECS Fargate deployment. Alarms cover:
#   - ALB 5xx error rates (backend + frontend target groups)
#   - ALB unhealthy host counts (backend + frontend target groups)
#   - ECS CPU and memory utilization (backend service)
#   - RDS CPU utilization, connection count, and free storage space
#
# Alarm actions are conditional on var.alarm_sns_arn — when empty (default),
# alarms still change state but send no notifications. This allows safe
# deployment in LocalStack and dev environments without requiring an SNS topic.
#
# Resource references:
#   - aws_lb.main                    → alb.tf
#   - aws_lb_target_group.backend    → alb.tf
#   - aws_lb_target_group.frontend   → alb.tf
#   - aws_ecs_cluster.main           → ecs-fargate.tf
#   - aws_db_instance.main           → rds.tf
#   - var.name_prefix                → variables.tf
#   - var.alarm_sns_arn              → variables.tf
#   - var.rds_allocated_storage      → variables.tf
# =============================================================================

# -----------------------------------------------------------------------------
# ALB 5xx Error Rate Alarms
# -----------------------------------------------------------------------------
# Monitor HTTP 5xx responses from each target group. A single 5xx response
# within a 5-minute window triggers the alarm, providing early detection of
# application errors or service degradation.
# -----------------------------------------------------------------------------

resource "aws_cloudwatch_metric_alarm" "backend_5xx_rate" {
  alarm_name          = "${var.name_prefix}-backend-5xx-rate"
  alarm_description   = "Backend target group returning HTTP 5xx errors. Investigate backend ECS task logs in CloudWatch log group ${var.name_prefix}-ecs-logs."
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = 1
  metric_name         = "HTTPCode_Target_5XX_Count"
  namespace           = "AWS/ApplicationELB"
  period              = 300
  statistic           = "Sum"
  threshold           = 1
  treat_missing_data  = "notBreaching"

  dimensions = {
    TargetGroup  = aws_lb_target_group.backend.arn_suffix
    LoadBalancer = aws_lb.main.arn_suffix
  }

  alarm_actions = var.alarm_sns_arn != "" ? [var.alarm_sns_arn] : []
  ok_actions    = var.alarm_sns_arn != "" ? [var.alarm_sns_arn] : []

  tags = {
    Name        = "${var.name_prefix}-backend-5xx-rate"
    Component   = "monitoring"
    ServiceType = "backend"
  }
}

resource "aws_cloudwatch_metric_alarm" "frontend_5xx_rate" {
  alarm_name          = "${var.name_prefix}-frontend-5xx-rate"
  alarm_description   = "Frontend target group returning HTTP 5xx errors. Investigate Nginx container logs in CloudWatch log group ${var.name_prefix}-ecs-logs."
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = 1
  metric_name         = "HTTPCode_Target_5XX_Count"
  namespace           = "AWS/ApplicationELB"
  period              = 300
  statistic           = "Sum"
  threshold           = 1
  treat_missing_data  = "notBreaching"

  dimensions = {
    TargetGroup  = aws_lb_target_group.frontend.arn_suffix
    LoadBalancer = aws_lb.main.arn_suffix
  }

  alarm_actions = var.alarm_sns_arn != "" ? [var.alarm_sns_arn] : []
  ok_actions    = var.alarm_sns_arn != "" ? [var.alarm_sns_arn] : []

  tags = {
    Name        = "${var.name_prefix}-frontend-5xx-rate"
    Component   = "monitoring"
    ServiceType = "frontend"
  }
}

# -----------------------------------------------------------------------------
# ALB Unhealthy Host Alarms
# -----------------------------------------------------------------------------
# Monitor unhealthy task counts in each target group. Two consecutive 60-second
# evaluation periods with >= 1 unhealthy host triggers the alarm, indicating
# ECS task health check failures or container crashes.
# -----------------------------------------------------------------------------

resource "aws_cloudwatch_metric_alarm" "backend_unhealthy_hosts" {
  alarm_name          = "${var.name_prefix}-backend-unhealthy-hosts"
  alarm_description   = "One or more backend ECS tasks failing ALB health checks. Check /api/health endpoint and backend container logs."
  comparison_operator = "GreaterThanOrEqualToThreshold"
  evaluation_periods  = 2
  metric_name         = "UnHealthyHostCount"
  namespace           = "AWS/ApplicationELB"
  period              = 60
  statistic           = "Maximum"
  threshold           = 1
  treat_missing_data  = "notBreaching"

  dimensions = {
    TargetGroup  = aws_lb_target_group.backend.arn_suffix
    LoadBalancer = aws_lb.main.arn_suffix
  }

  alarm_actions = var.alarm_sns_arn != "" ? [var.alarm_sns_arn] : []
  ok_actions    = var.alarm_sns_arn != "" ? [var.alarm_sns_arn] : []

  tags = {
    Name        = "${var.name_prefix}-backend-unhealthy-hosts"
    Component   = "monitoring"
    ServiceType = "backend"
  }
}

resource "aws_cloudwatch_metric_alarm" "frontend_unhealthy_hosts" {
  alarm_name          = "${var.name_prefix}-frontend-unhealthy-hosts"
  alarm_description   = "One or more frontend ECS tasks failing ALB health checks. Check /health endpoint and Nginx container logs."
  comparison_operator = "GreaterThanOrEqualToThreshold"
  evaluation_periods  = 2
  metric_name         = "UnHealthyHostCount"
  namespace           = "AWS/ApplicationELB"
  period              = 60
  statistic           = "Maximum"
  threshold           = 1
  treat_missing_data  = "notBreaching"

  dimensions = {
    TargetGroup  = aws_lb_target_group.frontend.arn_suffix
    LoadBalancer = aws_lb.main.arn_suffix
  }

  alarm_actions = var.alarm_sns_arn != "" ? [var.alarm_sns_arn] : []
  ok_actions    = var.alarm_sns_arn != "" ? [var.alarm_sns_arn] : []

  tags = {
    Name        = "${var.name_prefix}-frontend-unhealthy-hosts"
    Component   = "monitoring"
    ServiceType = "frontend"
  }
}

# -----------------------------------------------------------------------------
# ECS Service CPU and Memory Alarms
# -----------------------------------------------------------------------------
# Monitor backend ECS service resource utilization. These alarms complement
# the auto-scaling policies (70% threshold in ecs-fargate.tf) by alerting
# when utilization exceeds 85% — indicating the service is under sustained
# high load and may require manual investigation or scaling policy adjustment.
# -----------------------------------------------------------------------------

resource "aws_cloudwatch_metric_alarm" "backend_cpu" {
  alarm_name          = "${var.name_prefix}-backend-cpu"
  alarm_description   = "Backend ECS service CPU utilization exceeds 85%. Auto-scaling triggers at 70% — if this alarm fires, scaling may be insufficient for current load."
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = 1
  metric_name         = "CPUUtilization"
  namespace           = "AWS/ECS"
  period              = 300
  statistic           = "Average"
  threshold           = 85
  treat_missing_data  = "notBreaching"

  dimensions = {
    ClusterName = aws_ecs_cluster.main.name
    ServiceName = "${var.name_prefix}-backend-svc"
  }

  alarm_actions = var.alarm_sns_arn != "" ? [var.alarm_sns_arn] : []
  ok_actions    = var.alarm_sns_arn != "" ? [var.alarm_sns_arn] : []

  tags = {
    Name        = "${var.name_prefix}-backend-cpu"
    Component   = "monitoring"
    ServiceType = "backend"
  }
}

resource "aws_cloudwatch_metric_alarm" "backend_memory" {
  alarm_name          = "${var.name_prefix}-backend-memory"
  alarm_description   = "Backend ECS service memory utilization exceeds 85%. Auto-scaling triggers at 70% — if this alarm fires, scaling may be insufficient or a memory leak may be present."
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = 1
  metric_name         = "MemoryUtilization"
  namespace           = "AWS/ECS"
  period              = 300
  statistic           = "Average"
  threshold           = 85
  treat_missing_data  = "notBreaching"

  dimensions = {
    ClusterName = aws_ecs_cluster.main.name
    ServiceName = "${var.name_prefix}-backend-svc"
  }

  alarm_actions = var.alarm_sns_arn != "" ? [var.alarm_sns_arn] : []
  ok_actions    = var.alarm_sns_arn != "" ? [var.alarm_sns_arn] : []

  tags = {
    Name        = "${var.name_prefix}-backend-memory"
    Component   = "monitoring"
    ServiceType = "backend"
  }
}

# -----------------------------------------------------------------------------
# RDS SQL Server Alarms
# -----------------------------------------------------------------------------
# Monitor RDS instance health: CPU utilization, active connection count, and
# available storage space. These alarms detect database-layer issues before
# they cascade into application-level errors visible to users.
#
# The free storage alarm uses a dynamic threshold: 20% of the allocated storage
# (var.rds_allocated_storage) converted to bytes. This ensures the threshold
# scales automatically when storage is increased across environments.
# -----------------------------------------------------------------------------

resource "aws_cloudwatch_metric_alarm" "rds_cpu" {
  alarm_name          = "${var.name_prefix}-rds-cpu"
  alarm_description   = "RDS SQL Server CPU utilization exceeds 80%. Investigate slow queries, missing indexes, or consider scaling the instance class."
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = 1
  metric_name         = "CPUUtilization"
  namespace           = "AWS/RDS"
  period              = 300
  statistic           = "Average"
  threshold           = 80
  treat_missing_data  = "notBreaching"

  dimensions = {
    DBInstanceIdentifier = aws_db_instance.main.identifier
  }

  alarm_actions = var.alarm_sns_arn != "" ? [var.alarm_sns_arn] : []
  ok_actions    = var.alarm_sns_arn != "" ? [var.alarm_sns_arn] : []

  tags = {
    Name        = "${var.name_prefix}-rds-cpu"
    Component   = "monitoring"
    ServiceType = "database"
  }
}

resource "aws_cloudwatch_metric_alarm" "rds_connections" {
  alarm_name          = "${var.name_prefix}-rds-connections"
  alarm_description   = "RDS SQL Server active connections exceed 80. Investigate connection pool exhaustion or leaked connections in the backend application."
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = 1
  metric_name         = "DatabaseConnections"
  namespace           = "AWS/RDS"
  period              = 300
  statistic           = "Average"
  threshold           = 80
  treat_missing_data  = "notBreaching"

  dimensions = {
    DBInstanceIdentifier = aws_db_instance.main.identifier
  }

  alarm_actions = var.alarm_sns_arn != "" ? [var.alarm_sns_arn] : []
  ok_actions    = var.alarm_sns_arn != "" ? [var.alarm_sns_arn] : []

  tags = {
    Name        = "${var.name_prefix}-rds-connections"
    Component   = "monitoring"
    ServiceType = "database"
  }
}

resource "aws_cloudwatch_metric_alarm" "rds_storage" {
  alarm_name          = "${var.name_prefix}-rds-free-storage"
  alarm_description   = "RDS SQL Server free storage below 20% of allocated (${var.rds_allocated_storage} GB). Storage autoscaling may extend to max; investigate data growth or enable log cleanup."
  comparison_operator = "LessThanThreshold"
  evaluation_periods  = 1
  metric_name         = "FreeStorageSpace"
  namespace           = "AWS/RDS"
  period              = 900
  statistic           = "Average"
  # 20% of allocated storage in bytes (GB → bytes: * 1024^3)
  threshold          = var.rds_allocated_storage * 0.2 * 1024 * 1024 * 1024
  treat_missing_data = "notBreaching"

  dimensions = {
    DBInstanceIdentifier = aws_db_instance.main.identifier
  }

  alarm_actions = var.alarm_sns_arn != "" ? [var.alarm_sns_arn] : []
  ok_actions    = var.alarm_sns_arn != "" ? [var.alarm_sns_arn] : []

  tags = {
    Name        = "${var.name_prefix}-rds-free-storage"
    Component   = "monitoring"
    ServiceType = "database"
  }
}
