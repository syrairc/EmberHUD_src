using Hexa.NET.ImGui;

namespace EmberHUD.Ui;

public static class Chips
{
    internal const float InputWidth = 160f;
    internal const float RemoveGap = 2f;

    static readonly Dictionary<uint, string> _pending = [];

    public static bool Draw(string id, List<string> items, string hint = "add...", bool caseInsensitiveDedup = true, Func<string, bool>? validate = null)
    {
        var key = ImGui.GetID(id);
        var changed = false;
        ImGui.PushID(id);
        var style = ImGui.GetStyle();
        var wrapX = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;

        var toRemove = -1;
        for (var i = 0; i < items.Count; i++)
        {
            ImGui.PushID(i);
            if (i > 0 && ImGui.GetItemRectMax().X + style.ItemSpacing.X + ImGui.CalcTextSize(items[i]).X + ImGui.CalcTextSize("x").X + style.FramePadding.X * 4 + RemoveGap < wrapX) ImGui.SameLine();
            if (DrawChip(items[i])) toRemove = i;
            ImGui.PopID();
        }
        if (toRemove >= 0)
        {
            items.RemoveAt(toRemove);
            changed = true;
        }

        var pending = _pending.GetValueOrDefault(key, "");
        if (items.Count > 0) ImGui.Spacing();
        ImGui.SetNextItemWidth(InputWidth);
        ImGui.InputTextWithHint("##add", hint, ref pending, 128);
        var submit = ImGui.IsItemDeactivated() && (ImGui.IsKeyPressed(ImGuiKey.Enter, false) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter, false));
        ImGui.SameLine();
        submit |= ImGui.SmallButton("+");
        _pending[key] = pending;

        if (submit && ShouldAdd(pending, items, caseInsensitiveDedup, validate))
        {
            items.Add(pending);
            _pending[key] = "";
            changed = true;
        }

        ImGui.PopID();
        return changed;
    }

    internal static bool ShouldAdd(string? text, List<string> items, bool caseInsensitiveDedup, Func<string, bool>? validate)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var dup = caseInsensitiveDedup ? items.Exists(x => string.Equals(x, text, StringComparison.OrdinalIgnoreCase)) : items.Contains(text);
        return !dup && (validate is null || validate(text));
    }

    static bool DrawChip(string label)
    {
        ImGui.BeginGroup();
        using (new EColor.StyleColorScope((ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.FrameBg))))
            ImGui.Button(label + "##lbl");
        ImGui.SameLine(0, RemoveGap);
        var remove = ImGui.SmallButton("x##del");
        ImGui.EndGroup();
        return remove;
    }
}
