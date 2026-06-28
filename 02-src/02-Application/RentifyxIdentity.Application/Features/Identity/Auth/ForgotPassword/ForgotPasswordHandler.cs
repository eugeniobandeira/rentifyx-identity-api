using System.Security.Cryptography;
using System.Text;
using ErrorOr;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Extensions;
using RentifyxIdentity.Application.Features.Identity.Auth.ForgotPassword.Request;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Domain.Interfaces.Users;

namespace RentifyxIdentity.Application.Features.Identity.Auth.ForgotPassword;

public sealed class ForgotPasswordHandler(
    IUserRepository repository,
    IEmailService emailService,
    IValidator<ForgotPasswordRequest> validator,
    IConfiguration configuration,
    ILogger<ForgotPasswordHandler> logger) : IHandler<ForgotPasswordRequest, Success>
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);

    public async Task<ErrorOr<Success>> Handle(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Password reset requested. Email={Email}", request.Email);

        List<Error>? errors = await validator.ValidateToErrorsAsync(request, cancellationToken);
        if (errors is not null)
            return errors;

        UserEntity? user = await repository.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null)
        {
            logger.LogInformation("Password reset no-op: user not found. Email={Email}", request.Email);
            return Result.Success;
        }

        if (user.Status is not UserStatus.Active)
        {
            logger.LogInformation("Password reset no-op: account not active. UserId={UserId}", user.Id);
            return Result.Success;
        }

        string rawToken = Guid.NewGuid().ToString();
        string hmacKey = configuration["Hmac:Key"] ?? "dev-hmac-key";
        using HMACSHA256 hmac = new(Encoding.UTF8.GetBytes(hmacKey));
        string tokenHash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawToken)));

        user.SetPasswordResetToken(tokenHash, DateTimeOffset.UtcNow.Add(TokenLifetime));
        await repository.UpdateAsync(user, cancellationToken);

        try
        {
            await emailService.SendPasswordResetEmailAsync(user.Email.ToString(), rawToken, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Password reset email failed for {Email}", user.Email);
        }

        logger.LogInformation("Password reset email sent. UserId={UserId}", user.Id);

        return Result.Success;
    }
}
