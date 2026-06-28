using FluentAssertions;
using FluentValidation.Results;
using RentifyxIdentity.Application.Features.Identity.Auth.RefreshToken.Request;
using RentifyxIdentity.Application.Features.Identity.Auth.RefreshToken.Validator;
using RentifyxIdentity.Domain.MessageResource;
using RentifyxIdentity.Tests.Common.Constants;
using Xunit;

namespace RentifyxIdentity.Tests.Validators.Features.Identity;

public sealed class RefreshTokenValidatorTests
{
    private readonly RefreshTokenValidator _validator = new();

    [Fact]
    public async Task ValidRequest_ShouldPassValidation()
    {
        RefreshTokenRequest request = new(TestConstants.ValidEmail, TestConstants.RawRefreshToken);

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Email_Empty_ShouldFailValidation()
    {
        RefreshTokenRequest request = new(string.Empty, TestConstants.RawRefreshToken);

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email"
            && e.ErrorMessage == ValidationMessageResource.EMAIL_REQUIRED);
    }

    [Fact]
    public async Task Email_InvalidFormat_ShouldFailValidation()
    {
        RefreshTokenRequest request = new("notanemail", TestConstants.RawRefreshToken);

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email"
            && e.ErrorMessage == ValidationMessageResource.EMAIL_INVALID_FORMAT);
    }

    [Fact]
    public async Task RefreshToken_Empty_ShouldFailValidation()
    {
        RefreshTokenRequest request = new(TestConstants.ValidEmail, string.Empty);

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "RefreshToken"
            && e.ErrorMessage == ValidationMessageResource.TOKEN_REQUIRED);
    }
}
