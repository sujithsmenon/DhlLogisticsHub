# .NET 8 Compatibility Report — DhlLogisticsHub

Date: 2026-06-03 • Target: **.NET 8 (LTS)** • Prior: .NET 10

This report covers Task A (project analysis) of the AWS migration. It documents
the projects, SDK/runtime versions, the .NET 9/10 → 8 changes that were applied,
and the residual items.

---

## 1. Projects & target frameworks

| Project | Type | TargetFramework(s) | Status |
|---|---|---|---|
| `DhlLogistics.Web` | ASP.NET Core Blazor Server (`Microsoft.NET.Sdk.Web`) | `net8.0` | ✅ builds, 0 errors |
| `DhlLogistics.Shared` | Class library (`Microsoft.NET.Sdk`) | `net8.0` | ✅ builds |
| `DhlLogistics.Mobile` | MAUI Blazor (`Microsoft.NET.Sdk.Razor`) | `net8.0-android` / `-ios` / `-maccatalyst` | ✅ restores on net8; android compiles once Android SDK present |
| `CBM_Project_Reference/**` | Reference only (separate `CBM.sln`) | unchanged | ⏸️ out of scope — not part of the deployable solution |

The deployed artifact is **`DhlLogistics.Web`** only. The Dockerfile and all AWS
infra target that project; Mobile and the CBM reference are excluded from the
image (see `.dockerignore`).

## 2. SDK / runtime

- **SDK pin:** `global.json` pins the repo to the **8.0 SDK** (`version 8.0.100`,
  `rollForward: latestFeature`) so machines with both 8.0 and 10.0 SDKs resolve to 8.0.
- **Local toolchain verified:** SDK `8.0.419`; ASP.NET Core runtime `8.0.27`.
- **Docker:** build stage `mcr.microsoft.com/dotnet/sdk:8.0`; runtime stage
  `mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled`.

## 3. Package versions (framework-tied → 8.x)

| Package | Version |
|---|---|
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 8.0.27 |
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | 8.0.27 |
| `Microsoft.AspNetCore.SignalR.Client` | 8.0.27 |
| `Microsoft.EntityFrameworkCore.Design` | 8.0.27 |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 8.0.11 |
| `Microsoft.Extensions.Logging.Debug` | 8.0.1 |

Third-party packages already support net8 and were left as-is: Syncfusion.Blazor
33.1.49, AWSSDK.S3, MailKit, itext7, FirebaseAdmin, WebPush, Xamarin.Firebase.Messaging.
`$(MauiVersion)` resolves to the 8.0 MAUI workload packs.

## 4. .NET 9/10-only APIs removed/replaced

These compiled under .NET 10 but do not exist (or behave differently) on .NET 8:

| File | .NET 9/10 construct | .NET 8 replacement | Failure mode if left |
|---|---|---|---|
| `Program.cs` | `app.MapStaticAssets()` | removed; `app.UseStaticFiles()` already present | compile error |
| `Program.cs` | `RelationalEventId.PendingModelChangesWarning` suppression | removed (EF 9+ only) | compile error |
| `Components/Layout/ReconnectModal.razor` | `@Assets["…"]` fingerprint helper | literal asset path | compile error |
| `Components/Routes.razor` (Web + Mobile) | `<Router NotFoundPage="…">` | `<NotFound>` + `<LayoutView>` render fragment | **runtime** `InvalidOperationException` (parameter bound at render time) |

## 5. Build verification

```
dotnet build DhlLogistics.Web/DhlLogistics.Web.csproj -c Debug
→ Build succeeded. 0 Error(s), 115 Warning(s)
```

The 115 warnings are all pre-existing nullable-reference (`CS8602/8604`) and one
obsolete-API (`CS0618`, `GoogleCredential.FromFile`) warning — none introduced by
the downgrade, none blocking.

## 6. Residual / follow-up items

1. **EF migration snapshots** (`Database/Migrations/*.cs`) still carry the
   annotation `ProductVersion "10.0.6"`. Cosmetic only — they compile and run
   under EF 8. The annotation refreshes on the next `dotnet ef migrations add`.
2. **Mobile build prerequisites:** building `net8.0-android` from the CLI needs
   the .NET 8 MAUI workloads (installed) **and** the Android SDK (install via VS
   Installer / Android Studio, or `dotnet build -t:InstallAndroidDependencies
   -p:AcceptAndroidSDKLicenses=True`). iOS/MacCatalyst require a Mac. This is
   unrelated to the server deployment.
3. **Reconnect modal** uses the .NET 9/10 reconnect-UI CSS class names; .NET 8's
   `blazor.web.js` drives an older scheme. Cosmetic — only visible during a
   dropped SignalR circuit. Align later if needed.

## 7. PostgreSQL note

The app already runs on PostgreSQL via Npgsql (EF Core 8 provider 8.0.11). No
SQL-Server-specific code is in the deploy path, so the move to RDS PostgreSQL 16
is a connection-string change, not a code change. See `MIGRATION_GUIDE.md`.
