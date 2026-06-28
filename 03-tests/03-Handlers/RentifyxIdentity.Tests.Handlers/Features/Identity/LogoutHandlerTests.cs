using ErrorOr;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Moq;
using RentifyxIdentity.Application.Features.Identity.Auth.Logout;
using RentifyxIdentity.Application.Features.Identity.Auth.Logout.Request;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Domain.Interfaces.Users;
using RentifyxIdentity.Domain.ValueObjects;
using RentifyxIdentity.Tests.Common.Constants;
using Xunit;

namespace RentifyxIdentity.Tests.Handlers.Features.Identity;

public sealed class LogoutHandlerTests
{
    private readonly Mock<IUserRepository> _repositoryMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<IValidator<LogoutRequest>> _validatorMock = new();
    private readonly Mock<ILogger<LogoutHandler>> _loggerMock = new();
    private readonly LogoutHandler _handler;

    public LogoutHandlerTests()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(
                It.IsAny<LogoutRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _tokenServiceMock
            .Setup(t => t.VerifyTokenHash(TestConstants.RawRefreshToken, "stored-hash"))
            .Returns(true);

        _handler = new LogoutHandler(
            _repositoryMock.Object,
            _tokenServiceMock.Object,
            _validatorMock.Object,
            _loggerMock.Object);
    }

    private static UserEntity BuildActiveUserWithToken(string hash = "stored-hash")
    {
        UserEntity user = UserEntity.Create(
            Email.Create(TestConstants.ValidEmail),
            TaxDocument.Create(TestConstants.TaxIdCpfRaw),
            Password.FromPlaintext(TestConstants.ValidPassword),
            UserRole.Owner);

        user.VerifyEmail();
        user.SetRefreshToken(hash, DateTimeOffset.UtcNow.AddDays(30));

        return user;
    }

    [Fact]
    public async Task HappyPath_MatchingToken_ReturnsSuccessAndClearsToken()
    {
        UserEntity user = BuildActiveUserWithToken();

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        LogoutRequest request = new(TestConstants.ValidEmail, TestConstants.RawRefreshToken);

        ErrorOr<Success> result = await _handler.Handle(request);

        result.IsError.Should().BeFalse();

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<UserEntity>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UserNotFound_ReturnsSuccess_NoUpdate()
    {
        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity?)null);

        LogoutRequest request = new(TestConstants.ValidEmail, TestConstants.RawRefreshToken);

        ErrorOr<Success> result = await _handler.Handle(request);

        result.IsError.Should().BeFalse();

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<UserEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task NullRefreshTokenHash_ReturnsSuccess_NoUpdate()
    {
        UserEntity user = UserEntity.Create(
            Email.Create(TestConstants.ValidEmail),
            TaxDocument.Create(TestConstants.TaxIdCpfRaw),
            Password.FromPlaintext(TestConstants.ValidPassword),
            UserRole.Owner);

        user.VerifyEmail();
        // RefreshTokenHash remains null

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        LogoutRequest request = new(TestConstants.ValidEmail, TestConstants.RawRefreshToken);

        ErrorOr<Success> result = await _handler.Handle(request);

        result.IsError.Should().BeFalse();

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<UserEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TokenHashMismatch_ReturnsSuccess_NoUpdate()
    {
        UserEntity user = BuildActiveUserWithToken();

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _tokenServiceMock
            .Setup(t => t.VerifyTokenHash("wrong-token", "stored-hash"))
            .Returns(false);

        LogoutRequest request = new(TestConstants.ValidEmail, "wrong-token");

        ErrorOr<Success> result = await _handler.Handle(request);

        result.IsError.Should().BeFalse();

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<UserEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ValidationFailure_ReturnsErrors_AndDoesNotCallRepository()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(
                It.IsAny<LogoutRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[]
            {
                new ValidationFailure("Email", "Required")
                {
                    ErrorCode = TestConstants.ValidationErrorCodeNotEmpty
                }
            }));

        LogoutRequest request = new(string.Empty, TestConstants.RawRefreshToken);

        ErrorOr<Success> result = await _handler.Handle(request);

        result.IsError.Should().BeTrue();

        _repositoryMock.Verify(
            r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
