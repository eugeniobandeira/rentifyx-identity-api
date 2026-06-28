namespace RentifyxIdentity.Application.Features.Identity.Auth.Logout.Request;

public sealed record LogoutRequest(string Email, string RefreshToken);
