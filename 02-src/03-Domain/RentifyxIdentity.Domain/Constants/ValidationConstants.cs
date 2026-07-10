namespace RentifyxIdentity.Domain.Constants;

public static class ValidationConstants
{
    public static class UserRules
    {
        public const int EmailMaxLength = 320;
        public const int PasswordMinLength = 12;
        public const int PasswordMaxLength = 128;
        public const int TokenMaxLength = 512;
#pragma warning disable S2068
        public const string PasswordSymbols = "!@#$%^&*()-_=+[]{}|;:,.<>?";
#pragma warning restore S2068
    }
}
