using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Events;
using RentifyxIdentity.Domain.Interfaces.Common;

namespace RentifyxIdentity.Domain.Interfaces.Users;

public interface IUserRepository : IRepository<UserEntity>
{
    Task<UserEntity?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<UserEntity?> GetByTaxIdAsync(string taxId, CancellationToken ct = default);

    /// <summary>
    /// Writes the user item plus one Outbox item per event in <paramref name="entity"/>.DomainEvents and
    /// <paramref name="extraEvents"/>, atomically via a DynamoDB transaction, then clears the entity's raised
    /// events. <paramref name="extraEvents"/> covers events a handler raises directly rather than accumulating
    /// on the entity (e.g. PasswordResetRequested, UserLoggedIn) - see design.md.
    /// </summary>
    Task AddAsync(UserEntity entity, IReadOnlyCollection<IDomainEvent> extraEvents, CancellationToken ct = default);

    /// <summary>Same atomic write path as the AddAsync overload above, for update call sites.</summary>
    Task UpdateAsync(UserEntity entity, IReadOnlyCollection<IDomainEvent> extraEvents, CancellationToken ct = default);
}
