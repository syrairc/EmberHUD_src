using EmberHUD.Ui;

namespace EmberHUD.Api.Tests;

public class NumbersTests
{
    [Fact]
    public void CompactUsesThreeSignificantDigits()
    {
        Assert.Equal("0", Numbers.Compact(0));
        Assert.Equal("999", Numbers.Compact(999));
        Assert.Equal("1.00K", Numbers.Compact(1000));
        Assert.Equal("12.3K", Numbers.Compact(12_345));
        Assert.Equal("26.8M", Numbers.Compact(26_767_917));
        Assert.Equal("5.00T", Numbers.Compact(5_000_000_000_000));
        Assert.Equal("7.25T", Numbers.Compact(7_250_000_000_000));
        Assert.Equal("123Qa", Numbers.Compact(123_000_000_000_000_000));
        Assert.Equal("1.00M", Numbers.Compact(999_600));
    }
}
