# Spec: E-05 Security Hardening & LGPD Compliance

## Summary

E-05 hardens the Identity API across four concerns: (1) HTTP security headers added via a custom middleware to protect all responses from common browser-based attacks; (2) inline HMAC logic removed from three handlers and routed through `ITokenService.HashToken()` / `VerifyTokenHash()` to centralise secret handling; (3) explicit LGPD consent captured at registration time, stored as a `ConsentGivenAt` timestamp on `UserEntity` and persisted to DynamoDB; (4) an append-only DynamoDB audit log that records every data-access, export, and erasure event to satisfy LGPD Art. 37 record-keeping obligations.

---

## Concern 1 — Security Headers Middleware

### Interface Contract

Not an endpoint. A middleware registered in `Program.cs` before `UseAuthentication`.

### Handler / Service Logic

1. Add `SecurityHeadersMiddleware` to `Api/Middlewares/`.
2. On every response, set:
   - `Content-Security-Policy: default-src 'self'`
   - `X-Frame-Options: DENY`
   - `X-Content-Type-Options: nosniff`
   - `Referrer-Policy: strict-origin-when-cross-origin`
   - `Permissions-Policy: camera=(), microphone=(), geolocation=()`
3. Register via `app.UseSecurityHeaders()` extension method in `Api/Extensions/`.
4. Register before `UseAuthentication` in `Program.cs`.

### Validation Rules

None — middleware is unconditional.

### Error Cases

None.

### Testing Strategy

**Unit tests (handler-style):** None — middleware is a cross-cutting concern.

**Integration tests:**
- Happy-path response contains all 5 headers with expected values.

---

## Concern 2 — Handler HMAC Refactor

### Summary

Remove inline `IConfiguration["Hmac:Key"]` / `HMACSHA256` from `RegisterUserHandler`, `VerifyEmailHandler`, and `ResetPasswordHandler`. Route through `ITokenService.HashToken()` and `ITokenService.VerifyTokenHash()`.

### Impacted Handlers

| Handler | Current | After |
|---|---|---|
| `RegisterUserHandler` | `new HMACSHA256(Encoding.UTF8.GetBytes(config["Hmac:Key"]!))` | `_tokenService.HashToken(rawToken)` |
| `VerifyEmailHandler` | same pattern | `_tokenService.VerifyTokenHash(rawToken, storedHash)` |
| `ResetPasswordHandler` | same pattern | `_tokenService.VerifyTokenHash(rawToken, storedHash)` |

### Interface Contract

No API change. Pure internal refactor.

### ITokenService additions (if not already present)

`HashToken(string rawToken)` and `VerifyTokenHash(string rawToken, string storedHash)` must be declared on `ITokenService` (already present per E-04 implementation).

### Validation Rules

None — validation rules are unchanged.

### Error Cases

Unchanged.

### Testing Strategy

**Handler unit tests:**
- Existing tests must continue to pass; mock `ITokenService.HashToken` / `VerifyTokenHash`.
- `IConfiguration` must no longer be injected into these handlers after refactor.

**Integration tests:**
- All existing integration tests must continue to pass (FakeTokenService already implements `HashToken` / `VerifyTokenHash`).

---

## Concern 3 — LGPD Consent Capture

### Interface Contract

| Field | Value |
|---|---|
| Method | `POST` |
| Route | `/api/v1/identity/register` (existing) |
| Auth required | No |
| Success status | 201 (unchanged) |

### Request / Input — delta only

| Field | Type | Required | Notes |
|---|---|---|---|
| `consentGiven` | `bool` | Yes | Must be `true`; registration blocked if `false` |

### Response / Output

Unchanged — `RegisterUserResponse`.

### Data Model Changes

**`UserEntity`**
- Add `ConsentGivenAt` (`DateTimeOffset?`) — nullable for backward compatibility with existing records.

**DynamoDB**
- `ConsentGivenAt` stored as ISO-8601 string attribute on the user item.
- No migration needed (single-table, schema-less).

### Validation Rules

| Field | Rule | Source / Constant |
|---|---|---|
| `consentGiven` | Must be `true` | `ValidationMessages.ConsentRequired` |

### Handler / Service Logic

1. Validate `ConsentGiven == true` (validator).
2. Existing registration logic unchanged.
3. Set `userEntity.ConsentGivenAt = DateTimeOffset.UtcNow` before `_userRepository.AddAsync`.
4. `UserRepository.AddAsync` maps `ConsentGivenAt` to DynamoDB attribute.

### Error Cases

| Scenario | Error type | HTTP status |
|---|---|---|
| `consentGiven == false` | Validation failure | 422 |

### Testing Strategy

**Validator unit tests:**
- `consentGiven = true` → passes.
- `consentGiven = false` → fails with `ConsentRequired` message.

**Handler unit tests:**
- `ConsentGivenAt` is set to a non-null value before repository call.

**Integration tests:**
- `RegisterUserRequestBuilder` must default `ConsentGiven = true`.
- Existing registration integration tests continue to pass.

---

## Concern 4 — Audit Log

### Summary

An append-only DynamoDB record is written for every LGPD-sensitive event: profile read, data export, and account erasure. Satisfies LGPD Art. 37. TTL is 90 days.

### Domain Interface

```
IAuditLogService (Domain/Interfaces/)
  Task LogAsync(Guid userId, string eventType, CancellationToken ct)
```

### Event Types (constants)

| Constant | Value |
|---|---|
| `AuditEvents.ProfileAccessed` | `"PROFILE_ACCESSED"` |
| `AuditEvents.DataExported` | `"DATA_EXPORTED"` |
| `AuditEvents.AccountDeleted` | `"ACCOUNT_DELETED"` |

### DynamoDB Schema

| Attribute | Type | Value |
|---|---|---|
| `PK` | `S` | `AUDIT#{userId}#{timestamp:yyyyMMddHHmmss}_{guid}` |
| `SK` | `S` | same as PK (point lookup only) |
| `UserId` | `S` | `userId.ToString()` |
| `EventType` | `S` | e.g. `"DATA_EXPORTED"` |
| `OccurredAt` | `S` | ISO-8601 UTC timestamp |
| `TTL` | `N` | Unix epoch + 90 days |

No GSI needed — records are append-only; queries by userId not required for this epic.

### Impacted Handlers

| Handler | Event |
|---|---|
| `GetProfileHandler` | `AuditEvents.ProfileAccessed` |
| `ExportDataHandler` | `AuditEvents.DataExported` |
| `DeleteAccountHandler` | `AuditEvents.AccountDeleted` |

### Handler / Service Logic (per impacted handler)

1. Execute existing business logic unchanged.
2. On success only, call `await _auditLogService.LogAsync(userId, AuditEvents.X, ct)`.
3. Audit failure must NOT propagate — wrap in `try/catch`, log warning via `ILogger`.

### Infrastructure

`AuditLogService` in `Infrastructure/` implements `IAuditLogService`.
- Uses `IAmazonDynamoDB` (already registered via E-04 IoC).
- Table name read from `IConfiguration["DynamoDB:TableName"]`.
- TTL = `DateTimeOffset.UtcNow.AddDays(90).ToUnixTimeSeconds()`.

### IoC

Register `IAuditLogService` → `AuditLogService` as singleton in `InfrastructureDependencyInjection`.

### Testing Strategy

**Unit tests (`AuditLogService`):**
- `LogAsync` writes a `PutItemRequest` with correct PK pattern, EventType, OccurredAt, TTL.

**Handler unit tests (GetProfile, ExportData, DeleteAccount):**
- On success path, `IAuditLogService.LogAsync` is called once.
- Audit failure does not propagate (exception swallowed).

**Integration tests:**
- Register `FakeAuditLogService` (records calls in-memory) in `CustomWebApplicationFactory`.
- After `GET /profile`, `FakeAuditLogService` contains one `PROFILE_ACCESSED` entry.
- After `DELETE /account`, one `ACCOUNT_DELETED` entry.
- After `GET /export`, one `DATA_EXPORTED` entry.

---

## Open Questions

None blocking. All decisions derived from existing codebase patterns and E-04 implementation.
