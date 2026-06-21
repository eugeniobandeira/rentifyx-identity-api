using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RentifyxIdentity.IoC;

public static class DependencyInjectionExtension
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
        => ApplicationDependencyInjection.Register(services);

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        => InfrastructureDependencyInjection.Register(services, configuration);
}
