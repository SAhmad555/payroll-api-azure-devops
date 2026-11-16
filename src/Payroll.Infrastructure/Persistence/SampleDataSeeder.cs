using Microsoft.EntityFrameworkCore;
using Payroll.Domain.Contractors;
using Payroll.Domain.Pay;
using Payroll.Domain.Timesheets;

namespace Payroll.Infrastructure.Persistence;

/// <summary>
/// Seeds <b>entirely fictional</b> demo data. Names are invented; NI numbers use HMRC's reserved
/// "QQ" example prefix and e-mail addresses use the reserved <c>example.com</c> domain.
/// </summary>
public static class SampleDataSeeder
{
    public static readonly Guid AvaId = Guid.Parse("8d3c0a52-1f4e-4b8a-9a52-0d2c6b1f7a01");
    public static readonly Guid RaviId = Guid.Parse("8d3c0a52-1f4e-4b8a-9a52-0d2c6b1f7a02");
    public static readonly Guid ElenaId = Guid.Parse("8d3c0a52-1f4e-4b8a-9a52-0d2c6b1f7a03");

    public static async Task SeedAsync(PayrollDbContext db, TimeProvider clock, CancellationToken cancellationToken = default)
    {
        if (await db.Contractors.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = clock.GetUtcNow();

        db.Contractors.AddRange(
            new Contractor(AvaId, "UMB-0001", "Ava", "Thornbury", "ava.thornbury@example.com", "QQ123456A", "1257L", 32.50m, PayFrequency.Weekly, now),
            new Contractor(RaviId, "UMB-0002", "Ravi", "Castellan", "ravi.castellan@example.com", "QQ234567B", "1257L", 58.00m, PayFrequency.Monthly, now),
            new Contractor(ElenaId, "UMB-0003", "Elena", "Marchetti", "elena.marchetti@example.com", "QQ345678C", "BR", 41.75m, PayFrequency.Weekly, now));

        // A submitted week for Ava, ready to approve and pay in the demo walkthrough.
        var monday = new DateOnly(2026, 11, 2);
        var timesheet = new Timesheet(Guid.Parse("4b1e9f3a-6c2d-4e7f-8a1b-3c5d7e9f0a11"), AvaId, monday, monday.AddDays(6), now);
        for (var day = 0; day < 5; day++)
        {
            timesheet.AddEntry(monday.AddDays(day), 7.5m, HoursType.Standard);
        }

        timesheet.AddEntry(monday.AddDays(5), 4m, HoursType.Overtime);
        timesheet.Submit(now);
        db.Timesheets.Add(timesheet);

        await db.SaveChangesAsync(cancellationToken);
    }
}
