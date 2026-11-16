using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Payroll.Application.Abstractions;
using Payroll.Application.Common;

namespace Payroll.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.AddOptions<PayrollOptions>().BindConfiguration(PayrollOptions.SectionName);
        services.AddSingleton(TimeProvider.System);

        // Register every ICommandHandler<,> / IQueryHandler<,> implementation against its interface.
        var handlerInterfaces = new[] { typeof(ICommandHandler<,>), typeof(IQueryHandler<,>) };
        var registrations =
            from type in assembly.GetTypes()
            where type is { IsClass: true, IsAbstract: false }
            from contract in type.GetInterfaces()
            where contract.IsGenericType && handlerInterfaces.Contains(contract.GetGenericTypeDefinition())
            select (contract, type);

        foreach (var (contract, type) in registrations)
        {
            services.AddScoped(contract, type);
        }

        return services;
    }
}
