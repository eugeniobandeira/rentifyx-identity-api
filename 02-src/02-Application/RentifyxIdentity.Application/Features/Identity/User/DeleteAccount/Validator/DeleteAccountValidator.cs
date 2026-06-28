using FluentValidation;
using RentifyxIdentity.Application.Features.Identity.User.DeleteAccount.Request;

namespace RentifyxIdentity.Application.Features.Identity.User.DeleteAccount.Validator;

public sealed class DeleteAccountValidator : AbstractValidator<DeleteAccountRequest>
{
    public DeleteAccountValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();
    }
}
