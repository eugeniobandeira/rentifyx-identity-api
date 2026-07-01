using System.Globalization;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Domain.ValueObjects;
using RentifyxIdentity.Infrastructure.Models;

namespace RentifyxIdentity.Infrastructure.Mapping;

internal static class UserDynamoDbMapper
{
    public static UserDynamoDbItem ToItem(UserEntity entity)
    {
        string pk = $"USER#{entity.Id}";

        return new UserDynamoDbItem
        {
            Pk = pk,
            Sk = pk,
            Id = entity.Id.ToString(),
            Email = entity.Email.Value,
            GsiEmailPk = $"EMAIL#{entity.Email.Value.ToLowerInvariant()}",
            TaxId = entity.TaxId.RawValue,
            TaxDocumentType = entity.TaxId.DocumentType.ToString(),
            GsiTaxIdPk = $"TAXID#{entity.TaxId.RawValue.ToUpperInvariant()}",
            PasswordHash = entity.PasswordHash.HashValue,
            Role = entity.Role.ToString(),
            Status = entity.Status.ToString(),
            CreatedAt = entity.CreatedAt.ToString("O"),
            EmailVerificationTokenHash = entity.EmailVerificationTokenHash,
            EmailVerificationTokenExpiry = entity.EmailVerificationTokenExpiry?.ToString("O"),
            PasswordResetTokenHash = entity.PasswordResetTokenHash,
            PasswordResetTokenExpiry = entity.PasswordResetTokenExpiry?.ToString("O"),
            RefreshTokenHash = entity.RefreshTokenHash,
            RefreshTokenExpiry = entity.RefreshTokenExpiry?.ToString("O"),
            ConsentGivenAt = entity.ConsentGivenAt?.ToString("O"),
            FailedLoginAttempts = entity.FailedLoginAttempts,
            LockoutUntilEpoch = entity.LockoutUntil?.ToUnixTimeSeconds(),
            Ttl = entity.Status == UserStatus.PendingVerification
                ? DateTimeOffset.UtcNow.AddHours(48).ToUnixTimeSeconds()
                : null
        };
    }

    public static UserEntity ToEntity(UserDynamoDbItem item)
    {
        Email email = Email.Create(item.Email);

        TaxDocument taxId = string.Equals(item.TaxId, "ANONYMIZED", StringComparison.OrdinalIgnoreCase)
            ? TaxDocument.CreateAnonymized()
            : TaxDocument.Create(item.TaxId);

        Password passwordHash = Password.FromHash(item.PasswordHash);
        UserRole role = Enum.Parse<UserRole>(item.Role);
        UserStatus status = Enum.Parse<UserStatus>(item.Status);
        DateTimeOffset createdAt = DateTimeOffset.Parse(item.CreatedAt, CultureInfo.InvariantCulture);

        return UserEntity.Reconstitute(
            id: Guid.Parse(item.Id),
            email: email,
            taxId: taxId,
            passwordHash: passwordHash,
            role: role,
            status: status,
            createdAt: createdAt,
            emailVerificationTokenHash: item.EmailVerificationTokenHash,
            emailVerificationTokenExpiry: ParseDate(item.EmailVerificationTokenExpiry),
            passwordResetTokenHash: item.PasswordResetTokenHash,
            passwordResetTokenExpiry: ParseDate(item.PasswordResetTokenExpiry),
            refreshTokenHash: item.RefreshTokenHash,
            refreshTokenExpiry: ParseDate(item.RefreshTokenExpiry),
            consentGivenAt: ParseDate(item.ConsentGivenAt),
            failedLoginAttempts: item.FailedLoginAttempts,
            lockoutUntil: item.LockoutUntilEpoch.HasValue
                ? DateTimeOffset.FromUnixTimeSeconds(item.LockoutUntilEpoch.Value)
                : null);
    }

    private static DateTimeOffset? ParseDate(string? value) =>
        value is null ? null : DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
}
