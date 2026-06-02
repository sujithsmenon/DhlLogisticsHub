<#
.SYNOPSIS
    Migrate the existing Supabase Postgres database to AWS RDS Postgres.

.DESCRIPTION
    Two-step process:
      1. pg_dump from Supabase (data + schema, --no-owner --no-acl so it
         restores cleanly under a different RDS user).
      2. pg_restore (or psql, if SQL format) into the empty RDS database.

    Designed to run from a Windows dev box with PostgreSQL client tools
    installed. The PostgreSQL 16 installer puts pg_dump/pg_restore in
    "C:\Program Files\PostgreSQL\16\bin" — add it to PATH first.

    The RDS endpoint isn't publicly accessible (private subnet), so this
    script REQUIRES one of:
      - A bastion EC2 in the public subnet + SSH tunnel
      - AWS SSM Session Manager port forward to the RDS endpoint
      - VPN / Direct Connect into the VPC
    The simplest is SSM port forwarding — instructions printed at the end.

.PARAMETER SupabaseConn
    Source Postgres connection string (Supabase format).

.PARAMETER RdsHost
    Target RDS endpoint hostname (no port). Get from `terraform output db_endpoint`.

.PARAMETER RdsDb
    Target RDS database name (default: dhllogistics — matches variables.tf default).

.PARAMETER RdsUser
    Target RDS master username (default: dhladmin).

.PARAMETER RdsPassword
    Target RDS master password. Read from AWS Secrets Manager — see usage below.

.EXAMPLE
    # Pull the RDS password out of Secrets Manager first
    $secret = aws secretsmanager get-secret-value `
        --secret-id dhl-logistics-prod/db-url --query SecretString --output text
    # Extract Password=...; from the connection string
    if ($secret -match 'Password=([^;]+);') { $pw = $matches[1] }

    .\migrate-supabase-to-rds.ps1 `
        -SupabaseConn "postgresql://postgres.ABC:PWD@aws-0-ap-south-1.pooler.supabase.com:6543/postgres" `
        -RdsHost "dhl-logistics-prod-pg.xxxxxxxx.ap-south-1.rds.amazonaws.com" `
        -RdsPassword $pw
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $SupabaseConn,
    [Parameter(Mandatory = $true)] [string] $RdsHost,
    [int]    $RdsPort     = 5432,
    [string] $RdsDb       = "dhllogistics",
    [string] $RdsUser     = "dhladmin",
    [Parameter(Mandatory = $true)] [string] $RdsPassword,
    [string] $DumpFile    = "supabase-dump.sql"
)

$ErrorActionPreference = 'Stop'

# Verify pg_dump is on PATH.
$pgDump    = (Get-Command pg_dump    -ErrorAction SilentlyContinue)?.Source
$pgRestore = (Get-Command pg_restore -ErrorAction SilentlyContinue)?.Source
$psql      = (Get-Command psql       -ErrorAction SilentlyContinue)?.Source
if (-not $pgDump -or -not $psql) {
    Write-Host "PostgreSQL client tools not on PATH." -ForegroundColor Red
    Write-Host "Install Postgres 16 client, then add 'C:\Program Files\PostgreSQL\16\bin' to PATH."
    exit 1
}

Write-Host "============================================================"
Write-Host "1/3  Dumping Supabase schema + data to $DumpFile..."
Write-Host "============================================================"

# Plain-text SQL dump. --no-owner / --no-acl strip Supabase-specific role
# grants so the dump replays cleanly into RDS under a different user.
# --schema=public limits to your app data; Supabase has internal schemas
# (auth, storage, ...) we don't want.
& $pgDump `
    --dbname=$SupabaseConn `
    --no-owner `
    --no-acl `
    --schema=public `
    --format=plain `
    --file=$DumpFile

if ($LASTEXITCODE -ne 0) {
    Write-Host "pg_dump failed (exit $LASTEXITCODE)" -ForegroundColor Red
    exit $LASTEXITCODE
}

$sizeMb = [math]::Round((Get-Item $DumpFile).Length / 1MB, 2)
Write-Host "Dump complete — $sizeMb MB" -ForegroundColor Green

Write-Host ""
Write-Host "============================================================"
Write-Host "2/3  Verifying RDS connectivity..."
Write-Host "============================================================"

$env:PGPASSWORD = $RdsPassword
& $psql -h $RdsHost -p $RdsPort -U $RdsUser -d $RdsDb -c "SELECT version();"
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Cannot reach RDS at $RdsHost." -ForegroundColor Yellow
    Write-Host "If the RDS instance is in a private subnet (default), open an SSM"
    Write-Host "port-forward in another terminal first:"
    Write-Host ""
    Write-Host "  aws ssm start-session ``"
    Write-Host "    --target <id-of-an-ECS-or-bastion-host-in-the-VPC> ``"
    Write-Host "    --document-name AWS-StartPortForwardingSessionToRemoteHost ``"
    Write-Host "    --parameters host=$RdsHost,portNumber=5432,localPortNumber=5432"
    Write-Host ""
    Write-Host "Then rerun this script with -RdsHost localhost."
    exit 1
}

Write-Host "============================================================"
Write-Host "3/3  Restoring into RDS..."
Write-Host "============================================================"

& $psql `
    -h $RdsHost `
    -p $RdsPort `
    -U $RdsUser `
    -d $RdsDb `
    -v ON_ERROR_STOP=1 `
    --single-transaction `
    -f $DumpFile

if ($LASTEXITCODE -ne 0) {
    Write-Host "Restore failed (exit $LASTEXITCODE)" -ForegroundColor Red
    Write-Host "The transaction was rolled back. Inspect output above, fix, retry."
    exit $LASTEXITCODE
}

Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "✅ Migration complete." -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Run dotnet ef migrations to ensure schema matches the latest:"
Write-Host "     .\infra\scripts\apply-ef-migrations.ps1"
Write-Host "  2. Spot-check a few tables via psql or pgAdmin."
Write-Host "  3. Trigger a redeploy so the app re-reads the new ConnectionString."
