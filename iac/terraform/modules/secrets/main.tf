resource "aws_secretsmanager_secret" "jwt_private_key" {
  name        = "${var.prefix}/jwt-private-key-pem"
  description = "RSA-2048 private key PEM for RS256 JWT signing"
  kms_key_id  = var.kms_key_arn

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
  name        = "${var.prefix}/hmac-key"
  description = "64-byte hex string for HMAC-SHA256 token hashing"
  kms_key_id  = var.kms_key_arn

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

resource "aws_secretsmanager_secret" "ses_from_address" {
  name        = "${var.prefix}/ses-from-address"
  description = "Verified SES sender address used by EmailService"
  kms_key_id  = var.kms_key_arn

  tags = {
    Environment = var.environment
    ManagedBy   = "terraform"
  }
}

resource "aws_secretsmanager_secret_version" "ses_from_address" {
  secret_id     = aws_secretsmanager_secret.ses_from_address.id
  secret_string = "REPLACE_AT_DEPLOY_TIME"

  lifecycle {
    ignore_changes = [secret_string]
  }
}
