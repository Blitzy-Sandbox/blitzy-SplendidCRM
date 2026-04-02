# =============================================================================
# ECS Fargate — SplendidCRM Containerized Deployment
# =============================================================================
#
# Purpose:
#   Define the ECS Fargate cluster, 2 task definitions (backend + frontend),
#   2 ECS services, and auto-scaling resources for the SplendidCRM containerized
#   deployment. This is the most complex Terraform file in the module, tying
#   together all other module resources into a functioning deployment.
#
# Architecture:
#   - Single ECS cluster with Container Insights and ECS Exec enabled
#   - Backend task: .NET 10 Kestrel on port 8080 with 7 secrets + 7 env vars
#   - Frontend task: Nginx on port 80 with 3 env vars and zero secrets
#   - Both services behind a single internal ALB (same-origin cookies, G8)
#   - Auto-scaling at 70% CPU and Memory for both services
#
# Resources Created (12 total):
#   - aws_ecs_cluster.main                         — ECS cluster
#   - aws_ecs_task_definition.backend              — Backend task definition
#   - aws_ecs_task_definition.frontend             — Frontend task definition
#   - aws_ecs_service.backend                      — Backend ECS service
#   - aws_ecs_service.frontend                     — Frontend ECS service
#   - aws_appautoscaling_target.backend            — Backend scaling target
#   - aws_appautoscaling_target.frontend           — Frontend scaling target
#   - aws_appautoscaling_policy.backend_cpu        — Backend CPU scaling policy
#   - aws_appautoscaling_policy.frontend_cpu       — Frontend CPU scaling policy
#   - aws_appautoscaling_policy.backend_memory     — Backend memory scaling policy
#   - aws_appautoscaling_policy.frontend_memory    — Frontend memory scaling policy
#
# Cross-File Dependencies:
#   READS FROM:
#     locals.tf          → local.backend_image, local.frontend_image
#     iam.tf             → aws_iam_role.ecs_execution.arn,
#                           aws_iam_role.backend_task.arn,
#                           aws_iam_role.frontend_task.arn
#     security-groups.tf → aws_security_group.backend.id,
#                           aws_security_group.frontend.id
#     alb.tf             → aws_lb_target_group.backend.arn,
#                           aws_lb_target_group.frontend.arn,
#                           aws_lb_listener.http (depends_on)
#     cloudwatch.tf      → aws_cloudwatch_log_group.application.name
#     secrets.tf         → aws_secretsmanager_secret.db_connection.arn,
#                           aws_secretsmanager_secret.sso_client_id.arn,
#                           aws_secretsmanager_secret.sso_client_secret.arn,
#                           aws_secretsmanager_secret.duo_integration_key.arn,
#                           aws_secretsmanager_secret.duo_secret_key.arn,
#                           aws_secretsmanager_secret.smtp_credentials.arn
#     data.tf            → data.aws_region.current.name
#     variables.tf       → var.name_prefix, var.environment, var.container_port,
#                           var.task_cpu, var.task_memory, var.task_ephemeral_gb,
#                           var.min_tasks, var.max_tasks, var.app_subnet_ids
#
#   REFERENCED BY:
#     outputs.tf         → aws_ecs_cluster.main.name
#
# Guardrail Compliance:
#   G2  — Port 8080: containerPort = var.container_port (default 8080)
#   G7  — Full ARN: valueFrom = aws_secretsmanager_secret.*.arn
#   G8  — Same-origin: API_BASE_URL = "" (empty), single ALB for both services
#   G10 — No tfe.acme.com references; uses standard aws_* resource blocks
#
# Application Context:
#   - Backend health check: GET /api/health (HealthCheckController.cs)
#     Returns 200 OK or 503 Service Unavailable
#   - Frontend health check: GET /health (Nginx stub returning 200 OK "ok")
#   - ASP.NET Core __ separator: ConnectionStrings__SplendidCRM, SSO__ClientId, etc.
#   - StartupValidator.cs validates 7 required config values at container startup
#   - Frontend docker-entrypoint.sh generates config.json from env vars
#
# ACME Strategy:
#   Uses standard aws_* resource blocks for LocalStack compatibility.
#   When deploying to real AWS with ACME private module registry access,
#   swap to tfe.acme.com/acme/ecs-fargate/aws per the mapping table in
#   deployment documentation.
# =============================================================================


# =============================================================================
# SECTION 1: ECS CLUSTER
# =============================================================================
# Single ECS cluster hosting both backend and frontend services. Container
# Insights are enabled for monitoring metrics (CPU, memory, network, disk I/O)
# and ECS Exec is enabled for interactive troubleshooting.
# =============================================================================

resource "aws_ecs_cluster" "main" {
  name = "${var.name_prefix}-cluster"

  # Enable Container Insights for enhanced monitoring metrics.
  # Provides per-task and per-service CPU, memory, network, and storage metrics
  # in CloudWatch without requiring application-level instrumentation.
  setting {
    name  = "containerInsights"
    value = "enabled"
  }

  # Enable ECS Exec for interactive troubleshooting of running containers.
  # Operators can attach to containers using:
  #   aws ecs execute-command --cluster <name> --task <id> \
  #     --container <name> --interactive --command "/bin/sh"
  # Logging is set to DEFAULT which sends audit logs to CloudWatch.
  configuration {
    execute_command_configuration {
      logging = "DEFAULT"
    }
  }

  tags = {
    Name = "${var.name_prefix}-cluster"
  }
}


# =============================================================================
# SECTION 2: BACKEND TASK DEFINITION
# =============================================================================
# Defines the .NET 10 backend container running Kestrel on port 8080 (G2).
# Receives 7 secrets via Secrets Manager ARN references (G7) and 7 literal
# environment variables.
#
# The container_definitions JSON includes:
#   - Container image from ECR (local.backend_image)
#   - Port mapping: containerPort 8080 (var.container_port)
#   - Health check: curl GET /api/health (HealthCheckController.cs)
#   - Log configuration: awslogs driver → CloudWatch log group
#   - 7 secrets (Secrets Manager ARN valueFrom per G7)
#   - 7 environment variables (literal values)
#
# Execution role (ecs_execution) resolves secrets before container start.
# Task role (backend_task) provides runtime AWS SDK permissions for
# AwsSecretsManagerProvider.cs and AwsParameterStoreProvider.cs.
# =============================================================================

resource "aws_ecs_task_definition" "backend" {
  family                   = "${var.name_prefix}-backend-task"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = var.task_cpu
  memory                   = var.task_memory
  execution_role_arn       = aws_iam_role.ecs_execution.arn
  task_role_arn            = aws_iam_role.backend_task.arn

  # Ephemeral storage for temporary files, logs, and .NET runtime artifacts.
  # Default 21 GB for dev; larger allocations for staging (30 GB) and prod (50 GB).
  ephemeral_storage {
    size_in_gib = var.task_ephemeral_gb
  }

  # ---------------------------------------------------------------------------
  # Backend Container Definition
  # ---------------------------------------------------------------------------
  # Single-container task definition. The container runs the published .NET 10
  # application (SplendidCRM.Web.dll) via the ENTRYPOINT defined in
  # Dockerfile.backend. Kestrel listens on port 8080 per G2.
  # ---------------------------------------------------------------------------
  container_definitions = jsonencode([
    {
      # Container identity
      name      = "backend"
      image     = local.backend_image
      essential = true

      # Port mapping: Kestrel on port 8080 (G2 consistency)
      # Must match: Dockerfile EXPOSE, ALB target group, security group rule
      portMappings = [
        {
          containerPort = var.container_port
          protocol      = "tcp"
        }
      ]

      # ECS container health check — separate from ALB health check.
      # Uses curl to probe the HealthCheckController endpoint.
      # startPeriod of 60s allows time for .NET startup, configuration
      # provider initialization (Secrets Manager, Parameter Store), and
      # SplendidInit.InitApp() database schema validation.
      healthCheck = {
        command     = ["CMD-SHELL", "curl -f http://localhost:${var.container_port}/api/health || exit 1"]
        interval    = 30
        timeout     = 5
        retries     = 3
        startPeriod = 60
      }

      # CloudWatch Logs configuration — awslogs driver streams container
      # stdout/stderr to the centralized log group defined in cloudwatch.tf.
      # Stream prefix "backend" distinguishes backend logs from frontend logs
      # within the same log group.
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = aws_cloudwatch_log_group.application.name
          "awslogs-region"        = data.aws_region.current.region
          "awslogs-stream-prefix" = "backend"
        }
      }

      # -----------------------------------------------------------------
      # Backend Secrets (7) — Injected via Secrets Manager ARN (G7)
      # -----------------------------------------------------------------
      # The ECS agent resolves these valueFrom references BEFORE container
      # startup, using the execution role's secretsmanager:GetSecretValue
      # and kms:Decrypt permissions. Resolved values become environment
      # variables inside the container.
      #
      # CRITICAL (G7): valueFrom MUST use full ARN format, NOT friendly
      # secret name. Using friendly names causes silent container startup
      # failure with no useful error in CloudWatch logs.
      #
      # Secret mapping to ASP.NET Core configuration keys:
      #   ConnectionStrings__SplendidCRM → primary database connection
      #   Session__ConnectionString      → distributed SQL session (same DB)
      #   SSO__ClientId                  → OIDC client identifier
      #   SSO__ClientSecret              → OIDC client secret
      #   Duo__IntegrationKey            → Duo 2FA integration key
      #   Duo__SecretKey                 → Duo 2FA secret key
      #   Smtp__Credentials              → SMTP server credentials
      # -----------------------------------------------------------------
      secrets = [
        {
          name      = "ConnectionStrings__SplendidCRM"
          valueFrom = aws_secretsmanager_secret.db_connection.arn
        },
        {
          name      = "SSO__ClientId"
          valueFrom = aws_secretsmanager_secret.sso_client_id.arn
        },
        {
          name      = "SSO__ClientSecret"
          valueFrom = aws_secretsmanager_secret.sso_client_secret.arn
        },
        {
          name      = "Duo__IntegrationKey"
          valueFrom = aws_secretsmanager_secret.duo_integration_key.arn
        },
        {
          name      = "Duo__SecretKey"
          valueFrom = aws_secretsmanager_secret.duo_secret_key.arn
        },
        {
          name      = "Smtp__Credentials"
          valueFrom = aws_secretsmanager_secret.smtp_credentials.arn
        },
        {
          name      = "Session__ConnectionString"
          valueFrom = aws_secretsmanager_secret.db_connection.arn
        }
      ]

      # -----------------------------------------------------------------
      # Backend Environment Variables (7) — Literal values, NOT secrets
      # -----------------------------------------------------------------
      # These are non-sensitive configuration values injected directly as
      # environment variables. ASP.NET Core's configuration system reads
      # these automatically via the EnvironmentVariables provider.
      #
      # The __ (double underscore) separator maps to the : (colon) hierarchy
      # separator in ASP.NET Core configuration. For example:
      #   Session__Provider → IConfiguration["Session:Provider"]
      #   Authentication__Mode → IConfiguration["Authentication:Mode"]
      #
      # ASPNETCORE_URLS sets Kestrel's listen address to port 8080 (G2).
      # Cors__AllowedOrigins is empty for same-origin ALB deployment (G8).
      # -----------------------------------------------------------------
      environment = [
        {
          name  = "ASPNETCORE_ENVIRONMENT"
          value = var.environment
        },
        {
          name  = "ASPNETCORE_URLS"
          value = "http://+:${var.container_port}"
        },
        {
          name  = "Session__Provider"
          value = "SqlServer"
        },
        {
          name  = "Authentication__Mode"
          value = "Forms"
        },
        {
          name  = "SPLENDID_JOB_SERVER"
          value = "true"
        },
        {
          name  = "Cors__AllowedOrigins"
          value = ""
        },
        {
          name  = "Scheduler__JobServer"
          value = "true"
        }
      ]
    }
  ])

  tags = {
    Name = "${var.name_prefix}-backend-task"
  }
}


# =============================================================================
# SECTION 3: FRONTEND TASK DEFINITION
# =============================================================================
# Defines the Nginx container serving the React 19 SPA on port 80.
# Receives only 3 environment variables (no secrets per least-privilege).
#
# The docker-entrypoint.sh script generates /usr/share/nginx/html/config.json
# from these environment variables before starting Nginx. The config-loader.js
# script in the SPA synchronously loads this file before React initialization.
#
# Frontend task role (frontend_task) has NO Secrets Manager, KMS, or SSM
# permissions — only ECS Exec for troubleshooting.
# =============================================================================

resource "aws_ecs_task_definition" "frontend" {
  family                   = "${var.name_prefix}-frontend-task"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = var.task_cpu
  memory                   = var.task_memory
  execution_role_arn       = aws_iam_role.ecs_execution.arn
  task_role_arn            = aws_iam_role.frontend_task.arn

  ephemeral_storage {
    size_in_gib = var.task_ephemeral_gb
  }

  # ---------------------------------------------------------------------------
  # Frontend Container Definition
  # ---------------------------------------------------------------------------
  # Single-container task running Nginx Alpine with the React 19 SPA build
  # output in /usr/share/nginx/html/. The docker-entrypoint.sh generates
  # config.json from environment variables before starting Nginx.
  #
  # No secrets are injected — frontend has zero AWS SDK dependencies and
  # serves only static files. All API communication goes through the ALB
  # (same-origin, G8) to the backend service.
  # ---------------------------------------------------------------------------
  container_definitions = jsonencode([
    {
      # Container identity
      name      = "frontend"
      image     = local.frontend_image
      essential = true

      # Port mapping: Nginx on port 80
      portMappings = [
        {
          containerPort = 80
          protocol      = "tcp"
        }
      ]

      # ECS container health check for the Nginx health endpoint.
      # /health is configured in nginx.conf as a stub returning 200 OK "ok".
      # startPeriod of 15s is sufficient for Nginx (sub-second startup).
      healthCheck = {
        command     = ["CMD-SHELL", "curl -f http://localhost/health || exit 1"]
        interval    = 30
        timeout     = 5
        retries     = 3
        startPeriod = 15
      }

      # CloudWatch Logs — same log group as backend, different stream prefix.
      # Frontend logs include Nginx access/error logs and entrypoint output.
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = aws_cloudwatch_log_group.application.name
          "awslogs-region"        = data.aws_region.current.region
          "awslogs-stream-prefix" = "frontend"
        }
      }

      # -----------------------------------------------------------------
      # Frontend Environment Variables (3) — Per G8 (same-origin cookies)
      # -----------------------------------------------------------------
      # API_BASE_URL and SIGNALR_URL are empty strings, which means the
      # frontend uses relative URLs (same-origin ALB). This preserves
      # cookie-based session authentication without CORS complications.
      #
      # The docker-entrypoint.sh script writes these values to config.json:
      #   {"API_BASE_URL":"","SIGNALR_URL":"","ENVIRONMENT":"dev"}
      # The config-loader.js in the SPA loads this file synchronously
      # before React initialization, making values available via
      # window.__SPLENDID_CONFIG__.
      # -----------------------------------------------------------------
      environment = [
        {
          name  = "API_BASE_URL"
          value = ""
        },
        {
          name  = "SIGNALR_URL"
          value = ""
        },
        {
          name  = "ENVIRONMENT"
          value = var.environment
        }
      ]
    }
  ])

  tags = {
    Name = "${var.name_prefix}-frontend-task"
  }
}


# =============================================================================
# SECTION 4: BACKEND ECS SERVICE
# =============================================================================
# The backend service runs the .NET 10 Kestrel application in Fargate tasks
# placed in private application subnets. It registers with the backend ALB
# target group for path-based routing of API, SignalR, and static asset
# requests (per ALB listener rules in alb.tf).
#
# ECS Exec is enabled for interactive troubleshooting of running containers.
# The service depends on the ALB HTTP listener to ensure the load balancer
# infrastructure exists before tasks attempt to register.
# =============================================================================

resource "aws_ecs_service" "backend" {
  name            = "${var.name_prefix}-backend-svc"
  cluster         = aws_ecs_cluster.main.id
  task_definition = aws_ecs_task_definition.backend.arn
  desired_count   = var.min_tasks
  launch_type     = "FARGATE"

  # Enable ECS Exec for interactive container troubleshooting.
  # Requires ssmmessages:* permissions in the backend task role (iam.tf).
  enable_execute_command = true

  # Private subnet placement with no public IP — all traffic routes
  # through the internal ALB. Security group restricts inbound to ALB
  # only on the container port (8080).
  network_configuration {
    subnets          = var.app_subnet_ids
    security_groups  = [aws_security_group.backend.id]
    assign_public_ip = false
  }

  # Register containers with the backend ALB target group.
  # container_name and container_port must match the container definition above.
  # CRITICAL (G2): container_port = var.container_port (8080)
  load_balancer {
    target_group_arn = aws_lb_target_group.backend.arn
    container_name   = "backend"
    container_port   = var.container_port
  }

  # Ensure ALB listener exists before creating the service.
  # Without this dependency, Terraform may try to create the service before
  # the target group is associated with a listener, causing registration errors.
  depends_on = [aws_lb_listener.http]

  tags = {
    Name = "${var.name_prefix}-backend-svc"
  }
}


# =============================================================================
# SECTION 5: FRONTEND ECS SERVICE
# =============================================================================
# The frontend service runs the Nginx container serving the React 19 SPA
# build output. It registers with the frontend ALB target group, which
# receives all paths not matched by the backend listener rules (default
# action in alb.tf). Nginx uses try_files to serve index.html for SPA
# client-side routing.
# =============================================================================

resource "aws_ecs_service" "frontend" {
  name            = "${var.name_prefix}-frontend-svc"
  cluster         = aws_ecs_cluster.main.id
  task_definition = aws_ecs_task_definition.frontend.arn
  desired_count   = var.min_tasks
  launch_type     = "FARGATE"

  enable_execute_command = true

  network_configuration {
    subnets          = var.app_subnet_ids
    security_groups  = [aws_security_group.frontend.id]
    assign_public_ip = false
  }

  # Register containers with the frontend ALB target group.
  # Frontend Nginx listens on port 80.
  load_balancer {
    target_group_arn = aws_lb_target_group.frontend.arn
    container_name   = "frontend"
    container_port   = 80
  }

  depends_on = [aws_lb_listener.http]

  tags = {
    Name = "${var.name_prefix}-frontend-svc"
  }
}


# =============================================================================
# SECTION 6: AUTO-SCALING
# =============================================================================
# Both backend and frontend services use Application Auto Scaling with
# target tracking policies for CPU and Memory utilization. When either
# metric exceeds 70%, ECS scales out by launching additional tasks up to
# var.max_tasks. When utilization drops, ECS scales in to var.min_tasks.
#
# Environment-Specific Scaling Ranges (from locals.tf):
#   Dev:     1-4  tasks (cost optimization)
#   Staging: 2-6  tasks (moderate capacity)
#   Prod:    2-10 tasks (high availability)
# =============================================================================

# -----------------------------------------------------------------------------
# 6.1: Auto-Scaling Targets — Register services with Application Auto Scaling
# -----------------------------------------------------------------------------

resource "aws_appautoscaling_target" "backend" {
  service_namespace  = "ecs"
  resource_id        = "service/${aws_ecs_cluster.main.name}/${aws_ecs_service.backend.name}"
  scalable_dimension = "ecs:service:DesiredCount"
  min_capacity       = var.min_tasks
  max_capacity       = var.max_tasks
}

resource "aws_appautoscaling_target" "frontend" {
  service_namespace  = "ecs"
  resource_id        = "service/${aws_ecs_cluster.main.name}/${aws_ecs_service.frontend.name}"
  scalable_dimension = "ecs:service:DesiredCount"
  min_capacity       = var.min_tasks
  max_capacity       = var.max_tasks
}

# -----------------------------------------------------------------------------
# 6.2: CPU-Based Scaling Policies — Target 70% CPU utilization
# -----------------------------------------------------------------------------
# ECSServiceAverageCPUUtilization tracks the average CPU usage across all
# running tasks in the service. When CPU exceeds 70%, CloudWatch triggers
# a scale-out alarm and ECS launches additional tasks.
#
# The 70% threshold provides headroom for traffic spikes while avoiding
# over-provisioning. Scale-in cooldown prevents thrashing during
# fluctuating load patterns.
# -----------------------------------------------------------------------------

resource "aws_appautoscaling_policy" "backend_cpu" {
  name               = "${var.name_prefix}-backend-cpu-scaling"
  service_namespace  = "ecs"
  resource_id        = aws_appautoscaling_target.backend.resource_id
  scalable_dimension = aws_appautoscaling_target.backend.scalable_dimension
  policy_type        = "TargetTrackingScaling"

  target_tracking_scaling_policy_configuration {
    target_value = 70.0

    predefined_metric_specification {
      predefined_metric_type = "ECSServiceAverageCPUUtilization"
    }
  }
}

resource "aws_appautoscaling_policy" "frontend_cpu" {
  name               = "${var.name_prefix}-frontend-cpu-scaling"
  service_namespace  = "ecs"
  resource_id        = aws_appautoscaling_target.frontend.resource_id
  scalable_dimension = aws_appautoscaling_target.frontend.scalable_dimension
  policy_type        = "TargetTrackingScaling"

  target_tracking_scaling_policy_configuration {
    target_value = 70.0

    predefined_metric_specification {
      predefined_metric_type = "ECSServiceAverageCPUUtilization"
    }
  }
}

# -----------------------------------------------------------------------------
# 6.3: Memory-Based Scaling Policies — Target 70% memory utilization
# -----------------------------------------------------------------------------
# ECSServiceAverageMemoryUtilization tracks the average memory usage across
# all running tasks. The .NET 10 backend may consume significant memory for
# in-process caching (SplendidCache), SignalR hub connections, and request
# processing. Memory-based scaling ensures tasks are added before OOM kills.
#
# The same 70% threshold applies as CPU scaling to maintain consistent
# scaling behavior across both dimensions.
# -----------------------------------------------------------------------------

resource "aws_appautoscaling_policy" "backend_memory" {
  name               = "${var.name_prefix}-backend-memory-scaling"
  service_namespace  = "ecs"
  resource_id        = aws_appautoscaling_target.backend.resource_id
  scalable_dimension = aws_appautoscaling_target.backend.scalable_dimension
  policy_type        = "TargetTrackingScaling"

  target_tracking_scaling_policy_configuration {
    target_value = 70.0

    predefined_metric_specification {
      predefined_metric_type = "ECSServiceAverageMemoryUtilization"
    }
  }
}

resource "aws_appautoscaling_policy" "frontend_memory" {
  name               = "${var.name_prefix}-frontend-memory-scaling"
  service_namespace  = "ecs"
  resource_id        = aws_appautoscaling_target.frontend.resource_id
  scalable_dimension = aws_appautoscaling_target.frontend.scalable_dimension
  policy_type        = "TargetTrackingScaling"

  target_tracking_scaling_policy_configuration {
    target_value = 70.0

    predefined_metric_specification {
      predefined_metric_type = "ECSServiceAverageMemoryUtilization"
    }
  }
}
