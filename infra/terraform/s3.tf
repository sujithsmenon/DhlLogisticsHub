###############################################################################
# S3 bucket for PDF storage (the app's AWSSDK.S3 usage). The container reads the
# bucket name from the Aws__BucketName env var (= var.s3_bucket_name).
#
# Private, encrypted, versioned, ACLs disabled. Created here so the bucket the
# IAM task role + task env reference actually exists (we don't assume it does).
#
# NB: S3 bucket names are GLOBALLY unique across all AWS accounts. If
# `terraform apply` fails with BucketAlreadyExists/BucketAlreadyOwnedByYou,
# set a unique s3_bucket_name in terraform.tfvars and keep the app's
# appsettings 'Aws:BucketName' in sync.
###############################################################################

resource "aws_s3_bucket" "pdfs" {
  bucket = var.s3_bucket_name
  tags   = { Name = "${local.name}-pdfs" }
}

resource "aws_s3_bucket_public_access_block" "pdfs" {
  bucket                  = aws_s3_bucket.pdfs.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_ownership_controls" "pdfs" {
  bucket = aws_s3_bucket.pdfs.id
  rule {
    object_ownership = "BucketOwnerEnforced" # disables ACLs entirely
  }
}

resource "aws_s3_bucket_versioning" "pdfs" {
  bucket = aws_s3_bucket.pdfs.id
  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "pdfs" {
  bucket = aws_s3_bucket.pdfs.id
  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
    bucket_key_enabled = true
  }
}

# Expire old object versions + clean up failed multipart uploads to cap cost.
resource "aws_s3_bucket_lifecycle_configuration" "pdfs" {
  bucket = aws_s3_bucket.pdfs.id

  rule {
    id     = "expire-noncurrent-versions"
    status = "Enabled"

    filter {} # applies to all objects

    noncurrent_version_expiration {
      noncurrent_days = 90
    }

    abort_incomplete_multipart_upload {
      days_after_initiation = 7
    }
  }
}
