# Implementation Plan — Feature: Identity (RentifyX)

```
PROJECT CONTEXT
- Language / framework: C# / .NET 10, Minimal APIs
- Package manager: NuGet (centralized via Directory.Packages.props)
- Architectural pattern: Clean Architecture (5-layer)
- Implementation order: Domain → Application → Infrastructure (stubs) → IoC → API → Tests
- Modules / domains: Identity (User aggregate — new), Examples (reference implementation)
- Naming conventions: {Action}{Entity}Handler, {Entity}Repository, {Action}.cs (endpoint),
                      {Entity}ErrorCodes, {Feature}_{Condition}_{Expected} (tests)
- DI / IoC pattern: Reflection-based auto-discovery for IHandler<,> and IRepository<>;
                    explicit registration required for domain services (ITokenService, IEmailService)
- Error handling: ErrorOr<T> → result.Match(..., errors => errors.ToProblem(httpContext))
- Validation: FluentValidation validators, ValidateToErrorsAsync extension, message resources (.resx)
- Test framework: xUnit + Moq + FluentAssertions + Bogus; layered by 01-Common/02-Validators/
                  03-Handlers/04-Repositories/05-Integration
- Migration tool: None (DynamoDB — schema-less; DynamoDB wiring deferred to E-04)
```

---

## 1. Feature Summary

Building the full **User identity domain** for RentifyX, covering:

- **Domain**: `User` aggregate, 3 value objects (`Email`, `TaxDocument`, `Password`), 2 enums (`UserRole`, `UserStatus`), 4 domain events, 2 service interfaces, 1 repository interface.
- **Application**: 10 use-case handlers across two sub-groups — `Auth` (Register, VerifyEmail, Login, Refresh, Logout, ForgotPassword, ResetPassword) and `Users` (GetProfile, DeleteAccount, ExportData).
- **Infrastructure**: Stub implementations for `UserRepository`, `TokenService`, `EmailService` (real AWS wiring deferred to E-04).
- **API**: 10 Minimal API endpoints — 7 public auth endpoints and 3 authenticated user endpoints.
- **Tests**: Validator unit tests, handler unit tests (mocked dependencies), integration tests (happy path + error cases).

Maps directly to E-02 (Domain), E-03 (Application), and E-05 (API) from `docs/features/identity.md`.

---

## 2. Impacted Areas

| File / Module | Layer | Change | Reason |
|---|---|---|---|
| `Domain/Enums/UserRole.cs` | Domain | New | Role enum from spec |
| `Domain/Enums/UserStatus.cs` | Domain | New | Status machine from spec |
| `Domain/ValueObjects/Email.cs` | Domain | New | VO with format + disposable-domain validation |
| `Domain/ValueObjects/TaxDocument.cs` | Domain | New | CPF/CNPJ with mod-11 validation, masked ToString |
| `Domain/ValueObjects/Password.cs` | Domain | New | Complexity validation, stores hash not plaintext |
| `Domain/Entities/UserEntity.cs` | Domain | New | User aggregate root |
| `Domain/Events/UserRegistered.cs` | Domain | New | Domain event |
| `Domain/Events/UserEmailVerified.cs` | Domain | New | Domain event |
| `Domain/Events/UserPasswordChanged.cs` | Domain | New | Domain event |
| `Domain/Events/UserSuspended.cs` | Domain | New | Domain event |
| `Domain/Interfaces/Users/IUserRepository.cs` | Domain | New | Repository contract |
| `Domain/Interfaces/Users/ITokenService.cs` | Domain | New | JWT/refresh/verification token contract |
| `Domain/Interfaces/Users/IEmailService.cs` | Domain | New | SES email contract |
| `Domain/Constants/UserErrorCodes.cs` | Domain | New | Namespaced error codes |
| `Domain/Constants/ValidationConstants.cs` | Domain | Modify | Add `UserRules` nested class |
| `Domain/MessageResource/ValidationMessageResource.resx` | Domain | Modify | Add user-domain messages |
| `Application/Features/Identity/UserResponse.cs` | Application | New | Shared user response DTO |
| `Application/Features/Identity/Mapper/UserMapper.cs` | Application | New | Entity ↔ DTO mapping |
| `Application/Features/Identity/Auth/Register/…` (3 files) | Application | New | Request + Validator + Handler |
| `Application/Features/Identity/Auth/VerifyEmail/…` (3 files) | Application | New | Request + Validator + Handler |
| `Application/Features/Identity/Auth/Login/…` (4 files) | Application | New | Request + Validator + Handler + LoginResponse |
| `Application/Features/Identity/Auth/Refresh/…` (3 files) | Application | New | Request + Validator + Handler + RefreshResponse |
| `Application/Features/Identity/Auth/Logout/…` (2 files) | Application | New | Request + Handler (no validator) |
| `Application/Features/Identity/Auth/ForgotPassword/…` (3 files) | Application | New | Request + Validator + Handler |
| `Application/Features/Identity/Auth/ResetPassword/…` (3 files) | Application | New | Request + Validator + Handler |
| `Application/Features/Identity/Users/GetProfile/…` (2 files) | Application | New | Request + Handler |
| `Application/Features/Identity/Users/DeleteAccount/…` (2 files) | Application | New | Request + Handler |
| `Application/Features/Identity/Users/ExportData/…` (3 files) | Application | New | Request + Handler + ExportDataResponse |
| `Infrastructure/Repositories/UserRepository.cs` | Infrastructure | New | Stub (DynamoDB in E-04) |
| `Infrastructure/Services/TokenService.cs` | Infrastructure | New | Stub (Cognito/JWT in E-04) |
| `Infrastructure/Services/EmailService.cs` | Infrastructure | New | Stub (SES in E-04) |
| `IoC/InfrastructureDependencyInjection.cs` | IoC | Modify | Register ITokenService, IEmailService |
| `Api/Endpoints/Tags.cs` | API | Modify | Add `AUTH`, `USERS` constants |
| `Api/Endpoints/Auth/Register.cs` | API | New | POST /auth/register |
| `Api/Endpoints/Auth/VerifyEmail.cs` | API | New | POST /auth/verify-email |
| `Api/Endpoints/Auth/Login.cs` | API | New | POST /auth/login |
| `Api/Endpoints/Auth/Refresh.cs` | API | New | POST /auth/refresh |
| `Api/Endpoints/Auth/Logout.cs` | API | New | POST /auth/logout |
| `Api/Endpoints/Auth/ForgotPassword.cs` | API | New | POST /auth/forgot-password |
| `Api/Endpoints/Auth/ResetPassword.cs` | API | New | POST /auth/reset-password |
| `Api/Endpoints/Users/GetProfile.cs` | API | New | GET /users/me |
| `Api/Endpoints/Users/DeleteAccount.cs` | API | New | DELETE /users/me |
| `Api/Endpoints/Users/ExportData.cs` | API | New | GET /users/me/data-export |
| `Tests.Common/Builders/UserBuilder.cs` | Test | New | Bogus builder for UserEntity |
| `Tests.Validators/Features/Identity/…` (5 test files) | Test | New | Validator unit tests |
| `Tests.Handlers/Features/Identity/…` (10 test files) | Test | New | Handler unit tests (Moq) |
| `Tests.Integration/Api/Identity/…` (2 test files) | Test | New | End-to-end happy + error paths |

---

## 3. Data Model Changes

### User Entity Fields

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | `Guid.NewGuid()` at creation |
| `Email` | `Email` (VO) | Immutable after registration |
| `TaxId` | `TaxDocument` (VO) | CPF or CNPJ; encrypted at rest (E-04) |
| `PasswordHash` | `Password` (VO) | Stores hash; never exposes plaintext |
| `Role` | `UserRole` | Set at registration; not user-changeable |
| `Status` | `UserStatus` | Starts `PendingVerification` |
| `EmailVerificationTokenHash` | `string?` | HMAC-SHA256 hash; null after verification |
| `EmailVerificationTokenExpiry` | `DateTimeOffset?` | 24h from registration |
| `PasswordResetTokenHash` | `string?` | HMAC-SHA256 hash; null after use |
| `PasswordResetTokenExpiry` | `DateTimeOffset?` | 1h from request |
| `CreatedAt` | `DateTimeOffset` | UTC, set at creation |

### Token Model (managed by ITokenService, not stored in User aggregate)

- **Access JWT**: RSA-2048 signed, 15 min TTL — stateless, never persisted
- **Refresh token**: random bytes, stored as HMAC-SHA256 hash in DynamoDB with TTL 7d
- **Verification token / Reset token**: HMAC-SHA256, single-use, stored as hash in `UserEntity`

### No migrations needed
DynamoDB is schema-less. Table design and index definitions are deferred to E-04 (ADR-005).

---

## 4. API / Interface Contract

### Auth Endpoints (public — no authentication required)

| Method | Route | Success | Body |
|---|---|---|---|
| `POST` | `/api/v1/auth/register` | 201 Created | `UserResponse` |
| `POST` | `/api/v1/auth/verify-email` | 200 OK | `UserResponse` |
| `POST` | `/api/v1/auth/login` | 200 OK | `LoginResponse` |
| `POST` | `/api/v1/auth/refresh` | 200 OK | `RefreshResponse` |
| `POST` | `/api/v1/auth/logout` | 204 No Content | — |
| `POST` | `/api/v1/auth/forgot-password` | 204 No Content | — |
| `POST` | `/api/v1/auth/reset-password` | 200 OK | `UserResponse` |

### User Endpoints (authenticated — JWT required)

| Method | Route | Success | Body |
|---|---|---|---|
| `GET` | `/api/v1/users/me` | 200 OK | `UserResponse` |
| `DELETE` | `/api/v1/users/me` | 204 No Content | — |
| `GET` | `/api/v1/users/me/data-export` | 200 OK | `ExportDataResponse` |

### Request / Response Shapes

| DTO | Fields |
|---|---|
| `RegisterUserRequest` | `Email(string)`, `TaxId(string)`, `Password(string)`, `Role(string)` |
| `LoginRequest` | `Email(string)`, `Password(string)` |
| `LoginResponse` | `AccessToken(string)`, `RefreshToken(string)`, `ExpiresAt(DateTimeOffset)` |
| `RefreshRequest` | `RefreshToken(string)` |
| `RefreshResponse` | `AccessToken(string)`, `RefreshToken(string)`, `ExpiresAt(DateTimeOffset)` |
| `VerifyEmailRequest` | `Token(string)` |
| `LogoutRequest` | `RefreshToken(string)` |
| `ForgotPasswordRequest` | `Email(string)` |
| `ResetPasswordRequest` | `Token(string)`, `NewPassword(string)` |
| `UserResponse` | `Id(Guid)`, `Email(string)`, `Role(string)`, `Status(string)`, `CreatedAt(DateTimeOffset)` |
| `ExportDataResponse` | `UserId(Guid)`, `Email(string)`, `TaxId(string)` *(masked)*, `Role(string)`, `Status(string)`, `CreatedAt(DateTimeOffset)`, `ExportedAt(DateTimeOffset)` |

### Error Mapping

| Scenario | ErrorOr Type | HTTP Status |
|---|---|---|
| Field validation fails | `Error.Validation` | 422 |
| Email already registered | `Error.Conflict` | 409 |
| TaxId already registered | `Error.Conflict` | 409 |
| User not found | `Error.NotFound` | 404 |
| Invalid credentials | `Error.Unauthorized` | 401 |
| Account not active (suspended/deleted) | `Error.Unauthorized` | 401 |
| Token invalid or expired | `Error.Unauthorized` | 401 |
| Account not yet verified | `Error.Unauthorized` | 401 |
| Unhandled exception | `GlobalExceptionHandler` | 500 |

---

## 5. Step-by-Step Implementation Plan

### Summary Table

| # | Layer | What | Reference file | Depends on |
|---|---|---|---|---|
| 1 | Domain | `UserRole` + `UserStatus` enums | — | — |
| 2 | Domain | `Email` value object | `ExampleEntity.cs` | 1 |
| 3 | Domain | `TaxDocument` value object | `ExampleEntity.cs` | 1 |
| 4 | Domain | `Password` value object | `ExampleEntity.cs` | 1 |
| 5 | Domain | `UserEntity` aggregate root | `ExampleEntity.cs` | 1–4 |
| 6 | Domain | Domain events (4 records) | — | 5 |
| 7 | Domain | `IUserRepository` interface | `IRepository.cs` | 5 |
| 8 | Domain | `ITokenService` + `IEmailService` interfaces | `IRepository.cs` | 5 |
| 9 | Domain | `UserErrorCodes` constants | `ExampleErrorCodes.cs` | — |
| 10 | Domain | `ValidationConstants` — add `UserRules` | `ValidationConstants.cs` | — |
| 11 | Domain | `.resx` — add user-domain messages | `ValidationMessageResource.resx` | — |
| 12 | Application | `UserResponse` + `UserMapper` | `ExampleResponse.cs`, `ExampleMapper.cs` | 5 |
| 13 | Application | `RegisterUser` (Request + Validator + Handler) | `CreateExampleHandler.cs` | 7–12 |
| 14 | Application | `VerifyEmail` (Request + Validator + Handler) | `UpdateExampleHandler.cs` | 7–12 |
| 15 | Application | `Login` (Request + Validator + Handler + `LoginResponse`) | `CreateExampleHandler.cs` | 7–12 |
| 16 | Application | `Refresh` (Request + Validator + Handler + `RefreshResponse`) | `CreateExampleHandler.cs` | 7–12 |
| 17 | Application | `Logout` (Request + Handler) | `DeleteExampleHandler.cs` | 7–12 |
| 18 | Application | `ForgotPassword` (Request + Validator + Handler) | `CreateExampleHandler.cs` | 7–12 |
| 19 | Application | `ResetPassword` (Request + Validator + Handler) | `UpdateExampleHandler.cs` | 7–12 |
| 20 | Application | `GetProfile` (Request + Handler) | `GetByIdExampleHandler.cs` | 7–12 |
| 21 | Application | `DeleteAccount` (Request + Handler) | `DeleteExampleHandler.cs` | 7–12 |
| 22 | Application | `ExportData` (Request + Handler + `ExportDataResponse`) | `GetByIdExampleHandler.cs` | 7–12 |
| 23 | Infrastructure | `UserRepository` stub | `ExampleRepository.cs` | 7 |
| 24 | Infrastructure | `TokenService` stub | `ExampleRepository.cs` | 8 |
| 25 | Infrastructure | `EmailService` stub | `ExampleRepository.cs` | 8 |
| 26 | IoC | Register `ITokenService`, `IEmailService` in `InfrastructureDependencyInjection` | `InfrastructureDependencyInjection.cs` | 24–25 |
| 27 | API | Add `AUTH` + `USERS` to `Tags.cs` | `Tags.cs` | — |
| 28 | API | `POST /auth/register` endpoint | `Create.cs` | 13, 27 |
| 29 | API | `POST /auth/verify-email` endpoint | `Create.cs` | 14, 27 |
| 30 | API | `POST /auth/login` endpoint | `Create.cs` | 15, 27 |
| 31 | API | `POST /auth/refresh` endpoint | `Create.cs` | 16, 27 |
| 32 | API | `POST /auth/logout` endpoint | `Delete.cs` | 17, 27 |
| 33 | API | `POST /auth/forgot-password` endpoint | `Create.cs` | 18, 27 |
| 34 | API | `POST /auth/reset-password` endpoint | `Create.cs` | 19, 27 |
| 35 | API | `GET /users/me` endpoint | `GetById.cs` | 20, 27 |
| 36 | API | `DELETE /users/me` endpoint | `Delete.cs` | 21, 27 |
| 37 | API | `GET /users/me/data-export` endpoint | `GetById.cs` | 22, 27 |
| 38 | Test | `UserBuilder` in `Tests.Common` | `ExampleBuilder.cs` | 5 |
| 39 | Test | Validator tests (5 test classes) | `CreateExampleValidatorTests.cs` | 13–19, 38 |
| 40 | Test | Handler tests (10 test classes) | `CreateExampleHandlerTests.cs` | 13–22, 38 |
| 41 | Test | Integration tests (Auth + Users) | `ExampleEndpointTests.cs` | 28–37 |

---

### Step Details

---
**status:** pending
**title:** UserRole and UserStatus enums
**type:** backend
**complexity:** low
**dependencies:** []

**Layer:** Domain
**Files:**
- `02-src/03-Domain/RentifyxIdentity.Domain/Enums/UserRole.cs`
- `02-src/03-Domain/RentifyxIdentity.Domain/Enums/UserStatus.cs`

**Reference:** No existing enum files; model on `ExampleEntity.cs` namespace pattern.
**What:** Create `UserRole` enum (`Owner`, `Renter`, `Admin`) and `UserStatus` enum (`PendingVerification`, `Active`, `Suspended`, `Deleted`).
**Done when:** Both enums compile and are usable from Domain project.
**Commit:** `feat(domain): add UserRole and UserStatus enums`

---
**status:** pending
**title:** Email value object
**type:** backend
**complexity:** medium
**dependencies:** [1]

**Layer:** Domain
**File:** `02-src/03-Domain/RentifyxIdentity.Domain/ValueObjects/Email.cs`
**Reference:** `ExampleEntity.cs` (private constructor, static factory pattern)
**What:** Create sealed `Email` record with static `Create(string value)` that validates RFC format and rejects a short list of known disposable domains. Expose only lowercase normalized form. `ToString()` returns the normalized email.
**Done when:** `Email.Create("user@example.com")` succeeds; `Email.Create("bad@mailinator.com")` and `Email.Create("notanemail")` throw or return guard errors.
**Commit:** `feat(domain): add Email value object with RFC and disposable-domain validation`

---
**status:** pending
**title:** TaxDocument value object (CPF/CNPJ)
**type:** backend
**complexity:** high
**dependencies:** [1]

**Layer:** Domain
**File:** `02-src/03-Domain/RentifyxIdentity.Domain/ValueObjects/TaxDocument.cs`
**Reference:** `ExampleEntity.cs`
**What:** Create sealed `TaxDocument` record. `Create(string value)` strips formatting, detects CPF (11 digits) vs CNPJ (14 digits), runs mod-11 digit verification for CPF and the CNPJ equivalent check. `ToString()` returns masked form: `***.***.***-**` for CPF, `**.***.***/****-**` for CNPJ. Expose a `DocumentType` enum property (`Cpf`/`Cnpj`).
**Done when:** Valid CPF and CNPJ pass; invalid check digits and all-same-digit sequences (e.g. `111.111.111-11`) are rejected.
**Commit:** `feat(domain): add TaxDocument value object with CPF/CNPJ mod-11 validation`

---
**status:** pending
**title:** Password value object
**type:** backend
**complexity:** medium
**dependencies:** [1]

**Layer:** Domain
**File:** `02-src/03-Domain/RentifyxIdentity.Domain/ValueObjects/Password.cs`
**Reference:** `ExampleEntity.cs`
**What:** Create sealed `Password` record. `Create(string plaintext)` validates: min 12 chars, at least one upper, one lower, one digit, one symbol (OWASP A07). Stores the BCrypt hash internally. Expose `Verify(string plaintext)` for login. `ToString()` returns `"[REDACTED]"` to prevent log leakage.
**Done when:** Valid password hashes on creation and verifies correctly; invalid passwords fail with descriptive error messages; `ToString()` never exposes hash.
**Commit:** `feat(domain): add Password value object with OWASP complexity rules and BCrypt hashing`

---
**status:** pending
**title:** User entity (aggregate root)
**type:** backend
**complexity:** high
**dependencies:** [1, 2, 3, 4]

**Layer:** Domain
**File:** `02-src/03-Domain/RentifyxIdentity.Domain/Entities/UserEntity.cs`
**Reference:** `ExampleEntity.cs`
**What:** Create sealed `UserEntity` with all spec fields plus token fields (`EmailVerificationTokenHash`, `EmailVerificationTokenExpiry`, `PasswordResetTokenHash`, `PasswordResetTokenExpiry`). Implement static factory `Create(Email, TaxDocument, Password, UserRole)` which sets `Status = PendingVerification` and `CreatedAt = DateTimeOffset.UtcNow`. Add instance methods: `SetEmailVerificationToken(string hash, DateTimeOffset expiry)`, `VerifyEmail()` (sets `Status = Active`, clears token), `SetPasswordResetToken(string hash, DateTimeOffset expiry)`, `ResetPassword(Password newPassword)` (clears token), `Suspend(string reason, Guid suspendedBy)`, `Anonymize()` (sets `Status = Deleted`, clears PII fields). Use `ArgumentException.ThrowIfNullOrWhiteSpace` for guard checks.
**Done when:** All factory and state-change methods compile; invalid transitions (e.g. verify an already-active user) are defensible.
**Commit:** `feat(domain): add UserEntity aggregate root with status machine and token management`

---
**status:** pending
**title:** Domain events (4 records)
**type:** backend
**complexity:** low
**dependencies:** [5]

**Layer:** Domain
**Files:**
- `02-src/03-Domain/RentifyxIdentity.Domain/Events/UserRegistered.cs`
- `02-src/03-Domain/RentifyxIdentity.Domain/Events/UserEmailVerified.cs`
- `02-src/03-Domain/RentifyxIdentity.Domain/Events/UserPasswordChanged.cs`
- `02-src/03-Domain/RentifyxIdentity.Domain/Events/UserSuspended.cs`

**Reference:** No existing events; use sealed record pattern.
**What:** Create one sealed record per event with exactly the payload fields from the spec:
- `UserRegistered(Guid UserId, string Email, UserRole Role, DateTimeOffset OccurredAt)`
- `UserEmailVerified(Guid UserId, DateTimeOffset OccurredAt)`
- `UserPasswordChanged(Guid UserId, DateTimeOffset OccurredAt)`
- `UserSuspended(Guid UserId, string Reason, Guid SuspendedBy, DateTimeOffset OccurredAt)`

These are plain data records; no base class required yet (outbox wiring is E-04).
**Done when:** All four compile with correct property types.
**Commit:** `feat(domain): add UserRegistered, UserEmailVerified, UserPasswordChanged, UserSuspended domain events`

---
**status:** pending
**title:** IUserRepository interface
**type:** backend
**complexity:** low
**dependencies:** [5]

**Layer:** Domain
**File:** `02-src/03-Domain/RentifyxIdentity.Domain/Interfaces/Users/IUserRepository.cs`
**Reference:** `IRepository.cs`
**What:** Define `IUserRepository` extending `IRepository<UserEntity>` plus: `GetByEmailAsync(string email, CancellationToken)`, `GetByTaxIdAsync(string taxId, CancellationToken)`. All return `Task<UserEntity?>`.
**Done when:** Interface compiles and is usable as a type constraint.
**Commit:** `feat(domain): add IUserRepository contract with email and taxId lookup methods`

---
**status:** pending
**title:** ITokenService and IEmailService interfaces
**type:** backend
**complexity:** low
**dependencies:** [5]

**Layer:** Domain
**Files:**
- `02-src/03-Domain/RentifyxIdentity.Domain/Interfaces/Users/ITokenService.cs`
- `02-src/03-Domain/RentifyxIdentity.Domain/Interfaces/Users/IEmailService.cs`

**Reference:** `IRepository.cs` (interface pattern)
**What:**
- `ITokenService`: `GenerateAccessToken(UserEntity user) → string`, `GenerateRefreshToken() → string`, `HashToken(string token) → string`, `ValidateRefreshToken(string hash, string token) → bool`
- `IEmailService`: `SendVerificationEmailAsync(string to, string token, CancellationToken) → Task`, `SendPasswordResetEmailAsync(string to, string token, CancellationToken) → Task`

**Done when:** Both interfaces compile cleanly.
**Commit:** `feat(domain): add ITokenService and IEmailService contracts`

---
**status:** pending
**title:** UserErrorCodes constants
**type:** backend
**complexity:** low
**dependencies:** []

**Layer:** Domain
**File:** `02-src/03-Domain/RentifyxIdentity.Domain/Constants/UserErrorCodes.cs`
**Reference:** `ExampleErrorCodes.cs`
**What:** Add string constants: `User.NotFound`, `User.EmailAlreadyRegistered`, `User.TaxIdAlreadyRegistered`, `User.InvalidCredentials`, `User.AccountNotActive`, `User.AccountNotVerified`, `User.TokenInvalidOrExpired`.
**Done when:** Constants are accessible and follow `Namespace.Verb` pattern.
**Commit:** `feat(domain): add UserErrorCodes constants`

---
**status:** pending
**title:** ValidationConstants — UserRules nested class
**type:** backend
**complexity:** low
**dependencies:** []

**Layer:** Domain
**File:** `02-src/03-Domain/RentifyxIdentity.Domain/Constants/ValidationConstants.cs` *(modify)*
**Reference:** `ValidationConstants.cs` (`ExampleRules` nested class)
**What:** Add `UserRules` nested static class with: `EmailMaxLength = 320`, `PasswordMinLength = 12`, `PasswordMaxLength = 128`, `TokenMaxLength = 512`.
**Done when:** `ValidationConstants.UserRules.PasswordMinLength` resolves to 12.
**Commit:** `feat(domain): add UserRules to ValidationConstants`

---
**status:** pending
**title:** Validation message resources — user-domain additions
**type:** backend
**complexity:** low
**dependencies:** []

**Layer:** Domain
**File:** `02-src/03-Domain/RentifyxIdentity.Domain/MessageResource/ValidationMessageResource.resx` *(modify)*
**Reference:** Existing `.resx` file and its Designer.cs
**What:** Add resource strings for: `EMAIL_REQUIRED`, `EMAIL_INVALID_FORMAT`, `EMAIL_DISPOSABLE_DOMAIN`, `EMAIL_MAX_LENGTH`, `PASSWORD_REQUIRED`, `PASSWORD_MIN_LENGTH`, `PASSWORD_COMPLEXITY`, `TAXID_REQUIRED`, `TAXID_INVALID_FORMAT`, `ROLE_REQUIRED`, `ROLE_INVALID`, `TOKEN_REQUIRED`, `USER_NOT_FOUND`, `USER_EMAIL_ALREADY_REGISTERED`, `USER_TAXID_ALREADY_REGISTERED`, `USER_INVALID_CREDENTIALS`, `USER_ACCOUNT_NOT_ACTIVE`, `USER_ACCOUNT_NOT_VERIFIED`, `USER_TOKEN_INVALID_OR_EXPIRED`.
**Done when:** Designer.cs regenerates with all new constants; strings are in pt-BR.
**Commit:** `feat(domain): add user-domain validation messages to resource file`

---
**status:** pending
**title:** UserResponse DTO and UserMapper
**type:** backend
**complexity:** low
**dependencies:** [5]

**Layer:** Application
**Files:**
- `02-src/02-Application/RentifyxIdentity.Application/Features/Identity/UserResponse.cs`
- `02-src/02-Application/RentifyxIdentity.Application/Features/Identity/Mapper/UserMapper.cs`

**Reference:** `ExampleResponse.cs`, `ExampleMapper.cs`
**What:** `UserResponse(Guid Id, string Email, string Role, string Status, DateTimeOffset CreatedAt)`. `UserMapper.ToResponse(UserEntity)` maps entity to DTO using `.ToString()` on enums.
**Done when:** Mapper compiles; sensitive fields (TaxId, PasswordHash) are not included in response.
**Commit:** `feat(application): add UserResponse DTO and UserMapper`

---
**status:** pending
**title:** RegisterUser — Request, Validator, Handler
**type:** backend
**complexity:** medium
**dependencies:** [7, 8, 9, 10, 11, 12]

**Layer:** Application
**Files:**
- `…/Features/Identity/Auth/Register/Request/RegisterUserRequest.cs`
- `…/Features/Identity/Auth/Register/Validator/RegisterUserValidator.cs`
- `…/Features/Identity/Auth/Register/RegisterUserHandler.cs`

**Reference:** `CreateExampleHandler.cs`, `CreateExampleValidator.cs`
**What:**
- Request: `(string Email, string TaxId, string Password, string Role)`
- Validator: Email required + format, Password complexity, TaxId required, Role valid enum value
- Handler: validate → check duplicate email (409) → check duplicate TaxId (409) → create `UserEntity` → set verification token (24h HMAC) → `AddAsync` → `SendVerificationEmailAsync` → raise `UserRegistered` event → return `UserResponse`

> Note: Domain event dispatch and Outbox pattern wired in E-04. For now, raise the event as a no-op or log it.

**Done when:** Handler returns `UserResponse` on valid input; returns Conflict errors for duplicates.
**Commit:** `feat(application): add RegisterUser handler with duplicate detection and email verification`

---
**status:** pending
**title:** VerifyEmail — Request, Validator, Handler
**type:** backend
**complexity:** medium
**dependencies:** [7, 8, 9, 10, 11, 12]

**Layer:** Application
**Files:**
- `…/Features/Identity/Auth/VerifyEmail/Request/VerifyEmailRequest.cs`
- `…/Features/Identity/Auth/VerifyEmail/Validator/VerifyEmailValidator.cs`
- `…/Features/Identity/Auth/VerifyEmail/VerifyEmailHandler.cs`

**Reference:** `UpdateExampleHandler.cs`
**What:**
- Request: `(string Token)`
- Validator: Token required, max length
- Handler: hash token → find user by token hash → check expiry → call `user.VerifyEmail()` → `UpdateAsync` → raise `UserEmailVerified` → return `UserResponse`

**Done when:** Valid token transitions user to `Active`; expired or unknown token returns `User.TokenInvalidOrExpired` (401).
**Commit:** `feat(application): add VerifyEmail handler with expiry and single-use enforcement`

---
**status:** pending
**title:** Login — Request, Validator, Handler, LoginResponse
**type:** backend
**complexity:** medium
**dependencies:** [7, 8, 9, 10, 11, 12]

**Layer:** Application
**Files:**
- `…/Features/Identity/Auth/Login/Request/LoginRequest.cs`
- `…/Features/Identity/Auth/Login/Validator/LoginValidator.cs`
- `…/Features/Identity/Auth/Login/LoginHandler.cs`
- `…/Features/Identity/Auth/Login/LoginResponse.cs`

**Reference:** `CreateExampleHandler.cs`
**What:**
- Request: `(string Email, string Password)`
- Validator: Email required + format, Password required
- Handler: find user by email → if not found return Unauthorized (do NOT reveal existence) → check `Status == Active` → `passwordHash.Verify(plaintext)` → if fail return Unauthorized → generate access JWT + refresh token → store refresh token hash → return `LoginResponse`
- Response: `(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt)`

**Done when:** Valid credentials return tokens; invalid credentials, non-existent email, and non-active accounts all return the same `User.InvalidCredentials` Unauthorized error (no enumeration).
**Commit:** `feat(application): add Login handler with credential verification and token generation`

---
**status:** pending
**title:** RefreshToken — Request, Validator, Handler, RefreshResponse
**type:** backend
**complexity:** medium
**dependencies:** [7, 8, 9, 10, 11, 12]

**Layer:** Application
**Files:**
- `…/Features/Identity/Auth/Refresh/Request/RefreshTokenRequest.cs`
- `…/Features/Identity/Auth/Refresh/Validator/RefreshTokenValidator.cs`
- `…/Features/Identity/Auth/Refresh/RefreshTokenHandler.cs`
- `…/Features/Identity/Auth/Refresh/RefreshResponse.cs`

**Reference:** `CreateExampleHandler.cs`
**What:**
- Request: `(string RefreshToken)`
- Handler: hash token → find stored token → validate hash + TTL → invalidate old token (one-time use) → generate new access JWT + new refresh token → store new hash → return `RefreshResponse`

**Done when:** Valid refresh token rotates and returns new pair; replayed or expired token returns Unauthorized.
**Commit:** `feat(application): add RefreshToken handler with one-time-use rotation`

---
**status:** pending
**title:** Logout — Request, Handler
**type:** backend
**complexity:** low
**dependencies:** [7, 8, 9, 10, 11, 12]

**Layer:** Application
**Files:**
- `…/Features/Identity/Auth/Logout/Request/LogoutRequest.cs`
- `…/Features/Identity/Auth/Logout/LogoutHandler.cs`

**Reference:** `DeleteExampleHandler.cs`
**What:**
- Request: `(string RefreshToken)`
- Handler: hash token → find and delete stored token from DynamoDB → return `Result.Deleted`

No validator needed (token is required by design; any format is acceptable — invalid tokens are simply not found).
**Done when:** Valid token is revoked (idempotent — unknown token returns success silently to prevent enumeration).
**Commit:** `feat(application): add Logout handler with refresh token revocation`

---
**status:** pending
**title:** ForgotPassword — Request, Validator, Handler
**type:** backend
**complexity:** low
**dependencies:** [7, 8, 9, 10, 11, 12]

**Layer:** Application
**Files:**
- `…/Features/Identity/Auth/ForgotPassword/Request/ForgotPasswordRequest.cs`
- `…/Features/Identity/Auth/ForgotPassword/Validator/ForgotPasswordValidator.cs`
- `…/Features/Identity/Auth/ForgotPassword/ForgotPasswordHandler.cs`

**Reference:** `CreateExampleHandler.cs`
**What:**
- Request: `(string Email)`
- Validator: Email required + format
- Handler: find user by email → if not found return `Result.Success` silently (no enumeration) → if found: generate reset token → `user.SetPasswordResetToken(hash, 1h expiry)` → `UpdateAsync` → `SendPasswordResetEmailAsync`

**Done when:** Email is always accepted without revealing existence; token is persisted and email sent when user exists.
**Commit:** `feat(application): add ForgotPassword handler with blind success and HMAC reset token`

---
**status:** pending
**title:** ResetPassword — Request, Validator, Handler
**type:** backend
**complexity:** medium
**dependencies:** [7, 8, 9, 10, 11, 12]

**Layer:** Application
**Files:**
- `…/Features/Identity/Auth/ResetPassword/Request/ResetPasswordRequest.cs`
- `…/Features/Identity/Auth/ResetPassword/Validator/ResetPasswordValidator.cs`
- `…/Features/Identity/Auth/ResetPassword/ResetPasswordHandler.cs`

**Reference:** `UpdateExampleHandler.cs`
**What:**
- Request: `(string Token, string NewPassword)`
- Validator: Token required, Password complexity rules
- Handler: hash token → find user by reset token hash → check expiry → `Password.Create(newPassword)` → `user.ResetPassword(newPassword)` → `UpdateAsync` → raise `UserPasswordChanged` → return `UserResponse`

**Done when:** Valid token + strong password updates hash; expired/replayed token returns `User.TokenInvalidOrExpired`.
**Commit:** `feat(application): add ResetPassword handler with single-use token and password update`

---
**status:** pending
**title:** GetProfile — Request, Handler
**type:** backend
**complexity:** low
**dependencies:** [7, 8, 9, 10, 11, 12]

**Layer:** Application
**Files:**
- `…/Features/Identity/Users/GetProfile/Request/GetProfileRequest.cs`
- `…/Features/Identity/Users/GetProfile/GetProfileHandler.cs`

**Reference:** `GetByIdExampleHandler.cs`
**What:**
- Request: `(Guid UserId)` — populated from JWT claim at the endpoint level
- Handler: `GetByIdAsync(UserId)` → if null return `User.NotFound` → return `UserResponse`

**Done when:** Returns profile for authenticated user; 404 if user deleted between login and request.
**Commit:** `feat(application): add GetProfile handler (LGPD Art. 18)`

---
**status:** pending
**title:** DeleteAccount — Request, Handler
**type:** backend
**complexity:** low
**dependencies:** [7, 8, 9, 10, 11, 12]

**Layer:** Application
**Files:**
- `…/Features/Identity/Users/DeleteAccount/Request/DeleteAccountRequest.cs`
- `…/Features/Identity/Users/DeleteAccount/DeleteAccountHandler.cs`

**Reference:** `DeleteExampleHandler.cs`
**What:**
- Request: `(Guid UserId)`
- Handler: `GetByIdAsync(UserId)` → `user.Anonymize()` (sets Status=Deleted, clears Email/TaxId/PasswordHash PII) → `UpdateAsync` → return `Result.Deleted`

LGPD Art. 18 VI — soft delete with PII anonymization, not physical deletion.
**Done when:** User status is `Deleted` after call; PII fields are cleared/replaced with anonymized placeholders.
**Commit:** `feat(application): add DeleteAccount handler with LGPD PII anonymization`

---
**status:** pending
**title:** ExportData — Request, Handler, ExportDataResponse
**type:** backend
**complexity:** low
**dependencies:** [7, 8, 9, 10, 11, 12]

**Layer:** Application
**Files:**
- `…/Features/Identity/Users/ExportData/Request/ExportDataRequest.cs`
- `…/Features/Identity/Users/ExportData/ExportDataHandler.cs`
- `…/Features/Identity/Users/ExportData/ExportDataResponse.cs`

**Reference:** `GetByIdExampleHandler.cs`
**What:**
- Request: `(Guid UserId)`
- Response: all user data fields; TaxId masked via `TaxDocument.ToString()`; ExportedAt = now
- Handler: `GetByIdAsync(UserId)` → return `ExportDataResponse`

LGPD Art. 18 IV — portability export, must include all stored data fields.
**Done when:** Returns all user data; TaxId is masked, never raw digits.
**Commit:** `feat(application): add ExportData handler for LGPD Art. 18 IV portability`

---
**status:** pending
**title:** UserRepository stub
**type:** backend
**complexity:** low
**dependencies:** [7]

**Layer:** Infrastructure
**File:** `02-src/05-Infrastructure/RentifyxIdentity.Infrastructure/Repositories/UserRepository.cs`
**Reference:** `ExampleRepository.cs`
**What:** Sealed class implementing `IUserRepository`. All methods throw `NotImplementedException`. This is the scaffold for E-04 DynamoDB wiring.
**Done when:** Class compiles; auto-discovered by `InfrastructureDependencyInjection` reflection scan.
**Commit:** `feat(infrastructure): add UserRepository stub for future DynamoDB wiring`

---
**status:** pending
**title:** TokenService and EmailService stubs
**type:** backend
**complexity:** low
**dependencies:** [8]

**Layer:** Infrastructure
**Files:**
- `02-src/05-Infrastructure/RentifyxIdentity.Infrastructure/Services/TokenService.cs`
- `02-src/05-Infrastructure/RentifyxIdentity.Infrastructure/Services/EmailService.cs`

**Reference:** `ExampleRepository.cs`
**What:** Sealed classes implementing `ITokenService` and `IEmailService` respectively. All methods throw `NotImplementedException`.
**Done when:** Both compile cleanly.
**Commit:** `feat(infrastructure): add TokenService and EmailService stubs for E-04 wiring`

---
**status:** pending
**title:** Register ITokenService and IEmailService in IoC
**type:** backend
**complexity:** low
**dependencies:** [23, 24, 25]

**Layer:** IoC
**File:** `02-src/04-IoC/RentifyxIdentity.IoC/InfrastructureDependencyInjection.cs` *(modify)*
**Reference:** Same file — existing `AddRepositories` pattern
**What:** Add explicit `services.AddScoped<ITokenService, TokenService>()` and `services.AddScoped<IEmailService, EmailService>()` in the `Register` method. These cannot be auto-discovered because they don't implement `IRepository<>`.
**Done when:** DI container resolves both interfaces without errors at startup.
**Commit:** `feat(ioc): register ITokenService and IEmailService in InfrastructureDependencyInjection`

---
**status:** pending
**title:** Add AUTH and USERS tag constants
**type:** backend
**complexity:** low
**dependencies:** []

**Layer:** API
**File:** `02-src/01-Api/RentifyxIdentity.Api/Endpoints/Tags.cs` *(modify)*
**Reference:** Same file
**What:** Add `public const string AUTH = "Auth"` and `public const string USERS = "Users"`.
**Done when:** Both constants resolve in endpoint files.
**Commit:** `feat(api): add Auth and Users tag constants`

---
**status:** pending
**title:** Auth endpoints (7 files)
**type:** backend
**complexity:** medium
**dependencies:** [13, 14, 15, 16, 17, 18, 19, 27]

**Layer:** API
**Files:**
- `02-src/01-Api/RentifyxIdentity.Api/Endpoints/Auth/Register.cs`
- `02-src/01-Api/RentifyxIdentity.Api/Endpoints/Auth/VerifyEmail.cs`
- `02-src/01-Api/RentifyxIdentity.Api/Endpoints/Auth/Login.cs`
- `02-src/01-Api/RentifyxIdentity.Api/Endpoints/Auth/Refresh.cs`
- `02-src/01-Api/RentifyxIdentity.Api/Endpoints/Auth/Logout.cs`
- `02-src/01-Api/RentifyxIdentity.Api/Endpoints/Auth/ForgotPassword.cs`
- `02-src/01-Api/RentifyxIdentity.Api/Endpoints/Auth/ResetPassword.cs`

**Reference:** `Create.cs`, `Delete.cs`
**What:** Each implements `IEndpoint`. All map under the `/auth` sub-path. All are public (`AllowAnonymous()`). HTTP verbs per spec. Return codes: Register → 201; Login/Refresh/ResetPassword/VerifyEmail → 200; Logout/ForgotPassword → 204. Use `result.Match(...)` and `errors.ToProblem(httpContext)`.
**Done when:** All 7 endpoints are auto-discovered and appear in Scalar UI; routes match the spec exactly.
**Commit:** `feat(api): add 7 public auth endpoints (register, verify-email, login, refresh, logout, forgot-password, reset-password)`

---
**status:** pending
**title:** User endpoints (3 files)
**type:** backend
**complexity:** medium
**dependencies:** [20, 21, 22, 27]

**Layer:** API
**Files:**
- `02-src/01-Api/RentifyxIdentity.Api/Endpoints/Users/GetProfile.cs`
- `02-src/01-Api/RentifyxIdentity.Api/Endpoints/Users/DeleteAccount.cs`
- `02-src/01-Api/RentifyxIdentity.Api/Endpoints/Users/ExportData.cs`

**Reference:** `GetById.cs`, `Delete.cs`
**What:** All require authentication (`.RequireAuthorization()` — JWT middleware wired in E-04; for now leave the call in place). Extract `UserId` from `httpContext.User.FindFirst(ClaimTypes.NameIdentifier)` and inject into the request. Paths: `GET /users/me`, `DELETE /users/me`, `GET /users/me/data-export`.
**Done when:** Routes match spec; UserId is correctly extracted from token claim.
**Commit:** `feat(api): add 3 authenticated user endpoints (me, me-delete, me-data-export)`

---
**status:** pending
**title:** UserBuilder in Tests.Common
**type:** test
**complexity:** low
**dependencies:** [5]

**Layer:** Test
**File:** `03-tests/01-Common/RentifyxIdentity.Tests.Common/Builders/UserBuilder.cs`
**Reference:** `ExampleBuilder.cs`
**What:** Fluent builder with `WithEmail(string)`, `WithTaxId(string)`, `WithRole(UserRole)`, `WithStatus(UserStatus)` overrides. Defaults use `Bogus` faker to generate valid test values (valid email, valid CPF, random role). `Build()` calls `UserEntity.Create(...)` then optionally applies state mutations.
**Done when:** `new UserBuilder().Build()` returns a valid `Active` `UserEntity`; each `With...` override is respected.
**Commit:** `feat(tests): add UserBuilder with Bogus defaults for identity tests`

---
**status:** pending
**title:** Validator unit tests (5 test classes)
**type:** test
**complexity:** medium
**dependencies:** [13, 14, 15, 18, 19, 38]

**Layer:** Test
**Files:**
- `03-tests/02-Validators/RentifyxIdentity.Tests.Validators/Features/Identity/RegisterUserValidatorTests.cs`
- `03-tests/02-Validators/RentifyxIdentity.Tests.Validators/Features/Identity/VerifyEmailValidatorTests.cs`
- `03-tests/02-Validators/RentifyxIdentity.Tests.Validators/Features/Identity/LoginValidatorTests.cs`
- `03-tests/02-Validators/RentifyxIdentity.Tests.Validators/Features/Identity/ForgotPasswordValidatorTests.cs`
- `03-tests/02-Validators/RentifyxIdentity.Tests.Validators/Features/Identity/ResetPasswordValidatorTests.cs`

**Reference:** `CreateExampleValidatorTests.cs`
**What:** For each validator: one `[Fact]` for the happy path (all rules pass) and one `[Theory]` per rule (missing/invalid value → specific error message). See Section 6 for the full rule matrix.
**Done when:** All test methods pass; 100% of validator rules covered.
**Commit:** `test(validators): add validator unit tests for identity use cases`

---
**status:** pending
**title:** Handler unit tests (10 test classes)
**type:** test
**complexity:** high
**dependencies:** [13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 38]

**Layer:** Test
**Files (one per handler):**
- `03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers/Features/Identity/RegisterUserHandlerTests.cs`
- `03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers/Features/Identity/VerifyEmailHandlerTests.cs`
- `03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers/Features/Identity/LoginHandlerTests.cs`
- `03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers/Features/Identity/RefreshTokenHandlerTests.cs`
- `03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers/Features/Identity/LogoutHandlerTests.cs`
- `03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers/Features/Identity/ForgotPasswordHandlerTests.cs`
- `03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers/Features/Identity/ResetPasswordHandlerTests.cs`
- `03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers/Features/Identity/GetProfileHandlerTests.cs`
- `03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers/Features/Identity/DeleteAccountHandlerTests.cs`
- `03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers/Features/Identity/ExportDataHandlerTests.cs`

**Reference:** `CreateExampleHandlerTests.cs`
**What:** Mock `IUserRepository`, `ITokenService`, `IEmailService` with Moq. For each handler: happy path returning the expected `ErrorOr` success; each error branch returning the correct error type and code. See Section 8 (Testing Strategy) for the full scenario list.
**Done when:** All mocked paths exercised; no production code paths missing coverage.
**Commit:** `test(handlers): add handler unit tests for all 10 identity use cases`

---
**status:** pending
**title:** Integration tests (Auth + Users)
**type:** test
**complexity:** medium
**dependencies:** [28, 29, 30, 31, 32, 33, 34, 35, 36, 37]

**Layer:** Test
**Files:**
- `03-tests/05-Integration/RentifyxIdentity.Tests.Integration/Api/Identity/AuthEndpointTests.cs`
- `03-tests/05-Integration/RentifyxIdentity.Tests.Integration/Api/Identity/UserEndpointTests.cs`

**Reference:** `ExampleEndpointTests.cs`, `CustomWebApplicationFactory.cs`
**What:** Use `CustomWebApplicationFactory` with in-memory/stub service replacements. Happy path test for each endpoint (correct status + response shape). Error tests: duplicate email on register → 409; invalid credentials → 401; expired token → 401; unauthenticated on user endpoints → 401. Add a `TestAuthHandler` in the factory to bypass JWT validation for authenticated endpoint tests.

> Note: Full E2E with real DynamoDB/Cognito deferred to E-04 using Testcontainers + LocalStack.

**Done when:** All happy-path tests pass against stub implementations; error paths return correct HTTP status.
**Commit:** `test(integration): add integration tests for auth and user identity endpoints`

---

## 6. Validation Plan

| Field | Rule | Source |
|---|---|---|
| `Email` (register) | Required | `ValidationMessageResource.EMAIL_REQUIRED` |
| `Email` (register) | RFC email format | `ValidationMessageResource.EMAIL_INVALID_FORMAT` |
| `Email` (register) | Not a disposable domain | `ValidationMessageResource.EMAIL_DISPOSABLE_DOMAIN` |
| `Email` (register) | Max 320 chars | `ValidationConstants.UserRules.EmailMaxLength` |
| `TaxId` (register) | Required | `ValidationMessageResource.TAXID_REQUIRED` |
| `TaxId` (register) | Valid CPF or CNPJ (mod-11) | `ValidationMessageResource.TAXID_INVALID_FORMAT` |
| `Password` (register) | Required | `ValidationMessageResource.PASSWORD_REQUIRED` |
| `Password` (register) | Min 12 chars | `ValidationConstants.UserRules.PasswordMinLength` |
| `Password` (register) | Upper + lower + digit + symbol | `ValidationMessageResource.PASSWORD_COMPLEXITY` |
| `Password` (register) | Max 128 chars | `ValidationConstants.UserRules.PasswordMaxLength` |
| `Role` (register) | Required | `ValidationMessageResource.ROLE_REQUIRED` |
| `Role` (register) | One of `Owner`, `Renter`, `Admin` | `ValidationMessageResource.ROLE_INVALID` |
| `Email` (login, forgot-password) | Required + format | Same as above |
| `Password` (login) | Required | `ValidationMessageResource.PASSWORD_REQUIRED` |
| `Token` (verify-email, reset-password, refresh, logout) | Required | `ValidationMessageResource.TOKEN_REQUIRED` |
| `Token` (verify-email, reset-password) | Max 512 chars | `ValidationConstants.UserRules.TokenMaxLength` |
| `NewPassword` (reset-password) | Same complexity rules as registration password | Same as above |

---

## 7. Error Cases & Handler Logic

### RegisterUser

| Step | Condition | Action |
|---|---|---|
| 1 | Validation fails | Return `Error.Validation` list |
| 2 | Email already exists (`GetByEmailAsync` → not null) | Return `Error.Conflict(User.EmailAlreadyRegistered)` |
| 3 | TaxId already exists (`GetByTaxIdAsync` → not null) | Return `Error.Conflict(User.TaxIdAlreadyRegistered)` |
| 4 | Success | `AddAsync` + send verification email + return `UserResponse` |

### VerifyEmail / ResetPassword

| Step | Condition | Action |
|---|---|---|
| 1 | Validation fails | Return `Error.Validation` list |
| 2 | Token hash not found | Return `Error.Unauthorized(User.TokenInvalidOrExpired)` |
| 3 | Token exists but expired | Return `Error.Unauthorized(User.TokenInvalidOrExpired)` |
| 4 | Success | Update user + clear token + `UpdateAsync` + return response |

### Login

| Step | Condition | Action |
|---|---|---|
| 1 | Validation fails | Return `Error.Validation` list |
| 2 | Email not found | Return `Error.Unauthorized(User.InvalidCredentials)` *(no enumeration)* |
| 3 | User not `Active` | Return `Error.Unauthorized(User.InvalidCredentials)` *(same code — no enumeration)* |
| 4 | Password does not verify | Return `Error.Unauthorized(User.InvalidCredentials)` |
| 5 | Success | Generate tokens + store refresh hash + return `LoginResponse` |

### RefreshToken

| Step | Condition | Action |
|---|---|---|
| 1 | Validation fails | Return `Error.Validation` list |
| 2 | Token hash not found or expired | Return `Error.Unauthorized(User.TokenInvalidOrExpired)` |
| 3 | Success | Revoke old token + generate new pair + return `RefreshResponse` |

### Logout

| Step | Condition | Action |
|---|---|---|
| 1 | Token hash found | Delete from store |
| 2 | Token hash not found | Return success silently *(idempotent, prevents enumeration)* |

### ForgotPassword

| Step | Condition | Action |
|---|---|---|
| 1 | Validation fails | Return `Error.Validation` list |
| 2 | Email not found | Return `Result.Success` *(blind, no enumeration)* |
| 3 | Email found | Generate + store token + send email + return `Result.Success` |

### GetProfile / ExportData

| Step | Condition | Action |
|---|---|---|
| 1 | UserId not found (deleted mid-session) | Return `Error.NotFound(User.NotFound)` |
| 2 | Success | Return DTO |

### DeleteAccount

| Step | Condition | Action |
|---|---|---|
| 1 | UserId not found | Return `Error.NotFound(User.NotFound)` |
| 2 | Success | `user.Anonymize()` + `UpdateAsync` + return `Result.Deleted` |

---

## 8. Testing Strategy

### Unit Tests — Validators

**RegisterUserValidator:**
- Valid full request → passes
- `Email` empty → `EMAIL_REQUIRED` error
- `Email` invalid format → `EMAIL_INVALID_FORMAT` error
- `Email` disposable domain → `EMAIL_DISPOSABLE_DOMAIN` error
- `TaxId` empty → `TAXID_REQUIRED` error
- `TaxId` invalid CPF (bad check digit) → `TAXID_INVALID_FORMAT` error
- `Password` empty → `PASSWORD_REQUIRED` error
- `Password` < 12 chars → `PASSWORD_MIN_LENGTH` error
- `Password` no uppercase → `PASSWORD_COMPLEXITY` error
- `Password` no digit → `PASSWORD_COMPLEXITY` error
- `Password` no symbol → `PASSWORD_COMPLEXITY` error
- `Role` empty → `ROLE_REQUIRED` error
- `Role` invalid value → `ROLE_INVALID` error

**LoginValidator:** Email (required + format), Password (required)

**VerifyEmailValidator:** Token required, max length

**ForgotPasswordValidator:** Email required + format

**ResetPasswordValidator:** Token required, NewPassword complexity

### Unit Tests — Handlers

Mock `IUserRepository`, `ITokenService`, `IEmailService` with Moq for all handler tests.

**RegisterUserHandlerTests:**
- Valid request → `GetByEmailAsync` null, `GetByTaxIdAsync` null → `AddAsync` called once → `UserResponse`
- Duplicate email → `GetByEmailAsync` returns existing → 409 Conflict, `AddAsync` never called
- Duplicate TaxId → `GetByTaxIdAsync` returns existing → 409 Conflict

**VerifyEmailHandlerTests:**
- Valid token → user found, not expired → status becomes Active → 200 OK
- Invalid token → repository returns null → 401 Unauthorized
- Expired token → user found but expiry is past → 401 Unauthorized

**LoginHandlerTests:**
- Valid credentials → user exists, Active, password matches → tokens returned
- Email not found → 401 Unauthorized
- User suspended → 401 Unauthorized
- Wrong password → 401 Unauthorized
- `ITokenService.GenerateAccessToken` called with correct user on success

**RefreshTokenHandlerTests:**
- Valid token → old token revoked, new pair returned
- Invalid token → 401 Unauthorized

**LogoutHandlerTests:**
- Token found → deleted → returns `Result.Deleted`
- Token not found → returns success silently

**ForgotPasswordHandlerTests:**
- Email found → `SetPasswordResetToken` called + `SendPasswordResetEmailAsync` called → success
- Email not found → success, `SendPasswordResetEmailAsync` never called

**ResetPasswordHandlerTests:**
- Valid token + strong password → password updated, token cleared → `UserResponse`
- Expired token → 401 Unauthorized

**GetProfileHandlerTests:**
- User found → `UserResponse` returned
- User not found → `User.NotFound` error

**DeleteAccountHandlerTests:**
- User found → `Anonymize()` called → `UpdateAsync` called once → `Result.Deleted`
- User not found → `User.NotFound` error, `UpdateAsync` never called

**ExportDataHandlerTests:**
- User found → `ExportDataResponse` with masked TaxId + correct `ExportedAt`
- User not found → `User.NotFound` error

### Integration Tests

Uses `CustomWebApplicationFactory` with stub services wired to return canned data.

**AuthEndpointTests:**
- `POST /api/v1/auth/register` → 201 + `UserResponse` body
- `POST /api/v1/auth/register` duplicate email → 409 + problem details
- `POST /api/v1/auth/register` invalid password → 422 + validation errors
- `POST /api/v1/auth/login` valid credentials → 200 + tokens
- `POST /api/v1/auth/login` wrong password → 401 + problem details
- `POST /api/v1/auth/verify-email` valid token → 200 + `UserResponse`
- `POST /api/v1/auth/verify-email` invalid token → 401
- `POST /api/v1/auth/forgot-password` any email → 204
- `POST /api/v1/auth/reset-password` valid token → 200
- `POST /api/v1/auth/logout` → 204

**UserEndpointTests:**
- `GET /api/v1/users/me` without token → 401
- `GET /api/v1/users/me` with valid JWT → 200 + `UserResponse`
- `DELETE /api/v1/users/me` with valid JWT → 204
- `GET /api/v1/users/me/data-export` with valid JWT → 200 + `ExportDataResponse` (TaxId masked)

---

## 9. Risks & Unknowns

- **[non-blocking]** `BCrypt` is not in `Directory.Packages.props`. The `Password` VO needs a hashing library (e.g. `BCrypt.Net-Next`). Add to `Directory.Packages.props` before implementing step 4.

- **[non-blocking]** Disposable-domain list for `Email` VO is not defined in the spec. A minimal hardcoded list (`mailinator.com`, `guerrillamail.com`, etc.) should suffice for now; a configurable approach can be added later.

- **[non-blocking]** Rate limiting for login (5 failed attempts → 15-min lockout) is specified in security controls but requires per-user state (DynamoDB or distributed cache). Deferred to E-04 — the handler should leave a hook (e.g., a method call that the stub no-ops).

- **[non-blocking]** JWT authorization middleware (`.RequireAuthorization()`) requires a JWT bearer scheme registered. Until E-04 wires Cognito, integration tests for authenticated endpoints need a `TestAuthHandler` in `CustomWebApplicationFactory` that auto-approves requests.

- **[non-blocking]** Refresh tokens are a separate DynamoDB concern — one user can have multiple sessions. A `RefreshToken` DynamoDB item model is needed for E-04. Stubs can skip this for now.

- **[non-blocking]** `UserEntity.Anonymize()` must define what "anonymized" PII looks like (e.g., `email = "deleted_{id}@anonymized.local"`). Confirm the anonymization scheme before implementing step 5.

- **[blocking]** `IUserRepository.GetByEmailAsync` and `GetByTaxIdAsync` require DynamoDB GSIs. Steps 7 and 23 can proceed as interface/stub, but the actual implementation in E-04 depends on ADR-005 (single-table design). Do not implement real DynamoDB logic until ADR-005 is finalized.

- **[non-blocking]** `ExportDataResponse` should include consent and audit log data per LGPD Art. 18 IV. The spec does not enumerate those fields. Confirm with the team what additional data is exported (e.g., login history, consent records) before implementing step 22.
