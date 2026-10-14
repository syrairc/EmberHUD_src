using System.Drawing;
using Hexa.NET.ImGui;

namespace EmberHUD.Ui;

public static class Setting
{
    public static bool Checkbox(string label, ref bool value, string? tip = null, string? id = null)
    {
        Label(label, tip);
        return Done(ImGui.Checkbox("##" + (id ?? label), ref value), tip);
    }

    public static bool Slider(string label, ref int value, int min, int max, string? tip = null, string? id = null)
    {
        Label(label, tip);
        return Done(ImGui.SliderInt("##" + (id ?? label), ref value, min, max), tip);
    }

    public static bool Slider(string label, ref float value, float min, float max, string? tip = null, string? id = null, string? fmt = null, bool log = false)
    {
        Label(label, tip);
        return Done(ImGui.SliderFloat("##" + (id ?? label), ref value, min, max, fmt ?? FloatFormat(min, max), log ? ImGuiSliderFlags.Logarithmic : ImGuiSliderFlags.None), tip);
    }

    public static bool Color(string label, ref Color value, string? tip = null, string? id = null, bool alpha = true)
    {
        Label(label, tip);
        return Done(Controls.Color("", ref value, id ?? label, alpha), tip);
    }

    public static bool Hotkey(string label, ref Hotkey value, string? tip = null, string? id = null)
    {
        Label(label, tip);
        return Done(Controls.Hotkey("", ref value, id ?? label), tip);
    }

    public static bool Input(string label, ref string value, string? tip = null, string? id = null, int max = 256)
    {
        Label(label, tip);
        value ??= "";
        return Done(ImGui.InputText("##" + (id ?? label), ref value, (nuint)max), tip);
    }

    public static bool Choice<TEnum>(string label, ref TEnum value, string? tip = null, string? id = null) where TEnum : struct, Enum
    {
        Label(label, tip);
        return Done(Combo.Enum(ref value, id ?? label, ImGui.GetContentRegionAvail().X), tip);
    }

    public static bool Style(string label, TextStyle style, string? tip = null, string? id = null)
    {
        Label(label, tip);
        return Done(TextStyle.Edit("", style, id: id ?? label, swatchWidth: ImGui.GetContentRegionAvail().X), tip);
    }

    internal static string FloatFormat(float min, float max) => max - min <= 10 || min > 0 && max / min >= 100 ? "%.3f" : "%.1f";

    static void Label(string label, string? tip)
    {
        var x = ImGui.GetCursorPosX();
        var column = MathF.Max(160, ImGui.GetContentRegionAvail().X * 0.4f);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        Text.Tip(tip);
        ImGui.SameLine(x + MathF.Max(column, ImGui.CalcTextSize(label).X + ImGui.GetStyle().ItemSpacing.X));
        ImGui.SetNextItemWidth(-1);
    }

    static bool Done(bool changed, string? tip)
    {
        Text.Tip(tip);
        return changed;
    }
}
