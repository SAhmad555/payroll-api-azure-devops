namespace Payroll.Domain.Pay;

/// <summary>Per-period National Insurance thresholds.</summary>
public sealed record NationalInsuranceThresholds(
    decimal PrimaryThreshold,
    decimal UpperEarningsLimit,
    decimal SecondaryThreshold);

/// <summary>
/// Rates and thresholds for one tax year.
/// <para>
/// <b>ILLUSTRATIVE ONLY — NOT TAX ADVICE.</b> The figures below resemble recent UK rest-of-UK
/// rates so the demo feels realistic, but they are hard-coded sample values. Real payroll software
/// must source rates from HMRC each year and handle many rules this demo deliberately omits
/// (cumulative tax, Scottish/Welsh bands, allowance tapering, student loans, pensions, statutory pay…).
/// </para>
/// </summary>
public sealed record TaxYearRules
{
    public required string TaxYear { get; init; }

    // ---- Income tax (annual figures, applied pro-rata per period) ----
    public required decimal BasicRate { get; init; }
    public required decimal HigherRate { get; init; }
    public required decimal AdditionalRate { get; init; }

    /// <summary>Width of the basic-rate band of <i>taxable</i> income.</summary>
    public required decimal BasicRateBand { get; init; }

    /// <summary>Taxable income above which the additional rate applies.</summary>
    public required decimal AdditionalRateThreshold { get; init; }

    /// <summary>K-code overriding limit: tax may not exceed this share of gross pay in a period.</summary>
    public required decimal KCodeOverridingLimit { get; init; }

    // ---- National Insurance (Class 1) ----
    public required decimal EmployeeMainRate { get; init; }
    public required decimal EmployeeUpperRate { get; init; }
    public required decimal EmployerRate { get; init; }
    public required NationalInsuranceThresholds Weekly { get; init; }
    public required NationalInsuranceThresholds Monthly { get; init; }

    public NationalInsuranceThresholds NiThresholdsFor(PayFrequency frequency) => frequency switch
    {
        PayFrequency.Weekly => Weekly,
        PayFrequency.Monthly => Monthly,
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported pay frequency."),
    };

    /// <summary>Illustrative sample rules used throughout the demo and its tests.</summary>
    public static TaxYearRules Illustrative { get; } = new()
    {
        TaxYear = "2026/27 (illustrative)",
        BasicRate = 0.20m,
        HigherRate = 0.40m,
        AdditionalRate = 0.45m,
        BasicRateBand = 37_700m,
        AdditionalRateThreshold = 125_140m,
        KCodeOverridingLimit = 0.50m,
        EmployeeMainRate = 0.08m,
        EmployeeUpperRate = 0.02m,
        EmployerRate = 0.15m,
        Weekly = new NationalInsuranceThresholds(PrimaryThreshold: 242m, UpperEarningsLimit: 967m, SecondaryThreshold: 96m),
        Monthly = new NationalInsuranceThresholds(PrimaryThreshold: 1_048m, UpperEarningsLimit: 4_189m, SecondaryThreshold: 417m),
    };
}
