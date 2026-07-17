using RentifyxIdentity.Domain.Enums;

namespace RentifyxIdentity.Domain.Entities;

public sealed class OutboxEntry
{
    public Guid Id { get; private set; }
    public string TargetTopic { get; private set; } = string.Empty;
    public string MessageJson { get; private set; } = string.Empty;
    public OutboxStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public int RetryCount { get; private set; }

    private OutboxEntry() { }

    public static OutboxEntry Create(string targetTopic, string messageJson) =>
        Create(Guid.NewGuid(), targetTopic, messageJson);

    /// <summary>
    /// Overload for callers that need the entry's Id known before construction - e.g. OutboxEntryFactory
    /// embeds it as CorrelationId in the serialized message, so it must match this entry's own Id exactly.
    /// </summary>
    public static OutboxEntry Create(Guid id, string targetTopic, string messageJson)
    {
        return new OutboxEntry
        {
            Id = id,
            TargetTopic = targetTopic,
            MessageJson = messageJson,
            Status = OutboxStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            RetryCount = 0
        };
    }

    public void MarkPublished()
    {
        Status = OutboxStatus.Published;
    }

    public void MarkFailed()
    {
        Status = OutboxStatus.Failed;
    }

    public void IncrementRetryCount()
    {
        RetryCount++;
    }

    internal static OutboxEntry Reconstitute(
        Guid id,
        string targetTopic,
        string messageJson,
        OutboxStatus status,
        DateTimeOffset createdAt,
        int retryCount)
    {
        return new OutboxEntry
        {
            Id = id,
            TargetTopic = targetTopic,
            MessageJson = messageJson,
            Status = status,
            CreatedAt = createdAt,
            RetryCount = retryCount
        };
    }
}
