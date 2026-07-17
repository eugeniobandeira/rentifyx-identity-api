using System.Security.Claims;
using ErrorOr;
using RentifyxIdentity.Api.Abstract;
using RentifyxIdentity.Api.Extensions;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Features.Identity.User.Consent;
using RentifyxIdentity.Application.Features.Identity.User.Consent.Request;

namespace RentifyxIdentity.Api.Endpoints.Users;

internal sealed class GetConsent : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/users/me/consent", HandleAsync)
           .WithName("GetConsent")
           .WithDescription("Returns the authenticated user's current consent state per purpose (LGPD Art. 8 Section 5).")
           .WithTags(Tags.USERS);
    }

    private static async Task<IResult> HandleAsync(
        IHandler<GetConsentRequest, ConsentResponse> handler,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        if (!Guid.TryParse(httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out Guid userId))
            return Results.Unauthorized();

        ErrorOr<ConsentResponse> result = await handler.HandleAsync(
            new GetConsentRequest(userId),
            ct);

        return result.Match(
            response => Results.Ok(response),
            errors => errors.ToProblem(httpContext));
    }
}
