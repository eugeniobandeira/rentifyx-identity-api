# Login — Task Breakdown

## Status Legend

| Symbol | Meaning |
|---|---|
| ✅ | Pending |
| 🔄 | In Progress |
| ✅ | Complete |

---

## Tasks

| # | Layer | What | Status |
|---|---|---|---|
| T-01 | Domain | Add `RefreshTokenHash` (string?), `RefreshTokenExpiry` (DateTimeOffset?), and `SetRefreshToken(string hash, DateTimeOffset expiry)` to `UserEntity` | ✅ |
| T-02 | Infrastructure | Create `TokenService.cs` implementing `ITokenService` (stub: placeholder tokens, real Cognito deferred to E-04) | ✅ |
| T-03 | Application | Create `LoginRequest.cs` → `record(string Email, string Password)` | ✅ |
| T-04 | Application | Create `LoginValidator.cs` → Email (NotEmpty + EmailAddress), Password (NotEmpty) | ✅ |
| T-05 | Application | Create `LoginResponse.cs` → `record(string AccessToken, string RefreshToken, UserResponse User)` | ✅ |
| T-06 | Application | Create `LoginHandler.cs` implementing full login logic per spec | ✅ |
| T-07 | IoC | Register `ITokenService → TokenService` as Singleton in `InfrastructureDependencyInjection` | ✅ |
| T-08 | API | Create `Login.cs` endpoint: `POST /auth/login` → `Results.Ok(response)` | ✅ |
| T-09 | Tests.Common | Create `LoginRequestBuilder.cs` using Bogus | ✅ |
| T-10 | Tests | Create `LoginValidatorTests.cs` (V-01 to V-04) | ✅ |
| T-11 | Tests | Create `LoginHandlerTests.cs` (H-01 to H-07) | ✅ |
| T-12 | Tests | Create `LoginEndpointTests.cs` (I-01 to I-03) | ✅ |

## Dependencies

```
T-01 → T-06
T-02 → T-07 → T-06
T-03 → T-04 → T-06
T-05 → T-06
T-06 → T-08, T-11
T-08 → T-12
T-09 → T-11, T-12
```
