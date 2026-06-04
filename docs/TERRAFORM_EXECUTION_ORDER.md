# Terraform Execution Order — DhlLogisticsHub (ap-south-1)

How to stand up the AWS infrastructure in `infra/terraform/`, in the right order,
including the manual steps Terraform cannot do for you.

> Terraform `>= 1.6`, AWS provider `~> 5.70`. The config is split across files but
> is **one root module** — `terraform apply` builds the whole stack. The ordering
> below is what Terraform resolves internally from resource references; you don't
> apply files one by one.

---

## 0. Prerequisites (manual, before any apply)

1. **Install** Terraform ≥ 1.6, AWS CLI, Docker.
2. **AWS credentials** with admin (or sufficient) rights: `aws configure` /
   `aws sso login`.
3. **Route 53 public hosted zone for `pvgt.co.in` must already exist** in this
   account. Terraform *reads* it (`data "aws_route53_zone" "root"`); it does not
   create it.
   - Registered via Route 53 → zone exists automatically.
   - Registered elsewhere → create the hosted zone, then point the registrar's
     NS records at the 4 AWS nameservers. ACM DNS validation will not complete
     until NS delegation is live.
4. **`terraform.tfvars`** — copy the example and fill it in:
   ```powershell
   cd infra\terraform
   Copy-Item terraform.tfvars.example terraform.tfvars
   notepad terraform.tfvars   # root_domain, github_org/repo, jwt_signing_key, syncfusion key, email, ...
   ```
   `terraform.tfvars` is gitignored — never commit it.

## 1. Bootstrap remote state (run ONCE)

```powershell
cd infra\scripts
.\bootstrap-tf-backend.ps1 -Region ap-south-1 `
    -BucketName dhl-logistics-tfstate -LockTable dhl-logistics-tflock
```
Then uncomment the `backend "s3"` block in `infra/terraform/backend.tf` and run
`terraform init -migrate-state`. (You can skip this for a throwaway/dev stack and
keep local state — but do it for prod.)

## 2. Init / plan / apply

```powershell
cd infra\terraform
terraform init
terraform fmt -recursive          # format check (no AWS calls)
terraform validate                # static validation (no AWS calls)
terraform plan -out tfplan
terraform apply tfplan
```

**First apply ≈ 12–15 min** — dominated by RDS provisioning (~8 min) and ACM DNS
validation (~1–3 min). ACM validation **blocks** the HTTPS listener, which blocks
the ECS service — this is expected and handled by resource dependencies.

## 3. Internal dependency order (what `apply` does, conceptually)

```
providers + data sources (caller identity, AZs, route53 zone)
      │
      ▼
VPC ─► IGW, subnets ─► NAT (EIP) ─► route tables ─► S3 VPC endpoint
      │                                   │
      ├─► security groups (alb ► ecs ► rds)
      │
      ├─► ECR repo (+ lifecycle policy)         ─► (image pushed later by CI)
      ├─► S3 PDF bucket (+ PAB, encryption, versioning, lifecycle)
      ├─► CloudWatch log group + SNS + alarms
      ├─► Secrets Manager: app secret (from tfvars)
      │
      ├─► RDS subnet group + param group ─► RDS instance ─► db-url secret
      │
      ├─► ACM cert ─► Route53 validation records ─► cert validation (waits)
      │                                                    │
      └─► ALB ─► target group ─► HTTP(80) redirect listener │
                                 HTTPS(443) listener ◄───────┘ (needs valid cert)
                                 └─► www→apex redirect rule
                 Route53 A-alias (apex) + www A-alias ─► ALB
      │
      ▼
IAM (task exec role, task role, GitHub OIDC role)
      ▼
ECS cluster ─► task definition ─► service (waits on HTTPS listener + exec-secrets policy)
      ▼
App Autoscaling target + CPU policy
```

> **Note:** the ECS service will report the task failing to start until the first
> image exists in ECR (step 5). That is normal on a fresh stack.

## 4. Post-apply manual steps (Terraform can't / shouldn't do these)

1. **Capture outputs:** `terraform output`
2. **GitHub secret:** put `github_actions_role_arn` into the repo as
   `AWS_DEPLOY_ROLE_ARN` (Settings → Secrets and variables → Actions).
3. **Seed the first image** into ECR (CI takes over afterward) — see
   `AWS_DEPLOYMENT.md` §4, then `aws ecs update-service ... --force-new-deployment`.
4. **Subscribe to alarms:** SNS topic `alarms_sns_topic_arn` has no subscribers —
   add an email subscription in the console and confirm it.
5. **Database migration:** follow `MIGRATION_GUIDE.md`.
6. **NS delegation** (only if the domain is registered outside Route 53): point the
   registrar at the zone's AWS nameservers, or ACM validation stays `Pending`.

## 5. Targeted / iterative applies

- App-only changes deploy via **CI**, not Terraform. `ecs.tf` has
  `ignore_changes = [container_definitions, task_definition, desired_count]`, so
  Terraform won't fight CI's task-def revisions or autoscaling.
- Infra tweaks: normal `plan`/`apply`.
- Re-validate one resource: `terraform plan -target=aws_lb_listener.https`.

## 6. Engine-version gotcha

`rds.tf` pins `engine_version = "16.4"`. If apply fails with an invalid/withdrawn
version for ap-south-1, list what's available and bump it:
```powershell
aws rds describe-db-engine-versions --engine postgres --region ap-south-1 `
  --query "DBEngineVersions[?starts_with(EngineVersion,'16.')].EngineVersion"
```

## 7. Destroy (careful — protections are ON by design)

Two safety locks block a naive `terraform destroy`:
- `aws_db_instance.postgres.deletion_protection = true`
- `aws_lb.main.enable_deletion_protection = true`

To tear down (dev/staging only — think twice for prod):
```powershell
# 1. take a manual RDS snapshot first
aws rds create-db-snapshot --db-instance-identifier dhl-logistics-prod-pg `
  --db-snapshot-identifier dhl-logistics-prod-pg-teardown
# 2. flip protections (set the vars false in tfvars, or modify in console), apply
terraform apply -var db_deletion_protection=false
# 3. (ALB) disable deletion protection in the console or temporarily in alb.tf
# 4. destroy
terraform destroy
```
RDS will still emit a **final snapshot** (`skip_final_snapshot = false`). The
ECR repo and S3 bucket must be empty before they delete — empty them or expect
the destroy to error on those.
