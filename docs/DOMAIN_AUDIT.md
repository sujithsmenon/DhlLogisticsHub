# Domain Audit (Task 6)

**Date:** 2026-06-10 · Target domain: `pvgt.co.in`

Searched the repository (excluding the `CBM_Project_Reference` sample and build output) for: `elasticbeanstalk.com`, `localhost`, `127.0.0.1`, `pvgtlogistics.com`.

## Summary
- **`pvgtlogistics.com`** — **0 occurrences** in source (old placeholder fully removed; only referenced inside these audit docs).
- **`elasticbeanstalk.com`** — **0 occurrences** in application code (only the external EB CNAME in tooling).
- All `localhost` / `127.0.0.1` hits are **development-only, build/migration tooling, or unapplied infra** — none in a production code path.

## Findings (file · line · recommendation)

| File | Line | Match | Context | Recommendation |
|------|------|-------|---------|----------------|
| `DhlLogistics.Web/Properties/launchSettings.json` | 8 | `http://localhost:5200` | Dev `http` launch profile | ✅ Keep — local dev only, not deployed. |
| `DhlLogistics.Web/Properties/launchSettings.json` | 17 | `https://localhost:7010;http://localhost:5200` | Dev `https` launch profile | ✅ Keep — local dev only. |
| `DhlLogistics.Mobile/AppConfig.cs` | 12 | `…onrender.com` | **Comment** noting the old endpoint | ✅ Historical note; live value is now `https://pvgt.co.in` (line 18). Optional: delete the comment. |
| `DhlLogistics.Mobile/AppConfig.cs` | 15–16 | `10.0.2.2 / localhost` | Dev-override comments | ✅ Keep — documents local dev base URLs. |
| `infra/terraform/security.tf` | 84 | `127.0.0.1/32` | Terraform SG rule | ✅ Unapplied Terraform (ECS path), not used by the EB deploy. No action for current prod. |
| `infra/scripts/apply-ef-migrations.ps1` | 22,40–43 | `localhost` | SSM port-forward tunneling for RDS migration | ✅ Keep — operational script for the future RDS path. |
| `infra/scripts/migrate-supabase-to-rds.ps1` | 116 | `localhost` | RDS migration helper | ✅ Keep — future migration tooling. |
| `Dockerfile` | 71 | `http://localhost:8080/api/ping` | **Commented** healthcheck (non-chiseled path) | ✅ Keep — container path, not the EB/IIS deploy. |
| `docs/*.md` | various | `localhost`, `pvgtlogistics.com` | Documentation references | ✅ Expected — docs describing tunnels/history. |

## Application-code verdict
**No hardcoded production hostnames remain in the web application.** Blazor navigation is relative (`NavManager.NavigateTo("/...")`), Identity cookie paths are relative, and JWT issuer/audience are logical strings — so the app is host-agnostic and serves correctly from `pvgt.co.in`.

The only domain-coupled production value was the **mobile API base URL**, already corrected: `DhlLogistics.Mobile/AppConfig.cs:18` → `https://pvgt.co.in` (was the decommissioned Render URL). It requires TLS on `pvgt.co.in` to function (see `HTTPS_SETUP.md`).

## No action required for go-live
None of the remaining matches block production. The only domain dependency is **TLS on `pvgt.co.in`**, tracked separately in `HTTPS_SETUP.md` and `PRODUCTION_READINESS_REPORT.md`.
