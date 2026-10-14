using System.Numerics;
using Hexa.NET.ImGui;

namespace EmberHUD.Ui;

public sealed class TableColumn<T>
{
    public string Header = "";
    public float Width;
    public Func<T, IComparable?>? SortKey;
    public Action<T, int>? Draw;
}

public static class SortableTable
{
    sealed class Cache
    {
        public object? Items;
        public string Filter = "";
        public List<int>? Visible;
        public List<int>? Sorted;
        public int Column = -1;
        public bool Ascending;
        public int Count;
        public int Frame;
    }

    static readonly Dictionary<uint, Cache> Caches = [];

    public static void Draw<T>(string id, IList<T>? items, TableColumn<T>[]? columns, ref string filter, Func<T, string?>? filterText = null, float maxHeight = 0f)
    {
        if (items is null || columns is not { Length: > 0 }) return;

        ImGui.PushID(id);
        if (filterText is not null)
        {
            Controls.Filter(ref filter);
        }

        if (maxHeight > 0f)
        {
            var avail = ImGui.GetContentRegionAvail().Y;
            if (avail > 0f && avail < maxHeight) maxHeight = avail;
        }

        var frame = ImGui.GetFrameCount();
        var key = ImGui.GetID("t");
        if (!Caches.TryGetValue(key, out var cache)) Caches[key] = cache = new Cache();
        cache.Frame = frame;
        if (Caches.Count > 8) foreach (var stale in Caches.Where(pair => pair.Value.Frame < frame - 1).Select(pair => pair.Key).ToList()) Caches.Remove(stale);
        if (!ReferenceEquals(cache.Items, items) || cache.Filter != filter || items.Count < cache.Count)
        {
            cache.Items = items;
            cache.Count = items.Count;
            cache.Filter = filter;
            cache.Visible = filterText is null || string.IsNullOrWhiteSpace(filter) ? null : VisibleIndices(items, filterText, filter);
            cache.Sorted = null;
        }
        var visible = cache.Visible;

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
            var ascending = s.SortDirection == ImGuiSortDirection.Ascending;
            if (cache.Sorted is null || cache.Column != s.ColumnIndex || cache.Ascending != ascending)
            {
                cache.Column = s.ColumnIndex;
                cache.Ascending = ascending;
                cache.Sorted = [.. (cache.Visible ?? Enumerable.Range(0, items.Count)).Where(i => i < items.Count)];
                SortIndices(cache.Sorted, items, columns[s.ColumnIndex].SortKey, ascending);
            }
            visible = cache.Sorted;
        }

        var count = visible?.Count ?? items.Count;
        if (maxHeight > 0f)
        {
            var clipper = ImGui.ImGuiListClipper();
            clipper.Begin(count);
            while (clipper.Step())
                for (var k = clipper.DisplayStart; k < clipper.DisplayEnd; k++)
                    DrawRow(items, columns, visible is null ? k : visible[k]);
            clipper.End();
            clipper.Destroy();
        }
        else
            for (var k = 0; k < count; k++) DrawRow(items, columns, visible is null ? k : visible[k]);

        ImGui.EndTable();
        ImGui.PopID();
    }

    static void DrawRow<T>(IList<T> items, TableColumn<T>[] columns, int i)
    {
        if (i >= items.Count) return;
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
        var keys = new IComparable?[items.Count];
        foreach (var i in idx) keys[i] = key(items[i]);
        idx.Sort((a, b) =>
        {
            var ka = keys[a];
            var kb = keys[b];
            var c = ka is null ? (kb is null ? 0 : -1) : kb is null ? 1 : ka is string sa && kb is string sb ? string.Compare(sa, sb, StringComparison.OrdinalIgnoreCase) : ka.CompareTo(kb);
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
