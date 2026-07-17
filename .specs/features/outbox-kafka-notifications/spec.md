# Feature Spec: Domain Event Outbox & Kafka Notification Producer

## Status

Draft — resolves DEF-005 (deferred since v1.1.0) and unblocks integration with
`rentifyx-communications-api`, which already consumes `NotificationRequested` Kafka events for
transactional email (verification, password reset) but currently has no real producer — this
service still sends email itself, synchronously, in-process.

## Problem

`IEmailService`/`EmailService` calls AWS SES v2 directly from `RegisterUserHandler` and
`ForgotPasswordHandler`, inline, best-effort (a send failure only logs a warning — the use case
never fails). There is no Outbox, no Kafka client, no domain-event dispatch mechanism: `UserEntity`
does not accumulate events, the six existing event records (`UserRegistered`,
`UserEmailVerified`, `UserPasswordChanged`, `UserSuspended`, `UserAccountDeleted`, `UserLoggedIn`)
are constructed inline in handlers and only ever passed to `logger.LogInformation` — dead-end
telemetry, never actually raised or published anywhere.

Meanwhile `rentifyx-communications-api` already implements the consumer side of this exact flow
(`NotificationRequestedConsumer`, full outbox-pattern dispatch pipeline, retry/DLQ/reconciliation
— see AD-002/AD-007/AD-008 in that repo's `STATE.md`) and documents in its own AD-011 that identity-
api migration is expected "after communications-api's v1.0.0 has stabilized in production" — which
it now has (E-01 through E-06 complete, PR #10 merged 2026-07-15).

This spec builds the missing producer half: a generic `AggregateRoot`/domain-event mechanism, a
DynamoDB-backed Outbox written atomically alongside the aggregate, an `OutboxPublisher` hosted
service that publishes to Kafka, and a direct cutover of `RegisterUserHandler`/
`ForgotPasswordHandler` off `IEmailService`/SES entirely.

## Decisions (confirmed with user, 2026-07-15)

- **Cutover, not dual-write.** `IEmailService`/`EmailService`/SES v2 direct-send is removed
  entirely from the two handlers that use it today. `rentifyx-communications-api` becomes the
  sole sender of transactional email. No parallel/shadow SES calls.
- **MVP scope is broad, not narrow.** Build the generic Outbox mechanism and wire **all six**
  existing domain events through it (`UserRegistered`, `UserEmailVerified`, `UserPasswordChanged`,
  `UserSuspended`, `UserAccountDeleted`, `UserLoggedIn`), not just the two that currently trigger
  email. Only `UserRegistered` and a to-be-added `PasswordResetRequested` event (see R-06) actually
  publish `NotificationRequested` to comms-api in this cycle; the other four are published to their
  own topic(s) for future consumers, closing the "publish once, don't redo the plumbing later" gap
  the user explicitly asked for.
- **Transport is Kafka**, not SNS/EventBridge. `ROADMAP.md`'s DEF-005 description ("dispatches to
  SNS/EventBridge") is stale — reconciled by this spec to match ADR-004 (which already targeted
  Kafka) and to match `rentifyx-communications-api`'s existing Kafka-only architecture. See R-08.
- **Broker infrastructure is shared, not per-service.** Neither this repo nor
  `rentifyx-communications-api` has ever provisioned a real Kafka broker for production — both only
  run a local Aspire container in dev. A shared, self-hosted Kafka (Helm chart on the shared EKS
  cluster in `rentifyx-platform`, not AWS MSK — cost-driven, matches that repo's existing
  Fargate/no-managed-service philosophy) is being specced separately in
  `rentifyx-platform/.specs/features/shared-kafka-eks/spec.md`, with broker connection info
  published via SSM Parameter Store per that repo's ADR-005 convention. This spec's Kafka producer
  code must read broker config from `IConfiguration`/`IOptions<T>` in a way that's satisfied either
  by a local Aspire Kafka resource (dev) or by SSM-sourced config (prod) — it must not hardcode a
  broker address.

## Requirements

| ID | Requirement | Source | Notes |
|---|---|---|---|
| R-01 | `IDomainEvent` marker interface + `AggregateRoot<TId>` base class with `RaiseDomainEvent()`/`DomainEvents` (IReadOnlyCollection, cleared after dispatch) | DEF-005; confirmed absent by exploration — zero matches for `AggregateRoot`\|`RaiseDomainEvent` in `02-src` | `UserEntity` becomes the first (only, for now) consumer — must not change its existing `Create()`/mutation-method public API shape, only add event-raising calls inside those methods |
| R-02 | `OutboxEntry` DynamoDB item (own table or same single-table design as `UserEntity`, TBD at design time) written atomically in the *same* `SaveAsync`/`UpdateAsync` call as the aggregate — never a separate round trip | ADR-004; DEF-005 original description | Atomicity is the entire point of the pattern — a `UserEntity` write that succeeds but an Outbox write that fails (or vice versa) must not be possible |
| R-03 | `OutboxPublisher` — `IHostedService`, polls unpublished Outbox entries, publishes each to Kafka, marks `Published` (or deletes) on ack | ADR-004; mirrors comms-api's own `IHostedService`-based consumer pattern (AD-006 there) for architectural symmetry | Ordering only needs to be per-aggregate-id, not global — confirm at design time whether a `PeriodicTimer` poll (comms-api's `ReconciliationHostedService` precedent) or DynamoDB Streams trigger is the right mechanism |
| R-04 | Dead-letter handling: after N failed publish attempts (default 3, matches original DEF-005 description), mark the Outbox entry `Failed` and log at `Critical` — no infinite retry loop | DEF-005 original description | Does not need comms-api's full retry-topic-chain sophistication (F-09) — this is a producer-side safety net, not a consumer-side reliability pipeline; keep it simple |
| R-05 | `RegisterUserHandler` raises `UserRegistered`; handler no longer calls `IEmailService.SendVerificationEmailAsync` directly — the verification email now happens because comms-api consumes the resulting `NotificationRequested` event | Cutover decision (see above) | The verification token itself (`rawToken`) must travel inside the event payload so comms-api's template renderer has what it needs — coordinate payload shape with R-07 |
| R-06 | New domain event `PasswordResetRequested` (does not exist today) raised by `ForgotPasswordHandler`; handler no longer calls `IEmailService.SendPasswordResetEmailAsync` directly | Cutover decision — `ForgotPassword` flow currently has no domain event at all, only a direct SES call | Must preserve the existing "blind success" anti-enumeration behavior (`ForgotPasswordHandler`'s current 204-always-regardless-of-whether-email-exists) — raising the event only when a matching user is actually found, same as today's SES call is only made in that case |
| R-07 | `NotificationRequested` message contract emitted by the Outbox publisher must match the shape `rentifyx-communications-api` already consumes (see AD-002 there: `channel`, `templateId`, `recipient`, `payload`, `correlationId`) — not a new/divergent schema | Cross-repo contract alignment, this spec's core purpose | Read `docs/contracts/notification-requested.md` from comms-api if it exists by the time this is designed (E-08 F-15); if not yet written, read comms-api's `NotificationRequested` Kafka message DTO directly and match it field-for-field. `templateId` values needed: one for email verification, one for password reset — confirm exact IDs against comms-api's `ScribanTemplateRenderer` template registry (only `welcome-email` exists there today per F-07 — new templates likely needed on that side too, flag as a cross-repo dependency, not silently assumed) |
| R-08 | Reconcile `ROADMAP.md`'s DEF-005 description (currently says "dispatches to SNS/EventBridge") to say Kafka, matching ADR-004 and this spec | Doc-drift found during this spec's own research | Pure doc fix, bundle with R-01..R-07 implementation PR or land standalone first |
| R-09 | Remove `IEmailService`/`EmailService`/`AWSSDK.SimpleEmailV2` package reference and the `Ses:FromAddress` config/secret entry once R-05/R-06 land and nothing else calls it | Cutover decision — dead code once the two call sites are migrated | Confirm zero remaining references before deleting (grep `IEmailService`); Terraform `modules/ses` in this repo's own `iac/` also becomes dead and should be removed or explicitly kept as a documented rollback path — decide at design time |
| R-10 | Kafka producer client config (topic name, broker connection) sourced from `IConfiguration`, satisfied by local Aspire Kafka resource in dev and by `rentifyx-platform` SSM-published broker info in prod — never a hardcoded address | Shared-infra decision (see above) | Exact SSM parameter path/naming to agree with `rentifyx-platform/.specs/features/shared-kafka-eks/spec.md` — cross-repo dependency, do not design in isolation |

## Out of Scope

| Item | Reason |
|---|---|
| `UserLoggedIn`/`UserSuspended`/`UserEmailVerified`/`UserPasswordChanged` triggering any actual notification | Only `UserRegistered` and `PasswordResetRequested` map to an existing comms-api template/consumer today; the other four are published (per the broad-MVP decision) but have no consumer yet — that's future work, not this spec's problem to solve |
| `rentifyx-communications-api` template additions (e.g. password-reset template if `welcome-email` is the only one that exists) | Cross-repo — tracked as a flagged dependency in R-07, not built here |
| Comms-api's own DLQ/retry sophistication (F-09-equivalent) on the producer side | R-04's simple 3-retry-then-Failed is sufficient for a producer; full reliability engineering lives on the consumer side, already built in comms-api |
| Removing `docs/contracts/notification-requested.md` ownership ambiguity (which repo owns the canonical schema doc) | Should already be comms-api's E-08 F-15 deliverable; this spec consumes it, doesn't redefine ownership |

## Ordering rationale

1. R-08 (doc reconciliation) — trivial, zero risk, unblocks a clean design conversation without a stale SNS reference confusing it.
2. R-01 (`AggregateRoot`/`IDomainEvent`) — foundational, nothing else can start without it.
3. R-02/R-03/R-04 (Outbox table + publisher + dead-letter) — the generic mechanism, built and tested against a throwaway/no-op event before touching real handlers.
4. R-10 (Kafka config sourcing) — needs to land before R-05/R-06 actually publish anything real; coordinate timing with `rentifyx-platform`'s Kafka module landing.
5. R-07 (contract alignment) — design-time cross-repo coordination, blocks R-05/R-06's payload shape.
6. R-05, R-06 (cutover) — the actual behavior change; land together since they're the same pattern applied twice.
7. R-09 (dead code removal) — last, only after R-05/R-06 are verified working end-to-end.
