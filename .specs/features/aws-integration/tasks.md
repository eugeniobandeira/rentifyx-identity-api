# E-04 — AWS Infrastructure Integration: Tasks

**Spec**: `.specs/features/aws-integration/spec.md`
**Status**: Approved

---

## Execution Plan

```
Phase 1 — Package Setup
  T01
  └──→ T02 [P]
  └──→ T03 [P]
  └──→ T04 [P]
  └──→ T05 [P]

Phase 2 — Config & Scripts (parallel, after T01)
  T01 done →
  ├── T06 [P]
  └── T07 [P]

Phase 3 — Infrastructure Implementations (parallel, after T02)
  T02 done →
  ├── T08 [P]
  ├── T09 [P]
  ├── T10 [P]
  └── T11 [P]

Phase 4 — Mapper & Repository (sequential, after T08 + T02)
  T08 + T02 → T12 → T13

Phase 5 — Integration Tests (after T05 + T13)
  T05 + T13 → T14 → T15

Phase 6 — Wiring (sequential, after Phase 3 + 4)
  T03 + T06 + T07 → T16
  T04 + T09 + T10 + T11 + T13 → T17
  T09 + T17 → T18  ← final gate
```

---

## Task Breakdown

### T01: Add new package versions to Directory.Packages.props

**What**: Add AWS SDK, JWT, LocalStack client, Aspire.Hosting.AWS, and Testcontainers package versions to the centralized NuGet manifest.
**Where**: `Directory.Packages.props`
**Depends on**: None
**Reuses**: Existing `Directory.Packages.props` ItemGroup-per-category pattern

**Tools**:
- MCP: none
- Skill: none

**Done when**:
- [ ] `AWSSDK.DynamoDBv2`, `AWSSDK.SimpleEmailV2`, `AWSSDK.SecretsManager` added under `Label="AWS"`
- [ ] `Microsoft.AspNetCore.Authentication.JwtBearer`, `System.IdentityModel.Tokens.Jwt`, `Microsoft.IdentityModel.Tokens` added under `Label="Auth"`
- [ ] `LocalStack.Client.Extensions` added under `Label="LocalStack"`
- [ ] `Aspire.Hosting.AWS` added under `Label="Aspire"`
- [ ] `Testcontainers.LocalStack` added under `Label="Tests"` (alongside existing test packages)
- [ ] `dotnet restore RentifyxIdentity.slnx` exits 0 — all packages resolve

**Tests**: none
**Gate**: build
**Verify**: `dotnet restore RentifyxIdentity.slnx` — exit code 0, no unresolved packages

**Commit**: `build(packages): add AWS SDK, JWT, LocalStack, and Testcontainers package versions`

---

### T02: Add package references to Infrastructure.csproj [P]

**What**: Reference AWS SDK and JWT packages in the Infrastructure project.
**Where**: `02-src/05-Infrastructure/RentifyxIdentity.Infrastructure/RentifyxIdentity.Infrastructure.csproj`
**Depends on**: T01
**Reuses**: `02-src/01-Api/RentifyxIdentity.Api/RentifyxIdentity.Api.csproj` (PackageReference without version pattern)

**Tools**:
- MCP: none
- Skill: none

**Done when**:
- [ ] `AWSSDK.DynamoDBv2`, `AWSSDK.SimpleEmailV2`, `AWSSDK.SecretsManager` referenced
- [ ] `LocalStack.Client.Extensions` referenced
- [ ] `System.IdentityModel.Tokens.Jwt`, `Microsoft.IdentityModel.Tokens` referenced
- [ ] Gate check passes: `dotnet build 02-src/05-Infrastructure/RentifyxIdentity.Infrastructure`

**Tests**: none
**Gate**: build
**Verify**: `dotnet build 02-src/05-Infrastructure` — 0 errors

**Commit**: `build(infrastructure): add AWS SDK, JWT, and LocalStack package references`

---

### T03: Add Aspire.Hosting.AWS to AppHost.csproj [P]

**What**: Reference `Aspire.Hosting.AWS` in the Aspire AppHost project so `AddAWSSDKConfig()` is available.
**Where**: `01-aspire/01-AppHost/RentifyxIdentity.AppHost/RentifyxIdentity.AppHost.csproj`
**Depends on**: T01
**Reuses**: `AppHost.csproj` existing PackageReference pattern

**Tools**:
- MCP: none
- Skill: none

**Done when**:
- [ ] `<PackageReference Include="Aspire.Hosting.AWS" />` added
- [ ] `dotnet build 01-aspire/01-AppHost` exits 0

**Tests**: none
**Gate**: build
**Verify**: `dotnet build 01-aspire/01-AppHost` — 0 errors

**Commit**: `build(apphost): add Aspire.Hosting.AWS package reference`

---

### T04: Add JwtBearer to Api.csproj [P]

**What**: Reference `Microsoft.AspNetCore.Authentication.JwtBearer` in the API project.
**Where**: `02-src/01-Api/RentifyxIdentity.Api/RentifyxIdentity.Api.csproj`
**Depends on**: T01
**Reuses**: `Api.csproj` existing PackageReference pattern

**Tools**:
- MCP: none
- Skill: none

**Done when**:
- [ ] `<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />` added
- [ ] `dotnet build 02-src/01-Api` exits 0

**Tests**: none
**Gate**: build
**Verify**: `dotnet build 02-src/01-Api` — 0 errors

**Commit**: `build(api): add JwtBearer package reference`

---

### T05: Add Testcontainers.LocalStack to Tests.Repositories.csproj [P]

**What**: Reference `Testcontainers.LocalStack` in the repository integration test project.
**Where**: `03-tests/04-Repositories/RentifyxIdentity.Tests.Repositories/RentifyxIdentity.Tests.Repositories.csproj`
**Depends on**: T01
**Reuses**: Existing `<PackageReference>` pattern in the same file

**Tools**:
- MCP: none
- Skill: none

**Done when**:
- [ ] `<PackageReference Include="Testcontainers.LocalStack" />` added
- [ ] `dotnet build 03-tests/04-Repositories` exits 0

**Tests**: none
**Gate**: build
**Verify**: `dotnet build 03-tests/04-Repositories` — 0 errors

**Commit**: `build(tests.repositories): add Testcontainers.LocalStack package reference`

---

### T06: Create LocalStack init script [P]

**What**: Bash script that runs inside LocalStack on startup to create the DynamoDB table, GSIs, TTL attribute, and seed the Secrets Manager secret for development.
**Where**: `01-aspire/01-AppHost/scripts/init-localstack.sh` (new file)
**Depends on**: None
**Reuses**: ADR-005 table layout (docs/decisions/005-dynamodb-single-table-design.md)

**Tools**:
- MCP: none
- Skill: none

**Done when**:
- [ ] Script creates table `rentifyx-identity` with `PK` (S) as hash key, billing `PAY_PER_REQUEST`
- [ ] GSI `GSI_Email` created with `GSI_Email_PK` (S) as hash key, projection `ALL`
- [ ] GSI `GSI_TaxId` created with `GSI_TaxId_PK` (S) as hash key, projection `ALL`
- [ ] TTL enabled on `rentifyx-identity` for attribute `TTL`
- [ ] Secret `rentifyx/identity/development` created in Secrets Manager with JSON keys: `Hmac:Key` (64-char random hex), `Jwt:PrivateKeyPem` (RSA-2048 PEM via `openssl genrsa`), `Ses:FromAddress` (`noreply@rentifyx.com.br`)
- [ ] Script uses `awslocal` with `--region sa-east-1`
- [ ] Script is executable (`chmod +x`)

**Tests**: none
**Gate**: build
**Verify**: Run script manually against a local LocalStack instance; `awslocal dynamodb list-tables` returns `rentifyx-identity`; `awslocal secretsmanager get-secret-value --secret-id rentifyx/identity/development` returns the JSON

**Commit**: `feat(aspire): add LocalStack init script for DynamoDB table, GSIs, TTL, and secrets seed`

---

### T07: Add AWS, JWT, SES, and LocalStack configuration sections [P]

**What**: Add required configuration sections to `appsettings.json` and `appsettings.Development.json`.
**Where**:
- `02-src/01-Api/RentifyxIdentity.Api/appsettings.json`
- `02-src/01-Api/RentifyxIdentity.Api/appsettings.Development.json`
**Depends on**: None
**Reuses**: Existing `appsettings.json` section structure (RateLimit, Cors patterns)

**Tools**:
- MCP: none
- Skill: none

**Done when**:
- [ ] `appsettings.json` has section `AWS` with `Region: "sa-east-1"` and `SecretsManager.SecretName: "rentifyx/identity/{environment}"`
- [ ] `appsettings.json` has section `LocalStack` with `UseLocalStack: false`
- [ ] `appsettings.json` has section `Jwt` with `Issuer: "rentifyx-identity"` and `Audience: "rentifyx-services"`
- [ ] `appsettings.json` has section `Ses` with `FromAddress: ""` (overridden by Secrets Manager in prod)
- [ ] `appsettings.Development.json` overrides `LocalStack:UseLocalStack: true`, `LocalStack:Config:LocalStackHost: "localhost"`, `LocalStack:Config:EdgePort: 4566`, `LocalStack:Config:UseSsl: false`
- [ ] `dotnet build RentifyxIdentity.slnx` exits 0

**Tests**: none
**Gate**: build
**Verify**: `dotnet run --project AppHost` reads config without errors

**Commit**: `chore(config): add AWS, LocalStack, JWT, and SES configuration sections`

---

### T08: Add UserEntity.Reconstitute internal factory [P]

**What**: Add `internal static UserEntity Reconstitute(...)` factory method to `UserEntity` so the DynamoDB mapper can reconstruct entities from stored attributes without reflection.
**Where**: `02-src/03-Domain/RentifyxIdentity.Domain/Entities/UserEntity.cs`
**Depends on**: None (Domain has no external package deps)
**Reuses**: `UserEntity.Create(...)` static factory pattern (same file)

**Tools**:
- MCP: none
- Skill: none

**Done when**:
- [ ] `internal static UserEntity Reconstitute(Guid id, Email email, TaxDocument taxId, Password passwordHash, UserRole role, UserStatus status, DateTimeOffset createdAt, string? emailVerificationTokenHash, DateTimeOffset? emailVerificationTokenExpiry, string? passwordResetTokenHash, DateTimeOffset? passwordResetTokenExpiry, string? refreshTokenHash, DateTimeOffset? refreshTokenExpiry)` method exists
- [ ] Method populates all fields and returns a valid `UserEntity`
- [ ] Method is `internal` — not accessible outside Domain/Infrastructure (same assembly group)
- [ ] Existing `Create(...)` factory is unchanged
- [ ] `dotnet build 02-src/03-Domain` exits 0

**Tests**: none — tested via T15 integration tests (merge-forward per TLC co-location rule; Reconstitute cannot be unit-tested in isolation without a full round-trip)
**Gate**: build
**Verify**: `dotnet build 02-src/03-Domain` — 0 errors, 0 warnings

**Commit**: `feat(domain): add UserEntity.Reconstitute internal factory for DynamoDB deserialization`

---

### T09: Create SecretsManagerConfigurationProvider [P]

**What**: Implement `IConfigurationSource` + `IConfigurationProvider` that loads the `rentifyx/identity/{environment}` secret from AWS Secrets Manager into `IConfiguration` at app startup. Skip in `Testing` environment.
**Where**: `02-src/05-Infrastructure/RentifyxIdentity.Infrastructure/Configuration/SecretsManagerConfigurationProvider.cs`
**Depends on**: T02
**Reuses**: `02-src/05-Infrastructure/RentifyxIdentity.Infrastructure/Services/PasswordHasher.cs` (sealed class, namespace pattern)

**Tools**:
- MCP: none
- Skill: none

**Done when**:
- [ ] `SecretsManagerConfigurationSource` implements `IConfigurationSource` with `Build()` returning a `SecretsManagerConfigurationProvider`
- [ ] `SecretsManagerConfigurationProvider` implements `IConfigurationProvider` — `Load()` calls `GetSecretValueAsync`, deserializes JSON, and populates the internal data dictionary
- [ ] When `ASPNETCORE_ENVIRONMENT == "Testing"`, `Load()` returns immediately without calling AWS
- [ ] Secret name resolved as `IConfiguration["AWS:SecretsManager:SecretName"]` with `{environment}` replaced by current env
- [ ] `AmazonSecretsManagerClient` configured with region `sa-east-1` and LocalStack endpoint when `IConfiguration["LocalStack:UseLocalStack"] == "true"`
- [ ] Static extension method `AddSecretsManager(this IConfigurationBuilder builder, IConfiguration bootstrapConfig)` exported from the same file
- [ ] `dotnet build 02-src/05-Infrastructure` exits 0

**Tests**: none — secrets loading tested end-to-end via T15 (LocalStack integration) and T18 (full pipeline)
**Gate**: build
**Verify**: `dotnet build 02-src/05-Infrastructure` — 0 errors

**Commit**: `feat(infrastructure): add SecretsManagerConfigurationProvider for startup secret loading`

---

### T10: Implement TokenService with RS256 JWT [P]

**What**: Replace the stub `TokenService` with a real implementation that issues RS256 JWTs signed with an RSA-2048 key loaded from `IConfiguration["Jwt:PrivateKeyPem"]` and uses the real HMAC key from Secrets Manager for `HashToken`.
**Where**: `02-src/05-Infrastructure/RentifyxIdentity.Infrastructure/Services/TokenService.cs`
**Depends on**: T02
**Reuses**: `TokenService.cs` (current stub — keep `GenerateRefreshToken` and `VerifyTokenHash` as-is)

**Tools**:
- MCP: none
- Skill: none

**Done when**:
- [ ] Constructor injects `IConfiguration`; loads `Jwt:PrivateKeyPem` and creates `RSA` via `RSA.Create()` + `ImportFromPem(pem)` — fails fast with `InvalidOperationException` if PEM is missing/invalid
- [ ] RSA public key derived from private key in memory — no separate config entry needed
- [ ] `GenerateAccessToken(Guid userId, string email, string role)` returns a valid RS256 JWT with claims `sub`, `email`, `role`, `iss="rentifyx-identity"`, `aud="rentifyx-services"`, `exp=now+15min`
- [ ] `HashToken(string rawToken)` reads HMAC key from `IConfiguration["Hmac:Key"]` — removes the `"refresh-token-hmac-key"` hardcode
- [ ] `GenerateRefreshToken()` and `VerifyTokenHash()` unchanged
- [ ] Unit test `TokenServiceTests` created in `03-tests/03-Handlers/Features/Identity/` with: (a) valid PEM → `GenerateAccessToken` returns parseable RS256 JWT with correct claims; (b) missing PEM → constructor throws; (c) `HashToken` produces consistent HMAC with same key; (d) `VerifyTokenHash` returns false for tampered token
- [ ] Gate check passes: `dotnet test 03-tests/03-Handlers`

**Tests**: unit
**Gate**: quick
**Verify**: `dotnet test 03-tests/03-Handlers --filter TokenService` — all 4 tests pass

**Commit**: `feat(infrastructure): implement TokenService with RS256 JWT and HMAC from Secrets Manager`

---

### T11: Implement EmailService with SES v2 [P]

**What**: Replace the stub `EmailService` with a real implementation that sends emails via `IAmazonSimpleEmailServiceV2`.
**Where**: `02-src/05-Infrastructure/RentifyxIdentity.Infrastructure/Services/EmailService.cs`
**Depends on**: T02
**Reuses**: `EmailService.cs` (current stub — same class, implement the two methods)

**Tools**:
- MCP: none
- Skill: none

**Done when**:
- [ ] Constructor injects `IAmazonSimpleEmailServiceV2` and `IConfiguration`
- [ ] `SendVerificationEmailAsync`: builds `SendEmailRequest` with `FromEmailAddress = IConfiguration["Ses:FromAddress"]`, destination, subject `"Confirm your email — RentifyX"`, HTML body containing the token; calls `SendEmailAsync`; logs `MessageId`
- [ ] `SendPasswordResetEmailAsync`: same pattern, subject `"Password reset — RentifyX"`
- [ ] Unit test `EmailServiceTests` created in `03-tests/03-Handlers/Features/Identity/` with mock `IAmazonSimpleEmailServiceV2`: (a) `SendVerificationEmailAsync` calls `SendEmailAsync` once with correct recipient and from address; (b) `SendPasswordResetEmailAsync` calls `SendEmailAsync` once with correct subject
- [ ] Gate check passes: `dotnet test 03-tests/03-Handlers`

**Tests**: unit
**Gate**: quick
**Verify**: `dotnet test 03-tests/03-Handlers --filter EmailService` — all 2 tests pass

**Commit**: `feat(infrastructure): implement EmailService with AWS SES v2`

---

### T12: Create UserDynamoDbMapper

**What**: Create a static mapper that converts `UserEntity` to a DynamoDB `Dictionary<string, AttributeValue>` item and back.
**Where**: `02-src/05-Infrastructure/RentifyxIdentity.Infrastructure/Mapping/UserDynamoDbMapper.cs`
**Depends on**: T02, T08
**Reuses**: `02-src/02-Application/RentifyxIdentity.Application/Features/Identity/Mapper/UserMapper.cs` (static mapper pattern)

**Tools**:
- MCP: none
- Skill: none

**Done when**:
- [ ] `ToItem(UserEntity entity) → Dictionary<string, AttributeValue>` maps: `PK="USER#{entity.Id}"`, `Id`, `Email` (via `entity.Email.Value`), `GSI_Email_PK="EMAIL#{entity.Email.Value}"`, `TaxId` (via `entity.TaxId.RawValue`), `TaxDocumentType`, `GSI_TaxId_PK="TAXID#{entity.TaxId.RawValue}"`, `PasswordHash` (via `entity.PasswordHash.HashValue`), `Role`, `Status`, `CreatedAt` (ISO 8601); all nullable fields only included when non-null; `TTL` attribute set as Unix epoch when `Status == PendingVerification` (now + 48h), removed otherwise
- [ ] `ToEntity(Dictionary<string, AttributeValue> item) → UserEntity` calls `UserEntity.Reconstitute(...)` with values reconstructed via `Email.Create(...)`, `TaxDocument.Create(...)` (or `CreateAnonymized()` when `RawValue == "ANONYMIZED"`), `Password.FromHash(...)`; nullable fields read via `TryGetValue`
- [ ] `dotnet build 02-src/05-Infrastructure` exits 0

**Tests**: none — tested via T15 round-trip integration tests (mapper cannot be unit-tested in isolation without real `UserEntity` construction which itself requires Domain)
**Gate**: build
**Verify**: `dotnet build 02-src/05-Infrastructure` — 0 errors

**Commit**: `feat(infrastructure): add UserDynamoDbMapper for DynamoDB attribute serialization`

---

### T13: Implement UserRepository with DynamoDB

**What**: Replace all six `NotImplementedException` stubs in `UserRepository` with real DynamoDB operations using `IAmazonDynamoDB` and `UserDynamoDbMapper`.
**Where**: `02-src/05-Infrastructure/RentifyxIdentity.Infrastructure/Repositories/UserRepository.cs`
**Depends on**: T02, T12
**Reuses**: `UserRepository.cs` (current stub — modify in place); `UserDynamoDbMapper.cs` (T12)

**Tools**:
- MCP: none
- Skill: none

**Done when**:
- [ ] Constructor injects `IAmazonDynamoDB` and `IConfiguration`; table name from `IConfiguration["AWS:DynamoDB:TableName"]` with default `"rentifyx-identity"`
- [ ] `AddAsync`: `PutItemAsync` with `ConditionExpression = "attribute_not_exists(PK)"`
- [ ] `GetByIdAsync`: `GetItemAsync` with `Key = { PK: "USER#{id}" }`; returns null when item not found
- [ ] `GetByEmailAsync`: `QueryAsync` on GSI `GSI_Email` with `KeyConditionExpression = "GSI_Email_PK = :pk"` where `:pk = "EMAIL#{email.ToLowerInvariant()}"`; returns null when no results
- [ ] `GetByTaxIdAsync`: `QueryAsync` on GSI `GSI_TaxId` with `:pk = "TAXID#{taxId.ToUpperInvariant()}"`
- [ ] `UpdateAsync`: `PutItemAsync` (full item overwrite) using `UserDynamoDbMapper.ToItem()` — simpler than partial UpdateExpression and avoids expression attribute name complexity
- [ ] `DeleteAsync`: `DeleteItemAsync` with `Key = { PK: "USER#{entity.Id}" }`
- [ ] No `NotImplementedException` remains
- [ ] `dotnet build 02-src/05-Infrastructure` exits 0

**Tests**: integration — tests co-located in T15 (merged-forward: T15 is the earliest point where the LocalStack container exists to run them)
**Gate**: build
**Verify**: `dotnet build 02-src/05-Infrastructure` — 0 errors; integration tests verified in T15

**Commit**: `feat(infrastructure): implement UserRepository with DynamoDB single-table design`

---

### T14: Create LocalStackFixture for repository integration tests

**What**: Create a shared xUnit `IAsyncLifetime` fixture that starts a LocalStack Testcontainer, creates the `rentifyx-identity` DynamoDB table with GSIs, and exposes the `IAmazonDynamoDB` client for tests.
**Where**: `03-tests/04-Repositories/RentifyxIdentity.Tests.Repositories/Infrastructure/LocalStackFixture.cs`
**Depends on**: T05, T13
**Reuses**: `03-tests/05-Integration/RentifyxIdentity.Tests.Integration/CustomWebApplicationFactory.cs` (IAsyncLifetime pattern)

**Tools**:
- MCP: none
- Skill: none

**Done when**:
- [ ] `LocalStackFixture` implements `IAsyncLifetime`
- [ ] `InitializeAsync`: builds and starts `LocalStackBuilder().WithServices(Service.DynamoDb)` container; creates `AmazonDynamoDBClient` with endpoint `http://localhost:{port}`, region `sa-east-1`, credentials `("test", "test")`; creates table `rentifyx-identity` with `PK` (hash key), GSI `GSI_Email` (hash key `GSI_Email_PK`, projection ALL), GSI `GSI_TaxId` (hash key `GSI_TaxId_PK`, projection ALL), billing `PAY_PER_REQUEST`; waits for table status `ACTIVE`
- [ ] `DisposeAsync`: stops container
- [ ] `IAmazonDynamoDB Client` exposed as public property
- [ ] `string TableName` property returns `"rentifyx-identity"`
- [ ] `dotnet build 03-tests/04-Repositories` exits 0

**Tests**: none (fixture is test infrastructure, not a code layer under test)
**Gate**: build
**Verify**: `dotnet build 03-tests/04-Repositories` — 0 errors

**Commit**: `test(repositories): add LocalStackFixture with Testcontainers for DynamoDB integration tests`

---

### T15: Create UserRepositoryTests (integration tests for T13)

**What**: Implement integration tests for `UserRepository` using `LocalStackFixture` — covering all 6 repository methods with real DynamoDB via Testcontainers + LocalStack.
**Where**: `03-tests/04-Repositories/RentifyxIdentity.Tests.Repositories/Features/Identity/UserRepositoryTests.cs`
**Depends on**: T13, T14
**Reuses**: `03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers/` (test naming: `{Action}_{Condition}_{Expected}`); `03-tests/01-Common/` UserBuilder

**Tools**:
- MCP: none
- Skill: none

**Done when**:
- [ ] `UserRepositoryTests` is `sealed` and implements `IClassFixture<LocalStackFixture>`
- [ ] Instantiates `UserRepository` with `fixture.Client` and `IConfiguration` with `AWS:DynamoDB:TableName="rentifyx-identity"`
- [ ] Test: `Add_ValidUser_PersistsAllFields` — round-trip AddAsync → GetByIdAsync; all entity fields match
- [ ] Test: `GetById_NonExistentId_ReturnsNull`
- [ ] Test: `GetByEmail_ExistingEmail_ReturnsUser`
- [ ] Test: `GetByEmail_NonExistentEmail_ReturnsNull`
- [ ] Test: `GetByTaxId_ExistingTaxId_ReturnsUser`
- [ ] Test: `Update_AfterVerifyEmail_StatusIsActive` — AddAsync PendingVerification user → VerifyEmail() → UpdateAsync → GetByIdAsync → Status is Active, token fields null
- [ ] Test: `Add_PendingVerificationUser_HasTtlAttribute` — verify raw DynamoDB item has `TTL` attribute after AddAsync with PendingVerification status
- [ ] Test: `Update_ActiveUser_HasNoTtlAttribute` — verify `TTL` absent after UpdateAsync with Active status
- [ ] Each test deletes its own item in cleanup (via `DeleteAsync`) to prevent state leak between tests
- [ ] Gate check passes: `dotnet test 03-tests/04-Repositories`
- [ ] Test count: 8 tests pass

**Tests**: integration
**Gate**: full
**Verify**: `dotnet test 03-tests/04-Repositories` — 8 passed, 0 failed

**Commit**: `test(repositories): add UserRepository integration tests with LocalStack and Testcontainers`

---

### T16: Update AppHost.cs with LocalStack container and AWS config

**What**: Add the LocalStack container and `AddAWSSDKConfig` to AppHost so the API receives region and LocalStack endpoint configuration when running locally.
**Where**: `01-aspire/01-AppHost/RentifyxIdentity.AppHost/AppHost.cs`
**Depends on**: T03, T06, T07
**Reuses**: `AppHost.cs` (existing — modify `builder.AddProject` chain)

**Tools**:
- MCP: Context7 (Aspire.Hosting.AWS docs if needed)
- Skill: none

**Done when**:
- [ ] `builder.AddAWSSDKConfig().WithRegion(RegionEndpoint.SAEast1)` called and stored as `awsConfig`
- [ ] `builder.AddContainer("localstack", "localstack/localstack")` configured with: `WithEnvironment("SERVICES", "dynamodb,ses,secretsmanager,kms")`, `WithEnvironment("AWS_DEFAULT_REGION", "sa-east-1")`, `WithEnvironment("LOCALSTACK_HOST", "localhost")`, `WithBindMount("./scripts/init-localstack.sh", "/etc/localstack/init/ready.d/init-aws.sh")`, `WithEndpoint(targetPort: 4566, name: "http")`
- [ ] `.AddProject` for the API chains `.WithReference(awsConfig)` to inject region env vars
- [ ] `dotnet build 01-aspire/01-AppHost` exits 0

**Tests**: none
**Gate**: build
**Verify**: `dotnet build 01-aspire/01-AppHost` — 0 errors

**Commit**: `feat(aspire): wire LocalStack container and AWS SDK config in AppHost`

---

### T17: Update InfrastructureDependencyInjection with AWS clients and JWT bearer

**What**: Register `IAmazonDynamoDB`, `IAmazonSimpleEmailServiceV2`, `LocalStack` client extension, and real JWT bearer authentication in `InfrastructureDependencyInjection`.
**Where**: `02-src/04-IoC/RentifyxIdentity.IoC/InfrastructureDependencyInjection.cs`
**Depends on**: T04, T09, T10, T11, T13
**Reuses**: `InfrastructureDependencyInjection.cs` (existing — modify `Register` method)

**Tools**:
- MCP: none
- Skill: none

**Done when**:
- [ ] `services.AddLocalStack(configuration)` called so `AddAwsService<T>()` auto-detects LocalStack endpoint
- [ ] `services.AddAwsService<IAmazonDynamoDB>()` registered as singleton
- [ ] `services.AddAwsService<IAmazonSimpleEmailServiceV2>()` registered as singleton
- [ ] `services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => ...)` replaces the stub `services.AddAuthentication()`:
  - `options.TokenValidationParameters.ValidIssuer = configuration["Jwt:Issuer"]`
  - `options.TokenValidationParameters.ValidAudience = configuration["Jwt:Audience"]`
  - `options.TokenValidationParameters.IssuerSigningKey` = `RsaSecurityKey` created from `configuration["Jwt:PrivateKeyPem"]` via `RSA.Create() + ImportFromPem()`
  - `options.TokenValidationParameters.ValidateLifetime = true`
  - `options.TokenValidationParameters.ClockSkew = TimeSpan.Zero`
- [ ] `services.AddAuthorization()` retained
- [ ] Existing registrations (`IUserRepository`, `IEmailService`, `IPasswordHasher`, `ITokenService`) unchanged
- [ ] Gate check passes: `dotnet test RentifyxIdentity.slnx` (all existing tests still pass)
- [ ] Test count ≥ previous baseline (no tests deleted)

**Tests**: integration (IoC/DI layer per TESTING.md matrix — existing integration tests verify DI resolution)
**Gate**: full
**Verify**: `dotnet test RentifyxIdentity.slnx` — all tests pass; `GET /api/v1/users/me` without token returns 401

**Commit**: `feat(ioc): register AWS clients, LocalStack, and JWT bearer authentication`

---

### T18: Update Program.cs — UseAuthorization and AddSecretsManager

**What**: Wire `SecretsManagerConfigurationProvider` into the configuration pipeline and add `UseAuthorization()` to the HTTP middleware pipeline.
**Where**: `02-src/01-Api/RentifyxIdentity.Api/Program.cs`
**Depends on**: T09, T17
**Reuses**: `Program.cs` (existing — two surgical insertions)

**Tools**:
- MCP: none
- Skill: none

**Done when**:
- [ ] `builder.Configuration.AddSecretsManager(builder.Configuration)` called BEFORE `builder.Services.AddInfrastructure(builder.Configuration)` so secrets are in `IConfiguration` when the DI container is built
- [ ] `app.UseAuthorization()` added immediately after `app.UseAuthentication()` (currently missing)
- [ ] `IConfiguration["Hmac:Key"]` resolves to the value from LocalStack Secrets Manager in Development (not `"dev-hmac-key"`)
- [ ] Gate check passes: `dotnet build RentifyxIdentity.slnx -c Release && dotnet test RentifyxIdentity.slnx`
- [ ] Test count ≥ previous baseline

**Tests**: integration
**Gate**: build (final phase gate — full build + all tests)
**Verify**: `dotnet build RentifyxIdentity.slnx -c Release` — 0 errors; `dotnet test RentifyxIdentity.slnx` — all tests pass

**Commit**: `feat(api): wire SecretsManager config provider and add UseAuthorization to pipeline`

---

## Pre-Approval Checks

### Check 1: Task Granularity

| Task | Scope | Status |
|---|---|---|
| T01: Directory.Packages.props | 1 file — version entries | ✅ Granular |
| T02: Infrastructure.csproj | 1 file — package refs | ✅ Granular |
| T03: AppHost.csproj | 1 file — package ref | ✅ Granular |
| T04: Api.csproj | 1 file — package ref | ✅ Granular |
| T05: Tests.Repositories.csproj | 1 file — package ref | ✅ Granular |
| T06: init-localstack.sh | 1 file — bash script | ✅ Granular |
| T07: appsettings (2 files) | 2 files — same concern (base + override) | ✅ OK (cohesive pair) |
| T08: UserEntity.Reconstitute | 1 file — 1 method | ✅ Granular |
| T09: SecretsManagerConfigurationProvider | 1 file — 1 class | ✅ Granular |
| T10: TokenService | 1 file + unit tests | ✅ Granular |
| T11: EmailService | 1 file + unit tests | ✅ Granular |
| T12: UserDynamoDbMapper | 1 file — mapper | ✅ Granular |
| T13: UserRepository | 1 file — 6 methods | ✅ Granular (cohesive — one class) |
| T14: LocalStackFixture | 1 file — fixture | ✅ Granular |
| T15: UserRepositoryTests | 1 file — 8 test methods | ✅ Granular (cohesive — one test class) |
| T16: AppHost.cs | 1 file — wiring | ✅ Granular |
| T17: InfrastructureDependencyInjection | 1 file — DI wiring | ✅ Granular |
| T18: Program.cs | 1 file — 2 surgical insertions | ✅ Granular |

### Check 2: Diagram-Definition Cross-Check

| Task | Depends On (body) | Diagram Shows | Status |
|---|---|---|---|
| T01 | None | Start of Phase 1 | ✅ Match |
| T02 | T01 | T01 → T02 | ✅ Match |
| T03 | T01 | T01 → T03 | ✅ Match |
| T04 | T01 | T01 → T04 | ✅ Match |
| T05 | T01 | T01 → T05 | ✅ Match |
| T06 | None | Parallel Phase 2 | ✅ Match |
| T07 | None | Parallel Phase 2 | ✅ Match |
| T08 | None | Parallel Phase 3 after T02 | ⚠️ Body says "no external deps" — diagram shows after T02 for build context; T08 is Domain and technically doesn't require T02. Corrected: T08 depends on None (parallel in Phase 3 alongside T02-T05) |
| T09 | T02 | T02 → T09 | ✅ Match |
| T10 | T02 | T02 → T10 | ✅ Match |
| T11 | T02 | T02 → T11 | ✅ Match |
| T12 | T02, T08 | T02+T08 → T12 | ✅ Match |
| T13 | T02, T12 | T12 → T13 | ✅ Match |
| T14 | T05, T13 | T05+T13 → T14 | ✅ Match |
| T15 | T13, T14 | T14 → T15 | ✅ Match |
| T16 | T03, T06, T07 | T03+T06+T07 → T16 | ✅ Match |
| T17 | T04, T09, T10, T11, T13 | Phase 6 after Phase 3+4 | ✅ Match |
| T18 | T09, T17 | T17 → T18 | ✅ Match |

**T08 correction**: `Depends on: None` (Domain project has no external AWS deps). Parallel with T02-T05 in Phase 1 or independently in Phase 2.

### Check 3: Test Co-location Validation

| Task | Code Layer | Matrix Requires | Task Says | Status |
|---|---|---|---|---|
| T01–T07 | Config/Infra/Scripts | none | none | ✅ OK |
| T08 | Domain Entity | Unit | none (merged-forward → T15) | ✅ OK — Reconstitute cannot be tested without full round-trip; merged-forward per TLC rule |
| T09 | Infrastructure/Config | Integration | none (merged-forward → T18) | ✅ OK — tested via pipeline smoke in T18 |
| T10 | Infrastructure/Service | Unit | unit | ✅ OK |
| T11 | Infrastructure/Service | Unit | unit | ✅ OK |
| T12 | Infrastructure/Mapper | Integration (used by Repository) | none (merged-forward → T15) | ✅ OK — mapper tested via repository round-trip |
| T13 | Repository | Integration | integration (merged-forward → T15) | ✅ OK — TLC merge-forward: T15 is earliest runnable point |
| T14 | Test Infrastructure | none | none | ✅ OK |
| T15 | Test (Integration) | — | integration | ✅ OK |
| T16 | Aspire/AppHost | none | none | ✅ OK |
| T17 | IoC/DI | Integration | integration | ✅ OK |
| T18 | API Pipeline | Integration | integration | ✅ OK |

---

## Parallel Execution Map

```
Phase 1 (T01 sequential, then T02-T05+T08 parallel):
  T01
   ├──[P]──→ T02
   ├──[P]──→ T03
   ├──[P]──→ T04
   ├──[P]──→ T05
   └──[P]──→ T08

Phase 2 (parallel, no dependencies):
  ├──[P]──→ T06
  └──[P]──→ T07

Phase 3 (parallel, after T02):
  T02
   ├──[P]──→ T09
   ├──[P]──→ T10  (includes unit tests)
   └──[P]──→ T11  (includes unit tests)

Phase 4 (sequential, after T02 + T08):
  T02 + T08 ──→ T12 ──→ T13

Phase 5 (sequential, after T05 + T13):
  T05 + T13 ──→ T14 ──→ T15  (integration tests, Gate: full)

Phase 6 (sequential, after all):
  T03 + T06 + T07 ──→ T16
  T04 + T09 + T10 + T11 + T13 ──→ T17
  T09 + T17 ──→ T18  ← FINAL GATE (build + all tests)
```

**Parallelism notes:**
- T02-T05+T08: parallel-safe (independent .csproj edits + independent domain file)
- T06-T07: parallel-safe (script file + config files, no shared state)
- T09-T11: parallel-safe (independent Infrastructure files)
- T10 and T11 unit tests: parallel-safe (per TESTING.md — Handler tests are parallel-safe)
- T14-T15: NOT parallel — T15 depends on T14's fixture
- T17-T18: sequential — T18 depends on T17's DI registration

---

**Total tasks:** 18
**Estimated parallel phases:** 3 phases can run with sub-agents
**Gate commands:**
- Quick: `dotnet test 03-tests/02-Validators && dotnet test 03-tests/03-Handlers`
- Full: `dotnet test RentifyxIdentity.slnx`
- Build: `dotnet build RentifyxIdentity.slnx -c Release && dotnet test RentifyxIdentity.slnx`
