using FluentValidation;
using RentifyxIdentity.Application.Features.Identity.Auth.ResetPassword.Request;
using RentifyxIdentity.Domain.Constants;
using RentifyxIdentity.Domain.MessageResource;

namespace RentifyxIdentity.Application.Features.Identity.Auth.ResetPassword.Validator;

public sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordRequest>
{
    private static readonly string Symbols = "!@#$%^&*()-_=+[]{}|;:,.<>?";

    public ResetPasswordValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
                .WithMessage(ValidationMessageResource.EMAIL_REQUIRED);

        RuleFor(x => x.Email)
            .EmailAddress()
                .WithMessage(ValidationMessageResource.EMAIL_INVALID_FORMAT)
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.Token)
            .NotEmpty()
                .WithMessage(ValidationMessageResource.TOKEN_REQUIRED);

        RuleFor(x => x.NewPassword)
            .NotEmpty()
                .WithMessage(ValidationMessageResource.PASSWORD_REQUIRED);

        RuleFor(x => x.NewPassword)
            .MinimumLength(ValidationConstants.UserRules.PasswordMinLength)
                .WithMessage(ValidationMessageResource.PASSWORD_MIN_LENGTH)
            .MaximumLength(ValidationConstants.UserRules.PasswordMaxLength)
                .WithMessage(ValidationMessageResource.PASSWORD_MAX_LENGTH)
            .Must(HasComplexity)
                .WithMessage(ValidationMessageResource.PASSWORD_COMPLEXITY)
            .When(x => !string.IsNullOrEmpty(x.NewPassword));
    }

    private static bool HasComplexity(string password)
    {
        bool hasUpper = false;
        bool hasLower = false;
        bool hasDigit = false;
        bool hasSymbol = false;

        foreach (char c in password)
        {
            if (char.IsUpper(c))
                hasUpper = true;
            else if (char.IsLower(c))
                hasLower = true;
            else if (char.IsDigit(c))
                hasDigit = true;
            else if (Symbols.Contains(c, StringComparison.Ordinal))
                hasSymbol = true;
        }

        return hasUpper && hasLower && hasDigit && hasSymbol;
    }
}
