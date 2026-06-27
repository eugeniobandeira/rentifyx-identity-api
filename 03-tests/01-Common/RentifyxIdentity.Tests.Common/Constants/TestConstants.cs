namespace RentifyxIdentity.Tests.Common.Constants;

public static class TestConstants
{
    // Auth
    public const string ValidEmail = "user@example.com";
    public const string ValidPassword = "P@ssword123!";
    public static readonly string[] ValidRoles = ["Owner", "Renter", "Admin"];

    // HTTP Headers
    public const string CorrelationIdHeader = "X-Correlation-Id";

    // Configuration
    public const string HmacKey = "test-hmac-key-for-integration-tests!!";
    public const string HmacKeyConfigPath = "Hmac:Key";

    // Passwords (invalid scenarios)
    public const string PasswordTooShort = "Short1!";      // 7 chars — fails min length
    public const string PasswordNoUpper = "p@ssword123!";  // no uppercase — fails complexity

    // Emails (invalid scenarios)
    public const string DisposableDomainEmail = "user@mailinator.com";

    // TaxId scenarios
    public const string TaxIdCpfFormatted = "529.982.247-25"; // valid format, 11 digits after stripping (no mod-11)
    public const string TaxIdCpfRaw = "52998224725";

    // Entity status strings (must match UserStatus enum names)
    public const string StatusPendingVerification = "PendingVerification";

    // Validation error codes
    public const string ValidationErrorCodeNotEmpty = "NotEmpty";

    // Conflict error titles (must match handler description strings)
    public const string EmailAlreadyRegisteredTitle = "This email address is already registered.";
    public const string TaxIdAlreadyRegisteredTitle = "This Tax ID is already registered.";
}
