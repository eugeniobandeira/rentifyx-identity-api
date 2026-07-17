using Amazon.DynamoDBv2.DataModel;
using RentifyxIdentity.Infrastructure.Constants;

namespace RentifyxIdentity.Infrastructure.Models;

[DynamoDBTable(DynamoDbConstants.DefaultTableName)]
public sealed class OutboxDynamoDbItem
{
    [DynamoDBHashKey("PK")]
    public string Pk { get; set; } = null!;

    [DynamoDBRangeKey("SK")]
    public string Sk { get; set; } = null!;

    public string Id { get; set; } = null!;
    public string TargetTopic { get; set; } = null!;
    public string MessageJson { get; set; } = null!;
    public string Status { get; set; } = null!;

    [DynamoDBGlobalSecondaryIndexRangeKey(DynamoDbConstants.GsiOutbox)]
    public string CreatedAt { get; set; } = null!;

    public int RetryCount { get; set; }

    [DynamoDBGlobalSecondaryIndexHashKey(DynamoDbConstants.GsiOutbox)]
    [DynamoDBProperty("GsiOutboxStatusPk")]
    public string GsiOutboxStatusPk { get; set; } = null!;
}
