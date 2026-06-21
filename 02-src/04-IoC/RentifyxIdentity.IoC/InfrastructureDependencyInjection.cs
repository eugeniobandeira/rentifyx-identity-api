using RentifyxIdentity.Domain.Interfaces.Common;
using RentifyxIdentity.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace RentifyxIdentity.IoC;

internal static class InfrastructureDependencyInjection
{
    internal static IServiceCollection Register(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddRepositories();

        return services;
    }

    private static IServiceCollection AddRepositories(this IServiceCollection services)
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

        return services;
    }
}
