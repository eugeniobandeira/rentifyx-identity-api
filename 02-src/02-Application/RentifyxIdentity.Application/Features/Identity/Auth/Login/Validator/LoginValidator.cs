using FluentValidation;
using RentifyxIdentity.Application.Features.Identity.Auth.Login.Request;
using RentifyxIdentity.Domain.MessageResource;

namespace RentifyxIdentity.Application.Features.Identity.Auth.Login.Validator;

public sealed class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
                .WithMessage(ValidationMessageResource.EMAIL_REQUIRED);

        RuleFor(x => x.Email)
            .EmailAddress()
                .WithMessage(ValidationMessageResource.EMAIL_INVALID_FORMAT)
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.Password)
            .NotEmpty()
                .WithMessage(ValidationMessageResource.PASSWORD_REQUIRED);
    }
}
