using ErrorOr;
using FluentValidation;
using Microsoft.Extensions.Logging;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Extensions;
using RentifyxIdentity.Application.Features.Identity.Auth.Login;
using RentifyxIdentity.Application.Features.Identity.Auth.RefreshToken.Request;
using RentifyxIdentity.Application.Features.Identity.Mapper;
using RentifyxIdentity.Domain.Constants;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Domain.Interfaces.Users;

namespace RentifyxIdentity.Application.Features.Identity.Auth.RefreshToken;

public sealed class RefreshTokenHandler(
    IUserRepository repository,
    ITokenService tokenService,
    IValidator<RefreshTokenRequest> validator,
    ILogger<RefreshTokenHandler> logger) : IHandler<RefreshTokenRequest, LoginResponse>
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(TokenPolicyConstants.RefreshTokenDays);

    public async Task<ErrorOr<LoginResponse>> Handle(
        RefreshTokenRequest request,
        CancellationToken ct = default)
    {
        logger.LogInformation("Token refresh attempt. Email={Email}", request.Email);

        List<Error>? errors = await validator.ValidateToErrorsAsync(request, ct);
        if (errors is not null)
            return errors;

        UserEntity? user = await repository.GetByEmailAsync(request.Email, ct);
        if (user is null)
            return Error.Validation(UserErrorCodes.TokenInvalidOrExpired, "The refresh token is invalid or has expired.");

        if (user.Status is UserStatus.Suspended or UserStatus.Deleted)
            return Error.Conflict(UserErrorCodes.AccountNotVerifiable, "This account cannot be accessed.");

        if (user.Status is UserStatus.PendingVerification)
            return Error.Validation(UserErrorCodes.AccountNotVerified, "Email address has not been verified yet.");

        if (user.RefreshTokenHash is null || user.RefreshTokenExpiry < DateTimeOffset.UtcNow)
            return Error.Validation(UserErrorCodes.TokenInvalidOrExpired, "The refresh token is invalid or has expired.");

        if (!tokenService.VerifyTokenHash(request.RefreshToken, user.RefreshTokenHash))
            return Error.Validation(UserErrorCodes.TokenInvalidOrExpired, "The refresh token is invalid or has expired.");

        string accessToken = tokenService.GenerateAccessToken(
            user.Id,
            user.Email.ToString(),
            user.Role.ToString());

        string rawRefreshToken = tokenService.GenerateRefreshToken();
        string refreshTokenHash = tokenService.HashToken(rawRefreshToken);
        user.SetRefreshToken(refreshTokenHash, DateTimeOffset.UtcNow.Add(RefreshTokenLifetime));

        await repository.UpdateAsync(user, ct);

        logger.LogInformation("Token refreshed successfully. UserId={UserId}", user.Id);

        return new LoginResponse(
            accessToken,
            rawRefreshToken,
            UserMapper.ToResponse(user));
    }
}
