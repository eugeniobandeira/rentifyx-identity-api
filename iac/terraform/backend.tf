terraform {
  required_version = ">= 1.7"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }

  backend "s3" {
    bucket         = "rentifyx-tfstate-sa-166613156216"
    key            = "identity-api/terraform.tfstate"
    region         = "sa-east-1"
    dynamodb_table = "rentifyx-tflock"
    encrypt        = true
    profile        = "rentifyx-admin"
  }
}
