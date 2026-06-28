using ErrorOr;
using RentifyxIdentity.Api.Abstract;
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
           .WithDescription("Authenticates a user with email and password, returning access and refresh tokens.")
           .WithTags(Tags.AUTH)
           .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        LoginRequest request,
        IHandler<LoginRequest, LoginResponse> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        ErrorOr<LoginResponse> result = await handler.Handle(request, cancellationToken);

        return result.Match(
            response => Results.Ok(response),
            errors => errors.ToProblem(httpContext));
    }
}
