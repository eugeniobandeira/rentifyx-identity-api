using RentifyxIdentity.Domain.Interfaces.Users;

namespace RentifyxIdentity.Tests.Common.Fakes;

public sealed class FakeAuditLogService : IAuditLogService
{
    public List<(Guid UserId, string EventType)> Entries { get; } = new();

    public Task LogAsync(Guid userId, string eventType, CancellationToken ct = default)
    {
        Entries.Add((userId, eventType));
        return Task.CompletedTask;
    }
}
