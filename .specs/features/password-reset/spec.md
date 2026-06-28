# Password Reset Feature Spec

## Overview

Two-step flow: (1) request a reset token sent by email; (2) confirm the reset using the token and a new password.

---

## Step 1 — Forgot Password

**Endpoint:** `POST /api/v1/auth/forgot-password`
**Auth:** Anonymous

### Request

```json
{ "email": "user@example.com" }
```

### Response — 204 No Content

Always 204 regardless of whether the email exists (no enumeration).

### Requirements

| ID | Requirement |
|---|---|
| REQ-001 | `email` must be non-empty and valid email format |
| REQ-002 | If email not found → return `Success` (204, no-op, no enumeration) |
| REQ-003 | If user status is `Suspended`, `Deleted`, or `PendingVerification` → return `Success` (204, no-op, no status leak) |
| REQ-004 | Generate raw token (`Guid.NewGuid().ToString()`), hash via HMAC-SHA256 (`Hmac:Key`), store hash + 1-hour expiry via `SetPasswordResetToken` |
| REQ-005 | Persist token via `UpdateAsync` |
| REQ-006 | Send email via `IEmailService.SendPasswordResetEmailAsync` — email failure must not fail the request |
| REQ-007 | Return `Success` (204) |

---

## Step 2 — Reset Password

**Endpoint:** `POST /api/v1/auth/reset-password`
**Auth:** Anonymous

### Request

```json
{
  "email": "user@example.com",
  "token": "<raw_reset_token>",
  "newPassword": "NewP@ssword123!"
}
```

### Response — 204 No Content

### Requirements

| ID | Requirement |
|---|---|
| REQ-008 | `email` must be non-empty and valid email format |
| REQ-009 | `token` must be non-empty |
| REQ-010 | `newPassword` must be non-empty, ≥12 chars, ≤128 chars, and meet complexity (upper + lower + digit + symbol) |
| REQ-011 | If email not found → `Error.Validation(TokenInvalidOrExpired)` — no enumeration |
| REQ-012 | If user status is `Suspended` or `Deleted` → `Error.Conflict(AccountNotVerifiable)` |
| REQ-013 | If `PasswordResetTokenHash` is null or `PasswordResetTokenExpiry` is past → `Error.Validation(TokenInvalidOrExpired)` |
| REQ-014 | Verify token via HMAC-SHA256 — mismatch → `Error.Validation(TokenInvalidOrExpired)` |
| REQ-015 | Call `user.ResetPassword(Password.FromPlaintext(request.NewPassword))` |
| REQ-016 | Persist via `UpdateAsync` |
| REQ-017 | Log `UserPasswordChanged` domain event |
| REQ-018 | Return `Success` (204) |

---

## Test Scenarios

| ID | Scenario | Expected |
|---|---|---|
| V-01 | Forgot: valid email | No errors |
| V-02 | Forgot: empty email | Validation error on Email |
| V-03 | Forgot: invalid email format | Validation error on Email |
| V-04 | Reset: valid request | No errors |
| V-05 | Reset: empty token | Validation error on Token |
| V-06 | Reset: password too short | Validation error on NewPassword |
| V-07 | Reset: password no complexity | Validation error on NewPassword |
| H-01 | Forgot: active user → token stored, email sent, 204 | UpdateAsync once; SendPasswordResetEmailAsync once |
| H-02 | Forgot: user not found → 204, no-op | UpdateAsync never called |
| H-03 | Forgot: suspended user → 204, no-op | UpdateAsync never called |
| H-04 | Forgot: pending user → 204, no-op | UpdateAsync never called |
| H-05 | Forgot: email send failure → still returns 204 | |
| H-06 | Reset: valid token → password changed, 204 | UpdateAsync once |
| H-07 | Reset: email not found → TokenInvalidOrExpired | |
| H-08 | Reset: token hash mismatch → TokenInvalidOrExpired | |
| H-09 | Reset: token expired → TokenInvalidOrExpired | |
| H-10 | Reset: null token hash → TokenInvalidOrExpired | |
| H-11 | Reset: suspended user → AccountNotVerifiable | |
| H-12 | Reset: deleted user → AccountNotVerifiable | |
| H-13 | Reset: validation failure → errors, no repo call | |
| I-01 | Forgot → get token from fake → reset → 204 | |
| I-02 | Forgot for unknown email → 204 (no-enum) | |
| I-03 | Reset with wrong token → 422 | |
