namespace RentifyxIdentity.Infrastructure.Constants;

internal static class DynamoDbConstants
{
    internal const string TableNameConfigKey = "AWS:DynamoDB:TableName";
    internal const string DefaultTableName = "rentifyx-identity";
    internal const string AuditLogTablePlaceholder = DefaultTableName;

    internal const string UserKeyPrefix = "USER#";
    internal const string AuditKeyPrefix = "AUDIT#";

    internal const string GsiEmail = "GSI_Email";
    internal const string GsiTaxId = "GSI_TaxId";
    internal const string EmailAttribute = "Email";
    internal const string TaxIdAttribute = "TaxId";

    internal const int PendingVerificationTtlHours = 48;
    internal const int AuditLogRetentionDays = 90;
}
