using System.Text.Json;
using RentifyxIdentity.Domain.Constants;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Events;

namespace RentifyxIdentity.Application.Outbox;

public sealed class OutboxEntryFactory : IOutboxEntryFactory
{
    public IReadOnlyList<OutboxEntry> CreateEntries(IReadOnlyCollection<IDomainEvent> domainEvents) =>
        domainEvents.Select(CreateEntry).ToList();

    private static OutboxEntry CreateEntry(IDomainEvent domainEvent)
    {
        Guid entryId = Guid.NewGuid();

        return domainEvent switch
        {
            UserRegistered e => OutboxEntry.Create(
                entryId,
                KafkaTopics.NotificationRequested,
                SerializeNotificationRequested(entryId, e.UserId, e.Email, "email-verification", e.RawToken),
                e.UserId.ToString()),

            PasswordResetRequested e => OutboxEntry.Create(
                entryId,
                KafkaTopics.NotificationRequested,
                SerializeNotificationRequested(entryId, e.UserId, e.Email, "password-reset", e.RawToken),
                e.UserId.ToString()),

            _ => OutboxEntry.Create(
                entryId,
                KafkaTopics.UserLifecycleEvents,
                SerializeLifecycleEnvelope(domainEvent),
                LifecycleAggregateId(domainEvent).ToString())
        };
    }

    private static string SerializeNotificationRequested(
        Guid correlationId,
        Guid recipientId,
        string recipientEmail,
        string templateId,
        string rawToken)
    {
        NotificationRequestedMessage message = new(
            CorrelationId: correlationId,
            RecipientId: recipientId,
            RecipientEmail: recipientEmail,
            Channel: "Email",
            TemplateId: templateId,
            Payload: new Dictionary<string, string> { ["token"] = rawToken });

        return JsonSerializer.Serialize(message);
    }

    private static Guid LifecycleAggregateId(IDomainEvent domainEvent) => domainEvent switch
    {
        UserEmailVerified e => e.UserId,
        UserPasswordChanged e => e.UserId,
        UserSuspended e => e.UserId,
        UserAccountDeleted e => e.UserId,
        UserLoggedIn e => e.UserId,
        _ => throw new NotSupportedException(
            $"No lifecycle envelope mapping defined for domain event type '{domainEvent.GetType().Name}'.")
    };

    private static string SerializeLifecycleEnvelope(IDomainEvent domainEvent)
    {
        UserLifecycleEventEnvelope envelope = new(
            EventType: domainEvent.GetType().Name,
            AggregateId: LifecycleAggregateId(domainEvent),
            OccurredAt: domainEvent.OccurredAt,
            Data: domainEvent);

        return JsonSerializer.Serialize(envelope);
    }
}
