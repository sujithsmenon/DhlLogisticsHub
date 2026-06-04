###############################################################################
# ECS Fargate — cluster, task definition, service, autoscaling.
#
# CI updates the task definition's image tag on each deploy (see
# .github/workflows/deploy.yml). The lifecycle ignore on container_definitions
# below means Terraform won't fight the deploy.
###############################################################################

resource "aws_ecs_cluster" "main" {
  name = "${local.name}-cluster"

  setting {
    name  = "containerInsights"
    # "enabled" enables full Container Insights (paid). "enhanced" is the
    # cheaper, on-demand tier. Set "disabled" to save costs in early prod.
    value = "enhanced"
  }
}

resource "aws_ecs_cluster_capacity_providers" "main" {
  cluster_name = aws_ecs_cluster.main.name

  capacity_providers = ["FARGATE", "FARGATE_SPOT"]

  default_capacity_provider_strategy {
    capacity_provider = "FARGATE"
    base              = var.desired_count   # all tasks on-demand for prod stability
    weight            = 1
  }
}

# ── Task definition ────────────────────────────────────────────────────────
resource "aws_ecs_task_definition" "web" {
  family                   = "${local.name}-web"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = var.task_cpu
  memory                   = var.task_memory

  execution_role_arn = aws_iam_role.task_execution.arn
  task_role_arn      = aws_iam_role.task.arn

  runtime_platform {
    operating_system_family = "LINUX"
    cpu_architecture        = "X86_64"
  }

  container_definitions = jsonencode([
    {
      name      = "web"
      image     = "${aws_ecr_repository.web.repository_url}:${var.image_tag}"
      essential = true

      portMappings = [
        {
          containerPort = var.container_port
          hostPort      = var.container_port
          protocol      = "tcp"
        }
      ]

      environment = [
        { name = "ASPNETCORE_URLS",             value = "http://0.0.0.0:${var.container_port}" },
        { name = "ASPNETCORE_ENVIRONMENT",      value = "Production" },
        { name = "DOTNET_RUNNING_IN_CONTAINER", value = "true" },
        { name = "Aws__Region",                 value = var.region },
        { name = "Aws__BucketName",             value = var.s3_bucket_name },
        { name = "Jwt__Issuer",                 value = "DhlLogisticsHub" },
        { name = "Jwt__Audience",               value = "DhlLogisticsApp" },
        { name = "EmailSettings__ImapHost",            value = var.email_imap_host },
        { name = "EmailSettings__ImapPort",            value = tostring(var.email_imap_port) },
        { name = "EmailSettings__PollIntervalMinutes", value = tostring(var.email_poll_interval_minutes) },
      ]

      # Secrets pulled at task start from Secrets Manager. The value in each
      # `valueFrom` is "<secret-arn>:<json-key>::" for JSON-blob secrets, or
      # just "<secret-arn>" for plain-string secrets.
      # ECS secret entries support only { name, valueFrom }. For the JSON-blob
      # "app" secret, append ":<json-key>::" to pull a single key out.
      secrets = [
        { name = "ConnectionStrings__DefaultConnection", valueFrom = aws_secretsmanager_secret.db_url.arn },
        { name = "Jwt__Key",                valueFrom = "${aws_secretsmanager_secret.app.arn}:Jwt__Key::" },
        { name = "Syncfusion__LicenseKey",  valueFrom = "${aws_secretsmanager_secret.app.arn}:Syncfusion__LicenseKey::" },
        { name = "WebPush__Subject",        valueFrom = "${aws_secretsmanager_secret.app.arn}:WebPush__Subject::" },
        { name = "WebPush__PublicKey",      valueFrom = "${aws_secretsmanager_secret.app.arn}:WebPush__PublicKey::" },
        { name = "WebPush__PrivateKey",     valueFrom = "${aws_secretsmanager_secret.app.arn}:WebPush__PrivateKey::" },
        { name = "EmailSettings__Username", valueFrom = "${aws_secretsmanager_secret.app.arn}:EmailSettings__Username::" },
        { name = "EmailSettings__Password", valueFrom = "${aws_secretsmanager_secret.app.arn}:EmailSettings__Password::" },
      ]

      logConfiguration = {
        logDriver = "awslogs"
        options = {
          awslogs-group         = aws_cloudwatch_log_group.web.name
          awslogs-region        = var.region
          awslogs-stream-prefix = "web"
        }
      }

      # No container-level healthCheck: the chiseled runtime image has no shell,
      # so CMD-SHELL/wget/curl are unavailable. Task health is enforced by the
      # ALB target group (HTTP GET /api/ping in alb.tf), which already drains and
      # replaces unhealthy tasks. Adding a wget healthCheck here would fail every
      # probe on the chiseled image and put ECS into a task kill-loop.

      stopTimeout = 30   # gives Blazor SignalR circuits time to drain
    }
  ])

  tags = { Name = "${local.name}-web-taskdef" }

  lifecycle {
    # CI will register new task def revisions with new image tags. Terraform
    # should re-apply only when infra-level things change, not the image tag.
    ignore_changes = [container_definitions]
  }
}

# ── Service ────────────────────────────────────────────────────────────────
resource "aws_ecs_service" "web" {
  name            = "${local.name}-web"
  cluster         = aws_ecs_cluster.main.id
  task_definition = aws_ecs_task_definition.web.arn
  desired_count   = var.desired_count
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = aws_subnet.private[*].id
    security_groups  = [aws_security_group.ecs.id]
    assign_public_ip = false
  }

  load_balancer {
    target_group_arn = aws_lb_target_group.web.arn
    container_name   = "web"
    container_port   = var.container_port
  }

  deployment_controller {
    type = "ECS"
  }

  deployment_minimum_healthy_percent = 50    # rolling: at least 1 of 2 tasks always up
  deployment_maximum_percent         = 200   # can double-up during deploy

  health_check_grace_period_seconds = 60

  enable_execute_command = true   # so you can `aws ecs execute-command` for debugging

  propagate_tags = "SERVICE"

  depends_on = [
    aws_lb_listener.https,
    aws_iam_role_policy.task_execution_secrets,
  ]

  lifecycle {
    # CI's update-service call points us at the new task definition revision.
    ignore_changes = [task_definition, desired_count]
  }
}

# ── Autoscaling (CPU-based) ────────────────────────────────────────────────
resource "aws_appautoscaling_target" "web" {
  service_namespace  = "ecs"
  resource_id        = "service/${aws_ecs_cluster.main.name}/${aws_ecs_service.web.name}"
  scalable_dimension = "ecs:service:DesiredCount"
  min_capacity       = var.desired_count
  max_capacity       = var.desired_count * 4
}

resource "aws_appautoscaling_policy" "web_cpu" {
  name               = "${local.name}-web-cpu-scaling"
  policy_type        = "TargetTrackingScaling"
  resource_id        = aws_appautoscaling_target.web.resource_id
  scalable_dimension = aws_appautoscaling_target.web.scalable_dimension
  service_namespace  = aws_appautoscaling_target.web.service_namespace

  target_tracking_scaling_policy_configuration {
    predefined_metric_specification {
      predefined_metric_type = "ECSServiceAverageCPUUtilization"
    }
    target_value       = 65
    scale_in_cooldown  = 300
    scale_out_cooldown = 60
  }
}
