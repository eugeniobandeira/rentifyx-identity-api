output "role_arn" {
  description = "ARN of the IAM role assumed by the API pod via IRSA"
  value       = aws_iam_role.identity_api.arn
}

output "policy_arn" {
  description = "ARN of the least-privilege IAM policy"
  value       = aws_iam_policy.identity_api.arn
}
