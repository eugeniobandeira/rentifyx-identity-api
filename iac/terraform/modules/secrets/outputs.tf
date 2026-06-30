output "jwt_secret_arn" {
  description = "ARN of the JWT private key secret"
  value       = aws_secretsmanager_secret.jwt_private_key.arn
}

output "hmac_secret_arn" {
  description = "ARN of the HMAC key secret"
  value       = aws_secretsmanager_secret.hmac_key.arn
}

output "ses_secret_arn" {
  description = "ARN of the SES from-address secret"
  value       = aws_secretsmanager_secret.ses_from_address.arn
}
