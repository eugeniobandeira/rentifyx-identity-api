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
        CancellationToken ct = default)
    {
        logger.LogInformation("Exporting data. UserId={UserId}", request.UserId);

        List<Error>? errors = await validator.ValidateToErrorsAsync(request, ct);
        if (errors is not null)
            return errors;

        UserEntity? user = await repository.GetByIdAsync(request.UserId, ct);

        if (user is null || user.Status is UserStatus.Deleted)
            return Error.NotFound(UserErrorCodes.NotFound, "User not found.");

        logger.LogInformation("Data export prepared. UserId={UserId}", user.Id);

        IReadOnlyList<Domain.Contracts.AuditLogEntryRecord> auditHistory = [];
        try
        {
            auditHistory = await auditLogService.GetByUserIdAsync(user.Id, ct);
            await auditLogService.LogAsync(user.Id, AuditEvents.DataExported, ct);
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
            user.CreatedAt,
            user.ConsentGivenAt,
            user.EssentialConsentRevokedAt,
            user.IsMarketingConsentGranted,
            user.MarketingConsentGivenAt,
            user.MarketingConsentRevokedAt,
            auditHistory
        );
    }
}
