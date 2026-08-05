# Distributed Tracing Across the Kafka Boundary Specification

## Problem Statement

A user-facing request (e.g. `POST /register`) triggers a chain that crosses process and repo
boundaries: identity-api's HTTP handler → `OutboxEntry` (persisted, published later by a
background poller) → Kafka topic → `rentifyx-communications-api`'s consumer → SES send. Today
there is no way to follow one request through that whole chain. OpenTelemetry tracing exists on
both sides but only instruments ASP.NET Core/HttpClient — nothing wraps the Kafka
produce/consume boundary, so the trace stops dead the moment a domain event is written to the
Outbox. The `CorrelationId` field that already rides in the Kafka payload doesn't help either:
it's actually the Outbox entry's own ID, used by comms-api as an idempotency/dedup key
(`PK=NOTIF#{correlationId}` in DynamoDB) — it has no relationship to the originating HTTP
request and was never meant to.

## Goals

- [ ] A single trace, visible in the OTel backend/Aspire dashboard, spans: HTTP request →
      Outbox write → (deferred) Kafka publish → Kafka consume → dispatch processing in
      comms-api — using standard W3C Trace Context (`traceparent`), not a bespoke ID.
- [ ] The existing `CorrelationId` field (Outbox entry ID / idempotency key) is renamed to a name
      that reflects what it actually is, so it stops being confused with real trace/correlation
      concepts.
- [ ] Both repos' structured logs (Serilog) include the active trace/span IDs, so log lines from
      either service for the same request can be found by the same identifier without opening a
      tracing UI.

## Out of Scope

| Feature                                                              | Reason                                                                                   |
| ---------------------------------------------------------------------| ------------------------------------------------------------------------------------------ |
| Full OTel Kafka auto-instrumentation package                         | No such package exists for .NET/Confluent.Kafka today (verified — none in either repo's `Directory.Packages.props`, none found via research). Manual `Activity`/header propagation is the only option. |
| Tracing across SES itself (email send latency inside AWS)            | Out of either repo's control; HttpClient instrumentation already covers the outbound SES SDK call at a shallow level. |
| Retrofitting `UserStatusSnapshotPublisher` (admin snapshot broadcast)| Fire-and-forget admin tool, not triggered by a traceable user request — no meaningful parent trace to link to. |
| DLQ topic trace continuity                                           | DLQ is a terminal, manually-inspected state; P3 covers retry-topic continuity only, not DLQ. |
| Backward-compatible dual-field support for the renamed contract field| No real production traffic exists in either repo (both explicitly non-production study projects per their STATE.md) — clean rename, no migration shim needed. |

---

## User Stories

### P1: End-to-end trace from HTTP request through Kafka consume ⭐ MVP

**User Story**: As a developer debugging a failed/slow registration flow, I want one trace ID
that covers the HTTP request, the Outbox publish, and the comms-api consume/dispatch, so that I
can find every log line and span for that one user action without manually cross-referencing IDs.

**Why P1**: This is the entire point of the feature — without producer→consumer span linking,
nothing else in this spec has value.

**Acceptance Criteria**:

1. WHEN a domain event is turned into an `OutboxEntry` THEN the entry SHALL capture the
   originating request's trace context (`traceparent`) at creation time, because the actual
   Kafka publish happens later, asynchronously, in a background poller where the original
   `Activity` is no longer current.
2. WHEN `OutboxPublisher` actually produces a message to Kafka THEN it SHALL start a new
   `Activity` for the publish operation, linked to the captured trace context via an OTel
   `ActivityLink` (not reparented — the publish is causally related but temporally
   independent, per OTel semantic conventions for async messaging), and inject that new
   Activity's `traceparent` into the Kafka message headers.
3. WHEN comms-api's `NotificationRequestedConsumer` receives a message THEN it SHALL read the
   `traceparent` header, start a child `Activity` under that parent context wrapping the
   dispatch/processing work, and both spans SHALL appear connected in the OTel backend.
4. WHEN either service logs during this flow THEN the log entry SHALL include the active
   `TraceId`/`SpanId` (Serilog enrichment), so `grep`-ing a trace ID across both services' logs
   returns every related line.
5. WHEN a Kafka message has no `traceparent` header (e.g. produced by an older/未-instrumented
   path) THEN the consumer SHALL start a fresh root trace rather than throwing.

**Independent Test**: Register a user locally with both services running against the same OTel
collector/Aspire dashboard; confirm one trace shows HTTP span → Outbox publish span → Kafka
consume span → dispatch span, and that `grep`-ing the printed trace ID across both services'
console logs returns matching lines.

---

### P2: Rename the misleading `CorrelationId` field ⭐ MVP

**User Story**: As a developer reading the Kafka payload contract, I want the field that's
actually an idempotency key to be named accurately, so it isn't mistaken for the new trace
correlation mechanism.

**Why P2**: Directly requested by the user once P1 introduces a *real* correlation mechanism
(`traceparent`) — keeping a field called `CorrelationId` that means something else would make
the contract actively misleading going forward.

**Acceptance Criteria**:

1. WHEN `OutboxEntryFactory` builds a `NotificationRequestedMessage` THEN the field currently
   named `CorrelationId` SHALL be renamed to `IdempotencyKey` (still populated with the Outbox
   entry's own `Guid`, semantics unchanged — only the name changes).
2. WHEN comms-api deserializes the same payload (`DispatchNotificationRequest`) THEN its field
   SHALL be renamed identically (`IdempotencyKey`), and every reference to it (dedup lookup,
   DynamoDB `PK=NOTIF#{...}` construction, logs) SHALL be updated to match — the DynamoDB key
   *value* is unchanged, only the C#/JSON property name.
3. WHEN `docs/contracts/notification-requested.md` (comms-api) is read THEN it SHALL document
   the new field name and explicitly distinguish it from the new `traceparent`-based tracing
   mechanism, so a future reader doesn't reintroduce the same confusion.

**Independent Test**: Deserialize a freshly-produced message in comms-api and confirm the
idempotency dedup path (duplicate delivery within the retry window) still works — dedup is keyed
by the same GUID value under its new field name.

---

### P3: Trace continuity across comms-api's retry topics

**User Story**: As a developer investigating a notification that failed and got retried, I want
the retry attempts to still show up in the same trace, so I can see the full retry history
without losing the connection to the original request.

**Why P3**: Real value, but retries are a secondary path (most messages succeed on first
attempt) — the MVP trace (P1) already covers the common case.

**Acceptance Criteria**:

1. WHEN a message is republished onto a `-5s`/`-1m`/`-10m` retry topic THEN the `traceparent`
   header SHALL be carried forward unchanged (alongside the existing `x-original-topic`/
   `x-retry-count`/etc. headers already forwarded today).
2. WHEN `RetryTopicConsumer` processes a retried message THEN it SHALL extract that same
   `traceparent` and continue the same trace (new child `Activity`, not a new root), so all
   retry attempts for one message appear as sibling spans in one trace.

**Independent Test**: Force a dispatch failure (e.g. point at an invalid SES config), confirm the
message lands on the `-5s` retry topic, and that the retry's span appears in the same trace as
the original attempt.

---

## Edge Cases

- WHEN the OTel collector/exporter endpoint isn't configured (`OTEL_EXPORTER_OTLP_ENDPOINT`
  unset, current default in both repos) THEN Activities SHALL still be created (no-op export is
  fine — this only affects whether spans leave the process, not whether headers/log enrichment
  work).
- WHEN `OutboxEntry`s already exist in DynamoDB without a captured trace context (created before
  this feature ships) THEN `OutboxPublisher` SHALL treat a missing/null captured context as "no
  parent link" and still produce a valid (rootless-link) publish span, not throw.
- WHEN a Kafka message's `traceparent` header is malformed (not valid W3C format) THEN the
  consumer SHALL fall back to starting a fresh root trace, logging a warning, rather than
  crashing message processing.
- WHEN the same `OutboxEntry` is retried by `OutboxPublisher` itself (its own existing
  `RetryCount`/`MaxRetryCount` mechanism, unrelated to comms-api's retry topics) THEN each
  publish attempt SHALL still link back to the same originally-captured trace context.

---

## Requirement Traceability

| Requirement ID | Story                              | Phase   | Status  |
| --------------- | ----------------------------------- | ------- | ------- |
| TRACE-01        | P1: Capture trace context on entry  | Design  | Pending |
| TRACE-02        | P1: Linked publish span + header    | Design  | Pending |
| TRACE-03        | P1: Consumer child span             | Design  | Pending |
| TRACE-04        | P1: Log enrichment (both repos)     | Design  | Pending |
| TRACE-05        | P1: Missing-header fallback         | Design  | Pending |
| TRACE-06        | P2: Rename field, identity-api side | Design  | Pending |
| TRACE-07        | P2: Rename field, comms-api side    | Design  | Pending |
| TRACE-08        | P2: Update contract doc             | Design  | Pending |
| TRACE-09        | P3: Forward header on retry republish | Design | Pending |
| TRACE-10        | P3: Retry consumer continues trace  | Design  | Pending |

**Coverage:** 10 total, 0 mapped to tasks yet, 10 unmapped ⚠️ (expected — Design/Tasks phases follow)

---

## Success Criteria

- [ ] A live local run (both services + OTel collector) shows one connected trace for a full
      register → email-verification-notification flow.
- [ ] `grep <traceId>` across both services' log output returns every log line for that one
      request.
- [ ] No `CorrelationId` field remains in either repo's Kafka message contracts; `IdempotencyKey`
      fully replaces it, dedup behavior unchanged.
- [ ] `dotnet build`/`dotnet test` clean in both repos after the change.
