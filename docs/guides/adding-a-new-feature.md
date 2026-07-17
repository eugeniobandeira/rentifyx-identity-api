# Adding a New Feature

This walks through the 7-step process from `CLAUDE.md`'s "Adding a new feature" convention,
using the `register-user` feature as a real, working reference — every path below exists in the
repo today.

Follow the steps in order. Each layer only depends on the ones before it (Domain has zero
outward dependencies; Application depends on Domain; Infrastructure depends on Application +
Domain; Api depends only on IoC).

## 1. Domain

Add the entity, value object, or domain event the feature needs, in
`02-src/03-Domain/RentifyxIdentity.Domain/`.

Reference: `Entities/UserEntity.cs`'s `Create(...)` factory (private setters, no public
constructor — see "Domain entity pattern" in `CLAUDE.md`), and the domain event
`Events/UserRegistered.cs`, which `RegisterUserHandler` raises after a successful registration.

If the feature needs a new error code, add it to `Domain/Constants/UserErrorCodes.cs`
(`{Entity}ErrorCodes` pattern) and a matching message key to
`Domain/MessageResource/ValidationMessageResource.resx`.

**Gotcha**: editing the `.resx` does NOT regenerate `ValidationMessageResource.Designer.cs`
automatically — `dotnet build` via the CLI does not invoke `ResXFileCodeGenerator` (that's a
Visual Studio single-file-generator convention, not an MSBuild target). Add the matching property
to `Designer.cs` by hand, in the same style as the existing entries (see L-008 in
`.specs/project/STATE.md`).

## 2. Contracts

If the feature needs a new repository or service dependency, define the interface in
`Domain/Interfaces/`. Reference: `Domain/Interfaces/Users/IUserRepository.cs` (extends the
generic `IRepository<T>` from `Domain/Interfaces/Common/IRepository.cs` and adds
feature-specific lookups like `GetByEmailAsync`/`GetByTaxIdAsync`).

Most features reuse an existing interface (`IUserRepository`, `IAuditLogService`, etc.) rather
than adding a new one — only add one when the feature genuinely needs a new kind of dependency.

## 3. Application

Create a feature folder under
`02-src/02-Application/RentifyxIdentity.Application/Features/{Area}/{Feature}/`. For
`register-user` this is `Features/Identity/Auth/Register/`:

| File | Purpose | Reference |
|---|---|---|
| `Request/{Action}Request.cs` | Request record | `Request/RegisterUserRequest.cs` |
| `Validator/{Action}Validator.cs` | FluentValidation rules | `Validator/RegisterUserValidator.cs` |
| `{Action}Handler.cs` | Implements `IHandler<TRequest, TResponse>`, returns `ErrorOr<T>` | `RegisterUserHandler.cs` |
| `{Feature}Response.cs` + `Mapper/{Feature}Mapper.cs` | Response DTO + entity→DTO mapping | `Features/Identity/UserResponse.cs` + `Features/Identity/Mapper/UserMapper.cs` |

Handler shape to follow:

```csharp
public sealed class RegisterUserHandler(
    IUserRepository repository,
    IValidator<RegisterUserRequest> validator,
    ILogger<RegisterUserHandler> logger) : IHandler<RegisterUserRequest, UserResponse>
{
    public async Task<ErrorOr<UserResponse>> HandleAsync(RegisterUserRequest request, CancellationToken ct = default)
    {
        List<Error>? errors = await validator.ValidateToErrorsAsync(request, ct);
        if (errors is not null)
            return errors;

        // business logic, then:
        return UserMapper.ToResponse(entity);
    }
}
```

A more recent example worth reading alongside this one: `Features/Identity/User/Consent/`
(`GetConsentHandler.cs`/`UpdateConsentHandler.cs`) shows the same pattern for a feature with two
handlers sharing one response type and mapper.

## 4. Infrastructure

Implement the repository/service in `02-src/05-Infrastructure/RentifyxIdentity.Infrastructure/`.
Reference: `Repositories/UserRepository.cs` (uses `IDynamoDBContext`, not the low-level
`IAmazonDynamoDB` API — see D-012 in `.specs/project/STATE.md`), backed by
`Models/UserDynamoDbItem.cs` and `Mapping/UserDynamoDbMapper.cs` for the entity↔DynamoDB-item
translation.

If the feature only adds fields to an existing entity (as the LGPD granular consent feature
did), you only need to extend the existing `{Entity}DynamoDbItem`/`{Entity}DynamoDbMapper` —
don't create new Infrastructure files for fields on an entity that already has a repository.

## 5. IoC

Register the new validator and handler explicitly in
`02-src/04-IoC/RentifyxIdentity.IoC/ApplicationDependencyInjection.cs`:

```csharp
services.AddScoped<IValidator<RegisterUserRequest>, RegisterUserValidator>();
// ...
services.AddScoped<IHandler<RegisterUserRequest, UserResponse>, RegisterUserHandler>();
```

This is explicit DI, not reflection (D-011) — every validator and handler needs its own line
here. Infrastructure services (repositories, `IEmailService`, `ITokenService`, etc.) are
registered the same way in `InfrastructureDependencyInjection.cs`.

**Endpoints are the one place reflection is still used** — see step 6.

## 6. API

Add an endpoint file implementing `IEndpoint` in
`02-src/01-Api/RentifyxIdentity.Api/Endpoints/{Group}/`. Reference:
`Endpoints/Auth/Register.cs`.

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
        CancellationToken ct = default)
    {
        var result = await handler.HandleAsync(request, ct);
        return result.Match(
            user => Results.Created($"/api/v1/users/{user.Id}", user),
            errors => errors.ToProblem(httpContext));
    }
}
```

No manual wiring needed — `EndpointExtensions` reflects over the assembly and auto-discovers
every `IEndpoint` implementation. All endpoints land under `/api/v1/` via `MapVersionedApi(1)`.
Authenticated endpoints extract the user ID from the JWT claim instead of taking it from the
request body — see `Endpoints/Users/GetProfile.cs` for the pattern
(`httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)`).

Once the endpoint exists, document its request/response shape in `docs/api-contracts.md`,
matching the format already used there for the other endpoints.

## 7. Tests

| Test type | Location | Pattern |
|---|---|---|
| Validator | `03-tests/02-Validators/RentifyxIdentity.Tests.Validators/Features/{Area}/` | No mocks — instantiate the validator directly, call `ValidateAsync`. Reference: `RegisterUserValidatorTests.cs` |
| Handler | `03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers/Features/{Area}/` | Mock `IUserRepository`/`IValidator<>`/etc. with Moq. Reference: `RegisterUserHandlerTests.cs` |
| Repository | `03-tests/04-Repositories/RentifyxIdentity.Tests.Repositories/Features/{Area}/` | Testcontainers + LocalStack, no mocks. Reference: `UserRepositoryTests.cs` — requires Docker |
| Integration/E2E | `03-tests/05-Integration/RentifyxIdentity.Tests.Integration/` | `CustomWebApplicationFactory` + `Microsoft.AspNetCore.Mvc.Testing`. Authenticated endpoints need the factory's `TestAuthHandler` to bypass real JWT validation |

Test data comes from Bogus builder classes in `03-tests/01-Common/RentifyxIdentity.Tests.Common/Builders/`
(e.g. `RegisterUserRequestBuilder.cs`) — don't hardcode request payloads inline when a builder
exists or is worth adding.

Run the relevant gate before committing:

```bash
# After a validator or handler change
dotnet test 03-tests/02-Validators/RentifyxIdentity.Tests.Validators
dotnet test 03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers

# Full gate, after an endpoint or integration change
dotnet test RentifyxIdentity.slnx
```

## Checklist

- [ ] Domain entity/VO/event added or extended, with unit tests
- [ ] New error codes + validation messages added (remember to hand-edit `Designer.cs`)
- [ ] Request/Validator/Handler/Response/Mapper added under `Application/Features/{Area}/{Feature}/`
- [ ] Repository/service implemented in Infrastructure (or existing one extended)
- [ ] Validator + handler registered in `ApplicationDependencyInjection` (or Infrastructure
      service in `InfrastructureDependencyInjection`)
- [ ] Endpoint added implementing `IEndpoint` — no manual registration needed
- [ ] `docs/api-contracts.md` updated with the new/changed endpoint
- [ ] Tests added at every layer touched; full `dotnet test RentifyxIdentity.slnx` green
- [ ] Any non-obvious decision logged in `.specs/project/STATE.md`
