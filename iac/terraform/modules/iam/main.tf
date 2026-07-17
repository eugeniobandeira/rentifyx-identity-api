data "aws_iam_policy_document" "assume_role" {
  count = var.enable_irsa_role ? 1 : 0

  statement {
    effect  = "Allow"
    actions = ["sts:AssumeRoleWithWebIdentity"]

    principals {
      type        = "Federated"
      identifiers = [var.eks_oidc_provider_arn]
    }

    condition {
      test     = "StringEquals"
      variable = "${var.eks_oidc_provider_url}:sub"
      values   = ["system:serviceaccount:${var.service_account_namespace}:${var.service_account_name}"]
    }

    condition {
      test     = "StringEquals"
      variable = "${var.eks_oidc_provider_url}:aud"
      values   = ["sts.amazonaws.com"]
    }
  }
}

data "aws_iam_policy_document" "identity_api" {
  statement {
    sid    = "DynamoDBAccess"
    effect = "Allow"

    actions = [
      "dynamodb:GetItem",
      "dynamodb:PutItem",
      "dynamodb:UpdateItem",
      "dynamodb:DeleteItem",
      "dynamodb:Query",
    ]

    resources = [
      var.table_arn,
      "${var.table_arn}/index/*",
    ]
  }

  statement {
    sid    = "KMSAccess"
    effect = "Allow"

    actions = [
      "kms:Decrypt",
      "kms:Encrypt",
      "kms:GenerateDataKey",
    ]

    resources = [var.kms_key_arn]
  }

  statement {
    sid    = "SecretsManagerAccess"
    effect = "Allow"

    actions = ["secretsmanager:GetSecretValue"]

    resources = [var.secret_arn]
  }
}

resource "aws_iam_role" "identity_api" {
  count = var.enable_irsa_role ? 1 : 0

  name               = "${var.prefix}-api-role"
  assume_role_policy = data.aws_iam_policy_document.assume_role[0].json

  tags = {
    ManagedBy = "terraform"
  }
}

resource "aws_iam_policy" "identity_api" {
  name        = "${var.prefix}-api-policy"
  description = "Least-privilege policy for the RentifyX Identity API pod"
  policy      = data.aws_iam_policy_document.identity_api.json
}

resource "aws_iam_role_policy_attachment" "identity_api" {
  count = var.enable_irsa_role ? 1 : 0

  role       = aws_iam_role.identity_api[0].name
  policy_arn = aws_iam_policy.identity_api.arn
}
