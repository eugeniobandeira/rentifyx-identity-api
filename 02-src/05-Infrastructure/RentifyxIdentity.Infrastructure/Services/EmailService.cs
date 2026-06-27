using RentifyxIdentity.Domain.Interfaces.Users;

namespace RentifyxIdentity.Infrastructure.Services;

public sealed class EmailService : IEmailService
{
    public Task SendVerificationEmailAsync(string recipient, string token, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task SendPasswordResetEmailAsync(string recipient, string token, CancellationToken ct = default)
        => throw new NotImplementedException();
}
