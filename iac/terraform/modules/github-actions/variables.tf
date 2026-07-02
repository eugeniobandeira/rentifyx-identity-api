variable "prefix" {
  description = "Resource name prefix"
  type        = string
}

variable "github_repo" {
  description = "GitHub repository in owner/repo format (e.g. eugeniobandeira/rentifyx-identity-api)"
  type        = string
}

variable "ecr_repository_arn" {
  description = "ARN of the ECR repository the workflow will push to"
  type        = string
}

variable "ec2_instance_arn" {
  description = "ARN of the EC2 instance the workflow will send SSM commands to"
  type        = string
}
