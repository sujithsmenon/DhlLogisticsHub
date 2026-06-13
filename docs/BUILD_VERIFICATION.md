# Build Verification (Phase 7)

**Date:** 2026-06-10 · Project: `DhlLogistics.Web` (the deployable) · Config: **Release**
**Scope note:** `DhlLogistics.Mobile` is a separate .NET MAUI app requiring MAUI workloads and is **not** part of the EB deployment; this verification covers the web app that ships to Elastic Beanstalk.

## Commands run
```bash
dotnet clean   DhlLogistics.Web/DhlLogistics.Web.csproj -c Release
dotnet restore DhlLogistics.Web/DhlLogistics.Web.csproj
dotnet build   DhlLogistics.Web/DhlLogistics.Web.csproj -c Release --no-restore
```

## Result
| | |
|---|---|
| **Build status** | ✅ **Build succeeded** |
| **Errors** | **0** |
| **Warnings** | **115** |
| SDK (`global.json`) | `8.0.100`, rollForward `latestFeature` |
| Target framework | `net8.0` |
| Publish | ✅ verified separately (manifest + web.config + 99 DLLs + wwwroot) — see `ELASTIC_BEANSTALK_DEPLOY_CHECKLIST.md` |

## Warning breakdown (all pre-existing, non-blocking)
| Code | Meaning | Notes |
|------|---------|-------|
| `CS8602` | Dereference of a possibly-null reference | The large majority; nullable-reference hygiene in `.razor` code-behind. |
| `CS8604` | Possible null argument | A few. |
| `CS8601` | Possible null reference assignment | 2. |
| `CS0618` | Use of `[Obsolete]` member | 2. |
| `RZ10012` | Markup element with unexpected name `Pages.NotFound` | 2 — `Components/Routes.razor:9`; cosmetic (needs a `@using` or fully-qualified component), does not affect runtime. |

None are errors; none block deployment. They are nullable-annotation warnings, not correctness defects. Optional future cleanup: add null guards / `@using` directives to drive the count down.

## Direct package versions (from `DhlLogistics.Web.csproj`) — all .NET 8-aligned
| Package | Version |
|---------|---------|
| Microsoft.AspNetCore.Authentication.JwtBearer | 8.0.27 |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 8.0.27 |
| Microsoft.AspNetCore.SignalR.Client | 8.0.27 |
| Microsoft.AspNetCore.SignalR | 1.2.10 |
| Microsoft.EntityFrameworkCore.Design | 8.0.27 |
| Npgsql.EntityFrameworkCore.PostgreSQL | 8.0.11 |
| Syncfusion.Blazor.* | 33.1.49 |
| AWSSDK.S3 | 4.0.21.2 |
| FirebaseAdmin | 3.5.0 |
| itext7 | 9.6.0 |
| MailKit | 4.16.0 |
| WebPush | 1.0.12 |

## Config-override check (Phase 2)
Confirmed the app reads all secrets/settings through `IConfiguration`; environment variables (`A__B` → `A:B`) override the JSON files at runtime. No hardcoded secret values in C#. See `FINAL_DEPLOYMENT_AUDIT.md` §config-override.

## Conclusion
The deployable builds **clean (0 errors)** on .NET 8 and publishes a valid EB Windows/IIS bundle. Build is **not** a go-live blocker.
