using SkiaSharp;
using Svg.Skia;

namespace TorchlightSDK.Host.Icons;

internal static class SvgRasterizer
{
    public static DecodedImage Rasterize(string svg, int pixels)
    {
        using var document = new SKSvg();
        var picture = document.FromSvg(svg) ?? throw new InvalidDataException("not an SVG");
        var bounds = picture.CullRect;
        if (document.Model is not { Commands.Count: > 0 } || bounds.Width <= 0 || bounds.Height <= 0) throw new InvalidDataException("the SVG has nothing to draw");

        using var bitmap = new SKBitmap(new SKImageInfo(pixels, pixels, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Translate(pixels / 2f, pixels / 2f);
            canvas.Scale(pixels / MathF.Max(bounds.Width, bounds.Height));
            canvas.Translate(-bounds.MidX, -bounds.MidY);
            canvas.DrawPicture(picture);
        }
        return new DecodedImage(pixels, pixels, bitmap.Bytes);
    }
}
