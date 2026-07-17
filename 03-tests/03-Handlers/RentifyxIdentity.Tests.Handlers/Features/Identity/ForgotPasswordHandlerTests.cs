using ErrorOr;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Moq;
using RentifyxIdentity.Application.Features.Identity.Auth.ForgotPassword;
using RentifyxIdentity.Application.Features.Identity.Auth.ForgotPassword.Request;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Domain.Events;
using RentifyxIdentity.Domain.Interfaces.Users;
using RentifyxIdentity.Domain.ValueObjects;
using RentifyxIdentity.Tests.Common.Constants;
using Xunit;

namespace RentifyxIdentity.Tests.Handlers.Features.Identity;

public sealed class ForgotPasswordHandlerTests
{
    private readonly Mock<IUserRepository> _repositoryMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<IValidator<ForgotPasswordRequest>> _validatorMock = new();
    private readonly Mock<ILogger<ForgotPasswordHandler>> _loggerMock = new();
    private readonly ForgotPasswordHandler _handler;

    public ForgotPasswordHandlerTests()
    {
        _tokenServiceMock
            .Setup(t => t.HashToken(It.IsAny<string>()))
            .Returns("hashed-token");

        _validatorMock
            .Setup(v => v.ValidateAsync(
                It.IsAny<ForgotPasswordRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _handler = new ForgotPasswordHandler(
            _repositoryMock.Object,
            _tokenServiceMock.Object,
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

        return user;
    }

    [Fact]
    public async Task HappyPath_ActiveUser_StoresTokenAndRaisesPasswordResetRequestedEvent()
    {
        UserEntity user = BuildUser();

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        IReadOnlyCollection<IDomainEvent>? capturedEvents = null;
        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<UserEntity>(), It.IsAny<IReadOnlyCollection<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<UserEntity, IReadOnlyCollection<IDomainEvent>, CancellationToken>((_, events, _) => capturedEvents = events);

        ForgotPasswordRequest request = new(TestConstants.ValidEmail);

        ErrorOr<Success> result = await _handler.HandleAsync(request);

        result.IsError.Should().BeFalse();

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<UserEntity>(), It.IsAny<IReadOnlyCollection<IDomainEvent>>(), It.IsAny<CancellationToken>()),
            Times.Once);

        capturedEvents.Should().ContainSingle().Which.Should().BeOfType<PasswordResetRequested>();
        PasswordResetRequested domainEvent = (PasswordResetRequested)capturedEvents!.Single();
        domainEvent.UserId.Should().Be(user.Id);
        domainEvent.Email.Should().Be(user.Email.ToString());
        domainEvent.RawToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task UserNotFound_ReturnsSuccess_NoOp()
    {
        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity?)null);

        ForgotPasswordRequest request = new(TestConstants.ValidEmail);

        ErrorOr<Success> result = await _handler.HandleAsync(request);

        result.IsError.Should().BeFalse();

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<UserEntity>(), It.IsAny<IReadOnlyCollection<IDomainEvent>>(), It.IsAny<CancellationToken>()),
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

        ErrorOr<Success> result = await _handler.HandleAsync(request);

        result.IsError.Should().BeFalse();

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<UserEntity>(), It.IsAny<IReadOnlyCollection<IDomainEvent>>(), It.IsAny<CancellationToken>()),
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

        ErrorOr<Success> result = await _handler.HandleAsync(request);

        result.IsError.Should().BeFalse();

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<UserEntity>(), It.IsAny<IReadOnlyCollection<IDomainEvent>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ActiveUser_SetsPasswordResetTokenExpiryInTheFuture()
    {
        UserEntity user = BuildUser();

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        UserEntity? capturedUser = null;
        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<UserEntity>(), It.IsAny<IReadOnlyCollection<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<UserEntity, IReadOnlyCollection<IDomainEvent>, CancellationToken>((u, _, _) => capturedUser = u);

        ForgotPasswordRequest request = new(TestConstants.ValidEmail);

        ErrorOr<Success> result = await _handler.HandleAsync(request);

        result.IsError.Should().BeFalse();
        capturedUser!.PasswordResetTokenExpiry.Should().NotBeNull();
        capturedUser.PasswordResetTokenExpiry.Should().BeAfter(DateTimeOffset.UtcNow);
    }
}
