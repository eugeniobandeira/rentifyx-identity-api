import { useState } from "react";

const STACK = {
  framework: ".NET 10 · Minimal APIs",
  arch:      "Clean Architecture · DDD · TDD",
  cloud:     "AWS Cognito · Secrets Manager · DynamoDB · SES · KMS",
  infra:     "Terraform · .NET Aspire · Docker · GitHub Actions",
  obs:       "OpenTelemetry · Serilog · Scalar/ReDoc",
  compliance:"LGPD · OWASP Top 10 · BACEN · DevSecOps",
  template:  "dotnet new clean-arch -n RentifyX.Identity",
};

// T = template pre-done, source = "template" marks it visually
const PLAN = [
  {
    id: "E-01", type: "epic",
    title: "Project Foundation & DevSecOps Pipeline",
    color: "#4f6ef7", week: 1, days: "Day 1–3",
    goal: "Template gives you Day 1–2 for free — focus on CI security gates & secrets",
    features: [
      {
        id: "F-01", title: "Repo & Solution Structure",
        stories: [
          {
            id: "US-001",
            title: "As a dev, I want a clean solution scaffold so I can start coding without friction",
            tasks: [
              { id: "T-001", day: 1, source: "template", title: "Run: dotnet new install EugenioBandeira.CleanArchTemplate && dotnet new clean-arch -n RentifyX.Identity" },
              { id: "T-002", day: 1, source: "template", title: "[AUTO] Solution scaffold: API, Application, Domain, Infrastructure, Tests layers" },
              { id: "T-003", day: 1, source: "template", title: "[AUTO] Directory.Packages.props with centralized versioning" },
              { id: "T-004", day: 1, source: "template", title: "[AUTO] Directory.Build.props (Nullable, TreatWarningsAsErrors)" },
              { id: "T-005", day: 1, source: "template", title: "[AUTO] .NET Aspire AppHost + ServiceDefaults projects" },
              { id: "T-006", day: 1, source: "template", title: "[AUTO] SonarAnalyzer.CSharp wired across all projects" },
              { id: "T-007", day: 1, source: "template", title: "[AUTO] Serilog + CorrelationId middleware + GlobalExceptionHandler" },
              { id: "T-008", day: 1, source: "template", title: "[AUTO] OpenTelemetry traces + metrics via ServiceDefaults" },
              { id: "T-009", day: 1, source: "template", title: "[AUTO] Health checks /health/live + /health/ready" },
              { id: "T-010", day: 1, source: "template", title: "[AUTO] Scalar UI + endpoint auto-discovery via reflection" },
              { id: "T-011", day: 1, source: "template", title: "[AUTO] ErrorOr<T> as standard result type" },
              { id: "T-012", day: 1, source: "template", title: "Copy updated .editorconfig into repo (11/10 version with CA5xxx security rules)" },
            ],
          },
          {
            id: "US-002",
            title: "As a dev, I want AWS containers in Aspire so I can run DynamoDB, SES, Cognito locally with one command",
            tasks: [
              { id: "T-013", day: 1, title: "Add LocalStack container to AppHost (DynamoDB, S3, SES, SecretsManager, KMS)" },
              { id: "T-014", day: 1, title: "Add cognito-local Docker container to AppHost (port 9229)" },
              { id: "T-015", day: 1, title: "Add LocalStack init scripts: create DynamoDB tables, SES verified email, KMS key" },
              { id: "T-016", day: 1, title: "Validate: dotnet run --project AppHost boots all 3 containers cleanly" },
            ],
          },
        ],
      },
      {
        id: "F-02", title: "CI/CD Pipeline & DevSecOps Baseline",
        stories: [
          {
            id: "US-003",
            title: "As a tech lead, I want automated security gates in CI so vulnerabilities never reach main",
            tasks: [
              { id: "T-017", day: 2, source: "template", title: "[AUTO] GitHub Actions base workflow: build → test" },
              { id: "T-018", day: 2, title: "Extend CI: add coverage gate ≥80% (coverlet + ReportGenerator)" },
              { id: "T-019", day: 2, title: "Add OWASP dependency-check step (NuGet vulnerability scan)" },
              { id: "T-020", day: 2, title: "Add Trivy container scan step for Docker image" },
              { id: "T-021", day: 2, title: "Configure branch protection: require CI green + 1 PR review before merge to main" },
            ],
          },
          {
            id: "US-004",
            title: "As a dev, I want secrets never committed to Git so we comply with OWASP A02",
            tasks: [
              { id: "T-022", day: 3, title: "Add git-secrets pre-commit hook + .gitsecrets patterns (AWS keys, JWT secrets)" },
              { id: "T-023", day: 3, title: "Add ISecretsProvider abstraction in Infrastructure layer" },
              { id: "T-024", day: 3, title: "Configure AWSSDK.SecretsManager — load JWT signing key + Cognito secrets at startup" },
              { id: "T-025", day: 3, title: "Document ADR-001: Secrets Manager over appsettings for all sensitive config" },
            ],
          },
        ],
      },
    ],
  },
  {
    id: "E-02", type: "epic",
    title: "Domain Model & Core Identity Logic",
    color: "#14b8a6", week: 2, days: "Day 4–8",
    goal: "Pure domain — no frameworks, no AWS, fully unit-tested",
    features: [
      {
        id: "F-03", title: "User Aggregate & Value Objects",
        stories: [
          {
            id: "US-005",
            title: "As a domain expert, I want a rich User aggregate that enforces business rules so the domain is self-protecting",
            tasks: [
              { id: "T-026", day: 4, title: "Define User aggregate root: Id, Email, CPF, PasswordHash, Role, Status, CreatedAt" },
              { id: "T-027", day: 4, title: "Create Email value object: format + domain validation (LGPD data minimization)" },
              { id: "T-028", day: 4, title: "Create CPF value object: digit verification algorithm + masked display" },
              { id: "T-029", day: 4, title: "Create Password value object: min 12 chars, upper, lower, digit, symbol (OWASP A07)" },
              { id: "T-030", day: 4, title: "Create Role enum: Owner | Renter | Admin" },
              { id: "T-031", day: 4, title: "Create UserStatus enum: PendingVerification | Active | Suspended | Deleted" },
            ],
          },
          {
            id: "US-006",
            title: "As a dev, I want domain events so other services react to identity changes via Kafka",
            tasks: [
              { id: "T-032", day: 5, title: "Define IEvent + IDomainEvent interfaces in Domain layer" },
              { id: "T-033", day: 5, title: "Create UserRegistered domain event (UserId, Email, Role, OccurredAt)" },
              { id: "T-034", day: 5, title: "Create UserEmailVerified domain event" },
              { id: "T-035", day: 5, title: "Create UserPasswordChanged domain event" },
              { id: "T-036", day: 5, title: "Create UserSuspended domain event (reason, suspendedBy)" },
              { id: "T-037", day: 5, title: "Add RaiseDomainEvent() to AggregateRoot base class" },
            ],
          },
        ],
      },
      {
        id: "F-04", title: "Domain Services & Repository Contracts",
        stories: [
          {
            id: "US-007",
            title: "As a dev, I want domain-layer contracts so Infrastructure can be swapped without touching business logic",
            tasks: [
              { id: "T-038", day: 6, title: "Define IUserRepository: GetById, GetByEmail, GetByCPF, Save, SoftDelete" },
              { id: "T-039", day: 6, title: "Define ITokenService: GenerateAccessToken, GenerateRefreshToken, ValidateToken" },
              { id: "T-040", day: 6, title: "Define IPasswordHasher: Hash, Verify" },
              { id: "T-041", day: 6, title: "Define IEmailVerificationService: GenerateToken, Validate" },
              { id: "T-042", day: 6, title: "Define IConsentRepository: Record, GetLatest (LGPD Art. 8)" },
            ],
          },
          {
            id: "US-008",
            title: "As a dev, I want 100% unit-tested domain layer with no I/O so tests run in milliseconds",
            tasks: [
              { id: "T-043", day: 7, title: "Unit tests: Email VO — valid, invalid format, disposable domain rejection" },
              { id: "T-044", day: 7, title: "Unit tests: CPF VO — valid, invalid check digits, masked display" },
              { id: "T-045", day: 7, title: "Unit tests: Password VO — all rule combinations, edge cases" },
              { id: "T-046", day: 7, title: "Unit tests: User aggregate — state transitions, domain event emission" },
              { id: "T-047", day: 7, title: "Unit tests: Domain events — correct payload, OccurredAt, zero framework deps" },
            ],
          },
          {
            id: "US-009",
            title: "As an architect, I want ADRs for every key domain decision",
            tasks: [
              { id: "T-048", day: 8, title: "ADR-002: CPF as identity field — LGPD data minimization rationale" },
              { id: "T-049", day: 8, title: "ADR-003: ErrorOr<T> over exceptions for control flow" },
              { id: "T-050", day: 8, title: "ADR-004: Domain events over direct service calls" },
              { id: "T-051", day: 8, title: "Review: zero framework dependencies + zero AWS references in Domain layer" },
            ],
          },
        ],
      },
    ],
  },
  {
    id: "E-03", type: "epic",
    title: "Application Layer — Use Cases",
    color: "#f0a500", week: 3, days: "Day 9–13",
    goal: "All identity use cases implemented via IHandler<TRequest,TResponse> — pattern scaffolded by template",
    features: [
      {
        id: "F-05", title: "Registration & Email Verification Flow",
        stories: [
          {
            id: "US-010",
            title: "As a new user, I want to register so I can access the RentifyX platform",
            tasks: [
              { id: "T-052", day: 9,  source: "template", title: "[AUTO] Feature folder structure: Application/Features/Identity/Register/" },
              { id: "T-053", day: 9,  title: "Create RegisterUserRequest + RegisterUserHandler (IHandler<RegisterUserRequest, UserResponse>)" },
              { id: "T-054", day: 9,  title: "Add RegisterUserValidator (FluentValidation): Email, CPF, Password, Role rules" },
              { id: "T-055", day: 9,  title: "Idempotency check: reject duplicate Email or CPF (LGPD Article 46)" },
              { id: "T-056", day: 9,  title: "Publish UserRegistered to Kafka Outbox on success" },
              { id: "T-057", day: 9,  title: "Unit tests: RegisterUserHandler — success + all failure paths" },
            ],
          },
          {
            id: "US-011",
            title: "As a registered user, I want to verify my email so my account becomes active",
            tasks: [
              { id: "T-058", day: 10, title: "Create VerifyEmailRequest + VerifyEmailHandler" },
              { id: "T-059", day: 10, title: "Implement time-limited token (24h expiry, HMAC-SHA256 signed)" },
              { id: "T-060", day: 10, title: "Transition User: PendingVerification → Active on valid token" },
              { id: "T-061", day: 10, title: "Publish UserEmailVerified domain event" },
              { id: "T-062", day: 10, title: "Unit tests: token expiry, reuse prevention, wrong user" },
            ],
          },
        ],
      },
      {
        id: "F-06", title: "Authentication — Login & Token Management",
        stories: [
          {
            id: "US-012",
            title: "As a user, I want to log in and receive a JWT so I can call protected APIs",
            tasks: [
              { id: "T-063", day: 11, title: "Create LoginRequest + LoginHandler" },
              { id: "T-064", day: 11, title: "Rate limiting: max 5 failed attempts → account lock 15min (OWASP A07)" },
              { id: "T-065", day: 11, title: "Generate Access Token (15min TTL) + Refresh Token (7d TTL, stored in DynamoDB)" },
              { id: "T-066", day: 11, title: "Signing key loaded from AWS Secrets Manager — never hardcoded" },
              { id: "T-067", day: 11, title: "Unit tests: correct credentials, wrong password, locked account, unverified email" },
            ],
          },
          {
            id: "US-013",
            title: "As a user, I want to refresh my token so I stay logged in without re-entering credentials",
            tasks: [
              { id: "T-068", day: 12, title: "Create RefreshTokenRequest + handler (validate, rotate, blacklist old)" },
              { id: "T-069", day: 12, title: "Refresh token rotation: one-time use, new token on each refresh" },
              { id: "T-070", day: 12, title: "Store refresh token hash (not plaintext) in DynamoDB with TTL" },
              { id: "T-071", day: 12, title: "Create RevokeTokenRequest (logout): mark refresh token as revoked" },
              { id: "T-072", day: 12, title: "Unit tests: rotation, reuse attack detection, revocation" },
            ],
          },
          {
            id: "US-014",
            title: "As a user, I want to reset my password securely so I can recover my account",
            tasks: [
              { id: "T-073", day: 13, title: "Create RequestPasswordResetRequest: generate signed reset token" },
              { id: "T-074", day: 13, title: "Create ConfirmPasswordResetRequest: validate + update hash" },
              { id: "T-075", day: 13, title: "Reset token: single-use, 1h expiry, HMAC-SHA256 signed" },
              { id: "T-076", day: 13, title: "Publish UserPasswordChanged domain event" },
              { id: "T-077", day: 13, title: "Unit tests: expired token, reuse, wrong user, weak new password" },
            ],
          },
        ],
      },
    ],
  },
  {
    id: "E-04", type: "epic",
    title: "Infrastructure Layer — AWS Integration",
    color: "#a855f7", week: 4, days: "Day 14–18",
    goal: "DynamoDB, Cognito, SES, Secrets Manager all wired and integration-tested via Testcontainers",
    features: [
      {
        id: "F-07", title: "DynamoDB Repository & Outbox",
        stories: [
          {
            id: "US-015",
            title: "As a dev, I want a DynamoDB-backed user repository so user data persists in AWS",
            tasks: [
              { id: "T-078", day: 14, title: "Implement DynamoDbUserRepository: Save, GetById, GetByEmail, GetByCPF, SoftDelete" },
              { id: "T-079", day: 14, title: "DynamoDB single-table design: PK=USER#{id}, GSI1=EMAIL#{email}, GSI2=CPF#{cpf}" },
              { id: "T-080", day: 14, title: "Encrypt CPF at rest via AWS KMS before storing (LGPD Article 46)" },
              { id: "T-081", day: 14, title: "Testcontainers.DynamoDb + LocalStack integration tests for all repository methods" },
              { id: "T-082", day: 14, title: "ADR-005: Single-table DynamoDB design rationale" },
            ],
          },
          {
            id: "US-016",
            title: "As a dev, I want the Outbox Pattern so domain events are never lost even if Kafka is down",
            tasks: [
              { id: "T-083", day: 15, title: "Create OutboxMessage entity (Id, EventType, Payload, Status, CreatedAt, ProcessedAt)" },
              { id: "T-084", day: 15, title: "Store outbox record in DynamoDB atomically with user save" },
              { id: "T-085", day: 15, title: "Implement OutboxPublisher (IHostedService): poll → publish to Kafka → mark processed" },
              { id: "T-086", day: 15, title: "Dead-letter handling: after 3 retries → move to DLQ Kafka topic" },
              { id: "T-087", day: 15, title: "Integration tests: outbox persists on save, publisher delivers to Kafka" },
            ],
          },
        ],
      },
      {
        id: "F-08", title: "Cognito, SES & Secrets Manager",
        stories: [
          {
            id: "US-017",
            title: "As a dev, I want Cognito as the second auth layer for MFA and social login",
            tasks: [
              { id: "T-088", day: 16, title: "Implement CognitoTokenService: InitiateAuth + AdminCreateUser wrappers" },
              { id: "T-089", day: 16, title: "Add CognitoJwtValidator middleware: validate Cognito tokens in ASP.NET pipeline" },
              { id: "T-090", day: 16, title: "Google OAuth stub via Cognito Identity Provider" },
              { id: "T-091", day: 16, title: "ADR-006: Custom JWT for internal service auth, Cognito for user-facing auth" },
            ],
          },
          {
            id: "US-018",
            title: "As a dev, I want SES email sending abstracted so templates can change without code changes",
            tasks: [
              { id: "T-092", day: 17, title: "Implement SesEmailSender: send via AWS SES" },
              { id: "T-093", day: 17, title: "Create email templates: WelcomeEmail, VerificationEmail, PasswordResetEmail" },
              { id: "T-094", day: 17, title: "MockEmailSender for local + test environments" },
              { id: "T-095", day: 17, title: "Testcontainers.LocalStack integration test for SES send" },
            ],
          },
          {
            id: "US-019",
            title: "As a security engineer, I want all secrets from Secrets Manager at startup",
            tasks: [
              { id: "T-096", day: 18, title: "SecretsManagerConfigurationProvider: load secrets into IConfiguration on startup" },
              { id: "T-097", day: 18, title: "JWT signing key rotation: fetch latest version on each startup" },
              { id: "T-098", day: 18, title: "Secret caching (5min TTL) to avoid Secrets Manager rate limits" },
              { id: "T-099", day: 18, title: "Integration test: service boots with secrets, rejects missing secrets gracefully" },
            ],
          },
        ],
      },
    ],
  },
  {
    id: "E-05", type: "epic",
    title: "API Layer — Endpoints, Security & LGPD Compliance",
    color: "#10b981", week: 5, days: "Day 19–23",
    goal: "All endpoints live, OWASP hardened, LGPD rights implemented, fully documented",
    features: [
      {
        id: "F-09", title: "Minimal API Endpoints",
        stories: [
          {
            id: "US-020",
            title: "As a client, I want clean REST endpoints so I can integrate identity into other services",
            tasks: [
              { id: "T-100", day: 19, source: "template", title: "[AUTO] Endpoint auto-registration via IEndpoint reflection — no manual wiring" },
              { id: "T-101", day: 19, title: "Create Api/Endpoints/Auth/Register.cs → POST /v1/api/auth/register" },
              { id: "T-102", day: 19, title: "Create Api/Endpoints/Auth/VerifyEmail.cs → POST /v1/api/auth/verify-email" },
              { id: "T-103", day: 19, title: "Create Api/Endpoints/Auth/Login.cs → POST /v1/api/auth/login" },
              { id: "T-104", day: 19, title: "Create Api/Endpoints/Auth/Refresh.cs → POST /v1/api/auth/refresh" },
              { id: "T-105", day: 19, title: "Create Api/Endpoints/Auth/Logout.cs → POST /v1/api/auth/logout (requires auth)" },
              { id: "T-106", day: 19, title: "Create Api/Endpoints/Auth/ForgotPassword.cs + ResetPassword.cs" },
            ],
          },
          {
            id: "US-021",
            title: "As a security engineer, I want hardened HTTP middleware so OWASP Top 10 is addressed at transport level",
            tasks: [
              { id: "T-107", day: 20, source: "template", title: "[AUTO] GlobalExceptionHandler: no stack trace in responses (OWASP A05)" },
              { id: "T-108", day: 20, source: "template", title: "[AUTO] CorrelationId middleware: X-Correlation-Id flows through all logs" },
              { id: "T-109", day: 20, title: "Add rate limiting middleware: IP-based + user-based (AspNetCore.RateLimiting)" },
              { id: "T-110", day: 20, title: "Add security headers: HSTS, X-Content-Type-Options, X-Frame-Options, CSP" },
              { id: "T-111", day: 20, title: "Add request size limiting middleware (prevent oversized payload attacks)" },
            ],
          },
        ],
      },
      {
        id: "F-10", title: "LGPD Compliance Layer",
        stories: [
          {
            id: "US-022",
            title: "As a LGPD officer, I want user data rights endpoints so we comply with Articles 17–22",
            tasks: [
              { id: "T-112", day: 21, title: "GET /v1/api/users/me → return own profile (data access right, Art. 18)" },
              { id: "T-113", day: 21, title: "DELETE /v1/api/users/me → soft delete + anonymize PII (right to erasure, Art. 18)" },
              { id: "T-114", day: 21, title: "GET /v1/api/users/me/data-export → return all stored data as JSON (Art. 18 IV)" },
              { id: "T-115", day: 21, title: "PII anonymization: replace Email/CPF with hashed placeholders on deletion" },
              { id: "T-116", day: 21, title: "Audit log: every data access/change recorded with userId + timestamp + action" },
            ],
          },
          {
            id: "US-023",
            title: "As a dev, I want data retention policies auto-enforced so we never hold stale PII",
            tasks: [
              { id: "T-117", day: 22, title: "DynamoDB TTL on unverified accounts: auto-delete after 48h" },
              { id: "T-118", day: 22, title: "DynamoDB TTL on refresh tokens: auto-expire after 7 days" },
              { id: "T-119", day: 22, title: "ConsentRecord entity: store consent timestamp, IP, version (LGPD Art. 8)" },
              { id: "T-120", day: 22, title: "ADR-007: Data retention and anonymization strategy under LGPD" },
            ],
          },
          {
            id: "US-024",
            title: "As a dev, I want auto-generated API docs so consumers integrate without asking",
            tasks: [
              { id: "T-121", day: 23, source: "template", title: "[AUTO] Scalar UI at /scalar with OpenAPI 3.1 schema" },
              { id: "T-122", day: 23, title: "Add example request/response bodies to all endpoints in OpenAPI schema" },
              { id: "T-123", day: 23, title: "XML doc comments on all endpoint handlers" },
              { id: "T-124", day: 23, title: "Add ReDoc at /redoc as alternative documentation view" },
            ],
          },
        ],
      },
    ],
  },
  {
    id: "E-06", type: "epic",
    title: "Infrastructure as Code & Production Readiness",
    color: "#ef4444", week: 6, days: "Day 24–28",
    goal: "Full Terraform, Helm, SLOs, C4 diagrams, security review, tag v1.0.0",
    features: [
      {
        id: "F-11", title: "Terraform — Complete AWS Infrastructure",
        stories: [
          {
            id: "US-025",
            title: "As a DevOps engineer, I want 100% IaC so the entire identity infra is reproducible",
            tasks: [
              { id: "T-125", day: 24, title: "Terraform module: aws_cognito_user_pool (password policy, MFA, SES)" },
              { id: "T-126", day: 24, title: "Terraform module: aws_dynamodb_table (users, outbox, refresh tokens, audit log)" },
              { id: "T-127", day: 24, title: "Terraform module: aws_kms_key for CPF/PII encryption at rest" },
              { id: "T-128", day: 24, title: "Terraform module: aws_secretsmanager_secret (JWT key, Cognito client secret)" },
              { id: "T-129", day: 24, title: "Terraform module: aws_ses_domain_identity + verification" },
              { id: "T-130", day: 24, title: "Terraform: IAM roles with least-privilege policies for EKS service account (IRSA)" },
            ],
          },
        ],
      },
      {
        id: "F-12", title: "Kubernetes, Observability & Production Gate",
        stories: [
          {
            id: "US-026",
            title: "As a DevOps engineer, I want Helm charts so identity-api deploys to EKS with one command",
            tasks: [
              { id: "T-131", day: 25, title: "Helm chart: Deployment, Service, HPA (min 2 / max 10 replicas)" },
              { id: "T-132", day: 25, title: "Liveness + readiness probes → /health/live and /health/ready" },
              { id: "T-133", day: 25, title: "Resource requests/limits: memory 256Mi/512Mi, CPU 100m/500m" },
              { id: "T-134", day: 25, title: "PodDisruptionBudget: minAvailable=1 for zero-downtime deploys" },
              { id: "T-135", day: 25, title: "ADR-008: Kubernetes deployment strategy (RollingUpdate)" },
            ],
          },
          {
            id: "US-027",
            title: "As a dev, I want SLOs defined so we know when identity is degraded before users do",
            tasks: [
              { id: "T-136", day: 26, title: "Define SLOs: /login p99 < 300ms, availability > 99.9%, error rate < 0.1%" },
              { id: "T-137", day: 26, title: "Datadog dashboard: login latency, token issuance rate, error rate, lockouts" },
              { id: "T-138", day: 26, title: "Custom OTEL metrics: tokens_issued_total, login_failures_total, lockouts_total" },
              { id: "T-139", day: 26, title: "Alert: PagerDuty trigger if error rate > 1% for 5min" },
            ],
          },
          {
            id: "US-028",
            title: "As an architect, I want C4 diagrams + full ADR set so anyone understands identity at a glance",
            tasks: [
              { id: "T-140", day: 27, title: "C4 Context diagram: identity-api in RentifyX ecosystem" },
              { id: "T-141", day: 27, title: "C4 Container diagram: Minimal API + Application + Domain + Infrastructure" },
              { id: "T-142", day: 27, title: "C4 Component diagram: use cases, repositories, event publishers" },
              { id: "T-143", day: 27, title: "Finalize ADRs 001–008: review, cross-link, add to /docs/adr/" },
            ],
          },
          {
            id: "US-029",
            title: "As a tech lead, I want a final security review so we ship v1.0.0 with confidence",
            tasks: [
              { id: "T-144", day: 28, title: "Run OWASP ZAP scan against local env — fix all High/Critical findings" },
              { id: "T-145", day: 28, title: "Verify: no secrets in code, logs, or error responses (manual + automated)" },
              { id: "T-146", day: 28, title: "Verify: all 5 LGPD endpoints working (access, erasure, export, consent, audit)" },
              { id: "T-147", day: 28, title: "Final coverage run: enforce ≥80% across all layers" },
              { id: "T-148", day: 28, title: "Tag v1.0.0 → push Docker image to ECR → trigger staging deploy via GitHub Actions" },
            ],
          },
        ],
      },
    ],
  },
];

const SEVERITY = {
  template: { label: "TEMPLATE", color: "#f0a500", bg: "rgba(240,165,0,0.1)", border: "rgba(240,165,0,0.25)" },
  manual:   { label: "MANUAL",   color: "#4f6ef7", bg: "rgba(79,110,247,0.08)", border: "rgba(79,110,247,0.15)" },
};

const WEEK_SUMMARY = [
  { week:1, days:"1–3",  label:"Foundation",      saved:"~2 days", goal:"Template gives you structure/Aspire/Serilog/OTel for free — you focus on AWS containers + CI security gates" },
  { week:2, days:"4–8",  label:"Domain Model",    saved:"0",       goal:"Pure domain: User aggregate, CPF/Email/Password VOs, domain events — 100% unit tested, zero framework deps" },
  { week:3, days:"9–13", label:"Use Cases",        saved:"~1 day",  goal:"IHandler pattern + feature folder structure already scaffolded — fill in Register, Login, Refresh, Reset" },
  { week:4, days:"14–18",label:"AWS Integration",  saved:"0",       goal:"DynamoDB single-table, KMS for CPF, Outbox Pattern, Cognito federation, SES, Secrets Manager config" },
  { week:5, days:"19–23",label:"API & Compliance", saved:"~1 day",  goal:"Endpoint files are drop-ins (auto-registered) — focus on LGPD rights endpoints + security headers" },
  { week:6, days:"24–28",label:"IaC & Production", saved:"0",       goal:"Full Terraform, Helm/EKS, SLOs, C4 diagrams, OWASP ZAP scan, v1.0.0 ship" },
];

export default function IdentityPlan() {
  const [openEpics, setOpenEpics]       = useState({ "E-01": true });
  const [openFeatures, setOpenFeatures] = useState({});
  const [openStories, setOpenStories]   = useState({});
  const [done, setDone]                 = useState(() => {
    const pre = {};
    PLAN.forEach(e => e.features.forEach(f => f.stories.forEach(s => s.tasks.forEach(t => {
      if (t.source === "template") pre[t.id] = true;
    }))));
    return pre;
  });
  const [activeDay, setActiveDay]       = useState(null);
  const [view, setView]                 = useState("plan");

  const toggleDone = (id, e) => {
    e.stopPropagation();
    setDone(p => ({ ...p, [id]: !p[id] }));
  };

  const allTasks       = PLAN.flatMap(e => e.features.flatMap(f => f.stories.flatMap(s => s.tasks)));
  const templateTasks  = allTasks.filter(t => t.source === "template");
  const manualTasks    = allTasks.filter(t => t.source !== "template");
  const completedCount = allTasks.filter(t => done[t.id]).length;
  const progress       = Math.round((completedCount / allTasks.length) * 100);

  const tasksByDay = {};
  allTasks.forEach(t => {
    if (!tasksByDay[t.day]) tasksByDay[t.day] = [];
    tasksByDay[t.day].push(t);
  });

  const scoreColor = p => p >= 80 ? "#10b981" : p >= 40 ? "#f0a500" : "#4f6ef7";

  return (
    <div style={{ minHeight:"100vh", background:"#060b18", color:"#e8edf5", fontFamily:"'DM Sans','Segoe UI',sans-serif", paddingBottom:80 }}>

      {/* Header */}
      <div style={{
        borderBottom:"1px solid rgba(99,130,255,0.12)", padding:"0 32px", height:60,
        display:"flex", alignItems:"center", justifyContent:"space-between",
        background:"rgba(6,11,24,0.95)", position:"sticky", top:0, zIndex:20, backdropFilter:"blur(20px)",
      }}>
        <div style={{ display:"flex", alignItems:"center", gap:10 }}>
          <div style={{ width:30, height:30, borderRadius:7, background:"linear-gradient(135deg,#4f6ef7,#f0a500)", display:"flex", alignItems:"center", justifyContent:"center", fontSize:12, fontWeight:800 }}>RX</div>
          <span style={{ fontWeight:700, fontSize:14 }}>rentifyx-identity-api</span>
          <span style={{ color:"#4a5a75", fontSize:12 }}>/</span>
          <span style={{ color:"#7a8aaa", fontSize:12 }}>Project Plan</span>
        </div>
        <div style={{ display:"flex", gap:8, alignItems:"center" }}>
          <span style={{ fontSize:10, fontWeight:700, letterSpacing:1.5, color:"#f0a500", background:"rgba(240,165,0,0.1)", border:"1px solid rgba(240,165,0,0.25)", padding:"3px 10px", borderRadius:99 }}>
            TEMPLATE-AWARE
          </span>
          <span style={{ fontSize:11, color:"#4a5a75" }}>.NET 10 · Aspire · AWS · LGPD</span>
        </div>
      </div>

      <div style={{ maxWidth:1000, margin:"0 auto", padding:"32px 20px 0" }}>

        {/* Template bootstrap banner */}
        <div style={{
          background:"rgba(240,165,0,0.06)", border:"1px solid rgba(240,165,0,0.2)",
          borderRadius:12, padding:"16px 24px", marginBottom:24,
          display:"flex", gap:16, alignItems:"center", flexWrap:"wrap",
        }}>
          <div style={{ fontSize:20 }}>⚡</div>
          <div style={{ flex:1 }}>
            <div style={{ fontSize:13, fontWeight:800, color:"#f0a500", marginBottom:4 }}>Bootstrap with your template first</div>
            <code style={{ fontSize:12, color:"#b0bdd4", background:"#0d1425", padding:"4px 10px", borderRadius:6, display:"inline-block" }}>
              dotnet new install EugenioBandeira.CleanArchTemplate && dotnet new clean-arch -n RentifyX.Identity
            </code>
          </div>
          <div style={{ textAlign:"right", flexShrink:0 }}>
            <div style={{ fontSize:18, fontWeight:900, color:"#f0a500" }}>{templateTasks.length} tasks</div>
            <div style={{ fontSize:10, color:"#4a5a75" }}>pre-completed</div>
          </div>
        </div>

        {/* Stats */}
        <div style={{ display:"grid", gridTemplateColumns:"repeat(4,1fr)", gap:12, marginBottom:24 }}>
          {[
            { label:"Total Tasks",      value: allTasks.length,      color:"#e8edf5" },
            { label:"Template Free",    value: templateTasks.length, color:"#f0a500" },
            { label:"You Build",        value: manualTasks.length,   color:"#4f6ef7" },
            { label:"Days Saved",       value: "~4",                 color:"#10b981" },
          ].map(s => (
            <div key={s.label} style={{ background:"#0d1425", border:"1px solid rgba(99,130,255,0.12)", borderRadius:10, padding:"14px 18px" }}>
              <div style={{ fontSize:22, fontWeight:900, color:s.color, letterSpacing:"-1px" }}>{s.value}</div>
              <div style={{ fontSize:11, color:"#4a5a75", fontWeight:600, letterSpacing:0.5, marginTop:2 }}>{s.label}</div>
            </div>
          ))}
        </div>

        {/* Progress */}
        <div style={{ marginBottom:24 }}>
          <div style={{ display:"flex", justifyContent:"space-between", marginBottom:6 }}>
            <span style={{ fontSize:11, color:"#4a5a75", fontWeight:700, letterSpacing:1, textTransform:"uppercase" }}>Overall Progress</span>
            <span style={{ fontSize:11, color:scoreColor(progress), fontWeight:700 }}>{completedCount}/{allTasks.length} tasks · {progress}%</span>
          </div>
          <div style={{ height:6, background:"#111d35", borderRadius:99, overflow:"hidden", position:"relative" }}>
            {/* template portion */}
            <div style={{ position:"absolute", left:0, top:0, height:"100%", width:`${Math.round(templateTasks.length/allTasks.length*100)}%`, background:"rgba(240,165,0,0.4)", borderRadius:99 }} />
            {/* completed portion */}
            <div style={{ position:"absolute", left:0, top:0, height:"100%", width:`${progress}%`, background:"linear-gradient(90deg,#f0a500,#4f6ef7)", borderRadius:99, transition:"width 0.4s ease" }} />
          </div>
          <div style={{ display:"flex", gap:16, marginTop:8 }}>
            <div style={{ display:"flex", alignItems:"center", gap:6 }}>
              <div style={{ width:10, height:10, borderRadius:2, background:"#f0a500" }} />
              <span style={{ fontSize:11, color:"#4a5a75" }}>Template pre-done ({templateTasks.length})</span>
            </div>
            <div style={{ display:"flex", alignItems:"center", gap:6 }}>
              <div style={{ width:10, height:10, borderRadius:2, background:"#4f6ef7" }} />
              <span style={{ fontSize:11, color:"#4a5a75" }}>You build ({manualTasks.length})</span>
            </div>
          </div>
        </div>

        {/* View tabs */}
        <div style={{ display:"flex", gap:8, marginBottom:28 }}>
          {[
            { id:"plan",   label:"📋 Full Plan" },
            { id:"weekly", label:"📅 Weekly Goals" },
            { id:"daily",  label:"🗓 Daily Tasks" },
          ].map(tab => (
            <button key={tab.id} onClick={() => setView(tab.id)} style={{
              padding:"7px 18px", borderRadius:8, fontSize:12, fontWeight:700, cursor:"pointer",
              border:"1px solid", transition:"all 0.15s",
              borderColor: view===tab.id ? "#4f6ef7" : "rgba(99,130,255,0.15)",
              background:  view===tab.id ? "rgba(79,110,247,0.15)" : "transparent",
              color:       view===tab.id ? "#7c8ff8" : "#4a5a75",
            }}>{tab.label}</button>
          ))}
        </div>

        {/* ── WEEKLY VIEW ── */}
        {view==="weekly" && (
          <div style={{ display:"flex", flexDirection:"column", gap:12 }}>
            {WEEK_SUMMARY.map(w => {
              const epic = PLAN[w.week-1];
              const weekTasks = allTasks.filter(t => {
                const [s,e] = epic.days.replace("Day ","").split("–").map(Number);
                return t.day >= s && t.day <= e;
              });
              const weekDone = weekTasks.filter(t => done[t.id]).length;
              const pct = Math.round(weekDone/weekTasks.length*100);
              const tplCount = weekTasks.filter(t => t.source==="template").length;
              return (
                <div key={w.week} style={{ background:"#0d1425", border:`1px solid ${epic.color}22`, borderLeft:`4px solid ${epic.color}`, borderRadius:12, padding:"20px 24px" }}>
                  <div style={{ display:"flex", justifyContent:"space-between", alignItems:"flex-start", flexWrap:"wrap", gap:8 }}>
                    <div>
                      <div style={{ display:"flex", alignItems:"center", gap:8, marginBottom:6, flexWrap:"wrap" }}>
                        <span style={{ fontSize:11, fontWeight:800, letterSpacing:1.5, color:epic.color, textTransform:"uppercase" }}>Week {w.week} · Day {w.days}</span>
                        <span style={{ fontSize:11, color:"#4a5a75" }}>{weekTasks.length} tasks</span>
                        {tplCount > 0 && (
                          <span style={{ fontSize:10, fontWeight:700, color:"#f0a500", background:"rgba(240,165,0,0.1)", border:"1px solid rgba(240,165,0,0.25)", padding:"1px 8px", borderRadius:6 }}>
                            ⚡ {tplCount} from template
                          </span>
                        )}
                        {w.saved !== "0" && (
                          <span style={{ fontSize:10, fontWeight:700, color:"#10b981", background:"rgba(16,185,129,0.1)", border:"1px solid rgba(16,185,129,0.2)", padding:"1px 8px", borderRadius:6 }}>
                            ~{w.saved} saved
                          </span>
                        )}
                      </div>
                      <div style={{ fontSize:15, fontWeight:700, color:"#e8edf5", marginBottom:6 }}>{w.label}: {epic.title}</div>
                      <div style={{ fontSize:12, color:"#7a8aaa", lineHeight:1.5 }}>🎯 {w.goal}</div>
                    </div>
                    <div style={{ textAlign:"right" }}>
                      <div style={{ fontSize:20, fontWeight:900, color:epic.color }}>{pct}%</div>
                      <div style={{ fontSize:10, color:"#4a5a75" }}>{weekDone}/{weekTasks.length}</div>
                    </div>
                  </div>
                  <div style={{ height:4, background:"#111d35", borderRadius:99, marginTop:14, overflow:"hidden" }}>
                    <div style={{ height:"100%", width:`${pct}%`, background:epic.color, borderRadius:99, transition:"width 0.3s" }} />
                  </div>
                </div>
              );
            })}
          </div>
        )}

        {/* ── DAILY VIEW ── */}
        {view==="daily" && (
          <div>
            {[1,2,3,4,5,6].map(week => {
              const epic = PLAN[week-1];
              const [startDay] = epic.days.replace("Day ","").split("–").map(Number);
              const endDay = startDay + Object.keys(tasksByDay).filter(d => {
                const day = parseInt(d);
                const [s,e] = epic.days.replace("Day ","").split("–").map(Number);
                return day >= s && day <= e;
              }).length - 1;
              const days = [...new Set(allTasks.filter(t => {
                const [s,e] = epic.days.replace("Day ","").split("–").map(Number);
                return t.day >= s && t.day <= e;
              }).map(t => t.day))].sort((a,b)=>a-b);

              return (
                <div key={week} style={{ marginBottom:28 }}>
                  <div style={{ fontSize:11, fontWeight:800, letterSpacing:2, color:epic.color, textTransform:"uppercase", marginBottom:10 }}>
                    Week {week} — {epic.title}
                  </div>
                  <div style={{ display:"flex", flexDirection:"column", gap:8 }}>
                    {days.map(day => {
                      const tasks = tasksByDay[day] || [];
                      const dayDone = tasks.filter(t => done[t.id]).length;
                      const tplDay = tasks.filter(t => t.source==="template").length;
                      const isOpen = activeDay === `day-${day}`;
                      return (
                        <div key={day} style={{ background:"#0d1425", border:"1px solid rgba(99,130,255,0.12)", borderRadius:10, overflow:"hidden" }}>
                          <div onClick={() => setActiveDay(isOpen ? null : `day-${day}`)} style={{ padding:"14px 20px", cursor:"pointer", display:"flex", justifyContent:"space-between", alignItems:"center" }}>
                            <div style={{ display:"flex", alignItems:"center", gap:12 }}>
                              <div style={{
                                width:32, height:32, borderRadius:8, flexShrink:0,
                                background: dayDone===tasks.length ? "rgba(16,185,129,0.15)" : `${epic.color}15`,
                                border:`1px solid ${dayDone===tasks.length ? "#10b981" : epic.color}44`,
                                display:"flex", alignItems:"center", justifyContent:"center",
                                fontSize:12, fontWeight:800,
                                color: dayDone===tasks.length ? "#10b981" : epic.color,
                              }}>D{day}</div>
                              <div>
                                <div style={{ fontSize:13, fontWeight:700, color:"#e8edf5" }}>Day {day}</div>
                                <div style={{ fontSize:11, color:"#4a5a75" }}>
                                  {tasks.length} tasks · {dayDone} done
                                  {tplDay > 0 && <span style={{ color:"#f0a500", marginLeft:6 }}>· ⚡{tplDay} template</span>}
                                </div>
                              </div>
                            </div>
                            <div style={{ display:"flex", alignItems:"center", gap:10 }}>
                              <div style={{ height:4, width:60, background:"#111d35", borderRadius:99, overflow:"hidden" }}>
                                <div style={{ height:"100%", borderRadius:99, width:tasks.length?`${dayDone/tasks.length*100}%`:"0%", background:epic.color }} />
                              </div>
                              <span style={{ color:"#4a5a75", fontSize:14 }}>{isOpen?"▲":"▼"}</span>
                            </div>
                          </div>
                          {isOpen && (
                            <div style={{ borderTop:"1px solid rgba(99,130,255,0.1)", padding:"12px 20px 16px" }}>
                              {tasks.map(t => (
                                <div key={t.id} onClick={e=>toggleDone(t.id,e)} style={{
                                  display:"flex", alignItems:"flex-start", gap:10,
                                  padding:"8px 0", borderBottom:"1px solid rgba(99,130,255,0.06)",
                                  cursor:"pointer",
                                }}>
                                  <div style={{
                                    width:18, height:18, borderRadius:5, flexShrink:0, marginTop:1,
                                    border:`2px solid ${done[t.id] ? "#10b981" : t.source==="template" ? "#f0a500" : "rgba(99,130,255,0.3)"}`,
                                    background: done[t.id] ? "rgba(16,185,129,0.2)" : t.source==="template" ? "rgba(240,165,0,0.1)" : "transparent",
                                    display:"flex", alignItems:"center", justifyContent:"center",
                                    fontSize:10, color:"#10b981",
                                  }}>{done[t.id]?"✓":""}</div>
                                  <div style={{ flex:1 }}>
                                    {t.source==="template" && <span style={{ fontSize:9, fontWeight:800, color:"#f0a500", background:"rgba(240,165,0,0.1)", border:"1px solid rgba(240,165,0,0.2)", padding:"1px 6px", borderRadius:4, marginRight:6, letterSpacing:1 }}>⚡ TEMPLATE</span>}
                                    <span style={{ fontSize:12, color: done[t.id] ? "#3a4a60" : "#b0bdd4", textDecoration: done[t.id] ? "line-through" : "none" }}>{t.title}</span>
                                  </div>
                                  <span style={{ fontSize:10, color:"#4a5a75", background:"#111d35", border:"1px solid rgba(99,130,255,0.1)", padding:"1px 6px", borderRadius:4, flexShrink:0 }}>D{t.day}</span>
                                </div>
                              ))}
                            </div>
                          )}
                        </div>
                      );
                    })}
                  </div>
                </div>
              );
            })}
          </div>
        )}

        {/* ── FULL PLAN VIEW ── */}
        {view==="plan" && PLAN.map(epic => {
          const epicOpen = openEpics[epic.id];
          const epicTasks = epic.features.flatMap(f => f.stories.flatMap(s => s.tasks));
          const epicDone  = epicTasks.filter(t => done[t.id]).length;
          const epicPct   = Math.round(epicDone/epicTasks.length*100);
          const epicTpl   = epicTasks.filter(t => t.source==="template").length;

          return (
            <div key={epic.id} style={{ marginBottom:16 }}>
              <div onClick={() => setOpenEpics(p=>({...p,[epic.id]:!p[epic.id]}))} style={{
                background:"#0d1425", border:`1px solid ${epic.color}33`, borderLeft:`4px solid ${epic.color}`,
                borderRadius:12, padding:"18px 24px", cursor:"pointer", userSelect:"none",
                display:"flex", justifyContent:"space-between", alignItems:"center",
              }}>
                <div style={{ flex:1 }}>
                  <div style={{ display:"flex", alignItems:"center", gap:10, marginBottom:6, flexWrap:"wrap" }}>
                    <span style={{ fontSize:10, fontWeight:800, letterSpacing:1.5, color:epic.color, textTransform:"uppercase", background:`${epic.color}18`, border:`1px solid ${epic.color}44`, padding:"2px 10px", borderRadius:6 }}>EPIC · {epic.id}</span>
                    <span style={{ fontSize:11, color:"#4a5a75" }}>{epic.days} · {epicTasks.length} tasks</span>
                    <span style={{ fontSize:10, fontWeight:700, color:"#4a5a75", background:"#111d35", border:"1px solid rgba(99,130,255,0.12)", padding:"2px 8px", borderRadius:6 }}>Week {epic.week}</span>
                    {epicTpl > 0 && <span style={{ fontSize:10, fontWeight:700, color:"#f0a500", background:"rgba(240,165,0,0.1)", border:"1px solid rgba(240,165,0,0.25)", padding:"2px 8px", borderRadius:6 }}>⚡ {epicTpl} from template</span>}
                  </div>
                  <div style={{ fontSize:15, fontWeight:800, color:"#e8edf5", marginBottom:4 }}>{epic.title}</div>
                  <div style={{ fontSize:12, color:"#7a8aaa" }}>🎯 {epic.goal}</div>
                  <div style={{ height:3, background:"#111d35", borderRadius:99, marginTop:10, width:200, overflow:"hidden" }}>
                    <div style={{ height:"100%", width:`${epicPct}%`, background:epic.color, borderRadius:99 }} />
                  </div>
                </div>
                <div style={{ display:"flex", alignItems:"center", gap:16, flexShrink:0, marginLeft:16 }}>
                  <div style={{ textAlign:"right" }}>
                    <div style={{ fontSize:18, fontWeight:900, color:epic.color }}>{epicPct}%</div>
                    <div style={{ fontSize:10, color:"#4a5a75" }}>{epicDone}/{epicTasks.length}</div>
                  </div>
                  <span style={{ color:"#4a5a75", fontSize:16 }}>{epicOpen?"▲":"▼"}</span>
                </div>
              </div>

              {epicOpen && (
                <div style={{ marginTop:8, paddingLeft:16, display:"flex", flexDirection:"column", gap:10 }}>
                  {epic.features.map(feature => {
                    const fOpen  = openFeatures[feature.id];
                    const fTasks = feature.stories.flatMap(s => s.tasks);
                    const fDone  = fTasks.filter(t => done[t.id]).length;
                    return (
                      <div key={feature.id}>
                        <div onClick={() => setOpenFeatures(p=>({...p,[feature.id]:!p[feature.id]}))} style={{
                          background:"#0d1830", border:"1px solid rgba(79,110,247,0.2)", borderRadius:10,
                          padding:"14px 20px", cursor:"pointer", userSelect:"none",
                          display:"flex", justifyContent:"space-between", alignItems:"center",
                        }}>
                          <div>
                            <div style={{ display:"flex", alignItems:"center", gap:8, marginBottom:4 }}>
                              <span style={{ fontSize:10, fontWeight:800, letterSpacing:1, color:"#7c8ff8", background:"rgba(79,110,247,0.1)", border:"1px solid rgba(79,110,247,0.25)", padding:"1px 8px", borderRadius:5, textTransform:"uppercase" }}>Feature · {feature.id}</span>
                              <span style={{ fontSize:11, color:"#4a5a75" }}>{fDone}/{fTasks.length}</span>
                            </div>
                            <div style={{ fontSize:14, fontWeight:700, color:"#c8d4e8" }}>{feature.title}</div>
                          </div>
                          <span style={{ color:"#4a5a75" }}>{fOpen?"▲":"▼"}</span>
                        </div>

                        {fOpen && (
                          <div style={{ marginTop:6, paddingLeft:16, display:"flex", flexDirection:"column", gap:8 }}>
                            {feature.stories.map(story => {
                              const sOpen = openStories[story.id];
                              const sDone = story.tasks.filter(t => done[t.id]).length;
                              return (
                                <div key={story.id}>
                                  <div onClick={() => setOpenStories(p=>({...p,[story.id]:!p[story.id]}))} style={{
                                    background:"#0a1220", border:"1px solid rgba(20,184,166,0.18)", borderRadius:8,
                                    padding:"12px 18px", cursor:"pointer", userSelect:"none",
                                    display:"flex", justifyContent:"space-between", alignItems:"flex-start",
                                  }}>
                                    <div style={{ flex:1 }}>
                                      <div style={{ display:"flex", alignItems:"center", gap:8, marginBottom:4 }}>
                                        <span style={{ fontSize:10, fontWeight:800, letterSpacing:1, color:"#14b8a6", background:"rgba(20,184,166,0.1)", border:"1px solid rgba(20,184,166,0.25)", padding:"1px 8px", borderRadius:5, textTransform:"uppercase", flexShrink:0 }}>US · {story.id}</span>
                                        <span style={{ fontSize:11, color:"#4a5a75" }}>{sDone}/{story.tasks.length}</span>
                                      </div>
                                      <div style={{ fontSize:13, color:"#b0bdd4", lineHeight:1.4 }}>{story.title}</div>
                                    </div>
                                    <span style={{ color:"#4a5a75", marginLeft:12, flexShrink:0 }}>{sOpen?"▲":"▼"}</span>
                                  </div>

                                  {sOpen && (
                                    <div style={{ marginTop:4, paddingLeft:16 }}>
                                      {story.tasks.map(task => (
                                        <div key={task.id} onClick={e=>toggleDone(task.id,e)} style={{
                                          display:"flex", alignItems:"flex-start", gap:10,
                                          padding:"9px 16px", borderRadius:7, marginBottom:3,
                                          background: task.source==="template"
                                            ? "rgba(240,165,0,0.04)"
                                            : done[task.id] ? "rgba(16,185,129,0.04)" : "rgba(10,18,32,0.8)",
                                          border:`1px solid ${task.source==="template" ? "rgba(240,165,0,0.2)" : done[task.id] ? "rgba(16,185,129,0.2)" : "rgba(99,130,255,0.08)"}`,
                                          cursor:"pointer", transition:"all 0.15s",
                                        }}>
                                          <div style={{
                                            width:16, height:16, borderRadius:4, flexShrink:0, marginTop:2,
                                            border:`2px solid ${done[task.id] ? "#10b981" : task.source==="template" ? "#f0a500" : "rgba(99,130,255,0.3)"}`,
                                            background: done[task.id] ? "rgba(16,185,129,0.2)" : task.source==="template" ? "rgba(240,165,0,0.15)" : "transparent",
                                            display:"flex", alignItems:"center", justifyContent:"center",
                                            fontSize:10, color: task.source==="template" ? "#f0a500" : "#10b981",
                                          }}>{done[task.id] || task.source==="template" ? (task.source==="template" ? "⚡" : "✓") : ""}</div>
                                          <div style={{ flex:1 }}>
                                            {task.source==="template" && (
                                              <span style={{ fontSize:9, fontWeight:800, color:"#f0a500", background:"rgba(240,165,0,0.1)", border:"1px solid rgba(240,165,0,0.2)", padding:"1px 6px", borderRadius:4, marginRight:6, letterSpacing:1 }}>⚡ TEMPLATE</span>
                                            )}
                                            <span style={{ fontSize:12, color: task.source==="template" ? "#6a7a5a" : done[task.id] ? "#3a4a60" : "#8a9ab8", textDecoration: done[task.id] || task.source==="template" ? "line-through" : "none" }}>
                                              {task.title}
                                            </span>
                                          </div>
                                          <span style={{ fontSize:10, color:"#4a5a75", background:"#111d35", border:"1px solid rgba(99,130,255,0.1)", padding:"1px 6px", borderRadius:4, flexShrink:0 }}>D{task.day}</span>
                                        </div>
                                      ))}
                                    </div>
                                  )}
                                </div>
                              );
                            })}
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          );
        })}

        {/* Stack reference */}
        <div style={{ marginTop:32, background:"#0d1425", border:"1px solid rgba(240,165,0,0.2)", borderRadius:12, padding:"20px 24px" }}>
          <div style={{ fontSize:11, fontWeight:800, letterSpacing:2, color:"#f0a500", textTransform:"uppercase", marginBottom:14 }}>Tech Stack Reference</div>
          <div style={{ display:"grid", gridTemplateColumns:"repeat(2,1fr)", gap:8 }}>
            {Object.entries(STACK).map(([k,v]) => (
              <div key={k} style={{ display:"flex", gap:8, fontSize:12 }}>
                <span style={{ color:"#4a5a75", textTransform:"capitalize", minWidth:80 }}>{k}:</span>
                <span style={{ color: k==="template" ? "#f0a500" : "#b0bdd4", fontFamily:"monospace", fontSize:11 }}>{v}</span>
              </div>
            ))}
          </div>
        </div>

      </div>
    </div>
  );
}
