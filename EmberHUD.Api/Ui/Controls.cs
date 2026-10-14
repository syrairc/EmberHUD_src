using System.Numerics;
using Hexa.NET.ImGui;

namespace EmberHUD.Ui;

public static partial class Controls
{
    static uint _capturing;

    public static bool Hotkey(string label, ref Hotkey value, string? id = null)
    {
        var key = ImGui.GetID(id ?? label);
        var changed = false;
        if (_capturing == key)
        {
            if (Input.TryCapture(out var captured))
            {
                value = captured;
                changed = true;
                _capturing = 0;
            }
            else if (!Input.Capturing) _capturing = 0;
        }
        var text = _capturing == key ? "press a key... (Esc cancels)" : value.ToString();
        ImGui.PushItemFlag(ImGuiItemFlags.NoNav, true);
        if (ImGui.Button($"{text}###{id ?? label}", new Vector2(ImGui.CalcItemWidth(), 0)) && !Input.Capturing)
        {
            _capturing = key;
            Input.BeginCapture();
        }
        ImGui.PopItemFlag();
        if (label.Length > 0)
        {
            ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
            ImGui.TextUnformatted(label);
        }
        return changed;
    }

    public static bool Filter(ref string filter, string hint = "Filter", float width = -1f)
    {
        ImGui.SetNextItemWidth(width);
        return ImGui.InputTextWithHint("##filter", hint, ref filter, 128);
    }

    public static void Section(string text)
    {
        using (Fonts.Use(FontFace.Bold)) ImGui.SeparatorText(text);
    }

    public static void LogView(string id, IReadOnlyList<string> lines, float height = 200)
    {
        if (ImGui.BeginChild(id, new Vector2(0, height), ImGuiChildFlags.Borders))
        {
            ImGui.PushTextWrapPos(0);
            foreach (var line in lines) ImGui.TextUnformatted(line);
            ImGui.PopTextWrapPos();
            if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY()) ImGui.SetScrollHereY(1f);
        }
        ImGui.EndChild();
    }

    internal static Func<Hotkey, HotkeyBinding?, string?> HotkeyConflict { get; set; } = (_, _) => null;

    public static void HotkeyTable(Plugin plugin)
    {
        foreach (var group in plugin.Hotkeys.GroupBy(binding => binding.Category).OrderBy(group => group.Key is not null))
        {
            if (group.Key is { } category) ImGui.SeparatorText(category);
            foreach (var binding in group) HotkeyRow(binding);
        }
    }

    static void HotkeyRow(HotkeyBinding binding)
    {
        ImGui.PushID(binding.Id);
        var conflict = binding.Value.Key == Key.None ? null : HotkeyConflict(binding.Value, binding);
        ImGui.BeginGroup();
        using (new EColor.StyleColorScope(conflict is null ? [] : [(ImGuiCol.Text, EColor.U32(EColor.Bad))]))
            Hotkey(binding.Name, ref binding.Value, binding.Id);
        ImGui.EndGroup();
        Text.Tip(conflict is null ? binding.Description : $"{binding.Description}\nAlso bound to {conflict}");
        if (binding.Value != binding.Default)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton("Reset")) binding.Value = binding.Default;
        }
        ImGui.PopID();
    }
}
