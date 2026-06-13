# Production Readiness Report (Task 9)

**Date:** 2026-06-10 · **Branch:** master · **Env:** `DhlLogisticsHub-prod` (EB Windows/IIS, ap-south-1) · **Domain:** `pvgt.co.in`
**Not committed. Not pushed. Not deployed.**

---

## 1. Security score: **74 / 100**

| Dimension | Score | Notes |
|-----------|------:|-------|
| Build & deploy integrity | 18/20 | Builds clean; deploy config still untracked (−2). |
| App correctness / domain | 18/20 | Host-agnostic; mobile URL fixed; FCM no-ops (−2). |
| Transport security (TLS) | 7/20 | HSTS wired + redirect gated; **no cert on `pvgt.co.in` yet**. |
| Secrets hygiene | 15/25 | Validation added; **JWT placeholder + Syncfusion-in-repo remain**. |
| Diagnostics / observability | 16/15→16 | Diag endpoints dev-only; logger-based validation; health probes. |

Gated by two owner-only blockers (JWT key, TLS). Clearing both → ~90.

## 2. Build status
`dotnet build DhlLogistics.Web -c Release` → **Build succeeded, 0 errors**, 115 pre-existing nullable warnings. Two latent blockers were fixed earlier this audit (NETSDK1022 duplicate manifest item; BLAZOR106 stale `publish/` folder), so `dotnet publish` is now reproducible.

## 3. Deployment status
**Live and Green** on EB Windows/IIS over **HTTP only**. Framework-dependent in-process publish (`web.config` → `processPath="dotnet"`), Supabase Postgres via env-injected connection string. Deploy via `eb deploy` using `deploy.artifact: deploy.zip`. Mechanism unchanged by this pass. (Deploy config `.elasticbeanstalk/config.yml` + manifest are untracked — see `ELASTIC_BEANSTALK_DEPLOY_CHECKLIST.md`.)

## 4. Remaining blockers
1. **JWT key** — `appsettings.json:22` is the committed placeholder → token forgery. **Set `Jwt__Key`.** (App now logs Critical until fixed.)
2. **No TLS** on `pvgt.co.in` (A→bare EIP). **Add ACM+ALB or IIS cert**, then `Security__RequireHttps=true`.
3. **Seed admin** — set `Seed__AdminPassword`; reset admin if `Admin@1234` was ever live.
4. **Syncfusion key** in tracked `appsettings.json:19` — decision: env var vs committed fallback.

Non-blocking (handled in code): diag endpoints (dev-only), mobile URL, HTTPS plumbing, build.

## 5. Required Elastic Beanstalk environment variables
| Key | Value | When |
|-----|-------|------|
| `ConnectionStrings__DefaultConnection` | Supabase pooler conn string | already set ✓ |
| `Jwt__Key` | 32+ byte random (`openssl rand -base64 48`) | **now — blocker** |
| `Seed__AdminPassword` | strong initial admin password | **now — blocker** |
| `Security__RequireHttps` | `true` | **after TLS is live** |
| `Syncfusion__LicenseKey` | license key | if not using committed fallback |
| `EmailSettings__Username` / `EmailSettings__Password` | IMAP creds | if email polling used |
| `WebPush__PublicKey` / `WebPush__PrivateKey` | VAPID keys | if web push used |
| `ASPNETCORE_ENVIRONMENT` | `Production` | EB default — leave as-is |

Set under EB → Configuration → Software → Environment properties (or `eb setenv`). Env restarts after save.

## 6. Required Route53 actions
- **If adding an ALB (Path A):** replace the `pvgt.co.in` **A → 13.234.76.3** record with an **A/Alias → ALB** record. Add `www.pvgt.co.in` likewise if wanted.
- **If terminating TLS in IIS (Path B):** leave the A→EIP record as-is.
- **ACM DNS validation:** add the CNAME records ACM provides (one-click "Create records in Route53" from the ACM console) into the existing `pvgt.co.in` hosted zone.

## 7. Required ACM actions
1. ACM → **Request certificate** → public, region **ap-south-1** (must match the ALB region).
2. Domains: `pvgt.co.in` **and** `www.pvgt.co.in`.
3. Validation method **DNS** → "Create records in Route53".
4. Wait for **Issued**. *(Skip entirely if using Let's Encrypt/win-acme inside IIS.)*

## 8. Required AWS actions (summary)
1. **ACM** cert for the domain (§7).
2. **EB** → load-balanced env + HTTPS:443 listener with the cert + HTTP→HTTPS redirect (Path A); open 443 in the SG.
3. **Route53** repoint to ALB (§6).
4. **EB env vars** — `Jwt__Key`, `Seed__AdminPassword`, then `Security__RequireHttps=true` (§5).
5. *(Later, optional)* Secrets Manager migration — see `AWS_SECRETS_MANAGER_PLAN.md`.

---

## Files modified (5)
| File | Change |
|------|--------|
| `DhlLogistics.Web/Program.cs` | HTTPS: `UseHsts()` in non-Dev + `UseHttpsRedirection()` gated on `Security:RequireHttps`. Syncfusion + JWT validation moved post-build to use `app.Logger` (`LogInformation`/`LogWarning`/`LogCritical`). Diag endpoints already dev-only. |
| `DhlLogistics.Web/appsettings.json` | `Security.RequireHttps=false` block. |
| `DhlLogistics.Web/DhlLogistics.Web.csproj` | Build fixes: `Content Update` (NETSDK1022); `DefaultItemExcludes` for `publish\**;eb_bundle\**` (BLAZOR106). |
| `DhlLogistics.Mobile/AppConfig.cs` | `ApiBaseUrl` → `https://pvgt.co.in`. |
| `.gitignore` | EB ignore rules (from prior session). |

## Files created (canonical doc set, after consolidation)
- `DhlLogistics.Web/appsettings.Production.json`, `DhlLogistics.Web/appsettings.Template.json`
- `docs/` kept: `HTTPS_SETUP.md`, `SYNCFUSION_SETUP.md`, `JWT_CONFIGURATION.md`, `DOMAIN_AUDIT.md`, `AWS_SECRETS_MANAGER_PLAN.md`, `PRODUCTION_READINESS_REPORT.md` (this file)
- `docs/` added: `FINAL_DEPLOYMENT_AUDIT.md`, `SECRETS_MANAGER_MIGRATION.md`, `ELASTIC_BEANSTALK_DEPLOY_CHECKLIST.md`, `PVGT_TLS_IMPLEMENTATION.md`, `MOBILE_API_VALIDATION.md`, `BUILD_VERIFICATION.md`, `GO_LIVE_REPORT.md`
- *Removed 9 duplicate predecessors* (`PRODUCTION_AUDIT`, `HTTPS_CONFIGURATION`, `DOMAIN_READINESS`, `SYNCFUSION_CONFIGURATION`, `SUPABASE_REVIEW`, `AWS_FUTURE_ARCHITECTURE`, `SECRETS_INVENTORY`, `DIAGNOSTICS_SECURITY`, `ELASTIC_BEANSTALK_REVIEW`). Note: `appsettings.Production.json` now ships `Security:RequireHttps=true` (was false) — see safety caveat in `HTTPS_SETUP.md`.

## Not done (by instruction)
No commit, no push, no `eb deploy`. Syncfusion key left in place (working config). No Secrets Manager / ACM / Route53 / EB changes performed — those are the manual AWS steps above.
