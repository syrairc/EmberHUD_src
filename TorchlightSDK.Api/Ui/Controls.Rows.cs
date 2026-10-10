using Hexa.NET.ImGui;
using SColor = System.Drawing.Color;

namespace TorchlightSDK.Ui;

public static partial class Controls
{
    public static bool MarkerRow(string label, ref bool show, ref SColor colour, ref float size, float min = 1, float max = 20, string? id = null)
    {
        ImGui.PushID(id ?? label);
        var changed = ImGui.Checkbox("##show", ref show);
        ImGui.SameLine();
        changed |= Color("", ref colour, "colour");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120);
        changed |= SliderFloat("", ref size, min, max, "size");
        ImGui.SameLine();
        ImGui.TextUnformatted(label);
        ImGui.PopID();
        return changed;
    }

    public static bool SetCheckbox(string id, ICollection<string> set, string item, bool inverted = false)
    {
        var on = set.Contains(item) != inverted;
        if (!ImGui.Checkbox(id, ref on)) return false;
        if (on != inverted) set.Add(item);
        else set.Remove(item);
        return true;
    }
}
