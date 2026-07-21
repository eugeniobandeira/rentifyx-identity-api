# External Integrations

> **Status note:** All integrations below are real and implemented, not stubs — this note previously said otherwise (was written at project inception, 2026-06-21, and never updated). Local dev targets real AWS (D-022, 2026-07-12) via a named credentials profile, not LocalStack — LocalStack is used only for automated repository integration tests (Testcontainers).

## Identity & Authentication

**Service:** Custom JWT (RS256) — not AWS Cognito. Cognito is provisioned as an optional Terraform module (`iac/terraform/modules/cognito`, `enable_cognito` flag) but is not consumed by any application code (D-004: hybrid model originally planned Cognito for user-facing auth, deferred to E-05, and E-05 shipped without ever wiring it in).
**Interface:** `Domain/Interfaces/Users/ITokenService.cs`
**Implementation:** `Infrastructure/Services/TokenService.cs` (real, signs with an RSA-2048 private key)
**Configuration:** JWT signing key (PEM) via AWS Secrets Manager
**Key operations:**
- `GenerateAccessToken(UserEntity)` → signed JWT (15 min TTL, RSA-2048)
- `GenerateRefreshToken()` → random bytes (7d TTL)
- `HashToken(string)` → HMAC-SHA256 hash for storage
- `ValidateRefreshToken(hash, token)` → constant-time comparison

## Email — no longer sent directly by this service

Direct SES sending (`IEmailService`/`EmailService`) was removed 2026-07-17 (D-014, T14 of the `outbox-kafka-notifications` feature). Handlers that need to notify a user (registration, password reset) now raise a domain event (`UserRegistered`, `PasswordResetRequested`) alongside their own DynamoDB write; `OutboxPublisher` (`IHostedService`, `Infrastructure/`) polls for pending outbox entries and produces them to Kafka as a `NotificationRequested` message, consumed by `rentifyx-communications-api`, which owns the actual SES send. See that repo's `docs/contracts/notification-requested.md` for the message schema.

## Database

**Service:** AWS DynamoDB
**Purpose:** Single-table persistence for the User aggregate, refresh tokens, and outbox entries.
**Interface:** `Domain/Interfaces/Users/IUserRepository.cs` (extends `IRepository<UserEntity>`), `Domain/Interfaces/Notifications/IOutboxRepository.cs`
**Implementation:** `Infrastructure/Repositories/{UserRepository,OutboxRepository}.cs` — real, `IDynamoDBContext`-based (not `IAmazonDynamoDB` raw calls — see D-012)
**Configuration:** Table name via `appsettings.json` + Secrets Manager; real AWS DynamoDB in every environment (local dev included, per D-022) — no LocalStack outside automated repository tests
**Design:** Single-table (ADR-005); GSIs for email lookup, TaxId lookup, and the outbox's pending-entry query
**Key operations:**
- `AddAsync(UserEntity)` — PK: `USER#{id}`, SK: `PROFILE` — writes the user item and its outbox entries in one `TransactWriteItemsAsync` (T7)
- `GetByIdAsync(Guid)` — direct key lookup
- `GetByEmailAsync(string)` — GSI lookup
- `GetByTaxIdAsync(string)` — GSI lookup (TaxId stored as plaintext, D-010 — KMS encryption deferred, DEF-007)
- `UpdateAsync(UserEntity)` — conditional write
- `DeleteAsync(UserEntity)` — physical delete (not used; soft-delete via `Anonymize()`)

**ADR:** `docs/decisions/005-dynamodb-single-table-design.md`

## Secrets Management

**Service:** AWS Secrets Manager
**Purpose:** Runtime injection of all sensitive config — never in `appsettings.json` or env vars.
**Integration:** Loaded at startup via AWS SDK into `IConfiguration` pipeline
**Secrets managed:**
- JWT RS256 private key (PEM) — for `TokenService`
- HMAC signing key (for verification and reset tokens)
- OpenAPI contact info

**ADR:** `docs/decisions/001-secrets-manager-over-appsettings.md`

## Encryption at Rest — deferred (DEF-007)

TaxId (CPF/CNPJ) is currently stored as **plaintext** in DynamoDB (D-010) — KMS encryption + an HMAC blind index for secure search was scoped for E-04 but explicitly deferred post-v1.1.0 (DEF-007), acceptable for a study project at this stage. No `IKmsService`/KMS integration exists in application code today, even though `iac/terraform/modules/kms` provisions a KMS key (currently unused by any encrypt/decrypt call).

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
4. OWASP dependency-check
5. Trivy container scan

Coverage is collected and reported (`coverlet.collector`) but not gated on a percentage (gate removed 2026-07-21) — CI only blocks on a failing test.

## Git Hooks

**Tool:** gitleaks
**Purpose:** Pre-commit secret scanning — blocks commits with detected secrets.
**Files:** `.githooks/pre-commit`, `.gitleaks.toml`
**Activation:** `Directory.Build.props` sets `core.hooksPath = .githooks`
