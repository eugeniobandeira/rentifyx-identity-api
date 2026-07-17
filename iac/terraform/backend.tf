terraform {
  required_version = ">= 1.7"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }

  # Empty on purpose: values supplied via -backend-config flags at `terraform
  # init` time (bucket=rentifyx-tfstate-166613156216,
  # key=identity-api/terraform.tfstate, region=us-east-1,
  # dynamodb_table=rentifyx-tflock), not hardcoded here. Terraform requires
  # at least an empty `backend "s3" {}` skeleton for CLI-flag partial
  # configuration to persist correctly between commands - without it here,
  # `terraform plan`/`apply` re-detect "no backend configured" on every run
  # and demand `-reconfigure` again even right after a successful init.
  backend "s3" {}
}
