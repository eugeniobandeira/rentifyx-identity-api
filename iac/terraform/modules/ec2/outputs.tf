output "instance_id" {
  description = "EC2 instance ID"
  value       = aws_instance.identity_api.id
}

output "public_ip" {
  description = "Public IP of the EC2 instance"
  value       = aws_instance.identity_api.public_ip
}

output "public_dns" {
  description = "Public DNS of the EC2 instance"
  value       = aws_instance.identity_api.public_dns
}

output "ecr_repository_url" {
  description = "ECR repository URL for docker push"
  value       = aws_ecr_repository.identity_api.repository_url
}

output "ec2_role_arn" {
  description = "IAM role ARN attached to the EC2 instance profile"
  value       = aws_iam_role.ec2.arn
}

output "runtime_secret_arn" {
  description = "ARN of the combined runtime secret (Jwt:PrivateKeyPem, Hmac:Key, Ses:FromAddress)"
  value       = aws_secretsmanager_secret.runtime.arn
}
