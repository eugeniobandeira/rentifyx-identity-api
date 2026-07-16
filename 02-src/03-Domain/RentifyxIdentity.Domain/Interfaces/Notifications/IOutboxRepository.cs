using RentifyxIdentity.Domain.Entities;

namespace RentifyxIdentity.Domain.Interfaces.Notifications;

public interface IOutboxRepository
{
    Task<IReadOnlyList<OutboxEntry>> GetPendingAsync(int batchSize, CancellationToken ct = default);
    Task MarkPublishedAsync(Guid id, CancellationToken ct = default);
    Task MarkFailedAsync(Guid id, CancellationToken ct = default);
    Task IncrementRetryAsync(Guid id, CancellationToken ct = default);
}
