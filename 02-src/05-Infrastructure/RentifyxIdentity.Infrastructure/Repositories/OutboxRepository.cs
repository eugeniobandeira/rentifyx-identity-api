using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Microsoft.Extensions.Configuration;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Domain.Interfaces.Notifications;
using RentifyxIdentity.Infrastructure.Constants;
using RentifyxIdentity.Infrastructure.Mapping;
using RentifyxIdentity.Infrastructure.Models;

namespace RentifyxIdentity.Infrastructure.Repositories;

public sealed class OutboxRepository : IOutboxRepository
{
    private readonly IDynamoDBContext _context;
    private readonly string _tableName;

    public OutboxRepository(IDynamoDBContext context, IConfiguration configuration)
    {
        _context = context;
        _tableName = configuration[DynamoDbConstants.TableNameConfigKey]
            ?? DynamoDbConstants.DefaultTableName;
    }

    public async Task<IReadOnlyList<OutboxEntry>> GetPendingAsync(int batchSize, CancellationToken ct = default)
    {
        QueryOperationConfig config = new()
        {
            IndexName = DynamoDbConstants.GsiOutbox,
            KeyExpression = new Expression
            {
                ExpressionStatement = "GsiOutboxStatusPk = :v",
                ExpressionAttributeValues = { [":v"] = $"{DynamoDbConstants.OutboxStatusPrefix}{OutboxStatus.Pending}" }
            },
            Limit = batchSize
        };

        // GetRemainingAsync pages through the whole result set, ignoring Limit as anything but a per-page
        // fetch size - GetNextSetAsync returns exactly one page, which is what actually caps the batch here.
        List<OutboxDynamoDbItem> items = await _context
            .FromQueryAsync<OutboxDynamoDbItem>(config, new FromQueryConfig { OverrideTableName = _tableName })
            .GetNextSetAsync(ct);

        return items.Select(OutboxItemMapper.FromItem).ToList();
    }

    public Task MarkPublishedAsync(Guid id, CancellationToken ct = default) =>
        UpdateEntryAsync(id, entry => entry.MarkPublished(), ct);

    public Task MarkFailedAsync(Guid id, CancellationToken ct = default) =>
        UpdateEntryAsync(id, entry => entry.MarkFailed(), ct);

    public Task IncrementRetryAsync(Guid id, CancellationToken ct = default) =>
        UpdateEntryAsync(id, entry => entry.IncrementRetryCount(), ct);

    private async Task UpdateEntryAsync(Guid id, Action<OutboxEntry> mutate, CancellationToken ct)
    {
        string pk = $"{DynamoDbConstants.OutboxKeyPrefix}{id}";
        OutboxDynamoDbItem? item = await _context.LoadAsync<OutboxDynamoDbItem>(
            pk,
            pk,
            new LoadConfig { OverrideTableName = _tableName },
            ct);

        if (item is null)
            return;

        OutboxEntry entry = OutboxItemMapper.FromItem(item);
        mutate(entry);

        await _context.SaveAsync(
            OutboxItemMapper.ToItem(entry),
            new SaveConfig { OverrideTableName = _tableName },
            ct);
    }
}
