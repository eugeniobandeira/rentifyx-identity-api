# RentifyX · identity-api · Progress Audit

> Gerado em 27 jun 2026 — baseado em scan de 63 arquivos fonte contra o plano de 148 tarefas.

---

## Resumo executivo

| Categoria | Tarefas |
|---|---|
| ✅ Confirmadas concluídas | ~43 |
| ⚠️ Incertas (sem acesso ao conteúdo) | ~3 |
| ❌ Não iniciadas | ~102 |
| 💡 Desvios de design positivos | 2 |

**Progresso geral estimado: ~29% (43/148)** — atualizado em 2026-06-27 após leitura do código

---

## E-01 · Project Foundation & DevSecOps Pipeline — ~78%

### F-01 · Repo & Solution Structure

| Task | Título | Status |
|---|---|---|
| T-001 | `dotnet new clean-arch -n RentifyX.Identity` | ✅ done |
| T-002 | [AUTO] Solution scaffold: API / Application / Domain / Infrastructure / Tests | ✅ done |
| T-003 | [AUTO] `Directory.Packages.props` — centralized versioning | ✅ done |
| T-004 | [AUTO] `Directory.Build.props` (Nullable, TreatWarningsAsErrors, LangVersion) | ✅ done |
| T-005 | [AUTO] .NET Aspire AppHost + ServiceDefaults | ✅ done |
| T-006 | [AUTO] SonarAnalyzer.CSharp wired globalmente | ✅ done |
| T-007 | [AUTO] Serilog + CorrelationId middleware + GlobalExceptionHandler | ✅ done |
| T-008 | [AUTO] OpenTelemetry traces + metrics via ServiceDefaults | ✅ done |
| T-009 | [AUTO] Health checks `/health/live` + `/health/ready` | ✅ done |
| T-010 | [AUTO] Scalar UI + endpoint auto-discovery via reflection | ✅ done |
| T-011 | [AUTO] `ErrorOr<T>` como tipo de retorno padrão | ✅ done |
| T-012 | `.editorconfig` com regras CA5xxx de segurança | ✅ done |
| T-013 | LocalStack container no AppHost (DynamoDB, SES, SM, KMS) | ⚠️ uncertain |
| T-014 | cognito-local Docker container (porta 9229) | ⚠️ uncertain |
| T-015 | Init scripts LocalStack: tabelas DynamoDB, SES verified email, KMS key | ⚠️ uncertain |
| T-016 | Validar: `dotnet run --project AppHost` sobe os 3 containers limpo | ⚠️ uncertain |

### F-02 · CI/CD Pipeline & DevSecOps Baseline

| Task | Título | Status |
|---|---|---|
| T-017 | [AUTO] GitHub Actions: build → test (`ci.yml`) | ✅ done |
| T-018 | Coverage gate ≥80% (coverlet + ReportGenerator) | ❌ pending |
| T-019 | OWASP dependency-check step (NuGet vulnerability scan) | ❌ pending |
| T-020 | Trivy container scan do Docker image | ❌ pending |
| T-021 | Branch protection: CI green + 1 PR review antes do merge | ⚠️ uncertain |
| T-022 | gitleaks pre-commit hook + `.gitleaks.toml` (10 regras customizadas) | ✅ done |
| T-023 | `ISecretsProvider` abstraction na Infrastructure layer | ⚠️ uncertain |
| T-024 | `AWSSDK.SecretsManager` — carregar JWT key + Cognito secrets no startup | ❌ pending |
| T-025 | ADR-001: Secrets Manager over appsettings | ✅ done |

> **Nota:** T-018, T-019 e T-020 estão explicitamente marcados como "planned" no `CLAUDE.md`. Fechar esses 3 gates **antes** de avançar para a Week 2.

---

## E-02 · Domain Model & Core Identity Logic — ~55%

### F-03 · User Aggregate & Value Objects

| Task | Título | Status | Arquivo |
|---|---|---|---|
| T-026 | User aggregate root (Id, Email, CPF, PasswordHash, Role, Status, CreatedAt) | ✅ done | `UserEntity.cs` |
| T-027 | Email value object (format + domain validation) | ✅ done | `ValueObjects/Email.cs` |
| T-028 | CPF value object → implementado como `TaxDocument<TaxDocumentType>` ⭐ | ✅ done | `ValueObjects/TaxDocument.cs` · `Enums/TaxDocumentType.cs` |
| T-029 | Password value object (min 12 chars, upper, lower, digit, symbol) | ✅ done | `ValueObjects/Password.cs` |
| T-030 | Role enum: Owner \| Renter \| Admin | ✅ done | `Enums/UserRole.cs` |
| T-031 | UserStatus enum: PendingVerification \| Active \| Suspended \| Deleted | ✅ done | `Enums/UserStatus.cs` |

> ⭐ **Desvio de design positivo:** `TaxDocument<TaxDocumentType>` em vez de `Cpf` — acomoda CNPJ (pessoa jurídica) sem refactor futuro. Correto para uma plataforma onde tanto PF quanto PJ podem ser locadores.

### F-03 · Domain Events

| Task | Título | Status | Arquivo |
|---|---|---|---|
| T-032 | `IEvent` + `IDomainEvent` interfaces no Domain layer | ⚠️ uncertain | — |
| T-033 | `UserRegistered` domain event (UserId, Email, Role, OccurredAt) | ✅ done | `Events/UserRegistered.cs` |
| T-034 | `UserEmailVerified` domain event | ❌ pending | — |
| T-035 | `UserPasswordChanged` domain event | ❌ pending | — |
| T-036 | `UserSuspended` domain event (reason, suspendedBy) | ❌ pending | — |
| T-037 | `RaiseDomainEvent()` no AggregateRoot base class | ⚠️ uncertain | — |

### F-04 · Domain Services & Repository Contracts

| Task | Título | Status | Arquivo |
|---|---|---|---|
| T-038 | `IUserRepository`: GetById, GetByEmail, GetByCPF, Save, SoftDelete | ✅ done | `Interfaces/Users/IUserRepository.cs` |
| T-039 | `ITokenService`: GenerateAccessToken, GenerateRefreshToken, ValidateToken | ❌ pending | — |
| T-040 | `IPasswordHasher`: Hash, Verify — BCrypt.Net-Next já está no Packages.props | ❌ pending | — |
| T-041 | `IEmailVerificationService` → implementado como `IEmailService` ⭐ | ✅ done | `Interfaces/Users/IEmailService.cs` |
| T-042 | `IConsentRepository`: Record, GetLatest (LGPD Art. 8) | ❌ pending | — |

> ⭐ **Desvio de design positivo:** `IEmailService` é uma abstração mais limpa que `IEmailVerificationService` — cobre verificação, boas-vindas, reset de senha e notificações futuras com uma interface só.

### F-04 · Unit Tests & ADRs

| Task | Título | Status |
|---|---|---|
| T-043–047 | Unit tests: Email VO, TaxDocument VO, Password VO, User aggregate, domain events | ✅ done |
| T-048 | ADR-002: TaxDocument como campo de identidade — rationale LGPD | ✅ done |
| T-049 | ADR-003: ErrorOr\<T\> over exceptions | ✅ done |
| T-050 | ADR-004: Domain events over direct service calls | ✅ done |
| T-051 | Review: zero framework deps + zero AWS refs no Domain layer | ✅ done |

---

## E-03 · Application Layer — Use Cases — ~18%

### F-05 · Registration & Email Verification

| Task | Título | Status | Arquivo |
|---|---|---|---|
| T-052 | [AUTO] Feature folder structure: `Application/Features/Identity/` | ✅ done | `Features/Identity/Auth/Register/` |
| T-053 | `RegisterUserRequest` + `RegisterUserHandler` (IHandler\<RegisterUserRequest, UserResponse\>) | ✅ done | `RegisterUserHandler.cs` · `RegisterUserRequest.cs` · `UserResponse.cs` · `UserMapper.cs` |
| T-054 | `RegisterUserValidator` (FluentValidation): Email, CPF, Password, Role | ✅ done | `RegisterUserValidator.cs` |
| T-055 | Idempotency check: rejeitar Email ou CPF duplicado (LGPD Article 46) | ✅ done |
| T-056 | Publicar `UserRegistered` no Kafka Outbox | ❌ pending |
| T-057 | Unit tests: RegisterUserHandler — sucesso + todos os caminhos de falha | ✅ done |
| T-058–062 | `VerifyEmailHandler` + token HMAC-SHA256 + transição de status + testes | ❌ pending |

### F-06 · Authentication

| Task | Título | Status |
|---|---|---|
| T-063–067 | `LoginHandler` + rate limiting (5 tentativas / 15 min lock) + JWT + testes | ❌ pending |
| T-068–072 | `RefreshTokenHandler` + rotação + blacklist + revogação + testes | ❌ pending |
| T-073–077 | `RequestPasswordResetHandler` + `ConfirmPasswordResetHandler` + testes | ❌ pending |

---

## E-04 · Infrastructure Layer — AWS Integration — 0%

> ⚠️ **Nenhum AWSSDK presente no `Directory.Packages.props`.** `UserRepository.cs` e `EmailService.cs` existem como stubs mas sem implementação real.

| Task | Título | Status |
|---|---|---|
| T-078–082 | `DynamoDbUserRepository` + single-table design + KMS para CPF + Testcontainers | ❌ pending |
| T-083–087 | Outbox Pattern: `OutboxMessage` entity + `OutboxPublisher` + DLQ + testes | ❌ pending |
| T-088–091 | Cognito: `CognitoTokenService` + JWT validator middleware + Google OAuth stub + ADR-006 | ❌ pending |
| T-092–095 | SES: `SesEmailSender` + templates (Welcome, Verification, PasswordReset) + MockSender + testes | ❌ pending |
| T-096–099 | `SecretsManagerConfigurationProvider` + key rotation + cache (5 min) + testes | ❌ pending |

**Packages a adicionar no `Directory.Packages.props` para iniciar E-04:**

```xml
<!-- AWS SDK -->
<PackageVersion Include="AWSSDK.DynamoDBv2" Version="3.7.*" />
<PackageVersion Include="AWSSDK.SecretsManager" Version="3.7.*" />
<PackageVersion Include="AWSSDK.SimpleEmailV2" Version="3.7.*" />
<PackageVersion Include="AWSSDK.CognitoIdentityProvider" Version="3.7.*" />
<PackageVersion Include="AWSSDK.KeyManagementService" Version="3.7.*" />

<!-- Testcontainers -->
<PackageVersion Include="Testcontainers" Version="3.*" />
<PackageVersion Include="Testcontainers.LocalStack" Version="3.*" />

<!-- Messaging (Outbox) -->
<PackageVersion Include="Confluent.Kafka" Version="2.*" />
```

---

## E-05 · API Layer — Endpoints, Security & LGPD — ~12%

### F-09 · Minimal API Endpoints

| Task | Título | Status | Arquivo |
|---|---|---|---|
| T-100 | [AUTO] Endpoint auto-registration via `IEndpoint` reflection | ✅ done | `EndpointExtensions.cs` · `IEndpoint.cs` · `Tags.cs` |
| T-101 | `POST /v1/api/auth/register` | ✅ done | `Endpoints/Auth/Register.cs` |
| T-107 | [AUTO] `GlobalExceptionHandler`: sem stack trace nas respostas (OWASP A05) | ✅ done | `Middlewares/GlobalExceptionHandler.cs` |
| T-108 | [AUTO] `CorrelationId` middleware: X-Correlation-Id em todos os logs | ✅ done | `Middlewares/CorrelationIdMiddleware.cs` |
| T-109 | Rate limiting middleware (IP-based + user-based) | ✅ done | `Extensions/RateLimitExtension.cs` |
| T-102 | `POST /v1/api/auth/verify-email` | ❌ pending |
| T-103 | `POST /v1/api/auth/login` | ❌ pending |
| T-104 | `POST /v1/api/auth/refresh` | ❌ pending |
| T-105 | `POST /v1/api/auth/logout` (requer auth) | ❌ pending |
| T-106 | `POST /v1/api/auth/forgot-password` + `POST /v1/api/auth/reset-password` | ❌ pending |
| T-110–111 | Security headers (HSTS, X-Content-Type-Options, X-Frame-Options, CSP) + request size limit | ⚠️ uncertain |

### F-10 · LGPD Compliance Layer

| Task | Título | Status |
|---|---|---|
| T-112–116 | Endpoints de direitos do usuário (GET/DELETE/export `/users/me`) + anonimização + audit log | ❌ pending |
| T-117–120 | DynamoDB TTL policies + `ConsentRecord` entity + ADR-007 | ❌ pending |
| T-121 | [AUTO] Scalar UI em `/scalar` com OpenAPI 3.1 | ✅ done |
| T-122–124 | Exemplos de request/response no OpenAPI + XML doc comments + ReDoc em `/redoc` | ❌ pending |

---

## E-06 · Infrastructure as Code & Production Readiness — skeleton

> `iac/` e `k8s/base + overlays/dev + overlays/prod` existem como diretórios. Todo conteúdo pendente.

| Task | Título | Status |
|---|---|---|
| T-125–130 | Terraform: Cognito, DynamoDB, KMS, Secrets Manager, SES, IAM (IRSA) | ❌ pending |
| T-131–135 | Helm chart: Deployment, HPA, probes, resource limits, PDB + ADR-008 | ❌ pending |
| T-136–139 | SLOs: login p99 \< 300 ms, availability \> 99.9%, error rate \< 0.1% + Datadog + OTEL metrics + alertas | ❌ pending |
| T-140–143 | C4 diagrams (Context, Container, Component) + ADRs 001–008 finalizados | ❌ pending |
| T-144–148 | OWASP ZAP scan + verificação de secrets + LGPD endpoints + coverage final + tag v1.0.0 | ❌ pending |

---

## Próximas ações prioritárias

### Antes de avançar para o Login (Day 11)

Estes 5 arquivos desbloqueiam toda a Week 3:

```
02-src/03-Domain/RentifyxIdentity.Domain/Interfaces/Users/ITokenService.cs
02-src/03-Domain/RentifyxIdentity.Domain/Interfaces/Users/IPasswordHasher.cs
02-src/03-Domain/RentifyxIdentity.Domain/Events/UserEmailVerified.cs
02-src/03-Domain/RentifyxIdentity.Domain/Events/UserPasswordChanged.cs
02-src/03-Domain/RentifyxIdentity.Domain/Events/UserSuspended.cs
```

### CI gates pendentes (T-018, T-019, T-020)

Adicionar ao `ci.yml` antes de qualquer merge para `main`:

```yaml
- name: Coverage gate
  run: dotnet test --collect:"XPlat Code Coverage" && reportgenerator ...

- name: OWASP Dependency Check
  uses: dependency-check/Dependency-Check_Action@main
  with:
    format: SARIF
    failBuildOnCVSS: 7

- name: Trivy container scan
  uses: aquasecurity/trivy-action@master
  with:
    image-ref: rentifyx-identity-api:latest
    severity: HIGH,CRITICAL
    exit-code: 1
```

### Branch protection (T-021)

Settings → Branches → `main`:
- Require status checks: `ci / secret-scan`, `ci / build-and-test`
- Require 1 approving review
- Dismiss stale reviews on new push

---

## Legenda

| Símbolo | Significado |
|---|---|
| ✅ done | Confirmado no scan de arquivos |
| ⚠️ uncertain | Arquivo existe mas conteúdo não verificado, ou fora do escopo do scan |
| ❌ pending | Ausente no repo — não iniciado |
| ⭐ | Desvio de design positivo em relação ao plano original |
