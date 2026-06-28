using FluentAssertions;
using FluentValidation.Results;
using RentifyxIdentity.Application.Features.Identity.User.DeleteAccount.Request;
using RentifyxIdentity.Application.Features.Identity.User.DeleteAccount.Validator;
using Xunit;

namespace RentifyxIdentity.Tests.Validators.Features.Identity;

public sealed class DeleteAccountValidatorTests
{
    private readonly DeleteAccountValidator _validator = new();

    [Fact]
    public async Task ValidUserId_ShouldPassValidation()
    {
        DeleteAccountRequest request = new(Guid.NewGuid());

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task EmptyUserId_ShouldFailValidation()
    {
        DeleteAccountRequest request = new(Guid.Empty);

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "UserId");
    }
}
