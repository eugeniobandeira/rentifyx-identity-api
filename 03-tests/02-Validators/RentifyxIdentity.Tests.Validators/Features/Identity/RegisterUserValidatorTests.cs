using FluentAssertions;
using FluentValidation.Results;
using RentifyxIdentity.Application.Features.Identity.Auth.Register.Request;
using RentifyxIdentity.Application.Features.Identity.Auth.Register.Validator;
using RentifyxIdentity.Domain.Constants;
using RentifyxIdentity.Domain.MessageResource;
using RentifyxIdentity.Tests.Common.Constants;
using Xunit;

namespace RentifyxIdentity.Tests.Validators.Features.Identity;

public sealed class RegisterUserValidatorTests
{
    private readonly RegisterUserValidator _validator = new();

    [Fact]
    public async Task ValidRequest_ShouldPassValidation()
    {
        RegisterUserRequest request = new(
            TestConstants.ValidEmail,
            TestConstants.TaxIdCpfFormatted,
            TestConstants.ValidPassword,
            TestConstants.ValidRoles[0],
            ConsentGiven: true);

        ValidationResult result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Email_Empty_ShouldReturnEmailRequiredError()
    {
        RegisterUserRequest request = new(
            string.Empty,
            TestConstants.TaxIdCpfFormatted,
            TestConstants.ValidPassword,
            TestConstants.ValidRoles[0],
            ConsentGiven: true);

        ValidationResult result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == ValidationMessageResource.EMAIL_REQUIRED);
    }

    [Fact]
    public async Task Email_InvalidFormat_ShouldReturnEmailInvalidFormatError()
    {
        RegisterUserRequest request = new(
            "notanemail",
            TestConstants.TaxIdCpfFormatted,
            TestConstants.ValidPassword,
            TestConstants.ValidRoles[0],
            ConsentGiven: true);

        ValidationResult result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == ValidationMessageResource.EMAIL_INVALID_FORMAT);
    }

    [Fact]
    public async Task Email_DisposableDomain_ShouldReturnEmailDisposableDomainError()
    {
        RegisterUserRequest request = new(
            TestConstants.DisposableDomainEmail,
            TestConstants.TaxIdCpfFormatted,
            TestConstants.ValidPassword,
            TestConstants.ValidRoles[0],
            ConsentGiven: true);

        ValidationResult result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == ValidationMessageResource.EMAIL_DISPOSABLE_DOMAIN);
    }

    [Fact]
    public async Task Email_ExceedsMaxLength_ShouldReturnEmailMaxLengthError()
    {
        string email = new string('a', ValidationConstants.UserRules.EmailMaxLength - 5) + "@b.com";
        RegisterUserRequest request = new(
            email,
            TestConstants.TaxIdCpfFormatted,
            TestConstants.ValidPassword,
            TestConstants.ValidRoles[0],
            ConsentGiven: true);

        ValidationResult result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email"
            && e.ErrorMessage.Contains(
                ValidationConstants.UserRules.EmailMaxLength.ToString(),
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task TaxId_Empty_ShouldReturnTaxIdRequiredError()
    {
        RegisterUserRequest request = new(
            TestConstants.ValidEmail,
            string.Empty,
            TestConstants.ValidPassword,
            TestConstants.ValidRoles[0],
            ConsentGiven: true);

        ValidationResult result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == ValidationMessageResource.TAXID_REQUIRED);
    }

    [Fact]
    public async Task Password_Empty_ShouldReturnPasswordRequiredError()
    {
        RegisterUserRequest request = new(
            TestConstants.ValidEmail,
            TestConstants.TaxIdCpfFormatted,
            string.Empty,
            TestConstants.ValidRoles[0],
            ConsentGiven: true);

        ValidationResult result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == ValidationMessageResource.PASSWORD_REQUIRED);
    }

    [Fact]
    public async Task Password_TooShort_ShouldReturnPasswordMinLengthError()
    {
        RegisterUserRequest request = new(
            TestConstants.ValidEmail,
            TestConstants.TaxIdCpfFormatted,
            TestConstants.PasswordTooShort,
            TestConstants.ValidRoles[0],
            ConsentGiven: true);

        ValidationResult result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password"
            && e.ErrorMessage.Contains(
                ValidationConstants.UserRules.PasswordMinLength.ToString(),
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Password_MissingComplexity_ShouldReturnPasswordComplexityError()
    {
        RegisterUserRequest request = new(
            TestConstants.ValidEmail,
            TestConstants.TaxIdCpfFormatted,
            "alllowercase123!",
            TestConstants.ValidRoles[0],
            ConsentGiven: true);

        ValidationResult result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == ValidationMessageResource.PASSWORD_COMPLEXITY);
    }

    [Fact]
    public async Task Password_ExceedsMaxLength_ShouldReturnPasswordMaxLengthError()
    {
        string password = new string('A', ValidationConstants.UserRules.PasswordMaxLength) + "a1!";
        RegisterUserRequest request = new(
            TestConstants.ValidEmail,
            TestConstants.TaxIdCpfFormatted,
            password,
            TestConstants.ValidRoles[0],
            ConsentGiven: true);

        ValidationResult result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password"
            && e.ErrorMessage.Contains(
                ValidationConstants.UserRules.PasswordMaxLength.ToString(),
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Role_Empty_ShouldReturnRoleRequiredError()
    {
        RegisterUserRequest request = new(
            TestConstants.ValidEmail,
            TestConstants.TaxIdCpfFormatted,
            TestConstants.ValidPassword,
            string.Empty,
            ConsentGiven: true);

        ValidationResult result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == ValidationMessageResource.ROLE_REQUIRED);
    }

    [Fact]
    public async Task Role_Invalid_ShouldReturnRoleInvalidError()
    {
        RegisterUserRequest request = new(
            TestConstants.ValidEmail,
            TestConstants.TaxIdCpfFormatted,
            TestConstants.ValidPassword,
            "owner",
            ConsentGiven: true);

        ValidationResult result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == ValidationMessageResource.ROLE_INVALID);
    }

    [Fact]
    public async Task ConsentGiven_False_ShouldReturnConsentRequiredError()
    {
        RegisterUserRequest request = new(
            TestConstants.ValidEmail,
            TestConstants.TaxIdCpfFormatted,
            TestConstants.ValidPassword,
            TestConstants.ValidRoles[0],
            ConsentGiven: false);

        ValidationResult result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == ValidationMessageResource.CONSENT_REQUIRED);
    }

    [Fact]
    public async Task MultipleInvalidFields_ShouldReturnAllErrors()
    {
        RegisterUserRequest request = new(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            ConsentGiven: true);

        ValidationResult result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
        result.Errors.Should().Contain(e => e.PropertyName == "TaxId");
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
        result.Errors.Should().Contain(e => e.PropertyName == "Role");
    }

    [Fact]
    public async Task Password_ExactlyMinLength_WithComplexity_ShouldPassValidation()
    {
        RegisterUserRequest request = new(
            TestConstants.ValidEmail,
            TestConstants.TaxIdCpfFormatted,
            "Ab1!Ab1!Ab1!",
            TestConstants.ValidRoles[1],
            ConsentGiven: true);

        ValidationResult result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }
}
