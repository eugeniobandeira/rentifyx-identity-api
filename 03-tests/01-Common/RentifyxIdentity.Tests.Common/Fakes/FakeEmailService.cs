using RentifyxIdentity.Domain.Interfaces.Users;

namespace RentifyxIdentity.Tests.Common.Fakes;

public sealed class FakeEmailService : IEmailService
{
    public List<(string Recipient, string Token)> SentVerificationEmails { get; } = new();
    public List<(string Recipient, string Token)> SentPasswordResetEmails { get; } = new();

    public Task SendVerificationEmailAsync(string recipient, string token, CancellationToken ct = default)
    {
        SentVerificationEmails.Add((recipient, token));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetEmailAsync(string recipient, string token, CancellationToken ct = default)
    {
        SentPasswordResetEmails.Add((recipient, token));
        return Task.CompletedTask;
    }
}
