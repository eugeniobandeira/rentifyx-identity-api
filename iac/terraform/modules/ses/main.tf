# The email identity itself now lives in rentifyx-platform's module.ses -
# shared by this repo and rentifyx-communications-api via
# terraform_remote_state, since SES identities are unique per AWS account
# and two app repos each owning their own collided on the same real
# resource. This module keeps only the identity-specific configuration set.
resource "aws_sesv2_configuration_set" "identity" {
  configuration_set_name = "rentifyx-identity"

  suppression_options {
    suppressed_reasons = ["BOUNCE", "COMPLAINT"]
  }

  reputation_options {
    reputation_metrics_enabled = true
  }
}
