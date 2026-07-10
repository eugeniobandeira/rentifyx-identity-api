# Refresh Token Feature Spec

## Overview

Issues a new access token and rotates the refresh token for an authenticated user. Accepts the current raw refresh token and the user's email; verifies the token against the stored HMAC-SHA256 hash; on success issues new tokens and stores the new hash (rotation).

---

## Request / Response

**Endpoint:** `POST /api/v1/auth/refresh`
**Auth:** Anonymous

### Request

```json
{
  "email": "user@example.com"
}
```

The current `refreshToken` is read from the `refreshToken` `httpOnly` cookie, not the body. Client must send the request with `credentials: 'include'`.

### Response — 200 OK

Same body shape as `/auth/login` (`AuthTokenResponse`):

```json
{
  "accessToken": "<new_access_token>",
  "user": {
    "id": "uuid",
    "email": "user@example.com",
    "role": "Owner",
    "status": "Active",
    "createdAt": "2026-06-27T00:00:00Z"
  }
}
```

The rotated refresh token is set via `Set-Cookie`, not returned in the body.

---

## Requirements

| ID | Requirement |
|---|---|
| REQ-001 | `email` must be non-empty and a valid email format |
| REQ-002 | `refreshToken` must be non-empty |
| REQ-003 | If email is not found, return `Error.Validation(TokenInvalidOrExpired)` — no enumeration |
| REQ-004 | If user status is `Suspended` or `Deleted`, return `Error.Conflict(AccountNotVerifiable)` |
| REQ-005 | If user status is `PendingVerification`, return `Error.Validation(AccountNotVerified)` |
| REQ-006 | If `RefreshTokenHash` is null or `RefreshTokenExpiry` is past, return `Error.Validation(TokenInvalidOrExpired)` |
| REQ-007 | Verify token via `ITokenService.VerifyTokenHash(rawToken, storedHash)` — mismatch returns `Error.Validation(TokenInvalidOrExpired)` |
| REQ-008 | On success: generate new access token and rotate refresh token (new raw token → hash → `SetRefreshToken` with 30-day expiry) |
| REQ-009 | Return raw (unhashed) new refresh token. Handler returns it on `LoginResponse`; the API endpoint sets it as an `httpOnly` cookie instead of including it in the JSON body |
| REQ-010 | Handler response shape is identical to `LoginResponse` (reuse); HTTP response body (`AuthTokenResponse`) exposes only `accessToken` and `user` |
| REQ-011 | API layer: if the `refreshToken` cookie is missing, pass an empty token to the handler, which fails validation (`RefreshToken` required) — 422 |

---

## Error Codes

| Code | Type | HTTP | Description |
|---|---|---|---|
| `User.TokenInvalidOrExpired` | Validation | 422 | User not found, token mismatch, or token expired |
| `User.AccountNotVerified` | Validation | 422 | Account not yet email-verified |
| `User.AccountNotVerifiable` | Conflict | 409 | Account suspended or deleted |

---

## Test Scenarios

| ID | Scenario | Expected |
|---|---|---|
| V-01 | Valid request | No validation errors |
| V-02 | Empty email | Validation error on Email |
| V-03 | Invalid email format | Validation error on Email |
| V-04 | Empty refreshToken | Validation error on RefreshToken |
| H-01 | Active user, valid token, not expired | 200 with new tokens; UpdateAsync called once |
| H-02 | Email not found | Validation error, TokenInvalidOrExpired |
| H-03 | Token hash mismatch | Validation error, TokenInvalidOrExpired |
| H-04 | Token expired (expiry in the past) | Validation error, TokenInvalidOrExpired |
| H-05 | RefreshTokenHash is null | Validation error, TokenInvalidOrExpired |
| H-06 | User status = Suspended | Conflict, AccountNotVerifiable |
| H-07 | User status = Deleted | Conflict, AccountNotVerifiable |
| H-08 | User status = PendingVerification | Validation, AccountNotVerified |
| H-09 | FluentValidation failure | Errors returned; repository never called |
| I-01 | Register → verify → login → refresh | 200, new tokens different from originals |
| I-02 | Invalid refresh token | 422 |
