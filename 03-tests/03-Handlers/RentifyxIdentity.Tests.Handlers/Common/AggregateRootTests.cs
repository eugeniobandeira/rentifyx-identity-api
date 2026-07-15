using FluentAssertions;
using RentifyxIdentity.Domain.Common;
using RentifyxIdentity.Domain.Events;
using Xunit;

namespace RentifyxIdentity.Tests.Handlers.Common;

public sealed class AggregateRootTests
{
    private sealed record TestEvent(DateTimeOffset OccurredAt) : IDomainEvent;

    private sealed class TestAggregate : AggregateRoot
    {
        public void DoSomething(IDomainEvent domainEvent) => RaiseDomainEvent(domainEvent);
    }

    [Fact]
    public void DomainEvents_NewAggregate_IsEmpty()
    {
        TestAggregate aggregate = new();

        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RaiseDomainEvent_AddsEventToDomainEvents()
    {
        TestAggregate aggregate = new();
        TestEvent domainEvent = new(DateTimeOffset.UtcNow);

        aggregate.DoSomething(domainEvent);

        aggregate.DomainEvents.Should().ContainSingle().Which.Should().Be(domainEvent);
    }

    [Fact]
    public void RaiseDomainEvent_CalledMultipleTimes_AccumulatesAllEvents()
    {
        TestAggregate aggregate = new();

        aggregate.DoSomething(new TestEvent(DateTimeOffset.UtcNow));
        aggregate.DoSomething(new TestEvent(DateTimeOffset.UtcNow));

        aggregate.DomainEvents.Should().HaveCount(2);
    }

    [Fact]
    public void ClearDomainEvents_RemovesAllAccumulatedEvents()
    {
        TestAggregate aggregate = new();
        aggregate.DoSomething(new TestEvent(DateTimeOffset.UtcNow));

        aggregate.ClearDomainEvents();

        aggregate.DomainEvents.Should().BeEmpty();
    }
}
