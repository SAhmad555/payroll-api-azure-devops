using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Payroll.Application.Abstractions;
using Payroll.Domain.Contractors;
using Payroll.Domain.Pay;
using Payroll.Domain.Timesheets;

namespace Payroll.Infrastructure.Persistence;

public sealed class PayrollDbContext(DbContextOptions<PayrollDbContext> options) : DbContext(options), IPayrollDbContext
{
    public DbSet<Contractor> Contractors => Set<Contractor>();
    public DbSet<Timesheet> Timesheets => Set<Timesheet>();
    public DbSet<Payslip> Payslips => Set<Payslip>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PayrollDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Money: explicit precision so SQL Server never silently truncates.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);

        // Enums as readable strings in the database.
        configurationBuilder.Properties<PayFrequency>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<TimesheetStatus>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<HoursType>().HaveConversion<string>().HaveMaxLength(16);

        // SQLite has no native DateTimeOffset; store as sortable binary ticks there. SQL Server keeps datetimeoffset.
        if (Database.IsSqlite())
        {
            configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
        }
    }
}
