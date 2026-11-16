namespace Payroll.Domain.Pay;

public enum PayFrequency
{
    Weekly = 1,
    Monthly = 2,
}

public static class PayFrequencyExtensions
{
    /// <summary>Number of pay periods in a tax year (simplified: 52 weeks / 12 months).</summary>
    public static int PeriodsPerYear(this PayFrequency frequency) => frequency switch
    {
        PayFrequency.Weekly => 52,
        PayFrequency.Monthly => 12,
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported pay frequency."),
    };
}
