using ErrorOr;
using RentifyxIdentity.Api.Abstract;
using RentifyxIdentity.Api.Contracts.Auth;
using RentifyxIdentity.Api.Extensions;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Features.Identity.Auth.Login;
using RentifyxIdentity.Application.Features.Identity.Auth.Login.Request;

namespace RentifyxIdentity.Api.Endpoints.Auth;

internal sealed class Login : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/login", HandleAsync)
           .WithName("Login")
           .WithDescription("Authenticates a user with email and password. Returns the access token in the body and sets the refresh token as an httpOnly cookie.")
           .WithTags(Tags.AUTH)
           .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        LoginRequest request,
        IHandler<LoginRequest, LoginResponse> handler,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        ErrorOr<LoginResponse> result = await handler.HandleAsync(request, ct);

        return result.Match(
            response =>
            {
                httpContext.AppendRefreshTokenCookie(response.RefreshToken);
                return Results.Ok(new AuthTokenResponse(response.AccessToken, response.User));
            },
            errors => errors.ToProblem(httpContext));
    }
}
