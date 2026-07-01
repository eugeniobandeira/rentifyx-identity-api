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

    [Fact]
    public void RecordFailedLogin_IncrementsCounter()
    {
        UserEntity entity = UserEntity.Create(ValidEmail, ValidTaxId, ValidPassword, ValidRole);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        entity.RecordFailedLogin(now);

        entity.FailedLoginAttempts.Should().Be(1);
        entity.LockoutUntil.Should().BeNull();
    }

    [Fact]
    public void RecordFailedLogin_OnFifthCall_SetsLockoutUntil()
    {
        UserEntity entity = UserEntity.Create(ValidEmail, ValidTaxId, ValidPassword, ValidRole);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        for (int i = 0; i < 5; i++)
            entity.RecordFailedLogin(now);

        entity.FailedLoginAttempts.Should().Be(5);
        entity.LockoutUntil.Should().BeCloseTo(now.AddMinutes(15), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void RecordFailedLogin_BeyondFive_DoesNotExtendLockout()
    {
        UserEntity entity = UserEntity.Create(ValidEmail, ValidTaxId, ValidPassword, ValidRole);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        for (int i = 0; i < 5; i++)
            entity.RecordFailedLogin(now);

        DateTimeOffset originalLockout = entity.LockoutUntil!.Value;

        entity.RecordFailedLogin(now.AddMinutes(1));

        entity.LockoutUntil.Should().Be(originalLockout);
    }

    [Fact]
    public void ClearLockout_ResetsCounterAndLockoutUntil()
    {
        UserEntity entity = UserEntity.Create(ValidEmail, ValidTaxId, ValidPassword, ValidRole);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        for (int i = 0; i < 5; i++)
            entity.RecordFailedLogin(now);

        entity.ClearLockout();

        entity.FailedLoginAttempts.Should().Be(0);
        entity.LockoutUntil.Should().BeNull();
    }

    [Fact]
    public void IsLockedOut_ReturnsTrueWhenWithinWindow()
    {
        UserEntity entity = UserEntity.Create(ValidEmail, ValidTaxId, ValidPassword, ValidRole);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        for (int i = 0; i < 5; i++)
            entity.RecordFailedLogin(now);

        entity.IsLockedOut(DateTimeOffset.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void IsLockedOut_ReturnsFalseWhenExpired()
    {
        UserEntity entity = UserEntity.Create(ValidEmail, ValidTaxId, ValidPassword, ValidRole);
        DateTimeOffset pastNow = DateTimeOffset.UtcNow.AddMinutes(-16);

        for (int i = 0; i < 5; i++)
            entity.RecordFailedLogin(pastNow);

        entity.IsLockedOut(DateTimeOffset.UtcNow).Should().BeFalse();
    }
}
