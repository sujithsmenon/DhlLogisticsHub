locals {
  name = "${var.project_name}-${var.environment}"

  fqdn = var.subdomain == "" ? var.root_domain : "${var.subdomain}.${var.root_domain}"

  common_tags = {
    Project     = var.project_name
    Environment = var.environment
  }
}

data "aws_caller_identity" "current" {}
data "aws_availability_zones" "available" {
  state = "available"
}
