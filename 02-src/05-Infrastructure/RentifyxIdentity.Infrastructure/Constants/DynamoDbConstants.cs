namespace RentifyxIdentity.Infrastructure.Constants;

internal static class DynamoDbConstants
{
    internal const string TableNameConfigKey = "AWS:DynamoDB:TableName";
    internal const string DefaultTableName = "rentifyx-identity";
    internal const string AuditLogTablePlaceholder = DefaultTableName;
}
