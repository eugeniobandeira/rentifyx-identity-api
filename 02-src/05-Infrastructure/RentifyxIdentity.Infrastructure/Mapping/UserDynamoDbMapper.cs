using System.Globalization;
using Amazon.DynamoDBv2.Model;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Domain.ValueObjects;

namespace RentifyxIdentity.Infrastructure.Mapping;

internal static class UserDynamoDbMapper
{
    public static Dictionary<string, AttributeValue> ToItem(UserEntity entity)
    {
        Dictionary<string, AttributeValue> item = new()
        {
            ["PK"] = new AttributeValue { S = $"USER#{entity.Id}" },
            ["Id"] = new AttributeValue { S = entity.Id.ToString() },
            ["Email"] = new AttributeValue { S = entity.Email.Value },
            ["GSI_Email_PK"] = new AttributeValue { S = $"EMAIL#{entity.Email.Value.ToLowerInvariant()}" },
            ["TaxId"] = new AttributeValue { S = entity.TaxId.RawValue },
            ["TaxDocumentType"] = new AttributeValue { S = entity.TaxId.DocumentType.ToString() },
            ["GSI_TaxId_PK"] = new AttributeValue { S = $"TAXID#{entity.TaxId.RawValue.ToUpperInvariant()}" },
            ["PasswordHash"] = new AttributeValue { S = entity.PasswordHash.HashValue },
            ["Role"] = new AttributeValue { S = entity.Role.ToString() },
            ["Status"] = new AttributeValue { S = entity.Status.ToString() },
            ["CreatedAt"] = new AttributeValue { S = entity.CreatedAt.ToString("O") }
        };

        if (entity.EmailVerificationTokenHash is not null)
            item["EmailVerificationTokenHash"] = new AttributeValue { S = entity.EmailVerificationTokenHash };

        if (entity.EmailVerificationTokenExpiry.HasValue)
            item["EmailVerificationTokenExpiry"] = new AttributeValue { S = entity.EmailVerificationTokenExpiry.Value.ToString("O") };

        if (entity.PasswordResetTokenHash is not null)
            item["PasswordResetTokenHash"] = new AttributeValue { S = entity.PasswordResetTokenHash };

        if (entity.PasswordResetTokenExpiry.HasValue)
            item["PasswordResetTokenExpiry"] = new AttributeValue { S = entity.PasswordResetTokenExpiry.Value.ToString("O") };

        if (entity.RefreshTokenHash is not null)
            item["RefreshTokenHash"] = new AttributeValue { S = entity.RefreshTokenHash };

        if (entity.RefreshTokenExpiry.HasValue)
            item["RefreshTokenExpiry"] = new AttributeValue { S = entity.RefreshTokenExpiry.Value.ToString("O") };

        if (entity.ConsentGivenAt.HasValue)
            item["ConsentGivenAt"] = new AttributeValue { S = entity.ConsentGivenAt.Value.ToString("O") };

        if (entity.Status == UserStatus.PendingVerification)
        {
            long ttl = DateTimeOffset.UtcNow.AddHours(48).ToUnixTimeSeconds();
            item["TTL"] = new AttributeValue { N = ttl.ToString(CultureInfo.InvariantCulture) };
        }

        return item;
    }

    public static UserEntity ToEntity(Dictionary<string, AttributeValue> item)
    {
        Email email = Email.Create(item["Email"].S);

        TaxDocument taxId = string.Equals(item["TaxId"].S, "ANONYMIZED", StringComparison.OrdinalIgnoreCase)
            ? TaxDocument.CreateAnonymized()
            : TaxDocument.Create(item["TaxId"].S);

        Password passwordHash = Password.FromHash(item["PasswordHash"].S);
        UserRole role = Enum.Parse<UserRole>(item["Role"].S);
        UserStatus status = Enum.Parse<UserStatus>(item["Status"].S);
        DateTimeOffset createdAt = DateTimeOffset.Parse(item["CreatedAt"].S, CultureInfo.InvariantCulture);

        item.TryGetValue("EmailVerificationTokenHash", out AttributeValue? evthAttr);
        string? emailVerificationTokenHash = evthAttr?.S;

        item.TryGetValue("EmailVerificationTokenExpiry", out AttributeValue? evteAttr);
        DateTimeOffset? emailVerificationTokenExpiry = evteAttr is not null
            ? DateTimeOffset.Parse(evteAttr.S, CultureInfo.InvariantCulture)
            : null;

        item.TryGetValue("PasswordResetTokenHash", out AttributeValue? prthAttr);
        string? passwordResetTokenHash = prthAttr?.S;

        item.TryGetValue("PasswordResetTokenExpiry", out AttributeValue? prteAttr);
        DateTimeOffset? passwordResetTokenExpiry = prteAttr is not null
            ? DateTimeOffset.Parse(prteAttr.S, CultureInfo.InvariantCulture)
            : null;

        item.TryGetValue("RefreshTokenHash", out AttributeValue? rthAttr);
        string? refreshTokenHash = rthAttr?.S;

        item.TryGetValue("RefreshTokenExpiry", out AttributeValue? rteAttr);
        DateTimeOffset? refreshTokenExpiry = rteAttr is not null
            ? DateTimeOffset.Parse(rteAttr.S, CultureInfo.InvariantCulture)
            : null;

        item.TryGetValue("ConsentGivenAt", out AttributeValue? cgaAttr);
        DateTimeOffset? consentGivenAt = cgaAttr is not null
            ? DateTimeOffset.Parse(cgaAttr.S, CultureInfo.InvariantCulture)
            : null;

        return UserEntity.Reconstitute(
            id: Guid.Parse(item["Id"].S),
            email: email,
            taxId: taxId,
            passwordHash: passwordHash,
            role: role,
            status: status,
            createdAt: createdAt,
            emailVerificationTokenHash: emailVerificationTokenHash,
            emailVerificationTokenExpiry: emailVerificationTokenExpiry,
            passwordResetTokenHash: passwordResetTokenHash,
            passwordResetTokenExpiry: passwordResetTokenExpiry,
            refreshTokenHash: refreshTokenHash,
            refreshTokenExpiry: refreshTokenExpiry,
            consentGivenAt: consentGivenAt);
    }
}
