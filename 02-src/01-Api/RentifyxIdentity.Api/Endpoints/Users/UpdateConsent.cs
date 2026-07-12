using System.Security.Claims;
using ErrorOr;
using RentifyxIdentity.Api.Abstract;
using RentifyxIdentity.Api.Extensions;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Features.Identity.User.Consent;
using RentifyxIdentity.Application.Features.Identity.User.Consent.Request;

namespace RentifyxIdentity.Api.Endpoints.Users;

internal sealed record UpdateConsentApiRequest(string Purpose, bool Granted);

internal sealed class UpdateConsent : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/users/me/consent", HandleAsync)
           .WithName("UpdateConsent")
           .WithDescription("Grants or revokes the authenticated user's consent for a purpose (Essential/Marketing). Revoking Essential consent suspends the account; granting it reactivates the account.")
           .WithTags(Tags.USERS);
    }

    private static async Task<IResult> HandleAsync(
        UpdateConsentApiRequest request,
        IHandler<UpdateConsentRequest, ConsentResponse> handler,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        if (!Guid.TryParse(httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out Guid userId))
            return Results.Unauthorized();

        ErrorOr<ConsentResponse> result = await handler.Handle(
            new UpdateConsentRequest(userId, request.Purpose, request.Granted),
            ct);

        return result.Match(
            response => Results.Ok(response),
            errors => errors.ToProblem(httpContext));
    }
}
