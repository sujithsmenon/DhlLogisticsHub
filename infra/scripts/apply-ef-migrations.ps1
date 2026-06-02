<#
.SYNOPSIS
    Apply pending EF Core migrations to the AWS RDS Postgres instance.

.DESCRIPTION
    Reads the connection string from AWS Secrets Manager
    (dhl-logistics-prod/db-url), then runs `dotnet ef database update`
    against it.

    Requires:
      - AWS CLI configured (`aws configure` or environment creds)
      - dotnet ef tools installed: `dotnet tool install -g dotnet-ef`
      - Same network access caveat as migrate-supabase-to-rds.ps1 —
        RDS is in a private subnet by default. Use SSM port forwarding
        or run from within the VPC.
#>
[CmdletBinding()]
param(
    [string] $SecretId    = "dhl-logistics-prod/db-url",
    [string] $WebProject  = "DhlLogistics.Web",
    [string] $Region      = "ap-south-1",
    [switch] $UseLocalhost   # set when port-forwarding via SSM
)

$ErrorActionPreference = 'Stop'

Write-Host "Fetching connection string from Secrets Manager ($SecretId)..."

$secret = aws secretsmanager get-secret-value `
    --region $Region `
    --secret-id $SecretId `
    --query SecretString `
    --output text

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to read secret. Check AWS credentials + permissions." -ForegroundColor Red
    exit $LASTEXITCODE
}

# If you're tunneling, rewrite Host=... to Host=localhost.
if ($UseLocalhost) {
    $secret = ($secret -replace 'Host=[^;]+', 'Host=localhost')
    Write-Host "(Using Host=localhost — assumes SSM port-forward on 5432)" -ForegroundColor Yellow
}

# Pass the connection string in via env var so it doesn't leak to PS history.
$env:ConnectionStrings__DefaultConnection = $secret

Write-Host "Applying EF Core migrations..."
dotnet ef database update `
    --project $WebProject `
    --connection $env:ConnectionStrings__DefaultConnection

$exitCode = $LASTEXITCODE

Remove-Item Env:ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue

if ($exitCode -ne 0) {
    Write-Host "EF migration failed (exit $exitCode)" -ForegroundColor Red
    exit $exitCode
}

Write-Host "✅ Migrations applied." -ForegroundColor Green
