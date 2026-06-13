# Elastic Beanstalk Deploy Checklist (Phase 4)

**Date:** 2026-06-10 · Env: `DhlLogisticsHub-prod` · platform *IIS 10.0 / Windows Server Core 2025* · ap-south-1

## Verified artifacts

### `aws-windows-deployment-manifest.json` ✅
```json
{ "manifestVersion": 1, "deployments": { "aspNetCoreWeb": [ {
  "name": "dhl-logistics-web",
  "parameters": { "appBundle": ".", "iisPath": "/", "iisWebSite": "Default Web Site" } } ] } }
```
- Correct for the Windows/IIS platform. Required at the **bundle root** or EB enact fails with `Could not find file 'C:\staging\manifest.xml'`.
- Confirmed copied into publish output (csproj `Content Update … CopyToPublishDirectory=Always`).

### `.elasticbeanstalk/config.yml` ✅ (one cosmetic note)
```yaml
deploy:
  artifact: deploy.zip          # ✅ REQUIRED — deploys the prebuilt zip, not git source
global:
  application_name: DhlLogisticsHub
  default_platform: IIS 10.0 running on 64bit Windows Server Core 2025
  default_region: ap-south-1
  sc: git                       # cosmetic leftover; harmless because `artifact` overrides it
```
- `deploy.artifact: deploy.zip` is present and correct.
- ⚠️ This file + the manifest are **untracked** (and `.gitignore` ignores `.elasticbeanstalk/*`). Deploy config lives only on the dev machine — recommend committing the manifest and a deploy script.

### `DhlLogistics.Web.csproj` ✅ .NET 8
- `TargetFramework=net8.0`; `global.json` SDK `8.0.100`. No `.NET 10` anywhere in source.
- Build blockers fixed earlier this audit: `Content Update` (NETSDK1022) + `DefaultItemExcludes` for `publish\**;eb_bundle\**` (BLAZOR106).

## Publish output verification (actually published & inspected this pass)
`dotnet publish -c Release` bundle root contained:

| Required | Present? |
|----------|----------|
| `aws-windows-deployment-manifest.json` | ✅ |
| `web.config` | ✅ (`processPath="dotnet"`, `hostingModel="inprocess"`, AspNetCoreModuleV2) |
| `DhlLogistics.Web.dll` (+ **99** DLLs total) | ✅ |
| `DhlLogistics.Web.exe` (apphost) | ✅ |
| `wwwroot/` (static assets) | ✅ |
| `appsettings.json` / `appsettings.Production.json` | ✅ |

> Note: the publish is **framework-dependent** — it needs the .NET 8 ASP.NET Core runtime + ANCMv2 on the EB host. The env is Green, so it's present. For zero runtime risk, publish self-contained (`-r win-x64 --self-contained true`) → `web.config` becomes `processPath=".\DhlLogistics.Web.exe"`.

## Deploy procedure
```text
1. dotnet publish DhlLogistics.Web -c Release -o publish
2. Zip the CONTENTS of publish/ (manifest + web.config at the zip ROOT) → deploy.zip
3. eb deploy
4. Confirm the log says "Uploading deploy.zip"  (NOT "Creating application version archive")
5. Wait for environment Health = Green
```

## Pre-deploy checklist
- [ ] `Jwt__Key` set as EB env var (**blocker** — else token forgery).
- [ ] `Seed__AdminPassword` set as EB env var.
- [ ] `ConnectionStrings__DefaultConnection` set (Supabase) — already present.
- [ ] `Syncfusion__LicenseKey` set (or committed fallback accepted).
- [ ] **`Security__RequireHttps`** — ⚠️ `appsettings.Production.json` ships `true`. If TLS is **not** yet live, set EB env `Security__RequireHttps=false` to override, else the site 307-loops. Flip to `true` only after TLS (see `PVGT_TLS_IMPLEMENTATION.md`).
- [ ] `deploy.zip` rebuilt from a fresh publish (manifest + DLLs + web.config at root).
- [ ] Health check path = `/health`.

## Post-deploy verification
```text
GET /health        → 200 "OK"
GET /api/ping      → 200 { ok:true, at:... }
GET /diag          → 404 (dev-only; confirms prod hardening)
Login + a master grid + a Syncfusion grid render (no trial banner if key set)
```
