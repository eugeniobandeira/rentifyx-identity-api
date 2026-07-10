variable "prefix" {
  description = "Resource name prefix"
  type        = string
}

variable "table_arn" {
  description = "ARN of the DynamoDB identity table"
  type        = string
}

variable "kms_key_arn" {
  description = "ARN of the KMS key used for TaxId encryption"
  type        = string
}

variable "secret_arn" {
  description = "ARN of the Secrets Manager secret the API must read"
  type        = string
}

variable "ses_identity_arn" {
  description = "ARN of the SES verified identity for sending emails"
  type        = string
}

variable "eks_oidc_provider_arn" {
  description = "ARN of the EKS OIDC provider for IRSA trust policy"
  type        = string
}

variable "eks_oidc_provider_url" {
  description = "URL of the EKS OIDC provider without https:// prefix"
  type        = string
}

variable "service_account_namespace" {
  description = "Kubernetes namespace of the API service account"
  type        = string
}

variable "service_account_name" {
  description = "Kubernetes service account name that assumes this role"
  type        = string
}
