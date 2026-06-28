namespace RentifyxIdentity.Domain.Events;

public sealed record UserPasswordChanged(Guid UserId, DateTimeOffset OccurredAt);
