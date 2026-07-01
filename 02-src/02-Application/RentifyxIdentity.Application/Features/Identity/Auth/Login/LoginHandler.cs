using ErrorOr;
using FluentValidation;
using Microsoft.Extensions.Logging;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Extensions;
using RentifyxIdentity.Application.Features.Identity.Auth.Login.Request;
using RentifyxIdentity.Application.Features.Identity.Mapper;
using RentifyxIdentity.Domain.Constants;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Domain.Interfaces.Users;

namespace RentifyxIdentity.Application.Features.Identity.Auth.Login;

public sealed class LoginHandler(
    IUserRepository repository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IAuditLogService auditLogService,
    IValidator<LoginRequest> validator,
    ILogger<LoginHandler> logger) : IHandler<LoginRequest, LoginResponse>
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    public async Task<ErrorOr<LoginResponse>> Handle(
        LoginRequest request,
        CancellationToken ct = default)
    {
        logger.LogInformation("Login attempt. Email={Email}", request.Email);

        List<Error>? errors = await validator.ValidateToErrorsAsync(request, ct);
        if (errors is not null)
            return errors;

        UserEntity? user = await repository.GetByEmailAsync(request.Email, ct);
        if (user is null)
            return Error.Validation(UserErrorCodes.InvalidCredentials, "Invalid email or password.");

        if (user.IsLockedOut(DateTimeOffset.UtcNow))
            return Error.Custom(
                429,
                UserErrorCodes.LoginLocked,
                "Account is temporarily locked due to too many failed login attempts.");

        if (user.Status is UserStatus.PendingVerification)
            return Error.Validation(UserErrorCodes.AccountNotVerified, "Email address has not been verified yet.");

        if (user.Status is UserStatus.Suspended or UserStatus.Deleted)
            return Error.Conflict(UserErrorCodes.AccountNotVerifiable, "This account cannot be accessed.");

        if (!passwordHasher.Verify(request.Password, user.PasswordHash.HashValue))
        {
            user.RecordFailedLogin(DateTimeOffset.UtcNow);
            await repository.UpdateAsync(user, ct);
            return Error.Validation(UserErrorCodes.InvalidCredentials, "Invalid email or password.");
        }

        user.ClearLockout();

        string accessToken = tokenService.GenerateAccessToken(
            user.Id,
            user.Email.ToString(),
            user.Role.ToString());

        string rawRefreshToken = tokenService.GenerateRefreshToken();
        string refreshTokenHash = tokenService.HashToken(rawRefreshToken);
        user.SetRefreshToken(refreshTokenHash, DateTimeOffset.UtcNow.Add(RefreshTokenLifetime));

        await repository.UpdateAsync(user, ct);

        logger.LogInformation("Login successful. UserId={UserId}", user.Id);

        try
        {
            await auditLogService.LogAsync(user.Id, AuditEvents.UserLoggedIn, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Audit log failed for UserId={UserId}", user.Id);
        }

        return new LoginResponse(
            accessToken,
            rawRefreshToken,
            UserMapper.ToResponse(user));
    }
}
