# Login Feature Spec

## Overview

Authenticate a registered, active user using email and password. Returns a short-lived access token and a long-lived refresh token. Refresh token is stored as an HMAC-SHA256 hash on the user entity.

---

## Request / Response

**Endpoint:** `POST /api/v1/auth/login`
**Auth:** Anonymous

### Request

```json
{
  "email": "user@example.com",
  "password": "P@ssword123!"
}
```

### Response — 200 OK

```json
{
  "accessToken": "<access_token>",
  "user": {
    "id": "uuid",
    "email": "user@example.com",
    "role": "Owner",
    "status": "Active",
    "createdAt": "2026-06-27T00:00:00Z"
  }
}
```

The refresh token is **not** in the body. It is set via a `Set-Cookie: refreshToken=...; HttpOnly; SameSite=Strict; Path=/api/v1/auth` header (see ADR/`docs/api-contracts.md`).

---

## Requirements

| ID | Requirement |
|---|---|
| REQ-001 | `email` must be non-empty and a valid email format |
| REQ-002 | `password` must be non-empty |
| REQ-003 | If email is not found, return `Error.Validation(InvalidCredentials)` — no enumeration (same code as wrong password) |
| REQ-004 | If user status is `PendingVerification`, return `Error.Validation(AccountNotVerified)` |
| REQ-005 | If user status is `Suspended` or `Deleted`, return `Error.Conflict(AccountNotVerifiable)` |
| REQ-006 | Verify password using `IPasswordHasher.Verify(plaintext, storedHash)` — mismatch returns `Error.Validation(InvalidCredentials)` |
| REQ-007 | On success: generate access token via `ITokenService.GenerateAccessToken(userId, email, role)` |
| REQ-008 | On success: generate refresh token via `ITokenService.GenerateRefreshToken()`, hash it via `ITokenService.HashToken()`, store hash + 30-day expiry on `UserEntity` via `SetRefreshToken()` |
| REQ-009 | Return raw (unhashed) refresh token — never the hash. Handler returns it on `LoginResponse`; the API endpoint sets it as an `httpOnly` cookie instead of including it in the JSON body |
| REQ-010 | Handler response (`LoginResponse`) contains `accessToken`, `refreshToken`, and `user`; the HTTP response body (`AuthTokenResponse`) exposes only `accessToken` and `user` |
| REQ-011 | `UserEntity` must expose `RefreshTokenHash` and `RefreshTokenExpiry` properties, and a `SetRefreshToken(string hash, DateTimeOffset expiry)` method |

---

## Error Codes

| Code | Type | HTTP | Description |
|---|---|---|---|
| `User.InvalidCredentials` | Validation | 422 | Email not found or password incorrect |
| `User.AccountNotVerified` | Validation | 422 | Account exists but email not verified yet |
| `User.AccountNotVerifiable` | Conflict | 409 | Account is suspended or deleted |

---

## Test Scenarios

| ID | Scenario | Expected |
|---|---|---|
| V-01 | Valid request | No validation errors |
| V-02 | Empty email | Validation error on Email |
| V-03 | Invalid email format | Validation error on Email |
| V-04 | Empty password | Validation error on Password |
| H-01 | Active user, correct password | 200 with access + refresh token |
| H-02 | Email not found | Validation error, code = InvalidCredentials |
| H-03 | Correct email, wrong password | Validation error, code = InvalidCredentials |
| H-04 | User status = PendingVerification | Validation error, code = AccountNotVerified |
| H-05 | User status = Suspended | Conflict error, code = AccountNotVerifiable |
| H-06 | User status = Deleted | Conflict error, code = AccountNotVerifiable |
| H-07 | FluentValidation failure | Errors returned, repository never called |
| I-01 | Register → verify email → login | 200, tokens present |
| I-02 | Register → login without verifying email | 422 |
| I-03 | Login with wrong credentials | 422 |
