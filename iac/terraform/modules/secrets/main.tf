resource "aws_secretsmanager_secret" "identity" {
  name        = "${var.app_name}/identity/${var.environment}"
  description = "Combined app secrets for the RentifyX Identity API (Jwt:PrivateKeyPem, Hmac:Key)"
  kms_key_id  = var.kms_key_arn

  recovery_window_in_days = 0

  tags = {
    Environment = var.environment
    ManagedBy   = "terraform"
  }
}

resource "aws_secretsmanager_secret_version" "identity" {
  secret_id = aws_secretsmanager_secret.identity.id
  secret_string = jsonencode({
    "Jwt:PrivateKeyPem" = "REPLACE_AT_DEPLOY_TIME"
    "Hmac:Key"          = "REPLACE_AT_DEPLOY_TIME"
  })

  lifecycle {
    ignore_changes = [secret_string]
  }
}
