# Distributed Tracing Across the Kafka Boundary Tasks

**Design**: `.specs/features/distributed-tracing-kafka/design.md`
**Status**: Done — T1-T12 all implemented. identity-api PR #50, rentifyx-communications-api PR #22 (both open, not yet merged, 2026-08-05).

**Deviations from plan** (see individual commit messages for full SPEC_DEVIATION rationale):
- T1+T2 merged into one commit (tightly coupled, couldn't build independently)
- T8 scope expanded beyond plan (full-domain rename in comms-api, not just the wire DTO) per explicit user decision after being asked
- T12 needed no signature change to `IFailureRouter.RouteAsync`/`ProcessAsync` — `RetryContext` already carried the needed data end-to-end, so `TraceParent`/`TraceState` were added as fields on it instead of threading a new headers parameter through three signatures

**Note on test conventions**: `rentifyx-communications-api` has no `.specs/codebase/TESTING.md`.
Conventions below for that repo (xUnit/Moq/FluentAssertions, mirrored `00`-`06` test-project
structure) are inferred directly from its existing test files
(`NotificationRequestedConsumerTests.cs`, `RetryTopicConsumerTests.cs`,
`NotificationDispatchProcessorTests.cs`) rather than invented.

---

## Execution Plan

### Phase 1: identity-api domain + persistence foundation (Sequential)

```
T1 → T2 → T3
```

### Phase 2: identity-api producer wiring (Sequential, depends on Phase 1)

```
T3 → T4 → T5 → T6
```

### Phase 3: identity-api field rename (Sequential, independent of Phase 2 but same repo/build)

```
T7
```

### Phase 4: comms-api field rename + consumer wiring (Parallel OK, depends on T7 for the wire-contract shape)

```
T7 ──┬→ T8 ────────────┐
     ├→ T9 ─┬→ T10 [P] ─┼→ T12
     │      └→ T11 [P] ─┘
```

### Phase 5: P3 retry-topic trace continuity (Sequential, depends on Phase 4)

```
T10, T11 → T12
```

---

## Task Breakdown

### T1: Add `TraceParent`/`TraceState` to `OutboxEntry`

**What**: Two new nullable string properties on the domain entity, threaded through `Create`/`Reconstitute`, mirroring the existing `PartitionKey` shape from D-024.
**Where**: `02-src/03-Domain/RentifyxIdentity.Domain/Entities/OutboxEntry.cs`
**Depends on**: None
**Reuses**: `PartitionKey`'s exact optional-param/private-setter/`Reconstitute` pattern (same file, added this same session for D-024)
**Requirement**: TRACE-01

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `TraceParent`/`TraceState` (`string?`) added as private-set properties
- [ ] `Create(string targetTopic, string messageJson, string? partitionKey = null, string? traceParent = null, string? traceState = null)` and the `Guid id` overload both updated
- [ ] `Reconstitute(...)` gains `traceParent`/`traceState` params
- [ ] `03-tests/03-Handlers/.../Common/OutboxEntryTests.cs` unaffected (defaults still work with no new args)
- [ ] Gate check passes: `dotnet test 03-tests/02-Validators && dotnet test 03-tests/03-Handlers`
- [ ] Test count: 168 Handlers tests still pass, 0 removed

**Tests**: unit (Domain entities per identity-api's TESTING.md matrix)
**Gate**: quick

**Commit**: `feat(outbox): add TraceParent/TraceState to OutboxEntry`

---

### T2: Persist `TraceParent`/`TraceState` through the DynamoDB layer

**What**: Add matching columns to `OutboxDynamoDbItem` and map them in `OutboxItemMapper` (`ToItem`/`FromItem`).
**Where**: `02-src/05-Infrastructure/RentifyxIdentity.Infrastructure/Models/OutboxDynamoDbItem.cs`, `.../Mapping/OutboxItemMapper.cs`
**Depends on**: T1
**Reuses**: `PartitionKey`'s exact plain-string-column pattern in both files (no `[DynamoDBGlobalSecondaryIndex...]` attribute needed — same as `PartitionKey`)
**Requirement**: TRACE-01

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `OutboxDynamoDbItem.TraceParent`/`TraceState` (`string?`) added
- [ ] `OutboxItemMapper.ToItem` sets both from `entry.TraceParent`/`entry.TraceState`
- [ ] `OutboxItemMapper.FromItem` passes both into `OutboxEntry.Reconstitute`
- [ ] Gate check passes: `dotnet build RentifyxIdentity.slnx` (whole-solution build, since `03-tests/04-Repositories`/`05-Integration` reference these types but require Docker to actually run)
- [ ] Test count: N/A (no new tests at this layer alone — covered by T5's integration touch points)

**Tests**: none (pure data-shape change; covered transitively by existing repository/integration tests once Docker is available — matches how `PartitionKey`'s equivalent commit was verified this session)
**Gate**: build

**Commit**: `feat(outbox): persist TraceParent/TraceState in DynamoDB item mapping`

---

### T3: Add `OutboxActivitySource` and register it with the tracer provider

**What**: New static `ActivitySource` holder class, registered via `.AddSource(...)` in `ServiceDefaults/Extensions.cs`'s `WithTracing` call.
**Where**: `02-src/01-Api/RentifyxIdentity.Api/Messaging/OutboxActivitySource.cs` (new file), `01-aspire/02-ServiceDefaults/RentifyxIdentity.ServiceDefaults/Extensions.cs` (modify)
**Depends on**: None
**Reuses**: `KafkaTopics`/`DynamoDbConstants`-style single-constant-holder class pattern
**Requirement**: TRACE-02

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `internal static class OutboxActivitySource { internal const string Name = "RentifyxIdentity.Outbox"; internal static readonly ActivitySource Instance = new(Name); }`
- [ ] `Extensions.cs`'s tracing builder gains `.AddSource(OutboxActivitySource.Name)` — placed unconditionally (not inside the OTLP-endpoint-configured branch), per design's error-handling note that spans/headers must work even with no exporter configured
- [ ] Gate check passes: `dotnet build RentifyxIdentity.slnx`

**Tests**: none (registration wiring, no unit-testable behavior in isolation)
**Gate**: build

**Commit**: `feat(tracing): add OutboxActivitySource and register with tracer provider`

---

### T4: `OutboxEntryFactory` captures `Activity.Current` at entry-creation time

**What**: Every `OutboxEntry.Create(...)` call site in the factory also passes `traceParent: Activity.Current?.Id, traceState: Activity.Current?.TraceStateString`.
**Where**: `02-src/02-Application/RentifyxIdentity.Application/Outbox/OutboxEntryFactory.cs`
**Depends on**: T1
**Reuses**: The exact call sites already touched this session for `PartitionKey` (D-024) — one more pair of named args per branch
**Requirement**: TRACE-01

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `using System.Diagnostics;` added
- [ ] All three `OutboxEntry.Create(...)` branches (`UserRegistered`, `PasswordResetRequested`, lifecycle fallback) pass `traceParent`/`traceState`
- [ ] `03-tests/03-Handlers/.../Outbox/OutboxEntryFactoryTests.cs` extended: at least one test asserts `entry.TraceParent` is null when no `Activity` is current (the existing test-run default — xUnit doesn't start an `Activity` per test) and, separately, a test that wraps the `CreateEntries` call in a manually-started `Activity` and asserts `entry.TraceParent` matches `activity.Id`
- [ ] Gate check passes: `dotnet test 03-tests/03-Handlers`
- [ ] Test count: 168 + at least 2 new tests, 0 removed

**Tests**: unit
**Gate**: quick

**Commit**: `feat(outbox): capture Activity.Current trace context when creating OutboxEntry`

---

### T5: `OutboxPublisher` starts a linked publish `Activity` and injects W3C headers

**What**: Implement the `PublishEntryAsync` change from design.md exactly — build `ActivityLink[]` from `entry.TraceParent`/`TraceState` via `ActivityContext.TryParse`, start a `Producer`-kind `Activity` via `OutboxActivitySource.Instance`, inject its context into Kafka `Message.Headers` via `TraceContextPropagator`, push `TraceId`/`SpanId` into Serilog `LogContext` for the duration.
**Where**: `02-src/01-Api/RentifyxIdentity.Api/Messaging/OutboxPublisher.cs`
**Depends on**: T1, T2, T3
**Reuses**: `CorrelationIdMiddleware`'s `LogContext.PushProperty` pattern (same file family, `02-src/01-Api/.../Middlewares/`); `entry.PartitionKey ?? string.Empty` line stays unchanged
**Requirement**: TRACE-02, TRACE-05 (missing/malformed header → no link, no crash)

**Tools**:
- MCP: `context7` (re-verify `ActivityContext.TryParse` exact overload signature at implementation time, per design.md's flagged uncertainty)
- Skill: NONE

**Done when**:
- [ ] `ProduceAsync` call includes a populated `Headers` (was previously omitted entirely)
- [ ] `ActivityContext.TryParse` failure (null/malformed `TraceParent`) results in zero links, not an exception
- [ ] `Message.Headers["traceparent"]`/`["tracestate"]` present and W3C-formatted when the publish `Activity` is non-null
- [ ] `03-tests/05-Integration/.../Messaging/OutboxPublisherTests.cs` extended: seed an entry with a known `TraceParent`, assert the consumed-back Kafka message (via `WorkingKafkaProducerFactory`'s real broker) carries a `traceparent` header — new `[RequiresDocker]` test, not run in this session (no Docker daemon locally) but must compile and pass structurally
- [ ] Gate check passes: `dotnet build RentifyxIdentity.slnx` (compiles); `dotnet test 03-tests/03-Handlers` (unaffected tests still 168+ green)

**Tests**: integration (`RequiresDocker`, per identity-api's matrix — API endpoints/messaging touch points go through `05-Integration`)
**Gate**: full

**Commit**: `feat(outbox): start linked publish Activity and inject traceparent into Kafka headers`

---

### T6: Verify identity-api Phase 1-2 build/test gate

**What**: Run the full non-Docker suite once after T1-T5 land, confirm no regressions before moving to the field-rename task.
**Where**: N/A (verification task, no file changes)
**Depends on**: T5
**Reuses**: N/A
**Requirement**: N/A (gate, not a requirement)

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `dotnet build RentifyxIdentity.slnx --configuration Release` clean
- [ ] `dotnet test RentifyxIdentity.slnx --filter "Category!=RequiresDocker"` green, count ≥ (168 Handlers + 51 Validators + prior Integration count), 0 unexplained removals

**Tests**: none (this task IS the test gate)
**Gate**: full

**Commit**: none (verification only — no commit)

---

### T7: Rename `CorrelationId` → `IdempotencyKey` (identity-api side)

**What**: `NotificationRequestedMessage.CorrelationId` renamed to `IdempotencyKey`; every call site (`OutboxEntryFactory.SerializeNotificationRequested`) and test assertion updated to match.
**Where**: `02-src/02-Application/RentifyxIdentity.Application/Outbox/NotificationRequestedMessage.cs`, `.../Outbox/OutboxEntryFactory.cs`, `03-tests/03-Handlers/.../Outbox/OutboxEntryFactoryTests.cs`
**Depends on**: None (independent of Phase 1-2's tracing work — pure rename)
**Reuses**: N/A — this is the rename itself
**Requirement**: TRACE-06

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] Property renamed in the record; JSON output field is `IdempotencyKey` (record positional property → matching JSON name, no `[JsonPropertyName]` needed since `System.Text.Json` uses the property name by default and no custom naming policy was found in `OutboxEntryFactory`'s `JsonSerializer.Serialize` call)
- [ ] All 3 existing `entries[0]`/message-JSON assertions in `OutboxEntryFactoryTests.cs` (`GetProperty("CorrelationId")` → `GetProperty("IdempotencyKey")`) updated
- [ ] Gate check passes: `dotnet test 03-tests/03-Handlers`
- [ ] Test count: same count as before (rename only, no new/removed tests)

**Tests**: unit
**Gate**: quick

**Commit**: `refactor(outbox): rename CorrelationId to IdempotencyKey (matches actual semantics)`

---

### T8: Rename `CorrelationId` → `IdempotencyKey` (comms-api side) + update contract doc

**What**: `DispatchNotificationRequest.CorrelationId` renamed to `IdempotencyKey`; every usage (`NotificationDispatchProcessor`'s log statements and any dedup-key construction referencing it) updated; `docs/contracts/notification-requested.md` updated to document the new field name and explicitly note it's an idempotency key, distinct from the new `traceparent`-based tracing.
**Where** (`rentifyx-communications-api`): `02-src/02-Application/.../Dispatch/Request/DispatchNotificationRequest.cs`, `.../Common/NotificationDispatchProcessor.cs`, any handler/repository using the field for the DynamoDB `PK=NOTIF#{...}` dedup key (locate via grep on `CorrelationId` in that repo — the earlier research pass did not pin the exact dedup-key-builder file), `docs/contracts/notification-requested.md`
**Depends on**: T7 (must land after identity-api's producer stops calling it `CorrelationId`, so the wire contract stays consistent — though technically independent code, sequencing avoids a window where the two repos disagree on the field name)
**Reuses**: N/A — rename

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] Property renamed; every `CorrelationId` reference in this repo's notification-dispatch path updated (grep for zero remaining `CorrelationId` occurrences tied to this DTO — `RecipientId`/other fields untouched)
- [ ] DynamoDB dedup key value construction unchanged (still uses the same GUID value, only the C#/JSON property name changed)
- [ ] `docs/contracts/notification-requested.md` documents `IdempotencyKey` and calls out that trace correlation now uses `traceparent`/`tracestate` Kafka headers instead
- [ ] Existing `NotificationDispatchProcessorTests.cs` assertions updated to the new property name
- [ ] Gate check passes: comms-api's handler/unit test suite (mirror identity-api's `quick` gate — `dotnet test` on the Handlers-equivalent project)

**Tests**: unit
**Gate**: quick

**Commit**: `refactor(notifications): rename CorrelationId to IdempotencyKey (matches actual semantics)`

---

### T9: Add comms-api's `ActivitySource` and register it

**What**: Mirrors T3 for `rentifyx-communications-api` — new `MessagingActivitySource` holder, registered in that repo's own `ServiceDefaults/Extensions.cs`.
**Where** (`rentifyx-communications-api`): `02-src/01-Api/RentifyxCommunications.Api/Messaging/MessagingActivitySource.cs` (new), `01-aspire/.../RentifyxCommunications.ServiceDefaults/Extensions.cs`
**Depends on**: None
**Reuses**: Same pattern as T3
**Requirement**: TRACE-03

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `internal static class MessagingActivitySource { internal const string Name = "RentifyxCommunications.Messaging"; internal static readonly ActivitySource Instance = new(Name); }`
- [ ] `.AddSource(MessagingActivitySource.Name)` added to that repo's tracing builder, unconditionally
- [ ] Gate check passes: comms-api's build

**Tests**: none
**Gate**: build

**Commit**: `feat(tracing): add MessagingActivitySource and register with tracer provider`

---

### T10: `NotificationRequestedConsumer` extracts `traceparent` and starts a child `Activity` [P]

**What**: Before calling `NotificationDispatchProcessor.ProcessAsync`, read `traceparent`/`tracestate` from `result.Message.Headers` (UTF8-decode byte[] values), `ActivityContext.TryParse`, `StartActivity(name, ActivityKind.Consumer, parentContext)`, push `TraceId`/`SpanId` into `LogContext` for the `ProcessAsync` call's duration.
**Where** (`rentifyx-communications-api`): `02-src/01-Api/RentifyxCommunications.Api/Messaging/NotificationRequestedConsumer.cs`
**Depends on**: T9
**Reuses**: `RetryTopicConsumer.cs`'s existing header-reading helper for `x-original-topic`/etc. — extend/extract it into a shared helper rather than duplicating byte[]→string decode logic twice (T11 needs the identical extraction)
**Requirement**: TRACE-03, TRACE-04, TRACE-05

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] Missing/malformed `traceparent` header → `StartActivity` still runs with a `default` parent context (root trace), no exception
- [ ] `03-tests\06-Api\...\Messaging\NotificationRequestedConsumerTests.cs` extended: one test with a valid `traceparent` header asserting the started `Activity`'s parent matches; one test with no header asserting a root `Activity` still starts
- [ ] Gate check passes: `dotnet test 03-tests/06-Api/...` (or whichever project actually hosts `NotificationRequestedConsumerTests.cs`, confirmed as `06-Api` from this session's grep)
- [ ] Test count: existing count + 2, 0 removed

**Tests**: unit (mirrors the existing test file's own established pattern for this class)
**Gate**: quick

**Commit**: `feat(messaging): extract traceparent header and start consumer Activity in NotificationRequestedConsumer`

---

### T11: `RetryTopicConsumer` extracts `traceparent` and starts a child `Activity` [P]

**What**: Identical change to T10, applied to the retry-stage consumer.
**Where** (`rentifyx-communications-api`): `02-src/01-Api/RentifyxCommunications.Api/Messaging/RetryTopicConsumer.cs`
**Depends on**: T9
**Reuses**: The shared header-extraction helper introduced in T10 (do not duplicate)
**Requirement**: TRACE-03, TRACE-10

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] Same behavior as T10, applied to `RetryTopicConsumer`
- [ ] `RetryTopicConsumerTests.cs` extended with the same two test cases as T10
- [ ] Gate check passes: same project as T10
- [ ] Test count: existing count + 2, 0 removed

**Tests**: unit
**Gate**: quick

**Commit**: `feat(messaging): extract traceparent header and start consumer Activity in RetryTopicConsumer`

---

### T12: Forward `traceparent`/`tracestate` onto retry topics via `KafkaFailureRouter`

**What**: Thread the original message's `traceparent`/`tracestate` header values from the consumer, through `NotificationDispatchProcessor.ProcessAsync`, into `IFailureRouter.RouteAsync`/`KafkaFailureRouter.RouteAsync`, so they're copied into the new `Headers` built for the next retry-topic hop — exactly like the existing `x-original-topic`/`x-retry-count`/etc. headers already are.
**Where** (`rentifyx-communications-api`): `02-src/03-Domain/.../Interfaces/Notifications/IFailureRouter.cs`, `02-src/05-Infrastructure/.../Messaging/KafkaFailureRouter.cs`, `02-src/02-Application/.../Common/NotificationDispatchProcessor.cs`, both consumer classes (pass their `result.Message.Headers` through)
**Depends on**: T10, T11
**Reuses**: `KafkaFailureRouter.cs`'s existing `Headers` construction block — add two more conditional `Headers.Add(...)` calls, same shape as the existing five
**Requirement**: TRACE-09, TRACE-10

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `RouteAsync`'s signature gains an `IReadOnlyDictionary<string, byte[]>? originalHeaders` (or equivalent) parameter
- [ ] Both call sites in `NotificationDispatchProcessor` pass the headers through (currently only `rawMessage`/`context`/classification/exception info — the consumer classes must pass their own `result.Message.Headers` down into `ProcessAsync` first, which itself needs a new parameter)
- [ ] `KafkaFailureRouter.RouteAsync` copies `traceparent`/`tracestate` from `originalHeaders` into the new `Headers` if present, skips silently if absent
- [ ] Existing `KafkaFailureRouter` unit/integration tests (locate via grep — not yet confirmed to exist; if none exist, this task adds the first one covering header forwarding) extended/created to assert forwarding
- [ ] Gate check passes: comms-api's Handlers + Infrastructure test projects

**Tests**: unit
**Gate**: quick

**Commit**: `feat(messaging): forward traceparent/tracestate to retry topics via KafkaFailureRouter`

---

## Parallel Execution Map

```
Phase 1 (Sequential, identity-api):
  T1 ──→ T2 ──→ T3

Phase 2 (Sequential, identity-api):
  T3 ──→ T4 ──→ T5 ──→ T6

Phase 3 (Sequential, identity-api):
  T7 (independent of T1-T6, can run any time before T8)

Phase 4 (Parallel OK, comms-api, after T7):
  T7 ──→ T8
       ├── T9 ──┬── T10 [P]
       │        └── T11 [P]

Phase 5 (Sequential, comms-api):
  T10, T11 ──→ T12
```

---

## Task Granularity Check

| Task | Scope | Status |
|------|-------|--------|
| T1: Add fields to OutboxEntry | 1 file | ✅ Granular |
| T2: Persist through DynamoDB layer | 2 files, 1 concept (mapping) | ✅ Granular |
| T3: Add ActivitySource | 2 files, 1 concept (registration) | ✅ Granular |
| T4: Factory captures Activity.Current | 1 file | ✅ Granular |
| T5: Publisher starts linked Activity + headers | 1 file | ✅ Granular |
| T6: Verify gate | 0 files (verification) | ✅ Granular |
| T7: Rename field (identity-api) | 3 files, 1 concept (rename) | ✅ Granular |
| T8: Rename field (comms-api) + docs | 3-4 files, 1 concept (rename) | ✅ Granular |
| T9: Add ActivitySource (comms-api) | 2 files, 1 concept | ✅ Granular |
| T10: Consumer extracts header (Notification) | 1 file | ✅ Granular |
| T11: Consumer extracts header (Retry) | 1 file | ✅ Granular |
| T12: Forward headers through failure router | 4-5 files, 1 concept (header threading) | ✅ Granular |

---

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
|------|-------------------------|----------------|--------|
| T1 | None | None | ✅ Match |
| T2 | T1 | T1→T2 | ✅ Match |
| T3 | None | (T2→T3 shown in Phase 1 diagram — T3 has no real code dependency on T2, but is sequenced after it since both land in the same phase before Phase 2 begins) | ✅ Match (sequencing, not a hard dependency — noted) |
| T4 | T1 | T3→T4 (Phase 2) | ✅ Match — T4 depends on T1 (already satisfied by Phase 1), diagram shows phase order not just direct deps |
| T5 | T1, T2, T3 | T4→T5 | ✅ Match — same phase-order note as T4 |
| T6 | T5 | T5→T6 | ✅ Match |
| T7 | None | Shown as independent, feeding Phase 4 | ✅ Match |
| T8 | T7 | T7→T8 | ✅ Match |
| T9 | None | T7→T9 shown for phase sequencing only (no real dependency — T9 could start immediately) | ✅ Match (sequencing, not hard dependency — noted) |
| T10 | T9 | T9→T10 [P] | ✅ Match |
| T11 | T9 | T9→T11 [P] | ✅ Match |
| T12 | T10, T11 | T10,T11→T12 | ✅ Match |

**Note**: T9 has no real code dependency on T7/T8 and could execute in parallel with them — the
diagram groups it into "Phase 4" for readability only. Marking it `[P]` alongside T8 would also be
valid; kept sequential-looking in the diagram to keep the phase count small. Execution will run T8
and T9 concurrently in practice.

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
|------|------------------------------|-------------------|-----------|--------|
| T1 | Domain entity (`OutboxEntry`) | Unit (no mocks) | unit | ✅ OK |
| T2 | Infrastructure mapping | none (matches how `PartitionKey`'s equivalent change was verified this session — build-only, transitively covered by Docker-gated tests) | none | ✅ OK |
| T3 | DI/tracer registration | none (no coverage-matrix row for this — infra wiring) | none | ✅ OK |
| T4 | Application (`OutboxEntryFactory`) | Unit (Handlers row covers Application-layer factories in this repo's matrix) | unit | ✅ OK |
| T5 | API messaging (`OutboxPublisher`) | Integration (API Endpoints/Middleware row — this is the closest matrix entry for background messaging components under `02-src/01-Api`) | integration | ✅ OK |
| T6 | N/A (gate task) | N/A | none | ✅ OK |
| T7 | Application (message DTO + factory) | Unit | unit | ✅ OK |
| T8 | Application/Infrastructure (comms-api) | Inferred unit (matches `NotificationDispatchProcessorTests.cs`'s existing location in `03-Handlers`) | unit | ✅ OK |
| T9 | DI/tracer registration | none | none | ✅ OK |
| T10 | API messaging consumer | Inferred unit (matches `NotificationRequestedConsumerTests.cs`'s existing location in `06-Api`) | unit | ✅ OK |
| T11 | API messaging consumer | Inferred unit (matches `RetryTopicConsumerTests.cs`'s existing location in `06-Api`) | unit | ✅ OK |
| T12 | Domain interface + Infrastructure + Application | Unit | unit | ✅ OK |

---

## Notes for Execute Phase

- T5's new integration test is `[RequiresDocker]` and won't actually run in this session (no local
  Docker daemon, consistent with this session's earlier D-024 work) — it must compile and be
  structurally correct; full pass/fail confirmation is deferred to CI or a Docker-available
  session, same caveat already accepted for the D-024 PR.
- T8's exact dedup-key-builder file in comms-api wasn't pinned during Design — T8's sub-agent must
  grep for `CorrelationId` in that repo at task start (Knowledge Verification Chain Step 1) rather
  than assume a location.
- T12's exact `KafkaFailureRouter` test file (if any) wasn't confirmed during Design — same
  grep-first requirement.
