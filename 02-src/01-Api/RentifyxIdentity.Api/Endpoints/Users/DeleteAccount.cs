using System.Security.Claims;
using ErrorOr;
using RentifyxIdentity.Api.Abstract;
using RentifyxIdentity.Api.Extensions;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Features.Identity.User.DeleteAccount.Request;

namespace RentifyxIdentity.Api.Endpoints.Users;

internal sealed class DeleteAccount : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("/users/me", HandleAsync)
           .WithName("DeleteAccount")
           .WithDescription("Soft-deletes the authenticated user's account and anonymizes PII (LGPD Art. 18 VI).")
           .WithTags(Tags.USERS);
    }

    private static async Task<IResult> HandleAsync(
        IHandler<DeleteAccountRequest, Success> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out Guid userId))
            return Results.Unauthorized();

        ErrorOr<Success> result = await handler.Handle(
            new DeleteAccountRequest(userId),
            cancellationToken);

        return result.Match(
            _ => Results.NoContent(),
            errors => errors.ToProblem(httpContext));
    }
}
