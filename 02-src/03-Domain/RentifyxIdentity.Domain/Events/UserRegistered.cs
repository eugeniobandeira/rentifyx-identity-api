using RentifyxIdentity.Domain.Enums;

namespace RentifyxIdentity.Domain.Events;

public sealed record UserRegistered(
    Guid UserId,
    string Email,
    UserRole Role,
    DateTimeOffset OccurredAt
);
