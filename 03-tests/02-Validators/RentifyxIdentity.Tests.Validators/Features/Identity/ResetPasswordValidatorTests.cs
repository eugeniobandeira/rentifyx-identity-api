using FluentAssertions;
using FluentValidation.Results;
using RentifyxIdentity.Application.Features.Identity.Auth.ResetPassword.Request;
using RentifyxIdentity.Application.Features.Identity.Auth.ResetPassword.Validator;
using RentifyxIdentity.Domain.MessageResource;
using RentifyxIdentity.Tests.Common.Constants;
using Xunit;

namespace RentifyxIdentity.Tests.Validators.Features.Identity;

public sealed class ResetPasswordValidatorTests
{
    private readonly ResetPasswordValidator _validator = new();

    [Fact]
    public async Task ValidRequest_ShouldPassValidation()
    {
        ResetPasswordRequest request = new(TestConstants.ValidEmail, TestConstants.RawRefreshToken, TestConstants.ValidPassword);

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Token_Empty_ShouldFailValidation()
    {
        ResetPasswordRequest request = new(TestConstants.ValidEmail, string.Empty, TestConstants.ValidPassword);

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Token"
            && e.ErrorMessage == ValidationMessageResource.TOKEN_REQUIRED);
    }

    [Fact]
    public async Task NewPassword_TooShort_ShouldFailValidation()
    {
        ResetPasswordRequest request = new(TestConstants.ValidEmail, TestConstants.RawRefreshToken, TestConstants.PasswordTooShort);

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        // ErrorMessage has {MinLength} substituted at runtime; check PropertyName and ErrorCode instead
        result.Errors.Should().Contain(e => e.PropertyName == "NewPassword"
            && e.ErrorCode == "MinimumLengthValidator");
    }

    [Fact]
    public async Task NewPassword_NoComplexity_ShouldFailValidation()
    {
        ResetPasswordRequest request = new(TestConstants.ValidEmail, TestConstants.RawRefreshToken, TestConstants.PasswordNoUpper);

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NewPassword"
            && e.ErrorMessage == ValidationMessageResource.PASSWORD_COMPLEXITY);
    }
}
