using ErrorOr;
using RentifyxIdentity.Api.Abstract;
using RentifyxIdentity.Api.Extensions;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Features.Identity.Auth.ResetPassword.Request;

namespace RentifyxIdentity.Api.Endpoints.Auth;

internal sealed class ResetPassword : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/reset-password", HandleAsync)
           .WithName("ResetPassword")
           .WithDescription("Confirms a password reset using the token sent by email.")
           .WithTags(Tags.AUTH)
           .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        ResetPasswordRequest request,
        IHandler<ResetPasswordRequest, Success> handler,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        ErrorOr<Success> result = await handler.HandleAsync(request, ct);

        return result.Match(
            _ => Results.NoContent(),
            errors => errors.ToProblem(httpContext));
    }
}
