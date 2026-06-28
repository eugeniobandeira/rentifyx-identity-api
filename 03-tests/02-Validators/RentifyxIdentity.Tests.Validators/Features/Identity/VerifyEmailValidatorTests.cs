using FluentAssertions;
using FluentValidation.Results;
using RentifyxIdentity.Application.Features.Identity.Auth.VerifyEmail.Request;
using RentifyxIdentity.Application.Features.Identity.Auth.VerifyEmail.Validator;
using RentifyxIdentity.Domain.MessageResource;
using RentifyxIdentity.Tests.Common.Constants;
using Xunit;

namespace RentifyxIdentity.Tests.Validators.Features.Identity;

public sealed class VerifyEmailValidatorTests
{
    private readonly VerifyEmailValidator _validator = new();

    [Fact]
    public async Task ValidRequest_ShouldPassValidation()
    {
        VerifyEmailRequest request = new(TestConstants.ValidEmail, "some-token");

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Email_Empty_ShouldFailValidation()
    {
        VerifyEmailRequest request = new(string.Empty, "some-token");

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email"
            && e.ErrorMessage == ValidationMessageResource.EMAIL_REQUIRED);
    }

    [Fact]
    public async Task Email_InvalidFormat_ShouldFailValidation()
    {
        VerifyEmailRequest request = new("notanemail", "some-token");

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email"
            && e.ErrorMessage == ValidationMessageResource.EMAIL_INVALID_FORMAT);
    }

    [Fact]
    public async Task Token_Empty_ShouldFailValidation()
    {
        VerifyEmailRequest request = new(TestConstants.ValidEmail, string.Empty);

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Token");
    }
}
