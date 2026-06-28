# Password Reset — Task Breakdown

## Status Legend

| Symbol | Meaning |
|---|---|
| ✅ | Pending |
| ✅ | Complete |

## Tasks

| # | Layer | What | Status |
|---|---|---|---|
| T-01 | Tests.Common | Add `SentPasswordResetEmails` to `FakeEmailService` (needed for integration tests) | ✅ |
| T-02 | Application | `ForgotPasswordRequest.cs` → `record(string Email)` | ✅ |
| T-03 | Application | `ForgotPasswordValidator.cs` → Email (NotEmpty + EmailAddress) | ✅ |
| T-04 | Application | `ForgotPasswordHandler.cs` → always 204, no enumeration, HMAC token + email | ✅ |
| T-05 | Application | `ResetPasswordRequest.cs` → `record(string Email, string Token, string NewPassword)` | ✅ |
| T-06 | Application | `ResetPasswordValidator.cs` → Email + Token + NewPassword (full complexity rules) | ✅ |
| T-07 | Application | `ResetPasswordHandler.cs` → verify HMAC token, reset password, log domain event | ✅ |
| T-08 | API | `ForgotPassword.cs` endpoint: `POST /auth/forgot-password` → 204 | ✅ |
| T-09 | API | `ResetPassword.cs` endpoint: `POST /auth/reset-password` → 204 | ✅ |
| T-10 | Tests | `ForgotPasswordValidatorTests.cs` (V-01 to V-03) | ✅ |
| T-11 | Tests | `ResetPasswordValidatorTests.cs` (V-04 to V-07) | ✅ |
| T-12 | Tests | `ForgotPasswordHandlerTests.cs` (H-01 to H-05) | ✅ |
| T-13 | Tests | `ResetPasswordHandlerTests.cs` (H-06 to H-13) | ✅ |
| T-14 | Tests | `PasswordResetEndpointTests.cs` (I-01 to I-03) | ✅ |

## Dependencies

```
T-01 → T-14
T-02 → T-03 → T-04
T-05 → T-06 → T-07
T-04 → T-08, T-12
T-07 → T-09, T-13
T-08, T-09 → T-14
```
