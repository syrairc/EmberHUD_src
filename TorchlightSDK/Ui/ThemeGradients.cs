using System.Numerics;
using Hexa.NET.ImGui;

namespace TorchlightSDK.Host.Ui;

internal readonly record struct GradientShade(uint Top, uint Bottom, uint Highlight, uint Text, bool HasHighlight, bool HasText);

internal readonly record struct GradientRun(int Start, int End, Vector2 Min, Vector2 Max, int Shade);

internal static class ThemeGradients
{
    const uint RgbMask = 0xFFFFFF;
    const uint AlphaMask = 0xFF000000;
    const uint Marker = 0x0201;

    public static uint SentinelRgb(int index) => Marker | (uint)index << 16;

    public static Vector4 Sentinel(int index, float alpha) => new(1 / 255f, 2 / 255f, index / 255f, alpha);

    public static int IndexOf(uint colour) => (colour & 0xFFFF) == Marker ? (int)(colour >> 16 & 0xFF) : -1;

    public static unsafe void Apply(ImDrawDataPtr data, ReadOnlySpan<GradientShade> shades, uint textRgb, List<GradientRun> runs)
    {
        if (data.IsNull || shades.IsEmpty) return;
        for (var n = 0; n < data.CmdListsCount; n++)
        {
            var list = data.CmdLists[n];
            Apply(new Span<ImDrawVert>(list.VtxBuffer.Data, list.VtxBuffer.Size), shades, textRgb, runs);
        }
    }

    public static void Apply(Span<ImDrawVert> vertices, ReadOnlySpan<GradientShade> shades, uint textRgb, List<GradientRun> runs)
    {
        runs.Clear();
        var anyText = false;
        for (var i = 0; i < vertices.Length;)
        {
            var shade = IndexOf(vertices[i].Col);
            if (shade < 0 || shade >= shades.Length)
            {
                i++;
                continue;
            }
            var rgb = vertices[i].Col & RgbMask;
            var start = i;
            var min = vertices[i].Pos;
            var max = min;
            for (; i < vertices.Length && (vertices[i].Col & RgbMask) == rgb; i++)
            {
                min = Vector2.Min(min, vertices[i].Pos);
                max = Vector2.Max(max, vertices[i].Pos);
            }
            runs.Add(new GradientRun(start, i, min, max, shade));
            anyText |= shades[shade].HasText;
        }

        foreach (var run in runs)
        {
            var shade = shades[run.Shade];
            var height = run.Max.Y - run.Min.Y;
            for (var i = run.Start; i < run.End; i++)
            {
                ref var vertex = ref vertices[i];
                var rgb = shade.HasHighlight && vertex.Pos.Y <= run.Min.Y + 1 ? shade.Highlight : Mix(shade.Top, shade.Bottom, height > 0 ? (vertex.Pos.Y - run.Min.Y) / height : 0);
                vertex.Col = (vertex.Col & AlphaMask) | rgb;
            }
        }

        if (!anyText) return;
        for (var i = 0; i < vertices.Length; i++)
        {
            ref var vertex = ref vertices[i];
            if ((vertex.Col & RgbMask) != textRgb) continue;
            for (var r = runs.Count - 1; r >= 0; r--)
            {
                var run = runs[r];
                if (vertex.Pos.X < run.Min.X || vertex.Pos.X > run.Max.X || vertex.Pos.Y < run.Min.Y || vertex.Pos.Y > run.Max.Y) continue;
                var shade = shades[run.Shade];
                if (shade.HasText) vertex.Col = (vertex.Col & AlphaMask) | shade.Text;
                break;
            }
        }
    }

    static uint Mix(uint top, uint bottom, float t)
    {
        var mixed = 0u;
        for (var shift = 0; shift < 24; shift += 8)
        {
            float a = top >> shift & 0xFF, b = bottom >> shift & 0xFF;
            mixed |= (uint)MathF.Round(a + (b - a) * t) << shift;
        }
        return mixed;
    }
}
