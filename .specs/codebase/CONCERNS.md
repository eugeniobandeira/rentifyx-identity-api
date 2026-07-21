# Codebase Concerns

**Analysis Date:** 2026-06-21

## Known Bugs

**`chmod` hook fails on Windows (MSB3073):**

- Symptoms: Every project build emits `warning MSB3073: The command "chmod +x .githooks/pre-commit" exited with code 1`
- Trigger: Any `dotnet build` on Windows — `chmod` is a Unix command
- Files: `Directory.Build.props` line 23
- Workaround: Warnings are non-fatal; gitleaks hook still works on Windows via Git for Windows
- Root cause: `Directory.Build.props` runs `chmod +x .githooks/pre-commit` unconditionally
- Fix approach: Wrap in a condition: `<Exec Command="chmod +x .githooks/pre-commit" Condition="'$([MSBuild]::IsOSPlatform(Linux))' == 'true' Or '$([MSBuild]::IsOSPlatform(OSX))' == 'true'" />`

## Tech Debt

**All Infrastructure implementations are stubs:**

- Issue: Every method in `UserRepository`, `TokenService`, `EmailService` throws `NotImplementedException`. The entire persistence and AWS integration layer is unimplemented.
- Files: `02-src/05-Infrastructure/RentifyxIdentity.Infrastructure/Repositories/UserRepository.cs`, `Infrastructure/Services/TokenService.cs`, `Infrastructure/Services/EmailService.cs`
- Why: Deliberate scaffold — real wiring is E-04 scope.
- Impact: The API starts but every request that reaches infrastructure will crash with a 500. Integration tests cannot run against real data.
- Fix approach: Implement DynamoDB adapter in E-04 using `AWSSDK.DynamoDBv2`. Wire Cognito JWT in `TokenService`. Wire SES in `EmailService`.

**`ExampleEntity` / `ExampleRepository` reference implementation left in production code:**

- Issue: The scaffold `Example` domain (entity, repository, handler, validator, endpoints, tests) exists in all layers but serves no business purpose.
- Files: All `Examples/` folders across `Domain/`, `Application/`, `Infrastructure/`, `Api/Endpoints/Examples/`, `Tests.*`
- Why: Left as a reference pattern for developers to follow.
- Impact: Adds noise to the Scalar UI (`/api/v1/examples` endpoints exposed), increases build time marginally, and risks confusion about what is "real" vs. example code.
- Fix approach: Remove all `Example*` files once the first real feature (Identity) is implemented and serves as the de-facto reference. Do not remove before that — the pattern is actively needed.

## Security Considerations

**Authenticated endpoints have no JWT bearer scheme registered:**

- Risk: `GET /users/me`, `DELETE /users/me`, `GET /users/me/data-export` call `.RequireAuthorization()`, but no JWT bearer middleware is registered yet. Until E-04 wires Cognito, these endpoints will return 401 for all requests — including in integration tests.
- Files: `Api/Endpoints/Users/GetProfile.cs`, `DeleteAccount.cs`, `ExportData.cs` (to be created)
- Current mitigation: None — endpoints are not yet created.
- Recommendations: Add a `TestAuthHandler` in `CustomWebApplicationFactory` for integration tests. Wire real Cognito JWT bearer in E-04 via `AddAuthentication().AddJwtBearer()`.

**CPF/CNPJ encryption not yet implemented:**

- Risk: `TaxDocument` value object is designed for KMS encryption at rest, but `UserRepository` is a stub. If the stub is replaced naively without KMS wiring, raw CPF/CNPJ digits could be stored in plain text in DynamoDB.
- Files: `Infrastructure/Repositories/UserRepository.cs`
- Current mitigation: Stub throws — no data written.
- Recommendations: Implement KMS `Encrypt`/`Decrypt` calls inside `UserRepository.AddAsync` / `GetByEmailAsync` / `GetByTaxIdAsync` before any real data is persisted. Wire `IKmsService` in E-04 alongside the repository.

**Rate limiting is global, not per-user:**

- Risk: Current `FixedWindowLimiter` is a global policy (100 req/60s by default). The spec requires per-user login lockout (5 failures → 15-min lock). This is not yet implemented.
- Files: `Api/Extensions/RateLimitExtension.cs`
- Current mitigation: Global rate limiter provides broad DoS protection.
- Recommendations: Implement per-user failure counter in DynamoDB (with TTL) in the `LoginHandler` during E-04. The handler should call an `IRateLimitService.RecordFailureAsync(email)` abstraction.

**HMAC signing key for verification and reset tokens:**

- Risk: The key used to generate HMAC-SHA256 tokens (email verification, password reset) must come from Secrets Manager. If hardcoded during development, it could leak.
- Files: `Infrastructure/Services/TokenService.cs` (stub — not yet implemented)
- Current mitigation: Stub throws — no tokens generated.
- Recommendations: Load HMAC key from Secrets Manager at startup via `IConfiguration`. Never pass as a raw string in code.

## Fragile Areas

**Reflection-based DI auto-registration:**

- Files: `IoC/ApplicationDependencyInjection.cs`, `IoC/InfrastructureDependencyInjection.cs`
- Why fragile: New handlers and repositories are auto-discovered by interface scanning. If a class accidentally implements `IHandler<,>` or `IRepository<>` for a different purpose, it will be registered unexpectedly. Domain service interfaces (`ITokenService`, `IEmailService`) are NOT auto-discovered — forgetting to add them explicitly causes a `InvalidOperationException` at startup.
- Common failures: Missing service registration for non-repository/non-handler types; duplicate registrations if a class implements multiple `IHandler<,>` variants.
- Safe modification: Always register domain services explicitly in `InfrastructureDependencyInjection.Register()`. If adding a class that should NOT be auto-discovered, do not implement `IHandler<,>` or `IRepository<>` on it.
- Test coverage: DI registration is exercised indirectly by integration tests via `CustomWebApplicationFactory`.

**`ValidationExtensions.ToError` error-code routing:**

- Files: `Application/Common/Extensions/ValidationExtensions.cs`
- Why fragile: The logic `failure.ErrorCode.EndsWith(".NotFound")` routes to `Error.NotFound`; everything else becomes `Error.Validation`. This means any error code accidentally ending in `.NotFound` will be treated as a 404 — including typos.
- Safe modification: Always define error codes in `{Feature}ErrorCodes` constants and use them in validators. Never hand-write error code strings in validator rules.

## Test Coverage Gaps

**All identity handler and validator tests are currently skeleton stubs:**

- What's not tested: All 10 identity use-case handlers and 5 validators — `RegisterUser`, `VerifyEmail`, `Login`, `RefreshToken`, `Logout`, `ForgotPassword`, `ResetPassword`, `GetProfile`, `DeleteAccount`, `ExportData`.
- Risk: Business logic errors (e.g., account enumeration via login, token expiry bypass) go undetected until manual testing.
- Priority: High — these are security-critical flows.
- Difficulty: Low — pattern is well-established via `ExampleHandlerTests`. Blocked by: handlers not yet created.

**Repository integration tests require Testcontainers + LocalStack:**

- What's not tested: All DynamoDB read/write operations, GSI query correctness, TTL behavior for refresh tokens.
- Risk: Schema errors, wrong attribute names, or missing GSIs only discovered at runtime.
- Priority: High — DynamoDB is the only persistence layer.
- Difficulty: Medium — Testcontainers setup needed; LocalStack DynamoDB is well-supported.

---

_Concerns audit: 2026-06-21_
_Update as issues are fixed or new ones discovered during any workflow phase._
