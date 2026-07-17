using ErrorOr;
using FluentValidation;
using Microsoft.Extensions.Logging;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Extensions;
using RentifyxIdentity.Application.Features.Identity.Auth.Register.Request;
using RentifyxIdentity.Application.Features.Identity.Mapper;
using RentifyxIdentity.Domain.Constants;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Domain.Events;
using RentifyxIdentity.Domain.Interfaces.Users;
using RentifyxIdentity.Domain.ValueObjects;

namespace RentifyxIdentity.Application.Features.Identity.Auth.Register;

public sealed class RegisterUserHandler(
    IUserRepository repository,
    ITokenService tokenService,
    IValidator<RegisterUserRequest> validator,
    ILogger<RegisterUserHandler> logger) : IHandler<RegisterUserRequest, UserResponse>
{
    public async Task<ErrorOr<UserResponse>> Handle(
        RegisterUserRequest request,
        CancellationToken ct = default)
    {
        logger.LogInformation("Registering user. Email={Email}", request.Email);

        List<Error>? errors = await validator.ValidateToErrorsAsync(request, ct);
        if (errors is not null)
            return errors;

        UserEntity? existing = await repository.GetByEmailAsync(request.Email, ct);
        if (existing is not null)
            return Error.Conflict(UserErrorCodes.EmailAlreadyRegistered, "This email address is already registered.");

        UserEntity? existingTaxId = await repository.GetByTaxIdAsync(request.TaxId, ct);
        if (existingTaxId is not null)
            return Error.Conflict(UserErrorCodes.TaxIdAlreadyRegistered, "This Tax ID is already registered.");

        UserEntity user = UserEntity.Create(
            Email.Create(request.Email),
            TaxDocument.Create(request.TaxId),
            Password.FromPlaintext(request.Password),
            Enum.Parse<UserRole>(request.Role));

        user.SetConsent(DateTimeOffset.UtcNow);

        string rawToken = Guid.NewGuid().ToString();
        string tokenHash = tokenService.HashToken(rawToken);
        user.SetEmailVerificationToken(tokenHash, DateTimeOffset.UtcNow.AddHours(TokenPolicyConstants.EmailVerificationHours));

        UserRegistered domainEvent = new(user.Id, user.Email.ToString(), user.Role, rawToken, DateTimeOffset.UtcNow);
        await repository.AddAsync(user, [domainEvent], ct);

        logger.LogInformation("User registered successfully. UserId={UserId}", user.Id);

        return UserMapper.ToResponse(user);
    }
}
