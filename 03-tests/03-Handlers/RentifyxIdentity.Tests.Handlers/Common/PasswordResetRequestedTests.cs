using FluentAssertions;
using RentifyxIdentity.Domain.Events;
using Xunit;

namespace RentifyxIdentity.Tests.Handlers.Common;

public sealed class PasswordResetRequestedTests
{
    [Fact]
    public void Construct_SetsAllProperties()
    {
        Guid userId = Guid.NewGuid();
        const string email = "user@example.com";
        const string rawToken = "raw-token-value";
        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;

        PasswordResetRequested domainEvent = new(userId, email, rawToken, occurredAt);

        domainEvent.UserId.Should().Be(userId);
        domainEvent.Email.Should().Be(email);
        domainEvent.RawToken.Should().Be(rawToken);
        domainEvent.OccurredAt.Should().Be(occurredAt);
    }

    [Fact]
    public void Construct_IsAssignableToIDomainEvent()
    {
        PasswordResetRequested domainEvent = new(Guid.NewGuid(), "user@example.com", "token", DateTimeOffset.UtcNow);

        domainEvent.Should().BeAssignableTo<IDomainEvent>();
    }
}
