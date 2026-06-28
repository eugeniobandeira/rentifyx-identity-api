# LGPD Endpoints — Spec

## Goal

Implement the three LGPD rights endpoints under `GET|DELETE /api/v1/users/me`, enabling authenticated users to view their profile, export their data, and erase their account. These satisfy LGPD Art. 18 (rights of access, portability, and erasure).

## Authentication model (pre-E-04)

Real Cognito JWT is deferred to E-04. For now:
- `AddAuthentication()` is registered (no default scheme) — Cognito JWT bearer configured in E-04.
- `UseAuthentication()` is in the middleware pipeline so `HttpContext.User` is populated when a scheme is active.
- Endpoints manually check `ClaimTypes.NameIdentifier` claim and return `401 Unauthorized` if missing.
- Integration tests use `TestAuthHandler` (registered by `CustomWebApplicationFactory`) that reads the user ID from `Authorization: Bearer <guid>`.

## Requirements

### GetProfile — `GET /api/v1/users/me`

| ID | Requirement |
|---|---|
| GP-01 | Returns 200 + `UserResponse` if user found and not deleted |
| GP-02 | Returns 401 if `ClaimTypes.NameIdentifier` claim is absent or not a valid Guid |
| GP-03 | Returns 404 (`User.NotFound`) if no user with that ID exists |
| GP-04 | Returns 404 (`User.NotFound`) if user exists but `Status == Deleted` (PII already anonymized) |
| GP-05 | Active and Suspended users always get their profile (transparency right) |

### DeleteAccount — `DELETE /api/v1/users/me`

| ID | Requirement |
|---|---|
| DA-01 | Returns 204 after soft-deleting the account: `ClearRefreshToken()` + `Anonymize()` (email → `deleted_{id}@anonymized.local`, TaxId → `ANONYMIZED`, password → `ANONYMIZED`) |
| DA-02 | Returns 401 if unauthenticated |
| DA-03 | Returns 404 (`User.NotFound`) if user does not exist |
| DA-04 | Returns 409 (`User.AlreadyDeleted`) if `Status == Deleted` |
| DA-05 | Logs `UserAccountDeleted` domain event |
| DA-06 | `ClearRefreshToken()` is called before `Anonymize()` to prevent any lingering sessions |

### ExportData — `GET /api/v1/users/me/data-export`

| ID | Requirement |
|---|---|
| ED-01 | Returns 200 + `UserDataExportResponse` for active or suspended users |
| ED-02 | Returns 401 if unauthenticated |
| ED-03 | Returns 404 (`User.NotFound`) if user not found or `Status == Deleted` |
| ED-04 | Export includes: `Id`, `Email` (actual), `TaxId` (masked via `TaxDocument.ToString()`), `Role`, `Status`, `CreatedAt` |

## Domain changes

| Change | Rationale |
|---|---|
| `UserErrorCodes.AlreadyDeleted = "User.AlreadyDeleted"` | Needed for DA-04 |
| `UserAccountDeleted` domain event (`Guid UserId, DateTimeOffset OccurredAt`) | Logged at account erasure |

## Application layer

| File | Type | Notes |
|---|---|---|
| `User/GetProfile/Request/GetProfileRequest.cs` | `record(Guid UserId)` | UserId from JWT claim |
| `User/GetProfile/Validator/GetProfileValidator.cs` | Validates `UserId` NotEmpty | Guid.Empty guard |
| `User/GetProfile/GetProfileHandler.cs` | `IHandler<GetProfileRequest, UserResponse>` | GP-01 to GP-05 |
| `User/DeleteAccount/Request/DeleteAccountRequest.cs` | `record(Guid UserId)` | |
| `User/DeleteAccount/Validator/DeleteAccountValidator.cs` | Validates `UserId` NotEmpty | |
| `User/DeleteAccount/DeleteAccountHandler.cs` | `IHandler<DeleteAccountRequest, Success>` | DA-01 to DA-06 |
| `User/ExportData/Request/ExportDataRequest.cs` | `record(Guid UserId)` | |
| `User/ExportData/UserDataExportResponse.cs` | `record(Guid Id, string Email, string TaxId, string Role, string Status, DateTimeOffset CreatedAt)` | |
| `User/ExportData/Validator/ExportDataValidator.cs` | Validates `UserId` NotEmpty | |
| `User/ExportData/ExportDataHandler.cs` | `IHandler<ExportDataRequest, UserDataExportResponse>` | ED-01 to ED-04 |

## Infrastructure / IoC

- `InfrastructureDependencyInjection.cs` — add `AddAuthentication()` + `AddAuthorization()`
- `Program.cs` — add `UseAuthentication()` after `UseCorsPolicy()`

## API endpoints

All under `/api/v1/users/` via `MapVersionedApi(1)`:

| File | Method + Path | Response | Tag |
|---|---|---|---|
| `Endpoints/Users/GetProfile.cs` | `GET /users/me` | 200 / 401 / 404 | USERS |
| `Endpoints/Users/DeleteAccount.cs` | `DELETE /users/me` | 204 / 401 / 404 / 409 | USERS |
| `Endpoints/Users/ExportData.cs` | `GET /users/me/data-export` | 200 / 401 / 404 | USERS |

## Tests

| File | Type | Tests |
|---|---|---|
| `Tests.Integration/Auth/TestAuthHandler.cs` | Infrastructure | Reads `Authorization: Bearer <guid>`, sets `ClaimTypes.NameIdentifier` |
| `CustomWebApplicationFactory.cs` (modified) | Infrastructure | Registers `TestAuthHandler` as default scheme |
| `GetProfileValidatorTests.cs` | Validators | ValidUserId passes; Guid.Empty fails |
| `DeleteAccountValidatorTests.cs` | Validators | ValidUserId passes; Guid.Empty fails |
| `ExportDataValidatorTests.cs` | Validators | ValidUserId passes; Guid.Empty fails |
| `GetProfileHandlerTests.cs` | Handlers | Happy path, not found, deleted, validation failure |
| `DeleteAccountHandlerTests.cs` | Handlers | Happy path, not found, already deleted, validation failure |
| `ExportDataHandlerTests.cs` | Handlers | Happy path, not found, deleted, suspended, validation failure |
| `LgpdEndpointTests.cs` | Integration | GetProfile, DeleteAccount, ExportData — authenticated + unauthenticated flows |

## Out of scope

- Real JWT bearer with Cognito JWKS validation (E-04)
- `UseAuthorization()` + `RequireAuthorization()` (E-04 — requires a configured challenge scheme)
- Audit log for LGPD data access/export requests (E-05)
- Consent records (E-05)
