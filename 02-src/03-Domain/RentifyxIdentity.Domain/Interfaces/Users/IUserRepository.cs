using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Interfaces.Common;

namespace RentifyxIdentity.Domain.Interfaces.Users;

public interface IUserRepository : IRepository<UserEntity>
{
    Task<UserEntity?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<UserEntity?> GetByTaxIdAsync(string taxId, CancellationToken ct = default);
}
