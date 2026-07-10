using System.Globalization;
using Amazon.DynamoDBv2.DataModel;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RentifyxIdentity.Infrastructure.Models;
using RentifyxIdentity.Infrastructure.Services;
using Xunit;

namespace RentifyxIdentity.Tests.Handlers.Features.Identity;

public sealed class AuditLogServiceTests
{
    private readonly Mock<IDynamoDBContext> _contextMock = new();
    private readonly Mock<IConfiguration> _configurationMock = new();
    private readonly Mock<ILogger<AuditLogService>> _loggerMock = new();
    private readonly AuditLogService _service;

    public AuditLogServiceTests()
    {
        _configurationMock
            .Setup(c => c["AWS:DynamoDB:TableName"])
            .Returns("test-table");

        _contextMock
            .Setup(c => c.SaveAsync(
                It.IsAny<AuditLogEntry>(),
                It.IsAny<SaveConfig>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _service = new AuditLogService(
            _contextMock.Object,
            _configurationMock.Object,
            _loggerMock.Object);
    }

    private async Task<AuditLogEntry> CaptureEntryAsync(Guid? userId = null, string eventType = "TEST_EVENT")
    {
        AuditLogEntry? captured = null;

        _contextMock
            .Setup(c => c.SaveAsync(
                It.IsAny<AuditLogEntry>(),
                It.IsAny<SaveConfig>(),
                It.IsAny<CancellationToken>()))
            .Callback<AuditLogEntry, SaveConfig, CancellationToken>(
                (entry, _, _) => captured = entry)
            .Returns(Task.CompletedTask);

        await _service.LogAsync(userId ?? Guid.NewGuid(), eventType);

        return captured!;
    }

    [Fact]
    public async Task LogAsync_Pk_IsAuditPrefixWithUserId_And_Sk_ContainsTimestamp()
    {
        Guid userId = Guid.NewGuid();

        AuditLogEntry entry = await CaptureEntryAsync(userId);

        entry.Pk.Should().Be($"AUDIT#{userId}");
        entry.Sk.Should().MatchRegex(@"^\d{14}_[0-9a-f\-]{36}$");
    }

    [Fact]
    public async Task LogAsync_EventTypeMatchesInput()
    {
        const string eventType = "PROFILE_ACCESSED";

        AuditLogEntry entry = await CaptureEntryAsync(eventType: eventType);

        entry.EventType.Should().Be(eventType);
    }

    [Fact]
    public async Task LogAsync_OccurredAt_IsParseableIso8601Utc()
    {
        AuditLogEntry entry = await CaptureEntryAsync();

        bool parsed = DateTimeOffset.TryParse(
            entry.OccurredAt,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out DateTimeOffset result);

        parsed.Should().BeTrue();
        result.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task LogAsync_Ttl_IsApproximately90DaysFromNow()
    {
        long ttlMin = DateTimeOffset.UtcNow.AddDays(90).ToUnixTimeSeconds() - 5;

        AuditLogEntry entry = await CaptureEntryAsync();

        long ttlMax = DateTimeOffset.UtcNow.AddDays(90).ToUnixTimeSeconds() + 5;

        entry.Ttl.Should().BeInRange(ttlMin, ttlMax);
    }

    [Fact]
    public async Task LogAsync_CallsSaveAsyncExactlyOnce()
    {
        await _service.LogAsync(Guid.NewGuid(), "TEST_EVENT");

        _contextMock.Verify(
            c => c.SaveAsync(
                It.IsAny<AuditLogEntry>(),
                It.IsAny<SaveConfig>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
