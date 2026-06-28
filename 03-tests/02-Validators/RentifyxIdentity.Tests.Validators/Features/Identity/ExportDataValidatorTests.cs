using FluentAssertions;
using FluentValidation.Results;
using RentifyxIdentity.Application.Features.Identity.User.ExportData.Request;
using RentifyxIdentity.Application.Features.Identity.User.ExportData.Validator;
using Xunit;

namespace RentifyxIdentity.Tests.Validators.Features.Identity;

public sealed class ExportDataValidatorTests
{
    private readonly ExportDataValidator _validator = new();

    [Fact]
    public async Task ValidUserId_ShouldPassValidation()
    {
        ExportDataRequest request = new(Guid.NewGuid());

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task EmptyUserId_ShouldFailValidation()
    {
        ExportDataRequest request = new(Guid.Empty);

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "UserId");
    }
}
