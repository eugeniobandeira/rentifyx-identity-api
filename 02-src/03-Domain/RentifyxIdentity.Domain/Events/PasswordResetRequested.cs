namespace RentifyxIdentity.Domain.Events;

public sealed record PasswordResetRequested(
    Guid UserId,
    string Email,
    string RawToken,
    DateTimeOffset OccurredAt
) : IDomainEvent;
