using Amazon;
using Amazon.Runtime;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Configuration;
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

        if (string.Equals(env, "Testing", StringComparison.OrdinalIgnoreCase))
            return;

        string resolvedEnv = env ?? "Development";

        string secretNameTemplate = _bootstrapConfig["AWS:SecretsManager:SecretName"] ?? string.Empty;
        string secretName = secretNameTemplate.Replace("{environment}", resolvedEnv, StringComparison.OrdinalIgnoreCase);

        AmazonSecretsManagerConfig clientConfig = new()
        {
            RegionEndpoint = RegionEndpoint.SAEast1
        };

        bool useLocalStack = string.Equals(
            _bootstrapConfig["LocalStack:UseLocalStack"],
            "true",
            StringComparison.OrdinalIgnoreCase);

        try
        {
            AmazonSecretsManagerClient client;

            if (useLocalStack)
            {
                string host = _bootstrapConfig["LocalStack:Config:LocalStackHost"] ?? "localhost";
                string port = _bootstrapConfig["LocalStack:Config:EdgePort"] ?? "4566";
                clientConfig.ServiceURL = $"http://{host}:{port}";
                client = new AmazonSecretsManagerClient(
                    new BasicAWSCredentials("test", "test"),
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
            // Secret not yet seeded in local dev — skip silently
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
