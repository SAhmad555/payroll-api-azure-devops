using System.Globalization;
using System.Text.RegularExpressions;
using Payroll.Domain.Common;

namespace Payroll.Domain.Pay;

public enum TaxCodeKind
{
    /// <summary>Numeric code with a personal allowance, e.g. <c>1257L</c> or <c>0T</c>.</summary>
    Allowance,

    /// <summary>K code: a negative allowance that is <i>added</i> to taxable pay, e.g. <c>K100</c>.</summary>
    KCode,

    /// <summary>All pay taxed at a single flat rate (<c>BR</c>, <c>D0</c>, <c>D1</c>).</summary>
    FlatRate,

    /// <summary><c>NT</c> — no tax deducted.</summary>
    NoTax,
}

/// <summary>
/// A parsed PAYE tax code. <b>Simplified for demonstration:</b> Scottish (S) and Welsh (C) prefixes
/// are not supported, and emergency suffixes (W1/M1/X) are accepted but ignored because this demo
/// always calculates on a non-cumulative (week 1 / month 1) basis.
/// </summary>
public sealed partial record TaxCode
{
    private TaxCode(string code, TaxCodeKind kind, decimal annualAllowance, decimal? flatRate)
    {
        Code = code;
        Kind = kind;
        AnnualAllowance = annualAllowance;
        FlatRate = flatRate;
    }

    public string Code { get; }
    public TaxCodeKind Kind { get; }

    /// <summary>Annual tax-free allowance. Negative for K codes.</summary>
    public decimal AnnualAllowance { get; }

    /// <summary>Flat rate for BR/D0/D1 codes; <c>null</c> otherwise.</summary>
    public decimal? FlatRate { get; }

    public static bool TryParse(string? value, out TaxCode? taxCode)
    {
        try
        {
            taxCode = Parse(value);
            return true;
        }
        catch (DomainException)
        {
            taxCode = null;
            return false;
        }
    }

    public static TaxCode Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw Invalid(value);
        }

        var normalised = EmergencySuffix().Replace(value.Trim().ToUpperInvariant(), string.Empty).Trim();

        if (normalised.Length > 1 && normalised[0] is 'S' or 'C')
        {
            throw new DomainException(
                "taxcode.unsupported_region",
                $"Tax code '{value}' uses a Scottish/Welsh prefix, which this demo does not support.");
        }

        switch (normalised)
        {
            case "BR": return new TaxCode(normalised, TaxCodeKind.FlatRate, 0m, 0.20m);
            case "D0": return new TaxCode(normalised, TaxCodeKind.FlatRate, 0m, 0.40m);
            case "D1": return new TaxCode(normalised, TaxCodeKind.FlatRate, 0m, 0.45m);
            case "NT": return new TaxCode(normalised, TaxCodeKind.NoTax, 0m, null);
        }

        var allowance = AllowanceCode().Match(normalised);
        if (allowance.Success)
        {
            var number = int.Parse(allowance.Groups["n"].Value, CultureInfo.InvariantCulture);
            return new TaxCode(normalised, TaxCodeKind.Allowance, number * 10m, null);
        }

        var kCode = KCode().Match(normalised);
        if (kCode.Success)
        {
            var number = int.Parse(kCode.Groups["n"].Value, CultureInfo.InvariantCulture);
            return new TaxCode(normalised, TaxCodeKind.KCode, -number * 10m, null);
        }

        throw Invalid(value);
    }

    public override string ToString() => Code;

    private static DomainException Invalid(string? value) =>
        new("taxcode.invalid", $"'{value}' is not a supported tax code. Examples: 1257L, 0T, K100, BR, D0, D1, NT.");

    [GeneratedRegex(@"^(?<n>\d{1,5})[LMNT]$", RegexOptions.CultureInvariant)]
    private static partial Regex AllowanceCode();

    [GeneratedRegex(@"^K(?<n>\d{1,5})$", RegexOptions.CultureInvariant)]
    private static partial Regex KCode();

    [GeneratedRegex(@"\s*(W1|M1|X)$", RegexOptions.CultureInvariant)]
    private static partial Regex EmergencySuffix();
}
