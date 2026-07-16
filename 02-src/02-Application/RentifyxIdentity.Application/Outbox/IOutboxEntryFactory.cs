using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Events;

namespace RentifyxIdentity.Application.Outbox;

public interface IOutboxEntryFactory
{
    IReadOnlyList<OutboxEntry> CreateEntries(IReadOnlyCollection<IDomainEvent> domainEvents);
}
