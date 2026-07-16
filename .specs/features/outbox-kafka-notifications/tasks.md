# Domain Event Outbox & Kafka Notification Producer — Tasks

**Design**: `.specs/features/outbox-kafka-notifications/design.md`
**Status**: In Progress — T0-T12 done (2026-07-15: T0-T4; T5-T6 same day, doc updated 2026-07-16; T7-T12 all completed 2026-07-16, plus the AppHost Kafka resource gap flagged after T11). Next: T13 (cutover — `RegisterUserHandler`/`ForgotPasswordHandler`).

---

## Execution Plan

### Phase 0: Doc reconciliation (independent, no code dependency)

```
T0 [P]
```

### Phase 1: Domain foundation (sequential, then split)

```
T1 → T2 [P]
   → T3 [P]
```

### Phase 2: Entity wiring (sequential after Phase 1)

```
T2, T3 → T4
```

### Phase 3: Outbox mechanism (mostly sequential — each layer needs the one below)

```
T4 → T5 → T6 → T7 → T8 → T9
```

### Phase 4: Kafka producer infra (parallel with Phase 3 once T0 lands — no shared files)

```
T10 [P] → T11 [P]
```

### Phase 5: Cutover (sequential, needs Phase 3 + Phase 4 both done)

```
T9, T11 → T12 → T13
```

### Phase 6: Cleanup (last)

```
T12, T13 → T14
```

---

## Task Breakdown

### T0: Reconcile ROADMAP.md DEF-005 doc drift [P]

**What**: Fix `ROADMAP.md`'s v1.1.0 section — "Domain event Outbox (DEF-005)" currently says
"`OutboxProcessor` background service polls and dispatches to SNS/EventBridge." Change to Kafka,
matching ADR-004 and this feature. Also fix the section header inconsistency (DEF-005's bullets
have no ✅ checkmarks under a "COMPLETE ✅" milestone, despite `STATE.md` explicitly saying Outbox
was deferred — mark it correctly as not-yet-done, this feature is what completes it).
**Where**: `ROADMAP.md`
**Depends on**: None
**Reuses**: n/a
**Requirement**: R-08

**Tools**: NONE

**Done when**:
- [ ] DEF-005 section says Kafka, not SNS/EventBridge
- [ ] v1.1.0 milestone header/DEF-005 subsection accurately reflects "not done until this feature ships"

**Tests**: none
**Gate**: none (docs only, not a code layer in TESTING.md's matrix)

**Commit**: `docs(roadmap): reconcile DEF-005 to Kafka, fix v1.1.0 status`

---

### T1: `IDomainEvent` interface + `AggregateRoot` base class

**What**: `IDomainEvent` marker interface (`OccurredAt` property) and `AggregateRoot` base class
(`RaiseDomainEvent`/`DomainEvents`/`ClearDomainEvents`) per design.md's SPEC_DEVIATION (non-generic,
not `AggregateRoot<TId>`).
**Where**: `02-src/03-Domain/RentifyxIdentity.Domain/Events/IDomainEvent.cs`,
`02-src/03-Domain/RentifyxIdentity.Domain/Common/AggregateRoot.cs`
**Depends on**: None
**Reuses**: n/a — foundational, new
**Requirement**: R-01

**Tools**: NONE

**Done when**:
- [ ] `RaiseDomainEvent` appends to an internal list; `DomainEvents` exposes it read-only; `ClearDomainEvents` empties it
- [ ] Gate check passes: `dotnet test 03-tests/01-Common/RentifyxIdentity.Tests.Common`

**Tests**: unit (Domain entities/VOs → `Tests.Common` per TESTING.md matrix)
**Gate**: quick

**Commit**: `feat(domain): add IDomainEvent and AggregateRoot base class`

---

### T2: `OutboxEntry` entity + `OutboxStatus` enum [P]

**What**: `OutboxEntry` (Id, TargetTopic, MessageJson, Status, CreatedAt, RetryCount) and
`OutboxStatus` enum (`Pending`/`Published`/`Failed`, persisted as string per D-008).
**Where**: `02-src/03-Domain/RentifyxIdentity.Domain/Entities/OutboxEntry.cs`,
`02-src/03-Domain/RentifyxIdentity.Domain/Enums/OutboxStatus.cs`
**Depends on**: T1 (independent of T1's content, but sequenced after so both foundational Domain
pieces land before entity wiring touches them)
**Reuses**: D-008 string-enum convention, D-016 SK-equals-PK convention (documented on the item, not the entity itself)
**Requirement**: R-02 (data shape)

**Tools**: NONE

**Done when**:
- [ ] `OutboxEntry` constructs correctly, `RetryCount` defaults to 0
- [ ] Gate check passes: `dotnet test 03-tests/01-Common/RentifyxIdentity.Tests.Common`

**Tests**: unit
**Gate**: quick

**Commit**: `feat(domain): add OutboxEntry entity and OutboxStatus enum`

---

### T3: `PasswordResetRequested` event record [P]

**What**: New record `PasswordResetRequested(Guid UserId, string Email, string RawToken, DateTimeOffset OccurredAt) : IDomainEvent`.
**Where**: `02-src/03-Domain/RentifyxIdentity.Domain/Events/PasswordResetRequested.cs`
**Depends on**: T1
**Reuses**: existing event-record shape/style (`UserRegistered` etc.) as the pattern to match
**Requirement**: R-06 (event shape)

**Tools**: NONE

**Done when**:
- [ ] Record compiles, implements `IDomainEvent`
- [ ] Gate check passes: `dotnet test 03-tests/01-Common/RentifyxIdentity.Tests.Common`

**Tests**: unit
**Gate**: quick

**Commit**: `feat(domain): add PasswordResetRequested domain event`

---

### T4: `UserEntity` extends `AggregateRoot`, raises events from mutation methods

**What**: `UserEntity : AggregateRoot`. `VerifyEmail()`, `ResetPassword()`, `Suspend()`,
`Anonymize()` each call `RaiseDomainEvent(new XxxEvent(...))` using their existing (unchanged)
event records. **`Create()` does NOT raise `UserRegistered`** — corrected in design.md, that event
is handler-raised (T12/T13's concern, not this task's).
**Where**: `02-src/03-Domain/RentifyxIdentity.Domain/Entities/UserEntity.cs` (modify existing file)
**Depends on**: T2, T3
**Reuses**: all existing `UserEntity` logic — additive only, no signature changes (R-01's constraint)
**Requirement**: R-01

**Tools**: NONE

**Done when**:
- [ ] `UserEntity`'s existing public method signatures unchanged (verify by diff — no call site elsewhere needs to change)
- [ ] `VerifyEmail`/`ResetPassword`/`Suspend`/`Anonymize` each append exactly one event to `DomainEvents`
- [ ] `Create()` raises nothing (`DomainEvents` empty immediately after `Create()`, confirmed by a test)
- [ ] Existing `UserEntityTests` still pass unmodified (no test weakened to accommodate this change)
- [ ] Gate check passes: `dotnet test 03-tests/01-Common/RentifyxIdentity.Tests.Common && dotnet test 03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers`

**Tests**: unit (existing `UserEntityTests` extended, not replaced)
**Gate**: quick

**Commit**: `feat(domain): raise domain events from UserEntity mutation methods`

---

### T5: `OutboxDynamoDbItem` + mapper

**What**: DynamoDB item shape for `OutboxEntry` (`PK`/`SK` = `OUTBOX#{Id}` per D-016) and a static
mapper (`OutboxItemMapper.ToItem`/`FromItem`), matching `UserDynamoDbMapper`'s existing split.
**Where**: `02-src/05-Infrastructure/RentifyxIdentity.Infrastructure/Models/OutboxDynamoDbItem.cs`,
`02-src/05-Infrastructure/RentifyxIdentity.Infrastructure/Mapping/OutboxItemMapper.cs` (actual
existing folder names — `DynamoDb/` doesn't exist in this repo, corrected at implementation time)
**Depends on**: T4 (sequenced — not a hard dependency on T4's content, but keeps Domain fully
settled before Infrastructure mapping starts)
**Reuses**: `UserDynamoDbItem`/`UserDynamoDbMapper`'s exact file-split pattern; added
`OutboxEntry.Reconstitute()` (internal factory, mirrors `UserEntity.Reconstitute()`) since the
mapper needs to rebuild an entry with an existing `Status`/`RetryCount`, not just `Create()`'s
fresh-`Pending` shape
**Requirement**: R-02 (persistence shape)

**Tools**: NONE

**Done when**:
- [ ] Round-trip `ToItem(entry)` → `FromItem(item)` preserves all fields (deferred to T7's integration test, per correction note below)
- [x] `dotnet build 02-src/05-Infrastructure/RentifyxIdentity.Infrastructure -c Release` succeeds (re-verified 2026-07-16)

**Correction found at implementation time**: `OutboxItemMapper` is `internal` (matches
`UserDynamoDbMapper`'s existing visibility) — only `RentifyxIdentity.Infrastructure` and
`RentifyxIdentity.Tests.Repositories` have `InternalsVisibleTo` access (`Domain/Properties/
AssemblyInfo.cs`). `Tests.Handlers` cannot call it, so no standalone unit test is possible here,
matching the existing precedent: `UserDynamoDbMapper` itself has zero standalone unit tests either
— mapper correctness is proven only via `UserRepositoryTests`' Testcontainers integration tests.
The round-trip guarantee above is proven by **T7's** integration test instead, not this task.

**Tests**: none (see correction above — round-trip proven transitively by T7)
**Gate**: build

**Commit**: `feat(infra): add OutboxDynamoDbItem and mapper`

---

### T6: DynamoDB Terraform — `GSI_Outbox`

**What**: Add the new GSI (`PK = OUTBOX_STATUS#{Status}`, `SK = CreatedAt`) to this repo's own
DynamoDB table Terraform module.
**Where**: `iac/terraform/modules/dynamodb/main.tf` (existing module, add one GSI block)
**Depends on**: T5 (needs the item shape settled first)
**Reuses**: existing table's 2-GSI pattern (email/TaxId lookup) as the structural template
**Requirement**: R-02 (infra dependency)

**Tools**: NONE

**Done when**:
- [x] `terraform fmt -check && terraform validate` pass in `iac/terraform/` (re-verified 2026-07-16)
- [ ] GSI projection type matches what `T9`'s poll query actually needs (confirm at implementation — likely `ALL` or a minimal projection, decide when the poll query shape is known)

**Tests**: none (Terraform, no application test framework applies)
**Gate**: build (`terraform fmt -check && terraform validate`)

**Commit**: `feat(iac): add GSI_Outbox to DynamoDB table for outbox polling`

---

### T7: `IUserRepository`/`UserRepository` — atomic transactional write

**What**: New `AddAsync(UserEntity, IReadOnlyCollection<IDomainEvent>, ct)` /
`UpdateAsync(UserEntity, IReadOnlyCollection<IDomainEvent>, ct)` overloads on `IUserRepository`,
implemented via `IAmazonDynamoDB.TransactWriteItemsAsync` (user item Put + one Put per resulting
`OutboxEntry`), calling `entity.ClearDomainEvents()` after a successful transaction. Existing
no-events overloads become thin wrappers calling the new ones with an empty list — **no existing
call site changes**.
**Where**: `02-src/03-Domain/RentifyxIdentity.Domain/Interfaces/Users/IUserRepository.cs` (add
overloads), `02-src/05-Infrastructure/RentifyxIdentity.Infrastructure/Repositories/UserRepository.cs`
(modify)
**Depends on**: T6 (needs the GSI to exist before repository code that will eventually be polled via it lands, keeps infra ahead of application code)
**Reuses**: `UserDynamoDbMapper.ToItem`, `IDynamoDBContext.ToDocument()` (converts POCO → `AttributeValue` map for the low-level transact call), `OutboxItemMapper.ToItem` from T5
**Requirement**: R-02

**Tools**: `context7` (confirm current `TransactWriteItemsAsync`/`TransactWriteItem` request shape for the AWS SDK version pinned in this repo — new API surface for this codebase, do not guess field names)

**Done when**:
- [x] Existing `AddAsync(UserEntity, ct)`/`UpdateAsync(UserEntity, ct)` behavior unchanged for callers passing no events (verify existing `UserRepositoryTests` still pass) — verified 2026-07-16, all 13 pre-existing tests pass unmodified
- [x] New overload writes user item + N outbox items in one transaction — a forced failure on the outbox write also rolls back the user item (test with Testcontainers/LocalStack, not mocked — this is exactly the atomicity guarantee under test) — `AddAsync_WithExtraEvents_WritesUserAndOutboxItemsAtomically` (success path) + `AddAsync_TransactionFailsOnOutboxWrite_RollsBackUserItemToo` (forces a >400KB item to fail `TransactWriteItemsAsync` for real, not mocked)
- [x] `entity.ClearDomainEvents()` called only after the transaction actually succeeds
- [x] Gate check passes: `dotnet test 03-tests/04-Repositories/RentifyxIdentity.Tests.Repositories` (15/15 passing)

**Tests**: integration (Testcontainers + LocalStack, per TESTING.md matrix — this is exactly the kind of behavior mocks can't prove)
**Gate**: full

**Commit**: `feat(infra): atomic UserEntity + Outbox write via TransactWriteItemsAsync`

**Implementation notes (2026-07-16):**
- The design.md-listed `IOutboxEntryFactory` dependency doesn't exist yet (that's T9, sequenced after T8 in this doc). `UserRepository` uses a small private placeholder (`CreateOutboxEntry`) that serializes any `IDomainEvent` onto the generic `user-lifecycle-events` topic - T9 will need to inject the real factory here and replace this placeholder with per-event-type routing (comms-api's `DispatchNotificationRequest` shape for `UserRegistered`/`PasswordResetRequested`). Flagging so T9 doesn't get scoped as "just add the factory class" without also wiring it in.
- Found and fixed a pre-existing, unrelated bug while verifying this task: `LocalStackFixture.cs` set `RegionEndpoint` alongside `ServiceURL` on `AmazonDynamoDBConfig`, which makes every request fail with "The security token included in the request is invalid" against LocalStack (reproduced in an isolated console app outside the test project, confirmed on both AWSSDK.DynamoDBv2 4.0.21.7 and 4.0.101 - an SDK/LocalStack signing interaction, not a version issue). This silently blocked every LocalStack-backed repository test in this project until now on any machine with a working Docker install. Fixed by dropping `RegionEndpoint` - `ServiceURL` alone is sufficient.

---

### T8: `IOutboxRepository`/`OutboxRepository` — poll query

**What**: Thin repository for the publisher: `GetPendingAsync(int batchSize, ct)` (queries
`GSI_Outbox` for `Status = Pending`, ordered by `CreatedAt`), `MarkPublishedAsync(Guid id, ct)`,
`MarkFailedAsync(Guid id, ct)`, `IncrementRetryAsync(Guid id, ct)`.
**Where**: `02-src/03-Domain/RentifyxIdentity.Domain/Interfaces/Notifications/IOutboxRepository.cs`,
`02-src/05-Infrastructure/RentifyxIdentity.Infrastructure/Repositories/OutboxRepository.cs`
**Depends on**: T7
**Reuses**: `OutboxItemMapper` from T5, `GSI_Outbox` from T6
**Requirement**: R-03

**Tools**: NONE

**Done when**:
- [x] `GetPendingAsync` returns only `Pending` entries, respects `batchSize` — `GetNextSetAsync` used instead of `GetRemainingAsync` since the latter pages through the whole result set regardless of `Limit`
- [x] `MarkPublishedAsync`/`MarkFailedAsync`/`IncrementRetryAsync` each update exactly the targeted item
- [x] Gate check passes: `dotnet test 03-tests/04-Repositories/RentifyxIdentity.Tests.Repositories` (20/20 passing, 15 from T7 + 5 new)

**Tests**: integration (Testcontainers + LocalStack)
**Gate**: full

**Implementation notes (2026-07-16):** `LocalStackFixture`'s table now also provisions `GSI_Outbox` (`GsiOutboxStatusPk` hash / `CreatedAt` range) to match T6's real Terraform GSI. `OutboxItemMapper`/`OutboxDynamoDbItem` construction inside the new `OutboxRepositoryTests` is done directly against the public `OutboxDynamoDbItem` model (not the internal `OutboxItemMapper`, which only `RentifyxIdentity.Infrastructure` itself can see) - matches the constraint noted in T5.

**Commit**: `feat(infra): add OutboxRepository for publisher polling`

---

### T9: `IOutboxEntryFactory`/`OutboxEntryFactory`

**What**: Maps `IDomainEvent` → `OutboxEntry`, implementing design.md's routing table
(`UserRegistered`/`PasswordResetRequested` → `notification-requested` topic, comms-api's exact
`DispatchNotificationRequest` JSON shape; the other 4 events → `user-lifecycle-events`, generic
envelope).
**Where**: `02-src/02-Application/RentifyxIdentity.Application/Outbox/IOutboxEntryFactory.cs`,
`.../Outbox/OutboxEntryFactory.cs`
**Depends on**: T8
**Reuses**: comms-api's `DispatchNotificationRequest` shape (reproduced field-for-field, no shared
code/package — different repo/solution)
**Requirement**: R-05, R-06, R-07

**Tools**: NONE

**Done when**:
- [x] `UserRegistered` (with `RawToken`, added by this task since no earlier task touched that record — see Verify) maps to `DispatchNotificationRequest { TemplateId = "email-verification", Channel = "Email", Payload = { "token": RawToken }, CorrelationId = <new stable Guid per entry> }`
- [x] `PasswordResetRequested` maps to the same shape with `TemplateId = "password-reset"`
- [x] The other 4 events map to the generic `user-lifecycle-events` envelope
- [x] Gate check passes: `dotnet test 03-tests/03-Handlers/RentifyxIdentity.Tests.Handlers` (166/166 passing, 8 new)

**Verify**: this task also adds the `RawToken` field to the existing `UserRegistered` record
(`02-src/03-Domain/RentifyxIdentity.Domain/Events/UserRegistered.cs`) — confirm no other reader of
that record exists yet (grep `UserRegistered` — should only be this factory and, currently,
`RegisterUserHandler`'s dead-end log line, which T12 removes)

**Tests**: unit (pure mapping logic, no I/O)
**Gate**: quick

**Commit**: `feat(app): add OutboxEntryFactory, add RawToken to UserRegistered`

**Implementation notes (2026-07-16):** Verified via grep - `UserRegistered` had exactly one reader (`RegisterUserHandler`'s dead-end log line), updated to pass `rawToken`. `UserRegistered` and `UserLoggedIn` didn't implement `IDomainEvent` yet (both needed for the factory's `IReadOnlyCollection<IDomainEvent>` input) - added `: IDomainEvent` to both; no other change needed since both already had a matching `OccurredAt` property. Added an `OutboxEntry.Create(Guid id, string targetTopic, string messageJson)` overload so the factory can generate the entry's Id upfront and embed that same Id as `CorrelationId` in the serialized message, per design.md's `CorrelationId = outboxEntryId`. `NotificationRequestedMessage`/`UserLifecycleEventEnvelope` are `internal` records local to this task, not shared with comms-api (no project/package reference between the two repos). **This factory is not yet wired into `UserRepository`** (still uses T7's generic placeholder) or into any handler - that's T12/T13's job.

---

### T10: Add `Confluent.Kafka` package [P]

**What**: Add `Confluent.Kafka` to `Directory.Packages.props` and reference it from the
Infrastructure project.
**Where**: `Directory.Packages.props`, `02-src/05-Infrastructure/RentifyxIdentity.Infrastructure/RentifyxIdentity.Infrastructure.csproj`
**Depends on**: T0 (sequenced after doc reconciliation only for phase-ordering cleanliness, no real code dependency — first Kafka-touching task)
**Reuses**: comms-api's pinned `Confluent.Kafka` version as a reference point (confirm current version via Context7, don't assume it matches)
**Requirement**: R-10 (prerequisite)

**Tools**: `context7` (confirm current stable `Confluent.Kafka` version — new package for this repo)

**Done when**:
- [x] `dotnet restore` succeeds with the new package
- [x] Gate check passes: `dotnet build RentifyxIdentity.slnx -c Release`

**Tests**: none (package addition, no behavior yet)
**Gate**: build

**Commit**: `build(deps): add Confluent.Kafka package`

**Implementation notes (2026-07-16):** Confirmed 2.15.0 is both the current NuGet stable release and matches comms-api's own pin - no version drift between the two repos' Kafka clients. Added a new `Messaging` label group in `Directory.Packages.props`.

---

### T11: `IKafkaProducerFactory`/`KafkaProducerFactory` [P]

**What**: Builds a configured `IProducer<Null, string>`, broker address sourced from
`IConfiguration` (satisfied by local Aspire Kafka resource in dev, SSM-published
`/rentifyx/platform/kafka/bootstrap-servers` in prod — never hardcoded).
**Where**: `02-src/05-Infrastructure/RentifyxIdentity.Infrastructure/Messaging/KafkaProducerFactory.cs`
**Depends on**: T10
**Reuses**: comms-api's `IKafkaProducerFactory` shape as structural reference (different repo, no shared code)
**Requirement**: R-10

**Tools**: `context7` (confirm current `Confluent.Kafka` `ProducerBuilder` API — matches what comms-api's F-09 design already confirmed, but re-verify for this repo's pinned version)

**Done when**:
- [x] `Create()` returns a producer configured from `IConfiguration`, no hardcoded broker address anywhere
- [x] Gate check passes: `dotnet build RentifyxIdentity.slnx -c Release`

**Tests**: none (thin factory wrapping a well-tested external client; behavior proven at integration level in T13, not here — matches TESTING.md's existing precedent of not unit-testing thin SDK wrappers)
**Gate**: build

**Commit**: `feat(infra): add KafkaProducerFactory`

**Implementation notes (2026-07-16):** Mirrors comms-api's `KafkaProducerFactory` exactly (`configuration.GetConnectionString("kafka")`, `ProducerBuilder<Null, string>`), confirmed still the current stable API for `Confluent.Kafka` 2.15.0 via Context7. Registered as `AddSingleton<IKafkaProducerFactory, KafkaProducerFactory>()` in `InfrastructureDependencyInjection` for T12 to consume. **AppHost gap resolved same day**: added `Aspire.Hosting.Kafka` (9.3.1, matching comms-api's pin) and wired a standalone `builder.AddKafka("kafka").WithKafkaUI()` + `.WithReference(kafka)` on the API project in `01-aspire/01-AppHost/RentifyxIdentity.AppHost/AppHost.cs`, mirroring comms-api's AppHost exactly - `GetConnectionString("kafka")` now resolves in local dev.

---

### T12: `OutboxPublisher` hosted service

**What**: `IHostedService`, `PeriodicTimer`-driven poll loop: `IOutboxRepository.GetPendingAsync` →
produce each to its `TargetTopic` via `IKafkaProducerFactory` → `MarkPublishedAsync` on ack,
`IncrementRetryAsync` on failure, `MarkFailedAsync` + `Critical` log once `RetryCount >= 3` (R-04).
**Where**: `02-src/01-Api/RentifyxIdentity.Api/Messaging/OutboxPublisher.cs`
**Depends on**: T9, T11
**Reuses**: comms-api's `ReconciliationHostedService` polling shape as structural reference (`PeriodicTimer`, not DynamoDB Streams — design.md's decision)
**Requirement**: R-03, R-04

**Tools**: NONE

**Done when**:
- [x] A `Pending` entry gets produced and marked `Published` on ack (real Testcontainers.Kafka + LocalStack DynamoDB, not mocked)
- [x] A produce failure increments `RetryCount`; 3rd consecutive failure marks `Failed` and logs `Critical` (real Confluent.Kafka client against an unreachable broker, not a mocked failure)
- [x] `StopAsync` drains in-flight work before returning (mirrors `ReconciliationHostedService`'s `PeriodicTimer` + linked-timeout drain shape - this is the first `IHostedService` in this repo, so there was no prior convention to actually mirror, only comms-api's cross-repo one)
- [x] Gate check passes: `dotnet test RentifyxIdentity.slnx` (285/285 passing)

**Tests**: integration (the polling/produce/mark-status loop is exactly the kind of behavior that needs a real dependency, not mocks — matches TESTING.md's "Repositories: Integration" precedent extended to this hosted service)
**Gate**: full

**Commit**: `feat(api): add OutboxPublisher hosted service`

**Implementation notes (2026-07-16):** `OutboxPublisherOptions` (`PollIntervalSeconds`/`BatchSize`/`MaxRetryCount`, defaults 5/50/3) bound via `.Get<T>()` in `InfrastructureDependencyInjection`, same `Activator.CreateInstance<T>()` trap as T7/T8 avoided from the start this time. New packages: `Testcontainers.Kafka` 4.12.0 (test-only), `KafkaBuilder`'s parameterless constructor is obsolete now - pinned image `confluentinc/cp-kafka:7.6.0` (a `confluentinc/confluent-local` tag was tried first and failed to boot in KRaft combined mode - not this repo's bug, an image/wait-strategy mismatch).

**Real bug found and fixed**: registering `OutboxPublisher` unconditionally in `Program.cs` broke every single endpoint test using `CustomWebApplicationFactory` (46 tests, all of `RegisterEndpointTests`/`ConsentEndpointTests`/etc.) - `OutboxPublisher.StartAsync` calls `IKafkaProducerFactory.Create()`, which reads `GetConnectionString("kafka")`, absent in that test environment, throwing and aborting the whole host's startup (`IHostedService.StartAsync` failures are fatal to host startup by default) - manifested as "Server hasn't been initialized yet" on every test. Fixed by removing the `OutboxPublisher` hosted-service registration in `CustomWebApplicationFactory.ConfigureWebHost`, mirroring the `RemoveKafkaDependentHostedServices` pattern comms-api's own test factories already use for exactly this class of problem.

---

### T13: Cutover — `RegisterUserHandler` and `ForgotPasswordHandler`

**What**: Remove `IEmailService` calls from both handlers. `RegisterUserHandler` constructs
`UserRegistered` (now carrying `RawToken`) directly and passes it as an `extraEvents` argument to
`repository.AddAsync(user, [userRegisteredEvent], ct)` (per T4's correction — `Create()` itself
raises nothing). `ForgotPasswordHandler` does the same with `PasswordResetRequested`, preserving
the existing blind-success anti-enumeration behavior (only raises the event when a matching user is
actually found — same condition as today's SES call).
**Where**: `02-src/02-Application/RentifyxIdentity.Application/Features/Identity/Auth/Register/RegisterUserHandler.cs`,
`.../Auth/ForgotPassword/ForgotPasswordHandler.cs` (both modified, not rebuilt)
**Depends on**: T12
**Reuses**: existing handler structure — only the email-send step and the dead-end event-logging line change
**Requirement**: R-05, R-06

**Tools**: NONE

**Done when**:
- [ ] Neither handler references `IEmailService` anymore
- [ ] `RegisterUserHandlerTests`/`ForgotPasswordHandlerTests` updated: mock `IUserRepository`'s new overload instead of `IEmailService`; existing test count does not shrink (assertions moved, not deleted)
- [ ] `ForgotPasswordHandler`'s blind-success behavior (always 204/success response regardless of whether the user exists) is unchanged — verified by an existing test that already covers this
- [ ] Gate check passes: `dotnet test RentifyxIdentity.slnx`

**Tests**: unit (existing handler test files extended, `IEmailService` mock replaced with `IUserRepository`'s new overload)
**Gate**: full

**Commit**: `feat(app): cut over Register/ForgotPassword to Outbox, remove direct SES calls`

---

### T14: Remove `IEmailService`/`EmailService`/SES dead code

**What**: Delete `IEmailService`, `EmailService`, the `AWSSDK.SimpleEmailV2` package reference, and
the `Ses:FromAddress` config/secret entry, now that nothing calls them. Remove or explicitly mark
as a documented rollback path this repo's own `iac/terraform/modules/ses` (decide which at
implementation time per design.md's open item).
**Where**: `02-src/03-Domain/.../Interfaces/Users/IEmailService.cs`,
`02-src/05-Infrastructure/.../Services/EmailService.cs`, `Directory.Packages.props`,
`iac/terraform/modules/ses/` (review, not necessarily delete)
**Depends on**: T13
**Reuses**: n/a — deletion only
**Requirement**: R-09

**Tools**: NONE

**Done when**:
- [ ] Zero remaining references to `IEmailService`/`EmailService` (grep confirms)
- [ ] `AWSSDK.SimpleEmailV2` removed from `Directory.Packages.props`
- [ ] Decision recorded (in this repo's `STATE.md`, not silently done) on whether `iac/terraform/modules/ses` is deleted or kept as a documented rollback path
- [ ] Gate check passes: `dotnet build RentifyxIdentity.slnx -c Release && dotnet test RentifyxIdentity.slnx`

**Tests**: none (deletion; existing test count should drop only by tests that were testing the deleted code, never by silently deleting unrelated coverage — verify test count delta matches exactly what `EmailServiceTests` contributed)
**Gate**: build

**Commit**: `chore: remove IEmailService/EmailService, SES is fully replaced by Outbox`

---

## Parallel Execution Map

```
Phase 0: T0 [P] (independent)

Phase 1:
  T1 ──┬──→ T2 [P] ─┐
       └──→ T3 [P] ─┤
                     ▼
Phase 2:            T4

Phase 3 (sequential — each layer needs the one below):
  T4 → T5 → T6 → T7 → T8 → T9

Phase 4 (parallel with Phase 3, after T0):
  T0 → T10 [P] → T11 [P]

Phase 5 (sequential, needs both Phase 3 and Phase 4 done):
  T9, T11 → T12 → T13

Phase 6:
  T13 → T14
```

---

## Task Granularity Check

| Task | Scope | Status |
|---|---|---|
| T0: ROADMAP doc fix | 1 file | ✅ Granular |
| T1: IDomainEvent + AggregateRoot | 2 small files, 1 concept | ✅ Granular |
| T2: OutboxEntry + OutboxStatus | 2 files, cohesive (entity + its enum) | ✅ Granular |
| T3: PasswordResetRequested | 1 file | ✅ Granular |
| T4: UserEntity event-raising | 1 file (modified) | ✅ Granular |
| T5: OutboxDynamoDbItem + mapper | 2 files, cohesive (item + its mapper, matches existing UserDynamoDbItem/Mapper split) | ✅ Granular |
| T6: GSI_Outbox Terraform | 1 file (modified) | ✅ Granular |
| T7: UserRepository atomic write | 2 files (interface + impl), 1 concept | ✅ Granular |
| T8: OutboxRepository | 2 files (interface + impl), 1 concept | ✅ Granular |
| T9: OutboxEntryFactory | 2 files (interface + impl) + 1 record modified | ✅ Granular |
| T10: Confluent.Kafka package | 2 files (props + csproj) | ✅ Granular |
| T11: KafkaProducerFactory | 1 file | ✅ Granular |
| T12: OutboxPublisher | 1 file | ✅ Granular |
| T13: Handler cutover | 2 files (Register + ForgotPassword) — same pattern applied twice, cohesive | ✅ Granular |
| T14: Dead code removal | ~4 files, deletion only | ✅ Granular |

---

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
|---|---|---|---|
| T0 | None | independent [P] | ✅ Match |
| T1 | None | (start of Phase 1) | ✅ Match |
| T2 | T1 | T1 → T2 [P] | ✅ Match |
| T3 | T1 | T1 → T3 [P] | ✅ Match |
| T4 | T2, T3 | T2, T3 → T4 | ✅ Match |
| T5 | T4 | T4 → T5 | ✅ Match |
| T6 | T5 | T5 → T6 | ✅ Match |
| T7 | T6 | T6 → T7 | ✅ Match |
| T8 | T7 | T7 → T8 | ✅ Match |
| T9 | T8 | T8 → T9 | ✅ Match |
| T10 | T0 | T0 → T10 [P] | ✅ Match |
| T11 | T10 | T10 → T11 [P] | ✅ Match |
| T12 | T9, T11 | T9, T11 → T12 | ✅ Match |
| T13 | T12 | T12 → T13 | ✅ Match |
| T14 | T13 | T13 → T14 | ✅ Match |

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
|---|---|---|---|---|
| T0 | Docs only | none | none | ✅ OK |
| T1 | Domain (common base) | Unit (Tests.Common) | unit | ✅ OK |
| T2 | Domain entity/enum | Unit (Tests.Common) | unit | ✅ OK |
| T3 | Domain event record | Unit (Tests.Common) | unit | ✅ OK |
| T4 | Domain entity (modified) | Unit (Tests.Handlers/Common) | unit | ✅ OK |
| T5 | Infrastructure (DynamoDB item/mapper) | Unit (matches UserDynamoDbMapper precedent — mapping is pure, no I/O) | unit | ✅ OK |
| T6 | Terraform (IaC) | none (no test framework for IaC) | none | ✅ OK |
| T7 | Repositories | Integration (Testcontainers) | integration | ✅ OK |
| T8 | Repositories | Integration (Testcontainers) | integration | ✅ OK |
| T9 | Application (handler-adjacent mapping) | Unit (Tests.Handlers) | unit | ✅ OK |
| T10 | Build config | none | none | ✅ OK |
| T11 | Infrastructure (thin SDK wrapper) | none (matches existing precedent — thin wrappers around external clients aren't independently unit-tested in this repo; proven via T12's integration test) | none | ✅ OK |
| T12 | Api (hosted service) | Integration (mirrors Repositories' Testcontainers precedent — the matrix has no explicit "Hosted Services" row, closest analog is Repositories since both need a real external dependency to prove behavior) | integration | ✅ OK |
| T13 | Application (handlers) | Unit (Tests.Handlers) | unit | ✅ OK |
| T14 | Deletion | none | none | ✅ OK |

---

## Tools Question for User

Context7 required (not optional) on T7 (`TransactWriteItemsAsync` request shape — new API surface
for this codebase), T10 (current `Confluent.Kafka` stable version), and T11 (`ProducerBuilder` API
for the version T10 pins). No other MCPs/skills assumed. Confirm before Execute starts, or say if
you'd rather I proceed with these defaults.
