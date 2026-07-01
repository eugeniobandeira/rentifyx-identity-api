# Login Lockout — Feature Spec

## Problem

The `LoginHandler` has no brute-force protection. An attacker can make unlimited password guesses
without any rate limit. This violates OWASP A07 (Identification and Authentication Failures) and is a
mandatory control for a production Identity API.

## Solution

Track failed login attempts on `UserEntity`. After 5 consecutive failures, set a `LockoutUntil`
timestamp 15 minutes in the future and persist it via `UpdateAsync`. On every login attempt, check
`LockoutUntil` before verifying the password; return 429 with `LOGIN_LOCKED` if the lockout is
active. Reset the counter and clear `LockoutUntil` on successful login.

The lockout is a **logical TTL** enforced in application code — `LockoutUntil` is stored as a
nullable `DateTimeOffset` on `UserEntity` and mapped to a numeric DynamoDB attribute so DynamoDB TTL
can auto-clear expired lockouts at the persistence layer without a compensating write.

## Design Choices

| Decision | Choice | Rationale |
|---|---|---|
| Storage | Fields on `UserEntity` (`FailedLoginAttempts` int, `LockoutUntil` DateTimeOffset?) | Counter and lockout co-locate with the user record; single `UpdateAsync` call; no extra GSI |
| DynamoDB TTL | `LockoutUntil` mapped to `LockoutUntilEpoch` (long, Unix seconds) + DynamoDB TTL attribute on item | Auto-clears stale lockout state without a compensating write after expiry |
| Check order in handler | lockout check → status check → password check | Fail-fast; avoids unnecessary DB roundtrip on locked accounts |
| Counter reset | On successful password verification, before issuing tokens | Ensures counter is cleared only when auth truly succeeded |
| No enumeration | Lockout check happens before the no-enumeration `InvalidCredentials` path | Locked accounts leak existence — acceptable; lockout itself is evidence of active targeting |

## Requirements

| ID | Requirement | Priority |
|---|---|---|
| LOCK-01 | When a user fails password verification, `FailedLoginAttempts` SHALL be incremented by 1 and persisted | Must |
| LOCK-02 | When `FailedLoginAttempts` reaches 5, `LockoutUntil` SHALL be set to `UtcNow + 15 minutes` | Must |
| LOCK-03 | When `LockoutUntil` is set and `UtcNow < LockoutUntil`, the handler SHALL return HTTP 429 with error code `User.LoginLocked` | Must |
| LOCK-04 | The 429 response SHALL NOT disclose the remaining lockout duration or attempt count | Must |
| LOCK-05 | On successful login, `FailedLoginAttempts` SHALL be reset to 0 and `LockoutUntil` cleared | Must |
| LOCK-06 | Accounts already locked (LOCK-03) SHALL return 429 before any password verification is attempted | Must |
| LOCK-07 | Once `LockoutUntil` has elapsed, the next login attempt SHALL proceed normally (counter still at 5 until a success resets it, or until the next failure re-triggers lockout) | Must |
| LOCK-08 | `FailedLoginAttempts` counter SHALL only increment on wrong password — not on unknown email, unverified, or suspended account errors | Must |
| LOCK-09 | `UserEntity` SHALL expose `RecordFailedLogin(DateTimeOffset now)` and `ClearLockout()` mutation methods | Must |
| LOCK-10 | `UserErrorCodes.LoginLocked` constant SHALL be added (`"User.LoginLocked"`) | Must |

## Acceptance Criteria

### LOCK-01/02 — Counter and lockout trigger

GIVEN an active account with `FailedLoginAttempts = 4`
WHEN the user submits an incorrect password
THEN `FailedLoginAttempts` becomes 5, `LockoutUntil` is set to `now + 15 min`, and the response is HTTP 401 `InvalidCredentials` (the *triggering* attempt still gets the normal failure response)

### LOCK-03 — Locked account returns 429

GIVEN an account with `LockoutUntil = now + 10 min`
WHEN the user attempts to login (any password)
THEN the handler returns HTTP 429 with `User.LoginLocked` and does NOT verify the password

### LOCK-05 — Counter reset on success

GIVEN an account with `FailedLoginAttempts = 3`, `LockoutUntil = null`
WHEN the user submits the correct password
THEN `FailedLoginAttempts` becomes 0, `LockoutUntil` remains null, and tokens are issued

### LOCK-07 — Expired lockout clears on next attempt

GIVEN an account with `LockoutUntil = 1 minute ago`
WHEN the user attempts to login
THEN the lockout check passes and login proceeds normally

### LOCK-08 — Counter unchanged for non-password failures

GIVEN an unknown email, OR account status PendingVerification/Suspended/Deleted
WHEN login is attempted
THEN `FailedLoginAttempts` is NOT modified and no `UpdateAsync` is called for the counter

## Out of Scope

- IP-based rate limiting (separate infrastructure concern)
- Admin unlock endpoint (future)
- Notification email on lockout (future)
- Counter decrement on partial failures (reset-only on success)

## Affected Files

| Layer | File | Change |
|---|---|---|
| Domain | `UserEntity.cs` | Add `FailedLoginAttempts`, `LockoutUntil` fields; add `RecordFailedLogin()`, `ClearLockout()` methods |
| Domain | `UserErrorCodes.cs` | Add `LoginLocked = "User.LoginLocked"` |
| Application | `LoginHandler.cs` | Add lockout check (pre-password), counter increment, reset on success |
| Infrastructure | `UserDynamoDbItem.cs` | Add `FailedLoginAttempts` (int), `LockoutUntilEpoch` (long?) mapped to DynamoDB TTL attribute |
| Infrastructure | `UserDynamoDbMapper.cs` | Map new fields in `ToItem()` / `ToEntity()` |
| Tests | `UserEntityTests.cs` (new) | Unit tests for `RecordFailedLogin` and `ClearLockout` |
| Tests | `LoginHandlerTests.cs` | Add lockout scenarios (7–10 new tests) |
| Tests | `UserRepositoryTests.cs` | Verify `FailedLoginAttempts` persisted and read back |
