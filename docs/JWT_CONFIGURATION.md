# JWT Configuration (Task 3)

**Date:** 2026-06-10

## Where JWT is used
- **Validation** (incoming tokens): `Program.cs` `AddJwtBearer(...)` — `Encoding.UTF8.GetBytes(jwt["Key"]!)` (`Program.cs:66`).
- **Issuance** (login): `Api/AuthEndpoints.cs:29` — `new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!))`, HMAC-SHA256, 7-day expiry.
- **Config:** `appsettings.json` → `Jwt:Key` / `Jwt:Issuer` / `Jwt:Audience`. Both code paths bind from `IConfiguration` (no hardcoded key in C#). `Jwt__Key` env var overrides the file value.

## Startup validation implemented (`Program.cs`, after `builder.Build()`)

```csharp
var jwtKey = app.Configuration["Jwt:Key"];
var jwtKeyIsPlaceholder = !string.IsNullOrEmpty(jwtKey)
    && jwtKey.StartsWith("CHANGE-THIS", StringComparison.OrdinalIgnoreCase);
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32 || jwtKeyIsPlaceholder)
{
    app.Logger.LogCritical(
        "JWT signing key is missing, shorter than 32 characters, or still the committed placeholder. " +
        "Mobile/API tokens are INSECURE until Jwt__Key is set to a strong secret. The application will continue running.");
}
```

- Logs **Critical** (per spec). **Does not crash** — a misconfigured deploy still boots, and the warning is visible in IIS stdout / EB logs.
- Covers three cases: missing/blank, **length < 32 chars**, and the committed placeholder.

### Why the placeholder check is needed in addition to `length < 32`
The shipped placeholder `CHANGE-THIS-TO-A-32-CHAR-SECRET-KEY!!` is **37 characters** — it passes a length-only test even though it is the single biggest risk. The explicit `CHANGE-THIS…` check catches it. (A length-only check as literally specified would have missed it.)

## Current risk — CRITICAL
`appsettings.json:22` ships `Jwt:Key = "CHANGE-THIS-TO-A-32-CHAR-SECRET-KEY!!"`. Because it is public in the repo, **anyone can forge bearer tokens** for any role (incl. Admin) against the mobile/API surface.

## Required action (manual, AWS)
Set a strong key as an EB environment variable:
```bash
openssl rand -base64 48     # use the output as Jwt__Key
```
EB → Configuration → Software → Environment properties → `Jwt__Key = <generated>`. The env var overrides the file automatically; the Critical log line disappears on next boot.

> Note: rotating `Jwt__Key` invalidates all existing tokens — issued mobile sessions must re-login. Do it during a low-traffic window or coordinate a forced re-auth.

## Issuer / Audience
`Jwt:Issuer = DhlLogisticsHub`, `Jwt:Audience = DhlLogisticsApp` are logical identifiers (not URLs), so the `pvgt.co.in` domain move requires **no** JWT changes. Both are validated (`ValidateIssuer`/`ValidateAudience = true`).
