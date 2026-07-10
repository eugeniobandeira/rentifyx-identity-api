using Amazon;
using Amazon.Runtime;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Configuration;
using RentifyxIdentity.Infrastructure.Constants;
using System.Text.Json;

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

        string secretNameTemplate = _bootstrapConfig[ConfigurationKeys.AwsSecretsManagerSecretName] ?? string.Empty;
        string secretName = secretNameTemplate.Replace("{environment}", resolvedEnv, StringComparison.OrdinalIgnoreCase);

        string region = _bootstrapConfig[ConfigurationKeys.AwsRegion] ?? ConfigurationKeys.DefaultAwsRegion;
        AmazonSecretsManagerConfig clientConfig = new()
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(region)
        };

        bool useLocalStack = string.Equals(
            _bootstrapConfig[ConfigurationKeys.LocalStackEnabled],
            "true",
            StringComparison.OrdinalIgnoreCase);

        try
        {
            AmazonSecretsManagerClient client;

            if (useLocalStack)
            {
                string host = _bootstrapConfig[ConfigurationKeys.LocalStackHost] ?? ConfigurationKeys.DefaultLocalStackHost;
                string port = _bootstrapConfig[ConfigurationKeys.LocalStackEdgePort] ?? ConfigurationKeys.DefaultLocalStackEdgePort;
                clientConfig.ServiceURL = $"http://{host}:{port}";
                client = new AmazonSecretsManagerClient(
                    new BasicAWSCredentials(ConfigurationKeys.LocalStackTestAccessKey, ConfigurationKeys.LocalStackTestSecretKey),
                    clientConfig);
            }
            else
            {
                client = new AmazonSecretsManagerClient(clientConfig);
            }

            using (client)
            {
                GetSecretValueResponse response = client.GetSecretValueAsync(new GetSecretValueRequest
                {
                    SecretId = secretName
                }).GetAwaiter().GetResult();

                if (response.SecretString is null)
                    return;

                Dictionary<string, string>? secrets =
                    JsonSerializer.Deserialize<Dictionary<string, string>>(response.SecretString);

                if (secrets is not null)
                    Data = secrets;
            }
        }
        catch (ResourceNotFoundException)
        {
            // Secret not yet seeded — skip silently
        }
        catch (Exception ex) when (
            ex is DecryptionFailureException
            or InternalServiceErrorException
            or AmazonServiceException
            or JsonException)
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
