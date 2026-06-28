using FluentAssertions;
using FluentValidation.Results;
using RentifyxIdentity.Application.Features.Identity.User.GetProfile.Request;
using RentifyxIdentity.Application.Features.Identity.User.GetProfile.Validator;
using Xunit;

namespace RentifyxIdentity.Tests.Validators.Features.Identity;

public sealed class GetProfileValidatorTests
{
    private readonly GetProfileValidator _validator = new();

    [Fact]
    public async Task ValidUserId_ShouldPassValidation()
    {
        GetProfileRequest request = new(Guid.NewGuid());

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task EmptyUserId_ShouldFailValidation()
    {
        GetProfileRequest request = new(Guid.Empty);

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "UserId");
    }
}
