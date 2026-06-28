using FluentValidation;
using RentifyxIdentity.Application.Features.Identity.User.GetProfile.Request;

namespace RentifyxIdentity.Application.Features.Identity.User.GetProfile.Validator;

public sealed class GetProfileValidator : AbstractValidator<GetProfileRequest>
{
    public GetProfileValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();
    }
}
