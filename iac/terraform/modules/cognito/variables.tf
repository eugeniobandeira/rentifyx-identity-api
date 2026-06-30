variable "prefix" {
  description = "Resource name prefix"
  type        = string
}

variable "environment" {
  description = "Deployment environment"
  type        = string
}

variable "ses_from_address" {
  description = "Email address used as sender in Cognito emails (must match SES identity)"
  type        = string
}

variable "ses_identity_arn" {
  description = "ARN of the SES verified identity for sending Cognito emails"
  type        = string
}
