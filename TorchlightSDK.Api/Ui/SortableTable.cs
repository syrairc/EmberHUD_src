using System.Numerics;
using Hexa.NET.ImGui;

namespace TorchlightSDK.Ui;

public sealed class TableColumn<T>
{
    public string Header = "";
    public float Width;
    public Func<T, IComparable?>? SortKey;
    public Action<T, int>? Draw;
}

public static class SortableTable
{
    public static void Draw<T>(string id, IList<T>? items, TableColumn<T>[]? columns, ref string filter, Func<T, string?>? filterText = null, float maxHeight = 0f)
    {
        if (items is null || columns is not { Length: > 0 }) return;

        ImGui.PushID(id);
        if (filterText is not null)
        {
            ImGui.SetNextItemWidth(-1f);
            ImGui.InputTextWithHint("##filter", "Search...", ref filter, 128);
        }

        if (maxHeight > 0f)
        {
            var avail = ImGui.GetContentRegionAvail().Y;
            if (avail > 0f && avail < maxHeight) maxHeight = avail;
        }

        var visible = VisibleIndices(items, filterText, filter);

        var flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.Resizable | ImGuiTableFlags.Sortable | ImGuiTableFlags.SortTristate | ImGuiTableFlags.NoSavedSettings;
        if (maxHeight > 0f) flags |= ImGuiTableFlags.ScrollY;

        if (!ImGui.BeginTable("t", columns.Length, flags, new Vector2(0f, maxHeight)))
        {
            ImGui.PopID();
            return;
        }

        foreach (var col in columns)
            ImGui.TableSetupColumn(col.Header, (col.Width > 0f ? ImGuiTableColumnFlags.WidthFixed : ImGuiTableColumnFlags.WidthStretch) | (col.SortKey is null ? ImGuiTableColumnFlags.NoSort : ImGuiTableColumnFlags.None), col.Width);
        if (maxHeight > 0f) ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableHeadersRow();

        var specs = ImGui.TableGetSortSpecs();
        if (!specs.IsNull && specs.SpecsCount > 0)
        {
            var s = specs.Specs;
            SortIndices(visible, items, columns[s.ColumnIndex].SortKey, s.SortDirection == ImGuiSortDirection.Ascending);
        }

        if (maxHeight > 0f)
        {
            var clipper = ImGui.ImGuiListClipper();
            clipper.Begin(visible.Count);
            while (clipper.Step())
                for (var k = clipper.DisplayStart; k < clipper.DisplayEnd; k++)
                    DrawRow(items, columns, visible[k]);
            clipper.End();
            clipper.Destroy();
        }
        else
            foreach (var i in visible) DrawRow(items, columns, i);

        ImGui.EndTable();
        ImGui.PopID();
    }

    static void DrawRow<T>(IList<T> items, TableColumn<T>[] columns, int i)
    {
        ImGui.PushID(i);
        ImGui.TableNextRow();
        for (var c = 0; c < columns.Length; c++)
        {
            ImGui.TableNextColumn();
            ImGui.PushID(c);
            columns[c].Draw?.Invoke(items[i], i);
            ImGui.PopID();
        }
        ImGui.PopID();
    }

    internal static void SortIndices<T>(List<int>? idx, IList<T>? items, Func<T, IComparable?>? key, bool asc)
    {
        if (idx is null || items is null || key is null) return;
        idx.Sort((a, b) =>
        {
            var ka = key(items[a]);
            var kb = key(items[b]);
            var c = ka is null ? (kb is null ? 0 : -1) : kb is null ? 1 : ka.CompareTo(kb);
            return c != 0 ? (asc ? c : -c) : a.CompareTo(b);
        });
    }

    internal static List<int> VisibleIndices<T>(IList<T>? items, Func<T, string?>? label, string? filter)
    {
        if (items is null) return [];
        var all = Enumerable.Range(0, items.Count);
        return label is null ? [.. all] : [.. all.Where(i => Text.Matches(filter ?? "", label(items[i]) ?? ""))];
    }
}
