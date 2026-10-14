namespace EmberHUD.Tests;

public class TitleMatchTests
{
    [Theory]
    [InlineData("Torchlight: Infinite", "Torchlight: Infinite", true)]
    [InlineData("Torchlight: Infinite", "Torchlight: Infinite  ", true)]
    [InlineData("Torchlight", "Torchlight: Infinite  ", true)]
    [InlineData("Torchlight: Infinite", "Torchlight", false)]
    [InlineData("Torchlight: Infinite", "Notepad", false)]
    [InlineData("Torchlight: Infinite", " Torchlight: Infinite", false)]
    [InlineData("Torchlight: Infinite", "torchlight: infinite", false)]
    [InlineData(null, "anything", true)]
    [InlineData("", "anything", true)]
    [InlineData(null, "", true)]
    [InlineData("x", "", false)]
    public void MatchesByOrdinalPrefix(string? wanted, string actual, bool expected)
    {
        Assert.Equal(expected, Win32TargetSource.TitleMatches(wanted, actual));
    }
}
