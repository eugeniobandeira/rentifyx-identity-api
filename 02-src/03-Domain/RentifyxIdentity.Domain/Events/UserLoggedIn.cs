namespace RentifyxIdentity.Domain.Events;

public sealed record UserLoggedIn(Guid UserId, string Email, DateTimeOffset OccurredAt);
