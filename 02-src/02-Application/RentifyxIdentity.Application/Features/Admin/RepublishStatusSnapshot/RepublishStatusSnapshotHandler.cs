using ErrorOr;
using Microsoft.Extensions.Logging;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Features.Admin.RepublishStatusSnapshot.Request;
using RentifyxIdentity.Domain.Interfaces.Users;

namespace RentifyxIdentity.Application.Features.Admin.RepublishStatusSnapshot;

/// <summary>
/// Backfills a downstream service's cold-start owner-status cache (e.g. rentifyx-asset-registry-api's
/// G-005) by re-broadcasting every currently active user's status onto "user-lifecycle-events". Safe
/// to call repeatedly and at any time - not just on first deploy - since the consumer-side upsert is
/// idempotent per user.
/// </summary>
public sealed class RepublishStatusSnapshotHandler(
    IUserRepository repository,
    IUserStatusSnapshotPublisher publisher,
    ILogger<RepublishStatusSnapshotHandler> logger)
    : IHandler<RepublishStatusSnapshotRequest, RepublishStatusSnapshotResponse>
{
    public async Task<ErrorOr<RepublishStatusSnapshotResponse>> HandleAsync(
        RepublishStatusSnapshotRequest request,
        CancellationToken ct = default)
    {
        IReadOnlyList<Guid> activeUserIds = await repository.GetAllActiveUserIdsAsync(ct);

        await publisher.PublishSnapshotAsync(activeUserIds, ct);

        logger.LogInformation(
            "Owner-status snapshot republished. ActiveUserCount={ActiveUserCount}",
            activeUserIds.Count);

        return new RepublishStatusSnapshotResponse(activeUserIds.Count);
    }
}
