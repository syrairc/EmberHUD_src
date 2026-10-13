using System.Drawing;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace EmberHUD.Host.Icons;

internal static class IconSheet
{
    public static string Write(string directory, IReadOnlyDictionary<string, string> sources)
    {
        const int cell = 72;
        int[] sizes = [16, 32, 64];
        SKColor[] backgrounds = [new(0x1B, 0x22, 0x30), new(0xD9, 0xD9, 0xD9)];
        var names = sources.Keys.Order(StringComparer.OrdinalIgnoreCase).ToList();
        var columns = sizes.Length * backgrounds.Length;

        using var sheet = new SKBitmap(new SKImageInfo(cell * columns, cell * Math.Max(1, names.Count), SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using (var canvas = new SKCanvas(sheet))
        {
            for (var column = 0; column < columns; column++)
            {
                using var paint = new SKPaint { Color = backgrounds[column / sizes.Length] };
                canvas.DrawRect(column * cell, 0, cell, sheet.Height, paint);
            }

            for (var row = 0; row < names.Count; row++)
            {
                try
                {
                    var svg = SvgRecolor.Apply(sources[names[row]], Color.FromArgb(0xF9, 0xDD, 0x75), Color.FromArgb(0x1A, 0x1A, 0x1A));
                    for (var column = 0; column < columns; column++)
                    {
                        var size = sizes[column % sizes.Length];
                        var image = SvgRasterizer.Rasterize(svg, size);
                        using var icon = new SKBitmap(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Unpremul));
                        Marshal.Copy(image.Rgba, 0, icon.GetPixels(), image.Rgba.Length);
                        canvas.DrawBitmap(icon, column * cell + (cell - size) / 2f, row * cell + (cell - size) / 2f, new SKSamplingOptions());
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn($"icon sheet - {names[row]} - {ex.Message}");
                }
            }
        }

        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "icons.png");
        using var png = sheet.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, png.ToArray());
        return path;
    }
}
