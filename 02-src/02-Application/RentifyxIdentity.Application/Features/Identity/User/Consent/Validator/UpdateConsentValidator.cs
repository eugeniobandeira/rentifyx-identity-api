using FluentValidation;
using RentifyxIdentity.Application.Features.Identity.User.Consent.Request;
using RentifyxIdentity.Domain.MessageResource;

namespace RentifyxIdentity.Application.Features.Identity.User.Consent.Validator;

public sealed class UpdateConsentValidator : AbstractValidator<UpdateConsentRequest>
{
    public UpdateConsentValidator()
    {
        RuleFor(x => x.Purpose)
            .NotEmpty()
                .WithMessage(ValidationMessageResource.CONSENT_PURPOSE_REQUIRED);

        RuleFor(x => x.Purpose)
            .Must(purpose => purpose is "Essential" or "Marketing")
                .WithMessage(ValidationMessageResource.CONSENT_PURPOSE_INVALID)
            .When(x => !string.IsNullOrEmpty(x.Purpose));
    }
}
