namespace RentifyxIdentity.Application.Features.Identity.User.Consent.Request;

public sealed record UpdateConsentRequest(Guid UserId, string Purpose, bool Granted);
