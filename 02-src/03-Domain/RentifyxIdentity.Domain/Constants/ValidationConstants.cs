namespace RentifyxIdentity.Domain.Constants;

public static class ValidationConstants
{
    public static class ExampleRules
    {
        public const int NameMaxLength = 100;
        public const int DescriptionMaxLength = 500;
    }

    public static class UserRules
    {
        public const int EmailMaxLength = 320;
        public const int PasswordMinLength = 12;
        public const int PasswordMaxLength = 128;
        public const int TokenMaxLength = 512;
    }
}
