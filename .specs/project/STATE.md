# Project State

## Decisions

| ID | Decision | Rationale | Date |
|---|---|---|---|
| D-001 | ErrorOr<T> over exceptions for handler results | Explicit error modeling without exception overhead; maps cleanly to HTTP status codes | 2026-06-21 |
| D-002 | TaxId (CPF/CNPJ) as unique identity field | LGPD Art. 46 — prevents duplicate registrations; mod-11 validated at domain layer | 2026-06-21 |
| D-003 | DynamoDB single-table design | ADR-005; schema-less, pay-per-use, no migration overhead | 2026-06-21 |
| D-004 | Cognito for JWT issuance, not custom JWT | ADR-006; managed key rotation, no custom signing key ops | 2026-06-21 |
| D-005 | Refresh tokens stored as HMAC-SHA256 hash | Raw token only transmitted over HTTPS, never persisted | 2026-06-21 |
| D-006 | Soft delete + PII anonymization for account erasure | LGPD Art. 18 VI — hard delete breaks audit trails | 2026-06-21 |
| D-007 | Everything in English | User preference — no Portuguese in code, docs, or specs | 2026-06-21 |
| D-008 | Enums always stored as string values in DynamoDB, never as integers | Readability in DB and avoid int/value drift bugs; applies to UserRole and UserStatus | 2026-06-21 |

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

## Lessons Learned

_None yet._

## Preferences

- All output (code, docs, specs, comments) must be in English
