using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Domain.ValueObjects;

namespace RentifyxIdentity.Domain.Entities;

public sealed class UserEntity
{
    public Guid Id { get; private set; }
    public Email Email { get; private set; } = null!;
    public TaxDocument TaxId { get; private set; } = null!;
    public Password PasswordHash { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public UserStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? EmailVerificationTokenHash { get; private set; }
    public DateTimeOffset? EmailVerificationTokenExpiry { get; private set; }
    public string? PasswordResetTokenHash { get; private set; }
    public DateTimeOffset? PasswordResetTokenExpiry { get; private set; }
    public string? RefreshTokenHash { get; private set; }
    public DateTimeOffset? RefreshTokenExpiry { get; private set; }
    public DateTimeOffset? ConsentGivenAt { get; private set; }

    private UserEntity() { }

    public void SetConsent(DateTimeOffset timestamp)
    {
        ConsentGivenAt = timestamp;
    }

    public static UserEntity Create(Email email, TaxDocument taxId, Password passwordHash, UserRole role)
    {
        return new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = email,
            TaxId = taxId,
            PasswordHash = passwordHash,
            Role = role,
            Status = UserStatus.PendingVerification,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void SetEmailVerificationToken(string hash, DateTimeOffset expiry)
    {
        EmailVerificationTokenHash = hash;
        EmailVerificationTokenExpiry = expiry;
    }

    public void VerifyEmail()
    {
        Status = UserStatus.Active;
        EmailVerificationTokenHash = null;
        EmailVerificationTokenExpiry = null;
    }

    public void SetPasswordResetToken(string hash, DateTimeOffset expiry)
    {
        PasswordResetTokenHash = hash;
        PasswordResetTokenExpiry = expiry;
    }

    public void ResetPassword(Password newPassword)
    {
        PasswordHash = newPassword;
        PasswordResetTokenHash = null;
        PasswordResetTokenExpiry = null;
    }

    public void SetRefreshToken(string hash, DateTimeOffset expiry)
    {
        RefreshTokenHash = hash;
        RefreshTokenExpiry = expiry;
    }

    public void ClearRefreshToken()
    {
        RefreshTokenHash = null;
        RefreshTokenExpiry = null;
    }

    public void Suspend()
    {
        Status = UserStatus.Suspended;
    }

    internal static UserEntity Reconstitute(
        Guid id,
        Email email,
        TaxDocument taxId,
        Password passwordHash,
        UserRole role,
        UserStatus status,
        DateTimeOffset createdAt,
        string? emailVerificationTokenHash,
        DateTimeOffset? emailVerificationTokenExpiry,
        string? passwordResetTokenHash,
        DateTimeOffset? passwordResetTokenExpiry,
        string? refreshTokenHash,
        DateTimeOffset? refreshTokenExpiry,
        DateTimeOffset? consentGivenAt = null)
    {
        return new UserEntity
        {
            Id = id,
            Email = email,
            TaxId = taxId,
            PasswordHash = passwordHash,
            Role = role,
            Status = status,
            CreatedAt = createdAt,
            EmailVerificationTokenHash = emailVerificationTokenHash,
            EmailVerificationTokenExpiry = emailVerificationTokenExpiry,
            PasswordResetTokenHash = passwordResetTokenHash,
            PasswordResetTokenExpiry = passwordResetTokenExpiry,
            RefreshTokenHash = refreshTokenHash,
            RefreshTokenExpiry = refreshTokenExpiry,
            ConsentGivenAt = consentGivenAt
        };
    }

    public void Anonymize()
    {
        Status = UserStatus.Deleted;
        Email = Email.Create($"deleted_{Id}@anonymized.local");
        TaxId = TaxDocument.CreateAnonymized();
        PasswordHash = Password.FromHash("ANONYMIZED");
    }
}
