using FluentAssertions;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Enums;
using Xunit;

namespace RentifyxIdentity.Tests.Handlers.Common;

public sealed class OutboxEntryTests
{
    private const string TargetTopic = "notification-requested";
    private const string MessageJson = "{\"foo\":\"bar\"}";

    [Fact]
    public void Create_ValidParameters_ReturnsEntry_WithPendingStatus()
    {
        OutboxEntry entry = OutboxEntry.Create(TargetTopic, MessageJson);

        entry.Status.Should().Be(OutboxStatus.Pending);
        entry.TargetTopic.Should().Be(TargetTopic);
        entry.MessageJson.Should().Be(MessageJson);
        entry.RetryCount.Should().Be(0);
    }

    [Fact]
    public void Create_ValidParameters_SetsCreatedAt_ToUtcNow()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow;
        OutboxEntry entry = OutboxEntry.Create(TargetTopic, MessageJson);
        DateTimeOffset after = DateTimeOffset.UtcNow;

        entry.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void MarkPublished_SetsStatusToPublished()
    {
        OutboxEntry entry = OutboxEntry.Create(TargetTopic, MessageJson);

        entry.MarkPublished();

        entry.Status.Should().Be(OutboxStatus.Published);
    }

    [Fact]
    public void MarkFailed_SetsStatusToFailed()
    {
        OutboxEntry entry = OutboxEntry.Create(TargetTopic, MessageJson);

        entry.MarkFailed();

        entry.Status.Should().Be(OutboxStatus.Failed);
    }

    [Fact]
    public void IncrementRetryCount_IncrementsByOne()
    {
        OutboxEntry entry = OutboxEntry.Create(TargetTopic, MessageJson);

        entry.IncrementRetryCount();
        entry.IncrementRetryCount();

        entry.RetryCount.Should().Be(2);
    }
}
