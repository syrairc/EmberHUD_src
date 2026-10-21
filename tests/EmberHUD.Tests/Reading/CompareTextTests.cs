using System.Globalization;
using EmberHUD.Host.Reading;

namespace EmberHUD.Tests;

public sealed class CompareTextTests
{
    [Theory]
    [InlineData("+16.4%", 16.4f)]
    [InlineData("-4.4%", -4.4f)]
    [InlineData("0%", 0f)]
    [InlineData("+3.0%", 3f)]
    [InlineData("999%", 999f)]
    [InlineData(" +9.5% ", 9.5f)]
    [InlineData("10000%", 10000f)]
    [InlineData("-10000%", -10000f)]
    public void ParsesPercentText(string text, float expected)
    {
        Assert.Equal(expected, CompareText.Parse(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("%")]
    [InlineData("+-3%")]
    [InlineData("1e9%")]
    [InlineData("3")]
    [InlineData("NaN%")]
    [InlineData("10001%")]
    [InlineData("-10001%")]
    public void NonPercentOrOutOfRangeIsNull(string text)
    {
        Assert.Null(CompareText.Parse(text));
    }

    [Fact]
    public void CommaDecimalCultureDoesNotChangeTheResult()
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal(9.5f, CompareText.Parse("+9.5%"));
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }
}
