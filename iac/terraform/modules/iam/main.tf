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

resource "aws_iam_policy" "identity_api" {
  name        = "${var.prefix}-api-policy"
  description = "Least-privilege policy for the RentifyX Identity API"
  policy      = data.aws_iam_policy_document.identity_api.json
}
