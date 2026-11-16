using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Payroll.Application.Abstractions;
using Payroll.Infrastructure.Persistence;

namespace Payroll.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Payroll";

    /// <summary>
    /// Registers EF Core. Provider is chosen by <c>Database:Provider</c>:
    /// <c>Sqlite</c> (default, local dev/tests) or <c>SqlServer</c> (Azure SQL in the cloud, using
    /// managed identity via <c>Authentication=Active Directory Default</c> in the connection string).
    /// Configuration is read lazily so test hosts can override it.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<PayrollDbContext>((sp, options) =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var provider = configuration["Database:Provider"] ?? "Sqlite";
            var connectionString = configuration.GetConnectionString(ConnectionStringName) ?? "Data Source=payroll.db";

            if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(maxRetryCount: 5));
            }
            else
            {
                options.UseSqlite(connectionString);
            }
        });

        services.AddScoped<IPayrollDbContext>(sp => sp.GetRequiredService<PayrollDbContext>());

        return services;
    }
}
