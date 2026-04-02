# =============================================================================
# Security Groups — Layered Network Policy for SplendidCRM ECS Fargate
# =============================================================================
#
# Defines 4 security groups forming a defense-in-depth connectivity model:
#
#   VPC CIDR → ALB (80,443) → Backend (8080) → RDS (1433)
#                             → Frontend (80)
#   Backend → AWS APIs (443 outbound: Secrets Manager, Parameter Store, KMS, ECR)
#   Frontend → AWS APIs (443 outbound: ECR pull, CloudWatch logs)
#
# Security group references use source_security_group_id (not CIDR) for all
# inter-service rules, ensuring tight coupling between exactly the intended
# resources without relying on IP address ranges.
#
# Port 8080 is parameterized via var.container_port (G2 consistency) and must
# match across: Dockerfile.backend ENV/EXPOSE, ECS task definition containerPort,
# ALB backend target group port, and this security group.
#
# ACME Strategy: Uses standard aws_security_group and aws_security_group_rule
# resource blocks for LocalStack compatibility. When deploying to real AWS with
# ACME private module registry access, swap to tfe.acme.com/acme/security-group/aws
# per the mapping table in deployment documentation.
# =============================================================================

# -----------------------------------------------------------------------------
# 1. ALB Security Group
# -----------------------------------------------------------------------------
# Allows inbound HTTP/HTTPS from within the VPC CIDR (internal ALB).
# Allows outbound only to backend (8080) and frontend (80) target groups.
# -----------------------------------------------------------------------------

resource "aws_security_group" "alb" {
  name        = "${var.name_prefix}-alb-sg"
  description = "Security group for ${var.name_prefix} Application Load Balancer"
  vpc_id      = var.vpc_id

  tags = {
    Name = "${var.name_prefix}-alb-sg"
  }
}

# ALB Inbound: HTTP from VPC CIDR (internal traffic only)
resource "aws_security_group_rule" "alb_http_inbound" {
  type              = "ingress"
  from_port         = 80
  to_port           = 80
  protocol          = "tcp"
  cidr_blocks       = [var.vpc_cidr]
  security_group_id = aws_security_group.alb.id
  description       = "Allow HTTP inbound from VPC CIDR"
}

# ALB Inbound: HTTPS from VPC CIDR (internal traffic only)
resource "aws_security_group_rule" "alb_https_inbound" {
  type              = "ingress"
  from_port         = 443
  to_port           = 443
  protocol          = "tcp"
  cidr_blocks       = [var.vpc_cidr]
  security_group_id = aws_security_group.alb.id
  description       = "Allow HTTPS inbound from VPC CIDR"
}

# ALB Outbound: Forward to backend ECS tasks on container port (8080 per G2)
resource "aws_security_group_rule" "alb_to_backend" {
  type                     = "egress"
  from_port                = var.container_port
  to_port                  = var.container_port
  protocol                 = "tcp"
  source_security_group_id = aws_security_group.backend.id
  security_group_id        = aws_security_group.alb.id
  description              = "Allow outbound to backend ECS tasks on port ${var.container_port}"
}

# ALB Outbound: Forward to frontend ECS tasks on port 80 (Nginx)
resource "aws_security_group_rule" "alb_to_frontend" {
  type                     = "egress"
  from_port                = 80
  to_port                  = 80
  protocol                 = "tcp"
  source_security_group_id = aws_security_group.frontend.id
  security_group_id        = aws_security_group.alb.id
  description              = "Allow outbound to frontend ECS tasks on port 80"
}

# -----------------------------------------------------------------------------
# 2. Backend Security Group
# -----------------------------------------------------------------------------
# Allows inbound only from the ALB on the container port (8080).
# Allows outbound to RDS on port 1433 (SQL Server) and to AWS APIs on 443
# (Secrets Manager, Parameter Store, KMS, ECR image pull).
# -----------------------------------------------------------------------------

resource "aws_security_group" "backend" {
  name        = "${var.name_prefix}-backend-sg"
  description = "Security group for ${var.name_prefix} backend ECS tasks"
  vpc_id      = var.vpc_id

  tags = {
    Name = "${var.name_prefix}-backend-sg"
  }
}

# Backend Inbound: Traffic from ALB on container port (8080 per G2)
resource "aws_security_group_rule" "backend_from_alb" {
  type                     = "ingress"
  from_port                = var.container_port
  to_port                  = var.container_port
  protocol                 = "tcp"
  source_security_group_id = aws_security_group.alb.id
  security_group_id        = aws_security_group.backend.id
  description              = "Allow inbound from ALB on port ${var.container_port}"
}

# Backend Outbound: Connect to RDS SQL Server on port 1433
resource "aws_security_group_rule" "backend_to_rds" {
  type                     = "egress"
  from_port                = 1433
  to_port                  = 1433
  protocol                 = "tcp"
  source_security_group_id = aws_security_group.rds.id
  security_group_id        = aws_security_group.backend.id
  description              = "Allow outbound to RDS SQL Server on port 1433"
}

# Backend Outbound: HTTPS to AWS APIs (Secrets Manager, Parameter Store, KMS, ECR)
resource "aws_security_group_rule" "backend_https_egress" {
  type              = "egress"
  from_port         = 443
  to_port           = 443
  protocol          = "tcp"
  cidr_blocks       = ["0.0.0.0/0"]
  security_group_id = aws_security_group.backend.id
  description       = "Allow HTTPS outbound to AWS APIs (Secrets Manager, Parameter Store, KMS, ECR)"
}

# -----------------------------------------------------------------------------
# 3. Frontend Security Group
# -----------------------------------------------------------------------------
# Allows inbound only from the ALB on port 80 (Nginx static file serving).
# Allows outbound HTTPS for ECR image pull and CloudWatch log delivery.
# No outbound to RDS — frontend has zero database access (isolation).
# -----------------------------------------------------------------------------

resource "aws_security_group" "frontend" {
  name        = "${var.name_prefix}-frontend-sg"
  description = "Security group for ${var.name_prefix} frontend ECS tasks"
  vpc_id      = var.vpc_id

  tags = {
    Name = "${var.name_prefix}-frontend-sg"
  }
}

# Frontend Inbound: Traffic from ALB on port 80 (Nginx)
resource "aws_security_group_rule" "frontend_from_alb" {
  type                     = "ingress"
  from_port                = 80
  to_port                  = 80
  protocol                 = "tcp"
  source_security_group_id = aws_security_group.alb.id
  security_group_id        = aws_security_group.frontend.id
  description              = "Allow inbound from ALB on port 80"
}

# Frontend Outbound: HTTPS for ECR image pull and CloudWatch log delivery
resource "aws_security_group_rule" "frontend_https_egress" {
  type              = "egress"
  from_port         = 443
  to_port           = 443
  protocol          = "tcp"
  cidr_blocks       = ["0.0.0.0/0"]
  security_group_id = aws_security_group.frontend.id
  description       = "Allow HTTPS outbound for ECR image pull and CloudWatch logs"
}

# -----------------------------------------------------------------------------
# 4. RDS Security Group
# -----------------------------------------------------------------------------
# Allows inbound only from the backend ECS tasks on port 1433 (SQL Server).
# No outbound rules — RDS does not initiate connections.
# The ALB and frontend security groups have NO access to RDS (isolation).
# -----------------------------------------------------------------------------

resource "aws_security_group" "rds" {
  name        = "${var.name_prefix}-rds-sg"
  description = "Security group for ${var.name_prefix} RDS SQL Server instance"
  vpc_id      = var.vpc_id

  tags = {
    Name = "${var.name_prefix}-rds-sg"
  }
}

# RDS Inbound: Only backend ECS tasks can connect on port 1433
resource "aws_security_group_rule" "rds_from_backend" {
  type                     = "ingress"
  from_port                = 1433
  to_port                  = 1433
  protocol                 = "tcp"
  source_security_group_id = aws_security_group.backend.id
  security_group_id        = aws_security_group.rds.id
  description              = "Allow inbound from backend ECS tasks on SQL Server port 1433"
}
