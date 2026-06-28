namespace RentifyxIdentity.Application.Features.Identity.Auth.RefreshToken.Request;

public sealed record RefreshTokenRequest(string Email, string RefreshToken);
