using Payroll.Domain.Common;

namespace Payroll.Domain.Pay;

public sealed record UmbrellaFundsResult(decimal AvailableFunds, decimal EmployerNationalInsurance, decimal GrossPay);

/// <summary>
/// Umbrella-model gross-up: the agency pays the umbrella an <i>assignment income</i>; the umbrella
/// keeps its margin and must also fund employer NI out of what remains. What's left is gross salary.
/// <para>
/// Solves <c>funds = G + max(0, G − ST) × rate</c> for gross <c>G</c>. Simplified: no employer pension,
/// apprenticeship levy, holiday-pay retention or expenses. Illustrative only.
/// </para>
/// </summary>
public static class UmbrellaFundsCalculator
{
    public static UmbrellaFundsResult Calculate(
        decimal assignmentIncome,
        decimal umbrellaMargin,
        PayFrequency frequency,
        TaxYearRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        if (assignmentIncome < 0m)
        {
            throw new DomainException("umbrella.negative_income", "Assignment income cannot be negative.");
        }

        if (umbrellaMargin < 0m)
        {
            throw new DomainException("umbrella.negative_margin", "Umbrella margin cannot be negative.");
        }

        var funds = Math.Max(0m, assignmentIncome - umbrellaMargin);
        var st = rules.NiThresholdsFor(frequency).SecondaryThreshold;

        if (funds <= st)
        {
            return new UmbrellaFundsResult(funds, 0m, funds);
        }

        var gross = Money.RoundDownToPence((funds + st * rules.EmployerRate) / (1m + rules.EmployerRate));

        // Employer NI is the balancing figure so that gross + employer NI always equals available funds exactly.
        var employerNi = funds - gross;

        return new UmbrellaFundsResult(funds, employerNi, gross);
    }
}
