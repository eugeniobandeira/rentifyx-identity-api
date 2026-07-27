namespace RentifyxIdentity.Domain.Interfaces.Users;

/// <summary>
/// Broadcasts the current status of every active user onto the "user-lifecycle-events" topic so a
/// downstream consumer's cold-start cache (e.g. rentifyx-asset-registry-api's owner-status cache,
/// STATE.md G-005) can be backfilled on demand - not tied to any single user's domain event, so it
/// is not raised via UserEntity/IDomainEvent.
/// </summary>
public interface IUserStatusSnapshotPublisher
{
    Task PublishSnapshotAsync(IReadOnlyCollection<Guid> activeUserIds, CancellationToken ct = default);
}
