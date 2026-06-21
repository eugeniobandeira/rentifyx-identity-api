using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Features.Examples.Handlers.Create;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace RentifyxIdentity.IoC;

internal static class ApplicationDependencyInjection
{
    internal static IServiceCollection Register(IServiceCollection services)
    {
        Assembly assembly = typeof(CreateExampleHandler).Assembly;

        services.AddValidatorsFromAssembly(assembly);

        assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHandler<,>))
                .Select(i => (Implementation: t, Interface: i)))
            .ToList()
            .ForEach(x => services.AddScoped(x.Interface, x.Implementation));

        return services;
    }
}
