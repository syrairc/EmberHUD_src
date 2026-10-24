using System.Numerics;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed partial class GameUiReader
{
    private sealed record BagWidgets(nint Page, nint[] Lists);

    private nint _bagPage;
    private int _bagMisses;
    private BagWidgets? _bagWidgets;
    private BagView? _bagShown;
    private long _bagReadAt = long.MinValue;

    public BagView? ReadBag()
    {
        if (_bagPage == 0)
        {
            _bagWidgets = null;
            _bagShown = null;
            _bagReadAt = long.MinValue;
            return null;
        }

        var now = _clock();
        if (now < _bagReadAt) return _bagShown;
        _bagReadAt = now + Offsets.BagRefreshMilliseconds;

        var widgets = _bagWidgets;
        if (widgets is null || widgets.Page != _bagPage)
            _bagWidgets = widgets = BagWidgetsOf(_bagPage);
        if (widgets is null)
        {
            _bagReadAt = now + Offsets.RectChildRetryMilliseconds;
            return _bagShown = null;
        }
        return _bagShown = TryOverlay(out _, out var origin) ? ReadOpenBagTab(widgets, origin) : null;
    }

    private BagWidgets? BagWidgetsOf(nint page)
    {
        var grid = FindChild(page, Offsets.BagGrid);
        if (grid == 0) return null;
        var lists = new nint[Offsets.InventoryTabCount];
        for (var i = 0; i < lists.Length; i++)
            if ((lists[i] = FindChild(grid, Offsets.BagListPrefix + (Offsets.InventoryTabFirst + i))) == 0) return null;
        return new BagWidgets(page, lists);
    }

    private BagView? ReadOpenBagTab(BagWidgets widgets, Vector2 origin)
    {
        var open = -1;
        for (var i = 0; i < widgets.Lists.Length; i++)
        {
            if (!Visible(widgets.Lists[i])) continue;
            if (open >= 0) return null;
            open = i;
        }
        if (open < 0) return null;

        var list = widgets.Lists[open];
        if (!TryPointers(list + Offsets.ListViewItems, out var items, Offsets.MaxBagRows) ||
            Geometry(list, origin) is not { } clip ||
            !_slate.TryWeak(list + Offsets.UWidgetSlate, out var slate) ||
            !memory.TryRead(slate + Offsets.ListViewScrollOffset, out double offset) ||
            !(offset >= 0 && offset <= items.Length) ||
            !memory.TryRead(list + Offsets.ListViewEntrySpacing, out float spacing) || !(spacing >= 0) ||
            !TryPointers(list + Offsets.ListViewEntryPool, out var rows, Offsets.MaxBagRows))
            return null;

        var clipHeight = clip.Max.Y - clip.Min.Y;
        List<BagCell> cells = [];
        HashSet<int> seen = [], ambiguous = [];
        foreach (var row in rows)
        {
            if (!memory.TryReadPtr(row + Offsets.BagRowItemData, out var item) || item == 0 || Geometry(row, origin) is not { } rowRect) continue;
            var index = Array.IndexOf(items, item);
            if (index < 0) continue;
            if (!seen.Add(index)) ambiguous.Add(index);
            var pitch = rowRect.Max.Y - rowRect.Min.Y + spacing;
            if (!(pitch > 0) || index < Math.Floor(offset) || !(index <= offset + clipHeight / pitch) ||
                !memory.TryReadArray(row + Offsets.BagRowCells, Offsets.BagRowCellCount, out long[] rowCells))
                continue;
            for (var c = 0; c < rowCells.Length; c++)
                if (Geometry((nint)rowCells[c], origin) is { } rect && rect.Max.X > clip.Min.X && rect.Min.X < clip.Max.X && rect.Max.Y > clip.Min.Y && rect.Min.Y < clip.Max.Y)
                    cells.Add(new BagCell(index * Offsets.BagRowCellCount + c, rect.Min, rect.Max));
        }
        cells.RemoveAll(cell => ambiguous.Contains(cell.Slot / Offsets.BagRowCellCount));
        return new BagView((InventoryBag)open, clip.Min, clip.Max, [.. cells]);
    }

    private bool TryPointers(nint arrayAt, out nint[] pointers, int limit)
    {
        pointers = [];
        if (!memory.TryRead(arrayAt + Offsets.TArrayNum, out int count) || count < 0 || count > limit) return false;
        if (count == 0) return true;
        if (!memory.TryReadPtr(arrayAt, out var data) || !memory.TryReadArray(data, count, out long[] read)) return false;
        pointers = [.. read.Select(p => (nint)p)];
        return true;
    }
}
