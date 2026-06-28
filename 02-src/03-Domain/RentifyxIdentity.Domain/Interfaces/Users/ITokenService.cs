namespace RentifyxIdentity.Domain.Interfaces.Users;

public interface ITokenService
{
    string GenerateAccessToken(Guid userId, string email, string role);
    string GenerateRefreshToken();
    string HashToken(string rawToken);
    bool VerifyTokenHash(string rawToken, string storedHash);
}
