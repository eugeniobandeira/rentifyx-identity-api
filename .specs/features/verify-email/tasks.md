# Tasks — verify-email

**Feature:** verify-email (F-04-completion + F-05-verify-email)
**Spec:** spec.md
**Status:** Complete ✅
**Last updated:** 2026-06-27

---

## Dependency Map

```
T-01 ─┐
T-02 ─┤
T-03 ─┤─→ T-09 (handler needs events) ─→ T-12 (endpoint) ─→ T-14 ─→ T-15
T-04 ─┤
T-05 ─┘
T-06 ──→ (unblocks Login — standalone)
T-07 ──→ T-08 ──→ T-09
T-09 ──→ T-10 ──→ T-11
T-13 ──→ (can run parallel with T-09)
```

---

## Phase 1 — Domain Prerequisites

### T-01 — `UserEmailVerified` domain event

**Where:** `02-src/03-Domain/RentifyxIdentity.Domain/Events/UserEmailVerified.cs`
**Reuses:** `UserRegistered.cs` as pattern
**Depends on:** nothing

```csharp
public sealed record UserEmailVerified(Guid UserId, string Email, DateTimeOffset OccurredAt);
```

**Done when:** file compiles, matches REQ-010.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release` → 0 errors

---

### T-02 — `UserPasswordChanged` domain event

**Where:** `02-src/03-Domain/RentifyxIdentity.Domain/Events/UserPasswordChanged.cs`
**Reuses:** `UserRegistered.cs` as pattern
**Depends on:** nothing

```csharp
public sealed record UserPasswordChanged(Guid UserId, DateTimeOffset OccurredAt);
```

**Done when:** file compiles, matches REQ-011.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release` → 0 errors

---

### T-03 — `UserSuspended` domain event

**Where:** `02-src/03-Domain/RentifyxIdentity.Domain/Events/UserSuspended.cs`
**Reuses:** `UserRegistered.cs` as pattern
**Depends on:** nothing

```csharp
public sealed record UserSuspended(Guid UserId, string Reason, DateTimeOffset OccurredAt);
```

**Done when:** file compiles, matches REQ-012.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release` → 0 errors

---

### T-04 — `IPasswordHasher` interface

**Where:** `02-src/03-Domain/RentifyxIdentity.Domain/Interfaces/Users/IPasswordHasher.cs`
**Reuses:** `IEmailService.cs` as pattern (same namespace, same style)
**Depends on:** nothing

```csharp
public interface IPasswordHasher
{
    string Hash(string plaintext);
    bool Verify(string plaintext, string hash);
}
```

**Done when:** file compiles, matches REQ-013.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release` → 0 errors

---

### T-05 — `ITokenService` interface

**Where:** `02-src/03-Domain/RentifyxIdentity.Domain/Interfaces/Users/ITokenService.cs`
**Reuses:** `IEmailService.cs` as pattern
**Depends on:** nothing

```csharp
public interface ITokenService
{
    string GenerateAccessToken(Guid userId, string email, string role);
    string GenerateRefreshToken();
    string HashToken(string rawToken);
    bool VerifyTokenHash(string rawToken, string storedHash);
}
```

**Done when:** file compiles, matches REQ-014.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release` → 0 errors

---

### T-06 — `IPasswordHasher` stub + DI registration

**Where:**
- `02-src/05-Infrastructure/RentifyxIdentity.Infrastructure/Services/PasswordHasher.cs` (new stub)
- `02-src/04-IoC/RentifyxIdentity.IoC/InfrastructureDependencyInjection.cs` (add registration)

**Reuses:** `EmailService.cs` as stub pattern
**Depends on:** T-04

Stub:
```csharp
internal sealed class PasswordHasher : IPasswordHasher
{
    public string Hash(string plaintext) => BCrypt.Net.BCrypt.HashPassword(plaintext);
    public bool Verify(string plaintext, string hash) => BCrypt.Net.BCrypt.Verify(plaintext, hash);
}
```

DI:
```csharp
services.AddSingleton<IPasswordHasher, PasswordHasher>();
```

**Done when:** builds, `IPasswordHasher` is resolvable from DI.
**Gate:** `dotnet build RentifyxIdentity.slnx --configuration Release` → 0 errors

---

## Phase 2 — VerifyEmail Use Case

### T-07 — `VerifyEmailRequest`

**Where:** `02-src/02-Application/RentifyxIdentity.Application/Features/Identity/Auth/VerifyEmail/Request/VerifyEmailRequest.cs`
**Reuses:** `RegisterUserRequest.cs` as pattern
**Depends on:** nothing (can run parallel with T-01–T-06)

```csharp
public sealed record VerifyEmailRequest(string Email, string Token);
```

**Done when:** file compiles.
**Gate:** build → 0 errors

---

### T-08 — `VerifyEmailValidator`

**Where:** `02-src/02-Application/RentifyxIdentity.Application/Features/Identity/Auth/VerifyEmail/Validator/VerifyEmailValidator.cs`
**Reuses:** `RegisterUserValidator.cs` as pattern
**Depends on:** T-07

Rules (REQ validation section in spec):
- `Email`: `NotEmpty()` + `EmailAddress()`
- `Token`: `NotEmpty()`

**Done when:** all V-01–V-04 test scenarios would pass.
**Gate:** build → 0 errors

---

### T-09 — `VerifyEmailHandler`

**Where:** `02-src/02-Application/RentifyxIdentity.Application/Features/Identity/Auth/VerifyEmail/VerifyEmailHandler.cs`
**Reuses:** `RegisterUserHandler.cs` as pattern (IConfiguration for HMAC key, same HMAC-SHA256 computation)
**Depends on:** T-01, T-07, T-08

Logic:
1. Validate request → return errors if invalid
2. `GetByEmailAsync(request.Email)` → if null return `Error.Validation(InvalidOrExpiredToken)` (REQ-004)
3. If `Status == Suspended || Deleted` → `Error.Conflict(AccountNotVerifiable)` (REQ-007)
4. If `Status == Active` → return `UserMapper.ToResponse(user)` (REQ-006, idempotent)
5. Compute HMAC hash of `request.Token` using `Hmac:Key`
6. If hash ≠ `EmailVerificationTokenHash` or expiry < UtcNow → `Error.Validation(InvalidOrExpiredToken)` (REQ-002, REQ-003)
7. `user.VerifyEmail()` → `repository.UpdateAsync(user)` (REQ-005)
8. Log `UserEmailVerified` event
9. Return `UserMapper.ToResponse(user)`

**Constructor parameters (one per line):**
```csharp
public sealed class VerifyEmailHandler(
    IUserRepository repository,
    IValidator<VerifyEmailRequest> validator,
    IConfiguration configuration,
    ILogger<VerifyEmailHandler> logger) : IHandler<VerifyEmailRequest, UserResponse>
```

**Done when:** all H-01–H-07 scenarios behave as specified.
**Gate:** build → 0 errors

---

### T-10 — DI registration of `VerifyEmailHandler`

**Where:** `02-src/04-IoC/RentifyxIdentity.IoC/ApplicationDependencyInjection.cs`
**Reuses:** existing registration pattern for `RegisterUserHandler`
**Depends on:** T-09

Add:
```csharp
services.AddScoped<IHandler<VerifyEmailRequest, UserResponse>, VerifyEmailHandler>();
```

**Done when:** handler is resolvable from DI.
**Gate:** build → 0 errors

---

## Phase 3 — API Endpoint

### T-11 — `POST /api/v1/auth/verify-email` endpoint

**Where:** `02-src/01-Api/RentifyxIdentity.Api/Endpoints/Auth/VerifyEmail.cs`
**Reuses:** `Register.cs` as pattern (exact same structure, swap types)
**Depends on:** T-09, T-10

```csharp
internal sealed class VerifyEmail : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/verify-email", HandleAsync)
           .WithName("VerifyEmail")
           .WithDescription("Verifies a user's email address using the token sent via email.")
           .WithTags(Tags.Auth);
    }

    private static async Task<IResult> HandleAsync(
        VerifyEmailRequest request,
        IHandler<VerifyEmailRequest, UserResponse> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.Handle(request, cancellationToken);
        return result.Match(r => Results.Ok(r), e => e.ToProblem(httpContext));
    }
}
```

**Done when:** `POST /api/v1/auth/verify-email` returns 200/400/409 as per spec.
**Gate:** build → 0 errors

---

## Phase 4 — Tests

### T-12 — `VerifyEmailValidatorTests`

**Where:** `03-tests/02-Validators/RentifyxIdentity.Tests.Validators/Features/Identity/VerifyEmailValidatorTests.cs`
**Reuses:** `RegisterUserValidatorTests.cs` as pattern
**Depends on:** T-08

Cover V-01 to V-04 (see spec).

**Done when:** 4 tests pass.
**Gate:** `dotnet test --filter "VerifyEmailValidatorTests"` → 4 passed, 0 failed

---

### T-13 — `VerifyEmailHandlerTests`

**Where:** `03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers/Features/Identity/VerifyEmailHandlerTests.cs`
**Reuses:** `RegisterUserHandlerTests.cs` as pattern (Moq, FakeUserRepository, TestConstants)
**Depends on:** T-09

Cover H-01 to H-07 (see spec). Use `FakeUserRepository` with seeded user for most scenarios. For token hash scenarios, pre-compute HMAC using the same `dev-hmac-key` constant.

Add to `TestConstants`:
```csharp
public const string HmacKey = "dev-hmac-key";
public const string ValidVerificationToken = "<raw token>";
public static string ValidVerificationTokenHash => ComputeHmac(ValidVerificationToken, HmacKey);
```

**Done when:** 7 tests pass.
**Gate:** `dotnet test --filter "VerifyEmailHandlerTests"` → 7 passed, 0 failed

---

### T-14 — `VerifyEmailEndpointTests` (integration)

**Where:** `03-tests/05-Integration/RentifyxIdentity.Tests.Integration/Api/Identity/VerifyEmailEndpointTests.cs`
**Reuses:** `RegisterEndpointTests.cs` + `CustomWebApplicationFactory` + `FakeUserRepository`
**Depends on:** T-11, T-13

Scenarios I-01 to I-03 (see spec).

For I-01: extend `FakeEmailService` to capture the raw token sent in `SendVerificationEmailAsync`, then use it in the verify-email call.

**Done when:** 3 integration tests pass.
**Gate:** `dotnet test --filter "VerifyEmailEndpointTests"` → 3 passed, 0 failed

---

## Full gate check (all phases complete)

```bash
dotnet build RentifyxIdentity.slnx --configuration Release   # 0 errors
dotnet test RentifyxIdentity.slnx                            # all existing + 14 new = 66 passed
dotnet test --filter "VerifyEmail"                           # 14 passed (4+7+3)
```

---

## Status tracker

| Task | Status | Files changed | Tests |
|---|---|---|---|
| T-01 UserEmailVerified | ✅ done | `Events/UserEmailVerified.cs` | — |
| T-02 UserPasswordChanged | ✅ done | `Events/UserPasswordChanged.cs` | — |
| T-03 UserSuspended | ✅ done | `Events/UserSuspended.cs` | — |
| T-04 IPasswordHasher | ✅ done | `Interfaces/Users/IPasswordHasher.cs` | — |
| T-05 ITokenService | ✅ done | `Interfaces/Users/ITokenService.cs` | — |
| T-06 PasswordHasher stub + DI | ✅ done | `Infrastructure/Services/PasswordHasher.cs` · `InfrastructureDependencyInjection.cs` | — |
| T-07 VerifyEmailRequest | ✅ done | `Auth/VerifyEmail/Request/VerifyEmailRequest.cs` | — |
| T-08 VerifyEmailValidator | ✅ done | `Auth/VerifyEmail/Validator/VerifyEmailValidator.cs` | — |
| T-09 VerifyEmailHandler | ✅ done | `Auth/VerifyEmail/VerifyEmailHandler.cs` · `Constants/UserErrorCodes.cs` | — |
| T-10 DI registration | ✅ skipped | auto-discovered by reflection | — |
| T-11 API endpoint | ✅ done | `Endpoints/Auth/VerifyEmail.cs` · `IntegrationTestCollection.cs` | — |
| T-12 ValidatorTests | ✅ done | `VerifyEmailValidatorTests.cs` | 4/4 |
| T-13 HandlerTests | ✅ done | `VerifyEmailHandlerTests.cs` | 9/9 (8 handler + 1 validation) |
| T-14 EndpointTests | ✅ done | `VerifyEmailEndpointTests.cs` | 3/3 |
