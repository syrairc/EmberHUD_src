using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using EmberHUD.Host.Plugins;
using SkiaSharp;
using Svg.Skia;

namespace EmberHUD.Host.Icons;

internal static class SvgIcons
{
    static readonly SourceLog Log = EmberHUD.Log.For("Icons");

    const string ResourcePrefix = "icon:";

    static readonly Regex External = new("""@import|(?:xlink:)?href\s*=\s*["'](?!\s*#)|url\(\s*["']?(?!\s*["']?#)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static IEnumerable<(string Name, string Svg)> BuiltIns(Assembly host) =>
        host.GetManifestResourceNames().Where(resource => resource.StartsWith(ResourcePrefix, StringComparison.Ordinal)).Select(resource => (resource[ResourcePrefix.Length..], Read(host, resource)));

    public static Dictionary<string, string> Load(IEnumerable<(string Name, string Svg)> builtIns, string overrideDirectory, string pluginsDirectory)
    {
        var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, svg) in builtIns) Add(sources, Draw.SvgIconPrefix + name, svg, $"built-in {name}");
        AddFolder(sources, overrideDirectory, "");
        foreach (var plugin in PluginCompiler.Plugins(pluginsDirectory)) AddFolder(sources, Path.Combine(plugin, "Icons"), Path.GetFileName(plugin) + "/");
        return sources;
    }

    public static string? ExternalReference(string svg) => External.Match(svg) is { Success: true } match ? match.Value : null;

    public static string Recolor(string svg, Color colour, Color accent)
    {
        var start = RootStart(svg);
        if (start < 0) return svg;
        var end = svg.IndexOf('>', start);
        if (end < 0 || svg[end - 1] == '/') return svg;
        return string.Concat(svg.AsSpan(0, end + 1), $"<style>svg{{color:#{Hex(colour)}}}.accent{{color:#{Hex(accent)}}}</style>", svg.AsSpan(end + 1));
    }

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

    public static string WritePreviewSheet(string directory, IReadOnlyDictionary<string, string> sources)
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
                    var svg = Recolor(sources[names[row]], GameColors.MinimapMarker, GameColors.MinimapIconAccent);
                    for (var column = 0; column < columns; column++)
                    {
                        var size = sizes[column % sizes.Length];
                        var image = Rasterize(svg, size);
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

    static string Read(Assembly host, string resource)
    {
        using var reader = new StreamReader(host.GetManifestResourceStream(resource)!);
        return reader.ReadToEnd();
    }

    static void AddFolder(Dictionary<string, string> sources, string directory, string prefix)
    {
        if (!Directory.Exists(directory)) return;
        string[] files;
        try
        {
            files = Directory.GetFiles(directory, "*.svg");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn($"svg icon folder {directory} skipped - {ex.Message}");
            return;
        }
        foreach (var file in files)
        {
            try
            {
                if (new FileInfo(file).Length > 256 * 1024) Log.Warn($"svg icon {file} skipped - over 256 KB");
                else Add(sources, Draw.SvgIconPrefix + prefix + Path.GetFileNameWithoutExtension(file), File.ReadAllText(file), file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Warn($"svg icon {file} skipped - {ex.Message}");
            }
        }
    }

    static void Add(Dictionary<string, string> sources, string key, string svg, string origin)
    {
        if (ExternalReference(svg) is { } reference) Log.Warn($"svg icon {origin} skipped - external reference {reference}");
        else sources[key] = svg;
    }

    static int RootStart(string svg)
    {
        for (var at = svg.IndexOf("<svg", StringComparison.Ordinal); at >= 0; at = svg.IndexOf("<svg", at + 4, StringComparison.Ordinal))
            if (at + 4 < svg.Length && (char.IsWhiteSpace(svg[at + 4]) || svg[at + 4] is '>' or '/')) return at;
        return -1;
    }

    static string Hex(Color colour) => $"{colour.R:X2}{colour.G:X2}{colour.B:X2}";
}
