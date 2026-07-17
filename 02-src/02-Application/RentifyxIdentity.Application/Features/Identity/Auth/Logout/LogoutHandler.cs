using ErrorOr;
using FluentValidation;
using Microsoft.Extensions.Logging;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Extensions;
using RentifyxIdentity.Application.Features.Identity.Auth.Logout.Request;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Interfaces.Users;

namespace RentifyxIdentity.Application.Features.Identity.Auth.Logout;

public sealed class LogoutHandler(
    IUserRepository repository,
    ITokenService tokenService,
    IValidator<LogoutRequest> validator,
    ILogger<LogoutHandler> logger) : IHandler<LogoutRequest, Success>
{
    public async Task<ErrorOr<Success>> HandleAsync(
        LogoutRequest request,
        CancellationToken ct = default)
    {
        logger.LogInformation("Logout attempt. Email={Email}", request.Email);

        List<Error>? errors = await validator.ValidateToErrorsAsync(request, ct);
        if (errors is not null)
            return errors;

        UserEntity? user = await repository.GetByEmailAsync(request.Email, ct);
        if (user is null)
        {
            logger.LogInformation("Logout no-op: user not found. Email={Email}", request.Email);
            return Result.Success;
        }

        if (user.RefreshTokenHash is null)
        {
            logger.LogInformation("Logout no-op: already logged out. UserId={UserId}", user.Id);
            return Result.Success;
        }

        if (!tokenService.VerifyTokenHash(request.RefreshToken, user.RefreshTokenHash))
        {
            logger.LogInformation("Logout no-op: token mismatch. UserId={UserId}", user.Id);
            return Result.Success;
        }

        user.ClearRefreshToken();
        await repository.UpdateAsync(user, ct);

        logger.LogInformation("Logout successful. UserId={UserId}", user.Id);

        return Result.Success;
    }
}
