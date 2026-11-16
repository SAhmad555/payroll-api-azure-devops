using Payroll.Domain.Common;

namespace Payroll.Domain.Pay;

public sealed record IncomeTaxResult(decimal TaxablePay, decimal Tax);

/// <summary>
/// Simplified PAYE income tax on a <b>non-cumulative (week 1 / month 1)</b> basis.
/// Illustrative only — see <see cref="TaxYearRules"/>.
/// </summary>
public static class IncomeTaxCalculator
{
    public static IncomeTaxResult Calculate(decimal grossPay, TaxCode taxCode, PayFrequency frequency, TaxYearRules rules)
    {
        ArgumentNullException.ThrowIfNull(taxCode);
        ArgumentNullException.ThrowIfNull(rules);

        if (grossPay <= 0m || taxCode.Kind == TaxCodeKind.NoTax)
        {
            return new IncomeTaxResult(0m, 0m);
        }

        if (taxCode.Kind == TaxCodeKind.FlatRate)
        {
            var flatTaxable = Money.RoundDownToPounds(grossPay);
            return new IncomeTaxResult(flatTaxable, Money.RoundDownToPence(flatTaxable * taxCode.FlatRate!.Value));
        }

        var periods = frequency.PeriodsPerYear();

        // For K codes AnnualAllowance is negative, so subtracting it *adds* to taxable pay.
        var periodAllowance = taxCode.AnnualAllowance / periods;
        var taxablePay = Math.Max(0m, Money.RoundDownToPounds(grossPay - periodAllowance));

        // Period band limits are rounded UP to whole pounds (as published payroll tables do), which also
        // keeps every intermediate figure exact in decimal arithmetic.
        var basicBand = Math.Ceiling(rules.BasicRateBand / periods);
        var additionalThreshold = Math.Ceiling(rules.AdditionalRateThreshold / periods);

        var basicPortion = Math.Min(taxablePay, basicBand);
        var higherPortion = Math.Clamp(taxablePay - basicBand, 0m, additionalThreshold - basicBand);
        var additionalPortion = Math.Max(0m, taxablePay - additionalThreshold);

        var tax = basicPortion * rules.BasicRate
                + higherPortion * rules.HigherRate
                + additionalPortion * rules.AdditionalRate;

        if (taxCode.Kind == TaxCodeKind.KCode)
        {
            tax = Math.Min(tax, grossPay * rules.KCodeOverridingLimit);
        }

        return new IncomeTaxResult(taxablePay, Money.RoundDownToPence(tax));
    }
}
