# ---------------------------------------------------------------------------
# ECR repository — stores the API Docker image
# ---------------------------------------------------------------------------

resource "aws_ecr_repository" "identity_api" {
  name                 = "${var.prefix}-identity-api"
  image_tag_mutability = "MUTABLE"

  image_scanning_configuration {
    scan_on_push = true
  }

  tags = {
    Environment = var.environment
    ManagedBy   = "terraform"
  }
}

resource "aws_ecr_lifecycle_policy" "identity_api" {
  repository = aws_ecr_repository.identity_api.name

  policy = jsonencode({
    rules = [
      {
        rulePriority = 1
        description  = "Keep last 5 images"
        selection = {
          tagStatus   = "any"
          countType   = "imageCountMoreThan"
          countNumber = 5
        }
        action = { type = "expire" }
      }
    ]
  })
}

# ---------------------------------------------------------------------------
# IAM instance profile — reuses the least-privilege policy from the iam module
# ---------------------------------------------------------------------------

resource "aws_iam_role" "ec2" {
  name = "${var.prefix}-ec2-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect    = "Allow"
        Principal = { Service = "ec2.amazonaws.com" }
        Action    = "sts:AssumeRole"
      }
    ]
  })

  tags = {
    ManagedBy = "terraform"
  }
}

resource "aws_iam_role_policy_attachment" "ec2_identity_api" {
  role       = aws_iam_role.ec2.name
  policy_arn = var.policy_arn
}

resource "aws_iam_role_policy_attachment" "ec2_ssm" {
  role       = aws_iam_role.ec2.name
  policy_arn = "arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore"
}

# Combined runtime secret (rentifyx/identity/{environment}) and its
# SecretsManagerAccess grant live in module.secrets/module.iam — the EC2
# role gets read access via the ec2_identity_api policy attachment above,
# not a duplicate secret here.

# ECR pull permissions
resource "aws_iam_role_policy" "ec2_ecr" {
  name = "${var.prefix}-ec2-ecr"
  role = aws_iam_role.ec2.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid      = "ECRAuth"
        Effect   = "Allow"
        Action   = ["ecr:GetAuthorizationToken"]
        Resource = "*"
      },
      {
        Sid    = "ECRPull"
        Effect = "Allow"
        Action = [
          "ecr:BatchGetImage",
          "ecr:GetDownloadUrlForLayer",
          "ecr:BatchCheckLayerAvailability",
        ]
        Resource = aws_ecr_repository.identity_api.arn
      }
    ]
  })
}

# MSK Serverless access - see rentifyx-platform ADR-002. Skipped (count = 0)
# until that repo's module.kafka has actually been applied and its output
# is real JSON, not an empty string.
resource "aws_iam_role_policy" "ec2_kafka" {
  count = var.kafka_client_policy_json != "" ? 1 : 0

  name   = "${var.prefix}-ec2-kafka"
  role   = aws_iam_role.ec2.id
  policy = var.kafka_client_policy_json
}

resource "aws_iam_instance_profile" "ec2" {
  name = "${var.prefix}-ec2-profile"
  role = aws_iam_role.ec2.name
}

# ---------------------------------------------------------------------------
# Security group
# ---------------------------------------------------------------------------

resource "aws_security_group" "identity_api" {
  name        = "${var.prefix}-identity-api-sg"
  description = "Allow inbound HTTP on 8080 and optional SSH"
  vpc_id      = var.vpc_id

  ingress {
    description = "API"
    from_port   = 8080
    to_port     = 8080
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  dynamic "ingress" {
    for_each = var.ssh_key_name != "" ? [1] : []
    content {
      description = "SSH"
      from_port   = 22
      to_port     = 22
      protocol    = "tcp"
      cidr_blocks = ["0.0.0.0/0"]
    }
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    Environment = var.environment
    ManagedBy   = "terraform"
  }
}

# ---------------------------------------------------------------------------
# EC2 instance — t2.micro (free tier eligible)
# ---------------------------------------------------------------------------

data "aws_ami" "amazon_linux_2023" {
  most_recent = true
  owners      = ["amazon"]

  filter {
    name   = "name"
    values = ["al2023-ami-*-x86_64"]
  }

  filter {
    name   = "virtualization-type"
    values = ["hvm"]
  }
}

resource "aws_instance" "identity_api" {
  ami                         = data.aws_ami.amazon_linux_2023.id
  instance_type               = "t2.micro"
  iam_instance_profile        = aws_iam_instance_profile.ec2.name
  vpc_security_group_ids      = [aws_security_group.identity_api.id]
  subnet_id                   = var.subnet_id
  associate_public_ip_address = true
  key_name                    = var.ssh_key_name != "" ? var.ssh_key_name : null

  user_data = base64encode(templatefile("${path.module}/userdata.sh.tpl", {
    aws_region          = var.aws_region
    ecr_repository_url  = aws_ecr_repository.identity_api.repository_url
    dynamodb_table_name = var.dynamodb_table_name
  }))

  root_block_device {
    volume_size = 30
    volume_type = "gp3"
    encrypted   = true
  }

  tags = {
    Name        = "${var.prefix}-identity-api"
    Environment = var.environment
    ManagedBy   = "terraform"
  }

  # data.aws_ami's most_recent lookup re-resolves to a newer AMI ID every
  # time AWS publishes a patched al2023 image, which would otherwise force
  # a replace on every plan. AMI updates should be a deliberate redeploy
  # (new instance via CI/CD), not accidental churn from an unrelated apply.
  lifecycle {
    ignore_changes = [ami]
  }
}
