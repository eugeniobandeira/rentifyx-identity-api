using ErrorOr;
using RentifyxIdentity.Api.Abstract;
using RentifyxIdentity.Api.Contracts.Auth;
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
           .WithDescription("Invalidates the user's refresh token and clears the cookie. Idempotent — always returns 204.")
           .WithTags(Tags.AUTH)
           .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        EmailRequestBody request,
        IHandler<LogoutRequest, Success> handler,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        string? refreshToken = httpContext.GetRefreshTokenCookie();
        httpContext.DeleteRefreshTokenCookie();

        if (string.IsNullOrEmpty(refreshToken))
            return Results.NoContent();

        ErrorOr<Success> result = await handler.HandleAsync(
            new LogoutRequest(request.Email, refreshToken),
            ct);

        return result.Match(
            _ => Results.NoContent(),
            errors => errors.ToProblem(httpContext));
    }
}
