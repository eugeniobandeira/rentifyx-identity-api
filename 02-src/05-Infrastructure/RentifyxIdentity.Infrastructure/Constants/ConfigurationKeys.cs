namespace RentifyxIdentity.Infrastructure.Constants;

internal static class ConfigurationKeys
{
    internal const string JwtPrivateKeyPem = "Jwt:PrivateKeyPem";
    internal const string JwtIssuer = "Jwt:Issuer";
    internal const string JwtAudience = "Jwt:Audience";
    internal const string HmacKey = "Hmac:Key";
    internal const string SesFromAddress = "Ses:FromAddress";
    internal const string AwsRegion = "AWS:Region";
    internal const string AwsSecretsManagerSecretName = "AWS:SecretsManager:SecretName";
    internal const string LocalStackEnabled = "LocalStack:UseLocalStack";
    internal const string LocalStackHost = "LocalStack:Config:LocalStackHost";
    internal const string LocalStackEdgePort = "LocalStack:Config:EdgePort";

    internal const string DefaultAwsRegion = "sa-east-1";
    internal const string DefaultLocalStackHost = "localhost";
    internal const string DefaultLocalStackEdgePort = "4566";
    internal const string LocalStackTestAccessKey = "test";
    internal const string LocalStackTestSecretKey = "test";
    internal const string TestingEnvironment = "Testing";
}
