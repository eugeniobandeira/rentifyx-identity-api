resource "aws_sesv2_email_identity" "sender" {
  email_identity = var.ses_identity
}

resource "aws_sesv2_configuration_set" "identity" {
  configuration_set_name = "rentifyx-identity"

  suppression_options {
    suppressed_reasons = ["BOUNCE", "COMPLAINT"]
  }

  reputation_options {
    reputation_metrics_enabled = true
  }
}
