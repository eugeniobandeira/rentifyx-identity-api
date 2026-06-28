using System.Security.Cryptography;
using System.Text;
using ErrorOr;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RentifyxIdentity.Application.Features.Identity.Auth.ResetPassword;
using RentifyxIdentity.Application.Features.Identity.Auth.ResetPassword.Request;
using RentifyxIdentity.Domain.Constants;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Domain.Interfaces.Users;
using RentifyxIdentity.Domain.ValueObjects;
using RentifyxIdentity.Tests.Common.Constants;
using Xunit;

namespace RentifyxIdentity.Tests.Handlers.Features.Identity;

public sealed class ResetPasswordHandlerTests
{
    private const string RawToken = "test-password-reset-token-xyz";

    private readonly Mock<IUserRepository> _repositoryMock = new();
    private readonly Mock<IValidator<ResetPasswordRequest>> _validatorMock = new();
    private readonly Mock<IConfiguration> _configurationMock = new();
    private readonly Mock<ILogger<ResetPasswordHandler>> _loggerMock = new();
    private readonly ResetPasswordHandler _handler;
    private readonly string _tokenHash;

    public ResetPasswordHandlerTests()
    {
        _configurationMock
            .Setup(c => c[TestConstants.HmacKeyConfigPath])
            .Returns(TestConstants.HmacKey);

        _validatorMock
            .Setup(v => v.ValidateAsync(
                It.IsAny<ResetPasswordRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _handler = new ResetPasswordHandler(
            _repositoryMock.Object,
            _validatorMock.Object,
            _configurationMock.Object,
            _loggerMock.Object);

        using HMACSHA256 hmac = new(Encoding.UTF8.GetBytes(TestConstants.HmacKey));
        _tokenHash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(RawToken)));
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
    public async Task HappyPath_ValidToken_ResetsPassword()
    {
        UserEntity user = BuildUser();
        user.SetPasswordResetToken(_tokenHash, DateTimeOffset.UtcNow.AddHours(1));

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        ResetPasswordRequest request = new(TestConstants.ValidEmail, RawToken, "NewP@ssword123!");

        ErrorOr<Success> result = await _handler.Handle(request);

        result.IsError.Should().BeFalse();

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<UserEntity>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UserNotFound_ReturnsTokenInvalidOrExpired()
    {
        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity?)null);

        ResetPasswordRequest request = new(TestConstants.ValidEmail, RawToken, "NewP@ssword123!");

        ErrorOr<Success> result = await _handler.Handle(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
        result.FirstError.Code.Should().Be(UserErrorCodes.TokenInvalidOrExpired);
    }

    [Fact]
    public async Task WrongToken_ReturnsTokenInvalidOrExpired()
    {
        UserEntity user = BuildUser();
        user.SetPasswordResetToken(_tokenHash, DateTimeOffset.UtcNow.AddHours(1));

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        ResetPasswordRequest request = new(TestConstants.ValidEmail, "wrong-token", "NewP@ssword123!");

        ErrorOr<Success> result = await _handler.Handle(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
        result.FirstError.Code.Should().Be(UserErrorCodes.TokenInvalidOrExpired);
    }

    [Fact]
    public async Task ExpiredToken_ReturnsTokenInvalidOrExpired()
    {
        UserEntity user = BuildUser();
        user.SetPasswordResetToken(_tokenHash, DateTimeOffset.UtcNow.AddHours(-1));

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        ResetPasswordRequest request = new(TestConstants.ValidEmail, RawToken, "NewP@ssword123!");

        ErrorOr<Success> result = await _handler.Handle(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
        result.FirstError.Code.Should().Be(UserErrorCodes.TokenInvalidOrExpired);
    }

    [Fact]
    public async Task NullTokenHash_ReturnsTokenInvalidOrExpired()
    {
        UserEntity user = BuildUser();
        // PasswordResetTokenHash remains null

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        ResetPasswordRequest request = new(TestConstants.ValidEmail, RawToken, "NewP@ssword123!");

        ErrorOr<Success> result = await _handler.Handle(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
        result.FirstError.Code.Should().Be(UserErrorCodes.TokenInvalidOrExpired);
    }

    [Fact]
    public async Task SuspendedUser_ReturnsAccountNotVerifiable()
    {
        UserEntity user = BuildUser(UserStatus.Suspended);

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        ResetPasswordRequest request = new(TestConstants.ValidEmail, RawToken, "NewP@ssword123!");

        ErrorOr<Success> result = await _handler.Handle(request);

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

        ResetPasswordRequest request = new(TestConstants.ValidEmail, RawToken, "NewP@ssword123!");

        ErrorOr<Success> result = await _handler.Handle(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Code.Should().Be(UserErrorCodes.AccountNotVerifiable);
    }

    [Fact]
    public async Task ValidationFailure_ReturnsErrors_AndDoesNotCallRepository()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(
                It.IsAny<ResetPasswordRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[]
            {
                new ValidationFailure("NewPassword", "Required")
                {
                    ErrorCode = TestConstants.ValidationErrorCodeNotEmpty
                }
            }));

        ResetPasswordRequest request = new(TestConstants.ValidEmail, RawToken, string.Empty);

        ErrorOr<Success> result = await _handler.Handle(request);

        result.IsError.Should().BeTrue();

        _repositoryMock.Verify(
            r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
