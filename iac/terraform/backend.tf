terraform {
  required_version = ">= 1.7"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }

  # Backend values are supplied via backend.hcl (gitignored) to avoid
  # exposing the AWS account ID in a public repository.
  # Run: terraform init -backend-config=backend.hcl
  backend "s3" {}
}
