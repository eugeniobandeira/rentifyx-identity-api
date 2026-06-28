using Bogus;
using RentifyxIdentity.Application.Features.Identity.Auth.Login.Request;
using RentifyxIdentity.Tests.Common.Constants;

namespace RentifyxIdentity.Tests.Common.Builders;

public sealed class LoginRequestBuilder
{
    private readonly Faker _faker = new("en");
    private string _email;
    private string _password = TestConstants.ValidPassword;

    public LoginRequestBuilder()
    {
        _email = _faker.Internet.Email();
    }

    public LoginRequestBuilder WithEmail(string email) { _email = email; return this; }
    public LoginRequestBuilder WithPassword(string password) { _password = password; return this; }

    public LoginRequest Build() => new(_email, _password);
}
