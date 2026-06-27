using Bogus;
using RentifyxIdentity.Application.Features.Identity.Auth.Register.Request;
using RentifyxIdentity.Tests.Common.Constants;

namespace RentifyxIdentity.Tests.Common.Builders;

public sealed class RegisterUserRequestBuilder
{
    private readonly Faker _faker = new("en");
    private string _email;
    private string _taxId;
    private string _password = TestConstants.ValidPassword;
    private string _role;

    public RegisterUserRequestBuilder()
    {
        // faker.Internet.Email() uses safe domains (gmail, yahoo, etc.) — not in disposable list
        _email = _faker.Internet.Email();
        // 11 random digits = valid CPF length (mod-11 not validated)
        _taxId = _faker.Random.ReplaceNumbers("###########");
        _role = _faker.PickRandom(TestConstants.ValidRoles);
    }

    public RegisterUserRequestBuilder WithEmail(string email) { _email = email; return this; }
    public RegisterUserRequestBuilder WithTaxId(string taxId) { _taxId = taxId; return this; }
    public RegisterUserRequestBuilder WithPassword(string password) { _password = password; return this; }
    public RegisterUserRequestBuilder WithRole(string role) { _role = role; return this; }

    public RegisterUserRequest Build() => new(
        _email,
        _taxId,
        _password,
        _role);
}
