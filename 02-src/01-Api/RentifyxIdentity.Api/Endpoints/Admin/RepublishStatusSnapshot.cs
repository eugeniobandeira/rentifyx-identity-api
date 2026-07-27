using ErrorOr;
using RentifyxIdentity.Api.Abstract;
using RentifyxIdentity.Api.Extensions;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Features.Admin.RepublishStatusSnapshot;
using RentifyxIdentity.Application.Features.Admin.RepublishStatusSnapshot.Request;

namespace RentifyxIdentity.Api.Endpoints.Admin;

/// <summary>
/// Admin-only, callable at any time (not just on a downstream service's first deploy) - re-broadcasts
/// every active user's status so a consumer's cold-start cache can be backfilled or resynced on demand.
/// See rentifyx-asset-registry-api's STATE.md G-005 for the problem this closes.
/// </summary>
internal sealed class RepublishStatusSnapshot : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/admin/users/status-snapshot", HandleAsync)
           .WithName("RepublishUserStatusSnapshot")
           .WithDescription("Republishes every active user's status onto user-lifecycle-events for downstream cache backfill.")
           .WithTags(Tags.ADMIN)
           .RequireAuthorization("AdminOnly");
    }

    private static async Task<IResult> HandleAsync(
        IHandler<RepublishStatusSnapshotRequest, RepublishStatusSnapshotResponse> handler,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        ErrorOr<RepublishStatusSnapshotResponse> result = await handler.HandleAsync(
            new RepublishStatusSnapshotRequest(),
            ct);

        return result.Match(
            response => Results.Ok(response),
            errors => errors.ToProblem(httpContext));
    }
}
