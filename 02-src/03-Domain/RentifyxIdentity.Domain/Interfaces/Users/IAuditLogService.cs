using RentifyxIdentity.Domain.Contracts;

namespace RentifyxIdentity.Domain.Interfaces.Users;

public interface IAuditLogService
{
    Task LogAsync(Guid userId, string eventType, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLogEntryRecord>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
}
