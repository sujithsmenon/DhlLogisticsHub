# AWS Secrets Manager Plan (Task 8 — PLAN ONLY, do not deploy)

**Date:** 2026-06-10 · Region: `ap-south-1` · Env: `DhlLogisticsHub-prod` (EB Windows/IIS)

This is an implementation plan. **Nothing here is provisioned or deployed.** It describes how to move the four production secrets off plaintext EB environment variables into AWS Secrets Manager.

## Secrets in scope

| Secret | Config key | Today | Risk |
|--------|-----------|-------|------|
| Syncfusion license key | `Syncfusion:LicenseKey` | Committed in `appsettings.json:19` | HIGH — key in git |
| JWT signing key | `Jwt:Key` | Placeholder in `appsettings.json:22` | CRITICAL — forgeable tokens |
| Email (IMAP) password | `EmailSettings:Password` | Placeholder in `appsettings.json:10` | MEDIUM |
| DB connection string | `ConnectionStrings:DefaultConnection` | EB env var (Supabase) | MEDIUM — plaintext in EB config |

## Proposed secret layout (mirrors `infra/terraform`)

```
dhl-logistics-prod/db-url        →  plain string: the full Npgsql connection string
dhl-logistics-prod/app           →  JSON:
   {
     "Jwt__Key":                  "<32+ byte random>",
     "Syncfusion__LicenseKey":    "<license key>",
     "EmailSettings__Username":   "<inbox@…>",
     "EmailSettings__Password":   "<app password>",
     "Seed__AdminPassword":       "<strong initial admin pw>",
     "WebPush__PublicKey":        "<vapid public>",
     "WebPush__PrivateKey":       "<vapid private>"
   }
```

Two secrets (not one) so the DB URL can be rotated independently and granted to migration tooling separately.

## Delivery options (pick one)

### Option 1 — `.ebextensions` fetch → environment variables (simplest on EB Windows)
A deploy hook (PowerShell) reads the secrets at instance launch and writes them as process/system environment variables that ANCM passes to the app. The app keeps reading `IConfiguration` exactly as now (`Jwt__Key`, `Syncfusion__LicenseKey`, …) — **no app code change**.
- Pro: zero code change; secrets never in EB console plaintext.
- Con: secret values briefly materialize as env vars on the box; rotation needs an instance refresh.
- IAM: instance profile needs `secretsmanager:GetSecretValue` on the two secret ARNs only.

### Option 2 — In-app Secrets Manager configuration provider (cleanest, small code change)
Add the AWS Secrets Manager SDK and register a configuration source at startup that loads `dhl-logistics-prod/app` (JSON) and `dhl-logistics-prod/db-url` into `IConfiguration` **before** `builder.Build()`:
```csharp
// Pseudocode — not implemented
if (builder.Environment.IsProduction())
{
    var sm = new AmazonSecretsManagerClient(RegionEndpoint.APSouth1);
    builder.Configuration.AddSecretsManagerJson(sm, "dhl-logistics-prod/app");      // helper to flatten JSON → config
    builder.Configuration["ConnectionStrings:DefaultConnection"] =
        await sm.GetPlainSecretAsync("dhl-logistics-prod/db-url");
}
```
- Pro: secrets live only in process memory; supports rotation on restart; no `.ebextensions`.
- Con: small code change + `AWSSDK.SecretsManager` dependency (`AWSSDK.S3` is already referenced).
- IAM: same `GetSecretValue` grant on the instance profile.

> Recommended: **Option 1 first** (no code change, immediate hardening), migrate to **Option 2** when convenient.

## Rotation
- **JWT** — rotating invalidates all live tokens (forces mobile re-login). Schedule deliberately.
- **DB / Email / Syncfusion** — rotate via Secrets Manager; Option 1 needs an instance refresh, Option 2 an app restart to pick up new values (or add a periodic reload).

## Pre-work before adopting
1. Generate a strong `Jwt__Key` and a strong `Seed__AdminPassword` (do NOT reuse the placeholders).
2. Create the two secrets in `ap-south-1`.
3. Attach the `GetSecretValue` policy (scoped to the two ARNs) to the EB instance profile.
4. Blank `Syncfusion:LicenseKey` and `Jwt:Key` in `appsettings.json` commits once the secret path is live.

## Cost
Secrets Manager ≈ \$0.40/secret/month + \$0.05 per 10k API calls → ~\$1/month for two secrets. Negligible.

## Out of scope (this pass)
No secrets created, no IAM changed, no `.ebextensions` added, no SDK wired. Implement when ready.
