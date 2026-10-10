using System.Drawing;
using System.Globalization;
using System.Numerics;
using Hexa.NET.ImGui;

namespace TorchlightSDK.Ui;

public sealed class WeightOpts
{
    public float Min = -10f;
    public float Max = 10f;
    public int Decimals;
    public float Step;
    public Color? Flat;
    public Color[]? Scale;
    public bool Rail;
    public float RailAlpha = 0.35f;
    public float GrabWidth = 6f;
    public float TrackHeight;
    public bool EditReadout = true;

    public WeightOpts Copy() => (WeightOpts)MemberwiseClone();
}

public static class Weight
{
    const float ReadoutWidth = 64f;
    const float ReadoutGap = 10f;

    public static class Scales
    {
        public static readonly Color[] RedGreen = [Color.FromArgb(255, 224, 103, 103), Color.FromArgb(255, 111, 207, 122)];
        public static readonly Color[] RedGoldGreen = [Color.FromArgb(255, 224, 103, 103), Color.FromArgb(255, 217, 193, 103), Color.FromArgb(255, 111, 207, 122)];
    }

    public static float Norm(float v, float min, float max) => max - min == 0f ? 0f : Math.Clamp((v - min) / (max - min), 0f, 1f);

    public static float Snap(float v, float step)
    {
        if (step <= 0f) return v;
        var r = MathF.Round(v / step) * step;
        return r == 0f ? 0f : r;
    }

    public static float EffStep(WeightOpts o) => o.Step > 0f ? o.Step : o.Decimals == 0 ? 1f : 0f;

    public static string Fmt(float v, int decimals) => v.ToString("F" + Math.Max(0, decimals), CultureInfo.InvariantCulture);

    public static Color Lerp(Color[]? stops, float t)
    {
        if (stops is not { Length: > 0 }) return Color.FromArgb(0, 0, 0, 0);
        if (stops.Length == 1) return stops[0];
        var pos = Math.Clamp(t, 0f, 1f) * (stops.Length - 1);
        var i = Math.Min((int)pos, stops.Length - 2);
        var f = pos - i;
        var a = stops[i];
        var b = stops[i + 1];
        return Color.FromArgb((byte)MathF.Round(a.A + (b.A - a.A) * f), (byte)MathF.Round(a.R + (b.R - a.R) * f), (byte)MathF.Round(a.G + (b.G - a.G) * f), (byte)MathF.Round(a.B + (b.B - a.B) * f));
    }

    public static Color GrabColor(float v, WeightOpts o)
    {
        if (o.Scale is { Length: > 1 } scale) return Lerp(scale, Norm(v, o.Min, o.Max));
        if (o.Scale is [var only]) return only;
        if (o.Flat is { } flat) return flat;
        return EColor.FromVector4(ImGui.GetStyle().Colors[(int)ImGuiCol.SliderGrab]);
    }

    public static float Commit(float typed, WeightOpts o) => Math.Clamp(Snap(typed, EffStep(o)), MathF.Min(o.Min, o.Max), MathF.Max(o.Min, o.Max));

    public static bool Slider(string label, ref float v, WeightOpts? o = null, string? id = null)
    {
        o ??= new WeightOpts();
        ImGui.PushID(id ?? label);
        if (!string.IsNullOrEmpty(label))
        {
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(label);
            ImGui.SameLine();
        }
        var changed = Bar("bar", ref v, o, MathF.Max(40f, ImGui.GetContentRegionAvail().X - ReadoutWidth - ReadoutGap));
        ImGui.SameLine(0f, ReadoutGap);
        changed |= Readout(ref v, o, ReadoutWidth);
        ImGui.PopID();
        return changed;
    }

    public static bool Slider(string label, Func<float> get, Action<float> set, WeightOpts? o = null, string? id = null)
    {
        var v = get();
        if (!Slider(label, ref v, o, id)) return false;
        set(v);
        return true;
    }

    static bool Bar(string id, ref float v, WeightOpts o, float width)
    {
        var h = o.TrackHeight > 0 ? o.TrackHeight : ImGui.GetFrameHeight();
        width = MathF.Max(width, 1f);
        var p = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton("##" + id, new Vector2(width, h));
        var hovered = ImGui.IsItemHovered();
        var travel = MathF.Max(0f, width - o.GrabWidth - 2f);

        var changed = false;
        if (ImGui.IsItemActive())
        {
            var t = travel <= 0f ? 0f : Math.Clamp((ImGui.GetIO().MousePos.X - (p.X + 1f + o.GrabWidth * 0.5f)) / travel, 0f, 1f);
            var nv = Commit(o.Min + t * (o.Max - o.Min), o);
            if (nv != v)
            {
                v = nv;
                changed = true;
            }
        }

        var dl = ImGui.GetWindowDrawList();
        var max = new Vector2(p.X + width, p.Y + h);
        var col = GrabColor(v, o);
        dl.AddRectFilled(p, max, ImGui.GetColorU32(hovered ? ImGuiCol.FrameBgHovered : ImGuiCol.FrameBg));
        dl.AddRect(p, max, ImGui.GetColorU32(ImGuiCol.Border));

        var gx = p.X + 1f + travel * Norm(v, o.Min, o.Max);
        if (o.Rail) dl.AddRectFilled(new Vector2(p.X + 1f, p.Y + 2f), new Vector2(gx, max.Y - 2f), EColor.U32(EColor.Fade(col, o.RailAlpha)));
        dl.AddRectFilled(new Vector2(gx, p.Y + 2f), new Vector2(gx + o.GrabWidth, max.Y - 2f), EColor.U32(col));
        return changed;
    }

    static bool Readout(ref float v, WeightOpts o, float width)
    {
        if (!o.EditReadout)
        {
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(Fmt(v, o.Decimals));
            return false;
        }

        var t = v;
        ImGui.SetNextItemWidth(width);
        ImGui.InputFloat("##ro", ref t, 0f, 0f, "%." + Math.Max(0, o.Decimals) + "f");
        if (!ImGui.IsItemDeactivatedAfterEdit()) return false;
        t = Commit(t, o);
        if (t == v) return false;
        v = t;
        return true;
    }
}
