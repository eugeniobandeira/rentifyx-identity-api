using FluentValidation;
using RentifyxIdentity.Application.Features.Identity.Auth.RefreshToken.Request;
using RentifyxIdentity.Domain.MessageResource;

namespace RentifyxIdentity.Application.Features.Identity.Auth.RefreshToken.Validator;

public sealed class RefreshTokenValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
                .WithMessage(ValidationMessageResource.EMAIL_REQUIRED);

        RuleFor(x => x.Email)
            .EmailAddress()
                .WithMessage(ValidationMessageResource.EMAIL_INVALID_FORMAT)
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.RefreshToken)
            .NotEmpty()
                .WithMessage(ValidationMessageResource.TOKEN_REQUIRED);
    }
}
