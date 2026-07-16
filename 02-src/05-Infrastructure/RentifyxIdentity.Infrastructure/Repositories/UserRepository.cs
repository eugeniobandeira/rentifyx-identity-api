using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Configuration;
using RentifyxIdentity.Domain.Constants;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Events;
using RentifyxIdentity.Domain.Interfaces.Users;
using RentifyxIdentity.Infrastructure.Constants;
using RentifyxIdentity.Infrastructure.Mapping;
using RentifyxIdentity.Infrastructure.Models;

namespace RentifyxIdentity.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly IDynamoDBContext _context;
    private readonly IAmazonDynamoDB _client;
    private readonly string _tableName;

    public UserRepository(IDynamoDBContext context, IAmazonDynamoDB client, IConfiguration configuration)
    {
        _context = context;
        _client = client;
        _tableName = configuration[DynamoDbConstants.TableNameConfigKey]
            ?? DynamoDbConstants.DefaultTableName;
    }

    public Task AddAsync(
        UserEntity entity,
        CancellationToken ct = default)
        => AddAsync(entity, [], ct);

    public Task AddAsync(
        UserEntity entity,
        IReadOnlyCollection<IDomainEvent> extraEvents,
        CancellationToken ct = default)
        => WriteTransactionallyAsync(entity, extraEvents, ct);

    public async Task<UserEntity?> GetByIdAsync(
        Guid id,
        CancellationToken ct = default)
    {
        string pk = $"{DynamoDbConstants.UserKeyPrefix}{id}";
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
            indexName: DynamoDbConstants.GsiEmail,
            gsiAttribute: DynamoDbConstants.EmailAttribute,
            gsiValue: email.ToLowerInvariant(),
            ct);
    }

    public async Task<UserEntity?> GetByTaxIdAsync(
        string taxId,
        CancellationToken ct = default)
    {
        return await QueryByGsiAsync(
            indexName: DynamoDbConstants.GsiTaxId,
            gsiAttribute: DynamoDbConstants.TaxIdAttribute,
            gsiValue: taxId,
            ct);
    }

    public Task UpdateAsync(
        UserEntity entity,
        CancellationToken ct = default)
        => UpdateAsync(entity, [], ct);

    public Task UpdateAsync(
        UserEntity entity,
        IReadOnlyCollection<IDomainEvent> extraEvents,
        CancellationToken ct = default)
        => WriteTransactionallyAsync(entity, extraEvents, ct);

    public async Task DeleteAsync(
        UserEntity entity,
        CancellationToken ct = default)
    {
        string pk = $"{DynamoDbConstants.UserKeyPrefix}{entity.Id}";
        await _context.DeleteAsync<UserDynamoDbItem>(
            pk,
            pk,
            new DeleteConfig { OverrideTableName = _tableName },
            ct);
    }

    /// <summary>
    /// Writes the user item and one Outbox item per raised event in a single DynamoDB transaction - either all
    /// items land, or none do (TransactWriteItemsAsync fails the whole request and nothing is persisted).
    /// Domain events are only cleared after the transaction actually succeeds.
    /// </summary>
    private async Task WriteTransactionallyAsync(
        UserEntity entity,
        IReadOnlyCollection<IDomainEvent> extraEvents,
        CancellationToken ct)
    {
        UserDynamoDbItem userItem = UserDynamoDbMapper.ToItem(entity);

        List<TransactWriteItem> transactItems =
        [
            new TransactWriteItem
            {
                Put = new Put
                {
                    TableName = _tableName,
                    Item = _context.ToDocument(userItem).ToAttributeMap()
                }
            }
        ];

        foreach (IDomainEvent domainEvent in entity.DomainEvents.Concat(extraEvents))
        {
            OutboxDynamoDbItem outboxItem = OutboxItemMapper.ToItem(CreateOutboxEntry(domainEvent));

            transactItems.Add(new TransactWriteItem
            {
                Put = new Put
                {
                    TableName = _tableName,
                    Item = _context.ToDocument(outboxItem).ToAttributeMap()
                }
            });
        }

        await _client.TransactWriteItemsAsync(new TransactWriteItemsRequest { TransactItems = transactItems }, ct);

        entity.ClearDomainEvents();
    }

    /// <summary>
    /// Placeholder mapping: every event lands on the generic lifecycle topic, serialized as-is. T9
    /// (IOutboxEntryFactory) replaces this with per-event-type topic routing and comms-api's exact
    /// DispatchNotificationRequest shape for UserRegistered/PasswordResetRequested - this method exists only to
    /// keep T7's atomic write path testable before that factory lands.
    /// </summary>
    private static OutboxEntry CreateOutboxEntry(IDomainEvent domainEvent) =>
        OutboxEntry.Create(KafkaTopics.UserLifecycleEvents, JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

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
