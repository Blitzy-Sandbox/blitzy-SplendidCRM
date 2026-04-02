# Production environment sizing and configuration
#
# All values in this file are specific to the production environment.
# They follow the maximum / production-grade tier per the AAP §0.8.3
# environment sizing table. These locals are consumed by main.tf → module "common".
#
# Key production-specific design decisions:
#   - db.r5.large is memory-optimized (vs. db.t3 burstable for dev/staging)
#     for sustained SQL Server workloads under production load.
#   - Multi-AZ RDS, 35-day backup retention, and deletion protection are
#     enabled via the common module conditional (var.environment == "prod").
#   - 90-day log retention meets compliance requirements for production audit trails.
#
# Environment-Specific Sizing Reference:
# ┌─────────────┬──────────┬────────────┬───────────────────┬───────────┬───────────┐
# │ Environment │ Task CPU │ Task Memory│ Ephemeral Storage │ Min Tasks │ Max Tasks │
# ├─────────────┼──────────┼────────────┼───────────────────┼───────────┼───────────┤
# │ Dev         │ 512      │ 1024 MB    │ 21 GB             │ 1         │ 4         │
# │ Staging     │ 1024     │ 2048 MB    │ 30 GB             │ 2         │ 6         │
# │ Production  │ 2048     │ 4096 MB    │ 50 GB             │ 2         │ 10        │
# └─────────────┴──────────┴────────────┴───────────────────┴───────────┴───────────┘

locals {
  # ---------------------------------------------------------------------------
  # ECS Fargate Task Sizing (Production Tier)
  # ---------------------------------------------------------------------------
  # Uniform sizing for both backend and frontend tasks per ACME standard.
  # Backend runs .NET 10 (Kestrel) on port 8080; frontend runs Nginx on port 80.

  task_cpu          = 2048 # 2 vCPU — maximum Fargate tier for production workloads
  task_memory       = 4096 # 4096 MB (4 GB) — production-grade for .NET 10 + Nginx
  task_ephemeral_gb = 50   # 50 GB ephemeral storage — production maximum
  min_tasks         = 2    # Minimum 2 running tasks for production high availability
  max_tasks         = 10   # Maximum 10 tasks for production auto-scaling

  # ---------------------------------------------------------------------------
  # RDS SQL Server Instance Sizing (Production Tier)
  # ---------------------------------------------------------------------------
  # Memory-optimized instance class for production SQL Server workloads.

  rds_instance_class        = "db.r5.large" # Memory-optimized for production SQL Server
  rds_allocated_storage     = 100           # 100 GB initial storage for production
  rds_max_allocated_storage = 500           # 500 GB autoscaling threshold for production

  # ---------------------------------------------------------------------------
  # Observability Configuration
  # ---------------------------------------------------------------------------
  # Extended retention for production — compliance requirement for audit trails.

  log_retention_days = 90 # 90-day CloudWatch log retention for production compliance
}
