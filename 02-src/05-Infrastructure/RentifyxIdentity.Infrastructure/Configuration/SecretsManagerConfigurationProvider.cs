using Amazon;
using Amazon.Runtime;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Configuration;
using RentifyxIdentity.Infrastructure.Constants;

namespace RentifyxIdentity.Infrastructure.Configuration;

internal sealed class SecretsManagerConfigurationProvider : ConfigurationProvider
{
    private readonly IConfiguration _bootstrapConfig;

    public SecretsManagerConfigurationProvider(IConfiguration bootstrapConfig)
    {
        _bootstrapConfig = bootstrapConfig;
    }

    public override void Load()
    {
        string? env = _bootstrapConfig["environment"]
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

        if (string.Equals(env, ConfigurationKeys.TestingEnvironment, StringComparison.OrdinalIgnoreCase))
            return;

        string resolvedEnv = (env ?? "Development").ToLowerInvariant();
        string region = _bootstrapConfig[ConfigurationKeys.AwsRegion] ?? ConfigurationKeys.DefaultAwsRegion;
        AmazonSecretsManagerConfig clientConfig = new()
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(region)
        };

        using AmazonSecretsManagerClient client = new(clientConfig);

        // One Secrets Manager entry per credential, not a combined JSON blob -
        // each fetched/failed independently so one missing/unreadable secret
        // doesn't block the other, and each value is inspectable directly via
        // `aws secretsmanager get-secret-value` with no JSON unwrapping.
        LoadSecret(client, ConfigurationKeys.AwsSecretsManagerJwtPrivateKeySecretName, resolvedEnv, ConfigurationKeys.JwtPrivateKeyPem);
        LoadSecret(client, ConfigurationKeys.AwsSecretsManagerHmacKeySecretName, resolvedEnv, ConfigurationKeys.HmacKey);
    }

    private void LoadSecret(AmazonSecretsManagerClient client, string secretNameConfigKey, string resolvedEnv, string dataKey)
    {
        string secretNameTemplate = _bootstrapConfig[secretNameConfigKey] ?? string.Empty;
        if (secretNameTemplate.Length == 0)
            return;

        string secretName = secretNameTemplate.Replace("{environment}", resolvedEnv, StringComparison.OrdinalIgnoreCase);

        try
        {
            GetSecretValueResponse response = client.GetSecretValueAsync(new GetSecretValueRequest
            {
                SecretId = secretName
            }).GetAwaiter().GetResult();

            if (response.SecretString is not null)
                Data[dataKey] = response.SecretString;
        }
        catch (ResourceNotFoundException)
        {
            // Secret not yet seeded — skip silently
        }
        catch (Exception ex) when (
            ex is DecryptionFailureException
            or InternalServiceErrorException
            or AmazonServiceException)
        {
            // Write to stderr so it surfaces in docker logs without crashing the app
            Console.Error.WriteLine($"[SecretsManager] Failed to load secret '{secretName}': {ex.GetType().Name}: {ex.Message}");
        }
    }
}

internal sealed class SecretsManagerConfigurationSource : IConfigurationSource
{
    private readonly IConfiguration _bootstrapConfig;

    public SecretsManagerConfigurationSource(IConfiguration bootstrapConfig)
    {
        _bootstrapConfig = bootstrapConfig;
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
        => new SecretsManagerConfigurationProvider(_bootstrapConfig);
}

public static class SecretsManagerConfigurationExtensions
{
    public static IConfigurationBuilder AddSecretsManager(
        this IConfigurationBuilder builder,
        IConfiguration bootstrapConfig)
    {
        builder.Add(new SecretsManagerConfigurationSource(bootstrapConfig));
        return builder;
    }
}
