variable "prefix" {
  description = "Resource name prefix"
  type        = string
}

variable "environment" {
  description = "Deployment environment"
  type        = string
}

variable "policy_arn" {
  description = "ARN of the least-privilege IAM policy to attach to the instance profile"
  type        = string
}

variable "aws_region" {
  description = "AWS region"
  type        = string
}

variable "dynamodb_table_name" {
  description = "DynamoDB identity table name"
  type        = string
}

variable "ssh_key_name" {
  description = "EC2 key pair name for SSH access (leave empty to disable SSH)"
  type        = string
  default     = ""
}

variable "app_name" {
  description = "Application name used in the runtime secret path (e.g. rentifyx)"
  type        = string
}

variable "kms_key_arn" {
  description = "ARN of the KMS key used to encrypt the runtime secret"
  type        = string
}
