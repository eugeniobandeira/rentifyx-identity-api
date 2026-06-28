using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        services.AddRepositories();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();

        // Authentication: no default scheme configured here — Cognito JWT bearer wired in E-04.
        // Testing environment overrides this via CustomWebApplicationFactory + TestAuthHandler.
        services.AddAuthentication();
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
