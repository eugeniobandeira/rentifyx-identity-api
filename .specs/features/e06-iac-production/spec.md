# Spec: E-06 — IaC & Production Readiness

**Status**: Draft
**Epic**: E-06 (Week 6, Days 24–28)
**Requirements**: ADR-005, ADR-006, ADR-008 · ROADMAP M6

## Summary

E-06 delivers everything required to run the Identity API in production: Terraform modules for all AWS resources, complete Kubernetes manifests (probes, HPA, Secrets Store), an OTel + CloudWatch Logs observability stack, C4 architecture diagrams, and a v1.0.0 release. No new C# handler logic — this epic is entirely infrastructure and DevOps.

## Decisions Applied

- **D-003**: DynamoDB single-table (`rentifyx-identity`) — GSIs and TTL defined in Terraform
- **D-004**: Custom RS256 JWT; Cognito wired in Terraform (User Pool + App Client) but Cognito auth flows remain deferred post-v1.0.0
- **ADR-008**: Rolling update (max-surge 1, max-unavailable 0) for Kubernetes deployment
- **D-007**: All output in English

## Scope Boundaries

IN scope:
- Terraform root + 6 modules: `dynamodb`, `cognito`, `ses`, `kms`, `secrets`, `iam`
- S3 + DynamoDB backend for Terraform remote state
- Kubernetes manifests: health probes, HPA, ConfigMap, SecretProviderClass, updated overlays
- `appsettings.Production.json` wiring OTel OTLP exporter and CloudWatch Logs via Serilog
- SLO definitions document
- C4 Context, Container, and Component diagrams (Mermaid in `docs/architecture/`)
- Runbook (`docs/runbook.md`)
- v1.0.0 git tag

OUT of scope:
- Actual AWS deployment or `terraform apply` (study project — `plan` output is the artefact)
- Cognito login flows end-to-end (deferred post-v1.0.0)
- TaxId KMS encryption in the application layer (deferred post-v1.0.0 per D-010)
- OWASP ZAP DAST scan (requires a live environment)
- Social login, MFA, granular RBAC

---

## Section A — Terraform IaC

### Repository Structure

```
iac/
└── terraform/
    ├── backend.tf          — S3 + DynamoDB lock remote state
    ├── main.tf             — AWS provider, module calls
    ├── variables.tf        — environment, region, app_name, prefix inputs
    ├── outputs.tf          — table_name, kms_key_arn, cognito_pool_id, ses_identity
    └── modules/
        ├── dynamodb/       — table, GSIs, TTL, PITR
        ├── cognito/        — user pool, app client, RS256 token signing
        ├── ses/            — email identity, configuration set
        ├── kms/            — symmetric key for TaxId at-rest encryption
        ├── secrets/        — Secrets Manager entries (JWT key, HMAC key, SES from-address)
        └── iam/            — EKS pod identity role + least-privilege policy
```

### Module Contracts

#### `modules/dynamodb`

| Resource | Detail |
|---|---|
| Table | `${var.prefix}-identity`, `PAY_PER_REQUEST`, `REGIONAL` |
| PK | `PK` (S), SK `SK` (S) |
| GSI 1 | `GSI-Email` — PK: `Email` (S) |
| GSI 2 | `GSI-TaxId` — PK: `TaxId` (S) |
| TTL attribute | `TTL` |
| PITR | enabled |
| Encryption | `aws/dynamodb` CMK |

Inputs: `prefix`, `environment`
Outputs: `table_name`, `table_arn`

#### `modules/cognito`

| Resource | Detail |
|---|---|
| User Pool | password policy (12+ chars, upper, lower, digit, symbol), no self-signup |
| Token signing | RS256 |
| App client | non-web (server-to-server), `ALLOW_USER_PASSWORD_AUTH` disabled (tokens issued by identity-api) |

Inputs: `prefix`, `environment`, `ses_from_address`
Outputs: `user_pool_id`, `user_pool_arn`, `client_id`

#### `modules/ses`

| Resource | Detail |
|---|---|
| Email identity | domain or single address (configurable via variable) |
| Configuration set | suppression list, reputation tracking enabled |

Inputs: `ses_identity` (email or domain)
Outputs: `identity_arn`, `configuration_set_name`

#### `modules/kms`

| Resource | Detail |
|---|---|
| Key | symmetric `ENCRYPT_DECRYPT`, `ENABLED`, automatic rotation every 365 days |
| Alias | `alias/${var.prefix}-taxid` |

Inputs: `prefix`, `environment`
Outputs: `key_arn`, `key_id`

#### `modules/secrets`

Three Secrets Manager secrets:

| Secret name | Description |
|---|---|
| `${prefix}/jwt-private-key-pem` | RSA-2048 PEM (placeholder value at plan time) |
| `${prefix}/hmac-key` | 64-byte hex string (placeholder) |
| `${prefix}/ses-from-address` | verified SES sender address |

Inputs: `prefix`, `environment`, `kms_key_arn`
Outputs: `jwt_secret_arn`, `hmac_secret_arn`, `ses_secret_arn`

#### `modules/iam`

| Resource | Detail |
|---|---|
| Role | EKS IRSA trust policy (placeholder account/cluster values) |
| Policy | `dynamodb:GetItem`, `PutItem`, `UpdateItem`, `DeleteItem`, `Query` on the identity table and GSIs; `kms:Decrypt`, `Encrypt` on TaxId key; `secretsmanager:GetSecretValue` on the three secrets; `ses:SendEmail` |

Inputs: `prefix`, `table_arn`, `kms_key_arn`, `secret_arns` (list)
Outputs: `role_arn`, `policy_arn`

### Backend

```hcl
# backend.tf
terraform {
  backend "s3" {
    bucket         = "rentifyx-tfstate"
    key            = "identity-api/terraform.tfstate"
    region         = "us-east-1"
    dynamodb_table = "rentifyx-tflock"
    encrypt        = true
  }
}
```

The S3 bucket and DynamoDB lock table are bootstrapped manually (one-time; not managed by this Terraform root to avoid bootstrapping chicken-and-egg).

---

## Section B — Kubernetes Manifests

### Current State

`k8s/base/` has `deployment.yaml` (stub, no probes), `service.yaml`, `kustomization.yaml`.
`k8s/overlays/dev/` and `k8s/overlays/prod/` exist with minimal replica patches.

### Required Additions

#### Health Probes (`k8s/base/deployment.yaml` patch)

The API already exposes `/health/ready` and `/health/live` via .NET Aspire `ServiceDefaults` (`AddDefaultHealthChecks`).

```yaml
readinessProbe:
  httpGet:
    path: /health/ready
    port: 8080
  initialDelaySeconds: 5
  periodSeconds: 10
  failureThreshold: 3
livenessProbe:
  httpGet:
    path: /health/live
    port: 8080
  initialDelaySeconds: 15
  periodSeconds: 20
  failureThreshold: 3
```

#### HPA (`k8s/base/hpa.yaml`)

```
minReplicas: 2
maxReplicas: 10
targetCPUUtilizationPercentage: 70
```

#### ConfigMap (`k8s/base/configmap.yaml`)

Non-secret environment variables: `AWS__Region`, `AWS__DynamoDB__TableName`, `OTEL_SERVICE_NAME`, `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_RESOURCE_ATTRIBUTES`.

#### SecretProviderClass (`k8s/base/secret-provider-class.yaml`)

AWS Secrets Store CSI Driver object that mounts the three Secrets Manager secrets (`jwt-private-key-pem`, `hmac-key`, `ses-from-address`) as environment variables. References the IAM role via annotation.

#### Prod Overlay Additions

`k8s/overlays/prod/kustomization.yaml` must add `hpa.yaml` and `secret-provider-class.yaml` to resources and patch `minReplicas: 3`.

---

## Section C — Observability

### OTel — `appsettings.Production.json`

The API already has OTel wired via `.NET Aspire` `ServiceDefaults` (`AddOpenTelemetry`, `AddOtlpExporter`). Production config must point to a real exporter:

```json
{
  "OTEL_EXPORTER_OTLP_ENDPOINT": "",
  "OTEL_EXPORTER_OTLP_PROTOCOL": "http/protobuf",
  "OTEL_SERVICE_NAME": "RentifyxIdentity.Api",
  "OTEL_RESOURCE_ATTRIBUTES": "deployment.environment=production"
}
```

Values are empty placeholders — populated via K8s ConfigMap at runtime.

### Serilog → CloudWatch Logs

Add `Serilog.Sinks.AwsCloudWatch` to `RentifyxIdentity.Api.csproj`. Configure in `appsettings.Production.json`:

```json
"Serilog": {
  "WriteTo": [
    {
      "Name": "Console"
    },
    {
      "Name": "AmazonCloudWatch",
      "Args": {
        "logGroup": "/rentifyx/identity-api",
        "logStreamPrefix": "production",
        "region": "us-east-1"
      }
    }
  ]
}
```

### SLO Definitions (`docs/slo.md`)

| SLO | Target | Measurement window |
|---|---|---|
| Availability | ≥ 99.9% | 30-day rolling |
| p99 latency (all endpoints) | < 500 ms | 1-hour rolling |
| Error rate (5xx) | < 0.1% | 1-hour rolling |
| Auth endpoint p99 | < 300 ms | 1-hour rolling |

Error budget: 43.8 min downtime per 30 days.

---

## Section D — C4 Architecture Diagrams

Three diagrams in `docs/architecture/` as Mermaid fenced blocks inside Markdown files:

| File | Level | Scope |
|---|---|---|
| `c4-context.md` | Context (L1) | External actors and systems interacting with RentifyX Identity API |
| `c4-container.md` | Container (L2) | All deployable units: API, DynamoDB, Cognito, SES, Secrets Manager, KMS |
| `c4-component.md` | Component (L3) | Internal components of the Identity API: handlers, repositories, services, middlewares |

All diagrams use the `C4Context` / `C4Container` / `C4Component` Mermaid syntax (requires Mermaid ≥ 10.x, supported natively in GitHub).

---

## Section E — v1.0.0 Release

### Runbook (`docs/runbook.md`)

Covers: deployment steps, rollback procedure, secrets rotation, on-call escalation (placeholder contacts), CloudWatch dashboards URL pattern, known failure modes and mitigations.

### Release Checklist

- [ ] All E-01 through E-06 tasks complete
- [ ] CI pipeline green on `main` (build, tests, OWASP dep-check, Trivy)
- [ ] Coverage ≥ 80% verified
- [ ] `ROADMAP.md` and `STATE.md` updated to reflect v1.0.0
- [ ] Git tag `v1.0.0` created on `main`
- [ ] GitHub Release created with release notes

### Release Notes Content

- Summary of what the Identity API delivers (endpoints, LGPD compliance, security posture)
- Breaking changes: none (first release)
- Known limitations: Cognito auth flows deferred, TaxId KMS encryption deferred, OWASP ZAP scan deferred (no live environment)

---

## Open Questions

None blocking. All architectural decisions already recorded in ADRs 001–008.
