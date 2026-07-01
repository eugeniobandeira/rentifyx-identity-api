using ErrorOr;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Moq;
using RentifyxIdentity.Application.Features.Identity.Auth.Login;
using RentifyxIdentity.Application.Features.Identity.Auth.Login.Request;
using RentifyxIdentity.Domain.Constants;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Domain.Interfaces.Users;
using RentifyxIdentity.Domain.ValueObjects;
using RentifyxIdentity.Tests.Common.Constants;
using Xunit;

namespace RentifyxIdentity.Tests.Handlers.Features.Identity;

public sealed class LoginHandlerTests
{
    private readonly Mock<IUserRepository> _repositoryMock = new();
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<IAuditLogService> _auditLogServiceMock = new();
    private readonly Mock<IValidator<LoginRequest>> _validatorMock = new();
    private readonly Mock<ILogger<LoginHandler>> _loggerMock = new();
    private readonly LoginHandler _handler;

    public LoginHandlerTests()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(
                It.IsAny<LoginRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _tokenServiceMock
            .Setup(t => t.GenerateAccessToken(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns("stub-access-token");

        _tokenServiceMock
            .Setup(t => t.GenerateRefreshToken())
            .Returns("stub-raw-refresh-token");

        _tokenServiceMock
            .Setup(t => t.HashToken(It.IsAny<string>()))
            .Returns("stub-refresh-token-hash");

        _auditLogServiceMock
            .Setup(a => a.LogAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new LoginHandler(
            _repositoryMock.Object,
            _passwordHasherMock.Object,
            _tokenServiceMock.Object,
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

    private static UserEntity BuildLockedUser()
    {
        UserEntity user = BuildUser();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        for (int i = 0; i < 5; i++)
            user.RecordFailedLogin(now);
        return user;
    }

    private static UserEntity BuildExpiredLockoutUser()
    {
        UserEntity user = BuildUser();
        DateTimeOffset pastNow = DateTimeOffset.UtcNow.AddMinutes(-16);
        for (int i = 0; i < 5; i++)
            user.RecordFailedLogin(pastNow);
        return user;
    }

    [Fact]
    public async Task HappyPath_ActiveUser_CorrectPassword_ReturnsLoginResponse()
    {
        UserEntity user = BuildUser();

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(p => p.Verify(TestConstants.ValidPassword, user.PasswordHash.HashValue))
            .Returns(true);

        LoginRequest request = new(TestConstants.ValidEmail, TestConstants.ValidPassword);

        ErrorOr<LoginResponse> result = await _handler.Handle(request);

        result.IsError.Should().BeFalse();
        result.Value.AccessToken.Should().NotBeNullOrEmpty();
        result.Value.RefreshToken.Should().NotBeNullOrEmpty();
        result.Value.User.Status.Should().Be(nameof(UserStatus.Active));

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<UserEntity>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EmailNotFound_ReturnsInvalidCredentials()
    {
        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity?)null);

        LoginRequest request = new(TestConstants.ValidEmail, TestConstants.ValidPassword);

        ErrorOr<LoginResponse> result = await _handler.Handle(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
        result.FirstError.Code.Should().Be(UserErrorCodes.InvalidCredentials);

        _passwordHasherMock.Verify(
            p => p.Verify(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task WrongPassword_ReturnsInvalidCredentials()
    {
        UserEntity user = BuildUser();

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(p => p.Verify(It.IsAny<string>(), user.PasswordHash.HashValue))
            .Returns(false);

        LoginRequest request = new(TestConstants.ValidEmail, "wrong-password");

        ErrorOr<LoginResponse> result = await _handler.Handle(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
        result.FirstError.Code.Should().Be(UserErrorCodes.InvalidCredentials);
    }

    [Fact]
    public async Task PendingVerification_ReturnsAccountNotVerified()
    {
        UserEntity user = BuildUser(UserStatus.PendingVerification);

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        LoginRequest request = new(TestConstants.ValidEmail, TestConstants.ValidPassword);

        ErrorOr<LoginResponse> result = await _handler.Handle(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
        result.FirstError.Code.Should().Be(UserErrorCodes.AccountNotVerified);
    }

    [Fact]
    public async Task SuspendedUser_ReturnsAccountNotVerifiable()
    {
        UserEntity user = BuildUser(UserStatus.Suspended);

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        LoginRequest request = new(TestConstants.ValidEmail, TestConstants.ValidPassword);

        ErrorOr<LoginResponse> result = await _handler.Handle(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Code.Should().Be(UserErrorCodes.AccountNotVerifiable);
    }

    [Fact]
    public async Task DeletedUser_ReturnsAccountNotVerifiable()
    {
        UserEntity user = BuildUser(UserStatus.Deleted);

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        LoginRequest request = new(TestConstants.ValidEmail, TestConstants.ValidPassword);

        ErrorOr<LoginResponse> result = await _handler.Handle(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Code.Should().Be(UserErrorCodes.AccountNotVerifiable);
    }

    [Fact]
    public async Task ValidationFailure_ReturnsErrors_AndDoesNotCallRepository()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(
                It.IsAny<LoginRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[]
            {
                new ValidationFailure("Email", "Required")
                {
                    ErrorCode = TestConstants.ValidationErrorCodeNotEmpty
                }
            }));

        LoginRequest request = new(string.Empty, TestConstants.ValidPassword);

        ErrorOr<LoginResponse> result = await _handler.Handle(request);

        result.IsError.Should().BeTrue();

        _repositoryMock.Verify(
            r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SuccessfulLogin_WritesUserLoggedInAuditEntry()
    {
        UserEntity user = BuildUser();

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(p => p.Verify(TestConstants.ValidPassword, user.PasswordHash.HashValue))
            .Returns(true);

        LoginRequest request = new(TestConstants.ValidEmail, TestConstants.ValidPassword);

        await _handler.Handle(request);

        _auditLogServiceMock.Verify(
            a => a.LogAsync(user.Id, AuditEvents.UserLoggedIn, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LockedAccount_Returns429LoginLocked_WithoutVerifyingPassword()
    {
        UserEntity user = BuildLockedUser();

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        LoginRequest request = new(TestConstants.ValidEmail, TestConstants.ValidPassword);

        ErrorOr<LoginResponse> result = await _handler.Handle(request);

        result.IsError.Should().BeTrue();
        result.FirstError.NumericType.Should().Be(429);
        result.FirstError.Code.Should().Be(UserErrorCodes.LoginLocked);

        _passwordHasherMock.Verify(
            p => p.Verify(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task WrongPassword_IncrementsFailedLoginAttempts_AndPersists()
    {
        UserEntity user = BuildUser();
        int initialAttempts = user.FailedLoginAttempts;

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(p => p.Verify(It.IsAny<string>(), user.PasswordHash.HashValue))
            .Returns(false);

        LoginRequest request = new(TestConstants.ValidEmail, "wrong-password");

        await _handler.Handle(request);

        user.FailedLoginAttempts.Should().Be(initialAttempts + 1);

        _repositoryMock.Verify(
            r => r.UpdateAsync(user, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FifthWrongPassword_TriggersLockout()
    {
        UserEntity user = BuildUser();
        for (int i = 0; i < 4; i++)
            user.RecordFailedLogin(DateTimeOffset.UtcNow);

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(p => p.Verify(It.IsAny<string>(), user.PasswordHash.HashValue))
            .Returns(false);

        LoginRequest request = new(TestConstants.ValidEmail, "wrong-password");

        ErrorOr<LoginResponse> result = await _handler.Handle(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(UserErrorCodes.InvalidCredentials);
        user.LockoutUntil.Should().NotBeNull();
        user.FailedLoginAttempts.Should().Be(5);
    }

    [Fact]
    public async Task CorrectPassword_ClearsLockoutCounter()
    {
        UserEntity user = BuildUser();
        for (int i = 0; i < 3; i++)
            user.RecordFailedLogin(DateTimeOffset.UtcNow);

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(p => p.Verify(TestConstants.ValidPassword, user.PasswordHash.HashValue))
            .Returns(true);

        LoginRequest request = new(TestConstants.ValidEmail, TestConstants.ValidPassword);

        ErrorOr<LoginResponse> result = await _handler.Handle(request);

        result.IsError.Should().BeFalse();
        user.FailedLoginAttempts.Should().Be(0);
        user.LockoutUntil.Should().BeNull();
    }

    [Fact]
    public async Task UnknownEmail_DoesNotIncrementCounter_AndDoesNotCallUpdate()
    {
        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity?)null);

        LoginRequest request = new(TestConstants.ValidEmail, TestConstants.ValidPassword);

        ErrorOr<LoginResponse> result = await _handler.Handle(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(UserErrorCodes.InvalidCredentials);

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<UserEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExpiredLockout_ProceedsNormally_WhenPasswordCorrect()
    {
        UserEntity user = BuildExpiredLockoutUser();

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(p => p.Verify(TestConstants.ValidPassword, user.PasswordHash.HashValue))
            .Returns(true);

        LoginRequest request = new(TestConstants.ValidEmail, TestConstants.ValidPassword);

        ErrorOr<LoginResponse> result = await _handler.Handle(request);

        result.IsError.Should().BeFalse();
        result.Value.AccessToken.Should().NotBeNullOrEmpty();
    }
}
