# Roadmap

**Current Milestone:** M4 — Infrastructure & AWS Integration
**Status:** In Progress

---

## M1 — Project Foundation & DevSecOps

**Goal:** Runnable scaffold with CI security gates, LocalStack, and Aspire dashboard — nothing blocks feature development.
**Target:** Week 1 (Days 1–3) — E-01

### Features

**Project Scaffold** — COMPLETE

- .NET 10 solution structure (5-layer Clean Architecture + Aspire)
- `Directory.Build.props` with `Nullable=enable`, `TreatWarningsAsErrors=true`, SonarAnalyzer
- Centralized NuGet versions via `Directory.Packages.props`
- `.slnx` solution file with all projects referenced
- Reference implementation (`Example*`) as living pattern guide

**CI / Security Gates** — COMPLETE ✅

- GitHub Actions pipeline on PRs: gitleaks secret scan + build + test
- Pre-commit gitleaks hook via `.githooks/`
- Coverage gate ≥ 80% (`coverlet.collector` → `reportgenerator` merge → bash threshold check) — **95.6% line coverage**
- OWASP dependency-check (`dependency-check/Dependency-Check_Action`, fail on CVSS ≥ 7)
- Trivy container scan (`aquasecurity/trivy-action`, CRITICAL/HIGH, ignore-unfixed)

**Local Infrastructure** — PLANNED

- LocalStack via Docker (DynamoDB, SES, Cognito, KMS, Secrets Manager)
- Aspire AppHost wires API + LocalStack for `dotnet run` one-liner
- `.env.local` or Aspire secrets for local AWS config

---

## M2 — Domain Model & Core Identity Logic

**Goal:** User aggregate fully modelled, all value objects validated, domain events defined, 100% unit tested — no infrastructure dependency.
**Target:** Week 2 (Days 4–8) — E-02

### Features

**User Aggregate** — COMPLETE ✅

- `UserEntity` with `Id`, `Email`, `TaxId`, `PasswordHash`, `Role`, `Status`, `CreatedAt`
- Token fields: `EmailVerificationTokenHash/Expiry`, `PasswordResetTokenHash/Expiry`
- Status state machine: `PendingVerification → Active → Suspended | Deleted`
- Factory `Create(...)` + mutation methods (`VerifyEmail`, `ResetPassword`, `Suspend`, `Anonymize`)

**Value Objects** — COMPLETE ✅

- `Email` — RFC format validation, disposable-domain rejection, normalized lowercase
- `TaxDocument` — CPF (11 digits) and CNPJ (14 digits), length-only detection (mod-11 removed — D-002), masked `ToString()`
- `Password` — OWASP complexity (12+ chars, upper/lower/digit/symbol), BCrypt hash, `[REDACTED]` `ToString()`

**Domain Events** — COMPLETE ✅

- `UserRegistered` ✅
- `UserEmailVerified` ✅
- `UserLoggedIn` ✅
- `UserPasswordChanged` ✅
- `UserSuspended` ✅
- `UserAccountDeleted` ✅

**Domain Contracts** — COMPLETE ✅

- `IUserRepository` (extends `IRepository<UserEntity>` + `GetByEmailAsync`, `GetByTaxIdAsync`) ✅
- `IEmailService` (verification email, password reset email) ✅
- `ITokenService` (access JWT, refresh token, HMAC hash, verify) ✅
- `IPasswordHasher` ✅
- `UserErrorCodes`, `ValidationConstants.UserRules`, message resources ✅

---

## M3 — Application Layer (Use Cases)

**Goal:** All 10 identity use-case handlers implemented, validated, and unit tested against mocked dependencies.
**Target:** Week 3 (Days 9–13) — E-03

### Features

**Auth Use Cases** — COMPLETE ✅

- `RegisterUser` ✅ — duplicate email/TaxId detection, HMAC verification token, `UserRegistered` event logged, 52 tests
- `VerifyEmail` ✅ — HMAC token validation, 24h expiry, single-use, `Active` status transition, 16 tests
- `Login` ✅ — credential verification, no-enumeration error, access JWT + refresh token issuance, 17 tests
- `RefreshToken` ✅ — one-time-use rotation, replay protection, 15 tests
- `Logout` ✅ — refresh token revocation (idempotent), 11 tests
- `ForgotPassword` ✅ — blind success (no enumeration), 1h reset token, email dispatch, 23 tests
- `ResetPassword` ✅ — HMAC token validation, password update, `UserPasswordChanged` event, 23 tests

**User Use Cases (LGPD)** — COMPLETE ✅

- `GetProfile` ✅ (Art. 18) — return own profile from JWT claim
- `DeleteAccount` ✅ (Art. 18 VI) — soft delete + PII anonymization
- `ExportData` ✅ (Art. 18 IV) — full data export with masked TaxId

---

## M4 — Infrastructure & AWS Integration

**Goal:** Real DynamoDB, Cognito, SES, KMS, and Secrets Manager wired end-to-end. Repository integration tests green via Testcontainers + LocalStack.
**Target:** Week 4 (Days 14–18) — E-04

### Features

**DynamoDB Persistence** — PLANNED

- `UserRepository` — single-table design, GSI for email and TaxId lookups
- Refresh token as separate DynamoDB item with TTL
- KMS encryption/decryption for CPF/CNPJ on write/read

**AWS Service Adapters** — PLANNED

- `TokenService` — Cognito JWT issuance, RSA-2048 key, access token 15 min TTL
- `EmailService` — SES transactional email with HMAC token links
- `IKmsService` — TaxId encryption at rest

**Outbox Pattern** — PLANNED

- Domain events (`UserRegistered`, etc.) dispatched via DynamoDB Streams or SNS Outbox
- Ensures at-least-once delivery without two-phase commit

**Per-User Rate Limiting** — PLANNED

- 5 failed logins → 15-min lockout stored as DynamoDB item with TTL

**Repository Integration Tests** — PLANNED

- Testcontainers + LocalStack DynamoDB
- Tests: `AddAsync`, `GetByIdAsync`, `GetByEmailAsync`, `GetByTaxIdAsync`, `UpdateAsync`
- Verify KMS encryption roundtrip, refresh token TTL behavior

---

## M5 — API Layer, Security Hardening & LGPD

**Goal:** All 10 endpoints live, OWASP-hardened, LGPD rights exercisable via API, E2E tests green.
**Target:** Week 5 (Days 19–23) — E-05

### Features

**Auth Endpoints (public)** — COMPLETE ✅ (stubs — infrastructure throws NotImplementedException until E-04)

- `POST /api/v1/auth/register` → 201 ✅
- `POST /api/v1/auth/verify-email` → 200 ✅
- `POST /api/v1/auth/login` → 200 ✅ (JWT + refresh)
- `POST /api/v1/auth/refresh` → 200 ✅
- `POST /api/v1/auth/logout` → 204 ✅
- `POST /api/v1/auth/forgot-password` → 204 ✅
- `POST /api/v1/auth/reset-password` → 200 ✅

**User Endpoints (authenticated)** — COMPLETE ✅ (stubs — infrastructure throws NotImplementedException until E-04)

- `GET /api/v1/users/me` → 200 ✅ (Art. 18)
- `DELETE /api/v1/users/me` → 204 ✅ (Art. 18 VI)
- `GET /api/v1/users/me/data-export` → 200 ✅ (Art. 18 IV)

**Security Hardening** — PLANNED

- JWT bearer middleware (Cognito JWKS validation)
- HTTPS redirect in production
- CORS policy locked to known origins
- OWASP ZAP DAST scan — zero high/critical findings required
- Security headers (Content-Security-Policy, X-Frame-Options, etc.)

**LGPD Compliance** — PLANNED

- Audit log for data access, export, and erasure requests
- Consent tracking (capture + timestamp at registration)
- BACEN guidelines applied to PII handling

---

## M6 — IaC & Production Readiness

**Goal:** Terraform-managed AWS infrastructure, Helm/EKS deployment, SLOs defined, v1.0.0 tagged.
**Target:** Week 6 (Days 24–28) — E-06

### Features

**Infrastructure as Code** — PLANNED

- Terraform modules: DynamoDB table + GSIs + TTL, Cognito User Pool, SES identity, KMS key, Secrets Manager secrets, IAM roles
- Remote state (S3 + DynamoDB lock)

**Kubernetes / EKS** — PLANNED

- Helm chart or Kustomize overlays (`k8s/overlays/prod`)
- Rolling update strategy (ADR-008)
- Readiness / liveness probes via `/health`
- HPA (Horizontal Pod Autoscaler) config

**Observability** — PLANNED

- OTel traces and metrics exported to production backend (Grafana / CloudWatch)
- Serilog → CloudWatch Logs in production
- SLO definitions: p99 latency < 500ms, error rate < 0.1%, uptime > 99.9%

**C4 Architecture Diagrams** — PLANNED

- Context, Container, and Component diagrams

**v1.0.0 Release** — PLANNED

- All OWASP ZAP findings resolved
- Coverage gate green in CI
- Runbook documented
- Git tag `v1.0.0`

---

## Future Considerations

- Social login (OAuth — Google, GitHub, Apple)
- MFA / 2FA (TOTP or SMS)
- Granular RBAC beyond `Owner` / `Renter` / `Admin`
- Multi-tenancy (marketplace operator isolation)
- Account linking (merge social + email accounts)
- Subscription / billing identity integration
- Admin panel for user management and audit logs
- LGPD consent versioning and re-consent flows
