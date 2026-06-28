using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Configuration;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Interfaces.Users;
using RentifyxIdentity.Infrastructure.Mapping;

namespace RentifyxIdentity.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly IAmazonDynamoDB _client;
    private readonly string _tableName;

    public UserRepository(
        IAmazonDynamoDB client,
        IConfiguration configuration)
    {
        _client = client;
        _tableName = configuration["AWS:DynamoDB:TableName"] ?? "rentifyx-identity";
    }

    public async Task AddAsync(
        UserEntity entity,
        CancellationToken cancellationToken = default)
    {
        Dictionary<string, AttributeValue> item = UserDynamoDbMapper.ToItem(entity);

        PutItemRequest request = new()
        {
            TableName = _tableName,
            Item = item,
            ConditionExpression = "attribute_not_exists(PK)"
        };

        await _client.PutItemAsync(request, cancellationToken);
    }

    public async Task<UserEntity?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        GetItemRequest request = new()
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = $"USER#{id}" }
            }
        };

        GetItemResponse response = await _client.GetItemAsync(request, cancellationToken);

        if (!response.IsItemSet)
            return null;

        return UserDynamoDbMapper.ToEntity(response.Item);
    }

    public async Task<UserEntity?> GetByEmailAsync(
        string email,
        CancellationToken ct = default)
    {
        QueryRequest request = new()
        {
            TableName = _tableName,
            IndexName = "GSI_Email",
            KeyConditionExpression = "GSI_Email_PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue { S = $"EMAIL#{email.ToLowerInvariant()}" }
            }
        };

        QueryResponse response = await _client.QueryAsync(request, ct);

        return response.Items.Count == 0
            ? null
            : UserDynamoDbMapper.ToEntity(response.Items[0]);
    }

    public async Task<UserEntity?> GetByTaxIdAsync(
        string taxId,
        CancellationToken ct = default)
    {
        QueryRequest request = new()
        {
            TableName = _tableName,
            IndexName = "GSI_TaxId",
            KeyConditionExpression = "GSI_TaxId_PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue { S = $"TAXID#{taxId.ToUpperInvariant()}" }
            }
        };

        QueryResponse response = await _client.QueryAsync(request, ct);

        return response.Items.Count == 0
            ? null
            : UserDynamoDbMapper.ToEntity(response.Items[0]);
    }

    public async Task UpdateAsync(
        UserEntity entity,
        CancellationToken cancellationToken = default)
    {
        Dictionary<string, AttributeValue> item = UserDynamoDbMapper.ToItem(entity);

        PutItemRequest request = new()
        {
            TableName = _tableName,
            Item = item
        };

        await _client.PutItemAsync(request, cancellationToken);
    }

    public async Task DeleteAsync(
        UserEntity entity,
        CancellationToken cancellationToken = default)
    {
        DeleteItemRequest request = new()
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = $"USER#{entity.Id}" }
            }
        };

        await _client.DeleteItemAsync(request, cancellationToken);
    }
}
