# Dev environment sizing and configuration
#
# All values in this file are specific to the development environment.
# They follow the smallest/cheapest tier per the AAP §0.8.3 environment
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
  # ECS Fargate Task Sizing (Dev Tier)
  # ---------------------------------------------------------------------------
  # Uniform sizing for both backend and frontend tasks per ACME standard.
  # Backend runs .NET 10 (Kestrel) on port 8080; frontend runs Nginx on port 80.

  task_cpu          = 512  # 0.5 vCPU — minimum Fargate CPU for dev
  task_memory       = 1024 # 1024 MB (1 GB) — minimum viable for .NET 10 + Nginx
  task_ephemeral_gb = 21   # 21 GB ephemeral storage — Fargate minimum
  min_tasks         = 1    # Minimum 1 running task for dev environment
  max_tasks         = 4    # Maximum 4 tasks for dev auto-scaling

  # ---------------------------------------------------------------------------
  # RDS SQL Server Instance Sizing (Dev Tier)
  # ---------------------------------------------------------------------------
  # Cost-effective instance class for development workloads.

  rds_instance_class        = "db.t3.medium" # Cost-effective for dev SQL Server
  rds_allocated_storage     = 20             # 20 GB initial storage for dev
  rds_max_allocated_storage = 100            # 100 GB autoscaling threshold for dev

  # ---------------------------------------------------------------------------
  # Observability Configuration
  # ---------------------------------------------------------------------------
  # Shorter retention for dev to minimize CloudWatch costs.

  log_retention_days = 30 # 30-day CloudWatch log retention for dev
}
