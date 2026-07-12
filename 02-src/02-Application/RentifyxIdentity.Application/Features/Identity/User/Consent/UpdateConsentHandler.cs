using ErrorOr;
using FluentValidation;
using Microsoft.Extensions.Logging;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Extensions;
using RentifyxIdentity.Application.Features.Identity.User.Consent.Mapper;
using RentifyxIdentity.Application.Features.Identity.User.Consent.Request;
using RentifyxIdentity.Domain.Constants;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Domain.Interfaces.Users;

namespace RentifyxIdentity.Application.Features.Identity.User.Consent;

public sealed class UpdateConsentHandler(
    IUserRepository repository,
    IAuditLogService auditLogService,
    IValidator<UpdateConsentRequest> validator,
    ILogger<UpdateConsentHandler> logger) : IHandler<UpdateConsentRequest, ConsentResponse>
{
    public async Task<ErrorOr<ConsentResponse>> Handle(
        UpdateConsentRequest request,
        CancellationToken ct = default)
    {
        logger.LogInformation(
            "Updating consent. UserId={UserId} Purpose={Purpose} Granted={Granted}",
            request.UserId,
            request.Purpose,
            request.Granted);

        List<Error>? errors = await validator.ValidateToErrorsAsync(request, ct);
        if (errors is not null)
            return errors;

        UserEntity? user = await repository.GetByIdAsync(request.UserId, ct);

        if (user is null || user.Status is UserStatus.Deleted)
            return Error.NotFound(UserErrorCodes.NotFound, "User not found.");

        DateTimeOffset now = DateTimeOffset.UtcNow;
        string auditEvent = ApplyConsentChange(user, request.Purpose, request.Granted, now);

        await repository.UpdateAsync(user, ct);

        try
        {
            await auditLogService.LogAsync(user.Id, auditEvent, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Audit log failed for UserId={UserId}", user.Id);
        }

        return ConsentMapper.ToResponse(user);
    }

    private static string ApplyConsentChange(
        UserEntity user,
        string purpose,
        bool granted,
        DateTimeOffset now)
    {
        if (purpose == nameof(ConsentPurpose.Essential))
        {
            if (granted)
            {
                user.GrantEssentialConsent(now);
                return AuditEvents.EssentialConsentGranted;
            }

            user.RevokeEssentialConsent(now);
            return AuditEvents.EssentialConsentRevoked;
        }

        if (granted)
        {
            user.GrantMarketingConsent(now);
            return AuditEvents.MarketingConsentGranted;
        }

        user.RevokeMarketingConsent(now);
        return AuditEvents.MarketingConsentRevoked;
    }
}
