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
using RentifyxIdentity.Domain.Events;
using RentifyxIdentity.Domain.Interfaces.Users;

namespace RentifyxIdentity.Application.Features.Identity.Auth.Login;

public sealed class LoginHandler(
    IUserRepository repository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IValidator<LoginRequest> validator,
    ILogger<LoginHandler> logger) : IHandler<LoginRequest, LoginResponse>
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    public async Task<ErrorOr<LoginResponse>> Handle(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Login attempt. Email={Email}", request.Email);

        List<Error>? errors = await validator.ValidateToErrorsAsync(request, cancellationToken);
        if (errors is not null)
            return errors;

        UserEntity? user = await repository.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null)
            return Error.Validation(UserErrorCodes.InvalidCredentials, "Invalid email or password.");

        if (user.Status is UserStatus.PendingVerification)
            return Error.Validation(UserErrorCodes.AccountNotVerified, "Email address has not been verified yet.");

        if (user.Status is UserStatus.Suspended or UserStatus.Deleted)
            return Error.Conflict(UserErrorCodes.AccountNotVerifiable, "This account cannot be accessed.");

        if (!passwordHasher.Verify(request.Password, user.PasswordHash.HashValue))
            return Error.Validation(UserErrorCodes.InvalidCredentials, "Invalid email or password.");

        string accessToken = tokenService.GenerateAccessToken(
            user.Id,
            user.Email.ToString(),
            user.Role.ToString());

        string rawRefreshToken = tokenService.GenerateRefreshToken();
        string refreshTokenHash = tokenService.HashToken(rawRefreshToken);
        user.SetRefreshToken(refreshTokenHash, DateTimeOffset.UtcNow.Add(RefreshTokenLifetime));

        await repository.UpdateAsync(user, cancellationToken);

        UserLoggedIn domainEvent = new(user.Id, user.Email.ToString(), DateTimeOffset.UtcNow);
        logger.LogInformation("Domain event: {Event}", domainEvent);

        logger.LogInformation("Login successful. UserId={UserId}", user.Id);

        return new LoginResponse(
            accessToken,
            rawRefreshToken,
            UserMapper.ToResponse(user));
    }
}
