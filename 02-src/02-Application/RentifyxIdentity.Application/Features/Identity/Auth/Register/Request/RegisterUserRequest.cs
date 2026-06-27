namespace RentifyxIdentity.Application.Features.Identity.Auth.Register.Request;

public sealed record RegisterUserRequest(string Email, string TaxId, string Password, string Role);
