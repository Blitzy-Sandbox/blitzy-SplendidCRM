# =============================================================================
# infrastructure/modules/common/alb.tf
# =============================================================================
# Internal Application Load Balancer for SplendidCRM ECS Fargate deployment.
#
# Architecture:
#   Single internal ALB with path-based routing implementing same-origin
#   cookie architecture (G8). Both backend (.NET 10 Kestrel) and frontend
#   (React 19 / Nginx) services share one DNS name, preserving cookie-based
#   session authentication without CORS complications.
#
# Routing Rules (Priority Order — most specific first):
#   Priority 1: /Rest.svc/*                  → Backend (152 main REST API endpoints)
#   Priority 2: /Administration/Rest.svc/*   → Backend (65 admin REST API endpoints)
#   Priority 3: /hubs/*                      → Backend (SignalR WebSocket hubs)
#   Priority 4: /api/*                       → Backend (health check + API endpoints)
#   Priority 5: /App_Themes/*                → Backend (theme CSS/images via ASP.NET Core)
#   Priority 6: /Include/*                   → Backend (shared JS utilities via ASP.NET Core)
#   Default:    /*                           → Frontend (React SPA with Nginx try_files)
#
# Health Checks:
#   Backend:  GET /api/health → 200 OK {"status":"Healthy"} or 503 {"status":"Unhealthy"}
#             (HealthCheckController.cs — AllowAnonymous endpoint)
#   Frontend: GET /health     → 200 OK "ok"
#             (nginx.conf stub location)
#
# Cross-File Dependencies:
#   FROM security-groups.tf  → aws_security_group.alb.id
#   FROM variables.tf        → var.name_prefix, var.container_port, var.vpc_id,
#                               var.app_subnet_ids, var.certificate_arn
#   TO   ecs-fargate.tf      → aws_lb_target_group.backend, aws_lb_target_group.frontend,
#                               aws_lb_listener.http
#   TO   outputs.tf          → aws_lb.main.dns_name
# =============================================================================

# -----------------------------------------------------------------------------
# ALB Resource — Internal-facing Application Load Balancer
# -----------------------------------------------------------------------------
# The ALB is internal (no public internet access) per AAP §0.3.2.
# Both backend and frontend ECS services register with this single ALB,
# preserving same-origin cookie architecture (G8).
# -----------------------------------------------------------------------------
resource "aws_lb" "main" {
  name               = "${var.name_prefix}-alb"
  internal           = true
  load_balancer_type = "application"
  security_groups    = [aws_security_group.alb.id]
  subnets            = var.app_subnet_ids

  # Deletion protection disabled for dev/staging; production environments
  # should override via environment-specific configuration if needed.
  enable_deletion_protection = false

  tags = {
    Name = "${var.name_prefix}-alb"
  }
}

# -----------------------------------------------------------------------------
# Target Group — Backend (.NET 10 Kestrel on port 8080)
# -----------------------------------------------------------------------------
# CRITICAL (G2): Port MUST be var.container_port (8080) — consistent with:
#   - Dockerfile.backend ENV ASPNETCORE_URLS=http://+:8080
#   - ecs-fargate.tf containerPort
#   - security-groups.tf ALB→Backend inbound rule
#
# Health check path /api/health is served by HealthCheckController.cs:
#   - [AllowAnonymous] — accessible without authentication
#   - Returns 200 OK {"status":"Healthy","machineName":"...","timestamp":"...","initialized":true/false}
#   - Returns 503 Service Unavailable {"status":"Unhealthy","error":"..."} on DB failure
# -----------------------------------------------------------------------------
resource "aws_lb_target_group" "backend" {
  name        = "${var.name_prefix}-backend-tg"
  port        = var.container_port
  protocol    = "HTTP"
  vpc_id      = var.vpc_id
  target_type = "ip" # Required for Fargate awsvpc network mode

  health_check {
    path                = "/api/health"
    port                = "traffic-port"
    protocol            = "HTTP"
    healthy_threshold   = 2
    unhealthy_threshold = 3
    timeout             = 5
    interval            = 30
    matcher             = "200"
  }

  # Quick deregistration for faster deployments in dev/staging.
  # Production deployments may benefit from longer draining (e.g., 120s).
  deregistration_delay = 30

  tags = {
    Name = "${var.name_prefix}-backend-tg"
  }
}

# -----------------------------------------------------------------------------
# Target Group — Frontend (Nginx on port 80)
# -----------------------------------------------------------------------------
# Health check path /health is a stub Nginx location block that returns
# 200 OK with body "ok" — lightweight check for container readiness.
# -----------------------------------------------------------------------------
resource "aws_lb_target_group" "frontend" {
  name        = "${var.name_prefix}-frontend-tg"
  port        = 80
  protocol    = "HTTP"
  vpc_id      = var.vpc_id
  target_type = "ip" # Required for Fargate awsvpc network mode

  health_check {
    path                = "/health"
    port                = "traffic-port"
    protocol            = "HTTP"
    healthy_threshold   = 2
    unhealthy_threshold = 3
    timeout             = 5
    interval            = 30
    matcher             = "200"
  }

  deregistration_delay = 30

  tags = {
    Name = "${var.name_prefix}-frontend-tg"
  }
}

# -----------------------------------------------------------------------------
# HTTP Listener (Port 80) — Default routes to Frontend
# -----------------------------------------------------------------------------
# The default action forwards all unmatched paths to the frontend target group.
# The React SPA in Nginx uses try_files to serve index.html for client-side
# routing, so all non-API paths correctly reach the SPA.
# Path-based listener rules (below) intercept API and asset paths before
# the default action applies.
# -----------------------------------------------------------------------------
resource "aws_lb_listener" "http" {
  load_balancer_arn = aws_lb.main.arn
  port              = 80
  protocol          = "HTTP"

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.frontend.arn
  }

  tags = {
    Name = "${var.name_prefix}-http-listener"
  }
}

# -----------------------------------------------------------------------------
# HTTPS Listener (Port 443) — Optional, requires ACM certificate
# -----------------------------------------------------------------------------
# Conditional on var.certificate_arn being non-empty. When no certificate
# is provided (e.g., LocalStack testing, initial dev setup), only the HTTP
# listener is created. When a certificate is available, HTTPS serves traffic
# with TLS 1.3 enforcement via the ELBSecurityPolicy-TLS13-1-2-2021-06 policy.
#
# The default action mirrors the HTTP listener: forward to frontend.
# Path-based rules are duplicated for the HTTPS listener below.
# -----------------------------------------------------------------------------
resource "aws_lb_listener" "https" {
  count = var.certificate_arn != "" ? 1 : 0

  load_balancer_arn = aws_lb.main.arn
  port              = 443
  protocol          = "HTTPS"
  ssl_policy        = "ELBSecurityPolicy-TLS13-1-2-2021-06"
  certificate_arn   = var.certificate_arn

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.frontend.arn
  }

  tags = {
    Name = "${var.name_prefix}-https-listener"
  }
}

# =============================================================================
# Path-Based Listener Rules — HTTP Listener
# =============================================================================
# 6 explicit rules route backend-specific paths to the backend target group.
# The 7th "rule" is the HTTP listener's default_action (frontend).
# Priority ordering follows AAP §0.7.5: most specific first.
# ALB evaluates rules in numeric priority order and stops at the first match.
# =============================================================================

# -----------------------------------------------------------------------------
# Rule 1 (Priority 1): /Rest.svc/* → Backend
# Routes 152 main REST API endpoints served by RestController.cs
# Example: GET /Rest.svc/Contacts, POST /Rest.svc/Accounts
# -----------------------------------------------------------------------------
resource "aws_lb_listener_rule" "rest_svc" {
  listener_arn = aws_lb_listener.http.arn
  priority     = 1

  action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.backend.arn
  }

  condition {
    path_pattern {
      values = ["/Rest.svc/*"]
    }
  }

  tags = {
    Name = "${var.name_prefix}-rule-rest-svc"
  }
}

# -----------------------------------------------------------------------------
# Rule 2 (Priority 2): /Administration/Rest.svc/* → Backend
# Routes 65 admin REST API endpoints served by AdminRestController.cs
# Example: GET /Administration/Rest.svc/Modules, POST /Administration/Rest.svc/Users
# -----------------------------------------------------------------------------
resource "aws_lb_listener_rule" "admin_rest_svc" {
  listener_arn = aws_lb_listener.http.arn
  priority     = 2

  action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.backend.arn
  }

  condition {
    path_pattern {
      values = ["/Administration/Rest.svc/*"]
    }
  }

  tags = {
    Name = "${var.name_prefix}-rule-admin-rest-svc"
  }
}

# -----------------------------------------------------------------------------
# Rule 3 (Priority 3): /hubs/* → Backend
# Routes SignalR WebSocket hub connections (ChatManager, TwilioManager,
# PhoneBurnerManager). ALB natively supports WebSocket upgrades on HTTP/HTTPS.
# -----------------------------------------------------------------------------
resource "aws_lb_listener_rule" "hubs" {
  listener_arn = aws_lb_listener.http.arn
  priority     = 3

  action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.backend.arn
  }

  condition {
    path_pattern {
      values = ["/hubs/*"]
    }
  }

  tags = {
    Name = "${var.name_prefix}-rule-hubs"
  }
}

# -----------------------------------------------------------------------------
# Rule 4 (Priority 4): /api/* → Backend
# Routes the health check endpoint (GET /api/health from HealthCheckController.cs)
# and any future API endpoints under the /api/ prefix.
# -----------------------------------------------------------------------------
resource "aws_lb_listener_rule" "api" {
  listener_arn = aws_lb_listener.http.arn
  priority     = 4

  action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.backend.arn
  }

  condition {
    path_pattern {
      values = ["/api/*"]
    }
  }

  tags = {
    Name = "${var.name_prefix}-rule-api"
  }
}

# -----------------------------------------------------------------------------
# Rule 5 (Priority 5): /App_Themes/* → Backend
# Routes theme CSS, images, and chart assets served by ASP.NET Core static
# files middleware. Program.cs line 520 configures UseStaticFiles with
# PhysicalFileProvider pointing to /SplendidCRM/App_Themes in the container.
# 7 theme directories: Arctic, Atlantic, Mobile, Pacific, Seven, Six, Sugar.
# -----------------------------------------------------------------------------
resource "aws_lb_listener_rule" "app_themes" {
  listener_arn = aws_lb_listener.http.arn
  priority     = 5

  action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.backend.arn
  }

  condition {
    path_pattern {
      values = ["/App_Themes/*"]
    }
  }

  tags = {
    Name = "${var.name_prefix}-rule-app-themes"
  }
}

# -----------------------------------------------------------------------------
# Rule 6 (Priority 6): /Include/* → Backend
# Routes shared JavaScript utilities and browser assets served by ASP.NET Core
# static files middleware. Program.cs line 529 configures UseStaticFiles with
# PhysicalFileProvider pointing to /SplendidCRM/Include in the container.
# Subdirectories: Silverlight, charts, images, javascript, jqPlot.
# -----------------------------------------------------------------------------
resource "aws_lb_listener_rule" "include" {
  listener_arn = aws_lb_listener.http.arn
  priority     = 6

  action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.backend.arn
  }

  condition {
    path_pattern {
      values = ["/Include/*"]
    }
  }

  tags = {
    Name = "${var.name_prefix}-rule-include"
  }
}

# =============================================================================
# Path-Based Listener Rules — HTTPS Listener (Conditional)
# =============================================================================
# When the HTTPS listener exists (certificate_arn is non-empty), duplicate
# the same 6 path-based routing rules so that HTTPS traffic is routed
# identically to HTTP traffic. Without these rules, all HTTPS requests
# would fall through to the default action (frontend) regardless of path.
# =============================================================================

# HTTPS Rule 1: /Rest.svc/* → Backend
resource "aws_lb_listener_rule" "https_rest_svc" {
  count = var.certificate_arn != "" ? 1 : 0

  listener_arn = aws_lb_listener.https[0].arn
  priority     = 1

  action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.backend.arn
  }

  condition {
    path_pattern {
      values = ["/Rest.svc/*"]
    }
  }

  tags = {
    Name = "${var.name_prefix}-rule-https-rest-svc"
  }
}

# HTTPS Rule 2: /Administration/Rest.svc/* → Backend
resource "aws_lb_listener_rule" "https_admin_rest_svc" {
  count = var.certificate_arn != "" ? 1 : 0

  listener_arn = aws_lb_listener.https[0].arn
  priority     = 2

  action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.backend.arn
  }

  condition {
    path_pattern {
      values = ["/Administration/Rest.svc/*"]
    }
  }

  tags = {
    Name = "${var.name_prefix}-rule-https-admin-rest-svc"
  }
}

# HTTPS Rule 3: /hubs/* → Backend (SignalR WebSocket hubs)
resource "aws_lb_listener_rule" "https_hubs" {
  count = var.certificate_arn != "" ? 1 : 0

  listener_arn = aws_lb_listener.https[0].arn
  priority     = 3

  action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.backend.arn
  }

  condition {
    path_pattern {
      values = ["/hubs/*"]
    }
  }

  tags = {
    Name = "${var.name_prefix}-rule-https-hubs"
  }
}

# HTTPS Rule 4: /api/* → Backend (health check + API endpoints)
resource "aws_lb_listener_rule" "https_api" {
  count = var.certificate_arn != "" ? 1 : 0

  listener_arn = aws_lb_listener.https[0].arn
  priority     = 4

  action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.backend.arn
  }

  condition {
    path_pattern {
      values = ["/api/*"]
    }
  }

  tags = {
    Name = "${var.name_prefix}-rule-https-api"
  }
}

# HTTPS Rule 5: /App_Themes/* → Backend (theme CSS/images)
resource "aws_lb_listener_rule" "https_app_themes" {
  count = var.certificate_arn != "" ? 1 : 0

  listener_arn = aws_lb_listener.https[0].arn
  priority     = 5

  action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.backend.arn
  }

  condition {
    path_pattern {
      values = ["/App_Themes/*"]
    }
  }

  tags = {
    Name = "${var.name_prefix}-rule-https-app-themes"
  }
}

# HTTPS Rule 6: /Include/* → Backend (shared JS utilities)
resource "aws_lb_listener_rule" "https_include" {
  count = var.certificate_arn != "" ? 1 : 0

  listener_arn = aws_lb_listener.https[0].arn
  priority     = 6

  action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.backend.arn
  }

  condition {
    path_pattern {
      values = ["/Include/*"]
    }
  }

  tags = {
    Name = "${var.name_prefix}-rule-https-include"
  }
}
