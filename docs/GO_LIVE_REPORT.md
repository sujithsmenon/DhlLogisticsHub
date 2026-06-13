# Go-Live Report — DHL Logistics Hub

**Date:** 2026-06-10 · **Branch:** master (uncommitted) · **Env:** `DhlLogisticsHub-prod` (EB Windows/IIS, ap-south-1) · **Domain:** `pvgt.co.in`
**Source of truth:** repository working tree. **Not committed / not pushed / not deployed.**

---

## Production readiness score: **73 / 100**

| Dimension | Score | Notes |
|-----------|------:|-------|
| Build & publish integrity | 19/20 | 0 errors; valid EB bundle verified; deploy config untracked (−1). |
| App correctness / domain | 18/20 | Host-agnostic; mobile URL fixed; FCM no-ops (−2). |
| Transport security (TLS) | 7/20 | Code TLS-ready + HSTS; **no cert on `pvgt.co.in`**; Production.json `RequireHttps=true` is a pre-TLS deploy landmine. |
| Secrets hygiene | 14/25 | Validation + Secrets Manager runbook; **JWT placeholder + Syncfusion-in-repo remain**. |
| Diagnostics / observability | 15/15 | Diag endpoints dev-only; logger validation; health probes. |

The score is held down by two items only the account owner can clear (JWT key, TLS).

## Critical blockers (must fix before GO)
1. **JWT signing key is the committed placeholder** (`appsettings.json:22`) → anyone can forge tokens for any role. **Set EB env `Jwt__Key`** to a 32+ byte random secret. App logs Critical until fixed.
2. **No TLS on `pvgt.co.in`** → all traffic (incl. credentials/JWTs) is plaintext, and the mobile app (HTTPS base URL) cannot connect. **Provision ACM + ALB** and flip `Security__RequireHttps`.

## High-priority actions
- **Deploy-safety on `RequireHttps`:** `appsettings.Production.json` ships `true`. If you deploy before TLS, override with EB env **`Security__RequireHttps=false`** until the cert is live, or don't ship that file yet. (Outage risk otherwise.)
- **Set `Seed__AdminPassword`** and reset the admin account if `Admin@1234` was ever live.
- **Syncfusion key** out of the tracked file → EB env `Syncfusion__LicenseKey` (or accept committed fallback knowingly).

## Medium-priority actions
- Commit the EB deploy config (`.elasticbeanstalk/config.yml` + manifest) + a deploy script — currently untracked, single point of failure.
- Migrate secrets to AWS Secrets Manager (`SECRETS_MANAGER_MIGRATION.md`).
- Decide framework-dependent vs self-contained publish (runtime dependency on the EB host).
- Plan JWT-key rotation comms (rotation forces mobile re-login).

## Low-priority actions
- Harden Identity cookie (`SecurePolicy=Always`, `SameSite=Lax`) — apply with the TLS cutover.
- Tighten `AllowedHosts` to `pvgt.co.in;www.pvgt.co.in` once the host header is stable.
- Fix stale `.NET 10` / `app.yourdomain.com` text in `docs/AWS_DEPLOYMENT.md` (scaffold doc).
- Update stale "Render" comments in `MauiProgram.cs` / `ApiService.cs`.
- Resolve 115 nullable warnings (`CS86xx`) and the `RZ10012` in `Routes.razor` over time.
- Firebase credential → Secrets Manager + `GoogleCredential.FromJson` (FCM currently no-ops on AWS).

---

## GO / NO-GO recommendation: **NO-GO** (conditional GO)

**Do not go live as-is.** Two critical/high blockers — the placeholder JWT key and missing TLS — mean an authenticated, internet-facing logistics app would run with forgeable tokens over plaintext.

**Conditional GO** once this short checklist is done (no code changes required — all AWS console / env-var):
1. ☐ `Jwt__Key` set to a strong secret (EB env).
2. ☐ `Seed__AdminPassword` set (EB env); admin reset if needed.
3. ☐ ACM cert issued for `pvgt.co.in` (+`www`) in ap-south-1.
4. ☐ EB load-balanced + HTTPS:443 listener with the cert; HTTP→HTTPS redirect.
5. ☐ Route53 `pvgt.co.in` A-alias → ALB.
6. ☐ `Security__RequireHttps=true` (and confirm the env is NOT enforcing it before steps 3–5).
7. ☐ Validate: `curl -I https://pvgt.co.in` → 200 + HSTS; login + Syncfusion grid + mobile login all work.

Estimated effort: ~1–2 hours of AWS console work; **no code blockers** — the application build is clean and the code is TLS/secret-ready. After this checklist, readiness ≈ 90/100 → **GO**.

## Reference docs
`FINAL_DEPLOYMENT_AUDIT.md` · `HTTPS_SETUP.md` · `PVGT_TLS_IMPLEMENTATION.md` · `JWT_CONFIGURATION.md` · `SYNCFUSION_SETUP.md` · `AWS_SECRETS_MANAGER_PLAN.md` · `SECRETS_MANAGER_MIGRATION.md` · `ELASTIC_BEANSTALK_DEPLOY_CHECKLIST.md` · `MOBILE_API_VALIDATION.md` · `BUILD_VERIFICATION.md` · `DOMAIN_AUDIT.md` · `PRODUCTION_READINESS_REPORT.md`
