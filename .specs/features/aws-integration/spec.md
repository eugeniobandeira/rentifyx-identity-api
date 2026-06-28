# Spec: E-04 — AWS Infrastructure Integration

**Status**: Approved
**Epic**: E-04 (Week 4, Days 14–18)
**Requirements**: ADR-005 (DynamoDB single-table), ADR-006 (custom JWT + Cognito hybrid)

## Summary

Replace all `NotImplementedException` stubs in the Infrastructure layer with real AWS SDK
implementations. Deliver: DynamoDB `UserRepository` (single-table, GSIs by email + taxId),
RS256 JWT `TokenService` (RSA-2048 key from Secrets Manager), SES v2 `EmailService`,
and a `SecretsManagerConfigurationProvider` that loads secrets into `IConfiguration` at
startup. Wire LocalStack in .NET Aspire AppHost for local development. Add
Testcontainers + LocalStack integration tests for the repository layer.

## Decisions Applied

- **D-003**: DynamoDB single-table design (`rentifyx-identity` table)
- **D-004 (updated)**: Custom RS256 JWT for access tokens; Cognito for user-facing flows deferred to E-05
- **D-008**: Enums stored as strings in DynamoDB
- **D-009**: `ct` parameter name in our own interfaces
- **D-010**: TaxId stored as plaintext (KMS deferred to E-06)
- **DEF-005**: Outbox pattern deferred — domain event dispatch remains a no-op log in E-04
- **DEF-007**: TaxId KMS + HMAC blind index deferred to E-06

## Scope Boundaries

IN scope:
- DynamoDB UserRepository (6 methods: Add, GetById, GetByEmail, GetByTaxId, Update, Delete)
- RS256 JWT TokenService (GenerateAccessToken, HashToken via Secrets Manager key)
- SES v2 EmailService (SendVerification, SendPasswordReset)
- SecretsManagerConfigurationProvider (loads Hmac:Key, Jwt:PrivateKeyPem, Ses:FromAddress)
- LocalStack container in AppHost + init script (table + GSIs + secret seed)
- JWT bearer middleware registration
- Testcontainers integration tests for UserRepository

OUT of scope (separate commit):
- Handler refactor removing IConfiguration from VerifyEmail/ResetPassword handlers
- TaxId KMS encryption
- Outbox pattern / domain event dispatch
- Cognito integration
- Rate-limiting lockout state
