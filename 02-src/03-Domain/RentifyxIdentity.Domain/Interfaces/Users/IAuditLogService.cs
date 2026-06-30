namespace RentifyxIdentity.Domain.Interfaces.Users;

public interface IAuditLogService
{
    Task LogAsync(Guid userId, string eventType, CancellationToken ct = default);
}
