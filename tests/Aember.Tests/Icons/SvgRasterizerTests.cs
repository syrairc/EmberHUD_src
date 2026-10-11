using Aember.Host.Icons;

namespace Aember.Tests;

public sealed class SvgRasterizerTests
{
    const string Effects = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64">
          <defs>
            <linearGradient id="g" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#FFFFFF"/><stop offset="1" stop-color="#000000"/></linearGradient>
            <filter id="blur"><feGaussianBlur stdDeviation="2"/></filter>
            <filter id="shadow"><feDropShadow dx="2" dy="2" stdDeviation="1" flood-color="#000000"/></filter>
            <mask id="m"><rect x="0" y="0" width="64" height="32" fill="white"/></mask>
            <pattern id="p" width="4" height="4" patternUnits="userSpaceOnUse"><rect width="2" height="2" fill="#00FF00"/></pattern>
          </defs>
          <rect x="2" y="2" width="20" height="20" fill="#FF0000"/>
          <rect x="2" y="42" width="20" height="20" fill="url(#g)" filter="url(#shadow)"/>
          <circle cx="52" cy="52" r="8" fill="#FFFF00" filter="url(#blur)"/>
          <rect x="24" y="24" width="16" height="16" fill="url(#p)" mask="url(#m)"/>
        </svg>
        """;

    static (int R, int G, int B, int A) At(DecodedImage image, float x, float y)
    {
        var i = ((int)(y * image.Height / 64) * image.Width + (int)(x * image.Width / 64)) * 4;
        return (image.Rgba[i], image.Rgba[i + 1], image.Rgba[i + 2], image.Rgba[i + 3]);
    }

    [Fact]
    public void EveryEffectTheIconsUseRenders()
    {
        var image = SvgRasterizer.Rasterize(Effects, 128);

        Assert.Equal((255, 0, 0, 255), At(image, 12, 12));
        Assert.True(At(image, 12, 43).R > 200, "gradient top is light");
        Assert.True(At(image, 12, 61).R < 40, "gradient bottom is dark");
        var shadow = At(image, 23, 50);
        Assert.True(shadow.A > 0 && shadow.R < 60, $"drop shadow right of the rect {shadow}");
        var blurEdge = At(image, 60, 52);
        Assert.True(blurEdge.A is > 20 and < 235, $"blurred circle edge is partly transparent {blurEdge}");
        Assert.Equal((0, 255, 0, 255), At(image, 25, 25));
        Assert.Equal(0, At(image, 27, 25).A);
        Assert.Equal(0, At(image, 25, 37).A);
        Assert.Equal(0, At(image, 40, 2).A);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(256)]
    public void TheImageIsTheRequestedSquare(int pixels)
    {
        var image = SvgRasterizer.Rasterize(Effects, pixels);

        Assert.Equal(pixels, image.Width);
        Assert.Equal(pixels, image.Height);
        Assert.Equal(pixels * pixels * 4, image.Rgba.Length);
    }

    [Fact]
    public void AWideViewBoxFitsItsLongSideAndIsCentred()
    {
        var image = SvgRasterizer.Rasterize("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 32"><rect x="0" y="0" width="64" height="32" fill="#FF0000"/></svg>""", 64);

        Assert.Equal(0, At(image, 32, 8).A);
        Assert.Equal((255, 0, 0, 255), At(image, 1, 32));
        Assert.Equal((255, 0, 0, 255), At(image, 62, 32));
        Assert.Equal(0, At(image, 32, 56).A);
    }

    [Fact]
    public void MalformedOrEmptySvgThrows()
    {
        Assert.ThrowsAny<Exception>(() => SvgRasterizer.Rasterize("<svg><rect", 32));
        Assert.ThrowsAny<Exception>(() => SvgRasterizer.Rasterize("hello", 32));
        Assert.Throws<InvalidDataException>(() => SvgRasterizer.Rasterize("""<svg xmlns="http://www.w3.org/2000/svg"/>""", 32));
    }
}
