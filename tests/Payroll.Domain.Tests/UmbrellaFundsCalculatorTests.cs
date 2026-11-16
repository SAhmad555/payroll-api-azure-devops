using Payroll.Domain.Common;
using Payroll.Domain.Pay;

namespace Payroll.Domain.Tests;

public class UmbrellaFundsCalculatorTests
{
    private static readonly TaxYearRules Rules = TaxYearRules.Illustrative;

    [Fact]
    public void Grosses_up_after_margin_and_employer_NI()
    {
        // funds 982; G = (982 + 14.40) / 1.15 = 866.4347… → 866.43
        var r = UmbrellaFundsCalculator.Calculate(1_000m, 18m, PayFrequency.Weekly, Rules);

        Assert.Equal(982m, r.AvailableFunds);
        Assert.Equal(866.43m, r.GrossPay);
        Assert.Equal(115.57m, r.EmployerNationalInsurance);
    }

    [Fact]
    public void Employer_NI_is_within_a_penny_of_the_statutory_formula()
    {
        var r = UmbrellaFundsCalculator.Calculate(1_000m, 18m, PayFrequency.Weekly, Rules);
        var formula = NationalInsuranceCalculator.Employer(r.GrossPay, PayFrequency.Weekly, Rules);

        Assert.InRange(r.EmployerNationalInsurance - formula, 0m, 0.01m);
    }

    [Fact]
    public void Funds_below_secondary_threshold_attract_no_employer_NI()
    {
        var r = UmbrellaFundsCalculator.Calculate(100m, 18m, PayFrequency.Weekly, Rules);

        Assert.Equal(82m, r.GrossPay);
        Assert.Equal(0m, r.EmployerNationalInsurance);
    }

    [Fact]
    public void Margin_larger_than_income_yields_zero_gross()
    {
        var r = UmbrellaFundsCalculator.Calculate(10m, 18m, PayFrequency.Weekly, Rules);

        Assert.Equal(0m, r.GrossPay);
        Assert.Equal(0m, r.EmployerNationalInsurance);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(100, -1)]
    public void Negative_inputs_are_rejected(decimal income, decimal margin)
    {
        Assert.Throws<DomainException>(() => UmbrellaFundsCalculator.Calculate(income, margin, PayFrequency.Weekly, Rules));
    }
}
