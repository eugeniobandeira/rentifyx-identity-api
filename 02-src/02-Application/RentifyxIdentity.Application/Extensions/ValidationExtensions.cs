using ErrorOr;
using FluentValidation;
using FluentValidation.Results;

namespace RentifyxIdentity.Application.Extensions;

internal static class ValidationExtensions
{
    public static async Task<List<Error>?> ValidateToErrorsAsync<T>(
        this IValidator<T> validator,
        T instance,
        CancellationToken ct = default)
    {
        ValidationResult result = await validator.ValidateAsync(instance, ct);

        return result.IsValid
            ? null
            : [.. result.Errors.Select(ToError)];
    }

    private static Error ToError(ValidationFailure failure) =>
        failure.ErrorCode.EndsWith(".NotFound", StringComparison.Ordinal)
            ? Error.NotFound(failure.ErrorCode, failure.ErrorMessage)
            : Error.Validation(failure.PropertyName, failure.ErrorMessage);
}
