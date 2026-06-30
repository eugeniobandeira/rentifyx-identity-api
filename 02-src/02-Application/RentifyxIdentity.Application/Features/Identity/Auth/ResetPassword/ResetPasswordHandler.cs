using ErrorOr;
using FluentValidation;
using Microsoft.Extensions.Logging;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Extensions;
using RentifyxIdentity.Application.Features.Identity.Auth.ResetPassword.Request;
using RentifyxIdentity.Domain.Constants;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Domain.Events;
using RentifyxIdentity.Domain.Interfaces.Users;
using RentifyxIdentity.Domain.ValueObjects;

namespace RentifyxIdentity.Application.Features.Identity.Auth.ResetPassword;

public sealed class ResetPasswordHandler(
    IUserRepository repository,
    ITokenService tokenService,
    IValidator<ResetPasswordRequest> validator,
    ILogger<ResetPasswordHandler> logger) : IHandler<ResetPasswordRequest, Success>
{
    public async Task<ErrorOr<Success>> Handle(
        ResetPasswordRequest request,
        CancellationToken ct = default)
    {
        logger.LogInformation("Password reset confirmation. Email={Email}", request.Email);

        List<Error>? errors = await validator.ValidateToErrorsAsync(request, ct);
        if (errors is not null)
            return errors;

        UserEntity? user = await repository.GetByEmailAsync(request.Email, ct);
        if (user is null)
            return Error.Validation(UserErrorCodes.TokenInvalidOrExpired, "The reset token is invalid or has expired.");

        if (user.Status is UserStatus.Suspended or UserStatus.Deleted)
            return Error.Conflict(UserErrorCodes.AccountNotVerifiable, "This account cannot be accessed.");

        if (user.PasswordResetTokenHash is null || user.PasswordResetTokenExpiry < DateTimeOffset.UtcNow)
            return Error.Validation(UserErrorCodes.TokenInvalidOrExpired, "The reset token is invalid or has expired.");

        if (!tokenService.VerifyTokenHash(request.Token, user.PasswordResetTokenHash))
            return Error.Validation(UserErrorCodes.TokenInvalidOrExpired, "The reset token is invalid or has expired.");

        user.ResetPassword(Password.FromPlaintext(request.NewPassword));
        await repository.UpdateAsync(user, ct);

        UserPasswordChanged domainEvent = new(user.Id, DateTimeOffset.UtcNow);
        logger.LogInformation("Domain event: {Event}", domainEvent);

        logger.LogInformation("Password reset successful. UserId={UserId}", user.Id);

        return Result.Success;
    }
}
