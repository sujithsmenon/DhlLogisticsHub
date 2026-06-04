# Rollback Plan — DhlLogisticsHub on AWS

Region **ap-south-1**. Names assume `environment = prod` (resource prefix
`dhl-logistics-prod`). Pick the layer that broke and follow that runbook.

| # | Failure | Blast radius | Rollback |
|---|---|---|---|
| 1 | Bad app deploy | App only | Re-point ECS at the previous task-def revision |
| 2 | Bad image / build | App only | Redeploy the previous image SHA |
| 3 | Bad DB migration / data | Data | RDS snapshot or PITR; or fail back to Supabase during cutover |
| 4 | Bad Terraform change | Infra | `terraform apply` the previous state/commit, or targeted revert |
| 5 | DNS / TLS broken | Public reachability | Re-point Route 53 / re-validate ACM, or fail back to Render |

---

## 1. Roll back an application deploy (fastest, zero data risk)

ECS keeps every task-definition revision. CI deploys are rolling (min 50%
healthy), so the previous revision is intact.

```powershell
$CLUSTER = "dhl-logistics-prod-cluster"; $SERVICE = "dhl-logistics-prod-web"; $FAMILY = "dhl-logistics-prod-web"

# find the current and previous revisions
aws ecs describe-services --cluster $CLUSTER --services $SERVICE `
  --query 'services[0].taskDefinition' --output text
aws ecs list-task-definitions --family-prefix $FAMILY --sort DESC `
  --query 'taskDefinitionArns[0:5]' --output table

# roll back to a specific previous revision (e.g. :41)
aws ecs update-service --cluster $CLUSTER --service $SERVICE `
  --task-definition "$($FAMILY):41" --force-new-deployment

aws ecs wait services-stable --cluster $CLUSTER --services $SERVICE
```

## 2. Roll back to a previous image SHA

Every CI build pushes `sha-<commit>` (immutable) plus moves `:latest`. To redeploy
a known-good commit, re-register the current task def with the old image and update:

```powershell
$ACCOUNT = aws sts get-caller-identity --query Account --output text
$IMAGE = "$ACCOUNT.dkr.ecr.ap-south-1.amazonaws.com/dhl-logistics/web:sha-<good-sha>"

aws ecs describe-task-definition --task-definition dhl-logistics-prod-web `
  --query 'taskDefinition' --output json > td.json
# edit td.json: set containerDefinitions[0].image = $IMAGE; strip taskDefinitionArn,
# revision, status, requiresAttributes, compatibilities, registeredAt, registeredBy
$NEW = aws ecs register-task-definition --cli-input-json file://td.json `
  --query 'taskDefinition.taskDefinitionArn' --output text
aws ecs update-service --cluster dhl-logistics-prod-cluster `
  --service dhl-logistics-prod-web --task-definition $NEW --force-new-deployment
```
(The simplest path is often: `git revert` the bad commit and let CI redeploy.)

## 3. Database rollback

**Case A — migration/cutover failed and Supabase is still live (preferred during cutover):**
revert the connection string to Supabase and redeploy. No RDS writes happened in anger.
```powershell
aws secretsmanager put-secret-value --secret-id dhl-logistics-prod/db-url `
  --secret-string "Host=aws-0-ap-south-1.pooler.supabase.com;Port=6543;Database=postgres;Username=postgres.<ref>;Password=<pw>;SSL Mode=Require;Trust Server Certificate=true"
aws ecs update-service --cluster dhl-logistics-prod-cluster `
  --service dhl-logistics-prod-web --force-new-deployment
```

**Case B — bad data written to RDS:** restore from the pre-cutover snapshot or PITR.
```powershell
# point-in-time (within the 7-day backup window)
aws rds restore-db-instance-to-point-in-time `
  --source-db-instance-identifier dhl-logistics-prod-pg `
  --target-db-instance-identifier dhl-logistics-prod-pg-restore `
  --restore-time 2026-06-03T09:30:00Z

# or from the manual snapshot taken before cutover
aws rds restore-db-instance-from-db-snapshot `
  --db-instance-identifier dhl-logistics-prod-pg-restore `
  --db-snapshot-identifier dhl-logistics-prod-pg-precutover
```
A restore creates a **new** instance. Then either update the `db-url` secret to the
restored endpoint and redeploy, or rename instances during a short maintenance
window. RDS deletion protection means the bad instance is not auto-removed.

## 4. Terraform / infrastructure rollback

State is versioned in the S3 backend (bucket versioning is on).

- **Revert a change you just applied:** `git revert` the offending commit in
  `infra/terraform/`, then `terraform plan` / `apply`.
- **Restore a previous state file** (last resort): in the S3 state bucket, restore
  the prior version of `prod/terraform.tfstate`, then `terraform plan` to confirm
  drift before applying.
- **Scope a fix:** `terraform apply -target=<resource>` to change one thing.
- Remember `ignore_changes` on the ECS task def/service — Terraform will not undo a
  CI deploy; use §1/§2 for app rollbacks.

## 5. DNS / TLS rollback

- **ACM stuck `Pending validation`:** confirm the `cert_validation` CNAMEs exist in
  the `pvgt.co.in` zone and NS delegation is live, then `terraform apply` again.
- **Apex/www misrouted:** the A-aliases (`aws_route53_record.app` / `.www`) point at
  the ALB; re-`apply` to correct. TTL is 60s on validation records; alias records
  resolve to the ALB directly.
- **Full fail-back to Render (emergency):** while Render is still running, change the
  `pvgt.co.in` A/ALIAS record to Render's target (or re-enable the Render custom
  domain). Keep `render.yaml` until AWS has soaked. DNS TTLs govern propagation.

## 6. Pre-flagged safety nets already in the stack

- ECS rolling deploy, `deployment_minimum_healthy_percent = 50` → a failed deploy
  never takes 100% of capacity down; the old tasks keep serving.
- `aws_db_instance` `deletion_protection = true`, `skip_final_snapshot = false`.
- `aws_lb` `enable_deletion_protection = true`.
- RDS automated backups (7-day) + PITR; S3 + state-bucket versioning.
- CloudWatch alarms (5xx, unhealthy hosts, ECS/RDS CPU, RDS storage) → SNS, so you
  hear about a bad deploy quickly. **Subscribe an email to the SNS topic.**

## 7. Decision guide

- App broke after a deploy, data fine → **§1** (revision rollback). Seconds.
- Need a specific older build → **§2**. ~3–4 min.
- Data corrupted → **§3** (Case A if pre-cutover, Case B otherwise).
- Infra change went wrong → **§4**.
- Site unreachable / cert invalid → **§5**.
