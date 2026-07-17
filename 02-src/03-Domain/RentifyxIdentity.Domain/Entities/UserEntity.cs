using RentifyxIdentity.Domain.Common;
using RentifyxIdentity.Domain.Constants;
using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Domain.Events;
using RentifyxIdentity.Domain.ValueObjects;

namespace RentifyxIdentity.Domain.Entities;

public sealed class UserEntity : AggregateRoot
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
    public DateTimeOffset? EssentialConsentRevokedAt { get; private set; }
    public DateTimeOffset? MarketingConsentGivenAt { get; private set; }
    public DateTimeOffset? MarketingConsentRevokedAt { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTimeOffset? LockoutUntil { get; private set; }

    public bool IsEssentialConsentGranted => ConsentGivenAt.HasValue;
    public bool IsMarketingConsentGranted => MarketingConsentGivenAt.HasValue;

    private UserEntity() { }

    public bool IsLockedOut(DateTimeOffset now) =>
        LockoutUntil.HasValue && now < LockoutUntil.Value;

    public void SetConsent(DateTimeOffset timestamp)
    {
        ConsentGivenAt = timestamp;
    }

    public void GrantEssentialConsent(DateTimeOffset now)
    {
        ConsentGivenAt = now;
        EssentialConsentRevokedAt = null;
        Status = UserStatus.Active;
    }

    public void RevokeEssentialConsent(DateTimeOffset now)
    {
        ConsentGivenAt = null;
        EssentialConsentRevokedAt = now;
        Suspend();
    }

    public void GrantMarketingConsent(DateTimeOffset now)
    {
        MarketingConsentGivenAt = now;
        MarketingConsentRevokedAt = null;
    }

    public void RevokeMarketingConsent(DateTimeOffset now)
    {
        MarketingConsentGivenAt = null;
        MarketingConsentRevokedAt = now;
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
        RaiseDomainEvent(new UserEmailVerified(Id, Email.ToString(), DateTimeOffset.UtcNow));
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
        RaiseDomainEvent(new UserPasswordChanged(Id, DateTimeOffset.UtcNow));
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

    public void RecordFailedLogin(DateTimeOffset now)
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= UserPolicyConstants.MaxFailedLoginAttempts && !LockoutUntil.HasValue)
            LockoutUntil = now.AddMinutes(UserPolicyConstants.LockoutDurationMinutes);
    }

    public void ClearLockout()
    {
        FailedLoginAttempts = 0;
        LockoutUntil = null;
    }

    public void Suspend()
    {
        Status = UserStatus.Suspended;
        // Only call site today is RevokeEssentialConsent() — this reason text is accurate for
        // 100% of current callers. Revisit if Suspend() ever gets a second call site with a
        // different cause (Suspend()'s signature can't take a reason param, per R-01's
        // unchanged-public-API constraint).
        RaiseDomainEvent(new UserSuspended(Id, "Essential consent revoked", DateTimeOffset.UtcNow));
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
        DateTimeOffset? consentGivenAt = null,
        int failedLoginAttempts = 0,
        DateTimeOffset? lockoutUntil = null,
        DateTimeOffset? essentialConsentRevokedAt = null,
        DateTimeOffset? marketingConsentGivenAt = null,
        DateTimeOffset? marketingConsentRevokedAt = null)
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
            EssentialConsentRevokedAt = essentialConsentRevokedAt,
            MarketingConsentGivenAt = marketingConsentGivenAt,
            MarketingConsentRevokedAt = marketingConsentRevokedAt,
            RefreshTokenExpiry = refreshTokenExpiry,
            ConsentGivenAt = consentGivenAt,
            FailedLoginAttempts = failedLoginAttempts,
            LockoutUntil = lockoutUntil
        };
    }

    public void Anonymize()
    {
        Status = UserStatus.Deleted;
        Email = Email.Create(string.Format(System.Globalization.CultureInfo.InvariantCulture, AnonymizationConstants.EmailPattern, Id));
        TaxId = TaxDocument.CreateAnonymized();
        PasswordHash = Password.FromHash(AnonymizationConstants.Marker);
        RaiseDomainEvent(new UserAccountDeleted(Id, DateTimeOffset.UtcNow));
    }
}
