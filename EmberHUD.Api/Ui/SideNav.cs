using System.Numerics;
using System.Text;
using Hexa.NET.ImGui;

namespace EmberHUD.Ui;

public sealed class NavItem
{
    public string Key = "";
    public string Label = "";
    public Sprite? Icon;
    public Sprite? IconActive;
    public Func<bool>? Visible;
    public Func<string?>? Badge;
    public Func<bool>? Body;
}

public sealed class NavState
{
    public string? Active;
    public bool ShowLabels;
}

public static class SideNav
{
    internal const float RowPad = 8f;
    internal const float BadgePad = 2f;

    public static bool Rail(string id, NavItem[]? items, NavState? state, float collapsedWidth = 40f, float expandedWidth = 152f, float iconSize = 20f)
    {
        if (items is not { Length: > 0 } || state is null) return false;
        var active = Resolve(items, state);
        if (active < 0) return false;

        ImGui.PushID(id);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(2f, 2f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        ImGui.BeginChild("##rail", new Vector2(state.ShowLabels ? expandedWidth : collapsedWidth, 0), ImGuiChildFlags.Borders, ImGuiWindowFlags.NoScrollbar);

        for (var i = 0; i < items.Length; i++)
        {
            var it = items[i];
            if (it is null || it.Visible is { } visible && !visible()) continue;
            if (DrawItem(it, i, i == active, state.ShowLabels, iconSize)) state.Active = it.Key;
        }

        var slack = ImGui.GetContentRegionAvail().Y - ImGui.GetFrameHeight();
        if (slack > 0f) ImGui.Dummy(new Vector2(0f, slack));
        if (Chevron(state.ShowLabels)) state.ShowLabels = !state.ShowLabels;

        ImGui.EndChild();
        ImGui.PopStyleVar(2);

        ImGui.SameLine();
        ImGui.BeginChild("##body", Vector2.Zero, ImGuiChildFlags.Borders);
        var changed = items[active].Body?.Invoke() ?? false;
        ImGui.EndChild();

        ImGui.PopID();
        return changed;
    }

    static int Resolve(NavItem[] items, NavState state)
    {
        var first = -1;
        for (var i = 0; i < items.Length; i++)
        {
            var it = items[i];
            if (it is null || it.Visible is { } visible && !visible()) continue;
            if (first < 0) first = i;
            if (it.Key == state.Active) return i;
        }
        if (first >= 0) state.Active = items[first].Key;
        return first;
    }

    static bool DrawItem(NavItem it, int index, bool selected, bool labels, float side)
    {
        var label = it.Label ?? "";
        var h = Math.Max(ImGui.GetFrameHeight(), side + RowPad);
        var w = Math.Max(1f, ImGui.GetContentRegionAvail().X);

        ImGui.InvisibleButton("##i" + index, new Vector2(w, h));
        var clicked = ImGui.IsItemClicked();
        var hovered = ImGui.IsItemHovered();

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var dl = ImGui.GetWindowDrawList();

        if (hovered) dl.AddRectFilled(min, max, ImGui.GetColorU32(ImGuiCol.HeaderHovered));
        else if (selected) dl.AddRectFilled(min, max, ImGui.GetColorU32(ImGuiCol.Header));

        var pad = ImGui.GetStyle().FramePadding.X;
        var iconX = labels ? min.X + pad : min.X + (w - side) * 0.5f;
        var iconMin = new Vector2(iconX, min.Y + (h - side) * 0.5f);
        var iconMax = iconMin + new Vector2(side, side);

        var icon = selected && it.IconActive is not null ? it.IconActive : it.Icon;
        if (icon is { } sprite) Draw.Image(sprite, iconMin, iconMax, list: dl);
        else Initials(dl, label, iconMin, iconMax);

        if (labels && label.Length > 0)
        {
            dl.PushClipRect(min, max, true);
            dl.AddText(new Vector2(iconMax.X + pad, min.Y + (h - ImGui.GetTextLineHeight()) * 0.5f), ImGui.GetColorU32(ImGuiCol.Text), label);
            dl.PopClipRect();
        }
        else if (hovered && label.Length > 0) ImGui.SetTooltip(Text.EscPct(label));

        var badge = it.Badge?.Invoke();
        if (!string.IsNullOrEmpty(badge)) Badge(dl, badge, min, max, iconMax, labels);
        return clicked;
    }

    static void Badge(ImDrawListPtr dl, string text, Vector2 min, Vector2 max, Vector2 iconMax, bool labels)
    {
        var size = ImGui.CalcTextSize(text);
        var w = size.X + BadgePad * 2f;

        var bMin = labels
            ? new Vector2(max.X - w - BadgePad * 2f, min.Y + (max.Y - min.Y - size.Y) * 0.5f)
            : new Vector2(Math.Min(iconMax.X - w * 0.5f, max.X - w), min.Y);

        var bMax = bMin + new Vector2(w, size.Y);
        dl.AddRectFilled(bMin, bMax, ImGui.GetColorU32(ImGuiCol.Button));
        dl.AddText(bMin + new Vector2(BadgePad, 0f), ImGui.GetColorU32(ImGuiCol.Text), text);
    }

    static void Initials(ImDrawListPtr dl, string? label, Vector2 min, Vector2 max)
    {
        dl.AddRect(min, max, ImGui.GetColorU32(ImGuiCol.TextDisabled));

        var s = InitialsOf(label);
        if (s.Length == 0) return;

        var size = ImGui.CalcTextSize(s);
        dl.PushClipRect(min, max, true);
        dl.AddText((min + max) * 0.5f - size * 0.5f, ImGui.GetColorU32(ImGuiCol.Text), s);
        dl.PopClipRect();
    }

    static string InitialsOf(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return "";
        var sb = new StringBuilder(2);
        var boundary = true;
        foreach (var c in label)
        {
            if (c == ' ')
            {
                boundary = true;
                continue;
            }
            if (boundary && char.IsLetter(c))
            {
                sb.Append(char.ToUpperInvariant(c));
                if (sb.Length == 2) break;
                boundary = false;
            }
        }
        return sb.ToString();
    }

    static bool Chevron(bool labels)
    {
        var h = ImGui.GetFrameHeight();
        var w = Math.Max(1f, ImGui.GetContentRegionAvail().X);

        ImGui.InvisibleButton("##chev", new Vector2(w, h));
        var clicked = ImGui.IsItemClicked();

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var dl = ImGui.GetWindowDrawList();

        if (ImGui.IsItemHovered()) dl.AddRectFilled(min, max, ImGui.GetColorU32(ImGuiCol.HeaderHovered));
        dl.AddLine(min, new Vector2(max.X, min.Y), ImGui.GetColorU32(ImGuiCol.Border));

        var g = labels ? "<<" : ">>";
        dl.AddText(new Vector2(min.X + ImGui.GetStyle().FramePadding.X, min.Y + (h - ImGui.GetTextLineHeight()) * 0.5f), ImGui.GetColorU32(ImGuiCol.TextDisabled), g);

        return clicked;
    }
}
