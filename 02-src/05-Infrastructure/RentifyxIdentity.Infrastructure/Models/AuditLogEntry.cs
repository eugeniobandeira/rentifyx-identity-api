using Amazon.DynamoDBv2.DataModel;

namespace RentifyxIdentity.Infrastructure.Models;

[DynamoDBTable("placeholder")]
public sealed class AuditLogEntry
{
    [DynamoDBHashKey("PK")]
    public string Pk { get; set; } = null!;

    [DynamoDBRangeKey("SK")]
    public string Sk { get; set; } = null!;

    public string UserId { get; set; } = null!;
    public string EventType { get; set; } = null!;
    public string OccurredAt { get; set; } = null!;

    [DynamoDBProperty("TTL")]
    public long Ttl { get; set; }
}
