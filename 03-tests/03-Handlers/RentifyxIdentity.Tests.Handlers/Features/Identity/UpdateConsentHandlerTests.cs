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

public sealed class UpdateConsentHandlerTests
{
    private readonly Mock<IUserRepository> _repositoryMock = new();
    private readonly Mock<IAuditLogService> _auditLogServiceMock = new();
    private readonly Mock<IValidator<UpdateConsentRequest>> _validatorMock = new();
    private readonly Mock<ILogger<UpdateConsentHandler>> _loggerMock = new();
    private readonly UpdateConsentHandler _handler;

    public UpdateConsentHandlerTests()
    {
        _auditLogServiceMock
            .Setup(a => a.LogAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<UpdateConsentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _handler = new UpdateConsentHandler(
            _repositoryMock.Object,
            _auditLogServiceMock.Object,
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
    public async Task RevokeEssential_SuspendsAccount_AndReturnsRevoked()
    {
        UserEntity user = BuildUser();
        user.GrantEssentialConsent(DateTimeOffset.UtcNow);
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        ErrorOr<ConsentResponse> result = await _handler.HandleAsync(
            new UpdateConsentRequest(user.Id, "Essential", false));

        result.IsError.Should().BeFalse();
        result.Value.EssentialGranted.Should().BeFalse();
        result.Value.EssentialRevokedAt.Should().NotBeNull();
        user.Status.Should().Be(UserStatus.Suspended);

        _auditLogServiceMock.Verify(
            a => a.LogAsync(user.Id, AuditEvents.EssentialConsentRevoked, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GrantEssential_ReactivatesAccount_AndReturnsGranted()
    {
        UserEntity user = BuildUser();
        user.RevokeEssentialConsent(DateTimeOffset.UtcNow);
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        ErrorOr<ConsentResponse> result = await _handler.HandleAsync(
            new UpdateConsentRequest(user.Id, "Essential", true));

        result.IsError.Should().BeFalse();
        result.Value.EssentialGranted.Should().BeTrue();
        result.Value.EssentialRevokedAt.Should().BeNull();
        user.Status.Should().Be(UserStatus.Active);

        _auditLogServiceMock.Verify(
            a => a.LogAsync(user.Id, AuditEvents.EssentialConsentGranted, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GrantMarketing_DoesNotChangeAccountStatus_AndReturnsGranted()
    {
        UserEntity user = BuildUser();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        ErrorOr<ConsentResponse> result = await _handler.HandleAsync(
            new UpdateConsentRequest(user.Id, "Marketing", true));

        result.IsError.Should().BeFalse();
        result.Value.MarketingGranted.Should().BeTrue();
        user.Status.Should().Be(UserStatus.Active);

        _auditLogServiceMock.Verify(
            a => a.LogAsync(user.Id, AuditEvents.MarketingConsentGranted, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RevokeMarketing_DoesNotChangeAccountStatus_AndReturnsRevoked()
    {
        UserEntity user = BuildUser();
        user.GrantMarketingConsent(DateTimeOffset.UtcNow);
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        ErrorOr<ConsentResponse> result = await _handler.HandleAsync(
            new UpdateConsentRequest(user.Id, "Marketing", false));

        result.IsError.Should().BeFalse();
        result.Value.MarketingGranted.Should().BeFalse();
        user.Status.Should().Be(UserStatus.Active);

        _auditLogServiceMock.Verify(
            a => a.LogAsync(user.Id, AuditEvents.MarketingConsentRevoked, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RevokeEssentialTwice_IsIdempotent()
    {
        UserEntity user = BuildUser();
        user.GrantEssentialConsent(DateTimeOffset.UtcNow);
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        await _handler.HandleAsync(new UpdateConsentRequest(user.Id, "Essential", false));
        ErrorOr<ConsentResponse> result = await _handler.HandleAsync(
            new UpdateConsentRequest(user.Id, "Essential", false));

        result.IsError.Should().BeFalse();
        result.Value.EssentialGranted.Should().BeFalse();
        user.Status.Should().Be(UserStatus.Suspended);
    }

    [Fact]
    public async Task AuditLogFails_StillReturnsSuccess()
    {
        UserEntity user = BuildUser();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _auditLogServiceMock
            .Setup(a => a.LogAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DynamoDB unavailable"));

        ErrorOr<ConsentResponse> result = await _handler.HandleAsync(
            new UpdateConsentRequest(user.Id, "Essential", false));

        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task UserNotFound_ReturnsNotFound_AndDoesNotAudit()
    {
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity?)null);

        ErrorOr<ConsentResponse> result = await _handler.HandleAsync(
            new UpdateConsentRequest(Guid.NewGuid(), "Essential", false));

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
        result.FirstError.Code.Should().Be(UserErrorCodes.NotFound);

        _auditLogServiceMock.Verify(
            a => a.LogAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeletedUser_ReturnsNotFound_AndDoesNotAudit()
    {
        UserEntity user = BuildUser(UserStatus.Deleted);
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        ErrorOr<ConsentResponse> result = await _handler.HandleAsync(
            new UpdateConsentRequest(user.Id, "Essential", false));

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
        result.FirstError.Code.Should().Be(UserErrorCodes.NotFound);

        _auditLogServiceMock.Verify(
            a => a.LogAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ValidationFailure_ReturnsErrors_AndDoesNotCallRepository()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<UpdateConsentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[]
            {
                new ValidationFailure("Purpose", "Invalid")
                {
                    ErrorCode = TestConstants.ValidationErrorCodeNotEmpty
                }
            }));

        ErrorOr<ConsentResponse> result = await _handler.HandleAsync(
            new UpdateConsentRequest(Guid.NewGuid(), "NotAPurpose", true));

        result.IsError.Should().BeTrue();

        _repositoryMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
