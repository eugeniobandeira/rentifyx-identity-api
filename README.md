<div align="center">

# 🪪 RentifyX — Identity Service

**`rentifyx-identity-api`**

*The trust anchor of the RentifyX platform. Financial-grade identity, onboarding, and compliance.*

[![.NET](https://img.shields.io/badge/.NET_10-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-239120?style=flat-square&logo=csharp&logoColor=white)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-4169E1?style=flat-square&logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![AWS](https://img.shields.io/badge/AWS-232F3E?style=flat-square&logo=amazon-aws&logoColor=white)](https://aws.amazon.com/)
[![Kafka](https://img.shields.io/badge/Apache_Kafka-231F20?style=flat-square&logo=apache-kafka&logoColor=white)](https://kafka.apache.org/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow?style=flat-square)](LICENSE)
[![Status](https://img.shields.io/badge/status-in_development-blue?style=flat-square)]()

</div>

---

## Table of Contents

- [About RentifyX](#about-rentifyx)
- [The Problem and the Solution](#the-problem-and-the-solution)
- [About This Service](#about-this-service)
- [Domain Responsibilities](#domain-responsibilities)
- [Platform Architecture](#platform-architecture)
- [Service Architecture](#service-architecture)
- [Tech Stack](#tech-stack)
- [Patterns and Architectural Decisions](#patterns-and-architectural-decisions)
- [Local Setup](#local-setup)
- [Environment Variables](#environment-variables)
- [Running with Docker Compose](#running-with-docker-compose)
- [Tests](#tests)
- [Project Structure](#project-structure)
- [Domain Events](#domain-events)
- [Roadmap](#roadmap)
- [Infrastructure Costs](#infrastructure-costs)
- [Other Platform Services](#other-platform-services)
- [Architecture Decision Records](#architecture-decision-records)

---

## About RentifyX

> *"Own less. Experience more."*

**RentifyX** is a financial-grade rental platform — it applies the same rigor, security, and compliance standards used in banking to the rental market. Every lease agreement is treated as an auditable, traceable, and legally valid financial transaction.

The platform is composed of **6 independent microservices**, each responsible for a distinct bounded context:

| Service | Repository | Domain |
|---|---|---|
| **Identity** *(this repo)* | `rentifyx-identity-api` | Identity, authentication, KYC, LGPD/GDPR |
| Asset Registry | `rentifyx-asset-registry-api` | Asset catalog, availability, dynamic pricing |
| Leasing | `rentifyx-leasing-api` | Lease agreements, lifecycle, digital signing |
| Billing | `rentifyx-billing-api` | Invoicing, payments, financial reconciliation |
| Risk | `rentifyx-risk-api` | Credit scoring, fraud detection, compliance |
| Communications | `rentifyx-communications-api` | Notifications, email, SMS, SignalR |

**Platform numbers:**
- 6 microservices in an independent polyrepo
- 5 database engines (PostgreSQL, SQL Server, DynamoDB, MongoDB, Redis)
- 2 message brokers (Apache Kafka + RabbitMQ)
- 100% Infrastructure as Code with Terraform
- Build timeline: 6 months to production-ready

---

## The Problem and the Solution

### The Rental Market Today

The rental market moves billions annually, yet operates without the financial-grade trust, compliance, and automation that modern consumers expect:

| Problem | Impact |
|---|---|
| **No credit assessment** | Anyone can rent anything — no risk evaluation, no fraud protection |
| **High cost of ownership** | Consumers spend thousands on items used only a few times per year |
| **Fragmented market** | No unified platform across categories — electronics, vehicles, equipment |
| **No legal-grade contracts** | Informal agreements, no digital signing, no compliance audit trail |
| **Payment uncertainty** | No guaranteed receivables, no installment plans, no reconciliation |

### The RentifyX Solution

| Solution | Delivery |
|---|---|
| **AI-powered risk assessment** | Credit scoring, fraud detection, and automated compliance checks per party |
| **Unified marketplace** | Single platform across all rental categories with dynamic pricing |
| **Digital lease agreements** | Legally-binding digital contracts, S3-stored, Step Functions orchestrated |
| **Financial-grade billing** | Invoicing, installments, PIX/boleto, reconciliation — bank-level accuracy |
| **Real-time communication** | SignalR alerts, multi-channel notifications, full communication audit trail |

---

## About This Service

The **Identity Service** is the trust anchor of the entire RentifyX platform — the equivalent of account opening in a bank. No rental operation takes place without the parties involved having gone through the onboarding, verification, and authentication process managed by this service.

This service manages **every party** on the platform:

- **Tenants** — those who rent items
- **Lessors** — those who list items for rent
- **Guarantors** — those who act as co-signers in higher-risk contracts

Every party — whether an individual or a legal entity — holds a complete profile with verified identity, KYC history, LGPD/GDPR consent records, and authentication credentials.

---

## Domain Responsibilities

### Onboarding and KYC

The KYC (Know Your Customer) workflow is orchestrated via **AWS Step Functions**, providing full traceability of each step in the process:

1. Initial party registration (basic data)
2. Document submission (S3 upload via pre-signed URL)
3. Document verification (Lambda + bureau integrations)
4. Approval or rejection with recorded reason
5. Publication of the `PartyOnboarded` event to Kafka

### Authentication and Authorization

- Custom authentication with **JWT + Refresh Token** (implemented in Phase 1 for learning depth)
- Integration with **AWS Cognito** for MFA, social login, and federated identity (Phase 2)
- Role-based access control (RBAC) by party type

### LGPD/GDPR Compliance

This service is responsible for the complete lifecycle of personal data in compliance with the LGPD (Brazil's General Data Protection Law) and GDPR:

- **Consent**: explicit recording of each data processing purpose
- **Portability**: on-demand export of personal data
- **Erasure**: anonymization and deletion of personal data upon request
- **Audit trail**: every access or modification of sensitive data is logged

### Individual and Legal Entity Profiles

The domain model supports two party types with distinct fields and validations:

- **Individual (CPF)**: national ID, date of birth, address, declared income
- **Legal Entity (CNPJ)**: company registration, legal name, legal representatives, balance sheet

---

## Platform Architecture

```
                        ┌──────────────────────────────────────┐
                        │    AWS API Gateway + CloudFront       │
                        │    Cognito Auth · Rate Limiting        │
                        └──────────────┬───────────────────────┘
                                       │  HTTPS
          ┌────────────────────────────┼─────────────────────────────┐
          │                            │                             │
┌─────────▼──────────┐    ┌────────────▼───────────┐    ┌───────────▼──────────┐
│  identity-api      │    │  asset-registry-api    │    │  leasing-api          │
│  PostgreSQL        │    │  DynamoDB              │    │  SQL Server           │
│  Step Functions    │    │  S3 · Lambda           │    │  Step Functions       │
│  Outbox → Kafka    │    │  OpenSearch            │    │  Outbox → Kafka       │
└─────────┬──────────┘    └────────────┬───────────┘    └───────────┬──────────┘
          │                            │                             │
          └────────────────────────────▼─────────────────────────────┘
                                       │
                            ┌──────────▼──────────┐
                            │    Apache Kafka      │  ← Domain Events (immutable log)
                            │  MSK · 3 brokers    │    PartyOnboarded
                            │  Multi-AZ           │    AssetListed
                            └──────────┬──────────┘    LeaseActivated
                                       │               PaymentReceived
          ┌────────────────────────────┼──────────────────────────────┐
          │                            │                              │
┌─────────▼──────────┐    ┌────────────▼───────────┐    ┌────────────▼──────────┐
│  billing-api       │    │  risk-api              │    │  communications-api   │
│  PostgreSQL        │    │  PostgreSQL · Redis    │    │  MongoDB              │
│  DynamoDB (audit)  │    │  ML.NET · Claude API   │    │  SignalR Hub          │
│  RabbitMQ → jobs   │    │  Rule Engine           │    │  RabbitMQ → email     │
│  ReconcileWorker   │    │  RabbitMQ → review     │    │  AWS SES · SMS        │
└────────────────────┘    └────────────────────────┘    └──────────────────────┘
```

---

## Service Architecture

The Identity Service follows **Clean Architecture** with well-defined layers and no cross-layer dependency violations:

```
rentifyx-identity-api/
├── src/
│   ├── RentifyX.Identity.API/            # Presentation layer (Controllers, Middleware)
│   ├── RentifyX.Identity.Application/    # Use cases (Commands, Queries, Handlers)
│   ├── RentifyX.Identity.Domain/         # Aggregates, entities, value objects, events
│   └── RentifyX.Identity.Infrastructure/ # Repositories, EF Core, Kafka, AWS integrations
└── tests/
    ├── RentifyX.Identity.UnitTests/
    ├── RentifyX.Identity.IntegrationTests/
    └── RentifyX.Identity.ArchitectureTests/
```

### Command Flow (CQRS)

```
HTTP Request
    ↓
Controller (validates ModelState)
    ↓
MediatR.Send(Command)
    ↓
ValidationBehavior (FluentValidation) → returns 422 if invalid
    ↓
LoggingBehavior (Serilog structured log)
    ↓
CommandHandler
    ↓
Domain Aggregate (business logic)
    ↓
Repository.SaveAsync() → persists aggregate + outbox in the same transaction
    ↓
OutboxPublisher (background worker) → publishes event to Kafka
```

---

## Tech Stack

### Backend

| Technology | Version | Purpose |
|---|---|---|
| **.NET** | 10 | Runtime and main framework |
| **C#** | 13 | Programming language |
| **ASP.NET Core** | 10 | Web API and middleware |
| **MediatR** | Latest | Mediator for CQRS and pipeline behaviors |
| **FluentValidation** | Latest | Command and query validation |
| **Entity Framework Core** | 10 | ORM for the write side (aggregate persistence) |
| **Dapper** | Latest | Micro-ORM for the read side (optimized queries) |
| **Worker Service** | .NET 10 | Background outbox publisher |

### Data

| Technology | Purpose |
|---|---|
| **PostgreSQL** | Primary database — identity data, outbox, KYC records |
| **Redis** | Session and token cache (via AWS ElastiCache in production) |

### Messaging and Integration

| Technology | Purpose |
|---|---|
| **Apache Kafka** | Domain event publishing (Outbox Pattern) |
| **AWS Step Functions** | KYC workflow orchestration |
| **AWS Cognito** | MFA, federated authentication (Phase 2) |
| **AWS S3** | Document and verification photo storage |
| **AWS Lambda** | Uploaded document processing |

### Quality and Observability

| Technology | Purpose |
|---|---|
| **OpenTelemetry** | Distributed traces, metrics, and structured logs |
| **Serilog** | Structured logging (JSON) with enrichers |
| **Datadog** | APM, dashboards, and SLOs in production |
| **xUnit** | Test framework |
| **Testcontainers** | Integration tests with real infrastructure in Docker |
| **SonarQube** | Static analysis and code coverage (target: ≥ 80%) |

### CI/CD and Infrastructure

| Technology | Purpose |
|---|---|
| **GitHub Actions** | CI/CD pipeline |
| **Docker / Docker Compose** | Containerization and local environment |
| **Terraform** | Infrastructure as Code (IaC) for AWS |
| **Helm** | Kubernetes deployment (AWS EKS) |
| **AWS EKS** | Container orchestration in production |

---

## Patterns and Architectural Decisions

### Clean Architecture + DDD

The service is organized in concentric layers where dependencies always point inward (toward the domain):

- **Domain**: Aggregates, entities, value objects, domain events, repository interfaces. No framework dependencies.
- **Application**: Command/query handlers, DTOs, application services. Depends only on the domain.
- **Infrastructure**: Concrete implementations (EF Core, Kafka, AWS). Depends on application and domain.
- **API**: Controllers, middleware, configuration. Depends on the application.

### CQRS with MediatR

Commands and queries are completely separated:

- **Commands** → mutate state → handled by `CommandHandler` → persisted via EF Core → events published via Outbox
- **Queries** → pure reads → handled by `QueryHandler` → fetched via Dapper (optimized SQL) → never touch aggregates

### Outbox Pattern

Guarantees exactly-once delivery of domain events to Kafka, eliminating the dual-write problem:

1. The handler persists the aggregate **and** a record in the `outbox` table within the **same transaction**
2. A background Worker Service reads the `outbox` table and publishes events to Kafka
3. After Kafka acknowledges (ack), the record is marked as processed

This guarantees that **if the business transaction committed, the event will be published** — regardless of network failures or broker downtime.

### JWT + Refresh Token (Phase 1)

Custom implementation for learning depth:

- Short-lived access token (15 minutes)
- Long-lived refresh token, rotated on each use
- Refresh token revocation stored in PostgreSQL

### AWS Cognito Integration (Phase 2 — ADR-006)

After the custom implementation, Cognito is integrated to add:

- MFA (TOTP and SMS)
- Social login (Google, Apple)
- Federated identity via OIDC/SAML

---

## Local Setup

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [Git](https://git-scm.com/)

### Cloning the repository

```bash
git clone https://github.com/eugeniobandeira/rentifyx-identity-api.git
cd rentifyx-identity-api
```

### Restoring dependencies

```bash
dotnet restore
```

### Applying migrations

```bash
dotnet ef database update --project src/RentifyX.Identity.Infrastructure \
                           --startup-project src/RentifyX.Identity.API
```

---

## Environment Variables

Create an `appsettings.Development.json` file inside `src/RentifyX.Identity.API/` based on the template below:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=rentifyx_identity;Username=postgres;Password=postgres"
  },
  "Jwt": {
    "SecretKey": "your-secret-key-with-at-least-256-bits",
    "Issuer": "rentifyx-identity-api",
    "Audience": "rentifyx-platform",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 30
  },
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "Topics": {
      "PartyOnboarded": "rentifyx.identity.party-onboarded",
      "KycCompleted": "rentifyx.identity.kyc-completed"
    }
  },
  "Aws": {
    "Region": "us-east-1",
    "S3": {
      "BucketName": "rentifyx-identity-documents-dev"
    },
    "StepFunctions": {
      "KycWorkflowArn": "arn:aws:states:us-east-1:000000000000:stateMachine:rentifyx-kyc-workflow"
    }
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    }
  }
}
```

> **Note**: Never commit real credentials. In production, all sensitive configuration is injected via **AWS Secrets Manager**.

---

## Running with Docker Compose

The full local platform environment runs via Docker Compose — **at zero cost**.

```bash
# From the project root (or the platform infrastructure repository)
docker compose up -d

# Check container status
docker compose ps
```

**Locally available services:**

| Service | Port | Credentials |
|---|---|---|
| PostgreSQL | `5432` | `postgres` / `postgres` |
| Kafka (Broker) | `9092` | — |
| Kafka UI | `8080` | — |
| Redis | `6379` | — |
| Localstack (AWS Local) | `4566` | — |

**Starting the API:**

```bash
dotnet run --project src/RentifyX.Identity.API
```

The API will be available at `https://localhost:5001` and `http://localhost:5000`.

Swagger/OpenAPI will be accessible at `https://localhost:5001/swagger`.

---

## Tests

The project adopts a three-layer testing strategy:

### Unit Tests

Cover domain logic — aggregates, value objects, domain services, and isolated handlers:

```bash
dotnet test tests/RentifyX.Identity.UnitTests
```

### Integration Tests

Use **Testcontainers** to spin up real PostgreSQL and Kafka instances in Docker during test execution — no infrastructure mocks:

```bash
dotnet test tests/RentifyX.Identity.IntegrationTests
```

> The choice of Testcontainers over database mocks is deliberate (ADR): a mock that passes tests but diverges from real PostgreSQL behavior can hide critical migration and transaction bugs.

### Architecture Tests

Verify that Clean Architecture dependency rules are respected — for example, that no class in the `Domain` layer references `Infrastructure`:

```bash
dotnet test tests/RentifyX.Identity.ArchitectureTests
```

### Running all tests with coverage

```bash
dotnet test --collect:"XPlat Code Coverage" \
            --results-directory ./coverage \
            /p:CoverletOutputFormat=cobertura
```

> **Coverage target**: ≥ 80% (enforced via SonarQube in the CI pipeline)

---

## Project Structure

```
rentifyx-identity-api/
│
├── src/
│   ├── RentifyX.Identity.API/
│   │   ├── Controllers/
│   │   │   ├── AuthController.cs
│   │   │   ├── PartyController.cs
│   │   │   └── KycController.cs
│   │   ├── Middleware/
│   │   │   ├── ExceptionHandlingMiddleware.cs
│   │   │   └── CorrelationIdMiddleware.cs
│   │   ├── Program.cs
│   │   └── appsettings.json
│   │
│   ├── RentifyX.Identity.Application/
│   │   ├── Parties/
│   │   │   ├── Commands/
│   │   │   │   ├── RegisterParty/
│   │   │   │   │   ├── RegisterPartyCommand.cs
│   │   │   │   │   ├── RegisterPartyCommandHandler.cs
│   │   │   │   │   └── RegisterPartyCommandValidator.cs
│   │   │   │   └── UpdatePartyProfile/
│   │   │   └── Queries/
│   │   │       └── GetPartyById/
│   │   ├── Auth/
│   │   │   ├── Commands/
│   │   │   │   ├── Login/
│   │   │   │   └── RefreshToken/
│   │   │   └── Queries/
│   │   ├── Kyc/
│   │   │   ├── Commands/
│   │   │   │   ├── InitiateKyc/
│   │   │   │   └── SubmitDocument/
│   │   │   └── Queries/
│   │   ├── Common/
│   │   │   ├── Behaviors/
│   │   │   │   ├── ValidationBehavior.cs
│   │   │   │   └── LoggingBehavior.cs
│   │   │   └── Interfaces/
│   │   └── DependencyInjection.cs
│   │
│   ├── RentifyX.Identity.Domain/
│   │   ├── Parties/
│   │   │   ├── Party.cs                    # Aggregate root
│   │   │   ├── IndividualParty.cs          # Natural person
│   │   │   ├── LegalEntityParty.cs         # Legal entity
│   │   │   ├── PartyRole.cs                # ValueObject: Tenant, Lessor, Guarantor
│   │   │   └── Events/
│   │   │       ├── PartyRegisteredEvent.cs
│   │   │       └── PartyOnboardedEvent.cs
│   │   ├── Kyc/
│   │   │   ├── KycWorkflow.cs              # Aggregate
│   │   │   ├── KycStatus.cs               # ValueObject
│   │   │   └── Events/
│   │   │       └── KycCompletedEvent.cs
│   │   ├── Auth/
│   │   │   ├── Credential.cs              # Entity
│   │   │   └── RefreshToken.cs            # ValueObject
│   │   └── Common/
│   │       ├── Entity.cs
│   │       ├── AggregateRoot.cs
│   │       └── IDomainEvent.cs
│   │
│   └── RentifyX.Identity.Infrastructure/
│       ├── Persistence/
│       │   ├── IdentityDbContext.cs
│       │   ├── Configurations/            # EF Core fluent configurations
│       │   ├── Repositories/
│       │   ├── Outbox/
│       │   │   ├── OutboxMessage.cs
│       │   │   └── OutboxPublisherWorker.cs
│       │   └── Migrations/
│       ├── Messaging/
│       │   └── KafkaEventPublisher.cs
│       ├── Aws/
│       │   ├── S3DocumentService.cs
│       │   └── StepFunctionsKycOrchestrator.cs
│       └── DependencyInjection.cs
│
└── tests/
    ├── RentifyX.Identity.UnitTests/
    ├── RentifyX.Identity.IntegrationTests/
    └── RentifyX.Identity.ArchitectureTests/
```

---

## Domain Events

The Identity Service publishes the following events to Apache Kafka. Other platform services consume these events to react to changes in the identity domain:

| Event | Kafka Topic | Published when |
|---|---|---|
| `PartyRegistered` | `rentifyx.identity.party-registered` | A new party is registered on the platform |
| `PartyOnboarded` | `rentifyx.identity.party-onboarded` | KYC completed successfully — party is eligible to operate |
| `KycInitiated` | `rentifyx.identity.kyc-initiated` | KYC workflow started via Step Functions |
| `KycCompleted` | `rentifyx.identity.kyc-completed` | KYC finalized (approved or rejected) |
| `KycRejected` | `rentifyx.identity.kyc-rejected` | KYC rejected with reason |
| `PartyProfileUpdated` | `rentifyx.identity.party-profile-updated` | Party registration data was updated |
| `PartyDataEraseRequested` | `rentifyx.identity.data-erase-requested` | LGPD/GDPR data erasure request submitted |

All events follow the Outbox Pattern — guaranteeing **exactly-once delivery**, even in the event of broker or application failure.

---

## Roadmap

The Identity Service is **Month 1** of the 6-month RentifyX timeline — it establishes the architectural patterns that all other services will follow.

### Month 1 — Foundation *(in progress)*

**Identity Service · Core Patterns**

Build the platform foundation: Identity API with Clean Architecture, CQRS + MediatR, JWT auth, Outbox Pattern publishing to Kafka. Establish the full Docker Compose local environment and the architectural patterns all other services will follow.

**Deliverables:**
- [ ] Full Clean Architecture (API → Application → Domain → Infrastructure)
- [ ] DDD Aggregates: `Party`, `KycWorkflow`, `Credential`
- [ ] CQRS with MediatR + pipeline behaviors (validation, logging)
- [ ] JWT + Refresh Token (custom auth)
- [ ] Outbox Pattern → Kafka (local Docker)
- [ ] PostgreSQL with EF Core 10 + Dapper (read side)
- [ ] Docker Compose with all infrastructure services
- [ ] Tests with Testcontainers (real integration with PostgreSQL and Kafka)
- [ ] GitHub Actions pipeline (build, test, SonarQube)

---

### Month 2 — Asset Registry · IaC

DynamoDB data modeling for the Asset Registry, S3 + Lambda image processing pipeline, OpenSearch for full-text search. Start Terraform IaC for AWS infrastructure: VPC, ECR, RDS, MSK.

---

### Month 3 — Leasing · Saga

Core domain implementation: Leasing Service with SQL Server, Saga Choreography via Kafka for cross-service consistency, Step Functions for approval workflow orchestration, digital PDF contract generation to S3.

---

### Month 4 — Billing · RabbitMQ

Full billing implementation with PIX/boleto integration, RabbitMQ for payment task queues, Saga Orchestration pattern, and the `ReconciliationWorker` background service running daily financial reconciliation.

---

### Month 5 — Risk · Communications · Kubernetes

Risk Service with ML.NET scoring, Communications Service with SignalR and AWS SES, deploy all services to Kubernetes with Helm charts, and wire up OpenTelemetry distributed tracing across the full platform.

---

### Month 6 — Claude AI · Observability · Production Ready

Integrate Claude API for intelligent risk assessment and anomaly explanation, finalize Datadog dashboards with SLOs per service, complete Terraform for full production infrastructure, and document the full architecture with C4 Model diagrams and ADRs.

---

## Infrastructure Costs

### Local Development — $0/month

The full stack runs via Docker Compose. PostgreSQL, Kafka, Redis, RabbitMQ — all containerized, at zero cost.

### Cloud Dev / Staging — ~$310/month

Minimal cloud environment to validate AWS service integrations (Cognito, Step Functions, SES):

| Resource | Monthly Cost |
|---|---|
| EKS cluster + t3.medium nodes | $133 |
| RDS PostgreSQL t3.micro | $15 |
| RDS SQL Server t3.micro | $28 |
| MSK Kafka t3.small (×2) | $70 |
| Amazon MQ t3.micro | $18 |
| ElastiCache t3.micro | $12 |
| DynamoDB on-demand (dev) | $2 |
| MongoDB Atlas M0 | Free |
| S3 + SES + Lambda + Cognito | ~$2 |
| Step Functions (dev usage) | Free tier |

> **Cost tip**: the staging environment can be **scheduled on/off** (weekdays only), reducing actual monthly spend to ~$150.

### Production MVP (~1,000 MAU) — ~$1,450/month

Full production setup: Multi-AZ databases, 3-broker Kafka cluster, autoscaling Kubernetes nodes, Datadog monitoring, and all AWS managed services.

| Resource | Monthly Cost |
|---|---|
| EKS + 3× t3.large nodes | $253 |
| RDS PostgreSQL Multi-AZ db.t3.medium | $100 |
| RDS SQL Server Multi-AZ db.t3.medium | $180 |
| MSK Kafka m5.large (×3) | $450 |
| Amazon MQ m5.large | $120 |
| ElastiCache cache.t3.medium | $50 |
| DynamoDB on-demand (prod) | $25 |
| MongoDB Atlas M10 | $57 |
| API Gateway + CloudFront + SES | $30 |
| Lambda + Step Functions | $15 |
| Datadog (3 hosts) | $45 |
| Cognito (< 50k MAU) | Free |

> **Scaling**: at ~10,000 MAU the estimated cost is **~$3,500–5,000/month**. The largest cost driver is MSK (managed Kafka) — which can be replaced by self-hosted Kafka on EKS to save ~$350/month in early stages.

*All estimates based on AWS pricing for the São Paulo region (sa-east-1) as of 2026.*

---

## Other Platform Services

| Service | Repository | Database | Messaging | Domain |
|---|---|---|---|---|
| Asset Registry | `rentifyx-asset-registry-api` | DynamoDB + OpenSearch | Kafka | Catalog, availability, dynamic pricing |
| Leasing | `rentifyx-leasing-api` | SQL Server | Kafka + Step Functions | Contracts, lifecycle, digital signing |
| Billing | `rentifyx-billing-api` | PostgreSQL + DynamoDB | RabbitMQ + Kafka | Invoicing, PIX/boleto, reconciliation |
| Risk | `rentifyx-risk-api` | PostgreSQL + Redis | Kafka + RabbitMQ | ML scoring, fraud, compliance, Claude API |
| Communications | `rentifyx-communications-api` | MongoDB | RabbitMQ | SignalR, SES, SMS, dead-letter retry |

---

## Architecture Decision Records

ADRs document the context, options considered, and rationale behind every significant architectural decision.

### ADR-001 — Polyrepo over Monorepo

Each service evolves independently with its own CI/CD pipeline, dependency tree, and deployment cadence — mirroring real-world fintech engineering teams.

### ADR-002 — Kafka for domain events, RabbitMQ for tasks

**Kafka** provides an immutable event log with replay capability for cross-service domain events. **RabbitMQ** handles work queues (email, SMS, reports) where acknowledgment and dead-lettering matter more than ordering.

### ADR-003 — Polyglot persistence strategy

Each service uses the database engine best suited to its access patterns: relational for ACID-critical data, DynamoDB for high-read flexible schemas, MongoDB for variable document structures.

### ADR-004 — Outbox Pattern for guaranteed delivery

Domain events are written to an outbox table in the same transaction as business data, then published asynchronously. Prevents the dual-write problem — critical for financial event integrity.

### ADR-005 — Step Functions for workflow orchestration

Long-running workflows (KYC, lease approval, refund) are orchestrated via Step Functions Standard Workflows, providing built-in state management, retries, timeouts, and visual debugging.

### ADR-006 — AWS Cognito for identity management

Custom JWT auth is implemented first for learning depth, then Cognito integration is added for MFA, social login, and federated identity — demonstrating both managed and custom auth patterns.

### ADR-007 — CQRS + MediatR as the application layer

Separating reads from writes enables independent scaling and optimization. MediatR pipeline behaviors provide a clean cross-cutting concerns mechanism (validation, logging, caching) without polluting handlers.

### ADR-008 — SQL Server for Leasing (financial contracts)

Lease agreements require the strongest ACID guarantees, row-level locking precision, and audit-friendly tooling. SQL Server is the standard in financial institutions and familiar to compliance teams.

---

## Author

Built by **Eugênio Bandeira**

Financial-grade engineering · 2026

> [github.com/eugeniobandeira](https://github.com/eugeniobandeira)

---

<div align="center">

*RentifyX — Own less. Experience more.*

</div>
