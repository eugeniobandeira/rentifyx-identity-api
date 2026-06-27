# Feature: Identity

Core identity domain for the RentifyX platform. Covers the full user lifecycle from registration through deletion.

## Epics & timeline

| Epic | Week | Days | Goal |
|---|---|---|---|
| E-01: Project Foundation & DevSecOps | 1 | 1–3 | Template scaffold + CI security gates + LocalStack |
| E-02: Domain Model & Core Identity Logic | 2 | 4–8 | User aggregate, value objects, domain events — 100% unit tested |
| E-03: Application Layer — Use Cases | 3 | 9–13 | Register, VerifyEmail, Login, Refresh, Logout, PasswordReset handlers |
| E-04: Infrastructure — AWS Integration | 4 | 14–18 | DynamoDB, Cognito, SES, Secrets Manager, Outbox Pattern |
| E-05: API Layer — Endpoints, Security & LGPD | 5 | 19–23 | All endpoints live, OWASP hardened, LGPD rights implemented |
| E-06: IaC & Production Readiness | 6 | 24–28 | Terraform, Helm/EKS, SLOs, C4 diagrams, OWASP ZAP, v1.0.0 |

## Domain model

### User aggregate

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Stable internal identifier |
| `Email` | `Email` (VO) | Format + domain validation; masked in logs |
| `TaxId` | `TaxDocument` (VO) | CPF (individuals) or CNPJ (companies); mod-11 validated; encrypted at rest via KMS |
| `PasswordHash` | `Password` (VO) | Min 12 chars, upper+lower+digit+symbol (OWASP A07) |
| `Role` | `UserRole` enum | `Owner` \| `Renter` \| `Admin` |
| `Status` | `UserStatus` enum | `PendingVerification` → `Active` → `Suspended` \| `Deleted` |
| `CreatedAt` | `DateTimeOffset` | UTC, set at creation |

### Status transitions

```
[Registration]
      │
      ▼
PendingVerification ──► Active ──► Suspended
                                       │
                           (admin)     │
                                       ▼
                  [LGPD erasure] ──► Deleted (anonymized)
```

### Domain events

| Event | Raised when | Payload |
|---|---|---|
| `UserRegistered` | Registration completes | `UserId`, `Email`, `Role`, `OccurredAt` |
| `UserEmailVerified` | Email token validated | `UserId`, `OccurredAt` |
| `UserPasswordChanged` | Password reset confirmed | `UserId`, `OccurredAt` |
| `UserSuspended` | Admin suspends account | `UserId`, `Reason`, `SuspendedBy`, `OccurredAt` |

## Endpoints

### Auth endpoints (public)

| Method | Path | Use case |
|---|---|---|
| `POST` | `/v1/api/auth/register` | Create account + send verification email |
| `POST` | `/v1/api/auth/verify-email` | Validate 24h HMAC token → status Active |
| `POST` | `/v1/api/auth/login` | Credentials → Access JWT (15 min) + Refresh token (7d) |
| `POST` | `/v1/api/auth/refresh` | Rotate refresh token → new Access JWT |
| `POST` | `/v1/api/auth/logout` | Revoke refresh token |
| `POST` | `/v1/api/auth/forgot-password` | Send signed reset token via SES |
| `POST` | `/v1/api/auth/reset-password` | Validate 1h token → update password hash |

### User endpoints (authenticated)

| Method | Path | LGPD article | Use case |
|---|---|---|---|
| `GET` | `/v1/api/users/me` | Art. 18 | Return own profile |
| `DELETE` | `/v1/api/users/me` | Art. 18 VI | Soft delete + anonymize PII |
| `GET` | `/v1/api/users/me/data-export` | Art. 18 IV | Export all stored data as JSON |

## Security controls

| Control | Implementation |
|---|---|
| Rate limiting | 5 failed logins → 15-min lockout (OWASP A07) |
| Token security | Access JWT 15 min TTL; refresh token one-time use, stored as hash |
| Signing key | RSA-2048 in Secrets Manager; never in code or env vars |
| Reset token | HMAC-SHA256, single-use, 1h expiry |
| Verification token | HMAC-SHA256, single-use, 24h expiry |
| Idempotency | Duplicate email or TaxId (CPF/CNPJ) rejected at registration (LGPD Art. 46) |

## Implementation notes

- The `Password` value object validates complexity at construction time; the hash is stored, never the plaintext.
- The `CPF` value object runs the mod-11 digit-verification algorithm and exposes only the masked form (`***.***.***-**`) via `ToString()`.
- The `Email` value object validates RFC format and rejects disposable domains.
- All tokens (verification, reset, refresh) are stored as HMAC-SHA256 hashes — the raw token is only ever transmitted over HTTPS and never persisted.
