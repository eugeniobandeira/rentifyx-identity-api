using Bogus;
using RentifyxIdentity.Application.Features.Identity.Auth.RefreshToken.Request;
using RentifyxIdentity.Tests.Common.Constants;

namespace RentifyxIdentity.Tests.Common.Builders;

public sealed class RefreshTokenRequestBuilder
{
    private readonly Faker _faker = new("en");
    private string _email;
    private string _refreshToken = TestConstants.RawRefreshToken;

    public RefreshTokenRequestBuilder()
    {
        _email = _faker.Internet.Email();
    }

    public RefreshTokenRequestBuilder WithEmail(string email) { _email = email; return this; }
    public RefreshTokenRequestBuilder WithRefreshToken(string token) { _refreshToken = token; return this; }

    public RefreshTokenRequest Build() => new(_email, _refreshToken);
}
