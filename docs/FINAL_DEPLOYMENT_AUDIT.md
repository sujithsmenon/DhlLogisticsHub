# Final Deployment Audit — DHL Logistics Hub

**Date:** 2026-06-10 · **Branch:** master · **Source of truth:** repository working tree only
**Target:** AWS Elastic Beanstalk (Windows/IIS), ap-south-1, env `DhlLogisticsHub-prod`, domain `pvgt.co.in`

## Phase 1 search results

Patterns searched across the whole repo (excluding `CBM_Project_Reference` sample + build output).

| # | Pattern | Hits in **source** | Hits in **docs** | Verdict |
|---|---------|--------------------|------------------|---------|
| 1 | `CHANGE-THIS-TO-A-32-CHAR-SECRET-KEY` | `appsettings.json:22` | several (describing it) | ⚠️ real |
| 2 | `Admin@1234` | `Program.cs:228` | several | ⚠️ real |
| 3 | `localhost` | `launchSettings.json:8,17`; `AppConfig.cs:15-16` (dev comments) | infra scripts/docs | ✅ dev-only |
| 4 | `pvgtlogistics.com` | **none** | docs only | ✅ clean |
| 5 | `.NET 10` / `net10` | **none** | `AWS_DEPLOYMENT.md:3`, `DOTNET8_COMPATIBILITY.md` | ⚠️ stale doc text only |
| 6 | `app.yourdomain.com` | **none** | `AWS_DEPLOYMENT.md:11,200` | ⚠️ placeholder in scaffold doc |
| 7 | hardcoded secrets | `appsettings.json` (Syncfusion key, JWT placeholder, email placeholder) | n/a | ⚠️ see below |

## Findings (file · line · severity · action)

| ID | Finding | File · line | Severity | Recommended action |
|----|---------|-------------|----------|--------------------|
| F1 | JWT signing key is the committed placeholder → bearer-token forgery for any role | `DhlLogistics.Web/appsettings.json:22` | **CRITICAL** | Set EB env `Jwt__Key` to a 32+ byte random secret (`openssl rand -base64 48`). Startup logs Critical until fixed. See `JWT_CONFIGURATION.md`. |
| F2 | Syncfusion license key committed in a tracked file | `DhlLogistics.Web/appsettings.json:19` | **HIGH** | Blank in commits; inject `Syncfusion__LicenseKey` via EB env / Secrets Manager. See `SYNCFUSION_SETUP.md`. |
| F3 | No TLS on `pvgt.co.in` (A-record → bare EIP `13.234.76.3`) | infra (Route53/EB) | **HIGH** | Add ACM cert + ALB HTTPS listener; flip `Security__RequireHttps=true`. See `PVGT_TLS_IMPLEMENTATION.md`. |
| F4 | Seed admin password literal fallback `Admin@1234` | `DhlLogistics.Web/Program.cs:228` | **MEDIUM** | Set EB env `Seed__AdminPassword`; reset the admin account if `Admin@1234` was ever live. |
| F5 | Email IMAP password placeholder (`your-app-password`) | `DhlLogistics.Web/appsettings.json:10` | LOW | Only used if email polling enabled; set `EmailSettings__Password` via env when used. |
| F6 | `appsettings.Production.json` ships `Security:RequireHttps=true` while env is HTTP-only | `DhlLogistics.Web/appsettings.Production.json` | **HIGH (deploy-gating)** | Do **not** deploy this file until TLS is live, or set EB env `Security__RequireHttps=false` to override. Flagged in-file. |
| F7 | Stale `.NET 10` reference + `app.yourdomain.com` placeholder in scaffold doc | `docs/AWS_DEPLOYMENT.md:3,11,200` | LOW (doc) | Project is .NET 8 (`global.json` `8.0.100`, csproj `net8.0`). Fix or delete this scaffold doc (it documents the unapplied Terraform/RDS path — preserved, not deleted, this pass). |
| F8 | Anonymous diagnostic endpoints `/diag`, `/_diag/files` | `DhlLogistics.Web/Program.cs` | ✅ FIXED | Already gated to `IsDevelopment()` → 404 in production. `/health`, `/api/ping` retained intentionally. |
| F9 | No auto-migration; role/admin seed not in try/catch | `DhlLogistics.Web/Program.cs` seed block | LOW | Safe for the existing Supabase schema; for a fresh DB run `dotnet ef database update` first. |

## Config-override verification (Phase 2 requirement)
ASP.NET Core configuration providers layer in this order (later wins): `appsettings.json` → `appsettings.{Environment}.json` → **environment variables** → command line. Therefore **every** key (`Jwt:Key`, `Syncfusion:LicenseKey`, `ConnectionStrings:DefaultConnection`, `Security:RequireHttps`, `Seed:AdminPassword`, `EmailSettings:*`) is overridable at runtime via its env-var form `A__B` (double underscore → `:`). Confirmed by code reads in `Program.cs` / `AuthEndpoints.cs` / `EmailReaderService.cs` — all use `IConfiguration`, none hardcode.

## Phase 1 documentation cleanup performed
Removed 9 duplicate/superseded audit docs (untracked, generated earlier this session):
`PRODUCTION_AUDIT.md`, `HTTPS_CONFIGURATION.md`, `DOMAIN_READINESS.md`, `SYNCFUSION_CONFIGURATION.md`, `SUPABASE_REVIEW.md`, `AWS_FUTURE_ARCHITECTURE.md`, `SECRETS_INVENTORY.md`, `DIAGNOSTICS_SECURITY.md`, `ELASTIC_BEANSTALK_REVIEW.md`.

**Kept (canonical):** `PRODUCTION_READINESS_REPORT.md`, `HTTPS_SETUP.md`, `JWT_CONFIGURATION.md`, `SYNCFUSION_SETUP.md`, `AWS_SECRETS_MANAGER_PLAN.md`, `DOMAIN_AUDIT.md` + the new phase docs (this file, `SECRETS_MANAGER_MIGRATION.md`, `ELASTIC_BEANSTALK_DEPLOY_CHECKLIST.md`, `PVGT_TLS_IMPLEMENTATION.md`, `MOBILE_API_VALIDATION.md`, `BUILD_VERIFICATION.md`, `GO_LIVE_REPORT.md`).

**Preserved (NOT deleted — tracked/committed, separate concern):** `AWS_DEPLOYMENT.md`, `MIGRATION_GUIDE.md`, `TERRAFORM_EXECUTION_ORDER.md`, `ROLLBACK_PLAN.md`, `DOTNET8_COMPATIBILITY.md` — these document the unapplied Terraform/RDS migration, not the EB audit. They contain the stale `.NET 10` / `app.yourdomain.com` text (F7). **Confirm if you want these removed/updated too** — I did not delete committed work without sign-off. References to the deleted audit docs were updated in `AppConfig.cs` and `PRODUCTION_READINESS_REPORT.md`.
