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
  description = "Cognito User Pool ID (null when enable_cognito = false)"
  value       = one(module.cognito[*].user_pool_id)
}

output "ses_identity_arn" {
  description = "SES email identity ARN"
  value       = module.ses.identity_arn
}

output "iam_role_arn" {
  description = "IAM role ARN for EKS pod identity (IRSA)"
  value       = module.iam.role_arn
}

output "ec2_public_ip" {
  description = "Public IP of the EC2 instance (null when enable_ec2 = false)"
  value       = one(module.ec2[*].public_ip)
}

output "ec2_public_dns" {
  description = "Public DNS of the EC2 instance (null when enable_ec2 = false)"
  value       = one(module.ec2[*].public_dns)
}

output "ecr_repository_url" {
  description = "ECR repository URL — use this for docker push (null when enable_ec2 = false)"
  value       = one(module.ec2[*].ecr_repository_url)
}

output "ec2_role_arn" {
  description = "IAM role ARN attached to the EC2 instance profile (null when enable_ec2 = false)"
  value       = one(module.ec2[*].ec2_role_arn)
}

output "github_deploy_role_arn" {
  description = "IAM role ARN for GitHub Actions deploy workflow (OIDC) — set as GH Actions variable AWS_DEPLOY_ROLE_ARN (null when enable_ec2/enable_github_actions = false)"
  value       = one(module.github_actions[*].deploy_role_arn)
}
