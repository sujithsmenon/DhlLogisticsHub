# AWS Deployment Runbook — DhlLogisticsHub

Production-grade deploy of `DhlLogistics.Web` (.NET 10 Blazor Server) to AWS in
**ap-south-1 (Mumbai)** using Fargate + RDS Postgres + ALB + Route 53.

---

## 0. Architecture

```
Internet → Route 53 (app.yourdomain.com)
              ↓ DNS A-alias
            Application Load Balancer  (public subnets × 2 AZs, ACM TLS 1.2/1.3)
              ↓ HTTP :8080  (sticky-cookie target group, /api/ping health check)
            ECS Fargate service        (private subnets × 2 AZs, 2 tasks, autoscaling 2→8)
              ↓ TLS to 5432
            RDS PostgreSQL 16          (private subnets, Multi-AZ, encrypted)

Secrets Manager  →  ECS injects ConnectionStrings__*, Jwt__Key, Syncfusion__LicenseKey, WebPush__*
CloudWatch Logs  →  /ecs/dhl-logistics-prod-web   (30-day retention)
Alarms / SNS     →  5xx rate, unhealthy targets, ECS CPU > 80%, RDS CPU > 80%, low disk
ECR              →  dhl-logistics/web              (immutable SHA tags; :latest moves; 20-image cap)
GitHub Actions   →  OIDC role; assumes role on push to main; build → ECR → ECS rollout
```

---

## 1. Prerequisites

- AWS account, `aws configure` done locally with admin (or sufficient) creds
- Terraform `>= 1.6`
- Docker
- PostgreSQL 16 client tools (`pg_dump`, `psql`) on PATH
- A domain. Either:
  - Register through **Route 53** (creates the hosted zone for you), OR
  - Register elsewhere and create the Route 53 hosted zone, then point the
    registrar's NS records at the four AWS nameservers.

---

## 2. One-time bootstrap (Terraform remote state)

Terraform stores its state file. For production you want the state in S3 with
DynamoDB locking — otherwise two engineers running `apply` simultaneously will
corrupt it.

```powershell
cd infra\scripts
.\bootstrap-tf-backend.ps1 -Region ap-south-1 `
    -BucketName dhl-logistics-tfstate `
    -LockTable  dhl-logistics-tflock
```

Then edit `infra/terraform/backend.tf` — uncomment the `backend "s3"` block.

---

## 3. First Terraform apply

```powershell
cd infra\terraform
cp terraform.tfvars.example terraform.tfvars
notepad terraform.tfvars     # fill in root_domain, github_org/repo, jwt_signing_key, ...

terraform init
terraform plan -out tfplan
terraform apply tfplan
```

This creates **everything**: VPC, subnets, NAT, ECR, RDS, ALB, Route 53 record,
ACM cert (DNS-validated automatically), Secrets Manager entries, IAM roles,
ECS cluster + service + autoscaling, CloudWatch log group + alarms, SNS topic,
and the GitHub Actions OIDC role.

**The first apply takes ~12–15 minutes** — mostly waiting on RDS to provision
(~8 min) and ACM to validate (~1 min).

After it finishes, capture the outputs:

```powershell
terraform output
```

You'll need:
- `github_actions_role_arn` — set as GitHub repo secret `AWS_DEPLOY_ROLE_ARN`
- `db_endpoint` — for the data migration step below
- `ecr_repository_url` — informational

> **Note**: at this point ECS will fail to start the task because no image has
> been pushed to ECR yet. That's normal — fix it in the next step.

---

## 4. First image push (manual)

CI takes over after this, but you need to seed ECR once.

```powershell
$AWS_ACCOUNT_ID = aws sts get-caller-identity --query Account --output text
$ECR_URL        = "$AWS_ACCOUNT_ID.dkr.ecr.ap-south-1.amazonaws.com/dhl-logistics/web"

aws ecr get-login-password --region ap-south-1 |
    docker login --username AWS --password-stdin "$AWS_ACCOUNT_ID.dkr.ecr.ap-south-1.amazonaws.com"

docker build -t "${ECR_URL}:latest" .
docker push "${ECR_URL}:latest"

aws ecs update-service `
    --cluster dhl-logistics-prod-cluster `
    --service dhl-logistics-prod-web `
    --force-new-deployment
```

Watch the service stabilise in the AWS console (ECS → Clusters → Services).
First task should reach `RUNNING` and pass the ALB health check within ~90 s.

---

## 5. Database migration (Supabase → RDS)

The RDS instance is in a **private subnet** — not reachable from your laptop
directly. Two ways in:

**Option A — SSM port forward (easiest, no extra infra):**

```powershell
# Find a running ECS task ID
$TASK_ID = aws ecs list-tasks --cluster dhl-logistics-prod-cluster `
    --service-name dhl-logistics-prod-web --query 'taskArns[0]' --output text |
    Split-Path -Leaf

# Forward localhost:5432 → RDS:5432, via that task. (enable_execute_command=true in ecs.tf makes this work.)
aws ssm start-session `
    --target "ecs:dhl-logistics-prod-cluster_${TASK_ID}_$(...)" `
    --document-name AWS-StartPortForwardingSessionToRemoteHost `
    --parameters host=<rds-endpoint>,portNumber=5432,localPortNumber=5432
```

(Setting that up via ECS Exec is non-trivial; if you don't already use it, spin
up a small bastion EC2 in the public subnet for migration day, then terminate.)

**Option B — temporary bastion (10-minute version):**

```powershell
# Spin up a t3.nano in the public subnet with port 22 from your IP only
aws ec2 run-instances --image-id <al2023-ami> ...
ssh -L 5432:<rds-endpoint>:5432 ec2-user@<bastion>
# leave open in one terminal; run migration in another
```

**Run the migration:**

```powershell
# Pull the RDS password Terraform generated
$secret = aws secretsmanager get-secret-value `
    --secret-id dhl-logistics-prod/db-url --query SecretString --output text
if ($secret -match 'Password=([^;]+);') { $pw = $matches[1] }

cd infra\scripts
.\migrate-supabase-to-rds.ps1 `
    -SupabaseConn "postgresql://postgres.<ref>:<pw>@aws-0-ap-south-1.pooler.supabase.com:6543/postgres" `
    -RdsHost      "localhost" `
    -RdsPassword  $pw

# Then apply pending EF migrations to be safe.
.\apply-ef-migrations.ps1 -UseLocalhost
```

**Roll-forward (single cutover):**

1. Put Render's site into maintenance / take it offline.
2. Run the migration above.
3. Bump the Route 53 record's TTL to 60s ahead of time (already 60s — handled by Terraform).
4. The Route 53 A record is already pointing at the ALB. App is live.

---

## 6. Wire CI/CD (GitHub Actions)

In the GitHub repo:

1. **Settings → Secrets and variables → Actions → New repository secret**
   - Name: `AWS_DEPLOY_ROLE_ARN`
   - Value: paste the `github_actions_role_arn` output

2. Push a commit to `main`. The `Deploy to AWS ECS` workflow runs and:
   - Builds the image, pushes `sha-<commit>` and `:latest` to ECR
   - Registers a new task definition revision (with the new image URI)
   - Calls `aws ecs update-service` to roll the deployment
   - Waits for the service to reach steady state

Average deploy time: **3–4 minutes** end-to-end.

---

## 7. Health & verification

```powershell
# DNS + TLS
curl -I https://app.yourdomain.com/api/ping     # expect 200, valid cert

# Live tasks
aws ecs describe-services `
    --cluster dhl-logistics-prod-cluster `
    --services dhl-logistics-prod-web `
    --query 'services[0].{Desired:desiredCount,Running:runningCount,Pending:pendingCount}'

# Recent logs
aws logs tail /ecs/dhl-logistics-prod-web --follow
```

---

## 8. Operational tasks

### Subscribe to alarm emails
The Terraform creates an SNS topic (`alarms_sns_topic_arn`) but doesn't
subscribe anyone. In the AWS console → SNS → Topics → click the topic →
**Create subscription** → Protocol `Email` → enter your address →
confirm via the link you get by email.

### Rotate the JWT key
```powershell
aws secretsmanager update-secret `
    --secret-id dhl-logistics-prod/app `
    --secret-string '{"Jwt__Key":"<new>","Syncfusion__LicenseKey":"...", ...}'

aws ecs update-service `
    --cluster dhl-logistics-prod-cluster `
    --service dhl-logistics-prod-web `
    --force-new-deployment    # tasks reload secrets at start
```

### Run a one-off command in a task (e.g. `dotnet ef database update` from inside the VPC)
```powershell
$task = aws ecs run-task `
    --cluster dhl-logistics-prod-cluster `
    --launch-type FARGATE `
    --task-definition dhl-logistics-prod-web `
    --network-configuration "awsvpcConfiguration={subnets=[subnet-aaa,subnet-bbb],securityGroups=[sg-ecs],assignPublicIp=DISABLED}" `
    --overrides '{"containerOverrides":[{"name":"web","command":["dotnet","DhlLogistics.Web.dll","--migrate"]}]}'
```

(Add a `--migrate` switch to Program.cs that runs `Database.Migrate()` and exits if you want this clean.)

### Shell into a running task
```powershell
aws ecs execute-command `
    --cluster dhl-logistics-prod-cluster `
    --task <task-id> `
    --container web `
    --interactive `
    --command "/bin/sh"
```
(The chiseled image has no shell — switch the runtime base in `Dockerfile` if you want this.)

---

## 9. Cost outline (estimated, ap-south-1, USD/mo)

| Component | Notes | ~$/mo |
|---|---|---|
| Fargate (2× 0.5 vCPU, 1 GB, 24/7) | `task_cpu=512, task_memory=1024` | ~$25 |
| ALB | always-on | ~$22 |
| NAT Gateway | 1× single AZ, ~10 GB egress | ~$35 |
| RDS db.t4g.small Multi-AZ + 20 GB gp3 + backups | | ~$45 |
| Route 53 hosted zone + queries | | <$1 |
| CloudWatch Logs (30 days) | depends on log volume | $1–5 |
| CloudWatch alarms (5 of them) | | <$1 |
| ECR storage | < 3 GB images | <$1 |
| Container Insights "enhanced" | CW metrics | $5–10 |
| Secrets Manager (2 secrets) | $0.40 each + access | <$1 |
| **Total** | **~$140/mo** | |

### Cost optimisation knobs (low-risk first)
1. **`db_multi_az = false`** in `terraform.tfvars` → saves ~$22/mo. Failover is restore-from-backup (~30 min downtime) instead of <60s. Fine for non-critical periods.
2. **`db_instance_class = "db.t4g.micro"`** → saves ~$15/mo. Fine for the data volumes a freight agency handles.
3. **`task_cpu = 256, task_memory = 512`** + `desired_count = 1` → saves ~$15/mo. Use for staging or off-hours.
4. **`containerInsights = "disabled"`** in `ecs.tf` → saves ~$8/mo (lose richer dashboards).
5. **One-year Compute Savings Plan** on Fargate + RDS Reserved Instance → 30–40% off both, once usage is stable.
6. **VPC S3 endpoint** is already configured (free) — keeps S3 traffic off the NAT.
7. **`log_retention_days = 14`** if 30 days is more than you need.

---

## 10. Security recommendations

### Already in place
- ECS tasks + RDS in **private** subnets, no public IPs
- TLS 1.2 + 1.3 only at the ALB (`ELBSecurityPolicy-TLS13-1-2-2021-06`)
- HTTP → HTTPS 301 redirect
- HSTS automatically via `app.UseHttpsRedirection()` in Program.cs
- Secrets stored in **AWS Secrets Manager**, injected into ECS via `secrets:`
- IAM **task** role separate from **execution** role (least privilege)
- ECR **image scanning on push** (free, AWS Inspector)
- GitHub Actions uses **OIDC** (no long-lived AWS keys in GitHub)
- RDS storage encrypted (AES-256)
- RDS `rds.force_ssl = 1` — Postgres requires TLS
- RDS `deletion_protection = true`
- ALB `enable_deletion_protection = true`
- ALB `drop_invalid_header_fields = true`
- Default tags on all resources for cost allocation / audit

### Strongly recommended to add
1. **WAF on the ALB** — managed rules (AWSManagedRulesCommonRuleSet + AnonymousIpList). ~$5/mo + per-request. Blocks SQLi/XSS at the edge.
2. **AWS Backup vault for RDS** — copies the automated snapshots to a separate vault with cross-account access optional. Belt-and-braces for ransomware scenarios.
3. **GuardDuty** — anomaly detection. ~$3-5/mo for a small account.
4. **CloudTrail organisation trail** if you ever multi-account this.
5. **SCP / Permission boundary** on the GitHub OIDC role: tighten the wildcard ECS actions to specific ARNs.
6. **Rotate the RDS master password** quarterly: triggered Lambda + Secrets Manager rotation, restart ECS service. (Terraform's `random_password` is one-shot; you have to do this manually or wire in a rotator.)
7. **VPC Flow Logs** to S3 — cheap insurance for forensics. Add an `aws_flow_log` resource.
8. **Don't store `firebase-adminsdk.json` in the image.** Currently the app expects `Firebase:CredentialFile` to be a local path; move the JSON into Secrets Manager and switch to `GoogleCredential.FromJson(...)`.

### Things that are intentionally relaxed
- The single NAT in one AZ — if that AZ fails, the other AZ's tasks lose outbound internet (DB still works, ALB still serves). Acceptable for small scale. To harden, add a NAT per AZ (~$32/mo extra).
- ECR repo uses AES-256, not KMS-CMK. Switching to a CMK costs ~$1/mo per key.
- Only 1 month of automated RDS backups. Snapshots survive RDS deletion if you take a manual one; we do via `final_snapshot_identifier`.

---

## 11. Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| ECS tasks loop `STOPPED` with `Essential container exited` | App crashes — check CloudWatch Logs `/ecs/dhl-logistics-prod-web` | Usually a bad ConnectionString or missing secret JSON key |
| ALB returns 503 | No healthy targets | Wait 60s for first task to pass health check; or app is crashing — see logs |
| ACM cert stuck in `Pending validation` | DNS validation record not present | Re-run `terraform apply`; or check hosted zone NS records at the registrar |
| `terraform apply` fails: "no hosted zone found" | `var.root_domain` doesn't match a Route 53 zone in this account | Create the zone in Route 53 first, or fix the variable |
| ECS service stuck mid-deploy | New image tag missing from ECR, or task can't pull | Check ECR; check execution role policy includes ECR Pull |
| `pg_dump` from Supabase fails on a Supabase-internal schema | `--schema=public` not honoured for some object types | Add `--exclude-schema=auth --exclude-schema=storage --exclude-schema=graphql_public` |

---

## 12. Decommissioning Render

After 7+ days of stable operation on AWS:

1. Lower the Render service's `autoDeploy` to false in render.yaml so future pushes don't redeploy a now-stale stack.
2. In Render dashboard → Service → Settings → **Delete service**.
3. Delete the Supabase project (or downgrade to free tier and keep as cold backup until you're confident).
4. `git rm Dockerfile.render render.yaml` (we'll keep your *current* Dockerfile — it's the AWS one now).
