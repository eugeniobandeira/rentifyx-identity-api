output "key_arn" {
  description = "ARN of the KMS key"
  value       = aws_kms_key.taxid.arn
}

output "key_id" {
  description = "ID of the KMS key"
  value       = aws_kms_key.taxid.key_id
}
