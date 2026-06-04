locals {
  name = "${var.project_name}-${var.environment}"

  fqdn = var.subdomain == "" ? var.root_domain : "${var.subdomain}.${var.root_domain}"

  # We serve the app on the apex when no subdomain is set.
  serve_apex = var.subdomain == ""

  # www.<root_domain> is provisioned and 301-redirected to the apex, but only
  # when we're actually serving the apex and the toggle is enabled.
  www_fqdn   = "www.${var.root_domain}"
  enable_www = local.serve_apex && var.enable_www_redirect

  # Subject Alternative Names for the ACM cert (adds www when redirecting).
  cert_sans = local.enable_www ? [local.www_fqdn] : []

  common_tags = {
    Project     = var.project_name
    Environment = var.environment
  }
}

data "aws_caller_identity" "current" {}
data "aws_availability_zones" "available" {
  state = "available"
}
