###############################################################################
# Secrets Manager — two secrets:
#
#   db_url — full ConnectionStrings__DefaultConnection. Updated by Terraform
#            because it derives from the RDS endpoint + master password.
#   app    — JWT key, Syncfusion license, WebPush keys. Updated rarely.
#
# ECS pulls these at task start via the `secrets:` block on the container def.
# Code accesses them via plain env vars (ASP.NET Configuration auto-binds
# ConnectionStrings__DefaultConnection, Jwt__Key, etc.)
###############################################################################

resource "random_password" "db_master" {
  length  = 24
  special = true
  override_special = "!#$%&*()-_=+[]{}<>?"   # avoid characters Postgres URIs dislike
}

resource "aws_secretsmanager_secret" "db_url" {
  name                    = "${local.name}/db-url"
  description             = "Postgres connection string (auto-managed by Terraform)"
  recovery_window_in_days = 7
}

resource "aws_secretsmanager_secret_version" "db_url" {
  secret_id = aws_secretsmanager_secret.db_url.id

  # Plain string — ECS task def references this secret directly as the value of
  # the ConnectionStrings__DefaultConnection env var.
  secret_string = format(
    "Host=%s;Port=%s;Database=%s;Username=%s;Password=%s;SSL Mode=Require;Trust Server Certificate=true",
    aws_db_instance.postgres.address,
    aws_db_instance.postgres.port,
    var.db_name,
    var.db_username,
    random_password.db_master.result,
  )
}

resource "aws_secretsmanager_secret" "app" {
  name                    = "${local.name}/app"
  description             = "App secrets — JWT key, Syncfusion license, WebPush VAPID"
  recovery_window_in_days = 7
}

resource "aws_secretsmanager_secret_version" "app" {
  secret_id = aws_secretsmanager_secret.app.id
  secret_string = jsonencode({
    "Jwt__Key"             = var.jwt_signing_key
    "Syncfusion__LicenseKey" = var.syncfusion_license_key
    "WebPush__Subject"     = var.webpush_subject
    "WebPush__PublicKey"   = var.webpush_public_key
    "WebPush__PrivateKey"  = var.webpush_private_key
  })
}
