# Project Structure

**Root:** `rentifyx-identity-api/`

## Directory Tree

```
rentifyx-identity-api/
├── 01-aspire/
│   ├── 01-AppHost/
│   │   └── RentifyxIdentity.AppHost/          # Aspire orchestration entry point
│   └── 02-ServiceDefaults/
│       └── RentifyxIdentity.ServiceDefaults/  # OTel, health checks, service discovery
├── 02-src/
│   ├── 01-Api/
│   │   └── RentifyxIdentity.Api/
│   │       ├── Abstract/                      # IEndpoint interface
│   │       ├── Endpoints/                     # Endpoint classes + Tags.cs
│   │       ├── Extensions/                    # AddEndpoints, AddVersioning, AddRateLimiting,
│   │       │                                  # AddOpenApiDocumentation, ErrorOrExtensions,
│   │       │                                  # CorsExtensions, MiddlewareExtensions
│   │       ├── Middlewares/                   # CorrelationIdMiddleware, GlobalExceptionHandler
│   │       └── Program.cs
│   ├── 02-Application/
│   │   └── RentifyxIdentity.Application/
│   │       ├── Common/
│   │       │   ├── Handler/                   # IHandler<TRequest, TResponse>
│   │       │   ├── Response/                  # ApiListResponse<T>
│   │       │   ├── Mapper/                    # ApiListResponseMapper
│   │       │   └── Extensions/                # ValidationExtensions (ValidateToErrorsAsync)
│   │       └── Features/
│   │           └── {Feature}/
│   │               ├── {Action}/
│   │               │   ├── {Action}Handler.cs
│   │               │   ├── Request/           # {Action}Request.cs
│   │               │   └── Validator/         # {Action}Validator.cs
│   │               ├── {Feature}Response.cs
│   │               └── Mapper/                # {Feature}Mapper.cs
│   ├── 03-Domain/
│   │   └── RentifyxIdentity.Domain/
│   │       ├── Common/                        # PagedResult<T>
│   │       ├── Constants/                     # {Feature}ErrorCodes, ValidationConstants
│   │       ├── Entities/                      # Domain entities (aggregate roots)
│   │       ├── Interfaces/
│   │       │   └── Common/                    # IRepository<T>, IRepository<T,TFilter>
│   │       └── MessageResource/               # ValidationMessageResource.resx + Designer.cs
│   ├── 04-IoC/
│   │   └── RentifyxIdentity.IoC/
│   │       ├── DependencyInjectionExtension.cs   # Public facade: AddApplication, AddInfrastructure
│   │       ├── ApplicationDependencyInjection.cs # Auto-registers handlers + validators
│   │       └── InfrastructureDependencyInjection.cs # Auto-registers repositories
│   └── 05-Infrastructure/
│       └── RentifyxIdentity.Infrastructure/
│           ├── Repositories/                  # IRepository implementations
│           ├── Services/                      # External service adapters (planned E-04)
│           └── InfrastructureAssemblyMarker.cs
├── 03-tests/
│   ├── Directory.Build.props                  # Suppresses CA1707, CA1859, CA1305, CA1001 for tests
│   ├── 01-Common/
│   │   └── RentifyxIdentity.Tests.Common/
│   │       └── Builders/                      # Bogus fluent builders (ExampleBuilder pattern)
│   ├── 02-Validators/
│   │   └── RentifyxIdentity.Tests.Validators/
│   │       └── Features/{Feature}/            # Validator unit tests (no mocks)
│   ├── 03-Handlers/
│   │   └── RentifyxIdentity.Tests.Handlers/
│   │       └── Features/{Feature}/            # Handler unit tests (Moq)
│   ├── 04-Repositories/
│   │   └── RentifyxIdentity.Tests.Repositories/
│   │       └── Features/{Feature}/            # Repository integration tests (Testcontainers)
│   └── 05-Integration/
│       └── RentifyxIdentity.Tests.Integration/
│           ├── CustomWebApplicationFactory.cs
│           └── Api/{Feature}/                 # End-to-end HTTP tests
├── docs/
│   ├── architecture/overview.md
│   ├── decisions/                             # ADR-001 to ADR-008 (in progress)
│   ├── features/                              # identity.md, identity-implementation-plan.md
│   └── guides/adding-a-new-feature.md
├── iac/                                       # Terraform (Week 6 — empty)
├── k8s/
│   ├── base/                                  # Kustomize base manifests
│   └── overlays/dev/ + overlays/prod/
├── .specs/                                    # tlc-spec-driven skill workspace
├── .github/workflows/ci.yml
├── .githooks/pre-commit                       # gitleaks hook
├── .gitleaks.toml
├── Directory.Build.props                      # Global build settings
├── Directory.Packages.props                   # Centralized NuGet versions
└── RentifyxIdentity.slnx
```

## Module Organization

### API (`02-src/01-Api/`)
**Purpose:** HTTP surface — endpoint mapping, middleware pipeline, OpenAPI docs, error serialization.
**Key files:** `Program.cs`, `Abstract/IEndpoint.cs`, `Extensions/ErrorOrExtensions.cs`, `Extensions/EndpointExtensions.cs`, `Middlewares/CorrelationIdMiddleware.cs`, `Middlewares/GlobalExceptionHandler.cs`

### Application (`02-src/02-Application/`)
**Purpose:** Use-case orchestration — request/response DTOs, validators, handlers, mappers.
**Key files:** `Common/Handler/IHandler.cs`, `Common/Extensions/ValidationExtensions.cs`, `Features/Examples/` (full reference implementation)

### Domain (`02-src/03-Domain/`)
**Purpose:** Core business rules — entities, value objects, domain events, repository contracts. Zero framework dependencies.
**Key files:** `Interfaces/Common/IRepository.cs`, `Common/PagedResult.cs`, `Constants/ValidationConstants.cs`, `MessageResource/ValidationMessageResource.resx`

### IoC (`02-src/04-IoC/`)
**Purpose:** Dependency wiring — reflection-based auto-registration for handlers, validators, and repositories.
**Key files:** `DependencyInjectionExtension.cs`, `ApplicationDependencyInjection.cs`, `InfrastructureDependencyInjection.cs`

### Infrastructure (`02-src/05-Infrastructure/`)
**Purpose:** External adapters — DynamoDB repositories, AWS SDK wrappers. Currently stubs; real wiring in E-04.
**Key files:** `Repositories/ExampleRepository.cs`, `InfrastructureAssemblyMarker.cs`

## Where Things Live

**Adding a new feature (correct order):**
1. Domain entity/VO → `Domain/Entities/` or `Domain/ValueObjects/`
2. Repository interface → `Domain/Interfaces/{Feature}/`
3. Application feature folder → `Application/Features/{Feature}/{Action}/`
4. Infrastructure stub → `Infrastructure/Repositories/`
5. IoC (only if needed — handlers/repos auto-discovered) → `IoC/InfrastructureDependencyInjection.cs`
6. API endpoint → `Api/Endpoints/{Group}/{Action}.cs`
7. Tests → `Tests.Validators/`, `Tests.Handlers/`, `Tests.Integration/`

**Special Directories:**
- `.githooks/` — git hooks (pre-commit gitleaks); activated via `Directory.Build.props` `core.hooksPath`
- `.specs/` — tlc-spec-driven skill workspace (not committed to source, local only)
- `docs/decisions/` — ADRs using template `000-template.md`
