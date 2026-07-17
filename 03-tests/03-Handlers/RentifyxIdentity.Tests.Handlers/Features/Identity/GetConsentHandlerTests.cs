using ErrorOr;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Moq;
using RentifyxIdentity.Application.Features.Identity.User.Consent;
using RentifyxIdentity.Application.Features.Identity.User.Consent.Request;
using RentifyxIdentity.Domain.Constants;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Domain.Interfaces.Users;
using RentifyxIdentity.Domain.ValueObjects;
using RentifyxIdentity.Tests.Common.Constants;
using Xunit;

namespace RentifyxIdentity.Tests.Handlers.Features.Identity;

public sealed class GetConsentHandlerTests
{
    private readonly Mock<IUserRepository> _repositoryMock = new();
    private readonly Mock<IValidator<GetConsentRequest>> _validatorMock = new();
    private readonly Mock<ILogger<GetConsentHandler>> _loggerMock = new();
    private readonly GetConsentHandler _handler;

    public GetConsentHandlerTests()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<GetConsentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _handler = new GetConsentHandler(
            _repositoryMock.Object,
            _validatorMock.Object,
            _loggerMock.Object);
    }

    private static UserEntity BuildUser(UserStatus status = UserStatus.Active)
    {
        UserEntity user = UserEntity.Create(
            Email.Create(TestConstants.ValidEmail),
            TaxDocument.Create(TestConstants.TaxIdCpfRaw),
            Password.FromPlaintext(TestConstants.ValidPassword),
            UserRole.Owner);

        if (status is UserStatus.Active)
            user.VerifyEmail();
        else if (status is UserStatus.Deleted)
            user.Anonymize();

        return user;
    }

    [Fact]
    public async Task HappyPath_NoConsentGranted_ReturnsAllFalse()
    {
        UserEntity user = BuildUser();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        ErrorOr<ConsentResponse> result = await _handler.HandleAsync(new GetConsentRequest(user.Id));

        result.IsError.Should().BeFalse();
        result.Value.EssentialGranted.Should().BeFalse();
        result.Value.MarketingGranted.Should().BeFalse();
    }

    [Fact]
    public async Task HappyPath_EssentialConsentGranted_ReturnsEssentialGrantedTrue()
    {
        UserEntity user = BuildUser();
        user.GrantEssentialConsent(DateTimeOffset.UtcNow);
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        ErrorOr<ConsentResponse> result = await _handler.HandleAsync(new GetConsentRequest(user.Id));

        result.IsError.Should().BeFalse();
        result.Value.EssentialGranted.Should().BeTrue();
        result.Value.EssentialGrantedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task HappyPath_MarketingConsentGranted_ReturnsMarketingGrantedTrue()
    {
        UserEntity user = BuildUser();
        user.GrantMarketingConsent(DateTimeOffset.UtcNow);
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        ErrorOr<ConsentResponse> result = await _handler.HandleAsync(new GetConsentRequest(user.Id));

        result.IsError.Should().BeFalse();
        result.Value.MarketingGranted.Should().BeTrue();
        result.Value.MarketingGrantedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UserNotFound_ReturnsNotFound()
    {
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity?)null);

        ErrorOr<ConsentResponse> result = await _handler.HandleAsync(new GetConsentRequest(Guid.NewGuid()));

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
        result.FirstError.Code.Should().Be(UserErrorCodes.NotFound);
    }

    [Fact]
    public async Task DeletedUser_ReturnsNotFound()
    {
        UserEntity user = BuildUser(UserStatus.Deleted);
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        ErrorOr<ConsentResponse> result = await _handler.HandleAsync(new GetConsentRequest(user.Id));

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
        result.FirstError.Code.Should().Be(UserErrorCodes.NotFound);
    }

    [Fact]
    public async Task ValidationFailure_ReturnsErrors_AndDoesNotCallRepository()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<GetConsentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[]
            {
                new ValidationFailure("UserId", "Required")
                {
                    ErrorCode = TestConstants.ValidationErrorCodeNotEmpty
                }
            }));

        ErrorOr<ConsentResponse> result = await _handler.HandleAsync(new GetConsentRequest(Guid.Empty));

        result.IsError.Should().BeTrue();

        _repositoryMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
