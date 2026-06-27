# ADR-005: Single-table DynamoDB design

- **Date:** 2026-06-21
- **Status:** Accepted

## Context

The identity service needs to store user records, refresh tokens, outbox messages, audit log entries, and consent records. DynamoDB can be used with a multi-table design (one table per entity type) or a single-table design (all entity types in one table, differentiated by key prefixes).

## Options Considered

- **Option A — Multi-table design**: One DynamoDB table per entity type (`users`, `refresh_tokens`, `outbox`, `audit_log`, `consents`). Familiar to developers coming from relational databases. But requires more IAM policies, more Terraform resources, and more boto3/SDK calls for cross-entity transactions.
- **Option B — Single-table design**: All entities in one table, distinguished by PK/SK prefixes. Enables atomic transactions across entity types (e.g., write user + outbox message in one `TransactWriteItems`). Proven pattern for DynamoDB at scale.
- **Option C — Amazon RDS (PostgreSQL)**: Relational, familiar query model. But adds operational complexity (VPC, RDS clusters, connection pooling), conflicts with the serverless/managed ethos, and is overkill for a service whose access patterns are well-defined.

## Decision

**Option B** — single-table design.

### Table layout

| Entity | PK | SK / GSI key |
|---|---|---|
| User | `USER#{userId}` | — |
| Email lookup | GSI1 PK: `EMAIL#{email}` | — |
| CPF lookup | GSI2 PK: `CPF#{cpf_hmac}` | — |
| Refresh token | `REFRESH#{tokenHash}` | — |
| Outbox message | `OUTBOX#{messageId}` | — |
| Audit log entry | `AUDIT#{userId}` | SK: `#{ISO8601Timestamp}` |
| Consent record | `CONSENT#{userId}` | SK: `#{consentVersion}` |

### DynamoDB TTL

- Unverified user accounts: 48 hours (LGPD — no stale PII retained)
- Refresh tokens: 7 days
- Outbox messages: 24 hours after processing

### Encryption

- CPF stored KMS-encrypted in the `CpfEncrypted` attribute.
- GSI key uses HMAC-SHA256(`cpf`, `kmsKeyId`) — deterministic, never decryptable from the index alone.

## Consequences

**Easier:**
- Atomic `TransactWriteItems` across user + outbox in a single round-trip.
- Single IAM policy, single Terraform resource.
- TTL-based data retention is native to DynamoDB (no cron jobs for cleanup).

**Harder:**
- Access patterns must be defined upfront — ad-hoc queries require GSIs or full scans.
- New access patterns that were not anticipated may require GSI additions (up to 20 per table).
- Developers must internalize the PK/SK prefix convention; mistakes are hard to detect at compile time.
