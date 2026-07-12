namespace RentifyxIdentity.Domain.Constants;

public static class AuditEvents
{
    public const string ProfileAccessed = "PROFILE_ACCESSED";
    public const string DataExported = "DATA_EXPORTED";
    public const string AccountDeleted = "ACCOUNT_DELETED";
    public const string UserLoggedIn = "USER_LOGGED_IN";
    public const string EssentialConsentGranted = "CONSENT_ESSENTIAL_GRANTED";
    public const string EssentialConsentRevoked = "CONSENT_ESSENTIAL_REVOKED";
    public const string MarketingConsentGranted = "CONSENT_MARKETING_GRANTED";
    public const string MarketingConsentRevoked = "CONSENT_MARKETING_REVOKED";
}
