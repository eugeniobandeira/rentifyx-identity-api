using ErrorOr;
using FluentValidation;
using Microsoft.Extensions.Logging;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Extensions;
using RentifyxIdentity.Application.Features.Identity.Auth.ForgotPassword.Request;
using RentifyxIdentity.Domain.Constants;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Domain.Events;
using RentifyxIdentity.Domain.Interfaces.Users;

namespace RentifyxIdentity.Application.Features.Identity.Auth.ForgotPassword;

public sealed class ForgotPasswordHandler(
    IUserRepository repository,
    ITokenService tokenService,
    IValidator<ForgotPasswordRequest> validator,
    ILogger<ForgotPasswordHandler> logger) : IHandler<ForgotPasswordRequest, Success>
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(TokenPolicyConstants.PasswordResetHours);

    public async Task<ErrorOr<Success>> HandleAsync(
        ForgotPasswordRequest request,
        CancellationToken ct = default)
    {
        logger.LogInformation("Password reset requested. Email={Email}", request.Email);

        List<Error>? errors = await validator.ValidateToErrorsAsync(request, ct);
        if (errors is not null)
            return errors;

        UserEntity? user = await repository.GetByEmailAsync(request.Email, ct);
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
        string tokenHash = tokenService.HashToken(rawToken);

        user.SetPasswordResetToken(tokenHash, DateTimeOffset.UtcNow.Add(TokenLifetime));

        PasswordResetRequested domainEvent = new(user.Id, user.Email.ToString(), rawToken, DateTimeOffset.UtcNow);
        await repository.UpdateAsync(user, [domainEvent], ct);

        logger.LogInformation("Password reset token issued. UserId={UserId}", user.Id);

        return Result.Success;
    }
}
