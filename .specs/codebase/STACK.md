# Tech Stack

**Last reconciled:** 2026-07-21 (originally analyzed 2026-06-21 — kept stale for a month; see `.specs/project/STATE.md` for what's actively maintained)

## Core

- Framework: ASP.NET Core 10 — Minimal APIs
- Language: C# (LangVersion: latest)
- Runtime: .NET 10
- Package manager: NuGet — centralized via `Directory.Packages.props` (no version pinning per project)

## Backend

- API Style: REST, Minimal APIs (no controllers)
- Database: AWS DynamoDB (schema-less, single-table design) — real AWS in every environment including local dev (D-022, 2026-07-12); LocalStack only for automated repository integration tests
- Authentication: custom JWT RS256 (`TokenService`) + refresh token logic — not Cognito (D-004's Cognito plan was never wired into app code; Cognito exists only as an optional, unused Terraform module)
- Event publishing: Apache Kafka (Confluent.Kafka) — Outbox pattern, `IHostedService` publisher, PLAINTEXT against `rentifyx-platform`'s self-hosted broker (no SASL/IAM, since 2026-07-21)
- Result type: ErrorOr 2.1.1 — all handlers return `ErrorOr<T>`
- Validation: FluentValidation 12.1.1 + DI extension
- Versioning: Asp.Versioning.Http 8.1.0 — default v1, route-group based

## Observability

- Logging: Serilog 10.0.0 (Console + File sinks, structured, enriched with CorrelationId / MachineName / ThreadId)
- Distributed Tracing: OpenTelemetry 1.15.x (AspNetCore + Http + Runtime instrumentation, OTLP exporter)
- Metrics: OpenTelemetry (runtime metrics via `OpenTelemetry.Instrumentation.Runtime`)
- API Docs: Microsoft.AspNetCore.OpenApi 10.0.8 + Scalar.AspNetCore 2.14.14 (DeepSpace theme)

## Infrastructure & Orchestration

- Local orchestration: .NET Aspire 9.3.1 (AppHost + ServiceDefaults)
- Service discovery: Microsoft.Extensions.ServiceDiscovery 10.6.0
- Resilience: Microsoft.Extensions.Http.Resilience 10.6.0
- Containers: Docker (Testcontainers for repository test LocalStack/Kafka; local dev itself targets real AWS)
- IaC: Terraform (`iac/terraform/` — fully implemented: EC2, ECR, DynamoDB, KMS, Secrets Manager, SES, optional Cognito, GitHub Actions OIDC)
- Kubernetes: Kustomize overlays exist (`k8s/base`, `k8s/overlays/dev`, `k8s/overlays/prod`) but are not the current deploy path — real deploys go through the EC2 Terraform module + `deploy.yml`, not Kubernetes

## Testing

- Unit / Integration: xUnit 2.9.3
- Assertions: FluentAssertions 8.2.0
- Mocking: Moq 4.20.72
- Test data: Bogus 35.6.1 (fluent builder pattern)
- E2E: Microsoft.AspNetCore.Mvc.Testing 10.0.8 (`CustomWebApplicationFactory`)
- Repository tests: Testcontainers (LocalStack / DynamoDB + Kafka, `Category=RequiresDocker`)
- Coverage: coverlet.collector 6.0.4 — reported in CI as an artifact, no percentage gate
- Test SDK: Microsoft.NET.Test.Sdk 17.12.0

## External Services

- Auth: custom JWT RS256 (no external identity provider — Cognito module exists, unused by app code)
- Event publishing: Apache Kafka (self-hosted, `rentifyx-platform`) — notifications routed through `rentifyx-communications-api`, no direct email sending here
- Secrets: AWS Secrets Manager
- Storage: AWS DynamoDB
- Encryption: AWS KMS provisioned but unused (TaxId is plaintext today, DEF-007)

## Security & DevSecOps

- Secret scanning: gitleaks (pre-commit hook via `.githooks/` + CI)
- Container scanning: Trivy (real CI gate, `trivy-scan` job)
- Dependency check: OWASP dependency-check (real CI gate, `owasp-check` job)
- Analyzers: SonarAnalyzer.CSharp (wired globally via `Directory.Build.props`)

## Development Tools

- CI/CD: GitHub Actions (`ci.yml` — triggers on PRs to `main`)
- Git hooks: `.githooks/pre-commit` (gitleaks)
- Build config: `Directory.Build.props` (`Nullable=enable`, `TreatWarningsAsErrors=true`)
- Solution format: `.slnx` (new SDK-style solution file)
