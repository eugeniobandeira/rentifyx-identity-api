output "secret_arn" {
  description = "ARN of the combined identity app secret"
  value       = aws_secretsmanager_secret.identity.arn
}
