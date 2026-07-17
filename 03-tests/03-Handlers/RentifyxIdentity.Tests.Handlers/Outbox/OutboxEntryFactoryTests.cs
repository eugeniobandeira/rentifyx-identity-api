using System.Text.Json;
using FluentAssertions;
using RentifyxIdentity.Application.Outbox;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Domain.Events;
using Xunit;

namespace RentifyxIdentity.Tests.Handlers.Outbox;

public sealed class OutboxEntryFactoryTests
{
    private readonly OutboxEntryFactory _sut = new();

    [Fact]
    public void CreateEntries_UserRegistered_MapsToNotificationRequestedTopic_WithEmailVerificationTemplate()
    {
        UserRegistered domainEvent = new(Guid.NewGuid(), "user@example.com", UserRole.Renter, "raw-token", DateTimeOffset.UtcNow);

        IReadOnlyList<OutboxEntry> entries = _sut.CreateEntries([domainEvent]);

        entries.Should().ContainSingle();
        OutboxEntry entry = entries[0];
        entry.TargetTopic.Should().Be("notification-requested");

        using JsonDocument message = JsonDocument.Parse(entry.MessageJson);
        message.RootElement.GetProperty("CorrelationId").GetGuid().Should().Be(entry.Id);
        message.RootElement.GetProperty("RecipientId").GetGuid().Should().Be(domainEvent.UserId);
        message.RootElement.GetProperty("RecipientEmail").GetString().Should().Be(domainEvent.Email);
        message.RootElement.GetProperty("Channel").GetString().Should().Be("Email");
        message.RootElement.GetProperty("TemplateId").GetString().Should().Be("email-verification");
        message.RootElement.GetProperty("Payload").GetProperty("token").GetString().Should().Be("raw-token");
    }

    [Fact]
    public void CreateEntries_PasswordResetRequested_MapsToNotificationRequestedTopic_WithPasswordResetTemplate()
    {
        PasswordResetRequested domainEvent = new(Guid.NewGuid(), "user@example.com", "reset-token", DateTimeOffset.UtcNow);

        IReadOnlyList<OutboxEntry> entries = _sut.CreateEntries([domainEvent]);

        entries.Should().ContainSingle();
        OutboxEntry entry = entries[0];
        entry.TargetTopic.Should().Be("notification-requested");

        using JsonDocument message = JsonDocument.Parse(entry.MessageJson);
        message.RootElement.GetProperty("CorrelationId").GetGuid().Should().Be(entry.Id);
        message.RootElement.GetProperty("RecipientId").GetGuid().Should().Be(domainEvent.UserId);
        message.RootElement.GetProperty("RecipientEmail").GetString().Should().Be(domainEvent.Email);
        message.RootElement.GetProperty("TemplateId").GetString().Should().Be("password-reset");
        message.RootElement.GetProperty("Payload").GetProperty("token").GetString().Should().Be("reset-token");
    }

    [Theory]
    [MemberData(nameof(LifecycleEvents))]
    public void CreateEntries_LifecycleEvent_MapsToUserLifecycleEventsTopic_WithGenericEnvelope(
        IDomainEvent domainEvent,
        Guid expectedAggregateId,
        string expectedEventType)
    {
        IReadOnlyList<OutboxEntry> entries = _sut.CreateEntries([domainEvent]);

        entries.Should().ContainSingle();
        OutboxEntry entry = entries[0];
        entry.TargetTopic.Should().Be("user-lifecycle-events");

        using JsonDocument message = JsonDocument.Parse(entry.MessageJson);
        message.RootElement.GetProperty("EventType").GetString().Should().Be(expectedEventType);
        message.RootElement.GetProperty("AggregateId").GetGuid().Should().Be(expectedAggregateId);
        message.RootElement.TryGetProperty("Data", out _).Should().BeTrue();
    }

    [Fact]
    public void CreateEntries_MultipleEvents_ReturnsOneEntryPerEvent()
    {
        Guid userId = Guid.NewGuid();
        IReadOnlyList<IDomainEvent> events =
        [
            new UserEmailVerified(userId, "user@example.com", DateTimeOffset.UtcNow),
            new UserPasswordChanged(userId, DateTimeOffset.UtcNow)
        ];

        IReadOnlyList<OutboxEntry> entries = _sut.CreateEntries(events);

        entries.Should().HaveCount(2);
        entries.Select(e => e.Id).Should().OnlyHaveUniqueItems();
    }

    public static TheoryData<IDomainEvent, Guid, string> LifecycleEvents()
    {
        Guid userId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        return new TheoryData<IDomainEvent, Guid, string>
        {
            { new UserEmailVerified(userId, "user@example.com", now), userId, nameof(UserEmailVerified) },
            { new UserPasswordChanged(userId, now), userId, nameof(UserPasswordChanged) },
            { new UserSuspended(userId, "reason", now), userId, nameof(UserSuspended) },
            { new UserAccountDeleted(userId, now), userId, nameof(UserAccountDeleted) },
            { new UserLoggedIn(userId, "user@example.com", now), userId, nameof(UserLoggedIn) }
        };
    }
}
