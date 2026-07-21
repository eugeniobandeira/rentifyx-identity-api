# Roadmap

**Current Milestone:** post-v1.1.0 hardening (see `.specs/project/STATE.md` for the actively-maintained, up-to-date status — this file's milestone headers below predate several shipped epics and are not kept current)
**Last Released:** v1.1.0 ✅ (2026-06-30)

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

**Local Infrastructure** — DEFERRED to v1.1.0

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

**DynamoDB Persistence** — COMPLETE ✅

- `UserRepository` — `IDynamoDBContext` high-level API, single-table design (PK+SK composite), GSI for email and TaxId
- Refresh token stored as hash with TTL on `UserEntity`
- TaxId stored as plaintext (KMS deferred to v1.1.0 — D-010)

**AWS Service Adapters** — COMPLETE ✅

- `TokenService` — RS256 JWT (15 min), refresh token generation, HMAC-SHA256 hash/verify
- `EmailService` — SES v2 transactional email (verification + password reset)
- `SecretsManagerConfigurationProvider` — loads secrets at startup

**Outbox Pattern** — DEFERRED to v1.1.0 (DEF-005)

**Per-User Rate Limiting** — DEFERRED to v1.1.0 (DEF-004)

**Repository Integration Tests** — COMPLETE ✅

- Testcontainers + LocalStack DynamoDB, PK+SK composite table
- 8 tests: Add, GetById, GetByEmail, GetByTaxId, Update, Delete, TTL pending, TTL active

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

**Security Hardening** — COMPLETE ✅

- Security headers middleware (CSP, X-Frame-Options, X-Content-Type-Options, Referrer-Policy, Permissions-Policy)
- Handler HMAC refactor: Register/VerifyEmail/ResetPassword now delegate to `ITokenService.HashToken()` / `VerifyTokenHash()`
- JWT bearer middleware (Cognito JWKS) deferred to E-06

**LGPD Compliance** — COMPLETE ✅

- Audit log: `IAuditLogService` → `AuditLogService` (DynamoDB DataModel, PK=`AUDIT#{userId}#{ts}_{guid}`, TTL 90 days)
- `AuditEvents` constants: `PROFILE_ACCESSED`, `DATA_EXPORTED`, `ACCOUNT_DELETED`
- Consent tracking: `ConsentGiven` on request, `ConsentGivenAt` on `UserEntity`, `CONSENT_REQUIRED` validation

---

## M6 — IaC & Production Readiness

**Goal:** Terraform-managed AWS infrastructure, Helm/EKS deployment, SLOs defined, v1.0.0 tagged.
**Target:** Week 6 (Days 24–28) — E-06

### Features

**Infrastructure as Code** — COMPLETE ✅

- Terraform modules: `dynamodb` (table + 2 GSIs + TTL + PITR), `cognito` (user pool + app client), `ses` (identity + config set), `kms` (symmetric key + alias + rotation), `secrets` (3 Secrets Manager entries + lifecycle ignore_changes), `iam` (IRSA role + least-privilege policy)
- S3 + DynamoDB lock remote state backend

**Kubernetes / EKS** — COMPLETE ✅

- Kustomize base: deployment (rolling update maxSurge:1/maxUnavail:0, readiness + liveness probes), service, HPA (min:2 max:10 cpu:70%), ConfigMap, SecretProviderClass (AWS Secrets Store CSI)
- Dev overlay: ASPNETCORE_ENVIRONMENT=Development, ConfigMap override for dev table
- Prod overlay: replicas:3, IRSA annotation, CSI volume + secretKeyRef env vars, HPA min:3

**Observability** — COMPLETE ✅

- Serilog structured JSON to stdout via `RenderedCompactJsonFormatter`; Fluent Bit DaemonSet ships to CloudWatch (no SDK dependency in app)
- `appsettings.Production.json` with production log levels and AWS config
- SLO definitions: `docs/slo.md` — 4 SLOs, error budget policy, alert thresholds, CloudWatch dashboard spec

**C4 Architecture Diagrams** — COMPLETE ✅

- `docs/architecture/c4-context.md` — L1 Context (3 personas, 5 external systems)
- `docs/architecture/c4-container.md` — L2 Container (6 deployable units + DynamoDB data model table)
- `docs/architecture/c4-component.md` — L3 Component (11 internal components + layer dependency direction + key patterns)

**v1.0.0 Release** — COMPLETE ✅

- Coverage gate ≥ 80% verified in CI (95.6% line coverage)
- Runbook documented: `docs/runbook.md`
- Git tag `v1.0.0` on `main`

---

---

## v1.1.0 — Hardening & Deferred Infrastructure

**Goal:** Close the gaps identified in the post-v1.0.0 quality review: local dev experience, login security, domain event dispatch, TaxId encryption, and LGPD audit completeness.
**Status:** COMPLETE ✅ (2026-06-30)

### Features

**Aspire + LocalStack one-liner** _(from M1 — never delivered)_ — COMPLETE ✅

- LocalStack container wired in `AppHost` with `SERVICES=dynamodb,ses,secretsmanager,kms`
- `init-localstack.sh` creates DynamoDB table (PK+SK+2 GSIs+TTL) and seeds Secrets Manager on startup
- API waits for LocalStack via `.WaitFor(localstack)` before starting
- `.env.local.template` documents local AWS credential setup (LocalStack fake creds)

**Per-user login lockout** _(DEF-004)_ — COMPLETE ✅

- `FailedLoginAttempts` + `LockoutUntil` fields on `UserEntity`; `LockoutUntilEpoch` (Unix seconds) in DynamoDB for TTL auto-cleanup
- `RecordFailedLogin()` / `ClearLockout()` mutation methods; `IsLockedOut()` check in `LoginHandler`
- `User.LoginLocked` error code + 429 response via `Error.Custom(429, ...)`; `ToProblem()` extended for custom numeric HTTP codes
- 6 entity unit tests + 7 handler unit tests + 2 repository integration tests

**TaxId KMS encryption at rest** _(DEF-007 / D-010)_ — SKIPPED, deferred to post-v1.1.0

**Domain event Outbox** _(DEF-005)_ — **NOT DONE.** Despite sitting under this "COMPLETE ✅"
milestone header, this item was never implemented (confirmed via `STATE.md`: "Outbox (DEF-005) ...
deferred post-v1.1.0"). Full spec/design/tasks now live in
`.specs/features/outbox-kafka-notifications/`, in progress as of 2026-07-15.

- `OutboxEntry` DynamoDB item written atomically alongside the user item, via
  `TransactWriteItemsAsync` (not a plain `SaveAsync` call — `IDynamoDBContext.SaveAsync` cannot
  span two items atomically, confirmed during design)
- `OutboxPublisher` background service polls and dispatches to **Kafka** (not SNS/EventBridge —
  that was stale wording, inconsistent with ADR-004, which already targeted Kafka; corrected
  2026-07-15 to match both ADR-004 and `rentifyx-communications-api`'s Kafka-only architecture)
- Dead-letter handling: max 3 retries, then mark as `Failed`
- Integration tests: verify outbox entry created on `UserRegistered`

**LGPD audit completeness** _(DEF-006)_

- Include consent records and login history in `ExportData` response
- `LoginHistoryEntry` domain object (timestamp, IP, user-agent)
- Audit log query by userId range key prefix

---

## v1.2.0 — Post-Assessment Hardening & PF/PJ Support (Planned)

**Goal:** Close doc-drift and tracked gaps from the 2026-07-11 assessment; make PJ (business/CNPJ)
customers a first-class, intentionally modeled concept alongside PF (individual/CPF).
**Status:** Specced, not started. See `.specs/features/post-assessment-hardening/` and
`.specs/features/pf-pj-customer-support/`.

### Features

**Post-Assessment Hardening** — SPECCED

- CLAUDE.md refresh to match STATE.md/ROADMAP.md reality
- Commit `docs/api-contracts.md`; write missing `docs/guides/adding-a-new-feature.md`
- Split `LgpdEndpointTests.cs` into per-endpoint files
- Remove stale `coverlet.runsettings` exclusions; untrack `.csproj.user`
- Coverage polish on `PasswordHasher`/`CorrelationIdMiddleware`/`ErrorOrExtensions`/`OpenApiExtensions`
- LGPD consent view/revoke endpoint — **needs a `discuss` pass first** (does revoke suspend the account?)
- TaxId KMS encryption + HMAC blind index (DEF-007) — **needs a `design` pass first**, sequenced after PF/PJ support

**PF/PJ Customer Support** — SPECCED (new gap, D-018)

- `CustomerType` (Individual/Business) as an explicit field, not inferred from TaxId digit count
- `FullName` for PF; `CompanyLegalName` + `LegalRepresentativeName` for PJ
- Validator cross-checks declared type against TaxId format
- DynamoDB item + LGPD export/profile responses updated accordingly; existing records default to `Individual`

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
