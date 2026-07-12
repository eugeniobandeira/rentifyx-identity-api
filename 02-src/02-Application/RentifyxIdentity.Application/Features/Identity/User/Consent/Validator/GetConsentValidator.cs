using FluentValidation;
using RentifyxIdentity.Application.Features.Identity.User.Consent.Request;

namespace RentifyxIdentity.Application.Features.Identity.User.Consent.Validator;

public sealed class GetConsentValidator : AbstractValidator<GetConsentRequest>
{
    public GetConsentValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();
    }
}
