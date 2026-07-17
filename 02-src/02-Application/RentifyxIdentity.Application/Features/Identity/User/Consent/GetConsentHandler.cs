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

public sealed class GetConsentHandler(
    IUserRepository repository,
    IValidator<GetConsentRequest> validator,
    ILogger<GetConsentHandler> logger) : IHandler<GetConsentRequest, ConsentResponse>
{
    public async Task<ErrorOr<ConsentResponse>> HandleAsync(
        GetConsentRequest request,
        CancellationToken ct = default)
    {
        logger.LogInformation("Getting consent state. UserId={UserId}", request.UserId);

        List<Error>? errors = await validator.ValidateToErrorsAsync(request, ct);
        if (errors is not null)
            return errors;

        UserEntity? user = await repository.GetByIdAsync(request.UserId, ct);

        if (user is null || user.Status is UserStatus.Deleted)
            return Error.NotFound(UserErrorCodes.NotFound, "User not found.");

        return ConsentMapper.ToResponse(user);
    }
}
