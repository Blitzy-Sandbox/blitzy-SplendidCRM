# LocalStack validation environment sizing and configuration
#
# Dev-equivalent sizing per AAP — LocalStack is for infrastructure validation only.
# All values in this file are IDENTICAL to the dev environment because LocalStack
# exists to validate Terraform plans and resource creation, not production workloads.
#
# These locals are consumed by main.tf → module "common".
#
# Environment-Specific Sizing Reference (AAP §0.8.3):
# ┌─────────────┬──────────┬────────────┬───────────────────┬───────────┬───────────┐
# │ Environment │ Task CPU │ Task Memory│ Ephemeral Storage │ Min Tasks │ Max Tasks │
# ├─────────────┼──────────┼────────────┼───────────────────┼───────────┼───────────┤
# │ Dev         │ 512      │ 1024 MB    │ 21 GB             │ 1         │ 4         │
# │ Staging     │ 1024     │ 2048 MB    │ 30 GB             │ 2         │ 6         │
# │ Production  │ 2048     │ 4096 MB    │ 50 GB             │ 2         │ 10        │
# └─────────────┴──────────┴────────────┴───────────────────┴───────────┴───────────┘
# LocalStack uses Dev row values (identical sizing).

locals {
  # ---------------------------------------------------------------------------
  # ECS Fargate Task Sizing (Dev-Equivalent Tier for LocalStack)
  # ---------------------------------------------------------------------------
  # Uniform sizing for both backend and frontend tasks per ACME standard.
  # Backend runs .NET 10 (Kestrel) on port 8080; frontend runs Nginx on port 80.

  task_cpu          = 512  # 0.5 vCPU — minimum Fargate CPU (dev-equivalent)
  task_memory       = 1024 # 1024 MB (1 GB) — minimum viable for .NET 10 + Nginx
  task_ephemeral_gb = 21   # 21 GB ephemeral storage — Fargate minimum
  min_tasks         = 1    # Minimum 1 running task (dev-equivalent)
  max_tasks         = 4    # Maximum 4 tasks for auto-scaling (dev-equivalent)

  # ---------------------------------------------------------------------------
  # RDS SQL Server Instance Sizing (Dev-Equivalent Tier)
  # ---------------------------------------------------------------------------
  # Cost-effective instance class for LocalStack validation workloads.

  rds_instance_class        = "db.t3.medium" # Cost-effective for dev/localstack
  rds_allocated_storage     = 20             # 20 GB initial storage
  rds_max_allocated_storage = 100            # 100 GB autoscaling threshold

  # ---------------------------------------------------------------------------
  # Observability Configuration
  # ---------------------------------------------------------------------------
  # CloudWatch log retention matching dev environment.

  log_retention_days = 30 # 30-day CloudWatch log retention (dev-equivalent)
}
