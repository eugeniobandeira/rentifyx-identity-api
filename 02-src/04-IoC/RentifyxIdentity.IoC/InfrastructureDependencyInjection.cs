using System.Reflection;
using System.Security.Cryptography;
using Amazon.DynamoDBv2;
using Amazon.SimpleEmailV2;
using LocalStack.Client.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using RentifyxIdentity.Domain.Interfaces.Common;
using RentifyxIdentity.Domain.Interfaces.Users;
using RentifyxIdentity.Infrastructure;
using RentifyxIdentity.Infrastructure.Repositories;
using RentifyxIdentity.Infrastructure.Services;

namespace RentifyxIdentity.IoC;

internal static class InfrastructureDependencyInjection
{
    internal static IServiceCollection Register(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddLocalStack(configuration);
        services.AddAwsService<IAmazonDynamoDB>();
        services.AddAwsService<IAmazonSimpleEmailServiceV2>();

        services.AddRepositories();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();

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

    private static void AddRepositories(this IServiceCollection services)
    {
        Assembly assembly = typeof(InfrastructureAssemblyMarker).Assembly;

        assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .Where(t => t.GetInterfaces().Any(i => i.IsGenericType &&
                       (i.GetGenericTypeDefinition() == typeof(IRepository<>) ||
                        i.GetGenericTypeDefinition() == typeof(IRepository<,>))))
            .ToList()
            .ForEach(repositoryType =>
            {
                services.AddScoped(repositoryType);

                repositoryType.GetInterfaces()
                    .Where(i => i.IsGenericType &&
                               (i.GetGenericTypeDefinition() == typeof(IRepository<>) ||
                                i.GetGenericTypeDefinition() == typeof(IRepository<,>)))
                    .ToList()
                    .ForEach(iface => services.AddScoped(iface, sp => sp.GetRequiredService(repositoryType)));
            });
    }
}
