# Tasks: E-05 Security Hardening & LGPD Compliance

## Summary Table

| # | Layer | What | Reference | Depends on |
|---|---|---|---|---|
| T-01 | API | Create `SecurityHeadersMiddleware` | `CorrelationIdMiddleware.cs` | none |
| T-02 | API | Add `UseSecurityHeaders()` extension | `MiddlewareExtensions.cs` | T-01 |
| T-03 | API | Register `UseSecurityHeaders()` in `Program.cs` before `UseAuthentication` | `Program.cs` | T-02 |
| T-04 | Test | Integration test — verify all 5 security headers present | `RegisterEndpointTests.cs` | T-03 |
| T-05 | Application | Refactor `RegisterUserHandler` — replace inline HMAC with `ITokenService.HashToken()` | `RegisterUserHandler.cs` | none |
| T-06 | Application | Refactor `VerifyEmailHandler` — replace inline HMAC with `ITokenService.VerifyTokenHash()` | `VerifyEmailHandler.cs` | none |
| T-07 | Application | Refactor `ResetPasswordHandler` — replace inline HMAC with `ITokenService.VerifyTokenHash()` | `ResetPasswordHandler.cs` | none |
| T-08 | Test | Update handler unit tests — mock `ITokenService`, remove `IConfiguration` for three handlers | `RegisterUserHandlerTests.cs` | T-05, T-06, T-07 |
| T-09 | Domain | Add `ConsentGivenAt` (`DateTimeOffset?`) property to `UserEntity` | `UserEntity.cs` | none |
| T-10 | Application | Add `ConsentGiven` (`bool`) field to `RegisterUserRequest` | `RegisterUserRequest.cs` | none |
| T-11 | Application | Add `ConsentGiven == true` validation rule to `RegisterUserValidator` | `RegisterUserValidator.cs` | T-10 |
| T-12 | Application | Set `ConsentGivenAt` on `userEntity` in `RegisterUserHandler` success path | `RegisterUserHandler.cs` | T-09, T-10 |
| T-13 | Infrastructure | Map `ConsentGivenAt` to/from DynamoDB attribute in `UserDynamoDbMapper` | `UserDynamoDbMapper.cs` | T-09 |
| T-14 | Test | Update `RegisterUserRequestBuilder` — default `ConsentGiven = true`, add `WithConsentGiven()` | `RegisterUserRequestBuilder.cs` | T-10 |
| T-15 | Test | Add validator unit tests for `ConsentGiven` field | `RegisterUserValidatorTests.cs` | T-11, T-14 |
| T-16 | Test | Update `RegisterUserHandlerTests` — assert `ConsentGivenAt` is non-null on success path | `RegisterUserHandlerTests.cs` | T-12, T-14 |
| T-17 | Test | Run existing register integration tests to confirm no regressions | `RegisterEndpointTests.cs` | T-12, T-13, T-14 |
| T-18 | Domain | Add `IAuditLogService` interface to `Domain/Interfaces/` | `ITokenService.cs` | none |
| T-19 | Domain | Add `AuditEvents` static class with three event-type string constants | `UserErrorCodes.cs` | none |
| T-20 | Infrastructure | Implement `AuditLogService` in `Infrastructure/Services/` | `UserRepository.cs` / `TokenService.cs` | T-18, T-19 |
| T-21 | IoC | Register `IAuditLogService` → `AuditLogService` as singleton in `InfrastructureDependencyInjection` | `InfrastructureDependencyInjection.cs` | T-20 |
| T-22 | Application | Inject `IAuditLogService` into `GetProfileHandler` — call `LogAsync` on success, swallow exceptions | `GetProfileHandler.cs` | T-18, T-19, T-21 |
| T-23 | Application | Inject `IAuditLogService` into `ExportDataHandler` — call `LogAsync` on success, swallow exceptions | `ExportDataHandler.cs` | T-18, T-19, T-21 |
| T-24 | Application | Inject `IAuditLogService` into `DeleteAccountHandler` — call `LogAsync` on success, swallow exceptions | `DeleteAccountHandler.cs` | T-18, T-19, T-21 |
| T-25 | Test | Add `FakeAuditLogService` to `Tests.Common/Fakes/` | `FakeEmailService.cs` | T-18 |
| T-26 | Test | Register `FakeAuditLogService` in `CustomWebApplicationFactory` | `CustomWebApplicationFactory.cs` | T-25 |
| T-27 | Test | Unit test `AuditLogService.LogAsync` — verify PK pattern, EventType, OccurredAt, TTL | `TokenServiceTests.cs` | T-20 |
| T-28 | Test | Handler unit tests for `GetProfile`, `ExportData`, `DeleteAccount` — audit called on success; not on error | `GetProfileHandlerTests.cs` | T-22, T-23, T-24, T-25 |
| T-29 | Test | Integration tests — assert `FakeAuditLogService` captures correct entry after each LGPD endpoint call | `LgpdEndpointTests.cs` | T-26, T-22, T-23, T-24 |

---

## Section A — Security Headers Middleware

---
status: pending
title: Create SecurityHeadersMiddleware
type: backend
complexity: low
dependencies: none
---

**Layer:** API
**File:** `02-src/01-Api/RentifyxIdentity.Api/Middlewares/SecurityHeadersMiddleware.cs`
**Reference:** `02-src/01-Api/RentifyxIdentity.Api/Middlewares/CorrelationIdMiddleware.cs`
**What:** Create a middleware class that sets five HTTP security headers (`Content-Security-Policy`, `X-Frame-Options`, `X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy`) on every response before passing to the next delegate.
**Done when:** File compiles as part of `RentifyxIdentity.Api`; `dotnet build` passes; five headers are set unconditionally in `InvokeAsync`.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release`

---
status: pending
title: Add UseSecurityHeaders extension method
type: backend
complexity: low
dependencies: T-01
---

**Layer:** API
**File:** `02-src/01-Api/RentifyxIdentity.Api/Extensions/MiddlewareExtensions.cs`
**Reference:** `02-src/01-Api/RentifyxIdentity.Api/Extensions/MiddlewareExtensions.cs`
**What:** Add `UseSecurityHeaders(this IApplicationBuilder app)` extension method to the existing `MiddlewareExtensions` static class that calls `app.UseMiddleware<SecurityHeadersMiddleware>()`.
**Done when:** `dotnet build` passes; `UseSecurityHeaders` is visible on `IApplicationBuilder`.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release`

---
status: pending
title: Register UseSecurityHeaders in Program.cs before UseAuthentication
type: backend
complexity: low
dependencies: T-02
---

**Layer:** API
**File:** `02-src/01-Api/RentifyxIdentity.Api/Program.cs`
**Reference:** `02-src/01-Api/RentifyxIdentity.Api/Program.cs`
**What:** Add `app.UseSecurityHeaders()` call in `Program.cs` positioned after `app.UseCorrelationId()` and before `app.UseAuthentication()`.
**Done when:** `dotnet build` passes; call order in `Program.cs` places `UseSecurityHeaders` before `UseAuthentication`.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release`

---
status: pending
title: Integration test — verify all 5 security headers present in any response
type: test
complexity: low
dependencies: T-03
---

**Layer:** Test
**File:** `03-tests/05-Integration/RentifyxIdentity.Tests.Integration/Api/Identity/SecurityHeadersTests.cs`
**Reference:** `03-tests/05-Integration/RentifyxIdentity.Tests.Integration/Api/Identity/RegisterEndpointTests.cs`
**What:** Create an integration test class that issues a `POST /api/v1/auth/register` request and asserts all five security headers are present with their expected values.
**Done when:** `dotnet test` passes; one test per header (5 tests total) or one combined test; all assertions use FluentAssertions.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release`

---

## Section B — Handler HMAC Refactor

---
status: pending
title: Refactor RegisterUserHandler — replace HMAC inline with ITokenService.HashToken()
type: refactor
complexity: low
dependencies: none
---

**Layer:** Application
**File:** `02-src/02-Application/RentifyxIdentity.Application/Features/Identity/Auth/Register/RegisterUserHandler.cs`
**Reference:** `02-src/02-Application/RentifyxIdentity.Application/Features/Identity/Auth/Register/RegisterUserHandler.cs`
**What:** Remove `IConfiguration` constructor parameter, remove `using System.Security.Cryptography`, remove the inline `HMACSHA256` block, inject `ITokenService`, and replace HMAC computation with `_tokenService.HashToken(rawToken)`.
**Done when:** `dotnet build` passes; `IConfiguration` is no longer imported or injected in `RegisterUserHandler`; `ITokenService.HashToken` is called instead.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release`

---
status: pending
title: Refactor VerifyEmailHandler — replace HMAC inline with ITokenService.VerifyTokenHash()
type: refactor
complexity: low
dependencies: none
---

**Layer:** Application
**File:** `02-src/02-Application/RentifyxIdentity.Application/Features/Identity/Auth/VerifyEmail/VerifyEmailHandler.cs`
**Reference:** `02-src/02-Application/RentifyxIdentity.Application/Features/Identity/Auth/VerifyEmail/VerifyEmailHandler.cs`
**What:** Remove `IConfiguration` constructor parameter, remove `using System.Security.Cryptography` and `using System.Text`, inject `ITokenService`, and replace the HMAC comparison block with `_tokenService.VerifyTokenHash(request.Token, user.EmailVerificationTokenHash)`.
**Done when:** `dotnet build` passes; `IConfiguration` is no longer imported or injected in `VerifyEmailHandler`; `ITokenService.VerifyTokenHash` is called for token comparison.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release`

---
status: pending
title: Refactor ResetPasswordHandler — replace HMAC inline with ITokenService.VerifyTokenHash()
type: refactor
complexity: low
dependencies: none
---

**Layer:** Application
**File:** `02-src/02-Application/RentifyxIdentity.Application/Features/Identity/Auth/ResetPassword/ResetPasswordHandler.cs`
**Reference:** `02-src/02-Application/RentifyxIdentity.Application/Features/Identity/Auth/ResetPassword/ResetPasswordHandler.cs`
**What:** Remove `IConfiguration` constructor parameter, remove `using System.Security.Cryptography` and `using System.Text`, inject `ITokenService`, and replace the HMAC comparison block with `_tokenService.VerifyTokenHash(request.Token, user.PasswordResetTokenHash)`.
**Done when:** `dotnet build` passes; `IConfiguration` is no longer imported or injected in `ResetPasswordHandler`; `ITokenService.VerifyTokenHash` is called for token comparison.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release`

---
status: pending
title: Update handler unit tests for RegisterUser, VerifyEmail, ResetPassword — mock ITokenService, remove IConfiguration
type: test
complexity: medium
dependencies: T-05, T-06, T-07
---

**Layer:** Test
**File:** `03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers/Features/Identity/RegisterUserHandlerTests.cs`, `VerifyEmailHandlerTests.cs`, `ResetPasswordHandlerTests.cs`
**Reference:** `03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers/Features/Identity/RegisterUserHandlerTests.cs`
**What:** In all three handler test classes, remove `Mock<IConfiguration>` and its setup, add `Mock<ITokenService>`, set up `HashToken` to return a deterministic hash string (for Register) and `VerifyTokenHash` to return `true` (for Verify/Reset), and pass `_tokenServiceMock.Object` to each handler constructor instead of `_configurationMock.Object`.
**Done when:** `dotnet test` passes; no `IConfiguration` mock remains in any of the three test classes; all existing test scenarios continue to pass.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release`

---

## Section C — LGPD Consent Capture

---
status: pending
title: Add ConsentGivenAt property to UserEntity
type: backend
complexity: low
dependencies: none
---

**Layer:** Domain
**File:** `02-src/03-Domain/RentifyxIdentity.Domain/Entities/UserEntity.cs`
**Reference:** `02-src/03-Domain/RentifyxIdentity.Domain/Entities/UserEntity.cs`
**What:** Add `public DateTimeOffset? ConsentGivenAt { get; private set; }` property to `UserEntity`, add a `SetConsent(DateTimeOffset timestamp)` method that sets the property, and update `Reconstitute(...)` to accept and populate the new `consentGivenAt` parameter.
**Done when:** `dotnet build` passes; property has a private setter; `Reconstitute` signature includes `consentGivenAt`; existing tests still compile.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release`

---
status: pending
title: Add ConsentGiven field to RegisterUserRequest
type: backend
complexity: low
dependencies: none
---

**Layer:** Application
**File:** `02-src/02-Application/RentifyxIdentity.Application/Features/Identity/Auth/Register/Request/RegisterUserRequest.cs`
**Reference:** `02-src/02-Application/RentifyxIdentity.Application/Features/Identity/Auth/Register/Request/RegisterUserRequest.cs`
**What:** Add `bool ConsentGiven` as a positional parameter to the `RegisterUserRequest` record (last parameter, after `Role`).
**Done when:** `dotnet build` passes; `RegisterUserRequest` has a `ConsentGiven` property; all existing usages that omit the field will produce compiler errors caught by the build gate.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release`

---
status: pending
title: Add ConsentGiven validation rule to RegisterUserValidator
type: backend
complexity: low
dependencies: T-10
---

**Layer:** Application
**File:** `02-src/02-Application/RentifyxIdentity.Application/Features/Identity/Auth/Register/Validator/RegisterUserValidator.cs`
**Reference:** `02-src/02-Application/RentifyxIdentity.Application/Features/Identity/Auth/Register/Validator/RegisterUserValidator.cs`
**What:** Add a `RuleFor(x => x.ConsentGiven).Must(c => c).WithMessage(ValidationMessageResource.CONSENT_REQUIRED)` rule to `RegisterUserValidator`, and add `CONSENT_REQUIRED` to `ValidationMessageResource`.
**Done when:** `dotnet build` passes; a request with `ConsentGiven = false` fails validation with the `CONSENT_REQUIRED` message; a request with `ConsentGiven = true` continues to pass.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release`

---
status: pending
title: Set ConsentGivenAt in RegisterUserHandler on success path
type: backend
complexity: low
dependencies: T-09, T-10
---

**Layer:** Application
**File:** `02-src/02-Application/RentifyxIdentity.Application/Features/Identity/Auth/Register/RegisterUserHandler.cs`
**Reference:** `02-src/02-Application/RentifyxIdentity.Application/Features/Identity/Auth/Register/RegisterUserHandler.cs`
**What:** After `UserEntity.Create(...)` and before `repository.AddAsync(...)`, call `user.SetConsent(DateTimeOffset.UtcNow)` to record the consent timestamp.
**Done when:** `dotnet build` passes; `ConsentGivenAt` is non-null on the `UserEntity` instance passed to `AddAsync` whenever `ConsentGiven == true`.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release`

---
status: pending
title: Map ConsentGivenAt in UserDynamoDbMapper — ToItem and ToEntity
type: backend
complexity: low
dependencies: T-09
---

**Layer:** Infrastructure
**File:** `02-src/05-Infrastructure/RentifyxIdentity.Infrastructure/Mapping/UserDynamoDbMapper.cs`
**Reference:** `02-src/05-Infrastructure/RentifyxIdentity.Infrastructure/Mapping/UserDynamoDbMapper.cs`
**What:** In `ToItem`, conditionally write `item["ConsentGivenAt"] = new AttributeValue { S = entity.ConsentGivenAt.Value.ToString("O") }` when the property is non-null; in `ToEntity`, read the optional attribute and pass the parsed `DateTimeOffset?` to the updated `Reconstitute` call.
**Done when:** `dotnet build` passes; `ConsentGivenAt` round-trips through DynamoDB serialization; existing records without the attribute deserialize with `null`.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release`

---
status: pending
title: Update RegisterUserRequestBuilder — default ConsentGiven = true, add WithConsentGiven()
type: test
complexity: low
dependencies: T-10
---

**Layer:** Test
**File:** `03-tests/01-Common/RentifyxIdentity.Tests.Common/Builders/RegisterUserRequestBuilder.cs`
**Reference:** `03-tests/01-Common/RentifyxIdentity.Tests.Common/Builders/RegisterUserRequestBuilder.cs`
**What:** Add `private bool _consentGiven = true` field to `RegisterUserRequestBuilder`, add `WithConsentGiven(bool value)` fluent method, and pass `_consentGiven` as the new last argument to the `RegisterUserRequest` constructor in `Build()`.
**Done when:** `dotnet build` passes; `Build()` produces a request with `ConsentGiven = true` by default; `WithConsentGiven(false)` overrides it.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release`

---
status: pending
title: Add validator unit tests for ConsentGiven field
type: test
complexity: low
dependencies: T-11, T-14
---

**Layer:** Test
**File:** `03-tests/02-Validators/RentifyxIdentity.Tests.Validators/Features/Identity/RegisterUserValidatorTests.cs`
**Reference:** `03-tests/02-Validators/RentifyxIdentity.Tests.Validators/Features/Identity/RegisterUserValidatorTests.cs`
**What:** Add two test methods: `ConsentGiven_True_ShouldPassValidation` and `ConsentGiven_False_ShouldReturnConsentRequiredError`, verifying that `false` produces a failure with the `CONSENT_REQUIRED` message and `true` passes.
**Done when:** `dotnet test` passes; two new test methods exist and cover both branches of the `ConsentGiven` rule.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release`

---
status: pending
title: Update RegisterUserHandlerTests — assert ConsentGivenAt is set on success path
type: test
complexity: low
dependencies: T-12, T-14
---

**Layer:** Test
**File:** `03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers/Features/Identity/RegisterUserHandlerTests.cs`
**Reference:** `03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers/Features/Identity/RegisterUserHandlerTests.cs`
**What:** In the `HappyPath_RegistersUser_ReturnsUserResponse` test, add a `Verify` or captured-argument assertion that the `UserEntity` passed to `AddAsync` has a non-null `ConsentGivenAt`; also update the builder call to use `WithConsentGiven(true)` explicitly.
**Done when:** `dotnet test` passes; assertion confirms `ConsentGivenAt.HasValue == true` on the entity passed to the repository.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release`

---
status: pending
title: Integration test — confirm existing register tests still pass after ConsentGiven changes
type: test
complexity: low
dependencies: T-12, T-13, T-14
---

**Layer:** Test
**File:** `03-tests/05-Integration/RentifyxIdentity.Tests.Integration/Api/Identity/RegisterEndpointTests.cs`
**Reference:** `03-tests/05-Integration/RentifyxIdentity.Tests.Integration/Api/Identity/RegisterEndpointTests.cs`
**What:** Verify all existing `RegisterEndpointTests` continue to pass unchanged (the builder now defaults `ConsentGiven = true`, so no test edits are required beyond confirming the suite is green); add one test `RegisterUser_WithConsentFalse_Returns422` that uses `WithConsentGiven(false)` and asserts 422.
**Done when:** `dotnet test` passes; all pre-existing register integration tests are green; the new `ConsentFalse` test returns 422.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release`

---

## Section D — Audit Log

---
status: pending
title: Add IAuditLogService interface to Domain/Interfaces/
type: backend
complexity: low
dependencies: none
---

**Layer:** Domain
**File:** `02-src/03-Domain/RentifyxIdentity.Domain/Interfaces/Users/IAuditLogService.cs`
**Reference:** `02-src/03-Domain/RentifyxIdentity.Domain/Interfaces/Users/ITokenService.cs`
**What:** Create `IAuditLogService` interface with a single method `Task LogAsync(Guid userId, string eventType, CancellationToken ct)`.
**Done when:** `dotnet build` passes; interface is in the `RentifyxIdentity.Domain.Interfaces.Users` namespace.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release`

---
status: pending
title: Add AuditEvents static class with event-type string constants
type: backend
complexity: low
dependencies: none
---

**Layer:** Domain
**File:** `02-src/03-Domain/RentifyxIdentity.Domain/Constants/AuditEvents.cs`
**Reference:** `02-src/03-Domain/RentifyxIdentity.Domain/Constants/UserErrorCodes.cs`
**What:** Create a `public static class AuditEvents` with three `public const string` fields: `ProfileAccessed = "PROFILE_ACCESSED"`, `DataExported = "DATA_EXPORTED"`, and `AccountDeleted = "ACCOUNT_DELETED"`.
**Done when:** `dotnet build` passes; constants are accessible from Application and Infrastructure layers.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release`

---
status: pending
title: Implement AuditLogService in Infrastructure/Services/
type: backend
complexity: medium
dependencies: T-18, T-19
---

**Layer:** Infrastructure
**File:** `02-src/05-Infrastructure/RentifyxIdentity.Infrastructure/Services/AuditLogService.cs`
**Reference:** `02-src/05-Infrastructure/RentifyxIdentity.Infrastructure/Repositories/UserRepository.cs`
**What:** Implement `IAuditLogService` using `IAmazonDynamoDB`: build a PK of the form `AUDIT#{userId}#{timestamp:yyyyMMddHHmmss}_{guid}`, set `SK` equal to `PK`, write `UserId`, `EventType`, `OccurredAt` (ISO-8601), and `TTL` (Unix epoch + 90 days) as DynamoDB attributes using `PutItemAsync`. Read table name from `IConfiguration["DynamoDB:TableName"]`.
**Done when:** `dotnet build` passes; `LogAsync` constructs the correct PK format and calls `PutItemAsync` once per invocation.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release`

---
status: pending
title: Register IAuditLogService in InfrastructureDependencyInjection as singleton
type: backend
complexity: low
dependencies: T-20
---

**Layer:** IoC
**File:** `02-src/04-IoC/RentifyxIdentity.IoC/InfrastructureDependencyInjection.cs`
**Reference:** `02-src/04-IoC/RentifyxIdentity.IoC/InfrastructureDependencyInjection.cs`
**What:** Add `services.AddSingleton<IAuditLogService, AuditLogService>()` inside `InfrastructureDependencyInjection.Register(...)`.
**Done when:** `dotnet build` passes; `IAuditLogService` resolves from the DI container at runtime.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release`

---
status: pending
title: Inject IAuditLogService into GetProfileHandler and call LogAsync on success
type: backend
complexity: low
dependencies: T-18, T-19, T-21
---

**Layer:** Application
**File:** `02-src/02-Application/RentifyxIdentity.Application/Features/Identity/User/GetProfile/GetProfileHandler.cs`
**Reference:** `02-src/02-Application/RentifyxIdentity.Application/Features/Identity/User/GetProfile/GetProfileHandler.cs`
**What:** Add `IAuditLogService auditLogService` to the primary constructor, and after the successful `UserMapper.ToResponse(user)` call, wrap `await _auditLogService.LogAsync(user.Id, AuditEvents.ProfileAccessed, ct)` in a `try/catch` that logs a warning on failure without rethrowing.
**Done when:** `dotnet build` passes; `LogAsync` is called once on the success path; exceptions from `LogAsync` are swallowed and logged as warnings.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release`

---
status: pending
title: Inject IAuditLogService into ExportDataHandler and call LogAsync on success
type: backend
complexity: low
dependencies: T-18, T-19, T-21
---

**Layer:** Application
**File:** `02-src/02-Application/RentifyxIdentity.Application/Features/Identity/User/ExportData/ExportDataHandler.cs`
**Reference:** `02-src/02-Application/RentifyxIdentity.Application/Features/Identity/User/ExportData/ExportDataHandler.cs`
**What:** Add `IAuditLogService auditLogService` to the primary constructor, and after the `UserDataExportResponse` is constructed, wrap `await _auditLogService.LogAsync(user.Id, AuditEvents.DataExported, ct)` in a `try/catch` that logs a warning on failure without rethrowing.
**Done when:** `dotnet build` passes; `LogAsync` is called once on the success path; exceptions from `LogAsync` are swallowed and logged as warnings.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release`

---
status: pending
title: Inject IAuditLogService into DeleteAccountHandler and call LogAsync on success
type: backend
complexity: low
dependencies: T-18, T-19, T-21
---

**Layer:** Application
**File:** `02-src/02-Application/RentifyxIdentity.Application/Features/Identity/User/DeleteAccount/DeleteAccountHandler.cs`
**Reference:** `02-src/02-Application/RentifyxIdentity.Application/Features/Identity/User/DeleteAccount/DeleteAccountHandler.cs`
**What:** Add `IAuditLogService auditLogService` to the primary constructor, and after `repository.UpdateAsync(user, ct)` on the success path, wrap `await _auditLogService.LogAsync(userId, AuditEvents.AccountDeleted, ct)` in a `try/catch` that logs a warning on failure without rethrowing.
**Done when:** `dotnet build` passes; `LogAsync` is called once on the success path; exceptions from `LogAsync` are swallowed and logged as warnings.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release`

---
status: pending
title: Add FakeAuditLogService to Tests.Common/Fakes/
type: test
complexity: low
dependencies: T-18
---

**Layer:** Test
**File:** `03-tests/01-Common/RentifyxIdentity.Tests.Common/Fakes/FakeAuditLogService.cs`
**Reference:** `03-tests/01-Common/RentifyxIdentity.Tests.Common/Fakes/FakeEmailService.cs`
**What:** Create `FakeAuditLogService : IAuditLogService` that records each `LogAsync` call as a `(Guid UserId, string EventType)` tuple in a public `List<(Guid UserId, string EventType)> Entries` property and returns `Task.CompletedTask`.
**Done when:** `dotnet build` passes; `Entries` is populated after each `LogAsync` call; list is accessible for assertion in tests.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release`

---
status: pending
title: Register FakeAuditLogService in CustomWebApplicationFactory
type: test
complexity: low
dependencies: T-25
---

**Layer:** Test
**File:** `03-tests/05-Integration/RentifyxIdentity.Tests.Integration/CustomWebApplicationFactory.cs`
**Reference:** `03-tests/05-Integration/RentifyxIdentity.Tests.Integration/CustomWebApplicationFactory.cs`
**What:** Add `public FakeAuditLogService AuditLogService { get; } = new()` property to `CustomWebApplicationFactory` and register it as `services.AddSingleton<IAuditLogService>(AuditLogService)` inside `ConfigureServices`.
**Done when:** `dotnet build` passes; `factory.AuditLogService` is accessible in integration tests; the real `AuditLogService` is replaced in the test host.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release`

---
status: pending
title: Unit test AuditLogService.LogAsync — verify PK pattern, EventType, OccurredAt, TTL
type: test
complexity: medium
dependencies: T-20
---

**Layer:** Test
**File:** `03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers/Features/Identity/AuditLogServiceTests.cs`
**Reference:** `03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers/Features/Identity/TokenServiceTests.cs`
**What:** Create unit tests for `AuditLogService` using a `Mock<IAmazonDynamoDB>` — capture the `PutItemRequest` passed to `PutItemAsync` and assert: `PK` starts with `AUDIT#`, `EventType` attribute matches the passed event string, `OccurredAt` is a parseable ISO-8601 UTC string, and `TTL` is a number approximately 90 days from now (within 5 seconds tolerance).
**Done when:** `dotnet test` passes; at least one test per assertion category (PK format, EventType, OccurredAt, TTL); mock verifies `PutItemAsync` called exactly once.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release`

---
status: pending
title: Handler unit tests for GetProfile, ExportData, DeleteAccount — audit called on success, not on error
type: test
complexity: medium
dependencies: T-22, T-23, T-24, T-25
---

**Layer:** Test
**File:** `03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers/Features/Identity/GetProfileHandlerTests.cs`, `ExportDataHandlerTests.cs`, `DeleteAccountHandlerTests.cs`
**Reference:** `03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers/Features/Identity/GetProfileHandlerTests.cs`
**What:** In each of the three handler test classes, add `Mock<IAuditLogService>` to the constructor, pass it to the handler, and add two test scenarios per handler: (1) success path verifies `LogAsync` was called once with the correct event type; (2) error path (user not found) verifies `LogAsync` was never called. Also add a test where `LogAsync` throws and asserts the handler still returns success.
**Done when:** `dotnet test` passes; six new test methods exist (two per handler); `Times.Once` and `Times.Never` verifications are used correctly.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release`

---
status: pending
title: Integration tests — PROFILE_ACCESSED, DATA_EXPORTED, ACCOUNT_DELETED entries captured by FakeAuditLogService
type: test
complexity: medium
dependencies: T-26, T-22, T-23, T-24
---

**Layer:** Test
**File:** `03-tests/05-Integration/RentifyxIdentity.Tests.Integration/Api/Identity/LgpdEndpointTests.cs`
**Reference:** `03-tests/05-Integration/RentifyxIdentity.Tests.Integration/Api/Identity/LgpdEndpointTests.cs`
**What:** Add three test methods to `LgpdEndpointTests`: after `GET /api/v1/users/me` assert `factory.AuditLogService.Entries` contains one entry with `EventType == "PROFILE_ACCESSED"`; after `GET /api/v1/users/me/data-export` assert `"DATA_EXPORTED"`; after `DELETE /api/v1/users/me` assert `"ACCOUNT_DELETED"`.
**Done when:** `dotnet test` passes; three new test methods exist and all assertions use FluentAssertions; existing LGPD tests remain green.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release`
