using System.Security.Cryptography;
using System.Text;
using RentifyxIdentity.Domain.Interfaces.Users;

namespace RentifyxIdentity.Infrastructure.Services;

// Stub implementation — real Cognito JWT issuance deferred to E-04 (ADR-006)
public sealed class TokenService : ITokenService
{
    public string GenerateAccessToken(
        Guid userId,
        string email,
        string role)
        => $"stub-access-{userId}";

    public string GenerateRefreshToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public string HashToken(string rawToken)
    {
        byte[] key = Encoding.UTF8.GetBytes("refresh-token-hmac-key");
        using HMACSHA256 hmac = new(key);
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawToken)));
    }

    public bool VerifyTokenHash(
        string rawToken,
        string storedHash)
        => HashToken(rawToken) == storedHash;
}
