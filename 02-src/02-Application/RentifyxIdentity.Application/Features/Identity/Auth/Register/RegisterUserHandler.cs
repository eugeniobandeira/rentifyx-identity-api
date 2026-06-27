using ErrorOr;
using FluentValidation;
using Microsoft.Extensions.Configuration;
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
    IEmailService emailService,
    IValidator<RegisterUserRequest> validator,
    IConfiguration configuration,
    ILogger<RegisterUserHandler> logger) : IHandler<RegisterUserRequest, UserResponse>
{
    public async Task<ErrorOr<UserResponse>> Handle(
        RegisterUserRequest request,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Registering user. Payload={@Payload}", request);

        List<Error>? errors = await validator.ValidateToErrorsAsync(request, cancellationToken);
        if (errors is not null)
            return errors;

        UserEntity? existing = await repository.GetByEmailAsync(request.Email, cancellationToken);
        if (existing is not null)
            return Error.Conflict(UserErrorCodes.EmailAlreadyRegistered, "This email address is already registered.");

        UserEntity? existingTaxId = await repository.GetByTaxIdAsync(request.TaxId, cancellationToken);
        if (existingTaxId is not null)
            return Error.Conflict(UserErrorCodes.TaxIdAlreadyRegistered, "This Tax ID is already registered.");

        UserEntity user = UserEntity.Create(
            Email.Create(request.Email),
            TaxDocument.Create(request.TaxId),
            Password.FromPlaintext(request.Password),
            Enum.Parse<UserRole>(request.Role));

        string rawToken = Guid.NewGuid().ToString();
        string hmacKey = configuration["Hmac:Key"] ?? "dev-hmac-key";
        using System.Security.Cryptography.HMACSHA256 hmac = new(System.Text.Encoding.UTF8.GetBytes(hmacKey));
        string tokenHash = Convert.ToBase64String(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawToken)));
        user.SetEmailVerificationToken(tokenHash, DateTimeOffset.UtcNow.AddHours(24));

        await repository.AddAsync(user, cancellationToken);

        try
        {
            await emailService.SendVerificationEmailAsync(user.Email.ToString(), rawToken, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Verification email failed for {Email}", user.Email);
        }

        UserRegistered domainEvent = new(user.Id, user.Email.ToString(), user.Role, DateTimeOffset.UtcNow);
        logger.LogInformation("Domain event: {Event}", domainEvent);

        logger.LogInformation("User registered successfully. Response={@Response}", user);

        return UserMapper.ToResponse(user);
    }
}
