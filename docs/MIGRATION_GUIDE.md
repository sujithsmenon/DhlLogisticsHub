# Database Migration Guide — Supabase PostgreSQL → Amazon RDS PostgreSQL

Region **ap-south-1** • Source: Supabase (pooled Postgres) • Target: RDS
PostgreSQL 16, Multi-AZ, private subnets (provisioned by `infra/terraform`).

This guide covers Task D: the migration plan, backup + rollback strategy, EF Core
commands, and the connection-string / environment-variable mapping.

---

## 1. Strategy at a glance

The app is **already** PostgreSQL + EF Core, so this is a **data + schema copy**,
not a database-engine port:

1. Provision RDS via Terraform (empty `dhllogistics` database is created).
2. `pg_dump` the Supabase `public` schema (data + DDL).
3. Restore the dump into RDS over a private-network path (SSM port-forward or bastion).
4. Run `dotnet ef database update` to guarantee the schema matches the latest migration.
5. Point the app's connection string (Secrets Manager) at RDS and redeploy.
6. Verify, then decommission Supabase after a soak period.

A single **cutover window** (app offline for the dump+restore) is the simplest and
safest approach for a freight-agency data volume. Estimated downtime: 5–20 min.

Scripts that implement this live in `infra/scripts/`:
- `migrate-supabase-to-rds.ps1` — dump from Supabase, restore into RDS.
- `apply-ef-migrations.ps1` — pull the conn string from Secrets Manager, run EF update.

## 2. Prerequisites

- RDS provisioned (`terraform apply` done; `terraform output db_endpoint` available).
- PostgreSQL **16** client tools (`pg_dump`, `psql`) on PATH.
- A private-network path to RDS (it is **not** publicly accessible):
  - **Option A — SSM port-forward** (no extra infra; needs `enable_execute_command`, already on).
  - **Option B — temporary bastion** EC2 in a public subnet, SSH tunnel, terminate after.
- AWS CLI configured with permission to read `dhl-logistics-prod/db-url`.

## 3. Backup strategy (before you touch anything)

| What | How | Retention |
|---|---|---|
| **Source (Supabase) logical backup** | `pg_dump` → `supabase-dump.sql` (the migrate script produces this; keep a copy off-box) | keep ≥ 90 days |
| **Supabase stays live** | Do **not** delete the Supabase project until AWS has soaked ≥ 7 days | — |
| **RDS automated backups** | Enabled by Terraform: `backup_retention_period = 7`, window `18:00–19:00 UTC` (23:30 IST) | 7 days, PITR |
| **RDS pre-cutover manual snapshot** | `aws rds create-db-snapshot --db-instance-identifier dhl-logistics-prod-pg --db-snapshot-identifier dhl-logistics-prod-pg-precutover` | until deleted |
| **RDS final snapshot on destroy** | Terraform `skip_final_snapshot = false` → auto final snapshot | until deleted |

## 4. Step-by-step

```powershell
# 0. (Recommended) take a manual RDS snapshot you can roll back to
aws rds create-db-snapshot --region ap-south-1 `
  --db-instance-identifier dhl-logistics-prod-pg `
  --db-snapshot-identifier dhl-logistics-prod-pg-precutover

# 1. Open a private path to RDS (SSM port-forward shown; leave running)
#    localhost:5432 → RDS:5432
aws ssm start-session `
  --target <ecs-task-or-bastion-instance-id> `
  --document-name AWS-StartPortForwardingSessionToRemoteHost `
  --parameters host=<rds-endpoint>,portNumber=5432,localPortNumber=5432

# 2. Pull the RDS master password Terraform generated
$secret = aws secretsmanager get-secret-value --region ap-south-1 `
  --secret-id dhl-logistics-prod/db-url --query SecretString --output text
if ($secret -match 'Password=([^;]+);') { $pw = $matches[1] }

# 3. Dump from Supabase + restore into RDS (via the tunnel → -RdsHost localhost)
cd infra\scripts
.\migrate-supabase-to-rds.ps1 `
  -SupabaseConn "postgresql://postgres.<ref>:<supabase-pw>@aws-0-ap-south-1.pooler.supabase.com:6543/postgres" `
  -RdsHost      "localhost" `
  -RdsPassword  $pw

# 4. Apply EF migrations to guarantee schema parity with the codebase
.\apply-ef-migrations.ps1 -UseLocalhost
```

If `pg_dump` trips over Supabase-internal schemas, add to the dump:
`--exclude-schema=auth --exclude-schema=storage --exclude-schema=graphql_public`.

## 5. EF Core commands (reference)

```powershell
# install the tool once
dotnet tool install -g dotnet-ef     # or: dotnet tool update -g dotnet-ef

# list migrations (sanity)
dotnet ef migrations list --project DhlLogistics.Web

# apply all pending migrations to a target DB
dotnet ef database update --project DhlLogistics.Web `
  --connection "Host=<host>;Port=5432;Database=dhllogistics;Username=dhladmin;Password=<pw>;SSL Mode=Require;Trust Server Certificate=true"

# generate an idempotent SQL script instead of applying live (good for review/runbooks)
dotnet ef migrations script --idempotent --project DhlLogistics.Web -o migrate.sql
```

> The app also seeds roles + the admin user and runs idempotent master/permission
> seeds on startup (`Program.cs`). After cutover, the first task start will
> reconcile those automatically.

## 6. Connection string & environment-variable mapping

ASP.NET Core binds `__` (double underscore) env vars to nested config keys.

| App config key | Env var injected into the container | Value source on AWS |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | Secrets Manager `dhl-logistics-prod/db-url` (plain string) |
| `Jwt:Key` | `Jwt__Key` | Secrets Manager `dhl-logistics-prod/app` → key `Jwt__Key` |
| `Jwt:Issuer` / `Jwt:Audience` | `Jwt__Issuer` / `Jwt__Audience` | ECS task-def env |
| `Syncfusion:LicenseKey` | `Syncfusion__LicenseKey` | app secret |
| `WebPush:Subject/PublicKey/PrivateKey` | `WebPush__*` | app secret |
| `EmailSettings:Username/Password` | `EmailSettings__Username/Password` | app secret |
| `EmailSettings:ImapHost/ImapPort/PollIntervalMinutes` | `EmailSettings__*` | ECS task-def env |
| `Aws:Region` / `Aws:BucketName` | `Aws__Region` / `Aws__BucketName` | ECS task-def env |
| `Seed:AdminEmail` / `Seed:AdminPassword` | `Seed__AdminEmail` / `Seed__AdminPassword` | optional override (defaults in code) |

**Connection string formats**

```
# Supabase (source) — transaction pooler
Host=aws-0-ap-south-1.pooler.supabase.com;Port=6543;Database=postgres;Username=postgres.<ref>;Password=<pw>;SSL Mode=Require;Trust Server Certificate=true

# RDS (target) — built automatically by Terraform into the db-url secret
Host=<rds-endpoint>;Port=5432;Database=dhllogistics;Username=dhladmin;Password=<pw>;SSL Mode=Require;Trust Server Certificate=true
```

You do **not** hand-edit the RDS connection string — `infra/terraform/secrets.tf`
composes it from the RDS endpoint + generated password and stores it in Secrets
Manager. ECS injects it at task start.

## 7. Verification

```powershell
# row counts on a few core tables (via the tunnel)
$env:PGPASSWORD = $pw
psql -h localhost -U dhladmin -d dhllogistics -c "\dt"
psql -h localhost -U dhladmin -d dhllogistics -c "SELECT count(*) FROM \"AwbShipments\";"

# app health after repoint + redeploy
curl -I https://pvgt.co.in/api/ping     # 200
aws logs tail /ecs/dhl-logistics-prod-web --follow
```

## 8. Rollback (DB-specific — see ROLLBACK_PLAN.md for the full matrix)

- **Cutover failed, Supabase still live:** revert the `db-url` secret to the
  Supabase connection string and `force-new-deployment`. No data lost (you never
  wrote to RDS in anger). This is the fastest fail-back.
- **Bad data after writes to RDS:** restore the `dhl-logistics-prod-pg-precutover`
  snapshot, or use PITR (`aws rds restore-db-instance-to-point-in-time`).
- Keep `supabase-dump.sql` until you are confident — it can re-seed RDS from scratch.

## 9. Decommission Supabase

Only after ≥ 7 days stable on RDS: downgrade/pause the Supabase project first
(cold standby), confirm nothing references it, then delete. See
`AWS_DEPLOYMENT.md` §12.
