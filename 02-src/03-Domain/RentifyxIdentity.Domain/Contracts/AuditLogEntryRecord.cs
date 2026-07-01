namespace RentifyxIdentity.Domain.Contracts;

public sealed record AuditLogEntryRecord(
    string EventType,
    DateTimeOffset OccurredAt
);
