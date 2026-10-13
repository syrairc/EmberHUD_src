using System.Drawing;

namespace EmberHUD.Host.Icons;

internal static class SvgRecolor
{
    public static string Apply(string svg, Color colour, Color accent)
    {
        var start = RootStart(svg);
        if (start < 0) return svg;
        var end = svg.IndexOf('>', start);
        if (end < 0 || svg[end - 1] == '/') return svg;
        return string.Concat(svg.AsSpan(0, end + 1), $"<style>svg{{color:#{Hex(colour)}}}.accent{{color:#{Hex(accent)}}}</style>", svg.AsSpan(end + 1));
    }

    static int RootStart(string svg)
    {
        for (var at = svg.IndexOf("<svg", StringComparison.Ordinal); at >= 0; at = svg.IndexOf("<svg", at + 4, StringComparison.Ordinal))
            if (at + 4 < svg.Length && (char.IsWhiteSpace(svg[at + 4]) || svg[at + 4] is '>' or '/')) return at;
        return -1;
    }

    static string Hex(Color colour) => $"{colour.R:X2}{colour.G:X2}{colour.B:X2}";
}
