# Architecture Overview — rentifyx-identity-api

## Goal

Production-grade Identity API for the RentifyX platform. Handles registration, email verification, authentication (JWT + Cognito), token refresh/revocation, password reset, and LGPD data-rights endpoints.

---

## Tech Stack

| Concern | Choice |
|---|---|
| Framework | .NET 10 · Minimal APIs |
| Architecture | Clean Architecture · DDD · TDD |
| Result type | `ErrorOr<T>` — no exceptions for control flow (see ADR-003) |
| Validation | FluentValidation 12.1.1 |
| Logging | Serilog (structured) + CorrelationId enrichment |
| Observability | OpenTelemetry traces + metrics via .NET Aspire ServiceDefaults |
| API docs | Scalar UI (`/scalar`) · OpenAPI 3.1 |
| Database | AWS DynamoDB (single-table design — see ADR-005) |
| Auth provider | AWS Cognito (user-facing) + custom JWT (internal — see ADR-006) |
| Email | AWS SES |
| Secrets | AWS Secrets Manager (see ADR-001) |
| Encryption | AWS KMS (CPF at rest — see ADR-002) |
| Events | Domain events → Kafka via Outbox Pattern (see ADR-004) |
| Local dev | .NET Aspire AppHost + LocalStack + cognito-local |
| IaC | Terraform (Week 6) |
| Container | Kubernetes / Helm + Kustomize overlays (`k8s/`) |
| CI/CD | GitHub Actions (PRs to `main` only) |
| Compliance | LGPD · OWASP Top 10 · BACEN · DevSecOps |

---

## Solution Structure

```
01-aspire/
  01-AppHost/             .NET Aspire orchestration (boots API + LocalStack + cognito-local)
  02-ServiceDefaults/     OTel, health checks, service discovery — shared by all services

02-src/
  01-Api/                 Minimal API layer
    Abstract/             IEndpoint interface
    Endpoints/            One file per endpoint (auto-registered via reflection)
    Extensions/           CORS, versioning, rate limiting, OpenAPI, ErrorOr → HTTP
    Middlewares/          CorrelationIdMiddleware, GlobalExceptionHandler

  02-Application/         Use-case layer
    Common/Handler/       IHandler<TRequest, TResponse> interface
    Common/Response/      ApiListResponse<T>
    Features/             One folder per domain feature
      {Feature}/
        {Action}Request.cs
        {Action}Validator.cs    (FluentValidation)
        {Action}Handler.cs      (implements IHandler)
        {Feature}Response.cs
        {Feature}Mapper.cs

  03-Domain/              Pure domain — zero framework / AWS references
    Entities/             Aggregate roots and entities (static factory, private setters)
    ValueObjects/         Email, CPF, Password (to be added in E-02)
    Events/               IDomainEvent, domain event records (to be added in E-02)
    Interfaces/           Repository and service contracts
    Constants/            Error codes, validation constants
    MessageResource/      Localized validation messages (.resx)

  04-IoC/                 DI wiring — the only layer that references all others
    ApplicationDependencyInjection.cs
    InfrastructureDependencyInjection.cs
    DependencyInjectionExtension.cs  (extension methods consumed by Program.cs)

  05-Infrastructure/      AWS SDK adapters, repository implementations
    Repositories/         DynamoDB repositories
    (planned) Messaging/  Outbox publisher → Kafka
    (planned) Secrets/    SecretsManagerConfigurationProvider
    (planned) Email/      SesEmailSender
    (planned) Auth/       CognitoTokenService

03-tests/
  01-Common/              Shared Bogus builders
  02-Validators/          FluentValidation unit tests (no I/O)
  03-Handlers/            Handler unit tests (Moq repositories)
  04-Repositories/        Repository integration tests (Testcontainers / LocalStack)
  05-Integration/         HTTP pipeline tests (CustomWebApplicationFactory)

docs/                     This documentation
iac/                      Terraform modules (Week 6)
k8s/                      Kustomize base + dev/prod overlays
```

---

## Dependency Flow

```
Api ──► Application ──► Domain ◄── Infrastructure
             ▲                           │
             └─────────── IoC ───────────┘
```

Rules:
- **Domain** — no outbound dependencies. Defines interfaces, not implementations.
- **Application** — depends on Domain interfaces only. Never touches Infrastructure or AWS SDK.
- **Infrastructure** — implements Domain interfaces. Owns all AWS SDK calls.
- **IoC** — sole layer that references all others. Wires everything at startup.
- **Api** — depends on Application (handlers via DI) and IoC (registers services).

---

## Domain Model (planned — E-02)

```
User (Aggregate Root)
  ├── Id            : Guid
  ├── Email         : Email (value object)
  ├── TaxId         : TaxDocument (value object — CPF or CNPJ, encrypted via KMS in DynamoDB)
  ├── PasswordHash  : Password (value object, OWASP A07 rules)
  ├── Role          : Owner | Renter | Admin
  ├── Status        : PendingVerification | Active | Suspended | Deleted
  └── CreatedAt     : DateTimeOffset

Domain Events raised by User:
  UserRegistered(UserId, Email, Role, OccurredAt)
  UserEmailVerified(UserId, OccurredAt)
  UserPasswordChanged(UserId, OccurredAt)
  UserSuspended(UserId, Reason, SuspendedBy, OccurredAt)
```

---

## Identity Use Cases (planned — E-03)

| Use Case | Endpoint | Handler |
|---|---|---|
| Register | `POST /v1/api/auth/register` | `RegisterUserHandler` |
| Verify email | `POST /v1/api/auth/verify-email` | `VerifyEmailHandler` |
| Login | `POST /v1/api/auth/login` | `LoginHandler` |
| Refresh token | `POST /v1/api/auth/refresh` | `RefreshTokenHandler` |
| Logout | `POST /v1/api/auth/logout` | `RevokeTokenHandler` |
| Forgot password | `POST /v1/api/auth/forgot-password` | `RequestPasswordResetHandler` |
| Reset password | `POST /v1/api/auth/reset-password` | `ConfirmPasswordResetHandler` |
| Get own profile | `GET /v1/api/users/me` | LGPD Art. 18 |
| Delete own account | `DELETE /v1/api/users/me` | LGPD Art. 18 (soft delete + anonymize) |
| Export own data | `GET /v1/api/users/me/data-export` | LGPD Art. 18 IV |

---

## AWS Infrastructure (planned — E-04, E-06)

```
┌─────────────────────────────────────────────────┐
│  EKS (k8s)                                       │
│  ┌──────────────────────────────────────────┐   │
│  │  rentifyx-identity-api (Deployment)       │   │
│  │  HPA: min 2 / max 10 replicas             │   │
│  │  /health/live  /health/ready              │   │
│  └──────────────┬───────────────────────────┘   │
└─────────────────┼───────────────────────────────┘
                  │
        ┌─────────┼────────────────────────────────┐
        │         │                                │
   DynamoDB    Cognito      SES        Secrets Mgr + KMS
  (users,      (MFA,       (email      (JWT key,
   tokens,      social)     templates)  Cognito secret)
   outbox,
   audit)
```

DynamoDB single-table layout:

| PK | SK / GSI | Purpose |
|---|---|---|
| `USER#{id}` | — | User record |
| GSI1: `EMAIL#{email}` | — | Lookup by email |
| GSI2: `TAXDOC#{taxdoc_hmac}` | — | Lookup by CPF or CNPJ |
| `TAXDOC#{taxdoc_hmac}` | — | Deduplication index (CPF or CNPJ) |
| `REFRESH#{token_hash}` | — | Refresh token (TTL 7d) |
| `OUTBOX#{id}` | — | Outbox messages |
| `AUDIT#{userId}` | `#{timestamp}` | Audit log entries |

---

## Security Controls

| Threat | Control |
|---|---|
| Secret leakage | AWS Secrets Manager at startup; gitleaks in CI pre-commit |
| Brute force login | Rate limiting: 5 failures → 15-min lockout (OWASP A07) |
| Token theft | Refresh token rotation (one-time use), stored as hash with TTL |
| PII at rest | TaxId (CPF/CNPJ) encrypted via KMS before DynamoDB write |
| Stale PII | DynamoDB TTL: unverified accounts 48h, refresh tokens 7d |
| OWASP A05 | GlobalExceptionHandler strips stack traces from responses |
| Oversized payloads | Request size limiting middleware |
| Clickjacking/XSS | Security headers: HSTS, X-Content-Type-Options, X-Frame-Options, CSP |

---

## Observability

- **Traces & metrics**: OpenTelemetry → OTLP exporter (configurable via Aspire dashboard locally)
- **Logs**: Serilog structured JSON; every log line carries `CorrelationId` and `RequestHost`
- **Custom OTEL metrics** (Week 6): `tokens_issued_total`, `login_failures_total`, `lockouts_total`
- **SLOs** (Week 6): `/login` p99 < 300ms, availability > 99.9%, error rate < 0.1%
- **Alerts** (Week 6): PagerDuty if error rate > 1% for 5 min

---

## CI/CD Pipeline

```
PR to main
  └─► Secret Scanning (gitleaks)
        └─► Build & Test (dotnet restore → build Release → test)
              └─► [planned] Coverage gate ≥80% (coverlet)
                    └─► [planned] OWASP dep-check (NuGet vuln scan)
                          └─► [planned] Trivy (container scan)
                                └─► merge → staging deploy
```
