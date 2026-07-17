using ErrorOr;
using RentifyxIdentity.Api.Abstract;
using RentifyxIdentity.Api.Extensions;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Features.Identity.Auth.ForgotPassword.Request;

namespace RentifyxIdentity.Api.Endpoints.Auth;

internal sealed class ForgotPassword : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/forgot-password", HandleAsync)
           .WithName("ForgotPassword")
           .WithDescription("Sends a password reset email. Always returns 204 to prevent email enumeration.")
           .WithTags(Tags.AUTH)
           .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        ForgotPasswordRequest request,
        IHandler<ForgotPasswordRequest, Success> handler,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        ErrorOr<Success> result = await handler.HandleAsync(request, ct);

        return result.Match(
            _ => Results.NoContent(),
            errors => errors.ToProblem(httpContext));
    }
}
