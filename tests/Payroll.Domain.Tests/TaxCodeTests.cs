using Payroll.Domain.Common;
using Payroll.Domain.Pay;

namespace Payroll.Domain.Tests;

public class TaxCodeTests
{
    [Theory]
    [InlineData("1257L", TaxCodeKind.Allowance, 12_570)]
    [InlineData("1257l", TaxCodeKind.Allowance, 12_570)]
    [InlineData(" 1257L ", TaxCodeKind.Allowance, 12_570)]
    [InlineData("1257M", TaxCodeKind.Allowance, 12_570)]
    [InlineData("0T", TaxCodeKind.Allowance, 0)]
    [InlineData("K100", TaxCodeKind.KCode, -1_000)]
    [InlineData("NT", TaxCodeKind.NoTax, 0)]
    [InlineData("BR", TaxCodeKind.FlatRate, 0)]
    public void Parse_recognises_supported_codes(string input, TaxCodeKind expectedKind, decimal expectedAllowance)
    {
        var code = TaxCode.Parse(input);

        Assert.Equal(expectedKind, code.Kind);
        Assert.Equal(expectedAllowance, code.AnnualAllowance);
    }

    [Theory]
    [InlineData("BR", 0.20)]
    [InlineData("D0", 0.40)]
    [InlineData("D1", 0.45)]
    public void Flat_rate_codes_carry_their_rate(string input, decimal expectedRate)
    {
        Assert.Equal(expectedRate, TaxCode.Parse(input).FlatRate);
    }

    [Theory]
    [InlineData("1257L W1")]
    [InlineData("1257LM1")]
    [InlineData("1257L X")]
    public void Emergency_suffixes_are_accepted_and_ignored(string input)
    {
        Assert.Equal("1257L", TaxCode.Parse(input).Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("ABC")]
    [InlineData("1257")]
    [InlineData("1257Z")]
    [InlineData("K")]
    [InlineData("D2")]
    public void Parse_rejects_invalid_codes(string? input)
    {
        var ex = Assert.Throws<DomainException>(() => TaxCode.Parse(input));
        Assert.Equal("taxcode.invalid", ex.Code);
    }

    [Theory]
    [InlineData("S1257L")]
    [InlineData("C1257L")]
    [InlineData("SBR")]
    public void Scottish_and_Welsh_codes_are_explicitly_unsupported(string input)
    {
        var ex = Assert.Throws<DomainException>(() => TaxCode.Parse(input));
        Assert.Equal("taxcode.unsupported_region", ex.Code);
    }

    [Fact]
    public void TryParse_returns_false_instead_of_throwing()
    {
        Assert.False(TaxCode.TryParse("nonsense", out var code));
        Assert.Null(code);
    }
}
