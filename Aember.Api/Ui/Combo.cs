using System.Numerics;
using Hexa.NET.ImGui;

namespace Aember.Ui;

public static class Combo
{
    static class EnumCache<T> where T : struct, System.Enum
    {
        public static readonly string[] Names = System.Enum.GetNames<T>();
        public static readonly T[] Values = System.Enum.GetValues<T>();
        public static readonly string Id = typeof(T).Name;
    }

    public static bool Enum<T>(ref T value, string? id = null, float width = 120f) where T : struct, System.Enum
    {
        var names = EnumCache<T>.Names;
        var values = EnumCache<T>.Values;
        var cur = Math.Max(0, Array.IndexOf(values, value));
        ImGui.SetNextItemWidth(width);
        if (!ImGui.Combo("##" + (id ?? EnumCache<T>.Id), ref cur, names, names.Length)) return false;
        value = values[cur];
        return true;
    }

    public static bool Option(ref int index, string[] labels, string? id = null, float width = 120f)
    {
        ImGui.SetNextItemWidth(width);
        return ImGui.Combo("##" + (id ?? (labels.Length > 0 ? labels[0] : "opt")), ref index, labels, labels.Length);
    }

    public static bool SearchCombo(string id, string preview, IEnumerable<(string key, string label)> candidates, ref string filter, out string? picked, float width = 250f) =>
        SearchCombo(id, preview, candidates.Select(c => (c.key, c.label, c.label)), ref filter, out picked, width);

    public static bool SearchCombo(string id, string preview, IEnumerable<(string key, string label, string search)> candidates, ref string filter, out string? picked, float width = 250f)
    {
        picked = null;
        var changed = false;
        if (width < 0) width = ImGui.GetContentRegionAvail().X;
        ImGui.SetNextItemWidth(width);
        ImGui.SetNextWindowSizeConstraints(new Vector2(width, 0), new Vector2(width, 400));
        if (ImGui.BeginCombo("##" + id, preview))
        {
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText("##" + id + "_f", ref filter, 128);
            foreach (var c in candidates)
            {
                if (!Text.Matches(filter, c.search)) continue;
                if (!ImGui.Selectable(c.label + "##" + c.key)) continue;
                picked = c.key;
                changed = true;
            }
            if (changed) ImGui.CloseCurrentPopup();
            ImGui.EndCombo();
        }
        else if (filter.Length > 0) filter = "";
        if (changed) filter = "";
        return changed;
    }
}
