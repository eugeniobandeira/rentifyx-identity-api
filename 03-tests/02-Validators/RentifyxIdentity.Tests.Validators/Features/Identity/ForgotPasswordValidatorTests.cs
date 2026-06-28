using FluentAssertions;
using FluentValidation.Results;
using RentifyxIdentity.Application.Features.Identity.Auth.ForgotPassword.Request;
using RentifyxIdentity.Application.Features.Identity.Auth.ForgotPassword.Validator;
using RentifyxIdentity.Domain.MessageResource;
using RentifyxIdentity.Tests.Common.Constants;
using Xunit;

namespace RentifyxIdentity.Tests.Validators.Features.Identity;

public sealed class ForgotPasswordValidatorTests
{
    private readonly ForgotPasswordValidator _validator = new();

    [Fact]
    public async Task ValidRequest_ShouldPassValidation()
    {
        ForgotPasswordRequest request = new(TestConstants.ValidEmail);

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Email_Empty_ShouldFailValidation()
    {
        ForgotPasswordRequest request = new(string.Empty);

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email"
            && e.ErrorMessage == ValidationMessageResource.EMAIL_REQUIRED);
    }

    [Fact]
    public async Task Email_InvalidFormat_ShouldFailValidation()
    {
        ForgotPasswordRequest request = new("notanemail");

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email"
            && e.ErrorMessage == ValidationMessageResource.EMAIL_INVALID_FORMAT);
    }
}
