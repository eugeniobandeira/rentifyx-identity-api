# Refresh Token — Task Breakdown

## Status Legend

| Symbol | Meaning |
|---|---|
| ✅ | Pending |
| ✅ | Complete |

## Tasks

| # | Layer | What | Status |
|---|---|---|---|
| T-01 | Application | `RefreshTokenRequest.cs` → `record(string Email, string RefreshToken)` | ✅ |
| T-02 | Application | `RefreshTokenValidator.cs` → Email (NotEmpty + EmailAddress), RefreshToken (NotEmpty) | ✅ |
| T-03 | Application | `RefreshTokenHandler.cs` → full rotation logic; returns `LoginResponse` (reuse) | ✅ |
| T-04 | API | `RefreshToken.cs` endpoint: `POST /auth/refresh` → `Results.Ok(response)` | ✅ |
| T-05 | Tests.Common | `RefreshTokenRequestBuilder.cs` | ✅ |
| T-06 | Tests | `RefreshTokenValidatorTests.cs` (V-01 to V-04) | ✅ |
| T-07 | Tests | `RefreshTokenHandlerTests.cs` (H-01 to H-09) | ✅ |
| T-08 | Tests | `RefreshTokenEndpointTests.cs` (I-01 to I-02) | ✅ |

## Dependencies

```
T-01 → T-02 → T-03
T-03 → T-04, T-07
T-04 → T-08
T-05 → T-07, T-08
```
