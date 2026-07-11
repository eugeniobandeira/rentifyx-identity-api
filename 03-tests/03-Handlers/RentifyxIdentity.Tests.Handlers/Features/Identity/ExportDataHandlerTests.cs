using ErrorOr;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Moq;
using RentifyxIdentity.Application.Features.Identity.User.ExportData;
using RentifyxIdentity.Application.Features.Identity.User.ExportData.Request;
using RentifyxIdentity.Domain.Constants;
using RentifyxIdentity.Domain.Contracts;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Domain.Interfaces.Users;
using RentifyxIdentity.Domain.ValueObjects;
using RentifyxIdentity.Tests.Common.Constants;
using Xunit;

namespace RentifyxIdentity.Tests.Handlers.Features.Identity;

public sealed class ExportDataHandlerTests
{
    private readonly Mock<IUserRepository> _repositoryMock = new();
    private readonly Mock<IAuditLogService> _auditLogServiceMock = new();
    private readonly Mock<IValidator<ExportDataRequest>> _validatorMock = new();
    private readonly Mock<ILogger<ExportDataHandler>> _loggerMock = new();
    private readonly ExportDataHandler _handler;

    public ExportDataHandlerTests()
    {
        _auditLogServiceMock
            .Setup(a => a.LogAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _auditLogServiceMock
            .Setup(a => a.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<AuditLogEntryRecord>)[]);

        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<ExportDataRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _handler = new ExportDataHandler(
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
    public async Task HappyPath_ActiveUser_ReturnsExport_AndAudits()
    {
        UserEntity user = BuildUser();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        ErrorOr<UserDataExportResponse> result = await _handler.Handle(new ExportDataRequest(user.Id));

        result.IsError.Should().BeFalse();
        result.Value.Email.Should().Be(TestConstants.ValidEmail);
        result.Value.TaxId.Should().Be("***.***.***-**");
        result.Value.Role.Should().Be("Owner");
        result.Value.Status.Should().Be(TestConstants.StatusActive);

        _auditLogServiceMock.Verify(
            a => a.LogAsync(user.Id, AuditEvents.DataExported, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SuspendedUser_ReturnsExport_AndAudits()
    {
        UserEntity user = BuildUser(UserStatus.Suspended);
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        ErrorOr<UserDataExportResponse> result = await _handler.Handle(new ExportDataRequest(user.Id));

        result.IsError.Should().BeFalse();
        result.Value.Status.Should().Be("Suspended");

        _auditLogServiceMock.Verify(
            a => a.LogAsync(user.Id, AuditEvents.DataExported, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UserNotFound_ReturnsNotFound_AndDoesNotAudit()
    {
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity?)null);

        ErrorOr<UserDataExportResponse> result = await _handler.Handle(new ExportDataRequest(Guid.NewGuid()));

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

        ErrorOr<UserDataExportResponse> result = await _handler.Handle(new ExportDataRequest(user.Id));

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

        _auditLogServiceMock
            .Setup(a => a.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DynamoDB unavailable"));

        ErrorOr<UserDataExportResponse> result = await _handler.Handle(new ExportDataRequest(user.Id));

        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task HappyPath_IncludesAuditHistoryAndConsentDate()
    {
        UserEntity user = BuildUser();
        user.SetConsent(DateTimeOffset.UtcNow);

        IReadOnlyList<AuditLogEntryRecord> stubHistory =
        [
            new AuditLogEntryRecord(AuditEvents.ProfileAccessed, DateTimeOffset.UtcNow.AddMinutes(-5)),
            new AuditLogEntryRecord(AuditEvents.UserLoggedIn, DateTimeOffset.UtcNow.AddHours(-1))
        ];

        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _auditLogServiceMock
            .Setup(a => a.GetByUserIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stubHistory);

        ErrorOr<UserDataExportResponse> result = await _handler.Handle(new ExportDataRequest(user.Id));

        result.IsError.Should().BeFalse();
        result.Value.ConsentGivenAt.Should().NotBeNull();
        result.Value.AuditHistory.Should().HaveCount(2);
        result.Value.AuditHistory[0].EventType.Should().Be(AuditEvents.ProfileAccessed);
    }

    [Fact]
    public async Task HappyPath_MarketingConsentGranted_ReturnsMarketingConsentFields()
    {
        UserEntity user = BuildUser();
        user.GrantMarketingConsent(DateTimeOffset.UtcNow);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        ErrorOr<UserDataExportResponse> result = await _handler.Handle(new ExportDataRequest(user.Id));

        result.IsError.Should().BeFalse();
        result.Value.MarketingConsentGranted.Should().BeTrue();
        result.Value.MarketingConsentGivenAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidationFailure_ReturnsErrors_AndDoesNotCallRepository()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<ExportDataRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[]
            {
                new ValidationFailure("UserId", "Required")
                {
                    ErrorCode = TestConstants.ValidationErrorCodeNotEmpty
                }
            }));

        ErrorOr<UserDataExportResponse> result = await _handler.Handle(new ExportDataRequest(Guid.Empty));

        result.IsError.Should().BeTrue();

        _repositoryMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
