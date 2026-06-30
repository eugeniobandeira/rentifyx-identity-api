using RentifyxIdentity.Application.Features.Examples.Handlers.GetAll.Request;
using RentifyxIdentity.Domain.Common;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Interfaces.Common;

namespace RentifyxIdentity.Infrastructure.Repositories;

public sealed class ExampleRepository : IRepository<ExampleEntity, GetAllExampleRequest>
{
    public Task AddAsync(ExampleEntity entity, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<ExampleEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<PagedResult<ExampleEntity>> GetAllAsync(GetAllExampleRequest filter, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task UpdateAsync(ExampleEntity entity, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task DeleteAsync(ExampleEntity entity, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}
