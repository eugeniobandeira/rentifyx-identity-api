# Project State

## Last Updated

2026-06-24

## Current Work

`login` — complete. Next: refresh-token endpoint or CI gates (T-018/T-019/T-020).

## Decisions

| ID | Decision | Rationale | Date |
|---|---|---|---|
| D-001 | ErrorOr<T> over exceptions for handler results | Explicit error modeling without exception overhead; maps cleanly to HTTP status codes | 2026-06-21 |
| D-002 | TaxId (CPF/CNPJ) detected by digit count only, no mod-11 | Study project scope — length-only detection (11 digits = CPF, 14 = CNPJ) is sufficient; mod-11 algorithm removed from both VO and validator | 2026-06-24 |
| D-003 | DynamoDB single-table design | ADR-005; schema-less, pay-per-use, no migration overhead | 2026-06-21 |
| D-004 | Cognito for JWT issuance, not custom JWT | ADR-006; managed key rotation, no custom signing key ops | 2026-06-21 |
| D-005 | Refresh tokens stored as HMAC-SHA256 hash | Raw token only transmitted over HTTPS, never persisted | 2026-06-21 |
| D-006 | Soft delete + PII anonymization for account erasure | LGPD Art. 18 VI — hard delete breaks audit trails | 2026-06-21 |
| D-007 | Everything in English | User preference — no Portuguese in code, docs, or specs | 2026-06-21 |
| D-008 | Enums always stored as string values in DynamoDB, never as integers | Readability in DB and avoid int/value drift bugs; applies to UserRole and UserStatus | 2026-06-21 |
| D-009 | `ct` as CancellationToken parameter name in our own interfaces | Shorter, less noise in method signatures. Constraint: methods overriding a base interface (IRepository<T>, IHandler<,>) must keep `cancellationToken` to satisfy CA1725/S927 | 2026-06-24 |
| D-010 | TaxId stored as plaintext for now | KMS encryption + HMAC blind index deferred to E-04 (DynamoDB wiring epic); acceptable for a study project in local/dev stage | 2026-06-24 |

## Blockers

_None active._

## Deferred Ideas

| ID | Idea | Deferred until |
|---|---|---|
| DEF-001 | Social login (OAuth — Google, Facebook) | Post-v1 |
| DEF-002 | MFA / 2FA | Post-v1 |
| DEF-003 | Granular RBAC beyond Owner/Renter/Admin | Post-v1 |
| DEF-004 | Rate limiting per-user lockout state (5 failed logins → 15-min lockout) | E-04 (DynamoDB wiring) |
| DEF-005 | Domain event dispatch via Outbox pattern | E-04 |
| DEF-006 | LGPD export: consent records and login history | Confirm scope with team before E-05 |
| DEF-007 | TaxId KMS encryption + HMAC blind index for secure search | E-04 (DynamoDB wiring) |

## Lessons Learned

| ID | Lesson | Context |
|---|---|---|
| L-001 | Never use `replace_all: true` on strings ≤ 3 characters — it corrupts unrelated identifiers (e.g., replacing "ct" rewrote `ValueObjects` → `ValueObjecancellationTokens`, `Conflict` → `ConflicancellationToken`). Always use targeted single-occurrence edits. | Happened during CancellationToken → ct rename across handler and repository files |
| L-002 | After adding a NuGet package, Debug builds may still fail with CS0234 due to stale NuGet cache. Run `dotnet clean` on the affected project before rebuilding. Release builds were unaffected. | Happened after adding `Microsoft.Extensions.Configuration.Abstractions` to Application project |

## Preferences

- All output (code, docs, specs, comments) must be in English
- No hardcoded values in tests — always use constants (e.g., `TestConstants`) and Bogus builders (e.g., `RegisterUserRequestBuilder`)

## Feature Completion Log

| Feature | Tasks | Tests | Completed |
|---|---|---|---|
| register-user | T1–T18 (18/18) | 52 (14 validators + 32 handlers + 6 integration) | 2026-06-24 |
| verify-email | T-01–T-14 (14/14) | 16 (4 validators + 9 handlers + 3 integration) | 2026-06-27 |
| login | T-01–T-12 (12/12) | 17 (4 validators + 7 handlers + 3 integration + builder) | 2026-06-27 |
