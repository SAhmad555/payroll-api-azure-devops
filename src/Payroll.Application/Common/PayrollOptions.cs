using Payroll.Domain.Pay;

namespace Payroll.Application.Common;

/// <summary>Configurable payroll defaults (section <c>Payroll</c> in appsettings).</summary>
public sealed class PayrollOptions
{
    public const string SectionName = "Payroll";

    /// <summary>Umbrella margin retained per weekly pay period (sample value).</summary>
    public decimal WeeklyUmbrellaMargin { get; set; } = 18m;

    /// <summary>Umbrella margin retained per monthly pay period (sample value).</summary>
    public decimal MonthlyUmbrellaMargin { get; set; } = 78m;

    public decimal MarginFor(PayFrequency frequency) =>
        frequency == PayFrequency.Weekly ? WeeklyUmbrellaMargin : MonthlyUmbrellaMargin;
}
