using FluentAssertions;
using RentifyxIdentity.Domain.ValueObjects;
using Xunit;

namespace RentifyxIdentity.Tests.Handlers.Features.Identity.ValueObjects;

public sealed class PasswordTests
{
    [Fact]
    public void FromPlaintext_ValidPassword_ProducesHash()
    {
        Password password = Password.FromPlaintext("P@ssword123!");

        password.HashValue.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void FromPlaintext_EmptyPassword_ThrowsArgumentException()
    {
        Action act = () => Password.FromPlaintext("");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromPlaintext_NullPassword_ThrowsArgumentException()
    {
        Action act = () => Password.FromPlaintext(null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ToString_ReturnsRedacted()
    {
        Password password = Password.FromPlaintext("P@ssword123!");

        password.ToString().Should().Be("[REDACTED]");
    }

    [Fact]
    public void Verify_CorrectPlaintext_ReturnsTrue()
    {
        Password password = Password.FromPlaintext("P@ssword123!");

        password.Verify("P@ssword123!").Should().BeTrue();
    }

    [Fact]
    public void Verify_WrongPlaintext_ReturnsFalse()
    {
        Password password = Password.FromPlaintext("P@ssword123!");

        password.Verify("WrongPassword!").Should().BeFalse();
    }
}
