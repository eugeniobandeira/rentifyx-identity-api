using ErrorOr;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Moq;
using RentifyxIdentity.Application.Features.Identity.Auth.Login;
using RentifyxIdentity.Application.Features.Identity.Auth.RefreshToken;
using RentifyxIdentity.Application.Features.Identity.Auth.RefreshToken.Request;
using RentifyxIdentity.Domain.Constants;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Domain.Interfaces.Users;
using RentifyxIdentity.Domain.ValueObjects;
using RentifyxIdentity.Tests.Common.Constants;
using Xunit;

namespace RentifyxIdentity.Tests.Handlers.Features.Identity;

public sealed class RefreshTokenHandlerTests
{
    private readonly Mock<IUserRepository> _repositoryMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<IValidator<RefreshTokenRequest>> _validatorMock = new();
    private readonly Mock<ILogger<RefreshTokenHandler>> _loggerMock = new();
    private readonly RefreshTokenHandler _handler;

    public RefreshTokenHandlerTests()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(
                It.IsAny<RefreshTokenRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _tokenServiceMock
            .Setup(t => t.GenerateAccessToken(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns("new-stub-access-token");

        _tokenServiceMock
            .Setup(t => t.GenerateRefreshToken())
            .Returns("new-stub-raw-refresh-token");

        _tokenServiceMock
            .Setup(t => t.HashToken(It.IsAny<string>()))
            .Returns("new-stub-refresh-token-hash");

        _tokenServiceMock
            .Setup(t => t.VerifyTokenHash(TestConstants.RawRefreshToken, "stored-hash"))
            .Returns(true);

        _handler = new RefreshTokenHandler(
            _repositoryMock.Object,
            _tokenServiceMock.Object,
            _validatorMock.Object,
            _loggerMock.Object);
    }

    private static UserEntity BuildActiveUserWithToken(
        string hash = "stored-hash",
        DateTimeOffset? expiry = null)
    {
        UserEntity user = UserEntity.Create(
            Email.Create(TestConstants.ValidEmail),
            TaxDocument.Create(TestConstants.TaxIdCpfRaw),
            Password.FromPlaintext(TestConstants.ValidPassword),
            UserRole.Owner);

        user.VerifyEmail();
        user.SetRefreshToken(hash, expiry ?? DateTimeOffset.UtcNow.AddDays(30));

        return user;
    }

    [Fact]
    public async Task HappyPath_ValidToken_ReturnsNewTokens()
    {
        UserEntity user = BuildActiveUserWithToken();

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        RefreshTokenRequest request = new(TestConstants.ValidEmail, TestConstants.RawRefreshToken);

        ErrorOr<LoginResponse> result = await _handler.HandleAsync(request);

        result.IsError.Should().BeFalse();
        result.Value.AccessToken.Should().NotBeNullOrEmpty();
        result.Value.RefreshToken.Should().NotBeNullOrEmpty();
        result.Value.User.Status.Should().Be(nameof(UserStatus.Active));

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<UserEntity>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EmailNotFound_ReturnsTokenInvalidOrExpired()
    {
        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity?)null);

        RefreshTokenRequest request = new(TestConstants.ValidEmail, TestConstants.RawRefreshToken);

        ErrorOr<LoginResponse> result = await _handler.HandleAsync(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
        result.FirstError.Code.Should().Be(UserErrorCodes.TokenInvalidOrExpired);
    }

    [Fact]
    public async Task WrongToken_ReturnsTokenInvalidOrExpired()
    {
        UserEntity user = BuildActiveUserWithToken();

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _tokenServiceMock
            .Setup(t => t.VerifyTokenHash("wrong-token", "stored-hash"))
            .Returns(false);

        RefreshTokenRequest request = new(TestConstants.ValidEmail, "wrong-token");

        ErrorOr<LoginResponse> result = await _handler.HandleAsync(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
        result.FirstError.Code.Should().Be(UserErrorCodes.TokenInvalidOrExpired);
    }

    [Fact]
    public async Task ExpiredToken_ReturnsTokenInvalidOrExpired()
    {
        UserEntity user = BuildActiveUserWithToken(expiry: DateTimeOffset.UtcNow.AddHours(-1));

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        RefreshTokenRequest request = new(TestConstants.ValidEmail, TestConstants.RawRefreshToken);

        ErrorOr<LoginResponse> result = await _handler.HandleAsync(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
        result.FirstError.Code.Should().Be(UserErrorCodes.TokenInvalidOrExpired);
    }

    [Fact]
    public async Task NullRefreshTokenHash_ReturnsTokenInvalidOrExpired()
    {
        UserEntity user = UserEntity.Create(
            Email.Create(TestConstants.ValidEmail),
            TaxDocument.Create(TestConstants.TaxIdCpfRaw),
            Password.FromPlaintext(TestConstants.ValidPassword),
            UserRole.Owner);

        user.VerifyEmail();

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        RefreshTokenRequest request = new(TestConstants.ValidEmail, TestConstants.RawRefreshToken);

        ErrorOr<LoginResponse> result = await _handler.HandleAsync(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
        result.FirstError.Code.Should().Be(UserErrorCodes.TokenInvalidOrExpired);
    }

    [Fact]
    public async Task SuspendedUser_ReturnsAccountNotVerifiable()
    {
        UserEntity user = UserEntity.Create(
            Email.Create(TestConstants.ValidEmail),
            TaxDocument.Create(TestConstants.TaxIdCpfRaw),
            Password.FromPlaintext(TestConstants.ValidPassword),
            UserRole.Owner);

        user.Suspend();

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        RefreshTokenRequest request = new(TestConstants.ValidEmail, TestConstants.RawRefreshToken);

        ErrorOr<LoginResponse> result = await _handler.HandleAsync(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Code.Should().Be(UserErrorCodes.AccountNotVerifiable);
    }

    [Fact]
    public async Task DeletedUser_ReturnsAccountNotVerifiable()
    {
        UserEntity user = UserEntity.Create(
            Email.Create(TestConstants.ValidEmail),
            TaxDocument.Create(TestConstants.TaxIdCpfRaw),
            Password.FromPlaintext(TestConstants.ValidPassword),
            UserRole.Owner);

        user.Anonymize();

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        RefreshTokenRequest request = new(TestConstants.ValidEmail, TestConstants.RawRefreshToken);

        ErrorOr<LoginResponse> result = await _handler.HandleAsync(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Code.Should().Be(UserErrorCodes.AccountNotVerifiable);
    }

    [Fact]
    public async Task PendingVerification_ReturnsAccountNotVerified()
    {
        UserEntity user = UserEntity.Create(
            Email.Create(TestConstants.ValidEmail),
            TaxDocument.Create(TestConstants.TaxIdCpfRaw),
            Password.FromPlaintext(TestConstants.ValidPassword),
            UserRole.Owner);

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        RefreshTokenRequest request = new(TestConstants.ValidEmail, TestConstants.RawRefreshToken);

        ErrorOr<LoginResponse> result = await _handler.HandleAsync(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
        result.FirstError.Code.Should().Be(UserErrorCodes.AccountNotVerified);
    }

    [Fact]
    public async Task ValidationFailure_ReturnsErrors_AndDoesNotCallRepository()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(
                It.IsAny<RefreshTokenRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[]
            {
                new ValidationFailure("Email", "Required")
                {
                    ErrorCode = TestConstants.ValidationErrorCodeNotEmpty
                }
            }));

        RefreshTokenRequest request = new(string.Empty, TestConstants.RawRefreshToken);

        ErrorOr<LoginResponse> result = await _handler.HandleAsync(request);

        result.IsError.Should().BeTrue();

        _repositoryMock.Verify(
            r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
