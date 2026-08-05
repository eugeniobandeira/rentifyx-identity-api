# Distributed Tracing Across the Kafka Boundary Design

**Spec**: `.specs/features/distributed-tracing-kafka/spec.md`
**Status**: Draft
**Repos touched**: `rentifyx-identity-api` (producer), `rentifyx-communications-api` (consumer)

---

## Research Notes

Followed the Knowledge Verification Chain before designing:

- **`OpenTelemetry.Instrumentation.ConfluentKafka`** (Context7: `open-telemetry/opentelemetry-dotnet-contrib`) exists (prerelease) and auto-wraps `ProducerBuilder`/`ConsumerBuilder` for header injection/extraction. **Not used here** — its docs show no supported way to attach an `ActivityLink` back to a trace context captured earlier (the Outbox's deferred-publish scenario), only whatever `Activity.Current` is live at the moment `Produce()` is called. Since `OutboxPublisher` runs in a background `PeriodicTimer` loop with no live request `Activity`, this package alone can't solve TRACE-01/02. Manual instrumentation gives full control and is a documented, standard pattern.
- **Manual `ActivitySource`/`ActivityContext`/`ActivityLink` API** (Context7: `open-telemetry/opentelemetry-dotnet`, `OpenTelemetry.Api/README.md`) confirmed:
  - `activitySource.StartActivity(name, ActivityKind.Server, "00-traceid-spanid-01")` — start with a raw W3C traceparent string as literal parent.
  - `new ActivityContext(ActivityTraceId.CreateFromString(...), ActivitySpanId.CreateFromString(...), ActivityTraceFlags...)` — construct a context from parts.
  - `new ActivityLink(context)` — build a link for the "not-the-current-parent" case (async messaging).
  - `ActivityContext.TryParse(traceParent, traceState, isRemote, out context)` is the standard BCL parse method for a raw header string — used here rather than parsing trace-id/span-id substrings by hand. (High confidence; standard since .NET Core 3.0 — verify exact overload at implementation time, per the "flag uncertainty" rule.)
- **Header-based context propagation pattern** confirmed by OpenTelemetry .NET's own `MicroserviceExample` (RabbitMQ): "Distributed context propagation is implemented by injecting and extracting trace context within the message headers using OpenTelemetry APIs" — same pattern applies to Kafka `Message.Headers`, just a different transport.
- `Activity.DefaultIdFormat` is `W3C` by default since .NET Core 3.0, and ASP.NET Core's own instrumentation already produces W3C-format Activities — no format conversion needed on the producer side.

---

## Architecture Overview

```mermaid
sequenceDiagram
    participant HTTP as HTTP request (ASP.NET Core Activity)
    participant Outbox as OutboxEntryFactory
    participant DDB as DynamoDB (OutboxEntry)
    participant Poller as OutboxPublisher (background)
    participant Kafka as Kafka topic
    participant Consumer as NotificationRequestedConsumer

    HTTP->>Outbox: CreateEntries(domainEvents)
    Note over Outbox: Activity.Current is the HTTP request's Activity here
    Outbox->>DDB: persist OutboxEntry.TraceParent/TraceState (captured, not acted on yet)
    Note over Poller: Runs later, own PeriodicTimer loop - no live request Activity
    Poller->>Poller: StartActivity("publish", Producer, links: [stored TraceParent])
    Poller->>Kafka: ProduceAsync(headers: traceparent/tracestate = NEW activity's context)
    Consumer->>Consumer: extract traceparent header, StartActivity(..., Consumer, parentContext)
    Note over Consumer: Consumer span is a real child of the publish span;<br/>publish span is Link-ed (not parented) back to the HTTP request
```

**Why Link instead of Parent for the publish span**: the HTTP request has already completed by the
time `OutboxPublisher` actually produces to Kafka (deferred, could be seconds to minutes later
depending on poll interval and retries). Making the publish span a *child* of the request span
would make the request's reported duration include the wait time, which is wrong. An
`ActivityLink` records the causal relationship ("this publish happened because of that request")
without corrupting either span's duration — this is what the OTel messaging semantic conventions
recommend for async/queue-based systems.

---

## Code Reuse Analysis

### Existing Components to Leverage

| Component                                    | Location (identity-api)                                                    | How to Use |
| ---------------------------------------------| -----------------------------------------------------------------------------| ---------- |
| `OutboxEntry.PartitionKey` pattern            | `02-src/03-Domain/.../Entities/OutboxEntry.cs`                              | Same pattern as the just-shipped D-024 (nullable string field, set at `Create()`, persisted, read back via `Reconstitute()`) — `TraceParent`/`TraceState` follow it exactly. |
| `OutboxDynamoDbItem`/`OutboxItemMapper`       | `02-src/05-Infrastructure/.../Models/`, `.../Mapping/`                      | Same as `PartitionKey` — add two more nullable string columns, no schema migration (no infra provisioned). |
| `ServiceDefaults/Extensions.cs`'s `WithTracing` | `01-aspire/02-ServiceDefaults/.../Extensions.cs`                          | Add `.AddSource("RentifyxIdentity.Outbox")` (identity-api) / `.AddSource("RentifyxCommunications.Messaging")` (comms-api) alongside the existing `AddAspNetCoreInstrumentation()` call. |
| `CorrelationIdMiddleware`'s `LogContext.PushProperty` pattern | `02-src/01-Api/.../Middlewares/CorrelationIdMiddleware.cs` (both repos) | Reuse the exact same Serilog enrichment mechanism for `TraceId`/`SpanId` around the publish/consume blocks — no new logging package. |
| `KafkaTopics` constants                       | `02-src/03-Domain/.../Constants/KafkaTopics.cs`                            | Header name constants (`"traceparent"`, `"tracestate"`) added alongside, not a new file. |

### Integration Points

| System                                  | Integration Method |
| -----------------------------------------| ------------------- |
| `System.Diagnostics.Activity`/`ActivitySource` | BCL, already available — no new package. |
| `OpenTelemetry.Context.Propagation.TraceContextPropagator` | Transitively available via the `OpenTelemetry`/`OpenTelemetry.Extensions.Hosting` packages both repos already reference — used to `Inject`/`Extract` W3C context into/from Kafka `Headers` rather than hand-formatting the traceparent string. |
| Kafka `Message<TKey,TValue>.Headers`    | New `Headers` populated on every `ProduceAsync` call in `OutboxPublisher`; read in `NotificationRequestedConsumer`/`RetryTopicConsumer`. |

---

## Components

### `OutboxActivitySource` (new, identity-api)

- **Purpose**: Single shared `ActivitySource` instance for all Outbox-publish spans, so it can be registered once with the tracer provider.
- **Location**: `02-src/01-Api/RentifyxIdentity.Api/Messaging/OutboxActivitySource.cs`
- **Interfaces**: `internal static class OutboxActivitySource { internal const string Name = "RentifyxIdentity.Outbox"; internal static readonly ActivitySource Instance = new(Name); }`
- **Dependencies**: None.
- **Reuses**: Mirrors how `KafkaTopics`/`DynamoDbConstants` centralize a single well-known string.

### `OutboxEntry` (extended)

- **Purpose**: Carry the captured `traceparent`/`tracestate` from creation time through to publish time.
- **Location**: `02-src/03-Domain/RentifyxIdentity.Domain/Entities/OutboxEntry.cs`
- **New properties**: `string? TraceParent { get; private set; }`, `string? TraceState { get; private set; }`
- **Interfaces**: `Create(..., string? traceParent = null, string? traceState = null)`, `Reconstitute(..., string? traceParent, string? traceState)` — same shape as the existing `partitionKey` param added for D-024.
- **Dependencies**: None (still framework-free per Domain layer convention).
- **Reuses**: Exact same optional-param/persist/reconstitute shape as `PartitionKey`.

### `OutboxEntryFactory` (extended)

- **Purpose**: Capture `Activity.Current`'s W3C id/tracestate at the moment a domain event becomes an `OutboxEntry` — this is the only point where the original HTTP request's `Activity` is still current.
- **Location**: `02-src/02-Application/RentifyxIdentity.Application/Outbox/OutboxEntryFactory.cs`
- **Change**: Every `OutboxEntry.Create(...)` call also passes `traceParent: Activity.Current?.Id, traceState: Activity.Current?.TraceStateString`.
- **Reuses**: Same call sites already touched for `PartitionKey` (D-024) — one more constructor arg per branch.

### `OutboxPublisher` (extended)

- **Purpose**: Start a `Producer`-kind Activity for each actual Kafka publish, linked to the entry's captured trace context, and inject that new Activity's own context into the Kafka message headers.
- **Location**: `02-src/01-Api/RentifyxIdentity.Api/Messaging/OutboxPublisher.cs`
- **Change** (`PublishEntryAsync`):
  ```csharp
  ActivityLink[] links = [];
  if (entry.TraceParent is not null &&
      ActivityContext.TryParse(entry.TraceParent, entry.TraceState, isRemote: true, out ActivityContext parentContext))
      links = [new ActivityLink(parentContext)];

  using Activity? activity = OutboxActivitySource.Instance.StartActivity(
      $"{entry.TargetTopic} publish",
      ActivityKind.Producer,
      default(ActivityContext),
      links: links);

  using (LogContext.PushProperty("TraceId", activity?.TraceId.ToString()))
  using (LogContext.PushProperty("SpanId", activity?.SpanId.ToString()))
  {
      Headers headers = [];
      if (activity is not null)
      {
          TraceContextPropagator propagator = new();
          propagator.Inject(
              new PropagationContext(activity.Context, Baggage.Current),
              headers,
              static (h, key, value) => h.Add(key, System.Text.Encoding.UTF8.GetBytes(value)));
      }

      await _producer!.ProduceAsync(
          entry.TargetTopic,
          new Message<string, string> { Key = entry.PartitionKey ?? string.Empty, Value = entry.MessageJson, Headers = headers },
          token);

      await repository.MarkPublishedAsync(entry.Id, token);
  }
  ```
- **Dependencies**: `System.Diagnostics`, `OpenTelemetry.Context.Propagation`, `Serilog.Context` (already used by `CorrelationIdMiddleware`).
- **Reuses**: `entry.PartitionKey ?? string.Empty` line from D-024, unchanged.

### `NotificationRequestedConsumer` / `RetryTopicConsumer` (extended, comms-api)

- **Purpose**: Extract `traceparent`/`tracestate` from the consumed message's headers and start a `Consumer`-kind Activity as a real child of the publish span, wrapping the existing dispatch/processing call.
- **Location**: `02-src/01-Api/RentifyxCommunications.Api/Messaging/NotificationRequestedConsumer.cs`, `.../RetryTopicConsumer.cs`
- **Change**: Before calling `NotificationDispatchProcessor.ProcessAsync`, parse headers (`TraceContextPropagator.Extract`), `StartActivity(name, ActivityKind.Consumer, parentContext)`, push `TraceId`/`SpanId` into `LogContext` for the duration of processing (same pattern as identity-api's producer side).
- **Reuses**: The existing header-reading helper already in `RetryTopicConsumer.cs` for `x-original-topic`/`x-retry-count` — extend it for `traceparent`/`tracestate` rather than writing a second helper.

### Retry-topic republisher (comms-api, exact file TBD in Tasks)

- **Purpose**: Whichever component republishes a failed message onto the `-5s`/`-1m`/`-10m` retry topics must copy the `traceparent`/`tracestate` headers forward unchanged, exactly like it already does for `x-original-topic`/`x-retry-count`/etc.
- **Location**: Not yet identified — Tasks phase includes a locate-and-confirm step before editing (per Knowledge Verification Chain, Step 1: check the codebase before assuming a design).

---

## Data Models

### `OutboxEntry` (extended)

```
OutboxEntry
├── Id: Guid
├── TargetTopic: string
├── MessageJson: string
├── Status: OutboxStatus
├── CreatedAt: DateTimeOffset
├── RetryCount: int
├── PartitionKey: string?      (existing, D-024)
├── TraceParent: string?       (NEW - W3C traceparent captured at creation time)
└── TraceState: string?        (NEW - W3C tracestate captured at creation time, usually null)
```

**Relationships**: Persisted 1:1 into `OutboxDynamoDbItem` (two new plain string columns, no GSI
involvement — same as `PartitionKey`).

### `NotificationRequestedMessage` → renamed field (identity-api)

```
NotificationRequestedMessage
├── IdempotencyKey: Guid   (renamed from CorrelationId - same value: the OutboxEntry's own Id)
├── RecipientId: Guid
├── RecipientEmail: string
├── Channel: string
├── TemplateId: string
└── Payload: IReadOnlyDictionary<string, string>
```

### `DispatchNotificationRequest` → renamed field (comms-api)

```
DispatchNotificationRequest
├── IdempotencyKey: Guid   (renamed from CorrelationId - same value, same dedup semantics)
├── RecipientId: Guid
├── RecipientEmail: string
├── Channel: string
├── TemplateId: string
└── Payload: IReadOnlyDictionary<string, string>
```

**Relationships**: The DynamoDB dedup key construction (`PK=NOTIF#{correlationId}` in comms-api)
keeps the same *value* — only the C# property name and JSON field name change. Trace correlation
now lives entirely in Kafka headers (`traceparent`/`tracestate`), not in this payload field.

---

## Error Handling Strategy

| Error Scenario                                                  | Handling                                                                 | Impact |
| ------------------------------------------------------------------| --------------------------------------------------------------------------| ------ |
| `OutboxEntry.TraceParent` is null (pre-existing entry, or created outside an HTTP request) | `ActivityContext.TryParse` guard skips the link; publish `Activity` still starts with no links — a valid root span. | No crash; that publish just isn't connected to an upstream trace. |
| Consumed message has no `traceparent` header                    | `TraceContextPropagator.Extract` returns a `default` context when nothing is found; `StartActivity` with a `default(ActivityContext)` parent starts a fresh root trace. | No crash; per spec edge case, this is intended fallback behavior (TRACE-05). |
| Malformed `traceparent` header value                            | `ActivityContext.TryParse` returns `false` on malformed input; treated identically to "missing" (root trace + one warning log). | Matches spec edge case exactly. |
| `OTEL_EXPORTER_OTLP_ENDPOINT` unset (current default, both repos) | `ActivitySource.StartActivity` still returns a live `Activity` even with no listener/exporter configured (returns `null` only if nothing is subscribed to that source name at all — mitigated by registering `.AddSource(...)` unconditionally, not inside the `if (otlpEndpoint is not null)` branch). | Headers/log enrichment still work locally even without a configured collector. |

---

## Tech Decisions (only non-obvious ones)

| Decision                                                              | Choice                                             | Rationale |
| ------------------------------------------------------------------------| ----------------------------------------------------| ----------- |
| Manual instrumentation vs. `OpenTelemetry.Instrumentation.ConfluentKafka` | Manual, via `ActivitySource`/`TraceContextPropagator` | The package (prerelease) can't documented-ly attach an explicit `ActivityLink` to a trace context captured earlier than `Activity.Current` — required for the Outbox's deferred, background-polled publish. Revisit if a future package version documents that. |
| Publish span relationship to original HTTP request                     | `ActivityLink`, not `Activity` parent                | OTel semantic conventions for async/queue messaging: parenting would inflate the (already-completed) HTTP request span's reported duration by the queue wait time. |
| Consumer span relationship to publish span                             | Real parent (`ActivityKind.Consumer` with `parentContext`) | Consume happens synchronously as a direct reaction to that specific publish — a true parent-child relationship, unlike the deferred producer→original-request relationship above. |
| `CorrelationId` rename target name                                     | `IdempotencyKey`                                      | Matches what the field is actually used for on both sides (Outbox entry ID on read, DynamoDB dedup key on write) — user explicitly requested an accurate name once `traceparent` became the real correlation mechanism. |
| Header carrier for W3C context                                         | `TraceContextPropagator.Inject`/`Extract` against Kafka `Message.Headers`, not hand-built strings | Avoids re-implementing W3C traceparent string formatting/parsing (baggage, tracestate edge cases) — same propagator .NET's own OTel SDK uses for HTTP. |

---

## Open Questions for Tasks Phase

1. Exact file/class in comms-api that republishes to the `-5s`/`-1m`/`-10m` retry topics — needs
   a locate-and-confirm step before P3 tasks can be written precisely.
2. Whether `docs/contracts/notification-requested.md` (comms-api) needs a matching `docs/` update
   in identity-api too — TBD by checking if identity-api has an equivalent contract doc.
