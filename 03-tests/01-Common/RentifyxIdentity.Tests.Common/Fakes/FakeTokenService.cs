using System.Security.Cryptography;
using System.Text;
using RentifyxIdentity.Domain.Interfaces.Users;

namespace RentifyxIdentity.Tests.Common.Fakes;

public sealed class FakeTokenService : ITokenService
{
    private const string FixedKey = "test-hmac-key-for-integration-tests!!";

    public string GenerateAccessToken(Guid userId, string email, string role) =>
        $"test-access-token|{userId}|{email}|{role}";

    public string GenerateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public string HashToken(string rawToken)
    {
        byte[] key = Encoding.UTF8.GetBytes(FixedKey);
        byte[] data = Encoding.UTF8.GetBytes(rawToken);
        byte[] hash = HMACSHA256.HashData(key, data);
        return Convert.ToBase64String(hash);
    }

    public bool VerifyTokenHash(string rawToken, string storedHash) =>
        HashToken(rawToken) == storedHash;
}
