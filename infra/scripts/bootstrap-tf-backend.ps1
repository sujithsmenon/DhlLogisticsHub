<#
.SYNOPSIS
    One-off: create the S3 bucket + DynamoDB table that Terraform uses for
    remote state and state-locking.

.DESCRIPTION
    Run this ONCE before the first `terraform apply`. Afterwards uncomment
    the `terraform { backend "s3" { ... } }` block in backend.tf and run
    `terraform init -migrate-state` to move local state to S3.
#>
[CmdletBinding()]
param(
    [string] $Region       = "ap-south-1",
    [string] $BucketName   = "dhl-logistics-tfstate",
    [string] $LockTable    = "dhl-logistics-tflock"
)

$ErrorActionPreference = 'Stop'

Write-Host "Creating S3 bucket s3://$BucketName ..."
aws s3api create-bucket `
    --bucket $BucketName `
    --region $Region `
    --create-bucket-configuration LocationConstraint=$Region

aws s3api put-bucket-versioning `
    --bucket $BucketName `
    --versioning-configuration Status=Enabled

aws s3api put-bucket-encryption `
    --bucket $BucketName `
    --server-side-encryption-configuration '{"Rules":[{"ApplyServerSideEncryptionByDefault":{"SSEAlgorithm":"AES256"}}]}'

aws s3api put-public-access-block `
    --bucket $BucketName `
    --public-access-block-configuration BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true

Write-Host "Creating DynamoDB lock table $LockTable ..."
aws dynamodb create-table `
    --region $Region `
    --table-name $LockTable `
    --attribute-definitions AttributeName=LockID,AttributeType=S `
    --key-schema AttributeName=LockID,KeyType=HASH `
    --billing-mode PAY_PER_REQUEST

aws dynamodb wait table-exists --region $Region --table-name $LockTable

Write-Host ""
Write-Host "✅ Backend bootstrap complete." -ForegroundColor Green
Write-Host ""
Write-Host "Next:"
Write-Host "  1. Uncomment the 'terraform { backend ... }' block in infra/terraform/backend.tf"
Write-Host "  2. cd infra/terraform"
Write-Host "  3. terraform init -migrate-state"
