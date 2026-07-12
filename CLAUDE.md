# rentifyx-identity-api

Identity microservice for the RentifyX platform. Built with .NET 10 Minimal APIs, Clean Architecture, DDD, and TDD.

## What we are building

A production-grade Identity API covering:
- User registration, email verification, login, token refresh, logout, password reset
- LGPD compliance (data access, erasure, export, consent, audit)
- AWS integration: Cognito, DynamoDB, SES, Secrets Manager, KMS
- DevSecOps: OWASP ZAP, gitleaks, Trivy, coverage gate ≥80%

The full 28-day plan is in `RentifyX_IdentityAPI_Plan.jsx`. For current progress, active
decisions, and deferred work, `.specs/project/STATE.md` and `.specs/project/ROADMAP.md` are the
living source of truth — this file describes conventions and should be updated when they change,
but check STATE.md/ROADMAP.md before assuming something here reflects the current state.

## Tech stack

- **Framework**: .NET 10, Minimal APIs, C# latest
- **Architecture**: Clean Architecture · DDD · TDD
- **Cloud**: AWS Cognito · DynamoDB · SES · Secrets Manager · KMS (real AWS resources, provisioned via Terraform, for local dev too — no LocalStack; see `.specs/project/STATE.md` D-019)
- **Infra**: .NET Aspire (AppHost + ServiceDefaults) · Docker · Terraform · GitHub Actions
- **Observability**: OpenTelemetry · Serilog · Scalar/ReDoc
- **Compliance**: LGPD · OWASP Top 10 · BACEN

## Solution structure

```
01-aspire/
  01-AppHost/         – .NET Aspire orchestration (starts API + future LocalStack)
  02-ServiceDefaults/ – OTel traces/metrics, health checks, service discovery
02-src/
  01-Api/             – Minimal API endpoints, middlewares, extensions
  02-Application/     – Use cases via IHandler<TRequest,TResponse>, FluentValidation validators
  03-Domain/          – Entities, value objects, domain events, repository contracts (no framework deps)
  04-IoC/             – DI wiring (ApplicationDependencyInjection, InfrastructureDependencyInjection)
  05-Infrastructure/  – Repository implementations, AWS SDK adapters
03-tests/
  01-Common/          – Shared builders (Bogus)
  02-Validators/      – FluentValidation unit tests
  03-Handlers/        – Handler unit tests (Moq)
  04-Repositories/    – Repository integration tests (Testcontainers)
  05-Integration/     – End-to-end via CustomWebApplicationFactory
docs/
  architecture/       – Architecture overview (C4 context/container/component diagrams)
  decisions/          – ADRs (000-template + ADR-001 to 008, all written)
  guides/             – adding-a-new-feature.md
iac/                  – Terraform (DynamoDB, Cognito, SES, KMS, Secrets Manager, IAM modules — implemented in E-06)
k8s/                  – Kustomize base + dev/prod overlays (deployment, HPA, SecretProviderClass — implemented in E-06)
```

## Key conventions

### Adding a new feature (follow this order every time)

1. **Domain** – entity / value object / domain event in `02-src/03-Domain/`
2. **Contracts** – repository/service interface in `Domain/Interfaces/`
3. **Application** – feature folder under `Application/Features/{Feature}/`
   - `{Action}Request.cs` → request record
   - `{Action}Validator.cs` → FluentValidation validator
   - `{Action}Handler.cs` → implements `IHandler<TRequest, TResponse>` (returns `ErrorOr<T>`)
   - `{Feature}Response.cs` + `{Feature}Mapper.cs`
4. **Infrastructure** – implement repository/service in `Infrastructure/`
5. **IoC** – register in `ApplicationDependencyInjection` or `InfrastructureDependencyInjection`
6. **API** – add endpoint file implementing `IEndpoint` in `Api/Endpoints/{Group}/`
   - No manual wiring needed for endpoints: reflection auto-discovers all `IEndpoint` implementations
   - Validators and handlers, however, are registered explicitly in `ApplicationDependencyInjection`
     (one `AddScoped<IValidator<T>, ...>` / `AddScoped<IHandler<...>>` line per feature) — reflection
     was replaced here for explicitness (D-011 in `.specs/project/STATE.md`)
   - All endpoints land under `/api/v1/` via `MapVersionedApi(1)`
7. **Tests** – unit tests in `03-Handlers/` and `02-Validators/`; integration tests in `05-Integration/`

### Result type

All handlers return `ErrorOr<T>`. Map to HTTP with `result.Match(success => ..., errors => errors.ToProblem(httpContext))`.

### Endpoint pattern

```csharp
internal sealed class MyAction : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/resource", HandleAsync)
           .WithName("...").WithDescription("...").WithTags(Tags.XXX);
    }

    private static async Task<IResult> HandleAsync(
        MyRequest request,
        IHandler<MyRequest, MyResponse> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.Handle(request, cancellationToken);
        return result.Match(r => Results.Ok(r), e => e.ToProblem(httpContext));
    }
}
```

### Domain entity pattern

Static factory `Create(...)`, private setters, no public constructor. Use `ArgumentException.ThrowIfNullOrWhiteSpace` for guards.

### Build rules (enforced globally via Directory.Build.props)

- `Nullable=enable`, `TreatWarningsAsErrors=true`, `LangVersion=latest`
- SonarAnalyzer.CSharp wired on every project
- NuGet versions centralized in `Directory.Packages.props`
- Git hooks path set to `.githooks/` (pre-commit runs gitleaks)

## Running locally

```bash
# Start API via Aspire (Dashboard + Scalar UI)
dotnet run --project 01-aspire/01-AppHost/RentifyxIdentity.AppHost

# Run all tests
dotnet test RentifyxIdentity.slnx

# Build release
dotnet build RentifyxIdentity.slnx --configuration Release
```

## CI/CD

GitHub Actions (`ci.yml`) triggers on PRs to `main`:
1. **Secret Scanning** – gitleaks with `.gitleaks.toml` (blocks if secrets found)
2. **Build & Test** – restore → build Release → test
3. **Coverage gate** – ReportGenerator merges `coverlet.collector` output; PR fails if line coverage < 80% (currently ~95.6%)
4. **OWASP dependency-check** – `dependency-check/Dependency-Check_Action`, fails on CVSS ≥ 7 (suppressions in `.owasp-suppressions.xml`)
5. **Trivy container scan** – `aquasecurity/trivy-action` on the built image, blocks on CRITICAL/HIGH

## Security rules

- **Never** hardcode secrets. All sensitive config comes from AWS Secrets Manager.
- No stack traces in error responses (`GlobalExceptionHandler` strips them).
- Rate limiting is configured at the `v1` route group level.
- Refresh tokens stored as hash (not plaintext), with DynamoDB TTL.
- **Known gap**: TaxId (CPF/CNPJ) is currently stored as **plaintext** in DynamoDB (D-010 in
  `.specs/project/STATE.md`). KMS encryption + HMAC blind index for lookup is deferred
  (DEF-007) — do not assume it's already encrypted when working in this area.

## Test structure conventions

- **Validators** (`03-tests/02-Validators/`) – test all valid/invalid combinations, no mocks needed
- **Handlers** (`03-tests/03-Handlers/`) – mock `IRepository` / `IValidator` with Moq; use `ExampleBuilder` from `Tests.Common` as pattern
- **Repositories** (`03-tests/04-Repositories/`) – Testcontainers (LocalStack/DynamoDB); no mocks
- **Integration** (`03-tests/05-Integration/`) – `CustomWebApplicationFactory` + `Microsoft.AspNetCore.Mvc.Testing`
- Test data: Bogus via builder classes in `Tests.Common/Builders/`
- Assertions: FluentAssertions
