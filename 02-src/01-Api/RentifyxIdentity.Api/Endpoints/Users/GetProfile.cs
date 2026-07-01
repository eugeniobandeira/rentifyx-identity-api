using System.Security.Claims;
using ErrorOr;
using RentifyxIdentity.Api.Abstract;
using RentifyxIdentity.Api.Extensions;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Features.Identity;
using RentifyxIdentity.Application.Features.Identity.User.GetProfile.Request;

namespace RentifyxIdentity.Api.Endpoints.Users;

internal sealed class GetProfile : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/users/me", HandleAsync)
           .WithName("GetProfile")
           .WithDescription("Returns the authenticated user's profile (LGPD Art. 18).")
           .WithTags(Tags.USERS);
    }

    private static async Task<IResult> HandleAsync(
        IHandler<GetProfileRequest, UserResponse> handler,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        if (!Guid.TryParse(httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out Guid userId))
            return Results.Unauthorized();

        ErrorOr<UserResponse> result = await handler.Handle(
            new GetProfileRequest(userId),
            ct);

        return result.Match(
            response => Results.Ok(response),
            errors => errors.ToProblem(httpContext));
    }
}
