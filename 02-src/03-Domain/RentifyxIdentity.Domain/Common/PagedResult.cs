namespace RentifyxIdentity.Domain.Common;

public sealed record PagedResult<T>(IReadOnlyCollection<T> Items, int Total);
