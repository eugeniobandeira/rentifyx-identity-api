# ADR-007: LGPD data retention and anonymization strategy

- **Date:** 2026-06-21
- **Status:** Accepted

## Context

The Lei Geral de Proteção de Dados (LGPD) requires that personal data be retained only for as long as necessary to fulfill the purpose for which it was collected (Article 15), and that data subjects have the right to erasure (Article 18 VI). The identity service stores PII including email addresses, CPF (encrypted), and audit logs.

We must define explicit retention policies for each data category and an anonymization procedure that satisfies the erasure right without breaking referential integrity in audit logs.

## Options Considered

- **Option A — Hard delete**: Remove all rows belonging to the user. Simple, but breaks audit log integrity and may violate BACEN record-keeping requirements (5-year minimum for financial transaction records).
- **Option B — Soft delete only**: Mark the user as `Deleted` but keep all PII. Does not satisfy LGPD Article 18 VI erasure right.
- **Option C — Soft delete + PII anonymization**: Mark the user as `Deleted`, replace identifiable fields (Email, CPF) with hashed placeholders, delete the KMS-encrypted CPF ciphertext. Audit log entries retain the `userId` but the userId itself no longer maps to real PII. Satisfies both LGPD erasure and BACEN record-keeping.

## Decision

**Option C** — soft delete with PII anonymization.

### Retention schedule

| Data category | Retention period | Mechanism |
|---|---|---|
| Unverified user account | 48 hours from registration | DynamoDB TTL on `User` record |
| Refresh token | 7 days from issuance | DynamoDB TTL on `REFRESH#` record |
| Active user PII | Until erasure request or 5 years post-last-activity | Manual or policy-triggered |
| Audit log entries | 5 years (BACEN requirement) | No TTL; userId anonymized on erasure |
| Consent records | Duration of consent + 5 years | No TTL |
| Outbox messages | 24 hours after processing | DynamoDB TTL on `OUTBOX#` record |

### Anonymization procedure (triggered by `DELETE /v1/api/users/me`)

1. Set `User.Status = Deleted`.
2. Replace `Email` with `ANONYMIZED#{SHA256(userId)}@deleted.rentifyx.com`.
3. Delete the KMS-encrypted CPF ciphertext; replace `CpfEncrypted` with `ANONYMIZED`.
4. Replace `CpfHmac` (GSI2 key) with a random UUID so the GSI entry becomes unreachable.
5. Revoke all active refresh tokens for the user (DynamoDB delete).
6. Write an `AuditLog` entry: `USER_DELETED` with `userId`, `timestamp`, and `requestedBy`.
7. Publish `UserDeleted` domain event via the Outbox.

### Consent records (LGPD Article 8)

Every registration captures a `ConsentRecord` with: `userId`, `consentVersion`, `timestamp`, `ipAddress`. The consent record is retained for 5 years post-deletion as evidence of lawful processing.

## Consequences

**Easier:**
- DynamoDB TTL handles automatic deletion of short-lived records — no cron jobs.
- Anonymization satisfies LGPD Article 18 without breaking audit integrity.
- Audit log remains useful for fraud investigation after user deletion.

**Harder:**
- The anonymization procedure must be atomic (or idempotent) — partial anonymization must not leave PII exposed.
- The `userId` in audit logs becomes meaningless for identity lookups after anonymization; forensic investigation must use the audit entry itself, not cross-reference the user record.
- Consent records must survive the user record deletion — the `CONSENT#{userId}` partition retains the anonymized `userId` hash as its key.
