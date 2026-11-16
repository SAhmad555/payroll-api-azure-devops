using Microsoft.EntityFrameworkCore;
using Payroll.Infrastructure.Persistence;

namespace Payroll.Api.Infrastructure;

/// <summary>
/// Demo-friendly schema creation + fictional seed data, controlled by
/// <c>Database:InitializeOnStartup</c> and <c>Database:SeedSampleData</c>.
/// <para>
/// A production system would ship versioned EF Core migrations (e.g. a migrations bundle run as a
/// pipeline step) instead of <c>EnsureCreated</c>; this keeps the portfolio demo zero-setup.
/// </para>
/// </summary>
internal static class DatabaseBootstrapper
{
    public static async Task InitialiseAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        if (!configuration.GetValue("Database:InitializeOnStartup", true))
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PayrollDbContext>();

        await db.Database.EnsureCreatedAsync();

        if (configuration.GetValue("Database:SeedSampleData", true))
        {
            await SampleDataSeeder.SeedAsync(db, scope.ServiceProvider.GetRequiredService<TimeProvider>());
        }

        logger.LogInformation("Database ready using provider {Provider}", db.Database.ProviderName);
    }
}
