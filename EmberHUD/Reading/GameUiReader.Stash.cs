using System.Numerics;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed partial class GameUiReader
{
    private sealed record StashWidgets(nint Page, nint Grid, nint Scroll, nint Strip);

    private nint _stashPage;
    private int _stashMisses;
    private StashWidgets? _stashWidgets;
    private StashView? _stashShown;
    private long _stashReadAt = long.MinValue;
    private int _stashCellsTab = -1;
    private long _stashRetryAt = long.MinValue;

    public int? OpenStashTab => _stashShown?.Tab;

    public StashView? ReadStash()
    {
        if (_stashPage == 0)
        {
            _stashWidgets = null;
            _stashShown = null;
            _stashReadAt = long.MinValue;
            _stashCellsTab = -1;
            _stashRetryAt = long.MinValue;
            return null;
        }

        var now = _clock();
        if (now < _stashRetryAt) return _stashShown = null;
        var widgets = _stashWidgets;
        if (widgets is null || widgets.Page != _stashPage)
            _stashWidgets = widgets = StashWidgetsOf(_stashPage);
        if (widgets is null)
        {
            _stashRetryAt = now + Offsets.RectChildRetryMilliseconds;
            _stashCellsTab = -1;
            return _stashShown = null;
        }

        if (ReadOpenStashTab(widgets.Strip) is not { } tab)
        {
            _stashReadAt = long.MinValue;
            _stashCellsTab = -1;
            return _stashShown = null;
        }
        if (tab == _stashCellsTab && now < _stashReadAt) return _stashShown;
        _stashReadAt = now + Offsets.StashRefreshMilliseconds;
        _stashCellsTab = tab;
        return _stashShown = TryOverlay(out _, out var origin) ? ReadStashCells(widgets, tab, origin) : null;
    }

    private StashWidgets? StashWidgetsOf(nint page)
    {
        var grid = FindChild(page, Offsets.StashGrid);
        var scroll = FindChild(page, Offsets.StashScroll);
        var strip = FindChild(page, Offsets.StashTabStrip);
        return grid == 0 || scroll == 0 || strip == 0 || !IsWidgetOf(grid, Offsets.StashGridClassName) ? null : new StashWidgets(page, grid, scroll, strip);
    }

    private bool IsWidgetOf(nint widget, string className) =>
        memory.TryReadPtr(widget + Offsets.UObjectClass, out var cls) && cls != 0 && IsA(cls, className);

    private int? ReadOpenStashTab(nint strip)
    {
        if (!TryPointers(strip + Offsets.PanelSlots, out var slots) || slots.Length < 2) return null;
        var open = -1;
        for (var i = 0; i < slots.Length; i++)
        {
            if (slots[i] == 0 || !memory.TryReadPtr(slots[i] + Offsets.PanelSlotContent, out var title) || title == 0) return null;
            if (i == slots.Length - 1) break;
            if (!IsWidgetOf(title, Offsets.StashTabClassName) ||
                !memory.TryReadPtr(title + Offsets.TitleSelectIcon, out var icon) || icon == 0 ||
                !memory.TryRead(icon + Offsets.UWidgetVisibility, out byte visibility))
                return null;
            if (visibility != 4) continue;
            if (open >= 0) return null;
            open = i;
        }
        return open < 0 ? null : open;
    }

    private StashView? ReadStashCells(StashWidgets widgets, int tab, Vector2 origin)
    {
        if (Geometry(widgets.Scroll, origin) is not { } clip || !TryPointers(widgets.Grid + Offsets.PanelSlots, out var slots)) return null;

        var rows = Offsets.StashCells / Offsets.StashColumns;
        HashSet<int> seen = [], ambiguous = [];
        List<BagCell> cells = [];
        foreach (var slot in slots)
        {
            if (!memory.TryReadPtr(slot + Offsets.PanelSlotContent, out var cell) ||
                !memory.TryRead(slot + Offsets.GridSlotRow, out int row) || row < 0 || row >= rows ||
                !memory.TryRead(slot + Offsets.GridSlotColumn, out int column) || column < 0 || column >= Offsets.StashColumns)
                continue;
            var number = row * Offsets.StashColumns + column;
            if (!seen.Add(number)) ambiguous.Add(number);
            if (Geometry(cell, origin) is { } rect && rect.Max.X > clip.Min.X && rect.Min.X < clip.Max.X && rect.Max.Y > clip.Min.Y && rect.Min.Y < clip.Max.Y)
                cells.Add(new BagCell(number, rect.Min, rect.Max));
        }
        cells.RemoveAll(cell => ambiguous.Contains(cell.Slot));
        return new StashView(tab, clip.Min, clip.Max, [.. cells]);
    }
}
