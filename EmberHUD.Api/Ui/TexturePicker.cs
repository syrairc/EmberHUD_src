using System.Numerics;
using Hexa.NET.ImGui;

namespace EmberHUD.Ui;

public static class TexturePicker
{
    const float PreviewWidth = 48f;

    public static bool Combo(string label, ref string value, IReadOnlyList<string> names, Func<string, Sprite?> preview, string? id = null)
    {
        var changed = false;
        var shown = names.Contains(value) ? value : names.Count > 0 ? names[0] : "";
        ImGui.SetNextItemWidth(250f);
        if (ImGui.BeginCombo("##" + (id ?? label), shown))
        {
            foreach (var name in names)
            {
                var selected = ImGui.Selectable(name + "##" + name, name == shown);
                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                if (preview(name) is { } sprite) Draw.Image(sprite, new Vector2(max.X - PreviewWidth, min.Y), max);
                if (!selected || name == value) continue;
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
