using FluentValidation;
using RentifyxIdentity.Application.Features.Identity.User.ExportData.Request;

namespace RentifyxIdentity.Application.Features.Identity.User.ExportData.Validator;

public sealed class ExportDataValidator : AbstractValidator<ExportDataRequest>
{
    public ExportDataValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();
    }
}
