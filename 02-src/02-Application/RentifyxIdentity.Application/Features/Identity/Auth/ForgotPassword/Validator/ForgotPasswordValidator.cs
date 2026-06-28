using FluentValidation;
using RentifyxIdentity.Application.Features.Identity.Auth.ForgotPassword.Request;
using RentifyxIdentity.Domain.MessageResource;

namespace RentifyxIdentity.Application.Features.Identity.Auth.ForgotPassword.Validator;

public sealed class ForgotPasswordValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
                .WithMessage(ValidationMessageResource.EMAIL_REQUIRED);

        RuleFor(x => x.Email)
            .EmailAddress()
                .WithMessage(ValidationMessageResource.EMAIL_INVALID_FORMAT)
            .When(x => !string.IsNullOrEmpty(x.Email));
    }
}
