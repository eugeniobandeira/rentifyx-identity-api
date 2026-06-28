using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using RentifyxIdentity.Infrastructure.Services;
using Xunit;

namespace RentifyxIdentity.Tests.Handlers.Features.Identity;

public sealed class TokenServiceTests
{
    private static string GenerateRsaPrivateKeyPem()
    {
        using RSA rsa = RSA.Create(2048);
        return rsa.ExportRSAPrivateKeyPem();
    }

    private static IConfiguration BuildConfiguration(
        string? privateKeyPem,
        string hmacKey = "test-hmac-key-for-unit-tests")
    {
        Dictionary<string, string?> values = new()
        {
            ["Jwt:PrivateKeyPem"] = privateKeyPem,
            ["Jwt:Issuer"] = "https://test.issuer",
            ["Jwt:Audience"] = "test-audience",
            ["Hmac:Key"] = hmacKey
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    [Fact]
    public void GenerateAccessToken_ValidPem_ReturnsRS256Jwt()
    {
        string pem = GenerateRsaPrivateKeyPem();
        IConfiguration config = BuildConfiguration(pem);
        TokenService sut = new(config);

        Guid userId = Guid.NewGuid();
        string email = "test@example.com";
        string role = "Owner";

        string tokenString = sut.GenerateAccessToken(
            userId,
            email,
            role);

        JwtSecurityTokenHandler handler = new();
        JwtSecurityToken jwt = handler.ReadJwtToken(tokenString);

        jwt.Header.Alg.Should().Be("RS256");
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == userId.ToString());
        jwt.Claims.Should().Contain(c => c.Type == "email" && c.Value == email);
        jwt.Claims.Should().Contain(c => c.Type == "role" && c.Value == role);
    }

    [Fact]
    public void Constructor_MissingPem_ThrowsInvalidOperationException()
    {
        IConfiguration config = BuildConfiguration(privateKeyPem: string.Empty);

        Action act = () => _ = new TokenService(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Jwt:PrivateKeyPem is not configured.");
    }

    [Fact]
    public void HashToken_SameInput_ReturnsSameHash()
    {
        string pem = GenerateRsaPrivateKeyPem();
        IConfiguration config = BuildConfiguration(pem);
        TokenService sut = new(config);

        string hash1 = sut.HashToken("token");
        string hash2 = sut.HashToken("token");

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void VerifyTokenHash_TamperedToken_ReturnsFalse()
    {
        string pem = GenerateRsaPrivateKeyPem();
        IConfiguration config = BuildConfiguration(pem);
        TokenService sut = new(config);

        string storedHash = sut.HashToken("original");

        bool result = sut.VerifyTokenHash(
            "tampered",
            storedHash);

        result.Should().BeFalse();
    }
}
