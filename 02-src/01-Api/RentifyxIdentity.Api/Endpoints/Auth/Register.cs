using ErrorOr;
using RentifyxIdentity.Api.Abstract;
using RentifyxIdentity.Api.Extensions;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Features.Identity;
using RentifyxIdentity.Application.Features.Identity.Auth.Register.Request;

namespace RentifyxIdentity.Api.Endpoints.Auth;

internal sealed class Register : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/register", HandleAsync)
           .WithName("RegisterUser")
           .WithDescription("Register a new user account.")
           .WithTags(Tags.AUTH)
           .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        RegisterUserRequest request,
        IHandler<RegisterUserRequest, UserResponse> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        ErrorOr<UserResponse> result = await handler.Handle(request, cancellationToken);

        return result.Match(
            response => Results.Created($"/api/v1/users/{response.Id}", response),
            errors => errors.ToProblem(httpContext));
    }
}
