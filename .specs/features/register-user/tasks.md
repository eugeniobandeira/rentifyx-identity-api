# RegisterUser Tasks

**Spec**: `.specs/features/register-user/spec.md`
**Status**: Draft

---

## Execution Plan

### Phase 1 — Package prerequisite (Sequential)

```
T1
```

### Phase 2 — Domain primitives + request record (Parallel after T1)

```
T1 ──→ T2 [P]
    ├─→ T3 [P]
    ├─→ T4 [P]
    ├─→ T5 [P]
    ├─→ T6 [P]
    ├─→ T7 [P]
    └─→ T17 [P]
```

### Phase 3 — Aggregate + validator (Parallel after Phase 2)

```
T2+T3+T4+T5 ──→ T8
T6+T7        ──→ T9 [P]
```

T8 and T9 are independent — run in parallel.

### Phase 4 — Contracts, event, response (Parallel after T8)

```
T8 ──┬─→ T10 [P]
     ├─→ T11 [P]
     └─→ T12 [P]
```

### Phase 5 — Handler + stubs (Mixed after Phase 4)

```
T11 ──┬─→ T14 [P]
      └─→ T15 [P]

T9+T11+T12 ──→ T13
```

T14, T15 start as soon as T11 is done. T13 waits for T9 (Phase 3), T11 and T12 (Phase 4).

### Phase 6 — IoC wiring (Sequential after Phase 5)

```
T14+T15 ──→ T16
```

### Phase 7 — Endpoint + integration test (Sequential)

```
T12+T13+T16+T17 ──→ T18
```

---

## Task Breakdown

### T1: Add BCrypt.Net-Next to Directory.Packages.props ✅

**What**: Add `BCrypt.Net-Next` (v4.0.3) as a centrally managed package so `Password` VO can reference it without a version pin per project.
**Where**: `Directory.Packages.props` (modify), `02-src/03-Domain/RentifyxIdentity.Domain/RentifyxIdentity.Domain.csproj` (add PackageReference)
**Depends on**: None
**Reuses**: Existing `Directory.Packages.props` structure
**Requirement**: REG-17 (enables Password VO complexity + hashing)

**Tools**:
- MCP: None
- Skill: None

**Done when**:
- [ ] `<PackageVersion Include="BCrypt.Net-Next" Version="4.0.3" />` added to `Directory.Packages.props`
- [ ] `<PackageReference Include="BCrypt.Net-Next" />` added to Domain `.csproj`
- [ ] `dotnet build RentifyxIdentity.slnx -c Release` passes with zero warnings

**Tests**: none
**Gate**: build
**Commit**: `chore(deps): add BCrypt.Net-Next 4.0.3 for Password value object hashing`

---

### T2: UserRole and UserStatus enums [P]

**What**: Create `UserRole` (`Owner`, `Renter`, `Admin`) and `UserStatus` (`PendingVerification`, `Active`, `Suspended`, `Deleted`) enums.
**Where**:
- `02-src/03-Domain/RentifyxIdentity.Domain/Enums/UserRole.cs` (new)
- `02-src/03-Domain/RentifyxIdentity.Domain/Enums/UserStatus.cs` (new)
**Depends on**: None
**Reuses**: Namespace pattern from `Domain/Entities/ExampleEntity.cs`
**Requirement**: REG-01, REG-22

**Tools**:
- MCP: None
- Skill: None

**Done when**:
- [ ] `UserRole` enum has exactly `Owner`, `Renter`, `Admin` values
- [ ] `UserStatus` enum has exactly `PendingVerification`, `Active`, `Suspended`, `Deleted` values
- [ ] Both in namespace `RentifyxIdentity.Domain.Enums`
- [ ] `dotnet build RentifyxIdentity.slnx -c Release` passes with zero warnings

**Tests**: none (enums are trivial; covered implicitly by T8 entity tests and T9 validator tests)
**Gate**: build
**Commit**: `feat(domain): add UserRole and UserStatus enums`

---

### T3: Email value object + unit tests [P]

**What**: Create `Email` sealed record with RFC format validation, disposable-domain rejection, and lowercase normalization. Include unit tests.
**Where**:
- `02-src/03-Domain/RentifyxIdentity.Domain/ValueObjects/Email.cs` (new)
- `03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers/Features/Identity/ValueObjects/EmailTests.cs` (new)
**Depends on**: None
**Reuses**: Static factory pattern from `Domain/Entities/ExampleEntity.cs`
**Requirement**: REG-11, REG-12, REG-13, REG-14

**Tools**:
- MCP: None
- Skill: None

**Done when**:
- [ ] `Email.Create(string value)` normalizes to lowercase and stores in `Value` property
- [ ] Rejects null/empty → `ArgumentException`
- [ ] Rejects non-RFC format (e.g., `notanemail`, `@no-local`, `no-at-sign`)
- [ ] Rejects known disposable domains: `mailinator.com`, `guerrillamail.com`, `tempmail.com`, `throwam.com`, `yopmail.com` (minimum list — hardcoded constant)
- [ ] Accepts valid email up to 320 chars; rejects 321+
- [ ] `ToString()` returns the normalized email value
- [ ] Gate check passes: `dotnet test 03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers`
- [ ] Test count: 7 tests pass

**Tests**: unit
**Gate**: quick
**Verify**: `dotnet test --filter "EmailTests"` → 7 passed, 0 failed
**Commit**: `feat(domain): add Email value object with RFC and disposable-domain validation`

---

### T4: TaxDocument value object + unit tests [P]

**What**: Create `TaxDocument` sealed record supporting CPF (11 digits) and CNPJ (14 digits) with mod-11 validation, all-same-digit rejection, and masked `ToString()`.
**Where**:
- `02-src/03-Domain/RentifyxIdentity.Domain/ValueObjects/TaxDocument.cs` (new)
- `03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers/Features/Identity/ValueObjects/TaxDocumentTests.cs` (new)
**Depends on**: None
**Reuses**: Static factory pattern from `Domain/Entities/ExampleEntity.cs`
**Requirement**: REG-15, REG-16

**Tools**:
- MCP: None
- Skill: None

**Done when**:
- [ ] `TaxDocument.Create(string value)` strips formatting chars (`.`, `-`, `/`) before processing
- [ ] Detects CPF (11 raw digits) vs CNPJ (14 raw digits) and sets `DocumentType` (`Cpf` / `Cnpj`)
- [ ] CPF mod-11 check-digit validation implemented correctly
- [ ] CNPJ mod-11 check-digit validation implemented correctly
- [ ] All-same-digit sequences rejected for CPF (e.g., `11111111111`) and CNPJ
- [ ] `ToString()` returns `***.***.***-**` for CPF and `**.***.***/****-**` for CNPJ
- [ ] `RawValue` property exposes stripped digits (for KMS encryption in E-04)
- [ ] Gate check passes: `dotnet test 03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers`
- [ ] Test count: 9 tests pass

**Tests**: unit
**Gate**: quick
**Verify**: `dotnet test --filter "TaxDocumentTests"` → 9 passed, 0 failed
**Commit**: `feat(domain): add TaxDocument value object with CPF/CNPJ mod-11 validation and masked output`

---

### T5: Password value object + unit tests [P]

**What**: Create `Password` sealed record that validates OWASP A07 complexity at construction, stores BCrypt hash, exposes `Verify()`, and redacts itself in logs.
**Where**:
- `02-src/03-Domain/RentifyxIdentity.Domain/ValueObjects/Password.cs` (new)
- `03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers/Features/Identity/ValueObjects/PasswordTests.cs` (new)
**Depends on**: T1 (BCrypt.Net-Next package)
**Reuses**: Static factory pattern from `Domain/Entities/ExampleEntity.cs`
**Requirement**: REG-17, REG-18, REG-19, REG-20, REG-25, REG-27

**Tools**:
- MCP: None
- Skill: None

**Done when**:
- [ ] `Password.Create(string plaintext)` validates: min 12 chars, max 128, at least 1 uppercase, 1 lowercase, 1 digit, 1 symbol (`!@#$%^&*` etc.)
- [ ] Throws `ArgumentException` (or returns guard error) for each complexity violation
- [ ] Stores BCrypt hash in private field; plaintext is never retained
- [ ] `Verify(string plaintext) → bool` uses `BCrypt.Verify` for constant-time comparison
- [ ] `ToString()` returns `"[REDACTED]"` — never the hash
- [ ] `HashValue` property exposes the hash string (for persistence)
- [ ] Gate check passes: `dotnet test 03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers`
- [ ] Test count: 8 tests pass

**Tests**: unit
**Gate**: quick
**Verify**: `dotnet test --filter "PasswordTests"` → 8 passed, 0 failed
**Commit**: `feat(domain): add Password value object with OWASP complexity rules and BCrypt hashing`

---

### T6: UserErrorCodes, ValidationConstants.UserRules, and .resx messages [P]

**What**: Add all user-domain error codes, validation length constants, and pt-BR validation message strings to the existing resource infrastructure.
**Where**:
- `02-src/03-Domain/RentifyxIdentity.Domain/Constants/UserErrorCodes.cs` (new)
- `02-src/03-Domain/RentifyxIdentity.Domain/Constants/ValidationConstants.cs` (modify — add `UserRules` nested class)
- `02-src/03-Domain/RentifyxIdentity.Domain/MessageResource/ValidationMessageResource.resx` (modify — add 19 new strings)
**Depends on**: None
**Reuses**: `Domain/Constants/ExampleErrorCodes.cs`, `Domain/Constants/ValidationConstants.cs`, existing `.resx` file
**Requirement**: REG-07, REG-09, REG-11 through REG-24

**Tools**:
- MCP: None
- Skill: None

**Done when**:
- [ ] `UserErrorCodes` has: `User.NotFound`, `User.EmailAlreadyRegistered`, `User.TaxIdAlreadyRegistered`, `User.InvalidCredentials`, `User.AccountNotActive`, `User.AccountNotVerified`, `User.TokenInvalidOrExpired`
- [ ] `ValidationConstants.UserRules` has: `EmailMaxLength = 320`, `PasswordMinLength = 12`, `PasswordMaxLength = 128`, `TokenMaxLength = 512`
- [ ] `.resx` has all 19 message keys: `EMAIL_REQUIRED`, `EMAIL_INVALID_FORMAT`, `EMAIL_DISPOSABLE_DOMAIN`, `EMAIL_MAX_LENGTH`, `PASSWORD_REQUIRED`, `PASSWORD_MIN_LENGTH`, `PASSWORD_COMPLEXITY`, `PASSWORD_MAX_LENGTH`, `TAXID_REQUIRED`, `TAXID_INVALID_FORMAT`, `ROLE_REQUIRED`, `ROLE_INVALID`, `TOKEN_REQUIRED`, `USER_NOT_FOUND`, `USER_EMAIL_ALREADY_REGISTERED`, `USER_TAXID_ALREADY_REGISTERED`, `USER_INVALID_CREDENTIALS`, `USER_ACCOUNT_NOT_ACTIVE`, `USER_TOKEN_INVALID_OR_EXPIRED`
- [ ] Designer.cs regenerates correctly (all new keys accessible as static properties)
- [ ] `dotnet build RentifyxIdentity.slnx -c Release` passes with zero warnings

**Tests**: none (constants and resources; covered by validator tests in T9)
**Gate**: build
**Commit**: `feat(domain): add UserErrorCodes, UserRules validation constants, and user message resources`

---

### T7: RegisterUserRequest record [P]

**What**: Create the `RegisterUserRequest` sealed record with the four registration fields.
**Where**: `02-src/02-Application/RentifyxIdentity.Application/Features/Identity/Auth/Register/Request/RegisterUserRequest.cs` (new)
**Depends on**: None
**Reuses**: `Application/Features/Examples/Handlers/Create/Request/CreateExampleRequest.cs`
**Requirement**: REG-01

**Tools**:
- MCP: None
- Skill: None

**Done when**:
- [ ] `sealed record RegisterUserRequest(string Email, string TaxId, string Password, string Role)`
- [ ] In namespace `RentifyxIdentity.Application.Features.Identity.Auth.Register.Request`
- [ ] `dotnet build RentifyxIdentity.slnx -c Release` passes with zero warnings

**Tests**: none (data record; covered by T9 validator tests)
**Gate**: build
**Commit**: `feat(application): add RegisterUserRequest record`

---

### T8: UserEntity aggregate root + unit tests

**What**: Create `UserEntity` sealed class with all domain fields, token fields, static factory, state-machine methods, and unit tests covering each transition.
**Where**:
- `02-src/03-Domain/RentifyxIdentity.Domain/Entities/UserEntity.cs` (new)
- `03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers/Features/Identity/UserEntityTests.cs` (new)
**Depends on**: T2 (enums), T3 (Email VO), T4 (TaxDocument VO), T5 (Password VO)
**Reuses**: `Domain/Entities/ExampleEntity.cs` (sealed class, private constructor, static factory pattern)
**Requirement**: REG-01, REG-03, REG-06, REG-25, REG-26

**Tools**:
- MCP: None
- Skill: None

**Done when**:
- [ ] Fields: `Id (Guid)`, `Email (Email)`, `TaxId (TaxDocument)`, `PasswordHash (Password)`, `Role (UserRole)`, `Status (UserStatus)`, `CreatedAt (DateTimeOffset)`, `EmailVerificationTokenHash (string?)`, `EmailVerificationTokenExpiry (DateTimeOffset?)`, `PasswordResetTokenHash (string?)`, `PasswordResetTokenExpiry (DateTimeOffset?)`
- [ ] All properties have `private set`; private parameterless constructor
- [ ] `Create(Email, TaxDocument, Password, UserRole)` sets `Status = PendingVerification`, `CreatedAt = DateTimeOffset.UtcNow`; uses `ArgumentException.ThrowIfNullOrWhiteSpace` guard (not applicable to VOs, but string guards where used)
- [ ] `SetEmailVerificationToken(string hash, DateTimeOffset expiry)` sets token fields
- [ ] `VerifyEmail()` sets `Status = Active`, nulls token fields
- [ ] `SetPasswordResetToken(string hash, DateTimeOffset expiry)` sets reset token fields
- [ ] `ResetPassword(Password newPassword)` replaces `PasswordHash`, nulls reset token fields
- [ ] `Suspend(string reason, Guid suspendedBy)` sets `Status = Suspended`
- [ ] `Anonymize()` sets `Status = Deleted`, replaces Email/TaxId/PasswordHash with anonymized placeholders (`deleted_{Id}@anonymized.local`, blank TaxDocument, blank Password)
- [ ] Gate check passes: `dotnet test 03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers`
- [ ] Test count: 9 tests pass

**Tests**: unit
**Gate**: quick
**Verify**: `dotnet test --filter "UserEntityTests"` → 9 passed, 0 failed
**Commit**: `feat(domain): add UserEntity aggregate root with status machine and token management`

---

### T9: RegisterUserValidator + validator unit tests [P]

**What**: Create `RegisterUserValidator` with all field rules and unit tests covering every valid/invalid scenario from the spec.
**Where**:
- `02-src/02-Application/RentifyxIdentity.Application/Features/Identity/Auth/Register/Validator/RegisterUserValidator.cs` (new)
- `03-tests/02-Validators/RentifyxIdentity.Tests.Validators/Features/Identity/RegisterUserValidatorTests.cs` (new)
**Depends on**: T6 (constants + resources), T7 (request record)
**Reuses**: `Application/Features/Examples/Handlers/Create/Validator/CreateExampleValidator.cs`, `Tests.Validators/Features/Examples/CreateExampleValidatorTests.cs`
**Requirement**: REG-11 through REG-24

**Tools**:
- MCP: None
- Skill: None

**Done when**:
- [ ] `RuleFor(Email)`: NotEmpty, EmailAddress, custom Must for disposable domains, MaximumLength(320) — all with resource messages
- [ ] `RuleFor(TaxId)`: NotEmpty, custom Must for CPF/CNPJ mod-11 validation — all with resource messages
- [ ] `RuleFor(Password)`: NotEmpty, MinimumLength(12), MaximumLength(128), custom Must for complexity (upper/lower/digit/symbol) — all with resource messages
- [ ] `RuleFor(Role)`: NotEmpty, Must(IsValidRole) where valid = `Owner`, `Renter`, `Admin` (case-sensitive) — all with resource messages
- [ ] All 14 invalid scenarios from spec return exactly the right error message per AC REG-11→REG-24
- [ ] Valid request passes all rules in a single `ValidateAsync` call
- [ ] Gate check passes: `dotnet test 03-tests/02-Validators/RentifyxIdentity.Tests.Validators`
- [ ] Test count: 15 tests pass (1 happy path + 14 invalid scenarios)

**Tests**: unit
**Gate**: quick
**Verify**: `dotnet test --filter "RegisterUserValidatorTests"` → 15 passed, 0 failed
**Commit**: `test(validators): add RegisterUserValidator with full field validation coverage`

---

### T10: UserRegistered domain event [P]

**What**: Create `UserRegistered` sealed record domain event.
**Where**: `02-src/03-Domain/RentifyxIdentity.Domain/Events/UserRegistered.cs` (new)
**Depends on**: T2 (UserRole), T8 (UserEntity — establishes the domain pattern)
**Reuses**: Sealed record pattern; no existing event files
**Requirement**: REG-06

**Tools**:
- MCP: None
- Skill: None

**Done when**:
- [ ] `sealed record UserRegistered(Guid UserId, string Email, UserRole Role, DateTimeOffset OccurredAt)`
- [ ] In namespace `RentifyxIdentity.Domain.Events`
- [ ] `dotnet build RentifyxIdentity.slnx -c Release` passes with zero warnings

**Tests**: none (plain data record; verified structurally by handler tests in T13)
**Gate**: build
**Commit**: `feat(domain): add UserRegistered domain event`

---

### T11: IUserRepository and IEmailService interfaces [P]

**What**: Define `IUserRepository` (extends `IRepository<UserEntity>` + email/taxId lookups) and `IEmailService` (verification + reset email methods).
**Where**:
- `02-src/03-Domain/RentifyxIdentity.Domain/Interfaces/Users/IUserRepository.cs` (new)
- `02-src/03-Domain/RentifyxIdentity.Domain/Interfaces/Users/IEmailService.cs` (new)
**Depends on**: T8 (UserEntity)
**Reuses**: `Domain/Interfaces/Common/IRepository.cs`
**Requirement**: REG-04, REG-05

**Tools**:
- MCP: None
- Skill: None

**Done when**:
- [ ] `IUserRepository : IRepository<UserEntity>` adds `GetByEmailAsync(string email, CancellationToken) → Task<UserEntity?>` and `GetByTaxIdAsync(string taxId, CancellationToken) → Task<UserEntity?>`
- [ ] `IEmailService` defines `SendVerificationEmailAsync(string to, string token, CancellationToken) → Task` and `SendPasswordResetEmailAsync(string to, string token, CancellationToken) → Task`
- [ ] Both in namespace `RentifyxIdentity.Domain.Interfaces.Users`
- [ ] `dotnet build RentifyxIdentity.slnx -c Release` passes with zero warnings

**Tests**: none (interfaces; verified by handler tests in T13)
**Gate**: build
**Commit**: `feat(domain): add IUserRepository and IEmailService contracts`

---

### T12: UserResponse DTO and UserMapper [P]

**What**: Create `UserResponse` sealed record and `UserMapper` static class mapping `UserEntity` to the response DTO.
**Where**:
- `02-src/02-Application/RentifyxIdentity.Application/Features/Identity/UserResponse.cs` (new)
- `02-src/02-Application/RentifyxIdentity.Application/Features/Identity/Mapper/UserMapper.cs` (new)
**Depends on**: T8 (UserEntity)
**Reuses**: `Application/Features/Examples/ExampleResponse.cs`, `Application/Features/Examples/Mapper/ExampleMapper.cs`
**Requirement**: REG-02, REG-25

**Tools**:
- MCP: None
- Skill: None

**Done when**:
- [ ] `sealed record UserResponse(Guid Id, string Email, string Role, string Status, DateTimeOffset CreatedAt)`
- [ ] `UserMapper.ToResponse(UserEntity entity)` maps all five fields; uses `.ToString()` on `Email` VO, `.ToString()` on enums
- [ ] Response does NOT include `TaxId`, `PasswordHash`, any token field — verified by inspecting mapper
- [ ] `dotnet build RentifyxIdentity.slnx -c Release` passes with zero warnings

**Tests**: none (trivial mapper; REG-02/REG-25 verified by handler tests in T13 and integration test in T18)
**Gate**: build
**Commit**: `feat(application): add UserResponse DTO and UserMapper`

---

### T13: RegisterUserHandler + handler unit tests

**What**: Implement `RegisterUserHandler` with duplicate detection, token generation, email dispatch, and event raising — plus unit tests for all paths.
**Where**:
- `02-src/02-Application/RentifyxIdentity.Application/Features/Identity/Auth/Register/RegisterUserHandler.cs` (new)
- `03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers/Features/Identity/RegisterUserHandlerTests.cs` (new)
**Depends on**: T9 (validator), T11 (IUserRepository + IEmailService), T12 (UserResponse + UserMapper)
**Reuses**: `Application/Features/Examples/Handlers/Create/CreateExampleHandler.cs`, `Tests.Handlers/Features/Examples/CreateExampleHandlerTests.cs`
**Requirement**: REG-01 through REG-10, REG-24, REG-25

**Tools**:
- MCP: None
- Skill: None

**Done when**:
- [ ] Implements `IHandler<RegisterUserRequest, UserResponse>`
- [ ] Calls `validator.ValidateToErrorsAsync` first; returns error list on failure (REG-24)
- [ ] Calls `repository.GetByEmailAsync` → returns `Error.Conflict(User.EmailAlreadyRegistered)` if found (REG-07, REG-08)
- [ ] Calls `repository.GetByTaxIdAsync` → returns `Error.Conflict(User.TaxIdAlreadyRegistered)` if found (REG-09, REG-10)
- [ ] Creates `UserEntity` via `UserEntity.Create(Email.Create(email), TaxDocument.Create(taxId), Password.Create(password), role)`
- [ ] Generates HMAC-SHA256 verification token using `HMAC-SHA256(key, Guid.NewGuid().ToString())` (key from `IConfiguration["Hmac:Key"]`); calls `user.SetEmailVerificationToken(hash, DateTimeOffset.UtcNow.AddHours(24))`
- [ ] Calls `repository.AddAsync(user)` (REG-05)
- [ ] Calls `emailService.SendVerificationEmailAsync(email, rawToken)` (REG-04) — email failure is logged but does NOT prevent 201 return
- [ ] Logs `UserRegistered` event payload (no dispatch yet — Outbox deferred to E-04) (REG-06)
- [ ] Returns `UserMapper.ToResponse(user)` (REG-02)
- [ ] Unit tests — all mocked with Moq:
  - Happy path: `AddAsync` called once, email service called once, returns `UserResponse` with correct fields (REG-01, REG-02)
  - Duplicate email: `GetByEmailAsync` returns entity → 409 error, `AddAsync` NOT called (REG-07, REG-08)
  - Duplicate TaxId: `GetByTaxIdAsync` returns entity → 409 error, `AddAsync` NOT called (REG-09, REG-10)
  - Validation fail: validator returns errors → handler returns them without hitting repo (REG-24)
  - Email service throws: handler still returns 201 (email failure non-blocking)
- [ ] Gate check passes: `dotnet test 03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers`
- [ ] Test count: 5 tests pass

**Tests**: unit
**Gate**: quick
**Verify**: `dotnet test --filter "RegisterUserHandlerTests"` → 5 passed, 0 failed
**Commit**: `feat(application): add RegisterUserHandler with duplicate detection and email verification dispatch`

---

### T14: UserRepository stub [P]

**What**: Create `UserRepository` sealed class implementing `IUserRepository` with all methods throwing `NotImplementedException` — scaffold for E-04 DynamoDB wiring.
**Where**: `02-src/05-Infrastructure/RentifyxIdentity.Infrastructure/Repositories/UserRepository.cs` (new)
**Depends on**: T11 (IUserRepository)
**Reuses**: `Infrastructure/Repositories/ExampleRepository.cs`
**Requirement**: REG-05 (contract placeholder)

**Tools**:
- MCP: None
- Skill: None

**Done when**:
- [ ] `sealed class UserRepository : IUserRepository` with all 6 methods throwing `NotImplementedException`
- [ ] Methods: `AddAsync`, `GetByIdAsync`, `UpdateAsync`, `DeleteAsync`, `GetByEmailAsync`, `GetByTaxIdAsync`
- [ ] Class auto-discovered by `InfrastructureDependencyInjection` reflection scan (implements `IRepository<>`)
- [ ] `dotnet build RentifyxIdentity.slnx -c Release` passes with zero warnings

**Tests**: none (stub; real tests in E-04 via Testcontainers)
**Gate**: build
**Commit**: `feat(infrastructure): add UserRepository stub for E-04 DynamoDB wiring`

---

### T15: EmailService stub [P]

**What**: Create `EmailService` sealed class implementing `IEmailService` with all methods throwing `NotImplementedException` — scaffold for E-04 SES wiring.
**Where**: `02-src/05-Infrastructure/RentifyxIdentity.Infrastructure/Services/EmailService.cs` (new)
**Depends on**: T11 (IEmailService)
**Reuses**: `Infrastructure/Repositories/ExampleRepository.cs` (stub pattern)
**Requirement**: REG-04 (contract placeholder)

**Tools**:
- MCP: None
- Skill: None

**Done when**:
- [ ] `sealed class EmailService : IEmailService` with both methods throwing `NotImplementedException`
- [ ] Methods: `SendVerificationEmailAsync`, `SendPasswordResetEmailAsync`
- [ ] `dotnet build RentifyxIdentity.slnx -c Release` passes with zero warnings

**Tests**: none (stub; real tests in E-04)
**Gate**: build
**Commit**: `feat(infrastructure): add EmailService stub for E-04 SES wiring`

---

### T16: IoC — register IEmailService in InfrastructureDependencyInjection

**What**: Explicitly register `IEmailService → EmailService` as Scoped in `InfrastructureDependencyInjection.Register()` — this interface is NOT auto-discovered by the reflection scan.
**Where**: `02-src/04-IoC/RentifyxIdentity.IoC/InfrastructureDependencyInjection.cs` (modify)
**Depends on**: T14 (UserRepository — confirms auto-discovery works), T15 (EmailService stub exists)
**Reuses**: Same file — existing `AddRepositories` section
**Requirement**: REG-04

**Tools**:
- MCP: None
- Skill: None

**Done when**:
- [ ] `services.AddScoped<IEmailService, EmailService>()` added to `Register()`
- [ ] Application starts without `InvalidOperationException` for `IEmailService`
- [ ] `dotnet build RentifyxIdentity.slnx -c Release` passes with zero warnings

**Tests**: none (verified implicitly by T18 integration test — app must start cleanly)
**Gate**: build
**Commit**: `feat(ioc): register IEmailService in InfrastructureDependencyInjection`

---

### T17: Add AUTH and USERS tag constants [P]

**What**: Add `AUTH` and `USERS` string constants to `Tags.cs`.
**Where**: `02-src/01-Api/RentifyxIdentity.Api/Endpoints/Tags.cs` (modify)
**Depends on**: None
**Reuses**: Same file — existing `EXAMPLE` and `HEALTH` constants
**Requirement**: REG-01 (endpoint metadata)

**Tools**:
- MCP: None
- Skill: None

**Done when**:
- [ ] `public const string AUTH = "Auth"` added
- [ ] `public const string USERS = "Users"` added
- [ ] `dotnet build RentifyxIdentity.slnx -c Release` passes with zero warnings

**Tests**: none
**Gate**: build
**Commit**: `feat(api): add Auth and Users endpoint tag constants`

---

### T18: Register endpoint + integration tests

**What**: Implement `POST /api/v1/auth/register` endpoint and integration tests covering the happy path and all error paths via `CustomWebApplicationFactory` with in-memory service fakes.
**Where**:
- `02-src/01-Api/RentifyxIdentity.Api/Endpoints/Auth/Register.cs` (new)
- `03-tests/05-Integration/RentifyxIdentity.Tests.Integration/Api/Identity/RegisterEndpointTests.cs` (new)
- `03-tests/05-Integration/RentifyxIdentity.Tests.Integration/CustomWebApplicationFactory.cs` (modify — add `FakeUserRepository` + `FakeEmailService` in-memory registrations)
- `03-tests/01-Common/RentifyxIdentity.Tests.Common/Fakes/FakeUserRepository.cs` (new)
- `03-tests/01-Common/RentifyxIdentity.Tests.Common/Fakes/FakeEmailService.cs` (new)
**Depends on**: T12 (UserResponse), T13 (RegisterUserHandler), T16 (IoC), T17 (Tags)
**Reuses**: `Api/Endpoints/Examples/Create.cs`, `Tests.Integration/Api/Examples/ExampleEndpointTests.cs`, `Tests.Integration/CustomWebApplicationFactory.cs`
**Requirement**: REG-01 through REG-10, REG-11, REG-23, REG-25, REG-28, REG-29

**Tools**:
- MCP: None
- Skill: None

**Done when**:
- [ ] `Register.cs` implements `IEndpoint`, maps `POST /auth/register`, calls `AllowAnonymous()`, returns 201 on success
- [ ] `FakeUserRepository` holds an in-memory `Dictionary<Guid, UserEntity>`; returns `null` for unknown email/taxId and stores entity on `AddAsync`
- [ ] `FakeEmailService` records calls in a `List<string>` (verifiable in tests); does NOT throw
- [ ] `CustomWebApplicationFactory` replaces `IUserRepository` with `FakeUserRepository` and `IEmailService` with `FakeEmailService` in `ConfigureServices`
- [ ] Integration test: POST valid payload → 201 + `UserResponse` body with correct fields, no TaxId/PasswordHash in body (REG-02, REG-25)
- [ ] Integration test: POST with duplicate email → 409 + problem details with `User.EmailAlreadyRegistered` (REG-07)
- [ ] Integration test: POST with duplicate TaxId → 409 + problem details with `User.TaxIdAlreadyRegistered` (REG-09)
- [ ] Integration test: POST with empty body → 422 + errors on all required fields (REG-11, REG-15, REG-17, REG-21, REG-23)
- [ ] Integration test: POST with invalid password → 422 + `PASSWORD_MIN_LENGTH` or `PASSWORD_COMPLEXITY` error (REG-18, REG-19)
- [ ] Integration test: no `X-Correlation-Id` in request → response has `X-Correlation-Id` header (REG-28)
- [ ] Full gate check passes: `dotnet test RentifyxIdentity.slnx`
- [ ] Test count: 6 tests pass

**Tests**: e2e (integration)
**Gate**: full
**Verify**: `dotnet test --filter "RegisterEndpointTests"` → 6 passed, 0 failed
**Commit**: `feat(api): add POST /auth/register endpoint with integration tests`

---

## Granularity Check

| Task | Scope | Status |
|---|---|---|
| T1: BCrypt package | 2 file modifications | ✅ Granular |
| T2: UserRole + UserStatus enums | 2 trivially small enum files, same concern | ✅ Granular |
| T3: Email VO + tests | 1 VO + 1 test file | ✅ Granular |
| T4: TaxDocument VO + tests | 1 VO + 1 test file | ✅ Granular |
| T5: Password VO + tests | 1 VO + 1 test file | ✅ Granular |
| T6: Error codes + constants + .resx | 3 constant/resource files, single concern | ✅ Granular |
| T7: RegisterUserRequest | 1 record file | ✅ Granular |
| T8: UserEntity + tests | 1 entity + 1 test file | ✅ Granular |
| T9: RegisterUserValidator + tests | 1 validator + 1 test file | ✅ Granular |
| T10: UserRegistered event | 1 record file | ✅ Granular |
| T11: IUserRepository + IEmailService | 2 interface files, both needed by same handler | ✅ Granular |
| T12: UserResponse + UserMapper | 2 files, single mapping concern | ✅ Granular |
| T13: RegisterUserHandler + tests | 1 handler + 1 test file | ✅ Granular |
| T14: UserRepository stub | 1 file | ✅ Granular |
| T15: EmailService stub | 1 file | ✅ Granular |
| T16: IoC registration | 1 file modification | ✅ Granular |
| T17: Tags constants | 1 file modification | ✅ Granular |
| T18: Register endpoint + E2E tests | 1 endpoint + 2 fakes + 1 test file + factory update | ✅ Granular (E2E tasks are inherently broader by design) |

---

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
|---|---|---|---|
| T1 | None | Phase 1 start | ✅ Match |
| T2 | None | T1 → T2 | ✅ Match |
| T3 | None | T1 → T3 | ✅ Match |
| T4 | None | T1 → T4 | ✅ Match |
| T5 | T1 | T1 → T5 | ✅ Match |
| T6 | None | T1 → T6 | ✅ Match |
| T7 | None | T1 → T7 | ✅ Match |
| T17 | None | T1 → T17 | ✅ Match |
| T8 | T2, T3, T4, T5 | T2+T3+T4+T5 → T8 | ✅ Match |
| T9 | T6, T7 | T6+T7 → T9 | ✅ Match |
| T10 | T2, T8 | T8 → T10 | ✅ Match |
| T11 | T8 | T8 → T11 | ✅ Match |
| T12 | T8 | T8 → T12 | ✅ Match |
| T13 | T9, T11, T12 | T9+T11+T12 → T13 | ✅ Match |
| T14 | T11 | T11 → T14 | ✅ Match |
| T15 | T11 | T11 → T15 | ✅ Match |
| T16 | T14, T15 | T14+T15 → T16 | ✅ Match |
| T18 | T12, T13, T16, T17 | T12+T13+T16+T17 → T18 | ✅ Match |

---

## Test Co-location Validation

| Task | Code Layer Created | Matrix Requires | Task Says | Status |
|---|---|---|---|---|
| T1 | Build config | none | none | ✅ OK |
| T2 | Domain enums | none (trivial; covered by T8/T9) | none | ✅ OK |
| T3 | Domain VO | unit | unit | ✅ OK |
| T4 | Domain VO | unit | unit | ✅ OK |
| T5 | Domain VO | unit | unit | ✅ OK |
| T6 | Constants / resources | none (covered by T9 validator tests) | none | ✅ OK |
| T7 | Request record | none (data-only; covered by T9) | none | ✅ OK |
| T8 | Domain entity | unit | unit | ✅ OK |
| T9 | Validator | unit | unit | ✅ OK |
| T10 | Domain event record | none (data-only; verified structurally in T13) | none | ✅ OK |
| T11 | Domain interfaces | none (interfaces only; verified by T13) | none | ✅ OK |
| T12 | Response DTO + mapper | none (data-only; verified by T13 + T18) | none | ✅ OK |
| T13 | Handler | unit | unit | ✅ OK |
| T14 | Infrastructure stub | none (stub; Testcontainers tests in E-04) | none | ✅ OK |
| T15 | Infrastructure stub | none (stub; Testcontainers tests in E-04) | none | ✅ OK |
| T16 | IoC wiring | integration (via T18 factory startup) | none (verified by T18) | ✅ OK — IoC correctness proven when T18 factory starts without DI errors |
| T17 | API constants | none | none | ✅ OK |
| T18 | API endpoint | e2e | e2e | ✅ OK |
