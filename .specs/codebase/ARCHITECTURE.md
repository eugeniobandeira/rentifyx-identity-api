# Architecture

**Pattern:** Clean Architecture — 5 layers with strict inward dependency rule. Domain has zero external dependencies.

## High-Level Structure

```
┌─────────────────────────────────────────────────────┐
│                    API Layer                         │
│  Minimal API endpoints, middleware, OpenAPI, DI      │
│         (RentifyxIdentity.Api)                       │
└───────────────────────┬─────────────────────────────┘
                        │ depends on
┌───────────────────────▼─────────────────────────────┐
│                 Application Layer                    │
│   IHandler<TRequest,TResponse>, validators, mappers  │
│         (RentifyxIdentity.Application)               │
└───────────────────────┬─────────────────────────────┘
                        │ depends on
┌───────────────────────▼─────────────────────────────┐
│                  Domain Layer                        │
│  Entities, value objects, events, IRepository<T>     │
│         (RentifyxIdentity.Domain)                    │
└─────────────────────────────────────────────────────┘
                        ▲
                        │ depends on (implements)
┌───────────────────────┴─────────────────────────────┐
│              Infrastructure Layer                    │
│  DynamoDB repos, AWS service adapters                │
│         (RentifyxIdentity.Infrastructure)            │
└─────────────────────────────────────────────────────┘
                        ▲
                        │ wires together
┌───────────────────────┴─────────────────────────────┐
│                   IoC Layer                          │
│  Reflection-based DI registration                    │
│         (RentifyxIdentity.IoC)                       │
└─────────────────────────────────────────────────────┘
```

## Identified Patterns

### CQRS-style Handler Pattern

**Location:** `Application/Features/{Feature}/{Action}/{Action}Handler.cs`
**Purpose:** Each use case is a dedicated handler class; no MediatR.
**Implementation:** Implements `IHandler<TRequest, TResponse>`. Returns `ErrorOr<T>`. Injected into endpoint handlers via DI.
**Example:** `Application/Features/Examples/Handlers/Create/CreateExampleHandler.cs`

```
Request → Validator.ValidateToErrorsAsync() → Guard checks → Repo call → Return ErrorOr<Response>
```

### Minimal API Endpoint Auto-Discovery

**Location:** `Api/Endpoints/{Group}/{Action}.cs`
**Purpose:** Endpoints register themselves — no manual wiring in `Program.cs`.
**Implementation:** Each endpoint implements `IEndpoint`. `AddEndpoints(assembly)` scans and registers all as transient. `MapEndpoints()` instantiates them and calls `MapEndpoint(routeGroup)`.
**Example:** `Api/Endpoints/Examples/Create.cs`

### Generic Repository Contract

**Location:** `Domain/Interfaces/Common/IRepository.cs`
**Purpose:** Decouples application from persistence. Two variants: `IRepository<T>` (basic CRUD) and `IRepository<T, TFilter>` (CRUD + filtered paged list).
**Implementation:** Domain defines the interface; Infrastructure implements it. IoC auto-discovers implementations via reflection.
**Example:** `Infrastructure/Repositories/ExampleRepository.cs`

### Reflection-Based DI Auto-Registration

**Location:** `IoC/ApplicationDependencyInjection.cs`, `IoC/InfrastructureDependencyInjection.cs`
**Purpose:** New handlers and repositories are registered automatically — no manual DI wiring needed.
**Implementation:**
- Handlers: scans for types implementing `IHandler<,>` → registers as Scoped
- Validators: `AddValidatorsFromAssembly()` — FluentValidation DI extension
- Repositories: scans for types implementing `IRepository<>` or `IRepository<,>` → registers both concrete and all interface types as Scoped
**Caveat:** Domain services (`ITokenService`, `IEmailService`) are NOT auto-discovered — must be registered explicitly.

### ErrorOr Result Pattern

**Location:** All handlers and endpoints
**Purpose:** Typed error handling without exceptions for control flow. Maps cleanly to HTTP problem details.
**Implementation:** Handlers return `ErrorOr<T>`. Endpoints call `result.Match(success => ..., errors => errors.ToProblem(httpContext))`. `ToProblem()` maps `ErrorType` to HTTP status codes (422, 404, 409, 401, 500).
**Example:** `Api/Extensions/ErrorOrExtensions.cs`

### Validation via FluentValidation + Extension

**Location:** `Application/Features/{Feature}/{Action}/Validator/{Action}Validator.cs`, `Application/Common/Extensions/ValidationExtensions.cs`
**Purpose:** All input validation before business logic. Validation errors returned as `ErrorOr` — not thrown.
**Implementation:** `validator.ValidateToErrorsAsync(request, ct)` returns `List<Error>?`. Null means valid. Errors include `ErrorCode` routing: codes ending in `.NotFound` become `Error.NotFound`; all others become `Error.Validation`.

### Correlation ID Propagation

**Location:** `Api/Middlewares/CorrelationIdMiddleware.cs`
**Purpose:** Traces requests across logs and responses.
**Implementation:** Reads `X-Correlation-Id` header (or generates a new GUID). Stores in `HttpContext.Items`. Pushes to Serilog `LogContext`. Echoes in response header.

## Data Flow

### Incoming HTTP Request (happy path)

```
HTTP Request
  → CorrelationIdMiddleware (attach/generate correlation ID)
  → Serilog request logging
  → Rate limiter
  → Endpoint handler (static method)
      → IHandler<TRequest, TResponse>.Handle(request, ct)
          → IValidator<TRequest>.ValidateToErrorsAsync()
          → IRepository / ITokenService / IEmailService calls
          → return ErrorOr<TResponse>
      → result.Match(
            success → Results.Ok/Created/NoContent(response DTO)
            errors  → errors.ToProblem(httpContext) → RFC 7807 ProblemDetails
        )
  → HTTP Response
```

### Unhandled Exception Path

```
Exception
  → GlobalExceptionHandler.TryHandleAsync()
      → Log error (with correlationId + traceId)
      → Return 500 ProblemDetails (no stack trace in body)
```

## Code Organization

**Approach:** Layer-first, then feature-based within Application.

**Module boundaries:**
- Domain: no external references — pure C# records, classes, interfaces
- Application: references Domain only; no infrastructure or HTTP types
- Infrastructure: references Domain (implements interfaces); no Application references
- IoC: references all layers to wire them; not referenced by any layer
- API: references Application (handler interfaces, request/response types) and IoC (via DI extensions)

**Assembly scanning markers:**
- Application: `typeof(CreateExampleHandler).Assembly`
- Infrastructure: `typeof(InfrastructureAssemblyMarker).Assembly`
- API endpoints: `Assembly.GetExecutingAssembly()` (from `Program.cs`)
