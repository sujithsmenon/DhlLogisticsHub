###############################################################################
# ALB + listeners + target group.
#
# Blazor Server needs the SignalR circuit to stick to one task between
# requests OR cope with reconnects. We enable cookie stickiness on the
# target group to be safe; reconnects are reliable but sticky avoids them.
###############################################################################

resource "aws_lb" "main" {
  name               = "${local.name}-alb"
  internal           = false
  load_balancer_type = "application"
  security_groups    = [aws_security_group.alb.id]
  subnets            = aws_subnet.public[*].id

  # Idle timeout: SignalR sends a keep-alive every 15s, so 60s is fine.
  idle_timeout = 60

  drop_invalid_header_fields = true

  enable_deletion_protection = true

  tags = { Name = "${local.name}-alb" }
}

resource "aws_lb_target_group" "web" {
  name        = "${local.name}-tg"
  port        = var.container_port
  protocol    = "HTTP"
  target_type = "ip"
  vpc_id      = aws_vpc.main.id

  health_check {
    enabled             = true
    path                = "/api/ping"        # anonymous endpoint, see Program.cs
    protocol            = "HTTP"
    port                = "traffic-port"
    matcher             = "200"
    interval            = 30
    timeout             = 5
    healthy_threshold   = 2
    unhealthy_threshold = 3
  }

  stickiness {
    type            = "lb_cookie"
    cookie_duration = 86400        # 1 day
    enabled         = true
  }

  deregistration_delay = 30        # ECS rolling deploys: 30s drain, then kill

  tags = { Name = "${local.name}-tg" }
}

# ── Listeners ──────────────────────────────────────────────────────────────
resource "aws_lb_listener" "http" {
  load_balancer_arn = aws_lb.main.arn
  port              = 80
  protocol          = "HTTP"

  # Redirect everything to HTTPS.
  default_action {
    type = "redirect"
    redirect {
      port        = "443"
      protocol    = "HTTPS"
      status_code = "HTTP_301"
    }
  }
}

resource "aws_lb_listener" "https" {
  load_balancer_arn = aws_lb.main.arn
  port              = 443
  protocol          = "HTTPS"
  ssl_policy        = "ELBSecurityPolicy-TLS13-1-2-2021-06"   # TLS 1.2 + 1.3 only
  certificate_arn   = aws_acm_certificate_validation.alb.certificate_arn

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.web.arn
  }
}
