using FluentValidation;
using RentifyxIdentity.Application.Features.Identity.Auth.Register.Request;
using RentifyxIdentity.Domain.Constants;
using RentifyxIdentity.Domain.MessageResource;

namespace RentifyxIdentity.Application.Features.Identity.Auth.Register.Validator;

public sealed class RegisterUserValidator : AbstractValidator<RegisterUserRequest>
{
    private static readonly HashSet<string> DisposableDomains =
    [
        "mailinator.com",
        "guerrillamail.com",
        "tempmail.com",
        "throwam.com",
        "yopmail.com"
    ];

    public RegisterUserValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
                .WithMessage(ValidationMessageResource.EMAIL_REQUIRED);

        RuleFor(x => x.Email)
            .EmailAddress()
                .WithMessage(ValidationMessageResource.EMAIL_INVALID_FORMAT)
            .MaximumLength(ValidationConstants.UserRules.EmailMaxLength)
                .WithMessage(ValidationMessageResource.EMAIL_MAX_LENGTH)
            .Must(email => !IsDisposableDomain(email))
                .WithMessage(ValidationMessageResource.EMAIL_DISPOSABLE_DOMAIN)
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.TaxId)
            .NotEmpty()
                .WithMessage(ValidationMessageResource.TAXID_REQUIRED);

        RuleFor(x => x.Password)
            .NotEmpty()
                .WithMessage(ValidationMessageResource.PASSWORD_REQUIRED);

        RuleFor(x => x.Password)
            .MinimumLength(ValidationConstants.UserRules.PasswordMinLength)
                .WithMessage(ValidationMessageResource.PASSWORD_MIN_LENGTH)
            .MaximumLength(ValidationConstants.UserRules.PasswordMaxLength)
                .WithMessage(ValidationMessageResource.PASSWORD_MAX_LENGTH)
            .Must(HasComplexity)
                .WithMessage(ValidationMessageResource.PASSWORD_COMPLEXITY)
            .When(x => !string.IsNullOrEmpty(x.Password));

        RuleFor(x => x.Role)
            .NotEmpty()
                .WithMessage(ValidationMessageResource.ROLE_REQUIRED);

        RuleFor(x => x.Role)
            .Must(role => role is "Owner" or "Renter" or "Admin")
                .WithMessage(ValidationMessageResource.ROLE_INVALID)
            .When(x => !string.IsNullOrEmpty(x.Role));

        RuleFor(x => x.ConsentGiven)
            .Equal(true)
                .WithMessage(ValidationMessageResource.CONSENT_REQUIRED);
    }

    private static bool IsDisposableDomain(string email)
    {
        int atIndex = email.IndexOf('@', StringComparison.Ordinal);
        if (atIndex < 0)
            return false;

        string domain = email[(atIndex + 1)..].ToLowerInvariant();
        return DisposableDomains.Contains(domain);
    }

    private static bool HasComplexity(string password)
    {
        const string symbols = "!@#$%^&*()-_=+[]{}|;:,.<>?";

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
            else if (symbols.Contains(c, StringComparison.Ordinal))
                hasSymbol = true;
        }

        return hasUpper && hasLower && hasDigit && hasSymbol;
    }
}
