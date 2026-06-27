# Documentation — rentifyx-identity-api

This folder documents the architecture, decisions, and guides for the RentifyX Identity API.

## Sections

| Folder | Purpose |
|---|---|
| [architecture/](architecture/) | Layer structure, dependency flow, domain model, AWS integration |
| [decisions/](decisions/) | Architecture Decision Records (ADRs) — append-only |
| [features/](features/) | Per-feature spec and implementation notes |
| [guides/](guides/) | Developer how-tos (adding features, running locally, testing) |

## ADR Index

| ADR | Title | Status |
|---|---|---|
| [ADR-001](decisions/001-secrets-manager-over-appsettings.md) | Secrets Manager over appsettings for sensitive config | Accepted |
| [ADR-002](decisions/002-taxid-as-identity-field.md) | TaxId (CPF or CNPJ) as identity field (LGPD data minimization) | Accepted |
| [ADR-003](decisions/003-erroror-over-exceptions.md) | ErrorOr\<T\> over exceptions for control flow | Accepted |
| [ADR-004](decisions/004-domain-events-over-direct-calls.md) | Domain events over direct service calls | Accepted |
| [ADR-005](decisions/005-dynamodb-single-table-design.md) | Single-table DynamoDB design | Accepted |
| [ADR-006](decisions/006-cognito-vs-custom-jwt.md) | Custom JWT for internal auth, Cognito for user-facing auth | Accepted |
| [ADR-007](decisions/007-lgpd-data-retention-anonymization.md) | LGPD data retention and anonymization strategy | Accepted |
| [ADR-008](decisions/008-kubernetes-rolling-update.md) | Kubernetes deployment strategy (RollingUpdate) | Accepted |

## Project Plan

The full 28-day implementation plan (6 epics, 148 tasks) lives in [`RentifyX_IdentityAPI_Plan.jsx`](../RentifyX_IdentityAPI_Plan.jsx) at the repo root. Open it in a React sandbox to use the interactive checklist.
