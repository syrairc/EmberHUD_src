using System.Numerics;
using Hexa.NET.ImGui;

namespace EmberHUD.Ui;

public static class Combo
{
    static class EnumCache<T> where T : struct, System.Enum
    {
        public static readonly string[] Names = System.Enum.GetNames<T>();
        public static readonly T[] Values = System.Enum.GetValues<T>();
    }

    public static bool Enum<T>(ref T value, string id, float width = 120f) where T : struct, System.Enum
    {
        var names = EnumCache<T>.Names;
        var values = EnumCache<T>.Values;
        var cur = Math.Max(0, Array.IndexOf(values, value));
        ImGui.SetNextItemWidth(width);
        if (!ImGui.Combo("##" + id, ref cur, names, names.Length)) return false;
        value = values[cur];
        return true;
    }

    public static bool Flags<T>(ref T value, string id, float width = 200f, string none = "none") where T : struct, System.Enum
    {
        var bits = Convert.ToInt64(value);
        var flags = EnumCache<T>.Values.Where(flag => Convert.ToInt64(flag) != 0).ToArray();
        var names = flags.Where(flag => (bits & Convert.ToInt64(flag)) != 0).Select(flag => flag.ToString());
        var preview = string.Join(", ", names);
        var changed = false;
        ImGui.SetNextItemWidth(width);
        if (ImGui.BeginCombo("##" + id, preview.Length == 0 ? none : preview))
        {
            foreach (var flag in flags)
            {
                var bit = Convert.ToInt64(flag);
                var on = (bits & bit) != 0;
                if (!ImGui.Selectable($"{(on ? "[x] " : "[ ] ")}{flag}##{id}{bit}", on, ImGuiSelectableFlags.NoAutoClosePopups)) continue;
                bits ^= bit;
                changed = true;
            }
            ImGui.EndCombo();
        }
        if (changed) value = (T)System.Enum.ToObject(typeof(T), bits);
        return changed;
    }

    public static bool Option(ref int index, string[] labels, string id, float width = 120f)
    {
        ImGui.SetNextItemWidth(width);
        return ImGui.Combo("##" + id, ref index, labels, labels.Length);
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
