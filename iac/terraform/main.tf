locals {
  prefix = "${var.app_name}-${var.environment}"

  common_tags = {
    Application = var.app_name
    Environment = var.environment
    ManagedBy   = "terraform"
  }
}

provider "aws" {
  region  = var.aws_region
  profile = "rentifyx-admin"

  default_tags {
    tags = local.common_tags
  }
}

module "dynamodb" {
  source      = "./modules/dynamodb"
  prefix      = local.prefix
  environment = var.environment
}

module "ses" {
  source       = "./modules/ses"
  ses_identity = var.ses_identity
}

module "kms" {
  source      = "./modules/kms"
  prefix      = local.prefix
  environment = var.environment
}

module "cognito" {
  source           = "./modules/cognito"
  prefix           = local.prefix
  environment      = var.environment
  ses_from_address = var.ses_identity
  ses_identity_arn = module.ses.identity_arn
}

module "secrets" {
  source      = "./modules/secrets"
  app_name    = var.app_name
  environment = var.environment
  kms_key_arn = module.kms.key_arn
}

module "ec2" {
  source              = "./modules/ec2"
  prefix              = local.prefix
  environment         = var.environment
  app_name            = var.app_name
  policy_arn          = module.iam.policy_arn
  aws_region          = var.aws_region
  dynamodb_table_name = module.dynamodb.table_name
  kms_key_arn         = module.kms.key_arn
  ssh_key_name        = var.ssh_key_name
}

data "aws_caller_identity" "main" {}

module "github_actions" {
  source             = "./modules/github-actions"
  prefix             = local.prefix
  github_repo        = var.github_repo
  ecr_repository_arn = module.ec2.ecr_repository_arn
  ec2_instance_arn   = "arn:aws:ec2:${var.aws_region}:${data.aws_caller_identity.main.account_id}:instance/${module.ec2.instance_id}"
}

module "iam" {
  source                    = "./modules/iam"
  prefix                    = local.prefix
  table_arn                 = module.dynamodb.table_arn
  kms_key_arn               = module.kms.key_arn
  secret_arn                = module.secrets.secret_arn
  ses_identity_arn          = module.ses.identity_arn
  eks_oidc_provider_arn     = var.eks_oidc_provider_arn
  eks_oidc_provider_url     = var.eks_oidc_provider_url
  service_account_namespace = var.service_account_namespace
  service_account_name      = var.service_account_name
}
