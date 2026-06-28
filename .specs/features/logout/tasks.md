# Logout — Task Breakdown

## Status Legend

| Symbol | Meaning |
|---|---|
| ✅ | Pending |
| ✅ | Complete |

## Tasks

| # | Layer | What | Status |
|---|---|---|---|
| T-01 | Domain | Add `ClearRefreshToken()` to `UserEntity` | ✅ |
| T-02 | Application | `LogoutRequest.cs` → `record(string Email, string RefreshToken)` | ✅ |
| T-03 | Application | `LogoutValidator.cs` → Email (NotEmpty + EmailAddress), RefreshToken (NotEmpty) | ✅ |
| T-04 | Application | `LogoutHandler.cs` → idempotent token clear; returns `ErrorOr<Success>` | ✅ |
| T-05 | API | `Logout.cs` endpoint: `POST /auth/logout` → `Results.NoContent()` | ✅ |
| T-06 | Tests | `LogoutValidatorTests.cs` (V-01 to V-04) | ✅ |
| T-07 | Tests | `LogoutHandlerTests.cs` (H-01 to H-05) | ✅ |
| T-08 | Tests | `LogoutEndpointTests.cs` (I-01 to I-02) | ✅ |

## Dependencies

```
T-01 → T-04
T-02 → T-03 → T-04
T-04 → T-05, T-07
T-05 → T-08
```
