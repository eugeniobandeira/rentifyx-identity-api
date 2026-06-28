namespace RentifyxIdentity.Domain.Events;

public sealed record UserSuspended(Guid UserId, string Reason, DateTimeOffset OccurredAt);
