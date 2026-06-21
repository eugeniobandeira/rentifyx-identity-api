# Clean Architecture Template

[![CI](https://github.com/eugeniobandeira/clean-arch-template/actions/workflows/ci.yml/badge.svg)](https://github.com/eugeniobandeira/clean-arch-template/actions/workflows/ci.yml)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
[![NuGet Downloads](https://img.shields.io/nuget/dt/EugenioBandeira.RentifyxIdentityTemplate?label=downloads)](https://www.nuget.org/packages/EugenioBandeira.RentifyxIdentityTemplate)
[![NuGet Version](https://img.shields.io/nuget/v/EugenioBandeira.RentifyxIdentityTemplate)](https://www.nuget.org/packages/EugenioBandeira.RentifyxIdentityTemplate)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![LinkedIn](https://img.shields.io/badge/LinkedIn-eugeniobandeira-0077B5?logo=linkedin)](https://linkedin.com/in/eugeniobandeira)

A .NET 10 project template for building production-ready Web APIs using Clean Architecture.

## Tech Stack

| Concern | Library / Technology |
|---|---|
| Framework | ASP.NET Core 10 Minimal APIs |
| Error Handling | ErrorOr 2.0.1 |
| Validation | FluentValidation 12.1.1 |
| Logging | Serilog 10.0.0 |
| API Versioning | Asp.Versioning.Http 8.1.0 |
| API Documentation | Scalar + Microsoft.AspNetCore.OpenApi |
| Orchestration | .NET Aspire 9.3.1 |
| Observability | OpenTelemetry (traces, metrics, logs) |
| Testing | xUnit, Moq, FluentAssertions, Bogus |
| Code Analysis | SonarAnalyzer.CSharp |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- .NET Aspire workload:

```bash
dotnet workload install aspire
```

## Quick Start

```bash
dotnet new install EugenioBandeira.RentifyxIdentityTemplate
dotnet new clean-arch -n MyProject
```

## Project Structure

```
MyProject/
├── 01-aspire/
│   ├── 01-AppHost/
│   │   └── MyProject.AppHost/              # .NET Aspire orchestration
│   └── 02-ServiceDefaults/
│       └── MyProject.ServiceDefaults/      # OpenTelemetry, health checks, service discovery
├── 02-src/
│   ├── 01-Api/
│   │   └── MyProject.Api/                  # Endpoints, middlewares, extensions
│   ├── 02-Application/
│   │   └── MyProject.Application/          # Handlers, validators, DTOs, mappers
│   ├── 03-Domain/
│   │   └── MyProject.Domain/               # Entities, repository interfaces, constants
│   ├── 04-IoC/
│   │   └── MyProject.IoC/                  # Dependency injection wiring
│   └── 05-Infrastructure/
│       └── MyProject.Infrastructure/       # Repository implementations
├── 03-tests/
│   ├── 01-Common/                          # Shared builders (Bogus)
│   ├── 02-Validators/                      # FluentValidation unit tests
│   ├── 03-Handlers/                        # Handler unit tests
│   ├── 04-Repositories/                    # Repository tests
│   └── 05-Integration/                     # API integration tests (WebApplicationFactory)
├── docs/                                   # Architecture docs, ADRs, feature specs
├── iac/                                    # Infrastructure as Code (Terraform, Bicep, etc.)
├── k8s/                                    # Kubernetes manifests (Kustomize)
│   ├── base/
│   └── overlays/
│       ├── dev/
│       └── prod/
├── Directory.Build.props                   # Shared build settings for all projects
├── Directory.Packages.props                # Centralized NuGet package versions
├── Dockerfile
└── RentifyxIdentity.slnx
```

## Architecture

### Layer responsibilities

| Layer | Responsibility | Allowed dependencies |
|---|---|---|
| Domain | Entities, repository interfaces, error codes | None |
| Application | Handlers, validators, DTOs, mappers | Domain |
| Infrastructure | Repository implementations | Domain |
| IoC | DI registration | All layers |
| Api | Endpoints, middlewares, HTTP mapping | Application, Domain |

### Dependency flow

```
Api → Application → Domain ← Infrastructure
                       ↑
              IoC (wires all layers)
```

- **Domain** has no outbound dependencies — it defines interfaces, not implementations.
- **Infrastructure** implements Domain interfaces. It never references Application.
- **Application** depends only on Domain interfaces, never on Infrastructure directly.
- **IoC** is the only layer that references all others — it is the composition root.
- **Api** depends on Application (handlers) and IoC.

### Handler pattern

Every use case implements `IHandler<TRequest, TResponse>`, returning `ErrorOr<T>` instead of throwing exceptions:

```csharp
public interface IHandler<TRequest, TResponse>
{
    Task<ErrorOr<TResponse>> Handle(TRequest request, CancellationToken cancellationToken = default);
}
```

Handlers are registered automatically via reflection scan in IoC — no manual wiring needed when adding new features:

```csharp
assembly.GetTypes()
    .Where(t => !t.IsAbstract && !t.IsInterface)
    .SelectMany(t => t.GetInterfaces()
        .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHandler<,>))
        .Select(i => (Implementation: t, Interface: i)))
    .ToList()
    .ForEach(x => services.AddScoped(x.Interface, x.Implementation));
```

### Repository interfaces

Two interfaces cover all repository needs:

```csharp
// Basic CRUD — used by most handlers
IRepository<TEntity>

// Extends with pagination — used by GetAll handlers
IRepository<TEntity, TFilter> : IRepository<TEntity>
```

A concrete repository implements the extended interface, satisfying both:

```csharp
public sealed class ExampleRepository : IRepository<ExampleEntity, GetAllExampleRequest>
{ ... }
```

IoC registration is automatic — any class in the Infrastructure assembly that implements `IRepository<T>` or `IRepository<T, TFilter>` is discovered and registered via reflection. No manual wiring needed.

### Feature organization

Features are organized by name inside `Application/Features/{Feature}/`:

```
Application/Features/Example/
├── ExampleResponse.cs
├── Mapper/ExampleMapper.cs
└── Handlers/
    ├── Create/
    │   ├── CreateExampleHandler.cs
    │   ├── Request/CreateExampleRequest.cs
    │   └── Validator/CreateExampleValidator.cs
    ├── GetById/GetByIdExampleHandler.cs
    ├── GetAll/
    │   ├── GetAllExampleHandler.cs
    │   └── Request/GetAllExampleRequest.cs
    ├── Update/
    │   ├── UpdateExampleHandler.cs
    │   ├── Request/UpdateExampleRequest.cs
    │   └── Validator/UpdateExampleValidator.cs
    └── Delete/DeleteExampleHandler.cs
```

Endpoints follow the same convention in the Api layer:

```
Api/Endpoints/Example/
├── Create.cs
├── GetById.cs
├── GetAll.cs
├── Update.cs
└── Delete.cs
```

### Endpoints

Each endpoint is a single file implementing `IEndpoint` and is auto-registered via reflection — no manual wiring:

```csharp
internal sealed class Create : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/examples", HandleAsync)
           .WithName("CreateExample")
           .WithTags(Tags.EXAMPLE);
    }
}
```

All endpoints are mounted under `/api/v1` with rate limiting applied automatically.

## Centralized Package Management

All NuGet package versions are declared once in `Directory.Packages.props` at the solution root. Individual `.csproj` files reference packages **without specifying versions** — versions are resolved centrally.

```xml
<!-- Directory.Packages.props -->
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup Label="Application">
    <PackageVersion Include="ErrorOr" Version="2.0.1" />
    <PackageVersion Include="FluentValidation" Version="12.1.1" />
  </ItemGroup>

  <ItemGroup Label="Api">
    <PackageVersion Include="Scalar.AspNetCore" Version="2.14.14" />
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.0.8" />
  </ItemGroup>

  <ItemGroup Label="Tests">
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="Moq" Version="4.20.72" />
    <PackageVersion Include="FluentAssertions" Version="8.2.0" />
  </ItemGroup>
  <!-- ... -->
</Project>
```

**Benefits:**
- No version conflicts between projects — a single source of truth.
- To upgrade a package, edit one line in `Directory.Packages.props`.
- PRs show version changes in one file, making upgrades easy to review.

### Shared build settings

`Directory.Build.props` at the solution root applies common MSBuild properties to every project automatically:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisMode>Recommended</AnalysisMode>
    <LangVersion>latest</LangVersion>
    <NuGetAuditMode>direct</NuGetAuditMode>
  </PropertyGroup>
</Project>
```

`03-tests/Directory.Build.props` extends the root file and suppresses analyzer rules that conflict with test conventions (underscore naming, interface-typed fields, etc.) — without touching production project settings.

## Middlewares

### CorrelationIdMiddleware

Tracks every request end-to-end across logs, responses, and error payloads.

- Reads `X-Correlation-Id` from the request header; generates a new `Guid` if absent.
- Sanitizes the value (alphanumeric + dashes, max 64 chars) to prevent header injection.
- Stores the value in `HttpContext.Items` and echoes it in the `X-Correlation-Id` response header.
- Pushes it to Serilog's `LogContext` — every log line in that request automatically includes `{CorrelationId}`.

### GlobalExceptionHandler

Catches all unhandled exceptions and returns a structured `ProblemDetails` response (RFC 7807):

```json
{
  "status": 500,
  "title": "An unexpected error occurred.",
  "instance": "/api/v1/examples",
  "extensions": {
    "correlationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
    "exceptionType": "System.InvalidOperationException",
    "exceptionMessage": "Sequence contains no elements."
  }
}
```

`OperationCanceledException` triggered by client disconnection returns HTTP 499 and is logged as a warning, not an error.

### Rate Limiting

Fixed window policy applied globally to all versioned endpoints. Configurable via `appsettings.json`:

```json
"RateLimit": {
  "PermitLimit": 100,
  "WindowSeconds": 60,
  "QueueLimit": 0
}
```

### CORS

Configured via `appsettings.json`. Update the allowed origins before going to production:

```json
"Cors": {
  "AllowedOrigins": [ "https://your-frontend.com" ]
}
```

## Health Endpoints

| Route | Purpose |
|---|---|
| `GET /health` | All registered health checks |
| `GET /alive` | Liveness probe (checks tagged `live`) |
| `GET /api/v1/health` | Application-level health check (versioned, documented in Swagger) |

## Error Handling

Business logic never throws — it returns `ErrorOr<T>`. Endpoints map the result to HTTP responses:

```csharp
ErrorOr<ExampleEntity> result = await handler.Handle(request, cancellationToken);

return result.Match(
    entity => Results.Ok(entity.ToResponse()),
    errors => errors.ToProblem(httpContext));
```

Error types are mapped to HTTP status codes automatically:

| ErrorOr type | HTTP status |
|---|---|
| `Error.Validation` | 422 Unprocessable Entity |
| `Error.NotFound` | 404 Not Found |
| `Error.Conflict` | 409 Conflict |
| `Error.Unauthorized` | 401 Unauthorized |
| Other | 500 Internal Server Error |

## Observability

The template ships with OpenTelemetry pre-configured for traces, metrics, and logs via `.NET Aspire ServiceDefaults`.

Set the following environment variables to enable export to any OTLP-compatible collector:

| Variable | Description | Default |
|---|---|---|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Collector URL | _(empty — export disabled)_ |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | `http/protobuf` or `grpc` | `http/protobuf` |
| `OTEL_EXPORTER_OTLP_HEADERS` | Auth headers (e.g. API key) | _(empty)_ |
| `OTEL_SERVICE_NAME` | Service name in traces/metrics | `RentifyxIdentity.Api` |
| `OTEL_RESOURCE_ATTRIBUTES` | Additional resource metadata | `deployment.environment=production` |

Compatible platforms: Grafana Cloud, Datadog, New Relic, Honeycomb, Elastic, Jaeger, OpenTelemetry Collector.

### Serilog sinks (logs only)

If you prefer sending logs via a Serilog sink instead of OTLP:

```bash
dotnet add package Serilog.Sinks.Seq
dotnet add package Serilog.Sinks.Datadog.Logs
dotnet add package Serilog.Sinks.Elasticsearch
```

Configure in `appsettings.json` under `Serilog.WriteTo`.

## Post-Generation Setup

After running `dotnet new clean-arch -n MyProject`, complete the following steps:

### 1. Replace the connection string

In `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "your-real-connection-string"
}
```

### 2. Implement the repository

Open `Infrastructure/Repositories/ExampleRepository.cs` and implement the methods using your chosen persistence technology (EF Core, Dapper, MongoDB, etc.):

```csharp
public sealed class ExampleRepository : IRepository<ExampleEntity, GetAllExampleRequest>
{
    // Your implementation here
}
```

### 3. Update CORS origins

In `appsettings.json`, replace the placeholder with your frontend URL:

```json
"Cors": {
  "AllowedOrigins": [ "https://your-frontend.com" ]
}
```

### 4. Update OpenAPI contact info

In `appsettings.json`:

```json
"OpenApi": {
  "ContactName": "your-name",
  "ContactUrl": "https://github.com/your-handle"
}
```

### 5. Configure observability (optional)

Set `OTEL_EXPORTER_OTLP_ENDPOINT` to point to your collector. Leave it empty to disable export during local development.

### 6. Replace the Example stubs

The `Example*` files throughout the project are working stubs that demonstrate all patterns end-to-end. Use them as a reference, then replace them with your own features.

## Adding a New Feature

The workflow for adding a feature (e.g. `Product`) mirrors the existing `Example` feature:

**1. Domain** — add entity and error codes:

```
Domain/Entities/ProductEntity.cs
Domain/Constants/ProductErrorCodes.cs
```

**2. Application** — add handlers, requests, validators, mapper:

```
Application/Features/Products/
├── ProductResponse.cs
├── Mapper/ProductMapper.cs
└── Handlers/
    ├── Create/
    │   ├── CreateProductHandler.cs
    │   ├── Request/CreateProductRequest.cs
    │   └── Validator/CreateProductValidator.cs
    ├── GetById/GetByIdProductHandler.cs
    ├── GetAll/
    │   ├── GetAllProductHandler.cs
    │   └── Request/GetAllProductRequest.cs
    ├── Update/
    │   ├── UpdateProductHandler.cs
    │   ├── Request/UpdateProductRequest.cs
    │   └── Validator/UpdateProductValidator.cs
    └── Delete/DeleteProductHandler.cs
```

**3. Infrastructure** — implement the repository:

```csharp
public sealed class ProductRepository : IRepository<ProductEntity, GetAllProductRequest>
{
    // EF Core, Dapper, etc.
}
```

**4. IoC** — nenhuma alteração necessária. Handlers e repositórios são registrados automaticamente via reflection ao implementar `IHandler<,>` e `IRepository<,>`.

**5. Api** — add one file per endpoint:

```
Api/Endpoints/Products/
├── Create.cs
├── GetById.cs
├── GetAll.cs
├── Update.cs
└── Delete.cs
```

Endpoints are registered automatically via reflection — no additional wiring needed.

## Running Locally

With Aspire orchestration (recommended):

```bash
dotnet run --project "01-aspire/01-AppHost/RentifyxIdentity.AppHost"
```

Or directly:

```bash
dotnet run --project "02-src/01-Api/RentifyxIdentity.Api"
```

## Running with Docker

```bash
docker build -t myproject .
docker run -p 8080:8080 -e ASPNETCORE_ENVIRONMENT=Production myproject
```

## Running on Kubernetes

```bash
kubectl apply -k k8s/overlays/dev
kubectl apply -k k8s/overlays/prod
```

## Contributing

See [docs/](docs/) for architecture docs, ADRs, and contributor guides.

## License

MIT © eugeniobandeira
