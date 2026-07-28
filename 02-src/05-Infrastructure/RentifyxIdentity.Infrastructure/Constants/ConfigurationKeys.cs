namespace RentifyxIdentity.Infrastructure.Constants;

internal static class ConfigurationKeys
{
    internal const string JwtPrivateKeyPem = "Jwt:PrivateKeyPem";
    internal const string JwtIssuer = "Jwt:Issuer";
    internal const string JwtAudience = "Jwt:Audience";
    internal const string HmacKey = "Hmac:Key";
    internal const string AwsRegion = "AWS:Region";

    // One Secrets Manager entry per credential, not a combined JSON blob -
    // each secret's value is the raw string (PEM/key), inspectable directly
    // via `aws secretsmanager get-secret-value` with no JSON unwrapping.
    internal const string AwsSecretsManagerJwtPrivateKeySecretName = "AWS:SecretsManager:JwtPrivateKeySecretName";
    internal const string AwsSecretsManagerHmacKeySecretName = "AWS:SecretsManager:HmacKeySecretName";

    internal const string DefaultAwsRegion = "sa-east-1";
    internal const string TestingEnvironment = "Testing";
}
