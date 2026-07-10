using ErrorOr;
using RentifyxIdentity.Api.Constants;
using RentifyxIdentity.Domain.Constants;

namespace RentifyxIdentity.Api.Extensions;

internal static class ErrorOrExtensions
{
    public static IResult ToProblem(this List<Error> errors, HttpContext httpContext)
    {
        Error firstError = errors[0];

        int statusCode = firstError.NumericType is >= 400 and <= 599
            ? firstError.NumericType
            : firstError.Type switch
            {
                ErrorType.Validation => StatusCodes.Status422UnprocessableEntity,
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                _ => StatusCodes.Status500InternalServerError
            };

        string? correlationId = httpContext.Items[CorrelationIdConstants.Key]?.ToString();

        if (firstError.Type is ErrorType.Validation)
        {
            Dictionary<string, string[]> validationErrors = errors
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());

            return Results.ValidationProblem(
                validationErrors,
                statusCode: statusCode,
                extensions: new Dictionary<string, object?> { [ProblemDetailsKeys.CorrelationId] = correlationId });
        }

        return Results.Problem(
            title: firstError.Description,
            statusCode: statusCode,
            extensions: new Dictionary<string, object?> { [ProblemDetailsKeys.CorrelationId] = correlationId });
    }
}
