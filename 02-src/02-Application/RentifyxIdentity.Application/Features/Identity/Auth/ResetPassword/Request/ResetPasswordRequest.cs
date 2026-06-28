namespace RentifyxIdentity.Application.Features.Identity.Auth.ResetPassword.Request;

public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);
