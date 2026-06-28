# Logout Feature Spec

## Overview

Invalidates the user's current refresh token by clearing it from `UserEntity`. The access token is short-lived and Cognito-managed (cannot be revoked client-side — deferred to E-04). Logout is **idempotent**: if the user is already logged out or the token doesn't match, the response is still 204.

---

## Request / Response

**Endpoint:** `POST /api/v1/auth/logout`
**Auth:** Anonymous (JWT middleware deferred to E-04; token ownership verified via refresh token hash)

### Request

```json
{
  "email": "user@example.com",
  "refreshToken": "<raw_refresh_token>"
}
```

### Response — 204 No Content

Empty body.

---

## Requirements

| ID | Requirement |
|---|---|
| REQ-001 | `email` must be non-empty and a valid email format |
| REQ-002 | `refreshToken` must be non-empty |
| REQ-003 | If email is not found, return `Success` (no-op, 204) — no enumeration |
| REQ-004 | If `RefreshTokenHash` is null (already logged out), return `Success` (204) |
| REQ-005 | If `ITokenService.VerifyTokenHash` fails (hash mismatch), return `Success` (204) — do not confirm or deny token validity |
| REQ-006 | On valid token match: call `user.ClearRefreshToken()`, persist via `UpdateAsync`, return `Success` (204) |
| REQ-007 | `UserEntity` must expose a `ClearRefreshToken()` method that nullifies `RefreshTokenHash` and `RefreshTokenExpiry` |
| REQ-008 | Logout is idempotent — all non-validation paths return 204 |

---

## Domain change

Add to `UserEntity`:

```csharp
public void ClearRefreshToken()
{
    RefreshTokenHash = null;
    RefreshTokenExpiry = null;
}
```

---

## Test Scenarios

| ID | Scenario | Expected |
|---|---|---|
| V-01 | Valid request | No validation errors |
| V-02 | Empty email | Validation error on Email |
| V-03 | Invalid email format | Validation error on Email |
| V-04 | Empty refreshToken | Validation error on RefreshToken |
| H-01 | Active user, matching token | Success; `UpdateAsync` called once |
| H-02 | Email not found | Success; `UpdateAsync` never called |
| H-03 | `RefreshTokenHash` is null (already logged out) | Success; `UpdateAsync` never called |
| H-04 | Token hash mismatch | Success; `UpdateAsync` never called |
| H-05 | FluentValidation failure | Errors returned; repository never called |
| I-01 | Register → verify → login → logout | 204 |
| I-02 | Logout again (idempotent, no token) | 204 |
