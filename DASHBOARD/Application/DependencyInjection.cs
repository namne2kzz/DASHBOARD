using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using DASHBOARD.Application.Common.Behaviors;

namespace DASHBOARD.Application;

/// <summary>Extension methods for registering Application layer services into the DI container.</summary>
public static class DependencyInjection
{
    /// <summary>Registers MediatR (with pipeline behaviors), FluentValidation validators, and AutoMapper profiles from the Application assembly.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.AddAutoMapper(cfg => cfg.AddMaps(assembly));

        return services;
    }
}
