resource "aws_kms_key" "taxid" {
  description             = "RentifyX TaxId at-rest encryption"
  deletion_window_in_days = 30
  enable_key_rotation     = true

  tags = {
    Environment = var.environment
    ManagedBy   = "terraform"
  }
}

resource "aws_kms_alias" "taxid" {
  name          = "alias/${var.prefix}-taxid"
  target_key_id = aws_kms_key.taxid.key_id
}
