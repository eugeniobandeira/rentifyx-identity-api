output "jwt_private_key_secret_arn" {
  description = "ARN of the JWT signing private key secret"
  value       = aws_secretsmanager_secret.jwt_private_key.arn
}

output "hmac_key_secret_arn" {
  description = "ARN of the HMAC key secret"
  value       = aws_secretsmanager_secret.hmac_key.arn
}
