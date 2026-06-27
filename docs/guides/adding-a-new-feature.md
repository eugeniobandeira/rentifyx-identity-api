# Guide: Adding a New Feature

Use the `Example` feature as a living reference implementation. Follow this order strictly — each layer depends only on what came before it.

---

## 1. Domain

Location: `02-src/03-Domain/RentifyxIdentity.Domain/`

- Add the **entity** or **aggregate root** under `Entities/`. Use a static factory method (`Create(...)`), private setters, and `ArgumentException.ThrowIfNullOrWhiteSpace` for guards.
- Add **value objects** under `ValueObjects/` (e.g., `Email`, `CPF`, `Password`). Value objects are immutable; equality is by value, not reference.
- Add **domain events** under `Events/`. Events implement `IDomainEvent` and carry only primitive/value-object payloads.
- Add **error codes** as `static readonly Error` fields in a `{Feature}Errors.cs` class under `Constants/`.
- Add **repository/service contracts** (interfaces) under `Interfaces/`. No implementation here.
- Add validation message keys to `MessageResource/ValidationMessageResource.resx` and regenerate `ValidationMessageResource.Designer.cs`.

> The Domain layer must have **zero references** to any framework, AWS SDK, or Infrastructure assembly.

---

## 2. Application

Location: `02-src/02-Application/RentifyxIdentity.Application/Features/{Feature}/`

Create one subfolder per use-case operation:

```
Features/
  Identity/
    Register/
      RegisterUserRequest.cs          ← record with input fields
      RegisterUserValidator.cs        ← FluentValidation (injected via DI)
      RegisterUserHandler.cs          ← implements IHandler<RegisterUserRequest, UserResponse>
    Login/
      LoginRequest.cs
      LoginValidator.cs
      LoginHandler.cs
    ...
  UserResponse.cs                     ← shared response DTO for the feature
  UserMapper.cs                       ← static mapper (entity → response)
```

Handler pattern:

```csharp
public sealed class RegisterUserHandler(
    IUserRepository repository,
    IValidator<RegisterUserRequest> validator,
    ILogger<RegisterUserHandler> logger) : IHandler<RegisterUserRequest, UserResponse>
{
    public async Task<ErrorOr<UserResponse>> Handle(
        RegisterUserRequest request,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Registering user. Email={Email}", request.Email);

        List<Error>? errors = await validator.ValidateToErrorsAsync(request, cancellationToken);
        if (errors is not null)
            return errors;

        // domain logic ...

        return UserMapper.ToResponse(user);
    }
}
```

All handlers return `Task<ErrorOr<TResponse>>`. Never throw for expected domain failures — return `Error` values.

---

## 3. Infrastructure

Location: `02-src/05-Infrastructure/RentifyxIdentity.Infrastructure/`

- Add `{Feature}Repository.cs` implementing the domain interface (e.g., `IUserRepository`).
- For DynamoDB: follow the single-table PK/SK prefix convention (see ADR-005).
- For external services (SES, KMS, Cognito): wrap the AWS SDK client behind the domain interface.
- Catch all SDK exceptions at this boundary and convert them to `Error.Failure(...)`.

---

## 4. IoC

Location: `02-src/04-IoC/RentifyxIdentity.IoC/`

Register in the correct file:

| What | Where |
|---|---|
| Handler (`IHandler<TReq, TRes>`) | `ApplicationDependencyInjection.cs` |
| Validator (`IValidator<TReq>`) | `ApplicationDependencyInjection.cs` (auto-scanned via `AddValidatorsFromAssembly`) |
| Repository / AWS service | `InfrastructureDependencyInjection.cs` |

---

## 5. API

Location: `02-src/01-Api/RentifyxIdentity.Api/Endpoints/{Group}/`

Add one file per operation. All classes that implement `IEndpoint` are **auto-discovered by reflection** at startup — no manual wiring needed.

```csharp
internal sealed class Register : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/register", HandleAsync)
           .WithName("RegisterUser")
           .WithDescription("Register a new user account.")
           .WithTags(Tags.AUTH);
    }

    private static async Task<IResult> HandleAsync(
        RegisterUserRequest request,
        IHandler<RegisterUserRequest, UserResponse> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        ErrorOr<UserResponse> result = await handler.Handle(request, cancellationToken);

        return result.Match(
            user => Results.Created($"/v1/api/users/{user.Id}", user),
            errors => errors.ToProblem(httpContext));
    }
}
```

Add the new tag constant to `Endpoints/Tags.cs` if it doesn't exist.

All endpoints are automatically mounted under `/api/v1/` via `MapVersionedApi(1)` with rate limiting applied at the group level.

---

## 6. Tests

| Project | What to add |
|---|---|
| `Tests.Common/Builders/` | `{Feature}Builder.cs` using Bogus |
| `Tests.Validators/Features/{Feature}/` | `{Action}{Feature}ValidatorTests.cs` — test all valid/invalid combinations |
| `Tests.Handlers/Features/{Feature}/` | `{Action}{Feature}HandlerTests.cs` — mock `IRepository` / `IValidator` with Moq |
| `Tests.Repositories/Features/{Feature}/` | `{Feature}RepositoryTests.cs` — Testcontainers / LocalStack, real DynamoDB |
| `Tests.Integration/Api/{Group}/` | `{Feature}EndpointTests.cs` — `CustomWebApplicationFactory` + `HttpClient` |

Use **FluentAssertions** for assertions. Use **Bogus** builders from `Tests.Common` for test data. Use **Moq** for mocking in handler tests.

---

## 7. Running locally

```bash
# Boot Aspire (starts the API + LocalStack + cognito-local)
dotnet run --project 01-aspire/01-AppHost/RentifyxIdentity.AppHost

# Run all tests
dotnet test RentifyxIdentity.slnx

# Run only handler tests
dotnet test 03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers

# Build release (enforces TreatWarningsAsErrors)
dotnet build RentifyxIdentity.slnx --configuration Release
```

---

## Checklist before opening a PR

- [ ] Domain layer has zero framework/AWS references
- [ ] All handler paths (success + every failure) are unit-tested
- [ ] FluentValidation covers all invalid input combinations
- [ ] New DynamoDB access pattern documented in ADR-005 if a new GSI was added
- [ ] No secrets in code, logs, or error responses
- [ ] CI is green (gitleaks + build + tests)
- [ ] Coverage remains ≥ 80%
