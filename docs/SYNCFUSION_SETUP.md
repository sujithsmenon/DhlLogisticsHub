# Syncfusion Setup (Task 2)

**Date:** 2026-06-10 · Packages: `Syncfusion.Blazor.* 33.1.49` (license must be valid for the **v33 / 2025** release train).

## Implemented in `Program.cs` (after `builder.Build()`, so a DI `ILogger` is available)

```csharp
var syncfusionKey = app.Configuration["Syncfusion:LicenseKey"];
if (!string.IsNullOrWhiteSpace(syncfusionKey))
{
    Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(syncfusionKey);
    app.Logger.LogInformation("Syncfusion license registered.");
}
else
{
    app.Logger.LogWarning("Syncfusion license key missing. Components will render with a trial banner.");
}
```

Registration runs before `app.Run()`, which is all Syncfusion requires — Blazor Server components render on the first request, after the host starts.

## Both key forms are supported automatically

Reading the configuration key **`Syncfusion:LicenseKey`** transparently resolves **both**:

| Source | Form | Notes |
|--------|------|-------|
| `appsettings.json` / `appsettings.Production.json` | `"Syncfusion": { "LicenseKey": "..." }` → key `Syncfusion:LicenseKey` | committed fallback |
| Environment variable (EB / Secrets Manager) | `Syncfusion__LicenseKey` | ASP.NET Core maps `__` → `:`, so it lands on the same config key |

No extra code is needed for the `__` form — it is the standard environment-variable encoding of a `:` config path. The env var (if set) **overrides** the file value because environment variables are a higher-precedence configuration provider.

## Behavior
- **Key present** → `RegisterLicense(...)` + `LogInformation("Syncfusion license registered.")`.
- **Key missing/blank** → `LogWarning("Syncfusion license key missing…")` and the app **continues** (trial banner only). Never crashes.

Logs go to the IIS `stdout` log / EB logs, so you can confirm registration on each boot.

## Current state & recommendation
- The real key is currently committed in `appsettings.json:19` (a tracked file). It works, but a committed license key is a leak (Syncfusion ToS).
- **Recommended:** blank `Syncfusion:LicenseKey` in commits and inject `Syncfusion__LicenseKey` as an EB env var (ideally from Secrets Manager — see `AWS_SECRETS_MANAGER_PLAN.md`). The resolution above still finds it at runtime.
- If you keep the committed fallback for convenience, treat the key as semi-public and rotate from the Syncfusion portal if needed.
