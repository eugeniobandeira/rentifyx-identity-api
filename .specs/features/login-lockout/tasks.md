# Login Lockout — Tasks

## Status: In Progress

---

## T-01 — Domain: add lockout fields and mutation methods to `UserEntity`

**What:** Add `FailedLoginAttempts` (int, default 0) and `LockoutUntil` (DateTimeOffset?) to `UserEntity`. Add two mutation methods:
- `RecordFailedLogin(DateTimeOffset now)` — increments counter; when it reaches 5, sets `LockoutUntil = now + 15 min`
- `ClearLockout()` — resets `FailedLoginAttempts = 0` and `LockoutUntil = null`
- Add read-only property `IsLockedOut(DateTimeOffset now)` → `LockoutUntil.HasValue && now < LockoutUntil.Value`

Also add `FailedLoginAttempts` and `LockoutUntil` to the `Reconstitute(...)` factory.

**Where:** `02-src/03-Domain/RentifyxIdentity.Domain/Entities/UserEntity.cs`

**Reuses:** Existing mutation method patterns (`SetRefreshToken`, `ClearRefreshToken`)

**Done when:**
- Properties compile with no warnings
- `RecordFailedLogin` sets `LockoutUntil` on the 5th call
- `ClearLockout` zeroes the counter and nulls `LockoutUntil`
- `Reconstitute` includes the new parameters

**Tests:** Unit tests in T-02

**Gate:** `dotnet build RentifyxIdentity.slnx --no-incremental -c Release` passes

---

## T-02 — Tests: unit tests for `UserEntity` lockout methods

**What:** Create `UserEntityTests.cs` with the following test cases:
- `RecordFailedLogin_IncreasesCounter`
- `RecordFailedLogin_OnFifthCall_SetsLockoutUntil`
- `RecordFailedLogin_BeyondFive_DoesNotExtendLockout` (idempotent beyond 5)
- `ClearLockout_ResetsCounterAndLockoutUntil`
- `IsLockedOut_ReturnsTrueWhenWithinWindow`
- `IsLockedOut_ReturnsFalseWhenExpired`

**Where:** New file `03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers/Domain/UserEntityTests.cs`

**Depends on:** T-01

**Done when:** 6 tests pass, 0 warnings

**Gate:** `dotnet test --filter "FullyQualifiedName~UserEntityTests" --no-build` all green

---

## T-03 — Domain: add `LoginLocked` error code

**What:** Add constant to `UserErrorCodes`:
```csharp
public const string LoginLocked = "User.LoginLocked";
```

**Where:** `02-src/03-Domain/RentifyxIdentity.Domain/Constants/UserErrorCodes.cs`

**Depends on:** none

**Done when:** constant exists and compiles

**Gate:** build passes

---

## T-04 — Infrastructure: add lockout fields to `UserDynamoDbItem` and mapper

**What:**
1. Add to `UserDynamoDbItem`:
   - `public int FailedLoginAttempts { get; set; }` with `[DynamoDBProperty("FailedLoginAttempts")]`
   - `public long? LockoutUntilEpoch { get; set; }` with `[DynamoDBProperty("LockoutUntil")]` — stores Unix seconds for DynamoDB TTL compatibility
2. Update `UserDynamoDbMapper.ToItem()`: set `FailedLoginAttempts` and `LockoutUntilEpoch` (convert `DateTimeOffset?` → `long?` via `.ToUnixTimeSeconds()`)
3. Update `UserDynamoDbMapper.ToEntity()`: read `FailedLoginAttempts` and convert `LockoutUntilEpoch` → `DateTimeOffset?` via `DateTimeOffset.FromUnixTimeSeconds()`; pass both to `Reconstitute`

**Where:**
- `02-src/05-Infrastructure/RentifyxIdentity.Infrastructure/Models/UserDynamoDbItem.cs`
- `02-src/05-Infrastructure/RentifyxIdentity.Infrastructure/Mapping/UserDynamoDbMapper.cs`

**Depends on:** T-01

**Done when:** mapper roundtrip preserves both fields; build clean

**Gate:** `dotnet build RentifyxIdentity.slnx --no-incremental -c Release` passes

---

## T-05 — Application: update `LoginHandler` with lockout logic

**What:** Modify `LoginHandler.Handle` to:
1. After retrieving the user (and before the status checks): call `user.IsLockedOut(DateTimeOffset.UtcNow)`; if true, return `Error.Custom(429, UserErrorCodes.LoginLocked, "Account is temporarily locked due to too many failed login attempts.")`
2. After the wrong-password check: call `user.RecordFailedLogin(DateTimeOffset.UtcNow)` and `await repository.UpdateAsync(user, ct)`, then return the existing `InvalidCredentials` error
3. After setting the refresh token (successful login): call `user.ClearLockout()` before `repository.UpdateAsync` (consolidate into the single existing `UpdateAsync` call)

**Where:** `02-src/02-Application/RentifyxIdentity.Application/Features/Identity/Auth/Login/LoginHandler.cs`

**Depends on:** T-01, T-03

**Note on `Error.Custom`:** `ErrorOr` exposes `Error.Custom(int type, string code, string description)`. HTTP 429 maps to type 429. The `ToProblem()` extension already handles unknown types by returning 500 — verify it handles 429 correctly, or add the mapping in T-06.

**Done when:** handler compiles, logic matches spec LOCK-01 through LOCK-08

**Gate:** build passes

---

## T-06 — API: ensure 429 maps correctly in `ToProblem()`

**What:** Check `Api/Extensions/ErrorExtensions.cs` (or wherever `ToProblem` is defined). Add a case for HTTP 429 (or the `ErrorType` used by `Error.Custom(429, ...)`) so locked accounts receive a proper 429 response instead of 500.

**Where:** `02-src/01-Api/RentifyxIdentity.Api/Extensions/ErrorExtensions.cs` (verify path)

**Depends on:** T-05

**Done when:** a `Error.Custom(429, ...)` result produces HTTP 429 in integration tests

**Gate:** build passes

---

## T-07 — Tests: handler unit tests for lockout scenarios

**What:** Add to `LoginHandlerTests.cs` (or create if missing):
- `Handle_WhenAccountIsLocked_Returns429LoginLocked`
- `Handle_WhenWrongPassword_IncrementsFailedAttempts`
- `Handle_WhenFifthWrongPassword_TriggersLockout`
- `Handle_WhenCorrectPassword_ClearsLockoutCounter`
- `Handle_WhenLockedOut_DoesNotVerifyPassword`
- `Handle_WhenUnknownEmail_DoesNotIncrementCounter` (verify `UpdateAsync` never called with counter change)
- `Handle_WhenAccountLockExpired_ProceedsNormally`

Use `Mock<IUserRepository>` (existing pattern). Setup `GetByEmailAsync` to return a `UserEntity` built via `Reconstitute` with controlled `FailedLoginAttempts` and `LockoutUntil`.

**Where:** `03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers/Features/Identity/LoginHandlerTests.cs`

**Depends on:** T-05, T-01

**Done when:** all 7 tests pass, `UpdateAsync` call verified via `Mock.Verify`

**Gate:** `dotnet test --filter "FullyQualifiedName~LoginHandlerTests" --no-build` all green

---

## T-08 — Tests: repository integration test for lockout field roundtrip

**What:** Add to `UserRepositoryTests.cs`:
- `UpdateAsync_PersistsFailedLoginAttempts` — store user with `FailedLoginAttempts = 3`, read back, assert value
- `UpdateAsync_PersistsLockoutUntil` — store user with `LockoutUntil = now + 15 min`, read back, assert value (allow ±1 second from Unix epoch rounding)

**Where:** `03-tests/04-Repositories/RentifyxIdentity.Tests.Repositories/Features/Identity/UserRepositoryTests.cs`

**Depends on:** T-04

**Done when:** 2 new repository tests pass against LocalStack

**Gate:** `dotnet test --filter "FullyQualifiedName~UserRepositoryTests" --no-build` all green

---

## T-09 — Tests: integration test for 429 response

**What:** Add to the login integration test file:
- `Login_WhenAccountLocked_Returns429` — seed a user with `LockoutUntil = UtcNow + 10 min` via repository, POST to `/api/v1/auth/login`, assert 429 and `User.LoginLocked` in problem details

**Where:** `03-tests/05-Integration/RentifyxIdentity.Tests.Integration/Features/Identity/LoginIntegrationTests.cs`

**Depends on:** T-05, T-06

**Done when:** 1 integration test passes

**Gate:** `dotnet test --filter "FullyQualifiedName~LoginIntegrationTests" --no-build` all green

---

## T-10 — State: update STATE.md and ROADMAP.md

**What:**
- Add `D-017` to `STATE.md`: lockout state stored on `UserEntity` as `FailedLoginAttempts` + `LockoutUntilEpoch` (Unix seconds, DynamoDB TTL-compatible); no separate lockout item
- Mark login lockout as COMPLETE in `ROADMAP.md` v1.1.0 section

**Where:** `.specs/project/STATE.md`, `.specs/project/ROADMAP.md`

**Depends on:** T-09

**Done when:** docs updated

**Gate:** n/a

---

## Execution Order

```
T-03 (error code)  ──┐
T-01 (entity)      ──┤── T-02 (entity tests)
                     │── T-04 (infra mapper) ──── T-08 (repo tests)
                     │── T-05 (handler)      ──── T-06 (ToProblem) ── T-07 (handler tests)
                                                                    └── T-09 (integration)
                                                                          └── T-10 (docs)
```

T-01 and T-03 can be done in parallel. T-02, T-04, and T-05 all depend on T-01.
