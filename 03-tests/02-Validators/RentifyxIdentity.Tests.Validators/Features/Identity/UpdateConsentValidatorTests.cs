using FluentAssertions;
using FluentValidation.Results;
using RentifyxIdentity.Application.Features.Identity.User.Consent.Request;
using RentifyxIdentity.Application.Features.Identity.User.Consent.Validator;
using RentifyxIdentity.Domain.MessageResource;
using Xunit;

namespace RentifyxIdentity.Tests.Validators.Features.Identity;

public sealed class UpdateConsentValidatorTests
{
    private readonly UpdateConsentValidator _validator = new();

    [Fact]
    public async Task UpdateConsent_ValidEssentialPurposeGranted_ShouldPassValidation()
    {
        UpdateConsentRequest request = new(
            Guid.NewGuid(),
            "Essential",
            true);

        ValidationResult result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateConsent_ValidEssentialPurposeRevoked_ShouldPassValidation()
    {
        UpdateConsentRequest request = new(
            Guid.NewGuid(),
            "Essential",
            false);

        ValidationResult result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateConsent_ValidMarketingPurposeGranted_ShouldPassValidation()
    {
        UpdateConsentRequest request = new(
            Guid.NewGuid(),
            "Marketing",
            true);

        ValidationResult result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateConsent_ValidMarketingPurposeRevoked_ShouldPassValidation()
    {
        UpdateConsentRequest request = new(
            Guid.NewGuid(),
            "Marketing",
            false);

        ValidationResult result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateConsent_EmptyPurpose_ShouldFailWithPurposeError()
    {
        UpdateConsentRequest request = new(
            Guid.NewGuid(),
            string.Empty,
            true);

        ValidationResult result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Purpose"
            && e.ErrorMessage == ValidationMessageResource.CONSENT_PURPOSE_REQUIRED);
    }

    [Fact]
    public async Task UpdateConsent_InvalidPurpose_ShouldFailWithPurposeError()
    {
        UpdateConsentRequest request = new(
            Guid.NewGuid(),
            "NotAPurpose",
            true);

        ValidationResult result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Purpose"
            && e.ErrorMessage == ValidationMessageResource.CONSENT_PURPOSE_INVALID);
    }
}
