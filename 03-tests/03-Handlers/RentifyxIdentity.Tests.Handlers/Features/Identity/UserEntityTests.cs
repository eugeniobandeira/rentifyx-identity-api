using FluentAssertions;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Domain.ValueObjects;
using RentifyxIdentity.Tests.Common.Constants;
using Xunit;

namespace RentifyxIdentity.Tests.Handlers.Features.Identity;

public sealed class UserEntityTests
{
    private static Email ValidEmail => Email.Create(TestConstants.ValidEmail);
    private static TaxDocument ValidTaxId => TaxDocument.Create(TestConstants.TaxIdCpfFormatted);
    private static Password ValidPassword => Password.FromPlaintext(TestConstants.ValidPassword);
    private const UserRole ValidRole = UserRole.Owner;

    [Fact]
    public void Create_ValidParameters_ReturnsEntity_WithPendingVerificationStatus()
    {
        UserEntity entity = UserEntity.Create(ValidEmail, ValidTaxId, ValidPassword, ValidRole);

        entity.Status.Should().Be(UserStatus.PendingVerification);
    }

    [Fact]
    public void Create_ValidParameters_SetsCreatedAt_ToUtcNow()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow;
        UserEntity entity = UserEntity.Create(ValidEmail, ValidTaxId, ValidPassword, ValidRole);
        DateTimeOffset after = DateTimeOffset.UtcNow;

        entity.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void SetEmailVerificationToken_SetsTokenHashAndExpiry()
    {
        UserEntity entity = UserEntity.Create(ValidEmail, ValidTaxId, ValidPassword, ValidRole);
        DateTimeOffset expiry = DateTimeOffset.UtcNow.AddHours(24);

        entity.SetEmailVerificationToken("some-hash", expiry);

        entity.EmailVerificationTokenHash.Should().Be("some-hash");
        entity.EmailVerificationTokenExpiry.Should().Be(expiry);
    }

    [Fact]
    public void VerifyEmail_SetsStatusToActive_AndClearsTokenFields()
    {
        UserEntity entity = UserEntity.Create(ValidEmail, ValidTaxId, ValidPassword, ValidRole);
        entity.SetEmailVerificationToken("some-hash", DateTimeOffset.UtcNow.AddHours(24));

        entity.VerifyEmail();

        entity.Status.Should().Be(UserStatus.Active);
        entity.EmailVerificationTokenHash.Should().BeNull();
        entity.EmailVerificationTokenExpiry.Should().BeNull();
    }

    [Fact]
    public void SetPasswordResetToken_SetsResetHashAndExpiry()
    {
        UserEntity entity = UserEntity.Create(ValidEmail, ValidTaxId, ValidPassword, ValidRole);
        DateTimeOffset expiry = DateTimeOffset.UtcNow.AddHours(1);

        entity.SetPasswordResetToken("reset-hash", expiry);

        entity.PasswordResetTokenHash.Should().Be("reset-hash");
        entity.PasswordResetTokenExpiry.Should().Be(expiry);
    }

    [Fact]
    public void ResetPassword_UpdatesPasswordHash_AndClearsResetTokenFields()
    {
        UserEntity entity = UserEntity.Create(ValidEmail, ValidTaxId, ValidPassword, ValidRole);
        entity.SetPasswordResetToken("reset-hash", DateTimeOffset.UtcNow.AddHours(1));
        Password newPassword = Password.FromPlaintext("N3wP@ssword!");

        entity.ResetPassword(newPassword);

        entity.PasswordHash.Should().BeSameAs(newPassword);
        entity.PasswordResetTokenHash.Should().BeNull();
        entity.PasswordResetTokenExpiry.Should().BeNull();
    }

    [Fact]
    public void Suspend_SetsStatusToSuspended()
    {
        UserEntity entity = UserEntity.Create(ValidEmail, ValidTaxId, ValidPassword, ValidRole);

        entity.Suspend();

        entity.Status.Should().Be(UserStatus.Suspended);
    }

    [Fact]
    public void Anonymize_SetsStatusToDeleted_AndAnonymizesFields()
    {
        UserEntity entity = UserEntity.Create(ValidEmail, ValidTaxId, ValidPassword, ValidRole);

        entity.Anonymize();

        entity.Status.Should().Be(UserStatus.Deleted);
        entity.Email.Value.Should().Contain("anonymized.local");
        entity.TaxId.RawValue.Should().Be("ANONYMIZED");
    }

    [Fact]
    public void Create_ValidParameters_AssignsUniqueIds()
    {
        UserEntity entity1 = UserEntity.Create(ValidEmail, ValidTaxId, ValidPassword, ValidRole);
        UserEntity entity2 = UserEntity.Create(ValidEmail, ValidTaxId, ValidPassword, ValidRole);

        entity1.Id.Should().NotBe(entity2.Id);
    }
}
