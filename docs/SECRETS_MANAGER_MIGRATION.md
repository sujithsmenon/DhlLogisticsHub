# AWS Secrets Manager Migration — Exact Commands

**Date:** 2026-06-10 · Region: `ap-south-1` · Env: `DhlLogisticsHub-prod`

Migrates the four production secrets off plaintext into AWS Secrets Manager. **Plan + commands only — nothing here is executed.** Strategy/rationale is in `AWS_SECRETS_MANAGER_PLAN.md`.

## Secrets in scope
| Env-var key | Config key | Today |
|-------------|-----------|-------|
| `Jwt__Key` | `Jwt:Key` | placeholder in `appsettings.json:22` |
| `Seed__AdminPassword` | `Seed:AdminPassword` | `Admin@1234` fallback (`Program.cs:228`) |
| `Syncfusion__LicenseKey` | `Syncfusion:LicenseKey` | committed in `appsettings.json:19` |
| `ConnectionStrings__DefaultConnection` | `ConnectionStrings:DefaultConnection` | EB env (Supabase) |

## Step 0 — generate strong values (don't reuse placeholders)
```bash
# JWT key (32+ bytes):
openssl rand -base64 48
# Seed admin password (example generator):
openssl rand -base64 18
```

## Step 1 — create the secrets

```bash
# 1a. App secrets as a JSON blob
aws secretsmanager create-secret \
  --name dhl-logistics-prod/app \
  --description "DHL Logistics Hub application secrets" \
  --region ap-south-1 \
  --secret-string '{
    "Jwt__Key":"<PASTE_OPENSSL_RAND_BASE64_48>",
    "Seed__AdminPassword":"<PASTE_STRONG_PASSWORD>",
    "Syncfusion__LicenseKey":"<PASTE_SYNCFUSION_KEY>"
  }'

# 1b. DB connection string as a plain string (rotated independently)
aws secretsmanager create-secret \
  --name dhl-logistics-prod/db-url \
  --description "DHL Logistics Hub DB connection string" \
  --region ap-south-1 \
  --secret-string 'Host=aws-1-ap-northeast-1.pooler.supabase.com;Port=6543;Database=postgres;Username=postgres.<projectref>;Password=<DB_PASSWORD>;SSL Mode=Require;Trust Server Certificate=true'
```

## Step 2 — read back / verify
```bash
aws secretsmanager get-secret-value --secret-id dhl-logistics-prod/app    --region ap-south-1 --query SecretString --output text
aws secretsmanager get-secret-value --secret-id dhl-logistics-prod/db-url --region ap-south-1 --query SecretString --output text
```

## Step 3 — rotate / update a value later
```bash
aws secretsmanager put-secret-value \
  --secret-id dhl-logistics-prod/app \
  --region ap-south-1 \
  --secret-string '{"Jwt__Key":"<NEW>","Seed__AdminPassword":"<NEW>","Syncfusion__LicenseKey":"<NEW>"}'
```
> Rotating `Jwt__Key` invalidates all live mobile/API tokens (forces re-login). Schedule deliberately.

## Step 4 — grant the EB instance profile read access (scoped to these ARNs)
```bash
# Find the EB instance profile role name:
aws elasticbeanstalk describe-configuration-settings \
  --application-name DhlLogisticsHub --environment-name DhlLogisticsHub-prod \
  --region ap-south-1 \
  --query "ConfigurationSettings[0].OptionSettings[?OptionName=='IamInstanceProfile'].Value" --output text

# Attach an inline policy (replace ROLE_NAME and the two ARNs from create-secret output):
aws iam put-role-policy \
  --role-name <ROLE_NAME> \
  --policy-name DhlSecretsRead \
  --policy-document '{
    "Version":"2012-10-17",
    "Statement":[{
      "Effect":"Allow",
      "Action":"secretsmanager:GetSecretValue",
      "Resource":[
        "arn:aws:secretsmanager:ap-south-1:<ACCOUNT_ID>:secret:dhl-logistics-prod/app-*",
        "arn:aws:secretsmanager:ap-south-1:<ACCOUNT_ID>:secret:dhl-logistics-prod/db-url-*"
      ]
    }]
  }'
```

## Step 5 — wire into the app (choose one; see AWS_SECRETS_MANAGER_PLAN.md)
- **Option 1 (no code change):** `.ebextensions` PowerShell hook fetches the secrets at launch and sets them as environment variables (`Jwt__Key`, etc.). The app keeps reading `IConfiguration` unchanged.
- **Option 2 (small code change):** add a Secrets Manager configuration provider in `Program.cs` before `builder.Build()`.

## Step 6 — remove plaintext fallbacks from commits
After secrets resolve from Secrets Manager:
- Blank `Jwt:Key` and `Syncfusion:LicenseKey` in `appsettings.json`.
- Confirm `ConnectionStrings:DefaultConnection` stays empty in `appsettings.json`.

## Cost
~\$0.40/secret/month + \$0.05/10k API calls → ~\$1/month for both secrets.
