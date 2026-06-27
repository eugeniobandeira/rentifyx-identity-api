using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Interfaces.Users;

namespace RentifyxIdentity.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    // Methods from IRepository<UserEntity> — parameter name must match the base interface
    public Task AddAsync(UserEntity entity, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<UserEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task UpdateAsync(UserEntity entity, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task DeleteAsync(UserEntity entity, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    // Methods from IUserRepository — our interface, uses ct
    public Task<UserEntity?> GetByEmailAsync(string email, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<UserEntity?> GetByTaxIdAsync(string taxId, CancellationToken ct = default)
        => throw new NotImplementedException();
}
