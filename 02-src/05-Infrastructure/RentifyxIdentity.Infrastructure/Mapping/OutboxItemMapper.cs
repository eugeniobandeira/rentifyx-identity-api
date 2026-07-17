using System.Globalization;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Enums;
using RentifyxIdentity.Infrastructure.Constants;
using RentifyxIdentity.Infrastructure.Models;

namespace RentifyxIdentity.Infrastructure.Mapping;

internal static class OutboxItemMapper
{
    public static OutboxDynamoDbItem ToItem(OutboxEntry entry)
    {
        string pk = $"{DynamoDbConstants.OutboxKeyPrefix}{entry.Id}";

        return new OutboxDynamoDbItem
        {
            Pk = pk,
            Sk = pk,
            Id = entry.Id.ToString(),
            TargetTopic = entry.TargetTopic,
            MessageJson = entry.MessageJson,
            Status = entry.Status.ToString(),
            CreatedAt = entry.CreatedAt.ToString("O"),
            RetryCount = entry.RetryCount,
            GsiOutboxStatusPk = $"{DynamoDbConstants.OutboxStatusPrefix}{entry.Status}"
        };
    }

    public static OutboxEntry FromItem(OutboxDynamoDbItem item)
    {
        OutboxStatus status = Enum.Parse<OutboxStatus>(item.Status);
        DateTimeOffset createdAt = DateTimeOffset.Parse(item.CreatedAt, CultureInfo.InvariantCulture);

        return OutboxEntry.Reconstitute(
            id: Guid.Parse(item.Id),
            targetTopic: item.TargetTopic,
            messageJson: item.MessageJson,
            status: status,
            createdAt: createdAt,
            retryCount: item.RetryCount);
    }
}
