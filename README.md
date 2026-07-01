# RentifyX Identity API

[![CI](https://github.com/eugeniobandeira/rentifyx-identity-api/actions/workflows/ci.yml/badge.svg)](https://github.com/eugeniobandeira/rentifyx-identity-api/actions/workflows/ci.yml)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![LinkedIn](https://img.shields.io/badge/LinkedIn-eugeniobandeira-0077B5?logo=linkedin)](https://linkedin.com/in/eugeniobandeira)

Production-grade Identity microservice for the RentifyX platform. Built with .NET 10 Minimal APIs, Clean Architecture, DDD, and TDD.

Covers user registration, email verification, login, token refresh, logout, password reset, and LGPD compliance (data access, erasure, export, consent, audit).

## Tech Stack

| Concern | Library / Technology |
|---|---|
| Framework | ASP.NET Core 10 Minimal APIs |
| Architecture | Clean Architecture · DDD · TDD |
| Error Handling | ErrorOr 2.0.1 |
| Validation | FluentValidation 12.1.1 |
| Logging | Serilog 10.0.0 |
| API Versioning | Asp.Versioning.Http 8.1.0 |
| API Documentation | Scalar + Microsoft.AspNetCore.OpenApi |
| Orchestration | .NET Aspire 9.3.1 |
| Observability | OpenTelemetry (traces, metrics, logs) |
| Auth | JWT RS256 · AWS Cognito |
| Database | AWS DynamoDB (single-table design) |
| Email | AWS SES v2 |
| Secrets | AWS Secrets Manager |
| Encryption | AWS KMS |
| Local Cloud | LocalStack via Testcontainers |
| Testing | xUnit · Moq · FluentAssertions · Bogus · Testcontainers |
| Code Analysis | SonarAnalyzer.CSharp |
| Security | OWASP Top 10 · gitleaks · Trivy |
| Compliance | LGPD · BACEN |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://www.docker.com/) (required for LocalStack and repository integration tests)
- .NET Aspire workload:

```bash
dotnet workload install aspire
```

## Running Locally

Start the API with Aspire orchestration (recommended — includes dashboard and Scalar UI):

```bash
dotnet run --project 01-aspire/01-AppHost/RentifyxIdentity.AppHost
```

Or directly without Aspire:

```bash
dotnet run --project 02-src/01-Api/RentifyxIdentity.Api
```

## Running Tests

```bash
# All tests
dotnet test RentifyxIdentity.slnx

# Skip Docker-dependent tests
dotnet test RentifyxIdentity.slnx --filter "Category!=RequiresDocker"
```

## Running with Docker

```bash
docker build -t rentifyx-identity .
docker run -p 8080:8080 -e ASPNETCORE_ENVIRONMENT=Production rentifyx-identity
```

## Running on Kubernetes

```bash
kubectl apply -k k8s/overlays/dev
kubectl apply -k k8s/overlays/prod
```

## Project Structure

```
01-aspire/
  01-AppHost/             – .NET Aspire orchestration (starts API + LocalStack)
  02-ServiceDefaults/     – OTel traces/metrics, health checks, service discovery
02-src/
  01-Api/                 – Minimal API endpoints, middlewares, extensions
  02-Application/         – Use cases via IHandler<TRequest,TResponse>, FluentValidation validators
  03-Domain/              – Entities, value objects, domain events, repository contracts
  04-IoC/                 – DI wiring (ApplicationDependencyInjection, InfrastructureDependencyInjection)
  05-Infrastructure/      – Repository implementations, AWS SDK adapters
03-tests/
  01-Common/              – Shared builders (Bogus)
  02-Validators/          – FluentValidation unit tests
  03-Handlers/            – Handler unit tests (Moq)
  04-Repositories/        – Repository integration tests (Testcontainers + LocalStack)
  05-Integration/         – End-to-end via CustomWebApplicationFactory
docs/
  architecture/           – Architecture overview and C4 diagrams
  decisions/              – ADRs (ADR-001 to ADR-008)
  guides/                 – adding-a-new-feature.md
iac/                      – Terraform modules
k8s/                      – Kustomize base + dev/prod overlays
```

## Architecture

### Layer responsibilities

| Layer | Responsibility | Allowed dependencies |
|---|---|---|
| Domain | Entities, value objects, domain events, repository interfaces | None |
| Application | Handlers, validators, DTOs, mappers | Domain |
| Infrastructure | Repository implementations, AWS SDK adapters | Domain |
| IoC | DI registration | All layers |
| Api | Endpoints, middlewares, HTTP mapping | Application, Domain |

### Dependency flow

```
Api → Application → Domain ← Infrastructure
                       ↑
              IoC (wires all layers)
```

- **Domain** has zero outbound dependencies and zero AWS/framework references.
- **Infrastructure** implements Domain interfaces. It never references Application.
- **Application** depends only on Domain interfaces, never on Infrastructure directly.
- **IoC** is the only layer that references all others — it is the composition root.

### Handler pattern

Every use case implements `IHandler<TRequest, TResponse>`, returning `ErrorOr<T>` instead of throwing exceptions:

```csharp
public interface IHandler<TRequest, TResponse>
{
    Task<ErrorOr<TResponse>> Handle(TRequest request, CancellationToken cancellationToken = default);
}
```

Handlers are registered automatically via reflection — no manual wiring needed when adding new features.

### DynamoDB single-table design

All user data is stored in a single DynamoDB table using the following key schema:

| Key | Pattern | Purpose |
|---|---|---|
| PK (hash) | `USER#{id}` | Primary access by user ID |
| SK (range) | `USER#{id}` | Composite key (equals PK for user items) |
| GSI_Email | `EMAIL#{email}` | Lookup by email address |
| GSI_TaxId | `TAXID#{taxId}` | Lookup by CPF/CNPJ |

Unverified accounts have a DynamoDB TTL set to 48h — they are automatically deleted if the user never verifies their email.

### Endpoint pattern

```csharp
internal sealed class MyAction : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/resource", HandleAsync)
           .WithName("...").WithTags(Tags.XXX);
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

All endpoints are discovered and registered automatically via reflection — no manual wiring needed.

## Adding a New Feature

Follow this order every time:

**1. Domain** — entity / value object / domain event in `02-src/03-Domain/`

**2. Contracts** — repository/service interface in `Domain/Interfaces/`

**3. Application** — feature folder under `Application/Features/{Feature}/`:
- `{Action}Request.cs`
- `{Action}Validator.cs` (FluentValidation, messages via `ValidationMessageResource`)
- `{Action}Handler.cs` (implements `IHandler<TRequest, TResponse>`, returns `ErrorOr<T>`)
- `{Feature}Response.cs` + `{Feature}Mapper.cs`

**4. Infrastructure** — implement repository/service in `Infrastructure/`

**5. IoC** — no changes needed. Handlers and repositories are auto-discovered via reflection.

**6. Api** — add one file per endpoint implementing `IEndpoint` in `Api/Endpoints/{Group}/`

**7. Tests** — unit tests in `03-Handlers/` and `02-Validators/`; integration tests in `05-Integration/`

## Error Handling

Business logic never throws — it returns `ErrorOr<T>`. Endpoints map the result to HTTP responses:

```csharp
var result = await handler.Handle(request, cancellationToken);
return result.Match(r => Results.Ok(r), e => e.ToProblem(httpContext));
```

| ErrorOr type | HTTP status |
|---|---|
| `Error.Validation` | 422 Unprocessable Entity |
| `Error.NotFound` | 404 Not Found |
| `Error.Conflict` | 409 Conflict |
| `Error.Unauthorized` | 401 Unauthorized |
| Other | 500 Internal Server Error |

## Middlewares

### CorrelationIdMiddleware

Reads `X-Correlation-Id` from the request header (generates a new `Guid` if absent), sanitizes it, pushes it to Serilog's `LogContext`, and echoes it in the response header.

### GlobalExceptionHandler

Catches all unhandled exceptions and returns a structured RFC 7807 `ProblemDetails` response — no stack traces exposed. `OperationCanceledException` from client disconnection returns HTTP 499.

### Rate Limiting

Fixed window policy applied globally to all versioned endpoints. Configurable via `appsettings.json`:

```json
"RateLimit": {
  "PermitLimit": 100,
  "WindowSeconds": 60,
  "QueueLimit": 0
}
```

## Health Endpoints

| Route | Purpose |
|---|---|
| `GET /health` | All registered health checks |
| `GET /alive` | Liveness probe |
| `GET /api/v1/health` | Application-level health check |

## Observability

OpenTelemetry is pre-configured for traces, metrics, and logs via .NET Aspire ServiceDefaults.

| Variable | Description |
|---|---|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Collector URL (empty = export disabled) |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | `http/protobuf` or `grpc` |
| `OTEL_SERVICE_NAME` | Service name in traces/metrics |

## Security

- Secrets are never hardcoded — all sensitive config comes from AWS Secrets Manager.
- CPF is encrypted at rest via KMS before storing in DynamoDB.
- Refresh tokens are stored as HMAC-SHA256 hashes (not plaintext), with DynamoDB TTL.
- Rate limiting is applied at the route group level (OWASP A04).
- gitleaks runs as a pre-commit hook to prevent secret leaks.

## Centralized Package Management

All NuGet versions are declared in `Directory.Packages.props`. Individual `.csproj` files reference packages without specifying versions.

`Directory.Build.props` enforces `Nullable=enable`, `TreatWarningsAsErrors=true`, `LangVersion=latest`, and SonarAnalyzer.CSharp across every project.

## License

MIT © eugeniobandeira
