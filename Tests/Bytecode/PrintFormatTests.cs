using Bytecode;

namespace Tests.Bytecode;

public class PrintFormatTests
{
    [Theory]
    [InlineData(7, "7")]
    [InlineData(0, "0")]
    [InlineData(-42, "-42")]
    [InlineData(long.MaxValue, "9223372036854775807")]
    [InlineData(long.MinValue, "-9223372036854775808")]
    public void IntMatchesPrintfLld(long value, string expected)
    {
        Assert.Equal(expected, PrintFormat.Int(value));
    }

    [Theory]
    [InlineData(7.0, "7")]
    [InlineData(3.75, "3.75")]
    [InlineData(0.3333333333333333, "0.333333")]
    [InlineData(1e20, "1e+20")]
    [InlineData(1e-7, "1e-07")]
    [InlineData(-0.0, "-0")]
    [InlineData(1000000.0, "1e+06")]
    [InlineData(123456.0, "123456")]
    [InlineData(7.5, "7.5")]
    [InlineData(14.0, "14")]
    public void FloatMatchesPrintfG(double value, string expected)
    {
        Assert.Equal(expected, PrintFormat.Float(value));
    }
}
