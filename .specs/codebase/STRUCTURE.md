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
│   │           └── Identity/                  # Auth/, Mapper/, User/ - register, verify-email,
│   │                                          # login, refresh-token, logout, password-reset,
│   │                                          # LGPD endpoints (consent, export, erasure)
│   ├── 03-Domain/
│   │   └── RentifyxIdentity.Domain/
│   │       ├── Common/                        # PagedResult<T>
│   │       ├── Constants/                     # {Feature}ErrorCodes, ValidationConstants
│   │       ├── Entities/                      # UserEntity, OutboxEntry
│   │       ├── Interfaces/
│   │       │   ├── Common/                    # IRepository<T>, IRepository<T,TFilter>
│   │       │   ├── Notifications/             # IOutboxRepository
│   │       │   └── Users/                     # IUserRepository, ITokenService, IPasswordHasher,
│   │       │                                  # IAuditLogService
│   │       └── MessageResource/               # ValidationMessageResource.resx + Designer.cs
│   ├── 04-IoC/
│   │   └── RentifyxIdentity.IoC/
│   │       ├── DependencyInjectionExtension.cs   # Public facade: AddApplication, AddInfrastructure
│   │       ├── ApplicationDependencyInjection.cs # Auto-registers handlers + validators
│   │       └── InfrastructureDependencyInjection.cs # Auto-registers repositories
│   └── 05-Infrastructure/
│       └── RentifyxIdentity.Infrastructure/
│           ├── Configuration/                 # SecretsManagerConfigurationProvider
│           ├── Constants/                     # ConfigurationKeys, DynamoDbConstants
│           ├── Mapping/                       # UserDynamoDbMapper, OutboxItemMapper
│           ├── Messaging/                     # IKafkaProducerFactory/KafkaProducerFactory
│           │                                  # (PLAINTEXT, no auth - self-hosted broker)
│           ├── Models/                        # UserDynamoDbItem, OutboxDynamoDbItem, AuditLogEntry
│           ├── Options/                       # OutboxPublisherOptions
│           ├── Repositories/                  # UserRepository, OutboxRepository (real DynamoDB,
│           │                                  # IDynamoDBContext-based, not stubs)
│           └── Services/                      # TokenService (custom JWT RS256, D-004 - not
│                                              # Cognito), PasswordHasher, AuditLogService
├── 03-tests/
│   ├── Directory.Build.props                  # Suppresses CA1707, CA1859, CA1305, CA1001 for tests
│   ├── 01-Common/
│   │   └── RentifyxIdentity.Tests.Common/
│   │       └── Builders/                      # Bogus fluent builders (per-feature request builders)
│   ├── 02-Validators/
│   │   └── RentifyxIdentity.Tests.Validators/
│   │       └── Features/{Feature}/            # Validator unit tests (no mocks)
│   ├── 03-Handlers/
│   │   └── RentifyxIdentity.Tests.Handlers/
│   │       └── Features/{Feature}/            # Handler unit tests (Moq)
│   ├── 04-Repositories/
│   │   └── RentifyxIdentity.Tests.Repositories/
│   │       └── Features/{Feature}/            # Repository integration tests (Testcontainers/LocalStack,
│   │                                          # tagged Category=RequiresDocker)
│   └── 05-Integration/
│       └── RentifyxIdentity.Tests.Integration/
│           ├── CustomWebApplicationFactory.cs
│           └── Api/{Feature}/                 # End-to-end HTTP tests
├── docs/
│   ├── architecture/                          # c4-{context,container,component}.md
│   ├── decisions/                             # ADRs
│   ├── guides/adding-a-new-feature.md
│   ├── api-contracts.md
│   ├── runbook.md
│   └── slo.md
├── iac/terraform/                             # Terraform: network-agnostic app infra - EC2, ECR,
│                                              # DynamoDB, KMS, Secrets Manager, SES config, optional
│                                              # Cognito (enable_cognito flag, unused by app code today),
│                                              # GitHub Actions OIDC deploy role
├── k8s/
│   ├── base/                                  # Kustomize base manifests
│   └── overlays/dev/ + overlays/prod/         # Not the actual deploy path today - real deploy is
│                                              # via the EC2 module + deploy.yml, not Kubernetes; kept
│                                              # for a possible future migration, not currently applied
├── .specs/                                    # tlc-spec-driven skill workspace (fully tracked in git)
├── .github/workflows/{ci,deploy}.yml
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
**Key files:** `Common/Handler/IHandler.cs`, `Common/Extensions/ValidationExtensions.cs`, `Features/Identity/` (all real auth/LGPD use cases — register, verify-email, login, refresh-token, logout, password-reset, consent)

### Domain (`02-src/03-Domain/`)
**Purpose:** Core business rules — entities, value objects, domain events, repository contracts. Zero framework dependencies.
**Key files:** `Interfaces/Common/IRepository.cs`, `Interfaces/Users/{ITokenService,IUserRepository,IPasswordHasher,IAuditLogService}.cs`, `Interfaces/Notifications/IOutboxRepository.cs`, `Entities/{UserEntity,OutboxEntry}.cs`

### IoC (`02-src/04-IoC/`)
**Purpose:** Dependency wiring — reflection-based auto-registration for handlers, validators, and repositories.
**Key files:** `DependencyInjectionExtension.cs`, `ApplicationDependencyInjection.cs`, `InfrastructureDependencyInjection.cs`

### Infrastructure (`02-src/05-Infrastructure/`)
**Purpose:** External adapters — real DynamoDB repositories (`IDynamoDBContext`-based, not stubs), a custom JWT RS256 token service, Kafka producer (PLAINTEXT, self-hosted broker), Secrets Manager config provider.
**Key files:** `Repositories/{UserRepository,OutboxRepository}.cs`, `Services/{TokenService,PasswordHasher,AuditLogService}.cs`, `Messaging/KafkaProducerFactory.cs`, `Configuration/SecretsManagerConfigurationProvider.cs`

## Where Things Live

**Adding a new feature (correct order):**
1. Domain entity/VO → `Domain/Entities/` or `Domain/ValueObjects/`
2. Repository interface → `Domain/Interfaces/{Feature}/`
3. Application feature folder → `Application/Features/{Feature}/{Action}/`
4. Infrastructure implementation → `Infrastructure/Repositories/` or `Infrastructure/Services/`
5. IoC (only if needed — handlers/repos auto-discovered) → `IoC/InfrastructureDependencyInjection.cs`
6. API endpoint → `Api/Endpoints/{Group}/{Action}.cs`
7. Tests → `Tests.Validators/`, `Tests.Handlers/`, `Tests.Integration/`

**Special Directories:**
- `.githooks/` — git hooks (pre-commit gitleaks); activated via `Directory.Build.props` `core.hooksPath`
- `.specs/` — tlc-spec-driven skill workspace, fully committed to source (not local-only)
- `docs/decisions/` — ADRs using template `000-template.md`
