# Staging environment sizing and configuration
#
# All values in this file are specific to the staging environment.
# They follow the mid-range tier per the AAP §0.8.3 environment
# sizing table. These locals are consumed by main.tf → module "common".
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
  # ECS Fargate Task Sizing (Staging Tier)
  # ---------------------------------------------------------------------------
  # Uniform sizing for both backend and frontend tasks per ACME standard.
  # Backend runs .NET 10 (Kestrel) on port 8080; frontend runs Nginx on port 80.

  task_cpu          = 1024 # 1 vCPU — mid-range Fargate CPU for staging
  task_memory       = 2048 # 2048 MB (2 GB) — mid-range for .NET 10 + Nginx
  task_ephemeral_gb = 30   # 30 GB ephemeral storage — mid-range for staging
  min_tasks         = 2    # Minimum 2 running tasks for staging availability
  max_tasks         = 6    # Maximum 6 tasks for staging auto-scaling

  # ---------------------------------------------------------------------------
  # RDS SQL Server Instance Sizing (Staging Tier)
  # ---------------------------------------------------------------------------
  # Mid-range instance class for staging workloads.

  rds_instance_class        = "db.t3.large" # Mid-range for staging SQL Server
  rds_allocated_storage     = 50            # 50 GB initial storage for staging
  rds_max_allocated_storage = 200           # 200 GB autoscaling threshold for staging

  # ---------------------------------------------------------------------------
  # Observability Configuration
  # ---------------------------------------------------------------------------
  # Mid-range retention for staging — longer than dev (30), shorter than prod (90).

  log_retention_days = 60 # 60-day CloudWatch log retention for staging
}
