using System.Text.Json;
using Confluent.Kafka;
using RentifyxIdentity.Domain.Constants;
using RentifyxIdentity.Domain.Interfaces.Users;

namespace RentifyxIdentity.Infrastructure.Messaging;

public sealed class UserStatusSnapshotPublisher(IKafkaProducerFactory producerFactory) : IUserStatusSnapshotPublisher
{
    private const string SnapshotEventType = "UserActiveSnapshot";

    public async Task PublishSnapshotAsync(IReadOnlyCollection<Guid> activeUserIds, CancellationToken ct = default)
    {
        using IProducer<string, string> producer = producerFactory.Create();

        foreach (Guid userId in activeUserIds)
        {
            DateTimeOffset occurredAt = DateTimeOffset.UtcNow;

            // Same 4-field shape as Application.Outbox.UserLifecycleEventEnvelope (EventType/AggregateId/
            // OccurredAt/Data) - redeclared here rather than reused because that type is internal to the
            // Application assembly; this is a one-time broadcast, not a per-entity domain event routed
            // through the Outbox, so it never goes through OutboxEntryFactory.
            SnapshotEnvelope envelope = new(
                SnapshotEventType,
                userId,
                occurredAt,
                new UserActiveSnapshotPayload(userId, occurredAt));

            string json = JsonSerializer.Serialize(envelope);

            await producer.ProduceAsync(
                KafkaTopics.UserLifecycleEvents,
                new Message<string, string> { Key = userId.ToString(), Value = json },
                ct);
        }

        producer.Flush(TimeSpan.FromSeconds(5));
    }

    private sealed record SnapshotEnvelope(
        string EventType,
        Guid AggregateId,
        DateTimeOffset OccurredAt,
        object Data);

    private sealed record UserActiveSnapshotPayload(Guid UserId, DateTimeOffset OccurredAt);
}
