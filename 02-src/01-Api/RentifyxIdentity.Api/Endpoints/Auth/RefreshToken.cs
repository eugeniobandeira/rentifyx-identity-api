using ErrorOr;
using RentifyxIdentity.Api.Abstract;
using RentifyxIdentity.Api.Contracts.Auth;
using RentifyxIdentity.Api.Extensions;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Features.Identity.Auth.Login;
using RentifyxIdentity.Application.Features.Identity.Auth.RefreshToken.Request;

namespace RentifyxIdentity.Api.Endpoints.Auth;

internal sealed class RefreshToken : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/refresh", HandleAsync)
           .WithName("RefreshToken")
           .WithDescription("Issues a new access token and rotates the refresh token cookie.")
           .WithTags(Tags.AUTH)
           .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        EmailRequestBody request,
        IHandler<RefreshTokenRequest, LoginResponse> handler,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        string refreshToken = httpContext.GetRefreshTokenCookie() ?? string.Empty;

        ErrorOr<LoginResponse> result = await handler.HandleAsync(
            new RefreshTokenRequest(request.Email, refreshToken),
            ct);

        return result.Match(
            response =>
            {
                httpContext.AppendRefreshTokenCookie(response.RefreshToken);
                return Results.Ok(new AuthTokenResponse(response.AccessToken, response.User));
            },
            errors => errors.ToProblem(httpContext));
    }
}
