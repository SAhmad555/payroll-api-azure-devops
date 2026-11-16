using Microsoft.EntityFrameworkCore;
using Payroll.Domain.Contractors;
using Payroll.Domain.Pay;
using Payroll.Domain.Timesheets;

namespace Payroll.Application.Abstractions;

/// <summary>
/// Persistence port. Exposing <see cref="DbSet{TEntity}"/> is a deliberate, pragmatic trade-off:
/// it keeps handlers simple and LINQ-friendly while the concrete provider stays in Infrastructure.
/// </summary>
public interface IPayrollDbContext
{
    DbSet<Contractor> Contractors { get; }
    DbSet<Timesheet> Timesheets { get; }
    DbSet<Payslip> Payslips { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
