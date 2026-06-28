using ErrorOr;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RentifyxIdentity.Application.Features.Identity.Auth.ForgotPassword;
using RentifyxIdentity.Application.Features.Identity.Auth.ForgotPassword.Request;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Domain.Interfaces.Users;
using RentifyxIdentity.Domain.ValueObjects;
using RentifyxIdentity.Tests.Common.Constants;
using Xunit;

namespace RentifyxIdentity.Tests.Handlers.Features.Identity;

public sealed class ForgotPasswordHandlerTests
{
    private readonly Mock<IUserRepository> _repositoryMock = new();
    private readonly Mock<IEmailService> _emailServiceMock = new();
    private readonly Mock<IValidator<ForgotPasswordRequest>> _validatorMock = new();
    private readonly Mock<IConfiguration> _configurationMock = new();
    private readonly Mock<ILogger<ForgotPasswordHandler>> _loggerMock = new();
    private readonly ForgotPasswordHandler _handler;

    public ForgotPasswordHandlerTests()
    {
        _configurationMock
            .Setup(c => c[TestConstants.HmacKeyConfigPath])
            .Returns(TestConstants.HmacKey);

        _validatorMock
            .Setup(v => v.ValidateAsync(
                It.IsAny<ForgotPasswordRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _handler = new ForgotPasswordHandler(
            _repositoryMock.Object,
            _emailServiceMock.Object,
            _validatorMock.Object,
            _configurationMock.Object,
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

        return user;
    }

    [Fact]
    public async Task HappyPath_ActiveUser_StoresTokenAndSendsEmail()
    {
        UserEntity user = BuildUser();

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        ForgotPasswordRequest request = new(TestConstants.ValidEmail);

        ErrorOr<Success> result = await _handler.Handle(request);

        result.IsError.Should().BeFalse();

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<UserEntity>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _emailServiceMock.Verify(
            e => e.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UserNotFound_ReturnsSuccess_NoOp()
    {
        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity?)null);

        ForgotPasswordRequest request = new(TestConstants.ValidEmail);

        ErrorOr<Success> result = await _handler.Handle(request);

        result.IsError.Should().BeFalse();

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<UserEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _emailServiceMock.Verify(
            e => e.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SuspendedUser_ReturnsSuccess_NoOp()
    {
        UserEntity user = BuildUser(UserStatus.Suspended);

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        ForgotPasswordRequest request = new(TestConstants.ValidEmail);

        ErrorOr<Success> result = await _handler.Handle(request);

        result.IsError.Should().BeFalse();

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<UserEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PendingUser_ReturnsSuccess_NoOp()
    {
        UserEntity user = BuildUser(UserStatus.PendingVerification);

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        ForgotPasswordRequest request = new(TestConstants.ValidEmail);

        ErrorOr<Success> result = await _handler.Handle(request);

        result.IsError.Should().BeFalse();

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<UserEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EmailSendFailure_StillReturnsSuccess()
    {
        UserEntity user = BuildUser();

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _emailServiceMock
            .Setup(e => e.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SES unavailable"));

        ForgotPasswordRequest request = new(TestConstants.ValidEmail);

        ErrorOr<Success> result = await _handler.Handle(request);

        result.IsError.Should().BeFalse();

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<UserEntity>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
