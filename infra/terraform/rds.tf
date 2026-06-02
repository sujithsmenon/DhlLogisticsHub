###############################################################################
# RDS PostgreSQL 16, Multi-AZ, in the private subnets.
###############################################################################

resource "aws_db_subnet_group" "postgres" {
  name        = "${local.name}-db-subnets"
  description = "RDS subnet group — private subnets across all AZs"
  subnet_ids  = aws_subnet.private[*].id

  tags = { Name = "${local.name}-db-subnets" }
}

resource "aws_db_parameter_group" "postgres16" {
  name        = "${local.name}-pg16"
  family      = "postgres16"
  description = "Custom params for ${local.name} Postgres 16"

  parameter {
    name  = "rds.force_ssl"
    value = "1"
  }

  # Improve query planning for analytics-y workloads (ledger / trial balance).
  parameter {
    name  = "random_page_cost"
    value = "1.1"
  }
}

resource "aws_db_instance" "postgres" {
  identifier              = "${local.name}-pg"
  engine                  = "postgres"
  engine_version          = "16.4"
  instance_class          = var.db_instance_class
  allocated_storage       = var.db_allocated_storage
  max_allocated_storage   = var.db_max_allocated_storage
  storage_type            = "gp3"
  storage_encrypted       = true

  db_name                 = var.db_name
  username                = var.db_username
  password                = random_password.db_master.result

  port                    = 5432

  vpc_security_group_ids  = [aws_security_group.rds.id]
  db_subnet_group_name    = aws_db_subnet_group.postgres.name
  parameter_group_name    = aws_db_parameter_group.postgres16.name

  multi_az                = var.db_multi_az
  publicly_accessible     = false

  backup_retention_period = var.db_backup_retention_days
  backup_window           = "18:00-19:00"   # 23:30-00:30 IST
  maintenance_window      = "sun:19:30-sun:20:30"

  deletion_protection     = var.db_deletion_protection
  skip_final_snapshot     = false
  final_snapshot_identifier = "${local.name}-pg-final-${formatdate("YYYYMMDD-hhmm", timestamp())}"

  performance_insights_enabled = true
  monitoring_interval          = 60   # Enhanced Monitoring every 60s — minimal cost
  monitoring_role_arn          = aws_iam_role.rds_monitoring.arn

  apply_immediately = false   # take prod changes in the maintenance window

  tags = { Name = "${local.name}-pg" }

  lifecycle {
    ignore_changes = [
      # `password` is set once at creation. To rotate, do it via Secrets
      # Manager + RDS ModifyDBInstance manually (or wire in a rotator).
      password,
      # Final-snapshot-id contains a timestamp; we don't want spurious diffs.
      final_snapshot_identifier,
    ]
  }
}

# Role for RDS Enhanced Monitoring.
data "aws_iam_policy_document" "rds_monitoring_assume" {
  statement {
    actions = ["sts:AssumeRole"]
    principals {
      type        = "Service"
      identifiers = ["monitoring.rds.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "rds_monitoring" {
  name               = "${local.name}-rds-monitoring"
  assume_role_policy = data.aws_iam_policy_document.rds_monitoring_assume.json
}

resource "aws_iam_role_policy_attachment" "rds_monitoring" {
  role       = aws_iam_role.rds_monitoring.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonRDSEnhancedMonitoringRole"
}
