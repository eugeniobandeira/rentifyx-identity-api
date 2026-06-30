using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RentifyxIdentity.Domain.Interfaces.Users;

namespace RentifyxIdentity.Infrastructure.Services;

public sealed class EmailService : IEmailService
{
    private readonly IAmazonSimpleEmailServiceV2 _sesClient;
    private readonly string _fromAddress;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IAmazonSimpleEmailServiceV2 sesClient,
        IConfiguration configuration,
        ILogger<EmailService> logger)
    {
        _sesClient = sesClient;
        _fromAddress = configuration["Ses:FromAddress"]
            ?? throw new InvalidOperationException("Ses:FromAddress is not configured.");
        _logger = logger;
    }

    public async Task SendVerificationEmailAsync(
        string recipient,
        string token,
        CancellationToken ct = default)
    {
        SendEmailRequest request = BuildRequest(
            recipient,
            subject: "Confirm your email — RentifyX",
            htmlBody: $"<p>Your verification token: <strong>{token}</strong></p>");

        SendEmailResponse response = await _sesClient.SendEmailAsync(request, ct);

        _logger.LogInformation(
            "Verification email sent to {Recipient}. MessageId: {MessageId}",
            recipient,
            response.MessageId);
    }

    public async Task SendPasswordResetEmailAsync(
        string recipient,
        string token,
        CancellationToken ct = default)
    {
        SendEmailRequest request = BuildRequest(
            recipient,
            subject: "Password reset — RentifyX",
            htmlBody: $"<p>Your password reset token: <strong>{token}</strong></p>");

        SendEmailResponse response = await _sesClient.SendEmailAsync(request, ct);

        _logger.LogInformation(
            "Password reset email sent to {Recipient}. MessageId: {MessageId}",
            recipient,
            response.MessageId);
    }

    private SendEmailRequest BuildRequest(
        string recipient,
        string subject,
        string htmlBody)
    {
        return new SendEmailRequest
        {
            FromEmailAddress = _fromAddress,
            Destination = new Destination
            {
                ToAddresses = new List<string> { recipient }
            },
            Content = new EmailContent
            {
                Simple = new Message
                {
                    Subject = new Content { Data = subject },
                    Body = new Body
                    {
                        Html = new Content { Data = htmlBody }
                    }
                }
            }
        };
    }
}
