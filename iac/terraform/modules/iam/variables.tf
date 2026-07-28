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

variable "jwt_private_key_secret_arn" {
  description = "ARN of the JWT signing private key secret"
  type        = string
}

variable "hmac_key_secret_arn" {
  description = "ARN of the HMAC key secret"
  type        = string
}
