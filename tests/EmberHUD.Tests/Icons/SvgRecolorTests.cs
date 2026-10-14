using System.Drawing;
using EmberHUD.Host.Icons;

namespace EmberHUD.Tests;

public sealed class SvgRecolorTests
{
    const string TwoTone = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64"><rect x="2" y="2" width="20" height="20" fill="currentColor"/><rect class="accent" x="42" y="2" width="20" height="20" fill="currentColor"/><g class="accent"><line x1="24" y1="40" x2="40" y2="40" stroke="currentColor" stroke-width="4"/></g></svg>""";

    static (int R, int G, int B, int A) At(DecodedImage image, int x, int y)
    {
        var i = (y * image.Width + x) * 4;
        return (image.Rgba[i], image.Rgba[i + 1], image.Rgba[i + 2], image.Rgba[i + 3]);
    }

    [Fact]
    public void TheStyleGoesRightAfterTheRootStartTag() =>
        Assert.Equal(
            """<?xml version="1.0"?><!-- art --><svg xmlns="http://www.w3.org/2000/svg" color="#00FF00"><style>svg{color:#FFD700}.accent{color:#000080}</style><rect/></svg>""",
            SvgIcons.Recolor("""<?xml version="1.0"?><!-- art --><svg xmlns="http://www.w3.org/2000/svg" color="#00FF00"><rect/></svg>""", Color.Gold, Color.Navy));

    [Fact]
    public void AlphaIsIgnored() =>
        Assert.Contains("svg{color:#FF0000}.accent{color:#0000FF}", SvgIcons.Recolor("<svg><rect/></svg>", Color.FromArgb(10, 255, 0, 0), Color.FromArgb(0, 0, 0, 255)));

    [Theory]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("""<svg xmlns="http://www.w3.org/2000/svg"/>""")]
    [InlineData("<svgfoo></svgfoo>")]
    public void TextWithoutARootStartTagIsReturnedAsIs(string text) => Assert.Equal(text, SvgIcons.Recolor(text, Color.Red, Color.Blue));

    [Fact]
    public void TheColourAndAccentReachThePixels()
    {
        var image = SvgIcons.Rasterize(SvgIcons.Recolor(TwoTone, Color.Red, Color.Blue), 64);

        Assert.Equal((255, 0, 0, 255), At(image, 12, 12));
        Assert.Equal((0, 0, 255, 255), At(image, 52, 12));
        Assert.Equal((0, 0, 255, 255), At(image, 32, 40));
    }

    [Fact]
    public void TheInjectedColourBeatsAColourAttributeOnTheRoot()
    {
        var image = SvgIcons.Rasterize(SvgIcons.Recolor(TwoTone.Replace("<svg ", """<svg color="#00FF00" """), Color.Red, Color.Blue), 64);

        Assert.Equal((255, 0, 0, 255), At(image, 12, 12));
    }
}
