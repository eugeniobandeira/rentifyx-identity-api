namespace RentifyxIdentity.Domain.Events;

public sealed record UserAccountDeleted(Guid UserId, DateTimeOffset OccurredAt);
