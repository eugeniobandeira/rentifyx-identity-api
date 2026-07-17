output "policy_arn" {
  description = "ARN of the least-privilege IAM policy"
  value       = aws_iam_policy.identity_api.arn
}
