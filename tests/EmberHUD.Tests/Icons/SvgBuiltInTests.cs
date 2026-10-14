using System.Drawing;
using EmberHUD.Host.Icons;

namespace EmberHUD.Tests;

public sealed class SvgBuiltInTests
{
    static readonly Dictionary<string, string> BuiltIns = SvgIcons.BuiltIns(typeof(SvgIconStore).Assembly).ToDictionary(icon => icon.Name, icon => icon.Svg);

    static DecodedImage Render(string name, Color colour, Color accent) => SvgIcons.Rasterize(SvgIcons.Recolor(BuiltIns[name], colour, accent), 64);

    [Fact]
    public void TheBuiltInSetIsQuestPortalGemAndElite() => Assert.Equal(["elite", "gem", "portal", "quest"], BuiltIns.Keys.Order().ToArray());

    [Theory]
    [InlineData("quest")]
    [InlineData("portal")]
    [InlineData("gem")]
    [InlineData("elite")]
    public void EachRendersSolidInTheMiddleAndClearInTheCorner(string name)
    {
        var image = Render(name, Color.Red, Color.Blue);

        Assert.Null(SvgIcons.ExternalReference(BuiltIns[name]));
        Assert.Contains("viewBox=\"0 0 64 64\"", BuiltIns[name]);
        Assert.Equal(255, image.Rgba[(32 * 64 + 32) * 4 + 3]);
        Assert.Equal(0, image.Rgba[3]);
        Assert.Equal(0, image.Rgba[(63 * 64 + 63) * 4 + 3]);
    }

    [Theory]
    [InlineData("quest")]
    [InlineData("portal")]
    [InlineData("gem")]
    [InlineData("elite")]
    public void BothTheColourAndTheAccentChangeThePixels(string name)
    {
        var baseline = Render(name, Color.Red, Color.Blue).Rgba;

        Assert.NotEqual(baseline, Render(name, Color.Lime, Color.Blue).Rgba);
        Assert.NotEqual(baseline, Render(name, Color.Red, Color.Yellow).Rgba);
    }
}
