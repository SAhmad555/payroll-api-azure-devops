using Payroll.Domain.Pay;

namespace Payroll.Domain.Tests;

/// <summary>
/// Expected values are worked by hand from the illustrative rules in <see cref="TaxYearRules.Illustrative"/>
/// (monthly basic band £3,142, additional-rate threshold £10,429; weekly £725 / £2,407).
/// </summary>
public class IncomeTaxCalculatorTests
{
    private static readonly TaxYearRules Rules = TaxYearRules.Illustrative;

    private static IncomeTaxResult Calc(decimal gross, string code, PayFrequency frequency) =>
        IncomeTaxCalculator.Calculate(gross, TaxCode.Parse(code), frequency, Rules);

    [Fact]
    public void Basic_rate_monthly()
    {
        // 3000 − 1047.50 allowance = 1952.50 → 1952 taxable × 20%
        var result = Calc(3_000m, "1257L", PayFrequency.Monthly);

        Assert.Equal(1_952m, result.TaxablePay);
        Assert.Equal(390.40m, result.Tax);
    }

    [Fact]
    public void Basic_rate_weekly()
    {
        // 500 − 241.73 = 258.27 → 258 × 20%
        var result = Calc(500m, "1257L", PayFrequency.Weekly);

        Assert.Equal(258m, result.TaxablePay);
        Assert.Equal(51.60m, result.Tax);
    }

    [Fact]
    public void Higher_rate_monthly_splits_across_bands()
    {
        // taxable 4952: 3142 × 20% = 628.40, 1810 × 40% = 724.00
        Assert.Equal(1_352.40m, Calc(6_000m, "1257L", PayFrequency.Monthly).Tax);
    }

    [Fact]
    public void Additional_rate_monthly_with_no_allowance()
    {
        // taxable 15000: 628.40 + (10429 − 3142) × 40% = 2914.80 + (15000 − 10429) × 45% = 2056.95
        Assert.Equal(5_600.15m, Calc(15_000m, "0T", PayFrequency.Monthly).Tax);
    }

    [Fact]
    public void Pay_below_personal_allowance_is_not_taxed()
    {
        var result = Calc(1_000m, "1257L", PayFrequency.Monthly);

        Assert.Equal(0m, result.TaxablePay);
        Assert.Equal(0m, result.Tax);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void Zero_or_negative_pay_produces_no_tax(decimal gross)
    {
        Assert.Equal(0m, Calc(gross, "BR", PayFrequency.Weekly).Tax);
    }

    [Theory]
    [InlineData("BR", 812.34, 162.40)]   // taxable pay rounded down to whole pounds first
    [InlineData("D0", 1000, 400.00)]
    [InlineData("D1", 1000, 450.00)]
    [InlineData("NT", 1000, 0)]
    public void Flat_rate_and_no_tax_codes(string code, decimal gross, decimal expectedTax)
    {
        Assert.Equal(expectedTax, Calc(gross, code, PayFrequency.Weekly).Tax);
    }

    [Fact]
    public void K_code_adds_to_taxable_pay()
    {
        // K100 monthly: 2000 + 83.33 = 2083.33 → 2083 × 20%
        var result = Calc(2_000m, "K100", PayFrequency.Monthly);

        Assert.Equal(2_083m, result.TaxablePay);
        Assert.Equal(416.60m, result.Tax);
    }

    [Fact]
    public void K_code_tax_is_capped_at_half_of_gross_pay()
    {
        // K5000 would give tax of 1238.00 on £500 pay; overriding limit caps it at 50%.
        Assert.Equal(250.00m, Calc(500m, "K5000", PayFrequency.Monthly).Tax);
    }

    [Fact]
    public void Tax_is_rounded_down_to_the_penny()
    {
        // BR on £1 = 0.20 exactly; on £7 = 1.40. Use a case that produces a fraction: weekly 1257L on £300.
        // 300 − 241.7307 = 58.27 → 58 × 20% = 11.60 (no fraction leaks through because taxable pay is whole pounds)
        var result = Calc(300m, "1257L", PayFrequency.Weekly);

        Assert.Equal(11.60m, result.Tax);
        Assert.Equal(result.Tax, Math.Round(result.Tax, 2));
    }
}
