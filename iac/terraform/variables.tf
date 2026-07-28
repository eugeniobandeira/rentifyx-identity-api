variable "aws_region" {
  description = "AWS region to deploy resources"
  type        = string
  default     = "sa-east-1"
}

variable "environment" {
  description = "Deployment environment (production, staging, development)"
  type        = string
  default     = "production"
}

variable "app_name" {
  description = "Application name used as resource name prefix"
  type        = string
  default     = "rentifyx"
}

variable "ses_identity" {
  description = "Verified SES sender email address or domain"
  type        = string
}

variable "ssh_key_name" {
  description = "EC2 key pair name for SSH access (leave empty to disable SSH)"
  type        = string
  default     = ""
}

variable "github_repo" {
  description = "GitHub repository in owner/repo format allowed to assume the deploy role"
  type        = string
  default     = "eugeniobandeira/rentifyx-identity-api"
}

variable "enable_ec2" {
  description = "Provision the EC2 deploy target (instance, ECR repo, security group). Disable for a lightweight dev bootstrap that only needs DynamoDB/SES/KMS/Secrets."
  type        = bool
  default     = true
}

variable "enable_cognito" {
  description = "Provision the Cognito user pool. Disable if Cognito isn't wired into the API yet (see D-004 in .specs/project/STATE.md) or isn't needed for the current test."
  type        = bool
  default     = true
}

variable "enable_github_actions" {
  description = <<-EOT
    Provision the GitHub Actions OIDC deploy role. Requires enable_ec2 = true
    (it grants access to the EC2 instance and ECR repo); ignored otherwise.
    Defaults to false: this module's data lookup for the shared
    token.actions.githubusercontent.com OIDC provider (created by
    rentifyx-platform's module.github_actions_oidc) fails outright if that
    provider doesn't currently exist in the account, breaking every plan/
    destroy by default. Set to true only once that provider is confirmed
    live and a real CI/CD pipeline is ready to use this role.
  EOT
  type        = bool
  default     = false
}
