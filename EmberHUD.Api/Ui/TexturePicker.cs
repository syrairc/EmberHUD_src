using System.Drawing;
using System.Numerics;
using Hexa.NET.ImGui;

namespace EmberHUD.Ui;

public static class TexturePicker
{
    const float PreviewWidth = 48f;

    public static bool Combo(string label, ref string value, IReadOnlyList<string> names, Func<string, Sprite?> preview, string? id = null, Color? tint = null)
    {
        var changed = false;
        var current = value;
        var shown = names.FirstOrDefault(name => string.Equals(name, current, StringComparison.OrdinalIgnoreCase)) ?? (names.Count > 0 ? names[0] : "");
        ImGui.SetNextItemWidth(250f);
        if (ImGui.BeginCombo("##" + (id ?? label), shown))
        {
            foreach (var name in names)
            {
                var selected = ImGui.Selectable(name + "##" + name, name == shown);
                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                if (preview(name) is { } sprite) Draw.Image(sprite, new Vector2(max.X - PreviewWidth, min.Y), max, tint, ImGui.GetWindowDrawList());
                if (!selected || string.Equals(name, value, StringComparison.OrdinalIgnoreCase)) continue;
                value = name;
                changed = true;
            }
            ImGui.EndCombo();
        }
        ImGui.SameLine();
        ImGui.TextUnformatted(label);
        return changed;
    }
}
