# Code Conventions

## Naming Conventions

**Files / Classes:**

| Type | Pattern | Examples |
|---|---|---|
| Entity | `{Entity}Entity` (sealed class) | `ExampleEntity` |
| Value object | `{Concept}` (sealed record) | `Email`, `Password`, `TaxDocument` |
| Domain event | `{Subject}{PastTense}` (sealed record) | `UserRegistered`, `UserEmailVerified` |
| Repository interface | `I{Entity}Repository` | `IUserRepository` |
| Service interface | `I{Concept}Service` | `ITokenService`, `IEmailService` |
| Repository implementation | `{Entity}Repository` (sealed class) | `UserRepository` |
| Handler | `{Action}{Entity}Handler` (sealed class) | `RegisterUserHandler`, `LoginHandler` |
| Request DTO | `{Action}{Entity}Request` (sealed record) | `RegisterUserRequest`, `LoginRequest` |
| Validator | `{Action}{Entity}Validator` (sealed class) | `RegisterUserValidator` |
| Response DTO | `{Entity}Response` (sealed record) | `UserResponse`, `LoginResponse` |
| Mapper | `{Entity}Mapper` (static class) | `UserMapper` |
| API endpoint | `{Action}` (sealed class, inside namespace folder) | `Register`, `Login`, `GetProfile` |
| Error codes | `{Entity}ErrorCodes` (static class) | `UserErrorCodes` |
| Validation constants | `ValidationConstants.{Feature}Rules` (nested static) | `ValidationConstants.UserRules` |
| Tag constants | `Tags` (internal static class) | `Tags.AUTH`, `Tags.USERS` |
| Builder (test) | `{Entity}Builder` (sealed class) | `UserBuilder` |
| Test class | `{Class}Tests` (sealed class) | `RegisterUserHandlerTests` |

**Methods:**

| Type | Pattern | Examples |
|---|---|---|
| Entity factory | `Create(...)` (static) | `UserEntity.Create(email, taxId, password, role)` |
| Entity mutation | Verb-based instance methods | `user.VerifyEmail()`, `user.Suspend(reason, by)` |
| Handler | `Handle(request, ct)` | Defined by `IHandler<,>` |
| Endpoint handler | `HandleAsync(request, handler, httpContext, ct)` (private static) | — |
| Mapper | `ToResponse(entity)`, `Create{Entity}(request)` | `UserMapper.ToResponse(user)` |
| Repository | `AddAsync`, `GetByIdAsync`, `UpdateAsync`, `DeleteAsync`, `GetAllAsync` | Defined by `IRepository<>` |

**Test methods:** `{Action}_{Condition}_{Expected}` — e.g., `Register_DuplicateEmail_ReturnsConflict`

**Error codes:** `{Namespace}.{Verb}` — e.g., `User.NotFound`, `User.EmailAlreadyRegistered`

**Validation message keys:** `SCREAMING_SNAKE_CASE` in `.resx` — e.g., `EMAIL_REQUIRED`, `PASSWORD_COMPLEXITY`

## Code Organization

**Entity pattern:**
```csharp
public sealed class UserEntity
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    // ... other properties with private setters

    private UserEntity() { }  // private constructor (DynamoDB deserialization)

    public static UserEntity Create(...) { ... }  // static factory
    public void VerifyEmail() { ... }             // state mutation methods
}
```

**Handler pattern:**
```csharp
public sealed class RegisterUserHandler(
    IUserRepository repository,
    IValidator<RegisterUserRequest> validator,
    ILogger<RegisterUserHandler> logger) : IHandler<RegisterUserRequest, UserResponse>
{
    public async Task<ErrorOr<UserResponse>> Handle(RegisterUserRequest request, CancellationToken ct)
    {
        logger.LogInformation("...", request);
        List<Error>? errors = await validator.ValidateToErrorsAsync(request, ct);
        if (errors is not null) return errors;
        // ... business logic
        return UserMapper.ToResponse(entity);
    }
}
```

**Endpoint pattern:**
```csharp
internal sealed class Register : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/auth/register", HandleAsync)
           .WithName("RegisterUser").WithDescription("...").WithTags(Tags.AUTH)
           .AllowAnonymous();

    private static async Task<IResult> HandleAsync(
        RegisterUserRequest request,
        IHandler<RegisterUserRequest, UserResponse> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.Handle(request, cancellationToken);
        return result.Match(
            user => Results.Created($"/api/v1/users/{user.Id}", user),
            errors => errors.ToProblem(httpContext));
    }
}
```

## Type Safety

- `Nullable=enable` globally — null-safe throughout
- `TreatWarningsAsErrors=true` — nullable warnings are build errors
- Private setters on all entity properties
- Sealed classes/records everywhere to prevent unintended inheritance
- `ArgumentException.ThrowIfNullOrWhiteSpace` for guard checks in domain

## Error Handling

**Pattern:** ErrorOr, never exceptions for control flow.

```csharp
// In handlers — return errors directly:
if (existingUser is not null)
    return Error.Conflict(UserErrorCodes.EmailAlreadyRegistered,
                          ValidationMessageResource.USER_EMAIL_ALREADY_REGISTERED);

// In endpoints — match to IResult:
return result.Match(
    success => Results.Ok(success),
    errors  => errors.ToProblem(httpContext));

// ToProblem maps ErrorType → HTTP status:
// Validation  → 422
// NotFound    → 404
// Conflict    → 409
// Unauthorized → 401
// _           → 500
```

## Validation

```csharp
public sealed class RegisterUserValidator : AbstractValidator<RegisterUserRequest>
{
    public RegisterUserValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(ValidationMessageResource.EMAIL_REQUIRED)
            .EmailAddress().WithMessage(ValidationMessageResource.EMAIL_INVALID_FORMAT)
            .MaximumLength(ValidationConstants.UserRules.EmailMaxLength)
                .WithMessage(ValidationMessageResource.EMAIL_MAX_LENGTH);
    }
}
```

- All messages come from `.resx` resource file (`ValidationMessageResource`)
- All length/count rules come from `ValidationConstants.{Feature}Rules`
- Async rules (`MustAsync`) used for cross-entity checks (duplicate detection)
- `CascadeMode = CascadeMode.Stop` on class level when early-exit is needed

## Logging

```csharp
logger.LogInformation("Creating user. Payload={@Payload}", request);
logger.LogInformation("User created. Response={@Response}", entity);
```

- Structured logging with `{@Property}` destructuring
- Correlation ID automatically enriched via `LogContext` — no manual passing needed
- `ToString()` on sensitive types must return redacted form (e.g., `Password` → `[REDACTED]`, `TaxDocument` → masked)

## Comments

- No XML doc comments on internal types
- No inline comments unless logic is non-obvious
- `// TODO:` used in Infrastructure stubs only (`#pragma warning disable S1135`)
- `#pragma warning disable/restore` used only for legitimate SonarQube suppressions with justification
