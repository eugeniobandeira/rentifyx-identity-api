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

    public static OutboxEntry Create(string targetTopic, string messageJson)
    {
        return new OutboxEntry
        {
            Id = Guid.NewGuid(),
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
}
