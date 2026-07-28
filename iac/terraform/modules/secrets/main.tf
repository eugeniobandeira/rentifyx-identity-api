# One Secrets Manager entry per credential, not a combined JSON blob - each
# secret's value is the raw string (PEM/key), directly inspectable via
# `aws secretsmanager get-secret-value` with no JSON unwrapping, and each can
# be rotated/read independently.

resource "aws_secretsmanager_secret" "jwt_private_key" {
  name        = "${var.app_name}/identity/${var.environment}/jwt-private-key"
  description = "RS256 private key (PEM) the Identity API signs JWTs with"
  kms_key_id  = var.kms_key_arn

  recovery_window_in_days = 0

  tags = {
    Environment = var.environment
    ManagedBy   = "terraform"
  }
}

resource "aws_secretsmanager_secret_version" "jwt_private_key" {
  secret_id     = aws_secretsmanager_secret.jwt_private_key.id
  secret_string = "REPLACE_AT_DEPLOY_TIME"

  lifecycle {
    ignore_changes = [secret_string]
  }
}

resource "aws_secretsmanager_secret" "hmac_key" {
  name        = "${var.app_name}/identity/${var.environment}/hmac-key"
  description = "HMAC key used by the Identity API for token hashing"
  kms_key_id  = var.kms_key_arn

  recovery_window_in_days = 0

  tags = {
    Environment = var.environment
    ManagedBy   = "terraform"
  }
}

resource "aws_secretsmanager_secret_version" "hmac_key" {
  secret_id     = aws_secretsmanager_secret.hmac_key.id
  secret_string = "REPLACE_AT_DEPLOY_TIME"

  lifecycle {
    ignore_changes = [secret_string]
  }
}
