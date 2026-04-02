# -----------------------------------------------------------------------------
# Module: splendidcrm-common
# -----------------------------------------------------------------------------
# Description:
#   Shared Terraform module for SplendidCRM AWS infrastructure provisioning.
#   This module provisions all core AWS resources required to run the
#   SplendidCRM application on ECS Fargate behind an internal ALB.
#
# Resources Created:
#   - ECR repositories (backend + frontend container images)
#   - ECS Fargate cluster, task definitions, and services
#   - Internal Application Load Balancer with path-based routing
#   - RDS SQL Server instance in private subnets
#   - IAM roles and policies (execution, backend task, frontend task)
#   - KMS Customer Managed Key for Secrets Manager encryption
#   - Secrets Manager secrets (6 secrets, CMK-encrypted)
#   - SSM Parameter Store parameters (8 non-secret config values)
#   - CloudWatch log group and log stream
#   - Security groups (ALB, backend, frontend, RDS)
#
# ACME Strategy Note:
#   This module uses standard aws_* resource blocks for LocalStack
#   compatibility and autonomous validation. When deploying to real AWS
#   with ACME private module registry access (tfe.acme.com/acme/*/aws),
#   swap the corresponding resource blocks to ACME module sources per the
#   mapping table in the deployment documentation. The resource interfaces
#   are designed to align with ACME module input/output conventions.
#
# Usage:
#   module "common" {
#     source = "../../modules/common"
#
#     name_prefix    = "splendidcrm-dev"
#     environment    = "dev"
#     account_id     = "123456789012"
#     vpc_id         = data.aws_vpc.main.id
#     vpc_cidr       = data.aws_vpc.main.cidr_block
#     app_subnet_ids = data.aws_subnets.app.ids
#     db_subnet_ids  = data.aws_subnets.db.ids
#     task_cpu       = 512
#     task_memory    = 1024
#   }
# -----------------------------------------------------------------------------

terraform {
  required_version = ">= 1.12.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = ">= 6.0.0"
    }
  }
}
