using RentifyxIdentity.Domain.Common;

namespace RentifyxIdentity.Domain.Interfaces.Common;

public interface IRepository<T> where T : class
{
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
}

public interface IRepository<T, TFilter> : IRepository<T>
    where T : class
    where TFilter : class
{
    Task<PagedResult<T>> GetAllAsync(TFilter filter, CancellationToken cancellationToken = default);
}
