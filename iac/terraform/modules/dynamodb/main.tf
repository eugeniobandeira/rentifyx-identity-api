resource "aws_dynamodb_table" "identity" {
  name         = "${var.prefix}-identity"
  billing_mode = "PAY_PER_REQUEST"
  table_class  = "STANDARD"

  hash_key  = "PK"
  range_key = "SK"

  attribute {
    name = "PK"
    type = "S"
  }

  attribute {
    name = "SK"
    type = "S"
  }

  attribute {
    name = "Email"
    type = "S"
  }

  attribute {
    name = "TaxId"
    type = "S"
  }

  global_secondary_index {
    name            = "GSI_Email"
    hash_key        = "Email"
    projection_type = "ALL"
  }

  global_secondary_index {
    name            = "GSI_TaxId"
    hash_key        = "TaxId"
    projection_type = "ALL"
  }

  ttl {
    attribute_name = "TTL"
    enabled        = true
  }

  point_in_time_recovery {
    enabled = true
  }

  server_side_encryption {
    enabled = true
  }

  tags = {
    Environment = var.environment
    ManagedBy   = "terraform"
  }
}
