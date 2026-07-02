using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Microsoft.Extensions.Configuration;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Interfaces.Users;
using RentifyxIdentity.Infrastructure.Constants;
using RentifyxIdentity.Infrastructure.Mapping;
using RentifyxIdentity.Infrastructure.Models;

namespace RentifyxIdentity.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly IDynamoDBContext _context;
    private readonly string _tableName;

    public UserRepository(IDynamoDBContext context, IConfiguration configuration)
    {
        _context = context;
        _tableName = configuration[DynamoDbConstants.TableNameConfigKey]
            ?? DynamoDbConstants.DefaultTableName;
    }

    public async Task AddAsync(
        UserEntity entity,
        CancellationToken ct = default)
    {
        UserDynamoDbItem item = UserDynamoDbMapper.ToItem(entity);
        await _context.SaveAsync(item, new SaveConfig { OverrideTableName = _tableName }, ct);
    }

    public async Task<UserEntity?> GetByIdAsync(
        Guid id,
        CancellationToken ct = default)
    {
        string pk = $"USER#{id}";
        UserDynamoDbItem? item = await _context.LoadAsync<UserDynamoDbItem>(
            pk,
            pk,
            new LoadConfig { OverrideTableName = _tableName },
            ct);
        return item is null ? null : UserDynamoDbMapper.ToEntity(item);
    }

    public async Task<UserEntity?> GetByEmailAsync(
        string email,
        CancellationToken ct = default)
    {
        return await QueryByGsiAsync(
            indexName: "GSI_Email",
            gsiAttribute: "Email",
            gsiValue: email.ToLowerInvariant(),
            ct);
    }

    public async Task<UserEntity?> GetByTaxIdAsync(
        string taxId,
        CancellationToken ct = default)
    {
        return await QueryByGsiAsync(
            indexName: "GSI_TaxId",
            gsiAttribute: "TaxId",
            gsiValue: taxId,
            ct);
    }

    public Task UpdateAsync(
        UserEntity entity,
        CancellationToken ct = default)
        => AddAsync(entity, ct);

    public async Task DeleteAsync(
        UserEntity entity,
        CancellationToken ct = default)
    {
        string pk = $"USER#{entity.Id}";
        await _context.DeleteAsync<UserDynamoDbItem>(
            pk,
            pk,
            new DeleteConfig { OverrideTableName = _tableName },
            ct);
    }

    private async Task<UserEntity?> QueryByGsiAsync(
        string indexName,
        string gsiAttribute,
        string gsiValue,
        CancellationToken ct)
    {
        QueryOperationConfig config = new()
        {
            IndexName = indexName,
            KeyExpression = new Expression
            {
                ExpressionStatement = $"{gsiAttribute} = :v",
                ExpressionAttributeValues = { [":v"] = gsiValue }
            },
            Limit = 1
        };

        List<UserDynamoDbItem> results = await _context
            .FromQueryAsync<UserDynamoDbItem>(config, new FromQueryConfig { OverrideTableName = _tableName })
            .GetRemainingAsync(ct);

        return results.Count == 0 ? null : UserDynamoDbMapper.ToEntity(results[0]);
    }
}
