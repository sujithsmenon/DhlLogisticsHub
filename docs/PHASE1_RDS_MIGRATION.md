# Phase 1 — Supabase PostgreSQL → Amazon RDS PostgreSQL

**Goal:** move the DHLLogisticsHub ERP database from Supabase Postgres to Amazon RDS PostgreSQL, preserving all functionality. Hosting stays on Elastic Beanstalk / EC2. Region **ap-south-1**.

> This is the Phase 1 consolidated guide. The deep, step-by-step runbook with the `pg_dump`/restore commands lives in **`docs/MIGRATION_GUIDE.md`**; rollback detail in **`docs/ROLLBACK_PLAN.md`**; the migration helper script is **`infra/scripts/migrate-supabase-to-rds.ps1`**.

---

## 1. Migration Report (what changed in code)

**There is no Supabase-specific dependency in the codebase.** The app talks to Postgres only through `Npgsql.EntityFrameworkCore.PostgreSQL` (a standard PostgreSQL provider). "Supabase" was only ever the *host* of a plain Postgres endpoint, referenced in a connection string and a few comments. So Phase 1 is a **config + endpoint + documentation** change — no repository/service/ORM rewrite.

Changes made this phase:
- De-Supabase-ified the connection-string hint in `appsettings.json` and `appsettings.Template.json` → Amazon RDS format (`Port=5432`, dedicated `dhllogistics` database).
- Updated code comments in `Program.cs` and `Model/Menu.cs` (Supabase → PostgreSQL/RDS).
- Added `appsettings.Testing.json` so all three environments (Development / Testing / Production) are config-driven.
- **No package changes. No schema changes. No EF migration changes** — the existing Npgsql migrations are provider-agnostic and run unchanged on RDS.

Connection is fully environment-driven (unchanged mechanism): `appsettings.json` ships an **empty** `DefaultConnection`; the real value comes from **User Secrets** (Development) or the **`ConnectionStrings__DefaultConnection`** environment variable / AWS Secrets Manager (Testing/Production). Migrating to RDS = swap that one value; **no redeploy of code required** for the DB move itself.

---

## 2. RDS creation

Engine **PostgreSQL 16** (match the current server major version), ap-south-1.

Console → RDS → Create database (Standard create):
- Engine: PostgreSQL 16.x
- Templates: **Production** · Availability: **Multi-AZ** (single-AZ acceptable for staging)
- Instance class: start `db.t4g.medium` (scale later); Storage: gp3, 50 GB, **storage autoscaling on**
- Credentials: master user e.g. `dhlmaster` (store the password straight into Secrets Manager)
- Connectivity: **same VPC** as the EB environment; **no public access**; attach the DB security group (§3); use a DB subnet group across ≥2 AZs
- Additional config: **Initial database name `dhllogistics`**, backup retention **7–14 days**, enable **Performance Insights**, enable **automated minor version upgrades**, **deletion protection ON**, custom parameter group (§4)

CLI equivalent:
```bash
aws rds create-db-instance --region ap-south-1 \
  --db-instance-identifier dhl-logistics-prod \
  --engine postgres --engine-version 16.4 \
  --db-instance-class db.t4g.medium \
  --allocated-storage 50 --storage-type gp3 --max-allocated-storage 200 \
  --master-username dhlmaster --manage-master-user-password \
  --db-name dhllogistics \
  --db-subnet-group-name dhl-db-subnets \
  --vpc-security-group-ids sg-XXXXXXXX \
  --multi-az --backup-retention-period 14 \
  --db-parameter-group-name dhl-pg16 \
  --deletion-protection --no-publicly-accessible \
  --enable-performance-insights
```
(Terraform equivalents already scaffolded in `infra/terraform/rds.tf`.)

---

## 3. Security Groups
- **DB SG (`sg-db`)**: inbound TCP **5432** allowed **only** from the app tier's SG (EB/EC2 `sg-app`) — never `0.0.0.0/0`. No outbound rules needed for RDS.
- **App SG (`sg-app`)**: the EB instances' SG; add an outbound rule to `sg-db:5432` if egress is restricted.
- For a one-off migration from a workstation, use an **SSM port-forward / bastion** (see MIGRATION_GUIDE.md) rather than opening 5432 publicly.

## 4. Parameter Groups
Create a custom parameter group `dhl-pg16` (family `postgres16`):
- `rds.force_ssl = 1` — reject non-TLS connections (app already uses `SSL Mode=Require`).
- `timezone = UTC` (the app stores all timestamps in UTC — see the DateTime→UTC converters in `AppDbContext`).
- `log_min_duration_statement = 1000` (log slow queries), `log_connections/log_disconnections = 1` (optional).
- Leave `max_connections` at the class default; the app uses pooling via Npgsql.

## 5. Backup Strategy
- **Automated backups**: retention 14 days → enables **PITR** (point-in-time restore to any second in the window).
- **Manual snapshot** immediately after cutover and before every deploy: `aws rds create-db-snapshot --db-instance-identifier dhl-logistics-prod --db-snapshot-identifier dhl-precutover-<date>`.
- **Logical backup**: keep the `pg_dump` produced during migration (`supabase-dump.sql`) off-box ≥ 90 days as a portable fallback.
- Enable **cross-region snapshot copy** for DR if required.

## 6. Restore Strategy
- **PITR**: `aws rds restore-db-instance-to-point-in-time --source-db-instance-identifier dhl-logistics-prod --target-db-instance-identifier dhl-restore --restore-time <ts>` → then repoint the connection string to the restored instance.
- **Snapshot restore**: `aws rds restore-db-instance-from-db-snapshot ...`.
- **Logical**: `psql "<rds-conn>" < supabase-dump.sql` into a fresh DB.
- Restores create a **new instance** — cut over by swapping `ConnectionStrings__DefaultConnection`; no code change.

## 7. Connection String (per environment)
```
Host=<instance>.<hash>.ap-south-1.rds.amazonaws.com;Port=5432;Database=dhllogistics;Username=<db-user>;Password=<pw>;SSL Mode=Require;Trust Server Certificate=true
```
- **Development** → User Secrets (`dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..."`).
- **Testing** → env var `ConnectionStrings__DefaultConnection` on the staging environment (separate RDS/db).
- **Production** → AWS Secrets Manager → surfaced as the EB env var `ConnectionStrings__DefaultConnection`.
- Stronger TLS (recommended for prod): `SSL Mode=VerifyFull;Root Certificate=/path/rds-combined-ca-bundle.pem` (download the RDS global CA bundle and ship it with the app).

## 8. Secrets Management
- **Never** put the connection string, `Jwt:Key`, `Syncfusion:LicenseKey`, or `Seed:AdminPassword` in any `appsettings*.json` — those files ship in the bundle.
- Production: AWS **Secrets Manager** (paths scaffolded in `infra/terraform/secrets.tf`), surfaced to EB as env vars (`A__B` → config key `A:B`). Inventory: `docs/SECRETS_INVENTORY.md`.
- The RDS master password: use `--manage-master-user-password` so RDS stores/rotates it in Secrets Manager automatically; the app uses a **least-privilege** user, not the master.

---

## 9. Migrations on RDS (Requirement 5 & 9)
The Npgsql EF Core migrations are provider-agnostic and already validated on PostgreSQL. Apply them to the fresh RDS DB **before** first app boot:
```bash
ConnectionStrings__DefaultConnection="<rds-conn>" \
  dotnet ef database update --project DhlLogistics.Web
```
Then `dotnet ef migrations list` should show all applied, none pending. (Migrations are **not** auto-run at startup by design — only the seeds below are.)

## 10. Startup seeds (Requirement 10)
On first boot against RDS, `Program.cs` runs these idempotently (each in its own try/catch):
- **Roles + admin** (Identity) · **Chart of Accounts** (`AccountSeed`, 15 heads) · **M2 masters** · **Permissions** (`PermissionSeed`) · **Menu** (`MenuSeed` + `EnsureFinanceMenusAsync`) · **Billing backfill** (`BillingSyncService`, create-only for Approved/Closed jobs).
Verify post-boot: `AccountHeads=15`, `Menus` populated, roles present.

## 11. Functional verification (Requirement 6)
The application logic is **DB-engine agnostic** and was verified end-to-end on PostgreSQL (79/79 integration checks). Because RDS is the same engine, re-run this smoke set against RDS after cutover:
CRUD (masters) · Authentication (login/roles) · Workflow (job create→verify→approve) · Billing (bill submit→verify→approve) · Accounts (voucher posted) · Reports (P&L / Trial Balance / KPI) · Dashboard (live counters) · Background services (email poller starts).

---

## 12. Deployment Guide (cutover)
1. Snapshot-safe: confirm a fresh Supabase logical dump exists.
2. `terraform apply` the RDS resources (or create via console/CLI §2). Keep Supabase live.
3. Migrate data: `infra/scripts/migrate-supabase-to-rds.ps1` (pg_dump `public` → restore into RDS via SSM tunnel).
4. Apply EF migrations to RDS (§9) — usually already covered by the dump; run to reconcile.
5. Put `ConnectionStrings__DefaultConnection` (RDS) into Secrets Manager / EB env.
6. `eb deploy` (or just restart) so the app picks up the RDS endpoint; startup seeds run.
7. Run §11 smoke checks. Soak ≥ 7 days with Supabase kept as cold standby.
8. Decommission Supabase only after the soak.

## 13. Rollback Guide
- **During cutover, Supabase still live (preferred):** revert `ConnectionStrings__DefaultConnection` to the Supabase value and restart/redeploy — no RDS writes in anger, zero data loss.
- **After writes on RDS:** restore via RDS **PITR** or the **pre-cutover snapshot** (§6), then repoint the connection string.
- App rollback (unrelated to DB): `eb deploy --version <previous-label>`.
- Full detail: `docs/ROLLBACK_PLAN.md`.

---

## Files Modified (Phase 1)
- `DhlLogistics.Web/appsettings.json` — RDS connection-string hint.
- `DhlLogistics.Web/appsettings.Template.json` — RDS connection template + note.
- `DhlLogistics.Web/appsettings.Testing.json` — **new** Testing environment config.
- `DhlLogistics.Web/Program.cs` — DB/menu comments (Supabase → RDS/PostgreSQL).
- `DhlLogistics.Web/Model/Menu.cs` — comment (Supabase → PostgreSQL).
- `docs/PHASE1_RDS_MIGRATION.md` — **new** this document.

## Database Changes
- **None to schema or migrations.** Target endpoint changes from Supabase Postgres to Amazon RDS Postgres (same engine). Data is copied via `pg_dump`/restore at cutover.

## Architecture Notes
- No architecture change. Clean Architecture and all existing services reused. The persistence layer stays `AppDbContext` + Npgsql; only the connection endpoint moves. The env-driven connection design means the DB provider/host is swappable without code changes.
