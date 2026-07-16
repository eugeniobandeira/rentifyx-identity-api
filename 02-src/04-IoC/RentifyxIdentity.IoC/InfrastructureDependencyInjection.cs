using System.Security.Cryptography;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Extensions.NETCore.Setup;
using Amazon.SimpleEmailV2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using RentifyxIdentity.Domain.Interfaces.Notifications;
using RentifyxIdentity.Domain.Interfaces.Users;
using RentifyxIdentity.Infrastructure.Messaging;
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
        services.AddAWSService<IAmazonSimpleEmailServiceV2>();
        services.AddSingleton<IDynamoDBContext>(sp =>
            new DynamoDBContextBuilder()
                .WithDynamoDBClient(() => sp.GetRequiredService<IAmazonDynamoDB>())
                .Build());

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddSingleton<IKafkaProducerFactory, KafkaProducerFactory>();
        services.AddScoped<IEmailService, EmailService>();
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
