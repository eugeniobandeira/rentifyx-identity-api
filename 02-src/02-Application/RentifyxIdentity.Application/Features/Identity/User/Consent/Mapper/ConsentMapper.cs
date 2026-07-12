using RentifyxIdentity.Domain.Entities;

namespace RentifyxIdentity.Application.Features.Identity.User.Consent.Mapper;

public static class ConsentMapper
{
    public static ConsentResponse ToResponse(UserEntity entity)
        => new(
            entity.IsEssentialConsentGranted,
            entity.ConsentGivenAt,
            entity.EssentialConsentRevokedAt,
            entity.IsMarketingConsentGranted,
            entity.MarketingConsentGivenAt,
            entity.MarketingConsentRevokedAt
        );
}
