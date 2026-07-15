namespace RentifyxIdentity.Domain.Events;

public sealed record UserEmailVerified(Guid UserId, string Email, DateTimeOffset OccurredAt) : IDomainEvent;
