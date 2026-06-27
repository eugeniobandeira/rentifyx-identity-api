# Testing Infrastructure

## Test Frameworks

- **Unit / Integration:** xUnit 2.9.3
- **Assertions:** FluentAssertions 8.2.0
- **Mocking:** Moq 4.20.72
- **Test data:** Bogus 35.6.1 (fluent builder pattern)
- **E2E / HTTP:** Microsoft.AspNetCore.Mvc.Testing 10.0.8
- **Repository tests:** Testcontainers + LocalStack (planned E-04 — not yet wired)
- **Coverage:** coverlet.collector 6.0.4
- **Test SDK:** Microsoft.NET.Test.Sdk 17.12.0

## Test Organization

**Location:** `03-tests/` — 5 sub-projects, one per test type

**Naming:**
- Test class: `{ClassUnderTest}Tests` (sealed) — e.g., `RegisterUserHandlerTests`
- Test method: `{Action}_{Condition}_{Expected}` — e.g., `Register_DuplicateEmail_ReturnsConflict`

**Structure:** Mirror the `Features/{Feature}/` layout from Application, e.g.:
- `Tests.Validators/Features/Identity/RegisterUserValidatorTests.cs`
- `Tests.Handlers/Features/Identity/RegisterUserHandlerTests.cs`

**Analyzer suppressions** (in `03-tests/Directory.Build.props`):
- `CA1707` — underscore method names allowed in tests
- `CA1859` — typed as interfaces OK (Moq substitutability)
- `CA1305` — culture-aware string formatting not enforced in tests
- `CA1001` — false positive with `IAsyncLifetime`

## Testing Patterns

### Validator Tests (`03-tests/02-Validators/`)

No mocks needed — instantiate validator directly and call `ValidateAsync`.

```csharp
public sealed class RegisterUserValidatorTests
{
    private readonly RegisterUserValidator _sut = new();

    [Fact]
    public async Task Register_ValidRequest_ShouldPassValidation()
    {
        var request = new RegisterUserRequest("user@example.com", "529.982.247-25", "P@ssword123!", "Owner");
        var result = await _sut.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("notanemail")]
    public async Task Register_InvalidEmail_ShouldFailWithEmailError(string email)
    {
        var request = new RegisterUserRequest(email, "529.982.247-25", "P@ssword123!", "Owner");
        var result = await _sut.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }
}
```

### Handler Tests (`03-tests/03-Handlers/`)

Mock `IRepository`, `ITokenService`, `IEmailService`, `IValidator<>` with Moq. Use `UserBuilder` for entity test data.

```csharp
public sealed class RegisterUserHandlerTests
{
    private readonly Mock<IUserRepository> _repository = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IValidator<RegisterUserRequest>> _validator = new();
    private readonly RegisterUserHandler _sut;

    public RegisterUserHandlerTests()
    {
        _validator.Setup(v => v.ValidateToErrorsAsync(It.IsAny<RegisterUserRequest>(), default))
                  .ReturnsAsync((List<Error>?)null);
        _sut = new RegisterUserHandler(_repository.Object, _emailService.Object,
                                        _validator.Object, NullLogger<RegisterUserHandler>.Instance);
    }

    [Fact]
    public async Task Register_ValidRequest_ShouldReturnUserResponse()
    {
        _repository.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), default)).ReturnsAsync((UserEntity?)null);
        _repository.Setup(r => r.GetByTaxIdAsync(It.IsAny<string>(), default)).ReturnsAsync((UserEntity?)null);

        var result = await _sut.Handle(new RegisterUserRequest(...), default);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeOfType<UserResponse>();
        _repository.Verify(r => r.AddAsync(It.IsAny<UserEntity>(), default), Times.Once);
    }
}
```

### Repository Tests (`03-tests/04-Repositories/`)

Testcontainers + LocalStack (DynamoDB). **No mocks.** E-04 wiring.

```csharp
// Pattern (once wired):
public sealed class UserRepositoryTests : IAsyncLifetime
{
    // Testcontainers LocalStack setup
    // Real DynamoDB table creation
    // Tests against real DynamoDB
}
```

### Integration / E2E Tests (`03-tests/05-Integration/`)

`CustomWebApplicationFactory` with `Testing` environment. Stub services replace Infrastructure registrations.

```csharp
public sealed class AuthEndpointTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Register_ValidRequest_Returns201()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new { ... });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
```

**Note:** Authenticated endpoint tests require a `TestAuthHandler` in the factory to bypass JWT validation until E-04 wires Cognito.

## Test Execution

```bash
# Run all tests
dotnet test RentifyxIdentity.slnx

# Run specific test project
dotnet test 03-tests/02-Validators/RentifyxIdentity.Tests.Validators/

# Run with coverage
dotnet test RentifyxIdentity.slnx --collect:"XPlat Code Coverage"

# Run with filter
dotnet test --filter "FullyQualifiedName~Identity"
```

## Coverage Targets

- **Goal:** ≥ 80% enforced in CI
- **Tool:** coverlet.collector
- **Enforcement:** GitHub Actions gate — PR blocked if coverage drops below threshold (to be wired in E-01 T-018/019/020)

## Test Coverage Matrix

| Code Layer | Required Test Type | Location Pattern | Run Command |
|---|---|---|---|
| Validators | Unit (no mocks) | `Tests.Validators/Features/{Feature}/` | `dotnet test Tests.Validators` |
| Handlers | Unit (Moq) | `Tests.Handlers/Features/{Feature}/` | `dotnet test Tests.Handlers` |
| Domain entities / VOs | Unit (no mocks) | `Tests.Handlers/` or `Tests.Common/` | `dotnet test Tests.Handlers` |
| Repositories | Integration (Testcontainers) | `Tests.Repositories/Features/{Feature}/` | `dotnet test Tests.Repositories` |
| API Endpoints | E2E (WebApplicationFactory) | `Tests.Integration/Api/{Feature}/` | `dotnet test Tests.Integration` |
| Middleware | Unit or E2E | `Tests.Integration/` | `dotnet test Tests.Integration` |
| IoC / DI registration | Integration | `Tests.Integration/` | `dotnet test Tests.Integration` |

## Parallelism Assessment

| Test Type | Parallel-Safe? | Isolation Model | Evidence |
|---|---|---|---|
| Validator tests | Yes | No shared state; pure function calls | No DB, no mocks, stateless validators |
| Handler tests | Yes | All deps mocked per test class constructor | `Mock<>` instances scoped to class |
| Repository tests | No (per suite) | Testcontainers: new container per collection | `IAsyncLifetime` lifecycle management |
| Integration tests | No | Shared `WebApplicationFactory` instance | `IClassFixture<CustomWebApplicationFactory>` |

## Gate Check Commands

| Gate Level | When to Use | Command |
|---|---|---|
| Quick | After handler or validator task | `dotnet test 03-tests/02-Validators && dotnet test 03-tests/03-Handlers` |
| Full | After API endpoint or integration task | `dotnet test RentifyxIdentity.slnx` |
| Build | After phase completion (pre-PR) | `dotnet build RentifyxIdentity.slnx -c Release && dotnet test RentifyxIdentity.slnx` |
