using Payroll.Domain.Common;
using Payroll.Domain.Pay;

namespace Payroll.Domain.Tests;

public class PayCalculatorTests
{
    private static readonly TaxYearRules Rules = TaxYearRules.Illustrative;

    [Fact]
    public void Full_weekly_gross_to_net_walkthrough()
    {
        // 37.5 std h @ £32.50 + 4 OT h @ £48.75 = £1,413.75 assignment income
        var input = new PayInput(37.5m, 4m, 32.50m, UmbrellaMargin: 18m, TaxCode.Parse("1257L"), PayFrequency.Weekly);

        var result = PayCalculator.Calculate(input, Rules);

        Assert.Equal(48.75m, result.OvertimeRate);
        Assert.Equal(1_413.75m, result.AssignmentIncome);
        Assert.Equal(1_226.21m, result.GrossPay);                 // (1395.75 + 96 × 15%) / 1.15, rounded down
        Assert.Equal(169.54m, result.EmployerNationalInsurance);  // balancing figure
        Assert.Equal(248.60m, result.IncomeTax);                  // 725 × 20% + 259 × 40%
        Assert.Equal(63.18m, result.EmployeeNationalInsurance);   // 58.00 + 259.21 × 2%
        Assert.Equal(914.43m, result.NetPay);
    }

    [Fact]
    public void Funds_always_reconcile_to_assignment_income()
    {
        var input = new PayInput(40m, 7.25m, 27.13m, UmbrellaMargin: 18m, TaxCode.Parse("1257L"), PayFrequency.Weekly);

        var r = PayCalculator.Calculate(input, Rules);

        Assert.Equal(r.AssignmentIncome, r.UmbrellaMargin + r.EmployerNationalInsurance + r.GrossPay);
        Assert.Equal(r.GrossPay, r.NetPay + r.IncomeTax + r.EmployeeNationalInsurance);
    }

    [Fact]
    public void Zero_hours_produces_a_zero_payslip()
    {
        var r = PayCalculator.Calculate(new PayInput(0m, 0m, 25m, 18m, TaxCode.Parse("1257L"), PayFrequency.Weekly), Rules);

        Assert.Equal(0m, r.AssignmentIncome);
        Assert.Equal(0m, r.UmbrellaMargin); // margin can't exceed income
        Assert.Equal(0m, r.GrossPay);
        Assert.Equal(0m, r.NetPay);
    }

    [Fact]
    public void Fractional_hours_round_income_to_the_penny()
    {
        // 7.33 h × £13.37 = 98.0021 → 98.00
        var r = PayCalculator.Calculate(new PayInput(7.33m, 0m, 13.37m, 0m, TaxCode.Parse("NT"), PayFrequency.Weekly), Rules);

        Assert.Equal(98.00m, r.AssignmentIncome);
    }

    [Fact]
    public void Negative_hours_are_rejected()
    {
        var input = new PayInput(-1m, 0m, 20m, 0m, TaxCode.Parse("1257L"), PayFrequency.Weekly);
        var ex = Assert.Throws<DomainException>(() => PayCalculator.Calculate(input, Rules));
        Assert.Equal("pay.negative_hours", ex.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Non_positive_rate_is_rejected(decimal rate)
    {
        var input = new PayInput(10m, 0m, rate, 0m, TaxCode.Parse("1257L"), PayFrequency.Weekly);
        Assert.Throws<DomainException>(() => PayCalculator.Calculate(input, Rules));
    }

    [Fact]
    public void Breakdown_reports_the_illustrative_tax_year()
    {
        var r = PayCalculator.Calculate(new PayInput(1m, 0m, 10m, 0m, TaxCode.Parse("BR"), PayFrequency.Monthly), Rules);
        Assert.Contains("illustrative", r.TaxYear, StringComparison.OrdinalIgnoreCase);
    }
}
