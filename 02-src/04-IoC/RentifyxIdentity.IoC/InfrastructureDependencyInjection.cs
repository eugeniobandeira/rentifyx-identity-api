using System.Security.Cryptography;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RentifyxIdentity.Domain.Interfaces.Notifications;
using RentifyxIdentity.Domain.Interfaces.Users;
using RentifyxIdentity.Infrastructure.Messaging;
using RentifyxIdentity.Infrastructure.Options;
using RentifyxIdentity.Infrastructure.Repositories;
using RentifyxIdentity.Infrastructure.Services;

namespace RentifyxIdentity.IoC;

internal static class InfrastructureDependencyInjection
{
    internal static IServiceCollection Register(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAWSService<IAmazonDynamoDB>();
        services.AddSingleton<IDynamoDBContext>(sp =>
            new DynamoDBContextBuilder()
                .WithDynamoDBClient(() => sp.GetRequiredService<IAmazonDynamoDB>())
                .Build());

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddSingleton<IKafkaProducerFactory, KafkaProducerFactory>();
        // .Get<T>() (constructor-argument binding), not services.Configure<T>(section) - the latter relies on
        // Activator.CreateInstance<T>() before binding, which throws MissingMethodException for a record with
        // no parameterless constructor (every parameter here has a default value, but C# still doesn't emit a
        // real zero-arg constructor just because of that).
        services.AddSingleton<IOptions<OutboxPublisherOptions>>(sp => Options.Create(
            sp.GetRequiredService<IConfiguration>().GetSection("Outbox").Get<OutboxPublisherOptions>()
            ?? new OutboxPublisherOptions()));
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IAuditLogService, AuditLogService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                string? pem = configuration["Jwt:PrivateKeyPem"];

                TokenValidationParameters parameters = new()
                {
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                if (!string.IsNullOrWhiteSpace(pem))
                {
                    RSA rsa = RSA.Create();
                    rsa.ImportFromPem(pem.AsSpan());
                    parameters.IssuerSigningKey = new RsaSecurityKey(rsa);
                }

                options.TokenValidationParameters = parameters;
            });

        services.AddAuthorization();

        return services;
    }
}
