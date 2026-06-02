# Remote state — uncomment AFTER the bootstrap S3 bucket + DynamoDB lock
# table exist (see docs/AWS_DEPLOYMENT.md "Bootstrap" step). The first
# `terraform apply` runs with LOCAL state, then you migrate to S3.
#
# terraform {
#   backend "s3" {
#     bucket         = "dhl-logistics-tfstate"      # replace with your bucket
#     key            = "prod/terraform.tfstate"
#     region         = "ap-south-1"
#     dynamodb_table = "dhl-logistics-tflock"
#     encrypt        = true
#   }
# }
