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

variable "eks_oidc_provider_arn" {
  description = "ARN of the EKS OIDC provider for IRSA"
  type        = string
  default     = "arn:aws:iam::123456789012:oidc-provider/oidc.eks.us-east-1.amazonaws.com/id/EXAMPLE"
}

variable "eks_oidc_provider_url" {
  description = "URL of the EKS OIDC provider without https:// prefix"
  type        = string
  default     = "oidc.eks.us-east-1.amazonaws.com/id/EXAMPLE"
}

variable "service_account_namespace" {
  description = "Kubernetes namespace where the API service account runs"
  type        = string
  default     = "prod"
}

variable "service_account_name" {
  description = "Kubernetes service account name that assumes the IAM role"
  type        = string
  default     = "rentifyx-identity-api"
}

variable "ssh_key_name" {
  description = "EC2 key pair name for SSH access (leave empty to disable SSH)"
  type        = string
  default     = ""
}
