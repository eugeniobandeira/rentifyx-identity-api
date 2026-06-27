namespace RentifyxIdentity.Domain.Interfaces.Users;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string recipient, string token, CancellationToken ct = default);
    Task SendPasswordResetEmailAsync(string recipient, string token, CancellationToken ct = default);
}
