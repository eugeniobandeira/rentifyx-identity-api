using ErrorOr;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Moq;
using RentifyxIdentity.Application.Features.Identity;
using RentifyxIdentity.Application.Features.Identity.Auth.Register;
using RentifyxIdentity.Application.Features.Identity.Auth.Register.Request;
using RentifyxIdentity.Domain.Constants;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Domain.Events;
using RentifyxIdentity.Domain.Interfaces.Users;
using RentifyxIdentity.Domain.ValueObjects;
using RentifyxIdentity.Tests.Common.Builders;
using RentifyxIdentity.Tests.Common.Constants;
using Xunit;

namespace RentifyxIdentity.Tests.Handlers.Features.Identity;

public sealed class RegisterUserHandlerTests
{
    private readonly Mock<IUserRepository> _repositoryMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<IValidator<RegisterUserRequest>> _validatorMock = new();
    private readonly Mock<ILogger<RegisterUserHandler>> _loggerMock = new();
    private readonly RegisterUserHandler _handler;

    public RegisterUserHandlerTests()
    {
        _tokenServiceMock
            .Setup(t => t.HashToken(It.IsAny<string>()))
            .Returns("test-token-hash");

        _validatorMock
            .Setup(v => v.ValidateAsync(
                It.IsAny<RegisterUserRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _handler = new RegisterUserHandler(
            _repositoryMock.Object,
            _tokenServiceMock.Object,
            _validatorMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task HappyPath_RegistersUser_ReturnsUserResponse_AndSetsConsentGivenAt()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity?)null);

        _repositoryMock
            .Setup(r => r.GetByTaxIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity?)null);

        UserEntity? capturedUser = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<UserEntity>(), It.IsAny<IReadOnlyCollection<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<UserEntity, IReadOnlyCollection<IDomainEvent>, CancellationToken>((u, _, _) => capturedUser = u);

        RegisterUserRequest request = new RegisterUserRequestBuilder().WithConsentGiven(true).Build();

        ErrorOr<UserResponse> result = await _handler.HandleAsync(request);

        result.IsError.Should().BeFalse();
        result.Value.Email.Should().Be(request.Email.ToLowerInvariant());
        result.Value.Role.Should().Be(request.Role);
        result.Value.Status.Should().Be(TestConstants.StatusPendingVerification);

        _repositoryMock.Verify(
            r => r.AddAsync(It.IsAny<UserEntity>(), It.IsAny<IReadOnlyCollection<IDomainEvent>>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _tokenServiceMock.Verify(t => t.HashToken(It.IsAny<string>()), Times.Once);

        capturedUser!.ConsentGivenAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DuplicateEmail_ReturnsConflictError_AndDoesNotCallAddAsync()
    {
        UserEntity stubUser = UserEntity.Create(
            Email.Create(TestConstants.ValidEmail),
            TaxDocument.Create(TestConstants.TaxIdCpfRaw),
            Password.FromPlaintext(TestConstants.ValidPassword),
            UserRole.Owner);

        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stubUser);

        RegisterUserRequest request = new RegisterUserRequestBuilder()
            .WithEmail(TestConstants.ValidEmail)
            .Build();

        ErrorOr<UserResponse> result = await _handler.HandleAsync(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Code.Should().Be(UserErrorCodes.EmailAlreadyRegistered);

        _repositoryMock.Verify(
            r => r.AddAsync(It.IsAny<UserEntity>(), It.IsAny<IReadOnlyCollection<IDomainEvent>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DuplicateTaxId_ReturnsConflictError_AndDoesNotCallAddAsync()
    {
        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity?)null);

        UserEntity stubUser = UserEntity.Create(
            Email.Create("other@example.com"),
            TaxDocument.Create(TestConstants.TaxIdCpfRaw),
            Password.FromPlaintext(TestConstants.ValidPassword),
            UserRole.Owner);

        _repositoryMock
            .Setup(r => r.GetByTaxIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stubUser);

        RegisterUserRequest request = new RegisterUserRequestBuilder()
            .WithTaxId(TestConstants.TaxIdCpfRaw)
            .Build();

        ErrorOr<UserResponse> result = await _handler.HandleAsync(request);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(UserErrorCodes.TaxIdAlreadyRegistered);

        _repositoryMock.Verify(
            r => r.AddAsync(It.IsAny<UserEntity>(), It.IsAny<IReadOnlyCollection<IDomainEvent>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ValidationFailure_ReturnsErrors_AndDoesNotCallRepository()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(
                It.IsAny<RegisterUserRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[]
            {
                new ValidationFailure("Email", "Required")
                {
                    ErrorCode = TestConstants.ValidationErrorCodeNotEmpty
                }
            }));

        RegisterUserRequest request = new RegisterUserRequestBuilder()
            .WithEmail(string.Empty)
            .Build();

        ErrorOr<UserResponse> result = await _handler.HandleAsync(request);

        result.IsError.Should().BeTrue();

        _repositoryMock.Verify(
            r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _repositoryMock.Verify(
            r => r.AddAsync(It.IsAny<UserEntity>(), It.IsAny<IReadOnlyCollection<IDomainEvent>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HappyPath_RaisesUserRegisteredEvent_WithMatchingUserIdEmailAndToken()
    {
        _repositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity?)null);

        _repositoryMock
            .Setup(r => r.GetByTaxIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity?)null);

        IReadOnlyCollection<IDomainEvent>? capturedEvents = null;
        UserEntity? capturedUser = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<UserEntity>(), It.IsAny<IReadOnlyCollection<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<UserEntity, IReadOnlyCollection<IDomainEvent>, CancellationToken>((u, events, _) =>
            {
                capturedUser = u;
                capturedEvents = events;
            });

        RegisterUserRequest request = new RegisterUserRequestBuilder().Build();

        ErrorOr<UserResponse> result = await _handler.HandleAsync(request);

        result.IsError.Should().BeFalse();
        capturedEvents.Should().ContainSingle().Which.Should().BeOfType<UserRegistered>();

        UserRegistered domainEvent = (UserRegistered)capturedEvents!.Single();
        domainEvent.UserId.Should().Be(capturedUser!.Id);
        domainEvent.Email.Should().Be(capturedUser.Email.ToString());
        domainEvent.RawToken.Should().NotBeNullOrEmpty();
    }
}
