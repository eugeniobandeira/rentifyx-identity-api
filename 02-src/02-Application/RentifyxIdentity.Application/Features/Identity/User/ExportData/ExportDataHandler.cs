using ErrorOr;
using FluentValidation;
using Microsoft.Extensions.Logging;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Extensions;
using RentifyxIdentity.Application.Features.Identity.User.ExportData.Request;
using RentifyxIdentity.Domain.Constants;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Domain.Interfaces.Users;

namespace RentifyxIdentity.Application.Features.Identity.User.ExportData;

public sealed class ExportDataHandler(
    IUserRepository repository,
    IAuditLogService auditLogService,
    IValidator<ExportDataRequest> validator,
    ILogger<ExportDataHandler> logger) : IHandler<ExportDataRequest, UserDataExportResponse>
{
    public async Task<ErrorOr<UserDataExportResponse>> Handle(
        ExportDataRequest request,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Exporting data. UserId={UserId}", request.UserId);

        List<Error>? errors = await validator.ValidateToErrorsAsync(request, cancellationToken);
        if (errors is not null)
            return errors;

        UserEntity? user = await repository.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null || user.Status is UserStatus.Deleted)
            return Error.NotFound(UserErrorCodes.NotFound, "User not found.");

        logger.LogInformation("Data export prepared. UserId={UserId}", user.Id);

        try
        {
            await auditLogService.LogAsync(user.Id, AuditEvents.DataExported, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Audit log failed for UserId={UserId}", user.Id);
        }

        return new UserDataExportResponse(
            user.Id,
            user.Email.ToString(),
            user.TaxId.ToString(),
            user.Role.ToString(),
            user.Status.ToString(),
            user.CreatedAt
        );
    }
}
