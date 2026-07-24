# Infrastructure as Code

Terraform definitions that provision the real AWS infrastructure for `rentifyx-identity-api` —
used both for local development (real AWS, no LocalStack in the runtime path, see D-022 in
`.specs/project/STATE.md`) and for the deployed environment.

```
iac/
├── README.md          ← you are here
└── terraform/
    ├── main.tf         – provider, module wiring, cross-repo remote state
    ├── variables.tf    – input variables
    ├── outputs.tf      – root outputs
    ├── backend.tf       – S3 backend skeleton (values via -backend-config flags)
    ├── terraform.tfvars – local values (ses_identity, ...)
    └── modules/
        ├── dynamodb/
        ├── kms/
        ├── secrets/
        ├── cognito/
        ├── ec2/
        ├── iam/
        ├── github-actions/
        └── ses/
```

A `k8s/` directory (Kustomize base + dev/prod overlays) also exists at the repo root. It was built
alongside this Terraform during E-06 but is **not** the deployment path actually used — the real
deploy target is the `ec2` module below, driven by GitHub Actions via OIDC. Don't treat `k8s/` as
current unless that changes.

## Modules

| Module | Provisions |
|---|---|
| `dynamodb` | The single-table `{prefix}-identity` table — `PK`/`SK` hash+range key, `GSI_Email` (lookup by email), `GSI_TaxId` (lookup by CPF/CNPJ), `GSI_Outbox` (`GsiOutboxStatusPk`/`CreatedAt` — the Outbox publisher's poll query for `Pending` entries). Pay-per-request billing, TTL enabled, point-in-time recovery, server-side encryption at rest. |
| `kms` | A dedicated KMS key (`alias/{prefix}-taxid`, deletion window 30 days, rotation enabled) reserved for TaxId (CPF/CNPJ) encryption. **Not yet wired into the app** — TaxId is currently stored in plaintext in DynamoDB (see the main [README](../README.md#security) and D-010 in `.specs/project/STATE.md`). |
| `secrets` | An AWS Secrets Manager secret named `{app_name}/identity/{environment}` (e.g. `rentifyx/identity/production`), encrypted with the KMS key from `module.kms`, holding the combined runtime secret shape (`Jwt:PrivateKeyPem`, `Hmac:Key`) that `SecretsManagerConfigurationProvider` deserializes. Terraform only creates the secret with placeholder values (`REPLACE_AT_DEPLOY_TIME`) and then `ignore_changes` on `secret_string` — the real values are injected out-of-band at deploy time, never by Terraform/committed to state in the clear. |
| `cognito` (optional, `enable_cognito`) | A Cognito User Pool (email username, strong password policy, admin-create-user only) and app client restricted to `ALLOW_REFRESH_TOKEN_AUTH` only. Access tokens are still issued by the API itself using the RS256 key from `module.secrets`, not by Cognito (ADR-006) — Cognito isn't wired into the app's auth flow yet (D-004 in `.specs/project/STATE.md`). |
| `ec2` (optional, `enable_ec2`) | The actual deploy target: an ECR repository for the API image (image scanning on push, lifecycle policy keeping the last 5 images), an EC2 instance (`t2.micro`, Amazon Linux 2023, 30GB encrypted gp3 root volume) that pulls and runs the container via `userdata.sh.tpl`, its IAM role/instance profile (least-privilege policy from `module.iam` + ECR pull + SSM managed instance core, plus an optional Kafka client policy attachment), and a security group opening port 8080 (+ optional 22/SSH if `ssh_key_name` is set). |
| `iam` | A least-privilege IAM policy (`{prefix}-api-policy`) granting only the DynamoDB item/query actions the API needs on its own table (+ indexes), `kms:Decrypt`/`Encrypt`/`GenerateDataKey` on the TaxId key, and `secretsmanager:GetSecretValue` on its own secret — attached to the EC2 instance role by `module.ec2`. |
| `github-actions` (optional, `enable_github_actions`, requires `enable_ec2`) | An IAM role (`{prefix}-github-deploy`) assumable by GitHub Actions via OIDC (no long-lived AWS credentials in GitHub secrets), trust-scoped to `main`-branch workflows of `github_repo`, allowed to push to this repo's ECR repository and trigger deploys on the EC2 instance via SSM `SendCommand`. Looks up the shared `token.actions.githubusercontent.com` OIDC provider by default (`create_oidc_provider = false`) since `rentifyx-platform` already created it once for the account. |
| `ses` | Only the identity-specific SES v2 configuration set (`rentifyx-identity` — bounce/complaint suppression, reputation metrics). The shared SES email identity itself is **not** owned here: it lives in `rentifyx-platform`'s `module.ses` and is consumed read-only via `terraform_remote_state`, because SES identities are unique per AWS account and two app repos each declaring their own collided on the same real resource. |

## Cross-repo dependency on `rentifyx-platform`

Networking (VPC, subnets) and the shared Kafka broker are **not** owned by this repo. `main.tf`
reads them read-only from `rentifyx-platform`'s state via `data.terraform_remote_state.platform`
(same S3 backend/account, `key = "platform/terraform.tfstate"`):

| Output consumed | Used for |
|---|---|
| `vpc_id` | `module.ec2`'s security group |
| `public_subnets[0]` | `module.ec2`'s instance subnet |
| `ses_identity_arn` | `module.cognito`'s email sending config, and re-exported as this repo's own `ses_identity_arn` output |
| `kafka_ssm_parameter_path` | Looked up via `try(..., "")` (rentifyx-platform's Kafka module may not be applied yet) into an SSM parameter read (`data.aws_ssm_parameter.kafka_bootstrap_servers`), whose value is passed into `module.ec2` as `kafka_bootstrap_servers` and injected into the container as `ConnectionStrings__kafka`. Without a real value here the container fails to start — `KafkaProducerFactory` throws at boot (see `.specs/project/STATE.md`, 2026-07-20 session). |

Because of this, **`rentifyx-platform` must be applied first**. When tearing down, the order
reverses: destroy this repo (and `rentifyx-communications-api`) **before** destroying
`rentifyx-platform`, since both read its outputs via `terraform_remote_state`.

## Feature flags

| Variable | Default | Effect when `false` |
|---|---|---|
| `enable_ec2` | `true` | Skips `module.ec2` entirely (no ECR repo, no EC2 instance, no security group) — leaves a lightweight bootstrap of just DynamoDB/SES/KMS/Secrets/(Cognito/IAM policy). Useful when you only need the data-plane resources for local dev. |
| `enable_cognito` | `true` | Skips `module.cognito` (no User Pool/app client) — fine today since Cognito isn't wired into the API's auth flow yet (D-004). |
| `enable_github_actions` | `true` | Skips `module.github_actions` (no OIDC deploy role). Ignored unless `enable_ec2 = true` — the module needs the EC2 instance and ECR repo ARNs to scope its policy. |

## Initializing

The backend is S3 (state) + DynamoDB (lock), configured via `-backend-config` flags rather than
hardcoded in `backend.tf` (the file only declares an empty `backend "s3" {}` skeleton, required so
Terraform doesn't ask for `-reconfigure` on every subsequent command):

```bash
cd iac/terraform
terraform init \
  -backend-config="bucket=rentifyx-tfstate-166613156216" \
  -backend-config="key=identity-api/terraform.tfstate" \
  -backend-config="region=us-east-1" \
  -backend-config="dynamodb_table=rentifyx-tflock"
```

This matches the command documented in the main [README](../README.md#running-locally) — keep
both in sync if the backend bucket/table ever changes.

## Required variables / `terraform.tfvars`

Every variable in `variables.tf` has a default except one:

| Variable | Required | Notes |
|---|---|---|
| `ses_identity` | **Yes, no default** | Verified SES sender email address/domain. Must be set in `terraform.tfvars` or passed via `-var`. |
| `aws_region` | No (`sa-east-1`) | Region resources are created in. |
| `environment` | No (`production`) | Also drives resource-name prefixing (`{app_name}-{environment}`) and the Secrets Manager secret name. |
| `app_name` | No (`rentifyx`) | Resource name prefix. |
| `ssh_key_name` | No (`""`) | Leave empty to disable SSH ingress on the EC2 security group. |
| `github_repo` | No (`eugeniobandeira/rentifyx-identity-api`) | Trust scope for the GitHub Actions OIDC role. |
| `enable_ec2` / `enable_cognito` / `enable_github_actions` | No (all `true`) | See Feature flags above. |

The committed `terraform.tfvars` sets `ses_identity` for the current dev setup. Note it also still
carries `eks_oidc_provider_arn`/`eks_oidc_provider_url` values from an earlier EKS-based design —
those are no longer declared in `variables.tf` (the dead IRSA/EKS IAM setup was removed from
`modules/iam`, see `.specs/project/STATE.md`, 2026-07-17 session) and Terraform silently ignores
unknown keys in `.tfvars`, so they're inert leftovers rather than something you need to supply.

Apply, e.g. for a local/dev bootstrap:

```bash
terraform apply -var="environment=development" -var="ses_identity=you@example.com"
```

## Outputs

`outputs.tf` exposes `table_name`/`table_arn`, `kms_key_arn`, `cognito_user_pool_id` (null when
`enable_cognito=false`), `ses_identity_arn`, `iam_policy_arn`, `ec2_public_ip`/`ec2_public_dns`/
`ecr_repository_url`/`ec2_role_arn` (null when `enable_ec2=false`), and `github_deploy_role_arn`
(null unless both `enable_ec2` and `enable_github_actions` are true — set this as the
`AWS_DEPLOY_ROLE_ARN` GitHub Actions variable).

## Deploy path

The API is packaged by the root [`Dockerfile`](../Dockerfile) and that image is what the `ec2`
module's instance pulls from ECR and runs — this Terraform + GitHub Actions OIDC (`github-actions`
module) is the actual, currently-used deploy path. The `k8s/` Kustomize manifests are not wired
into any CI/CD workflow and should not be assumed to reflect how the service is actually deployed.
