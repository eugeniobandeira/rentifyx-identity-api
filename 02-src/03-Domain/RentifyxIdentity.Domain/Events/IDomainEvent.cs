namespace RentifyxIdentity.Domain.Events;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
