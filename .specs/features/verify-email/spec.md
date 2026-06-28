# Feature Spec — verify-email

**Feature ID:** F-04-completion + F-05-verify-email
**Plan tasks:** T-032 (partial), T-034, T-035, T-036, T-039, T-040, T-058–T-062
**Status:** Specifying
**Last updated:** 2026-06-27

---

## Context

`register-user` is complete and shipping. The next gate before Login (Day 11) requires:

1. **Domain prerequisites** — three missing domain events and two missing contracts (`ITokenService`, `IPasswordHasher`) that Login depends on.
2. **VerifyEmail use case** — the handler that validates the HMAC token sent via email, transitions the user from `PendingVerification → Active`, and raises `UserEmailVerified`.

The `UserEntity` already has `VerifyEmail()`, `SetEmailVerificationToken()`, `EmailVerificationTokenHash`, and `EmailVerificationTokenExpiry`. No entity changes needed.

---

## Goals

| ID | Requirement |
|---|---|
| REQ-001 | A user in `PendingVerification` status can verify their email by submitting the raw token received by email. |
| REQ-002 | The handler recomputes the HMAC-SHA256 hash of the submitted token and compares it against the stored `EmailVerificationTokenHash`. |
| REQ-003 | If the token is expired (`EmailVerificationTokenExpiry < UtcNow`), return a validation error — do not transition status. |
| REQ-004 | If the token hash does not match or the user is not found, return the same generic error (no enumeration). |
| REQ-005 | On success, transition user to `Active`, clear token fields, persist, and raise `UserEmailVerified`. |
| REQ-006 | A user already `Active` returns success (idempotent — re-verification is a no-op, 200 OK). |
| REQ-007 | A `Suspended` or `Deleted` user returns a conflict error. |
| REQ-008 | The endpoint is public (no JWT required). |
| REQ-009 | The HMAC key is read from `IConfiguration["Hmac:Key"]`, defaulting to `"dev-hmac-key"` (same pattern as RegisterUserHandler). |

---

## Domain Prerequisites (unblocking Login Day 11)

### REQ-010 — `UserEmailVerified` domain event

```csharp
public sealed record UserEmailVerified(Guid UserId, string Email, DateTimeOffset OccurredAt);
```

### REQ-011 — `UserPasswordChanged` domain event

```csharp
public sealed record UserPasswordChanged(Guid UserId, DateTimeOffset OccurredAt);
```

### REQ-012 — `UserSuspended` domain event

```csharp
public sealed record UserSuspended(Guid UserId, string Reason, DateTimeOffset OccurredAt);
```

### REQ-013 — `IPasswordHasher` interface

Abstracts BCrypt operations out of the `Password` value object so infrastructure can inject a real hasher.

```csharp
public interface IPasswordHasher
{
    string Hash(string plaintext);
    bool Verify(string plaintext, string hash);
}
```

### REQ-014 — `ITokenService` interface

Used by Login (access JWT + refresh token) and Logout (revocation). Defined now so Login can be implemented without changing contracts.

```csharp
public interface ITokenService
{
    string GenerateAccessToken(Guid userId, string email, string role);
    string GenerateRefreshToken();
    string HashToken(string rawToken);
    bool VerifyTokenHash(string rawToken, string storedHash);
}
```

---

## Request / Response Contract

### Request

```csharp
public sealed record VerifyEmailRequest(string Email, string Token);
```

> Rationale: email + token in the request lets the handler look up the user by email (existing `GetByEmailAsync`) and then validate the token hash — no new repository method needed.

### Response

```csharp
// Reuse UserResponse (already contains Id, Email, Role, Status)
```

### Endpoint

```
POST /api/v1/auth/verify-email
Body: { "email": "...", "token": "..." }
200 OK  → UserResponse (status = "Active")
400     → validation errors (invalid input, expired token, wrong token — generic message)
409     → user is Suspended or Deleted
```

---

## Validation Rules

| Field | Rule | Error |
|---|---|---|
| `Email` | NotEmpty, valid RFC email format | standard email errors |
| `Token` | NotEmpty, min length 1 | `"Token is required."` |

Token authenticity and expiry are **handler concerns**, not validator concerns.

---

## Error Codes (add to `UserErrorCodes`)

| Constant | Value | Used when |
|---|---|---|
| `InvalidOrExpiredToken` | `"User.InvalidOrExpiredToken"` | Token not found, hash mismatch, or expired |
| `AccountNotVerifiable` | `"User.AccountNotVerifiable"` | User is Suspended or Deleted |

---

## Test Scenarios

### Validator tests (`VerifyEmailValidatorTests`)

| # | Input | Expected |
|---|---|---|
| V-01 | Valid email + token | No errors |
| V-02 | Empty email | Error on Email |
| V-03 | Malformed email | Error on Email |
| V-04 | Empty token | Error on Token |

### Handler tests (`VerifyEmailHandlerTests`)

| # | Scenario | Expected |
|---|---|---|
| H-01 | Valid token, user PendingVerification | `ErrorOr.Value` → UserResponse with Status=Active |
| H-02 | Token expired | `Error.Validation` with `InvalidOrExpiredToken` |
| H-03 | Token hash mismatch | `Error.Validation` with `InvalidOrExpiredToken` |
| H-04 | User not found by email | `Error.Validation` with `InvalidOrExpiredToken` (no enumeration) |
| H-05 | User already Active (idempotent) | `ErrorOr.Value` → UserResponse with Status=Active |
| H-06 | User Suspended | `Error.Conflict` with `AccountNotVerifiable` |
| H-07 | User Deleted | `Error.Conflict` with `AccountNotVerifiable` |

### Integration tests (`VerifyEmailEndpointTests`)

| # | Scenario | Expected HTTP |
|---|---|---|
| I-01 | Valid token flow (register → extract token from FakeEmailService → verify) | 200 |
| I-02 | Invalid token | 400 |
| I-03 | Expired token (manually set expiry in past via FakeUserRepository) | 400 |

---

## Out of Scope

- Real email delivery (SES) — deferred to E-04
- Token rotation / single-use enforcement — deferred to E-04 (requires DynamoDB TTL)
- Resend verification email endpoint — deferred to E-05
