using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using RentifyxIdentity.Domain.Constants;
using RentifyxIdentity.Domain.Interfaces.Users;
using RentifyxIdentity.Infrastructure.Constants;

namespace RentifyxIdentity.Infrastructure.Services;

public sealed class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly RSA _rsa;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;

        string? pem = configuration[ConfigurationKeys.JwtPrivateKeyPem];

        if (string.IsNullOrWhiteSpace(pem))
            throw new InvalidOperationException($"{ConfigurationKeys.JwtPrivateKeyPem} is not configured.");

        _rsa = RSA.Create();
        _rsa.ImportFromPem(pem.AsSpan());
    }

    public string GenerateAccessToken(
        Guid userId,
        string email,
        string role)
    {
        RsaSecurityKey securityKey = new(_rsa);
        SigningCredentials signingCreds = new(
            securityKey,
            SecurityAlgorithms.RsaSha256);

        List<Claim> claims =
        [
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim("role", role)
        ];

        JwtSecurityToken token = new(
            issuer: _configuration[ConfigurationKeys.JwtIssuer],
            audience: _configuration[ConfigurationKeys.JwtAudience],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(TokenPolicyConstants.AccessTokenMinutes),
            signingCredentials: signingCreds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public string HashToken(string rawToken)
    {
        string? hmacKey = _configuration[ConfigurationKeys.HmacKey];

        if (string.IsNullOrWhiteSpace(hmacKey))
            throw new InvalidOperationException($"{ConfigurationKeys.HmacKey} is not configured.");

        byte[] key = Encoding.UTF8.GetBytes(hmacKey);
        using HMACSHA256 hmac = new(key);
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawToken)));
    }

    public bool VerifyTokenHash(
        string rawToken,
        string storedHash)
        => HashToken(rawToken) == storedHash;
}
