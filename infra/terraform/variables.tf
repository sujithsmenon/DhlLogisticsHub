variable "region" {
  description = "AWS region for all resources"
  type        = string
  default     = "ap-south-1"
}

variable "project_name" {
  description = "Short, dns-safe name used as a prefix for all resources"
  type        = string
  default     = "dhl-logistics"
}

variable "environment" {
  description = "Environment tag (prod/staging/dev). Determines task counts and instance sizes via locals."
  type        = string
  default     = "prod"
}

# ── Network ────────────────────────────────────────────────────────────────
variable "vpc_cidr" {
  description = "CIDR for the new VPC"
  type        = string
  default     = "10.40.0.0/16"
}

variable "az_count" {
  description = "Number of AZs (and pairs of public/private subnets) to spread across"
  type        = number
  default     = 2
}

# ── Domain / DNS ───────────────────────────────────────────────────────────
variable "root_domain" {
  description = "Root domain — must already have a Route 53 hosted zone in this account (e.g. pvgtlogistics.com)"
  type        = string
}

variable "subdomain" {
  description = "Subdomain to serve the app on. The full FQDN becomes <subdomain>.<root_domain>. Use '' to use the apex."
  type        = string
  default     = "app"
}

# ── Container ──────────────────────────────────────────────────────────────
variable "container_port" {
  description = "Port the container listens on (must match Dockerfile EXPOSE / ASPNETCORE_URLS)"
  type        = number
  default     = 8080
}

variable "image_tag" {
  description = "ECR image tag to deploy. CI overrides this with the commit SHA on each deploy."
  type        = string
  default     = "latest"
}

variable "task_cpu" {
  description = "Fargate task CPU units (256, 512, 1024, ...)"
  type        = number
  default     = 512
}

variable "task_memory" {
  description = "Fargate task memory (MiB)"
  type        = number
  default     = 1024
}

variable "desired_count" {
  description = "Number of ECS tasks to run. 2 = HA (one per AZ). 1 = cheaper but downtime on deploy/crash."
  type        = number
  default     = 2
}

# ── RDS ────────────────────────────────────────────────────────────────────
variable "db_name" {
  description = "Initial Postgres database name created at provision time"
  type        = string
  default     = "dhllogistics"
}

variable "db_username" {
  description = "Master username for the RDS Postgres instance"
  type        = string
  default     = "dhladmin"
}

variable "db_instance_class" {
  description = "RDS instance class. db.t4g.micro = ARM Graviton, cheapest viable for prod."
  type        = string
  default     = "db.t4g.small"
}

variable "db_allocated_storage" {
  description = "GB of gp3 storage to start with. RDS will autoscale up to db_max_allocated_storage."
  type        = number
  default     = 20
}

variable "db_max_allocated_storage" {
  description = "Cap on storage autoscaling"
  type        = number
  default     = 100
}

variable "db_multi_az" {
  description = "Multi-AZ standby (doubles the bill but gives <60s failover)"
  type        = bool
  default     = true
}

variable "db_backup_retention_days" {
  description = "Days of automated backups to retain"
  type        = number
  default     = 7
}

variable "db_deletion_protection" {
  description = "Block accidental destroy via Terraform"
  type        = bool
  default     = true
}

# ── App secrets (entered ONCE via terraform.tfvars or env vars, then stored
#    in Secrets Manager). Mark sensitive so Terraform never logs them. ──────
variable "jwt_signing_key" {
  description = "Base64 or 32+ char string used to sign JWTs for the mobile app"
  type        = string
  sensitive   = true
}

variable "syncfusion_license_key" {
  description = "Your Syncfusion Blazor license key"
  type        = string
  sensitive   = true
  default     = ""
}

variable "webpush_subject" {
  description = "mailto: address used by Web Push (VAPID)"
  type        = string
  default     = ""
}

variable "webpush_public_key" {
  type      = string
  sensitive = true
  default   = ""
}

variable "webpush_private_key" {
  type      = string
  sensitive = true
  default   = ""
}

# ── Observability ──────────────────────────────────────────────────────────
variable "log_retention_days" {
  description = "CloudWatch Logs retention. 30 days is the cost-efficient sweet spot."
  type        = number
  default     = 30
}

# ── GitHub OIDC (CI) ───────────────────────────────────────────────────────
variable "github_org" {
  description = "Your GitHub org / username (the owner segment of the repo URL)"
  type        = string
}

variable "github_repo" {
  description = "Repo name (without owner)"
  type        = string
}
