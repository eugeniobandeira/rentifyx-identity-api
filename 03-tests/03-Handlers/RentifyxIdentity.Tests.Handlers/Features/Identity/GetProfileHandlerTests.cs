using ErrorOr;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Moq;
using RentifyxIdentity.Application.Features.Identity;
using RentifyxIdentity.Application.Features.Identity.User.GetProfile;
using RentifyxIdentity.Application.Features.Identity.User.GetProfile.Request;
using RentifyxIdentity.Domain.Constants;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Domain.Interfaces.Users;
using RentifyxIdentity.Domain.ValueObjects;
using RentifyxIdentity.Tests.Common.Constants;
using Xunit;

namespace RentifyxIdentity.Tests.Handlers.Features.Identity;

public sealed class GetProfileHandlerTests
{
    private readonly Mock<IUserRepository> _repositoryMock = new();
    private readonly Mock<IAuditLogService> _auditLogServiceMock = new();
    private readonly Mock<IValidator<GetProfileRequest>> _validatorMock = new();
    private readonly Mock<ILogger<GetProfileHandler>> _loggerMock = new();
    private readonly GetProfileHandler _handler;

    public GetProfileHandlerTests()
    {
        _auditLogServiceMock
            .Setup(a => a.LogAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<GetProfileRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _handler = new GetProfileHandler(
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
        else if (status is UserStatus.Suspended)
            user.Suspend();
        else if (status is UserStatus.Deleted)
            user.Anonymize();

        return user;
    }

    [Fact]
    public async Task HappyPath_ActiveUser_ReturnsProfile()
    {
        UserEntity user = BuildUser();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        ErrorOr<UserResponse> result = await _handler.Handle(new GetProfileRequest(user.Id));

        result.IsError.Should().BeFalse();
        result.Value.Email.Should().Be(TestConstants.ValidEmail);
        result.Value.Status.Should().Be(TestConstants.StatusActive);

        _auditLogServiceMock.Verify(
            a => a.LogAsync(user.Id, AuditEvents.ProfileAccessed, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SuspendedUser_ReturnsProfile()
    {
        UserEntity user = BuildUser(UserStatus.Suspended);
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        ErrorOr<UserResponse> result = await _handler.Handle(new GetProfileRequest(user.Id));

        result.IsError.Should().BeFalse();
        result.Value.Status.Should().Be("Suspended");
    }

    [Fact]
    public async Task UserNotFound_ReturnsNotFound_AndDoesNotAudit()
    {
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity?)null);

        ErrorOr<UserResponse> result = await _handler.Handle(new GetProfileRequest(Guid.NewGuid()));

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

        ErrorOr<UserResponse> result = await _handler.Handle(new GetProfileRequest(user.Id));

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
        result.FirstError.Code.Should().Be(UserErrorCodes.NotFound);

        _auditLogServiceMock.Verify(
            a => a.LogAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
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

        ErrorOr<UserResponse> result = await _handler.Handle(new GetProfileRequest(user.Id));

        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task ValidationFailure_ReturnsErrors_AndDoesNotCallRepository()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<GetProfileRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[]
            {
                new ValidationFailure("UserId", "Required")
                {
                    ErrorCode = TestConstants.ValidationErrorCodeNotEmpty
                }
            }));

        ErrorOr<UserResponse> result = await _handler.Handle(new GetProfileRequest(Guid.Empty));

        result.IsError.Should().BeTrue();

        _repositoryMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
