# ADR-004: Domain events over direct service calls

- **Date:** 2026-06-21
- **Status:** Accepted

## Context

When a user registers, other services in the RentifyX platform need to react: the notification service sends a welcome email, the KYC service initiates verification, and analytics records the sign-up. The identity service could call these services directly from the `RegisterUserHandler`, but this creates tight coupling and synchronous failure cascades.

## Options Considered

- **Option A — Direct service calls from handlers**: Simple to implement. But the identity service becomes tightly coupled to every downstream consumer; a SES timeout fails the entire registration; adding a new consumer requires modifying the handler.
- **Option B — Domain events + in-process dispatcher**: Handlers raise domain events; an in-process dispatcher routes them to local event handlers. Decoupled within the service but events are lost on process crash before publishing to the broker.
- **Option C — Domain events + Outbox Pattern → Kafka**: Handlers raise domain events; the aggregate stores them; the Outbox writes the event to DynamoDB atomically with the user record; a background `OutboxPublisher` polls and publishes to Kafka. Guarantees at-least-once delivery even if the process crashes between the write and the Kafka publish.

## Decision

**Option C** — Domain events raised by aggregates, persisted via the Outbox Pattern to DynamoDB, published asynchronously to Kafka.

Events raised by `User`:
- `UserRegistered`
- `UserEmailVerified`
- `UserPasswordChanged`
- `UserSuspended`

Outbox flow:
1. Handler calls `user.RaiseDomainEvent(new UserRegistered(...))`.
2. Repository saves the `User` record and an `OutboxMessage` record atomically in DynamoDB.
3. `OutboxPublisher` (`IHostedService`) polls pending outbox messages, publishes to Kafka, marks as processed.
4. After 3 failed retries, the message moves to a Kafka DLQ topic.

## Consequences

**Easier:**
- Identity service has zero knowledge of downstream consumers.
- New consumers subscribe to Kafka topics — no identity code changes.
- Guaranteed delivery: crash between write and publish is safe because the outbox survives.
- Domain events are testable in unit tests with no Kafka dependency.

**Harder:**
- Eventually consistent: downstream services react after a short delay (typically < 1 second).
- Requires monitoring the DLQ for failed deliveries.
- Local development requires a Kafka broker (provided via LocalStack / Docker Compose).
