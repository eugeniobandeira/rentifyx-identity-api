namespace RentifyxIdentity.Application.Outbox;

/// <summary>
/// Generic envelope for the "user-lifecycle-events" topic - no consumer exists yet, so this is
/// deliberately simple rather than a rigid per-event contract (see design.md).
/// </summary>
internal sealed record UserLifecycleEventEnvelope(
    string EventType,
    Guid AggregateId,
    DateTimeOffset OccurredAt,
    object Data);
