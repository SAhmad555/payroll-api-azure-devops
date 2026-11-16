using Payroll.Domain.Pay;

namespace Payroll.Domain.Tests;

public class NationalInsuranceCalculatorTests
{
    private static readonly TaxYearRules Rules = TaxYearRules.Illustrative;

    [Theory]
    [InlineData(0, 0)]
    [InlineData(200, 0)]          // below primary threshold (242)
    [InlineData(242, 0)]          // exactly at threshold
    [InlineData(243, 0.08)]       // £1 over → 8p
    [InlineData(500, 20.64)]      // (500 − 242) × 8%
    [InlineData(967, 58.00)]      // exactly at UEL
    [InlineData(1000, 58.66)]     // 58.00 + (1000 − 967) × 2%
    public void Employee_weekly(decimal gross, decimal expected)
    {
        Assert.Equal(expected, NationalInsuranceCalculator.Employee(gross, PayFrequency.Weekly, Rules));
    }

    [Theory]
    [InlineData(3_000, 156.16)]   // (3000 − 1048) × 8%
    [InlineData(6_000, 287.50)]   // (4189 − 1048) × 8% = 251.28 + (6000 − 4189) × 2% = 36.22
    public void Employee_monthly(decimal gross, decimal expected)
    {
        Assert.Equal(expected, NationalInsuranceCalculator.Employee(gross, PayFrequency.Monthly, Rules));
    }

    [Fact]
    public void Employee_NI_is_rounded_down_to_the_penny()
    {
        // (250.99 − 242) × 8% = 0.7192 → 0.71
        Assert.Equal(0.71m, NationalInsuranceCalculator.Employee(250.99m, PayFrequency.Weekly, Rules));
    }

    [Theory]
    [InlineData(96, 0)]
    [InlineData(500, 60.60)]      // (500 − 96) × 15%
    public void Employer_weekly(decimal gross, decimal expected)
    {
        Assert.Equal(expected, NationalInsuranceCalculator.Employer(gross, PayFrequency.Weekly, Rules));
    }
}
