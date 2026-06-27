# External Integrations

> **Status note:** All AWS integrations are planned/stubbed. Real wiring happens in E-04 (Week 4). LocalStack is used for local development. Interfaces are defined in Domain; stubs live in Infrastructure.

## Identity & Authentication

**Service:** AWS Cognito
**Purpose:** JWT access token issuance, RSA-2048 key management, token validation.
**Interface:** `Domain/Interfaces/Users/ITokenService.cs`
**Implementation:** `Infrastructure/Services/TokenService.cs` (stub → E-04)
**Configuration:** Cognito User Pool ID and App Client ID via AWS Secrets Manager
**Auth:** IAM role / service account (no hardcoded credentials)
**Key operations:**
- `GenerateAccessToken(UserEntity)` → signed JWT (15 min TTL, RSA-2048)
- `GenerateRefreshToken()` → random bytes (7d TTL)
- `HashToken(string)` → HMAC-SHA256 hash for storage
- `ValidateRefreshToken(hash, token)` → constant-time comparison

**ADR:** `docs/decisions/006-cognito-vs-custom-jwt.md`

## Email

**Service:** AWS SES (Simple Email Service)
**Purpose:** Transactional email — email verification and password reset links.
**Interface:** `Domain/Interfaces/Users/IEmailService.cs`
**Implementation:** `Infrastructure/Services/EmailService.cs` (stub → E-04)
**Configuration:** SES region, sender address via AWS Secrets Manager
**Key operations:**
- `SendVerificationEmailAsync(to, token, ct)` — 24h HMAC token link
- `SendPasswordResetEmailAsync(to, token, ct)` — 1h HMAC token link

## Database

**Service:** AWS DynamoDB
**Purpose:** Single-table persistence for User aggregate and Refresh Token items.
**Interface:** `Domain/Interfaces/Users/IUserRepository.cs` (extends `IRepository<UserEntity>`)
**Implementation:** `Infrastructure/Repositories/UserRepository.cs` (stub → E-04)
**Configuration:** Table name, endpoint (LocalStack locally) via `appsettings.json` + Secrets Manager
**Design:** Single-table (ADR-005); GSIs for email and TaxId lookups
**Local dev:** LocalStack (Docker) simulates DynamoDB — started via Aspire AppHost
**Key operations:**
- `AddAsync(UserEntity)` — PK: `USER#{id}`, SK: `PROFILE`
- `GetByIdAsync(Guid)` — direct key lookup
- `GetByEmailAsync(string)` — GSI: `email-index`
- `GetByTaxIdAsync(string)` — GSI: `taxid-index` (stores encrypted value)
- `UpdateAsync(UserEntity)` — conditional write
- `DeleteAsync(UserEntity)` — physical delete (not used; soft-delete via `Anonymize()`)

**ADR:** `docs/decisions/005-dynamodb-single-table-design.md`

## Secrets Management

**Service:** AWS Secrets Manager
**Purpose:** Runtime injection of all sensitive config — never in `appsettings.json` or env vars.
**Integration:** Loaded at startup via AWS SDK into `IConfiguration` pipeline
**Secrets managed:**
- Cognito User Pool ID / App Client ID
- SES sender address / region
- OpenAPI contact info
- HMAC signing key (for verification and reset tokens)

**ADR:** `docs/decisions/001-secrets-manager-over-appsettings.md`

## Encryption at Rest

**Service:** AWS KMS (Key Management Service)
**Purpose:** Encrypt CPF/CNPJ (TaxId) before storing in DynamoDB.
**Interface:** (to be defined in E-04) — likely `IKmsService`
**Configuration:** KMS Key ARN via Secrets Manager
**Flow:** `TaxDocument` value object exposes raw digits for KMS encryption; `UserRepository` encrypts before write, decrypts after read
**Local dev:** LocalStack KMS

**ADR:** `docs/decisions/002-taxid-as-identity-field.md`

## Observability

**Service:** OpenTelemetry (OTLP exporter)
**Purpose:** Distributed tracing and metrics, exportable to any OTel-compatible backend (Grafana, Jaeger, etc.)
**Implementation:** Configured in `ServiceDefaults` via Aspire
**Instrumentation:**
- `OpenTelemetry.Instrumentation.AspNetCore` — HTTP request traces
- `OpenTelemetry.Instrumentation.Http` — outbound HTTP client traces
- `OpenTelemetry.Instrumentation.Runtime` — CLR metrics
**Local:** Aspire Dashboard shows traces + metrics during development

## CI/CD

**Service:** GitHub Actions
**Purpose:** Build, test, and security gate on every PR to `main`.
**File:** `.github/workflows/ci.yml`
**Pipeline:**
1. Secret scanning — gitleaks with `.gitleaks.toml`
2. Build (Release) — `dotnet build`
3. Test — `dotnet test`
**Planned gates (E-01):** Coverage ≥80%, OWASP dependency-check, Trivy container scan

## Git Hooks

**Tool:** gitleaks
**Purpose:** Pre-commit secret scanning — blocks commits with detected secrets.
**Files:** `.githooks/pre-commit`, `.gitleaks.toml`
**Activation:** `Directory.Build.props` sets `core.hooksPath = .githooks`
