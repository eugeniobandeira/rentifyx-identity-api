using ErrorOr;
using FluentValidation;
using Microsoft.Extensions.Logging;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Extensions;
using RentifyxIdentity.Application.Features.Identity.Mapper;
using RentifyxIdentity.Application.Features.Identity.User.GetProfile.Request;
using RentifyxIdentity.Domain.Constants;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Domain.Interfaces.Users;

namespace RentifyxIdentity.Application.Features.Identity.User.GetProfile;

public sealed class GetProfileHandler(
    IUserRepository repository,
    IValidator<GetProfileRequest> validator,
    ILogger<GetProfileHandler> logger) : IHandler<GetProfileRequest, UserResponse>
{
    public async Task<ErrorOr<UserResponse>> Handle(
        GetProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Getting profile. UserId={UserId}", request.UserId);

        List<Error>? errors = await validator.ValidateToErrorsAsync(request, cancellationToken);
        if (errors is not null)
            return errors;

        UserEntity? user = await repository.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null || user.Status is UserStatus.Deleted)
            return Error.NotFound(UserErrorCodes.NotFound, "User not found.");

        logger.LogInformation("Profile retrieved. UserId={UserId}", user.Id);

        return UserMapper.ToResponse(user);
    }
}
