namespace RentifyxIdentity.Domain.Constants;

public static class UserErrorCodes
{
    public const string NotFound = "User.NotFound";
    public const string EmailAlreadyRegistered = "User.EmailAlreadyRegistered";
    public const string TaxIdAlreadyRegistered = "User.TaxIdAlreadyRegistered";
    public const string InvalidCredentials = "User.InvalidCredentials";
    public const string AccountNotActive = "User.AccountNotActive";
    public const string AccountNotVerified = "User.AccountNotVerified";
    public const string TokenInvalidOrExpired = "User.TokenInvalidOrExpired";
    public const string AccountNotVerifiable = "User.AccountNotVerifiable";
    public const string AlreadyDeleted = "User.AlreadyDeleted";
}
