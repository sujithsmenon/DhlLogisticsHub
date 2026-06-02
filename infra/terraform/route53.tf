###############################################################################
# Route 53 — A record (alias) for the app's FQDN pointing at the ALB.
#
# The hosted zone for var.root_domain must exist already. If you registered
# the domain through Route 53, the zone is created automatically. If you
# registered it elsewhere, create the zone manually and update the
# registrar's NS records to the four AWS nameservers shown in the zone.
###############################################################################

data "aws_route53_zone" "root" {
  name         = var.root_domain
  private_zone = false
}

resource "aws_route53_record" "app" {
  zone_id = data.aws_route53_zone.root.zone_id
  name    = local.fqdn
  type    = "A"

  alias {
    name                   = aws_lb.main.dns_name
    zone_id                = aws_lb.main.zone_id
    evaluate_target_health = true
  }
}
