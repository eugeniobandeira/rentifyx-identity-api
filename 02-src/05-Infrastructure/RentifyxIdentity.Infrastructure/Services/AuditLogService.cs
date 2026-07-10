using System.Globalization;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RentifyxIdentity.Domain.Contracts;
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

        AuditLogEntry entry = new()
        {
            Pk = $"{DynamoDbConstants.AuditKeyPrefix}{userId}",
            Sk = $"{now:yyyyMMddHHmmss}_{Guid.NewGuid()}",
            UserId = userId.ToString(),
            EventType = eventType,
            OccurredAt = now.ToString("O", CultureInfo.InvariantCulture),
            Ttl = now.AddDays(DynamoDbConstants.AuditLogRetentionDays).ToUnixTimeSeconds()
        };

        await context.SaveAsync(
            entry,
            new SaveConfig { OverrideTableName = tableName },
            ct);

        logger.LogInformation("Audit log written. UserId={UserId} EventType={EventType}", userId, eventType);
    }

    public async Task<IReadOnlyList<AuditLogEntryRecord>> GetByUserIdAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        string tableName = configuration[DynamoDbConstants.TableNameConfigKey]
            ?? throw new InvalidOperationException($"{DynamoDbConstants.TableNameConfigKey} is not configured.");

        QueryOperationConfig config = new()
        {
            KeyExpression = new Expression
            {
                ExpressionStatement = "PK = :pk",
                ExpressionAttributeValues = { [":pk"] = $"{DynamoDbConstants.AuditKeyPrefix}{userId}" }
            }
        };

        List<AuditLogEntry> entries = await context
            .FromQueryAsync<AuditLogEntry>(config, new FromQueryConfig { OverrideTableName = tableName })
            .GetRemainingAsync(ct);

        return entries
            .OrderByDescending(e => e.OccurredAt)
            .Select(e => new AuditLogEntryRecord(
                e.EventType,
                DateTimeOffset.Parse(e.OccurredAt, CultureInfo.InvariantCulture)))
            .ToList();
    }
}
