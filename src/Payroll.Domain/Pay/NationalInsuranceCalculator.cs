using Payroll.Domain.Common;

namespace Payroll.Domain.Pay;

/// <summary>
/// Simplified Class 1 National Insurance (category A only). Illustrative only — see <see cref="TaxYearRules"/>.
/// </summary>
public static class NationalInsuranceCalculator
{
    /// <summary>Employee (primary) contribution for one pay period.</summary>
    public static decimal Employee(decimal grossPay, PayFrequency frequency, TaxYearRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var t = rules.NiThresholdsFor(frequency);

        var mainBand = Math.Max(0m, Math.Min(grossPay, t.UpperEarningsLimit) - t.PrimaryThreshold);
        var upperBand = Math.Max(0m, grossPay - t.UpperEarningsLimit);

        return Money.RoundDownToPence(mainBand * rules.EmployeeMainRate + upperBand * rules.EmployeeUpperRate);
    }

    /// <summary>Employer (secondary) contribution for one pay period.</summary>
    public static decimal Employer(decimal grossPay, PayFrequency frequency, TaxYearRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var t = rules.NiThresholdsFor(frequency);

        return Money.RoundDownToPence(Math.Max(0m, grossPay - t.SecondaryThreshold) * rules.EmployerRate);
    }
}
