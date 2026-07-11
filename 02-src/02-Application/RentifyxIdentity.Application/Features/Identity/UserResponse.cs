namespace RentifyxIdentity.Application.Features.Identity;

public sealed record UserResponse(
    Guid Id,
    string Email,
    string Role,
    string Status,
    DateTimeOffset CreatedAt,
    bool EssentialConsentGranted,
    DateTimeOffset? EssentialConsentGivenAt,
    DateTimeOffset? EssentialConsentRevokedAt,
    bool MarketingConsentGranted,
    DateTimeOffset? MarketingConsentGivenAt,
    DateTimeOffset? MarketingConsentRevokedAt
);
