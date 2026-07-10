using RentifyxIdentity.Application.Features.Identity;

namespace RentifyxIdentity.Api.Contracts.Auth;

internal sealed record AuthTokenResponse(
    string AccessToken,
    UserResponse User
);
