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

        _handler = new LoginHandler(
            _repositoryMock.Object,
            _passwordHasherMock.Object,
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
        else if (status is UserStatus.Deleted)
            user.Anonymize();

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
}
