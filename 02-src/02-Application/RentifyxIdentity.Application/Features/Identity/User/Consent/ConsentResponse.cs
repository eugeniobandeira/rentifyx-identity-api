namespace RentifyxIdentity.Application.Features.Identity.User.Consent;

public sealed record ConsentResponse(
    bool EssentialGranted,
    DateTimeOffset? EssentialGrantedAt,
    DateTimeOffset? EssentialRevokedAt,
    bool MarketingGranted,
    DateTimeOffset? MarketingGrantedAt,
    DateTimeOffset? MarketingRevokedAt
);
