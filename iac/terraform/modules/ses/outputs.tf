output "configuration_set_name" {
  description = "Name of the SES configuration set"
  value       = aws_sesv2_configuration_set.identity.configuration_set_name
}
