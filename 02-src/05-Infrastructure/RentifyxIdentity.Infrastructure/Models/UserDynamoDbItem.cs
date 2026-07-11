using Amazon.DynamoDBv2.DataModel;
using RentifyxIdentity.Infrastructure.Constants;

namespace RentifyxIdentity.Infrastructure.Models;

[DynamoDBTable(DynamoDbConstants.DefaultTableName)]
public sealed class UserDynamoDbItem
{
    [DynamoDBHashKey("PK")]
    public string Pk { get; set; } = null!;

    [DynamoDBRangeKey("SK")]
    public string Sk { get; set; } = null!;

    public string Id { get; set; } = null!;
    [DynamoDBGlobalSecondaryIndexHashKey("GSI_Email")]
    public string Email { get; set; } = null!;

    [DynamoDBGlobalSecondaryIndexHashKey("GSI_TaxId")]
    public string TaxId { get; set; } = null!;
    public string TaxDocumentType { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;
    public string Role { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string CreatedAt { get; set; } = null!;
    public string? EmailVerificationTokenHash { get; set; }
    public string? EmailVerificationTokenExpiry { get; set; }
    public string? PasswordResetTokenHash { get; set; }
    public string? PasswordResetTokenExpiry { get; set; }
    public string? RefreshTokenHash { get; set; }
    public string? RefreshTokenExpiry { get; set; }
    public string? ConsentGivenAt { get; set; }
    public string? EssentialConsentRevokedAt { get; set; }
    public string? MarketingConsentGivenAt { get; set; }
    public string? MarketingConsentRevokedAt { get; set; }
    public int FailedLoginAttempts { get; set; }

    [DynamoDBProperty("LockoutUntil")]
    public long? LockoutUntilEpoch { get; set; }

    [DynamoDBProperty("TTL")]
    public long? Ttl { get; set; }
}
