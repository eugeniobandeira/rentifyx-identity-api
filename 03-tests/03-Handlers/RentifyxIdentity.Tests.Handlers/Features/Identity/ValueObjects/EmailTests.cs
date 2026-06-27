using FluentAssertions;
using RentifyxIdentity.Domain.ValueObjects;
using Xunit;

namespace RentifyxIdentity.Tests.Handlers.Features.Identity.ValueObjects;

public sealed class EmailTests
{
    [Fact]
    public void Create_ValidEmail_ReturnsNormalizedLowercase()
    {
        Email email = Email.Create("User@Example.COM");

        email.Value.Should().Be("user@example.com");
    }

    [Fact]
    public void Create_EmptyEmail_ThrowsArgumentException()
    {
        Action act = () => Email.Create("");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_NullEmail_ThrowsArgumentException()
    {
        Action act = () => Email.Create(null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_DisposableDomain_ThrowsArgumentException()
    {
        Action act = () => Email.Create("user@mailinator.com");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        Email email = Email.Create("user@example.com");

        email.ToString().Should().Be("user@example.com");
    }
}
