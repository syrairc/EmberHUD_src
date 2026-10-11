using System.Drawing;
using System.Numerics;
using Hexa.NET.ImGui;

namespace Aember.Ui;

public static class EColor
{
    internal static readonly Color Bad = Color.FromArgb(255, 220, 80, 80);

    public static Vector4 ToVector4(Color c) => new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);

    public static Color FromVector4(Vector4 v) => Color.FromArgb(Channel(v.W), Channel(v.X), Channel(v.Y), Channel(v.Z));

    public static uint U32(Color c) => (uint)c.A << 24 | (uint)c.B << 16 | (uint)c.G << 8 | c.R;

    public static Color Fade(Color c, float a) => Color.FromArgb(Channel(a), c.R, c.G, c.B);

    public static Color Scale(Color c, float f) => Color.FromArgb(c.A, (byte)Math.Clamp(c.R * f, 0, 255), (byte)Math.Clamp(c.G * f, 0, 255), (byte)Math.Clamp(c.B * f, 0, 255));

    public static Color Desaturate(Color c)
    {
        var l = (byte)Math.Clamp(c.R * 0.299f + c.G * 0.587f + c.B * 0.114f, 0, 255);
        return Color.FromArgb(c.A, l, l, l);
    }

    public static Color Contrast(Color bg) => bg.R * 0.299f + bg.G * 0.587f + bg.B * 0.114f > 150f ? Color.FromArgb(255, 0, 0, 0) : Color.FromArgb(255, 255, 255, 255);

    public static Color CategoryColor(string? s)
    {
        var h = 2166136261u;
        foreach (var ch in s ?? "")
        {
            h ^= ch;
            h *= 16777619u;
        }
        float r = 0, g = 0, b = 0;
        ImGui.ColorConvertHSVtoRGB(h % 360u / 360f, 0.55f, 0.95f, ref r, ref g, ref b);
        return Color.FromArgb(255, (byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    static byte Channel(float v) => (byte)Math.Clamp(MathF.Round(v * 255f), 0, 255);

    public readonly struct StyleColorScope : IDisposable
    {
        readonly int _count;

        public StyleColorScope(params (ImGuiCol col, uint rgba)[] colors)
        {
            _count = colors.Length;
            foreach (var (col, rgba) in colors) ImGui.PushStyleColor(col, rgba);
        }

        public void Dispose() => ImGui.PopStyleColor(_count);
    }
}
