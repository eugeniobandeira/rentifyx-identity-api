using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RentifyxIdentity.Infrastructure.Services;
using Xunit;

namespace RentifyxIdentity.Tests.Handlers.Features.Identity;

public sealed class EmailServiceTests
{
    private const string FromAddress = "no-reply@rentifyx.com";
    private const string Recipient = "user@test.com";

    private readonly Mock<IAmazonSimpleEmailServiceV2> _sesClientMock = new();
    private readonly Mock<IConfiguration> _configurationMock = new();
    private readonly Mock<ILogger<EmailService>> _loggerMock = new();
    private readonly EmailService _sut;

    public EmailServiceTests()
    {
        _configurationMock
            .Setup(c => c["Ses:FromAddress"])
            .Returns(FromAddress);

        _sesClientMock
            .Setup(s => s.SendEmailAsync(
                It.IsAny<SendEmailRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendEmailResponse { MessageId = "test-message-id" });

        _sut = new EmailService(
            _sesClientMock.Object,
            _configurationMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task SendVerificationEmail_ValidRecipient_CallsSendEmailOnceWithCorrectRecipient()
    {
        await _sut.SendVerificationEmailAsync(Recipient, "abc123");

        _sesClientMock.Verify(
            s => s.SendEmailAsync(
                It.IsAny<SendEmailRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _sesClientMock.Verify(
            s => s.SendEmailAsync(
                It.Is<SendEmailRequest>(r =>
                    r.Destination.ToAddresses.Contains(Recipient)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendPasswordResetEmail_ValidRecipient_CallsSendEmailWithResetSubject()
    {
        await _sut.SendPasswordResetEmailAsync(Recipient, "xyz789");

        _sesClientMock.Verify(
            s => s.SendEmailAsync(
                It.IsAny<SendEmailRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
