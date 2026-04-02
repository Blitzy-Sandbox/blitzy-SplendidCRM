# Data sources for VPC and subnet discovery in the staging environment
#
# ACME standards require tag-based discovery for all network resources.
# VPCs and subnets are NEVER referenced by hardcoded IDs — they are
# discovered dynamically via their AWS Name tags. This ensures the same
# Terraform code works across AWS accounts without modification.

# -----------------------------------------------------------------------------
# VPC Discovery
# -----------------------------------------------------------------------------
# "acme-dev-vpc-use2" is the ACME development VPC name tag in us-east-2.
# ACME naming convention: {company}-{env}-vpc-{region_short}
# Consumed by main.tf as:
#   data.aws_vpc.main.id         → vpc_id
#   data.aws_vpc.main.cidr_block → vpc_cidr
# -----------------------------------------------------------------------------
data "aws_vpc" "main" {
  filter {
    name   = "tag:Name"
    values = ["acme-dev-vpc-use2"]
  }
}

# -----------------------------------------------------------------------------
# Application-Tier Subnet Discovery
# -----------------------------------------------------------------------------
# Wildcard "*-app-*" matches all application-tier subnets within the VPC.
# These subnets host ECS Fargate tasks and the internal ALB.
# Consumed by main.tf as:
#   data.aws_subnets.app.ids → app_subnet_ids
# -----------------------------------------------------------------------------
data "aws_subnets" "app" {
  filter {
    name   = "vpc-id"
    values = [data.aws_vpc.main.id]
  }

  filter {
    name   = "tag:Name"
    values = ["*-app-*"]
  }
}

# -----------------------------------------------------------------------------
# Database-Tier Subnet Discovery
# -----------------------------------------------------------------------------
# Wildcard "*-db-*" matches all database-tier subnets within the VPC.
# These subnets host the RDS SQL Server instance in private isolation.
# Consumed by main.tf as:
#   data.aws_subnets.db.ids → db_subnet_ids
# -----------------------------------------------------------------------------
data "aws_subnets" "db" {
  filter {
    name   = "vpc-id"
    values = [data.aws_vpc.main.id]
  }

  filter {
    name   = "tag:Name"
    values = ["*-db-*"]
  }
}
