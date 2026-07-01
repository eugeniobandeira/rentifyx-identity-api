# Project State

## Last Updated

2026-06-30

## Current Work

v1.0.0 shipped. Post-release quality pass completed 2026-06-30. v1.1.0 in progress — login lockout (DEF-004) implemented. See ROADMAP.

## Decisions

| ID | Decision | Rationale | Date |
|---|---|---|---|
| D-001 | ErrorOr<T> over exceptions for handler results | Explicit error modeling without exception overhead; maps cleanly to HTTP status codes | 2026-06-21 |
| D-002 | TaxId (CPF/CNPJ) detected by digit count only, no mod-11 | Study project scope — length-only detection (11 digits = CPF, 14 = CNPJ) is sufficient; mod-11 algorithm removed from both VO and validator | 2026-06-24 |
| D-003 | DynamoDB single-table design | ADR-005; schema-less, pay-per-use, no migration overhead | 2026-06-21 |
| D-004 | Custom RS256 JWT for internal access tokens; Cognito for user-facing auth deferred to E-05 | ADR-006 hybrid model: identity-api issues short-lived RS256 JWTs (15 min) for service-to-service calls; Cognito handles MFA/social login for end users — not yet wired | 2026-06-28 |
| D-005 | Refresh tokens stored as HMAC-SHA256 hash | Raw token only transmitted over HTTPS, never persisted | 2026-06-21 |
| D-006 | Soft delete + PII anonymization for account erasure | LGPD Art. 18 VI — hard delete breaks audit trails | 2026-06-21 |
| D-007 | Everything in English | User preference — no Portuguese in code, docs, or specs | 2026-06-21 |
| D-008 | Enums always stored as string values in DynamoDB, never as integers | Readability in DB and avoid int/value drift bugs; applies to UserRole and UserStatus | 2026-06-21 |
| D-009 | `ct` as CancellationToken parameter name everywhere in own interfaces and implementations | Shorter, less noise. Applied to `IRepository<T>`, `IHandler<,>`, all handlers, endpoints, repositories, and fakes. External interfaces (e.g. `IExceptionHandler`) keep their declared name. | 2026-06-30 |
| D-010 | TaxId stored as plaintext for now | KMS encryption + HMAC blind index deferred to E-04 (DynamoDB wiring epic); acceptable for a study project in local/dev stage | 2026-06-24 |
| D-011 | Coverage gate excludes Example scaffold; Infrastructure stubs replaced in E-04 | Example files are living-pattern templates, not features. UserRepository/EmailService/TokenService stubs replaced with real AWS adapters in E-04 — coverage exclusions should be revisited. | 2026-06-28 |

## Blockers

_None active._

## Deferred Ideas

| ID | Idea | Deferred until |
|---|---|---|
| DEF-001 | Social login (OAuth — Google, Facebook) | Post-v1 |
| DEF-002 | MFA / 2FA | Post-v1 |
| DEF-003 | Granular RBAC beyond Owner/Renter/Admin | Post-v1 |
| DEF-004 | Rate limiting per-user lockout state (5 failed logins → 15-min lockout) | E-05 |
| DEF-005 | Domain event dispatch via Outbox pattern | E-05 |
| DEF-006 | LGPD export: consent records and login history | Confirm scope with team before E-05 |
| DEF-007 | TaxId KMS encryption + HMAC blind index for secure search | E-05 (Cognito/KMS epic) |

| D-012 | `UserRepository` uses `IDynamoDBContext` (high-level API), not `IAmazonDynamoDB` | Eliminates manual `Dictionary<string, AttributeValue>` construction; `SaveAsync`/`LoadAsync`/`DeleteAsync` are cleaner and less error-prone | 2026-06-30 |
| D-013 | `UserDynamoDbItem` GSI properties named in PascalCase with `[DynamoDBProperty]` for physical name | CA1707 forbids underscores in member names; `[DynamoDBProperty("GSI_Email_PK")]` preserves the DynamoDB attribute name | 2026-06-30 |
| D-014 | `ForgotPasswordHandler` delegates HMAC hashing to `ITokenService.HashToken()` | Eliminates duplicated HMAC-SHA256 logic and the security risk of a hardcoded `"dev-hmac-key"` fallback | 2026-06-30 |
| D-015 | `EmailService` validates `Ses:FromAddress` at construction time | Fail-fast pattern: invalid config surfaces at startup, not at the first email send | 2026-06-30 |
| D-016 | DynamoDB table requires SK as range key (`USER#{id}`) equal to PK | `[DynamoDBRangeKey("SK")]` on `UserDynamoDbItem` requires the table to define SK; enables future composite-key access patterns (e.g. audit log items on same table) | 2026-06-30 |
| D-017 | Login lockout state stored as `FailedLoginAttempts` (int) + `LockoutUntil` (DateTimeOffset?) on `UserEntity` | Co-locates lockout state with the user record; single `UpdateAsync` call; `LockoutUntilEpoch` (Unix seconds) mapped in `UserDynamoDbItem` for DynamoDB TTL auto-cleanup compatibility | 2026-06-30 |

## Lessons Learned

| ID | Lesson | Context |
|---|---|---|
| L-001 | Never use `replace_all: true` on strings ≤ 3 characters — it corrupts unrelated identifiers (e.g., replacing "ct" rewrote `ValueObjects` → `ValueObjecancellationTokens`, `Conflict` → `ConflicancellationToken`). Always use targeted single-occurrence edits. | Happened during CancellationToken → ct rename across handler and repository files |
| L-002 | After adding a NuGet package, Debug builds may still fail with CS0234 due to stale NuGet cache. Run `dotnet clean` on the affected project before rebuilding. Release builds were unaffected. | Happened after adding `Microsoft.Extensions.Configuration.Abstractions` to Application project |
| L-003 | `LocalStack.Client.Extensions` 2.0.0 requires `AWSSDK.Core >= 4.0.0.15`. Pinning any AWSSDK package to v3.x causes a restore conflict. All AWSSDK packages must be on v4.x when using LocalStack.Client.Extensions 2.x. | Surfaced in E-04 when initial versions were 3.7.x |
| L-004 | `Aspire.Hosting.AWS` 13.x is CDK-based and not compatible with the standard Aspire hosting model. The correct Aspire-compatible package is 9.3.1. | Pin to 9.3.1; do not follow the latest NuGet version for this package. |
| L-005 | `Testcontainers.LocalStack` 4.x deprecates the parameterless `LocalStackBuilder()` ctor. With `TreatWarningsAsErrors=true`, use `new LocalStackBuilder("localstack/localstack:latest")` instead. | Surfaced in E-04 repository integration tests. |
| L-006 | Singletons that require secrets at construction time (e.g., `TokenService` reading `Jwt:PrivateKeyPem`) will crash integration tests unless a `FakeTokenService` is registered in `CustomWebApplicationFactory`. The real service must be explicitly overridden — DI does not auto-substitute. | Caused 7 integration test failures at the end of E-04 until `FakeTokenService` was added. |

## Preferences

- All output (code, docs, specs, comments) must be in English
- No hardcoded values in tests — always use constants (e.g., `TestConstants`) and Bogus builders (e.g., `RegisterUserRequestBuilder`)

## Feature Completion Log

| Feature | Tasks | Tests | Completed |
|---|---|---|---|
| register-user | T1–T18 (18/18) | 52 (14 validators + 32 handlers + 6 integration) | 2026-06-24 |
| verify-email | T-01–T-14 (14/14) | 16 (4 validators + 9 handlers + 3 integration) | 2026-06-27 |
| login | T-01–T-12 (12/12) | 17 (4 validators + 7 handlers + 3 integration + builder) | 2026-06-27 |
| refresh-token | T-01–T-08 (8/8) | 15 (4 validators + 9 handlers + 2 integration + builder) | 2026-06-27 |
| logout | T-01–T-08 (8/8) | 11 (4 validators + 5 handlers + 2 integration) | 2026-06-27 |
| password-reset | T-01–T-14 (14/14) | 23 (7 validators + 13 handlers + 3 integration) | 2026-06-27 |
| ci-gates | T-018–T-020 (3/3) | — (CI-only; 95.6% line coverage verified locally) | 2026-06-27 |
| lgpd-endpoints | all tasks | 28 (6 validators + 14 handlers + 8 integration) | 2026-06-27 |
| aws-integration (E-04) | T01–T18 (18/18) | 6 unit (TokenService + EmailService) + 8 repository integration (Testcontainers/LocalStack) | 2026-06-28 |
| e05-security-lgpd (E-05) | T-01–T-29 (29/29) | 5 security header integration + 5 audit service unit + 6 audit handler unit + 3 LGPD integration audit + 2 consent validator + 1 consent integration + handler refactors (Register/VerifyEmail/ResetPassword) | 2026-06-29 |
| e06-iac-production (E-06) | T-01–T-25 (25/25) | 6 Terraform modules + root + backend · 7 K8s manifests + 2 overlays · appsettings.Production.json · docs/slo.md · 3 C4 diagrams · docs/runbook.md · git tag v1.0.0 | 2026-06-29 |
