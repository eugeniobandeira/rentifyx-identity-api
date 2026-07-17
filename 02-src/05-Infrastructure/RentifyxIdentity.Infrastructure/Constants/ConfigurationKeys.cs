namespace RentifyxIdentity.Infrastructure.Constants;

internal static class ConfigurationKeys
{
    internal const string JwtPrivateKeyPem = "Jwt:PrivateKeyPem";
    internal const string JwtIssuer = "Jwt:Issuer";
    internal const string JwtAudience = "Jwt:Audience";
    internal const string HmacKey = "Hmac:Key";
    internal const string AwsRegion = "AWS:Region";
    internal const string AwsSecretsManagerSecretName = "AWS:SecretsManager:SecretName";

    internal const string DefaultAwsRegion = "sa-east-1";
    internal const string TestingEnvironment = "Testing";
}
