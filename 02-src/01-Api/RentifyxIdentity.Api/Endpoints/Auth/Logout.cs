using ErrorOr;
using RentifyxIdentity.Api.Abstract;
using RentifyxIdentity.Api.Extensions;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Features.Identity.Auth.Logout.Request;

namespace RentifyxIdentity.Api.Endpoints.Auth;

internal sealed class Logout : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/logout", HandleAsync)
           .WithName("Logout")
           .WithDescription("Invalidates the user's refresh token. Idempotent — always returns 204.")
           .WithTags(Tags.AUTH)
           .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        LogoutRequest request,
        IHandler<LogoutRequest, Success> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        ErrorOr<Success> result = await handler.Handle(request, cancellationToken);

        return result.Match(
            _ => Results.NoContent(),
            errors => errors.ToProblem(httpContext));
    }
}
