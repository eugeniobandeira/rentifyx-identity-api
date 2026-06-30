output "identity_arn" {
  description = "ARN of the SES email identity"
  value       = aws_sesv2_email_identity.sender.arn
}

output "configuration_set_name" {
  description = "Name of the SES configuration set"
  value       = aws_sesv2_configuration_set.identity.configuration_set_name
}
