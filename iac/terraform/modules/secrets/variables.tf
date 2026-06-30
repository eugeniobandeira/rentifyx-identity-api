variable "prefix" {
  description = "Resource name prefix"
  type        = string
}

variable "environment" {
  description = "Deployment environment"
  type        = string
}

variable "kms_key_arn" {
  description = "ARN of the KMS key used to encrypt secrets at rest"
  type        = string
}
