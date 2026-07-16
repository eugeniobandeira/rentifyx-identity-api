using RentifyxIdentity.Domain.Enums;

namespace RentifyxIdentity.Domain.Events;

public sealed record UserRegistered(
    Guid UserId,
    string Email,
    UserRole Role,
    string RawToken,
    DateTimeOffset OccurredAt) : IDomainEvent;
