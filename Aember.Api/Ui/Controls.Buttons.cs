using System.Numerics;
using Hexa.NET.ImGui;
using SColor = System.Drawing.Color;

namespace Aember.Ui;

public static partial class Controls
{
    public enum IconSide
    {
        Left,
        Right,
    }

    public readonly record struct GroupButton(string Text, SColor? Color = null, string? Sheet = null, int Cell = 0, IconSide Side = IconSide.Left);

    public static bool PrimaryButton(string label, Vector2 size = default)
    {
        var colours = ImGui.GetStyle().Colors;
        ImGui.PushStyleColor(ImGuiCol.Button, colours[(int)ImGuiCol.ButtonActive]);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, colours[(int)ImGuiCol.HeaderHovered]);
        var clicked = ImGui.Button(label, size);
        ImGui.PopStyleColor(2);
        return clicked;
    }

    public static int GroupCount(int n) => n < 2 ? 0 : Math.Min(n, 5);

    public static int ButtonGroup(string id, GroupButton[]? buttons, int selected = -1, IconSheet[]? sheets = null, float iconSize = 16f, bool desaturate = false)
    {
        if (buttons is null) return -1;
        var n = GroupCount(buttons.Length);
        if (n == 0) return -1;

        int clicked = -1;
        var style = ImGui.GetStyle();
        var dl = ImGui.GetWindowDrawList();
        bool picker = selected >= 0;
        ImGui.PushID(id);
        for (int i = 0; i < n; i++)
        {
            var b = buttons[i];
            if (i > 0) ImGui.SameLine(0, 1);

            var label = b.Text ?? "";
            var sh = b.Sheet is not null && sheets is { Length: > 0 } ? sheets[SheetIndex(sheets, b.Sheet)] : null;
            var tex = sh?.Texture;
            var iconW = tex.HasValue ? iconSize + style.ItemInnerSpacing.X : 0f;
            var ts = ImGui.CalcTextSize(label);
            var size = new Vector2(ts.X + iconW + style.FramePadding.X * 2, ImGui.GetFrameHeight());

            bool on = !picker || i == selected;
            SColor bas = b.Color ?? EColor.FromVector4(
                style.Colors[(int)(on && picker ? ImGuiCol.Header : ImGuiCol.Button)]);
            SColor fill = on ? bas
                             : desaturate && b.Color.HasValue ? EColor.Desaturate(bas)
                                                              : EColor.Fade(bas, 0.3f);
            uint bg = EColor.U32(fill);
            uint fg = b.Color.HasValue && (on || desaturate)
                ? EColor.U32(EColor.Contrast(fill))
                : ImGui.GetColorU32(ImGuiCol.Text, on ? 1f : 0.55f);

            using (new EColor.StyleColorScope(
                (ImGuiCol.Button, bg),
                (ImGuiCol.ButtonHovered, b.Color.HasValue ? EColor.U32(EColor.Scale(fill, 1.2f)) : ImGui.GetColorU32(ImGuiCol.ButtonHovered)),
                (ImGuiCol.ButtonActive, b.Color.HasValue ? EColor.U32(EColor.Scale(fill, 0.8f)) : ImGui.GetColorU32(ImGuiCol.ButtonActive))))
                if (ImGui.Button("##b" + i, size)) clicked = i;

            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            float textX = min.X + style.FramePadding.X;
            float iconX = b.Side == IconSide.Right ? max.X - style.FramePadding.X - iconSize : textX;
            if (tex.HasValue && b.Side == IconSide.Left) textX += iconW;
            if (tex is { } t)
            {
                var (uv0, uv1) = CellUv(sh!, b.Cell);
                var iy = min.Y + (size.Y - iconSize) * 0.5f;
                dl.AddImage(t, new Vector2(iconX, iy), new Vector2(iconX + iconSize, iy + iconSize), uv0, uv1, fg);
            }
            dl.AddText(new Vector2(textX, min.Y + (size.Y - ts.Y) * 0.5f), fg, label);
        }
        ImGui.PopID();
        return clicked;
    }

    public static bool ButtonGroup(string id, ref int selected, GroupButton[]? buttons, IconSheet[]? sheets = null, float iconSize = 16f, bool desaturate = false)
    {
        int hit = ButtonGroup(id, buttons, selected, sheets, iconSize, desaturate);
        if (hit < 0 || hit == selected) return false;
        selected = hit;
        return true;
    }

    public static bool ButtonGroup<T>(string id, ref T value, params (string Label, T Value)[] options)
    {
        var current = value;
        var selected = Array.FindIndex(options, o => EqualityComparer<T>.Default.Equals(o.Value, current));
        var buttons = Array.ConvertAll(options, o => new GroupButton(o.Label));
        if (!ButtonGroup(id, ref selected, buttons)) return false;
        value = options[selected].Value;
        return true;
    }

    static void Chevron(ImDrawListPtr dl, Vector2 c, float r, uint col) =>
        dl.AddTriangleFilled(new Vector2(c.X - r, c.Y - r * 0.5f), new Vector2(c.X + r, c.Y - r * 0.5f),
            new Vector2(c.X, c.Y + r * 0.75f), col);

    static int MenuPopup(string popId, string?[] items, Vector2 anchor)
    {
        int hit = -1;
        if (ImGui.IsPopupOpen(popId)) ImGui.SetNextWindowPos(anchor);
        if (ImGui.BeginPopup(popId))
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] is null) { ImGui.Separator(); continue; }
                if (ImGui.Selectable(items[i] + "##i" + i)) hit = i;
            }
            ImGui.EndPopup();
        }
        return hit;
    }

    public static int MenuButton(string label, string?[]? items, string? id = null, float width = 0f)
    {
        if (items is null || items.Length == 0) return -1;
        var style = ImGui.GetStyle();
        string txt = label ?? "";
        float h = ImGui.GetFrameHeight();
        float arrow = h * 0.4f;
        var ts = ImGui.CalcTextSize(txt);
        float w = width > 0 ? width : ts.X + arrow + style.ItemInnerSpacing.X + style.FramePadding.X * 2;

        ImGui.PushID(id ?? label);
        if (ImGui.Button("##m", new Vector2(w, h))) ImGui.OpenPopup("##menu");
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var dl = ImGui.GetWindowDrawList();
        uint fg = ImGui.GetColorU32(ImGuiCol.Text);
        dl.AddText(new Vector2(min.X + style.FramePadding.X, min.Y + (h - ts.Y) * 0.5f), fg, txt);
        Chevron(dl, new Vector2(max.X - style.FramePadding.X - arrow * 0.5f, min.Y + h * 0.5f), arrow * 0.5f, fg);
        int hit = MenuPopup("##menu", items, new Vector2(min.X, max.Y));
        ImGui.PopID();
        return hit;
    }

    public static float SplitPrimaryWidth(float total, float arrow, float gap = 0f) =>
        Math.Max(total - arrow - gap, 1f);

    public static bool SplitButton(string label, string?[]? menu, out int picked, string? id = null, float width = 0f)
    {
        picked = -1;
        var style = ImGui.GetStyle();
        string txt = label ?? "";
        float h = ImGui.GetFrameHeight();
        float aw = h * 0.8f;
        float pw = width > 0 ? SplitPrimaryWidth(width, aw)
                             : ImGui.CalcTextSize(txt).X + style.FramePadding.X * 2;
        bool has = menu is { Length: > 0 };

        ImGui.PushID(id ?? label);
        bool hit = ImGui.Button(txt + "##p", new Vector2(pw, h));
        float left = ImGui.GetItemRectMin().X;
        float r = style.FrameRounding;
        ImGui.SameLine(0, 0);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() - r);
        if (ImGui.Button("##d", new Vector2(aw + r, h)) && has) ImGui.OpenPopup("##menu");
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var dl = ImGui.GetWindowDrawList();
        dl.AddLine(new Vector2(min.X + r, min.Y + r), new Vector2(min.X + r, max.Y - r),
            ImGui.GetColorU32(ImGuiCol.Text, 0.25f));
        Chevron(dl, new Vector2((min.X + r + max.X) * 0.5f, min.Y + h * 0.5f),
            h * 0.2f, ImGui.GetColorU32(ImGuiCol.Text));
        if (has) picked = MenuPopup("##menu", menu!, new Vector2(left, max.Y));
        ImGui.PopID();
        return hit;
    }

    const float SubScale = 0.85f;

    public static float CompoundHeight(float topH, float subH, float gap, float iconSize, float padY) =>
        Math.Max(topH + (subH > 0f ? gap + subH : 0f), iconSize) + padY * 2;

    public static bool CompoundButton(GroupButton b, string? secondary, IconSheet[]? sheets = null, float iconSize = 24f, float width = 0f, string? id = null)
    {
        var style = ImGui.GetStyle();
        string top = b.Text ?? "";
        string sub = secondary ?? "";
        var sh = b.Sheet is not null && sheets is { Length: > 0 } ? sheets[SheetIndex(sheets, b.Sheet)] : null;
        var tex = sh?.Texture;

        float subPx = ImGui.GetFontSize() * SubScale;
        var t0 = ImGui.CalcTextSize(top);
        float subW = sub.Length > 0 ? ImGui.CalcTextSize(sub).X * SubScale : 0f;
        float subH = sub.Length > 0 ? subPx : 0f;
        float gap = style.ItemInnerSpacing.Y;
        float iconW = tex.HasValue ? iconSize + style.ItemInnerSpacing.X * 2 : 0f;
        float w = width > 0 ? width : Math.Max(t0.X, subW) + iconW + style.FramePadding.X * 2;
        float th = CompoundHeight(t0.Y, subH, gap, 0f, 0f);
        float hgt = CompoundHeight(t0.Y, subH, gap, tex.HasValue ? iconSize : 0f, style.FramePadding.Y);

        bool clicked;
        var col = b.Color;
        ImGui.PushID(id ?? b.Text);
        using (new EColor.StyleColorScope(
                   (ImGuiCol.Button, col.HasValue ? EColor.U32(col.Value) : ImGui.GetColorU32(ImGuiCol.Button)),
                   (ImGuiCol.ButtonHovered, col.HasValue ? EColor.U32(EColor.Scale(col.Value, 1.2f)) : ImGui.GetColorU32(ImGuiCol.ButtonHovered)),
                   (ImGuiCol.ButtonActive, col.HasValue ? EColor.U32(EColor.Scale(col.Value, 0.8f)) : ImGui.GetColorU32(ImGuiCol.ButtonActive))))
            clicked = ImGui.Button("##c", new Vector2(w, hgt));

        var min = ImGui.GetItemRectMin();
        var dl = ImGui.GetWindowDrawList();
        uint fg = col.HasValue ? EColor.U32(EColor.Contrast(col.Value)) : ImGui.GetColorU32(ImGuiCol.Text);
        uint dim = col.HasValue ? EColor.U32(EColor.Fade(EColor.Contrast(col.Value), 0.7f))
                                : ImGui.GetColorU32(ImGuiCol.Text, 0.6f);
        float x = min.X + style.FramePadding.X;
        if (tex is { } t)
        {
            var (uv0, uv1) = CellUv(sh!, b.Cell);
            var iy = min.Y + (hgt - iconSize) * 0.5f;
            dl.AddImage(t, new Vector2(x, iy), new Vector2(x + iconSize, iy + iconSize), uv0, uv1, fg);
            x += iconSize + style.ItemInnerSpacing.X * 2;
        }
        var y = min.Y + (hgt - th) * 0.5f;
        dl.AddText(new Vector2(x, y), fg, top);
        if (sub.Length > 0) ImGui.AddText(dl, ImGui.GetFont(), subPx, new Vector2(x, y + t0.Y + gap), dim, sub);
        ImGui.PopID();
        return clicked;
    }

    public static int FitItems(float[]? widths, float avail, float spacing, float overflowW)
    {
        if (widths is null || widths.Length == 0) return 0;
        float all = 0f;
        for (int i = 0; i < widths.Length; i++) all += widths[i] + (i > 0 ? spacing : 0f);
        if (all <= avail) return widths.Length;

        float budget = avail - overflowW - spacing;
        int n = 0;
        float used = 0f;
        for (int i = 0; i < widths.Length; i++)
        {
            float w = widths[i] + (i > 0 ? spacing : 0f);
            if (used + w > budget) break;
            used += w;
            n++;
        }
        return n;
    }

    public static int OverflowRow(string id, string?[]? items, float width = 0f, string more = "...")
    {
        if (items is null || items.Length == 0) return -1;
        var style = ImGui.GetStyle();
        float avail = width > 0 ? width : ImGui.GetContentRegionAvail().X;
        float pad = style.FramePadding.X * 2;

        var widths = new float[items.Length];
        for (int i = 0; i < items.Length; i++) widths[i] = ImGui.CalcTextSize(items[i] ?? "").X + pad;
        float moreW = ImGui.CalcTextSize(more).X + pad + ImGui.GetFrameHeight() * 0.4f
                    + style.ItemInnerSpacing.X;
        int n = FitItems(widths, avail, style.ItemSpacing.X, moreW);

        int hit = -1;
        ImGui.PushID(id);
        for (int i = 0; i < n; i++)
        {
            if (i > 0) ImGui.SameLine();
            if (ImGui.Button((items[i] ?? "") + "##o" + i)) hit = i;
        }
        if (n < items.Length)
        {
            if (n > 0) ImGui.SameLine();
            var rest = new string?[items.Length - n];
            Array.Copy(items, n, rest, 0, rest.Length);
            int m = MenuButton(more, rest, "more");
            if (m >= 0) hit = n + m;
        }
        ImGui.PopID();
        return hit;
    }
}
