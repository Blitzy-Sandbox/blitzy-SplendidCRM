# =============================================================================
# RDS SQL Server Instance — SplendidCRM Infrastructure Module
# =============================================================================
#
# Purpose:
#   Define the RDS SQL Server instance deployed in private subnets with
#   per-environment instance sizing, associated with the RDS security group.
#   This is the database backend for SplendidCRM, provisioned with SQL Server
#   2022 (engine version 16.00) to match the local Docker development
#   environment (mcr.microsoft.com/mssql/server:2022-latest).
#
# Resources Created:
#   - aws_db_subnet_group.main  — DB subnet group for private subnet placement
#   - aws_db_instance.main      — RDS SQL Server instance
#
# Application Context:
#   - SplendidCRM uses Microsoft.Data.SqlClient 6.1.4 for database connectivity
#   - Connection string format:
#       Server=<rds-endpoint>;Database=SplendidCRM;User Id=splendidadmin;
#       Password=<managed>;TrustServerCertificate=True;
#   - Schema provisioned by scripts/deploy-schema.sh running Build.sql
#     (from SQL Scripts Community/) followed by SplendidSessions DDL
#   - Port 1433 is the default SQL Server port, controlled by RDS security group
#
# Environment-Conditional Behavior:
#   - Engine edition: sqlserver-ex (Express) for dev/staging/localstack;
#     sqlserver-se (Standard) for production (variable-driven)
#   - Multi-AZ: enabled only in production
#   - Backup retention: 35 days for production, 7 days for other environments
#   - Deletion protection: enabled only in production
#   - Final snapshot: required only in production; skipped in non-production
#
# Cross-File References:
#   Consumes:
#     - security-groups.tf → aws_security_group.rds.id
#     - kms.tf             → aws_kms_key.secrets.arn (storage encryption)
#     - variables.tf       → var.name_prefix, var.environment, var.db_subnet_ids,
#                            var.rds_instance_class, var.rds_allocated_storage,
#                            var.rds_max_allocated_storage
#   Referenced BY:
#     - outputs.tf         → aws_db_instance.main.endpoint
#
# Security:
#   - Storage encrypted at rest using the KMS Customer Managed Key (CMK)
#     defined in kms.tf — NOT the AWS default key (aws/rds)
#   - publicly_accessible = false — instance resides in private subnets only
#   - AWS manages the master user password via Secrets Manager
#     (manage_master_user_password = true)
#   - Only the backend ECS security group can reach port 1433
#
# ACME Compliance:
#   - No tfe.acme.com references (Guardrail G10)
#   - Uses standard aws_db_instance resource for LocalStack compatibility
#   - ACME module swap: aws_db_instance → tfe.acme.com/acme/rds/aws
# =============================================================================

# -----------------------------------------------------------------------------
# 1. DB Subnet Group
# -----------------------------------------------------------------------------
# Places the RDS instance in private subnets discovered by the environment
# layer's tag-based data sources. These subnets have no internet gateway
# route, ensuring the database is not publicly reachable even if
# publicly_accessible were accidentally set to true.
#
# The subnet IDs are passed from the environment layer via var.db_subnet_ids,
# which are populated from VPC subnet data source lookups filtered by the
# tag pattern *-db-* or *-data-* (environment-specific).
# -----------------------------------------------------------------------------
resource "aws_db_subnet_group" "main" {
  name        = "${var.name_prefix}-db-subnet-group"
  description = "DB subnet group for ${var.name_prefix} RDS instance"
  subnet_ids  = var.db_subnet_ids

  tags = {
    Name = "${var.name_prefix}-db-subnet-group"
  }
}

# -----------------------------------------------------------------------------
# 2. RDS SQL Server Instance
# -----------------------------------------------------------------------------
# Provisions a SQL Server instance with the following key design decisions:
#
# Engine Selection:
#   - Production uses sqlserver-se (Standard Edition) for Multi-AZ support,
#     higher connection limits, and enterprise feature access.
#   - Dev/Staging/LocalStack uses sqlserver-ex (Express Edition) for cost
#     optimization. Express has a 10 GB database size limit, which is
#     sufficient for development and staging workloads.
#
# Storage:
#   - gp3 storage type provides baseline 3,000 IOPS and 125 MiB/s throughput
#     without provisioned IOPS charges, suitable for the CRM workload.
#   - Storage autoscaling expands up to rds_max_allocated_storage when usage
#     exceeds 90% of allocated storage for 5+ minutes.
#   - Encrypted at rest with the KMS CMK (alias/splendidcrm-{env}-secrets)
#     to satisfy the requirement that all data at rest uses customer-managed
#     encryption keys.
#
# Password Management:
#   - manage_master_user_password = true delegates password lifecycle to AWS
#     Secrets Manager. AWS automatically generates a strong password, stores
#     it in Secrets Manager, and handles rotation. This avoids storing the
#     password in Terraform state or version control.
#
# Maintenance Windows:
#   - Backup window:      03:00-04:00 UTC (low-traffic window)
#   - Maintenance window:  Mon:04:00-Mon:05:00 UTC (after backup completes)
#   - These windows do not overlap, preventing maintenance from interrupting
#     backup operations.
#
# Production Safeguards:
#   - Multi-AZ: Provides automatic failover to a standby replica in another
#     Availability Zone. Only enabled for production to control costs.
#   - Deletion protection: Prevents accidental terraform destroy in production.
#   - Final snapshot: Required in production to preserve data before deletion.
#   - 35-day backup retention in production (vs 7 days in non-production)
#     supports compliance and disaster recovery requirements.
#
# License:
#   - license-included covers SQL Server Express and Standard editions.
#     No separate BYOL license management is required.
# -----------------------------------------------------------------------------
resource "aws_db_instance" "main" {
  identifier = "${var.name_prefix}-sqlserver"

  # ---------------------------------------------------------------------------
  # Engine Configuration
  # ---------------------------------------------------------------------------
  # SQL Server 2022 (engine version 16.00) matches the local Docker dev
  # environment (mcr.microsoft.com/mssql/server:2022-latest) used in
  # scripts/build-and-run.sh, ensuring schema and behavioral parity between
  # local development and cloud deployment.
  #
  # Production uses Standard Edition (sqlserver-se) for Multi-AZ support and
  # higher resource limits. Non-production uses Express (sqlserver-ex) for
  # cost optimization — Express supports up to 10 GB databases and 1 GB RAM,
  # which is adequate for development and staging workloads.
  # ---------------------------------------------------------------------------
  engine         = var.environment == "prod" ? "sqlserver-se" : "sqlserver-ex"
  engine_version = "16.00"
  license_model  = "license-included"

  # ---------------------------------------------------------------------------
  # Instance Sizing
  # ---------------------------------------------------------------------------
  # Instance class is variable-driven from the environment layer:
  #   Dev:        db.t3.medium  (2 vCPU, 4 GB RAM)
  #   Staging:    db.t3.medium  (or larger per staging.auto.tfvars)
  #   Production: db.r6i.xlarge (4 vCPU, 32 GB RAM) or larger per prod.auto.tfvars
  # ---------------------------------------------------------------------------
  instance_class = var.rds_instance_class

  # ---------------------------------------------------------------------------
  # Storage Configuration
  # ---------------------------------------------------------------------------
  # gp3 provides consistent baseline performance (3,000 IOPS, 125 MiB/s)
  # without the burst-credit model of gp2. Autoscaling handles growth beyond
  # the initial allocation without manual intervention.
  # ---------------------------------------------------------------------------
  allocated_storage     = var.rds_allocated_storage
  max_allocated_storage = var.rds_max_allocated_storage
  storage_type          = "gp3"
  storage_encrypted     = true
  kms_key_id            = aws_kms_key.secrets.arn

  # ---------------------------------------------------------------------------
  # Network Configuration
  # ---------------------------------------------------------------------------
  # The instance is placed in private subnets with security group access
  # restricted to the backend ECS tasks only (port 1433 inbound from
  # aws_security_group.backend per security-groups.tf).
  # ---------------------------------------------------------------------------
  db_subnet_group_name   = aws_db_subnet_group.main.name
  vpc_security_group_ids = [aws_security_group.rds.id]
  publicly_accessible    = false

  # ---------------------------------------------------------------------------
  # Authentication
  # ---------------------------------------------------------------------------
  # The master username 'splendidadmin' is used for initial schema provisioning
  # by scripts/deploy-schema.sh and for runtime database access by the
  # SplendidCRM backend application.
  #
  # manage_master_user_password = true instructs AWS to:
  #   1. Generate a cryptographically strong password
  #   2. Store it in AWS Secrets Manager (encrypted with the default RDS key)
  #   3. Return the secret ARN in aws_db_instance.main.master_user_secret
  #
  # The application's connection string (stored in a separate Secrets Manager
  # secret defined in secrets.tf) must be updated with this managed password
  # after initial provisioning.
  # ---------------------------------------------------------------------------
  username                    = "splendidadmin"
  manage_master_user_password = true

  # ---------------------------------------------------------------------------
  # High Availability
  # ---------------------------------------------------------------------------
  # Multi-AZ provides automatic failover to a standby replica in another AZ.
  # Only enabled for production to balance cost vs. availability requirements.
  # Note: SQL Server Express (sqlserver-ex) does NOT support Multi-AZ —
  # this conditional ensures Multi-AZ is only set when using Standard Edition.
  # ---------------------------------------------------------------------------
  multi_az = var.environment == "prod" ? true : false

  # ---------------------------------------------------------------------------
  # Backup Configuration
  # ---------------------------------------------------------------------------
  # Production retains 35 days of automated backups for compliance and
  # disaster recovery. Non-production retains 7 days for development agility.
  #
  # The backup window (03:00-04:00 UTC) and maintenance window
  # (Mon:04:00-Mon:05:00 UTC) are sequential and non-overlapping to prevent
  # maintenance operations from interrupting backup processes.
  # ---------------------------------------------------------------------------
  backup_retention_period = var.environment == "prod" ? 35 : 7
  backup_window           = "03:00-04:00"
  maintenance_window      = "Mon:04:00-Mon:05:00"
  copy_tags_to_snapshot   = true

  # ---------------------------------------------------------------------------
  # Deletion Protection and Lifecycle
  # ---------------------------------------------------------------------------
  # Production environments require deletion protection and a final snapshot
  # before the instance can be destroyed. Non-production environments allow
  # rapid teardown without these safeguards.
  #
  # final_snapshot_identifier is set to null for non-production (required when
  # skip_final_snapshot = true) to avoid Terraform validation errors.
  # ---------------------------------------------------------------------------
  deletion_protection       = var.environment == "prod" ? true : false
  skip_final_snapshot       = var.environment == "prod" ? false : true
  final_snapshot_identifier = var.environment == "prod" ? "${var.name_prefix}-final-snapshot" : null

  # ---------------------------------------------------------------------------
  # Tags
  # ---------------------------------------------------------------------------
  tags = {
    Name = "${var.name_prefix}-sqlserver"
  }
}
