using System.Numerics;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed partial class GameUiReader
{
    private sealed record BoothWidgets(nint Page, nint Switcher, nint[] Tiles);

    private nint _boothPage;
    private int _boothMisses;
    private BoothWidgets? _boothWidgets;
    private BagView? _boothShown;
    private long _boothReadAt = long.MinValue;

    public BagView? ReadBooth()
    {
        if (_boothPage == 0)
        {
            _boothWidgets = null;
            _boothShown = null;
            _boothReadAt = long.MinValue;
            return null;
        }

        var now = _clock();
        if (now < _boothReadAt) return _boothShown;
        _boothReadAt = now + Offsets.BagRefreshMilliseconds;

        var widgets = _boothWidgets;
        if (widgets is null || widgets.Page != _boothPage)
            _boothWidgets = widgets = BoothWidgetsOf(_boothPage);
        if (widgets is null)
        {
            _boothReadAt = now + Offsets.RectChildRetryMilliseconds;
            return _boothShown = null;
        }
        return _boothShown = TryOverlay(out _, out var origin) ? ReadOpenBoothTile(widgets, origin) : null;
    }

    private BoothWidgets? BoothWidgetsOf(nint page)
    {
        var switcher = FindChild(page, Offsets.BoothSwitcher);
        if (switcher == 0) return null;
        var tiles = new nint[Offsets.InventoryTabCount];
        for (var i = 0; i < tiles.Length; i++)
            if ((tiles[i] = FindChild(page, Offsets.BoothTilePrefix + (Offsets.InventoryTabFirst + i))) == 0) return null;
        return new BoothWidgets(page, switcher, tiles);
    }

    private BagView? ReadOpenBoothTile(BoothWidgets widgets, Vector2 origin)
    {
        if (!memory.TryRead(widgets.Switcher + Offsets.SwitcherActiveIndex, out int active) || active != Offsets.BoothSwitcherIndex) return null;
        var open = Array.FindLastIndex(widgets.Tiles, Visible);
        if (open < 0) return null;

        var tile = widgets.Tiles[open];
        if (!TryPointers(tile + Offsets.ListViewItems, out var items, Offsets.MaxBoothItems) ||
            Geometry(tile, origin) is not { } clip ||
            !TryPointers(tile + Offsets.ListViewEntryPool, out var entries, Offsets.MaxBoothItems))
            return null;

        List<BagCell> cells = [];
        HashSet<int> seen = [], ambiguous = [];
        foreach (var entry in entries)
        {
            if (!memory.TryReadPtr(entry + Offsets.BoothEntryItemData, out var item) || item == 0 || Geometry(entry, origin) is not { } rect) continue;
            var index = Array.IndexOf(items, item);
            if (index < 0) continue;
            if (!seen.Add(index)) ambiguous.Add(index);
            if (rect.Max.X > clip.Min.X && rect.Min.X < clip.Max.X && rect.Max.Y > clip.Min.Y && rect.Min.Y < clip.Max.Y)
                cells.Add(new BagCell(index, rect.Min, rect.Max));
        }
        cells.RemoveAll(cell => ambiguous.Contains(cell.Slot));
        return new BagView((InventoryBag)open, clip.Min, clip.Max, [.. cells]);
    }
}
