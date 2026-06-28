using FluentAssertions;
using FluentValidation.Results;
using RentifyxIdentity.Application.Features.Identity.Auth.Login.Request;
using RentifyxIdentity.Application.Features.Identity.Auth.Login.Validator;
using RentifyxIdentity.Domain.MessageResource;
using RentifyxIdentity.Tests.Common.Constants;
using Xunit;

namespace RentifyxIdentity.Tests.Validators.Features.Identity;

public sealed class LoginValidatorTests
{
    private readonly LoginValidator _validator = new();

    [Fact]
    public async Task ValidRequest_ShouldPassValidation()
    {
        LoginRequest request = new(TestConstants.ValidEmail, TestConstants.ValidPassword);

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Email_Empty_ShouldFailValidation()
    {
        LoginRequest request = new(string.Empty, TestConstants.ValidPassword);

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email"
            && e.ErrorMessage == ValidationMessageResource.EMAIL_REQUIRED);
    }

    [Fact]
    public async Task Email_InvalidFormat_ShouldFailValidation()
    {
        LoginRequest request = new("notanemail", TestConstants.ValidPassword);

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email"
            && e.ErrorMessage == ValidationMessageResource.EMAIL_INVALID_FORMAT);
    }

    [Fact]
    public async Task Password_Empty_ShouldFailValidation()
    {
        LoginRequest request = new(TestConstants.ValidEmail, string.Empty);

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password"
            && e.ErrorMessage == ValidationMessageResource.PASSWORD_REQUIRED);
    }
}
