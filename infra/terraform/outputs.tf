output "app_url" {
  description = "Public HTTPS URL of the app"
  value       = "https://${local.fqdn}"
}

output "alb_dns_name" {
  description = "ALB DNS (useful for quick curl tests before DNS propagates)"
  value       = aws_lb.main.dns_name
}

output "ecr_repository_url" {
  description = "ECR URL — used by CI to docker push"
  value       = aws_ecr_repository.web.repository_url
}

output "ecs_cluster_name" {
  value = aws_ecs_cluster.main.name
}

output "ecs_service_name" {
  value = aws_ecs_service.web.name
}

output "ecs_task_family" {
  value = aws_ecs_task_definition.web.family
}

output "db_endpoint" {
  description = "RDS endpoint (host:port)"
  value       = "${aws_db_instance.postgres.address}:${aws_db_instance.postgres.port}"
  sensitive   = false
}

output "db_secret_arn" {
  description = "Secrets Manager ARN for the DB connection string"
  value       = aws_secretsmanager_secret.db_url.arn
}

output "app_secret_arn" {
  description = "Secrets Manager ARN for app secrets (JWT, Syncfusion, WebPush)"
  value       = aws_secretsmanager_secret.app.arn
}

output "github_actions_role_arn" {
  description = "Put this in your GitHub repo as secret AWS_DEPLOY_ROLE_ARN"
  value       = aws_iam_role.github_actions.arn
}

output "alarms_sns_topic_arn" {
  description = "Subscribe an email/Slack webhook here to receive alarms"
  value       = aws_sns_topic.alarms.arn
}

output "cloudwatch_log_group" {
  value = aws_cloudwatch_log_group.web.name
}
