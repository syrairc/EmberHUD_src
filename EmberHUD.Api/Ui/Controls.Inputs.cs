using System.Numerics;
using Hexa.NET.ImGui;
using SColor = System.Drawing.Color;

namespace EmberHUD.Ui;

public static partial class Controls
{
    static string Sfx(string label, string? id) => "##" + (id ?? label);

    public static bool SliderInt(string label, ref int v, int min, int max, string? id = null) =>
        ImGui.SliderInt(label + Sfx(label, id), ref v, min, max);

    public static bool SliderInt(string label, Func<int> get, Action<int> set, int min, int max, string? id = null)
    {
        int v = get();
        if (SliderInt(label, ref v, min, max, id)) { set(v); return true; }
        return false;
    }

    public static bool SliderFloat(string label, ref float v, float min, float max, string? id = null, string fmt = "%.1f") =>
        ImGui.SliderFloat(label + Sfx(label, id), ref v, min, max, fmt);

    public static bool SliderFloat(string label, Func<float> get, Action<float> set, float min, float max,
        string? id = null, string fmt = "%.1f")
    {
        float v = get();
        if (SliderFloat(label, ref v, min, max, id, fmt)) { set(v); return true; }
        return false;
    }

    public static bool Color(string label, ref SColor c, string? id = null, bool alpha = true)
    {
        Vector4 v = EColor.ToVector4(c);
        var flags = ImGuiColorEditFlags.NoInputs | (alpha ? ImGuiColorEditFlags.AlphaBar : ImGuiColorEditFlags.NoAlpha);
        if (ImGui.ColorEdit4(label + Sfx(label, id), ref v, flags)) { c = EColor.FromVector4(v); return true; }
        return false;
    }

    public static bool Color(string label, Func<SColor> get, Action<SColor> set, string? id = null, bool alpha = true)
    {
        SColor c = get();
        if (Color(label, ref c, id, alpha)) { set(c); return true; }
        return false;
    }

    public static bool Category(string label, string? id = null) =>
        ImGui.CollapsingHeader(label + Sfx(label, id), ImGuiTreeNodeFlags.DefaultOpen);

    public static void InfoMark(string? info, float wrap = 320f)
    {
        ImGui.TextDisabled("(?)");
        if (string.IsNullOrEmpty(info) || !ImGui.IsItemHovered()) return;
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(wrap);
        ImGui.TextUnformatted(info);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    public static void InfoLabel(string? label, string? info, float wrap = 320f)
    {
        ImGui.TextUnformatted(label ?? "");
        ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
        InfoMark(info, wrap);
    }

    public static bool Field(string id, string? label, Func<bool>? control, string? hint = null, string? error = null)
    {
        if (control == null) return false;
        bool bad = !string.IsNullOrEmpty(error);
        ImGui.PushID(id);
        if (!string.IsNullOrEmpty(label))
        {
            if (bad) ImGui.PushStyleColor(ImGuiCol.Text, EColor.U32(EColor.Bad));
            ImGui.TextUnformatted(label);
            if (bad) ImGui.PopStyleColor();
        }
        ImGui.BeginGroup();
        bool changed = control();
        ImGui.EndGroup();
        if (bad)
            ImGui.GetWindowDrawList().AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(),
                EColor.U32(EColor.Bad), ImGui.GetStyle().FrameRounding);
        var msg = bad ? error : hint;
        if (!string.IsNullOrEmpty(msg))
        {
            if (bad) ImGui.TextColored(EColor.ToVector4(EColor.Bad), Text.EscPct(msg));
            else ImGui.TextDisabled(Text.EscPct(msg));
        }
        ImGui.PopID();
        return changed;
    }

    public static bool OverrideRow(string id, string label, ref bool ovr, Func<bool> control, string? tip = null)
    {
        ImGui.PushID(id);
        bool changed = ImGui.Checkbox("##o", ref ovr);
        Text.Tip(tip ?? (ovr
            ? "Overriding " + label.ToLowerInvariant() + ". Untick to inherit."
            : "Inheriting " + label.ToLowerInvariant() + ". Tick to override."));
        ImGui.SameLine();
        if (!ovr) ImGui.BeginDisabled();
        changed |= control();
        if (!ovr) ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.TextUnformatted(label);
        ImGui.PopID();
        return changed;
    }

    public static void AlignMid(float itemHeight) =>
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + Math.Max(0f, (ImGui.GetFrameHeight() - itemHeight) * 0.5f));

    public static bool IconToggle(string glyph, ref bool v, string? tooltip = null, string? id = null, float width = 22f)
    {
        id ??= glyph;
        bool changed = false;
        using (new EColor.StyleColorScope(
                   (ImGuiCol.Button, ImGui.GetColorU32(v ? ImGuiCol.ButtonActive : ImGuiCol.FrameBg)),
                   (ImGuiCol.Text, ImGui.GetColorU32(v ? ImGuiCol.Text : ImGuiCol.TextDisabled))))
            if (ImGui.Button(glyph + "##" + id, new Vector2(width, 0)))
            {
                v = !v;
                changed = true;
            }
        if (v)
            ImGui.GetWindowDrawList().AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(),
                ImGui.GetColorU32(ImGuiCol.CheckMark), ImGui.GetStyle().FrameRounding);
        Text.Tip(tooltip);
        return changed;
    }

    public record struct TriItem(string Label, int Value = 0);

    internal static int CycleTri(int v, bool back) => back ? (v + 2) % 3 : (v + 1) % 3;

    public static bool TriState(string? label, ref int v, string? id = null, float box = 22f, float glyphScale = 1.4f)
    {
        bool changed = false;
        ImGui.PushID(id ?? label);
        uint col = v == 1 ? ImGui.GetColorU32(ImGuiCol.CheckMark)
                 : v == 2 ? EColor.U32(EColor.Bad)
                 : ImGui.GetColorU32(ImGuiCol.TextDisabled);
        string glyph = v == 1 ? "+" : v == 2 ? "x" : "-";

        var padY = ImGui.GetStyle().FramePadding.Y;
        var rowH = ImGui.GetFrameHeight();
        ImGui.PushFont(default(ImFontPtr), ImGui.GetStyle().FontSizeBase * glyphScale);
        var size = new Vector2(box, Math.Max(rowH, ImGui.GetTextLineHeight() + padY * 2));
        using (new EColor.StyleColorScope((ImGuiCol.Text, col), (ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.FrameBg))))
            if (ImGui.Button(glyph + "##t", size))
            {
                v = CycleTri(v, ImGui.GetIO().KeyShift);
                changed = true;
            }
        ImGui.PopFont();
        float btnH = ImGui.GetItemRectSize().Y;
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right) && v != 0) { v = 0; changed = true; }
        if (!string.IsNullOrEmpty(label))
        {
            ImGui.SameLine();
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (btnH - ImGui.GetTextLineHeight()) * 0.5f);
            ImGui.TextUnformatted(label);
        }
        ImGui.PopID();
        return changed;
    }

    public static bool TriStateGrid(string id, TriItem[]? items, int columns)
    {
        if (items == null || items.Length == 0) return false;
        if (columns < 1) columns = 1;
        if (!ImGui.BeginTable("##" + id, columns, ImGuiTableFlags.SizingStretchSame)) return false;

        bool changed = false;
        for (int i = 0; i < items.Length; i++)
        {
            if (i % columns == 0) ImGui.TableNextRow();
            ImGui.TableNextColumn();
            int v = items[i].Value;
            if (TriState(items[i].Label, ref v, id + i)) { items[i].Value = v; changed = true; }
        }
        ImGui.EndTable();
        return changed;
    }

    public readonly struct PanelStyleScope : IDisposable
    {
        public PanelStyleScope()
        {
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 3f);
            ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, 3f);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 3f);
            ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 3f);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(6, 3));
        }

        public void Dispose() => ImGui.PopStyleVar(5);
    }
}
