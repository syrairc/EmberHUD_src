using Hexa.NET.ImGui;

namespace Aember.Ui;

public static partial class Controls
{
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
