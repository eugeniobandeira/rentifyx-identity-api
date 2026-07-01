using ErrorOr;
using FluentValidation;
using Microsoft.Extensions.Logging;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Extensions;
using RentifyxIdentity.Application.Features.Identity.User.DeleteAccount.Request;
using RentifyxIdentity.Domain.Constants;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Domain.Events;
using RentifyxIdentity.Domain.Interfaces.Users;

namespace RentifyxIdentity.Application.Features.Identity.User.DeleteAccount;

public sealed class DeleteAccountHandler(
    IUserRepository repository,
    IAuditLogService auditLogService,
    IValidator<DeleteAccountRequest> validator,
    ILogger<DeleteAccountHandler> logger) : IHandler<DeleteAccountRequest, Success>
{
    public async Task<ErrorOr<Success>> Handle(
        DeleteAccountRequest request,
        CancellationToken ct = default)
    {
        logger.LogInformation("Deleting account. UserId={UserId}", request.UserId);

        List<Error>? errors = await validator.ValidateToErrorsAsync(request, ct);
        if (errors is not null)
            return errors;

        UserEntity? user = await repository.GetByIdAsync(request.UserId, ct);

        if (user is null)
            return Error.NotFound(UserErrorCodes.NotFound, "User not found.");

        if (user.Status is UserStatus.Deleted)
            return Error.Conflict(UserErrorCodes.AlreadyDeleted, "This account has already been deleted.");

        user.ClearRefreshToken();
        user.Anonymize();

        await repository.UpdateAsync(user, ct);

        UserAccountDeleted domainEvent = new(user.Id, DateTimeOffset.UtcNow);
        logger.LogInformation("Domain event: {Event}", domainEvent);

        logger.LogInformation("Account deleted. UserId={UserId}", request.UserId);

        try
        {
            await auditLogService.LogAsync(request.UserId, AuditEvents.AccountDeleted, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Audit log failed for UserId={UserId}", request.UserId);
        }

        return Result.Success;
    }
}
