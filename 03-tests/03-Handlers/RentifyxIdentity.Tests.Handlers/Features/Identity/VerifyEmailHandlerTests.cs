using ErrorOr;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Moq;
using RentifyxIdentity.Application.Features.Identity;
using RentifyxIdentity.Application.Features.Identity.Auth.VerifyEmail;
using RentifyxIdentity.Application.Features.Identity.Auth.VerifyEmail.Request;
using RentifyxIdentity.Domain.Constants;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Domain.Interfaces.Users;
using RentifyxIdentity.Domain.ValueObjects;
using RentifyxIdentity.Tests.Common.Constants;
using Xunit;

namespace RentifyxIdentity.Tests.Handlers.Features.Identity;

public sealed class VerifyEmailHandlerTests
{
    private const string RawToken = "test-verification-token-abc123";
    private const string StoredHash = "stored-email-verification-hash";

    private readonly Mock<IUserRepository> _repositoryMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<IValidator<VerifyEmailRequest>> _validatorMock = new();
    private readonly Mock<ILogger<VerifyEmailHandler>> _loggerMock = new();
    private readonly VerifyEmailHandler _handler;

    public VerifyEmailHandlerTests()
    {
        _tokenServiceMock
            .Setup(t => t.VerifyTokenHash(RawToken, It.IsAny<string>()))
            .Returns(true);

        _tokenServiceMock
            .Setup(t => t.VerifyTokenHash(It.Is<string>(s => s != RawToken), It.IsAny<string>()))
            .Returns(false);

        _validatorMock
            .Setup(v => v.ValidateAsync(
                It.IsAny<VerifyEmailRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _handler = new VerifyEmailHandler(
            _repositoryMock.Object,
            _tokenServiceMock.Object,
            _validatorMock.Object,
            _loggerMock.Object);
    }

    private static UserEntity BuildUser(UserStatus status = UserStatus.PendingVerification)
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
    public async Task HappyPath_ValidToken_ReturnsActiveUserResponse()
    {
        UserEntity user = BuildUser();
        user.SetEmailVerificationToken(StoredHash, DateTimeOffset.UtcNow.AddHours(24));

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        VerifyEmailRequest request = new(TestConstants.ValidEmail, RawToken);

        ErrorOr<UserResponse> result = await _handler.Handle(request);

        result.IsError.Should().BeFalse();
        result.Value.Status.Should().Be(nameof(UserStatus.Active));

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<UserEntity>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExpiredToken_ReturnsValidationError()
    {
        UserEntity user = BuildUser();
        user.SetEmailVerificationToken(StoredHash, DateTimeOffset.UtcNow.AddHours(-1));

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        VerifyEmailRequest request = new(TestConstants.ValidEmail, RawToken);

        ErrorOr<UserResponse> result = await _handler.Handle(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
        result.FirstError.Code.Should().Be(UserErrorCodes.TokenInvalidOrExpired);
    }

    [Fact]
    public async Task WrongToken_ReturnsValidationError()
    {
        UserEntity user = BuildUser();
        user.SetEmailVerificationToken(StoredHash, DateTimeOffset.UtcNow.AddHours(24));

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        VerifyEmailRequest request = new(TestConstants.ValidEmail, "wrong-token");

        ErrorOr<UserResponse> result = await _handler.Handle(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
        result.FirstError.Code.Should().Be(UserErrorCodes.TokenInvalidOrExpired);
    }

    [Fact]
    public async Task UserNotFound_ReturnsValidationError()
    {
        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity?)null);

        VerifyEmailRequest request = new(TestConstants.ValidEmail, RawToken);

        ErrorOr<UserResponse> result = await _handler.Handle(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
        result.FirstError.Code.Should().Be(UserErrorCodes.TokenInvalidOrExpired);
    }

    [Fact]
    public async Task AlreadyActive_ReturnsSuccessIdempotent()
    {
        UserEntity user = BuildUser(UserStatus.Active);

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        VerifyEmailRequest request = new(TestConstants.ValidEmail, RawToken);

        ErrorOr<UserResponse> result = await _handler.Handle(request);

        result.IsError.Should().BeFalse();
        result.Value.Status.Should().Be(nameof(UserStatus.Active));

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<UserEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SuspendedUser_ReturnsConflictError()
    {
        UserEntity user = BuildUser(UserStatus.Suspended);

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        VerifyEmailRequest request = new(TestConstants.ValidEmail, RawToken);

        ErrorOr<UserResponse> result = await _handler.Handle(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Code.Should().Be(UserErrorCodes.AccountNotVerifiable);
    }

    [Fact]
    public async Task DeletedUser_ReturnsConflictError()
    {
        UserEntity user = BuildUser();
        user.Anonymize();

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        VerifyEmailRequest request = new(TestConstants.ValidEmail, RawToken);

        ErrorOr<UserResponse> result = await _handler.Handle(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Code.Should().Be(UserErrorCodes.AccountNotVerifiable);
    }

    [Fact]
    public async Task ValidationFailure_ReturnsErrors_AndDoesNotCallRepository()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(
                It.IsAny<VerifyEmailRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[]
            {
                new ValidationFailure("Email", "Required")
                {
                    ErrorCode = TestConstants.ValidationErrorCodeNotEmpty
                }
            }));

        VerifyEmailRequest request = new(string.Empty, RawToken);

        ErrorOr<UserResponse> result = await _handler.Handle(request);

        result.IsError.Should().BeTrue();

        _repositoryMock.Verify(
            r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
