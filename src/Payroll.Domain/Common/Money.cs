namespace Payroll.Domain.Common;

/// <summary>Rounding helpers for GBP amounts. Deductions are always rounded <b>down</b> to the penny.</summary>
public static class Money
{
    /// <summary>Truncates to whole pence (e.g. 12.3499 → 12.34). Used for tax and NI deductions.</summary>
    public static decimal RoundDownToPence(decimal amount) => Math.Round(amount, 2, MidpointRounding.ToZero);

    /// <summary>Truncates to whole pounds (e.g. 1234.99 → 1234). Used for taxable pay.</summary>
    public static decimal RoundDownToPounds(decimal amount) => Math.Round(amount, 0, MidpointRounding.ToZero);

    /// <summary>Banker-free conventional rounding to pence (e.g. 0.125 → 0.13). Used for earnings.</summary>
    public static decimal RoundToPence(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
