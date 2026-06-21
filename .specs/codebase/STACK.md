# Tech Stack

**Analyzed:** 2026-06-21

## Core

- Framework: ASP.NET Core 10 — Minimal APIs
- Language: C# (LangVersion: latest)
- Runtime: .NET 10
- Package manager: NuGet — centralized via `Directory.Packages.props` (no version pinning per project)

## Backend

- API Style: REST, Minimal APIs (no controllers)
- Database: AWS DynamoDB (schema-less, single-table design) — LocalStack locally
- Authentication: AWS Cognito (JWT issuance) + custom refresh token logic
- Result type: ErrorOr 2.0.1 — all handlers return `ErrorOr<T>`
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
- Containers: Docker (LocalStack for AWS services)
- IaC: Terraform (Week 6 — not yet implemented)
- Kubernetes: Kustomize overlays (`k8s/base`, `k8s/overlays/dev`, `k8s/overlays/prod`)

## Testing

- Unit / Integration: xUnit 2.9.3
- Assertions: FluentAssertions 8.2.0
- Mocking: Moq 4.20.72
- Test data: Bogus 35.6.1 (fluent builder pattern)
- E2E: Microsoft.AspNetCore.Mvc.Testing 10.0.8 (`CustomWebApplicationFactory`)
- Repository tests: Testcontainers (LocalStack / DynamoDB — planned E-04)
- Coverage: coverlet.collector 6.0.4 — gate ≥ 80% (enforced in CI)
- Test SDK: Microsoft.NET.Test.Sdk 17.12.0

## External Services

- Identity: AWS Cognito
- Messaging / Email: AWS SES
- Secrets: AWS Secrets Manager
- Encryption: AWS KMS (CPF/CNPJ at rest)
- Storage: AWS DynamoDB

## Security & DevSecOps

- Secret scanning: gitleaks (pre-commit hook via `.githooks/` + CI)
- Container scanning: Trivy (planned CI gate)
- DAST: OWASP ZAP (planned Week 5)
- Dependency check: OWASP dependency-check (planned Week 1)
- Analyzers: SonarAnalyzer.CSharp (wired globally via `Directory.Build.props`)

## Development Tools

- CI/CD: GitHub Actions (`ci.yml` — triggers on PRs to `main`)
- Git hooks: `.githooks/pre-commit` (gitleaks)
- Build config: `Directory.Build.props` (`Nullable=enable`, `TreatWarningsAsErrors=true`)
- Solution format: `.slnx` (new SDK-style solution file)
