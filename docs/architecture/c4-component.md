# C4 Level 3 — Component

Shows the internal components of the Identity API container and their relationships.

```mermaid
C4Component
    title Component Diagram — RentifyX Identity API (.NET 10)

    Person(user, "RentifyX User")
    System_Ext(dynamo, "DynamoDB")
    System_Ext(ses, "AWS SES")
    System_Ext(secrets, "Secrets Manager")

    Container_Boundary(api, "Identity API") {

        Component(auth_endpoints, "Auth Endpoints", "Minimal API / IEndpoint", "POST register, verify-email, login, refresh, logout, forgot-password, reset-password — all public")
        Component(user_endpoints, "User Endpoints", "Minimal API / IEndpoint", "GET /users/me, DELETE /users/me, GET /users/me/data-export — require Bearer JWT")

        Component(middlewares, "Security Middlewares", "ASP.NET Core Middleware", "CorrelationId, SecurityHeaders (CSP / X-Frame-Options / …), GlobalExceptionHandler, RateLimiter")

        Component(auth_handlers, "Auth Handlers", "IHandler<TRequest,TResponse> / ErrorOr<T>", "RegisterUser, VerifyEmail, Login, RefreshToken, Logout, ForgotPassword, ResetPassword")
        Component(lgpd_handlers, "LGPD Handlers", "IHandler<TRequest,TResponse> / ErrorOr<T>", "GetProfile, DeleteAccount, ExportData — each calls AuditLogService on success")

        Component(validators, "FluentValidation Validators", "AbstractValidator<T>", "One validator per request record; enforces field rules, email format, CPF/CNPJ length, password complexity, consent")

        Component(user_repo, "UserRepository", "IUserRepository / DynamoDB SDK v4", "GetById, GetByEmail, GetByTaxId, Add, Update — maps UserEntity to/from DynamoDB AttributeMap")
        Component(token_svc, "TokenService", "ITokenService / System.IdentityModel", "GenerateAccessToken (RS256), GenerateRefreshToken, HashToken (HMAC-SHA256), VerifyTokenHash")
        Component(email_svc, "EmailService", "IEmailService / SES v2", "SendVerificationEmail, SendPasswordResetEmail")
        Component(audit_svc, "AuditLogService", "IAuditLogService / DynamoDB DataModel", "LogAsync — writes AUDIT# entry; exceptions are swallowed, never propagated")
        Component(secrets_provider, "SecretsManagerConfigProvider", "IConfigurationProvider", "Loads JWT key PEM, HMAC secret, SES from-address into IConfiguration at startup")
    }

    Rel(user, auth_endpoints, "HTTPS request", "JSON")
    Rel(user, user_endpoints, "HTTPS request + Bearer JWT", "JSON")

    Rel(auth_endpoints, middlewares, "passes through", "pipeline")
    Rel(user_endpoints, middlewares, "passes through", "pipeline")

    Rel(auth_endpoints, auth_handlers, "delegates to", "DI")
    Rel(user_endpoints, lgpd_handlers, "delegates to", "DI")

    Rel(auth_handlers, validators, "validates request via", "FluentValidation")
    Rel(lgpd_handlers, validators, "validates request via", "FluentValidation")

    Rel(auth_handlers, user_repo, "reads / writes users via", "IUserRepository")
    Rel(lgpd_handlers, user_repo, "reads / writes users via", "IUserRepository")

    Rel(auth_handlers, token_svc, "issues and verifies tokens via", "ITokenService")
    Rel(auth_handlers, email_svc, "sends emails via", "IEmailService")

    Rel(lgpd_handlers, audit_svc, "logs data-access events via", "IAuditLogService")

    Rel(secrets_provider, secrets, "GetSecretValue at startup", "HTTPS")
    Rel(user_repo, dynamo, "GetItem / PutItem / UpdateItem / Query", "HTTPS")
    Rel(audit_svc, dynamo, "SaveAsync (AUDIT# item)", "HTTPS")
    Rel(email_svc, ses, "SendEmail", "HTTPS")
```

## Layer Dependency Direction

```
Api  ──▶  Application  ──▶  Domain  ◀──  Infrastructure
```

- **Domain** has no framework dependencies — entities, value objects, interfaces, constants only.
- **Application** depends on Domain interfaces; never on Infrastructure directly.
- **Infrastructure** implements Domain interfaces; never referenced by Application.
- **Api** wires everything via IoC and routes requests through handlers.

## Key Patterns

| Pattern | Where applied |
|---|---|
| `IHandler<TRequest, TResponse>` returning `ErrorOr<T>` | All use-case handlers |
| Static factory `Create(...)`, private setters | `UserEntity` and all value objects |
| Validator injected into handler constructor | Each handler validates its own request |
| Fire-and-forget audit (exception swallowed) | `GetProfileHandler`, `ExportDataHandler`, `DeleteAccountHandler` |
| Fake services in `CustomWebApplicationFactory` | `FakeTokenService`, `FakeEmailService`, `FakeAuditLogService` |
