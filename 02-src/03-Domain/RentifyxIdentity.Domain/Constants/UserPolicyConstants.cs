namespace RentifyxIdentity.Domain.Constants;

public static class UserPolicyConstants
{
    public const int MaxFailedLoginAttempts = 5;
    public const int LockoutDurationMinutes = 15;
}
