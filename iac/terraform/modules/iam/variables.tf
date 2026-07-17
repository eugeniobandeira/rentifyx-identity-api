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
