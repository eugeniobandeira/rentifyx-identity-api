using RentifyxIdentity.Application.Features.Identity;

namespace RentifyxIdentity.Application.Features.Identity.Auth.Login;

public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    UserResponse User
);
