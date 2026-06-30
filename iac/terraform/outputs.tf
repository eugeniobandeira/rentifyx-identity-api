output "table_name" {
  description = "DynamoDB table name"
  value       = module.dynamodb.table_name
}

output "table_arn" {
  description = "DynamoDB table ARN"
  value       = module.dynamodb.table_arn
}

output "kms_key_arn" {
  description = "KMS key ARN used for TaxId encryption"
  value       = module.kms.key_arn
}

output "cognito_user_pool_id" {
  description = "Cognito User Pool ID"
  value       = module.cognito.user_pool_id
}

output "ses_identity_arn" {
  description = "SES email identity ARN"
  value       = module.ses.identity_arn
}

output "iam_role_arn" {
  description = "IAM role ARN for EKS pod identity (IRSA)"
  value       = module.iam.role_arn
}
