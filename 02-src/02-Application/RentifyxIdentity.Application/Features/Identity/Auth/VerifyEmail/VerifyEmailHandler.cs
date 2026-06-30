using ErrorOr;
using FluentValidation;
using Microsoft.Extensions.Logging;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Extensions;
using RentifyxIdentity.Application.Features.Identity.Auth.VerifyEmail.Request;
using RentifyxIdentity.Application.Features.Identity.Mapper;
using RentifyxIdentity.Domain.Constants;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Domain.Events;
using RentifyxIdentity.Domain.Interfaces.Users;

namespace RentifyxIdentity.Application.Features.Identity.Auth.VerifyEmail;

public sealed class VerifyEmailHandler(
    IUserRepository repository,
    ITokenService tokenService,
    IValidator<VerifyEmailRequest> validator,
    ILogger<VerifyEmailHandler> logger) : IHandler<VerifyEmailRequest, UserResponse>
{
    public async Task<ErrorOr<UserResponse>> Handle(
        VerifyEmailRequest request,
        CancellationToken ct = default)
    {
        logger.LogInformation("Verifying email. Email={Email}", request.Email);

        List<Error>? errors = await validator.ValidateToErrorsAsync(request, ct);
        if (errors is not null)
            return errors;

        UserEntity? user = await repository.GetByEmailAsync(request.Email, ct);
        if (user is null)
            return Error.Validation(UserErrorCodes.TokenInvalidOrExpired, "The verification token is invalid or has expired.");

        if (user.Status is UserStatus.Suspended or UserStatus.Deleted)
            return Error.Conflict(UserErrorCodes.AccountNotVerifiable, "This account cannot be verified.");

        if (user.Status is UserStatus.Active)
        {
            logger.LogInformation("Email already verified for {Email}", request.Email);
            return UserMapper.ToResponse(user);
        }

        if (!tokenService.VerifyTokenHash(request.Token, user.EmailVerificationTokenHash!) || user.EmailVerificationTokenExpiry < DateTimeOffset.UtcNow)
            return Error.Validation(UserErrorCodes.TokenInvalidOrExpired, "The verification token is invalid or has expired.");

        user.VerifyEmail();
        await repository.UpdateAsync(user, ct);

        UserEmailVerified domainEvent = new(user.Id, user.Email.ToString(), DateTimeOffset.UtcNow);
        logger.LogInformation("Domain event: {Event}", domainEvent);

        logger.LogInformation("Email verified successfully. UserId={UserId}", user.Id);

        return UserMapper.ToResponse(user);
    }
}
