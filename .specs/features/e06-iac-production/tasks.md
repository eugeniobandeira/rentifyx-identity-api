# Tasks: E-06 IaC & Production Readiness

## Summary Table

| # | Section | Layer | What | File(s) | Depends on |
|---|---|---|---|---|---|
| T-01 | A | IaC | Terraform backend config (S3 + DynamoDB lock) | `iac/terraform/backend.tf` | none |
| T-02 | A | IaC | Terraform root — provider, variables, outputs, module calls | `iac/terraform/main.tf`, `variables.tf`, `outputs.tf` | T-01 |
| T-03 | A | IaC | Terraform module: `dynamodb` — table, GSIs, TTL, PITR | `iac/terraform/modules/dynamodb/` | T-02 |
| T-04 | A | IaC | Terraform module: `cognito` — user pool, app client, RS256 | `iac/terraform/modules/cognito/` | T-02 |
| T-05 | A | IaC | Terraform module: `ses` — email identity, configuration set | `iac/terraform/modules/ses/` | T-02 |
| T-06 | A | IaC | Terraform module: `kms` — symmetric key + alias for TaxId | `iac/terraform/modules/kms/` | T-02 |
| T-07 | A | IaC | Terraform module: `secrets` — three Secrets Manager entries | `iac/terraform/modules/secrets/` | T-02, T-06 |
| T-08 | A | IaC | Terraform module: `iam` — EKS IRSA role + least-privilege policy | `iac/terraform/modules/iam/` | T-03, T-06, T-07 |
| T-09 | A | IaC | Validate `terraform fmt` and `terraform validate` pass on all modules | CI / local | T-03–T-08 |
| T-10 | B | K8s | Add health probes to `k8s/base/deployment.yaml` | `k8s/base/deployment.yaml` | none |
| T-11 | B | K8s | Add `k8s/base/hpa.yaml` — HPA (min 2, max 10, CPU 70%) | `k8s/base/hpa.yaml` | T-10 |
| T-12 | B | K8s | Add `k8s/base/configmap.yaml` — non-secret environment variables | `k8s/base/configmap.yaml` | none |
| T-13 | B | K8s | Add `k8s/base/secret-provider-class.yaml` — AWS Secrets Store CSI | `k8s/base/secret-provider-class.yaml` | T-08 |
| T-14 | B | K8s | Update `k8s/base/kustomization.yaml` — add hpa, configmap, secret-provider-class | `k8s/base/kustomization.yaml` | T-11, T-12, T-13 |
| T-15 | B | K8s | Update `k8s/overlays/prod/kustomization.yaml` — add HPA minReplicas: 3 patch + IRSA annotation | `k8s/overlays/prod/kustomization.yaml` | T-14 |
| T-16 | B | K8s | Update `k8s/overlays/dev/kustomization.yaml` — reference configmap, disable secret-provider-class | `k8s/overlays/dev/kustomization.yaml` | T-14 |
| T-17 | C | App | Add `Serilog.Sinks.AwsCloudWatch` NuGet to `Directory.Packages.props` and `RentifyxIdentity.Api.csproj` | `Directory.Packages.props`, `Api.csproj` | none |
| T-18 | C | App | Create `appsettings.Production.json` — Serilog CloudWatch sink + OTel placeholder values | `02-src/01-Api/RentifyxIdentity.Api/appsettings.Production.json` | T-17 |
| T-19 | C | Docs | Write SLO definitions document | `docs/slo.md` | none |
| T-20 | D | Docs | Write C4 Context diagram (L1) | `docs/architecture/c4-context.md` | none |
| T-21 | D | Docs | Write C4 Container diagram (L2) | `docs/architecture/c4-container.md` | none |
| T-22 | D | Docs | Write C4 Component diagram (L3) | `docs/architecture/c4-component.md` | none |
| T-23 | E | Docs | Write runbook | `docs/runbook.md` | none |
| T-24 | E | Release | Update `STATE.md` and `ROADMAP.md` — mark E-06 complete, all milestones done | `.specs/project/STATE.md`, `ROADMAP.md` | T-01–T-23 |
| T-25 | E | Release | Create GitHub Release with release notes and git tag `v1.0.0` | git / GitHub | T-24 |

---

## Section A — Terraform IaC

---
status: pending
title: Terraform backend config
type: iac
complexity: low
dependencies: none
---

**Layer:** IaC
**File:** `iac/terraform/backend.tf`
**Reference:** spec Section A → Backend
**What:** Create `backend.tf` declaring an `s3` backend with bucket `rentifyx-tfstate`, key `identity-api/terraform.tfstate`, region `us-east-1`, `dynamodb_table = "rentifyx-tflock"`, and `encrypt = true`. Add a `terraform {}` block with `required_version = ">= 1.9"` and `required_providers { aws = { source = "hashicorp/aws", version = "~> 5.0" } }`.
**Done when:** `terraform init -backend=false` succeeds (no actual S3 bucket needed); file compiles without syntax errors; `terraform fmt -check` exits 0.
**Gate:** `terraform fmt -check iac/terraform/` and `terraform validate` (after init with `-backend=false`)

---
status: pending
title: Terraform root — provider, variables, outputs, module calls
type: iac
complexity: medium
dependencies: T-01
---

**Layer:** IaC
**Files:** `iac/terraform/main.tf`, `iac/terraform/variables.tf`, `iac/terraform/outputs.tf`
**Reference:** spec Section A → Module Contracts
**What:**
- `variables.tf`: declare `aws_region` (default `"us-east-1"`), `environment` (default `"production"`), `app_name` (default `"rentifyx-identity"`), `prefix` (computed as `"${var.app_name}-${var.environment}"`).
- `main.tf`: configure `provider "aws"` with `region = var.aws_region`; call all six modules passing their required inputs.
- `outputs.tf`: expose `table_name`, `table_arn`, `kms_key_arn`, `cognito_user_pool_id`, `ses_identity_arn`, `iam_role_arn`.
**Done when:** `terraform validate` succeeds after all six modules exist; all module references resolve; no unused variables.
**Gate:** `terraform validate`

---
status: pending
title: Terraform module — dynamodb
type: iac
complexity: medium
dependencies: T-02
---

**Layer:** IaC
**Files:** `iac/terraform/modules/dynamodb/main.tf`, `variables.tf`, `outputs.tf`
**Reference:** spec Section A → `modules/dynamodb`
**What:**
- `aws_dynamodb_table` resource: `PAY_PER_REQUEST`, `REGIONAL` class.
- Hash key `PK` (S), range key `SK` (S).
- GSI `GSI-Email`: hash key `Email` (S), `ALL` projection.
- GSI `GSI-TaxId`: hash key `TaxId` (S), `ALL` projection.
- TTL attribute `TTL`, enabled.
- `point_in_time_recovery { enabled = true }`.
- `server_side_encryption { enabled = true }` (AWS-managed CMK).
- `tags` block with `Environment` and `ManagedBy = "terraform"`.
**Done when:** `terraform validate` succeeds; resource block is syntactically correct; both GSIs are declared; `terraform plan -out=tfplan` would not error on a valid AWS account.
**Gate:** `terraform validate`

---
status: pending
title: Terraform module — cognito
type: iac
complexity: medium
dependencies: T-02
---

**Layer:** IaC
**Files:** `iac/terraform/modules/cognito/main.tf`, `variables.tf`, `outputs.tf`
**Reference:** spec Section A → `modules/cognito`
**What:**
- `aws_cognito_user_pool`: `username_attributes = ["email"]`; password policy matching domain rules (12+ chars, upper, lower, digits, symbols); `allow_admin_create_user_only = true` (no self-signup); email MFA configuration placeholder.
- Token signing: Cognito defaults to RS256; set `access_token_validity = 15` minutes.
- `aws_cognito_user_pool_client`: `explicit_auth_flows = []` (no direct auth — tokens issued by identity-api); `prevent_user_existence_errors = "ENABLED"`.
- Outputs: `user_pool_id`, `user_pool_arn`, `client_id`.
**Done when:** `terraform validate` succeeds; pool and client resources declared; no `ALLOW_USER_PASSWORD_AUTH` in explicit_auth_flows.
**Gate:** `terraform validate`

---
status: pending
title: Terraform module — ses
type: iac
complexity: low
dependencies: T-02
---

**Layer:** IaC
**Files:** `iac/terraform/modules/ses/main.tf`, `variables.tf`, `outputs.tf`
**Reference:** spec Section A → `modules/ses`
**What:**
- `aws_sesv2_email_identity` resource for the sender address (variable `ses_identity`).
- `aws_sesv2_configuration_set`: suppression list reason `BOUNCE` and `COMPLAINT`; reputation tracking enabled.
- Outputs: `identity_arn`, `configuration_set_name`.
**Done when:** `terraform validate` succeeds; both resources declared; variable `ses_identity` required (no default — must be provided at apply time).
**Gate:** `terraform validate`

---
status: pending
title: Terraform module — kms
type: iac
complexity: low
dependencies: T-02
---

**Layer:** IaC
**Files:** `iac/terraform/modules/kms/main.tf`, `variables.tf`, `outputs.tf`
**Reference:** spec Section A → `modules/kms`
**What:**
- `aws_kms_key`: `description = "RentifyX TaxId at-rest encryption"`, `enable_key_rotation = true`, `deletion_window_in_days = 30`.
- `aws_kms_alias`: `name = "alias/${var.prefix}-taxid"`.
- Outputs: `key_arn`, `key_id`.
**Done when:** `terraform validate` succeeds; alias references the key; rotation enabled.
**Gate:** `terraform validate`

---
status: pending
title: Terraform module — secrets
type: iac
complexity: low
dependencies: T-02, T-06
---

**Layer:** IaC
**Files:** `iac/terraform/modules/secrets/main.tf`, `variables.tf`, `outputs.tf`
**Reference:** spec Section A → `modules/secrets`
**What:**
- Three `aws_secretsmanager_secret` resources: `${var.prefix}/jwt-private-key-pem`, `${var.prefix}/hmac-key`, `${var.prefix}/ses-from-address`. All encrypted with `var.kms_key_arn`.
- Three `aws_secretsmanager_secret_version` resources with placeholder `secret_string` values (e.g. `"REPLACE_AT_DEPLOY_TIME"`). Marked with `lifecycle { ignore_changes = [secret_string] }` so real values set outside Terraform are not overwritten.
- Outputs: `jwt_secret_arn`, `hmac_secret_arn`, `ses_secret_arn`.
**Done when:** `terraform validate` succeeds; `ignore_changes` is present on all three versions; all three ARNs exported.
**Gate:** `terraform validate`

---
status: pending
title: Terraform module — iam
type: iac
complexity: medium
dependencies: T-03, T-06, T-07
---

**Layer:** IaC
**Files:** `iac/terraform/modules/iam/main.tf`, `variables.tf`, `outputs.tf`
**Reference:** spec Section A → `modules/iam`
**What:**
- `aws_iam_role`: assume-role policy with `sts:AssumeRoleWithWebIdentity` trust (EKS IRSA pattern); `var.eks_oidc_provider_arn` and `var.service_account_namespace` as inputs (with placeholder defaults for study project).
- `aws_iam_policy` with a JSON document granting:
  - `dynamodb:GetItem`, `PutItem`, `UpdateItem`, `DeleteItem`, `Query` on `var.table_arn` and `${var.table_arn}/index/*`.
  - `kms:Decrypt`, `Encrypt`, `GenerateDataKey` on `var.kms_key_arn`.
  - `secretsmanager:GetSecretValue` on each ARN in `var.secret_arns`.
  - `ses:SendEmail`, `ses:SendRawEmail` scoped to the SES identity ARN.
- `aws_iam_role_policy_attachment`: attaches the policy to the role.
- Outputs: `role_arn`, `policy_arn`.
**Done when:** `terraform validate` succeeds; least-privilege resource conditions used (no `*` resources); IRSA trust pattern correct.
**Gate:** `terraform validate`

---
status: pending
title: Validate all Terraform modules with fmt + validate
type: iac
complexity: low
dependencies: T-03, T-04, T-05, T-06, T-07, T-08
---

**Layer:** IaC
**What:** Run `terraform fmt -recursive -check iac/terraform/` to verify all `.tf` files are correctly formatted. Run `terraform init -backend=false` followed by `terraform validate` in `iac/terraform/` to confirm all module references and variable types are valid. Fix any errors found.
**Done when:** Both commands exit 0; no formatting diff; no validation errors.
**Gate:** `terraform fmt -recursive -check iac/terraform/` and `terraform validate`

---

## Section B — Kubernetes Manifests

---
status: pending
title: Add health probes to k8s/base/deployment.yaml
type: k8s
complexity: low
dependencies: none
---

**Layer:** K8s
**File:** `k8s/base/deployment.yaml`
**Reference:** spec Section B → Health Probes
**What:** Add `readinessProbe` (`httpGet /health/ready :8080`, `initialDelaySeconds: 5`, `periodSeconds: 10`, `failureThreshold: 3`) and `livenessProbe` (`httpGet /health/live :8080`, `initialDelaySeconds: 15`, `periodSeconds: 20`, `failureThreshold: 3`) to the `api` container spec. Also update `strategy` to rolling update: `maxSurge: 1`, `maxUnavailable: 0` (ADR-008).
**Done when:** YAML is syntactically valid (`kubectl apply --dry-run=client -f k8s/base/deployment.yaml` succeeds); both probes present; rolling update strategy present.
**Gate:** `kubectl apply --dry-run=client -f k8s/base/deployment.yaml`

---
status: pending
title: Add k8s/base/hpa.yaml — HPA
type: k8s
complexity: low
dependencies: T-10
---

**Layer:** K8s
**File:** `k8s/base/hpa.yaml`
**Reference:** spec Section B → HPA
**What:** Create `HorizontalPodAutoscaler` targeting the `api` deployment, `minReplicas: 2`, `maxReplicas: 10`, `metrics: [{type: Resource, resource: {name: cpu, target: {type: Utilization, averageUtilization: 70}}}]`. Use `autoscaling/v2` API version.
**Done when:** `kubectl apply --dry-run=client -f k8s/base/hpa.yaml` succeeds; API version is `autoscaling/v2`.
**Gate:** `kubectl apply --dry-run=client -f k8s/base/hpa.yaml`

---
status: pending
title: Add k8s/base/configmap.yaml
type: k8s
complexity: low
dependencies: none
---

**Layer:** K8s
**File:** `k8s/base/configmap.yaml`
**Reference:** spec Section B → ConfigMap
**What:** Create a `ConfigMap` named `api-config` with data keys: `AWS__Region: "us-east-1"`, `AWS__DynamoDB__TableName: "rentifyx-identity-production"`, `OTEL_SERVICE_NAME: "RentifyxIdentity.Api"`, `OTEL_EXPORTER_OTLP_ENDPOINT: ""` (populated per overlay), `OTEL_RESOURCE_ATTRIBUTES: "deployment.environment=production"`. Update `k8s/base/deployment.yaml` container env to use `envFrom: [configMapRef: {name: api-config}]` alongside any remaining inline env vars.
**Done when:** `kubectl apply --dry-run=client -f k8s/base/configmap.yaml` succeeds; deployment references the ConfigMap via `envFrom`.
**Gate:** `kubectl apply --dry-run=client -f k8s/base/configmap.yaml`

---
status: pending
title: Add k8s/base/secret-provider-class.yaml — AWS Secrets Store CSI
type: k8s
complexity: medium
dependencies: T-08
---

**Layer:** K8s
**File:** `k8s/base/secret-provider-class.yaml`
**Reference:** spec Section B → SecretProviderClass
**What:** Create a `SecretProviderClass` (`secrets-store.csi.x-k8s.io/v1`) named `api-secrets` with `provider: aws`. Declare three secret objects referencing the three Secrets Manager ARNs from the IAM module outputs (use placeholder ARN variables with inline comments). Add a `secretObjects` block that projects each secret as a K8s `Secret` entry. Update `k8s/base/deployment.yaml` to mount the CSI volume and populate env vars `Jwt__PrivateKeyPem`, `Hmac__Key`, and `Ses__FromAddress` from the projected secret.
**Done when:** YAML is syntactically valid; `SecretProviderClass` API version is `secrets-store.csi.x-k8s.io/v1`; deployment references the volume and env vars.
**Gate:** YAML lint / `kubectl apply --dry-run=client` (requires CRD to be present; dry-run with `--validate=false` is acceptable for study context)

---
status: pending
title: Update k8s/base/kustomization.yaml — add new resources
type: k8s
complexity: low
dependencies: T-11, T-12, T-13
---

**Layer:** K8s
**File:** `k8s/base/kustomization.yaml`
**Reference:** spec Section B → Prod Overlay Additions
**What:** Add `hpa.yaml`, `configmap.yaml`, and `secret-provider-class.yaml` to the `resources` list in `k8s/base/kustomization.yaml`.
**Done when:** `kustomize build k8s/base/` succeeds and outputs all five manifests (deployment, service, hpa, configmap, secret-provider-class).
**Gate:** `kustomize build k8s/base/`

---
status: pending
title: Update k8s/overlays/prod — HPA patch + IRSA annotation
type: k8s
complexity: low
dependencies: T-14
---

**Layer:** K8s
**File:** `k8s/overlays/prod/kustomization.yaml`
**Reference:** spec Section B → Prod Overlay Additions
**What:** In the prod overlay: patch `hpa.yaml` to set `minReplicas: 3`; add a patch on the `Deployment` `spec.template.metadata.annotations` to set `eks.amazonaws.com/role-arn: $(IAM_ROLE_ARN)` (placeholder comment explains the value comes from Terraform output). Fix `bases:` → `resources:` to use current Kustomize API.
**Done when:** `kustomize build k8s/overlays/prod/` succeeds; HPA shows `minReplicas: 3`; IRSA annotation present on pod template.
**Gate:** `kustomize build k8s/overlays/prod/`

---
status: pending
title: Update k8s/overlays/dev — reference configmap, no secret-provider-class
type: k8s
complexity: low
dependencies: T-14
---

**Layer:** K8s
**File:** `k8s/overlays/dev/kustomization.yaml`
**Reference:** spec Section B
**What:** In the dev overlay: patch `configmap.yaml` to override `OTEL_RESOURCE_ATTRIBUTES: "deployment.environment=development"` and `AWS__DynamoDB__TableName: "rentifyx-identity-development"`; add a strategic merge patch on the deployment to remove the CSI volume and replace secret env vars with literal placeholder values (LocalStack / dev config). Fix `bases:` → `resources:`.
**Done when:** `kustomize build k8s/overlays/dev/` succeeds; dev deployment has no SecretProviderClass reference; env values reflect development.
**Gate:** `kustomize build k8s/overlays/dev/`

---

## Section C — Observability

---
status: pending
title: Add Serilog.Sinks.AwsCloudWatch NuGet
type: backend
complexity: low
dependencies: none
---

**Layer:** App
**Files:** `Directory.Packages.props`, `02-src/01-Api/RentifyxIdentity.Api/RentifyxIdentity.Api.csproj`
**Reference:** spec Section C → Serilog
**What:** Add `<PackageVersion Include="Serilog.Sinks.AwsCloudWatch" Version="1.7.0" />` (or latest stable) to `Directory.Packages.props`. Add `<PackageReference Include="Serilog.Sinks.AwsCloudWatch" />` (no version) to the API `.csproj`. Run `dotnet restore` to verify the package resolves.
**Done when:** `dotnet build RentifyxIdentity.slnx --configuration Release` passes; the package appears in the restored assets.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release`

---
status: pending
title: Create appsettings.Production.json
type: backend
complexity: low
dependencies: T-17
---

**Layer:** App
**File:** `02-src/01-Api/RentifyxIdentity.Api/appsettings.Production.json`
**Reference:** spec Section C
**What:** Create `appsettings.Production.json` with:
- `Serilog` section: `WriteTo` array with `Console` and `AmazonCloudWatch` sinks (logGroup `/rentifyx/identity-api`, logStreamPrefix `production`, region `us-east-1`).
- OTel env-variable placeholders as empty strings (values overridden by K8s ConfigMap).
- `AllowedHosts: "*"`.
**Done when:** `dotnet build` passes; file is included in publish output (`<Content Include=...>` or default convention); Serilog config parses without error at startup (validate by running the API locally with `ASPNETCORE_ENVIRONMENT=Production` and confirming no Serilog startup exception).
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release`

---
status: pending
title: Write SLO definitions document
type: docs
complexity: low
dependencies: none
---

**Layer:** Docs
**File:** `docs/slo.md`
**Reference:** spec Section C → SLO Definitions
**What:** Create `docs/slo.md` with a table of four SLOs (Availability ≥ 99.9%, p99 latency < 500 ms, 5xx error rate < 0.1%, auth endpoint p99 < 300 ms), measurement windows, error budget calculation (43.8 min/month downtime budget), and a section describing how each SLO is measured (CloudWatch metrics, OTel spans) and the alerting threshold (alert at 50% budget burn in 1 hour).
**Done when:** File exists with all four SLOs defined, error budget calculated, measurement method described for each.
**Gate:** File presence and content review

---

## Section D — C4 Architecture Diagrams

---
status: pending
title: C4 Context diagram (L1)
type: docs
complexity: low
dependencies: none
---

**Layer:** Docs
**File:** `docs/architecture/c4-context.md`
**Reference:** spec Section D
**What:** Write a Mermaid `C4Context` diagram showing:
- **Person**: RentifyX User (tenant, owner, admin)
- **System**: RentifyX Identity API (this system)
- **External systems**: AWS Cognito (future MFA/social), AWS SES (email delivery), AWS DynamoDB (persistence), AWS Secrets Manager (secret storage), RentifyX Platform (consuming services that validate JWTs)
- Relationships between each actor/system with short labels (e.g. "registers, logs in, resets password").
**Done when:** Mermaid block renders correctly on GitHub (validate via GitHub preview or `mmdc` CLI); all external systems and the primary user are represented.
**Gate:** Content and Mermaid syntax review

---
status: pending
title: C4 Container diagram (L2)
type: docs
complexity: low
dependencies: none
---

**Layer:** Docs
**File:** `docs/architecture/c4-container.md`
**Reference:** spec Section D
**What:** Write a Mermaid `C4Container` diagram showing the deployable containers within the RentifyX Identity system boundary:
- **Container**: `RentifyxIdentity.Api` (.NET 10 Minimal API, runs in EKS pod)
- **Container**: `DynamoDB Table` (single-table, AWS managed)
- **Container**: `Cognito User Pool` (future MFA, AWS managed)
- **Container**: `SES` (email delivery, AWS managed)
- **Container**: `Secrets Manager` (secrets at runtime, AWS managed)
- **Container**: `KMS` (TaxId encryption key, AWS managed)
- Relationships: API reads/writes DynamoDB; API sends email via SES; API loads secrets from Secrets Manager at startup; API encrypts TaxId via KMS (deferred, noted).
**Done when:** Mermaid block renders; all six containers present; relationships labelled with protocol/method.
**Gate:** Content and Mermaid syntax review

---
status: pending
title: C4 Component diagram (L3)
type: docs
complexity: medium
dependencies: none
---

**Layer:** Docs
**File:** `docs/architecture/c4-component.md`
**Reference:** spec Section D
**What:** Write a Mermaid `C4Component` diagram showing the internal components of `RentifyxIdentity.Api`:
- **Component**: Auth Endpoints (Register, Login, VerifyEmail, RefreshToken, Logout, ForgotPassword, ResetPassword)
- **Component**: User Endpoints (GetProfile, DeleteAccount, ExportData)
- **Component**: Security Middlewares (CorrelationId, SecurityHeaders, GlobalExceptionHandler)
- **Component**: Auth Handlers (one box per use case or grouped)
- **Component**: UserRepository (DynamoDB adapter)
- **Component**: TokenService (RS256 JWT + HMAC)
- **Component**: EmailService (SES v2)
- **Component**: AuditLogService (DynamoDB append-only)
- Relationships showing the request flow: Endpoint → Handler → Repository / Services.
**Done when:** Mermaid block renders; all major components present; relationships reflect Clean Architecture layer boundaries (no Domain component depends on Infrastructure).
**Gate:** Content and Mermaid syntax review

---

## Section E — v1.0.0 Release

---
status: pending
title: Write runbook
type: docs
complexity: medium
dependencies: none
---

**Layer:** Docs
**File:** `docs/runbook.md`
**Reference:** spec Section E → Runbook
**What:** Create `docs/runbook.md` covering:
1. **Deployment**: `kustomize build k8s/overlays/prod | kubectl apply -f -`; verify rollout with `kubectl rollout status deployment/api -n prod`.
2. **Rollback**: `kubectl rollout undo deployment/api -n prod`; verify with `kubectl rollout status`.
3. **Secrets rotation**: steps to update Secrets Manager value; trigger pod restart via `kubectl rollout restart deployment/api`.
4. **Health check**: `curl https://<endpoint>/health/ready` and `/health/live` expected responses.
5. **Known failure modes**: DynamoDB throttling (PAY_PER_REQUEST, should not throttle), SES rate limits (max send rate, back-off), Secrets Manager cold-start latency, CloudWatch Logs ingestion delay.
6. **Escalation contacts**: placeholder names and channels.
7. **CloudWatch dashboard URL pattern**: `https://console.aws.amazon.com/cloudwatch/home?region=us-east-1#dashboards:name=rentifyx-identity`.
**Done when:** All seven sections present; commands are copy-paste ready; no placeholder text other than the explicitly noted contact names and dashboard URL.
**Gate:** Content review

---
status: pending
title: Update STATE.md and ROADMAP.md for v1.0.0
type: docs
complexity: low
dependencies: T-01–T-23
---

**Layer:** Docs
**Files:** `.specs/project/STATE.md`, `.specs/project/ROADMAP.md`
**What:** Update `STATE.md`: set `Last Updated` to release date, update `Current Work` to "v1.0.0 released", add E-06 to the Feature Completion Log. Update `ROADMAP.md`: set `Current Milestone` to "v1.0.0 — Released", mark all M6 features as `COMPLETE ✅`.
**Done when:** Both files reflect the completed state; no milestone remains marked as `PLANNED` or `In Progress`.
**Gate:** File content review

---
status: pending
title: Create GitHub Release and git tag v1.0.0
type: release
complexity: low
dependencies: T-24
---

**Layer:** Release
**What:** On `main` after all E-06 PRs are merged and CI is green: create git tag `v1.0.0`; create GitHub Release via `gh release create v1.0.0` with release notes covering all delivered epics (E-01 through E-06), known limitations (Cognito auth flows deferred, TaxId KMS deferred, OWASP ZAP deferred), and the link to the runbook.
**Done when:** `git tag v1.0.0` exists on `main`; GitHub Release is visible at `github.com/<repo>/releases/tag/v1.0.0`; release notes include the known limitations section.
**Gate:** `gh release view v1.0.0`
