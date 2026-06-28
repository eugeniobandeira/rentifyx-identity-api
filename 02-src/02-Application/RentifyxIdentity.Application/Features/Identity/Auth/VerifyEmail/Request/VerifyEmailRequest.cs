namespace RentifyxIdentity.Application.Features.Identity.Auth.VerifyEmail.Request;

public sealed record VerifyEmailRequest(string Email, string Token);
