using System.Globalization;
using Amazon.DynamoDBv2.DataModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RentifyxIdentity.Domain.Interfaces.Users;
using RentifyxIdentity.Infrastructure.Constants;
using RentifyxIdentity.Infrastructure.Models;

namespace RentifyxIdentity.Infrastructure.Services;

public sealed class AuditLogService(
    IDynamoDBContext context,
    IConfiguration configuration,
    ILogger<AuditLogService> logger) : IAuditLogService
{
    public async Task LogAsync(Guid userId, string eventType, CancellationToken ct = default)
    {
        string tableName = configuration[DynamoDbConstants.TableNameConfigKey]
            ?? throw new InvalidOperationException($"{DynamoDbConstants.TableNameConfigKey} is not configured.");

        DateTimeOffset now = DateTimeOffset.UtcNow;
        string pk = $"AUDIT#{userId}#{now:yyyyMMddHHmmss}_{Guid.NewGuid()}";

        AuditLogEntry entry = new()
        {
            Pk = pk,
            Sk = pk,
            UserId = userId.ToString(),
            EventType = eventType,
            OccurredAt = now.ToString("O", CultureInfo.InvariantCulture),
            Ttl = now.AddDays(90).ToUnixTimeSeconds()
        };

        await context.SaveAsync(
            entry,
            new SaveConfig { OverrideTableName = tableName },
            ct);

        logger.LogInformation("Audit log written. UserId={UserId} EventType={EventType}", userId, eventType);
    }
}
